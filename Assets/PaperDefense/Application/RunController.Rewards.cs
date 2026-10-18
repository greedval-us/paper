using System;
using PaperDefense.Core;

namespace PaperDefense.Application
{
    public sealed partial class RunController
    {
        RunRewardService runRewards;
        public RunRewardService RunRewards => runRewards ?? new RunRewardService(this);
        public bool HasPendingRunRewards => runRewards != null && runRewards.HasPending;

        internal void RegisterRunRewardService(RunRewardService service)
        {
            if (runRewards != null) throw new InvalidOperationException("A reward service is already attached to this profile.");
            runRewards = service;
        }

        internal bool IsCurrentRunRewardService(RunRewardService service) => ReferenceEquals(runRewards, service);

        public bool RetryPendingRewards(out string reason)
        {
            var result = RunRewards.RetryPending();
            reason = result.Success ? "" : result.Diagnostics.Count > 0 && result.Diagnostics[0].Arguments.Count > 0
                ? result.Diagnostics[0].Arguments[0] : result.Code;
            return result.Success;
        }

        internal bool CreateRewardCandidate(out WorkshopProfile candidate, out string reason)
        {
            candidate = null;
            if (!persistence.CanEdit(out reason)) { RefreshSaveInfo(); return false; }
            if (profile == null) { reason = "profile.edit.unavailable"; return false; }
            candidate = profile.CreateWorkingCopy();
            return true;
        }

        internal bool CommitRewardCandidate(WorkshopProfile candidate, out string reason)
            => CommitCandidate(candidate, out reason);

        internal void SetPendingRunRewards(RunRewardService service, bool pending)
        {
            if (!IsCurrentRunRewardService(service)) return;
            persistence.SetPendingRunRewards(pending); RefreshSaveInfo();
        }
    }
}
