using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class MortarFootprintCombatTests
    {
        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void GeometryPreservesAreaAndNormalizedDirection(float q)
        {
            var shape = FootprintGeometry.Resolve(new MortarFootprint(q), 2, new Point2(1, 1), new Point2(4, 5));
            Assert.That(shape.ForwardRadius, Is.EqualTo(2 * Math.Sqrt(q)).Within(.000001));
            Assert.That(shape.SideRadius, Is.EqualTo(2 / Math.Sqrt(q)).Within(.000001));
            Assert.That(shape.ForwardRadius * shape.SideRadius, Is.EqualTo(4).Within(.000001));
            Assert.That(shape.Forward.X, Is.EqualTo(.6f).Within(.000001));
            Assert.That(shape.Forward.Y, Is.EqualTo(.8f).Within(.000001));
            Assert.That(shape.BoundingRadius, Is.EqualTo(shape.ForwardRadius));
        }
        [TestCase(false)] [TestCase(true)]
        public void EllipseIncludesAxisBoundaryAndRejectsBoundingCircleOnlyPoint(bool vertical)
        {
            var shape = FootprintGeometry.Resolve(new MortarFootprint(3), 2, default, vertical ? new Point2(0, 1) : new Point2(1, 0));
            var center = new Point2(10, 10);
            Point2 Point(float along, float across) => vertical ? new Point2(10 + across, 10 + along) : new Point2(10 + along, 10 + across);
            Assert.That(shape.Contains(center, Point(shape.ForwardRadius, 0)), Is.True);
            Assert.That(shape.Contains(center, Point(0, shape.SideRadius)), Is.True);
            Assert.That(shape.Contains(center, Point(0, 2)), Is.False);
            Assert.That(shape.Contains(center, Point(2, .5f)), Is.True);
        }
        [Test] public void ZeroDirectionAndRadiusAreExplicit()
        {
            var shape = FootprintGeometry.Resolve(new MortarFootprint(3), 0, default, default);
            Assert.That(shape.Forward, Is.EqualTo(new Point2(1, 0)));
            Assert.That(shape.Contains(default, default), Is.True);
            Assert.That(shape.Contains(default, new Point2(.0001f, 0)), Is.False);
        }
        [TestCase(-1)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidRadiusRejected(float radius) => Assert.Throws<ArgumentOutOfRangeException>(() => FootprintGeometry.Resolve(MortarFootprint.Circle, radius, default, default));
        [Test] public void NonfiniteCoordinatesRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FootprintGeometry.Resolve(MortarFootprint.Circle, 1, new Point2(float.NaN, 0), default));
            Assert.Throws<ArgumentOutOfRangeException>(() => FootprintGeometry.Resolve(MortarFootprint.Circle, 1, default, new Point2(0, float.PositiveInfinity)));
        }
        [Test] public void CircleKeepsExactOldDistanceBoundary()
        {
            var shape = FootprintGeometry.Resolve(MortarFootprint.Circle, 2, default, new Point2(1, 1));
            Assert.That(shape.Contains(default, new Point2(2, 0)), Is.True);
            Assert.That(shape.Contains(default, new Point2(2.000001f, 0)), Is.False);
            Assert.That(shape.Contains(default, new Point2(1.5f, 1.5f)), Is.False);
        }
        [TestCase(false)] [TestCase(true)]
        public void TwoMountsCaptureGeometryAndSharedColorThroughSourceRemovalOrRefit(bool removeSource)
        {
            var balance = GameBalance.CreateDefault();
            balance.Effects.LimitTowerColor = true; balance.Effects.ColorTargetsPerWindow = 1;
            var mortar = balance.Structure(StructureKind.Mortar);
            mortar.Radius = 2; mortar.Range = 20; mortar.Damage = 5; mortar.FlightSeconds = .5f;
            var tower = new StructureState { Id = 1, Cell = new Cell(10, 10), Kind = StructureKind.Mortar,
                Tier = MaterialTier.Pencil, IsComplete = true, Health = 100, Color = InkColor.Blue, PrimaryTargetId = 1, SecondaryTargetId = 1,
                Blueprint = new TowerBlueprint("dual", "Dual", ChassisKind.Heavy, StructureKind.Mortar, StructureKind.Mortar,
                    firstFootprint: MortarFootprint.Circle, secondFootprint: new MortarFootprint(3)) };
            var structures = new List<StructureState> { tower };
            // Target is closest to tower; the other points move into place during flight.
            var enemies = new List<EnemyState> { Enemy(1, 10.5f, 13.5f), Enemy(2, 10.5f, 17), Enemy(3, 14, 13.5f), Enemy(4, 14, 17) };
            var weapons = new WeaponSystem(balance); var stats = new RunStatistics(); var events = new SimulationEventBuffer();
            weapons.Fire(structures, enemies, new List<ShotEvent>(), 1, 1, .01f, 1, stats, events);
            Assert.That(weapons.Projectiles.Count, Is.EqualTo(2));
            Assert.That(weapons.Projectiles[0].ImpactFootprint.Value.ForwardRadius, Is.EqualTo(2));
            Assert.That(weapons.Projectiles[1].ImpactFootprint.Value.ForwardRadius, Is.EqualTo(2 * Math.Sqrt(3)).Within(.000001));
            var snapshot = new ProjectileView(weapons.Projectiles[1]);
            Assert.That(snapshot.ImpactFootprint, Is.EqualTo(events.Where(e=>e.Kind==SimulationEventKind.ProjectileLaunched).Last().ImpactFootprint));
            enemies[1].Position = new Point2(10.5f, 16.5f); // ellipse only
            enemies[2].Position = new Point2(12.3f, 13.5f); // circle only
            enemies[3].Position = new Point2(12.5f, 15.5f); // bounding circle only
            if (removeSource) { tower.Health = 0; structures.Clear(); }
            else { tower.Blueprint = tower.Blueprint.WithMortarFootprint(1, MortarFootprint.Circle); tower.SecondaryCooldown = tower.Cooldown = 100; }
            weapons.Fire(structures, enemies, new List<ShotEvent>(), 1, 1, .5f, 2, stats, events);
            Assert.That(enemies[0].Health, Is.EqualTo(90));
            Assert.That(enemies[1].Health, Is.EqualTo(95));
            Assert.That(enemies[2].Health, Is.EqualTo(95));
            Assert.That(enemies[3].Health, Is.EqualTo(100));
            Assert.That(stats.Damage(StructureKind.Mortar), Is.EqualTo(20));
            Assert.That(enemies.FindAll(e => e.BlueRemaining > 0).Count, Is.EqualTo(1));
            Assert.That(events.Where(e=>e.Kind==SimulationEventKind.ProjectileImpact).Last().ImpactFootprint, Is.EqualTo(snapshot.ImpactFootprint));
        }
        [Test] public void RealImpactIncludesTranslatedForwardAxisBoundary()
        {
            var balance = GameBalance.CreateDefault();
            var mortar = balance.Structure(StructureKind.Mortar);
            mortar.Radius = 2; mortar.Range = 20; mortar.Damage = 5; mortar.FlightSeconds = .5f;
            var tower = new StructureState { Id = 1, Cell = new Cell(10, 10), Kind = StructureKind.Mortar,
                Tier = MaterialTier.Pencil, IsComplete = true, Health = 100, PrimaryTargetId = 1,
                Blueprint = new TowerBlueprint("ellipse", "Ellipse", ChassisKind.Standard, StructureKind.Mortar,
                    firstFootprint: new MortarFootprint(3)) };
            var structures = new List<StructureState> { tower };
            var enemies = new List<EnemyState> { Enemy(1, 10.5f, 13.5f), Enemy(2, 10.5f, 20) };
            var weapons = new WeaponSystem(balance); var stats = new RunStatistics(); var events = new SimulationEventBuffer();
            weapons.Fire(structures, enemies, new List<ShotEvent>(), 1, 1, .01f, 1, stats, events);
            var geometry = weapons.Projectiles[0].ImpactFootprint.Value;
            enemies[1].Position = new Point2(10.5f, 13.5f + geometry.ForwardRadius);
            Assert.That(geometry.Contains(new Point2(10.5f, 13.5f), enemies[1].Position), Is.True);
            structures.Clear();
            weapons.Fire(structures, enemies, new List<ShotEvent>(), 1, 1, .5f, 2, stats, events);
            Assert.That(enemies[1].Health, Is.EqualTo(95), "Broad phase must not clip an included ellipse boundary after world-coordinate rounding.");
        }
        static EnemyState Enemy(int id, float x, float y) => new EnemyState { Id = id, Position = new Point2(x, y), Health = 100, MaxHealth = 100 };
    }
}
