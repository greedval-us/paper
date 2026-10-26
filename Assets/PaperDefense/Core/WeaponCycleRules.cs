using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public sealed class WeaponCycleResult
    {
        public WeaponCycleState State { get; }
        public IReadOnlyList<float> ShotOffsets { get; }
        internal IReadOnlyList<WeaponCycleState> StatesAfterShots { get; }

        internal WeaponCycleResult(WeaponCycleState state, List<float> shots, List<WeaponCycleState> statesAfterShots)
        {
            State = state;
            ShotOffsets = new List<float>(shots).AsReadOnly();
            StatesAfterShots = new List<WeaponCycleState>(statesAfterShots).AsReadOnly();
        }
    }

    public static class WeaponCycleRules
    {
        internal static WeaponCycleState CreateState(ResolvedWeapon weapon)
        {
            ValidateWeapon(weapon);
            return new WeaponCycleState(weapon.MagazineSize, false, 0);
        }

        public static WeaponCycleResult Step(WeaponCycleState state, ResolvedWeapon weapon,
            float delta, bool targetAcquired, float feed)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            ValidateWeapon(weapon);
            if (!Finite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            if (!Finite(feed) || feed < 0) throw new ArgumentOutOfRangeException(nameof(feed));

            var shots = new List<float>();
            var shotStates = new List<WeaponCycleState>();
            if (delta == 0 || feed == 0) return new WeaponCycleResult(state, shots, shotStates);

            int rounds = state.Initialized ? state.RoundsRemaining : weapon.MagazineSize;
            bool reloading = state.IsReloading;
            double work = state.RemainingWork;
            double remaining = delta;
            double elapsed = 0;
            double intervalDuration = Math.Max(weapon.MinimumActionInterval, weapon.Interval / (double)feed);
            double reloadDuration = weapon.ReloadSeconds / (double)feed;

            while (true)
            {
                if (work > 0)
                {
                    double duration = reloading ? reloadDuration : intervalDuration;
                    double wait = work * duration;
                    // Float deltas can straddle an otherwise identical endpoint by a few ulps.
                    double tolerance = Math.Min(.000001, duration * .00001);
                    if (wait > remaining + tolerance)
                    {
                        work = Math.Max(0, work - remaining / duration);
                        break;
                    }

                    double consumed = Math.Min(wait, remaining);
                    elapsed += consumed;
                    remaining -= consumed;
                    if (remaining <= tolerance)
                    {
                        elapsed = delta;
                        remaining = 0;
                    }
                    work = 0;
                    if (reloading)
                    {
                        rounds = weapon.MagazineSize;
                        reloading = false;
                    }
                }

                if (!targetAcquired) break;

                rounds--;
                reloading = rounds == 0;
                work = 1;
                shots.Add((float)elapsed);
                shotStates.Add(new WeaponCycleState(rounds, reloading, work));
                if (remaining == 0) break;
            }

            return new WeaponCycleResult(new WeaponCycleState(rounds, reloading, work), shots, shotStates);
        }

        static void ValidateWeapon(ResolvedWeapon weapon)
        {
            if (weapon == null) throw new ArgumentNullException(nameof(weapon));
            if (weapon.MagazineSize <= 0 || !Positive(weapon.Interval) || !Positive(weapon.ReloadSeconds) ||
                !Positive(weapon.MinimumActionInterval))
                throw new ArgumentException("A weapon cycle requires a positive magazine size and finite positive timings.", nameof(weapon));
        }

        static bool Positive(float value) => Finite(value) && value > 0;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
