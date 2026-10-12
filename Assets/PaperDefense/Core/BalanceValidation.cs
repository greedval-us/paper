using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>Checks configured rules once at a consumer boundary, without normalizing or copying them.</summary>
    public static class BalanceValidation
    {
        public static void Validate(GameBalance balance)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            if (balance.Width < 10 || balance.Height < 10 || (long)balance.Width * balance.Height > int.MaxValue / 3)
                throw new ArgumentOutOfRangeException(nameof(balance), "Поле должно вмещаться в сетку и быть не меньше 10 × 10 клеток.");
            if (!Enum.IsDefined(typeof(GameBalancePreset), balance.Preset)) throw new ArgumentOutOfRangeException(nameof(balance.Preset));
            NonNegative(balance.StartingInk, nameof(balance.StartingInk));
            Positive(balance.BaseHealth, nameof(balance.BaseHealth));
            NonNegative(balance.PlanningSeconds, nameof(balance.PlanningSeconds)); NonNegative(balance.IntermissionSeconds, nameof(balance.IntermissionSeconds));
            NonNegative(balance.PassiveInkPerSecond, nameof(balance.PassiveInkPerSecond)); NonNegative(balance.EarlyLaunchInkPerSecond, nameof(balance.EarlyLaunchInkPerSecond));
            NonNegative(balance.MaxEarlyLaunchInk, nameof(balance.MaxEarlyLaunchInk)); NonNegative(balance.WaveInkGrant, nameof(balance.WaveInkGrant));
            NonNegative(balance.SimulationStep, nameof(balance.SimulationStep)); NonNegative(balance.MaxEnemiesPerCell, nameof(balance.MaxEnemiesPerCell));
            NonNegative(balance.BlotLifetime, nameof(balance.BlotLifetime)); NonNegative(balance.BlotBuffSeconds, nameof(balance.BlotBuffSeconds));
            NonNegative(balance.BlotSpeedBonus, nameof(balance.BlotSpeedBonus)); Fraction(balance.BlotReduction, nameof(balance.BlotReduction));
            NonNegative(balance.BlotterCost, nameof(balance.BlotterCost)); NonNegative(balance.BlotterCooldown, nameof(balance.BlotterCooldown));
            NonNegative(balance.BlotterRadius, nameof(balance.BlotterRadius)); NonNegative(balance.WellActivationCost, nameof(balance.WellActivationCost));
            NonNegative(balance.WellInkPerWave, nameof(balance.WellInkPerWave)); Positive(balance.WellHealth, nameof(balance.WellHealth));
            NonNegative(balance.WaveGold, nameof(balance.WaveGold)); NonNegative(balance.VictoryGold, nameof(balance.VictoryGold));
            Positive(balance.DrawingMinimumRate, nameof(balance.DrawingMinimumRate)); Fraction(balance.DrawingMaximumRate, nameof(balance.DrawingMaximumRate));
            if (balance.DrawingMinimumRate > balance.DrawingMaximumRate) throw new ArgumentOutOfRangeException(nameof(balance.DrawingMinimumRate));
            if (balance.Construction == null || balance.Effects == null || balance.Progression == null)
                throw new ArgumentException("Отсутствует набор правил конструкции, эффектов или развития.", nameof(balance));
            balance.Construction.Validate(); balance.Effects.Validate(); ValidateProgression(balance.Progression);
            ValidateStructures(balance.StructureDefinitions); ValidateMaterials(balance.MaterialDefinitions);
            ValidateEnemies(balance.EnemyDefinitions); ValidateWaves(balance.Waves);
        }

        static void ValidateStructures(List<StructureDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentException("Отсутствуют определения построек.", nameof(definitions));
            var seen = new HashSet<StructureKind>();
            foreach (var item in definitions)
            {
                if (item == null || !Enum.IsDefined(typeof(StructureKind), item.Kind) || !seen.Add(item.Kind))
                    throw new ArgumentException("Неизвестное, пустое или повторяющееся определение постройки.", nameof(definitions));
                string name = "StructureDefinitions." + item.Kind + ".";
                NonNegative(item.Cost, name + nameof(item.Cost)); Positive(item.MaxHealth, name + nameof(item.MaxHealth));
                NonNegative(item.BuildSeconds, name + nameof(item.BuildSeconds)); NonNegative(item.Range, name + nameof(item.Range));
                NonNegative(item.Damage, name + nameof(item.Damage)); NonNegative(item.ShotInterval, name + nameof(item.ShotInterval));
                NonNegative(item.Radius, name + nameof(item.Radius)); NonNegative(item.FlightSeconds, name + nameof(item.FlightSeconds));
                NonNegative(item.MaxPierce, name + nameof(item.MaxPierce)); NonNegative(item.MaxTargets, name + nameof(item.MaxTargets));
                NonNegative(item.ConeAngleDegrees, name + nameof(item.ConeAngleDegrees));
                if (item.ConeAngleDegrees > 360) throw new ArgumentOutOfRangeException(name + nameof(item.ConeAngleDegrees));
                NonNegative(item.JumpRange, name + nameof(item.JumpRange)); Fraction(item.SlowSpeedMultiplier, name + nameof(item.SlowSpeedMultiplier));
                NonNegative(item.SlowDuration, name + nameof(item.SlowDuration));
            }
            if (seen.Count != Enum.GetValues(typeof(StructureKind)).Length) throw new ArgumentException("Неполный каталог построек.", nameof(definitions));
        }

        static void ValidateMaterials(List<MaterialDefinition> definitions)
        {
            if (definitions == null || definitions.Count != Enum.GetValues(typeof(MaterialTier)).Length)
                throw new ArgumentException("Неполный набор материалов.", nameof(definitions));
            for (int i = 0; i < definitions.Count; i++)
            {
                var item = definitions[i];
                if (item == null) throw new ArgumentException("Пустое определение материала.", nameof(definitions));
                string name = "MaterialDefinitions." + (MaterialTier)i + ".";
                NonNegative(item.CostMultiplier, name + nameof(item.CostMultiplier)); Positive(item.HealthMultiplier, name + nameof(item.HealthMultiplier));
                NonNegative(item.DamageMultiplier, name + nameof(item.DamageMultiplier)); Fraction(item.RefundFraction, name + nameof(item.RefundFraction));
                NonNegative(item.BuildTimeMultiplier, name + nameof(item.BuildTimeMultiplier));
            }
        }

        static void ValidateEnemies(List<EnemyDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentException("Отсутствуют определения врагов.", nameof(definitions));
            var seen = new HashSet<EnemyKind>();
            foreach (var item in definitions)
            {
                if (item == null || !Enum.IsDefined(typeof(EnemyKind), item.Kind) || !seen.Add(item.Kind))
                    throw new ArgumentException("Неизвестное, пустое или повторяющееся определение врага.", nameof(definitions));
                string name = "EnemyDefinitions." + item.Kind + ".";
                Positive(item.MaxHealth, name + nameof(item.MaxHealth)); NonNegative(item.Speed, name + nameof(item.Speed));
                NonNegative(item.Damage, name + nameof(item.Damage)); NonNegative(item.AttackInterval, name + nameof(item.AttackInterval));
                NonNegative(item.StructureDamageMultiplier, name + nameof(item.StructureDamageMultiplier)); NonNegative(item.Reward, name + nameof(item.Reward));
                NonNegative(item.Range, name + nameof(item.Range)); NonNegative(item.WindupSeconds, name + nameof(item.WindupSeconds));
                NonNegative(item.ProjectileSeconds, name + nameof(item.ProjectileSeconds));
            }
            if (seen.Count != Enum.GetValues(typeof(EnemyKind)).Length) throw new ArgumentException("Неполный каталог врагов.", nameof(definitions));
        }

        static void ValidateWaves(List<WaveDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentException("Отсутствует список волн.", nameof(definitions));
            foreach (var item in definitions)
            {
                if (item == null) throw new ArgumentException("Пустое определение волны.", nameof(definitions));
                if (item.InkGrant < -1) throw new ArgumentOutOfRangeException(nameof(item.InkGrant));
                NonNegative(item.Count, nameof(item.Count)); NonNegative(item.SpawnInterval, nameof(item.SpawnInterval));
                Fraction(item.RunnerFraction, nameof(item.RunnerFraction)); Fraction(item.GnawerFraction, nameof(item.GnawerFraction));
                Fraction(item.SpitterFraction, nameof(item.SpitterFraction)); Fraction(item.OutlinedFraction, nameof(item.OutlinedFraction));
                Fraction(item.HatchedFraction, nameof(item.HatchedFraction));
                if ((item.Sides & ~15) != 0) throw new ArgumentOutOfRangeException(nameof(item.Sides));
                // Overlapping fractions, zero sides and zero intervals keep the existing simulation clamps/priority.
            }
        }

        static void ValidateProgression(ProgressionRules rules)
        {
            NonNegative(rules.RapidFireMultiplier, nameof(rules.RapidFireMultiplier)); NonNegative(rules.DeepInkDamageMultiplier, nameof(rules.DeepInkDamageMultiplier));
            Positive(rules.DeepInkHealthMultiplier, nameof(rules.DeepInkHealthMultiplier)); NonNegative(rules.SalvageRefundBonus, nameof(rules.SalvageRefundBonus));
            NonNegative(rules.SalvageSupplyMultiplier, nameof(rules.SalvageSupplyMultiplier)); NonNegative(rules.SalvageWaveRewardMultiplier, nameof(rules.SalvageWaveRewardMultiplier));
            NonNegative(rules.SalvageImmediateInk, nameof(rules.SalvageImmediateInk)); Fraction(rules.MaximumRefundFraction, nameof(rules.MaximumRefundFraction));
            NonNegative(rules.LegacyHealthGrowthPerWave, nameof(rules.LegacyHealthGrowthPerWave));
            NonNegative(rules.LegacyHealthGrowthStartsAfterWave, nameof(rules.LegacyHealthGrowthStartsAfterWave));
            NonNegative(rules.EnemySpeedScaleMinimum, nameof(rules.EnemySpeedScaleMinimum)); NonNegative(rules.EnemySpeedScaleVariation, nameof(rules.EnemySpeedScaleVariation));
            NonNegative(rules.UpgradeEveryWaves, nameof(rules.UpgradeEveryWaves)); NonNegative(rules.LastUpgradeWave, nameof(rules.LastUpgradeWave));
            Fraction(rules.WellThreatFraction, nameof(rules.WellThreatFraction));
        }

        static void NonNegative(int value, string name)
        { if (value < 0) throw new ArgumentOutOfRangeException(name); }
        static void NonNegative(float value, string name)
        { if (value < 0 || float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(name); }
        static void Positive(float value, string name)
        { NonNegative(value, name); if (value == 0) throw new ArgumentOutOfRangeException(name); }
        static void Fraction(float value, string name)
        { NonNegative(value, name); if (value > 1) throw new ArgumentOutOfRangeException(name); }
    }
}
