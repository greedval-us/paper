using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class TrialBalanceIntegrationTests
    {
        readonly List<Object> owned = new List<Object>();

        [TearDown] public void TearDown()
        {
            foreach (Object value in owned) Object.DestroyImmediate(value);
            owned.Clear();
        }

        GameBalanceAsset Asset()
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            owned.Add(asset); asset.ResetToDefaults();
            return asset;
        }

        RunController Controller(GameBalanceAsset asset)
        {
            var root = new GameObject("Configured trial balance controller");
            root.SetActive(false); owned.Insert(0, root);
            var controller = root.AddComponent<RunController>();
            controller.balanceAsset = asset;
            controller.InitializeWorkshop(false);
            return controller;
        }

        static GameBalanceAsset.WeaponSettings Weapon(GameBalanceAsset asset, StructureKind kind)
        {
            foreach (var weapon in asset.weapons) if (weapon.kind == kind) return weapon;
            Assert.Fail("Missing weapon " + kind); return null;
        }

        [Test]
        public void PreviewTrialAndBattleConstructionUseTheSameConfiguredPriceAndMaterialHealth()
        {
            var asset = Asset();
            var gun = Weapon(asset, StructureKind.Gun);
            gun.cost = 99; gun.shotInterval = 1.3f; gun.range = 7;
            asset.materials[0].health = 2;
            var controller = Controller(asset);
            TowerBlueprint selected = controller.Workshop.Loadout[0];
            TowerPreview preview = controller.PreviewBlueprint(selected);
            TrialReport trial = controller.RunTrial(selected, TrialScenario.Crowd).First;
            Assert.That(preview.Cost, Is.EqualTo(99));
            Assert.That(preview.Health, Is.EqualTo(110));
            Assert.That(preview.FireRate, Is.EqualTo(1f / 1.3f).Within(.0001f));
            Assert.That(preview.Range, Is.EqualTo(7));
            Assert.That(trial.Copies, Is.EqualTo(4));
            Assert.That(trial.Cost, Is.EqualTo(396));
            Assert.That(trial.Frames[0].Towers[0].Health, Is.EqualTo(preview.Health));

            Assert.That(controller.StartRun(out string reason), Is.True, reason);
            var cell = new Cell(10, 10);
            int before = controller.Session.Ink;
            Assert.That(controller.BuildBlueprint(new[] { cell }, 0, MaterialTier.Pencil, InkColor.None, out reason), Is.True, reason);
            Assert.That(before - controller.Session.Ink, Is.EqualTo(preview.Cost));
            Assert.That(controller.Session.StructureAt(cell).Value.MaxHealth, Is.EqualTo(preview.Health));
        }

        [TestCase("damage")]
        [TestCase("range")]
        public void TrialFireUsesConfiguredWeaponDamageAndRange(string parameter)
        {
            var asset = Asset();
            var gun = Weapon(asset, StructureKind.Gun);
            if (parameter == "damage") gun.damage = 0;
            else gun.range = 0;
            var controller = Controller(asset);
            TrialReport report = controller.RunTrial(controller.Workshop.Loadout[0], TrialScenario.Crowd, budget: 40).First;
            Assert.That(report.Damage, Is.Zero);
        }

        [Test]
        public void TrialFireUsesConfiguredShotInterval()
        {
            var standard = Controller(Asset());
            float standardDamage = standard.RunTrial(standard.Workshop.Loadout[0], TrialScenario.Crowd, budget: 40).First.Damage;
            var asset = Asset();
            Weapon(asset, StructureKind.Gun).shotInterval = 1000;
            var slower = Controller(asset);
            float slowDamage = slower.RunTrial(slower.Workshop.Loadout[0], TrialScenario.Crowd, budget: 40).First.Damage;
            Assert.That(slowDamage, Is.LessThan(standardDamage));
        }

        [Test]
        public void TrialPiercerUsesConfiguredTargetLimit()
        {
            var narrowAsset = Asset();
            Weapon(narrowAsset, StructureKind.Piercer).pierce = 1;
            Weapon(narrowAsset, StructureKind.Piercer).radius = 4;
            var narrow = Controller(narrowAsset);
            TowerBlueprint selected = narrow.Workshop.Loadout[2];
            float singleTargetDamage = narrow.RunTrial(selected, TrialScenario.Crowd, budget: 65).First.Damage;
            var broadAsset = Asset();
            Weapon(broadAsset, StructureKind.Piercer).pierce = 7;
            Weapon(broadAsset, StructureKind.Piercer).radius = 4;
            var broad = Controller(broadAsset);
            float broadDamage = broad.RunTrial(broad.Workshop.Loadout[2], TrialScenario.Crowd, budget: 65).First.Damage;
            Assert.That(broadDamage, Is.GreaterThan(singleTargetDamage));
        }

        [Test]
        public void TrialMortarUsesConfiguredRadiusAndFlightDuration()
        {
            var asset = Asset();
            Weapon(asset, StructureKind.Mortar).radius = 3.7f;
            Weapon(asset, StructureKind.Mortar).flightSeconds = 2.1f;
            var controller = Controller(asset);
            TrialReport report = controller.RunTrial(controller.Workshop.Loadout[3], TrialScenario.Crowd).First;
            bool found = false;
            foreach (TrialFrame frame in report.Frames)
                foreach (ProjectileView projectile in frame.Projectiles)
                {
                    found = true;
                    Assert.That(projectile.Radius, Is.EqualTo(3.7f));
                    Assert.That(projectile.Duration, Is.EqualTo(2.1f));
                }
            Assert.That(found, Is.True);
        }

        [Test]
        public void ChangedTrialBalanceStillLeavesProfileRunTickAndCurrencyUntouched()
        {
            var asset = Asset();
            Weapon(asset, StructureKind.Gun).cost = 99;
            var controller = Controller(asset);
            int ink = controller.Session.Ink, gold = controller.Workshop.Gold, album = controller.Workshop.Album.Count;
            long tick = controller.Session.TickIndex;
            TrialComparison report = controller.RunTrial(controller.Workshop.Loadout[0], TrialScenario.Fast, true, 400);
            Assert.That(report.First.Cost, Is.EqualTo(396));
            Assert.That(report.Second.Cost, Is.EqualTo(396));
            Assert.That(controller.Session.Ink, Is.EqualTo(ink));
            Assert.That(controller.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(controller.Workshop.Album.Count, Is.EqualTo(album));
            Assert.That(controller.Session.TickIndex, Is.EqualTo(tick));
            Assert.That(Weapon(asset, StructureKind.Gun).cost, Is.EqualTo(99));
        }
    }
}
