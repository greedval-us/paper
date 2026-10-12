using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class GameSessionTests
    {
        private static GameBalance Balance(int count = 1)
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
                new WaveDefinition { Count = count, SpawnInterval = 0.01f, Sides = 1, Title = "Test" }
            };
            foreach (StructureKind kind in Enum.GetValues(typeof(StructureKind)))
                balance.Structure(kind).BuildSeconds = 0;
            balance.Structure(StructureKind.Wall).Cost = 10;
            balance.Structure(StructureKind.Wall).MaxHealth = 30;
            balance.Material(MaterialTier.Pencil).RefundFraction = 0.5f;
            balance.Material(MaterialTier.Pencil).CostMultiplier = 1;
            balance.Material(MaterialTier.Ink).CostMultiplier = 2;
            balance.Material(MaterialTier.Marker).CostMultiplier = 4;
            balance.Enemy(EnemyKind.Doodle).MaxHealth = 100;
            balance.Enemy(EnemyKind.Doodle).Speed = 2;
            balance.Enemy(EnemyKind.Doodle).Damage = 10;
            balance.Enemy(EnemyKind.Doodle).AttackInterval = 0.2f;
            balance.Enemy(EnemyKind.Doodle).Reward = 0;
            return balance;
        }

        [Test]
        public void InvalidCellRejectsEntireStrokeWithoutSpendingInk()
        {
            var game = new GameSession(Balance(), starterDefenses: false);
            Assert.That(game.TryBuild(new[] { new Cell(3, 3), game.BaseCell }, StructureKind.Wall,
                MaterialTier.Pencil, out _), Is.False);
            Assert.That(game.Ink, Is.EqualTo(1000));
            Assert.That(game.Structures, Is.Empty);
        }

        [Test]
        public void RepeatedCellsAreChargedAndBuiltOnce()
        {
            var game = new GameSession(Balance(), starterDefenses: false);
            var cells = new[] { new Cell(3, 3), new Cell(3, 3), new Cell(4, 3) };
            Assert.That(game.QuoteBuild(cells, StructureKind.Wall, MaterialTier.Pencil).Cost, Is.EqualTo(20));
            Assert.That(game.TryBuild(cells, StructureKind.Wall, MaterialTier.Pencil, out _), Is.True);
            Assert.That(game.Ink, Is.EqualTo(980));
            Assert.That(game.Structures.Count, Is.EqualTo(2));
        }

        [TestCase(StructureKind.Wall)]
        [TestCase(StructureKind.Spikes)]
        public void CompatibleSegmentsAreSkippedAndAnExistingStrokeIsAFreeNoOp(StructureKind kind)
        {
            var game = new GameSession(Balance(), starterDefenses: false);
            var existing = new Cell(3, 3);
            game.TryBuild(new[] { existing }, kind, MaterialTier.Pencil, out _);
            int ink = game.Ink;
            var noOp = game.QuoteBuild(new[] { existing, existing }, kind, MaterialTier.Pencil);
            Assert.That(noOp.Valid, Is.True);
            Assert.That(noOp.Cost, Is.Zero);
            Assert.That(game.TryBuild(new[] { existing }, kind, MaterialTier.Pencil, out _), Is.True);
            Assert.That(game.Ink, Is.EqualTo(ink));
            Assert.That(game.Structures.Count, Is.EqualTo(1));
            Assert.That(game.TryBuild(new[] { existing, new Cell(4, 3) }, kind, MaterialTier.Pencil, out _), Is.True);
            Assert.That(game.Structures.Count, Is.EqualTo(2));
        }

        [Test]
        public void ExecutionRechecksOccupancyAfterAnEarlierValidQuote()
        {
            var game = new GameSession(Balance(), starterDefenses: false);
            var cells = new[] { new Cell(3, 3), new Cell(4, 3) };
            Assert.That(game.QuoteBuild(cells, StructureKind.Wall, MaterialTier.Pencil).Valid, Is.True);
            game.TryBuild(new[] { cells[1] }, StructureKind.Gun, MaterialTier.Pencil, out _);
            int ink = game.Ink;
            Assert.That(game.TryBuild(cells, StructureKind.Wall, MaterialTier.Pencil, out _), Is.False);
            Assert.That(game.Ink, Is.EqualTo(ink));
            Assert.That(game.StructureAt(cells[0]), Is.Null);
        }

        [Test]
        public void IncompatibleTierRejectsWholeStroke()
        {
            var game = new GameSession(Balance(), starterDefenses: false);
            game.TryBuild(new[] { new Cell(3, 3) }, StructureKind.Wall, MaterialTier.Ink, out _);
            int ink = game.Ink;
            Assert.That(game.TryBuild(new[] { new Cell(3, 3), new Cell(4, 3) }, StructureKind.Wall,
                MaterialTier.Pencil, out _), Is.False);
            Assert.That(game.Ink, Is.EqualTo(ink));
            Assert.That(game.Structures.Count, Is.EqualTo(1));
        }

        [Test]
        public void BlueprintAllowsEntryAndWaitsForOccupantBeforeBecomingWall()
        {
            var balance = Balance();
            balance.Structure(StructureKind.Wall).BuildSeconds = 0.8f;
            var game = new GameSession(balance, starterDefenses: false);
            var cell = new Cell(7, 8);
            game.TryBuild(new[] { cell }, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.StartNextWave(); game.Tick(0.02f);
            game.Enemies[0].Position = new Point2(6.5f, 8.5f);
            game.Tick(0.6f);
            Assert.That(game.Enemies[0].Position.X, Is.GreaterThan(7));
            game.Enemies[0].SpeedScale = 0;
            game.Enemies[0].Position = new Point2(7.5f, 8.5f);
            game.Tick(0.3f);
            Assert.That(game.StructureAt(cell).BuildRemaining, Is.Zero);
            Assert.That(game.StructureAt(cell).IsComplete, Is.False);
            Assert.That(game.StructureAt(cell).WaitingForClearance, Is.True);
            game.Enemies[0].Position = new Point2(6.5f, 8.5f);
            game.Tick(0.02f);
            Assert.That(game.StructureAt(cell).IsComplete, Is.True);
            Assert.That(game.StructureAt(cell).WaitingForClearance, Is.False);
        }

        [Test]
        public void DiscountedUpgradeRefundUsesOnlyActualInvestment()
        {
            var balance = Balance();
            int historicalInvestment=TowerCatalog.ToolCost(balance,StructureKind.Wall,MaterialTier.Pencil);
            balance.Material(MaterialTier.Ink).RefundFraction = 0.95f;
            balance.Material(MaterialTier.Pencil).CostMultiplier = 0.1f;
            balance.Material(MaterialTier.Ink).CostMultiplier = 0.2f;
            // Restore the budget and recorded payment of a prior purchase under today's discounted configuration.
            int currentPrice=TowerCatalog.ToolCost(balance,StructureKind.Wall,MaterialTier.Pencil);
            balance.StartingInk -= historicalInvestment-currentPrice;
            var game = new GameSession(balance, starterDefenses: false);
            var cell = new Cell(3, 3);
            game.TryBuild(new[] { cell }, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.StructureAt(cell).PaidInk = historicalInvestment;
            Assert.That(game.TryUpgrade(cell, out _), Is.True);
            Assert.That(game.TryErase(cell, out int refund), Is.True);
            Assert.That(refund, Is.EqualTo(10));
            Assert.That(game.Ink, Is.EqualTo(999));
        }

        [Test]
        public void SpawnBorderAndEnemyOccupiedCellsCannotBeBuiltUpon()
        {
            var game = new GameSession(Balance(), starterDefenses: false);
            Assert.That(game.QuoteBuild(new[] { new Cell(0, 5) }, StructureKind.Wall, MaterialTier.Pencil).Valid, Is.False);
            game.StartNextWave();
            game.Tick(0.02f);
            game.Enemies[0].Position = new Point2(4.5f, 4.5f);
            Assert.That(game.QuoteBuild(new[] { new Cell(4, 4) }, StructureKind.Wall, MaterialTier.Pencil).Valid, Is.False);
        }

        [Test]
        public void InsufficientInkDoesNotPartiallyBuildStroke()
        {
            var balance = Balance();
            balance.StartingInk = 15;
            var game = new GameSession(balance, starterDefenses: false);
            Assert.That(game.TryBuild(new[] { new Cell(3, 3), new Cell(4, 3) }, StructureKind.Wall,
                MaterialTier.Pencil, out _), Is.False);
            Assert.That(game.Ink, Is.EqualTo(15));
            Assert.That(game.Structures, Is.Empty);
        }

        [Test]
        public void DamagedEraseRefundsOnlyRemainingMaterial()
        {
            var game = new GameSession(Balance(), starterDefenses: false);
            var cell = new Cell(3, 3);
            game.TryBuild(new[] { cell }, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.StructureAt(cell).Health = 15;
            Assert.That(game.TryErase(cell, out int refund), Is.True);
            Assert.That(refund, Is.EqualTo(2));
            Assert.That(game.Ink, Is.EqualTo(992));
            Assert.That(game.StructureAt(cell), Is.Null);
        }

        [Test]
        public void MaterialUpgradeChargesDifferenceAndKeepsDamageFraction()
        {
            var balance = Balance();
            balance.Material(MaterialTier.Ink).HealthMultiplier = 2;
            var game = new GameSession(balance, starterDefenses: false);
            var cell = new Cell(3, 3);
            game.TryBuild(new[] { cell }, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.StructureAt(cell).Health = 15;
            Assert.That(game.TryUpgrade(cell, out _), Is.True);
            Assert.That(game.Ink, Is.EqualTo(980));
            Assert.That(game.StructureAt(cell).Tier, Is.EqualTo(MaterialTier.Ink));
            Assert.That(game.StructureAt(cell).MaxHealth, Is.EqualTo(60));
            Assert.That(game.StructureAt(cell).Health, Is.EqualTo(30));
        }

        [Test]
        public void ZeroDeltaFreezesConstructionCountdownAndEconomy()
        {
            var balance = Balance();
            balance.Structure(StructureKind.Wall).BuildSeconds = 2;
            balance.PassiveInkPerSecond = 5;
            var game = new GameSession(balance, starterDefenses: false);
            var cell = new Cell(3, 3);
            game.TryBuild(new[] { cell }, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.Tick(0);
            Assert.That(game.StructureAt(cell).BuildRemaining, Is.EqualTo(2));
            Assert.That(game.Countdown, Is.EqualTo(1000));
            Assert.That(game.Elapsed, Is.Zero);
            Assert.That(game.Ink, Is.EqualTo(990));
            game.Tick(1);
            Assert.That(game.StructureAt(cell).BuildRemaining, Is.EqualTo(1).Within(0.001f));
            Assert.That(game.Ink, Is.EqualTo(995));
        }

        [Test]
        public void EnemiesRouteAroundIsolatedWallsWithoutDamagingThem()
        {
            var balance = Balance();
            balance.Structure(StructureKind.Wall).MaxHealth = 100;
            var game = new GameSession(balance, starterDefenses: false);
            var wall = new Cell(7, 8);
            game.TryBuild(new[] { wall }, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.StartNextWave();
            game.Tick(0.02f);
            game.Enemies[0].Position = new Point2(6.5f, 8.5f);
            game.Tick(1.8f);
            Assert.That(game.StructureAt(wall).Health, Is.EqualTo(100));
            Assert.That(game.Enemies[0].Position.X, Is.GreaterThan(7.4f));
        }

        [Test]
        public void ClosedRingIsLegalAndEnemiesBreachBeforeEntering()
        {
            var game = new GameSession(Balance(), starterDefenses: false);
            var ring = new List<Cell>();
            for (int x = 8; x <= 12; x++) { ring.Add(new Cell(x, 6)); ring.Add(new Cell(x, 10)); }
            for (int y = 7; y <= 9; y++) { ring.Add(new Cell(8, y)); ring.Add(new Cell(12, y)); }
            Assert.That(game.TryBuild(ring, StructureKind.Wall, MaterialTier.Pencil, out _), Is.True);
            game.StartNextWave();
            game.Tick(0.02f);
            game.Enemies[0].Position = new Point2(7.5f, 8.5f);
            game.Tick(0.1f);
            Assert.That(game.Enemies[0].Position.X, Is.LessThan(8));
            game.Tick(1);
            Assert.That(game.Structures.Count, Is.LessThan(ring.Count));
        }

        [Test]
        public void NewlyDrawnWallRebuildsExistingEnemyRoute()
        {
            var balance = Balance();
            balance.Structure(StructureKind.Wall).MaxHealth = 100;
            var game = new GameSession(balance, starterDefenses: false);
            game.StartNextWave(); game.Tick(0.02f);
            game.Enemies[0].Position = new Point2(6.5f, 8.5f);
            game.Tick(0.05f);
            var wall = new Cell(7, 8);
            Assert.That(game.TryBuild(new[] { wall }, StructureKind.Wall, MaterialTier.Pencil, out _), Is.True);
            game.Tick(0.05f);
            Assert.That(game.StructureAt(wall).Health, Is.EqualTo(100));
            Assert.That(game.Enemies[0].Position.Y, Is.GreaterThan(8.5f));
        }

        [Test]
        public void HealthBandsCoalesceNavigationRebuildsWithinOneStep()
        {
            var balance = Balance();
            balance.Structure(StructureKind.Wall).MaxHealth = 100;
            var game = new GameSession(balance, starterDefenses: false);
            var cells = new[] { new Cell(7, 8), new Cell(7, 9) };
            game.TryBuild(cells, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.StartNextWave(); game.Tick(0.02f);
            int rebuilt = game.FlowRebuildCount;
            game.StructureAt(cells[0]).Health = 90;
            game.Tick(0.02f);
            Assert.That(game.FlowRebuildCount, Is.EqualTo(rebuilt));
            game.StructureAt(cells[0]).Health = 74;
            game.StructureAt(cells[1]).Health = 24;
            game.Tick(0.02f);
            Assert.That(game.FlowRebuildCount, Is.EqualTo(rebuilt + 1));
            game.Tick(0.02f);
            Assert.That(game.FlowRebuildCount, Is.EqualTo(rebuilt + 1));
            Assert.That(game.Statistics.NavigationRebuilds, Is.EqualTo(game.FlowRebuildCount));
            Assert.That(game.Statistics.NavigationTotalMilliseconds, Is.GreaterThan(0));
        }

        [Test]
        public void CompletingBlueprintInvalidatesEnemyDestination()
        {
            var balance = Balance();
            balance.Structure(StructureKind.Wall).MaxHealth = 100;
            balance.Structure(StructureKind.Wall).BuildSeconds = 0.2f;
            var game = new GameSession(balance, starterDefenses: false);
            var wall = new Cell(7, 8);
            game.TryBuild(new[] { wall }, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.StartNextWave(); game.Tick(0.02f);
            game.Enemies[0].Position = new Point2(6.5f, 8.5f);
            game.Tick(0.1f);
            Assert.That(game.Enemies[0].Position.X, Is.GreaterThan(6.5f));
            game.Tick(0.15f);
            Assert.That(game.StructureAt(wall).IsComplete, Is.True);
            Assert.That(game.StructureAt(wall).Health, Is.EqualTo(100));
            Assert.That(game.Enemies[0].Position.Y, Is.GreaterThan(8.5f));
        }

        [Test]
        public void CrowdedBreachHasOnlyThreeFrontlineAttackersDuringCooldown()
        {
            var balance = Balance(100);
            balance.Waves[0].SpawnInterval = 0.005f;
            balance.Structure(StructureKind.Wall).MaxHealth = 100;
            balance.Enemy(EnemyKind.Doodle).Damage = 1;
            var game = new GameSession(balance, starterDefenses: false);
            var ring = new List<Cell>();
            for (int x = 8; x <= 12; x++) { ring.Add(new Cell(x, 6)); ring.Add(new Cell(x, 10)); }
            for (int y = 7; y <= 9; y++) { ring.Add(new Cell(8, y)); ring.Add(new Cell(12, y)); }
            game.TryBuild(ring, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.StartNextWave(); game.Tick(0.51f);
            Assert.That(game.Enemies.Count, Is.EqualTo(100));
            foreach (var enemy in game.Enemies) enemy.Position = new Point2(7.5f, 8.5f);
            game.Tick(0.02f);
            Assert.That(game.StructureAt(new Cell(8, 8)).Health, Is.EqualTo(97));
            game.Tick(0.1f);
            Assert.That(game.StructureAt(new Cell(8, 8)).Health, Is.EqualTo(97));
        }

        [Test]
        public void NearestContendersTakeFrontageAndDeathReleasesTheirPlace()
        {
            var balance = Balance(4);
            balance.Structure(StructureKind.Wall).MaxHealth = 1000;
            balance.Enemy(EnemyKind.Doodle).Damage = 1;
            balance.Enemy(EnemyKind.Doodle).AttackInterval = 10;
            balance.Enemy(EnemyKind.Gnawer).Damage = 50;
            var game = new GameSession(balance, starterDefenses: false);
            var ring = new List<Cell>();
            for (int x = 8; x <= 12; x++) { ring.Add(new Cell(x, 6)); ring.Add(new Cell(x, 10)); }
            for (int y = 7; y <= 9; y++) { ring.Add(new Cell(8, y)); ring.Add(new Cell(12, y)); }
            game.TryBuild(ring, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.StartNextWave(); game.Tick(0.05f);
            game.Enemies[0].Kind = EnemyKind.Gnawer;
            game.Enemies[0].Position = new Point2(7.05f, 8.5f);
            for (int i = 1; i < 4; i++) game.Enemies[i].Position = new Point2(7.9f - i * 0.1f, 8.5f);
            game.Tick(0.02f);
            var target = game.StructureAt(new Cell(8, 8));
            Assert.That(target.Health, Is.EqualTo(997));
            game.Enemies[1].Health = 0;
            game.Enemies[0].Kind = EnemyKind.Doodle;
            game.Enemies[0].Position = new Point2(7.7f, 8.5f);
            game.Tick(0.02f);
            Assert.That(target.Health, Is.EqualTo(996));
            game.Tick(0.02f);
            Assert.That(target.Health, Is.EqualTo(996));
        }

        [Test]
        public void CloserWaitingEnemyCannotReplaceCoolingFrontageOwner()
        {
            var balance = Balance(4);
            balance.Waves[0].SpawnInterval = 0.005f;
            balance.Structure(StructureKind.Wall).MaxHealth = 1000;
            balance.Enemy(EnemyKind.Doodle).Damage = 1;
            balance.Enemy(EnemyKind.Doodle).AttackInterval = 10;
            var game = new GameSession(balance, starterDefenses: false);
            var ring = new List<Cell>();
            for (int x = 8; x <= 12; x++) { ring.Add(new Cell(x, 6)); ring.Add(new Cell(x, 10)); }
            for (int y = 7; y <= 9; y++) { ring.Add(new Cell(8, y)); ring.Add(new Cell(12, y)); }
            game.TryBuild(ring, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.StartNextWave(); game.Tick(0.02f);
            for (int i = 0; i < 3; i++) game.Enemies[i].Position = new Point2(7.46f, 8.5f);
            var waiting = game.Enemies[3];
            waiting.Position = new Point2(7.44f, 8.5f);
            game.Tick(1f / 30f);
            var target = game.StructureAt(new Cell(8, 8));
            Assert.That(target.Health, Is.EqualTo(997));
            Assert.That(waiting.Position.X, Is.GreaterThan(7.46f));
            game.Tick(1f / 30f);
            Assert.That(target.Health, Is.EqualTo(997));
            game.Enemies[0].Health = 0;
            game.Tick(1f / 30f);
            Assert.That(target.Health, Is.EqualTo(996));
            game.Tick(1f / 30f);
            Assert.That(target.Health, Is.EqualTo(996));
        }

        [Test]
        public void QueueOccupancyStaysBoundedAcrossSpawnAndClosedWall()
        {
            var balance = Balance(180);
            balance.Waves[0].SpawnInterval = 0.005f;
            balance.Structure(StructureKind.Wall).MaxHealth = 10000;
            balance.Enemy(EnemyKind.Doodle).Damage = 0;
            var game = new GameSession(balance, starterDefenses: false);
            var ring = new List<Cell>();
            for (int x = 8; x <= 12; x++) { ring.Add(new Cell(x, 6)); ring.Add(new Cell(x, 10)); }
            for (int y = 7; y <= 9; y++) { ring.Add(new Cell(8, y)); ring.Add(new Cell(12, y)); }
            game.TryBuild(ring, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.StartNextWave(); game.Tick(6);
            var occupancy = new Dictionary<Cell, int>();
            foreach (var enemy in game.Enemies)
            {
                var cell = new Cell((int)Math.Floor(enemy.Position.X), (int)Math.Floor(enemy.Position.Y));
                occupancy.TryGetValue(cell, out int count);
                occupancy[cell] = count + 1;
            }
            Assert.That(game.Enemies.Count, Is.GreaterThan(60));
            foreach (int count in occupancy.Values) Assert.That(count, Is.LessThanOrEqualTo(6));
            Assert.That(occupancy.Count, Is.GreaterThan(20));
        }

        [Test]
        public void DestroyedDestinationIsReleasedWithoutWaitingForAnotherAttack()
        {
            var balance = Balance();
            var game = new GameSession(balance, starterDefenses: false);
            var ring = new List<Cell>();
            for (int x = 8; x <= 12; x++) { ring.Add(new Cell(x, 6)); ring.Add(new Cell(x, 10)); }
            for (int y = 7; y <= 9; y++) { ring.Add(new Cell(8, y)); ring.Add(new Cell(12, y)); }
            game.TryBuild(ring, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.StartNextWave(); game.Tick(0.02f);
            game.Enemies[0].Position = new Point2(7.5f, 8.5f);
            game.Tick(0.02f);
            Assert.That(game.TryErase(new Cell(8, 8), out _), Is.True);
            game.Tick(0.6f);
            Assert.That(game.Enemies[0].Position.X, Is.GreaterThan(8));
        }

        [TestCase(EnemyKind.Doodle, 90f)]
        [TestCase(EnemyKind.Gnawer, 85f)]
        public void GnawersHaveStrongerBreachDamage(EnemyKind kind, float remainingHealth)
        {
            var balance = Balance();
            balance.Structure(StructureKind.Wall).MaxHealth = 100;
            balance.Enemy(kind).Damage = 10;
            var game = new GameSession(balance, starterDefenses: false);
            var ring = new List<Cell>();
            for (int x = 8; x <= 12; x++) { ring.Add(new Cell(x, 6)); ring.Add(new Cell(x, 10)); }
            for (int y = 7; y <= 9; y++) { ring.Add(new Cell(8, y)); ring.Add(new Cell(12, y)); }
            game.TryBuild(ring, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.StartNextWave(); game.Tick(0.02f);
            game.Enemies[0].Kind = kind;
            game.Enemies[0].Position = new Point2(7.5f, 8.5f);
            game.Tick(0.02f);
            Assert.That(game.StructureAt(new Cell(8, 8)).Health, Is.EqualTo(remainingHealth));
        }

        [TestCase(StructureKind.Gun, 1)]
        [TestCase(StructureKind.Piercer, 3)]
        [TestCase(StructureKind.Mortar, 3)]
        public void WeaponRolesDamageTheExpectedNumberOfTargets(StructureKind kind, int damaged)
        {
            var balance = Balance(3);
            var definition = balance.Structure(kind);
            definition.Range = 8;
            definition.Damage = 10;
            definition.ShotInterval = 10;
            definition.Radius = 2.1f;
            definition.MaxPierce = 3;
            balance.Enemy(EnemyKind.Doodle).Speed = 0;
            var game = new GameSession(balance, starterDefenses: false);
            game.StartNextWave();
            game.Tick(0.05f);
            for (int i = 0; i < 3; i++) game.Enemies[i].Position = new Point2(5.5f + i, 4.5f);
            game.TryBuild(new[] { new Cell(3, 4) }, kind, MaterialTier.Pencil, out _);
            game.Tick(0.02f);
            if (kind == StructureKind.Mortar)
            {
                foreach (var enemy in game.Enemies) Assert.That(enemy.Health, Is.EqualTo(100));
                game.Tick(definition.FlightSeconds + 0.02f);
            }
            int actual = 0;
            foreach (var enemy in game.Enemies) if (enemy.Health < 100) actual++;
            Assert.That(actual, Is.EqualTo(damaged));
            Assert.That(game.Shots.Count, Is.EqualTo(kind == StructureKind.Mortar ? 0 : 1));
        }

        [Test]
        public void SpikesDamagePassingEnemiesWithoutBlockingFlow()
        {
            var balance = Balance();
            balance.Structure(StructureKind.Spikes).Damage = 20;
            var game = new GameSession(balance, starterDefenses: false);
            game.TryBuild(new[] { new Cell(7, 8) }, StructureKind.Spikes, MaterialTier.Pencil, out _);
            game.StartNextWave();
            game.Tick(0.02f);
            game.Enemies[0].Position = new Point2(6.5f, 8.5f);
            game.Tick(0.7f);
            Assert.That(game.Enemies[0].Health, Is.LessThan(100));
            Assert.That(game.Enemies[0].Position.X, Is.GreaterThan(7));
        }

        [Test]
        public void LastKillCompletesWaveAndFinalWaveWins()
        {
            var balance = Balance();
            balance.Structure(StructureKind.Gun).Range = 30;
            balance.Structure(StructureKind.Gun).Damage = 1000;
            var game = new GameSession(balance, starterDefenses: false);
            game.TryBuild(new[] { new Cell(6, 6) }, StructureKind.Gun, MaterialTier.Pencil, out _);
            game.StartNextWave();
            game.Tick(0.05f);
            Assert.That(game.Phase, Is.EqualTo(RunPhase.Victory));
            Assert.That(game.Kills, Is.EqualTo(1));
            Assert.That(game.Enemies, Is.Empty);
        }

        [Test]
        public void UpgradeChoiceAppearsAfterSecondWaveAndIsConsumedOnce()
        {
            var balance = Balance();
            balance.Waves.Add(new WaveDefinition { Count = 1, SpawnInterval = 0.01f, Sides = 1 });
            balance.Waves.Add(new WaveDefinition { Count = 1, SpawnInterval = 0.01f, Sides = 1 });
            balance.Structure(StructureKind.Gun).Range = 30;
            balance.Structure(StructureKind.Gun).Damage = 1000;
            balance.Structure(StructureKind.Gun).ShotInterval = 0.05f;
            var game = new GameSession(balance, starterDefenses: false);
            game.TryBuild(new[] { new Cell(6, 6) }, StructureKind.Gun, MaterialTier.Pencil, out _);
            game.StartNextWave(); game.Tick(0.1f);
            Assert.That(game.Phase, Is.EqualTo(RunPhase.Intermission));
            game.StartNextWave(); game.Tick(0.1f);
            Assert.That(game.PendingUpgrade, Is.True);
            game.ChooseUpgrade(RunUpgrade.DeepInk);
            Assert.That(game.PendingUpgrade, Is.False);
        }

        [Test]
        public void EarlyLaunchGrantIsBoundedAndCannotBeClaimedTwice()
        {
            var balance = Balance();
            balance.EarlyLaunchInkPerSecond = 2;
            balance.MaxEarlyLaunchInk = 30;
            var game = new GameSession(balance, starterDefenses: false);
            game.StartNextWave(); game.StartNextWave();
            Assert.That(game.Ink, Is.EqualTo(1030));
        }

        [Test]
        public void SameSeedAndCommandsProduceIdenticalSimulation()
        {
            var balance = Balance(15);
            balance.Waves[0].Sides = 15;
            balance.Waves[0].RunnerFraction = 0.3f;
            balance.Waves[0].GnawerFraction = 0.2f;
            var first = new GameSession(balance, 42, false);
            var second = new GameSession(balance, 42, false);
            first.StartNextWave(); second.StartNextWave();
            for (int i = 0; i < 120; i++) { first.Tick(1f / 60); second.Tick(1f / 60); }
            Assert.That(first.Enemies.Count, Is.EqualTo(second.Enemies.Count));
            Assert.That(first.BaseHealth, Is.EqualTo(second.BaseHealth));
            for (int i = 0; i < first.Enemies.Count; i++)
            {
                Assert.That(first.Enemies[i].Kind, Is.EqualTo(second.Enemies[i].Kind));
                Assert.That(first.Enemies[i].Position.X, Is.EqualTo(second.Enemies[i].Position.X));
                Assert.That(first.Enemies[i].Position.Y, Is.EqualTo(second.Enemies[i].Position.Y));
            }
        }
    }
}
