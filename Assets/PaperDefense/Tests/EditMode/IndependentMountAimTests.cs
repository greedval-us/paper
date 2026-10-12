using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class IndependentMountAimTests
    {
        static StructureState Tower() => new StructureState
        {
            Id = 1, Cell = new Cell(10, 10), Kind = StructureKind.Gun, Tier = MaterialTier.Pencil,
            Health = 55, MaxHealth = 55, IsComplete = true, TargetId = 1,
            Blueprint = new TowerBlueprint("mixed", "Gun and pen", ChassisKind.Standard, StructureKind.Gun, StructureKind.Piercer)
        };
        static EnemyState Enemy(int id, float x, float y, float hp) => new EnemyState
        { Id = id, Position = new Point2(x, y), Health = hp, MaxHealth = hp, Kind = EnemyKind.Doodle };

        static void Fire(GameBalance balance, StructureState tower, List<EnemyState> enemies)
        {
            var weapons = new WeaponSystem(balance);
            weapons.Fire(new List<StructureState> { tower }, enemies, new List<ShotEvent>(),
                1, 1, 1f / 30, 1, new RunStatistics(), new SimulationEventBuffer());
        }

        [Test] public void EachMountRecordsTheTargetItActuallySelected()
        {
            var balance = GameBalance.CreateDefault(); balance.Structure(StructureKind.Gun).Damage = 100;
            var tower = Tower();
            var first = Enemy(1, 11.5f, 10.5f, 1); var second = Enemy(2, 10.5f, 15.5f, 100);
            Fire(balance, tower, new List<EnemyState> { first, second });
            Assert.That(first.Health, Is.Zero);
            Assert.That(tower.PrimaryAim.X, Is.EqualTo(first.Position.X));
            Assert.That(tower.PrimaryAim.Y, Is.EqualTo(first.Position.Y));
            Assert.That(tower.SecondaryAim.X, Is.EqualTo(second.Position.X));
            Assert.That(tower.SecondaryAim.Y, Is.EqualTo(second.Position.Y));
            Assert.That(tower.PrimaryTargetId, Is.EqualTo(first.Id));
            Assert.That(tower.SecondaryTargetId, Is.EqualTo(second.Id));
            Assert.That(tower.Aim.Y, Is.EqualTo(second.Position.Y), "Historical shared Aim remains the last launch for replay compatibility.");
        }

        [Test] public void CoolingMountTracksWithoutChangingDamageOrCooldown()
        {
            var tower = Tower(); tower.Cooldown = 1; tower.SecondaryCooldown = 2;
            var target = Enemy(1, 13.5f, 10.5f, 100);
            Fire(GameBalance.CreateDefault(), tower, new List<EnemyState> { target });
            Assert.That(tower.PrimaryAim.X, Is.EqualTo(target.Position.X));
            Assert.That(tower.SecondaryAim.X, Is.EqualTo(target.Position.X));
            Assert.That(target.Health, Is.EqualTo(100));
            Assert.That(tower.Cooldown, Is.EqualTo(1)); Assert.That(tower.SecondaryCooldown, Is.EqualTo(2));
        }

        [Test] public void MissingTargetClearsOnlyItsTrackingIdAndKeepsLastOrientation()
        {
            var tower = Tower(); tower.PrimaryAim = new Point2(7, 8); tower.SecondaryAim = new Point2(9, 11);
            tower.PrimaryTargetId = 42; tower.SecondaryTargetId = 43;
            Fire(GameBalance.CreateDefault(), tower, new List<EnemyState>());
            Assert.That(tower.PrimaryTargetId, Is.Zero); Assert.That(tower.SecondaryTargetId, Is.Zero);
            Assert.That(tower.PrimaryAim.X, Is.EqualTo(7)); Assert.That(tower.SecondaryAim.X, Is.EqualTo(9));
        }

        [Test] public void ReadSnapshotAndTrialRecordingPreserveBothDirections()
        {
            var tower = Tower(); tower.PrimaryAim = new Point2(2, 3); tower.SecondaryAim = new Point2(4, 5);
            tower.PrimaryTargetId = 7; tower.SecondaryTargetId = 8;
            var view = new StructureView(tower);
            Assert.That(view.PrimaryAim.X, Is.EqualTo(2)); Assert.That(view.SecondaryAim.X, Is.EqualTo(4));
            Assert.That(view.PrimaryTargetId, Is.EqualTo(7)); Assert.That(view.SecondaryTargetId, Is.EqualTo(8));
            var frame = new TrialFrame(0, new EnemyState[0], new[] { tower }, new MortarProjectile[0], new ShotEvent[0]);
            tower.PrimaryAim = new Point2(30, 30); tower.SecondaryAim = new Point2(40, 40);
            Assert.That(frame.Towers[0].PrimaryAim.X, Is.EqualTo(2));
            Assert.That(frame.Towers[0].SecondaryAim.X, Is.EqualTo(4));
        }
    }
}
