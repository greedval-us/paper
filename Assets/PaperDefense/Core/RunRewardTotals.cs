using System;

namespace PaperDefense.Core
{
    /// <summary>The cumulative permanent reward shared by profile publication and result presentation.</summary>
    public readonly struct RunRewardTotals
    {
        public int WaveGold { get; }
        public int WaveXp { get; }
        public int VictoryGold { get; }
        public int VictoryXp { get; }
        public int Gold => WaveGold + VictoryGold;
        public int TotalXp => WaveXp + VictoryXp;
        RunRewardTotals(int waves, bool victory)
        { WaveGold = waves * 8; WaveXp = waves * 10; VictoryGold = victory ? 30 : 0; VictoryXp = victory ? 40 : 0; }
        public static RunRewardTotals Calculate(int completedWaves, bool victory)
        {
            if (completedWaves < 0 || completedWaves > 8 || victory && completedWaves != 8)
                throw new ArgumentException("Rewards require zero to eight completed waves and victory after wave eight.");
            return new RunRewardTotals(completedWaves, victory);
        }
    }
}
