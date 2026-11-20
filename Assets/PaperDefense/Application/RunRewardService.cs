using System;
using System.Collections.Generic;
using System.Linq;
using PaperDefense.Core;

namespace PaperDefense.Application
{
    /// <summary>Unpaid cumulative intents are rebuilt on the latest published profile for every commit attempt.</summary>
    public sealed class RunRewardService
    {
        readonly RunController owner;
        readonly Dictionary<Guid, PendingReward> pending = new Dictionary<Guid, PendingReward>();

        sealed class PendingReward
        {
            internal readonly Guid RunId;
            internal int Waves;
            internal bool Victory;
            internal CampaignMissionDefinition Mission;
            internal int Stars;
            internal PendingReward(Guid runId, int waves, bool victory)
            {
                RunId = runId;
                Waves = waves;
                Victory = victory;
            }
        }

        public RunRewardService(RunController owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            owner.RegisterRunRewardService(this);
        }

        public bool HasPending => pending.Count != 0;

        // Continuation writes its checkpoint and every outstanding reward into the same candidate.
        internal bool ApplyPendingToCandidate(WorkshopProfile candidate, out string reason)
        {
            reason = "";
            if (!owner.IsCurrentRunRewardService(this)) { reason = "profile.reward.stale"; return false; }
            foreach (var intent in pending.Values.ToArray())
            {
                bool accepted = intent.Mission == null
                    ? candidate.TryApplyRunReward(intent.RunId, intent.Waves, intent.Victory, out reason)
                    : candidate.TryApplyMissionReward(intent.Mission, intent.RunId, intent.Waves, intent.Victory, intent.Stars, out reason);
                if (!accepted) return false;
            }
            return true;
        }
        internal void ReconcilePublishedRewards() => RetirePublishedIntents();

        public AssemblyCommandResult ApplyMission(CampaignMissionDefinition mission, Guid runId, int completedWaves, bool victory, int stars)
        {
            if (!owner.IsCurrentRunRewardService(this))
                return AssemblyCommandResult.Fail("profile.reward.stale", "run");
            try
            {
                CampaignMissionResult.Preview(mission, runId, completedWaves, victory, stars, false);
            }
            catch (ArgumentException) { return AssemblyCommandResult.Fail("profile.reward.invalid", "run"); }
            var saved = owner.AssemblyProfile?.FindCampaignRunReward(runId);
            if (owner.AssemblyProfile == null || !owner.AssemblyProfile.IsMissionUnlocked(mission.Id))
                return AssemblyCommandResult.Fail("profile.mission.locked", "mission");
            if (saved != null && saved.MissionId != mission.Id || owner.AssemblyProfile?.FindRunReward(runId) != null)
                return AssemblyCommandResult.Fail("profile.reward.mission_mismatch", "run");
            if (completedWaves == 0)
                return AssemblyCommandResult.Ok(runId.ToString("N"));
            if (Covers(saved, completedWaves, victory, stars))
            {
                RetirePublishedIntents();
                return AssemblyCommandResult.Ok(runId.ToString("N"));
            }
            if (pending.TryGetValue(runId, out var intent))
            {
                if (intent.Mission?.Id != mission.Id)
                    return AssemblyCommandResult.Fail("profile.reward.mission_mismatch", "run");
                intent.Waves = Math.Max(intent.Waves, completedWaves);
                intent.Victory |= victory;
                intent.Stars = Math.Max(intent.Stars, stars);
            }
            else
                pending.Add(runId, new PendingReward(runId, completedWaves, victory) { Mission = mission, Stars = stars });
            owner.SetPendingRunRewards(this, true);
            return RetryPending();
        }

        public AssemblyCommandResult Apply(Guid runId, int completedWaves, bool victory)
        {
            if (!owner.IsCurrentRunRewardService(this))
                return AssemblyCommandResult.Fail("profile.reward.stale", "run");
            if (runId == Guid.Empty || completedWaves < 0 || completedWaves > 8 || (victory && completedWaves != 8))
                return AssemblyCommandResult.Fail("profile.reward.invalid", "run");
            string id = runId.ToString("N");
            if (owner.AssemblyProfile?.FindCampaignRunReward(runId) != null || pending.TryGetValue(runId, out var bound) && bound.Mission != null)
                return AssemblyCommandResult.Fail("profile.reward.mission_mismatch", "run");
            if (completedWaves == 0)
                return AssemblyCommandResult.Ok(id);
            if (Covers(owner.AssemblyProfile?.FindRunReward(runId), completedWaves, victory))
            {
                RetirePublishedIntents();
                return AssemblyCommandResult.Ok(id);
            }
            if (pending.TryGetValue(runId, out var intent))
            {
                intent.Waves = Math.Max(intent.Waves, completedWaves);
                intent.Victory |= victory;
            }
            else
                pending.Add(runId, new PendingReward(runId, completedWaves, victory));
            owner.SetPendingRunRewards(this, true);
            var result = RetryPending();
            return result.Success ? AssemblyCommandResult.Ok(id) : result;
        }

        public AssemblyCommandResult RetryPending()
        {
            if (!owner.IsCurrentRunRewardService(this))
                return AssemblyCommandResult.Fail("profile.reward.stale", "run");
            RetirePublishedIntents();
            if (!HasPending)
                return AssemblyCommandResult.Ok(null);
            if (!owner.CreateRewardCandidate(out var candidate, out var reason))
                return AssemblyCommandResult.Fail("profile.edit.unavailable", "profile", reason);
            foreach (var intent in pending.Values.ToArray())
            {
                bool accepted = intent.Mission == null ? candidate.TryApplyRunReward(intent.RunId, intent.Waves, intent.Victory, out var code)
                    : candidate.TryApplyMissionReward(intent.Mission, intent.RunId, intent.Waves, intent.Victory, intent.Stars, out code);
                if (!accepted)
                    return AssemblyCommandResult.Fail(code, "run");
            }
            try
            {
                return owner.CommitRewardCandidate(candidate, out reason)
                    ? AssemblyCommandResult.Ok(null)
                    : AssemblyCommandResult.Fail("profile.write_failed", "profile", reason);
            }
            finally
            {
                // Publication precedes observers. A throwing observer must not leave an already paid intent pending.
                RetirePublishedIntents();
            }
        }

        void RetirePublishedIntents()
        {
            if (!owner.IsCurrentRunRewardService(this))
                return;
            foreach (var pair in pending.ToArray())
                if (pair.Value.Mission == null ? Covers(owner.AssemblyProfile?.FindRunReward(pair.Key), pair.Value.Waves, pair.Value.Victory)
                    : Covers(owner.AssemblyProfile?.FindCampaignRunReward(pair.Key), pair.Value.Waves, pair.Value.Victory, pair.Value.Stars))
                    pending.Remove(pair.Key);
            owner.SetPendingRunRewards(this, HasPending);
        }

        static bool Covers(RunRewardReceipt receipt, int waves, bool victory)
            => receipt != null && receipt.Waves >= waves && (!victory || receipt.VictoryBonus);
        static bool Covers(CampaignMissionResult receipt, int waves, bool victory, int stars)
            => receipt != null && receipt.CompletedWaves >= waves && (!victory || receipt.Victory) && receipt.Stars >= stars;
    }
}
