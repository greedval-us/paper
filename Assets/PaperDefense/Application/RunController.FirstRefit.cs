using System;
using System.Linq;
using PaperDefense.Core;

namespace PaperDefense.Application
{
    public sealed partial class RunController
    {
        Guid firstRefitStartedRunId;

        public bool FirstRefitTutorialHandled => profile?.FirstRefitTutorialHandled ?? false;

        bool CanEditFirstRefitProgress => CampaignEnabled && profile != null && !FirstRefitTutorialHandled &&
            !HasPendingRunRewards && profile.IsUnlocked("head.long") && persistence.CanEdit(out _) &&
            (InWorkshop || session?.Phase == RunPhase.Victory);

        bool HasStartedFirstRefitTutorial
        {
            get
            {
                if (firstRefitStartedRunId == Guid.Empty || !CanEditFirstRefitProgress) return false;
                // Bind activation to an earned receipt in this profile, not the result of the latest attempt.
                var receipt = profile.FindCampaignRunReward(firstRefitStartedRunId);
                return receipt != null && receipt.MissionId == "notebook-1-page-1" && receipt.Victory && receipt.FirstClear;
            }
        }

        public bool CanOfferFirstRefitTutorial
        {
            get
            {
                if (!CanEditFirstRefitProgress) return false;
                var result = MissionResult;
                return result != null && result.MissionId == "notebook-1-page-1" && result.Victory &&
                    result.FirstClear && IsCurrentMissionRewardSaved;
            }
        }

        public bool BeginFirstRefitTutorial(out string reason)
        {
            reason = "";
            if (HasStartedFirstRefitTutorial) return true;
            if (!CanOfferFirstRefitTutorial)
            {
                reason = "Обучение доступно после сохранённой первой победы.";
                return false;
            }
            firstRefitStartedRunId = MissionResult.RunId;
            return true;
        }

        public bool DismissFirstRefitTutorial(out string reason)
        {
            reason = "";
            if (FirstRefitTutorialHandled) return true;
            if (!CanOfferFirstRefitTutorial && !HasStartedFirstRefitTutorial)
            {
                reason = "Обучение доступно после сохранённой первой победы.";
                return false;
            }
            // The invitation can be dismissed from the result screen before opening the workshop.
            if (!CreateRewardCandidate(out var candidate, out reason)) return false;
            candidate.MarkFirstRefitTutorialHandled();
            return CommitCandidate(candidate, out reason);
        }

        public bool CompleteFirstRefitTutorial(string entryId, out string reason)
        {
            if (!Editing(out reason)) return false;
            if (FirstRefitTutorialHandled) return true;
            if (!CanOfferFirstRefitTutorial && !HasStartedFirstRefitTutorial)
            {
                reason = "Обучение доступно после сохранённой первой победы.";
                return false;
            }
            if (!CreateCandidate(out var candidate, out reason)) return false;
            var entry = candidate.Entries.FirstOrDefault(item => item.Id == entryId);
            if (entry?.Kind != AlbumEntryKind.Tower ||
                !entry.Snapshot.Recipe.Tower.Mounts.Any(mount => mount.Weapon.Head.Id == "head.long") ||
                !candidate.GetLoadoutEntries().Any(item => item?.Id == entryId))
            {
                reason = "Сохрани башню с длинным стволом и назначь её в боевой набор.";
                return false;
            }
            // The UI owns the successful trial step; this command verifies durable recipe and slot state.
            candidate.MarkFirstRefitTutorialHandled();
            return CommitCandidate(candidate, out reason);
        }
    }
}
