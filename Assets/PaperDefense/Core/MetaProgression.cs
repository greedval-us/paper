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
    }
}
