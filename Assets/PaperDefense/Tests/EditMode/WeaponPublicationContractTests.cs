using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class WeaponPublicationContractTests
    {
        // Literal expectations from the existing one-step behavior, before extracting publication/range helpers.
        [TestCase(StructureKind.Gun, 1, 12, 1)]
        [TestCase(StructureKind.Piercer, 1, 36, 2)]
        [TestCase(StructureKind.Fan, 3, 30, 3)]
        [TestCase(StructureKind.Ricochet, 3, 36, 3)]
        [TestCase(StructureKind.Stapler, 1, 4, 1)]
        public void ImmediateWeaponsKeepHitOrderDamageAndPairedVisualEvents(StructureKind kind, int count, float damage, int affected)
        {
            var balance = GameBalance.CreateDefault();
            var tower = new StructureState { Id = 7, Cell = new Cell(10, 10), Kind = kind,
                Tier = MaterialTier.Pencil, IsComplete = true, Health = 55, Color = InkColor.Blue };
            var enemies = new List<EnemyState>
            {
                new EnemyState { Id = 1, Position = new Point2(10.5f,12.5f), Health = 100, MaxHealth = 100 },
                new EnemyState { Id = 2, Position = new Point2(10.5f,13.5f), Health = 100, MaxHealth = 100 },
                new EnemyState { Id = 3, Position = new Point2(11.5f,13.5f), Health = 100, MaxHealth = 100 }
            };
            var shots = new List<ShotEvent>(); var events = new SimulationEventBuffer(); var stats = new RunStatistics();
            new WeaponSystem(balance).Fire(new List<StructureState> { tower }, enemies, shots, 1, 1, 1f / 30, 42, stats, events);
            Assert.That(stats.Launches(kind), Is.EqualTo(1)); Assert.That(stats.Damage(kind), Is.EqualTo(damage));
            var shotEvents=events.Where(e=>e.Kind==SimulationEventKind.Shot).ToArray();
            Assert.That(shots.Count, Is.EqualTo(count)); Assert.That(shotEvents.Length, Is.EqualTo(count));
            Assert.That(events.Count(e=>e.Kind==SimulationEventKind.WeaponActionStarted),Is.EqualTo(1));
            Assert.That(tower.PrimaryTargetId, Is.EqualTo(1));
            for (int i = 0; i < enemies.Count; i++) Assert.That(enemies[i].BlueRemaining > 0, Is.EqualTo(i < affected));
            for (int i = 0; i < count; i++)
            {
                var shot = shots[i]; var item = shotEvents[i];
                float expectedX = i == 2 ? 11.5f : 10.5f;
                float expectedY = kind == StructureKind.Piercer ? 24.5f : i == 0 ? 12.5f : 13.5f;
                Assert.That(shot.To.X, Is.EqualTo(expectedX)); Assert.That(shot.To.Y, Is.EqualTo(expectedY));
                Assert.That(item.Sequence, Is.GreaterThan(i==0?0:shotEvents[i-1].Sequence)); Assert.That(item.Tick, Is.EqualTo(42));
                Assert.That(item.Kind, Is.EqualTo(SimulationEventKind.Shot)); Assert.That(item.EntityId, Is.EqualTo(7));
                Assert.That(item.Weapon, Is.EqualTo(kind)); Assert.That(item.From, Is.EqualTo(shot.From));
                Assert.That(item.To, Is.EqualTo(shot.To)); Assert.That(item.Radius, Is.EqualTo(shot.Radius));
            }
        }
    }
}
