using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>One source tower's shared allowance, retained by its flying shells.</summary>
    internal sealed class TowerColorBudget
    {
        readonly float windowSeconds;
        readonly int targetLimit;
        readonly List<int> affected;
        double nextWindow;
        bool hasWindow;

        internal TowerColorBudget(float windowSeconds, int targetLimit)
        {
            if (windowSeconds <= 0 || float.IsNaN(windowSeconds) || float.IsInfinity(windowSeconds))
                throw new ArgumentOutOfRangeException(nameof(windowSeconds));
            if (targetLimit <= 0) throw new ArgumentOutOfRangeException(nameof(targetLimit));
            this.windowSeconds = windowSeconds; this.targetLimit = targetLimit;
            affected = new List<int>(Math.Min(16, targetLimit));
        }
        internal bool TryApply(int targetId, double timeSeconds)
        {
            if (!hasWindow || timeSeconds >= nextWindow)
            {
                affected.Clear(); nextWindow = timeSeconds + windowSeconds; hasWindow = true;
            }
            if (affected.Count >= targetLimit || affected.Contains(targetId)) return false;
            affected.Add(targetId); return true;
        }
    }
}
