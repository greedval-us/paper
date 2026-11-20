using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public enum DefeatAdviceKind
    {
        None = 0,
        DefendObservedApproach = 1,
        RepositionUnfiredTower = 2,
        ReviewLossWave = 3
    }

    /// <summary>Measured observations for one attempt; no inferred spawn routes or counterfactual causes.</summary>
    public sealed class DefeatReport
    {
        public Guid RunId { get; }
        public bool HasEvidence => HasRecordedDamage || IdlePositions.Count != 0;
        public bool HasRecordedDamage => TotalBaseDamage > 0;
        public float TotalBaseDamage { get; }
        public WaveLossView? WorstWave { get; }
        public ApproachLossView? DangerousApproach { get; }
        public IReadOnlyList<PositionActivityView> IdlePositions { get; }
        public float UnattributedBaseDamage { get; }
        public int UnattributedBaseHits { get; }
        public bool TelemetryTruncated { get; }
        public DefeatAdviceKind Advice { get; }

        internal DefeatReport(Guid runId, float totalBaseDamage, WaveLossView? worstWave,
            ApproachLossView? dangerousApproach, List<PositionActivityView> idlePositions,
            float unattributedBaseDamage, int unattributedBaseHits, bool telemetryTruncated)
        {
            RunId = runId;
            TotalBaseDamage = totalBaseDamage;
            WorstWave = worstWave;
            DangerousApproach = dangerousApproach;
            IdlePositions = Array.AsReadOnly(idlePositions.ToArray());
            UnattributedBaseDamage = unattributedBaseDamage;
            UnattributedBaseHits = unattributedBaseHits;
            TelemetryTruncated = telemetryTruncated;
            Advice = dangerousApproach.HasValue ? DefeatAdviceKind.DefendObservedApproach :
                IdlePositions.Count != 0 ? DefeatAdviceKind.RepositionUnfiredTower :
                worstWave.HasValue ? DefeatAdviceKind.ReviewLossWave : DefeatAdviceKind.None;
        }
    }

    public static class DefeatAnalysis
    {
        const float MinimumActiveSeconds = 15;
        const int MaximumIdlePositions = 3;

        /// <summary>Build once at the result boundary. The returned values do not follow later simulation changes.</summary>
        public static DefeatReport Analyze(IRunReadModel run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            var statistics = run.Statistics ?? throw new ArgumentException("A run requires statistics.", nameof(run));

            WaveLossView? worstWave = null;
            foreach (var wave in statistics.WaveLosses)
            {
                if (wave.WaveIndex <= 0 || !PositiveFinite(wave.BaseDamage)) continue;
                if (!worstWave.HasValue || CompareWaves(wave, worstWave.Value) < 0) worstWave = wave;
            }

            ApproachLossView? dangerousApproach = null;
            foreach (var approach in statistics.ApproachLosses)
            {
                if (!PositiveFinite(approach.BaseDamage)) continue;
                if (!dangerousApproach.HasValue || CompareApproaches(approach, dangerousApproach.Value) < 0)
                    dangerousApproach = approach;
            }

            var candidates = new List<PositionActivityView>();
            foreach (var position in statistics.PositionActivities)
                if (position.StructureId > 0 && !position.Removed && !position.Destroyed && position.Launches == 0 &&
                    PositiveFinite(position.ActiveSeconds) && position.ActiveSeconds >= MinimumActiveSeconds)
                    candidates.Add(position);
            candidates.Sort(ComparePositions);
            var cells = new HashSet<Cell>();
            var idlePositions = new List<PositionActivityView>(MaximumIdlePositions);
            foreach (var position in candidates)
            {
                if (!cells.Add(position.Cell)) continue;
                idlePositions.Add(position);
                if (idlePositions.Count == MaximumIdlePositions) break;
            }

            return new DefeatReport(run.RunId, NonnegativeMeasurement(statistics.BaseDamageTaken), worstWave,
                dangerousApproach, idlePositions, NonnegativeMeasurement(statistics.UnattributedBaseDamage),
                Math.Max(0, statistics.UnattributedBaseHits), statistics.DefeatTelemetryTruncated);
        }

        static bool PositiveFinite(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
        static float NonnegativeMeasurement(float value) => PositiveFinite(value) ? value : 0;

        static int CompareWaves(WaveLossView first, WaveLossView second)
        {
            int result = second.BaseDamage.CompareTo(first.BaseDamage);
            if (result == 0) result = second.DestroyedStructures.CompareTo(first.DestroyedStructures);
            if (result == 0) result = second.LostInvestment.CompareTo(first.LostInvestment);
            return result != 0 ? result : first.WaveIndex.CompareTo(second.WaveIndex);
        }

        static int CompareApproaches(ApproachLossView first, ApproachLossView second)
        {
            int result = second.BaseDamage.CompareTo(first.BaseDamage);
            if (result == 0) result = second.Hits.CompareTo(first.Hits);
            return result != 0 ? result : CompareCells(first.Cell, second.Cell);
        }

        static int ComparePositions(PositionActivityView first, PositionActivityView second)
        {
            int result = second.ActiveSeconds.CompareTo(first.ActiveSeconds);
            if (result == 0) result = CompareCells(first.Cell, second.Cell);
            return result != 0 ? result : first.StructureId.CompareTo(second.StructureId);
        }

        static int CompareCells(Cell first, Cell second)
        {
            int result = first.Y.CompareTo(second.Y);
            return result != 0 ? result : first.X.CompareTo(second.X);
        }
    }
}
