using System;
using System.Collections.Generic;
using PaperDefense.Core;

namespace PaperDefense.Presentation
{
    /// <summary>Reconstructs only bounded visual state from interval events; seeking resets all cursors.</summary>
    public sealed class NotebookTrialPlayback
    {
        readonly List<SimulationEvent> effects = new List<SimulationEvent>(256);
        readonly List<SimulationEvent> projectiles = new List<SimulationEvent>(128);
        readonly HashSet<int> deadSnapshotEnemies = new HashSet<int>();
        readonly HashSet<long> endedSnapshotProjectiles = new HashSet<long>();
        readonly HashSet<int> destroyedSnapshotStructures = new HashSet<int>();
        readonly HashSet<Cell> endedSnapshotBlots = new HashSet<Cell>();
        public NotebookAnimationState Animation { get; } = new NotebookAnimationState();
        public IReadOnlyList<SimulationEvent> Effects => effects;
        public IReadOnlyList<SimulationEvent> Projectiles => projectiles;
        public TrialReport Report { get; private set; }
        public int FrameIndex { get; private set; }
        public double Seconds { get; private set; }
        public bool IsEnemyEnded(int id) => deadSnapshotEnemies.Contains(id);
        public bool IsProjectileEnded(int id, bool enemyGlob = false) => endedSnapshotProjectiles.Contains(ProjectileKey(id, enemyGlob));
        public bool IsStructureEnded(int id) => destroyedSnapshotStructures.Contains(id);
        public bool IsBlotEnded(Cell cell) => endedSnapshotBlots.Contains(cell);
        static long ProjectileKey(int id, bool enemyGlob) => ((enemyGlob ? 1L : 0L) << 32) | (uint)id;
        int eventFrame, eventIndex;
        public void Bind(TrialReport report)
        { Report = report; Reset(); }
        void Reset()
        { effects.Clear(); projectiles.Clear(); ClearTombstones(); Animation.Clear(); FrameIndex = 0; eventFrame = eventIndex = 0; Seconds = 0; }
        void ClearTombstones()
        { deadSnapshotEnemies.Clear(); endedSnapshotProjectiles.Clear(); destroyedSnapshotStructures.Clear(); endedSnapshotBlots.Clear(); }
        public void Seek(double seconds, NotebookVisualThemeAsset theme = null)
        {
            seconds = Math.Max(0, seconds);
            if (seconds < Seconds) Reset();
            Seconds = seconds;
            if (Report == null || Report.Frames.Count == 0) return;
            int previousFrame = FrameIndex;
            while (FrameIndex + 1 < Report.Frames.Count && Report.Frames[FrameIndex + 1].Seconds <= seconds) FrameIndex++;
            if (FrameIndex != previousFrame) ClearTombstones();
            double snapshotSeconds = Report.Frames[FrameIndex].Seconds;
            int maximum = Math.Max(1, (theme ?? NotebookVisualThemeAsset.Default)?.MaximumEffects ?? 256);
            while (eventFrame < Report.Frames.Count)
            {
                var events = Report.Frames[eventFrame].Events;
                if (eventIndex >= events.Count) { eventIndex = 0; eventFrame++; continue; }
                var item = events[eventIndex];
                if (item.SimulationSeconds > seconds) break;
                eventIndex++;
                Animation.AddEvent(item, (theme ?? NotebookVisualThemeAsset.Default)?.MaximumDeaths ?? 128);
                if (item.Kind == SimulationEventKind.BlotSpawned)
                    endedSnapshotBlots.Remove(new Cell((int)Math.Floor(item.To.X), (int)Math.Floor(item.To.Y)));
                // The current snapshot predates these removals. Keep interval tombstones until a newer snapshot reconciles them.
                if (item.SimulationSeconds > snapshotSeconds)
                {
                    if (item.Kind == SimulationEventKind.EnemyDied && deadSnapshotEnemies.Count < TrialRunner.MaximumFrameEvents)
                        deadSnapshotEnemies.Add(item.EntityId);
                    else if ((item.Kind == SimulationEventKind.ProjectileImpact || item.Kind == SimulationEventKind.EnemyGlobImpact)
                        && endedSnapshotProjectiles.Count < TrialRunner.MaximumFrameEvents)
                        endedSnapshotProjectiles.Add(ProjectileKey(item.EntityId, item.Kind == SimulationEventKind.EnemyGlobImpact));
                    else if (item.Kind == SimulationEventKind.StructureDestroyed && destroyedSnapshotStructures.Count < TrialRunner.MaximumFrameEvents)
                        destroyedSnapshotStructures.Add(item.EntityId);
                    else if ((item.Kind == SimulationEventKind.BlotExpired || item.Kind == SimulationEventKind.BlotCleared || item.Kind == SimulationEventKind.BlotAbsorbed)
                        && endedSnapshotBlots.Count < TrialRunner.MaximumFrameEvents)
                        endedSnapshotBlots.Add(new Cell((int)Math.Floor(item.To.X), (int)Math.Floor(item.To.Y)));
                }
                if (item.Kind == SimulationEventKind.ProjectileLaunched || item.Kind == SimulationEventKind.EnemyGlobLaunched)
                {
                    if (projectiles.Count >= 128) projectiles.RemoveAt(0);
                    projectiles.Add(item);
                }
                else if (item.Kind == SimulationEventKind.ProjectileImpact || item.Kind == SimulationEventKind.EnemyGlobImpact)
                    for (int i = projectiles.Count - 1; i >= 0; i--)
                        if (projectiles[i].EntityId == item.EntityId && (projectiles[i].Kind == SimulationEventKind.ProjectileLaunched
                            ? item.Kind == SimulationEventKind.ProjectileImpact : item.Kind == SimulationEventKind.EnemyGlobImpact)) projectiles.RemoveAt(i);
                if (NotebookWorldView.EffectId(item) == null) continue;
                if (effects.Count >= maximum) effects.RemoveAt(0);
                effects.Add(item);
            }
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var clip = (theme ?? NotebookVisualThemeAsset.Default)?.FindClip(NotebookWorldView.EffectId(effects[i]));
                if (seconds - effects[i].SimulationSeconds >= (clip?.Duration ?? .5f)) effects.RemoveAt(i);
            }
            Animation.Prune(seconds, theme);
            for (int i = projectiles.Count - 1; i >= 0; i--)
                if (seconds - projectiles[i].SimulationSeconds >= projectiles[i].TravelSeconds) projectiles.RemoveAt(i);
        }
    }
}
