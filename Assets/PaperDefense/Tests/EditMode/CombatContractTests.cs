using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class CombatContractTests
    {
        private static GameBalance Balance()
        {
            var balance = GameBalance.CreateDefault();
            balance.Width = 20;
            balance.Height = 16;
            balance.StartingInk = 1000;
            balance.BaseHealth = 500;
            balance.PlanningSeconds = 1000;
            balance.PassiveInkPerSecond = 0;
            balance.EarlyLaunchInkPerSecond = 0;
            balance.WaveInkGrant = 0;
            balance.Waves = new List<WaveDefinition>
            {
                new WaveDefinition { Count = 1, SpawnInterval = 0.01f, Sides = 1 }
            };
            foreach (StructureKind kind in Enum.GetValues(typeof(StructureKind)))
                balance.Structure(kind).BuildSeconds = 0;
            foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
            {
                balance.Enemy(kind).Speed = 0;
                balance.Enemy(kind).MaxHealth = 100;
                balance.Enemy(kind).Damage = 0;
                balance.Enemy(kind).Reward = 7;
            }
            var mortar = balance.Structure(StructureKind.Mortar);
            mortar.Range = 20;
            mortar.Damage = 25;
            mortar.Radius = 0.4f;
            mortar.ShotInterval = 100;
            return balance;
        }

        private static GameSession SpawnTarget(GameBalance balance)
        {
            var game = new GameSession(balance, starterDefenses: false);
            game.StartNextWave();
            game.Tick(0.02f);
            game.Enemies[0].Position = new Point2(5.5f, 4.5f);
            return game;
        }

        private static void BuildMortar(GameSession game)
        {
            Assert.That(game.TryBuild(new[] { new Cell(3, 4) }, StructureKind.Mortar,
                MaterialTier.Pencil, out string reason), Is.True, reason);
        }

        [Test]
        public void MortarDoesNotDamageAtLaunch()
        {
            var game = SpawnTarget(Balance());
            BuildMortar(game);
            game.Tick(0.02f);
            Assert.That(game.Enemies[0].Health, Is.EqualTo(100));
        }

        [Test]
        public void RunnerLeavingMortarDestinationBeforeImpactTakesNoDamage()
        {
            var balance = Balance();
            balance.Enemy(EnemyKind.Runner).Speed = 4;
            var game = SpawnTarget(balance);
            game.Enemies[0].Kind = EnemyKind.Runner;
            BuildMortar(game);
            game.Tick(0.02f);
            game.Tick(1);
            Assert.That(game.Enemies[0].Health, Is.EqualTo(100));
        }

        [Test]
        public void MortarImpactsWithoutAnyPresentation()
        {
            var game = SpawnTarget(Balance());
            BuildMortar(game);
            game.Tick(0.02f);
            Assert.That(game.Enemies[0].Health, Is.EqualTo(100));
            game.Tick(0.9f);
            Assert.That(game.Enemies[0].Health, Is.EqualTo(75));
        }

        [Test]
        public void HeadlessMortarKillAwardsRewardOnlyAfterImpact()
        {
            var balance = Balance();
            balance.Structure(StructureKind.Mortar).Damage = 1000;
            var game = SpawnTarget(balance);
            BuildMortar(game);
            int inkBefore = game.Ink;
            game.Tick(0.02f);
            Assert.That(game.Kills, Is.Zero);
            Assert.That(game.Ink, Is.EqualTo(inkBefore));
            game.Tick(0.9f);
            Assert.That(game.Kills, Is.EqualTo(1));
            Assert.That(game.Ink - inkBefore, Is.EqualTo(7));
            Assert.That(game.Enemies, Is.Empty);
            Assert.That(game.Phase, Is.EqualTo(RunPhase.Victory));
        }

        [Test]
        public void ErasingSourceMortarDoesNotCancelShell()
        {
            var game = SpawnTarget(Balance());
            BuildMortar(game);
            game.Tick(0.02f);
            Assert.That(game.TryErase(new Cell(3, 4), out _), Is.True);
            Assert.That(game.Enemies[0].Health, Is.EqualTo(100));
            game.Tick(0.9f);
            Assert.That(game.Enemies[0].Health, Is.EqualTo(75));
        }

        [Test]
        public void TwoFatalTowersAwardOneKillAndOneReward()
        {
            var balance = Balance();
            balance.Structure(StructureKind.Gun).Range = 20;
            balance.Structure(StructureKind.Gun).Damage = 1000;
            var game = SpawnTarget(balance);
            Assert.That(game.TryBuild(new[] { new Cell(3, 3), new Cell(3, 4) }, StructureKind.Gun,
                MaterialTier.Pencil, out _), Is.True);
            int inkBefore = game.Ink;
            game.Tick(0.02f);
            Assert.That(game.Kills, Is.EqualTo(1));
            Assert.That(game.Ink - inkBefore, Is.EqualTo(7));
            Assert.That(game.Enemies, Is.Empty);
        }

        [Test]
        public void FatalHitClampsHealthAtZero()
        {
            var balance = Balance();
            balance.Structure(StructureKind.Gun).Range = 20;
            balance.Structure(StructureKind.Gun).Damage = 1000;
            var game = SpawnTarget(balance);
            var target = game.Enemies[0];
            Assert.That(game.TryBuild(new[] { new Cell(3, 4) }, StructureKind.Gun,
                MaterialTier.Pencil, out _), Is.True);
            game.Tick(0.02f);
            Assert.That(target.Health, Is.Zero);
        }

        private static StructureState CompleteWeapon(StructureKind kind)
        {
            return new StructureState
            {
                Id = 1, Cell = new Cell(3, 4), Kind = kind, Tier = MaterialTier.Pencil,
                Health = 60, MaxHealth = 60, IsComplete = true
            };
        }

        private static EnemyState Enemy(int id, Point2 position, float health = 100)
        {
            return new EnemyState { Id = id, Position = position, Health = health, MaxHealth = health };
        }

        [Test]
        public void MortarImpactQueriesCurrentPositionsRatherThanLaunchVictims()
        {
            var balance = Balance();
            balance.Structure(StructureKind.Mortar).FlightSeconds = 0.2f;
            var weapons = new WeaponSystem(balance);
            var structures = new List<StructureState> { CompleteWeapon(StructureKind.Mortar) };
            var original = Enemy(1, new Point2(5.5f, 4.5f));
            var arriving = Enemy(2, new Point2(9.5f, 4.5f));
            var enemies = new List<EnemyState> { original, arriving };
            var shots = new List<ShotEvent>();
            var statistics = new RunStatistics();
            var events = new SimulationEventBuffer();
            weapons.Fire(structures, enemies, shots, 1, 1, 0.02f, 1, statistics, events);
            original.Position = new Point2(9.5f, 4.5f);
            arriving.Position = new Point2(5.5f, 4.5f);
            weapons.Fire(structures, enemies, shots, 1, 1, 0.2f, 2, statistics, events);
            Assert.That(original.Health, Is.EqualTo(100));
            Assert.That(arriving.Health, Is.EqualTo(75));
        }

        [Test]
        public void NewMortarShellKeepsFullFlightTimeOnItsLaunchStep()
        {
            var balance = Balance();
            balance.Structure(StructureKind.Mortar).FlightSeconds = 0.2f;
            var weapons = new WeaponSystem(balance);
            var enemy = Enemy(1, new Point2(5.5f, 4.5f));
            weapons.Fire(new List<StructureState> { CompleteWeapon(StructureKind.Mortar) },
                new List<EnemyState> { enemy }, new List<ShotEvent>(), 1, 1, 1, 1,
                new RunStatistics(), new SimulationEventBuffer());
            Assert.That(enemy.Health, Is.EqualTo(100));
            Assert.That(weapons.Projectiles[0].Remaining, Is.EqualTo(0.2f));
        }

        [Test]
        public void MortarShellSnapshotsDamageAndFlightBeforeSourceChanges()
        {
            var balance = Balance();
            var definition = balance.Structure(StructureKind.Mortar);
            definition.FlightSeconds = 0.2f;
            var weapons = new WeaponSystem(balance);
            var source = CompleteWeapon(StructureKind.Mortar);
            var structures = new List<StructureState> { source };
            var enemy = Enemy(1, new Point2(5.5f, 4.5f));
            var enemies = new List<EnemyState> { enemy };
            var shots = new List<ShotEvent>();
            var statistics = new RunStatistics();
            var events = new SimulationEventBuffer();
            weapons.Fire(structures, enemies, shots, 1, 1, 0.02f, 1, statistics, events);
            source.Health = 0;
            source.Tier = MaterialTier.Marker;
            definition.Damage = 1000;
            definition.FlightSeconds = 100;
            weapons.Fire(structures, enemies, shots, 1, 4, 0.2f, 2, statistics, events);
            Assert.That(enemy.Health, Is.EqualTo(75));
            Assert.That(weapons.HasPendingProjectiles, Is.False);
        }

        [TestCase(StructureKind.Gun)]
        [TestCase(StructureKind.Piercer)]
        [TestCase(StructureKind.Spikes)]
        [TestCase(StructureKind.Mortar)]
        public void DamageStatisticsCountOnlyHealthActuallyRemoved(StructureKind kind)
        {
            var balance = Balance();
            var definition = balance.Structure(kind);
            definition.Damage = 1000;
            definition.Radius = 3;
            definition.Range = 20;
            definition.FlightSeconds = 0.2f;
            var weapons = new WeaponSystem(balance);
            var structures = new List<StructureState> { CompleteWeapon(kind), CompleteWeapon(kind) };
            var enemy = Enemy(1, new Point2(5.5f, 4.5f), 7);
            var enemies = new List<EnemyState> { enemy };
            var statistics = new RunStatistics();
            var events = new SimulationEventBuffer();
            var shots = new List<ShotEvent>();
            weapons.Fire(structures, enemies, shots, 1, 1, 0.02f, 1, statistics, events);
            weapons.Fire(structures, enemies, shots, 1, 1, 0.2f, 2, statistics, events);
            Assert.That(statistics.Damage(kind), Is.EqualTo(7));
            Assert.That(enemy.Health, Is.Zero);
        }

        [Test]
        public void CoolingWeaponStillCountsTimeWithoutAnAvailableTarget()
        {
            var source = CompleteWeapon(StructureKind.Gun);
            source.Cooldown = 1;
            var statistics = new RunStatistics();
            new WeaponSystem(Balance()).Fire(new List<StructureState> { source },
                new List<EnemyState> { Enemy(1, new Point2(19.5f, 15.5f)) }, new List<ShotEvent>(),
                1, 1, 0.25f, 1, statistics, new SimulationEventBuffer());
            Assert.That(statistics.IdleSeconds(StructureKind.Gun), Is.EqualTo(0.25f));
            Assert.That(statistics.Launches(StructureKind.Gun), Is.Zero);
        }

        [Test]
        public void CoolingWeaponWithLiveTargetDoesNotCountIdleTime()
        {
            var source = CompleteWeapon(StructureKind.Gun);
            source.Cooldown = 1;
            var statistics = new RunStatistics();
            new WeaponSystem(Balance()).Fire(new List<StructureState> { source },
                new List<EnemyState> { Enemy(1, new Point2(5.5f, 4.5f)) }, new List<ShotEvent>(),
                1, 1, 0.25f, 1, statistics, new SimulationEventBuffer());
            Assert.That(statistics.IdleSeconds(StructureKind.Gun), Is.Zero);
            Assert.That(statistics.Launches(StructureKind.Gun), Is.Zero);
        }

        [Test]
        public void BlueprintWeaponNeitherShootsNorAccumulatesIdleTime()
        {
            var source = CompleteWeapon(StructureKind.Gun);
            source.IsComplete = false;
            var statistics = new RunStatistics();
            var enemy = Enemy(1, new Point2(5.5f, 4.5f));
            var events = new SimulationEventBuffer();
            new WeaponSystem(Balance()).Fire(new List<StructureState> { source },
                new List<EnemyState> { enemy }, new List<ShotEvent>(),
                1, 1, 0.25f, 1, statistics, events);
            Assert.That(enemy.Health, Is.EqualTo(100));
            Assert.That(statistics.IdleSeconds(StructureKind.Gun), Is.Zero);
            Assert.That(events.Count, Is.Zero);
        }

        [Test]
        public void MortarPublishesSeparateLaunchAndImpactEvents()
        {
            var balance = Balance();
            balance.Structure(StructureKind.Mortar).FlightSeconds = 0.2f;
            var weapons = new WeaponSystem(balance);
            var structures = new List<StructureState> { CompleteWeapon(StructureKind.Mortar) };
            var enemies = new List<EnemyState> { Enemy(1, new Point2(5.5f, 4.5f)) };
            var shots = new List<ShotEvent>();
            var statistics = new RunStatistics();
            var events = new SimulationEventBuffer();
            weapons.Fire(structures, enemies, shots, 1, 1, 0.02f, 10, statistics, events);
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Kind, Is.EqualTo(SimulationEventKind.ProjectileLaunched));
            Assert.That(events[0].TravelSeconds, Is.EqualTo(0.2f));
            Assert.That(shots, Is.Empty);
            weapons.Fire(structures, enemies, shots, 1, 1, 0.2f, 11, statistics, events);
            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events[1].Kind, Is.EqualTo(SimulationEventKind.ProjectileImpact));
            Assert.That(events[1].Tick, Is.EqualTo(11));
            Assert.That(events[1].Sequence, Is.GreaterThan(events[0].Sequence));
            Assert.That(events[1].EntityId, Is.EqualTo(events[0].EntityId));
        }

        [TestCase(1)]
        [TestCase(512)]
        public void DroppingVisualEventsDoesNotDropMortarDamage(int eventCapacity)
        {
            var balance = Balance();
            balance.Structure(StructureKind.Mortar).FlightSeconds = 0.2f;
            var weapons = new WeaponSystem(balance);
            var structures = new List<StructureState> { CompleteWeapon(StructureKind.Mortar) };
            var enemies = new List<EnemyState>();
            for (int i = 0; i < 10; i++) enemies.Add(Enemy(i + 1, new Point2(5.5f, 4.5f), 7));
            var statistics = new RunStatistics();
            var events = new SimulationEventBuffer(eventCapacity);
            var shots = new List<ShotEvent>();
            weapons.Fire(structures, enemies, shots, 1, 1, 0.02f, 1, statistics, events);
            for (int i = 0; i < 520; i++)
                events.Publish(1, SimulationEventKind.Shot, new Point2(), new Point2(), StructureKind.Gun);
            weapons.Fire(structures, enemies, shots, 1, 1, 0.2f, 2, statistics, events);
            Assert.That(events.Count, Is.EqualTo(eventCapacity));
            Assert.That(events.LatestSequence, Is.EqualTo(522));
            Assert.That(statistics.Damage(StructureKind.Mortar), Is.EqualTo(70));
            Assert.That(enemies.TrueForAll(enemy => enemy.Health == 0), Is.True);
        }

        [Test]
        public void WaveCompletionWaitsForShellEvenWhenAnotherWeaponKillsItsTarget()
        {
            var balance = Balance();
            balance.Structure(StructureKind.Gun).Range = 20;
            balance.Structure(StructureKind.Gun).Damage = 1000;
            var game = SpawnTarget(balance);
            BuildMortar(game);
            Assert.That(game.TryBuild(new[] { new Cell(3, 3) }, StructureKind.Gun,
                MaterialTier.Pencil, out _), Is.True);
            game.Tick(0.02f);
            Assert.That(game.Enemies, Is.Empty);
            Assert.That(game.Phase, Is.EqualTo(RunPhase.Assault));
            game.Tick(0.9f);
            Assert.That(game.Phase, Is.EqualTo(RunPhase.Victory));
        }

        [Test]
        public void ExistingShellImpactResolvesBeforeNewWeaponTargetSelection()
        {
            var balance = Balance();
            balance.Structure(StructureKind.Mortar).FlightSeconds = 0.2f;
            var weapons = new WeaponSystem(balance);
            var structures = new List<StructureState> { CompleteWeapon(StructureKind.Mortar) };
            var enemies = new List<EnemyState> { Enemy(1, new Point2(5.5f, 4.5f), 7) };
            var statistics = new RunStatistics();
            var events = new SimulationEventBuffer();
            var shots = new List<ShotEvent>();
            weapons.Fire(structures, enemies, shots, 1, 1, 0.02f, 1, statistics, events);
            structures.Add(CompleteWeapon(StructureKind.Gun));
            weapons.Fire(structures, enemies, shots, 1, 1, 0.2f, 2, statistics, events);
            Assert.That(statistics.Damage(StructureKind.Mortar), Is.EqualTo(7));
            Assert.That(statistics.Launches(StructureKind.Gun), Is.Zero);
            Assert.That(shots, Is.Empty);
        }
    }
}
