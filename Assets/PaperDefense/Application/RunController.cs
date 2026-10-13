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
        public bool AutomaticSimulation { get; set; } = true;
        public int Speed { get; private set; } = 1;
        public bool InWorkshop {get;private set;}
        public WorkshopReadModel Workshop {get;private set;}
        ContentCatalogSnapshot contentSnapshot;
        public ContentCatalogSnapshot Content => contentSnapshot;
        public event Action WorkshopChanged;
        WorkshopProfile profile;
        IReadOnlyList<TowerBlueprint> runLoadout;
        public IReadOnlyList<TowerBlueprint> BuildLoadout =>
            (InWorkshop ? Workshop?.Loadout : runLoadout) ?? Array.Empty<TowerBlueprint>();
        bool workshopMode;
        bool legacyFixtureDefaults;
        readonly ProfileSaveCoordinator persistence = new ProfileSaveCoordinator();
        int depositedGold;
        public event Action<IRunReadModel> SessionChanged;
        public event Action<SimulationEvent> SimulationEventReceived;
        readonly FixedStepClock clock = new FixedStepClock();
        long lastEventSequence;
        public double DiscardedSimulationSeconds => clock.DiscardedSeconds;
        public long SkippedVisualEvents { get; private set; }
        public string LastConfigurationDiagnostic { get; private set; }

        public void Initialize(bool allowLegacyFixtureDefaults = false)
        {
            var preparedSession = new GameSession(CreateConfiguredBalance(false, allowLegacyFixtureDefaults));
            var preparedProfile = new WorkshopProfile(preparedSession.Balance.Content, preparedSession.Balance);
            legacyFixtureDefaults = allowLegacyFixtureDefaults;
            persistence.Reset();workshopMode=false;InWorkshop=false;
            profile=preparedProfile;Workshop=new WorkshopReadModel(profile);runLoadout=profile.GetLoadoutBlueprints();
            Initialize(preparedSession);
        }

        public void InitializeWorkshop(bool persistent=true, bool allowLegacyFixtureDefaults = false)
        {
            if (persistent)
            {
                if (allowLegacyFixtureDefaults) throw new ArgumentException("Legacy fixtures require memory mode or an explicitly isolated profile store.");
                InitializeWorkshop(new WorkshopProfileStore(WorkshopPersistence.ProfilePath)); return;
            }
            var preparedSession = new GameSession(CreateConfiguredBalance(true, allowLegacyFixtureDefaults), starterDefenses:false);
            var preparedProfile = new WorkshopProfile(preparedSession.Balance.Content, preparedSession.Balance);
            legacyFixtureDefaults = allowLegacyFixtureDefaults;
            persistence.Reset();workshopMode=true;
            profile=preparedProfile;Workshop=new WorkshopReadModel(profile);
            runLoadout=profile.GetLoadoutBlueprints();
            Initialize(preparedSession);
            InWorkshop=true;WorkshopChanged?.Invoke();
        }

        public void InitializeWorkshop(IWorkshopProfileStore store, bool allowLegacyFixtureDefaults = false)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            // Reject missing/invalid configuration before reading any profile, including the real path.
            var preparedSession = new GameSession(CreateConfiguredBalance(true, allowLegacyFixtureDefaults), starterDefenses:false);
            var freshProfile = new WorkshopProfile(preparedSession.Balance.Content, preparedSession.Balance);
            var loaded = store.Load();
            legacyFixtureDefaults = allowLegacyFixtureDefaults;
            persistence.Attach(store, loaded);workshopMode=true;
            profile=loaded.Source==ProfileLoadSource.NewProfile ? freshProfile : loaded.Profile??freshProfile;
            Workshop=new WorkshopReadModel(profile);RefreshSaveInfo();
            runLoadout=profile.GetLoadoutBlueprints();
            Initialize(preparedSession);
            InWorkshop=true;WorkshopChanged?.Invoke();
        }

        public bool RetrySave(out string reason)
        {
            bool result=persistence.Retry(profile,out reason);
            RefreshSaveInfo();
            if(result)WorkshopChanged?.Invoke();
            return result;
        }

        void RefreshSaveInfo() => persistence.CopyStatusTo(Workshop);

        GameBalance CreateWorkshopBalance() => CreateConfiguredBalance(true, legacyFixtureDefaults);

        GameBalance CreateConfiguredBalance(bool workshop, bool allowLegacyFixtureDefaults)
        {
            if(balanceAsset!=null)return workshop ? balanceAsset.CreateWorkshopBalance() : balanceAsset.CreateBalance();
            if(!allowLegacyFixtureDefaults)throw new ArgumentException(UserText.Message("copy.5243cc3304142438"));
            if(!workshop)return GameBalance.CreateDefault();
            var balance=GameBalancePresets.CreateTargetWorkshop();GameBalanceAsset.AddWorkshopThreats(balance);
            return balance;
        }

        // Only the dedicated Validation assembly can attach a replay; Presentation cannot.
        internal void Initialize(GameSession replay)
        {
            session = replay ?? throw new ArgumentNullException(nameof(replay));
            contentSnapshot = session.Balance.ResolveContent();
            InWorkshop=false;
            if(profile==null){profile=new WorkshopProfile();Workshop=new WorkshopReadModel(profile);runLoadout=profile.GetLoadoutBlueprints();}
            if (profile.ConfigureContent(contentSnapshot)) persistence.ContentChanged();
            depositedGold=0;
            Session = new RunReadModel(session);
            Workshop.Refresh(session.Balance);
            RefreshSaveInfo();
            Paused = false; Speed = 1; clock.Reset();
            lastEventSequence = 0; SkippedVisualEvents = 0;
            SessionChanged?.Invoke(Session);
        }

        void Update()
        {
            if (AutomaticSimulation) AdvanceFrame(Time.unscaledDeltaTime, Paused, Speed);
        }

        /// <summary>A validation stand owns time explicitly; one press advances exactly one simulation tick.</summary>
        public int StepSimulation()
        {
            if (session == null || InWorkshop) return 0;
            clock.ClearDebt();
            session.Tick(FixedStepClock.Step);
            DrainEvents();
            DepositGold();
            return 1;
        }
        internal void SynchronizeExternalStep()
        {
            DrainEvents();
            DepositGold();
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
        public void Restart() {if(workshopMode)OpenWorkshop();else Initialize(legacyFixtureDefaults);}
        public bool Build(IReadOnlyList<Cell> cells, StructureKind kind, MaterialTier tier, out string reason)
        {
            if (!CanBuildTool(kind, out reason)) return false;
            return session.TryBuild(cells,kind,tier,out reason);
        }
        public BuildQuote QuoteBuild(IReadOnlyList<Cell> cells, StructureKind kind, MaterialTier tier)
            => CanBuildTool(kind, out var reason) ? session.QuoteBuild(cells, kind, tier) : RejectedQuote(reason);

        bool CanBuildTool(StructureKind kind, out string reason)
        {
            reason = "";
            if (session == null || InWorkshop) { reason = UserText.Message("copy.e6a1f147d46938e5"); return false; }
            if (workshopMode && kind != StructureKind.Wall && kind != StructureKind.Spikes)
            { reason = UserText.Message("copy.65fd0a869c823b56"); return false; }
            return true;
        }

        static BuildQuote RejectedQuote(string reason) => new BuildQuote { Cells = Array.Empty<Cell>(), Reason = reason };
        public bool Erase(Cell cell, out int refund) {refund=0;return !InWorkshop&&session.TryErase(cell,out refund);}
        public bool Upgrade(Cell cell, out string reason) {reason=UserText.Message("copy.e6a1f147d46938e5");return !InWorkshop&&session.TryUpgrade(cell,out reason);}
        public bool SetPriority(Cell cell, TargetPriority priority) => !InWorkshop&&session.SetPriority(cell, priority);
        public void ChooseUpgrade(RunUpgrade upgrade) {if(!InWorkshop)session.ChooseUpgrade(upgrade);}

        bool Editing(out string reason){reason=InWorkshop?"":UserText.Message("copy.8873645dd56497ed");return InWorkshop;}
        bool CreateCandidate(out WorkshopProfile candidate,out string reason)
        {
            candidate=null;
            if(!Editing(out reason))return false;
            if(!persistence.CanEdit(out reason))return false;
            candidate=profile.CreateWorkingCopy();return true;
        }
        bool CommitCandidate(WorkshopProfile candidate,out string reason)
        {
            reason="";
            if(!persistence.SaveCandidate(candidate,out reason)){RefreshSaveInfo();return false;}
            profile=candidate;Workshop=new WorkshopReadModel(profile,session.Balance);RefreshSaveInfo();
            WorkshopChanged?.Invoke();return true;
        }
        void OnApplicationQuit()
        {
            persistence.Flush(profile);RefreshSaveInfo();
        }
        void DepositGold()
        {
            int reward=session.GoldEarned-depositedGold;if(reward<=0)return;
            // Advance this cursor before IO so retry/transition cannot replay the award.
            profile.GrantGold(reward);depositedGold=session.GoldEarned;
            persistence.SaveEarnedReward(profile);
            RefreshSaveInfo();WorkshopChanged?.Invoke();
        }
        public bool SaveBlueprint(TowerBlueprint blueprint,out string reason)
        {
            if(!CreateCandidate(out var candidate,out reason)||!candidate.SaveBlueprint(blueprint,out reason,session.Balance))return false;
            return CommitCandidate(candidate,out reason);
        }
        public bool DeleteBlueprint(string id,out string reason)
        {
            if(!CreateCandidate(out var candidate,out reason)||!candidate.RemoveBlueprint(id,out reason))return false;
            return CommitCandidate(candidate,out reason);
        }
        public bool AssignBlueprint(int slot,string id,out string reason)
        {
            if(!CreateCandidate(out var candidate,out reason)||!candidate.AssignSlot(slot,id,out reason,session.Balance))return false;
            return CommitCandidate(candidate,out reason);
        }
        public bool UnlockPart(string id,out string reason)
        {
            if(!CreateCandidate(out var candidate,out reason)||!candidate.Unlock(id,out reason,session.Balance))return false;
            return CommitCandidate(candidate,out reason);
        }
        public bool StartRun(out string reason)
        {
            if(!Editing(out reason))return false;
            GameBalance balance;
            try { balance=CreateWorkshopBalance(); }
            catch (ArgumentException error) { LastConfigurationDiagnostic=error.ToString();reason=UserText.Message("run.error.configuration");return false; }
            var candidate = profile.CreateWorkingCopy();
            var preparedContent = balance.ResolveContent();
            balance.Content = preparedContent;
            candidate.ConfigureContent(preparedContent);
            var selection=candidate.GetLoadoutBlueprints();
            foreach(var blueprint in selection)if(!candidate.ValidateOwned(blueprint,out reason,balance))return false;
            var prepared = new TowerBlueprint[selection.Count];
            for (int i = 0; i < prepared.Length; i++) prepared[i] = selection[i].WithContent(preparedContent);
            GameSession preparedSession;
            try { preparedSession=new GameSession(balance,starterDefenses:false); }
            catch (ArgumentException error) { LastConfigurationDiagnostic=error.ToString();reason=UserText.Message("run.error.configuration");return false; }
            runLoadout=Array.AsReadOnly(prepared);InWorkshop=false;
            Initialize(preparedSession);
            reason="";WorkshopChanged?.Invoke();return true;
        }
        public void OpenWorkshop()
        {
            var preparedSession = new GameSession(CreateWorkshopBalance(),starterDefenses:false);
            if(session!=null)DepositGold();
            workshopMode=true;InWorkshop=true;
            Initialize(preparedSession);
            InWorkshop=true;
            WorkshopChanged?.Invoke();
        }
        public TowerPreview PreviewBlueprint(TowerBlueprint blueprint,MaterialTier material=MaterialTier.Pencil)
            =>new TowerPreview(blueprint,session.Balance,material,session.Mechanics);
        public BuildQuote QuoteBlueprint(IReadOnlyList<Cell> cells,int slot,MaterialTier material,InkColor color)
        {
            if (session == null || InWorkshop) return RejectedQuote(UserText.Message("copy.e6a1f147d46938e5"));
            if(slot<0||slot>=runLoadout.Count)return new BuildQuote{Cells=Array.Empty<Cell>(),Reason=UserText.Message("copy.48635af5e97f1a11")};
            return session.QuoteBlueprint(cells,runLoadout[slot],material,color);
        }
        public bool BuildBlueprint(IReadOnlyList<Cell> cells,int slot,MaterialTier material,InkColor color,out string reason)
        {
            reason=UserText.Message("copy.e6a1f147d46938e5");if(session == null || InWorkshop)return false;
            if(slot<0||slot>=runLoadout.Count){reason=UserText.Message("copy.48635af5e97f1a11");return false;}
            return session.TryBuildBlueprint(cells,runLoadout[slot],material,color,out reason);
        }
        public bool Paint(Cell cell,InkColor color,out string reason)
        {reason=UserText.Message("copy.e6a1f147d46938e5");return !InWorkshop&&session.TryPaint(cell,color,out reason);}
        public bool Blotter(Cell cell,out string reason)
        {reason=UserText.Message("copy.e6a1f147d46938e5");return !InWorkshop&&session.TryBlotter(cell,out reason);}
        public bool ActivateWell(int index,out string reason)
        {reason=UserText.Message("copy.e6a1f147d46938e5");return !InWorkshop&&session.TryActivateWell(index,out reason);}
        public FootprintProbeComparison CompareMortarFootprints(TowerBlueprint blueprint,int mount,MortarFootprint first,MortarFootprint second,FootprintFormation formation)
            => MortarFootprintProbe.Compare(blueprint,mount,first,second,session.Balance,formation);

        public TrialComparison RunTrial(TowerBlueprint blueprint,TrialScenario scenario,bool counterfire=false,int budget=400)
        {
            if(!InWorkshop||!profile.ValidateOwned(blueprint,out _,session.Balance))return null;
            var trials=session.Balance.Trials;
            var baseline=new TowerBlueprint("trial.baseline",UserText.Message("copy.5c07091d535b3296"),trials.BaselineChassisId,trials.BaselineWeaponId,content:contentSnapshot);
            return TrialRunner.Compare(blueprint,baseline,session.Balance,scenario,budget,1337,counterfire);
        }
        public TrialComparison RunQualityTrial(TowerBlueprint blueprint,TrialScenario scenario,bool counterfire=false,int budget=400)
        {
            if(!InWorkshop||!profile.ValidateOwned(blueprint,out _,session.Balance))return null;
            return TrialRunner.CompareQuality(blueprint,session.Balance,scenario,budget,1337,counterfire);
        }
    }
}
