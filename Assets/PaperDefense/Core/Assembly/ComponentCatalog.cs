using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public sealed class ComponentCatalog
    {
        readonly Dictionary<(string, int), ComponentDefinition> lookup = new Dictionary<(string, int), ComponentDefinition>();
        public string BalanceRevision { get; }
        public IReadOnlyList<ComponentDefinition> Definitions { get; }
        public IReadOnlyList<AlbumEntry> Starters { get; }
        public ComponentCatalog(string balanceRevision, IEnumerable<ComponentDefinition> definitions, IEnumerable<AssemblyStarterInput> starters = null)
        {
            if (string.IsNullOrWhiteSpace(balanceRevision)) throw new ArgumentException(nameof(balanceRevision));
            BalanceRevision = balanceRevision; var owned = new List<ComponentDefinition>();
            foreach (var source in definitions ?? throw new ArgumentNullException(nameof(definitions)))
            {
                if (source == null) throw AssemblyCatalogException.At("assembly.component.missing", "definitions");
                var d = source.Copy();
                if (!lookup.TryAdd((d.Id, d.Revision), d)) throw AssemblyCatalogException.At("assembly.component.duplicate", d.Id, d.Revision.ToString());
                owned.Add(d);
            }
            Definitions = owned.AsReadOnly();
            var entries = new List<AlbumEntry>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in starters ?? Array.Empty<AssemblyStarterInput>())
            {
                if (s == null || !ids.Add(s.Id ?? "")) throw AssemblyCatalogException.At("assembly.entry.duplicate", "starters");
                var recipe = s.CreateRecipe(); var issues = ValidateRecipe(recipe);
                if (issues.Count != 0) throw new AssemblyCatalogException(issues);
                var used = new List<ComponentDefinition>(); var seen = new HashSet<(string, int)>();
                foreach (var part in recipe.Parts)
                {
                    var d = lookup[(part.Id, part.Revision)];
                    if (d.UnlockGold != 0 || d.RequiredLevel != 1) throw AssemblyCatalogException.At("assembly.starter.locked", s.Id, d.Id);
                    if (seen.Add((part.Id, part.Revision))) used.Add(d);
                }
                entries.Add(new AlbumEntry(s.Id, s.Name, s.Revision, new RecipeSnapshot(recipe, balanceRevision, used)));
            }
            Starters = entries.AsReadOnly();
        }
        public bool TryGet(string id, int revision, out ComponentDefinition definition)
        {
            definition = null;
            return id != null && lookup.TryGetValue((id, revision), out definition);
        }
        public IReadOnlyList<AssemblyDiagnostic> ValidateRecipe(AssemblyRecipe recipe)
        {
            var issues = new List<AssemblyDiagnostic>();
            void Check(PartRef part, ComponentCategory category, string path)
            {
                if (!TryGet(part.Id, part.Revision, out var d)) issues.Add(new AssemblyDiagnostic("assembly.part.missing", path, part.Id, part.Revision.ToString()));
                else if (d.Category != category) issues.Add(new AssemblyDiagnostic("assembly.part.category", path, part.Id, category.ToString()));
            }
            if (recipe.Structure != null)
            {
                var s = recipe.Structure;
                Check(s.Site, ComponentCategory.Site, "structure.site"); Check(s.Function, ComponentCategory.Function, "structure.function");
                Check(s.Shell, ComponentCategory.Shell, "structure.shell"); Check(s.Utility, ComponentCategory.Utility, "structure.utility");
                foreach (var p in s.Parts) if (TryGet(p.Id, p.Revision, out var d) && (d.StructureFamilies & s.Family) == 0)
                    issues.Add(new AssemblyDiagnostic("assembly.part.incompatible", "structure", p.Id));
            }
            else
            {
                var f = recipe.Tower.Foundation;
                Check(f.Frame, ComponentCategory.Frame, "foundation.frame"); Check(f.Hull, ComponentCategory.Hull, "foundation.hull");
                Check(f.Armor, ComponentCategory.Armor, "foundation.armor"); Check(f.Feed, ComponentCategory.Feed, "foundation.feed"); Check(f.Turn, ComponentCategory.Turn, "foundation.turn");
                foreach (var m in recipe.Tower.Mounts)
                {
                    var w = m.Weapon; string path = "mounts[" + m.SlotIndex + "]";
                    Check(w.Mechanism, ComponentCategory.Mechanism, path + ".mechanism"); Check(w.Head, ComponentCategory.Head, path + ".head");
                    Check(w.Sight, ComponentCategory.Sight, path + ".sight"); Check(w.Magazine, ComponentCategory.Magazine, path + ".magazine"); Check(w.Trigger, ComponentCategory.Trigger, path + ".trigger");
                    if (!TryGet(w.Mechanism.Id, w.Mechanism.Revision, out var core)) continue;
                    foreach (var p in w.Parts) if (TryGet(p.Id, p.Revision, out var d))
                    {
                        if ((d.WeaponFamilies & core.WeaponFamilies) == 0) issues.Add(new AssemblyDiagnostic("assembly.part.incompatible", path, p.Id));
                        foreach (var required in d.RequiredParts)
                        {
                            bool found = false;
                            foreach (var selected in w.Parts) if (selected.Id == required.Id && selected.Revision == required.Revision) found = true;
                            if (!found) issues.Add(new AssemblyDiagnostic("assembly.part.required", path, d.Id, required.Id));
                        }
                    }
                }
            }
            return issues.AsReadOnly();
        }
    }

    [Serializable] public sealed class AssemblyCatalogInput
    {
        public string BalanceRevision;
        public ComponentDefinitionInput[] Definitions = Array.Empty<ComponentDefinitionInput>();
        public AssemblyStarterInput[] Starters = Array.Empty<AssemblyStarterInput>();
        public ComponentCatalog CreateSnapshot()
        {
            var definitions = new List<ComponentDefinition>();
            foreach (var d in Definitions ?? throw new ArgumentNullException(nameof(Definitions))) definitions.Add(d.CreateSnapshot());
            return new ComponentCatalog(BalanceRevision, definitions, Starters);
        }
    }
}
