using System;
using System.Text;

namespace PaperDefense.Core
{
    /// <summary>Durable mission identity and opaque battle state, separate from permanent profile data.</summary>
    public sealed class MissionCheckpoint
    {
        public const int MaximumPayloadBytes = 4 * 1024 * 1024;
        public Guid RunId { get; }
        public string MissionId { get; }
        public int MissionRevision { get; }
        public int CompletedWaves { get; }
        public string PayloadJson { get; }

        public MissionCheckpoint(Guid runId, string missionId, int missionRevision, int completedWaves, string payloadJson)
        {
            var mission = CampaignCatalog.Default.Find(missionId);
            if (runId == Guid.Empty || mission == null || missionRevision < 1 || completedWaves < 1 || completedWaves >= mission.WaveCount)
                throw new ArgumentException("Invalid mission checkpoint identity or completed-wave boundary.");
            if (string.IsNullOrWhiteSpace(payloadJson) || Encoding.UTF8.GetByteCount(payloadJson) > MaximumPayloadBytes)
                throw new ArgumentException("Invalid mission checkpoint payload size.");
            RunId = runId; MissionId = missionId; MissionRevision = missionRevision;
            CompletedWaves = completedWaves; PayloadJson = payloadJson;
        }
    }

    [Serializable]
    public sealed class MissionCheckpointData
    {
        public int Version = 1;
        public string RunId, MissionId, PayloadJson;
        public int MissionRevision, CompletedWaves;
        public MissionCheckpointData() { }
        public MissionCheckpointData(MissionCheckpoint source)
        {
            RunId = source.RunId.ToString("N"); MissionId = source.MissionId;
            MissionRevision = source.MissionRevision; CompletedWaves = source.CompletedWaves; PayloadJson = source.PayloadJson;
        }
        public MissionCheckpoint CreateSnapshot()
        {
            AssemblyProfileDataCopy.Version(Version);
            if (!Guid.TryParseExact(RunId, "N", out var run)) throw new ArgumentException("Invalid checkpoint run ID.");
            return new MissionCheckpoint(run, MissionId, MissionRevision, CompletedWaves, PayloadJson);
        }
    }
}
