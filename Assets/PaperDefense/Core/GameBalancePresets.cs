using System;

namespace PaperDefense.Core
{
    public static class GameBalancePresets
    {
        public const int DefaultWorkshopStartingInk = 720, DefaultWorkshopWidth = 96, DefaultWorkshopHeight = 64;
        /// <summary>Transform fresh configured tuning; an explicit per-wave grant overrides the target pattern.</summary>
        public static GameBalance ApplyTargetWorkshop(GameBalance source, TargetPresetSettings settings = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var tuning = (settings ?? new TargetPresetSettings()).CreateSnapshot();
            var result = source.CreateSnapshot();
            result.Preset = GameBalancePreset.Target;
            result.PassiveInkPerSecond = 0; result.EarlyLaunchInkPerSecond = 0; result.MaxEarlyLaunchInk = 0;
            for (int i = 0; i < result.Waves.Count; i++)
                if (result.Waves[i].InkGrant < 0)
                    result.Waves[i].InkGrant = (int)Math.Min(int.MaxValue, (long)tuning.WaveInkBase + (long)tuning.WaveInkIncrement * i);
            foreach (var material in result.MaterialDefinitions) material.DamageMultiplier = tuning.MaterialDamageMultiplier;
            result.Construction.ArmorHealthMultiplier = tuning.ArmorHealthMultiplier;
            result.Construction.ArmorDefense = tuning.ArmorDefense;
            result.Construction.DynamicFeed = true; result.Construction.DynamicFeedCapacity = tuning.DynamicFeedCapacity;
            result.Effects.MaximumDefense = tuning.MaximumDefense;
            result.Effects.LimitTowerColor = true; result.Effects.ColorWindowSeconds = tuning.ColorWindowSeconds;
            result.Effects.ColorTargetsPerWindow = tuning.ColorTargetsPerWindow;
            result.Progression.RequireAssignedEnemyRewards = true;
            result.Progression.SalvageSupplyMultiplier = tuning.SalvageSupplyMultiplier;
            result.Progression.SalvageWaveRewardMultiplier = tuning.SalvageWaveRewardMultiplier;
            return result;
        }
        public static GameBalance CreateTargetWorkshop(TargetPresetSettings settings = null)
        {
            var balance = GameBalance.CreateDefault();
            balance.Width = DefaultWorkshopWidth; balance.Height = DefaultWorkshopHeight; balance.StartingInk = DefaultWorkshopStartingInk;
            balance.EnableEnemyMechanics = true; balance.EnableInkWells = true;
            return ApplyTargetWorkshop(balance, settings);
        }
    }
}
