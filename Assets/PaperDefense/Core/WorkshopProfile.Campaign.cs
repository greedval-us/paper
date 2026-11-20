using System;
using System.Collections.Generic;
using System.Linq;

namespace PaperDefense.Core
{
    [Serializable]
    public sealed class CampaignStateData
    {
        public int Version = 1;
        public string SelectedMissionId = "notebook-1-page-1";
        public CampaignMissionStateData[] Missions = Array.Empty<CampaignMissionStateData>();
    }
    [Serializable]
    public sealed class CampaignMissionStateData
    {
        public int Version = 1;
        public string MissionId;
        public int BestStars, Wins;
    }
    [Serializable]
    public sealed class CampaignRunRewardData
    {
        public int Version = 1;
        public string RunId, MissionId;
        public int MissionRevision, WaveCount, CompletedWaves, Stars;
        public int GoldPerWave, XpPerWave, CompletionGold, CompletionXp, FirstClearGold, FirstClearXp;
        public int Gold, TotalXp;
        public bool Victory, FirstClear;
    }

    public sealed class CampaignMissionResult
    {
        public Guid RunId
        { get; }
        public string MissionId
        { get; }
        public bool Victory
        { get; }
        public int Stars
        { get; }
        public bool FirstClear
        { get; }
        public int CompletedWaves
        { get; }
        public int WaveCount
        { get; }
        public int WaveGold
        { get; }
        public int WaveXp
        { get; }
        public int CompletionGold
        { get; }
        public int CompletionXp
        { get; }
        public int FirstClearGold
        { get; }
        public int FirstClearXp
        { get; }
        public int Gold => WaveGold + CompletionGold + FirstClearGold;
        public int TotalXp => WaveXp + CompletionXp + FirstClearXp;
        public string NextMissionId => Victory ? CampaignCatalog.Default.Next(MissionId)?.Id : null;
        internal CampaignMissionResult(CampaignRunRewardData receipt)
        {
            RunId = Guid.ParseExact(receipt.RunId, "N");
            MissionId = receipt.MissionId;
            Victory = receipt.Victory;
            Stars = receipt.Stars;
            FirstClear = receipt.FirstClear;
            CompletedWaves = receipt.CompletedWaves;
            WaveCount = receipt.WaveCount;
            WaveGold = receipt.CompletedWaves * receipt.GoldPerWave;
            WaveXp = receipt.CompletedWaves * receipt.XpPerWave;
            CompletionGold = receipt.Victory ? receipt.CompletionGold : 0;
            CompletionXp = receipt.Victory ? receipt.CompletionXp : 0;
            FirstClearGold = receipt.FirstClear ? receipt.FirstClearGold : 0;
            FirstClearXp = receipt.FirstClear ? receipt.FirstClearXp : 0;
        }
        public static CampaignMissionResult Preview(CampaignMissionDefinition mission, Guid runId,
            int waves, bool victory, int stars, bool firstClear)
            => new CampaignMissionResult(WorkshopProfile.CreateMissionReceipt(mission, runId, waves, victory, stars, firstClear));
    }

    public sealed class CampaignMissionProgress
    {
        public CampaignMissionDefinition Definition
        { get; }
        public bool IsUnlocked
        { get; }
        public bool IsCompleted => BestStars > 0;
        public int BestStars
        { get; }
        public int Wins
        { get; }
        internal CampaignMissionProgress(CampaignMissionDefinition definition, bool unlocked, int stars, int wins)
        {
            Definition = definition;
            IsUnlocked = unlocked;
            BestStars = stars;
            Wins = wins;
        }
    }
    public sealed class CampaignReadModel
    {
        public IReadOnlyList<CampaignMissionProgress> Missions
        { get; }
        public string SelectedMissionId
        { get; }
        public int TotalStars => Missions.Sum(mission => mission.BestStars);
        public CampaignReadModel(WorkshopProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            SelectedMissionId = profile.IsMissionUnlocked(profile.SelectedMissionId) ? profile.SelectedMissionId
                : CampaignCatalog.Default.Missions.First(mission => profile.IsMissionUnlocked(mission.Id)).Id;
            Missions = Array.AsReadOnly(CampaignCatalog.Default.Missions.Select(mission =>
                new CampaignMissionProgress(mission, profile.IsMissionUnlocked(mission.Id), profile.BestMissionStars(mission.Id), profile.MissionWins(mission.Id))).ToArray());
        }
    }

