using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public sealed partial class WorkshopProfile
    {
        public MissionCheckpoint MissionCheckpoint { get; private set; }
        public void SetMissionCheckpoint(MissionCheckpoint checkpoint) => MissionCheckpoint = checkpoint;

        void ReadMissionCheckpointData(WorkshopProfileData data)
        {
            MissionCheckpoint = null;
            if (data.Version < 11) return;
            if (!data.HasMissionCheckpoint)
            {
                var empty = data.MissionCheckpoint;
                if (empty != null && (!string.IsNullOrEmpty(empty.RunId) || !string.IsNullOrEmpty(empty.MissionId) ||
                    !string.IsNullOrEmpty(empty.PayloadJson) || empty.MissionRevision != 0 || empty.CompletedWaves != 0))
                    throw new ArgumentException("Checkpoint presence metadata does not match its envelope.");
                return;
            }
            if (data.MissionCheckpoint == null) throw new ArgumentException("Missing mission checkpoint envelope.");
            MissionCheckpoint = data.MissionCheckpoint.CreateSnapshot();
        }
        void WriteMissionCheckpointData(WorkshopProfileData data)
        {
            data.HasMissionCheckpoint = MissionCheckpoint != null;
            data.MissionCheckpoint = MissionCheckpoint == null ? null : new MissionCheckpointData(MissionCheckpoint);
        }

        /// <summary>Rebuilds the old drawing-input projection from the attempt's frozen pages, never the current album.</summary>
        public static IReadOnlyList<TowerBlueprint> ProjectLoadoutBlueprints(IReadOnlyList<AlbumEntry> savedEntries, ContentCatalogSnapshot content)
        {
            if (savedEntries == null || savedEntries.Count != ProfileLimits.LoadoutSlotCount) throw new ArgumentException("Eight saved positions are required.");
            var result = new TowerBlueprint[ProfileLimits.LoadoutSlotCount];
            for (int i = 0; i < result.Length; i++)
            {
                var entry = savedEntries[i]; if (entry == null) continue;
                result[i] = (entry.Kind == AlbumEntryKind.Legacy ? entry.LegacyBlueprint : CompatibilityBlueprint(entry))?.WithContent(content);
            }
            return Array.AsReadOnly(result);
        }
    }
}
