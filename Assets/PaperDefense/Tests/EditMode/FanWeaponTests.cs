using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class FanWeaponTests
    {
        static GameBalance Balance()
        {
            var balance = GameBalance.CreateDefault(); var fan = balance.Structure(StructureKind.Fan);
            fan.Range = 6; fan.Damage = 10; fan.ShotInterval = 1; fan.ConeAngleDegrees = 90; fan.MaxTargets = 8;
            return balance;
        }
        static EnemyState Enemy(int id, float x, float y, EnemyKind kind = EnemyKind.Doodle)
            => new EnemyState { Id = id, Kind = kind, Position = new Point2(x, y), Health = 100, MaxHealth = 100 };
        static StructureState Tower(TowerBlueprint blueprint = null)
            => new StructureState { Id = 1, Cell = new Cell(10, 10), Kind = StructureKind.Fan,
                Health = 55, IsComplete = true, Tier = MaterialTier.Pencil, TargetId = 1,
                Blueprint = blueprint ?? new TowerBlueprint("fan", "Fan", ChassisKind.Standard, StructureKind.Fan) };
        static RunStatistics Fire(GameBalance balance, StructureState tower, List<EnemyState> enemies, out List<ShotEvent> shots)
        {
            var stats = new RunStatistics(); shots = new List<ShotEvent>();
            new WeaponSystem(balance).Fire(new List<StructureState> { tower }, enemies, shots,
                1, 1, 1f / 30, 1, stats, new SimulationEventBuffer());
            return stats;
        }

        [Test] public void FanHitsOnlyItsFiniteConeIncludingTheRangeAndAngleBoundaries()
        {
            var tower = Tower(); tower.Priority = TargetPriority.Siege;
            var enemies = new List<EnemyState> { Enemy(1, 12.5f, 10.5f, EnemyKind.Gnawer),
                Enemy(2, 12.5f, 12.5f), Enemy(3, 12.5f, 12.6f), Enemy(4, 9.5f, 10.5f),
                Enemy(5, 16.5f, 10.5f), Enemy(6, 16.6f, 10.5f) };
            var stats = Fire(Balance(), tower, enemies, out var shots);
            Assert.That(enemies[0].Health, Is.EqualTo(90)); Assert.That(enemies[1].Health, Is.EqualTo(90));
            Assert.That(enemies[2].Health, Is.EqualTo(100)); Assert.That(enemies[3].Health, Is.EqualTo(100));
            Assert.That(enemies[4].Health, Is.EqualTo(90)); Assert.That(enemies[5].Health, Is.EqualTo(100));
            Assert.That(stats.Damage(StructureKind.Fan), Is.EqualTo(30));
            Assert.That(stats.Launches(StructureKind.Fan), Is.EqualTo(1));
            Assert.That(shots.Count, Is.EqualTo(3), "Every rendered ray corresponds to one actual cone hit.");
        }

        [Test] public void FanTargetLimitUsesDistanceThenIdRegardlessOfEnemyListOrder()
        {
            var balance = Balance(); balance.Structure(StructureKind.Fan).MaxTargets = 2;
            var third = Enemy(3, 12.5f, 9.5f); var second = Enemy(2, 12.5f, 11.5f); var first = Enemy(1, 11.5f, 10.5f);
            Fire(balance, Tower(), new List<EnemyState> { third, second, first }, out _);
            Assert.That(first.Health, Is.EqualTo(90)); Assert.That(second.Health, Is.EqualTo(90));
            Assert.That(third.Health, Is.EqualTo(100));
        }

        [Test] public void FanColorBudgetLimitsEffectsWhileAllConeHitsUseNormalDefenseAndDamage()
        {
            var balance = Balance(); balance.EnableEnemyMechanics = true; balance.Effects.LimitTowerColor = true;
            balance.Effects.HatchedDefense = .2f; var tower = Tower(); tower.Color = InkColor.Blue;
            var enemies = new List<EnemyState>();
            for (int i = 0; i < 5; i++) { var enemy = Enemy(i + 1, 12.5f, 10.5f + i * .1f); enemy.DrawingTier = DrawingTier.Hatched; enemies.Add(enemy); }
            var stats = Fire(balance, tower, enemies, out _); int colored = 0;
            foreach (var enemy in enemies) { Assert.That(enemy.Health, Is.EqualTo(92)); if (enemy.BlueRemaining > 0) colored++; }
            Assert.That(colored, Is.EqualTo(3)); Assert.That(stats.Damage(StructureKind.Fan), Is.EqualTo(40));
        }

        [Test] public void GunAndFanKeepIndependentTargetsAndAttackGeometryOnOneFoundation()
        {
            var balance = Balance(); balance.Construction.DynamicFeed = true;
            balance.Structure(StructureKind.Gun).Range = 2; balance.Structure(StructureKind.Gun).Damage = 4;
            balance.Structure(StructureKind.Fan).Damage = 7;
            var blueprint = new TowerBlueprint("gun-fan", "Gun and fan", ChassisKind.Standard, StructureKind.Gun, StructureKind.Fan);
            var tower = Tower(blueprint); tower.Kind = StructureKind.Gun; tower.Priority = TargetPriority.Siege;
            var far = Enemy(1, 14.5f, 10.5f, EnemyKind.Gnawer); var near = Enemy(2, 11.5f, 10.5f); near.Health = 4;
            var side = Enemy(3, 13.5f, 11.5f);
            var stats = Fire(balance, tower, new List<EnemyState> { far, near, side }, out var shots);
            Assert.That(near.Health, Is.Zero); Assert.That(far.Health, Is.EqualTo(93)); Assert.That(side.Health, Is.EqualTo(93));
            Assert.That(tower.PrimaryTargetId, Is.EqualTo(2)); Assert.That(tower.SecondaryTargetId, Is.EqualTo(1));
            Assert.That(tower.PrimaryAim.X, Is.EqualTo(11.5f)); Assert.That(tower.SecondaryAim.X, Is.EqualTo(14.5f));
            Assert.That(tower.Cooldown, Is.EqualTo(.42f / .8f).Within(.00001f));
            Assert.That(tower.SecondaryCooldown, Is.EqualTo(1 / .8f).Within(.00001f));
            Assert.That(stats.Damage(StructureKind.Gun), Is.EqualTo(4)); Assert.That(stats.Damage(StructureKind.Fan), Is.EqualTo(14));
            Assert.That(shots.Count, Is.EqualTo(3));
        }

        [Test] public void RealTrialUsesConfiguredFanAngleAndTargetLimitAtTheSameBudget()
        {
            var balance = Balance(); balance.Structure(StructureKind.Fan).Cost = 70;
            var blueprint = new TowerBlueprint("trial-fan", "Fan", ChassisKind.Standard, StructureKind.Fan);
            var fan = balance.Structure(StructureKind.Fan); fan.ConeAngleDegrees = 1; fan.MaxTargets = 1;
            var narrow = TrialRunner.Run(blueprint, balance, TrialScenario.Crowd, 140);
            fan.ConeAngleDegrees = 120; fan.MaxTargets = 8;
            var wide = TrialRunner.Run(blueprint, balance, TrialScenario.Crowd, 140);
            Assert.That(narrow.Copies, Is.EqualTo(2)); Assert.That(wide.Cost, Is.EqualTo(140));
            Assert.That(narrow.Damage, Is.GreaterThan(0)); Assert.That(wide.Damage, Is.GreaterThan(narrow.Damage));
            Assert.That(balance.Structure(StructureKind.Fan).MaxTargets, Is.EqualTo(8));
        }

        [Test] public void NewWeaponStatisticsIncludeDamageLaunchesIdleAndMaterialBuildCounts()
        {
            var stats = new RunStatistics();
            Assert.DoesNotThrow(() => { stats.RecordLaunch(StructureKind.Fan); stats.RecordDamage(StructureKind.Fan, 3);
                stats.RecordIdle(StructureKind.Fan, .5f); stats.RecordBuild(70, 1, StructureKind.Fan, MaterialTier.Marker); });
            Assert.That(stats.Launches(StructureKind.Fan), Is.EqualTo(1)); Assert.That(stats.Damage(StructureKind.Fan), Is.EqualTo(3));
            Assert.That(stats.IdleSeconds(StructureKind.Fan), Is.EqualTo(.5f));
            Assert.That(stats.BuiltCount(StructureKind.Fan, MaterialTier.Marker), Is.EqualTo(1));
        }
    }
}
