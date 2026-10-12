using System;

namespace PaperDefense.Core
{
    /// <summary>Effects tuning shared by the live simulation and the training lane.</summary>
    [Serializable]
    public sealed class EffectsRules
    {
        public float OutlinedHealthMultiplier = 1.3f, HatchedHealthMultiplier = 1.7f;
        public float OutlinedDefense = .08f, HatchedDefense = .15f, MaximumDefense = .95f;
        public float RedDuration = 3, RedTickSeconds = .25f, RedDamagePerTick = 1;
        public bool RedIgnoresDefense = true;
        public float BlueDuration = 3, BlueSpeedMultiplier = .65f;
        public float GreenDuration = 3, GreenDefenseReduction = .2f;
        public bool LimitTowerColor;
        public float ColorWindowSeconds = 1;
        public int ColorTargetsPerWindow = 3;

        public EffectsRules CreateSnapshot()
        {
            Validate();
            return (EffectsRules)MemberwiseClone();
        }

        internal EffectsRules ForEnemyDrawingTiers(bool enabled)
        {
            if (enabled) return this;
            var copy = CreateSnapshot();
            copy.OutlinedDefense = copy.HatchedDefense = 0;
            return copy;
        }

        internal void Validate()
        {
            Positive(OutlinedHealthMultiplier, nameof(OutlinedHealthMultiplier));
            Positive(HatchedHealthMultiplier, nameof(HatchedHealthMultiplier));
            Fraction(OutlinedDefense, nameof(OutlinedDefense));
            Fraction(HatchedDefense, nameof(HatchedDefense));
            Fraction(MaximumDefense, nameof(MaximumDefense));
            if (MaximumDefense >= 1) throw new ArgumentOutOfRangeException(nameof(MaximumDefense));
            NonNegative(RedDuration, nameof(RedDuration));
            Positive(RedTickSeconds, nameof(RedTickSeconds));
            NonNegative(RedDamagePerTick, nameof(RedDamagePerTick));
            NonNegative(BlueDuration, nameof(BlueDuration));
            Fraction(BlueSpeedMultiplier, nameof(BlueSpeedMultiplier));
            NonNegative(GreenDuration, nameof(GreenDuration));
            Fraction(GreenDefenseReduction, nameof(GreenDefenseReduction));
            Positive(ColorWindowSeconds, nameof(ColorWindowSeconds));
            if (ColorTargetsPerWindow <= 0) throw new ArgumentOutOfRangeException(nameof(ColorTargetsPerWindow));
        }

        static void NonNegative(float value, string name)
        {
            if (value < 0 || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name);
        }
        static void Positive(float value, string name)
        {
            NonNegative(value, name);
            if (value == 0) throw new ArgumentOutOfRangeException(name);
        }
        static void Fraction(float value, string name)
        {
            NonNegative(value, name);
            if (value > 1) throw new ArgumentOutOfRangeException(name);
        }
    }

    public sealed partial class GameBalance
    {
        public EffectsRules Effects = new EffectsRules();
    }
}
