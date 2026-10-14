using System;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Simulation time drives poses; only a bounded, paused-aware terminal tail uses display time.</summary>
    public sealed class NotebookVisualClock
    {
        public double Seconds { get; private set; }
        public bool Terminal { get; private set; }
        double previousSimulation, tail;
        public void Reset(double simulationSeconds = 0)
        { Seconds = previousSimulation = simulationSeconds; tail = 0; Terminal = false; }
        public void Advance(double simulationSeconds, RunPhase phase, bool paused, double displayDelta, double maximumTail)
        {
            if (simulationSeconds < previousSimulation) Reset(simulationSeconds);
            previousSimulation = simulationSeconds;
            bool terminal = phase == RunPhase.Victory || phase == RunPhase.Defeat;
            if (!terminal) { Seconds = simulationSeconds; tail = 0; Terminal = false; return; }
            if (!Terminal) { Seconds = simulationSeconds; Terminal = true; return; }
            if (!paused && displayDelta > 0)
                tail = Math.Min(Math.Max(0, maximumTail), tail + displayDelta);
            Seconds = simulationSeconds + tail;
        }
    }

    public enum NotebookEnemyMotion { Idle, Walk, Attack, Hit, Death }

    public readonly struct NotebookDeathVisual
    {
        public readonly int Id;
        public readonly EnemyKind Kind;
        public readonly DrawingTier Tier;
        public readonly Vector2 Position;
        public readonly double Born;
        public readonly float VisualScale;
        public NotebookDeathVisual(SimulationEvent item)
        { Id = item.EntityId; Kind = item.EnemyKind; Tier = item.EnemyDrawingTier; Position = new Vector2(item.To.X, item.To.Y); Born = item.SimulationSeconds; VisualScale = item.EnemyVisualScale; }
    }

    /// <summary>Bounded presentation records. Events cannot cause gameplay or modify author geometry.</summary>
    public sealed class NotebookAnimationState
    {
        public const int MaximumMounts = 512, MaximumEnemies = 2048;
        readonly Dictionary<long, MountAction> mounts = new Dictionary<long, MountAction>(128);
        readonly Dictionary<int, EnemyMotion> enemies = new Dictionary<int, EnemyMotion>(1024);
        readonly List<NotebookDeathVisual> deaths = new List<NotebookDeathVisual>(128);
        readonly List<long> expiredMounts = new List<long>(128);
        readonly List<int> expiredEnemies = new List<int>(128);
        long sequence;
        static readonly string[,,] enemyIds = CreateEnemyIds();
        public int MountCount => mounts.Count;
        public int EnemyCount => enemies.Count;
        public IReadOnlyList<NotebookDeathVisual> Deaths => deaths;
        public int RecoilStartCount { get; private set; }
        public long DroppedRecords { get; private set; }
        struct MountAction { public long Action; public double Born; public string Weapon; }
        struct EnemyMotion
        {
            public Vector2 Position;
            public double Observed, Hit, Attack;
            public bool HasPosition, Moving, HasHit, HasAttack;
        }
        static long Key(int structure, int slot) => ((long)structure << 32) | (uint)slot;
        public void Clear()
        { mounts.Clear(); enemies.Clear(); deaths.Clear(); sequence = 0; RecoilStartCount = 0; DroppedRecords = 0; }
        public bool AddEvent(SimulationEvent item, int maximumDeaths = 128)
        {
            if (item.Sequence > 0 && item.Sequence <= sequence) return false;
            if (item.Sequence > 0) sequence = item.Sequence;
            if (item.Kind == SimulationEventKind.WeaponActionStarted && item.MountSlotIndex >= 0)
            {
                long key = Key(item.SourceStructureId, item.MountSlotIndex);
                if (mounts.TryGetValue(key, out var old) && old.Action == item.ActionId) return false;
                if (!mounts.ContainsKey(key) && mounts.Count >= MaximumMounts)
                {
                    long oldest = 0; double born = double.MaxValue;
                    foreach (var entry in mounts) if (entry.Value.Born < born) { oldest = entry.Key; born = entry.Value.Born; }
                    mounts.Remove(oldest); DroppedRecords++;
                }
                mounts[key] = new MountAction { Action = item.ActionId, Born = item.SimulationSeconds, Weapon = item.WeaponId };
                RecoilStartCount++;
            }
            else if (item.Kind == SimulationEventKind.EnemyAttack || item.Kind == SimulationEventKind.EnemyHit)
            {
                if (!enemies.TryGetValue(item.EntityId, out var state) && enemies.Count >= MaximumEnemies)
                { DroppedRecords++; return true; }
                if (item.Kind == SimulationEventKind.EnemyHit) { state.Hit = item.SimulationSeconds; state.HasHit = true; }
                else { state.Attack = item.SimulationSeconds; state.HasAttack = true; }
                enemies[item.EntityId] = state;
            }
            else if (item.Kind == SimulationEventKind.EnemyDied)
            {
                enemies.Remove(item.EntityId);
                if (deaths.Count >= Math.Max(1, maximumDeaths)) { deaths.RemoveAt(0); DroppedRecords++; }
                deaths.Add(new NotebookDeathVisual(item));
            }
            return true;
        }
        public float Recoil(int structureId, int slot, double now, NotebookVisualThemeAsset theme = null)
        {
            if (!mounts.TryGetValue(Key(structureId, slot), out var action)) return 0;
            float age = (float)(now - action.Born);
            var recoil = (theme ?? NotebookVisualThemeAsset.Default)?.FindRecoil(action.Weapon);
            if (recoil != null) return age < 0 ? 0 : recoil.Sample(age);
            if (age < 0 || age >= .2f) return 0;
            return -.085f * (1 - age / .2f);
        }
        public NotebookEnemyMotion Observe(EnemyView enemy, double now, out float phase, NotebookVisualThemeAsset theme = null,
            double sampleSeconds = double.NaN, float windupSeconds = 0)
        {
            Vector2 position = new Vector2(enemy.Position.X, enemy.Position.Y);
            bool tracked = enemies.TryGetValue(enemy.Id, out var state);
            if (!tracked && enemies.Count >= MaximumEnemies)
            { phase = (float)now + enemy.Id * .137f; return enemy.WindupRemaining > 0 ? NotebookEnemyMotion.Attack : NotebookEnemyMotion.Idle; }
            double observed = double.IsNaN(sampleSeconds) ? now : sampleSeconds;
            if (!state.HasPosition || observed > state.Observed)
            {
                state.Moving = state.HasPosition && (position - state.Position).sqrMagnitude > .00000001f;
                state.Position = position; state.Observed = observed; state.HasPosition = true;
                enemies[enemy.Id] = state;
            }
            if (state.HasHit && now - state.Hit < ClipDuration(enemy.Kind, enemy.DrawingTier, NotebookEnemyMotion.Hit, theme))
            { phase = (float)(now - state.Hit); return NotebookEnemyMotion.Hit; }
            if (enemy.WindupRemaining > 0)
            {
                float duration = ClipDuration(enemy.Kind, enemy.DrawingTier, NotebookEnemyMotion.Attack, theme);
                phase = Mathf.Clamp01(1 - enemy.WindupRemaining / Mathf.Max(.001f, windupSeconds > 0 ? windupSeconds : duration)) * duration * .7f;
                return NotebookEnemyMotion.Attack;
            }
            if (state.HasAttack && now - state.Attack < ClipDuration(enemy.Kind, enemy.DrawingTier, NotebookEnemyMotion.Attack, theme))
            { phase = (float)(now - state.Attack); return NotebookEnemyMotion.Attack; }
            phase = (float)now + enemy.Id * .137f;
            return state.Moving ? NotebookEnemyMotion.Walk : NotebookEnemyMotion.Idle;
        }
        public void Prune(double now, NotebookVisualThemeAsset theme = null)
        {
            expiredMounts.Clear();
            foreach (var item in mounts)
            {
                float duration = (theme ?? NotebookVisualThemeAsset.Default)?.FindRecoil(item.Value.Weapon)?.Duration ?? .2f;
                if (now - item.Value.Born > duration) expiredMounts.Add(item.Key);
            }
            foreach (long key in expiredMounts) mounts.Remove(key);
            expiredEnemies.Clear();
            foreach (var item in enemies) if (now - Math.Max(item.Value.Observed, Math.Max(item.Value.Hit, item.Value.Attack)) > 2) expiredEnemies.Add(item.Key);
            foreach (int id in expiredEnemies) enemies.Remove(id);
            for (int i = deaths.Count - 1; i >= 0; i--)
                if (now - deaths[i].Born >= ClipDuration(deaths[i].Kind, deaths[i].Tier, NotebookEnemyMotion.Death, theme)) deaths.RemoveAt(i);
        }
        internal static string EnemyId(EnemyKind kind, DrawingTier tier, NotebookEnemyMotion motion)
            => enemyIds[Mathf.Clamp((int)kind, 0, enemyIds.GetLength(0) - 1), Mathf.Clamp((int)tier, 0, 2), Mathf.Clamp((int)motion, 0, 4)];
        static string[,,] CreateEnemyIds()
        {
            int families = Enum.GetValues(typeof(EnemyKind)).Length;
            var values = new string[families, 3, 5];
            for (int kind = 0; kind < families; kind++) for (int tier = 0; tier < 3; tier++) for (int motion = 0; motion < 5; motion++)
                values[kind, tier, motion] = "enemy." + Family((EnemyKind)kind) + "." + Tier((DrawingTier)tier) + "." + Motion((NotebookEnemyMotion)motion);
            return values;
        }
        static string Family(EnemyKind kind) => kind == EnemyKind.Runner ? "runner" : kind == EnemyKind.Gnawer ? "gnawer" : kind == EnemyKind.Spitter ? "spitter" : kind == EnemyKind.Brute ? "brute" : kind == EnemyKind.Bulwark ? "bulwark" : "doodle";
        static string Tier(DrawingTier tier) => tier == DrawingTier.Hatched ? "hatched" : tier == DrawingTier.Outlined ? "outlined" : "sketch";
        static string Motion(NotebookEnemyMotion motion) => motion == NotebookEnemyMotion.Walk ? "walk" : motion == NotebookEnemyMotion.Attack ? "attack" : motion == NotebookEnemyMotion.Hit ? "hit" : motion == NotebookEnemyMotion.Death ? "death" : "idle";
        internal static float ClipDuration(EnemyKind kind, DrawingTier tier, NotebookEnemyMotion motion, NotebookVisualThemeAsset theme)
            => (theme ?? NotebookVisualThemeAsset.Default)?.FindClip(EnemyId(kind, tier, motion))?.Duration ?? (motion == NotebookEnemyMotion.Death ? .6f : .24f);
    }
}
