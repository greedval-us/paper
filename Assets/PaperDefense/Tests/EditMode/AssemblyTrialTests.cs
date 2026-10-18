using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class AssemblyTrialTests
    {
        static Type Runner
        {
            get
            {
                var type = typeof(TrialRunner).Assembly.GetType("PaperDefense.Core.AssemblyTrialRunner");
                Assert.That(type, Is.Not.Null, "The shared assembly trial runner is missing.");
                return type;
            }
        }
        static object Run(AlbumEntry entry, GameBalance balance, string scenario, int budget = 400, int seed = 1337)
        {
            var type = Runner;
            var enumType = type.Assembly.GetType("PaperDefense.Core.AssemblyTrialScenario");
            Assert.That(enumType, Is.Not.Null);
            var method = type.GetMethod("Run", new[] { typeof(AlbumEntry), typeof(GameBalance), enumType, typeof(int), typeof(int) });
            Assert.That(method, Is.Not.Null);
            try { return method.Invoke(null, new[] { entry, balance, Enum.Parse(enumType, scenario), budget, seed }); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
        }
        static T Read<T>(object report, string name)
        {
            Assert.That(report, Is.Not.Null);
            var property = report.GetType().GetProperty(name);
            var field = report.GetType().GetField(name);
            Assert.That(property != null || field != null, Is.True, "Missing report member " + name);
            return (T)(property != null ? property.GetValue(report) : field.GetValue(report));
        }
        static AlbumEntry Starter(int index) => AssemblyResolverFixture.Catalog().Starters[index];
        static AlbumEntry Tower(string core)
        {
            var catalog = AssemblyResolverFixture.Catalog();
            var recipe = AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(core));
            var snapshot = new RecipeSnapshot(recipe, catalog.BalanceRevision,
                catalog.Definitions.Where(d => recipe.Parts.Any(p => p.Id == d.Id && p.Revision == d.Revision)));
            return new AlbumEntry("trial." + core, "Проба " + core, 1, snapshot);
        }
        static GameBalance Rules(int ticks = 90)
        {
            var balance = GameBalance.CreateDefault();
            balance.EnableEnemyMechanics = true;
            balance.Trials.MaximumTicks = ticks;
            balance.Trials.CloseTowerX = 20;
            return balance;
        }
        static string[] ShotSignature(TrialReport report) => report.Frames.SelectMany(f => f.Shots)
            .Select(s => string.Join("|", s.SourceStructureId, s.MountSlotIndex, s.ActionId,
                s.WeaponId, s.SimulationSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                s.From.X.ToString("R"), s.From.Y.ToString("R"), s.To.X.ToString("R"), s.To.Y.ToString("R"))).ToArray();

        [TestCase(0, 40, 10, 400)]
        [TestCase(1, 64, 6, 384)]
        [TestCase(4, 16, 25, 400)]
        [TestCase(5, 22, 18, 396)]
        [TestCase(6, 65, 6, 390)]
        [TestCase(7, 60, 2, 120)]
        public void BudgetIncludesOnlyWholeLegalCopies(int index, int price, int copies, int cost)
        {
            string scenario = index == 6 ? "Repair" : index == 7 ? "Generator" : "Crowd";
            var report = Run(Starter(index), Rules(), scenario);
            Assert.That(Read<int>(report, "Budget"), Is.EqualTo(400));
            Assert.That(Read<int>(report, "UnitCost"), Is.EqualTo(price));
            Assert.That(Read<int>(report, "Copies"), Is.EqualTo(copies));
            Assert.That(Read<int>(report, "Cost"), Is.EqualTo(cost));
            Assert.That(Read<TrialReport>(report, "Recording").Copies, Is.EqualTo(copies));
        }

        [Test]
        public void TrialResolutionMatchesPreviewAndBuiltEntityWithoutChangingBalance()
        {
            var entry = Starter(0); var balance = Rules();
            balance.StartingInk = 400; balance.PlanningSeconds = 3600;
            var preview = TowerCatalog.ResolveEntry(entry, balance).Value;
            var session = new GameSession(balance, starterDefenses: false);
            Assert.That(session.TryBuildEntry(new[] { new Cell(4, 4) }, preview,
                MaterialTier.Pencil, InkColor.None, out string reason), Is.True, reason);
            var report = Run(entry, balance, "Crowd", 40);
            var actual = Read<ResolvedEntry>(report, "Resolved");
            var built = session.StructureAt(new Cell(4, 4)).ResolvedEntry;
            foreach (var value in new[] { actual, built, preview })
            {
                Assert.That(value.Cost, Is.EqualTo(40));
                Assert.That(value.Health, Is.EqualTo(55));
                Assert.That(value.Weapons[0].Damage, Is.EqualTo(12));
                Assert.That(value.Weapons[0].MagazineSize, Is.EqualTo(6));
                Assert.That(value.Weapons[0].Interval, Is.EqualTo(.42f).Within(.0001f));
            }
            Assert.That(balance.StartingInk, Is.EqualTo(400));
            Assert.That(balance.PlanningSeconds, Is.EqualTo(3600));
            Assert.That(balance.Waves[0].Count, Is.EqualTo(80));
        }

        [Test]
        public void SameSeedAndEntryPreserveAllMeasuredValuesAndRecordedShots()
        {
            var entry = Starter(1); var balance = Rules(300);
            var first = Run(entry, balance, "Crowd", seed: 2026);
            var second = Run(entry, balance, "Crowd", seed: 2026);
            Assert.That(Read<int>(first, "Seed"), Is.EqualTo(2026));
            Assert.That(Read<float?>(first, "Damage"), Is.GreaterThan(0));
            Assert.That(Read<float?>(first, "Damage"), Is.EqualTo(Read<float?>(second, "Damage")));
            Assert.That(Read<int?>(first, "Leaks"), Is.EqualTo(Read<int?>(second, "Leaks")));
            Assert.That(Read<float>(first, "DurationSeconds"), Is.EqualTo(Read<float>(second, "DurationSeconds")));
            Assert.That(ShotSignature(Read<TrialReport>(first, "Recording")),
                Is.EqualTo(ShotSignature(Read<TrialReport>(second, "Recording"))));
        }

        [Test]
        public void TowerUsesTheEstablishedProductionFiringLaneWithoutSyntheticDps()
        {
            var entry = Starter(0); var balance = Rules(300);
            var expected = TrialRunner.Run(entry, balance, TrialScenario.Crowd, 40, 4242);
            var actual = Run(entry, balance, "Crowd", 40, 4242);
            Assert.That(Read<float?>(actual, "Damage"), Is.EqualTo(expected.Damage));
            Assert.That(Read<int?>(actual, "Leaks"), Is.EqualTo(expected.Leaks));
            Assert.That(ShotSignature(Read<TrialReport>(actual, "Recording")), Is.EqualTo(ShotSignature(expected)));
            Assert.That(ShotSignature(expected), Is.Not.Empty);
        }

        [Test]
        public void RecordingUsesProductionMagazineSequenceForTheSameStationaryTarget()
        {
            var entry = Starter(0); var balance = Rules(60);
            balance.Trials.CloseTowerX = 10; balance.Trials.CloseTowerY = 10;
            balance.Trials.SpawnX = 12.5f; balance.Trials.SpawnY = 10.5f; balance.Trials.SpawnYVariation = 0;
            var fixture = balance.Trials.For(TrialScenario.Crowd);
            fixture.Count = 1; fixture.Speed = .001f; fixture.Health = 100000;
            var report = Run(entry, balance, "Crowd", 40);
            var resolved = TowerCatalog.ResolveEntry(entry, balance).Value;
            var tower = new StructureState(resolved) { Id = 1, Cell = new Cell(10, 10),
                IsComplete = true, BuildRemaining = 0, Aim = new Point2(11.5f, 10.5f) };
            var enemy = new EnemyState { Id = 1, Kind = EnemyKind.Doodle, Position = new Point2(12.5f, 10.5f),
                Health = 100000, MaxHealth = 100000, DrawingTier = DrawingTier.Sketch };
            var weapons = new WeaponSystem(balance); var shots = new List<ShotEvent>();
            var events = new SimulationEventBuffer();
            var allShots = new List<ShotEvent>(); var stats = new RunStatistics();
            for (int tick = 0; tick < 60; tick++)
            {
                enemy.Position = new Point2(enemy.Position.X + .001f * balance.Trials.StepSeconds, enemy.Position.Y);
                shots.Clear(); weapons.Fire(new List<StructureState> { tower }, new List<EnemyState> { enemy }, shots, 1, 1,
                    balance.Trials.StepSeconds, tick + 1, stats, events, (tick + 1) * balance.Trials.StepSeconds);
                allShots.AddRange(shots);
            }
            var recorded = Read<TrialReport>(report, "Recording").Frames.SelectMany(f => f.Shots).ToArray();
            Assert.That(recorded.Length, Is.EqualTo(allShots.Count));
            Assert.That(recorded.Length, Is.GreaterThan(1));
            Assert.That(recorded.Select(s => s.SimulationSeconds), Is.EqualTo(allShots.Select(s => s.SimulationSeconds)).Within(.000001));
            Assert.That(recorded.Select(s => s.ActionId), Is.EqualTo(allShots.Select(s => s.ActionId)));
            Assert.That(Read<float?>(report, "Damage"), Is.EqualTo(stats.TotalDamage));
        }

        [TestCase("Single")]
        [TestCase("Line")]
        [TestCase("Crowd")]
        [TestCase("Armored")]
        public void EveryTargetArrangementHasRecordedLiveTargetsAndMeasuredDamage(string scenario)
        {
            var report = Run(Starter(0), Rules(120), scenario, 40);
            Assert.That(Read<TrialReport>(report, "Recording").Frames.Any(f => f.Enemies.Count > 0), Is.True);
            Assert.That(Read<float?>(report, "Damage"), Is.GreaterThan(0));
            Assert.That(Read<float>(report, "DurationSeconds"), Is.GreaterThan(0));
        }

        [Test]
        public void RepairMeasuresHealthActuallyRestoredByProductionSupport()
        {
            var report = Run(Starter(6), Rules(30), "Repair", 65);
            Assert.That(Read<int>(report, "Copies"), Is.EqualTo(1));
            Assert.That(Read<float?>(report, "RepairedHealth"), Is.EqualTo(6).Within(.005f));
            Assert.That(Read<float?>(report, "Damage"), Is.Null);
            Assert.That(Read<int?>(report, "Income"), Is.Null);
            var frames = Read<TrialReport>(report, "Recording").Frames;
            var initial = frames[0].Towers.Single(t => t.Kind == StructureKind.Wall);
            var final = frames[frames.Count - 1].Towers.Single(t => t.Id == initial.Id);
            Assert.That(final.Health - initial.Health, Is.EqualTo(6).Within(.005f));
            Assert.That(frames.SelectMany(f => f.Shots), Is.Empty);
        }

        [Test]
        public void OverlappingRepairPostsDoNotMultiplyOneTargetsHealing()
        {
            var report = Run(Starter(6), Rules(30), "Repair", 130);
            Assert.That(Read<int>(report, "Copies"), Is.EqualTo(2));
            Assert.That(Read<float?>(report, "RepairedHealth"), Is.EqualTo(6).Within(.005f));
        }

        [Test]
        public void GeneratorReportsIncomeOnlyForEightCompletedLocalWavesAndHonorsTwoInstanceLimit()
        {
            var report = Run(Starter(7), Rules(), "Generator", 400);
            Assert.That(Read<int>(report, "Copies"), Is.EqualTo(2));
            Assert.That(Read<int>(report, "Cost"), Is.EqualTo(120));
            Assert.That(Read<int?>(report, "CompletedWaves"), Is.EqualTo(8));
            Assert.That(Read<int?>(report, "PaidWaves"), Is.EqualTo(8));
            Assert.That(Read<int?>(report, "Income"), Is.EqualTo(192));
            Assert.That(Read<float?>(report, "Damage"), Is.Null);
            var final = Read<TrialReport>(report, "Recording").Frames.Last();
            Assert.That(final.Towers.Select(t => t.GeneratorPaidWaves), Is.All.EqualTo(8));
            Assert.That(final.Towers.Count, Is.EqualTo(2));
        }

        [Test]
        public void UnaffordableGeneratorDoesNotInventIncomeOrPaidWaves()
        {
            var report = Run(Starter(7), Rules(), "Generator", 59);
            Assert.That(Read<int>(report, "Copies"), Is.Zero);
            Assert.That(Read<int?>(report, "Income"), Is.Zero);
            Assert.That(Read<int?>(report, "PaidWaves"), Is.Zero);
            Assert.That(Read<int?>(report, "CompletedWaves"), Is.EqualTo(8));
        }

        [Test]
        public void BlotterReportsRealCleaningWithoutAnInventedDamageMetric()
        {
            var report = Run(Tower("blotter"), Rules(300), "Blots", 400);
            Assert.That(Read<int?>(report, "BlotsCleared"), Is.GreaterThan(0));
            Assert.That(Read<float?>(report, "Damage"), Is.Null);
            Assert.That(Read<TrialReport>(report, "Recording").Damage, Is.Zero);
            Assert.That(Read<TrialReport>(report, "Recording").Frames.SelectMany(f => f.Events)
                .Count(e => e.Kind == SimulationEventKind.BlotCleared), Is.GreaterThan(0));
        }

        [Test]
        public void SlowMeasuresProductionMovementControlWithoutPretendingToShoot()
        {
            var report = Run(Starter(5), Rules(120), "Crowd", 22);
            Assert.That(Read<float?>(report, "PreventedMovement"), Is.GreaterThan(0));
            Assert.That(Read<float?>(report, "Damage"), Is.Null);
            Assert.That(Read<TrialReport>(report, "Recording").Frames.SelectMany(f => f.Shots), Is.Empty);
        }

        [TestCase(1)]
        [TestCase(3)]
        public void SupportRespectsTheConfiguredCrowdLimitInItsPreparedTargets(int maximumPerCell)
        {
            var balance = Rules(120); balance.MaxEnemiesPerCell = maximumPerCell;
            var report = Run(Starter(5), balance, "Crowd", 22);
            var initial = Read<TrialReport>(report, "Recording").Frames[0];
            Assert.That(initial.Enemies.Count, Is.EqualTo(240));
            Assert.That(initial.Enemies.GroupBy(e => new Cell((int)e.Position.X, (int)e.Position.Y))
                .All(group => group.Count() <= maximumPerCell), Is.True);
            Assert.That(balance.MaxEnemiesPerCell, Is.EqualTo(maximumPerCell));
            Assert.That(Read<int>(report, "Copies"), Is.EqualTo(1));
        }

        [TestCase(-1)]
        [TestCase(20001)]
        public void InvalidBudgetIsRejectedBeforeSimulation(int budget)
        {
            _ = Runner;
            Assert.Throws<ArgumentOutOfRangeException>(() => Run(Starter(0), Rules(), "Crowd", budget));
        }

        [Test]
        public void UnknownScenarioIsRejectedBeforeSimulation()
        {
            _ = Runner;
            Assert.Throws<ArgumentOutOfRangeException>(() => Run(Starter(0), Rules(), "999"));
        }

        [TestCase(0, "Repair")]
        [TestCase(6, "Generator")]
        [TestCase(7, "Crowd")]
        public void ScenarioMustApplyToTheEntry(int index, string scenario)
        {
            _ = Runner;
            Assert.Throws<ArgumentException>(() => Run(Starter(index), Rules(), scenario));
        }

        [Test]
        public void NullEntryAndBalanceAreRejected()
        {
            _ = Runner;
            Assert.Throws<ArgumentNullException>(() => Run(null, Rules(), "Crowd"));
            Assert.Throws<ArgumentNullException>(() => Run(Starter(0), null, "Crowd"));
        }

        [Test]
        public void OverloadedEntryDoesNotRun()
        {
            _ = Runner;
            var catalog = AssemblyResolverFixture.Catalog();
            var recipe = AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon("mortar"),
                AssemblyResolverFixture.Weapon("mortar"));
            var entry = new AlbumEntry("trial.overload", "Перегрузка", 1,
                new RecipeSnapshot(recipe, catalog.BalanceRevision,
                    catalog.Definitions.Where(d => recipe.Parts.Any(p => p.Id == d.Id && p.Revision == d.Revision))));
            Assert.Throws<ArgumentException>(() => Run(entry, Rules(), "Crowd"));
        }
    }
}
