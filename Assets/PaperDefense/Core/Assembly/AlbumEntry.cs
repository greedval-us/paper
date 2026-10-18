using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public enum AlbumEntryKind { Tower, Structure, Legacy }

    public sealed class RecipeSnapshot
    {
        public AssemblyRecipe Recipe { get; }
        public string BalanceRevision { get; }
        public IReadOnlyList<ComponentDefinition> Definitions { get; }
        public RecipeSnapshot(AssemblyRecipe recipe, string balanceRevision, IEnumerable<ComponentDefinition> definitions)
        {
            Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
            if (string.IsNullOrWhiteSpace(balanceRevision)) throw new ArgumentException(nameof(balanceRevision));
            BalanceRevision = balanceRevision;
            var owned = new List<ComponentDefinition>();
            foreach (var d in definitions ?? throw new ArgumentNullException(nameof(definitions))) owned.Add(d.Copy());
            var catalog = new ComponentCatalog(balanceRevision, owned);
            var issues = catalog.ValidateRecipe(recipe);
            if (issues.Count != 0) throw new AssemblyCatalogException(issues);
            Definitions = owned.AsReadOnly();
        }
    }

    public sealed class AlbumEntry
    {
        public string Id { get; } public string Name { get; } public int Revision { get; }
        public AlbumEntryKind Kind { get; }
        public RecipeSnapshot Snapshot { get; }
        public TowerBlueprint LegacyBlueprint { get; }
        public AlbumEntry(string id, string name, int revision, RecipeSnapshot snapshot) : this(id, name, revision)
        { Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot)); Kind = snapshot.Recipe.Tower != null ? AlbumEntryKind.Tower : AlbumEntryKind.Structure; }
        public AlbumEntry(string id, string name, int revision, TowerBlueprint legacyBlueprint) : this(id, name, revision)
        { LegacyBlueprint = legacyBlueprint ?? throw new ArgumentNullException(nameof(legacyBlueprint)); Kind = AlbumEntryKind.Legacy; }
        AlbumEntry(string id, string name, int revision)
        {
            if (string.IsNullOrWhiteSpace(id) || revision < 1) throw AssemblyCatalogException.At("assembly.entry.identity", "entry", id);
            if (string.IsNullOrWhiteSpace(name) || name.Length > 48) throw AssemblyCatalogException.At("assembly.entry.name", id);
            Id = id; Name = name; Revision = revision;
        }
    }
}