    public sealed partial class WorkshopProfile
    {
        string selectedMissionId = "notebook-1-page-1";
        readonly List<CampaignMissionStateData> campaignMissions = new List<CampaignMissionStateData>();
        readonly List<CampaignRunRewardData> campaignRewards = new List<CampaignRunRewardData>();
        public string SelectedMissionId => selectedMissionId;
        public int BestMissionStars(string id) => campaignMissions.FirstOrDefault(mission => mission.MissionId == id)?.BestStars ?? 0;
        public int MissionWins(string id) => campaignMissions.FirstOrDefault(mission => mission.MissionId == id)?.Wins ?? 0;
        public bool IsMissionUnlocked(string id)
        {
            var mission = CampaignCatalog.Default.Find(id);
            return mission != null && (mission.Index == 0 || BestMissionStars(CampaignCatalog.Default.Missions[mission.Index - 1].Id) > 0);
        }
        public bool TrySelectMission(string id, out string reason)
        {
            reason = "";
            if (!IsMissionUnlocked(id))
            {
                reason = "Миссия ещё не открыта.";
                return false;
            }
            selectedMissionId = id;
            return true;
        }
        public CampaignMissionResult FindCampaignRunReward(Guid runId)
        {
            int index = CampaignRewardIndex(runId);
            return index < 0 ? null : new CampaignMissionResult(campaignRewards[index]);
        }
        int CampaignRewardIndex(Guid runId)
        {
            if (runId == Guid.Empty) return -1;
            for (int i = 0; i < campaignRewards.Count; i++)
                if (Guid.TryParseExact(campaignRewards[i].RunId, "N", out var savedId) && savedId == runId) return i;
            return -1;
        }

