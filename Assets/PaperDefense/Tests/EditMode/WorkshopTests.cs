using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class WorkshopTests
    {
        static TowerBlueprint Blueprint(StructureKind first, StructureKind? second = null,
            TowerModules modules = TowerModules.None, ChassisKind chassis = ChassisKind.Standard)
            => new TowerBlueprint("test", "Тест", chassis, first, second, modules);

        [TestCase(StructureKind.Gun, StructureKind.Gun, TowerModules.None, 8, true)]
        [TestCase(StructureKind.Gun, StructureKind.Gun, TowerModules.Armor, 11, false)]
        [TestCase(StructureKind.Gun, StructureKind.Piercer, TowerModules.None, 10, true)]
        [TestCase(StructureKind.Mortar, StructureKind.Gun, TowerModules.None, 11, false)]
        [TestCase(StructureKind.Mortar, null, TowerModules.Armor, 10, true)]
        public void LoadRulesRejectWeaponsThatCannotFit(StructureKind first, StructureKind? second,
            TowerModules modules, int load, bool valid)
        {
            var blueprint = Blueprint(first, second, modules);
            Assert.That(TowerCatalog.Load(blueprint), Is.EqualTo(load));
            Assert.That(TowerCatalog.Validate(blueprint, out _), Is.EqualTo(valid));
        }

        [Test]
        public void HeavyChassisAllowsTwoMortarsButStillHasAFiniteLimit()
        {
            Assert.That(TowerCatalog.Validate(Blueprint(StructureKind.Mortar, StructureKind.Mortar,
                chassis: ChassisKind.Heavy), out _), Is.True);
            Assert.That(TowerCatalog.Validate(Blueprint(StructureKind.Mortar, StructureKind.Mortar,
                TowerModules.Feed, ChassisKind.Heavy), out _), Is.False);
        }

        [Test] public void OverloadedAssemblyExplainsTheExactCapacityDeficit()
        {
            Assert.That(TowerCatalog.Validate(Blueprint(StructureKind.Gun, StructureKind.Gun, TowerModules.Armor),
                out string reason), Is.False);
            Assert.That(reason, Does.Contain("1 ед."));
        }

        [Test]
        public void SecondWeaponCostsInkAndBuildTimeAndSharesFeed()
        {
            var balance = GameBalance.CreateDefault();
            var single = Blueprint(StructureKind.Gun);
            var dual = Blueprint(StructureKind.Gun, StructureKind.Gun);
            Assert.That(TowerCatalog.Cost(single, balance, MaterialTier.Pencil), Is.EqualTo(40));
            Assert.That(TowerCatalog.Cost(dual, balance, MaterialTier.Pencil), Is.EqualTo(64));
            Assert.That(TowerCatalog.BuildSeconds(dual, balance, MaterialTier.Pencil),
                Is.GreaterThan(TowerCatalog.BuildSeconds(single, balance, MaterialTier.Pencil)));
            Assert.That(TowerCatalog.FeedMultiplier(dual), Is.EqualTo(.8f));
        }

        [Test]
        public void ArmorHealthAndTrackingAcquisitionHaveActualTradeoffs()
        {
            var balance = GameBalance.CreateDefault();
            var gun = Blueprint(StructureKind.Gun);
            Assert.That(TowerCatalog.Health(Blueprint(StructureKind.Gun, modules: TowerModules.Armor),
                balance, MaterialTier.Pencil), Is.EqualTo(82.5f));
            Assert.That(TowerCatalog.AcquisitionSeconds(Blueprint(StructureKind.Gun, modules: TowerModules.Tracking)),
                Is.LessThan(TowerCatalog.AcquisitionSeconds(gun)));
            Assert.That(TowerCatalog.Validate(Blueprint(StructureKind.Wall), out _), Is.False);
            Assert.That(TowerCatalog.Validate(Blueprint(StructureKind.Gun, modules: (TowerModules)128), out _), Is.False);
        }

        [Test]
        public void FoundationHealthDependsOnChassisArmorAndMaterialRatherThanMountOrder()
        {
            var balance = GameBalance.CreateDefault();
            float gunFirst = TowerCatalog.Health(Blueprint(StructureKind.Gun, StructureKind.Piercer), balance, MaterialTier.Pencil);
            float piercerFirst = TowerCatalog.Health(Blueprint(StructureKind.Piercer, StructureKind.Gun), balance, MaterialTier.Pencil);
            Assert.That(gunFirst, Is.EqualTo(55));
            Assert.That(piercerFirst, Is.EqualTo(gunFirst));
            Assert.That(TowerCatalog.Health(Blueprint(StructureKind.Mortar, modules: TowerModules.Armor),
                balance, MaterialTier.Pencil), Is.EqualTo(82.5f));
            Assert.That(TowerCatalog.Health(Blueprint(StructureKind.Mortar, chassis: ChassisKind.Heavy),
                balance, MaterialTier.Pencil), Is.EqualTo(71.5f).Within(.0001f));
            Assert.That(TowerCatalog.Health(Blueprint(StructureKind.Mortar, modules: TowerModules.Armor),
                balance, MaterialTier.Marker), Is.EqualTo(82.5f * balance.Material(MaterialTier.Marker).HealthMultiplier));
        }

        [Test]
        public void StarterAlbumAnswersEveryBasicWeaponRoleWithoutGold()
        {
            var profile = new WorkshopProfile();
            Assert.That(profile.Gold, Is.Zero);
            Assert.That(profile.Loadout.Count, Is.EqualTo(4));
            foreach (var blueprint in profile.GetLoadoutBlueprints())
                Assert.That(profile.ValidateOwned(blueprint, out string reason), Is.True, reason);
            Assert.That(profile.GetLoadoutBlueprints()[1].SecondWeapon, Is.EqualTo(StructureKind.Gun));
        }

        [Test]
        public void UnlockingPartIsPermanentAndAssemblyDoesNotSpendGold()
        {
            var profile = new WorkshopProfile();
            Assert.That(profile.Unlock("module.feed", out _), Is.False);
            profile.GrantGold(100);
            Assert.That(profile.Unlock("module.feed", out _), Is.True);
            int afterPurchase = profile.Gold;
            var blueprint = Blueprint(StructureKind.Gun, modules: TowerModules.Feed);
            Assert.That(profile.SaveBlueprint(blueprint, out _), Is.True);
            Assert.That(profile.SaveBlueprint(blueprint, out _), Is.True);
            Assert.That(profile.Gold, Is.EqualTo(afterPurchase));
            Assert.That(profile.Unlock("module.feed", out _), Is.True);
            Assert.That(profile.Gold, Is.EqualTo(afterPurchase));
        }

        [Test]
        public void LockedPartsCannotBeSavedOrAssignedThroughProfileData()
        {
            var profile = new WorkshopProfile();
            Assert.That(profile.SaveBlueprint(Blueprint(StructureKind.Gun, modules: TowerModules.Tracking), out _), Is.False);
            var data = profile.ToData();
            data.Version = 1; // Exercise the legacy loader's repair contract.
            data.Album = new[] { new BlueprintData { Id = "cheat", Name = "Locked", Chassis = ChassisKind.Heavy,
                FirstWeapon = StructureKind.Gun, Modules = TowerModules.Tracking } };
            data.Loadout = new[] { "cheat", "cheat", "missing", "" };
            var restored = WorkshopProfile.FromData(data);
            foreach (var blueprint in restored.GetLoadoutBlueprints())
                Assert.That(restored.ValidateOwned(blueprint, out _), Is.True);
            Assert.That(restored.IsUnlocked("module.tracking"), Is.False);
        }

        [Test]
        public void SavedAssemblyIsReplacedByIdAndAssignedLoadoutRemainsValid()
        {
            var profile = new WorkshopProfile();
            var first = new TowerBlueprint("custom", "Первый", ChassisKind.Standard, StructureKind.Gun);
            Assert.That(profile.SaveBlueprint(first, out _), Is.True);
            Assert.That(profile.AssignSlot(0, first.Id, out _), Is.True);
            var replacement = new TowerBlueprint("custom", "Второй", ChassisKind.Standard, StructureKind.Piercer);
            Assert.That(profile.SaveBlueprint(replacement, out _), Is.True);
            Assert.That(profile.GetLoadoutBlueprints()[0].FirstWeapon, Is.EqualTo(StructureKind.Piercer));
            Assert.That(first.FirstWeapon, Is.EqualTo(StructureKind.Gun));
            Assert.That(profile.RemoveBlueprint(first.Id, out _), Is.False);
        }

        [Test]
        public void ProfileRoundTripPreservesGoldUnlocksAlbumAndFourDistinctSlots()
        {
            var profile = new WorkshopProfile();
            profile.GrantGold(500); profile.Unlock("chassis.heavy", out _);
            var restored = WorkshopProfile.FromData(profile.ToData());
            Assert.That(restored.Gold, Is.EqualTo(profile.Gold));
            Assert.That(restored.IsUnlocked("chassis.heavy"), Is.True);
            Assert.That(new HashSet<string>(restored.Loadout).Count, Is.EqualTo(4));
            Assert.That(restored.AssignSlot(0, restored.Loadout[1], out _), Is.False);
        }

        static int FireFor(TowerBlueprint blueprint, float seconds)
        {
            var balance = GameBalance.CreateDefault();
            var weapons = new WeaponSystem(balance);
            var tower = new StructureState { Id = 1, Cell = new Cell(3, 4), Kind = blueprint.FirstWeapon,
                Blueprint = blueprint, Health = 100, MaxHealth = 100, IsComplete = true };
            var towers = new List<StructureState> { tower };
            var enemies = new List<EnemyState> { new EnemyState { Id = 1, Position = new Point2(5.5f, 4.5f), Health = 100000, MaxHealth = 100000 } };
            var stats = new RunStatistics(); var events = new SimulationEventBuffer(); var shots = new List<ShotEvent>();
            for (int i = 0; i < (int)(seconds * 30 + .001f); i++)
            {
                tower.Cooldown = Math.Max(0, tower.Cooldown - 1f / 30);
                tower.SecondaryCooldown = Math.Max(0, tower.SecondaryCooldown - 1f / 30);
                weapons.Fire(towers, enemies, shots, 1, 1, 1f / 30, i, stats, events);
            }
            return stats.Launches(StructureKind.Gun);
        }

        [Test]
        public void TwoGunMountsProduceAboutOnePointSixTimesSingleSustainedFire()
        {
            int single = FireFor(Blueprint(StructureKind.Gun), 30);
            int dual = FireFor(Blueprint(StructureKind.Gun, StructureKind.Gun), 30);
            Assert.That((float)dual / single, Is.InRange(1.5f, 1.7f));
        }

        [Test]
        public void TrackingFiresAtNewTargetBeforeOrdinaryAcquisitionCompletes()
        {
            Assert.That(FireFor(Blueprint(StructureKind.Gun), .1f), Is.Zero);
            Assert.That(FireFor(Blueprint(StructureKind.Gun, modules: TowerModules.Tracking), .1f), Is.EqualTo(1));
        }

        [Test]
        public void AlbumCannotGrowPastSixtyFourOnSaveOrRestore()
        {
            var profile = new WorkshopProfile();
            for (int i = 0; i < 60; i++)
                Assert.That(profile.SaveBlueprint(new TowerBlueprint("custom" + i, "Тест", ChassisKind.Standard, StructureKind.Gun), out _), Is.True);
            Assert.That(profile.SaveBlueprint(new TowerBlueprint("extra", "Лишний", ChassisKind.Standard, StructureKind.Gun), out _), Is.False);
            Assert.That(WorkshopProfile.FromData(profile.ToData()).Album.Count, Is.EqualTo(64));
        }

        [Test] public void FullCustomAlbumRestoresWithoutEvictingAssembliesToRepairSlots()
        {
            var data = new WorkshopProfileData { Album = new BlueprintData[64], Loadout = new[] { "missing", "missing", "", "" } };
            for (int i = 0; i < 64; i++) data.Album[i] = new BlueprintData(new TowerBlueprint("saved" + i,
                "Сохранённый " + i, ChassisKind.Standard, StructureKind.Gun));
            WorkshopProfile restored = WorkshopProfile.FromData(data);
            Assert.That(restored.Album.Count, Is.EqualTo(64));
            Assert.That(restored.Album[63].Id, Is.EqualTo("saved63"));
            Assert.That(new HashSet<string>(restored.Loadout).Count, Is.EqualTo(4));
            Assert.That(restored.IsUnlocked("weapon.gun"), Is.True);
            foreach (TowerBlueprint blueprint in restored.GetLoadoutBlueprints())
                Assert.That(restored.ValidateOwned(blueprint, out _), Is.True);
        }

        [Test]
        public void MortarColorIsCapturedAtLaunchAndAppliesAtImpact()
        {
            var balance = GameBalance.CreateDefault();
            balance.Structure(StructureKind.Mortar).FlightSeconds = .2f;
            var weapons = new WeaponSystem(balance);
            var tower = new StructureState { Id = 1, Cell = new Cell(3, 4), Kind = StructureKind.Mortar,
                Color = InkColor.Blue, Health = 100, IsComplete = true };
            var enemy = new EnemyState { Id = 1, Position = new Point2(5.5f, 4.5f), Health = 100, MaxHealth = 100 };
            var towers = new List<StructureState> { tower }; var enemies = new List<EnemyState> { enemy };
            var stats = new RunStatistics(); var events = new SimulationEventBuffer(); var shots = new List<ShotEvent>();
            weapons.Fire(towers, enemies, shots, 1, 1, .02f, 1, stats, events);
            Assert.That(enemy.BlueRemaining, Is.Zero);
            tower.Color = InkColor.Red;
            weapons.Fire(towers, enemies, shots, 1, 1, .2f, 2, stats, events);
            Assert.That(enemy.BlueRemaining, Is.EqualTo(3));
            Assert.That(enemy.RedRemaining, Is.Zero);
        }
    }
}
