using System;

namespace PaperDefense.Core
{
    // Retains the existing seeded System.Random algorithm. All session call sites use
    // one-sample overloads; keeping the inner instance separate avoids virtual re-entry.
    internal sealed class CheckpointRandom : Random
    {
        internal const long MaximumReplayDraws = 10000000;
        readonly Random inner;
        internal int Seed { get; }
        internal long DrawCount { get; private set; }
        internal CheckpointRandom(int seed) { Seed = seed; inner = new Random(seed); }
        public override int Next() { DrawCount++; return inner.Next(); }
        public override int Next(int maxValue)
        {
            if (maxValue < 0) throw new ArgumentOutOfRangeException(nameof(maxValue));
            DrawCount++; return inner.Next(maxValue);
        }
        public override int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));
            // The large-range legacy overload consumes two samples. Session callers use
            // small map coordinates, but account for it without changing Random behavior.
            DrawCount += (long)maxValue - minValue > int.MaxValue ? 2 : 1;
            return inner.Next(minValue, maxValue);
        }
        public override double NextDouble() { DrawCount++; return inner.NextDouble(); }
        public override void NextBytes(byte[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            DrawCount += buffer.Length; inner.NextBytes(buffer);
        }
        internal void RestoreDrawCount(long count)
        {
            if (DrawCount != 0 || count < 0 || count > MaximumReplayDraws) throw new ArgumentOutOfRangeException(nameof(count));
            for (long i = 0; i < count; i++) inner.Next();
            DrawCount = count;
        }
    }
}
