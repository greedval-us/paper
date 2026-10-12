using System;

namespace PaperDefense.Core
{
    /// <summary>Color parameters captured when an attack is launched.</summary>
    public readonly struct ColorEffectPayload
    {
        public readonly InkColor Color;
        public readonly float Duration, Strength, TickSeconds;
        public readonly bool IgnoresDefense;

        ColorEffectPayload(InkColor color, float duration, float strength, float tickSeconds, bool ignoresDefense)
        { Color = color; Duration = duration; Strength = strength; TickSeconds = tickSeconds; IgnoresDefense = ignoresDefense; }

        public static ColorEffectPayload Capture(InkColor color, EffectsRules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            switch (color)
            {
                case InkColor.None: return default;
                case InkColor.Red: return new ColorEffectPayload(color, rules.RedDuration, rules.RedDamagePerTick, rules.RedTickSeconds, rules.RedIgnoresDefense);
                case InkColor.Blue: return new ColorEffectPayload(color, rules.BlueDuration, rules.BlueSpeedMultiplier, 0, false);
                case InkColor.Green: return new ColorEffectPayload(color, rules.GreenDuration, rules.GreenDefenseReduction, 0, false);
                default: throw new ArgumentOutOfRangeException(nameof(color));
            }
        }

        internal static ColorEffectPayload Slow(float multiplier, float duration)
            => new ColorEffectPayload(InkColor.Blue, duration, multiplier, 0, false);
    }
}
