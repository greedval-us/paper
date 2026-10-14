using PaperDefense.Core;

namespace PaperDefense.Application
{
    /// <summary>Profile write policy and status. The caller owns the profile and publishes accepted candidates.</summary>
    internal sealed class ProfileSaveCoordinator
    {
        IWorkshopProfileStore store;
        bool canWrite, dirty;
        ProfileSaveState state = ProfileSaveState.MemoryOnly;
        string message = UserText.Message("copy.0780dfa419e08bc0"), lastWriteError = "";

        public void Reset()
        {
            store = null;
            canWrite = dirty = false;
            lastWriteError = "";
            state = ProfileSaveState.MemoryOnly;
            message = UserText.Message("copy.0780dfa419e08bc0");
        }

        public void Attach(IWorkshopProfileStore store, ProfileLoadResult loaded)
        {
            this.store = store;
            canWrite = loaded.CanWrite && loaded.Source != ProfileLoadSource.RecoveryRequired;
            dirty = canWrite && (loaded.Source == ProfileLoadSource.NewProfile || loaded.Source == ProfileLoadSource.Backup);
            lastWriteError = "";
            state = !canWrite ? ProfileSaveState.RecoveryRequired : dirty ? ProfileSaveState.Unsaved : ProfileSaveState.Saved;
            message = loaded.Message;
            if (string.IsNullOrEmpty(message))
                message = dirty ? UserText.Message("copy.251cfddd45554f35") : UserText.Message("copy.131e1e34691edbf8");
        }

        public bool CanEdit(out string reason)
        {
            reason = store != null && !canWrite ? message : "";
            return store == null || canWrite;
        }

        public bool SaveCandidate(WorkshopProfile candidate, out string reason)
        {
            reason = "";
            return store == null || Write(candidate, true, out reason);
        }

        public bool Retry(WorkshopProfile current, out string reason)
        {
            if (store == null) { reason = UserText.Message("copy.694da7c7454199ef"); return false; }
            return Write(current, false, out reason);
        }

        public void ContentChanged()
        {
            if (store == null || !canWrite) return;
            dirty = true;
            state = ProfileSaveState.Unsaved;
            message = UserText.Message("copy.f122ad5b22da3b62");
        }

        public void SaveEarnedReward(WorkshopProfile current)
        {
            if (store == null) return;
            dirty = true;
            if (canWrite) { state = ProfileSaveState.Unsaved; Write(current, false, out _); }
            else state = ProfileSaveState.RecoveryRequired;
        }

        public void Flush(WorkshopProfile current)
        {
            if (store != null && canWrite && dirty && current != null) Write(current, false, out _);
        }

        public void CopyStatusTo(WorkshopReadModel target)
        {
            if (target == null) return;
            target.SaveState = state;
            target.SaveMessage = message;
            target.HasUnsavedChanges = dirty;
            target.LastWriteError = lastWriteError;
        }

        bool Write(WorkshopProfile candidate, bool manual, out string reason)
        {
            if (!canWrite) { reason = message; return false; }
            var result = store.Save(candidate);
            if (!result.Success)
            {
                lastWriteError = string.IsNullOrEmpty(result.Message) ? UserText.Message("copy.9ceb493e00375e13") : result.Message;
                // Re-read safety only: never replace earned memory data with a fallback profile.
                var source = store.Load();
                if (!source.CanWrite || source.Source == ProfileLoadSource.RecoveryRequired)
                {
                    canWrite = false;
                    state = ProfileSaveState.RecoveryRequired;
                    message = (manual ? UserText.Message("copy.cd9a90bc886e9cfd") : "") +
                        (string.IsNullOrEmpty(source.Message) ? UserText.Message("copy.17e344fd85f0edd1") : source.Message);
                    if (dirty) message += UserText.Message("copy.5686d674034d00da");
                }
                else
                {
                    state = ProfileSaveState.WriteFailed;
                    message = manual ? UserText.Message("copy.0563e105e83c1577", lastWriteError) :
                        UserText.Message("copy.f668b11e1c640cb6", lastWriteError, dirty ? UserText.Message("copy.f50f494fcc3d45a0") : "");
                }
                reason = message;
                return false;
            }
            bool rejectedManual = !dirty && !string.IsNullOrEmpty(lastWriteError);
            dirty = false;
            lastWriteError = "";
            state = ProfileSaveState.Saved;
            message = UserText.Message("copy.3d5411a4bb91f858", !manual && rejectedManual ? UserText.Message("copy.627cced3972bf99b") : "");
            reason = message;
            return true;
        }
    }
}
