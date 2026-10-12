using System;

namespace PaperDefense.Core
{
    /// <summary>Finite assembly tuning; permanent unlock prices and battle construction costs are distinct.</summary>
    [Serializable] public sealed class TowerConstructionRules
    {
        public int StandardCapacity = 10, HeavyCapacity = 14;
        public int GunLoad = 4, PiercerLoad = 6, MortarLoad = 7, ArmorLoad = 3, FeedLoad = 2, TrackingLoad = 2;
        public int FanLoad = 5, FanUnlockGold = 60;
        public int RicochetLoad = 5, RicochetUnlockGold = 60;
        public int StaplerLoad = 3, StaplerUnlockGold = 40;
        public int BlotterLoad = 3, BlotterUnlockGold = 40;
        public int RailLoad = 2, RailCost = 20, RailUnlockGold = 60;
        public float RailBuildSeconds = .25f, RailRangeMultiplier = 1.25f, RailIntervalMultiplier = 1.2f;
        public int StandardBaseCost = 16, HeavyBaseCost = 28, WeaponAssemblyCredit = 16;
        public int ArmorCost = 18, FeedCost = 16, TrackingCost = 14;
        public float FoundationHealth = 55, HeavyHealthMultiplier = 1.3f, ArmorHealthMultiplier = 1.5f;
        public float ArmorDefense;
        public float SecondaryBuildSeconds = .4f, HeavyBuildSeconds = .45f;
        public float ArmorBuildSeconds = .25f, FeedBuildSeconds = .25f, TrackingBuildSeconds = .25f;
        public float DualFeedMultiplier = .8f, FeedModuleMultiplier = 1.15f, MaxFeedMultiplier = 1.15f;
        public bool DynamicFeed;
        public float DynamicFeedCapacity = 1.6f;
        public float AcquisitionSeconds = .25f, TrackingAcquisitionSeconds = .08f;
        public int ColorMinimum = 2, ColorCostDivisor = 8;
        public int HeavyUnlockGold = 90, FeedUnlockGold = 60, TrackingUnlockGold = 60;
        public TowerConstructionRules CreateSnapshot()
        {
            NonNegative(StandardCapacity, nameof(StandardCapacity)); NonNegative(HeavyCapacity, nameof(HeavyCapacity));
            NonNegative(GunLoad, nameof(GunLoad)); NonNegative(PiercerLoad, nameof(PiercerLoad)); NonNegative(MortarLoad, nameof(MortarLoad));
            NonNegative(FanLoad, nameof(FanLoad)); NonNegative(FanUnlockGold, nameof(FanUnlockGold));
            NonNegative(RicochetLoad, nameof(RicochetLoad)); NonNegative(RicochetUnlockGold, nameof(RicochetUnlockGold));
            NonNegative(StaplerLoad, nameof(StaplerLoad)); NonNegative(StaplerUnlockGold, nameof(StaplerUnlockGold));
            NonNegative(BlotterLoad, nameof(BlotterLoad)); NonNegative(BlotterUnlockGold, nameof(BlotterUnlockGold));
            NonNegative(RailLoad, nameof(RailLoad)); NonNegative(RailCost, nameof(RailCost)); NonNegative(RailUnlockGold, nameof(RailUnlockGold));
            NonNegative(RailBuildSeconds, nameof(RailBuildSeconds)); Positive(RailRangeMultiplier, nameof(RailRangeMultiplier)); Positive(RailIntervalMultiplier, nameof(RailIntervalMultiplier));
            NonNegative(ArmorLoad, nameof(ArmorLoad)); NonNegative(FeedLoad, nameof(FeedLoad)); NonNegative(TrackingLoad, nameof(TrackingLoad));
            NonNegative(StandardBaseCost, nameof(StandardBaseCost)); NonNegative(HeavyBaseCost, nameof(HeavyBaseCost));
            NonNegative(WeaponAssemblyCredit, nameof(WeaponAssemblyCredit)); NonNegative(ArmorCost, nameof(ArmorCost));
            NonNegative(FeedCost, nameof(FeedCost)); NonNegative(TrackingCost, nameof(TrackingCost));
            Positive(FoundationHealth, nameof(FoundationHealth)); Positive(HeavyHealthMultiplier, nameof(HeavyHealthMultiplier));
            Positive(ArmorHealthMultiplier, nameof(ArmorHealthMultiplier));
            NonNegative(ArmorDefense, nameof(ArmorDefense));
            if (ArmorDefense > 1) throw new ArgumentOutOfRangeException(nameof(ArmorDefense));
            NonNegative(SecondaryBuildSeconds, nameof(SecondaryBuildSeconds)); NonNegative(HeavyBuildSeconds, nameof(HeavyBuildSeconds));
            NonNegative(ArmorBuildSeconds, nameof(ArmorBuildSeconds)); NonNegative(FeedBuildSeconds, nameof(FeedBuildSeconds));
            NonNegative(TrackingBuildSeconds, nameof(TrackingBuildSeconds));
            Positive(DualFeedMultiplier, nameof(DualFeedMultiplier)); Positive(FeedModuleMultiplier, nameof(FeedModuleMultiplier));
            Positive(MaxFeedMultiplier, nameof(MaxFeedMultiplier));
            Positive(DynamicFeedCapacity, nameof(DynamicFeedCapacity));
            NonNegative(AcquisitionSeconds, nameof(AcquisitionSeconds)); NonNegative(TrackingAcquisitionSeconds, nameof(TrackingAcquisitionSeconds));
            NonNegative(ColorMinimum, nameof(ColorMinimum));
            if (ColorCostDivisor <= 0) throw new ArgumentOutOfRangeException(nameof(ColorCostDivisor));
            NonNegative(HeavyUnlockGold, nameof(HeavyUnlockGold)); NonNegative(FeedUnlockGold, nameof(FeedUnlockGold));
            NonNegative(TrackingUnlockGold, nameof(TrackingUnlockGold));
            return (TowerConstructionRules)MemberwiseClone();
        }
        static void NonNegative(float value, string name)
        { if (value < 0 || float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(name); }
        static void Positive(float value, string name)
        { NonNegative(value, name); if (value == 0) throw new ArgumentOutOfRangeException(name); }
        public int WeaponLoad(StructureKind kind) => kind == StructureKind.Gun ? GunLoad : kind == StructureKind.Piercer ? PiercerLoad : kind == StructureKind.Mortar ? MortarLoad : kind == StructureKind.Fan ? FanLoad : kind == StructureKind.Ricochet ? RicochetLoad : kind == StructureKind.Stapler ? StaplerLoad : kind == StructureKind.Blotter ? BlotterLoad : 100;
        public int ModuleLoad(TowerModules modules) => (int)Math.Min(int.MaxValue, ((modules & TowerModules.Armor) != 0 ? (long)ArmorLoad : 0) +
            ((modules & TowerModules.Feed) != 0 ? FeedLoad : 0) + ((modules & TowerModules.Tracking) != 0 ? TrackingLoad : 0) +
            ((modules & TowerModules.Rail) != 0 ? RailLoad : 0));
        public int UnlockGold(string id) => id == "chassis.heavy" ? HeavyUnlockGold : id == "module.feed" ? FeedUnlockGold : id == "module.tracking" ? TrackingUnlockGold : id == "weapon.fan" ? FanUnlockGold : id == "weapon.ricochet" ? RicochetUnlockGold : id == "weapon.stapler" ? StaplerUnlockGold : id == "weapon.blotter" ? BlotterUnlockGold : id == "module.rail" ? RailUnlockGold : 0;
    }
}
