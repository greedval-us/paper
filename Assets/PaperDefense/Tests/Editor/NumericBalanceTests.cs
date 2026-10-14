using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Validation;
using UnityEditor;

namespace PaperDefense.Tests
{
    public sealed class NumericBalanceTests
    {
        static GameBalance Balance() => AssetDatabase.LoadAssetAtPath<BalancePresetAsset>("Assets/PaperDefense/Configuration/Game/PlayerRules.asset").CreateBalance();

        [TestCase("gun-tracking", "gun", "grazing", 55)]
        [TestCase("piercer-rail", "piercer", "standoff", 400)]
        [TestCase("precision", "gun", "edge", 400)]
        public void SituationalModulesEarnTheirCostWhenCoverageIsConstrained(string build, string plain, string lane, int budget)
        {
            var b = Balance();
            var specialized = NumericBalanceScenario.Measure(b, build, lane, budget, 1337);
            var baseline = NumericBalanceScenario.Measure(b, plain, lane, budget, 1337);
            Assert.That(specialized.Unresolved, Is.Zero);
            Assert.That(baseline.Unresolved, Is.Zero);
            Assert.That(specialized.Leaks, Is.LessThan(baseline.Leaks));
        }

        [TestCase(17)] [TestCase(41)] [TestCase(1337)]
        public void SpecialistsHaveDistinctEqualBudgetMatchups(int seed)
        {
            var b = Balance();
            Assert.That(Leaks("fan", "crowd"), Is.LessThan(Leaks("gun", "crowd")), "Short-range fan must pay for its exposure with crowd efficiency.");
            Assert.That(Leaks("piercer", "line"), Is.LessThan(Leaks("gun", "line")), "Piercing needs a useful alignment advantage.");
            Assert.That(Leaks("gun", "fast"), Is.LessThan(Leaks("mortar", "fast")), "Delayed artillery must retain a fast-target weakness.");
            Assert.That(Leaks("mortar-stapler", "fast"), Is.LessThan(Leaks("mortar", "fast")), "Built-in slow must compensate for its cost and shared feed.");
            int Leaks(string build, string lane)
            {
                var r = NumericBalanceScenario.Measure(b, build, lane, 400, seed);
                Assert.That(r.Unresolved, Is.Zero, build + "/" + lane);
                return r.Leaks;
            }
        }

        [Test]
        public void FeedAndArmorHaveAffordableBenefitsWithoutFreeDamage()
        {
            var b = Balance();
            var gun = NumericBalanceScenario.Build(b, "gun");
            var feed = NumericBalanceScenario.Build(b, "gun-feed");
            var armor = NumericBalanceScenario.Build(b, "gun-armor");
            float cost = TowerCatalog.Cost(gun, b, MaterialTier.Pencil);
            Assert.That(TowerCatalog.Cost(feed, b, MaterialTier.Pencil) / cost, Is.LessThanOrEqualTo(b.Construction.FeedModuleMultiplier + .001f));
            float effectiveHealthGain = 1 / (1 - TowerCatalog.Defense(armor, b));
            Assert.That(effectiveHealthGain, Is.GreaterThan(TowerCatalog.Cost(armor, b, MaterialTier.Pencil) / cost));
            Assert.That(TowerCatalog.WeaponDamage(armor, b, "weapon.gun", MaterialTier.Pencil), Is.EqualTo(TowerCatalog.WeaponDamage(gun, b, "weapon.gun", MaterialTier.Pencil)));
        }

        [Test]
        public void ColorsImproveTheirMatchupsAndCleanupKeepsItsSeparateRole()
        {
            var b = Balance();
            Assert.That(Measure("mortar", "fast", InkColor.Blue).Leaks, Is.LessThan(Measure("mortar", "fast").Leaks));
            Assert.That(Measure("gun", "armored", InkColor.Green).Damage, Is.GreaterThan(Measure("gun", "armored").Damage));
            Assert.That(Measure("gun", "armored", InkColor.Red).Damage, Is.GreaterThan(Measure("gun", "armored").Damage));
            var cleanup = Measure("gun-blotter", "blots");
            Assert.That(cleanup.BlotsCleared, Is.GreaterThan(20));
            Assert.That(Measure("blotter", "crowd").Damage, Is.Zero);
            NumericBalanceRow Measure(string build, string lane, InkColor color = InkColor.None)
                => NumericBalanceScenario.Measure(b, build, lane, 400, 1337, color);
        }

