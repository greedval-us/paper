using System;
using System.Collections.Generic;
using PaperDefense.Core;

namespace PaperDefense.Validation
{
    /// <summary>Read-only comparison of actual trial rules; no profile, rewards or live run.</summary>
    public static class BalanceComparisonScenario
    {
        public static BalanceComparisonReport Run(GameBalance historical, GameBalance target)
        {
            var rows = new List<BalanceComparisonRow>();
            var pages = new[]
            {
                new TowerBlueprint("compare-single", UserText.Message("copy.285d0432d031dec6"), ChassisKind.Standard, StructureKind.Gun),
                new TowerBlueprint("compare-double", UserText.Message("copy.f32e6591c784b007"), ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun),
                new TowerBlueprint("compare-line", UserText.Message("copy.585e2c4c9f48cee1"), ChassisKind.Standard, StructureKind.Gun, StructureKind.Piercer),
                new TowerBlueprint("compare-mortar", UserText.Message("copy.1a8fe86ca03d1b8f"), ChassisKind.Standard, StructureKind.Mortar, modules: TowerModules.Feed)
            };
            foreach (var page in pages)
                foreach (TrialScenario scenario in Enum.GetValues(typeof(TrialScenario)))
                    foreach (int budget in new[] { 200, 400, 800 })
                    {
                        var before = TrialRunner.Run(page, historical, scenario, budget, 1337, true, InkColor.Blue);
                        var after = TrialRunner.Run(page, target, scenario, budget, 1337, true, InkColor.Blue);
                        rows.Add(new BalanceComparisonRow
                        {
                            Blueprint = page.Name, Scenario = scenario.ToString(), Budget = budget,
                            Historical = TrialValues.From(before), Target = TrialValues.From(after)
                        });
                    }
            var materials = new List<MaterialComparisonRow>();
            var armored = new TowerBlueprint("compare-armor", UserText.Message("copy.6c483a5e388e346f"), ChassisKind.Standard, StructureKind.Gun, modules: TowerModules.Armor);
            foreach (MaterialTier tier in Enum.GetValues(typeof(MaterialTier)))
                materials.Add(new MaterialComparisonRow
                {
                    Material = tier.ToString(), Historical = MaterialValues.From(armored, historical, tier),
                    Target = MaterialValues.From(armored, target, tier)
                });
            return new BalanceComparisonReport
            {
                HistoricalRules = ComparisonRules.From(historical), TargetRules = ComparisonRules.From(target),
                Rows = rows.ToArray(), ArmoredSingleMaterials = materials.ToArray()
            };
        }
    }

    [Serializable] public sealed class BalanceComparisonReport
    {
        public string Conditions = "Seed1337; 60s; complete pencil towers at fixed positions; blue color; simplified counterfire; budget ceiling200/400/800. Legacy-standard geometry keeps quality100 in both presets. No profile or live battle is changed.";
        public BalanceComparisonRow[] Rows;
        public ComparisonRules HistoricalRules, TargetRules;
        public MaterialComparisonRow[] ArmoredSingleMaterials;
    }
    [Serializable] public sealed class ComparisonRules
    {
        public string Preset;
        public int StartingInk;
        public float PassiveInkPerSecond, EarlyLaunchInkPerSecond, DynamicFeedCapacity, MaximumDefense, ColorWindowSeconds;
        public bool DynamicFeed, LimitTowerColor;
        public int ColorTargetsPerWindow;
        public int[] WaveGrants;
        public static ComparisonRules From(GameBalance balance)
        {
            var grants = new int[balance.Waves.Count];
            for (int i = 0; i < grants.Length; i++) grants[i] = balance.Waves[i].InkGrant < 0 ? balance.WaveInkGrant : balance.Waves[i].InkGrant;
            return new ComparisonRules
            {
                Preset = balance.Preset.ToString(), StartingInk = balance.StartingInk,
                PassiveInkPerSecond = balance.PassiveInkPerSecond, EarlyLaunchInkPerSecond = balance.EarlyLaunchInkPerSecond,
                DynamicFeed = balance.Construction.DynamicFeed, DynamicFeedCapacity = balance.Construction.DynamicFeedCapacity,
                MaximumDefense = balance.Effects.MaximumDefense, LimitTowerColor = balance.Effects.LimitTowerColor,
                ColorWindowSeconds = balance.Effects.ColorWindowSeconds, ColorTargetsPerWindow = balance.Effects.ColorTargetsPerWindow,
                WaveGrants = grants
            };
        }
    }
    [Serializable] public sealed class MaterialComparisonRow
    {
        public string Material;
        public MaterialValues Historical, Target;
    }
    [Serializable] public sealed class MaterialValues
    {
        public int Cost;
        public float Health, Defense, DamagePerHit, BuildSeconds;
        public static MaterialValues From(TowerBlueprint blueprint, GameBalance balance, MaterialTier tier) => new MaterialValues
        {
            Cost = TowerCatalog.Cost(blueprint, balance, tier), Health = TowerCatalog.Health(blueprint, balance, tier),
            Defense = DamageRules.EffectiveDefense(TowerCatalog.Defense(blueprint, balance), balance.Effects.MaximumDefense),
            DamagePerHit = balance.Structure(blueprint.FirstWeapon).Damage * balance.Material(tier).DamageMultiplier,
            BuildSeconds = TowerCatalog.BuildSeconds(blueprint, balance, tier)
        };
    }
    [Serializable] public sealed class BalanceComparisonRow
    {
        public string Blueprint, Scenario;
        public int Budget;
        public TrialValues Historical, Target;
    }
    [Serializable] public sealed class TrialValues
    {
        public int Cost, Copies, Leaks, Unresolved, Survivors;
        public float Damage, IdleSeconds;
        public static TrialValues From(TrialReport report) => new TrialValues
        {
            Cost = report.Cost, Copies = report.Copies, Leaks = report.Leaks,
            Unresolved = report.UnresolvedEnemies, Survivors = report.SurvivingTowers,
            Damage = report.Damage, IdleSeconds = report.IdleSeconds
        };
    }
}
