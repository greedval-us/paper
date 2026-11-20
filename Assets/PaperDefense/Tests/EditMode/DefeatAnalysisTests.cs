using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class DefeatAnalysisTests
    {
        static Type Analyzer()
        {
            var type = typeof(GameSession).Assembly.GetType("PaperDefense.Core.DefeatAnalysis");
            Assert.That(type, Is.Not.Null, "Missing pure defeat-analysis API.");
            return type;
        }
        static GameSession Session()
        {
            Analyzer(); // Keep RED compilable before both telemetry and analysis types exist.
            return new GameSession(GameBalance.CreateDefault(), starterDefenses: false);
        }
        static object Analyze(GameSession session)
        {
            var method = Analyzer().GetMethod("Analyze", new[] { typeof(IRunReadModel) });
            Assert.That(method, Is.Not.Null, "DefeatAnalysis must consume the read-only run contract.");
            return method.Invoke(null, new object[] { new RunReadModel(session) });
        }
        static T Value<T>(object source, string name)
        {
            var property = source.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, source.GetType().Name + "." + name);
            return (T)property.GetValue(source);
        }
        static object[] Positions(object report) => Value<IEnumerable>(report, "IdlePositions").Cast<object>().ToArray();
        static void Record(GameSession session, string method, params object[] arguments)
        {
            var hook = typeof(RunStatistics).GetMethods().SingleOrDefault(item => item.Name == method && item.GetParameters().Length == arguments.Length);
            Assert.That(hook, Is.Not.Null, "Missing measured telemetry hook: " + method);
            hook.Invoke(session.Statistics, arguments);
        }
        static void Damage(GameSession session, float amount, int wave, Point2? source = null)
            => Record(session, "RecordBaseDamage", amount, wave, source);
        static void Loss(GameSession session, int wave, int investment)
            => Record(session, "RecordLoss", new Cell(4, 4), 1L, 1f, StructureKind.Wall,
                MaterialTier.Pencil, investment, "structure.wall", wave);
        static void Active(GameSession session, int id, Cell cell, float seconds)
            => Record(session, "RecordPositionActive", id, cell, "weapon.gun", seconds);
        static void Launch(GameSession session, int id, Cell cell)
            => Record(session, "RecordPositionLaunch", id, cell, "weapon.gun");
        static void Remove(GameSession session, int id, bool destroyed)
            => Record(session, "RecordPositionRemoved", id, destroyed);

        [Test] public void EmptyStatisticsHaveNoInventedDamageWaveApproachOrAdvice()
        {
            var session = Session(); var report = Analyze(session);
            Assert.That(Value<Guid>(report, "RunId"), Is.EqualTo(session.RunId));
            Assert.That(Value<bool>(report, "HasEvidence"), Is.False);
            Assert.That(Value<bool>(report, "HasRecordedDamage"), Is.False);
            Assert.That(Value<float>(report, "TotalBaseDamage"), Is.Zero);
            Assert.That(Value<object>(report, "WorstWave"), Is.Null);
            Assert.That(Value<object>(report, "DangerousApproach"), Is.Null);
            Assert.That(Positions(report), Is.Empty);
            Assert.That(Value<object>(report, "Advice").ToString(), Is.EqualTo("None"));
        }

        [Test] public void WorstWaveUsesActualCastleDamageRatherThanInvestmentOrTheLastWave()
        {
            var session = Session();
            Damage(session, 9, 2); Damage(session, 3, 3);
            Loss(session, 3, 500); Loss(session, 4, 1000);
            var report = Analyze(session); var worst = Value<object>(report, "WorstWave");
            Assert.That(Value<int>(worst, "WaveIndex"), Is.EqualTo(2));
            Assert.That(Value<float>(worst, "BaseDamage"), Is.EqualTo(9));
            Assert.That(Value<float>(report, "TotalBaseDamage"), Is.EqualTo(12));
            Assert.That(Value<bool>(report, "HasRecordedDamage"), Is.True);
        }

        [Test] public void StructureLossesAloneDoNotInventACastleDamageWave()
        {
            var session = Session(); Loss(session, 3, 500);
            var report = Analyze(session);
            Assert.That(Value<object>(report, "WorstWave"), Is.Null);
            Assert.That(Value<object>(report, "DangerousApproach"), Is.Null);
            Assert.That(Value<bool>(report, "HasRecordedDamage"), Is.False);
            Assert.That(Value<object>(report, "Advice").ToString(), Is.EqualTo("None"));
        }

        [Test] public void EqualDamageWavesUseLossCountsInvestmentThenEarlierWaveDeterministically()
        {
            var session = Session();
            foreach (int wave in new[] { 4, 3, 2, 1 }) Damage(session, 10, wave);
            Loss(session, 1, 100);
            Loss(session, 2, 5); Loss(session, 2, 5);
            Loss(session, 3, 10); Loss(session, 3, 10);
            Loss(session, 4, 10); Loss(session, 4, 10);
            var worst = Value<object>(Analyze(session), "WorstWave");
            Assert.That(Value<int>(worst, "WaveIndex"), Is.EqualTo(3));
            Assert.That(Value<int>(worst, "DestroyedStructures"), Is.EqualTo(2));
            Assert.That(Value<int>(worst, "LostInvestment"), Is.EqualTo(20));
        }

        [Test] public void ApproachRankingUsesDamageHitsThenStableCellOrderRegardlessOfInsertion()
        {
            var observations = new[] {
                (new Point2(4.9f, 8.2f), 5f), (new Point2(4.1f, 8.8f), 5f),
                (new Point2(2.9f, 3.2f), 5f), (new Point2(2.1f, 3.8f), 5f),
                (new Point2(1.2f, 3.2f), 10f),
                (new Point2(.9f, 3.2f), 5f), (new Point2(.1f, 3.8f), 5f)
            };
            foreach (bool reverse in new[] { false, true })
            {
                var session = Session();
                foreach (var observation in reverse ? observations.Reverse() : observations)
                    Damage(session, observation.Item2, 1, observation.Item1);
                var approach = Value<object>(Analyze(session), "DangerousApproach");
                Assert.That(Value<Cell>(approach, "Cell"), Is.EqualTo(new Cell(0, 3)));
                Assert.That(Value<float>(approach, "BaseDamage"), Is.EqualTo(10));
                Assert.That(Value<int>(approach, "Hits"), Is.EqualTo(2));
            }
        }

        [Test] public void UnknownSourceDamageStaysUnknownAndDoesNotInventAnAttackPoint()
        {
            var session = Session(); Damage(session, 12, 1);
            var report = Analyze(session);
            Assert.That(Value<float>(report, "TotalBaseDamage"), Is.EqualTo(12));
            Assert.That(Value<float>(report, "UnattributedBaseDamage"), Is.EqualTo(12));
            Assert.That(Value<int>(report, "UnattributedBaseHits"), Is.EqualTo(1));
            Assert.That(Value<object>(report, "DangerousApproach"), Is.Null);
            Assert.That(Value<object>(report, "Advice").ToString(), Is.EqualTo("ReviewLossWave"));
        }

        [Test] public void OnlySurvivingUnremovedPositionsWithFifteenSecondsAndNoLaunchesQualify()
        {
            var session = Session();
            Active(session, 1, new Cell(1, 1), 14.99f);
            Active(session, 2, new Cell(2, 1), 20); Launch(session, 2, new Cell(2, 1));
            Active(session, 3, new Cell(3, 1), 100); Remove(session, 3, false);
            Active(session, 4, new Cell(4, 1), 100); Remove(session, 4, true);
            Active(session, 5, new Cell(5, 1), 15);
            var report = Analyze(session); var positions = Positions(report);
            Assert.That(positions.Length, Is.EqualTo(1));
            Assert.That(Value<int>(positions[0], "StructureId"), Is.EqualTo(5));
            Assert.That(Value<float>(positions[0], "ActiveSeconds"), Is.EqualTo(15));
            Assert.That(Value<bool>(report, "HasEvidence"), Is.True);
            Assert.That(Value<object>(report, "Advice").ToString(), Is.EqualTo("RepositionUnfiredTower"));
        }

        [Test] public void IdleRankingHasAtMostThreeDistinctCellsAndStableExposureCellIdTies()
        {
            var records = new[] { (9, new Cell(1, 2), 50f), (4, new Cell(1, 2), 50f),
                (5, new Cell(3, 2), 50f), (6, new Cell(3, 3), 50f), (7, new Cell(0, 0), 20f) };
            foreach (bool reverse in new[] { false, true })
            {
                var session = Session();
                foreach (var record in reverse ? records.Reverse() : records) Active(session, record.Item1, record.Item2, record.Item3);
                var positions = Positions(Analyze(session));
                Assert.That(positions.Select(item => Value<int>(item, "StructureId")), Is.EqualTo(new[] { 4, 5, 6 }));
                Assert.That(positions.Select(item => Value<Cell>(item, "Cell")).Distinct().Count(), Is.EqualTo(3));
            }
        }

        [Test] public void ReportIsAnImmutableRunSnapshotAndAdvicePrefersAnObservedAttackPoint()
        {
            var session = Session(); Damage(session, 5, 1, new Point2(2.2f, 4.2f));
            Active(session, 1, new Cell(6, 6), 20);
            var report = Analyze(session);
            Assert.That(Value<object>(report, "Advice").ToString(), Is.EqualTo("DefendObservedApproach"));
            Damage(session, 40, 2, new Point2(7.2f, 8.2f)); Launch(session, 1, new Cell(6, 6));
            Assert.That(Value<float>(report, "TotalBaseDamage"), Is.EqualTo(5));
            Assert.That(Value<int>(Value<object>(report, "WorstWave"), "WaveIndex"), Is.EqualTo(1));
            Assert.That(Value<int>(Positions(report).Single(), "Launches"), Is.Zero);
            var list = Value<IEnumerable>(report, "IdlePositions") as IList;
            Assert.That(list, Is.Not.Null);
            Assert.Throws<NotSupportedException>(() => list.Clear());
            foreach (var property in report.GetType().GetProperties())
                Assert.That(property.SetMethod == null || !property.SetMethod.IsPublic, Is.True, property.Name);
            var current = Analyze(session);
            Assert.That(Value<float>(current, "TotalBaseDamage"), Is.EqualTo(45));
            Assert.That(Positions(current), Is.Empty);
            var next = Session(); var fresh = Analyze(next);
            Assert.That(Value<Guid>(fresh, "RunId"), Is.Not.EqualTo(Value<Guid>(report, "RunId")));
            Assert.That(Value<bool>(fresh, "HasEvidence"), Is.False);
        }

        [Test] public void TruncatedTelemetryRetainsTheExactTotalAndExposesItsLimit()
        {
            var session = Session();
            for (int i = 0; i < 513; i++) Damage(session, 1, 1, new Point2(i % 40 + .5f, i / 40 + .5f));
            var report = Analyze(session);
            Assert.That(Value<float>(report, "TotalBaseDamage"), Is.EqualTo(513));
            Assert.That(Value<bool>(report, "HasRecordedDamage"), Is.True);
            Assert.That(Value<bool>(report, "TelemetryTruncated"), Is.True);
        }
    }
}