        [Test]
        public void BasesAndMaterialsTradeSpaceAndSurvivalForInk()
        {
            var b = Balance();
            foreach (var id in new[] { "heavy", "precision", "battery" })
                Assert.That(TowerCatalog.Validate(NumericBalanceScenario.Build(b, id), b, out string reason), Is.True, reason);
            var gun = NumericBalanceScenario.Build(b, "gun");
            foreach (var material in new[] { MaterialTier.Ink, MaterialTier.Marker })
            {
                float priceRatio = (float)TowerCatalog.Cost(gun, b, material) / TowerCatalog.Cost(gun, b, MaterialTier.Pencil);
                float hpRatio = TowerCatalog.Health(gun, b, material) / TowerCatalog.Health(gun, b, MaterialTier.Pencil);
                Assert.That(hpRatio, Is.GreaterThan(priceRatio));
                Assert.That(TowerCatalog.WeaponDamage(gun, b, "weapon.gun", material), Is.EqualTo(TowerCatalog.WeaponDamage(gun, b, "weapon.gun", MaterialTier.Pencil)));
            }
            var battery = b.Content.GetChassis("chassis.battery");
            Assert.That(battery.FeedCapacity, Is.LessThan(3), "Three mounts must retain a shared-feed tradeoff.");
            Assert.That(TowerCatalog.Health(NumericBalanceScenario.Build(b, "battery"), b, MaterialTier.Pencil),
                Is.LessThan(3 * TowerCatalog.Health(gun, b, MaterialTier.Pencil)), "Concentration must retain a survival cost.");
        }

        [Test]
        public void TarImprovesSpikeContactDamageUsingPlayerRules()
        {
            Assert.That(Damage(true), Is.GreaterThan(Damage(false)));
            float Damage(bool tar)
            {
                var b = Balance(); b.Waves[0].Count = 1; b.PlanningSeconds = 10000; b.Crowd.Enabled = false;
                var g = new GameSession(b, 17, false);
                Assert.That(g.TryBuild(new[] { new Cell(39, 32) }, StructureKind.Spikes, MaterialTier.Pencil, out string reason), Is.True, reason);
                if (tar) Assert.That(g.TryBuild(new[] { new Cell(38, 32) }, StructureKind.TarPit, MaterialTier.Pencil, out reason), Is.True, reason);
                g.Tick(2); g.StartNextWave(); g.Tick(.05f);
                Assert.That(g.Enemies.Count, Is.EqualTo(1));
                g.Enemies[0].Position = new Point2(37.5f, 32.5f);
                for (int i = 0; i < 150; i++) g.Tick(b.SimulationStep);
                return g.Statistics.TotalDamage;
            }
        }

        [Test]
        public void RepairSustainsChipDamageButCannotOuthealSiegeOrStack()
        {
            var b = Balance(); b.PlanningSeconds = 10000;
            var post = b.Structure(StructureKind.RepairPost);
            var siege = b.Enemy(EnemyKind.Gnawer);
            Assert.That(post.RepairPerSecond, Is.LessThan(siege.Damage * siege.StructureDamageMultiplier / siege.AttackInterval));
            var g = new GameSession(b, 17, false);
            Assert.That(g.TryBuild(new[] { new Cell(46, 32) }, StructureKind.Wall, MaterialTier.Pencil, out _), Is.True);
            Assert.That(g.TryBuild(new[] { new Cell(44, 32), new Cell(46, 34) }, StructureKind.RepairPost, MaterialTier.Pencil, out _), Is.True);
            g.Tick(3); var wall = g.StructureAt(new Cell(46, 32)); wall.Health = 20;
            g.Tick(1);
            Assert.That(wall.Health, Is.EqualTo(20 + post.RepairPerSecond).Within(.01f));
            Assert.That(g.Structures.Where(s => s.Kind == StructureKind.RepairPost).All(s => s.IsComplete), Is.True);
        }

        [Test]
        public void ExpectedWaveHealthRisesWithoutAbruptDoubling()
        {
            var b = Balance(); double previous = 0;
            for (int i = 0; i < b.Waves.Count; i++)
            {
                var w = b.Waves[i];
                double[] fractions = { 1-w.RunnerFraction-w.GnawerFraction-w.SpitterFraction-w.BruteFraction-w.BulwarkFraction,
                    w.RunnerFraction, w.GnawerFraction, w.SpitterFraction, w.BruteFraction, w.BulwarkFraction };
                double hp = 0;
                for (int kind = 0; kind < fractions.Length; kind++) hp += fractions[kind] * b.Enemy((EnemyKind)kind).MaxHealth;
                hp *= w.Count * (1 + i * b.WaveHealthGrowth) * (1 + w.OutlinedFraction * (b.Effects.OutlinedHealthMultiplier - 1) + w.HatchedFraction * (b.Effects.HatchedHealthMultiplier - 1));
                if (i > 0) Assert.That(hp / previous, Is.InRange(1.15, 1.85), "Wave " + (i + 1));
                previous = hp;
            }
        }
    }
}
