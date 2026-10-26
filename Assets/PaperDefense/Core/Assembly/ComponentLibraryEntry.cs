using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public enum ComponentLibraryKind { Foundation, Weapon }

    public sealed class ComponentLibraryReference
    {
        public string Id { get; }
        public int Revision { get; }
        public ComponentLibraryKind Kind { get; }
        public int SlotIndex { get; }
        public ComponentLibraryReference(string id, int revision, ComponentLibraryKind kind, int slotIndex)
        {
            if (string.IsNullOrWhiteSpace(id) || revision < 1 || !Enum.IsDefined(typeof(ComponentLibraryKind), kind))
                throw new ArgumentException("Invalid component library reference.");
            if (kind == ComponentLibraryKind.Foundation ? slotIndex != -1 : slotIndex < 0 || slotIndex > 2)
                throw new ArgumentException("Invalid component library reference slot.");
            Id = id; Revision = revision; Kind = kind; SlotIndex = slotIndex;
        }
    }

    /// <summary>A component owns its recipe and definitions independently of the live catalog.</summary>
    public sealed class ComponentRecipeSnapshot
    {
        public ComponentLibraryKind Kind { get; }
        public FoundationRecipe Foundation { get; }
        public WeaponRecipe Weapon { get; }
        public string BalanceRevision { get; }
        public IReadOnlyList<ComponentDefinition> Definitions { get; }
        public ComponentRecipeSnapshot(FoundationRecipe foundation, string balanceRevision, IEnumerable<ComponentDefinition> definitions)
            : this(foundation ?? throw new ArgumentNullException(nameof(foundation)), null, balanceRevision, definitions) { }
        public ComponentRecipeSnapshot(WeaponRecipe weapon, string balanceRevision, IEnumerable<ComponentDefinition> definitions)
            : this(null, weapon ?? throw new ArgumentNullException(nameof(weapon)), balanceRevision, definitions) { }
        ComponentRecipeSnapshot(FoundationRecipe foundation, WeaponRecipe weapon, string balanceRevision, IEnumerable<ComponentDefinition> definitions)
        {
            if (string.IsNullOrWhiteSpace(balanceRevision)) throw new ArgumentException(nameof(balanceRevision));
            BalanceRevision = balanceRevision; Foundation = foundation; Weapon = weapon;
            Kind = foundation != null ? ComponentLibraryKind.Foundation : ComponentLibraryKind.Weapon;
            var owned = new List<ComponentDefinition>();
            foreach (var definition in definitions ?? throw new ArgumentNullException(nameof(definitions)))
            {
                if (definition == null) throw new ArgumentException("Missing component definition.");
                owned.Add(definition.Copy());
            }
            var catalog = new ComponentCatalog(balanceRevision, owned);
            var parts = new List<PartRef>(foundation != null ? foundation.Parts : weapon.Parts);
            var categories = foundation != null
                ? new[] { ComponentCategory.Frame, ComponentCategory.Hull, ComponentCategory.Armor, ComponentCategory.Feed, ComponentCategory.Turn }
                : new[] { ComponentCategory.Mechanism, ComponentCategory.Head, ComponentCategory.Sight, ComponentCategory.Magazine, ComponentCategory.Trigger };
            if (owned.Count != categories.Length) throw new ArgumentException("A component snapshot requires exactly its five selected definitions.");
            var selected = new HashSet<(string, int)>();
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (!catalog.TryGet(part.Id, part.Revision, out var definition) || definition.Category != categories[i] || !selected.Add((part.Id, part.Revision)))
                    throw new ArgumentException("A component snapshot has a missing part or incorrect category.");
            }
            AssemblyWeaponFamily family = AssemblyWeaponFamily.None;
            if (weapon != null)
            {
                catalog.TryGet(weapon.Mechanism.Id, weapon.Mechanism.Revision, out var mechanism);
                family = mechanism.WeaponFamilies; int mask = (int)family;
                if (mask <= 0 || (mask & (mask - 1)) != 0) throw new ArgumentException("A weapon component requires one mechanism family.");
            }
            foreach (var definition in owned)
            {
                if (weapon != null && (definition.WeaponFamilies & family) == 0)
                    throw new ArgumentException("Incompatible weapon component part.");
                foreach (var required in definition.RequiredParts)
                    if (!selected.Contains((required.Id, required.Revision))) throw new ArgumentException("Missing required component part.");
            }
            Definitions = owned.AsReadOnly();
        }
    }

    public sealed class ComponentLibraryEntry
    {
        public string Id { get; }
        public string Name { get; }
        public int Revision { get; }
        public ComponentLibraryKind Kind => Snapshot.Kind;
        public ComponentRecipeSnapshot Snapshot { get; }
        public ComponentLibraryEntry(string id, string name, int revision, ComponentRecipeSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > ProfileLimits.MaximumBlueprintIdLength || revision < 1)
                throw new ArgumentException("Invalid component library identity.");
            if (string.IsNullOrWhiteSpace(name) || name.Length > ProfileLimits.MaximumBlueprintNameLength)
                throw new ArgumentException("Invalid component library name.");
            Id = id; Name = name; Revision = revision; Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }
    }
}
