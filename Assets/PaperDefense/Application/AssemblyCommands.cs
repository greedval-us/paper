using System;
using System.Collections.Generic;
using System.Linq;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Application
{
    /// <summary>One candidate, one durable receipt and one publication after the existing persistence gate.</summary>
    public sealed class AssemblyCommands
    {
        readonly RunController owner;
        readonly ComponentCatalog catalog;
        readonly AssemblyFitCatalog fits;
        [Serializable] sealed class FitEnvelope { public AssemblyFitCatalogInput fits; }

        public AssemblyCommands(RunController owner, ComponentCatalog catalog = null, AssemblyFitCatalog fits = null)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.catalog = catalog ?? owner.ConfiguredAssemblyCatalog;
            if (fits != null) this.fits = fits;
            else
            {
                var asset = Resources.Load<TextAsset>("Art/NotebookMounts");
                var data = asset == null ? null : JsonUtility.FromJson<FitEnvelope>(asset.text);
                this.fits = data?.fits == null ? null : new AssemblyFitCatalog(data.fits);
            }
        }

        public AssemblyCommandResult PurchasePart(string partId, Guid operationId)
            => Execute("PurchasePart", operationId, candidate => {
                var part = catalog?.Definitions.Where(p => p.Id == partId).OrderByDescending(p => p.Revision).FirstOrDefault();
                return candidate.TryPurchaseAssemblyPart(part, out var code) ? AssemblyCommandResult.Ok(part.Id) : AssemblyCommandResult.Fail(code, partId);
            });

        public AssemblyCommandResult SaveEntry(AlbumEntry entry, Guid operationId) => Save(entry, operationId, false);
        public AssemblyCommandResult SaveEntryAsVariant(AlbumEntry entry, Guid operationId) => Save(entry, operationId, true);
        AssemblyCommandResult Save(AlbumEntry entry, Guid operationId, bool variant)
            => Execute(variant ? "SaveEntryAsVariant" : "SaveEntry", operationId, candidate => Save(candidate, entry, operationId, variant));

        AssemblyCommandResult Save(WorkshopProfile candidate, AlbumEntry entry, Guid operationId, bool variant)
        {
            if (entry == null) return AssemblyCommandResult.Fail("profile.entry.missing");
            if (entry.Kind == AlbumEntryKind.Legacy) return AssemblyCommandResult.Fail("profile.entry.legacy_readonly", entry.Id);
            var valid = Prepare(candidate, entry.Snapshot.Recipe, out var snapshot);
            if (!valid.Success) return valid;
            var canonical = new AlbumEntry(entry.Id, entry.Name, entry.Revision, snapshot, entry.LibraryReferences);
            return candidate.TrySaveAssemblyEntry(canonical, variant, operationId, out var saved, out var code)
                ? AssemblyCommandResult.Ok(saved.Id) : AssemblyCommandResult.Fail(code, entry.Id);
        }

        public AssemblyCommandResult SaveLibraryEntry(ComponentLibraryEntry entry, Guid operationId)
            => SaveLibrary(entry, operationId, false);
        public AssemblyCommandResult SaveLibraryEntryAsVariant(ComponentLibraryEntry entry, Guid operationId)
            => SaveLibrary(entry, operationId, true);
        AssemblyCommandResult SaveLibrary(ComponentLibraryEntry entry, Guid operationId, bool variant)
            => Execute(variant ? "SaveLibraryEntryAsVariant" : "SaveLibraryEntry", operationId, candidate => {
                if (entry == null) return AssemblyCommandResult.Fail("profile.library.missing");
                var foundation = entry.Snapshot.Foundation ?? DefaultFoundation();
                var weapon = entry.Snapshot.Weapon ?? DefaultWeapon();
                var recipe = new AssemblyRecipe(new TowerRecipe(foundation, new[] { new WeaponMountRecipe(0, weapon) }));
                var valid = Prepare(candidate, recipe, out _);
                if (!valid.Success) return valid;
                var parts = entry.Kind == ComponentLibraryKind.Foundation ? entry.Snapshot.Foundation.Parts : entry.Snapshot.Weapon.Parts;
                var definitions = Definitions(parts);
                var snapshot = entry.Kind == ComponentLibraryKind.Foundation
                    ? new ComponentRecipeSnapshot(entry.Snapshot.Foundation, catalog.BalanceRevision, definitions)
                    : new ComponentRecipeSnapshot(entry.Snapshot.Weapon, catalog.BalanceRevision, definitions);
                var canonical = new ComponentLibraryEntry(entry.Id, entry.Name, entry.Revision, snapshot);
                return candidate.TrySaveLibraryComponent(canonical, variant, operationId, out var saved, out var code)
                    ? AssemblyCommandResult.Ok(saved.Id) : AssemblyCommandResult.Fail(code, entry.Id);
            });

        public AssemblyCommandResult DeleteEntry(string entryId, Guid operationId)
            => Execute("DeleteEntry", operationId, candidate => candidate.TryDeleteAssemblyEntry(entryId, out var code)
                ? AssemblyCommandResult.Ok(entryId) : AssemblyCommandResult.Fail(code, entryId));
        public AssemblyCommandResult DeleteLibraryEntry(string entryId, Guid operationId)
            => Execute("DeleteLibraryEntry", operationId, candidate => candidate.TryDeleteLibraryComponent(entryId, out var code)
                ? AssemblyCommandResult.Ok(entryId) : AssemblyCommandResult.Fail(code, entryId));
        public AssemblyCommandResult AssignSlot(int slotIndex, string entryId, Guid operationId)
            => Execute("AssignSlot", operationId, candidate => {
                if (entryId != null)
                {
                    var entry = candidate.Entries.FirstOrDefault(e => e.Id == entryId);
                    var valid = ValidateOwned(candidate, entry);
                    if (!valid.Success) return valid;
                }
                return candidate.TryAssignAssemblySlot(slotIndex, entryId, out var code)
                    ? AssemblyCommandResult.Ok(entryId) : AssemblyCommandResult.Fail(code, "loadout");
            });

        public AssemblyCommandResult ApplyLibraryEntry(string entryId, string libraryId, int slotIndex, Guid operationId)
            => Execute("ApplyLibraryEntry", operationId, candidate => {
                var entry = candidate.Entries.FirstOrDefault(e => e.Id == entryId);
                var component = candidate.Library.FirstOrDefault(e => e.Id == libraryId);
                if (entry == null) return AssemblyCommandResult.Fail("profile.entry.missing", entryId);
                if (entry.Kind != AlbumEntryKind.Tower) return AssemblyCommandResult.Fail("profile.entry.kind", entryId);
                if (component == null) return AssemblyCommandResult.Fail("profile.library.missing", libraryId);
                var tower = entry.Snapshot.Recipe.Tower;
                var foundation = tower.Foundation;
                var mounts = tower.Mounts.ToList();
                if (component.Kind == ComponentLibraryKind.Foundation && slotIndex == -1) foundation = component.Snapshot.Foundation;
                else if (component.Kind == ComponentLibraryKind.Weapon && mounts.Any(m => m.SlotIndex == slotIndex))
                    mounts = mounts.Select(m => m.SlotIndex == slotIndex ? new WeaponMountRecipe(slotIndex, component.Snapshot.Weapon) : m).ToList();
                else return AssemblyCommandResult.Fail("profile.library.slot", entryId);
                var references = entry.LibraryReferences.Where(r => r.SlotIndex != slotIndex).ToList();
                references.Add(new ComponentLibraryReference(component.Id, component.Revision, component.Kind, slotIndex));
                var recipe = new AssemblyRecipe(new TowerRecipe(foundation, mounts));
                // Prepare uses the authoritative catalog; edited library revisions never update pages implicitly.
                var valid = Prepare(candidate, recipe, out var snapshot);
                if (!valid.Success) return valid;
                return Save(candidate, new AlbumEntry(entry.Id, entry.Name, entry.Revision, snapshot, references), operationId, false);
            });

        AssemblyCommandResult Execute(string type, Guid operationId, Func<WorkshopProfile, AssemblyCommandResult> mutate)
        {
            if (operationId == Guid.Empty) return AssemblyCommandResult.Fail("profile.operation.invalid");
            var receipt = owner.AssemblyProfile?.FindOperation(operationId);
            if (receipt != null) return receipt.Type == type ? AssemblyCommandResult.Ok(receipt.ResultId) : AssemblyCommandResult.Fail("profile.operation.conflict");
            if (!owner.CreateAssemblyCandidate(out var candidate, out var reason)) return AssemblyCommandResult.Fail("profile.edit.unavailable", "profile", reason);
            AssemblyCommandResult result;
            try
            {
                result = mutate(candidate);
                if (!result.Success) return result;
                candidate.RecordOperation(operationId, type, result.ResultId);
            }
            catch (AssemblyCatalogException error) { return new AssemblyCommandResult(false, error.Diagnostics.FirstOrDefault()?.Code ?? "assembly.invalid", diagnostics: error.Diagnostics); }
            catch (ArgumentException error) { return AssemblyCommandResult.Fail("assembly.invalid", "entry", error.Message); }
            catch (NotSupportedException error) { return AssemblyCommandResult.Fail("assembly.unsupported", "entry", error.Message); }
            // Observer failures after publication must not masquerade as a rejected transaction.
            return owner.CommitAssemblyCandidate(candidate, out reason) ? result : AssemblyCommandResult.Fail("profile.write_failed", "profile", reason);
        }

        AssemblyCommandResult Prepare(WorkshopProfile candidate, AssemblyRecipe recipe, out RecipeSnapshot snapshot)
        {
            snapshot = null;
            if (catalog == null) return AssemblyCommandResult.Fail("assembly.catalog.missing");
            var issues = catalog.ValidateRecipe(recipe);
            if (issues.Count != 0) return new AssemblyCommandResult(false, issues[0].Code, diagnostics: issues);
            foreach (var part in recipe.Parts)
                if (!candidate.IsUnlocked(part.Id)) return AssemblyCommandResult.Fail("profile.part.locked", part.Id);
            var balance = owner.AssemblyBalance;
            var result = AssemblyResolver.Resolve(recipe, catalog, balance, MaterialTier.Pencil, fits);
            if (!result.Valid) return new AssemblyCommandResult(false, result.Diagnostics[0].Code, diagnostics: result.Diagnostics);
            var materials = result.Value.Support?.AllowedMaterials ?? new[] { MaterialTier.Pencil, MaterialTier.Ink, MaterialTier.Marker };
            foreach (var material in materials)
            {
                var resolved = AssemblyResolver.Resolve(recipe, catalog, balance, material);
                if (!resolved.Valid) return new AssemblyCommandResult(false, resolved.Diagnostics[0].Code, diagnostics: resolved.Diagnostics);
            }
            snapshot = new RecipeSnapshot(recipe, catalog.BalanceRevision, Definitions(recipe.Parts));
            return AssemblyCommandResult.Ok(null);
        }
        IEnumerable<ComponentDefinition> Definitions(IEnumerable<PartRef> parts)
        {
            var seen = new HashSet<(string, int)>();
            foreach (var part in parts)
                if (seen.Add((part.Id, part.Revision)))
                {
                    if (!catalog.TryGet(part.Id, part.Revision, out var definition)) throw AssemblyCatalogException.At("assembly.part.missing", part.Id);
                    yield return definition;
                }
        }
        AssemblyCommandResult ValidateOwned(WorkshopProfile candidate, AlbumEntry entry)
        {
            if (entry == null) return AssemblyCommandResult.Fail("profile.entry.missing");
            if (entry.Kind == AlbumEntryKind.Legacy)
                return candidate.ValidateOwned(entry.LegacyBlueprint, out var reason, owner.AssemblyBalance)
                    ? AssemblyCommandResult.Ok(entry.Id) : AssemblyCommandResult.Fail("profile.entry.unavailable", entry.Id, reason);
            foreach (var part in entry.Snapshot.Recipe.Parts)
                if (!candidate.IsUnlocked(part.Id)) return AssemblyCommandResult.Fail("profile.part.locked", part.Id);
            var resolved = TowerCatalog.ResolveEntry(entry, owner.AssemblyBalance);
            return resolved.Valid ? AssemblyCommandResult.Ok(entry.Id) : new AssemblyCommandResult(false, resolved.Diagnostics[0].Code, diagnostics: resolved.Diagnostics);
        }
        static PartRef P(string id) => new PartRef(id, 1);
        static FoundationRecipe DefaultFoundation() => new FoundationRecipe(P("frame.standard"), P("hull.standard"), P("armor.none"), P("feed.standard"), P("turn.standard"));
        static WeaponRecipe DefaultWeapon() => new WeaponRecipe(P("core.gun"), P("head.gun"), P("sight.iron"), P("magazine.standard"), P("trigger.standard"));
    }
}
