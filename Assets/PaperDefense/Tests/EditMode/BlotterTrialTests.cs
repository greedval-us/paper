using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class BlotterTrialTests
    {
        static TowerBlueprint Support => new TowerBlueprint("blot-trial", "Промокашка", ChassisKind.Standard, (StructureKind)8);
        static TowerBlueprint Gun => new TowerBlueprint("blot-control", "Пулемёт", ChassisKind.Standard, StructureKind.Gun);

        static GameBalance LiveBalance()
        {
            var balance = GameBalance.CreateDefault();
            balance.Width = 40; balance.Height = 24; balance.StartingInk = 1000;
            balance.PlanningSeconds = 1000; balance.EnableEnemyMechanics = true; balance.EnableInkWells = false;
            balance.PassiveInkPerSecond = balance.EarlyLaunchInkPerSecond = 0;
            balance.Construction.DynamicFeed = true;
            balance.Waves = new List<WaveDefinition> { new WaveDefinition { Count = 3, SpawnInterval = .005f, Sides = 1 } };
            var enemy = balance.Enemy(EnemyKind.Doodle);
            enemy.Speed = enemy.Damage = 0; enemy.MaxHealth = 100; enemy.Reward = 0;
            var support = balance.Structure((StructureKind)8);
            support.BuildSeconds = 0; support.Range = 7; support.Radius = 1.5f; support.ShotInterval = .1f;
            return balance;
        }

        [Test]
        public void BlotQueriesUseCellCentersIgnoreExpiredMarksAndResolveTiesByCell()
        {
            var marks = new List<BlotState>
            {
                new BlotState { Cell = new Cell(7, 5), Remaining = 5 },
                new BlotState { Cell = new Cell(5, 7), Remaining = 5 },
                new BlotState { Cell = new Cell(5, 5), Remaining = 0 }
            };
            Assert.That(BlotQueries.TryFindNearest(marks, new Point2(5.5f, 5.5f), 2, out Cell nearest), Is.True);
            Assert.That(nearest, Is.EqualTo(new Cell(7, 5)));
            marks.Reverse();
            Assert.That(BlotQueries.TryFindNearest(marks, new Point2(5.5f, 5.5f), 2, out nearest), Is.True);
            Assert.That(nearest, Is.EqualTo(new Cell(7, 5)));
            Assert.That(BlotQueries.TryFindNearest(marks, new Point2(5.5f, 5.5f), 1.99f, out _), Is.False);
            Assert.That(BlotQueries.TryFindNearest(marks, new Point2(7.5f, 5.5f), 0, out nearest), Is.True);
            Assert.That(nearest, Is.EqualTo(new Cell(7, 5)));
        }

        [Test]
        public void ClearCountsLiveMarksOnlyOnceAndInvalidRadiiDoNotMutateThem()
        {
            var marks = new List<BlotState>
            {
                new BlotState { Cell = new Cell(5, 5), Remaining = 3 },
                new BlotState { Cell = new Cell(6, 5), Remaining = 4 },
                new BlotState { Cell = new Cell(8, 5), Remaining = 6 },
                new BlotState { Cell = new Cell(5, 6), Remaining = 0 }
            };
            foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity })
            {
                Assert.That(BlotQueries.Clear(marks, new Cell(5, 5), invalid), Is.Zero);
                Assert.That(BlotQueries.TryFindNearest(marks, new Point2(5.5f, 5.5f), invalid, out _), Is.False);
            }
            Assert.That(marks[0].Remaining, Is.EqualTo(3));
            Assert.That(BlotQueries.Clear(marks, new Cell(5, 5), 1), Is.EqualTo(2));
            Assert.That(BlotQueries.Clear(marks, new Cell(5, 5), 1), Is.Zero);
            Assert.That(marks[2].Remaining, Is.EqualTo(6));
        }

        [Test]
        public void LiveContextSeesCorpsesCreatedAfterItAndSharesTheExistingMarkCollection()
        {
            var session = new GameSession(LiveBalance(), starterDefenses: false);
            IBlotContext context = session.CreateBlotContext();
            Assert.That(context.TryFindNearest(new Point2(5.5f, 5.5f), 5, out _), Is.False);
            session.StartNextWave(); session.Tick(.02f);
            session.Enemies[0].Position = new Point2(5.5f, 5.5f);
            session.Enemies[1].Position = new Point2(6.5f, 5.5f);
            session.Enemies[2].Position = new Point2(2.5f, 2.5f);
            session.Enemies[0].Health = session.Enemies[1].Health = 0;
            session.Tick(1f / 30);
            Assert.That(session.Blots.Count, Is.EqualTo(2));
            Assert.That(context.TryFindNearest(new Point2(5.5f, 5.5f), 0, out Cell center), Is.True);
            int ink = session.Ink;
            Assert.That(context.Clear(center, 1), Is.EqualTo(2));
            Assert.That(session.Blots, Is.Empty);
            Assert.That(session.Ink, Is.EqualTo(ink));
            Assert.That(session.BlotterCooldownRemaining, Is.Zero);
        }

        [Test]
        public void LiveAutomaticSupportCleansNearbyCorpsesWithNoEnemyInItsRange()
        {
            var session = new GameSession(LiveBalance(), starterDefenses: false);
            Assert.That(session.TryBuildBlueprint(new[] { new Cell(8, 6) }, Support, MaterialTier.Pencil, InkColor.None, out string reason),
                Is.True, reason);
            session.StartNextWave(); session.Tick(.02f);
            session.Enemies[0].Position = new Point2(9.5f, 6.5f);
            session.Enemies[1].Position = new Point2(10.5f, 6.5f);
            session.Enemies[2].Position = new Point2(2.5f, 2.5f);
            session.Enemies[0].Health = session.Enemies[1].Health = 0;
            session.Tick(1f / 30);
            int ink = session.Ink;
            for (int i = 0; i < 90; i++) session.Tick(1f / 30);
            Assert.That(session.Blots, Is.Empty);
            Assert.That(session.Statistics.BlotsCleared, Is.EqualTo(2));
            Assert.That(session.Statistics.Damage((StructureKind)8), Is.Zero);
            Assert.That(session.Enemies[0].Health, Is.EqualTo(session.Enemies[0].MaxHealth));
            Assert.That(session.Ink, Is.EqualTo(ink));
            Assert.That(session.BlotterCooldownRemaining, Is.Zero);
        }

        [Test]
        public void UncleanedFiniteTrialMarksRemainUnresolvedForTheFullDurationWithoutEnemies()
        {
            TrialReport report = TrialRunner.Run(Gun, GameBalance.CreateDefault(), TrialScenario.Blots, seed: 79);
            Assert.That(report.Frames[0].Blots.Count, Is.GreaterThan(0).And.LessThanOrEqualTo(128));
            Assert.That(report.BlotsCleared, Is.Zero);
            Assert.That(report.UnresolvedBlots, Is.EqualTo(report.Frames[0].Blots.Count));
            Assert.That(report.DurationSeconds, Is.EqualTo(60).Within(.001));
            Assert.That(report.Damage, Is.Zero);
            Assert.That(report.Leaks, Is.Zero);
            foreach (TrialFrame frame in report.Frames) Assert.That(frame.Enemies, Is.Empty);
            StringAssert.Contains("finite", report.Scope);
        }

        [Test]
        public void SupportTrialClearsTheFiniteFixtureAndOwnsPlaybackSnapshotsWithoutRewards()
        {
            var balance = GameBalance.CreateDefault();
            balance.Construction.DynamicFeed = true;
            var support = balance.Structure((StructureKind)8);
            support.Range = support.Radius = 100; support.ShotInterval = .1f;
            int cost = TowerCatalog.Cost(Support, balance, MaterialTier.Pencil);
            TrialReport report = TrialRunner.Run(Support, balance, TrialScenario.Blots, budget: cost, seed: 79);
            TrialFrame first = report.Frames[0], last = report.Frames[report.Frames.Count - 1];
            Assert.That(first.Blots.Count, Is.GreaterThan(0));
            Assert.That(report.BlotsCleared, Is.EqualTo(first.Blots.Count));
            Assert.That(report.UnresolvedBlots, Is.Zero);
            Assert.That(last.Blots, Is.Empty);
            Assert.That(first.Blots[0].Remaining, Is.GreaterThan(0), "Later cleanup must not rewrite an earlier playback frame.");
            Assert.That(report.DurationSeconds, Is.LessThan(60));
            Assert.That(report.Damage, Is.Zero);
            Assert.That(report.UnresolvedEnemies, Is.Zero);
            Assert.That(report.Cost, Is.EqualTo(cost));
            Assert.That(support.Range, Is.EqualTo(100));
            Assert.That(balance.Construction.DynamicFeed, Is.True);
        }

        [Test]
        public void BlotTrialComparisonsHaveIdenticalSeededMarksAndRepeatedRunsAreDeterministic()
        {
            var balance = GameBalance.CreateDefault();
            TrialComparison comparison = TrialRunner.Compare(Support, Gun, balance, TrialScenario.Blots, seed: 173);
            TrialReport repeat = TrialRunner.Run(Support, balance, TrialScenario.Blots, seed: 173);
            IReadOnlyList<BlotView> first = comparison.First.Frames[0].Blots, second = comparison.Second.Frames[0].Blots;
            Assert.That(first.Count, Is.GreaterThan(0));
            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++) Assert.That(first[i].Cell, Is.EqualTo(second[i].Cell));
            Assert.That(repeat.BlotsCleared, Is.EqualTo(comparison.First.BlotsCleared));
            Assert.That(repeat.UnresolvedBlots, Is.EqualTo(comparison.First.UnresolvedBlots));
            Assert.That(repeat.DurationSeconds, Is.EqualTo(comparison.First.DurationSeconds));
        }

        [TestCase(TrialScenario.Crowd)]
        [TestCase(TrialScenario.Fast)]
        [TestCase(TrialScenario.Armored)]
        public void ExistingTrialLanesKeepTheirExplicitlyBlotFreeFixtures(TrialScenario scenario)
        {
            TrialReport report = TrialRunner.Run(Gun, GameBalance.CreateDefault(), scenario);
            Assert.That(report.BlotsCleared, Is.Zero);
            Assert.That(report.UnresolvedBlots, Is.Zero);
            foreach (TrialFrame frame in report.Frames) Assert.That(frame.Blots, Is.Empty);
        }

        [Test]
        public void FrameCopiesMarkValuesInsteadOfRetainingMutableSourceStates()
        {
            var marks = new List<BlotState> { new BlotState { Cell = new Cell(4, 6), Remaining = 5 } };
            var frame = new TrialFrame(0, Array.Empty<EnemyState>(), Array.Empty<StructureState>(),
                Array.Empty<MortarProjectile>(), Array.Empty<ShotEvent>(), marks);
            marks[0].Cell = new Cell(9, 9); marks[0].Remaining = 0; marks.Clear();
            Assert.That(frame.Blots.Count, Is.EqualTo(1));
            Assert.That(frame.Blots[0].Cell, Is.EqualTo(new Cell(4, 6)));
            Assert.That(frame.Blots[0].Remaining, Is.EqualTo(5));
            var list = (IList<BlotView>)frame.Blots;
            Assert.That(list.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => list[0] = default);
        }
    }
}
