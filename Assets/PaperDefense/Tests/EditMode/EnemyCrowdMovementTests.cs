using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class EnemyCrowdMovementTests
    {
        static GameBalance Main(int count, int sides = 15)
        {
            var b = NotebookTowerDefensePreset.Create();
            b.EnableInkWells = false;
            b.BaseHealth = 100000;
            b.Progression.EnemySpeedScaleMinimum = 1;
            b.Progression.EnemySpeedScaleVariation = 0;
            b.Waves = new List<WaveDefinition> { new WaveDefinition { Count = count, Sides = sides, SpawnInterval = .01f } };
            return b;
        }

        [TestCase(4)] [TestCase(8)]
        public void MainEntrancesAdmitWideGroupsWithSpaceBetweenBodies(int side)
        {
            var b = Main(24, side);
            b.Enemy(EnemyKind.Doodle).Speed = 0;
            var game = new GameSession(b, 17, false);
            game.StartNextWave();
            for (int i = 0; i < 60; i++) game.Tick(1f / 30);
            Assert.That(game.Enemies.Count, Is.EqualTo(24), "Spawn bands must use the space beside entrance markers.");
            AssertSeparated(game, .60f);
            var columns = new HashSet<int>();
            foreach (var e in game.Enemies) columns.Add((int)e.Position.X);
            Assert.That(columns.Count, Is.GreaterThanOrEqualTo(20));
        }

        [Test]
        public void MixedCrowdStaysSeparatedWhileAdvancingAroundMainMapTerrain()
        {
            var b = Main(100);
            b.Waves[0].RunnerFraction = .25f;
            b.Waves[0].BruteFraction = .2f;
            var game = new GameSession(b, 47, false);
            game.StartNextWave();
            bool reachedInterior = false;
            for (int tick = 0; tick < 1200; tick++)
            {
                game.Tick(1f / 30);
                AssertSeparated(game, .60f);
                foreach (var e in game.Enemies)
                {
                    Assert.That(game.Battlefield.IsWalkable(new Cell((int)e.Position.X, (int)e.Position.Y)), Is.True);
                    if (e.Position.X > 30 && e.Position.X < 65 && e.Position.Y > 20 && e.Position.Y < 44) reachedInterior = true;
                }
            }
            Assert.That(game.RemainingToSpawn, Is.Zero);
            Assert.That(reachedInterior, Is.True);
            Assert.That(game.BaseHealth, Is.LessThan(b.BaseHealth), "Avoidance must retain progress into combat.");
        }

        [TestCase(EnemyKind.Brute)] [TestCase(EnemyKind.Spitter)]
        public void MovingEnemyPassesAStationaryLargerNeighborWithoutOscillating(EnemyKind kind)
        {
            var b = Main(0); b.Battlefield = null;
            var game = GameSession.CreatePrepared(b, 17, new[] {
                new PreparedEnemy(new Point2(66.94f, 33.5f), 100, kind),
                new PreparedEnemy(new Point2(68.65f, 33.58f), 100, stationary: false) });
            // Start with enough room for the actual 4x Brute body.
            game.Enemies[1].Position = new Point2(69.15f, 33.58f);
            for (int i = 0; i < 450; i++) { game.Tick(1f / 30); AssertSeparated(game, .60f); }
            Assert.That(game.Enemies[1].Position.X, Is.LessThan(65), "A moving unit must flank a stationary attacker.");
        }

        [Test]
        public void MovingEnemyFlanksAnAttackerFacingAnotherObjective()
        {
            var b = Main(0); b.EnableInkWells = true; b.StartingInk = 10000;
            b.Structure(StructureKind.Wall).BuildSeconds = 0;
            b.Structure(StructureKind.RepairPost).BuildSeconds = 0;
            var game = GameSession.CreatePrepared(b, 17, new[] {
                new PreparedEnemy(new Point2(24.5f, 16.5f), 100, EnemyKind.Spitter, stationary: false),
                new PreparedEnemy(new Point2(23.3f, 15), 100, stationary: false) });
            Assert.That(game.TryActivateWell(0, out _), Is.True);
            game.Enemies[0].ObjectiveIndex = 0;
            Assert.That(game.TryBuild(new[] { new Cell(22, 16) }, StructureKind.Wall, MaterialTier.Pencil, out _), Is.True);
            Assert.That(game.TryBuild(new[] { new Cell(22, 19) }, StructureKind.RepairPost, MaterialTier.Pencil, out _), Is.True);
            for (int i = 0; i < 450; i++) { game.Tick(1f / 30); AssertSeparated(game, .60f); }
            Assert.That(game.Enemies[0].Position, Is.EqualTo(new Point2(24.5f, 16.5f)));
            Assert.That(game.Enemies[1].Position.X, Is.GreaterThan(28), "A stationary attacker cannot claim merge right of way forever.");
        }

        [Test]
        public void NarrowPassageAndBarricadeRetainProgressWithoutTerrainClipping()
        {
            var b = Main(12, 1); b.Width = 32; b.Height = 20; b.StartingInk = 1000;
            b.Battlefield = new BattlefieldDefinition { Enabled = true, Mode = BattlefieldMode.OpenTerrain };
            b.Battlefield.Entrances.Add(new BattlefieldCell(0, 9));
            b.Battlefield.Props.Add(new TerrainPropDefinition { X = 10, Y = 0, Width = 1, Height = 9 });
            b.Battlefield.Props.Add(new TerrainPropDefinition { X = 10, Y = 10, Width = 1, Height = 10 });
            b.Structure(StructureKind.Wall).BuildSeconds = 0;
            b.Structure(StructureKind.Wall).MaxHealth = 30;
            var game = new GameSession(b, 17, false);
            Assert.That(game.TryBuild(new[] { new Cell(10, 9) }, StructureKind.Wall, MaterialTier.Pencil, out var reason), Is.True, reason);
            game.StartNextWave();
            for (int i = 0; i < 1800; i++)
            {
                game.Tick(1f / 30); AssertSeparated(game, .60f);
                foreach (var enemy in game.Enemies)
                    Assert.That(game.Battlefield.IsWalkable(new Cell((int)enemy.Position.X, (int)enemy.Position.Y)), Is.True);
            }
            Assert.That(game.RemainingToSpawn, Is.Zero);
            Assert.That(game.StructureAt(new Cell(10, 9)), Is.Null, "A closed gap must be breached.");
            Assert.That(game.BaseHealth, Is.LessThan(b.BaseHealth));
            foreach (var enemy in game.Enemies) Assert.That(enemy.Position.X, Is.GreaterThan(10), "The queue must drain through the gap.");
        }

        [Test]
        public void CrowdsReplayDeterministicallyAndOwnTheirConfiguration()
        {
            var b = Main(80);
            var first = new GameSession(b, 381, false); var second = new GameSession(b, 381, false);
            b.Crowd.RadiusPerVisualScale = 9;
            first.Balance.Crowd.RadiusPerVisualScale = 6;
            first.StartNextWave(); second.StartNextWave();
            for (int i = 0; i < 600; i++) { first.Tick(1f / 30); second.Tick(1f / 30); }
            Assert.That(first.Enemies.Count, Is.EqualTo(second.Enemies.Count));
            for (int i = 0; i < first.Enemies.Count; i++) Assert.That(first.Enemies[i].Position, Is.EqualTo(second.Enemies[i].Position));
        }

        [Test]
        public void SweptBodiesCannotTunnelThroughNeighborsEvenAtHighSpeed()
        {
            var rules = new CrowdMovementRules { Enabled = true };
            var motion = new EnemyCrowdMotion(32, 20, rules);
            var mover = new EnemyState { Id = 1, Position = new Point2(2, 5), Health = 10 };
            var blocker = new EnemyState { Id = 2, Position = new Point2(5, 5), Health = 10 };
            motion.Rebuild(new[] { mover, blocker });
            motion.Steer(mover, new Point2(1, 0), 8);
            Assert.That(motion.CanMove(mover, new Point2(10, 5)), Is.False);
            Assert.That(motion.CanMove(mover, new Point2(10, 8)), Is.True);
            blocker.Position = new Point2(7, 9); motion.Moved(blocker, new Point2(5, 5));
            motion.Steer(mover, new Point2(1, 0), 8);
            Assert.That(motion.CanMove(mover, new Point2(10, 5)), Is.True);
        }

        [Test]
        public void CompletingAdjacentWallCannotTrapAnEnemyInsideItsClearance()
        {
            var b = Main(0); b.Battlefield = null; b.StartingInk = 1000;
            b.Structure(StructureKind.Wall).BuildSeconds = .01f;
            var game = GameSession.CreatePrepared(b, 17, new[] { new PreparedEnemy(new Point2(8, 20.5f), 100, stationary: false) });
            Assert.That(game.TryBuild(new[] { new Cell(9, 20) }, StructureKind.Wall, MaterialTier.Pencil, out _), Is.True);
            game.Enemies[0].Position = new Point2(10.04f, 20.5f);
            for (int i = 0; i < 150; i++) game.Tick(1f / 30);
            Assert.That(game.Enemies[0].Position.X, Is.GreaterThan(11));
            Assert.That(game.StructureAt(new Cell(9, 20)).IsComplete, Is.True);
        }

        static void AssertSeparated(GameSession game, float diameterPerScale)
        {
            for (int i = 0; i < game.Enemies.Count; i++)
                for (int j = i + 1; j < game.Enemies.Count; j++)
                {
                    var a = game.Enemies[i]; var b = game.Enemies[j];
                    float dx = a.Position.X - b.Position.X, dy = a.Position.Y - b.Position.Y;
                    float minimum = (a.VisualScale + b.VisualScale) * diameterPerScale * .5f;
                    if (dx * dx + dy * dy < minimum * minimum - .001f)
                        Assert.Fail("Enemy bodies overlap: " + a.Id + " / " + b.Id);
                }
        }
    }
}
