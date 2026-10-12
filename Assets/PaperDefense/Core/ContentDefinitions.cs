using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public enum PartCategory { Chassis = 0, Weapon = 1, Module = 2 }
    public enum AttackKind { Single = 0, Line = 1, AreaProjectile = 2, Cone = 3, Ricochet = 4, Radial = 5, Cleanup = 6 }
    public enum ModifierStat { FoundationHealth = 0, FoundationDefense = 1, WeaponRange = 2, ActionInterval = 3, FeedMultiplier = 4, AcquisitionSeconds = 5 }
    public enum ModifierOperation { Set = 0, Add = 1, Multiply = 2 }
    public enum ModifierScope { Foundation = 0, AllWeapons = 1, Mount = 2, Support = 3 }
    public enum EffectKind { DamageOverTime = 0, Slow = 1, DefenseReduction = 2 }
    public enum ModuleSlotKind { Upgrade = 0, Armor = 1 }

    [Serializable] public class PartDefinitionInput
    {
        public string Id, Name, Description, ButtonLabel, UnlockLabel, TemplateId;
        public bool Enabled = true, InitiallyUnlocked;
        public int Order, UnlockGold;
        public DrawingVisualStyle VisualStyle;
    }
    [Serializable] public sealed class ChassisDefinitionInput : PartDefinitionInput
    {
        public int Capacity, BaseCost, MountCount = 2;
        public int UpgradeSlotCount = int.MaxValue, ArmorSlotCount = int.MaxValue;
        // Zero uses legacy global tuning, allowing old standalone fixtures to keep their cadence.
        public float FeedCapacity;
        public float BaseHealth, HealthMultiplier = 1, BuildSeconds, SecondaryBuildSeconds;
        public float PrimaryMountPivotX, PrimaryMountPivotY, PrimaryDualMountPivotX, PrimaryDualMountPivotY, SecondaryMountPivotX, SecondaryMountPivotY;
        public ChassisKind LegacyKind = (ChassisKind)(-1);
        public List<MountPivotInput> MountPivots = new List<MountPivotInput>();
    }
    [Serializable] public sealed class MountPivotInput
    {public int SlotIndex;public float X,Y;}
    [Serializable] public sealed class WeaponDefinitionInput : PartDefinitionInput
    {
        public AttackKind Attack;
        public StructureDefinition Stats = new StructureDefinition();
        public int Load, AssemblyCredit;
        public float MinimumLineRadius, MinimumRadialRange, MinimumRange;
        public float MaximumFootprintAspectRatio = 3;
        public List<string> EffectIds = new List<string>();
        public StructureKind LegacyKind = (StructureKind)(-1);
    }
    [Serializable] public sealed class ModuleDefinitionInput : PartDefinitionInput
    {
        public int Load, Cost;
        public float BuildSeconds;
        public bool Repeatable;
        public string ExclusiveGroup;
        public List<string> AllowedChassisIds = new List<string>();
        public List<AttackKind> AllowedAttacks = new List<AttackKind>();
        public List<StatModifierInput> Modifiers = new List<StatModifierInput>();
        public TowerModules LegacyModule;
        public ModuleSlotKind SlotKind;
    }
    [Serializable] public sealed class StatModifierInput
    {
        public ModifierStat Stat;
        public ModifierOperation Operation;
        public ModifierScope Scope;
        public int MountIndex;
        public float Value;
    }
    [Serializable] public sealed class EffectDefinitionInput
    {
        public string Id;
        public EffectKind Kind;
        public float Duration, Strength, TickSeconds;
        public bool IgnoresDefense;
    }
    [Serializable] public sealed class StartingModuleDefinitionInput
    {
        public string InstanceId,DefinitionId;
    }
    [Serializable] public sealed class StartingBlueprintDefinitionInput
    {
        public string Id,Name,ChassisId,FirstWeaponId,SecondWeaponId;
        public List<StartingModuleDefinitionInput> Modules=new List<StartingModuleDefinitionInput>();
        public List<WeaponMountSelectionData> WeaponMounts=new List<WeaponMountSelectionData>();
        public int Slot=-1;
    }

    public sealed class ModuleSelection
    {
        public string InstanceId { get; }
        public string DefinitionId { get; }
        public ModuleSelection(string instanceId, string definitionId)
        {
            InstanceId = ContentValidation.Id(instanceId, "module.instanceId");
            DefinitionId = ContentValidation.Id(definitionId, "module.definitionId");
        }
    }
    public sealed class StatModifier
    {
        public ModifierStat Stat { get; }
        public ModifierOperation Operation { get; }
        public ModifierScope Scope { get; }
        public int MountIndex { get; }
        public float Value { get; }
        internal StatModifier(StatModifierInput input, string path)
        {
            if (input == null) throw new ArgumentException(UserText.Message("copy.1512687a1bee5d82", path));
            ContentValidation.EnumValue(input.Stat, path + ".stat"); ContentValidation.EnumValue(input.Operation, path + ".operation");
            ContentValidation.EnumValue(input.Scope, path + ".scope"); ContentValidation.Finite(input.Value, path + ".value");
            if (input.Scope == ModifierScope.Mount && (input.MountIndex < 0 || input.MountIndex >= TowerBlueprint.MaximumWeaponMounts)) throw new ArgumentException(UserText.Message("copy.305cc1a81e0870ec", path));
            bool foundation = input.Stat == ModifierStat.FoundationHealth || input.Stat == ModifierStat.FoundationDefense;
            if (foundation != (input.Scope == ModifierScope.Foundation)) throw new ArgumentException(UserText.Message("copy.c2d277c38239867a", path));
            if (input.Stat == ModifierStat.FeedMultiplier && input.Scope != ModifierScope.AllWeapons) throw new ArgumentException(UserText.Message("copy.3cf4cd4769501f07", path));
            if (input.Operation == ModifierOperation.Multiply && input.Value < 0) throw new ArgumentException(UserText.Message("copy.92a86acc13e8c916", path));
            Stat = input.Stat; Operation = input.Operation; Scope = input.Scope; MountIndex = input.MountIndex; Value = input.Value;
        }
        internal float Apply(float value) => Operation == ModifierOperation.Set ? Value : Operation == ModifierOperation.Add ? value + Value : value * Value;
    }
    public sealed class ChassisDefinition : WorkshopPartDefinition
    {
        public int Capacity { get; } public int BaseCost { get; } public int MountCount { get; }
        public int UpgradeSlotCount { get; } public int ArmorSlotCount { get; } public float FeedCapacity { get; }
        public float BaseHealth { get; } public float HealthMultiplier { get; } public float BuildSeconds { get; } public float SecondaryBuildSeconds { get; }
        public Point2 PrimaryMountPivot { get; } public Point2 PrimaryDualMountPivot { get; } public Point2 SecondaryMountPivot { get; }
        public IReadOnlyList<Point2> MountPivots { get; }
        readonly bool hasMountPivots;
        internal ChassisDefinition(ChassisDefinitionInput input) : base(input, PartCategory.Chassis, chassis: LegacyContentIds.Chassis(input.LegacyKind)==input.Id ? input.LegacyKind : (ChassisKind?)null)
        {
            ContentValidation.NonNegative(input.Capacity, Id + ".capacity"); ContentValidation.NonNegative(input.BaseCost, Id + ".baseCost");
            if (input.MountCount < 1 || input.MountCount > TowerBlueprint.MaximumWeaponMounts) throw new ArgumentException(UserText.Message("copy.c09541540db2e990", Id));
            ContentValidation.NonNegative(input.UpgradeSlotCount, Id + ".upgradeSlotCount");ContentValidation.NonNegative(input.ArmorSlotCount, Id + ".armorSlotCount");ContentValidation.NonNegative(input.FeedCapacity, Id + ".feedCapacity");
            ContentValidation.Positive(input.BaseHealth, Id + ".baseHealth"); ContentValidation.Positive(input.HealthMultiplier, Id + ".healthMultiplier");
            ContentValidation.NonNegative(input.BuildSeconds, Id + ".buildSeconds"); ContentValidation.NonNegative(input.SecondaryBuildSeconds, Id + ".secondaryBuildSeconds");
            float[] coordinates = { input.PrimaryMountPivotX, input.PrimaryMountPivotY, input.PrimaryDualMountPivotX, input.PrimaryDualMountPivotY, input.SecondaryMountPivotX, input.SecondaryMountPivotY };
            foreach (float coordinate in coordinates) { ContentValidation.Finite(coordinate, Id + ".mountPivot"); if (Math.Abs(coordinate) > 1) throw new ArgumentException(UserText.Message("copy.bb14831d5e7fab4d", Id)); }
            Capacity = input.Capacity; BaseCost = input.BaseCost; MountCount = input.MountCount; BaseHealth = input.BaseHealth; HealthMultiplier = input.HealthMultiplier;
            UpgradeSlotCount=input.UpgradeSlotCount;ArmorSlotCount=input.ArmorSlotCount;FeedCapacity=input.FeedCapacity;
            BuildSeconds = input.BuildSeconds; SecondaryBuildSeconds = input.SecondaryBuildSeconds;
            PrimaryMountPivot = new Point2(input.PrimaryMountPivotX,input.PrimaryMountPivotY); PrimaryDualMountPivot = new Point2(input.PrimaryDualMountPivotX,input.PrimaryDualMountPivotY); SecondaryMountPivot = new Point2(input.SecondaryMountPivotX,input.SecondaryMountPivotY);
            var pivots=new[]{new Point2(-.24f,.04f),new Point2(0,.04f),new Point2(.24f,.04f)};
            if(input.MountPivots==null)throw new ArgumentException(Id+".mountPivots: missing list");
            hasMountPivots=input.MountPivots.Count>0;
            if(hasMountPivots)
            {
                if(input.MountPivots.Count!=MountCount)throw new ArgumentException(Id+".mountPivots: one pivot per slot is required");
                var seen=new HashSet<int>();
                foreach(var pivot in input.MountPivots)
                {
                    if(pivot==null||pivot.SlotIndex<0||pivot.SlotIndex>=MountCount||!seen.Add(pivot.SlotIndex))throw new ArgumentException(Id+".mountPivots: invalid or duplicate slot");
                    ContentValidation.Finite(pivot.X,Id+".mountPivots.x");ContentValidation.Finite(pivot.Y,Id+".mountPivots.y");
                    if(Math.Abs(pivot.X)>1||Math.Abs(pivot.Y)>1)throw new ArgumentException(Id+".mountPivots: coordinate outside [-1,1]");
                    pivots[pivot.SlotIndex]=new Point2(pivot.X,pivot.Y);
                }
            }
            MountPivots=Array.AsReadOnly(pivots);
        }
        public Point2 GetMountPivot(int slotIndex,int occupiedCount)
        {
            if(slotIndex<0||slotIndex>=TowerBlueprint.MaximumWeaponMounts)throw new ArgumentOutOfRangeException(nameof(slotIndex));
            if(hasMountPivots||occupiedCount>2)return MountPivots[slotIndex];
            return slotIndex==0?(occupiedCount>1?PrimaryDualMountPivot:PrimaryMountPivot):slotIndex==1?SecondaryMountPivot:new Point2(.24f,.04f);
        }
    }
    public sealed class WeaponDefinition : WorkshopPartDefinition
    {
        internal StructureDefinition Numeric { get; }
        public StructureSpec Stats { get; } public AttackKind Attack { get; } public int Load { get; } public int AssemblyCredit { get; }
        public float MinimumLineRadius { get; } public float MinimumRadialRange { get; } public float MinimumRange { get; }
        public float MaximumFootprintAspectRatio { get; }
        public IReadOnlyList<string> EffectIds { get; }
        public IReadOnlyList<EffectDefinition> Effects { get; }
        public StructureKind? LegacyKind => MountKind;
        public bool SupportsFootprint => Attack == AttackKind.AreaProjectile;
        internal WeaponDefinition(WeaponDefinitionInput input,IReadOnlyDictionary<string,EffectDefinition> knownEffects=null) : base(input, PartCategory.Weapon, mountKind: LegacyContentIds.Weapon(input.LegacyKind)==input.Id ? input.LegacyKind : (StructureKind?)null, combatMount: input.Attack != AttackKind.Cleanup)
        {
            ContentValidation.EnumValue(input.Attack, Id + ".attack"); ContentValidation.NonNegative(input.Load,Id + ".load"); ContentValidation.NonNegative(input.AssemblyCredit,Id + ".assemblyCredit");
            ContentValidation.NonNegative(input.MinimumLineRadius,Id + ".minimumLineRadius"); ContentValidation.NonNegative(input.MinimumRadialRange,Id + ".minimumRadialRange");
            ContentValidation.Positive(input.MaximumFootprintAspectRatio,Id + ".maximumFootprintAspectRatio");
            if(input.MaximumFootprintAspectRatio<1||input.MaximumFootprintAspectRatio>MortarFootprint.StructuralMaximumAspectRatio)throw new ArgumentException(UserText.Message("copy.2d76e3a6af048cc2", Id));
            MaximumFootprintAspectRatio=input.MaximumFootprintAspectRatio;
            Numeric = CopyStats(input.Stats ?? throw new ArgumentException(UserText.Message("copy.686ca7f7e7e7cd4f", Id)));
            ContentValidation.ValidateWeapon(Numeric, Id);
            ContentValidation.NonNegative(input.MinimumRange,Id + ".minimumRange");
            float maximum=input.Attack==AttackKind.Radial?Math.Max(input.MinimumRadialRange,Numeric.Radius):Numeric.Range;
            if(input.MinimumRange>=maximum&&(input.MinimumRange!=0||maximum!=0))throw new ArgumentException(UserText.Message("copy.2a0c9766160d6e89", Id));
            MinimumRange=input.MinimumRange;
            Stats = new StructureSpec(Numeric); Attack = input.Attack; Load = input.Load; AssemblyCredit = input.AssemblyCredit; MinimumLineRadius = input.MinimumLineRadius; MinimumRadialRange = input.MinimumRadialRange;
            EffectIds = Array.AsReadOnly((input.EffectIds ?? throw new ArgumentException(UserText.Message("copy.697ca00931ccc35a", Id))).ToArray());
            var resolved=new List<EffectDefinition>();
            foreach(string id in EffectIds){if(id==null||knownEffects==null||!knownEffects.TryGetValue(id,out var effect))throw new ArgumentException(UserText.Message("copy.2da5a9607ff6d827", Id, id));resolved.Add(effect);}
            Effects=resolved.AsReadOnly();
        }
        internal static StructureDefinition CopyStats(StructureDefinition d) => new StructureDefinition { Kind=d.Kind,Cost=d.Cost,MaxHealth=d.MaxHealth,BuildSeconds=d.BuildSeconds,Range=d.Range,Damage=d.Damage,ShotInterval=d.ShotInterval,Radius=d.Radius,FlightSeconds=d.FlightSeconds,MaxPierce=d.MaxPierce,ConeAngleDegrees=d.ConeAngleDegrees,MaxTargets=d.MaxTargets,JumpRange=d.JumpRange,SlowSpeedMultiplier=d.SlowSpeedMultiplier,SlowDuration=d.SlowDuration };
    }
    public sealed class ModuleDefinition : WorkshopPartDefinition
    {
        public int Load { get; } public int Cost { get; } public float BuildSeconds { get; } public bool Repeatable { get; } public string ExclusiveGroup { get; }
        public ModuleSlotKind SlotKind { get; }
        public IReadOnlyList<string> AllowedChassisIds { get; } public IReadOnlyList<AttackKind> AllowedAttacks { get; } public IReadOnlyList<StatModifier> Modifiers { get; }
        internal ModuleDefinition(ModuleDefinitionInput input) : base(input, PartCategory.Module, module: LegacyContentIds.Module(input.LegacyModule)==input.Id ? input.LegacyModule : TowerModules.None)
        {
            ContentValidation.NonNegative(input.Load,Id + ".load"); ContentValidation.NonNegative(input.Cost,Id + ".cost"); ContentValidation.NonNegative(input.BuildSeconds,Id + ".buildSeconds");
            ContentValidation.EnumValue(input.SlotKind,Id + ".slotKind");SlotKind=input.SlotKind;
            Load=input.Load;Cost=input.Cost;BuildSeconds=input.BuildSeconds;Repeatable=input.Repeatable;ExclusiveGroup=input.ExclusiveGroup ?? "";
            AllowedChassisIds=Array.AsReadOnly((input.AllowedChassisIds ?? throw new ArgumentException(UserText.Message("copy.64e28815269509a3", Id))).ToArray());
            AllowedAttacks=Array.AsReadOnly((input.AllowedAttacks ?? throw new ArgumentException(UserText.Message("copy.2a557d60d11cf5e5", Id))).ToArray());
            foreach(var attack in AllowedAttacks) ContentValidation.EnumValue(attack,Id + ".allowedAttacks");
            var modifiers=new List<StatModifier>(); if(input.Modifiers==null)throw new ArgumentException(UserText.Message("copy.9920a380dd97ad22", Id));
            for(int i=0;i<input.Modifiers.Count;i++)modifiers.Add(new StatModifier(input.Modifiers[i],Id + ".modifiers[" + i + "]")); Modifiers=modifiers.AsReadOnly();
        }
    }
    public sealed class EffectDefinition
    {
        public string Id { get; } public EffectKind Kind { get; } public float Duration { get; } public float Strength { get; } public float TickSeconds { get; } public bool IgnoresDefense { get; }
        internal EffectDefinition(EffectDefinitionInput input)
        {
            if(input==null)throw new ArgumentException(UserText.Message("copy.743eb8d027adf09d")); Id=ContentValidation.Id(input.Id,"effect.id"); ContentValidation.EnumValue(input.Kind,Id + ".kind");
            ContentValidation.NonNegative(input.Duration,Id + ".duration");ContentValidation.NonNegative(input.Strength,Id + ".strength");ContentValidation.NonNegative(input.TickSeconds,Id + ".tickSeconds");
            if(input.Kind==EffectKind.DamageOverTime && input.TickSeconds<=0)throw new ArgumentException(UserText.Message("copy.a6e1b178a835d647", Id));
            if(input.Kind!=EffectKind.DamageOverTime && input.Strength>1)throw new ArgumentException(UserText.Message("copy.b4bde442cb088268", Id));
            Kind=input.Kind;Duration=input.Duration;Strength=input.Strength;TickSeconds=input.TickSeconds;IgnoresDefense=input.IgnoresDefense;
        }
    }
    internal static class ContentValidation
    {
        internal static string Id(string value,string path) { if(string.IsNullOrWhiteSpace(value)||value!=value.Trim()||value.Length>128)throw new ArgumentException(UserText.Message("copy.8450aa8e054addee", path)); foreach(char c in value)if(char.IsControl(c))throw new ArgumentException(UserText.Message("copy.af0751ba0d6f77e9", path)); return value; }
        internal static void Finite(float value,string path){if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentException(UserText.Message("copy.830cb12827b79088", path));}
        internal static void NonNegative(float value,string path){Finite(value,path);if(value<0)throw new ArgumentException(UserText.Message("copy.eddab252c5ca2357", path));}
        internal static void Positive(float value,string path){Finite(value,path);if(value<=0)throw new ArgumentException(UserText.Message("copy.9b6a841ef615d988", path));}
        internal static void EnumValue<T>(T value,string path){if(!Enum.IsDefined(typeof(T),value))throw new ArgumentException(UserText.Message("copy.90efa17f315e338e", path));}
        internal static void ValidateWeapon(StructureDefinition d,string id)
        {
            NonNegative(d.Cost,id + ".stats.cost");Positive(d.MaxHealth,id + ".stats.maxHealth");NonNegative(d.BuildSeconds,id + ".stats.buildSeconds");NonNegative(d.Range,id + ".attack.range");NonNegative(d.Damage,id + ".attack.damage");NonNegative(d.ShotInterval,id + ".attack.intervalSeconds");NonNegative(d.Radius,id + ".attack.radius");NonNegative(d.FlightSeconds,id + ".attack.flightSeconds");NonNegative(d.MaxPierce,id + ".attack.maxPierce");NonNegative(d.MaxTargets,id + ".attack.maxTargets");NonNegative(d.ConeAngleDegrees,id + ".attack.coneAngleDegrees");if(d.ConeAngleDegrees>360)throw new ArgumentException(UserText.Message("copy.32415190416a0a3c", id));NonNegative(d.JumpRange,id + ".attack.jumpRange");NonNegative(d.SlowSpeedMultiplier,id + ".attack.slowSpeedMultiplier");if(d.SlowSpeedMultiplier>1)throw new ArgumentException(UserText.Message("copy.ed52c6a17fb05135", id));NonNegative(d.SlowDuration,id + ".attack.slowDuration");
        }
    }
}
