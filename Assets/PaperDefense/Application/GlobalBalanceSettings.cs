using System;
using System.Collections.Generic;
using PaperDefense.Core;

namespace PaperDefense.Application
{
    /// <summary>Authoring input contains shared rules only. Concrete part/effect parameters exist solely in the catalog.</summary>
    [Serializable] public sealed class GlobalBalanceSettings
    {
        public GameBalancePreset Preset;
        public int Width, Height, StartingInk, MaxEarlyLaunchInk, WaveInkGrant, MaxEnemiesPerCell, BlotterCost, WellActivationCost, WellInkPerWave, WaveGold, VictoryGold;
        public float BaseHealth, PlanningSeconds, IntermissionSeconds, PassiveInkPerSecond, EarlyLaunchInkPerSecond, SimulationStep, BlotLifetime, BlotBuffSeconds, BlotSpeedBonus, BlotReduction, BlotterCooldown, BlotterRadius, WellHealth, DrawingMinimumRate, DrawingMaximumRate;
        public bool EnableEnemyMechanics, EnableInkWells;
        public MechanicsConfiguration Mechanics;
        public SharedConstructionSettings Construction;
        public SharedEffectsSettings Effects;
        public ProgressionRules Progression;
        public CombatTuningRules Combat;
        public TrialRules Trials;
        public List<WellPlacementDefinition> WellPlacements;
        public List<StructureDefinition> StructureDefinitions;
        public List<MaterialDefinition> MaterialDefinitions;
        public List<EnemyDefinition> EnemyDefinitions;
        public List<WaveDefinition> Waves;

        public GameBalance CreateBalance()
        {
            if (Mechanics==null) throw new ArgumentException("preset.rules.Mechanics: отсутствует обязательная группа правил.");
            if (Construction==null) throw new ArgumentException("preset.rules.Construction: отсутствует обязательная группа правил.");
            if (Effects==null) throw new ArgumentException("preset.rules.Effects: отсутствует обязательная группа правил.");
            if (Progression==null) throw new ArgumentException("preset.rules.Progression: отсутствует обязательная группа правил.");
            if (Combat==null) throw new ArgumentException("preset.rules.Combat: отсутствует обязательная группа правил.");
            if (Trials==null) throw new ArgumentException("preset.rules.Trials: отсутствует обязательная группа правил.");
            if (WellPlacements==null) throw new ArgumentException("preset.rules.WellPlacements: отсутствует обязательная группа правил.");
            if (StructureDefinitions==null) throw new ArgumentException("preset.rules.StructureDefinitions: отсутствует обязательная группа правил.");
            if (MaterialDefinitions==null) throw new ArgumentException("preset.rules.MaterialDefinitions: отсутствует обязательная группа правил.");
            if (EnemyDefinitions==null) throw new ArgumentException("preset.rules.EnemyDefinitions: отсутствует обязательная группа правил.");
            if (Waves==null) throw new ArgumentException("preset.rules.Waves: отсутствует обязательная группа правил.");
            var b=new GameBalance(); b.Preset=this.Preset; b.Width=this.Width; b.Height=this.Height; b.StartingInk=this.StartingInk; b.MaxEarlyLaunchInk=this.MaxEarlyLaunchInk; b.WaveInkGrant=this.WaveInkGrant; b.MaxEnemiesPerCell=this.MaxEnemiesPerCell; b.BlotterCost=this.BlotterCost; b.WellActivationCost=this.WellActivationCost; b.WellInkPerWave=this.WellInkPerWave; b.WaveGold=this.WaveGold; b.VictoryGold=this.VictoryGold; b.BaseHealth=this.BaseHealth; b.PlanningSeconds=this.PlanningSeconds; b.IntermissionSeconds=this.IntermissionSeconds; b.PassiveInkPerSecond=this.PassiveInkPerSecond; b.EarlyLaunchInkPerSecond=this.EarlyLaunchInkPerSecond; b.SimulationStep=this.SimulationStep; b.BlotLifetime=this.BlotLifetime; b.BlotBuffSeconds=this.BlotBuffSeconds; b.BlotSpeedBonus=this.BlotSpeedBonus; b.BlotReduction=this.BlotReduction; b.BlotterCooldown=this.BlotterCooldown; b.BlotterRadius=this.BlotterRadius; b.WellHealth=this.WellHealth; b.DrawingMinimumRate=this.DrawingMinimumRate; b.DrawingMaximumRate=this.DrawingMaximumRate; b.EnableEnemyMechanics=this.EnableEnemyMechanics; b.EnableInkWells=this.EnableInkWells;
            b.Mechanics=Mechanics; Construction.Apply(b.Construction); Effects.Apply(b.Effects); b.Progression=Progression;
            b.Combat=Combat; b.Trials=Trials; b.WellPlacements=WellPlacements; b.StructureDefinitions=StructureDefinitions;
            b.MaterialDefinitions=MaterialDefinitions; b.EnemyDefinitions=EnemyDefinitions; b.Waves=Waves;
            if (StructureDefinitions.Exists(item=>item==null || (item.Kind!=StructureKind.Wall && item.Kind!=StructureKind.Spikes)))
                throw new ArgumentException("preset.rules.StructureDefinitions: только непосредственные инструменты Wall/Spikes; оружие задаётся каталогом.");
            return b.CreateSnapshot();
        }

        public static GlobalBalanceSettings From(GameBalance source)
        {
            var b=source.CreateSnapshot(); var input=new GlobalBalanceSettings(); input.Preset=b.Preset; input.Width=b.Width; input.Height=b.Height; input.StartingInk=b.StartingInk; input.MaxEarlyLaunchInk=b.MaxEarlyLaunchInk; input.WaveInkGrant=b.WaveInkGrant; input.MaxEnemiesPerCell=b.MaxEnemiesPerCell; input.BlotterCost=b.BlotterCost; input.WellActivationCost=b.WellActivationCost; input.WellInkPerWave=b.WellInkPerWave; input.WaveGold=b.WaveGold; input.VictoryGold=b.VictoryGold; input.BaseHealth=b.BaseHealth; input.PlanningSeconds=b.PlanningSeconds; input.IntermissionSeconds=b.IntermissionSeconds; input.PassiveInkPerSecond=b.PassiveInkPerSecond; input.EarlyLaunchInkPerSecond=b.EarlyLaunchInkPerSecond; input.SimulationStep=b.SimulationStep; input.BlotLifetime=b.BlotLifetime; input.BlotBuffSeconds=b.BlotBuffSeconds; input.BlotSpeedBonus=b.BlotSpeedBonus; input.BlotReduction=b.BlotReduction; input.BlotterCooldown=b.BlotterCooldown; input.BlotterRadius=b.BlotterRadius; input.WellHealth=b.WellHealth; input.DrawingMinimumRate=b.DrawingMinimumRate; input.DrawingMaximumRate=b.DrawingMaximumRate; input.EnableEnemyMechanics=b.EnableEnemyMechanics; input.EnableInkWells=b.EnableInkWells;
            input.Mechanics=b.Mechanics; input.Construction=SharedConstructionSettings.From(b.Construction); input.Effects=SharedEffectsSettings.From(b.Effects); input.Progression=b.Progression;
            input.Combat=b.Combat; input.Trials=b.Trials; input.WellPlacements=b.WellPlacements;
            input.StructureDefinitions=b.StructureDefinitions.FindAll(item=>item.Kind==StructureKind.Wall || item.Kind==StructureKind.Spikes);
            input.MaterialDefinitions=b.MaterialDefinitions; input.EnemyDefinitions=b.EnemyDefinitions; input.Waves=b.Waves;
            return input;
        }
    }

