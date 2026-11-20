using System;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Application
{
    public sealed partial class RunController : MonoBehaviour
    {
        public GameBalanceAsset balanceAsset;
        GameSession session;
        public IRunReadModel Session { get; private set; }
        public bool TacticalPaused { get; private set; }
        public bool MenuSuspended { get; private set; }
        public bool ModalSuspended { get; private set; }
        public bool CommandsSuspended => MenuSuspended || ModalSuspended;
        public bool Paused => TacticalPaused || CommandsSuspended;
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
        int submittedRewardWaves;
        bool submittedVictoryBonus;
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
            var preparedProfile = FreshProfile(preparedSession.Balance);
            PrepareRewardProfileReplacement();
            assemblyCommands = null; RunLoadout = null; runRequiresSlots = false;
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
                var balance = CreateConfiguredBalance(true, false);
                InitializeWorkshop(new WorkshopProfileStore(WorkshopPersistence.ProfilePath, catalog: ConfiguredAssemblies(), legacyBalance: balance)); return;
            }
            var preparedSession = new GameSession(CreateConfiguredBalance(true, allowLegacyFixtureDefaults), starterDefenses:false);
            var preparedProfile = FreshProfile(preparedSession.Balance);
            PrepareRewardProfileReplacement();
            assemblyCommands = null; RunLoadout = null; runRequiresSlots = false;
            legacyFixtureDefaults = allowLegacyFixtureDefaults;
            persistence.Reset();workshopMode=true;
            profile=preparedProfile;Workshop=new WorkshopReadModel(profile);
            runLoadout=profile.GetLoadoutBlueprints();
            Initialize(PrepareCampaignPreview(preparedSession));
            InWorkshop=true;WorkshopChanged?.Invoke();
        }

        public void InitializeWorkshop(IWorkshopProfileStore store, bool allowLegacyFixtureDefaults = false)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            // Reject missing/invalid configuration before reading any profile, including the real path.
            var preparedSession = new GameSession(CreateConfiguredBalance(true, allowLegacyFixtureDefaults), starterDefenses:false);
            var freshProfile = FreshProfile(preparedSession.Balance);
            PrepareRewardProfileReplacement();
            var loaded = store.Load();
            assemblyCommands = null; RunLoadout = null; runRequiresSlots = false;
            legacyFixtureDefaults = allowLegacyFixtureDefaults;
            persistence.Attach(store, loaded);workshopMode=true;
            profile=loaded.Source==ProfileLoadSource.NewProfile ? freshProfile : loaded.Profile??freshProfile;
            Workshop=new WorkshopReadModel(profile);RefreshSaveInfo();
            runLoadout=profile.GetLoadoutBlueprints();
            Initialize(PrepareCampaignPreview(preparedSession));
            InWorkshop=true;WorkshopChanged?.Invoke();
        }

        public bool RetrySave(out string reason)
        {
            if (HasPendingRunRewards) return RetryPendingRewards(out reason);
            bool result=persistence.Retry(profile,out reason);
            RefreshSaveInfo();
            if(result)WorkshopChanged?.Invoke();
            return result;
        }

        void RefreshSaveInfo() => persistence.CopyStatusTo(Workshop);

        void PrepareRewardProfileReplacement()
        {
            if (HasPendingRunRewards)
            {
                bool saved = RetryPendingRewards(out var reason);
                // A commit observer may have queued a newer unpaid result during the successful retry.
                if (!saved || HasPendingRunRewards)
                    throw new InvalidOperationException(string.IsNullOrEmpty(reason) ? UserText.Message("run.reward.pending") : reason);
            }
            runRewards = null;
            ResetMissionAttempt();
        }

        GameBalance CreateWorkshopBalance()
        {
            var baseline=CreateConfiguredBalance(true,legacyFixtureDefaults);
            FreezeModernizationTechnology(baseline);
            return CampaignEnabled?MissionBalanceFactory.Create(baseline,SelectedMission):baseline;
        }
        // Published profile candidates are accepted only after their atomic save succeeds.
        // The session takes its own snapshot; a victory cannot alter technology mid-attempt.
        public int UnlockedModernizationRank => profile?.MissionWins("notebook-1-page-3") > 0 ? 3 :
            profile?.MissionWins("notebook-1-page-1") > 0 ? 2 : 1;
        void FreezeModernizationTechnology(GameBalance balance)
        {
            if (balance.Mechanics.FieldModernization)
                balance.Mechanics.MaximumModernizationRank = UnlockedModernizationRank;
        }
        ComponentCatalog ConfiguredAssemblies() => balanceAsset?.dataPreset?.database?.assemblyCatalog?.CreateSnapshot();
        WorkshopProfile FreshProfile(GameBalance balance) => WorkshopProfile.CreateNew(ConfiguredAssemblies(), balance.Content, balance);

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
            => Initialize(replay, false);

        void Initialize(GameSession replay, bool resuming, bool isolateObservers = false, bool workshopPreview = false)
        {
            session = replay ?? throw new ArgumentNullException(nameof(replay));
            contentSnapshot = session.Balance.ResolveContent();
            InWorkshop=workshopPreview;
            if(profile==null){profile=new WorkshopProfile();Workshop=new WorkshopReadModel(profile);runLoadout=profile.GetLoadoutBlueprints();}
            if (profile.ConfigureContent(contentSnapshot)) persistence.ContentChanged();
            depositedGold=resuming ? replay.GoldEarned : 0;
            submittedRewardWaves=resuming ? replay.CompletedWaves : 0;submittedVictoryBonus=false;
            Session = new RunReadModel(session);
            Workshop.Refresh(session.Balance);
            RefreshSaveInfo();
            TacticalPaused = resuming; MenuSuspended = ModalSuspended = false; Speed = 1; clock.Reset();
            lastEventSequence = resuming ? replay.EventBuffer.LatestSequence : 0; SkippedVisualEvents = 0;
            if (isolateObservers) NotifyContinuationSessionChanged();
            else SessionChanged?.Invoke(Session);
        }

        void Update()
        {
            if (AutomaticSimulation) AdvanceFrame(Time.unscaledDeltaTime, Paused, Speed);
        }

        /// <summary>A validation stand owns time explicitly; one press advances exactly one simulation tick.</summary>
        public int StepSimulation()
        {
            if (session == null || InWorkshop || continuationTransition) return 0;
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
            if (session == null || InWorkshop || continuationTransition) {clock.ClearDebt();return 0;}
            int steps = clock.AdvanceFrame(realDelta, paused || Paused, speed);
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

        public void TogglePause() { if (CommandsSuspended) return; TacticalPaused = !TacticalPaused; clock.ClearDebt(); }
        public void SetMenuSuspended(bool value) { MenuSuspended = value; clock.ClearDebt(); }
        public void SetModalSuspended(bool value) { ModalSuspended = value; clock.ClearDebt(); }
        public void ToggleSpeed() { if (!CommandsSuspended) Speed = Speed == 1 ? 2 : 1; }
        public void NextWave() { if (session != null && !InWorkshop && !CommandsSuspended && !continuationTransition) session.StartNextWave(); }
        public void Restart() {if(workshopMode)OpenWorkshop();else Initialize(legacyFixtureDefaults);}
        public bool Build(IReadOnlyList<Cell> cells, StructureKind kind, MaterialTier tier, out string reason, float? facingDegrees = null)
        {
            if (!CanBuildTool(kind, out reason)) return false;
            return session.TryBuild(cells,kind,tier,out reason,facingDegrees);
        }
        public BuildQuote QuoteBuild(IReadOnlyList<Cell> cells, StructureKind kind, MaterialTier tier)
            => CanBuildTool(kind, out var reason) ? session.QuoteBuild(cells, kind, tier) : RejectedQuote(reason);

        bool CanBuildTool(StructureKind kind, out string reason)
        {
            reason = "";
            if (session == null || InWorkshop || CommandsSuspended) { reason = UserText.Message("copy.e6a1f147d46938e5"); return false; }
            if (runRequiresSlots)
            { reason = UserText.Message("copy.48635af5e97f1a11"); return false; }
            if (workshopMode && !ConstructionToolRules.IsTool(kind))
            { reason = UserText.Message("copy.65fd0a869c823b56"); return false; }
            if (workshopMode)
            {
                var family = kind switch {
                    StructureKind.Spikes => AssemblyStructureFamily.Spikes,
                    StructureKind.Beacon => AssemblyStructureFamily.Beacon,
                    StructureKind.Prism => AssemblyStructureFamily.Prism,
                    _ => AssemblyStructureFamily.None
                };
                if (family != AssemblyStructureFamily.None && !profile.GetStructureOffer(family).Owned)
                { reason = UserText.Message("profile.structure.locked"); return false; }
            }
            return true;
        }

        static BuildQuote RejectedQuote(string reason) => new BuildQuote { Cells = Array.Empty<Cell>(), Reason = reason };
        public bool Erase(Cell cell, out int refund) {refund=0;return !InWorkshop&&!CommandsSuspended&&session!=null&&session.TryErase(cell,out refund);}
        public bool Upgrade(Cell cell, out string reason) {reason=UserText.Message("copy.e6a1f147d46938e5");return !InWorkshop&&!CommandsSuspended&&session!=null&&session.TryUpgrade(cell,out reason);}
        public bool SetPriority(Cell cell, TargetPriority priority) => !InWorkshop&&!CommandsSuspended&&session!=null&&session.SetPriority(cell, priority);
        public ModernizationQuote QuoteModernization(int structureId, ModernizationDirection direction) => session?.QuoteModernization(structureId, direction);
        public ModernizationQuote QuoteColorModernization(int structureId, InkColor color) => session?.QuoteColorModernization(structureId, color);
        public bool Modernize(ModernizationQuote quote, out string reason)
        {
            reason = CommandsSuspended ? "Закройте меню или подтверждение." : "Бой недоступен.";
            return session != null && !InWorkshop && !CommandsSuspended && session.TryModernize(quote, out reason);
        }
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
            => CommitCandidate(candidate, out reason, true);

        bool CommitCandidate(WorkshopProfile candidate, out string reason, bool notify)
        {
            reason="";
            if(!persistence.SaveCandidate(candidate,out reason)){RefreshSaveInfo();return false;}
            profile=candidate;Workshop=new WorkshopReadModel(profile,session.Balance);RefreshSaveInfo();
            if (notify) WorkshopChanged?.Invoke();return true;
        }
        void OnApplicationQuit()
        {
            if (HasPendingRunRewards) RetryPendingRewards(out _);
            persistence.Flush(profile);RefreshSaveInfo();
        }
        void DepositGold()
        {
            if(activeMission!=null&&session.RunId==activeMissionRunId){DepositMissionReward();return;}
            if (runRequiresSlots)
            {
                int completed = session.CompletedWaves;
                bool victory = session.Phase == RunPhase.Victory && completed == 8;
                if (completed <= submittedRewardWaves && (!victory || submittedVictoryBonus)) return;
                submittedRewardWaves = Math.Max(submittedRewardWaves, completed);
                submittedVictoryBonus |= victory;
                RunRewards.Apply(session.RunId, completed, victory);
                return;
            }
            int reward=session.GoldEarned-depositedGold;if(reward<=0)return;
            // Advance this cursor before IO so retry/transition cannot replay the award.
            profile.GrantGold(reward);depositedGold=session.GoldEarned;
            persistence.SaveEarnedReward(profile);
            RefreshSaveInfo();WorkshopChanged?.Invoke();
        }
        public bool SaveBlueprint(TowerBlueprint blueprint,out string reason)
        {
            blueprint=blueprint?.WithContent(contentSnapshot);
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
        public bool UnlockDrawingAid(string id,out string reason)
        {
            if(!CreateCandidate(out var candidate,out reason)||!candidate.UnlockDrawingAid(id,out reason,session.Balance))return false;
            if(profile.IsDrawingUnlocked(id,session.Balance))return true;
            return CommitCandidate(candidate,out reason);
        }
        public bool StartRun(out string reason)
        {
            return StartAssemblyRun(out reason);
        }
        public void OpenWorkshop()
        {
            if (continuationTransition) return;
            OpenWorkshopCore();
        }

        void OpenWorkshopCore()
        {
            var preparedSession = new GameSession(CreateWorkshopBalance(),starterDefenses:false);
            OpenWorkshopCore(preparedSession, false);
        }

        void OpenWorkshopCore(GameSession preparedSession, bool isolateObservers)
        {
            if(session!=null)DepositGold();
            RunLoadout = null; runRequiresSlots = false;
            workshopMode=true;InWorkshop=true;
            Initialize(preparedSession, false, isolateObservers, isolateObservers);
            InWorkshop=true;
            if (isolateObservers) NotifyContinuationWorkshopChanged();
            else WorkshopChanged?.Invoke();
        }
        public TowerPreview PreviewBlueprint(TowerBlueprint blueprint,MaterialTier material=MaterialTier.Pencil)
            =>new TowerPreview(blueprint,session.Balance,material);
        public bool ValidateAssembly(TowerBlueprint blueprint,out string reason)
            =>TowerCatalog.ValidateAssembly(blueprint,session.Balance,out reason);

        public BuildQuote QuoteEntry(IReadOnlyList<Cell> cells, int slot, MaterialTier material, InkColor color)
        {
            if (!TryResolveBuildSlot(slot, material, out var entry, out var resolved, out var reason)) return RejectedQuote(reason);
            return entry.Kind == AlbumEntryKind.Legacy
                ? session.QuoteBlueprint(cells, entry.LegacyBlueprint, material, color)
                : session.QuoteEntry(cells, resolved, material, color);
        }

        public bool BuildEntry(IReadOnlyList<Cell> cells, int slot, MaterialTier material, InkColor color, out string reason, float? facingDegrees = null)
        {
            if (!TryResolveBuildSlot(slot, material, out var entry, out var resolved, out reason)) return false;
            return entry.Kind == AlbumEntryKind.Legacy
                ? session.TryBuildBlueprint(cells, entry.LegacyBlueprint, material, color, out reason, facingDegrees, entry.Id)
                : session.TryBuildEntry(cells, resolved, material, color, out reason, entry.Id, facingDegrees);
        }

        bool TryResolveBuildSlot(int slot, MaterialTier material, out AlbumEntry entry, out ResolvedEntry resolved, out string reason)
        {
            entry = null; resolved = null; reason = UserText.Message("copy.e6a1f147d46938e5");
            if (session == null || InWorkshop || CommandsSuspended) return false;
            reason = UserText.Message("copy.48635af5e97f1a11");
            if (RunLoadout == null || slot < 0 || slot >= RunLoadout.Entries.Count) return false;
            entry = RunLoadout.Entries[slot];
            if (entry == null) return false;
            if (!Enum.IsDefined(typeof(MaterialTier), material))
            { reason = UserText.Message("assembly.material.unsupported"); return false; }
            if (entry.Kind == AlbumEntryKind.Legacy) { reason = ""; return true; }
            try { resolved = RunLoadout.ResolveSlot(slot, material); }
            catch (ArgumentException error)
            {
                LastConfigurationDiagnostic = error.ToString();
                reason = UserText.Message("assembly.material.unsupported");
                return false;
            }
            reason = "";
            return true;
        }

        public BuildQuote QuoteBlueprint(IReadOnlyList<Cell> cells,int slot,MaterialTier material,InkColor color)
        {
            if (RunLoadout != null) return QuoteEntry(cells, slot, material, color);
            if (session == null || InWorkshop || CommandsSuspended) return RejectedQuote(UserText.Message("copy.e6a1f147d46938e5"));
            if(runLoadout==null||slot<0||slot>=runLoadout.Count)return new BuildQuote{Cells=Array.Empty<Cell>(),Reason=UserText.Message("copy.48635af5e97f1a11")};
            return session.QuoteBlueprint(cells,runLoadout[slot],material,color);
        }
        public bool BuildBlueprint(IReadOnlyList<Cell> cells,int slot,MaterialTier material,InkColor color,out string reason, float? facingDegrees = null)
        {
            if (RunLoadout != null) return BuildEntry(cells, slot, material, color, out reason, facingDegrees);
            reason=UserText.Message("copy.e6a1f147d46938e5");if(session == null || InWorkshop || CommandsSuspended)return false;
            if(runLoadout==null||slot<0||slot>=runLoadout.Count){reason=UserText.Message("copy.48635af5e97f1a11");return false;}
            return session.TryBuildBlueprint(cells,runLoadout[slot],material,color,out reason,facingDegrees);
        }
        public bool Paint(Cell cell,InkColor color,out string reason)
        {reason=UserText.Message("copy.e6a1f147d46938e5");return !InWorkshop&&!CommandsSuspended&&session!=null&&session.TryPaint(cell,color,out reason);}
        public bool Blotter(Cell cell,out string reason)
        {reason=UserText.Message("copy.e6a1f147d46938e5");return !InWorkshop&&!CommandsSuspended&&session!=null&&session.TryBlotter(cell,out reason);}
        public bool ActivateWell(int index,out string reason)
        {reason=UserText.Message("copy.e6a1f147d46938e5");return !InWorkshop&&!CommandsSuspended&&session!=null&&session.TryActivateWell(index,out reason);}
        public bool ClaimInkFlask(Guid runId, int flaskId, InkColor color, out string reason)
        {
            reason = UserText.Message("copy.e6a1f147d46938e5");
            return !InWorkshop && !CommandsSuspended && session != null && session.TryClaimInkFlask(runId, flaskId, color, out reason);
        }
        public FootprintProbeComparison CompareMortarFootprints(TowerBlueprint blueprint,int mount,MortarFootprint first,MortarFootprint second,FootprintFormation formation)
            => MortarFootprintProbe.Compare(blueprint,mount,first,second,session.Balance,formation);
        public FootprintStudyReport CompareMortarForms(TowerBlueprint blueprint,int mount)
            => FootprintStudy.Run(blueprint,mount,session.Balance);
        public ArmorProbeReport ProbeArmor(TowerBlueprint blueprint,int side=-1)=>ArmorProbe.Run(blueprint,session.Balance,side);
        public DrawingProbeReport ProbeDrawing(TowerBlueprint blueprint,int mount,float distance,DrawingProbeFormation formation)
            =>DrawingProbe.Run(blueprint,mount,distance,formation,session.Balance);
        public float ArmorDefense(TowerBlueprint blueprint)=>TowerCatalog.Defense(blueprint,session.Balance);
        public float MaximumStructureDefense=>session.Balance.Effects.MaximumDefense;
        public FanVolleyProbeReport ProbeFanVolley(TowerBlueprint blueprint,int mount,FanFormation formation)
            => FanVolleyProbe.Run(blueprint,mount,formation,session.Balance);

        public TrialComparison RunTrial(TowerBlueprint blueprint,TrialScenario scenario,bool counterfire=false,int budget=400)
        {
            blueprint=blueprint?.WithContent(contentSnapshot);
            if(!InWorkshop||!profile.ValidateOwned(blueprint,out _,session.Balance))return null;
            var trials=session.Balance.Trials;
            var baseline=new TowerBlueprint("trial.baseline",UserText.Message("copy.5c07091d535b3296"),trials.BaselineChassisId,trials.BaselineWeaponId,content:contentSnapshot);
            return TrialRunner.Compare(blueprint,baseline,session.Balance,scenario,budget,1337,counterfire);
        }
    }
}
