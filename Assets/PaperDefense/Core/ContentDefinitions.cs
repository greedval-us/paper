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
        public float BaseHealth, HealthMultiplier = 1, BuildSeconds, SecondaryBuildSeconds;
        public float PrimaryMountPivotX, PrimaryMountPivotY, PrimaryDualMountPivotX, PrimaryDualMountPivotY, SecondaryMountPivotX, SecondaryMountPivotY;
        public ChassisKind LegacyKind = (ChassisKind)(-1);
    }
    [Serializable] public sealed class WeaponDefinitionInput : PartDefinitionInput
    {
        public AttackKind Attack;
        public StructureDefinition Stats = new StructureDefinition();
        public int Load, AssemblyCredit;
        public float MinimumLineRadius, MinimumRadialRange;
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
            if (input == null) throw new ArgumentException(path + ": отсутствует модификатор");
            ContentValidation.EnumValue(input.Stat, path + ".stat"); ContentValidation.EnumValue(input.Operation, path + ".operation");
            ContentValidation.EnumValue(input.Scope, path + ".scope"); ContentValidation.Finite(input.Value, path + ".value");
            if (input.Scope == ModifierScope.Mount && (input.MountIndex < 0 || input.MountIndex > 1)) throw new ArgumentException(path + ".mountIndex: допустимы 0 и 1");
            bool foundation = input.Stat == ModifierStat.FoundationHealth || input.Stat == ModifierStat.FoundationDefense;
            if (foundation != (input.Scope == ModifierScope.Foundation)) throw new ArgumentException(path + ".scope: несовместим с характеристикой");
            if (input.Stat == ModifierStat.FeedMultiplier && input.Scope != ModifierScope.AllWeapons) throw new ArgumentException(path + ".scope: подача общая для всех креплений");
            if (input.Operation == ModifierOperation.Multiply && input.Value < 0) throw new ArgumentException(path + ".value: отрицательный множитель");
            Stat = input.Stat; Operation = input.Operation; Scope = input.Scope; MountIndex = input.MountIndex; Value = input.Value;
        }
        internal float Apply(float value) => Operation == ModifierOperation.Set ? Value : Operation == ModifierOperation.Add ? value + Value : value * Value;
    }
    public sealed class ChassisDefinition : WorkshopPartDefinition
    {
        public int Capacity { get; } public int BaseCost { get; } public int MountCount { get; }
        public float BaseHealth { get; } public float HealthMultiplier { get; } public float BuildSeconds { get; } public float SecondaryBuildSeconds { get; }
        public Point2 PrimaryMountPivot { get; } public Point2 PrimaryDualMountPivot { get; } public Point2 SecondaryMountPivot { get; }
        internal ChassisDefinition(ChassisDefinitionInput input) : base(input, PartCategory.Chassis, chassis: LegacyContentIds.Chassis(input.LegacyKind)==input.Id ? input.LegacyKind : (ChassisKind?)null)
        {
            ContentValidation.NonNegative(input.Capacity, Id + ".capacity"); ContentValidation.NonNegative(input.BaseCost, Id + ".baseCost");
            if (input.MountCount < 1 || input.MountCount > 2) throw new ArgumentException(Id + ".mountCount: поддерживается 1 или 2 крепления");
            ContentValidation.Positive(input.BaseHealth, Id + ".baseHealth"); ContentValidation.Positive(input.HealthMultiplier, Id + ".healthMultiplier");
            ContentValidation.NonNegative(input.BuildSeconds, Id + ".buildSeconds"); ContentValidation.NonNegative(input.SecondaryBuildSeconds, Id + ".secondaryBuildSeconds");
            float[] coordinates = { input.PrimaryMountPivotX, input.PrimaryMountPivotY, input.PrimaryDualMountPivotX, input.PrimaryDualMountPivotY, input.SecondaryMountPivotX, input.SecondaryMountPivotY };
            foreach (float coordinate in coordinates) { ContentValidation.Finite(coordinate, Id + ".mountPivot"); if (Math.Abs(coordinate) > 1) throw new ArgumentException(Id + ".mountPivot: координата вне [-1,1]"); }
            Capacity = input.Capacity; BaseCost = input.BaseCost; MountCount = input.MountCount; BaseHealth = input.BaseHealth; HealthMultiplier = input.HealthMultiplier;
            BuildSeconds = input.BuildSeconds; SecondaryBuildSeconds = input.SecondaryBuildSeconds;
            PrimaryMountPivot = new Point2(input.PrimaryMountPivotX,input.PrimaryMountPivotY); PrimaryDualMountPivot = new Point2(input.PrimaryDualMountPivotX,input.PrimaryDualMountPivotY); SecondaryMountPivot = new Point2(input.SecondaryMountPivotX,input.SecondaryMountPivotY);
        }
    }
    public sealed class WeaponDefinition : WorkshopPartDefinition
    {
        internal StructureDefinition Numeric { get; }
        public StructureSpec Stats { get; } public AttackKind Attack { get; } public int Load { get; } public int AssemblyCredit { get; }
        public float MinimumLineRadius { get; } public float MinimumRadialRange { get; }
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
            if(input.MaximumFootprintAspectRatio<1||input.MaximumFootprintAspectRatio>MortarFootprint.StructuralMaximumAspectRatio)throw new ArgumentException(Id + ".maximumFootprintAspectRatio: выходит за технические границы");
            MaximumFootprintAspectRatio=input.MaximumFootprintAspectRatio;
            Numeric = CopyStats(input.Stats ?? throw new ArgumentException(Id + ".stats: отсутствуют параметры"));
            ContentValidation.ValidateWeapon(Numeric, Id);
            Stats = new StructureSpec(Numeric); Attack = input.Attack; Load = input.Load; AssemblyCredit = input.AssemblyCredit; MinimumLineRadius = input.MinimumLineRadius; MinimumRadialRange = input.MinimumRadialRange;
            EffectIds = Array.AsReadOnly((input.EffectIds ?? throw new ArgumentException(Id + ".effectIds: отсутствует список")).ToArray());
            var resolved=new List<EffectDefinition>();
            foreach(string id in EffectIds){if(id==null||knownEffects==null||!knownEffects.TryGetValue(id,out var effect))throw new ArgumentException(Id + ".effectIds: неизвестный ID " + id);resolved.Add(effect);}
            Effects=resolved.AsReadOnly();
        }
        internal static StructureDefinition CopyStats(StructureDefinition d) => new StructureDefinition { Kind=d.Kind,Cost=d.Cost,MaxHealth=d.MaxHealth,BuildSeconds=d.BuildSeconds,Range=d.Range,Damage=d.Damage,ShotInterval=d.ShotInterval,Radius=d.Radius,FlightSeconds=d.FlightSeconds,MaxPierce=d.MaxPierce,ConeAngleDegrees=d.ConeAngleDegrees,MaxTargets=d.MaxTargets,JumpRange=d.JumpRange,SlowSpeedMultiplier=d.SlowSpeedMultiplier,SlowDuration=d.SlowDuration };
    }
    public sealed class ModuleDefinition : WorkshopPartDefinition
    {
        public int Load { get; } public int Cost { get; } public float BuildSeconds { get; } public bool Repeatable { get; } public string ExclusiveGroup { get; }
        public IReadOnlyList<string> AllowedChassisIds { get; } public IReadOnlyList<AttackKind> AllowedAttacks { get; } public IReadOnlyList<StatModifier> Modifiers { get; }
        internal ModuleDefinition(ModuleDefinitionInput input) : base(input, PartCategory.Module, module: LegacyContentIds.Module(input.LegacyModule)==input.Id ? input.LegacyModule : TowerModules.None)
        {
            ContentValidation.NonNegative(input.Load,Id + ".load"); ContentValidation.NonNegative(input.Cost,Id + ".cost"); ContentValidation.NonNegative(input.BuildSeconds,Id + ".buildSeconds");
            Load=input.Load;Cost=input.Cost;BuildSeconds=input.BuildSeconds;Repeatable=input.Repeatable;ExclusiveGroup=input.ExclusiveGroup ?? "";
            AllowedChassisIds=Array.AsReadOnly((input.AllowedChassisIds ?? throw new ArgumentException(Id + ".allowedChassisIds: отсутствует список")).ToArray());
            AllowedAttacks=Array.AsReadOnly((input.AllowedAttacks ?? throw new ArgumentException(Id + ".allowedAttacks: отсутствует список")).ToArray());
            foreach(var attack in AllowedAttacks) ContentValidation.EnumValue(attack,Id + ".allowedAttacks");
            var modifiers=new List<StatModifier>(); if(input.Modifiers==null)throw new ArgumentException(Id + ".modifiers: отсутствует список");
            for(int i=0;i<input.Modifiers.Count;i++)modifiers.Add(new StatModifier(input.Modifiers[i],Id + ".modifiers[" + i + "]")); Modifiers=modifiers.AsReadOnly();
        }
    }
    public sealed class EffectDefinition
    {
        public string Id { get; } public EffectKind Kind { get; } public float Duration { get; } public float Strength { get; } public float TickSeconds { get; } public bool IgnoresDefense { get; }
        internal EffectDefinition(EffectDefinitionInput input)
        {
            if(input==null)throw new ArgumentException("effects: пустая запись"); Id=ContentValidation.Id(input.Id,"effect.id"); ContentValidation.EnumValue(input.Kind,Id + ".kind");
            ContentValidation.NonNegative(input.Duration,Id + ".duration");ContentValidation.NonNegative(input.Strength,Id + ".strength");ContentValidation.NonNegative(input.TickSeconds,Id + ".tickSeconds");
            if(input.Kind==EffectKind.DamageOverTime && input.TickSeconds<=0)throw new ArgumentException(Id + ".tickSeconds: требуется положительное число");
            if(input.Kind!=EffectKind.DamageOverTime && input.Strength>1)throw new ArgumentException(Id + ".strength: требуется число [0,1]");
            Kind=input.Kind;Duration=input.Duration;Strength=input.Strength;TickSeconds=input.TickSeconds;IgnoresDefense=input.IgnoresDefense;
        }
    }
    internal static class ContentValidation
    {
        internal static string Id(string value,string path) { if(string.IsNullOrWhiteSpace(value)||value!=value.Trim()||value.Length>128)throw new ArgumentException(path + ": требуется устойчивый ID до 128 символов"); foreach(char c in value)if(char.IsControl(c))throw new ArgumentException(path + ": управляющий символ в ID"); return value; }
        internal static void Finite(float value,string path){if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentException(path + ": требуется конечное число");}
        internal static void NonNegative(float value,string path){Finite(value,path);if(value<0)throw new ArgumentException(path + ": требуется неотрицательное число");}
        internal static void Positive(float value,string path){Finite(value,path);if(value<=0)throw new ArgumentException(path + ": требуется положительное число");}
        internal static void EnumValue<T>(T value,string path){if(!Enum.IsDefined(typeof(T),value))throw new ArgumentException(path + ": неизвестный обработчик или операция");}
        internal static void ValidateWeapon(StructureDefinition d,string id)
        {
            NonNegative(d.Cost,id + ".stats.cost");Positive(d.MaxHealth,id + ".stats.maxHealth");NonNegative(d.BuildSeconds,id + ".stats.buildSeconds");NonNegative(d.Range,id + ".attack.range");NonNegative(d.Damage,id + ".attack.damage");NonNegative(d.ShotInterval,id + ".attack.intervalSeconds");NonNegative(d.Radius,id + ".attack.radius");NonNegative(d.FlightSeconds,id + ".attack.flightSeconds");NonNegative(d.MaxPierce,id + ".attack.maxPierce");NonNegative(d.MaxTargets,id + ".attack.maxTargets");NonNegative(d.ConeAngleDegrees,id + ".attack.coneAngleDegrees");if(d.ConeAngleDegrees>360)throw new ArgumentException(id + ".attack.coneAngleDegrees: превышает 360");NonNegative(d.JumpRange,id + ".attack.jumpRange");NonNegative(d.SlowSpeedMultiplier,id + ".attack.slowSpeedMultiplier");if(d.SlowSpeedMultiplier>1)throw new ArgumentException(id + ".attack.slowSpeedMultiplier: превышает 1");NonNegative(d.SlowDuration,id + ".attack.slowDuration");
        }
    }
}
