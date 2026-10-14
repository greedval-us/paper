using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    [Serializable]
    public sealed class CombatTuningRules
    {
        public float MinimumActionInterval = .05f, MinimumProductionMultiplier = .01f;
        public float DestinationRepathDistanceSquared = 2.25f, FrontageContactDistance = 1.3f;
        public float ObjectiveContactDistance = 1.15f, StructureContactDistance = 1.05f;
        public float MinimumRangedDistance = 1.2f, MinimumSpawnInterval = .005f, MinimumPathDamage = .01f, MinimumRadialRange = .5f;
        public CombatTuningRules CreateSnapshot()
        {
            Positive(MinimumActionInterval, nameof(MinimumActionInterval)); Positive(MinimumProductionMultiplier, nameof(MinimumProductionMultiplier));
            Positive(DestinationRepathDistanceSquared, nameof(DestinationRepathDistanceSquared)); Positive(FrontageContactDistance, nameof(FrontageContactDistance));
            Positive(ObjectiveContactDistance, nameof(ObjectiveContactDistance)); Positive(StructureContactDistance, nameof(StructureContactDistance));
            Positive(MinimumRangedDistance, nameof(MinimumRangedDistance)); Positive(MinimumSpawnInterval, nameof(MinimumSpawnInterval)); Positive(MinimumPathDamage, nameof(MinimumPathDamage));
            Positive(MinimumRadialRange, nameof(MinimumRadialRange));
            return (CombatTuningRules)MemberwiseClone();
        }
        internal static void Positive(float value, string field)
        { if (value <= 0 || float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException(UserText.Message("copy.d5fa0446bafc1d84", field)); }
    }

    [Serializable]
    public sealed class WellPlacementDefinition
    {
        public InkColor Color;
        public int XNumerator, XDenominator, YNumerator, YDenominator;
        public int ReservedRadius = 1;
        public WellPlacementDefinition CreateSnapshot()
        {Validate();return (WellPlacementDefinition)MemberwiseClone();}
        void Validate()
        {
            if (Color == InkColor.None || !Enum.IsDefined(typeof(InkColor), Color)) throw new ArgumentException(UserText.Message("copy.f03877a94c16a152"));
            if (XDenominator <= 0 || YDenominator <= 0 || XNumerator <= 0 || XNumerator >= XDenominator || YNumerator <= 0 || YNumerator >= YDenominator || ReservedRadius < 0)
                throw new ArgumentException(UserText.Message("copy.fe6c39e68cd08967"));
        }
        public Cell Resolve(int width, int height)
        {
            if(width<=0)throw new ArgumentOutOfRangeException(nameof(width));
            if(height<=0)throw new ArgumentOutOfRangeException(nameof(height));
            Validate();
            return new Cell((int)((long)width*XNumerator/XDenominator),(int)((long)height*YNumerator/YDenominator));
        }
    }

    [Serializable]
    public sealed partial class GameBalance
    {
        public CombatTuningRules Combat = new CombatTuningRules();
        public CrowdMovementRules Crowd = new CrowdMovementRules();
        public TrialRules Trials = new TrialRules();
        public List<WellPlacementDefinition> WellPlacements = new List<WellPlacementDefinition>
        {
            new WellPlacementDefinition { Color = InkColor.Red, XNumerator = 1, XDenominator = 5, YNumerator = 1, YDenominator = 4 },
            new WellPlacementDefinition { Color = InkColor.Blue, XNumerator = 4, XDenominator = 5, YNumerator = 1, YDenominator = 4 },
            new WellPlacementDefinition { Color = InkColor.Green, XNumerator = 1, XDenominator = 2, YNumerator = 5, YDenominator = 6 }
        };
    }
}
