using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;

namespace PaperDefense.Tests
{
    public sealed class AssemblyBalanceStatisticsTests
    {
        static T Metric<T>(RunStatistics statistics, string name)
        {
            var property = typeof(RunStatistics).GetProperty(name);
            Assert.That(property, Is.Not.Null, "Missing measured production statistic: " + name);
            return (T)property.GetValue(statistics);
        }

        static GameBalance QuietRules()
        {
            var rules = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(
                "Assets/PaperDefense/Configuration/Game/PlayerBalance.asset").CreateWorkshopBalance();
            rules.PlanningSeconds = rules.IntermissionSeconds = 10000;
            rules.PassiveInkPerSecond = rules.EarlyLaunchInkPerSecond = 0;
            rules.MaxEarlyLaunchInk = 0; rules.StartingInk = 1000;
            rules.WaveInkGrant = 0; rules.Progression.UpgradeEveryWaves = 0;
            rules.Waves.Clear();
            for (int i = 0; i < 8; i++) rules.Waves.Add(new WaveDefinition { Count = 0, InkGrant = 0 });
            return rules;
        }

        static ResolvedEntry Entry(GameBalance rules, string id)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(
                "Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
            return TowerCatalog.ResolveEntry(catalog.Starters.Single(e => e.Id == id), rules).Value;
        }

        [Test]
        public void RepairCountsOnlyHealthActuallyRestoredAndStopsAtFullHealth()
        {
            var rules = QuietRules(); var session = new GameSession(rules, 1337, false);
            var wall = new Cell(session.BaseCell.X, session.BaseCell.Y + 3);
            var post = new Cell(session.BaseCell.X, session.BaseCell.Y + 2);
            Assert.That(session.TryBuildEntry(new[] { wall }, Entry(rules, "album.wall"), MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
            Assert.That(session.TryBuildEntry(new[] { post }, Entry(rules, "album.repair"), MaterialTier.Pencil, InkColor.None, out reason), Is.True, reason);
            session.Tick(10);
            var target = session.StructureAt(wall); target.Health = target.MaxHealth - 1;
            session.Tick(1);
            Assert.That(Metric<float>(session.Statistics, "TotalRepairedHealth"), Is.EqualTo(1).Within(.0001f));
            session.Tick(1);
            Assert.That(Metric<float>(session.Statistics, "TotalRepairedHealth"), Is.EqualTo(1).Within(.0001f));
        }

        [Test]
        public void GeneratorCountsOnlyActualWavePayoutAndCannotPayAgainWhileIdle()
        {
            var rules = QuietRules(); var session = new GameSession(rules, 2026, false);
            var generator = Entry(rules, "album.generator");
            Assert.That(session.TryBuildEntry(new[] { new Cell(session.BaseCell.X - 2, session.BaseCell.Y) }, generator,
                MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
            session.Tick(10);
            Assert.That(Metric<int>(session.Statistics, "TotalGeneratorIncome"), Is.Zero);
            session.StartNextWave(); session.Tick(rules.SimulationStep);
            Assert.That(Metric<int>(session.Statistics, "TotalGeneratorIncome"), Is.EqualTo(generator.Support.IncomePerWave));
            session.Tick(1);
            Assert.That(Metric<int>(session.Statistics, "TotalGeneratorIncome"), Is.EqualTo(generator.Support.IncomePerWave));
        }

        [Test]
        public void PreparedEnemiesAndRealBaseAttacksAreCountedWithoutEventBufferInference()
        {
            var rules = QuietRules(); rules.Enemy(EnemyKind.Doodle).Damage = 1;
            var target = new PreparedEnemy(new Point2(rules.Width / 2 + .5f, rules.Height / 2 + .5f), 10,
                EnemyKind.Doodle, DrawingTier.Sketch, true);
            var session = GameSession.CreatePrepared(rules, 4242, new[] { target });
            Assert.That(Metric<int>(session.Statistics, "TotalSpawnedEnemies"), Is.EqualTo(1));
            session.Tick(rules.SimulationStep);
            Assert.That(Metric<int>(session.Statistics, "BaseHits"), Is.EqualTo(1));
            Assert.That(Metric<float>(session.Statistics, "BaseDamageTaken"), Is.EqualTo(1));
        }

        [Test]
        public void InvalidMeasurementInputsCannotContaminateAggregates()
        {
            var stats = new RunStatistics();
            var repair = typeof(RunStatistics).GetMethod("RecordRepair");
            Assert.That(repair, Is.Not.Null, "Missing production repair measurement hook.");
            foreach (float value in new[] { -1, float.NaN, float.PositiveInfinity }) repair.Invoke(stats, new object[] { value });
            Assert.That(Metric<float>(stats, "TotalRepairedHealth"), Is.Zero);
        }
    }
}
