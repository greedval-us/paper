#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;

namespace PaperDefense.Tests
{
    public sealed class CompositeWeaponIntegrationTests
    {
        const float Step = .01f;
        const int TowerId = 42;
        static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        static AlbumEntry Page(bool twoMounts = false)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(
                "Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
            return catalog.Starters.First(entry => entry.Kind == AlbumEntryKind.Tower &&
                entry.Snapshot.Recipe.Tower.Mounts.Count == (twoMounts ? 2 : 1) &&
                entry.Snapshot.Recipe.Tower.Mounts.All(mount => mount.Weapon.Mechanism.Id == "core.gun"));
        }

        static GameBalance Rules(bool legacyDynamicFeed = false)
        {
            var balance = GameBalance.CreateDefault();
            balance.PassiveInkPerSecond = 0; balance.EnableEnemyMechanics = false; balance.EnableInkWells = false;
            balance.Construction.AcquisitionSeconds = 0;
            balance.Construction.DynamicFeed = legacyDynamicFeed;
            balance.SimulationStep = Step;
            balance.Trials.StepSeconds = Step; balance.Trials.MaximumTicks = 340;
            balance.Trials.CloseTowerX = 32; balance.Trials.CloseTowerY = 10;
            balance.Trials.SpawnX = 34.5f; balance.Trials.SpawnY = 10.5f; balance.Trials.SpawnYVariation = 0;
            var crowd = balance.Trials.For(TrialScenario.Crowd);
            crowd.Count = 1; crowd.Health = 1000000; crowd.Speed = .00001f; crowd.SpawnInterval = 1;
            return balance;
        }

        static GameSession Battle(AlbumEntry page, GameBalance balance, out StructureState tower)
        {
            var resolved = TowerCatalog.ResolveEntry(page, balance);
            Assert.That(resolved.Valid, Is.True, string.Join(",", resolved.Diagnostics.Select(issue => issue.Code)));
            var session = GameSession.CreatePrepared(balance, 1337,
                new[] { new PreparedEnemy(new Point2(12.5f, 10.5f), 1000000) });
            tower = new StructureState(resolved.Value) { Id = TowerId, Cell = new Cell(10, 10), IsComplete = true, BuildRemaining = 0 };
            // Task 7 owns construction commands. This bounded validation stand injects initial state,
            // then exercises the normal public session tick, targeting, events and read model.
            var structures = typeof(GameSession).GetField("structures", PrivateInstance);
            var grid = typeof(GameSession).GetField("grid", PrivateInstance);
            Assert.That(structures, Is.Not.Null); Assert.That(grid, Is.Not.Null);
            ((List<StructureState>)structures.GetValue(session)).Add(tower);
            ((StructureState[])grid.GetValue(session))[tower.Cell.Y * balance.Width + tower.Cell.X] = tower;
            return session;
        }

        static SimulationEvent[] Shots(GameSession session, int mount = 0)
            => session.EventBuffer.Where(item => item.Kind == SimulationEventKind.Shot &&
                item.SourceStructureId == TowerId && item.MountSlotIndex == mount).ToArray();
        static SimulationEvent[] Shots(TrialReport report, int mount = 0)
            => report.Frames.SelectMany(frame => frame.Events).Where(item => item.Kind == SimulationEventKind.Shot &&
                item.SourceStructureId == 1 && item.MountSlotIndex == mount).ToArray();

