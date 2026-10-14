using System;

namespace PaperDefense.Core
{
    /// <summary>Local movement tuning. Legacy mechanics fixtures opt in explicitly.</summary>
    [Serializable]
    public sealed class CrowdMovementRules
    {
        public bool Enabled;
        public int EntranceHalfWidth = 6;
        public float RadiusPerVisualScale = .32f;
        public float PersonalSpace = .35f;
        public float SeparationWeight = 1.4f;
        public float DirectionMemory = .25f;
        public float ObstacleClearance = .08f;

        public CrowdMovementRules CreateSnapshot()
        {
            if (EntranceHalfWidth < 0 || EntranceHalfWidth > 64) throw new ArgumentOutOfRangeException(nameof(EntranceHalfWidth));
            CombatTuningRules.Positive(RadiusPerVisualScale, nameof(RadiusPerVisualScale));
            CombatTuningRules.Positive(PersonalSpace, nameof(PersonalSpace));
            CombatTuningRules.Positive(SeparationWeight, nameof(SeparationWeight));
            if (float.IsNaN(DirectionMemory) || DirectionMemory < 0 || DirectionMemory > 1) throw new ArgumentOutOfRangeException(nameof(DirectionMemory));
            if (float.IsNaN(ObstacleClearance) || ObstacleClearance < 0 || ObstacleClearance >= .5f) throw new ArgumentOutOfRangeException(nameof(ObstacleClearance));
            return (CrowdMovementRules)MemberwiseClone();
        }
    }
}
