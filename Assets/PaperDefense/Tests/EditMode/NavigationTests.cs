using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class NavigationTests
    {
        [TestCase(4, 19, 4, 18)]
        [TestCase(16, 19, 16, 18)]
        [TestCase(4, 0, 4, 1)]
        [TestCase(16, 0, 16, 1)]
        [TestCase(0, 4, 1, 4)]
        [TestCase(19, 4, 18, 4)]
        public void EqualCostRoutesAdvanceTowardTheGoalInsteadOfFormingBorderRows(int x, int y, int nx, int ny)
        {
            var field = new WeightedFlowField(20, 20);
            field.Rebuild(new Cell(10, 10), new StructureState[400]);
            Assert.That(field.Next(new Cell(x, y)), Is.EqualTo(new Cell(nx, ny)),
                "The dominant direction toward the goal must win a tie, regardless of the entry side.");
        }

        [TestCase(4)] [TestCase(8)]
        public void NorthAndSouthEnemiesLeaveTheBorderBeforeConvergingOnTheCenter(int side)
        {
            var balance = NotebookTowerDefensePreset.Create();
            balance.EnableInkWells = false;
            balance.Enemy(EnemyKind.Doodle).Speed = 2;
            balance.Progression.EnemySpeedScaleMinimum = 1;
            balance.Progression.EnemySpeedScaleVariation = 0;
            balance.Waves = new List<WaveDefinition> { new WaveDefinition { Count = 12, SpawnInterval = .01f, Sides = side } };
            var game = new GameSession(balance, 17, false);
            game.StartNextWave();
            for (int tick = 0; tick < 150; tick++) game.Tick(1f / 30);
            Assert.That(game.Enemies.Count, Is.EqualTo(12));
            foreach (var enemy in game.Enemies)
            {
                float borderDistance = side == 4 ? balance.Height - enemy.Position.Y : enemy.Position.Y;
                Assert.That(borderDistance, Is.GreaterThan(2), "Every entry must advance into the map within five seconds.");
                Assert.That(game.Battlefield.IsWalkable(new Cell((int)enemy.Position.X, (int)enemy.Position.Y)), Is.True);
            }
        }

        [Test]
        public void SpatialQueriesMatchFullScanAtEdgesAndAfterDeaths()
        {
            var random = new Random(704);
            var enemies = new List<EnemyState>();
            for (int i = 0; i < 150; i++)
                enemies.Add(new EnemyState { Id = i + 1, Health = i % 7 == 0 ? 0 : 10,
                    Position = new Point2((float)random.NextDouble() * 20, (float)random.NextDouble() * 16) });
            enemies.Add(new EnemyState { Id = 151, Health = 1, Position = new Point2(0, 0) });
            enemies.Add(new EnemyState { Id = 152, Health = 1, Position = new Point2(20, 16) });
            var index = new EnemySpatialIndex(20, 16);
            var found = new List<EnemyState>();
            for (int pass = 0; pass < 2; pass++)
            {
                index.Rebuild(enemies);
                for (int query = 0; query < 60; query++)
                {
                    var center = new Point2((float)random.NextDouble() * 24 - 2,
                        (float)random.NextDouble() * 20 - 2);
                    float radius = query % 5 == 0 ? 0 : (float)random.NextDouble() * 12;
                    var expected = new List<int>();
                    foreach (var enemy in enemies)
                    {
                        float dx = enemy.Position.X - center.X, dy = enemy.Position.Y - center.Y;
                        if (enemy.Health > 0 && dx * dx + dy * dy <= radius * radius) expected.Add(enemy.Id);
                    }
                    index.Query(center, radius, found);
                    var actual = new List<int>();
                    foreach (var enemy in found) actual.Add(enemy.Id);
                    Assert.That(actual, Is.EquivalentTo(expected));
                }
                foreach (var enemy in enemies) if (enemy.Id % 3 == 0) enemy.Health = 0;
            }
        }

        [Test]
        public void FlowUsesRemainingHealthAndIgnoresUnfinishedBlockers()
        {
            var grid = new StructureState[12 * 10];
            var wall = new StructureState { Cell = new Cell(5, 5), Kind = StructureKind.Wall,
                Health = 200, MaxHealth = 200, IsComplete = true };
            grid[5 * 12 + 5] = wall;
            var field = new WeightedFlowField(12, 10);
            field.Rebuild(new Cell(8, 5), grid);
            Assert.That(field.Next(new Cell(4, 5)), Is.Not.EqualTo(new Cell(5, 5)));
            wall.Health = 1;
            field.Rebuild(new Cell(8, 5), grid);
            Assert.That(field.Next(new Cell(4, 5)), Is.EqualTo(new Cell(5, 5)));
            wall.Health = 200;
            wall.IsComplete = false;
            wall.BuildRemaining = 1;
            field.Rebuild(new Cell(8, 5), grid);
            Assert.That(field.Next(new Cell(4, 5)), Is.EqualTo(new Cell(5, 5)));
        }
    }
}
