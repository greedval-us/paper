using System;

namespace PaperDefense.Core
{
    /// <summary>Intrinsic timings exclude feed. The cycle system supplies current active-mount feed once.</summary>
    public sealed class ResolvedWeapon
    {
        public int SlotIndex { get; }
        public string SourceId { get; }
        public AssemblyWeaponFamily Family { get; }
        public AttackKind Attack { get; }
        public bool LegacyContinuous { get; }
        public float Damage { get; } public float Range { get; } public float MinimumRange { get; }
        public float Interval { get; } public int MagazineSize { get; } public float ReloadSeconds { get; }
        public float AcquisitionSeconds { get; } public int TargetLimit { get; }
        public float Radius { get; } public float FlightSeconds { get; } public float JumpRange { get; } public float ConeDegrees { get; }
        public float SlowSpeedMultiplier { get; } public float SlowSeconds { get; }
        public float MinimumActionInterval { get; }
        public float CycleSeconds => AssemblyValidation.Positive((MagazineSize - 1) * (double)Interval + ReloadSeconds, "weapon.cycle");
        public float NominalDps => AssemblyValidation.NonNegative(Damage * (double)MagazineSize / CycleSeconds, "weapon.dps");
        internal ResolvedWeapon(int slotIndex, string sourceId, AssemblyWeaponFamily family, AttackKind attack,
            float damage, float range, float minimumRange, float interval, int magazineSize, float reloadSeconds,
            float acquisitionSeconds, int targetLimit, float radius, float flightSeconds, float jumpRange,
            float coneDegrees, float slowSpeedMultiplier, float slowSeconds, float minimumActionInterval, bool legacyContinuous = false)
        {
            SlotIndex = slotIndex; SourceId = sourceId; Family = family; Attack = attack; LegacyContinuous = legacyContinuous;
            Damage = damage; Range = range; MinimumRange = minimumRange; Interval = interval; MagazineSize = magazineSize;
            ReloadSeconds = reloadSeconds; AcquisitionSeconds = acquisitionSeconds; TargetLimit = targetLimit;
            Radius = radius; FlightSeconds = flightSeconds; JumpRange = jumpRange; ConeDegrees = coneDegrees;
            SlowSpeedMultiplier = slowSpeedMultiplier; SlowSeconds = slowSeconds; MinimumActionInterval = minimumActionInterval;
        }
        public float IntervalAtFeed(float feed)
            => Math.Max(MinimumActionInterval, AssemblyValidation.Positive(Interval / (double)AssemblyValidation.Positive(feed, "feed"), "weapon.interval"));
        public float ReloadAtFeed(float feed)
            => AssemblyValidation.Positive(ReloadSeconds / (double)AssemblyValidation.Positive(feed, "feed"), "weapon.reload");
        public float DpsAtFeed(float feed)
        {
            AssemblyValidation.NonNegative(feed, "feed");
            if (feed == 0) return 0;
            double cycle = (MagazineSize - 1) * (double)IntervalAtFeed(feed) + ReloadAtFeed(feed);
            return AssemblyValidation.NonNegative(Damage * (double)MagazineSize / cycle, "weapon.dps");
        }
    }
}