        internal static CampaignRunRewardData CreateMissionReceipt(CampaignMissionDefinition mission, Guid runId,
            int waves, bool victory, int stars, bool firstClear)
        {
            if (mission == null || runId == Guid.Empty || waves < 0 || waves > mission.WaveCount || victory && waves != mission.WaveCount || stars < 0 || stars > 3 || victory != (stars > 0))
                throw new ArgumentException("Invalid mission result.");
            var receipt = new CampaignRunRewardData
            {
                RunId = runId.ToString("N"),
                MissionId = mission.Id,
                MissionRevision = mission.Revision,
                WaveCount = mission.WaveCount,
                CompletedWaves = waves,
                Victory = victory,
                Stars = stars,
                FirstClear = victory && firstClear,
                GoldPerWave = mission.GoldPerWave,
                XpPerWave = mission.XpPerWave,
                CompletionGold = mission.CompletionGold,
                CompletionXp = mission.CompletionXp,
                FirstClearGold = mission.FirstClearGold,
                FirstClearXp = mission.FirstClearXp
            };
            receipt.Gold = waves * receipt.GoldPerWave + (victory ? receipt.CompletionGold : 0) + (receipt.FirstClear ? receipt.FirstClearGold : 0);
            receipt.TotalXp = waves * receipt.XpPerWave + (victory ? receipt.CompletionXp : 0) + (receipt.FirstClear ? receipt.FirstClearXp : 0);
            return receipt;
        }
        public bool TryApplyMissionReward(CampaignMissionDefinition mission, Guid runId, int waves, bool victory, int stars, out string reason)
        {
            reason = "";
            CampaignRunRewardData next;
            try
            {
                next = CreateMissionReceipt(mission, runId, waves, victory, stars, false);
            }
            catch (ArgumentException) { reason = "profile.reward.invalid"; return false; }
            if (FindRunReward(runId) != null)
            {
                reason = "profile.reward.mission_mismatch";
                return false;
            }
            int index = CampaignRewardIndex(runId);
            var old = index < 0 ? null : campaignRewards[index];
            if (old != null && (old.MissionId != mission.Id || old.MissionRevision != mission.Revision || old.WaveCount != mission.WaveCount))
            {
                reason = "profile.reward.mission_mismatch";
                return false;
            }
            if (!IsMissionUnlocked(mission.Id))
            {
                reason = "Миссия ещё не открыта.";
                return false;
            }
            if (old != null)
            {
                // A saved attempt owns its economy even if the current content has been rebalanced.
                next.GoldPerWave = old.GoldPerWave;
                next.XpPerWave = old.XpPerWave;
                next.CompletionGold = old.CompletionGold;
                next.CompletionXp = old.CompletionXp;
                next.FirstClearGold = old.FirstClearGold;
                next.FirstClearXp = old.FirstClearXp;
                next.CompletedWaves = Math.Max(old.CompletedWaves, waves);
                next.Victory |= old.Victory;
                next.Stars = Math.Max(old.Stars, stars);
            }
            next.FirstClear = old?.FirstClear ?? (next.Victory && BestMissionStars(mission.Id) == 0);
            // An initially partial receipt can become this mission's first successful clear.
            if (next.Victory && old?.Victory != true && BestMissionStars(mission.Id) == 0)
                next.FirstClear = true;
            next.Gold = next.CompletedWaves * next.GoldPerWave + (next.Victory ? next.CompletionGold : 0) + (next.FirstClear ? next.FirstClearGold : 0);
            next.TotalXp = next.CompletedWaves * next.XpPerWave + (next.Victory ? next.CompletionXp : 0) + (next.FirstClear ? next.FirstClearXp : 0);
            Gold = (int)Math.Min(int.MaxValue, (long)Gold + next.Gold - (old?.Gold ?? 0));
            TotalXp = (int)Math.Min(int.MaxValue, (long)TotalXp + next.TotalXp - (old?.TotalXp ?? 0));
            if (index < 0)
                campaignRewards.Add(next);
            else
                campaignRewards[index] = next;
            if (next.Victory)
            {
                var state = campaignMissions.FirstOrDefault(item => item.MissionId == mission.Id);
                if (state == null)
                {
                    state = new CampaignMissionStateData { MissionId = mission.Id };
                    campaignMissions.Add(state);
                }
                state.BestStars = Math.Max(state.BestStars, next.Stars);
                if (old?.Victory != true)
                    state.Wins = (int)Math.Min(int.MaxValue, (long)state.Wins + 1);
                if (next.FirstClear)
                foreach (var id in mission.UnlockPartIds)
                    unlocked.Add(id);
            }
            return true;
        }
        static CampaignRunRewardData Copy(CampaignRunRewardData source)
            => new CampaignRunRewardData
            {
                Version = source.Version,
                RunId = source.RunId,
                MissionId = source.MissionId,
                MissionRevision = source.MissionRevision,
                WaveCount = source.WaveCount,
                CompletedWaves = source.CompletedWaves,
                Stars = source.Stars,
                Victory = source.Victory,
                FirstClear = source.FirstClear,
                GoldPerWave = source.GoldPerWave,
                XpPerWave = source.XpPerWave,
                CompletionGold = source.CompletionGold,
                CompletionXp = source.CompletionXp,
                FirstClearGold = source.FirstClearGold,
                FirstClearXp = source.FirstClearXp,
                Gold = source.Gold,
                TotalXp = source.TotalXp
            };
        static CampaignMissionStateData Copy(CampaignMissionStateData source)
            => new CampaignMissionStateData { Version = source.Version, MissionId = source.MissionId, BestStars = source.BestStars, Wins = source.Wins };
        void ReadCampaignData(WorkshopProfileData data)
        {
            if (data.Version < 9)
                return;
            if (data.Campaign == null || data.Campaign.Missions == null || data.CampaignRewards == null)
                throw new ArgumentException("Missing campaign data.");
            AssemblyProfileDataCopy.Version(data.Campaign.Version);
            ContentValidation.Id(data.Campaign.SelectedMissionId, "campaign.selected");
            selectedMissionId = data.Campaign.SelectedMissionId;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var state in data.Campaign.Missions)
            {
                if (state == null)
                    throw new ArgumentException("Missing campaign mission.");
                AssemblyProfileDataCopy.Version(state.Version);
                ContentValidation.Id(state.MissionId, "campaign.mission");
                if (!ids.Add(state.MissionId) || state.BestStars < 1 || state.BestStars > 3 || state.Wins < 1)
                    throw new ArgumentException("Invalid campaign progress.");
                campaignMissions.Add(Copy(state));
            }
            var runs = new HashSet<Guid>();
            var firstClears = new HashSet<string>(StringComparer.Ordinal);
            foreach (var receipt in data.CampaignRewards)
            {
                if (receipt == null)
                    throw new ArgumentException("Missing campaign receipt.");
                AssemblyProfileDataCopy.Version(receipt.Version);
                var runId = ReceiptGuid(receipt.RunId);
                ContentValidation.Id(receipt.MissionId, "campaign.receipt.mission");
                if (receipt.FirstClear && !firstClears.Add(receipt.MissionId))
                    throw new ArgumentException("Duplicate first-clear reward.");
                if (!runs.Add(runId) || FindRunReward(runId) != null || receipt.MissionRevision < 1 || receipt.WaveCount < 1 || receipt.WaveCount > 100 ||
                    receipt.CompletedWaves < 0 || receipt.CompletedWaves > receipt.WaveCount || receipt.Stars < 0 || receipt.Stars > 3 || receipt.Victory != (receipt.Stars > 0) ||
                    receipt.Victory && receipt.CompletedWaves != receipt.WaveCount || receipt.FirstClear && !receipt.Victory)
                    throw new ArgumentException("Invalid campaign receipt.");
                foreach (int amount in new[] { receipt.GoldPerWave, receipt.XpPerWave, receipt.CompletionGold, receipt.CompletionXp, receipt.FirstClearGold, receipt.FirstClearXp })
                    if (amount < 0 || amount > 1000000)
                        throw new ArgumentException("Invalid campaign reward amount.");
                var result = new CampaignMissionResult(receipt);
                if (receipt.Gold != result.Gold || receipt.TotalXp != result.TotalXp || receipt.Victory && BestMissionStars(receipt.MissionId) < receipt.Stars)
                    throw new ArgumentException("Inconsistent campaign receipt.");
                campaignRewards.Add(Copy(receipt));
            }
        }
        void WriteCampaignData(WorkshopProfileData data)
        {
            data.Campaign = new CampaignStateData { SelectedMissionId = selectedMissionId, Missions = campaignMissions.Select(Copy).ToArray() };
            data.CampaignRewards = campaignRewards.Select(Copy).ToArray();
        }
        void CopyCampaignStateTo(WorkshopProfile copy)
        {
            copy.selectedMissionId = selectedMissionId;
            copy.campaignMissions.AddRange(campaignMissions.Select(Copy));
            copy.campaignRewards.AddRange(campaignRewards.Select(Copy));
        }
    }
}
