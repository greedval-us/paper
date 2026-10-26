using System;

namespace PaperDefense.Core
{
    /// <summary>Immutable cumulative record of the permanent reward already issued for one run.</summary>
    public sealed class RunRewardReceipt
    {
        public Guid RunId { get; }
        public int Waves { get; }
        public bool VictoryBonus { get; }
        public int Gold => RunRewardTotals.Calculate(Waves, VictoryBonus).Gold;
        public int TotalXp => RunRewardTotals.Calculate(Waves, VictoryBonus).TotalXp;

        internal RunRewardReceipt(Guid runId, int waves, bool victoryBonus)
        { RunId = runId; Waves = waves; VictoryBonus = victoryBonus; }
    }
}
