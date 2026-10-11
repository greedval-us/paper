using System;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Application
{
    public sealed class RunController : MonoBehaviour
    {
        public GameBalanceAsset balanceAsset;
        GameSession session;
        public IRunReadModel Session { get; private set; }
        public bool Paused { get; private set; }
        public int Speed { get; private set; } = 1;
        public event Action<IRunReadModel> SessionChanged;
        public event Action<SimulationEvent> SimulationEventReceived;
        readonly FixedStepClock clock = new FixedStepClock();
        long lastEventSequence;
        public double DiscardedSimulationSeconds => clock.DiscardedSeconds;
        public long SkippedVisualEvents { get; private set; }

        public void Initialize()
        {
            Initialize(new GameSession(balanceAsset != null ? balanceAsset.CreateBalance() : GameBalance.CreateDefault()));
        }

        // Only the dedicated Validation assembly can attach a replay; Presentation cannot.
        internal void Initialize(GameSession replay)
        {
            session = replay ?? throw new ArgumentNullException(nameof(replay));
            Session = new RunReadModel(session);
            Paused = false; Speed = 1; clock.Reset();
            lastEventSequence = 0; SkippedVisualEvents = 0;
            SessionChanged?.Invoke(Session);
        }

        void Update()
        {
            AdvanceFrame(Time.unscaledDeltaTime, Paused, Speed);
        }

        /// <summary>Same deterministic frame boundary used by Update and integration tests.</summary>
        public int AdvanceFrame(float realDelta, bool paused, int speed)
        {
            if (session == null) return 0;
            int steps = clock.AdvanceFrame(realDelta, paused, speed);
            for (int i = 0; i < steps; i++)
            {
                session.Tick(FixedStepClock.Step);
                DrainEvents();
            }
            return steps;
        }

        void DrainEvents()
        {
            var events = session.EventBuffer.Items;
            int unread = (int)Math.Min(events.Count, Math.Max(0, session.EventBuffer.LatestSequence - lastEventSequence));
            for (int i = events.Count - unread; i < events.Count; i++)
            {
                var item = events[i];
                if (item.Sequence <= lastEventSequence) continue;
                SkippedVisualEvents += Math.Max(0, item.Sequence - lastEventSequence - 1);
                lastEventSequence = item.Sequence;
                SimulationEventReceived?.Invoke(item);
            }
        }

        public void TogglePause() { Paused = !Paused; clock.ClearDebt(); }
        public void ToggleSpeed() => Speed = Speed == 1 ? 2 : 1;
        public void NextWave() { if (session != null) session.StartNextWave(); }
        public void Restart() => Initialize();
        public bool Build(IReadOnlyList<Cell> cells, StructureKind kind, MaterialTier tier, out string reason)
            => session.TryBuild(cells, kind, tier, out reason);
        public bool Erase(Cell cell, out int refund) => session.TryErase(cell, out refund);
        public bool Upgrade(Cell cell, out string reason) => session.TryUpgrade(cell, out reason);
        public bool SetPriority(Cell cell, TargetPriority priority) => session.SetPriority(cell, priority);
        public void ChooseUpgrade(RunUpgrade upgrade) => session.ChooseUpgrade(upgrade);
    }
}
