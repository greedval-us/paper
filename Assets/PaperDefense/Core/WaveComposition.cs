using System;

namespace PaperDefense.Core
{
    /// <summary>Effective kind and drawing-tier weights for one wave, after applying the run's rules.</summary>
    public readonly struct WaveComposition
    {
        readonly float brutes, bulwarks, gnawers, runners, spitters;
        readonly float hatched, armored;

        internal WaveComposition(WaveDefinition wave, MechanicsAvailability mechanics)
        {
            // Oversubscribed historical presets admit these kinds in this order.
            // Preserve the order of the float sums used by the seeded simulation.
            brutes = Limit(wave.BruteFraction, 1);
            bulwarks = Limit(wave.BulwarkFraction, 1 - brutes);
            gnawers = Limit(wave.GnawerFraction, 1 - brutes - bulwarks);
            runners = Limit(wave.RunnerFraction, 1 - gnawers - brutes - bulwarks);
            spitters = mechanics.RangedEnemies
                ? Limit(wave.SpitterFraction, 1 - gnawers - runners - brutes - bulwarks)
                : 0;
            hatched = mechanics.EnemyDrawingTiers ? Limit(wave.HatchedFraction, 1) : 0;
            armored = mechanics.EnemyDrawingTiers ? Limit(wave.HatchedFraction + wave.OutlinedFraction, 1) : 0;
        }

        public float Fraction(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Doodle: return Math.Max(0, 1 - (brutes + bulwarks) - (gnawers + runners + spitters));
                case EnemyKind.Brute: return brutes;
                case EnemyKind.Bulwark: return bulwarks;
                case EnemyKind.Gnawer: return gnawers;
                case EnemyKind.Runner: return runners;
                case EnemyKind.Spitter: return spitters;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public float Fraction(DrawingTier tier)
        {
            switch (tier)
            {
                case DrawingTier.Sketch: return 1 - armored;
                case DrawingTier.Outlined: return armored - hatched;
                case DrawingTier.Hatched: return hatched;
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }

        internal EnemyKind SelectKind(double roll)
        {
            if (roll < brutes) return EnemyKind.Brute;
            if (roll < brutes + bulwarks) return EnemyKind.Bulwark;
            roll -= brutes + bulwarks;
            if (roll < gnawers) return EnemyKind.Gnawer;
            if (roll < gnawers + runners) return EnemyKind.Runner;
            if (roll < gnawers + runners + spitters) return EnemyKind.Spitter;
            return EnemyKind.Doodle;
        }

        internal DrawingTier SelectTier(double roll)
        {
            if (roll < hatched) return DrawingTier.Hatched;
            return roll < armored ? DrawingTier.Outlined : DrawingTier.Sketch;
        }

        static float Limit(float value, float remaining) => Math.Max(0, Math.Min(remaining, value));
    }
}
