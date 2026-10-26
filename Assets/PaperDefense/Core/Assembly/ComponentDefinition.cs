using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    // Separate from the persisted legacy PartCategory enum. Categories are semantic sockets;
    // physical sockets and swept clearance are supplied by the later fit catalog.
    public enum ComponentCategory { Frame, Hull, Armor, Feed, Turn, Mechanism, Head, Sight, Magazine, Trigger, Site, Function, Shell, Utility }
    [Flags] public enum AssemblyWeaponFamily { None = 0, Gun = 1, Piercer = 2, Mortar = 4, Fan = 8, Ricochet = 16, Stapler = 32, Blotter = 64, All = 127 }
    [Flags] public enum AssemblyStructureFamily { None = 0, Wall = 1, Slow = 2, Repair = 4, Generator = 8, All = 15 }
    public enum AssemblyStat
    {
        Cost, Load, BuildSeconds, Health, Capacity, MountCount, Feed, Defense, FrontDefense,
        AcquisitionSeconds, Damage, Interval, Range, MinimumRange, TargetLimit, EvaluationTargets,
        Radius, MagazineSize, MagazineOverride, ReloadSeconds, Slow, SlowSeconds, FlightSeconds,
        JumpRange, SupportRate, ConeDegrees
    }

    public sealed class ComponentModifier
    {
        public AssemblyStat Stat { get; }
        public ModifierOperation Operation { get; }
        public float Value { get; }
        public ComponentModifier(AssemblyStat stat, ModifierOperation operation, float value)
        {
            if (!Enum.IsDefined(typeof(AssemblyStat), stat) || !Enum.IsDefined(typeof(ModifierOperation), operation))
                throw AssemblyCatalogException.At("assembly.modifier.unknown", "modifier");
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw AssemblyCatalogException.At("assembly.number.nonfinite", "modifier", stat.ToString());
            Stat = stat; Operation = operation; Value = value;
        }
    }

    public sealed class ComponentDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public int Revision { get; }
        public ComponentCategory Category { get; }
        public int RequiredLevel { get; }
        public int UnlockGold { get; }
        public AssemblyWeaponFamily WeaponFamilies { get; }
        public AssemblyStructureFamily StructureFamilies { get; }
        public IReadOnlyList<ComponentModifier> Modifiers { get; }
        public IReadOnlyList<PartRef> RequiredParts { get; }
        public ComponentDefinition(string id, int revision, string name, ComponentCategory category, int requiredLevel,
            int unlockGold, AssemblyWeaponFamily weaponFamilies, AssemblyStructureFamily structureFamilies,
            IEnumerable<ComponentModifier> modifiers, IEnumerable<PartRef> requiredParts = null)
        {
            if (string.IsNullOrWhiteSpace(id) || revision < 1) throw AssemblyCatalogException.At("assembly.component.identity", "component", id);
            if (!Enum.IsDefined(typeof(ComponentCategory), category)) throw AssemblyCatalogException.At("assembly.category.unknown", id);
            if (requiredLevel < 1 || requiredLevel > 8) throw AssemblyCatalogException.At("assembly.level.range", id);
            if (unlockGold < 0) throw AssemblyCatalogException.At("assembly.unlock.negative", id);
            if ((weaponFamilies & ~AssemblyWeaponFamily.All) != 0 || (structureFamilies & ~AssemblyStructureFamily.All) != 0)
                throw AssemblyCatalogException.At("assembly.compatibility.unknown", id);
            Id = id; Revision = revision; Name = name; Category = category; RequiredLevel = requiredLevel; UnlockGold = unlockGold;
            WeaponFamilies = weaponFamilies; StructureFamilies = structureFamilies;
            var owned = new List<ComponentModifier>();
            var keys = new HashSet<(AssemblyStat, ModifierOperation)>();
            foreach (var modifier in modifiers ?? throw new ArgumentNullException(nameof(modifiers)))
            {
                if (modifier == null || !keys.Add((modifier.Stat, modifier.Operation)))
                    throw AssemblyCatalogException.At("assembly.modifier.duplicate", id);
                owned.Add(new ComponentModifier(modifier.Stat, modifier.Operation, modifier.Value));
            }
            Modifiers = owned.AsReadOnly();
            RequiredParts = new List<PartRef>(requiredParts ?? Array.Empty<PartRef>()).AsReadOnly();
        }
        public float Get(AssemblyStat stat, ModifierOperation operation, float fallback = 0)
        {
            foreach (var modifier in Modifiers) if (modifier.Stat == stat && modifier.Operation == operation) return modifier.Value;
            return fallback;
        }
        internal ComponentDefinition Copy() => new ComponentDefinition(Id, Revision, Name, Category, RequiredLevel, UnlockGold, WeaponFamilies, StructureFamilies, Modifiers, RequiredParts);
    }

    [Serializable] public sealed class ComponentModifierInput
    {
        public AssemblyStat Stat;
        public ModifierOperation Operation;
        public float Value;
    }
    [Serializable] public sealed class ComponentDefinitionInput
    {
        public string Id, Name;
        public int Revision = 1, RequiredLevel = 1, UnlockGold;
        public ComponentCategory Category;
        public AssemblyWeaponFamily WeaponFamilies;
        public AssemblyStructureFamily StructureFamilies;
        public ComponentModifierInput[] Modifiers = Array.Empty<ComponentModifierInput>();
        public PartRefInput[] RequiredParts = Array.Empty<PartRefInput>();
        public ComponentDefinition CreateSnapshot()
        {
            var modifiers = new List<ComponentModifier>();
            foreach (var m in Modifiers ?? throw new ArgumentNullException(nameof(Modifiers)))
                modifiers.Add(new ComponentModifier(m.Stat, m.Operation, m.Value));
            var required = new List<PartRef>();
            foreach (var p in RequiredParts ?? throw new ArgumentNullException(nameof(RequiredParts))) required.Add(p.CreateSnapshot());
            return new ComponentDefinition(Id, Revision, Name, Category, RequiredLevel, UnlockGold, WeaponFamilies, StructureFamilies, modifiers, required);
        }
    }
}
