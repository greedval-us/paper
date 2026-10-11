using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class EnemyMechanicsTests
    {
        private static GameBalance MechanicsBalance(int count = 3)
        {
            var balance = GameBalance.CreateDefault();
            balance.Width = 40; balance.Height = 24; balance.StartingInk = 1000;
            balance.BaseHealth = 500; balance.PlanningSeconds = 1000;
            balance.EnableEnemyMechanics = true; balance.EnableInkWells = false;
            balance.PassiveInkPerSecond = 0; balance.EarlyLaunchInkPerSecond = 0; balance.WaveInkGrant = 0;
            balance.Waves = new List<WaveDefinition> { new WaveDefinition { Count = count, SpawnInterval = .005f, Sides = 1 } };
            foreach (var definition in balance.StructureDefinitions) definition.BuildSeconds = 0;
            var doodle = balance.Enemy(EnemyKind.Doodle);
            doodle.Speed = 0; doodle.Damage = 0; doodle.MaxHealth = 100; doodle.Reward = 0;
            return balance;
        }

        [TestCase(DrawingTier.Sketch, 1f, 0f)]
        [TestCase(DrawingTier.Outlined, 1.3f, .08f)]
        [TestCase(DrawingTier.Hatched, 1.7f, .15f)]
        public void DrawingTierChangesHealthAndDefenseWithoutChangingRole(DrawingTier tier, float health, float defense)
        {
            Assert.That(EnemyEffects.DrawingHealthMultiplier(tier), Is.EqualTo(health));
            var enemy = new EnemyState { Kind = EnemyKind.Runner, DrawingTier = tier, Health = 1000, MaxHealth = 1000 };
            Assert.That(EnemyEffects.Defense(enemy), Is.EqualTo(defense).Within(.0001));
            float actual = EnemyEffects.ApplyHit(enemy, 100, InkColor.None, new RunStatistics(), StructureKind.Gun);
            Assert.That(actual, Is.EqualTo(100 * (1 - defense)).Within(.001));
            Assert.That(enemy.Kind, Is.EqualTo(EnemyKind.Runner));
        }

        [TestCase(DrawingTier.Outlined, 1.3f)]
        [TestCase(DrawingTier.Hatched, 1.7f)]
        public void SpawnedDrawingTierScalesRoleHealthWithoutChangingItsMovement(DrawingTier tier, float multiplier)
        {
            var balance = MechanicsBalance(1);
            balance.Waves[0].RunnerFraction = 1;
            balance.Waves[0].OutlinedFraction = tier == DrawingTier.Outlined ? 1 : 0;
            balance.Waves[0].HatchedFraction = tier == DrawingTier.Hatched ? 1 : 0;
            var game = new GameSession(balance, starterDefenses: false);
            game.StartNextWave(); game.Tick(.02f);
            var enemy = game.Enemies[0];
            Assert.That(enemy.Kind, Is.EqualTo(EnemyKind.Runner));
            Assert.That(enemy.DrawingTier, Is.EqualTo(tier));
            Assert.That(enemy.MaxHealth, Is.EqualTo(balance.Enemy(EnemyKind.Runner).MaxHealth * multiplier).Within(.001));
            Assert.That(EnemyEffects.MovementMultiplier(enemy), Is.EqualTo(1));
        }

        [Test]
        public void GreenCountersCombinedDefenseAndBlueCapsBlotSpeed()
        {
            var enemy = new EnemyState { Health = 1000, MaxHealth = 1000, DrawingTier = DrawingTier.Hatched, BlotRemaining = 4 };
            Assert.That(EnemyEffects.MovementMultiplier(enemy), Is.EqualTo(1.15f).Within(.001));
            float damage = EnemyEffects.ApplyHit(enemy, 100, InkColor.Green, new RunStatistics(), StructureKind.Piercer);
            Assert.That(damage, Is.EqualTo(90).Within(.001));
            EnemyEffects.ApplyHit(enemy, 0, InkColor.Blue, new RunStatistics(), StructureKind.Gun);
            Assert.That(EnemyEffects.MovementMultiplier(enemy), Is.EqualTo(.65f).Within(.001));
            EnemyEffects.Advance(enemy, 3, new RunStatistics());
            Assert.That(enemy.BlueRemaining, Is.Zero);
            Assert.That(enemy.GreenRemaining, Is.Zero);
            Assert.That(EnemyEffects.MovementMultiplier(enemy), Is.EqualTo(1.15f).Within(.001));
        }

        [Test]
        public void RedRefreshesOneBoundedDotAndRecordsActualSourceDamage()
        {
            var enemy = new EnemyState { Health = 100, MaxHealth = 100 };
            var statistics = new RunStatistics();
            EnemyEffects.ApplyHit(enemy, 0, InkColor.Red, statistics, StructureKind.Mortar);
            EnemyEffects.Advance(enemy, 2, statistics);
            Assert.That(enemy.Health, Is.EqualTo(92).Within(.001));
            EnemyEffects.ApplyHit(enemy, 0, InkColor.Red, statistics, StructureKind.Mortar);
            EnemyEffects.Advance(enemy, 10, statistics);
            Assert.That(enemy.Health, Is.EqualTo(80).Within(.001));
            Assert.That(statistics.Damage(StructureKind.Mortar), Is.EqualTo(20).Within(.001));
            Assert.That(statistics.Damage(StructureKind.Gun), Is.Zero);
            EnemyEffects.Advance(enemy, 10, statistics);
            Assert.That(enemy.Health, Is.EqualTo(80).Within(.001));
        }

        [Test]
        public void RedFatalDamageClampsHealthAndNeverAppliesASecondChain()
        {
            var enemy = new EnemyState { Health = 2, MaxHealth = 2 };
            var statistics = new RunStatistics();
            EnemyEffects.ApplyHit(enemy, 0, InkColor.Red, statistics, StructureKind.Gun);
            EnemyEffects.Advance(enemy, 10, statistics);
            Assert.That(enemy.Health, Is.Zero);
            Assert.That(statistics.Damage(StructureKind.Gun), Is.EqualTo(2));
            Assert.That(enemy.RedRemaining, Is.Zero);
            Assert.That(enemy.BlotRemaining, Is.Zero);
        }

        [Test]
        public void CorpseBlotIsConsumedOnceAndBuffedCorpseLeavesOrdinaryBlot()
        {
            var game = new GameSession(MechanicsBalance(), starterDefenses: false);
            game.StartNextWave(); game.Tick(.02f);
            game.Enemies[0].Position = new Point2(5.5f, 5.5f);
            game.Enemies[1].Position = new Point2(5.5f, 5.5f);
            game.Enemies[0].Health = 0;
            game.Tick(1f / 30);
            Assert.That(game.Blots.Count, Is.EqualTo(1));
            game.Tick(1f / 30);
            Assert.That(game.Blots, Is.Empty);
            var buffed = game.Enemies[0];
            Assert.That(buffed.BlotRemaining, Is.EqualTo(4).Within(.001));
            buffed.Health = 0;
            game.Tick(1f / 30);
            Assert.That(game.Blots.Count, Is.EqualTo(1));
            Assert.That(game.Blots[0].Remaining, Is.EqualTo(8).Within(.001));
        }

        [Test]
        public void MultipleCorpsesMergeAndOnlyFirstEnemyRefreshesANonStackingBuff()
        {
            var game = new GameSession(MechanicsBalance(4), starterDefenses: false);
            game.StartNextWave(); game.Tick(.02f);
            game.Enemies[0].Position = game.Enemies[1].Position = new Point2(5.5f, 5.5f);
            game.Enemies[0].Health = game.Enemies[1].Health = 0;
            game.Tick(1f / 30);
            Assert.That(game.Blots.Count, Is.EqualTo(1));
            game.Enemies[0].Position = game.Enemies[1].Position = new Point2(5.5f, 5.5f);
            game.Enemies[0].BlotRemaining = 2;
            game.Tick(1f / 30);
            Assert.That(game.Blots, Is.Empty);
            Assert.That(game.Enemies[0].BlotRemaining, Is.EqualTo(4).Within(.001));
            Assert.That(game.Enemies[1].BlotRemaining, Is.Zero);
            Assert.That(EnemyEffects.Defense(game.Enemies[0]), Is.EqualTo(.15f).Within(.001));
            Assert.That(EnemyEffects.MovementMultiplier(game.Enemies[0]), Is.EqualTo(1.15f).Within(.001));
        }

        [Test]
        public void BlotterPaysOnceClearsRadiusAndHonorsCooldown()
        {
            var game = new GameSession(MechanicsBalance(), starterDefenses: false);
            game.StartNextWave(); game.Tick(.02f);
            game.Enemies[0].Position = new Point2(5.5f, 5.5f);
            game.Enemies[1].Position = new Point2(7.5f, 5.5f);
            game.Enemies[0].Health = game.Enemies[1].Health = 0;
            game.Tick(1f / 30);
            Assert.That(game.Blots.Count, Is.EqualTo(2));
            int ink = game.Ink;
            Assert.That(game.TryBlotter(new Cell(5, 5), out _), Is.True);
            Assert.That(game.Ink, Is.EqualTo(ink - 8));
            Assert.That(game.Blots, Is.Empty);
            Assert.That(game.BlotterCooldownRemaining, Is.EqualTo(3));
            Assert.That(game.TryBlotter(new Cell(5, 5), out _), Is.False);
            Assert.That(game.Ink, Is.EqualTo(ink - 8));
            game.Tick(3.1f);
            Assert.That(game.BlotterCooldownRemaining, Is.Zero);
            Assert.That(game.TryBlotter(new Cell(5, 5), out _), Is.False);
            Assert.That(game.Ink, Is.EqualTo(ink - 8));
        }

        [Test]
        public void BlotsExpireAndPauseFreezesTheirLifetime()
        {
            var balance = MechanicsBalance(); balance.BlotLifetime = .1f;
            var game = new GameSession(balance, starterDefenses: false);
            game.StartNextWave(); game.Tick(.02f);
            game.Enemies[0].Position = new Point2(5.5f, 5.5f); game.Enemies[0].Health = 0;
            game.Tick(1f / 30);
            game.Tick(0);
            Assert.That(game.Blots[0].Remaining, Is.EqualTo(.1f).Within(.001));
            game.Tick(.12f);
            Assert.That(game.Blots, Is.Empty);
        }

        [Test]
        public void SpitterTelegraphsThenFirstCompletedBlockerAbsorbsGlob()
        {
            var balance = MechanicsBalance();
            var spitter = balance.Enemy(EnemyKind.Spitter);
            spitter.Damage = 10; spitter.Speed = 0; spitter.Range = 12.5f;
            spitter.WindupSeconds = .1f; spitter.ProjectileSeconds = .2f; spitter.AttackInterval = 10;
            balance.Structure(StructureKind.Wall).MaxHealth = 100;
            var game = new GameSession(balance, starterDefenses: false);
            game.TryBuild(new[] { new Cell(12, 12), new Cell(14, 12) }, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.StartNextWave(); game.Tick(.02f);
            var enemy = game.Enemies[0]; enemy.Kind = EnemyKind.Spitter; enemy.Position = new Point2(8.5f, 12.5f);
            game.Tick(1f / 30);
            Assert.That(enemy.WindupRemaining, Is.GreaterThan(0));
            Assert.That(game.EnemyProjectiles, Is.Empty);
            game.Tick(.11f);
            Assert.That(game.EnemyProjectiles.Count, Is.EqualTo(1));
            Assert.That(game.BaseHealth, Is.EqualTo(500));
            game.Tick(.25f);
            Assert.That(game.StructureAt(new Cell(12, 12)).Health, Is.EqualTo(90));
            Assert.That(game.StructureAt(new Cell(14, 12)).Health, Is.EqualTo(100));
            Assert.That(game.BaseHealth, Is.EqualTo(500));
            Assert.That(game.EnemyProjectiles, Is.Empty);
        }

        [Test]
        public void SpitterTargetsOuterWallAtRangeAndDoesNotHitBaseWhenThatWallVanishes()
        {
            var balance = MechanicsBalance(); balance.Width = 96; balance.Height = 64;
            var spitter = balance.Enemy(EnemyKind.Spitter);
            spitter.Damage = 10; spitter.Speed = 0; spitter.Range = 12.5f;
            spitter.WindupSeconds = .1f; spitter.ProjectileSeconds = .2f; spitter.AttackInterval = 10;
            var game = new GameSession(balance, starterDefenses: false);
            var wallCell = new Cell(20, 32);
            game.TryBuild(new[] { wallCell }, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.StartNextWave(); game.Tick(.02f);
            var enemy = game.Enemies[0]; enemy.Kind = EnemyKind.Spitter; enemy.Position = new Point2(8.5f, 32.5f);
            game.Tick(1f / 30);
            Assert.That(enemy.WindupRemaining, Is.GreaterThan(0), "Outer wall is within ranged distance even though the base is far away.");
            game.Tick(.11f);
            Assert.That(game.EnemyProjectiles.Count, Is.EqualTo(1));
            Assert.That(game.EnemyProjectiles[0].To.X, Is.EqualTo(20.5f));
            Assert.That(game.EnemyProjectiles[0].To.Y, Is.EqualTo(32.5f));
            Assert.That(game.EnemyProjectiles[0].ObjectiveIndex, Is.LessThan(-1));
            game.TryErase(wallCell, out _);
            game.Tick(.25f);
            Assert.That(game.BaseHealth, Is.EqualTo(500));
            Assert.That(game.EnemyProjectiles, Is.Empty);
        }

        [Test]
        public void UnfinishedWallDoesNotAbsorbGlobAndDeadSpitterShellPersists()
        {
            var balance = MechanicsBalance();
            var spitter = balance.Enemy(EnemyKind.Spitter);
            spitter.Damage = 10; spitter.Speed = 0; spitter.Range = 12.5f;
            spitter.WindupSeconds = .01f; spitter.ProjectileSeconds = .2f; spitter.AttackInterval = 10;
            balance.Structure(StructureKind.Wall).BuildSeconds = 2;
            var game = new GameSession(balance, starterDefenses: false);
            game.TryBuild(new[] { new Cell(14, 12) }, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.StartNextWave(); game.Tick(.02f);
            var enemy = game.Enemies[0]; enemy.Kind = EnemyKind.Spitter; enemy.Position = new Point2(8.5f, 12.5f);
            game.Tick(.07f);
            Assert.That(game.EnemyProjectiles.Count, Is.EqualTo(1));
            enemy.Health = 0;
            game.Tick(.25f);
            Assert.That(game.BaseHealth, Is.EqualTo(490));
            Assert.That(game.StructureAt(new Cell(14, 12)).IsComplete, Is.False);
            Assert.That(game.EnemyProjectiles, Is.Empty);
        }

        [Test]
        public void WallCompletedDuringFlightInterceptsTheApproachingGlob()
        {
            var balance = MechanicsBalance();
            var spitter = balance.Enemy(EnemyKind.Spitter);
            spitter.Damage = 10; spitter.Speed = 0; spitter.Range = 12.5f;
            spitter.WindupSeconds = .01f; spitter.ProjectileSeconds = .5f; spitter.AttackInterval = 10;
            var wall = balance.Structure(StructureKind.Wall); wall.BuildSeconds = .2f; wall.MaxHealth = 100;
            var game = new GameSession(balance, starterDefenses: false);
            game.TryBuild(new[] { new Cell(14, 12) }, StructureKind.Wall, MaterialTier.Pencil, out _);
            game.StartNextWave(); game.Tick(.02f);
            var enemy = game.Enemies[0]; enemy.Kind = EnemyKind.Spitter; enemy.Position = new Point2(8.5f, 12.5f);
            game.Tick(.07f);
            Assert.That(game.EnemyProjectiles.Count, Is.EqualTo(1));
            Assert.That(game.StructureAt(new Cell(14, 12)).IsComplete, Is.False);
            game.Tick(.5f);
            Assert.That(game.StructureAt(new Cell(14, 12)).IsComplete, Is.True);
            Assert.That(game.StructureAt(new Cell(14, 12)).Health, Is.EqualTo(90));
            Assert.That(game.BaseHealth, Is.EqualTo(500));
        }

        [Test]
        public void GlobKeepsItsWellTargetAfterTheShooterDies()
        {
            var balance = MechanicsBalance(); balance.EnableInkWells = true;
            var spitter = balance.Enemy(EnemyKind.Spitter);
            spitter.Damage = 10; spitter.Speed = 0;
            spitter.WindupSeconds = .01f; spitter.ProjectileSeconds = .1f; spitter.AttackInterval = 10;
            var game = new GameSession(balance, starterDefenses: false);
            game.TryActivateWell(0, out _);
            game.StartNextWave(); game.Tick(.02f);
            var enemy = game.Enemies[0]; enemy.Kind = EnemyKind.Spitter;
            enemy.ObjectiveIndex = 0; enemy.Position = new Point2(1.5f, game.Wells[0].Cell.Y + .5f);
            game.Tick(.07f);
            Assert.That(game.EnemyProjectiles.Count, Is.EqualTo(1));
            Assert.That(game.EnemyProjectiles[0].ObjectiveIndex, Is.Zero);
            enemy.Health = 0; game.Tick(.2f);
            Assert.That(game.Wells[0].Health, Is.EqualTo(balance.WellHealth - 10));
            Assert.That(game.BaseHealth, Is.EqualTo(500));
            Assert.That(game.EnemyProjectiles, Is.Empty);
        }
    }
}
