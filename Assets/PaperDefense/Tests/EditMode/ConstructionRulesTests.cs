using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class ConstructionRulesTests
    {
        static TowerBlueprint Dual => new TowerBlueprint("dual", "Два ствола", ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun);

        [Test] public void ExtractedDefaultsKeepEstablishedAssemblyCostsHealthTimingAndConstraints()
        {
            var balance = GameBalance.CreateDefault(); var page = Dual;
            Assert.That(TowerCatalog.Load(page, balance), Is.EqualTo(8));
            Assert.That(TowerCatalog.Capacity(page, balance), Is.EqualTo(10));
            Assert.That(TowerCatalog.Cost(page, balance, MaterialTier.Pencil), Is.EqualTo(64));
            Assert.That(TowerCatalog.Health(page, balance, MaterialTier.Pencil), Is.EqualTo(55));
            Assert.That(TowerCatalog.BuildSeconds(page, balance, MaterialTier.Pencil), Is.EqualTo(1.8f).Within(.0001f));
            Assert.That(TowerCatalog.FeedMultiplier(page, balance), Is.EqualTo(.8f));
            Assert.That(TowerCatalog.AcquisitionSeconds(page, balance), Is.EqualTo(.25f));
            Assert.That(TowerCatalog.ColorCost(page, balance), Is.EqualTo(8));
        }

        [Test] public void ConfiguredWeaponLoadsAndCapacityControlActualAssemblyValidation()
        {
            var balance = GameBalance.CreateDefault();
            balance.Construction.GunLoad = 6; balance.Construction.StandardCapacity = 11;
            Assert.That(TowerCatalog.Load(Dual, balance), Is.EqualTo(12));
            Assert.That(TowerCatalog.Capacity(Dual, balance), Is.EqualTo(11));
            Assert.That(TowerCatalog.Validate(Dual, balance, out string reason), Is.False);
            Assert.That(reason, Does.Contain("1"));
            balance.Construction.StandardCapacity = 12;
            Assert.That(TowerCatalog.Validate(Dual, balance, out reason), Is.True, reason);
            Assert.That(TowerCatalog.Load(Dual), Is.EqualTo(8)); // Explicit historical wrapper remains independent.
        }

        [Test] public void ConfiguredFoundationModulesAndPaintPriceReplaceEveryAssemblyConstant()
        {
            var balance = GameBalance.CreateDefault(); var rules = balance.Construction;
            rules.StandardBaseCost = 20; rules.WeaponAssemblyCredit = 10; rules.ArmorCost = 7;
            rules.FoundationHealth = 70; rules.ArmorHealthMultiplier = 2;
            rules.ArmorBuildSeconds = .6f; rules.SecondaryBuildSeconds = .9f;
            rules.ColorMinimum = 4; rules.ColorCostDivisor = 10;
            var page = new TowerBlueprint("armored", "Броня", ChassisKind.Standard, StructureKind.Gun, modules: TowerModules.Armor);
            Assert.That(TowerCatalog.Cost(page, balance, MaterialTier.Pencil), Is.EqualTo(57));
            Assert.That(TowerCatalog.Health(page, balance, MaterialTier.Pencil), Is.EqualTo(140));
            Assert.That(TowerCatalog.BuildSeconds(page, balance, MaterialTier.Pencil), Is.EqualTo(2).Within(.0001f));
            Assert.That(TowerCatalog.ColorCost(page, balance), Is.EqualTo(6));
            Assert.That(TowerCatalog.BuildSeconds(Dual, balance, MaterialTier.Pencil), Is.EqualTo(2.3f).Within(.0001f));
        }

        [Test] public void ConfiguredFeedAndAcquisitionChangeProductionWeaponFire()
        {
            var standard = GameBalance.CreateDefault(); int normal = Fire(standard, Dual, 10);
            var changed = standard.CreateSnapshot(); changed.Construction.DualFeedMultiplier = .1f;
            Assert.That(Fire(changed, Dual, 10), Is.LessThan(normal / 2));
            var single = new TowerBlueprint("one", "Один", ChassisKind.Standard, StructureKind.Gun);
            Assert.That(Fire(standard, single, .1f), Is.Zero);
            changed.Construction.AcquisitionSeconds = 0;
            Assert.That(Fire(changed, single, .1f), Is.GreaterThan(0));
        }

        static int Fire(GameBalance balance, TowerBlueprint blueprint, float seconds)
        {
            var weapon = new WeaponSystem(balance); var tower = new StructureState { Id = 1, Cell = new Cell(3, 4), Kind = StructureKind.Gun,
                Blueprint = blueprint, Health = 100, MaxHealth = 100, IsComplete = true };
            var towers = new List<StructureState> { tower };
            var enemies = new List<EnemyState> { new EnemyState { Id = 1, Position = new Point2(5.5f, 4.5f), Health = 100000, MaxHealth = 100000 } };
            var statistics = new RunStatistics(); var events = new SimulationEventBuffer(); var shots = new List<ShotEvent>();
            for (int i = 0; i < (int)(seconds * 30 + .001f); i++)
            {
                tower.Cooldown = Math.Max(0, tower.Cooldown - 1f / 30); tower.SecondaryCooldown = Math.Max(0, tower.SecondaryCooldown - 1f / 30);
                weapon.Fire(towers, enemies, shots, 1, 1, 1f / 30, i, statistics, events);
            }
            return statistics.Launches(StructureKind.Gun);
        }

        [Test] public void ConfiguredUnlockGoldIsUsedWithoutChangingHistoricalPartPrices()
        {
            var balance = GameBalance.CreateDefault(); balance.Construction.HeavyUnlockGold = 7;
            var profile = new WorkshopProfile(); profile.GrantGold(10);
            Assert.That(profile.GetParts(balance).Single(p => p.Id == "chassis.heavy").GoldCost, Is.EqualTo(7));
            Assert.That(profile.Unlock("chassis.heavy", out string reason, balance), Is.True, reason);
            Assert.That(profile.Gold, Is.EqualTo(3));
            Assert.That(profile.Parts.Single(p => p.Id == "chassis.heavy").GoldCost, Is.EqualTo(90));
        }

        [Test] public void AffordableAssemblyUsesItsOwnPriceBeforeCheckingAvailableInk()
        {
            var balance = GameBalance.CreateDefault(); balance.StartingInk = 10;
            balance.Construction.StandardBaseCost = 5; balance.Construction.WeaponAssemblyCredit = 40;
            var session = new GameSession(balance, starterDefenses: false);
            var page = new TowerBlueprint("cheap", "Лёгкая сборка", ChassisKind.Standard, StructureKind.Gun);
            var cells = new[] { new Cell(4, 4) };
            var quote = session.QuoteBlueprint(cells, page, MaterialTier.Pencil, InkColor.None);
            Assert.That(quote.Valid, Is.True, quote.Reason);
            Assert.That(quote.Cost, Is.EqualTo(5));
            Assert.That(session.TryBuildBlueprint(cells, page, MaterialTier.Pencil, InkColor.None, out string reason), Is.True, reason);
            Assert.That(session.Ink, Is.EqualTo(5));
        }

        [Test] public void LoadedOwnedPagesKeepTheirGeometryWhenCurrentCapacityChanges()
        {
            var balance = GameBalance.CreateDefault(); balance.Construction.StandardCapacity = 20;
            var profile = new WorkshopProfile();
            var page = new TowerBlueprint("wide", "По старым правилам", ChassisKind.Standard, StructureKind.Gun, StructureKind.Mortar);
            Assert.That(profile.SaveBlueprint(page, out string reason, balance), Is.True, reason);
            Assert.That(profile.AssignSlot(0, page.Id, out reason, balance), Is.True, reason);
            Assert.That(TowerCatalog.ValidateParts(page, out reason), Is.True, reason);
            var restored = WorkshopProfile.FromData(profile.ToData());
            Assert.That(restored.Album.Count, Is.EqualTo(5));
            Assert.That(restored.Loadout[0], Is.EqualTo("wide"));
            Assert.That(restored.ValidateOwned(restored.GetLoadoutBlueprints()[0], out _, GameBalance.CreateDefault()), Is.False);
            Assert.That(restored.ValidateOwned(restored.GetLoadoutBlueprints()[0], out reason, balance), Is.True, reason);
        }

        [Test] public void RuleSnapshotIsIndependentAndRejectsNonfiniteTuning()
        {
            var balance = GameBalance.CreateDefault(); var snapshot = balance.CreateSnapshot();
            snapshot.Construction.StandardCapacity = 99;
            Assert.That(balance.Construction.StandardCapacity, Is.EqualTo(10));
            balance.Construction.FoundationHealth = float.NaN;
            Assert.That(() => balance.Construction.CreateSnapshot(), Throws.InstanceOf<ArgumentException>());
        }
    }
}
