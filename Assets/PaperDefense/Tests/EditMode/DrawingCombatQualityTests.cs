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

        static int Launches(float quality, bool dual, int ticks)
        {
            var balance = GameBalance.CreateDefault();
            var tower = new StructureState { Id = 1, Cell = new Cell(10, 10), Kind = StructureKind.Gun,
                IsComplete = true, Health = 55, MaxHealth = 55, Blueprint = Authored(dual), PrimaryTargetId = 1, SecondaryTargetId = 1 };
            var enemies = new List<EnemyState> { new EnemyState { Id = 1, Kind = EnemyKind.Doodle,
                Position = new Point2(11.5f, 10.5f), Health = 100000, MaxHealth = 100000 } };
            var weapons = new WeaponSystem(balance, quality); var statistics = new RunStatistics();
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

        [TestCase(0, 1, 70, 114)]
        [TestCase(60, 1, 70, 114)]
        [TestCase(100, 1, 70, 114)]
        public void HistoricalQualityScoresNoLongerChangeCombatTempo(float quality, float factor, int single, int dual)
        {
            var balance = GameBalance.CreateDefault();
            Assert.That(TowerCatalog.QualityMultiplier(quality, balance), Is.EqualTo(factor).Within(.00001f));
            Assert.That(Launches(quality, false, 900), Is.EqualTo(single));
            Assert.That(Launches(quality, true, 900), Is.EqualTo(dual));
        }

        [Test] public void QualityIsAppliedAfterTheProductionLimitAndExactlyOnce()
        {
            var definition = new StructureDefinition { ShotInterval = .001f };
            Assert.That(TowerCatalog.ActionInterval(definition, 1.15f, .85f), Is.EqualTo(.05f / .85f).Within(.000001f));
            definition.ShotInterval = .42f;
            Assert.That(TowerCatalog.ActionInterval(definition, .8f, .94f), Is.EqualTo(.42f / .8f / .94f).Within(.000001f));
        }

        [Test] public void DrawingDoesNotChangeHealthDamagePriceRangeLoadOrConstructionTime()
        {
            var balance = GameBalance.CreateDefault(); var authored = Authored(true);
            var legacy = new TowerBlueprint("legacy", "Legacy double", ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun);
            Assert.That(TowerCatalog.QualityMultiplier(authored, balance), Is.EqualTo(1));
            Assert.That(TowerCatalog.QualityMultiplier(legacy, balance), Is.EqualTo(1));
            Assert.That(TowerCatalog.Cost(authored, balance, MaterialTier.Marker), Is.EqualTo(TowerCatalog.Cost(legacy, balance, MaterialTier.Marker)));
            Assert.That(TowerCatalog.Health(authored, balance, MaterialTier.Marker), Is.EqualTo(TowerCatalog.Health(legacy, balance, MaterialTier.Marker)));
            Assert.That(TowerCatalog.Load(authored), Is.EqualTo(TowerCatalog.Load(legacy)));
            Assert.That(TowerCatalog.BuildSeconds(authored, balance, MaterialTier.Marker), Is.EqualTo(TowerCatalog.BuildSeconds(legacy, balance, MaterialTier.Marker)));
            var target = new EnemyState { Id = 1, Position = new Point2(11.5f, 10.5f), Health = 100, MaxHealth = 100 };
            var tower = new StructureState { Id = 1, Cell = new Cell(10, 10), IsComplete = true, Health = 55,
                Kind = StructureKind.Gun, Blueprint = authored, PrimaryTargetId = 1, SecondaryTargetId = 1 };
            new WeaponSystem(balance).Fire(new List<StructureState> { tower }, new List<EnemyState> { target }, new List<ShotEvent>(),
                1, 1, 1f / 30, 1, new RunStatistics(), new SimulationEventBuffer());
            Assert.That(target.Health, Is.EqualTo(76), "Two normal 12-damage hits; quality affects cooldown only.");
            Assert.That(balance.Structure(StructureKind.Gun).Range, Is.EqualTo(11));
            Assert.That(tower.Cooldown, Is.EqualTo(.42f / .8f).Within(.000001f));
            Assert.That(tower.SecondaryCooldown, Is.EqualTo(tower.Cooldown));
        }

        [Test] public void QualityTrialUsesTheSameDrawingAssemblyBudgetAndSeedForBothLanes()
        {
            var blueprint = Authored(true); var balance = GameBalance.CreateDefault();
            var result = TrialRunner.CompareQuality(blueprint, balance, TrialScenario.Fast, 400, 99);
            Assert.That(result.First.DrawingQuality, Is.EqualTo(0)); Assert.That(result.Second.DrawingQuality, Is.EqualTo(100));
            Assert.That(result.First.QualityMultiplier, Is.EqualTo(1)); Assert.That(result.Second.QualityMultiplier, Is.EqualTo(1));
            Assert.That(result.First.Cost, Is.EqualTo(result.Second.Cost)); Assert.That(result.First.Copies, Is.EqualTo(result.Second.Copies));
            Assert.That(result.First.Seed, Is.EqualTo(result.Second.Seed));
            Assert.That(result.First.Frames[0].Towers[0].Blueprint, Is.SameAs(blueprint));
            Assert.That(result.Second.Frames[0].Towers[0].Blueprint, Is.SameAs(blueprint));
            Assert.That(balance.DrawingMinimumRate, Is.EqualTo(.85f));
            Assert.That(balance.Structure(StructureKind.Gun).Damage, Is.EqualTo(12));
        }
    }
}
