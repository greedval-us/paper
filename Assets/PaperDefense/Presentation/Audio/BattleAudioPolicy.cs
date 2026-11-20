using System;
using PaperDefense.Core;

namespace PaperDefense.Presentation
{
    /// <summary>Small, clock-injected presentation policy. It never changes simulation or retains a battle journal.</summary>
    public sealed class BattleAudioPolicy
    {
        public const int MaximumPending = 16, MaximumStartsPerFrame = 3;
        const double MaximumAge = .2;
        readonly BattleAudioRequest[] requests = new BattleAudioRequest[MaximumPending];
        readonly bool[] pending = new bool[MaximumPending];
        readonly double[] queuedAt = new double[MaximumPending], nextAllowed = new double[MaximumPending];
        int starts;
        public int PendingCount { get; private set; }

        public bool Queue(BattleAudioCue cue, double now, float pan = 0)
        {
            int index = (int)cue;
            if (index < 0 || index >= MaximumPending || !Finite(now) || now < 0 ||
                float.IsNaN(pan) || float.IsInfinity(pan) || now < nextAllowed[index]) return false;
            bool added = !pending[index];
            requests[index] = new BattleAudioRequest(cue, Math.Max(-.65f, Math.Min(.65f, pan)));
            queuedAt[index] = now;
            if (added) { pending[index] = true; PendingCount++; }
            return added;
        }

        public void BeginFrame() => starts = 0;

        public bool TryTake(double now, out BattleAudioRequest request)
        {
            request = default;
            if (!Finite(now) || starts >= MaximumStartsPerFrame) return false;
            int best = -1;
            for (int i = 0; i < MaximumPending; i++)
            {
                if (!pending[i]) continue;
                if (now < queuedAt[i] || now - queuedAt[i] > MaximumAge || now < nextAllowed[i])
                { Remove(i); continue; }
                if (best < 0 || requests[i].Priority > requests[best].Priority) best = i;
            }
            if (best < 0) return false;
            request = requests[best]; Remove(best);
            nextAllowed[best] = now + Cooldown(request.Cue); starts++;
            return true;
        }

        void Remove(int index) { pending[index] = false; PendingCount--; }
        // A muted/hidden page discards work; returning does not waive the recent-shot cooldown.
        public void Clear() { Array.Clear(pending, 0, pending.Length); PendingCount = 0; starts = 0; }
        public void Reset() { Clear(); Array.Clear(nextAllowed, 0, nextAllowed.Length); }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static bool TryClassify(SimulationEvent item, out BattleAudioCue cue)
        {
            cue = default;
            switch (item.Kind)
            {
                case SimulationEventKind.WeaponActionStarted:
                    if (item.Attack == AttackKind.Cleanup) return false;
                    if (item.Attack == AttackKind.Single && (item.Weapon == StructureKind.Stapler ||
                        (item.WeaponId?.EndsWith(".stapler", StringComparison.Ordinal) ?? false)))
                    { cue = BattleAudioCue.ShotStapler; return true; }
                    switch (item.Attack)
                    {
                        case AttackKind.Single: cue = BattleAudioCue.ShotSingle; return true;
                        case AttackKind.Line: cue = BattleAudioCue.ShotLine; return true;
                        case AttackKind.AreaProjectile: cue = BattleAudioCue.ShotMortar; return true;
                        case AttackKind.Cone: cue = BattleAudioCue.ShotCone; return true;
                        case AttackKind.Ricochet: cue = BattleAudioCue.ShotRicochet; return true;
                        case AttackKind.Radial: cue = BattleAudioCue.ShotRadial; return true;
                        default: return false;
                    }
                case SimulationEventKind.ProjectileImpact: cue = BattleAudioCue.MortarImpact; return true;
                case SimulationEventKind.BaseHit: cue = BattleAudioCue.BaseHit; return true;
                case SimulationEventKind.StructureDestroyed: cue = BattleAudioCue.StructureDestroyed; return true;
                case SimulationEventKind.StructureCompleted: cue = BattleAudioCue.StructureCompleted; return true;
                case SimulationEventKind.ModernizationCompleted: cue = BattleAudioCue.ModernizationCompleted; return true;
                case SimulationEventKind.SpikesTriggered: cue = BattleAudioCue.ShotRadial; return true;
                default: return false;
            }
        }

        public static int Priority(BattleAudioCue cue)
        {
            switch (cue)
            {
                case BattleAudioCue.Defeat: return 120;
                case BattleAudioCue.Victory: return 110;
                case BattleAudioCue.CastleAlarm: return 100;
                case BattleAudioCue.BaseHit: return 90;
                case BattleAudioCue.StructureDestroyed: return 80;
                case BattleAudioCue.WaveStarted: return 60;
                case BattleAudioCue.ModernizationCompleted: return 40;
                case BattleAudioCue.StructureCompleted: return 30;
                case BattleAudioCue.MortarImpact: return 20;
                default: return 10;
            }
        }

        public static float Gain(BattleAudioCue cue)
            => Priority(cue) >= 80 ? .45f : Priority(cue) >= 30 ? .35f : cue == BattleAudioCue.MortarImpact ? .32f : .28f;

        static double Cooldown(BattleAudioCue cue)
        {
            switch (cue)
            {
                case BattleAudioCue.ShotSingle: return .08;
                case BattleAudioCue.ShotLine: case BattleAudioCue.ShotRicochet: case BattleAudioCue.ShotStapler: return .1;
                case BattleAudioCue.ShotCone: case BattleAudioCue.ShotRadial: return .12;
                case BattleAudioCue.ShotMortar: case BattleAudioCue.MortarImpact: return .14;
                case BattleAudioCue.BaseHit: return .35;
                case BattleAudioCue.StructureDestroyed: return .2;
                case BattleAudioCue.WaveStarted: return .5;
                case BattleAudioCue.Victory: case BattleAudioCue.Defeat: return 5;
                case BattleAudioCue.CastleAlarm: return 3;
                case BattleAudioCue.ModernizationCompleted: return .22;
                default: return .18;
            }
        }
    }
}
