using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Application
{
    [Serializable]
    internal sealed class MissionContinuationData
    {
        public int Version = 1;
        public string Fingerprint;
        public int MaximumModernizationRank, SubmittedRewardWaves;
        public float StartingBaseHealth;
        public bool RequiresSlots;
        // Occupied keeps empty positions unambiguous through JsonUtility's null-class handling.
        public bool[] Occupied;
        public AlbumEntryData[] Loadout;
        public GameSessionCheckpointData Session;
    }

    [Serializable]
    internal sealed class MissionFingerprintData
    {
        public int ContinuationVersion = 1, SimulationVersion = 1, RandomVersion = 1;
        public GameBalance Rules;
        public ContentCatalogInput Content;
        public AssemblyCatalogInput Assemblies;
        public string MissionId;
        public int MissionRevision, WaveCount, GoldPerWave, XpPerWave, CompletionGold, CompletionXp, FirstClearGold, FirstClearXp;
    }

    public sealed partial class RunController
    {
        string continuationFingerprint;
        bool continuationTransition;
        public bool HasMissionCheckpoint => profile?.MissionCheckpoint != null;
        public MissionCheckpoint MissionCheckpoint => profile?.MissionCheckpoint;

        public bool CanSuspendMission(out string reason)
        {
            reason = "Сохранить миссию можно между волнами, после завершения хотя бы одной волны.";
            if (continuationTransition) { reason = "Дождись завершения сохранения миссии."; return false; }
            if (!CampaignEnabled || profile == null || InWorkshop || session == null || activeMission == null ||
                activeMissionRunId != session.RunId || session.Phase != RunPhase.Intermission || session.CompletedWaves < 1 ||
                session.CompletedWaves >= activeMission.WaveCount || session.WaveIndex != session.CompletedWaves ||
                session.RemainingToSpawn != 0 || session.Enemies.Count != 0 || session.Projectiles.Count != 0 || session.EnemyProjectiles.Count != 0)
                return false;
            if (HasMissionCheckpoint) { reason = "Сначала продолжи или удали уже сохранённую миссию."; return false; }
            if (RunLoadout == null || string.IsNullOrEmpty(continuationFingerprint))
            { reason = "Эту попытку нельзя сохранить для продолжения."; return false; }
            if (!persistence.CanEdit(out reason)) return false;
            reason = ""; return true;
        }

        public bool TrySuspendMission(out string reason)
        {
            if (!CanSuspendMission(out reason)) return false;
            continuationTransition = true;
            try { return SuspendMissionCore(out reason); }
            finally { continuationTransition = false; }
        }

        bool SuspendMissionCore(out string reason)
        {
            MissionCheckpoint checkpoint;
            GameSession preparedPreview;
            try
            {
                // Configuration errors must fail before the checkpoint/reward candidate is written.
                preparedPreview = new GameSession(CreateWorkshopBalance(), starterDefenses: false);
                if (!session.TryCaptureIntermissionCheckpoint(out var captured, out reason)) return false;
                var data = new MissionContinuationData {
                    Fingerprint = continuationFingerprint,
                    MaximumModernizationRank = session.Balance.Mechanics.MaximumModernizationRank,
                    SubmittedRewardWaves = session.CompletedWaves, StartingBaseHealth = missionStartingBaseHealth,
                    RequiresSlots = runRequiresSlots, Session = captured,
                    Occupied = RunLoadout.Entries.Select(entry => entry != null).ToArray(),
                    Loadout = RunLoadout.Entries.Select(entry => entry == null ? null : new AlbumEntryData(entry)).ToArray()
                };
                checkpoint = new MissionCheckpoint(session.RunId, activeMission.Id, activeMission.Revision, session.CompletedWaves, JsonUtility.ToJson(data));
            }
            catch (Exception error) when (ContinuationDataError(error))
            {
                LastConfigurationDiagnostic = error.ToString();
                reason = "Не удалось подготовить сохранение этой попытки. Миссия остаётся открытой."; return false;
            }
            if (!CreateRewardCandidate(out var candidate, out reason) || !MergePendingRewards(candidate, out reason)) return false;
            if (!candidate.TryApplyMissionReward(activeMission, session.RunId, session.CompletedWaves, false, 0, out reason)) return false;
            candidate.SetMissionCheckpoint(checkpoint);
            if (!CommitContinuationCandidate(candidate, out reason)) return false;
            // Already covered by the same commit; exiting must not create a second reward write.
            submittedRewardWaves = session.CompletedWaves;
            OpenWorkshopCore(preparedPreview, true); reason = ""; return true;
        }

        public bool CheckMissionCheckpoint(out string reason)
        {
            if (continuationTransition) { reason = "Дождись завершения сохранения миссии."; return false; }
            return PrepareMissionContinuation(out _, out _, out _, out _, out _, out reason);
        }

        public bool TryResumeMission(out string reason)
        {
            if (continuationTransition) { reason = "Дождись завершения сохранения миссии."; return false; }
            continuationTransition = true;
            try { return ResumeMissionCore(out reason); }
            finally { continuationTransition = false; }
        }

        bool ResumeMissionCore(out string reason)
        {
            if (!PrepareMissionContinuation(out var restored, out var loadout, out var blueprints, out var data, out var mission, out reason)) return false;
            if (!CreateRewardCandidate(out var candidate, out reason) || !MergePendingRewards(candidate, out reason)) return false;
            candidate.SetMissionCheckpoint(null);
            if (!CommitContinuationCandidate(candidate, out reason)) return false;
            BindContinuationMission(restored, mission, data.StartingBaseHealth);
            missionCompletedWaves = restored.CompletedWaves;
            RunLoadout = loadout; runLoadout = blueprints;
            runRequiresSlots = data.RequiresSlots; workshopMode = true;
            continuationFingerprint = data.Fingerprint;
            Initialize(restored, true, true);
            NotifyContinuationWorkshopChanged();
            reason = ""; return true;
        }

        public bool TryDiscardMissionCheckpoint(out string reason)
        {
            if (continuationTransition) { reason = "Дождись завершения сохранения миссии."; return false; }
            if (!Editing(out reason)) return false;
            if (!HasMissionCheckpoint) { reason = ""; return true; }
            continuationTransition = true;
            try
            {
                if (!CommitCheckpointReplacement(out reason)) return false;
                NotifyContinuationWorkshopChanged(); return true;
            }
            finally { continuationTransition = false; }
        }

        public bool StartRunReplacingCheckpoint(out string reason) => StartAssemblyRun(out reason, true);

        bool CommitCheckpointReplacement(out string reason)
        {
            if (!CreateCandidate(out var candidate, out reason) || !MergePendingRewards(candidate, out reason)) return false;
            candidate.SetMissionCheckpoint(null);
            return CommitContinuationCandidate(candidate, out reason);
        }

        bool MergePendingRewards(WorkshopProfile candidate, out string reason)
        { reason = ""; return runRewards == null || runRewards.ApplyPendingToCandidate(candidate, out reason); }

        bool CommitContinuationCandidate(WorkshopProfile candidate, out string reason)
        {
            try { return CommitCandidate(candidate, out reason, false); }
            finally { runRewards?.ReconcilePublishedRewards(); }
        }

        // A committed transition must stay accessible even if one view fails to refresh.
        // Keep ordinary profile-command observer semantics unchanged.
        void NotifyContinuationWorkshopChanged()
        {
            var listeners = WorkshopChanged?.GetInvocationList();
            if (listeners == null) return;
            foreach (Action listener in listeners)
                try { listener(); }
                catch (Exception error) { LastConfigurationDiagnostic = error.ToString(); }
        }
        void NotifyContinuationSessionChanged()
        {
            var listeners = SessionChanged?.GetInvocationList();
            if (listeners == null) return;
            foreach (Action<IRunReadModel> listener in listeners)
                try { listener(Session); }
                catch (Exception error) { LastConfigurationDiagnostic = error.ToString(); }
        }

        bool PrepareMissionContinuation(out GameSession restored, out RunLoadoutSnapshot loadout,
            out IReadOnlyList<TowerBlueprint> blueprints, out MissionContinuationData data, out CampaignMissionDefinition mission, out string reason)
        {
            restored = null; loadout = null; blueprints = null; data = null; mission = null;
            if (!Editing(out reason) || !persistence.CanEdit(out reason)) return false;
            var saved = MissionCheckpoint;
            if (!CampaignEnabled || saved == null) { reason = "Нет сохранённой миссии для продолжения."; return false; }
            try
            {
                mission = CampaignCatalog.Default.Find(saved.MissionId);
                if (mission == null || mission.Revision != saved.MissionRevision)
                { reason = "Правила сохранённой миссии изменились. Можно удалить сохранение и начать новую попытку."; return false; }
                data = JsonUtility.FromJson<MissionContinuationData>(saved.PayloadJson);
                if (data == null || data.Version != 1 || data.Session == null || data.Occupied == null || data.Occupied.Length != 8 ||
                    data.Loadout == null || data.Loadout.Length != 8 || data.MaximumModernizationRank < 1 || data.MaximumModernizationRank > 3 ||
                    data.Session.RunId != saved.RunId.ToString("N") || data.Session.CompletedWaves != saved.CompletedWaves ||
                    data.SubmittedRewardWaves != saved.CompletedWaves || data.Session.WaveIndex != saved.CompletedWaves ||
                    data.Session.Phase != RunPhase.Intermission || float.IsNaN(data.StartingBaseHealth) || float.IsInfinity(data.StartingBaseHealth) || data.StartingBaseHealth <= 0)
                { reason = "Сохранение миссии повреждено или имеет неподдерживаемый формат. Постоянный прогресс сохранён."; return false; }
                var balance = CreateConfiguredBalance(true, legacyFixtureDefaults);
                balance.Mechanics.MaximumModernizationRank = data.MaximumModernizationRank;
                balance = MissionBalanceFactory.Create(balance, mission); balance.Content = balance.ResolveContent();
                if (data.StartingBaseHealth != balance.BaseHealth || data.Fingerprint != ContinuationFingerprint(balance, mission))
                { reason = "Правила или детали игры изменились после сохранения. Можно удалить сохранение и начать новую попытку."; return false; }
                var receipt = profile.FindCampaignRunReward(saved.RunId);
                if (receipt == null || receipt.MissionId != saved.MissionId || receipt.CompletedWaves != saved.CompletedWaves ||
                    receipt.WaveCount != mission.WaveCount || receipt.Victory)
                { reason = "Сохранение миссии не совпадает с сохранёнными наградами. Продолжение недоступно."; return false; }
                var entries = new AlbumEntry[8];
                for (int slot = 0; slot < entries.Length; slot++)
                    if (data.Occupied[slot])
                    {
                        if (data.Loadout[slot] == null) { reason = "В сохранённом наборе отсутствует чертёж."; return false; }
                        entries[slot] = data.Loadout[slot].CreateSnapshot();
                    }
                bool requiresSlots = ConfiguredAssemblyCatalog != null || entries.Any(entry => entry != null && entry.Kind != AlbumEntryKind.Legacy);
                if (data.RequiresSlots != requiresSlots) { reason = "Формат боевого набора изменился."; return false; }
                loadout = new RunLoadoutSnapshot(entries, balance);
                blueprints = WorkshopProfile.ProjectLoadoutBlueprints(entries, balance.Content);
                if (!GameSession.TryRestoreIntermissionCheckpoint(balance, data.Session, out restored, out reason)) return false;
                reason = ""; return true;
            }
            catch (Exception error) when (ContinuationDataError(error))
            {
                restored = null; loadout = null; blueprints = null;
                reason = "Не удалось прочитать сохранённую миссию. Постоянный прогресс сохранён.";
                LastConfigurationDiagnostic = error.ToString(); return false;
            }
        }

        static bool ContinuationDataError(Exception error)
            => error is ArgumentException || error is NotSupportedException || error is InvalidOperationException || error is OverflowException;

        void BindContinuationMission(GameSession prepared, CampaignMissionDefinition mission, float startingHealth)
        {
            ResetMissionAttempt();
            if (mission == null) return;
            activeMission = mission; activeMissionRunId = prepared.RunId; missionStartingBaseHealth = startingHealth;
        }

        string ContinuationFingerprint(GameBalance balance, CampaignMissionDefinition mission)
        {
            var preset = balanceAsset?.dataPreset;
            var content = preset?.database?.catalog?.CreateCopy();
            if (content != null) preset.overrides?.Apply(content);
            var input = new MissionFingerprintData {
                Rules = balance, Content = content, Assemblies = preset?.database?.assemblyCatalog?.catalog,
                MissionId = mission.Id, MissionRevision = mission.Revision, WaveCount = mission.WaveCount,
                GoldPerWave = mission.GoldPerWave, XpPerWave = mission.XpPerWave, CompletionGold = mission.CompletionGold,
                CompletionXp = mission.CompletionXp, FirstClearGold = mission.FirstClearGold, FirstClearXp = mission.FirstClearXp
            };
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(input)))).Replace("-", "").ToLowerInvariant();
        }
    }
}
