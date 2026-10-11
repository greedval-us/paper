using System;

namespace PaperDefense.Application
{
    /// <summary>
    /// Application time policy: six 30 Hz steps at most per render frame. Excess
    /// time is discarded explicitly, so an overloaded single-player run slows
    /// down without carrying an ever-growing catch-up debt.
    /// </summary>
    public sealed class FixedStepClock
    {
        public const float Step = 1f / 30f;
        public const int MaxCatchUpSteps = 6;
        const double Epsilon = 0.0000001;
        double accumulated;

        public double AccumulatedSeconds => accumulated;
        public double DiscardedSeconds { get; private set; }
        public long FramesWithDiscardedTime { get; private set; }

        public int AdvanceFrame(float realDelta, bool paused, int speed)
        {
            if (paused) { accumulated = 0; return 0; }
            if (float.IsNaN(realDelta) || float.IsInfinity(realDelta) || realDelta <= 0) return 0;
            double requested = accumulated + realDelta * (speed >= 2 ? 2d : 1d);
            double limit = Step * (double)MaxCatchUpSteps;
            if (requested > limit + Epsilon)
            {
                DiscardedSeconds += requested - limit;
                FramesWithDiscardedTime++;
                requested = limit;
            }
            int steps = Math.Min(MaxCatchUpSteps, (int)((requested + Epsilon) / Step));
            accumulated = Math.Max(0, requested - steps * (double)Step);
            return steps;
        }

        public void ClearDebt() => accumulated = 0;
        public void Reset()
        {
            accumulated = 0;
            DiscardedSeconds = 0;
            FramesWithDiscardedTime = 0;
        }
    }
}
