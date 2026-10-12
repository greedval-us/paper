using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class MountReadinessTests
    {
        sealed class Fixture
        {
            internal readonly GameBalance Balance = GameBalance.CreateDefault();
            internal readonly StructureState Tower;
            internal readonly List<EnemyState> Enemies = new List<EnemyState>();
            internal readonly RunStatistics Stats = new RunStatistics();
            readonly WeaponSystem weapons;
            readonly List<StructureState> structures;
            readonly List<ShotEvent> shots = new List<ShotEvent>();
            readonly SimulationEventBuffer events = new SimulationEventBuffer();
            long tick;

            internal Fixture(bool dynamic = true, StructureKind? second = null, TowerModules modules = TowerModules.None)
            {
                Balance.Construction.DynamicFeed = dynamic;
                Balance.Construction.AcquisitionSeconds = .25f;
                Balance.Construction.TrackingAcquisitionSeconds = .08f;
                Balance.Structure(StructureKind.Gun).Range = 2;
                Balance.Structure(StructureKind.Mortar).Range = 5;
                Tower = new StructureState { Id = 1, Cell = new Cell(10, 10), Kind = StructureKind.Gun,
                    Health = 100, IsComplete = true, Tier = MaterialTier.Pencil, Priority = TargetPriority.Strongest,
                    Blueprint = new TowerBlueprint("readiness", "Readiness", ChassisKind.Heavy, StructureKind.Gun, second, modules) };
                structures = new List<StructureState> { Tower };
                weapons = new WeaponSystem(Balance);
                Enemies.Add(Enemy(1, 1, 100000));
            }

            internal void Step(float delta = .01f)
            {
                if (!Balance.Construction.DynamicFeed)
                {
                    Tower.Cooldown = Math.Max(0, Tower.Cooldown - delta);
                    Tower.SecondaryCooldown = Math.Max(0, Tower.SecondaryCooldown - delta);
                }
                weapons.Fire(structures, Enemies, shots, 1, 1, delta, ++tick, Stats, events);
            }
            internal void AcquireAndFire() { for (int i = 0; i < 30; i++) Step(); }
            internal void ReadyBoth()
            {
                Tower.PrimaryActionRemaining = Tower.SecondaryActionRemaining = 0;
                Tower.Cooldown = Tower.SecondaryCooldown = 0;
            }
        }

        static EnemyState Enemy(int id, float distance, float health) => new EnemyState
        { Id = id, Position = new Point2(10.5f + distance, 10.5f), Health = health, MaxHealth = health };

        [Test] public void DynamicReloadFinishesWithoutTargetsAndStoresOnlyOneReadyShot()
        {
            var f = new Fixture(); f.AcquireAndFire();
            Assert.That(f.Stats.Launches(StructureKind.Gun), Is.EqualTo(1));
            f.Enemies.Clear();
            for (int i = 0; i < 300; i++) f.Step(1f / 30);
            Assert.That(f.Tower.PrimaryActionRemaining, Is.Zero, "An existing reload continues while no target is present.");
            Assert.That(f.Tower.Cooldown, Is.Zero);
            f.Enemies.Add(Enemy(2, 1, 100000)); f.AcquireAndFire();
            Assert.That(f.Stats.Launches(StructureKind.Gun), Is.EqualTo(2));
            f.Step();
            Assert.That(f.Stats.Launches(StructureKind.Gun), Is.EqualTo(2), "Idle time cannot bank an extra volley.");
        }

        [Test] public void TwoTargetlessReloadsShareSupplyUntilReady()
        {
            var f = new Fixture(second: StructureKind.Gun); f.AcquireAndFire(); f.Enemies.Clear();
            float before = f.Tower.PrimaryActionRemaining;
            f.Step(.1f);
            Assert.That(f.Tower.PrimaryActionRemaining, Is.EqualTo(before - .1f / (.42f / .8f)).Within(.00001f));
            Assert.That(f.Tower.SecondaryActionRemaining, Is.EqualTo(f.Tower.PrimaryActionRemaining).Within(.00001f));
        }

        [Test] public void FinishedIdleReloadReleasesSupplyToTheWorkingMount()
        {
            var f = new Fixture(second: StructureKind.Mortar); f.AcquireAndFire();
            f.Balance.Structure(StructureKind.Mortar).Range = 0;
            f.Tower.PrimaryActionRemaining = 1; f.Tower.SecondaryActionRemaining = 0;
            f.Step(.1f);
            Assert.That(f.Tower.PrimaryActionRemaining, Is.EqualTo(1 - .1f / .42f).Within(.00001f));
        }

        [TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)] [TestCase(true, true)]
        public void RetargetingOneBarrelDoesNotStallTheOther(bool dynamic, bool swapped)
        {
            var f = new Fixture(dynamic, StructureKind.Mortar);
            if (swapped)
            {
                f.Tower.Blueprint = new TowerBlueprint("swapped", "Swapped mounts", ChassisKind.Heavy, StructureKind.Mortar, StructureKind.Gun);
                f.Tower.Kind = StructureKind.Mortar;
            }
            f.Enemies.Add(Enemy(2, 4, 200000)); f.AcquireAndFire(); f.ReadyBoth();
            int guns = f.Stats.Launches(StructureKind.Gun), mortars = f.Stats.Launches(StructureKind.Mortar);
            f.Enemies.Add(Enemy(3, 4.5f, 300000)); f.Step();
            Assert.That(swapped ? f.Tower.SecondaryTargetId : f.Tower.PrimaryTargetId, Is.EqualTo(1));
            Assert.That(swapped ? f.Tower.PrimaryTargetId : f.Tower.SecondaryTargetId, Is.EqualTo(3));
            Assert.That(f.Stats.Launches(StructureKind.Gun), Is.EqualTo(guns + 1));
            Assert.That(f.Stats.Launches(StructureKind.Mortar), Is.EqualTo(mortars));
        }

        [TestCase(TowerModules.None, 25)] [TestCase(TowerModules.Tracking, 8)]
        public void EveryBarrelUsesItsOwnConfiguredAcquisitionDelay(TowerModules modules, int acquisitionTicks)
        {
            var f = new Fixture(second: StructureKind.Mortar, modules: modules);
            f.Enemies.Add(Enemy(2, 4, 200000)); f.AcquireAndFire(); f.ReadyBoth();
            int mortars = f.Stats.Launches(StructureKind.Mortar);
            f.Enemies.Add(Enemy(3, 4.5f, 300000));
            for (int i = 0; i < acquisitionTicks - 1; i++) f.Step();
            Assert.That(f.Stats.Launches(StructureKind.Mortar), Is.EqualTo(mortars));
            Assert.That(f.Stats.Launches(StructureKind.Gun), Is.GreaterThan(1), "The unchanged short-range target keeps its lock.");
            f.Step();
            Assert.That(f.Stats.Launches(StructureKind.Mortar), Is.EqualTo(mortars + 1));
        }

        [Test] public void SecondaryReacquiresTheActualTargetAfterPrimaryKillsItsCandidate()
        {
            var f = new Fixture(second: StructureKind.Gun);
            f.Enemies[0].Health = 1;
            f.Enemies.Add(Enemy(2, 1.5f, .5f));
            f.Tower.Priority = TargetPriority.Nearest;
            for (int i = 0; i < 25; i++) f.Step();
            Assert.That(f.Enemies[0].Health, Is.Zero);
            Assert.That(f.Tower.SecondaryTargetId, Is.EqualTo(2));
            Assert.That(f.Stats.Launches(StructureKind.Gun), Is.EqualTo(1), "A lock on a killed target cannot authorize fire at a different target.");
            for (int i = 0; i < 25; i++) f.Step();
            Assert.That(f.Enemies[1].Health, Is.Zero);
            Assert.That(f.Stats.Launches(StructureKind.Gun), Is.EqualTo(2));
        }

        [Test] public void LiveSessionReloadsDuringIntermissionButNotWhilePaused()
        {
            var balance = GameBalance.CreateDefault(); balance.Construction.DynamicFeed = true;
            balance.Construction.AcquisitionSeconds = 0; balance.PlanningSeconds = balance.IntermissionSeconds = 1000;
            balance.Structure(StructureKind.Gun).Range = 100;
            balance.Structure(StructureKind.Gun).Damage = 100000;
            balance.Structure(StructureKind.Gun).BuildSeconds = 0;
            balance.Waves = new List<WaveDefinition> {
                new WaveDefinition { Count = 1, Sides = 1 }, new WaveDefinition { Count = 1, Sides = 1 } };
            var game = new GameSession(balance, starterDefenses: false);
            var blueprint = new TowerBlueprint("gap", "Wave gap", ChassisKind.Standard, StructureKind.Gun);
            Assert.That(game.TryBuildBlueprint(new[] { new Cell(30, 20) }, blueprint, MaterialTier.Pencil, InkColor.None, out string reason), Is.True, reason);
            game.StartNextWave(); game.Tick(.1f);
            Assert.That(game.Phase, Is.EqualTo(RunPhase.Intermission));
            var tower = game.Structures[0]; float remaining = tower.PrimaryActionRemaining;
            Assert.That(remaining, Is.GreaterThan(0));
            game.Tick(0);
            Assert.That(tower.PrimaryActionRemaining, Is.EqualTo(remaining));
            game.Tick(2);
            Assert.That(tower.PrimaryActionRemaining, Is.Zero);
            Assert.That(tower.Cooldown, Is.Zero);
            Assert.That(game.Statistics.Launches(StructureKind.Gun), Is.EqualTo(1), "Wave-gap readiness must not launch attacks.");
        }
    }
}
