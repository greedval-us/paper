using System;

namespace PaperDefense.Core
{
    [Serializable] public sealed class GameSessionCheckpointData
    {
        public int Version = 1;
        public string RunId;
        public RunPhase Phase;
        public int WaveIndex, CompletedWaves, Ink, Kills, GoldEarned, NextStructureId, NextEnemyId;
        public long TickIndex, RandomDrawCount, EventSequence, DroppedEvents, NextActionId;
        public int RandomSeed, TopologyVersion, StructureVisualVersion, FlowRebuildCount;
        public float BaseHealth, Countdown, Elapsed, BlotterCooldownRemaining;
        public bool PendingUpgrade, FlowDirty;
        public double SupplyRemainder;
        public float FireMultiplier, DamageMultiplier, HealthMultiplier, SalvageBonus, SupplyMultiplier, WaveRewardMultiplier;
        public int[] ColoredInk;
        public int ClaimedInkFlasks;
        public StructureCheckpointData[] Structures;
        public WellCheckpointData[] Wells;
        public BlotCheckpointData[] Blots;
        public RunStatisticsCheckpointData Statistics;
    }

    [Serializable] public sealed class StructureCheckpointData
    {
        public int Id, X, Y, PaidInk, GeneratorPaidWaves;
        public StructureKind Kind;
        public MaterialTier Tier;
        public InkColor Color;
        public TargetPriority Priority;
        public string ContentId, SourceAlbumEntryId;
        public RecipeSnapshotData Recipe;
        public BlueprintData Blueprint;
        public bool HasRecipe, HasBlueprint, HasModernization, HasColorBudget;
        public float Health, MaxHealth, BuildRemaining, FacingDegrees, AimX, AimY;
        public bool IsComplete, WaitingForClearance;
        public double NextSupportFeedbackAt, NextSpikePulseAt, NextPrismPulseAt;
        public InkColor NextPrismColor;
        public MountCheckpointData[] Mounts;
        public ModernizationCheckpointData Modernization;
        public ColorBudgetCheckpointData ColorBudget;
    }

    [Serializable] public sealed class MountCheckpointData
    {
        public float AimX, AimY, Cooldown, AcquisitionRemaining, ActionRemaining;
        public int TargetId, RoundsRemaining;
        public bool CycleInitialized, IsReloading;
        public double RemainingWork;
    }

    [Serializable] public sealed class ModernizationCheckpointData
    {
        public int Power, Mechanics, Range, Resilience, ColorRank, Revision;
        public InkColor Color;
        public bool HasPending, PendingIsColor;
        public ModernizationDirection PendingDirection;
        public InkColor PendingColor;
        public float Remaining;
    }

    [Serializable] public sealed class ColorBudgetCheckpointData
    {
        public float WindowSeconds;
        public int TargetLimit;
        public bool HasWindow;
        public double NextWindow;
        public int[] Affected;
    }

    [Serializable] public sealed class WellCheckpointData
    {
        public int Id, X, Y, EligibleWave;
        public InkColor Color;
        public float Health, MaxHealth;
        public bool Active, Threatened;
    }

    [Serializable] public sealed class BlotCheckpointData
    {
        public int X, Y;
        public float Remaining;
    }
}
