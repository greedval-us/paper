using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class TrialTests
    {
        static TowerBlueprint Gun => new TowerBlueprint("gun", "Пулемёт", ChassisKind.Standard, StructureKind.Gun);
        [Test]
        public void ComparisonUsesSameBudgetAndSeedAndChargesEveryCopy()
        {
            var dual = new TowerBlueprint("dual", "Два", ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun);
            var result = TrialRunner.Compare(Gun, dual, TrialScenario.Crowd, 400, 42);
            Assert.That(result.First.Budget, Is.EqualTo(result.Second.Budget));
            Assert.That(result.First.Seed, Is.EqualTo(result.Second.Seed));
            Assert.That(result.First.Copies, Is.EqualTo(10));
            Assert.That(result.Second.Copies, Is.EqualTo(6));
            Assert.That(result.First.Cost, Is.EqualTo(400));
            Assert.That(result.Second.Cost, Is.EqualTo(384));
            Assert.That(result.First.Damage, Is.GreaterThan(0));
            Assert.That(result.Second.Damage, Is.GreaterThan(0));
        }

        [Test]
        public void RepeatedTrialIsDeterministicAndDoesNotModifyPermanentProfile()
        {
            var profile = new WorkshopProfile(); profile.GrantGold(50);
            var first = TrialRunner.Run(Gun, TrialScenario.Fast, 160, 1337);
            var second = TrialRunner.Run(Gun, TrialScenario.Fast, 160, 1337);
            Assert.That(first.Damage, Is.EqualTo(second.Damage));
            Assert.That(first.Leaks, Is.EqualTo(second.Leaks));
            Assert.That(first.SurvivingTowers, Is.EqualTo(second.SurvivingTowers));
            Assert.That(profile.Gold, Is.EqualTo(50));
        }

        [Test]
        public void OptionalCounterfireCanDestroyUnprotectedTowers()
        {
            var safe = TrialRunner.Run(Gun, TrialScenario.Armored, 80, counterfire: false);
            var exposed = TrialRunner.Run(Gun, TrialScenario.Armored, 80, counterfire: true);
            Assert.That(safe.SurvivingTowers, Is.EqualTo(safe.Copies));
            Assert.That(exposed.SurvivingTowers, Is.LessThan(safe.SurvivingTowers));
        }

        [Test]
        public void InsufficientBudgetReportsZeroCopiesAndRealLeaks()
        {
            var report = TrialRunner.Run(Gun, TrialScenario.Crowd, 39);
            Assert.That(report.Copies, Is.Zero);
            Assert.That(report.Cost, Is.Zero);
            Assert.That(report.Damage, Is.Zero);
            Assert.That(report.Leaks, Is.GreaterThan(0));
        }

        [Test]
        public void RecordedFramesAreBoundedAndIncludeExactInitialAndFinalState()
        {
            var report = TrialRunner.Run(Gun, TrialScenario.Armored, 80, counterfire: true);
            Assert.That(report.Frames.Count, Is.InRange(2, 181));
            Assert.That(report.Frames[0].Seconds, Is.Zero);
            Assert.That(report.Frames[0].Enemies.Count, Is.Zero);
            Assert.That(report.Frames[0].Towers.Count, Is.EqualTo(report.Copies));
            TrialFrame last = report.Frames[report.Frames.Count - 1];
            Assert.That(last.Seconds, Is.EqualTo(report.DurationSeconds));
            int survivors = 0;
            foreach (StructureView tower in last.Towers) if (tower.Health > 0) survivors++;
            Assert.That(survivors, Is.EqualTo(report.SurvivingTowers));
            for (int i = 1; i < report.Frames.Count; i++)
            {
                Assert.That(report.Frames[i].Seconds, Is.GreaterThan(report.Frames[i - 1].Seconds));
                Assert.That(report.Frames[i].Seconds - report.Frames[i - 1].Seconds, Is.LessThanOrEqualTo(.33334f));
                Assert.That(report.Frames[i].Enemies.Count, Is.LessThanOrEqualTo(240));
            }
        }

        [Test]
        public void RecordedCollectionsAndFrameSnapshotsCannotBeMutated()
        {
            var report = TrialRunner.Run(Gun, TrialScenario.Fast, 40);
            var frames = (IList<TrialFrame>)report.Frames;
            Assert.Throws<NotSupportedException>(() => frames[0] = frames[1]);
            var towers = (IList<StructureView>)frames[0].Towers;
            Assert.Throws<NotSupportedException>(() => towers[0] = default);
            var enemies = (IList<EnemyView>)frames[1].Enemies;
            Assert.Throws<NotSupportedException>(() => enemies[0] = default);
            foreach (var property in typeof(TrialFrame).GetProperties())
                Assert.That(property.GetSetMethod(true), Is.Null, property.Name);
            EnemyView first = frames[1].Enemies[0];
            EnemyView later = frames[2].Enemies[0];
            Assert.That(later.Id, Is.EqualTo(first.Id));
            Assert.That(later.Position.X, Is.GreaterThan(first.Position.X));
            Assert.That(frames[1].Enemies[0].Position.X, Is.EqualTo(first.Position.X));
        }

        [Test]
        public void RecordedFramesContainActualShotsAndFlyingMortarShells()
        {
            var gun = TrialRunner.Run(Gun, TrialScenario.Crowd, 40);
            bool shot = false;
            foreach (TrialFrame frame in gun.Frames) if (frame.Shots.Count > 0) shot = true;
            Assert.That(shot, Is.True);
            var mortar = new TowerBlueprint("mortar", "Миномёт", ChassisKind.Standard, StructureKind.Mortar);
            var shells = TrialRunner.Run(mortar, TrialScenario.Crowd, 125);
            bool airborne = false;
            foreach (TrialFrame frame in shells.Frames)
                if (frame.Projectiles.Count > 0) { airborne = true; Assert.That(frame.Projectiles[0].Remaining, Is.GreaterThan(0)); }
            Assert.That(airborne, Is.True);
        }
    }
}