        [TestCase(false)] [TestCase(true)]
        public void TrialAndRealSessionUseTheSameSixRoundCycleAndTimestampEveryAction(bool legacyDynamicFeed)
        {
            var page = Page(); var balance = Rules(legacyDynamicFeed);
            var resolved = TowerCatalog.ResolveEntry(page, balance).Value;
            Assert.That(resolved.Weapons.Single().MagazineSize, Is.EqualTo(6));
            Assert.That(resolved.Weapons.Single().Interval, Is.EqualTo(.42f).Within(.00001f));
            Assert.That(resolved.Weapons.Single().ReloadSeconds, Is.EqualTo(.42f).Within(.00001f));
            var session = Battle(page, balance, out _);
            for (int tick = 0; tick < balance.Trials.MaximumTicks; tick++) session.Tick(Step);
            var trial = TrialRunner.Run(page, balance, TrialScenario.Crowd, budget: resolved.Cost);
            var liveShots = Shots(session); var trialShots = Shots(trial);
            Assert.That(liveShots.Length, Is.GreaterThanOrEqualTo(7));
            Assert.That(trialShots.Length, Is.EqualTo(liveShots.Length));
            Assert.That(liveShots[6].SimulationSeconds - liveShots[0].SimulationSeconds, Is.EqualTo(2.52).Within(.001));
            Assert.That(trialShots[6].SimulationSeconds - trialShots[0].SimulationSeconds, Is.EqualTo(2.52).Within(.001));
            for (int shot = 0; shot < liveShots.Length; shot++)
                Assert.That(trialShots[shot].SimulationSeconds - trialShots[0].SimulationSeconds,
                    Is.EqualTo(liveShots[shot].SimulationSeconds - liveShots[0].SimulationSeconds).Within(.001), "shot " + shot);
            Assert.That(trial.Copies, Is.EqualTo(1)); Assert.That(trial.Cost, Is.EqualTo(resolved.Cost));
            Assert.That(trial.BlueprintId, Is.EqualTo(page.Id));
            Assert.That(trial.Damage, Is.EqualTo(session.Statistics.TotalDamage).Within(.0001f));
            Assert.That(trialShots.Select(shot => shot.ActionId).Distinct().Count(), Is.EqualTo(trialShots.Length));
            var actions = trial.Frames.SelectMany(frame => frame.Events).Where(item => item.Kind == SimulationEventKind.WeaponActionStarted).ToArray();
            foreach (var shot in trialShots)
            {
                Assert.That(shot.ActionId, Is.GreaterThan(0)); Assert.That(shot.WeaponId, Is.EqualTo("weapon.gun"));
                var action = actions.Single(item => item.ActionId == shot.ActionId);
                Assert.That(action.SourceStructureId, Is.EqualTo(shot.SourceStructureId));
                Assert.That(action.MountSlotIndex, Is.EqualTo(shot.MountSlotIndex));
                Assert.That(action.SimulationSeconds, Is.EqualTo(shot.SimulationSeconds).Within(.000001));
            }
        }

        [Test] public void TwoCompositeMountsKeepIndependentActionIdsAndShareFeedOnlyOnce()
        {
            var page = Page(twoMounts: true); var balance = Rules();
            var session = Battle(page, balance, out _);
            for (int tick = 0; tick < balance.Trials.MaximumTicks; tick++) session.Tick(Step);
            var first = Shots(session, 0); var second = Shots(session, 1);
            Assert.That(first.Length, Is.GreaterThanOrEqualTo(7));
            Assert.That(second.Length, Is.EqualTo(first.Length));
            Assert.That(first[6].SimulationSeconds - first[0].SimulationSeconds, Is.EqualTo(3.15).Within(.001));
            Assert.That(second[6].SimulationSeconds - second[0].SimulationSeconds, Is.EqualTo(3.15).Within(.001));
            var all = first.Concat(second).ToArray();
            Assert.That(all.Select(shot => shot.ActionId).Distinct().Count(), Is.EqualTo(all.Length));
            Assert.That(first.Select(shot => shot.MountSlotIndex), Is.All.EqualTo(0));
            Assert.That(second.Select(shot => shot.MountSlotIndex), Is.All.EqualTo(1));
        }

        [Test] public void CompositeTrialComparisonUsesTheSameOwnedPageInBothLanes()
        {
            var page = Page(); var balance = Rules();
            var comparison = TrialRunner.Compare(page, page, balance, TrialScenario.Crowd, budget: 40);
            Assert.That(comparison.First.Copies, Is.EqualTo(1)); Assert.That(comparison.Second.Copies, Is.EqualTo(1));
            Assert.That(comparison.First.Damage, Is.GreaterThan(0));
            Assert.That(comparison.First.Damage, Is.EqualTo(comparison.Second.Damage));
            Assert.That(Shots(comparison.First).Select(shot => shot.SimulationSeconds),
                Is.EqualTo(Shots(comparison.Second).Select(shot => shot.SimulationSeconds)));
        }

        [Test] public void LegacyAlbumPageKeepsTheExistingTrialCalculationAndContinuousWeaponPath()
        {
            var blueprint = new TowerBlueprint("legacy.trial", "Historical trial gun", ChassisKind.Standard, StructureKind.Gun);
            var entry = new AlbumEntry(blueprint.Id, blueprint.Name, 1, blueprint); var balance = Rules();
            var previous = TrialRunner.Run(blueprint, balance, TrialScenario.Crowd, budget: 40);
            var adapted = TrialRunner.Run(entry, balance, TrialScenario.Crowd, budget: 40);
            Assert.That(adapted.Copies, Is.EqualTo(previous.Copies));
            Assert.That(adapted.Cost, Is.EqualTo(previous.Cost));
            Assert.That(adapted.Damage, Is.EqualTo(previous.Damage));
            Assert.That(Shots(adapted).Select(shot => shot.SimulationSeconds), Is.EqualTo(Shots(previous).Select(shot => shot.SimulationSeconds)));
            Assert.That(adapted.Frames[0].Towers.Single().Blueprint, Is.Not.Null);
            Assert.That(adapted.Frames[0].Towers.Single().ResolvedEntry, Is.Null);
        }

