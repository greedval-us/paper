using System;

namespace PaperDefense.Core
{
    /// <summary>Historical run progression and upgrade tuning shared by every simulation.</summary>
    [Serializable]
    public sealed class ProgressionRules
    {
        public float RapidFireMultiplier = 1.22f;
        public float DeepInkDamageMultiplier = 1.18f;
        public float DeepInkHealthMultiplier = 1.25f;
        public float SalvageRefundBonus = .12f;
        public float SalvageSupplyMultiplier = 1.25f;
        public float SalvageWaveRewardMultiplier = 1;
        public bool RequireAssignedEnemyRewards;
        public int SalvageImmediateInk = 60;
        public float MaximumRefundFraction = .95f;
        public float LegacyHealthGrowthPerWave = .06f;
        public int LegacyHealthGrowthStartsAfterWave = 3;
        public float EnemySpeedScaleMinimum = .92f;
        public float EnemySpeedScaleVariation = .16f;
        public int UpgradeEveryWaves = 2;
        public int LastUpgradeWave = 6;
        public float WellThreatFraction = .3f;

        public ProgressionRules CreateSnapshot()
        {
            if (SalvageWaveRewardMultiplier < 0 || float.IsNaN(SalvageWaveRewardMultiplier) || float.IsInfinity(SalvageWaveRewardMultiplier))
                throw new ArgumentOutOfRangeException(nameof(SalvageWaveRewardMultiplier));
            return (ProgressionRules)MemberwiseClone();
        }
    }

    public sealed partial class GameBalance
    {
        public ProgressionRules Progression = new ProgressionRules();
    }
}
