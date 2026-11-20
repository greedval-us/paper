using PaperDefense.Core;

namespace PaperDefense.Presentation
{
    /// <summary>Read-only facts for field development indicators; quotes own purchase eligibility.</summary>
    internal readonly struct TowerDevelopmentState
    {
        internal readonly int StepsUsed, ColorRank;
        internal readonly InkColor Color;
        internal readonly bool IsWorking, IsComplete, HasOrdinaryUpgrade, HasColorUpgrade;
        internal bool CanImprove => HasOrdinaryUpgrade || HasColorUpgrade;

        TowerDevelopmentState(StructureView tower, bool ordinary = false, bool color = false)
        {
            StepsUsed = tower.Modernization.StepsUsed;
            Color = tower.Color; ColorRank = tower.Modernization.ColorRank;
            IsWorking = tower.Modernization.IsWorking; IsComplete = tower.IsComplete;
            HasOrdinaryUpgrade = ordinary; HasColorUpgrade = color;
        }

        internal static TowerDevelopmentState Evaluate(IRunReadModel run, StructureView tower)
        {
            if (run == null || !run.Balance.Mechanics.FieldModernization ||
                tower.ModernizationEntry == null || !(tower.Health > 0) ||
                !tower.IsComplete || tower.BuildRemaining > 0 || tower.Modernization.IsWorking ||
                run.Phase == RunPhase.Victory || run.Phase == RunPhase.Defeat)
                return new TowerDevelopmentState(tower);

            bool ordinary = false, color = false;
            for (int direction = 0; direction < 4; direction++)
                if (run.QuoteModernization(tower.Id, (ModernizationDirection)direction)?.Valid == true)
                { ordinary = true; break; }
            if (tower.Color != InkColor.None)
                color = run.QuoteColorModernization(tower.Id, tower.Color)?.Valid == true;
            else
                for (int candidate = (int)InkColor.Red; candidate <= (int)InkColor.Green; candidate++)
                    if (run.QuoteColorModernization(tower.Id, (InkColor)candidate)?.Valid == true)
                    { color = true; break; }
            return new TowerDevelopmentState(tower, ordinary, color);
        }
    }
}
