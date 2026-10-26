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
            internal PendingReward(Guid runId, int waves, bool victory)
            { RunId = runId; Waves = waves; Victory = victory; }
        }

        public RunRewardService(RunController owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            owner.RegisterRunRewardService(this);
        }

        public bool HasPending => pending.Count != 0;

        public AssemblyCommandResult Apply(Guid runId, int completedWaves, bool victory)
        {
            if (!owner.IsCurrentRunRewardService(this)) return AssemblyCommandResult.Fail("profile.reward.stale", "run");
            if (runId == Guid.Empty || completedWaves < 0 || completedWaves > 8 || (victory && completedWaves != 8))
                return AssemblyCommandResult.Fail("profile.reward.invalid", "run");
            string id = runId.ToString("N");
            if (completedWaves == 0) return AssemblyCommandResult.Ok(id);
            if (Covers(owner.AssemblyProfile?.FindRunReward(runId), completedWaves, victory))
            {
                RetirePublishedIntents();
                return AssemblyCommandResult.Ok(id);
            }
            if (pending.TryGetValue(runId, out var intent))
            { intent.Waves = Math.Max(intent.Waves, completedWaves); intent.Victory |= victory; }
            else pending.Add(runId, new PendingReward(runId, completedWaves, victory));
            owner.SetPendingRunRewards(this, true);
            var result = RetryPending();
            return result.Success ? AssemblyCommandResult.Ok(id) : result;
        }

        public AssemblyCommandResult RetryPending()
        {
            if (!owner.IsCurrentRunRewardService(this)) return AssemblyCommandResult.Fail("profile.reward.stale", "run");
            RetirePublishedIntents();
            if (!HasPending) return AssemblyCommandResult.Ok(null);
            if (!owner.CreateRewardCandidate(out var candidate, out var reason))
                return AssemblyCommandResult.Fail("profile.edit.unavailable", "profile", reason);
            foreach (var intent in pending.Values.ToArray())
                if (!candidate.TryApplyRunReward(intent.RunId, intent.Waves, intent.Victory, out var code))
                    return AssemblyCommandResult.Fail(code, "run");
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
            if (!owner.IsCurrentRunRewardService(this)) return;
            foreach (var pair in pending.ToArray())
                if (Covers(owner.AssemblyProfile?.FindRunReward(pair.Key), pair.Value.Waves, pair.Value.Victory))
                    pending.Remove(pair.Key);
            owner.SetPendingRunRewards(this, HasPending);
        }

        static bool Covers(RunRewardReceipt receipt, int waves, bool victory)
            => receipt != null && receipt.Waves >= waves && (!victory || receipt.VictoryBonus);
    }
}
