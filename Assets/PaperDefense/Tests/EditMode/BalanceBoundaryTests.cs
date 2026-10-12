using System;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class BalanceBoundaryTests
    {
        [TestCase("base health")]
        [TestCase("simulation step")]
        [TestCase("drawing rate")]
        [TestCase("weapon damage")]
        [TestCase("material cost")]
        [TestCase("enemy speed")]
        [TestCase("wave interval")]
        [TestCase("progression rate")]
        public void SessionRejectsNonfiniteRulesAtItsBoundary(string field)
        {
            var balance = GameBalance.CreateDefault();
            switch (field)
            {
                case "base health": balance.BaseHealth = float.NaN; break;
                case "simulation step": balance.SimulationStep = float.PositiveInfinity; break;
                case "drawing rate": balance.DrawingMinimumRate = float.NaN; break;
                case "weapon damage": balance.Structure(StructureKind.Gun).Damage = float.NaN; break;
                case "material cost": balance.Material(MaterialTier.Ink).CostMultiplier = float.PositiveInfinity; break;
                case "enemy speed": balance.Enemy(EnemyKind.Doodle).Speed = float.NaN; break;
                case "wave interval": balance.Waves[0].SpawnInterval = float.NaN; break;
                case "progression rate": balance.Progression.RapidFireMultiplier = float.NaN; break;
            }
            Assert.That(() => new GameSession(balance, starterDefenses: false), Throws.InstanceOf<ArgumentException>());
        }

        [TestCase("missing weapon")]
        [TestCase("duplicate weapon")]
        [TestCase("unknown weapon")]
        [TestCase("null weapon")]
        [TestCase("missing material")]
        [TestCase("null material")]
        [TestCase("missing enemy")]
        [TestCase("duplicate enemy")]
        [TestCase("unknown enemy")]
        [TestCase("null wave")]
        [TestCase("null construction")]
        public void SessionRejectsIncompleteOrAmbiguousDefinitionsBeforeAllocation(string defect)
        {
            var balance = GameBalance.CreateDefault();
            switch (defect)
            {
                case "missing weapon": balance.StructureDefinitions.RemoveAt(1); break;
                case "duplicate weapon": balance.StructureDefinitions[1].Kind = StructureKind.Wall; break;
                case "unknown weapon": balance.StructureDefinitions[1].Kind = (StructureKind)999; break;
                case "null weapon": balance.StructureDefinitions[1] = null; break;
                case "missing material": balance.MaterialDefinitions.RemoveAt(1); break;
                case "null material": balance.MaterialDefinitions[1] = null; break;
                case "missing enemy": balance.EnemyDefinitions.RemoveAt(1); break;
                case "duplicate enemy": balance.EnemyDefinitions[1].Kind = EnemyKind.Doodle; break;
                case "unknown enemy": balance.EnemyDefinitions[1].Kind = (EnemyKind)999; break;
                case "null wave": balance.Waves[0] = null; break;
                case "null construction": balance.Construction = null; break;
            }
            Assert.That(() => new GameSession(balance, starterDefenses: false), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void BoundaryKeepsStationaryEnemiesEmptyWavesAndZeroDamageFixturesValid()
        {
            var balance = GameBalance.CreateDefault();
            balance.Waves.Clear();
            balance.Enemy(EnemyKind.Doodle).Speed = 0;
            balance.Enemy(EnemyKind.Doodle).Damage = 0;
            balance.Structure(StructureKind.Gun).Damage = 0;
            balance.Structure(StructureKind.Gun).BuildSeconds = 0;
            Assert.That(() => new GameSession(balance, starterDefenses: false), Throws.Nothing);
            var blueprint = new TowerBlueprint("empty-fixture", "Empty fixture", ChassisKind.Standard, StructureKind.Gun);
            Assert.That(() => TrialRunner.Run(blueprint, balance, TrialScenario.Crowd, budget: 0), Throws.Nothing);
            balance.Waves.Add(new WaveDefinition { Count = 0 });
            Assert.That(() => new GameSession(balance, starterDefenses: false), Throws.Nothing);
        }

        [Test]
        public void TrialRejectsNonfiniteDefinitionsBeforeRunningTheLane()
        {
            var balance = GameBalance.CreateDefault();
            balance.Structure(StructureKind.Gun).Damage = float.NaN;
            var blueprint = new TowerBlueprint("invalid-fixture", "Invalid fixture", ChassisKind.Standard, StructureKind.Gun);
            Assert.That(() => TrialRunner.Run(blueprint, balance, TrialScenario.Crowd, budget: 0),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void TrialReportsAnInvalidSourceDefinitionBeforeAttemptingItsDeepCopy()
        {
            var balance = GameBalance.CreateDefault(); balance.StructureDefinitions[1] = null;
            var blueprint = new TowerBlueprint("null-fixture", "Invalid fixture", ChassisKind.Standard, StructureKind.Gun);
            Assert.That(() => TrialRunner.Run(blueprint, balance, TrialScenario.Crowd, budget: 0),
                Throws.InstanceOf<ArgumentException>());
        }
    }
}
