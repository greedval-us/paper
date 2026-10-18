using System;

namespace PaperDefense.Core
{
    public sealed partial class WorkshopProfile
    {
        public RunRewardReceipt FindRunReward(Guid runId)
        {
            int index = RunRewardIndex(runId);
            if (index < 0) return null;
            var saved = rewards[index];
            return new RunRewardReceipt(runId, saved.Waves, saved.VictoryBonus);
        }

        /// <summary>Merge a cumulative result into this candidate, issuing only its unpaid delta.</summary>
        public bool TryApplyRunReward(Guid runId, int completedWaves, bool victory, out string code)
        {
            code = "";
            if (runId == Guid.Empty) { code = "profile.reward.run"; return false; }
            if (completedWaves < 0 || completedWaves > 8) { code = "profile.reward.waves"; return false; }
            if (victory && completedWaves != 8) { code = "profile.reward.victory"; return false; }

            int index = RunRewardIndex(runId);
            var previous = index < 0 ? null : rewards[index];
            int oldWaves = previous?.Waves ?? 0;
            bool oldVictory = previous?.VictoryBonus ?? false;
            int nextWaves = Math.Max(oldWaves, completedWaves);
            bool nextVictory = oldVictory || victory;
            if (nextWaves == oldWaves && nextVictory == oldVictory) return true;

            int newWaves = nextWaves - oldWaves;
            int victoryDelta = nextVictory && !oldVictory ? 1 : 0;
            int nextGold = (int)Math.Min(int.MaxValue, (long)Gold + newWaves * 8 + victoryDelta * 30);
            int nextXp = (int)Math.Min(int.MaxValue, (long)TotalXp + newWaves * 10 + victoryDelta * 40);
            var receipt = new RunRewardReceiptData
                { RunId = runId.ToString("N"), Waves = nextWaves, VictoryBonus = nextVictory };

            if (index < 0) rewards.Add(receipt); else rewards[index] = receipt;
            Gold = nextGold;
            TotalXp = nextXp;
            return true;
        }

        int RunRewardIndex(Guid runId)
        {
            if (runId == Guid.Empty) return -1;
            for (int i = 0; i < rewards.Count; i++)
                if (Guid.TryParseExact(rewards[i].RunId, "N", out var savedId) && savedId == runId) return i;
            return -1;
        }
    }
}