        [Test] public void AReadViewOwnsItsAmmoAndReloadSnapshotWhileLaterTicksAdvanceTheLiveMagazine()
        {
            var session = Battle(Page(), Rules(), out _); var read = new RunReadModel(session);
            for (int tick = 0; tick < 300 && Shots(session).Length < 6; tick++) session.Tick(Step);
            Assert.That(Shots(session).Length, Is.EqualTo(6));
            var heldTower = read.Structures[0]; var heldMount = heldTower.GetMountState(0);
            Assert.That(heldMount.RoundsRemaining, Is.Zero);
            Assert.That(heldMount.IsReloading, Is.True);
            Assert.That(heldMount.CycleRemainingWork, Is.GreaterThan(0));
            double heldWork = heldMount.CycleRemainingWork;
            session.Tick(.1f);
            Assert.That(read.Structures[0].GetMountState(0).CycleRemainingWork, Is.LessThan(heldWork));
            Assert.That(heldTower.GetMountState(0).RoundsRemaining, Is.Zero);
            Assert.That(heldTower.GetMountState(0).IsReloading, Is.True);
            Assert.That(heldTower.GetMountState(0).CycleRemainingWork, Is.EqualTo(heldWork));
            for (int tick = 0; tick < 100 && Shots(session).Length < 7; tick++) session.Tick(Step);
            Assert.That(Shots(session).Length, Is.EqualTo(7));
            Assert.That(read.Structures[0].GetMountState(0).RoundsRemaining, Is.EqualTo(5));
            Assert.That(heldTower.GetMountState(0).RoundsRemaining, Is.Zero);
            Assert.That(heldTower.GetMountState(0).CycleRemainingWork, Is.EqualTo(heldWork));
        }

        [Test] public void TrialFramesRetainLoadedAndReloadingMagazineSnapshotsForPlayback()
        {
            var report = TrialRunner.Run(Page(), Rules(), TrialScenario.Crowd, budget: 40);
            var initial = report.Frames[0].Towers.Single().GetMountState(0);
            Assert.That(initial.RoundsRemaining, Is.EqualTo(6));
            Assert.That(initial.IsReloading, Is.False);
            var frame = report.Frames.First(value => value.Towers.Single().GetMountState(0).IsReloading);
            var reload = frame.Towers.Single().GetMountState(0); double work = reload.CycleRemainingWork;
            Assert.That(reload.RoundsRemaining, Is.Zero); Assert.That(work, Is.GreaterThan(0));
            Assert.That(report.Frames.Last().Towers.Single().GetMountState(0).RoundsRemaining, Is.GreaterThan(0));
            Assert.That(report.Frames[0].Towers.Single().GetMountState(0).RoundsRemaining, Is.EqualTo(6));
            Assert.That(frame.Towers.Single().GetMountState(0).RoundsRemaining, Is.Zero);
            Assert.That(frame.Towers.Single().GetMountState(0).CycleRemainingWork, Is.EqualTo(work));
        }

        [Test] public void ZeroDeltaTicksDoNotFireSpendAmmoOrAdvanceReload()
        {
            var session = Battle(Page(), Rules(), out _); var read = new RunReadModel(session);
            for (int tick = 0; tick < 300 && Shots(session).Length < 6; tick++) session.Tick(Step);
            Assert.That(Shots(session).Length, Is.EqualTo(6));
            var before = read.Structures[0].GetMountState(0);
            Assert.That(before.IsReloading, Is.True);
            int events = session.EventBuffer.Count; long tickIndex = session.TickIndex;
            float elapsed = session.Elapsed, health = read.Enemies.Single().Health;
            for (int pause = 0; pause < 20; pause++) session.Tick(0);
            var after = read.Structures[0].GetMountState(0);
            Assert.That(session.EventBuffer.Count, Is.EqualTo(events));
            Assert.That(session.TickIndex, Is.EqualTo(tickIndex)); Assert.That(session.Elapsed, Is.EqualTo(elapsed));
            Assert.That(after.RoundsRemaining, Is.EqualTo(before.RoundsRemaining));
            Assert.That(after.IsReloading, Is.EqualTo(before.IsReloading));
            Assert.That(after.CycleRemainingWork, Is.EqualTo(before.CycleRemainingWork));
            Assert.That(read.Enemies.Single().Health, Is.EqualTo(health));
        }
    }
}
#endif
