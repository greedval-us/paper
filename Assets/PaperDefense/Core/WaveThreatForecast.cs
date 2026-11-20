using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public enum WaveThreat { Crowd, Runners, Armor, Breach, Ranged }

    /// <summary>Tactical assessment of the enemies that the spawner can actually admit.</summary>
    public readonly struct WaveThreatForecast
    {
        public WaveThreat Primary { get; }
        public float PrimaryFraction { get; }
        public float RunnerFraction { get; }
        public float ArmoredFraction { get; }
        public float RangedFraction { get; }
        public bool OnlySpittersFireGlobs { get; }
        public float MaximumArrivalsPerSecond { get; }

        internal WaveThreatForecast(WaveComposition composition, float effectiveSpawnInterval,
            IReadOnlyList<EnemyDefinition> enemies, bool rangedEnabled)
        {
            RunnerFraction = composition.Fraction(EnemyKind.Runner);
            ArmoredFraction = composition.Fraction(DrawingTier.Hatched) + composition.Fraction(DrawingTier.Outlined);
            MaximumArrivalsPerSecond = effectiveSpawnInterval > 0 ? 1f / effectiveSpawnInterval : 0;
            float gnawers = composition.Fraction(EnemyKind.Gnawer);
            float ranged = 0;
            bool otherKindsFireGlobs = false;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (!EnemyAttackSystem.UsesGlob(enemy, rangedEnabled)) continue;
                float share = composition.Fraction(enemy.Kind);
                ranged += share;
                if (share > 0 && enemy.Kind != EnemyKind.Spitter) otherKindsFireGlobs = true;
            }
            RangedFraction = Math.Min(1, ranged);
            OnlySpittersFireGlobs = RangedFraction > 0 && !otherKindsFireGlobs;

            // The scouting scale promotes armor at a rounded 45%. Kind and drawing tier may overlap.
            if (Math.Round(100 * ArmoredFraction) >= 45)
            {
                Primary = WaveThreat.Armor;
                PrimaryFraction = ArmoredFraction;
            }
            else if (gnawers >= .15f)
            {
                Primary = WaveThreat.Breach;
                PrimaryFraction = gnawers;
            }
            else if (RangedFraction >= .075f)
            {
                Primary = WaveThreat.Ranged;
                PrimaryFraction = RangedFraction;
            }
            else if (RunnerFraction >= .25f)
            {
                Primary = WaveThreat.Runners;
                PrimaryFraction = RunnerFraction;
            }
            else
            {
                Primary = WaveThreat.Crowd;
                PrimaryFraction = composition.Fraction(EnemyKind.Doodle);
            }
        }
    }
}