    [Serializable] public sealed class SharedConstructionSettings
    {
        public float DualFeedMultiplier, MaxFeedMultiplier, DynamicFeedCapacity, AcquisitionSeconds;
        public bool DynamicFeed;
        public int ColorMinimum, ColorCostDivisor;
        internal void Apply(TowerConstructionRules target) { target.DualFeedMultiplier=this.DualFeedMultiplier; target.MaxFeedMultiplier=this.MaxFeedMultiplier; target.DynamicFeedCapacity=this.DynamicFeedCapacity; target.AcquisitionSeconds=this.AcquisitionSeconds; target.DynamicFeed=this.DynamicFeed; target.ColorMinimum=this.ColorMinimum; target.ColorCostDivisor=this.ColorCostDivisor; }
        internal static SharedConstructionSettings From(TowerConstructionRules source)
        { var input=new SharedConstructionSettings(); input.DualFeedMultiplier=source.DualFeedMultiplier; input.MaxFeedMultiplier=source.MaxFeedMultiplier; input.DynamicFeedCapacity=source.DynamicFeedCapacity; input.AcquisitionSeconds=source.AcquisitionSeconds; input.DynamicFeed=source.DynamicFeed; input.ColorMinimum=source.ColorMinimum; input.ColorCostDivisor=source.ColorCostDivisor; return input; }
    }

    [Serializable] public sealed class SharedEffectsSettings
    {
        public float OutlinedHealthMultiplier, HatchedHealthMultiplier, OutlinedDefense, HatchedDefense, MaximumDefense, ColorWindowSeconds;
        public bool LimitTowerColor;
        public int ColorTargetsPerWindow;
        internal void Apply(EffectsRules target) { target.OutlinedHealthMultiplier=this.OutlinedHealthMultiplier; target.HatchedHealthMultiplier=this.HatchedHealthMultiplier; target.OutlinedDefense=this.OutlinedDefense; target.HatchedDefense=this.HatchedDefense; target.MaximumDefense=this.MaximumDefense; target.ColorWindowSeconds=this.ColorWindowSeconds; target.LimitTowerColor=this.LimitTowerColor; target.ColorTargetsPerWindow=this.ColorTargetsPerWindow; }
        internal static SharedEffectsSettings From(EffectsRules source)
        { var input=new SharedEffectsSettings(); input.OutlinedHealthMultiplier=source.OutlinedHealthMultiplier; input.HatchedHealthMultiplier=source.HatchedHealthMultiplier; input.OutlinedDefense=source.OutlinedDefense; input.HatchedDefense=source.HatchedDefense; input.MaximumDefense=source.MaximumDefense; input.ColorWindowSeconds=source.ColorWindowSeconds; input.LimitTowerColor=source.LimitTowerColor; input.ColorTargetsPerWindow=source.ColorTargetsPerWindow; return input; }
    }
}
