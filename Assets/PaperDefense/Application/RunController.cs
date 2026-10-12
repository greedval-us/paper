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
        bool workshopMode;
        IWorkshopProfileStore profileStore;
        bool profileCanWrite, profileDirty;
        ProfileSaveState profileSaveState = ProfileSaveState.MemoryOnly;
        string profileSaveMessage = "Только в памяти; профиль не записывается.", lastWriteError = "";
        int depositedGold;
        public event Action<IRunReadModel> SessionChanged;
        public event Action<SimulationEvent> SimulationEventReceived;
        readonly FixedStepClock clock = new FixedStepClock();
        long lastEventSequence;
        public double DiscardedSimulationSeconds => clock.DiscardedSeconds;
        public long SkippedVisualEvents { get; private set; }

        public void Initialize()
        {
            ResetPersistence();workshopMode=false;InWorkshop=false;
            profile=new WorkshopProfile();Workshop=new WorkshopReadModel(profile);runLoadout=profile.GetLoadoutBlueprints();
            Initialize(new GameSession(balanceAsset != null ? balanceAsset.CreateBalance() : GameBalance.CreateDefault()));
        }

        public void InitializeWorkshop(bool persistent=true)
        {
            if (persistent) { InitializeWorkshop(new WorkshopProfileStore(WorkshopPersistence.ProfilePath)); return; }
            ResetPersistence();workshopMode=true;
            profile=new WorkshopProfile();Workshop=new WorkshopReadModel(profile);
            runLoadout=profile.GetLoadoutBlueprints();
            Initialize(new GameSession(CreateWorkshopBalance(),starterDefenses:false));
            InWorkshop=true;WorkshopChanged?.Invoke();
        }

        public void InitializeWorkshop(IWorkshopProfileStore store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            var loaded = store.Load();
            profileStore=store;workshopMode=true;
            profileCanWrite=loaded.CanWrite&&loaded.Source!=ProfileLoadSource.RecoveryRequired;
            profileDirty=profileCanWrite&&(loaded.Source==ProfileLoadSource.NewProfile||loaded.Source==ProfileLoadSource.Backup);
            lastWriteError="";
            profileSaveState=!profileCanWrite?ProfileSaveState.RecoveryRequired:profileDirty?ProfileSaveState.Unsaved:ProfileSaveState.Saved;
            profileSaveMessage=loaded.Message;
            if(string.IsNullOrEmpty(profileSaveMessage))profileSaveMessage=profileDirty?"Профиль ещё не записан.":"Профиль сохранён.";
            profile=loaded.Profile??new WorkshopProfile();Workshop=new WorkshopReadModel(profile);RefreshSaveInfo();
            runLoadout=profile.GetLoadoutBlueprints();
            Initialize(new GameSession(CreateWorkshopBalance(),starterDefenses:false));
            InWorkshop=true;WorkshopChanged?.Invoke();
        }

        public bool RetrySave(out string reason)
        {
            if(profileStore==null){reason="Режим только в памяти: запись профиля отключена.";return false;}
            if(!profileCanWrite){reason=profileSaveMessage;return false;}
            bool result=WriteProfile(profile,false,out reason);
            RefreshSaveInfo();
            if(result)WorkshopChanged?.Invoke();
            return result;
        }

        void ResetPersistence()
        {
            profileStore=null;profileCanWrite=false;profileDirty=false;lastWriteError="";
            profileSaveState=ProfileSaveState.MemoryOnly;profileSaveMessage="Только в памяти; профиль не записывается.";
        }

        void RefreshSaveInfo()
        {
            if(Workshop==null)return;
            Workshop.SaveState=profileSaveState;Workshop.SaveMessage=profileSaveMessage;
            Workshop.HasUnsavedChanges=profileDirty;Workshop.LastWriteError=lastWriteError;
        }

        // A failed manual candidate never becomes the current profile. Earned gold is already current.
        bool WriteProfile(WorkshopProfile candidate,bool manual,out string reason)
        {
            if(!profileCanWrite){reason=profileSaveMessage;return false;}
            var result=profileStore.Save(candidate);
            if(!result.Success)
            {
                lastWriteError=string.IsNullOrEmpty(result.Message)?"Не удалось записать профиль.":result.Message;
                // Load only re-reads source safety; never publish its fallback over earned memory data.
                // The file store keeps protection sticky, including a future version discovered during Save.
                var source=profileStore.Load();
                if(!source.CanWrite||source.Source==ProfileLoadSource.RecoveryRequired)
                {
                    profileCanWrite=false;profileSaveState=ProfileSaveState.RecoveryRequired;
                    profileSaveMessage=(manual?"Изменение не применено. ":"")+
                        (string.IsNullOrEmpty(source.Message)?"Профиль требует восстановления; запись защищена.":source.Message);
                    if(profileDirty)profileSaveMessage+=" Несохранённые изменения остаются только в памяти.";
                    reason=profileSaveMessage;RefreshSaveInfo();return false;
                }
                profileSaveState=ProfileSaveState.WriteFailed;
                profileSaveMessage=manual?"Изменение не применено. "+lastWriteError+" Повторите это действие.":
                    "Запись не удалась. "+lastWriteError+(profileDirty?" Изменения остаются только в памяти до успешной записи.":"");
                reason=profileSaveMessage;RefreshSaveInfo();return false;
            }
            bool rejectedManual=!profileDirty&&!string.IsNullOrEmpty(lastWriteError);
            profileDirty=false;lastWriteError="";profileSaveState=ProfileSaveState.Saved;
            profileSaveMessage="Профиль сохранён."+(!manual&&rejectedManual?" Отклонённое действие повторите отдельно.":"");
            reason=profileSaveMessage;return true;
        }

        GameBalance CreateWorkshopBalance()
        {
            if(balanceAsset!=null)return balanceAsset.CreateWorkshopBalance();
            var balance=GameBalancePresets.CreateTargetWorkshop();GameBalanceAsset.AddWorkshopThreats(balance);
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
            Workshop.Refresh(session.Balance);
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
        bool CreateCandidate(out WorkshopProfile candidate,out string reason)
        {
            candidate=null;
            if(!Editing(out reason))return false;
            if(profileStore!=null&&!profileCanWrite){reason=profileSaveMessage;return false;}
            candidate=profile.CreateWorkingCopy();return true;
        }
        bool CommitCandidate(WorkshopProfile candidate,out string reason)
        {
            reason="";
            if(profileStore!=null&&!WriteProfile(candidate,true,out reason))return false;
            profile=candidate;Workshop=new WorkshopReadModel(profile,session.Balance);RefreshSaveInfo();
            WorkshopChanged?.Invoke();return true;
        }
        void OnApplicationQuit()
        {
            if(profileStore==null||!profileCanWrite||!profileDirty||profile==null)return;
            WriteProfile(profile,false,out _);RefreshSaveInfo();
        }
        void DepositGold()
        {
            int reward=session.GoldEarned-depositedGold;if(reward<=0)return;
            // Advance this cursor before IO so retry/transition cannot replay the award.
            profile.GrantGold(reward);depositedGold=session.GoldEarned;
            if(profileStore!=null)
            {
                profileDirty=true;
                if(profileCanWrite)
                {profileSaveState=ProfileSaveState.Unsaved;WriteProfile(profile,false,out _);}
                else profileSaveState=ProfileSaveState.RecoveryRequired;
            }
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
            var balance=CreateWorkshopBalance();
            var selection=profile.GetLoadoutBlueprints();
            foreach(var blueprint in selection)if(!profile.ValidateOwned(blueprint,out reason,balance))return false;
            runLoadout=selection;InWorkshop=false;
            Initialize(new GameSession(balance,starterDefenses:false));
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
            if(!InWorkshop||!profile.ValidateOwned(blueprint,out _,session.Balance))return null;
            var baseline=new TowerBlueprint("trial.baseline","Одиночные пулемёты",ChassisKind.Standard,StructureKind.Gun);
            return TrialRunner.Compare(blueprint,baseline,session.Balance,scenario,budget,1337,counterfire);
        }
        public TrialComparison RunQualityTrial(TowerBlueprint blueprint,TrialScenario scenario,bool counterfire=false,int budget=400)
        {
            if(!InWorkshop||!profile.ValidateOwned(blueprint,out _,session.Balance))return null;
            return TrialRunner.CompareQuality(blueprint,session.Balance,scenario,budget,1337,counterfire);
        }
    }
}
