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
        public static ColorEffectPayload Capture(InkColor color,ContentCatalogSnapshot catalog)
        {
            if(color==InkColor.None)return default;
            if(!Enum.IsDefined(typeof(InkColor),color))throw new ArgumentOutOfRangeException(nameof(color));
            return Capture(catalog.GetEffect(color==InkColor.Red?catalog.RedEffectId:color==InkColor.Blue?catalog.BlueEffectId:catalog.GreenEffectId));
        }
        public static ColorEffectPayload Capture(EffectDefinition effect)
            =>new ColorEffectPayload(effect.Kind==EffectKind.DamageOverTime?InkColor.Red:effect.Kind==EffectKind.Slow?InkColor.Blue:InkColor.Green,effect.Duration,effect.Strength,effect.TickSeconds,effect.IgnoresDefense);
    }
}
