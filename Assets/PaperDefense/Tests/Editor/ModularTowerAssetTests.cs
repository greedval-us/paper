using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;

namespace PaperDefense.Tests
{
    public sealed class ModularTowerAssetTests
    {
        static GameBalanceAsset Asset(string name) => AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(
            "Assets/PaperDefense/Configuration/" + name + ".asset");

        [Test]
        public void TargetContainsOneThreeAndThreeOneSlotTradeoffs()
        {
            var content = Asset("TargetNotebookBalance").CreateBalance().ResolveContent();
            Assert.That(content.TryGetChassis("chassis.precision", out var precision), Is.True);
            Assert.That(content.TryGetChassis("chassis.battery", out var battery), Is.True);
            Assert.That(precision.Enabled && battery.Enabled, Is.True);
            Assert.That(precision.MountCount, Is.EqualTo(1));
            Assert.That(battery.MountCount, Is.EqualTo(3));
            Assert.That(precision.Capacity, Is.EqualTo(16));
            Assert.That(battery.Capacity, Is.EqualTo(22));
            Assert.That(precision.BaseCost, Is.EqualTo(20));
            Assert.That(battery.BaseCost, Is.EqualTo(34));
        }

        [Test]
        public void TargetDefinesCloseMediumAndLongFiringBands()
        {
            var content = Asset("TargetNotebookBalance").CreateBalance().ResolveContent();
            Assert.That(content.GetWeapon("weapon.gun").Stats.Range, Is.EqualTo(8));
            Assert.That(content.GetWeapon("weapon.piercer").Stats.Range, Is.EqualTo(12));
            var mortar = content.GetWeapon("weapon.mortar");
            Assert.That(mortar.Stats.Range, Is.EqualTo(18));
            var minimum = mortar.GetType().GetProperty("MinimumRange");
            Assert.That(minimum, Is.Not.Null);
            Assert.That(minimum.GetValue(mortar), Is.EqualTo(6f));
        }

        [Test]
        public void HistoricalRangesAndBasePricesArePreserved()
        {
            var content = Asset("NotebookBalance").CreateBalance().ResolveContent();
            Assert.That(content.GetWeapon("weapon.gun").Stats.Range, Is.EqualTo(11));
            Assert.That(content.GetWeapon("weapon.piercer").Stats.Range, Is.EqualTo(14));
            Assert.That(content.GetWeapon("weapon.mortar").Stats.Range, Is.EqualTo(16));
            Assert.That(content.GetChassis("chassis.standard").BaseCost, Is.EqualTo(16));
            Assert.That(content.GetChassis("chassis.heavy").BaseCost, Is.EqualTo(28));
        }

        [Test]
        public void OneCellCombatHasDifferentUsefulChassisAndWeaponNiches()
        {
            var balance = Asset("TargetNotebookBalance").CreateBalance();
            var gun = new TowerBlueprint("niche.gun", "gun", "chassis.standard", "weapon.gun", content: balance.Content);
            var precision = new TowerBlueprint("niche.precision", "precision", "chassis.precision", "weapon.mortar",
                modules: new[] { new ModuleSelection("feed", "module.feed"), new ModuleSelection("tracking", "module.tracking"),
                    new ModuleSelection("rail", "module.rail"), new ModuleSelection("armor", "module.armor") }, content: balance.Content);
            var battery = new TowerBlueprint("niche.battery", "battery", "chassis.battery", new[] {
                new WeaponMountSelection(0, "weapon.mortar"), new WeaponMountSelection(1, "weapon.mortar"),
                new WeaponMountSelection(2, "weapon.mortar") }, content: balance.Content);
            Assert.That(Damage(gun, balance, 3), Is.GreaterThan(0), "Close defense remains useful.");
            Assert.That(Damage(precision, balance, 3), Is.Zero, "Rail does not remove the mortar dead zone.");
            Assert.That(Damage(battery, balance, 3), Is.Zero);
            Assert.That(Damage(gun, balance, 17), Is.Zero);
            Assert.That(Damage(battery, balance, 17), Is.GreaterThan(Damage(precision, balance, 17)),
                "A costly three-barrel battery concentrates more artillery on one cell.");
            Assert.That(Damage(precision, balance, 21), Is.GreaterThan(0), "The upgrade-focused chassis reaches farther.");
            Assert.That(Damage(battery, balance, 21), Is.Zero);
        }

        static float Damage(TowerBlueprint blueprint, GameBalance balance, float distance)
        {
            var result = ContentCombatProbe.Run(blueprint, balance, new Cell(10, 10),
                new[] { new Point2(distance, 0) }, 100000, 1f / 30, 600, false);
            return result.TargetDamage[0];
        }
    }
}
