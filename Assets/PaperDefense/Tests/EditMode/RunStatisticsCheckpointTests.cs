using System;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class RunStatisticsCheckpointTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Capture(RunStatistics stats)
        {
            var method = typeof(RunStatistics).GetMethod("CaptureCheckpoint", Hidden);
            Assert.That(method, Is.Not.Null, "Statistics need a checkpoint snapshot.");
            return method.Invoke(stats, null);
        }
        static void Restore(RunStatistics stats, object data)
        {
            var method = typeof(RunStatistics).GetMethod("RestoreCheckpoint", Hidden);
            Assert.That(method, Is.Not.Null, "Statistics need checkpoint restoration.");
            try { method.Invoke(stats, new[] { data }); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
        static RunStatistics Sample()
        {
            var stats = new RunStatistics();
            stats.RecordBuild(70, 2, "test.gun", MaterialTier.Pencil);
            stats.RecordDamage("test.gun", 12.5f); stats.RecordIdle("test.gun", 3.25f);
            stats.RecordLaunch("test.gun"); stats.RecordUpgrade(15); stats.RecordRefund(4);
            stats.RecordRepair(8); stats.RecordGeneratorIncome(20); stats.RecordSpawn(EnemyKind.Runner, DrawingTier.Sketch);
            stats.RecordBaseDamage(9, 2, new Point2(7.5f, 9.5f)); stats.RecordBaseDamage(2, 1, null);
            stats.RecordLoss(new Cell(8, 9), 100, 4, StructureKind.Gun, MaterialTier.Pencil, 35, "test.gun", 2);
            stats.RecordPositionActive(5, new Cell(8, 9), "test.gun", 12);
            stats.RecordPositionLaunch(5, new Cell(8, 9), "test.gun"); stats.RecordPositionRemoved(5, true);
            stats.RecordBlotsCleared(3); stats.ObserveEnemies(14); stats.RecordNavigation(0.25);
            stats.RecordUpgradeChoice(RunUpgrade.RapidFire);
            var marked = new EnemyState(); stats.RecordArmorMark(marked); stats.RecordArmorMark(marked);
            stats.RecordPrismSlow(marked);
            return stats;
        }

        [Test]
        public void RoundTripKeepsReportsAndLaterMeasurementsAccumulateOnce()
        {
            var original = Sample(); var data = Capture(original);
            string saved = JsonUtility.ToJson(data);
            var restored = new RunStatistics(); Restore(restored, JsonUtility.FromJson(saved, data.GetType()));
            Assert.That(JsonUtility.ToJson(Capture(restored)), Is.EqualTo(saved));
            restored.RecordDamage("test.gun", 2.5f); restored.RecordBaseDamage(3, 3, null);
            Assert.That(restored.Damage("test.gun"), Is.EqualTo(15));
            Assert.That(restored.BaseDamageTaken, Is.EqualTo(14));
            Assert.That(restored.Breaches.Count, Is.EqualTo(1));
            Assert.That(restored.PositionActivities[0].Destroyed, Is.True);
            Assert.That(restored.TotalEnemiesMarked, Is.EqualTo(1));
            Assert.That(restored.TotalEnemiesSlowedByPrism, Is.EqualTo(1));
            Assert.That(restored.Upgrades, Is.EqualTo(new[] { RunUpgrade.RapidFire }));
            Assert.That(original.TotalDamage, Is.EqualTo(12.5f));
        }

        [Test]
        public void TruncatedTelemetryAndRollingBreachesRemainHonestAfterResume()
        {
            var source = new RunStatistics();
            for (int i = 0; i < 520; i++)
            {
                source.RecordBaseDamage(1, 1, new Point2(i, 1));
                source.RecordPositionActive(i + 1, new Cell(i, 1), "gun", 1);
            }
            for (int i = 0; i < 40; i++) source.RecordLoss(new Cell(i, 2), i, i, StructureKind.Wall, MaterialTier.Pencil, 3, waveIndex: 1);
            var restored = new RunStatistics(); Restore(restored, Capture(source));
            Assert.That(restored.DefeatTelemetryTruncated, Is.True);
            Assert.That(restored.UnattributedBaseHits, Is.EqualTo(8));
            Assert.That(restored.ApproachLosses.Count, Is.EqualTo(512));
            Assert.That(restored.PositionActivities.Count, Is.EqualTo(512));
            Assert.That(restored.Breaches.Count, Is.EqualTo(32));
            Assert.That(restored.Breaches[0].Cell.X, Is.EqualTo(8));
            Assert.That(restored.DestroyedStructures, Is.EqualTo(40));
        }

        [Test]
        public void CorruptMeasurementIsRejectedWithoutPartiallyChangingExistingStatistics()
        {
            var source = Sample(); var data = Capture(source);
            data.GetType().GetField("TotalDamage").SetValue(data, float.NaN);
            var target = new RunStatistics(); target.RecordDamage("existing", 33);
            Assert.Throws<ArgumentException>(() => Restore(target, data));
            Assert.That(target.TotalDamage, Is.EqualTo(33));
            Assert.That(target.WeaponIds, Is.EquivalentTo(new[] { "existing" }));
        }

        [Test]
        public void InvalidLatePositionRecordCannotReplaceAnyEarlierAggregate()
        {
            var source = Sample(); source.RecordPositionActive(6, new Cell(9, 9), "test.gun", 2);
            var data = Capture(source);
            var positions = (Array)data.GetType().GetField("Positions").GetValue(data);
            var duplicate = positions.GetValue(1); duplicate.GetType().GetField("Id").SetValue(duplicate, 5);
            var target = new RunStatistics(); target.RecordDamage("existing", 33);
            target.RecordPositionActive(42, new Cell(2, 3), "existing", 4);
            Assert.Throws<ArgumentException>(() => Restore(target, data));
            Assert.That(target.TotalDamage, Is.EqualTo(33));
            Assert.That(target.PositionActivities.Count, Is.EqualTo(1));
            Assert.That(target.PositionActivities[0].StructureId, Is.EqualTo(42));
        }

        [Test]
        public void FutureStatisticsSchemaIsRejected()
        {
            var data = Capture(Sample()); data.GetType().GetField("Version").SetValue(data, 2);
            Assert.Throws<NotSupportedException>(() => Restore(new RunStatistics(), data));
        }

        [Test]
        public void CapturedArraysAndRestoredStatisticsDoNotShareMutableState()
        {
            var source = Sample(); var data = Capture(source);
            var restored = new RunStatistics(); Restore(restored, data);
            var counts = (int[])data.GetType().GetField("SpawnedByKind").GetValue(data);
            counts[(int)EnemyKind.Runner] = 999;
            Assert.That(source.SpawnedByKind[(int)EnemyKind.Runner], Is.EqualTo(1));
            Assert.That(restored.SpawnedByKind[(int)EnemyKind.Runner], Is.EqualTo(1));
        }
    }
}
