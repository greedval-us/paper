using System;
using System.Collections.Generic;
using System.Linq;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Application
{
    public enum WorkshopComponentKind { None, Foundation, Weapon, Structure }

    /// <summary>Owns local recipes. Durable component selection publishes its parent only after the profile commit.</summary>
    public sealed class WorkshopDraftSession
    {
        readonly RunController owner;
        readonly AssemblyCommands commands;
        readonly AssemblyFitCatalog fits;
        AssemblyRecipe componentRecipe;
        List<ComponentDefinition> componentDefinitions;
        AlbumEntry componentPreview;
        string componentId;
        int componentRevision;
        Guid completedComponentOperation;
        AssemblyCommandResult completedComponentResult;
        IReadOnlyList<string> replacements = Array.Empty<string>();
        [Serializable] sealed class FitEnvelope { public AssemblyFitCatalogInput fits; }

        public WorkshopDraftSession(RunController owner, ComponentCatalog catalog = null, AssemblyFitCatalog fits = null)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Catalog = catalog ?? owner.ConfiguredAssemblyCatalog;
            if (fits == null)
            {
                var asset = Resources.Load<TextAsset>("Art/NotebookMounts");
                var input = asset == null ? null : JsonUtility.FromJson<FitEnvelope>(asset.text);
                fits = input?.fits == null ? null : new AssemblyFitCatalog(input.fits);
            }
            this.fits = fits;
            commands = new AssemblyCommands(owner, Catalog, fits);
        }

        public ComponentCatalog Catalog { get; }
        public AlbumEntry Draft { get; private set; }
        public AlbumEntry PreviewEntry => IsComponentOpen ? componentPreview : Draft;
        public AssemblyRecipe PreviewRecipe => IsComponentOpen ? componentRecipe : Draft?.Snapshot?.Recipe;
        public string Name { get; private set; }
        public WorkshopComponentKind ActiveComponent { get; private set; }
        public int ActiveMount { get; private set; } = -1;
        public bool IsComponentOpen => ActiveComponent != WorkshopComponentKind.None;
        public ComponentCategory ActiveCategory { get; private set; }
        public AssemblyCommandResult LastResult { get; private set; } = AssemblyCommandResult.Ok(null);
        public IReadOnlyList<string> ReplacedPartIds => replacements;
        public IReadOnlyList<ComponentCategory> Categories
        {
            get
            {
                switch (ActiveComponent)
                {
                    case WorkshopComponentKind.Foundation: return Array.AsReadOnly(new[] { ComponentCategory.Frame, ComponentCategory.Hull, ComponentCategory.Armor, ComponentCategory.Feed, ComponentCategory.Turn });
                    case WorkshopComponentKind.Weapon: return Array.AsReadOnly(new[] { ComponentCategory.Mechanism, ComponentCategory.Head, ComponentCategory.Sight, ComponentCategory.Magazine, ComponentCategory.Trigger });
                    case WorkshopComponentKind.Structure: return Array.AsReadOnly(new[] { ComponentCategory.Site, ComponentCategory.Function, ComponentCategory.Shell, ComponentCategory.Utility });
                    default: return Array.Empty<ComponentCategory>();
                }
            }
        }
        public IReadOnlyList<ComponentDefinition> CompatibleParts => Catalog == null || !IsComponentOpen
            ? Array.Empty<ComponentDefinition>()
            : Catalog.Definitions.Where(d => d.Category == ActiveCategory && Compatible(d, PreviewRecipe))
                .GroupBy(d => d.Id).Select(g => g.OrderByDescending(d => d.Revision).First()).ToList().AsReadOnly();
        public bool IsOwned(string id) => id != null && owner.Workshop != null && owner.Workshop.IsUnlocked(id);

        ComponentCatalog PreviewCatalog => PreviewRecipe == null ? Catalog :
            new ComponentCatalog(Draft.Snapshot.BalanceRevision, IsComponentOpen ? componentDefinitions : Draft.Snapshot.Definitions);
        public AssemblyResolution Resolution => Draft?.Kind == AlbumEntryKind.Legacy
            ? AssemblyResolver.ResolveEntry(Draft, owner.AssemblyBalance, MaterialTier.Pencil)
            : AssemblyResolver.Resolve(PreviewRecipe, PreviewCatalog, owner.AssemblyBalance, MaterialTier.Pencil, fits);
        public ResolvedEntry Resolved => Draft?.Kind == AlbumEntryKind.Legacy
            ? AssemblyResolver.ResolveEntry(Draft, owner.AssemblyBalance, MaterialTier.Pencil).Value
            : AssemblyResolver.Resolve(PreviewRecipe, PreviewCatalog, owner.AssemblyBalance, MaterialTier.Pencil).Value;
        public AssemblyPreviewMetrics PreviewMetrics => Draft?.Kind == AlbumEntryKind.Legacy ? null :
            AssemblyResolver.PreviewMetrics(PreviewRecipe, PreviewCatalog, owner.AssemblyBalance, MaterialTier.Pencil);
        public IReadOnlyList<AssemblyDiagnostic> Diagnostics
        {
            get
            {
                var issues = new List<AssemblyDiagnostic>();
                if (Draft == null) issues.Add(new AssemblyDiagnostic("profile.entry.missing", "draft"));
                else if (Draft.Kind == AlbumEntryKind.Legacy) issues.Add(new AssemblyDiagnostic("profile.entry.legacy_readonly", Draft.Id));
                else
                {
                    issues.AddRange(Resolution.Diagnostics);
                    foreach (var part in PreviewRecipe.Parts)
                        if (!IsOwned(part.Id)) issues.Add(new AssemblyDiagnostic("profile.part.locked", part.Id));
                }
                if (!ValidName) issues.Add(new AssemblyDiagnostic("assembly.entry.name", "name", ProfileLimits.MaximumBlueprintNameLength.ToString()));
                return issues.AsReadOnly();
            }
        }
        public string SuggestedPartId => Diagnostics.Concat(LastResult.Diagnostics)
            .Where(d => d.Code == "assembly.part.required").SelectMany(d => d.Arguments.Skip(1))
            .FirstOrDefault(id => id == "magazine.standard");
        bool ValidName => !string.IsNullOrWhiteSpace(Name) && Name.Length <= ProfileLimits.MaximumBlueprintNameLength;
        bool OwnedRecipe => PreviewRecipe != null && PreviewRecipe.Parts.All(p => IsOwned(p.Id));
        bool ValidParent => Draft != null && Draft.Kind != AlbumEntryKind.Legacy && ValidName && OwnedRecipe && Resolution.Valid;
        public bool CanSave => !IsComponentOpen && ValidParent && (owner.Workshop.Entries.Count < ProfileLimits.MaximumAlbumPages || owner.Workshop.Entries.Any(e => e.Id == Draft.Id));
        public bool CanTrial => !IsComponentOpen && ValidParent;
        public bool CanSaveComponent => IsComponentOpen && ValidParent;

        public void Begin(AlbumEntry source)
        {
            Draft = source ?? throw new ArgumentNullException(nameof(source));
            Name = source.Name; CloseComponent(); completedComponentOperation = Guid.Empty; completedComponentResult = null;
            LastResult = AssemblyCommandResult.Ok(source.Id);
        }

        public bool Rename(string name)
        {
            Name = name;
            LastResult = ValidName ? AssemblyCommandResult.Ok(Draft?.Id) : AssemblyCommandResult.Fail("assembly.entry.name", "name");
            return ValidName;
        }

        public void OpenFoundation() => Open(WorkshopComponentKind.Foundation, -1);
        public void OpenWeapon(int mount) => Open(WorkshopComponentKind.Weapon, mount);
        public void OpenStructure() => Open(WorkshopComponentKind.Structure, -1);
        void Open(WorkshopComponentKind kind, int mount)
        {
            if (Draft == null || Draft.Kind == AlbumEntryKind.Legacy ||
                (kind == WorkshopComponentKind.Structure ? Draft.Kind != AlbumEntryKind.Structure : Draft.Kind != AlbumEntryKind.Tower))
            { LastResult = AssemblyCommandResult.Fail("profile.entry.kind"); return; }
            if (kind == WorkshopComponentKind.Weapon && !Draft.Snapshot.Recipe.Tower.Mounts.Any(m => m.SlotIndex == mount))
            { LastResult = AssemblyCommandResult.Fail("profile.library.slot"); return; }
            ActiveComponent = kind; ActiveMount = mount; ActiveCategory = Categories[0];
            componentRecipe = Draft.Snapshot.Recipe; componentPreview = Draft;
            componentDefinitions = Draft.Snapshot.Definitions.ToList();
            var reference = Draft.LibraryReferences.FirstOrDefault(r => r.SlotIndex == mount &&
                (kind == WorkshopComponentKind.Foundation ? r.Kind == ComponentLibraryKind.Foundation : r.Kind == ComponentLibraryKind.Weapon));
            var existing = reference == null ? null : owner.Workshop.Library.FirstOrDefault(c => c.Id == reference.Id && c.Revision == reference.Revision);
            componentId = existing?.Id ?? Guid.NewGuid().ToString("N"); componentRevision = existing?.Revision ?? 1;
            completedComponentOperation = Guid.Empty; completedComponentResult = null; replacements = Array.Empty<string>();
            LastResult = AssemblyCommandResult.Ok(Draft.Id);
        }

        public void SelectCategory(ComponentCategory category)
        {
            if (Categories.Contains(category)) ActiveCategory = category;
        }

        public bool SelectPart(string id)
        {
            replacements = Array.Empty<string>();
            if (!IsComponentOpen) return Reject("assembly.component.closed");
            var selected = Catalog?.Definitions.Where(d => d.Id == id).OrderByDescending(d => d.Revision).FirstOrDefault();
            if (selected == null) return Reject("assembly.part.missing", id);
            if (!Categories.Contains(selected.Category) || !Compatible(selected, componentRecipe)) return Reject("assembly.part.incompatible", id);
            if (!IsOwned(id)) return Reject("profile.part.locked", id);
            var selectedDefinitions = new List<ComponentDefinition> { selected };
            AssemblyRecipe next;
            if (ActiveComponent == WorkshopComponentKind.Foundation)
            {
                var old = componentRecipe.Tower.Foundation;
                var parts = old.Parts.Select(p => Category(p) == selected.Category ? Ref(selected) : p).ToArray();
                next = new AssemblyRecipe(new TowerRecipe(new FoundationRecipe(parts[0], parts[1], parts[2], parts[3], parts[4]), componentRecipe.Tower.Mounts));
            }
            else if (ActiveComponent == WorkshopComponentKind.Structure)
            {
                var old = componentRecipe.Structure;
                var parts = old.Parts.Select(p => Category(p) == selected.Category ? Ref(selected) : p).ToArray();
                next = new AssemblyRecipe(new StructureRecipe(old.Family, parts[0], parts[1], parts[2], parts[3]));
            }
            else
            {
                var old = componentRecipe.Tower.Mounts.Single(m => m.SlotIndex == ActiveMount).Weapon;
                var parts = old.Parts.Select(p => Category(p) == selected.Category ? Ref(selected) : p).ToArray();
                var changed = new List<string>();
                if (selected.Category == ComponentCategory.Mechanism)
                {
                    for (int i = 1; i < parts.Length; i++)
                    {
                        var definition = Definition(parts[i]);
                        if ((definition.WeaponFamilies & selected.WeaponFamilies) != 0) continue;
                        var replacement = Catalog.Definitions.Where(d => d.Category == definition.Category &&
                            (d.WeaponFamilies & selected.WeaponFamilies) != 0 && IsOwned(d.Id))
                            .OrderBy(d => d.Get(AssemblyStat.Cost, ModifierOperation.Add)).ThenBy(d => d.Id, StringComparer.Ordinal).FirstOrDefault();
                        if (replacement == null) return Reject("assembly.part.incompatible", selected.Id);
                        parts[i] = Ref(replacement); selectedDefinitions.Add(replacement); changed.Add(replacement.Id);
                    }
                }
                var weapon = new WeaponRecipe(parts[0], parts[1], parts[2], parts[3], parts[4]);
                next = new AssemblyRecipe(new TowerRecipe(componentRecipe.Tower.Foundation,
                    componentRecipe.Tower.Mounts.Select(m => m.SlotIndex == ActiveMount ? new WeaponMountRecipe(ActiveMount, weapon) : m)));
                replacements = changed.AsReadOnly();
            }
            componentRecipe = next; componentDefinitions = MergeDefinitions(componentDefinitions, selectedDefinitions).ToList();
            ActiveCategory = selected.Category;
            // A required-part conflict remains editable even though AlbumEntry cannot own an invalid snapshot.
            var issues = PreviewCatalog.ValidateRecipe(next);
            if (issues.Count == 0) componentPreview = Entry(next, componentDefinitions, Draft.LibraryReferences);
            LastResult = AssemblyCommandResult.Ok(id); return true;
        }

        bool Compatible(ComponentDefinition definition, AssemblyRecipe recipe)
        {
            if (recipe?.Structure != null) return (definition.StructureFamilies & recipe.Structure.Family) != 0;
            if (definition.Category <= ComponentCategory.Turn || definition.Category == ComponentCategory.Mechanism) return true;
            var weapon = recipe?.Tower?.Mounts.FirstOrDefault(m => m.SlotIndex == ActiveMount)?.Weapon;
            if (weapon == null) return false;
            var core = Definition(weapon.Mechanism);
            return (definition.WeaponFamilies & core.WeaponFamilies) != 0;
        }
        ComponentDefinition Definition(PartRef part)
        {
            var definition = componentDefinitions?.FirstOrDefault(d => d.Id == part.Id && d.Revision == part.Revision)
                ?? Draft?.Snapshot?.Definitions.FirstOrDefault(d => d.Id == part.Id && d.Revision == part.Revision);
            if (definition == null) Catalog?.TryGet(part.Id, part.Revision, out definition);
            return definition ?? throw AssemblyCatalogException.At("assembly.part.missing", part.Id);
        }
        ComponentCategory Category(PartRef part) => Definition(part).Category;
        static PartRef Ref(ComponentDefinition definition) => new PartRef(definition.Id, definition.Revision);

        public void CancelComponent() { CloseComponent(); LastResult = AssemblyCommandResult.Ok(Draft?.Id); }
        void CloseComponent()
        {
            ActiveComponent = WorkshopComponentKind.None; ActiveMount = -1; componentRecipe = null;
            componentDefinitions = null; componentPreview = null; replacements = Array.Empty<string>();
        }

        public AssemblyCommandResult SaveComponent(Guid operationId)
        {
            if (operationId != Guid.Empty && operationId == completedComponentOperation) return completedComponentResult;
            if (operationId == Guid.Empty) return LastResult = AssemblyCommandResult.Fail("profile.operation.invalid");
            if (!CanSaveComponent) return LastResult = InvalidCandidate();
            if (ActiveComponent == WorkshopComponentKind.Structure)
            {
                Draft = Entry(componentRecipe, componentDefinitions, Draft.LibraryReferences);
                completedComponentResult = AssemblyCommandResult.Ok(Draft.Id); completedComponentOperation = operationId;
                CloseComponent(); return LastResult = completedComponentResult;
            }
            var kind = ActiveComponent == WorkshopComponentKind.Foundation ? ComponentLibraryKind.Foundation : ComponentLibraryKind.Weapon;
            var foundation = componentRecipe.Tower.Foundation;
            var weapon = componentRecipe.Tower.Mounts.SingleOrDefault(m => m.SlotIndex == ActiveMount)?.Weapon;
            var parts = kind == ComponentLibraryKind.Foundation ? foundation.Parts : weapon.Parts;
            var definitions = SelectedDefinitions(parts, componentDefinitions);
            var snapshot = kind == ComponentLibraryKind.Foundation
                ? new ComponentRecipeSnapshot(foundation, Catalog.BalanceRevision, definitions)
                : new ComponentRecipeSnapshot(weapon, Catalog.BalanceRevision, definitions);
            var libraryName = Name.Length <= 39 ? Name + (kind == ComponentLibraryKind.Foundation ? " · основа" : " · оружие") : Name;
            var component = new ComponentLibraryEntry(componentId, libraryName, componentRevision, snapshot);
            var result = commands.SaveLibraryEntry(component, operationId);
            LastResult = result;
            if (!result.Success) return result;
            var saved = owner.Workshop.Library.Single(c => c.Id == result.ResultId);
            var references = Draft.LibraryReferences.Where(r => r.SlotIndex != ActiveMount).ToList();
            references.Add(new ComponentLibraryReference(saved.Id, saved.Revision, saved.Kind, ActiveMount));
            var canonical = ReplaceComponent(componentRecipe, saved.Snapshot.Foundation, saved.Snapshot.Weapon, ActiveMount);
            Draft = Entry(canonical, MergeDefinitions(componentDefinitions, saved.Snapshot.Definitions), references);
            completedComponentResult = result; completedComponentOperation = operationId; CloseComponent();
            return result;
        }

        public AssemblyCommandResult SaveEntry(Guid operationId, bool variant = false)
        {
            if (IsComponentOpen) return LastResult = AssemblyCommandResult.Fail("assembly.component.open");
            if (!ValidParent) return LastResult = InvalidCandidate();
            var input = new AlbumEntry(Draft.Id, Name, Draft.Revision, Draft.Snapshot, Draft.LibraryReferences);
            var result = variant ? commands.SaveEntryAsVariant(input, operationId) : commands.SaveEntry(input, operationId);
            LastResult = result;
            if (result.Success) { Draft = owner.Workshop.Entries.Single(e => e.Id == result.ResultId); Name = Draft.Name; }
            return result;
        }
        public AssemblyCommandResult PurchasePart(string id, Guid operationId) => LastResult = commands.PurchasePart(id, operationId);
        public TrialReport RunTrial(TrialScenario scenario = TrialScenario.Crowd, int budget = 400, int seed = 1337)
        {
            if (!CanTrial || !Enum.IsDefined(typeof(TrialScenario), scenario)) return null;
            var selected = PreviewEntry.Kind == AlbumEntryKind.Structure
                ? AssemblyTrialRunner.ScenariosFor(PreviewEntry)[0] : (AssemblyTrialScenario)scenario;
            return owner.RunAssemblyTrial(PreviewEntry, selected, budget, seed)?.Recording;
        }

        public bool ApplyLibrary(ComponentLibraryEntry component, int slotIndex)
        {
            if (component == null || IsComponentOpen || Draft?.Kind != AlbumEntryKind.Tower) return Reject("profile.library.slot");
            if (component.Kind == ComponentLibraryKind.Foundation ? slotIndex != -1 : slotIndex < 0 || slotIndex > 2) return Reject("profile.library.slot");
            var recipe = ReplaceComponent(Draft.Snapshot.Recipe, component.Snapshot.Foundation, component.Snapshot.Weapon, slotIndex);
            var references = Draft.LibraryReferences.Where(r => r.SlotIndex != slotIndex).ToList();
            references.Add(new ComponentLibraryReference(component.Id, component.Revision, component.Kind, slotIndex));
            return ApplyLocal(recipe, component.Snapshot.Definitions, references);
        }
        public bool ApplyPreset(AlbumEntry source, int slotIndex = -1)
        {
            if (source?.Kind != AlbumEntryKind.Tower || IsComponentOpen || Draft?.Kind != AlbumEntryKind.Tower || slotIndex < -1 || slotIndex > 2)
                return Reject("profile.library.slot");
            var recipe = slotIndex == -1
                ? ReplaceComponent(Draft.Snapshot.Recipe, source.Snapshot.Recipe.Tower.Foundation, null, -1)
                : ReplaceComponent(Draft.Snapshot.Recipe, null, source.Snapshot.Recipe.Tower.Mounts[0].Weapon, slotIndex);
            var parts = slotIndex == -1 ? source.Snapshot.Recipe.Tower.Foundation.Parts : source.Snapshot.Recipe.Tower.Mounts[0].Weapon.Parts;
            return ApplyLocal(recipe, SelectedDefinitions(parts, source.Snapshot.Definitions), Draft.LibraryReferences.Where(r => r.SlotIndex != slotIndex));
        }
        public bool RemoveWeapon(int mount)
        {
            if (IsComponentOpen || Draft?.Kind != AlbumEntryKind.Tower || mount <= 0 || !Draft.Snapshot.Recipe.Tower.Mounts.Any(m => m.SlotIndex == mount))
                return Reject("assembly.recipe.first_mount");
            var tower = Draft.Snapshot.Recipe.Tower;
            return ApplyLocal(new AssemblyRecipe(new TowerRecipe(tower.Foundation, tower.Mounts.Where(m => m.SlotIndex != mount))),
                Array.Empty<ComponentDefinition>(), Draft.LibraryReferences.Where(r => r.SlotIndex != mount));
        }
        bool ApplyLocal(AssemblyRecipe recipe, IEnumerable<ComponentDefinition> definitions, IEnumerable<ComponentLibraryReference> references)
        {
            var merged = MergeDefinitions(Draft.Snapshot.Definitions, definitions).ToList();
            foreach (var part in recipe.Parts) if (!IsOwned(part.Id)) return Reject("profile.part.locked", part.Id);
            try { var candidate = Entry(recipe, merged, references); Draft = candidate; LastResult = AssemblyCommandResult.Ok(candidate.Id); return true; }
            catch (AssemblyCatalogException error) { LastResult = new AssemblyCommandResult(false, error.Diagnostics[0].Code, diagnostics: error.Diagnostics); return false; }
        }
        static AssemblyRecipe ReplaceComponent(AssemblyRecipe source, FoundationRecipe foundation, WeaponRecipe weapon, int mount)
        {
            var mounts = source.Tower.Mounts.ToList();
            if (weapon != null)
            {
                mounts.RemoveAll(m => m.SlotIndex == mount); mounts.Add(new WeaponMountRecipe(mount, weapon));
            }
            return new AssemblyRecipe(new TowerRecipe(foundation ?? source.Tower.Foundation, mounts));
        }
        AlbumEntry Entry(AssemblyRecipe recipe, IEnumerable<ComponentDefinition> definitions, IEnumerable<ComponentLibraryReference> references)
            => new AlbumEntry(Draft.Id, Draft.Name, Draft.Revision,
                new RecipeSnapshot(recipe, Draft.Snapshot.BalanceRevision, SelectedDefinitions(recipe.Parts, definitions)), references);
        static IEnumerable<ComponentDefinition> MergeDefinitions(IEnumerable<ComponentDefinition> previous, IEnumerable<ComponentDefinition> selected)
            => previous.Concat(selected).GroupBy(d => (d.Id, d.Revision)).Select(g => g.Last());
        static IEnumerable<ComponentDefinition> SelectedDefinitions(IEnumerable<PartRef> parts, IEnumerable<ComponentDefinition> definitions)
        {
            var selected = new HashSet<(string, int)>(parts.Select(p => (p.Id, p.Revision)));
            return definitions.Where(d => selected.Contains((d.Id, d.Revision))).GroupBy(d => (d.Id, d.Revision)).Select(g => g.Last()).ToArray();
        }
        bool Reject(string code, string id = null) { LastResult = AssemblyCommandResult.Fail(code, id ?? "draft"); return false; }
        AssemblyCommandResult InvalidCandidate()
        {
            var issues = Diagnostics;
            return issues.Count == 0 ? AssemblyCommandResult.Fail("assembly.invalid") : new AssemblyCommandResult(false, issues[0].Code, diagnostics: issues);
        }
    }
}
