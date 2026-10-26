using System;

namespace PaperDefense.Core
{
    public static class MetaProgression
    {
        static readonly int[] thresholds = { 0, 100, 230, 390, 580, 800, 1050, 1330 };

        public static int LevelForXp(int totalXp)
        {
            if (totalXp < 0) throw new ArgumentOutOfRangeException(nameof(totalXp));
            for (int index = thresholds.Length - 1; index > 0; index--)
                if (totalXp >= thresholds[index]) return index + 1;
            return 1;
        }

        /// <summary>A read-only view of the same level thresholds used by profile progression.</summary>
        public static MetaLevelProgress GetLevelProgress(int totalXp)
        {
            int level = LevelForXp(totalXp);
            int? next = level < thresholds.Length ? thresholds[level] : (int?)null;
            return new MetaLevelProgress(totalXp, level, thresholds[level - 1], next);
        }
    }

    public readonly struct MetaLevelProgress
    {
        public int TotalXp { get; }
        public int Level { get; }
        public int LevelStartXp { get; }
        public int? NextLevelXp { get; }
        public bool IsMaximum => !NextLevelXp.HasValue;
        public float Normalized => IsMaximum ? 1 : (float)(TotalXp - LevelStartXp) / (NextLevelXp.Value - LevelStartXp);

        internal MetaLevelProgress(int totalXp, int level, int start, int? next)
        { TotalXp = totalXp; Level = level; LevelStartXp = start; NextLevelXp = next; }
    }
}
