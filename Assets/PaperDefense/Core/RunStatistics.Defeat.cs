using System;
using System.Collections.Generic;
using System.Linq;

namespace PaperDefense.Core
{
    public readonly struct WaveLossView
    {
        public int WaveIndex { get; }
        public float BaseDamage { get; }
        public int DestroyedStructures { get; }
        public int LostInvestment { get; }
        public WaveLossView(int waveIndex, float baseDamage, int destroyedStructures, int lostInvestment)
        { WaveIndex = waveIndex; BaseDamage = baseDamage; DestroyedStructures = destroyedStructures; LostInvestment = lostInvestment; }
    }

    public readonly struct ApproachLossView
    {
        public Cell Cell { get; }
        public float BaseDamage { get; }
        public int Hits { get; }
        public ApproachLossView(Cell cell, float baseDamage, int hits)
        { Cell = cell; BaseDamage = baseDamage; Hits = hits; }
    }

    public readonly struct PositionActivityView
    {
        public int StructureId { get; }
        public Cell Cell { get; }
        public string ContentId { get; }
        public float ActiveSeconds { get; }
        public int Launches { get; }
        public bool Destroyed { get; }
        public bool Removed { get; }
        public PositionActivityView(int structureId, Cell cell, string contentId, float activeSeconds, int launches, bool destroyed, bool removed)
        { StructureId = structureId; Cell = cell; ContentId = contentId; ActiveSeconds = activeSeconds; Launches = launches; Destroyed = destroyed; Removed = removed; }
    }

    public sealed partial class RunStatistics
    {
        const int MaximumWaveLossRecords = 2048, MaximumApproachLossRecords = 512, MaximumPositionRecords = 512;
        sealed class WaveLoss { internal float Damage; internal int Destroyed, Lost; }
        sealed class ApproachLoss { internal float Damage; internal int Hits; }
        sealed class PositionActivity
        {
            internal int Id, Launches;
            internal Cell Cell;
            internal string ContentId;
            internal float ActiveSeconds;
            internal bool Destroyed, Removed;
        }
        readonly Dictionary<int, WaveLoss> waveLosses = new Dictionary<int, WaveLoss>();
        readonly Dictionary<Cell, ApproachLoss> approachLosses = new Dictionary<Cell, ApproachLoss>();
        readonly Dictionary<int, PositionActivity> positionActivities = new Dictionary<int, PositionActivity>();
        public float UnattributedBaseDamage { get; private set; }
        public int UnattributedBaseHits { get; private set; }
        public bool DefeatTelemetryTruncated { get; private set; }

        // These copies are made only when the report asks for a snapshot, never during a simulation tick.
        public IReadOnlyList<WaveLossView> WaveLosses => Array.AsReadOnly(waveLosses.OrderBy(item => item.Key)
            .Select(item => new WaveLossView(item.Key, item.Value.Damage, item.Value.Destroyed, item.Value.Lost)).ToArray());
        public IReadOnlyList<ApproachLossView> ApproachLosses => Array.AsReadOnly(approachLosses.OrderBy(item => item.Key.Y).ThenBy(item => item.Key.X)
            .Select(item => new ApproachLossView(item.Key, item.Value.Damage, item.Value.Hits)).ToArray());
        public IReadOnlyList<PositionActivityView> PositionActivities => Array.AsReadOnly(positionActivities.OrderBy(item => item.Key)
            .Select(item => new PositionActivityView(item.Key, item.Value.Cell, item.Value.ContentId, item.Value.ActiveSeconds,
                item.Value.Launches, item.Value.Destroyed, item.Value.Removed)).ToArray());

        WaveLoss WaveLossFor(int waveIndex)
        {
            if (waveIndex <= 0) return null;
            if (waveLosses.TryGetValue(waveIndex, out var existing)) return existing;
            if (waveLosses.Count == MaximumWaveLossRecords) { DefeatTelemetryTruncated = true; return null; }
            var created = new WaveLoss(); waveLosses.Add(waveIndex, created); return created;
        }

        public void RecordBaseDamage(float actual, int waveIndex, Point2? source)
        {
            if (!(actual > 0) || float.IsNaN(actual) || float.IsInfinity(actual)) return;
            BaseHits = AddCount(BaseHits, 1);
            BaseDamageTaken = AddMeasurement(BaseDamageTaken, actual);
            var wave = WaveLossFor(waveIndex);
            if (wave != null) wave.Damage = AddMeasurement(wave.Damage, actual);
            if (TrySourceCell(source, out var cell))
            {
                if (!approachLosses.TryGetValue(cell, out var approach))
                {
                    if (approachLosses.Count < MaximumApproachLossRecords)
                    { approach = new ApproachLoss(); approachLosses.Add(cell, approach); }
                    else DefeatTelemetryTruncated = true;
                }
                if (approach != null)
                {
                    approach.Damage = AddMeasurement(approach.Damage, actual);
                    approach.Hits = AddCount(approach.Hits, 1);
                    return;
                }
            }
            UnattributedBaseDamage = AddMeasurement(UnattributedBaseDamage, actual);
            UnattributedBaseHits = AddCount(UnattributedBaseHits, 1);
        }

        static bool TrySourceCell(Point2? source, out Cell cell)
        {
            cell = default;
            if (!source.HasValue) return false;
            double x = source.Value.X, y = source.Value.Y;
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y) ||
                x < int.MinValue || x >= (double)int.MaxValue + 1 || y < int.MinValue || y >= (double)int.MaxValue + 1) return false;
            cell = new Cell((int)Math.Floor(x), (int)Math.Floor(y)); return true;
        }

        void RecordWaveStructureLoss(int waveIndex, int investment)
        {
            var wave = WaveLossFor(waveIndex);
            if (wave == null) return;
            wave.Destroyed = AddCount(wave.Destroyed, 1);
            wave.Lost = AddCount(wave.Lost, investment);
        }

        PositionActivity PositionFor(int structureId, Cell cell, string contentId)
        {
            if (structureId <= 0) return null;
            if (positionActivities.TryGetValue(structureId, out var existing)) return existing;
            if (positionActivities.Count == MaximumPositionRecords) { DefeatTelemetryTruncated = true; return null; }
            var created = new PositionActivity { Id = structureId, Cell = cell, ContentId = contentId };
            positionActivities.Add(structureId, created); return created;
        }

        public void RecordPositionActive(int structureId, Cell cell, string contentId, float seconds)
        {
            if (!(seconds > 0) || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            var position = PositionFor(structureId, cell, contentId);
            if (position != null && !position.Removed) position.ActiveSeconds = AddMeasurement(position.ActiveSeconds, seconds);
        }

        public void RecordPositionLaunch(int structureId, Cell cell, string contentId)
        {
            var position = PositionFor(structureId, cell, contentId);
            if (position != null && !position.Removed) position.Launches = AddCount(position.Launches, 1);
        }

        public void RecordPositionRemoved(int structureId, bool destroyed)
        {
            if (!positionActivities.TryGetValue(structureId, out var position)) return;
            position.Removed = true; position.Destroyed |= destroyed;
        }
    }

    public sealed partial class RunStatisticsView
    {
        public IReadOnlyList<WaveLossView> WaveLosses => source.WaveLosses;
        public IReadOnlyList<ApproachLossView> ApproachLosses => source.ApproachLosses;
        public IReadOnlyList<PositionActivityView> PositionActivities => source.PositionActivities;
        public float UnattributedBaseDamage => source.UnattributedBaseDamage;
        public int UnattributedBaseHits => source.UnattributedBaseHits;
        public bool DefeatTelemetryTruncated => source.DefeatTelemetryTruncated;
    }
}
