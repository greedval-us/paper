using System;

namespace PaperDefense.Core
{
    /// <summary>Adjustable first calibration of the finite-economy workshop preset.</summary>
    [Serializable] public sealed class TargetPresetSettings
    {
        public int WaveInkBase = 140, WaveInkIncrement = 20;
        public float MaterialDamageMultiplier = 1, ArmorHealthMultiplier = 1, ArmorDefense = .25f, MaximumDefense = .6f;
        public float DynamicFeedCapacity = 1.6f;
        public float ColorWindowSeconds = 1;
        public int ColorTargetsPerWindow = 3;
        public float SalvageSupplyMultiplier = 1, SalvageWaveRewardMultiplier = 1.25f;
        public TargetPresetSettings CreateSnapshot()
        {
            NonNegative(WaveInkBase, nameof(WaveInkBase)); NonNegative(WaveInkIncrement, nameof(WaveInkIncrement));
            NonNegative(MaterialDamageMultiplier, nameof(MaterialDamageMultiplier)); Positive(ArmorHealthMultiplier, nameof(ArmorHealthMultiplier));
            Fraction(ArmorDefense, nameof(ArmorDefense)); Fraction(MaximumDefense, nameof(MaximumDefense));
            if (MaximumDefense >= 1) throw new ArgumentOutOfRangeException(nameof(MaximumDefense));
            Positive(DynamicFeedCapacity, nameof(DynamicFeedCapacity)); Positive(ColorWindowSeconds, nameof(ColorWindowSeconds));
            if (ColorTargetsPerWindow <= 0) throw new ArgumentOutOfRangeException(nameof(ColorTargetsPerWindow));
            NonNegative(SalvageSupplyMultiplier, nameof(SalvageSupplyMultiplier));
            NonNegative(SalvageWaveRewardMultiplier, nameof(SalvageWaveRewardMultiplier));
            return (TargetPresetSettings)MemberwiseClone();
        }
        static void NonNegative(float value, string name)
        { if (value < 0 || float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(name); }
        static void Positive(float value, string name)
        { NonNegative(value, name); if (value == 0) throw new ArgumentOutOfRangeException(name); }
        static void Fraction(float value, string name)
        { NonNegative(value, name); if (value > 1) throw new ArgumentOutOfRangeException(name); }
    }
}
