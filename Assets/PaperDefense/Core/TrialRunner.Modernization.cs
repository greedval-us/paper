using System;

namespace PaperDefense.Core
{
    /// <summary>Explicit isolated lane setup. Never grants technology or writes a player profile.</summary>
    public sealed class TrialModernizationConfiguration
    {
        public ModernizationRanks Ranks { get; }
        public int MaximumRank { get; }
        public static TrialModernizationConfiguration Baseline { get; } = new TrialModernizationConfiguration(default);

        public TrialModernizationConfiguration(ModernizationRanks ranks, int maximumRank = 1)
        {
            if (maximumRank < 1 || maximumRank > 3) throw new ArgumentOutOfRangeException(nameof(maximumRank));
            foreach (ModernizationDirection direction in Enum.GetValues(typeof(ModernizationDirection)))
                if (ranks.Rank(direction) > maximumRank) throw new ArgumentException("Rank exceeds trial technology.", nameof(ranks));
            if (ranks.ColorRank > maximumRank) throw new ArgumentException("Color rank exceeds trial technology.", nameof(ranks));
            if ((ranks.Color == InkColor.None) != (ranks.ColorRank == 0))
                throw new ArgumentException("An isolated trial color requires a completed color rank.", nameof(ranks));
            Ranks = ranks; MaximumRank = maximumRank;
        }

        internal int OrdinaryInvestment(int baseCost)
        {
            int total = 0;
            foreach (ModernizationDirection direction in Enum.GetValues(typeof(ModernizationDirection)))
                for (int rank = 1; rank <= Ranks.Rank(direction); rank++)
                    total = checked(total + ModernizationRules.OrdinaryCost(baseCost, direction, rank));
            return total;
        }
    }
}
