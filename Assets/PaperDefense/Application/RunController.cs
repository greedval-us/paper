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
        public bool InWorkshop {get;private set;}
        public WorkshopReadModel Workshop {get;private set;}
        public event Action WorkshopChanged;
        WorkshopProfile profile;
        IReadOnlyList<TowerBlueprint> runLoadout;
        bool persistentProfile,workshopMode;
        int depositedGold;
        public event Action<IRunReadModel> SessionChanged;
        public event Action<SimulationEvent> SimulationEventReceived;
        readonly FixedStepClock clock = new FixedStepClock();
        long lastEventSequence;
        public double DiscardedSimulationSeconds => clock.DiscardedSeconds;
        public long SkippedVisualEvents { get; private set; }

        public void Initialize()
        {
            persistentProfile=false;workshopMode=false;InWorkshop=false;
            profile=new WorkshopProfile();Workshop=new WorkshopReadModel(profile);runLoadout=profile.GetLoadoutBlueprints();
            Initialize(new GameSession(balanceAsset != null ? balanceAsset.CreateBalance() : GameBalance.CreateDefault()));
        }

        public void InitializeWorkshop(bool persistent=true)
        {
            persistentProfile=persistent;workshopMode=true;
            profile=persistent?WorkshopPersistence.Load():new WorkshopProfile();Workshop=new WorkshopReadModel(profile);
            runLoadout=profile.GetLoadoutBlueprints();
            Initialize(new GameSession(CreateWorkshopBalance(),starterDefenses:false));
            InWorkshop=true;WorkshopChanged?.Invoke();
        }

        GameBalance CreateWorkshopBalance()
        {
            if(balanceAsset!=null)return balanceAsset.CreateWorkshopBalance();
            var balance=GameBalance.CreateDefault();balance.Width=96;balance.Height=64;balance.StartingInk=720;
            balance.EnableEnemyMechanics=true;balance.EnableInkWells=true;GameBalanceAsset.AddWorkshopThreats(balance);
            return balance;
        }

        // Only the dedicated Validation assembly can attach a replay; Presentation cannot.
        internal void Initialize(GameSession replay)
        {
            session = replay ?? throw new ArgumentNullException(nameof(replay));
            InWorkshop=false;
            if(profile==null){profile=new WorkshopProfile();Workshop=new WorkshopReadModel(profile);runLoadout=profile.GetLoadoutBlueprints();}
            depositedGold=0;
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
            if (session == null || InWorkshop) {clock.ClearDebt();return 0;}
            int steps = clock.AdvanceFrame(realDelta, paused, speed);
            for (int i = 0; i < steps; i++)
            {
                session.Tick(FixedStepClock.Step);
                DrainEvents();
                DepositGold();
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
        public void NextWave() { if (session != null && !InWorkshop) session.StartNextWave(); }
        public void Restart() {if(workshopMode)OpenWorkshop();else Initialize();}
        public bool Build(IReadOnlyList<Cell> cells, StructureKind kind, MaterialTier tier, out string reason)
        {
            if(InWorkshop){reason="Сначала начните партию.";return false;}
            if(workshopMode&&kind!=StructureKind.Wall&&kind!=StructureKind.Spikes)
            {reason="Башни строятся по одному из четырёх взятых чертежей.";return false;}
            return session.TryBuild(cells,kind,tier,out reason);
        }
        public bool Erase(Cell cell, out int refund) {refund=0;return !InWorkshop&&session.TryErase(cell,out refund);}
        public bool Upgrade(Cell cell, out string reason) {reason="Сначала начните партию.";return !InWorkshop&&session.TryUpgrade(cell,out reason);}
        public bool SetPriority(Cell cell, TargetPriority priority) => !InWorkshop&&session.SetPriority(cell, priority);
        public void ChooseUpgrade(RunUpgrade upgrade) {if(!InWorkshop)session.ChooseUpgrade(upgrade);}

        bool Editing(out string reason){reason=InWorkshop?"":"Сборка меняется в мастерской перед новой партией.";return InWorkshop;}
        void SaveProfile(){Workshop.Refresh();if(persistentProfile)WorkshopPersistence.Save(profile);WorkshopChanged?.Invoke();}
        void OnApplicationQuit(){if(persistentProfile&&profile!=null)WorkshopPersistence.Save(profile);}
        void DepositGold()
        {
            int reward=session.GoldEarned-depositedGold;if(reward<=0)return;
            profile.GrantGold(reward);depositedGold=session.GoldEarned;SaveProfile();
        }
        public bool SaveBlueprint(TowerBlueprint blueprint,out string reason)
        {if(!Editing(out reason)||!profile.SaveBlueprint(blueprint,out reason))return false;SaveProfile();return true;}
        public bool DeleteBlueprint(string id,out string reason)
        {if(!Editing(out reason)||!profile.RemoveBlueprint(id,out reason))return false;SaveProfile();return true;}
        public bool AssignBlueprint(int slot,string id,out string reason)
        {if(!Editing(out reason)||!profile.AssignSlot(slot,id,out reason))return false;SaveProfile();return true;}
        public bool UnlockPart(string id,out string reason)
        {if(!Editing(out reason)||!profile.Unlock(id,out reason))return false;SaveProfile();return true;}
        public bool StartRun(out string reason)
        {
            if(!Editing(out reason))return false;
            var selection=profile.GetLoadoutBlueprints();
            foreach(var blueprint in selection)if(!profile.ValidateOwned(blueprint,out reason))return false;
            runLoadout=selection;InWorkshop=false;
            Initialize(new GameSession(CreateWorkshopBalance(),starterDefenses:false));
            reason="";WorkshopChanged?.Invoke();return true;
        }
        public void OpenWorkshop()
        {
            if(session!=null)DepositGold();
            workshopMode=true;InWorkshop=true;
            Initialize(new GameSession(CreateWorkshopBalance(),starterDefenses:false));
            InWorkshop=true;
            WorkshopChanged?.Invoke();
        }
        public TowerPreview PreviewBlueprint(TowerBlueprint blueprint,MaterialTier material=MaterialTier.Pencil)
            =>new TowerPreview(blueprint,session.Balance,material);
        public BuildQuote QuoteBlueprint(IReadOnlyList<Cell> cells,int slot,MaterialTier material,InkColor color)
        {
            if(slot<0||slot>=runLoadout.Count)return new BuildQuote{Cells=Array.Empty<Cell>(),Reason="Выберите чертёж."};
            return session.QuoteBlueprint(cells,runLoadout[slot],material,color);
        }
        public bool BuildBlueprint(IReadOnlyList<Cell> cells,int slot,MaterialTier material,InkColor color,out string reason)
        {
            reason="Сначала начните партию.";if(InWorkshop)return false;
            if(slot<0||slot>=runLoadout.Count){reason="Выберите чертёж.";return false;}
            return session.TryBuildBlueprint(cells,runLoadout[slot],material,color,out reason);
        }
        public bool Paint(Cell cell,InkColor color,out string reason)
        {reason="Сначала начните партию.";return !InWorkshop&&session.TryPaint(cell,color,out reason);}
        public bool Blotter(Cell cell,out string reason)
        {reason="Сначала начните партию.";return !InWorkshop&&session.TryBlotter(cell,out reason);}
        public bool ActivateWell(int index,out string reason)
        {reason="Сначала начните партию.";return !InWorkshop&&session.TryActivateWell(index,out reason);}
        public TrialComparison RunTrial(TowerBlueprint blueprint,TrialScenario scenario,bool counterfire=false,int budget=400)
        {
            if(!InWorkshop||!profile.ValidateOwned(blueprint,out _))return null;
            var baseline=new TowerBlueprint("trial.baseline","Одиночные пулемёты",ChassisKind.Standard,StructureKind.Gun);
            return TrialRunner.Compare(blueprint,baseline,scenario,budget,1337,counterfire);
        }
    }
}
