using System;
using PaperDefense.Core;

namespace PaperDefense.Application
{
    public sealed partial class RunController
    {
        CampaignMissionDefinition activeMission;
        Guid activeMissionRunId;
        int missionCompletedWaves, missionStars;
        float missionStartingBaseHealth;
        bool missionVictory, missionFinished;
        public bool CampaignEnabled => balanceAsset != null && balanceAsset.campaignEnabled;
        public CampaignReadModel Campaign => profile == null ? null : new CampaignReadModel(profile);
        public CampaignMissionDefinition SelectedMission => profile != null && profile.IsMissionUnlocked(profile.SelectedMissionId)
            ? CampaignCatalog.Default.Find(profile.SelectedMissionId) : CampaignCatalog.Default.Missions[0];
        public CampaignMissionDefinition ActiveMission => activeMission;
        public CampaignMissionResult MissionResult
        {
            get
            {
                if (activeMission == null || !missionFinished && missionCompletedWaves == 0)
                    return null;
                var saved = profile?.FindCampaignRunReward(activeMissionRunId);
                if (saved != null && saved.CompletedWaves >= missionCompletedWaves && (!missionVictory || saved.Victory) && saved.Stars >= missionStars)
                    return saved;
                return CampaignMissionResult.Preview(activeMission, activeMissionRunId, missionCompletedWaves, missionVictory, missionStars,
                    saved?.Victory == true ? saved.FirstClear : profile.BestMissionStars(activeMission.Id) == 0);
            }
        }
        public bool IsCurrentMissionRewardSaved
        {
            get
            {
                if (activeMission == null || missionCompletedWaves == 0 && !missionVictory)
                    return true;
                var saved = profile?.FindCampaignRunReward(activeMissionRunId);
                return saved != null && saved.CompletedWaves >= missionCompletedWaves && (!missionVictory || saved.Victory) && saved.Stars >= missionStars;
            }
        }
        public CampaignMissionResult FindCampaignRunReward(Guid runId) => profile?.FindCampaignRunReward(runId);
        public bool IsMissionUnlocked(string missionId) => CampaignEnabled && profile != null && profile.IsMissionUnlocked(missionId);
        public GameSession CreateMissionPreview(string missionId)
        {
            var mission = CampaignCatalog.Default.Find(missionId) ?? throw new ArgumentException("Unknown mission.", nameof(missionId));
            return new GameSession(MissionBalanceFactory.Create(CreateConfiguredBalance(true, legacyFixtureDefaults), mission), mission.Seed, false);
        }
        GameSession PrepareCampaignPreview(GameSession prepared)
            => CampaignEnabled ? new GameSession(MissionBalanceFactory.Create(prepared.Balance, SelectedMission), SelectedMission.Seed, false) : prepared;
        public bool SelectMission(string missionId, out string reason)
        {
            reason = "";
            if (!CampaignEnabled)
            {
                reason = "Кампания недоступна для этого набора правил.";
                return false;
            }
            if (!CreateCandidate(out var candidate, out reason) || !candidate.TrySelectMission(missionId, out reason))
                return false;
            if (profile.SelectedMissionId == missionId)
                return true;
            if (!CommitCandidate(candidate, out reason))
                return false;
            RunLoadout = null;
            runRequiresSlots = false;
            Initialize(CreateMissionPreview(missionId));
            InWorkshop = true;
            WorkshopChanged?.Invoke();
            return true;
        }
        void ResetMissionAttempt()
        {
            activeMission = null;
            activeMissionRunId = Guid.Empty;
            missionCompletedWaves = missionStars = 0;
            missionStartingBaseHealth = 0;
            missionVictory = missionFinished = false;
        }
        void BeginMissionAttempt(GameSession prepared)
        {
            ResetMissionAttempt();
            if (!CampaignEnabled)
                return;
            activeMission = SelectedMission;
            activeMissionRunId = prepared.RunId;
            missionStartingBaseHealth = prepared.Balance.BaseHealth;
        }
        void DepositMissionReward()
        {
            missionCompletedWaves = session.CompletedWaves;
            missionVictory = session.Phase == RunPhase.Victory && missionCompletedWaves == activeMission.WaveCount;
            missionFinished = missionVictory || session.Phase == RunPhase.Defeat;
            float health = missionVictory && missionStartingBaseHealth > 0 ? session.BaseHealth / missionStartingBaseHealth : 0;
            missionStars = missionVictory ? health >= .9f ? 3 : health >= .6f ? 2 : 1 : 0;
            if (missionCompletedWaves <= submittedRewardWaves && (!missionVictory || submittedVictoryBonus))
                return;
            submittedRewardWaves = Math.Max(submittedRewardWaves, missionCompletedWaves);
            submittedVictoryBonus |= missionVictory;
            RunRewards.ApplyMission(activeMission, activeMissionRunId, missionCompletedWaves, missionVictory, missionStars);
        }
    }
}
