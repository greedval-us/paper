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
        public readonly BossRank Boss;
        public readonly DrawingTier Tier;
        public readonly Vector2 Position;
        public readonly double Born;
        public readonly float VisualScale;
        public NotebookDeathVisual(SimulationEvent item)
        { Id = item.EntityId; Kind = item.EnemyKind; Boss = item.Boss; Tier = item.EnemyDrawingTier; Position = new Vector2(item.To.X, item.To.Y); Born = item.SimulationSeconds; VisualScale = item.EnemyVisualScale; }
    }

    /// <summary>Bounded presentation records. Events cannot cause gameplay or modify author geometry.</summary>
    public sealed class NotebookAnimationState
    {
        public const int MaximumMounts = 512, MaximumEnemies = 2048;
        readonly Dictionary<long, MountAction> mounts = new Dictionary<long, MountAction>(128);
        readonly Dictionary<int, EnemyMotion> enemies = new Dictionary<int, EnemyMotion>(1024);
        readonly Dictionary<int, StructurePulse> structures = new Dictionary<int, StructurePulse>(128);
        readonly List<NotebookDeathVisual> deaths = new List<NotebookDeathVisual>(128);
        readonly List<long> expiredMounts = new List<long>(128);
        readonly List<int> expiredEnemies = new List<int>(128);
        readonly List<int> expiredStructures = new List<int>(128);
        long sequence;
        static readonly string[,,,] enemyIds = CreateEnemyIds();
        public int MountCount => mounts.Count;
        public int EnemyCount => enemies.Count;
        public int StructurePulseCount => structures.Count;
        public IReadOnlyList<NotebookDeathVisual> Deaths => deaths;
        public int RecoilStartCount { get; private set; }
        public long DroppedRecords { get; private set; }
        struct MountAction { public long Action; public double Born; public string Weapon; }
        struct StructurePulse { public double Hit, Work; public bool HasHit, HasWork; }
        struct EnemyMotion
        {
            public Vector2 Position;
            public double Observed, Hit, Attack;
            public bool HasPosition, Moving, HasHit, HasAttack;
        }
        static long Key(int structure, int slot) => ((long)structure << 32) | (uint)slot;
        public void Clear()
        { mounts.Clear(); enemies.Clear(); structures.Clear(); deaths.Clear(); sequence = 0; RecoilStartCount = 0; DroppedRecords = 0; }
        internal void ObserveStructureWork(int id, double seconds) => StructureMoment(id, seconds, false);
        internal void ObserveStructureHit(int id, double seconds) => StructureMoment(id, seconds, true);
        void StructureMoment(int id, double seconds, bool hit)
        {
            if (id <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            if (!structures.TryGetValue(id, out var pulse) && structures.Count >= MaximumMounts)
            {
                int oldest = 0; double born = double.MaxValue;
                foreach (var item in structures)
                {
                    double last = Math.Max(item.Value.HasHit ? item.Value.Hit : double.MinValue,
                        item.Value.HasWork ? item.Value.Work : double.MinValue);
                    if (last < born) { born = last; oldest = item.Key; }
                }
                structures.Remove(oldest); DroppedRecords++;
            }
            if (hit) { pulse.Hit = seconds; pulse.HasHit = true; }
            else { pulse.Work = seconds; pulse.HasWork = true; }
            structures[id] = pulse;
        }
        internal AssemblyMechanicsPose StructureMechanics(int id, double now, bool reducedMotion)
        {
            if (!structures.TryGetValue(id, out var pulse)) return default;
            float hit = pulse.HasHit ? Pulse(now - pulse.Hit, .18) : 0;
            float work = !reducedMotion && pulse.HasWork ? Pulse(now - pulse.Work, .45) : 0;
            return new AssemblyMechanicsPose(activity: work, hit: hit);
        }
        static float Pulse(double age, double duration)
            => age < 0 || age >= duration ? 0 : (float)(1 - age / duration);
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
            else if (item.Kind == SimulationEventKind.StructureHit)
            { if (item.Magnitude > 0) ObserveStructureHit(item.EntityId, item.SimulationSeconds); }
            else if (item.Kind == SimulationEventKind.SpikesTriggered || item.Kind == SimulationEventKind.PrismPulsed)
            { if (item.Radius > 0) ObserveStructureWork(item.SourceStructureId, item.SimulationSeconds); }
            else if (item.Kind == SimulationEventKind.RepairApplied || item.Kind == SimulationEventKind.GeneratorPaid ||
                item.Kind == SimulationEventKind.SlowApplied || item.Kind == SimulationEventKind.BeaconMarked)
            { if (item.Magnitude > 0) ObserveStructureWork(item.SourceStructureId, item.SimulationSeconds); }
            else if (item.Kind == SimulationEventKind.EnemyAttack || item.Kind == SimulationEventKind.EnemyHit)
            {
                if (!enemies.TryGetValue(item.EntityId, out var state) && enemies.Count >= MaximumEnemies)
                { DroppedRecords++; return true; }
                if (item.Kind == SimulationEventKind.EnemyHit)
                {
                    // Sustained tower fire should leave time to see a boss's stride and windup.
                    if (item.Boss == BossRank.None || !state.HasHit || item.SimulationSeconds - state.Hit >= .8)
                    { state.Hit = item.SimulationSeconds; state.HasHit = true; }
                }
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
            double sampleSeconds = double.NaN, float windupSeconds = 0, EnemyFamily family = EnemyFamily.Ink)
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
            bool hit = state.HasHit && now - state.Hit < ClipDuration(enemy.Kind, enemy.DrawingTier, NotebookEnemyMotion.Hit, theme, family, enemy.Boss);
            if (hit && enemy.Boss == BossRank.None)
            { phase = (float)(now - state.Hit); return NotebookEnemyMotion.Hit; }
            if (enemy.WindupRemaining > 0)
            {
                float duration = ClipDuration(enemy.Kind, enemy.DrawingTier, NotebookEnemyMotion.Attack, theme, family, enemy.Boss);
                phase = Mathf.Clamp01(1 - enemy.WindupRemaining / Mathf.Max(.001f, windupSeconds > 0 ? windupSeconds : duration)) * duration * .7f;
                return NotebookEnemyMotion.Attack;
            }
            if (state.HasAttack)
            {
                float duration = ClipDuration(enemy.Kind, enemy.DrawingTier, NotebookEnemyMotion.Attack, theme, family, enemy.Boss);
                // Ranged bosses already showed the windup. Release the feather, ring or card on the firing tick.
                phase = (float)(now - state.Attack) + (enemy.Boss == BossRank.Finale ? duration * .7f : 0);
                if (phase < duration) return NotebookEnemyMotion.Attack;
            }
            if (hit) { phase = (float)(now - state.Hit); return NotebookEnemyMotion.Hit; }
            phase = (float)now + enemy.Id * .137f;
            return state.Moving ? NotebookEnemyMotion.Walk : NotebookEnemyMotion.Idle;
        }
        public void Prune(double now, NotebookVisualThemeAsset theme = null, EnemyFamily family = EnemyFamily.Ink)
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
            expiredStructures.Clear();
            foreach (var item in structures)
                if (now - Math.Max(item.Value.HasHit ? item.Value.Hit : double.MinValue,
                    item.Value.HasWork ? item.Value.Work : double.MinValue) > .5)
                    expiredStructures.Add(item.Key);
            foreach (int id in expiredStructures) structures.Remove(id);
            for (int i = deaths.Count - 1; i >= 0; i--)
                if (now - deaths[i].Born >= ClipDuration(deaths[i].Kind, deaths[i].Tier, NotebookEnemyMotion.Death, theme, family, deaths[i].Boss)) deaths.RemoveAt(i);
        }
        internal static string EnemyId(EnemyKind kind, DrawingTier tier, NotebookEnemyMotion motion, EnemyFamily family = EnemyFamily.Ink,
            BossRank boss = BossRank.None)
            => boss != BossRank.None ? InkSwarmEnemyArt.BossId(family, boss, motion) :
                enemyIds[(int)family, Mathf.Clamp((int)kind, 0, enemyIds.GetLength(1) - 1), Mathf.Clamp((int)tier, 0, 2), Mathf.Clamp((int)motion, 0, 4)];
        static string[,,,] CreateEnemyIds()
        {
            var roles = InkSwarmEnemyArt.Roles;
            var families = (EnemyFamily[])Enum.GetValues(typeof(EnemyFamily));
            var values = new string[families.Length, roles.Length, 3, 5];
            foreach (EnemyFamily family in families)
            {
                string prefix = family == EnemyFamily.Undead ? "enemy.undead." : family == EnemyFamily.Clockwork ? "enemy.clockwork." : "enemy.";
                for (int kind = 0; kind < roles.Length; kind++)
                for (int tier = 0; tier < 3; tier++)
                for (int motion = 0; motion < 5; motion++)
                    values[(int)family, kind, tier, motion] = prefix + roles[kind] + "." + Tier((DrawingTier)tier) + "." + Motion((NotebookEnemyMotion)motion);
            }
            return values;
        }
        static string Tier(DrawingTier tier) => tier == DrawingTier.Hatched ? "hatched" : tier == DrawingTier.Outlined ? "outlined" : "sketch";
        static string Motion(NotebookEnemyMotion motion) => motion == NotebookEnemyMotion.Walk ? "walk" : motion == NotebookEnemyMotion.Attack ? "attack" : motion == NotebookEnemyMotion.Hit ? "hit" : motion == NotebookEnemyMotion.Death ? "death" : "idle";
        internal static float ClipDuration(EnemyKind kind, DrawingTier tier, NotebookEnemyMotion motion, NotebookVisualThemeAsset theme,
            EnemyFamily family = EnemyFamily.Ink, BossRank boss = BossRank.None)
            => (theme ?? NotebookVisualThemeAsset.Default)?.FindClip(EnemyId(kind, tier, motion, family, boss))?.Duration ?? (motion == NotebookEnemyMotion.Death ? .6f : .24f);
    }
}
