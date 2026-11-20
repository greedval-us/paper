using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>One source tower's shared allowance, retained by its flying shells.</summary>
    internal sealed class TowerColorBudget
    {
        readonly float windowSeconds;
        int targetLimit;
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
        internal void SetTargetLimit(int limit)
        { if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit)); targetLimit = limit; }
        internal bool TryApply(int targetId, double timeSeconds)
        {
            if (!hasWindow || timeSeconds >= nextWindow)
            {
                affected.Clear(); nextWindow = timeSeconds + windowSeconds; hasWindow = true;
            }
            if (affected.Count >= targetLimit || affected.Contains(targetId)) return false;
            affected.Add(targetId); return true;
        }
        internal ColorBudgetCheckpointData CaptureCheckpoint() => new ColorBudgetCheckpointData {
            WindowSeconds = windowSeconds, TargetLimit = targetLimit, HasWindow = hasWindow,
            NextWindow = nextWindow, Affected = affected.ToArray()
        };
        internal static TowerColorBudget RestoreCheckpoint(ColorBudgetCheckpointData data, int nextEnemyId)
        {
            if (data == null || data.TargetLimit <= 0 || data.TargetLimit > 65536 ||
                data.Affected == null || data.Affected.Length > data.TargetLimit ||
                double.IsNaN(data.NextWindow) || double.IsInfinity(data.NextWindow) || data.NextWindow < 0 ||
                !data.HasWindow && (data.Affected.Length != 0 || data.NextWindow != 0))
                throw new ArgumentException("Invalid color budget checkpoint.");
            var result = new TowerColorBudget(data.WindowSeconds, data.TargetLimit);
            var unique = new HashSet<int>();
            foreach (int id in data.Affected)
                if (id <= 0 || id >= nextEnemyId || !unique.Add(id)) throw new ArgumentException("Invalid color budget target.");
            result.hasWindow = data.HasWindow; result.nextWindow = data.NextWindow;
            result.affected.AddRange(data.Affected); return result;
        }
    }
}
