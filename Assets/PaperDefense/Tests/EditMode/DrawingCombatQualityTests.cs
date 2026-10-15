using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class DrawingCombatQualityTests
    {
        static TowerBlueprint Authored(bool dual = false)
            => new TowerBlueprint("quality", "Decoration only", ChassisKind.Standard, StructureKind.Gun,
                dual ? (StructureKind?)StructureKind.Gun : null,
                drawing: new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Decoration, new[] {
                    new DrawingStroke(new[] { new DrawingPoint(-.8f, -.8f), new DrawingPoint(-.7f, -.7f) }) }) }),
                origin: DrawingOrigin.PlayerDrawing);

        static int Launches(bool authored, bool dual, int ticks)
        {
            var balance = GameBalance.CreateDefault();
            var tower = new StructureState { Id = 1, Cell = new Cell(10, 10), Kind = StructureKind.Gun,
                IsComplete = true, Health = 55, MaxHealth = 55, Blueprint = authored ? Authored(dual) : new TowerBlueprint("standard", "Standard", ChassisKind.Standard, StructureKind.Gun, dual ? (StructureKind?)StructureKind.Gun : null), PrimaryTargetId = 1, SecondaryTargetId = 1 };
            var enemies = new List<EnemyState> { new EnemyState { Id = 1, Kind = EnemyKind.Doodle,
                Position = new Point2(11.5f, 10.5f), Health = 100000, MaxHealth = 100000 } };
            var weapons = new WeaponSystem(balance); var statistics = new RunStatistics();
            var shots = new List<ShotEvent>(); var events = new SimulationEventBuffer();
            const float delta = 1f / 30;
            for (int tick = 0; tick < ticks; tick++)
            {
                tower.Cooldown = Math.Max(0, tower.Cooldown - delta);
                tower.SecondaryCooldown = Math.Max(0, tower.SecondaryCooldown - delta);
                shots.Clear(); weapons.Fire(new List<StructureState> { tower }, enemies, shots, 1, 1, delta, tick, statistics, events);
            }
            return statistics.Launches(StructureKind.Gun);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ArchivedDrawingAndStandardAssemblyHaveTheSameCombatTempo(bool authored)
        {
            Assert.That(Launches(authored, false, 900), Is.EqualTo(70));
            Assert.That(Launches(authored, true, 900), Is.EqualTo(114));
        }

        [Test] public void ProductionIntervalObeysItsFloor()
        {
            var definition = new StructureDefinition { ShotInterval = .001f };
            Assert.That(TowerCatalog.ActionInterval(definition, 1.15f), Is.EqualTo(.05f).Within(.000001f));
            definition.ShotInterval = .42f;
            Assert.That(TowerCatalog.ActionInterval(definition, .8f), Is.EqualTo(.42f / .8f).Within(.000001f));
        }

        [Test] public void DrawingDoesNotChangeHealthDamagePriceRangeLoadOrConstructionTime()
        {
            var balance = GameBalance.CreateDefault(); var authored = Authored(true);
            var legacy = new TowerBlueprint("legacy", "Legacy double", ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun);

            Assert.That(TowerCatalog.Cost(authored, balance, MaterialTier.Marker), Is.EqualTo(TowerCatalog.Cost(legacy, balance, MaterialTier.Marker)));
            Assert.That(TowerCatalog.Health(authored, balance, MaterialTier.Marker), Is.EqualTo(TowerCatalog.Health(legacy, balance, MaterialTier.Marker)));
            Assert.That(TowerCatalog.Load(authored), Is.EqualTo(TowerCatalog.Load(legacy)));
            Assert.That(TowerCatalog.BuildSeconds(authored, balance, MaterialTier.Marker), Is.EqualTo(TowerCatalog.BuildSeconds(legacy, balance, MaterialTier.Marker)));
            var target = new EnemyState { Id = 1, Position = new Point2(11.5f, 10.5f), Health = 100, MaxHealth = 100 };
            var tower = new StructureState { Id = 1, Cell = new Cell(10, 10), IsComplete = true, Health = 55,
                Kind = StructureKind.Gun, Blueprint = authored, PrimaryTargetId = 1, SecondaryTargetId = 1 };
            new WeaponSystem(balance).Fire(new List<StructureState> { tower }, new List<EnemyState> { target }, new List<ShotEvent>(),
                1, 1, 1f / 30, 1, new RunStatistics(), new SimulationEventBuffer());
            Assert.That(target.Health, Is.EqualTo(76), "Two normal 12-damage hits; archived quality must not affect damage or cooldown.");
            Assert.That(balance.Structure(StructureKind.Gun).Range, Is.EqualTo(11));
            Assert.That(tower.Cooldown, Is.EqualTo(.42f / .8f).Within(.000001f));
            Assert.That(tower.SecondaryCooldown, Is.EqualTo(tower.Cooldown));
        }

    }
}
