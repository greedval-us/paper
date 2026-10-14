using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>Independent lifetimes for different strengths of a single effect.</summary>
    internal sealed class TimedEffectState
    {
        internal const int MaximumStrengths = 16;
        const float Epsilon = .000001f;
        readonly InkColor color;
        readonly List<Entry> entries = new List<Entry>(2);

        sealed class Entry
        {
            internal ColorEffectPayload Payload;
            internal StructureKind Weapon;
            internal string WeaponId;
            internal float Remaining, TickRemaining;
        }

        internal TimedEffectState(InkColor color) { this.color = color; }
        internal bool HasActive => entries.Count > 0;
        internal float Remaining => Strongest()?.Remaining ?? 0;
        internal float TickRemaining => Strongest()?.TickRemaining ?? 0;
        internal ColorEffectPayload Active => Strongest()?.Payload ?? default;
        internal StructureKind Weapon => Strongest()?.Weapon ?? StructureKind.Gun;

        internal string WeaponId=>Strongest()?.WeaponId;
        internal bool TryApply(ColorEffectPayload payload, StructureKind weapon,string weaponId=null)
        {
            Validate(payload);
            if (payload.Color != color) throw new ArgumentException(UserText.Message("copy.36d7a57dd7d85a7f"), nameof(payload));
            if (payload.Duration == 0) return true;
            foreach (Entry entry in entries)
            {
                if (SameStrength(entry.Payload, payload))
                {
                    entry.Remaining = Math.Max(entry.Remaining, payload.Duration);
                    entry.Weapon = weapon;entry.WeaponId=weaponId??LegacyContentIds.Weapon(weapon);
                    return true;
                }
                if (Compare(entry.Payload, payload) >= 0 && entry.Remaining >= payload.Duration) return true;
            }
            for (int i = entries.Count - 1; i >= 0; i--)
                if (Compare(payload, entries[i].Payload) >= 0 && payload.Duration >= entries[i].Remaining) entries.RemoveAt(i);
            if (entries.Count >= MaximumStrengths) return false;
            entries.Add(new Entry { Payload = payload, Weapon = weapon,WeaponId=weaponId??LegacyContentIds.Weapon(weapon), Remaining = payload.Duration, TickRemaining = payload.TickSeconds });
            return true;
        }

        internal (StructureKind Weapon, string WeaponId) Advance(float delta, EnemyState enemy, EffectsRules rules, RunStatistics statistics, float blotReduction)
        {
            var source = (Weapon, WeaponId);
            if (color != InkColor.Red)
            {
                foreach (Entry entry in entries) entry.Remaining = Math.Max(0, entry.Remaining - delta);
                RemoveExpired();
                return source;
            }

            double remaining = delta;
            while (remaining > .0000001 && entries.Count > 0 && enemy.Health > 0)
            {
                Entry active = Strongest();
                float step = (float)remaining;
                foreach (Entry entry in entries) step = Math.Min(step, Math.Min(entry.Remaining, entry.TickRemaining));
                foreach (Entry entry in entries) { entry.Remaining -= step; entry.TickRemaining -= step; }
                // A pulse exactly at expiry belongs to the effect that was active during this interval.
                if (active.TickRemaining <= Epsilon)
                {
                    float raw = active.Payload.Strength;
                    float damage = active.Payload.IgnoresDefense ? raw :
                        DamageRules.Apply(raw, EnemyEffects.Defense(enemy, rules, blotReduction), rules.MaximumDefense);
                    float actual = Math.Min(enemy.Health, damage);
                    enemy.Health = Math.Max(0, enemy.Health - actual);
                    statistics?.RecordDamage(active.WeaponId, actual);
                    if (actual > 0) source = (active.Weapon, active.WeaponId);
                }
                foreach (Entry entry in entries)
                    if (entry.TickRemaining <= Epsilon) entry.TickRemaining += entry.Payload.TickSeconds;
                RemoveExpired();
                remaining -= step;
            }
            if (enemy.Health <= 0) entries.Clear();
            // Aggregate hit feedback uses the last pulse's actual source, even if
            // that source expired or killed the enemy before this advance ended.
            return source;
        }

        Entry Strongest()
        {
            Entry strongest = null;
            foreach (Entry entry in entries)
                if (strongest == null || Compare(entry.Payload, strongest.Payload) > 0) strongest = entry;
            return strongest;
        }

        void RemoveExpired()
        { for (int i = entries.Count - 1; i >= 0; i--) if (entries[i].Remaining <= Epsilon) entries.RemoveAt(i); }

        static bool SameStrength(ColorEffectPayload a, ColorEffectPayload b)
            => a.Strength == b.Strength && a.TickSeconds == b.TickSeconds && a.IgnoresDefense == b.IgnoresDefense;

        static int Compare(ColorEffectPayload a, ColorEffectPayload b)
        {
            if (a.Color == InkColor.Blue) return b.Strength.CompareTo(a.Strength);
            float first = a.Color == InkColor.Red ? a.Strength / a.TickSeconds : a.Strength;
            float second = b.Color == InkColor.Red ? b.Strength / b.TickSeconds : b.Strength;
            int result = first.CompareTo(second);
            return result != 0 || a.Color != InkColor.Red ? result : a.IgnoresDefense.CompareTo(b.IgnoresDefense);
        }

        internal static void Validate(ColorEffectPayload payload)
        {
            NonNegative(payload.Duration, nameof(payload.Duration));
            NonNegative(payload.Strength, nameof(payload.Strength));
            if ((payload.Color == InkColor.Blue || payload.Color == InkColor.Green) && payload.Strength > 1)
                throw new ArgumentOutOfRangeException(nameof(payload.Strength));
            if (payload.Color == InkColor.Red && (payload.TickSeconds <= 0 || float.IsNaN(payload.TickSeconds) || float.IsInfinity(payload.TickSeconds)))
                throw new ArgumentOutOfRangeException(nameof(payload.TickSeconds));
        }

        static void NonNegative(float value, string name)
        { if (value < 0 || float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(name); }
    }
}
