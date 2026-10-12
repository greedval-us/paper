using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class EffectsRulesTests
    {
        static EnemyState Enemy(DrawingTier tier = DrawingTier.Hatched) => new EnemyState
        { Id = 1, Kind = EnemyKind.Doodle, DrawingTier = tier, Health = 100, MaxHealth = 100, Position = new Point2(6.5f, 5.5f) };

        [Test] public void ConfiguredDrawingHealthAndDefenseAffectTheActualHit()
        {
            var rules = new EffectsRules { OutlinedHealthMultiplier = 2, HatchedHealthMultiplier = 3,
                OutlinedDefense = .3f, HatchedDefense = .7f, MaximumDefense = .6f };
            Assert.That(EnemyEffects.DrawingHealthMultiplier(DrawingTier.Outlined, rules), Is.EqualTo(2));
            Assert.That(EnemyEffects.DrawingHealthMultiplier(DrawingTier.Hatched, rules), Is.EqualTo(3));
            var enemy = Enemy(); enemy.BlotRemaining = 1;
            float damage = EnemyEffects.ApplyHit(enemy, 100, InkColor.None, new RunStatistics(), StructureKind.Gun, rules);
            Assert.That(damage, Is.EqualTo(40).Within(.0001f));
            Assert.That(enemy.Health, Is.EqualTo(60).Within(.0001f));
        }

        [Test] public void ConfiguredGreenReducesDefenseOnItsOwnHitAndExpires()
        {
            var rules = new EffectsRules { HatchedDefense = .7f, GreenDefenseReduction = .5f, GreenDuration = .75f };
            var enemy = Enemy();
            Assert.That(EnemyEffects.ApplyHit(enemy, 10, InkColor.Green, new RunStatistics(), StructureKind.Gun, rules), Is.EqualTo(8).Within(.0001f));
            Assert.That(enemy.GreenRemaining, Is.EqualTo(.75f));
            EnemyEffects.Advance(enemy, 1, new RunStatistics(), rules);
            Assert.That(EnemyEffects.ApplyHit(enemy, 10, InkColor.None, new RunStatistics(), StructureKind.Gun, rules), Is.EqualTo(3).Within(.0001f));
        }

        [Test] public void ConfiguredBlueCapsMovementForOnlyItsConfiguredDuration()
        {
            var rules = new EffectsRules { BlueDuration = 1.25f, BlueSpeedMultiplier = .4f };
            var enemy = Enemy(); enemy.BlotRemaining = 4;
            EnemyEffects.ApplyHit(enemy, 0, InkColor.Blue, new RunStatistics(), StructureKind.Gun, rules);
            EnemyEffects.Advance(enemy, 1.2f, new RunStatistics(), rules);
            Assert.That(EnemyEffects.MovementMultiplier(enemy, rules, .2f), Is.EqualTo(.4f));
            EnemyEffects.Advance(enemy, .1f, new RunStatistics(), rules);
            Assert.That(EnemyEffects.MovementMultiplier(enemy, rules, .2f), Is.EqualTo(1.2f).Within(.0001f));
        }

        [TestCase(true, 91f)]
        [TestCase(false, 95.5f)]
        public void ConfiguredRedTicksHaveAnExplicitDefenseRule(bool ignoresDefense, float health)
        {
            var rules = new EffectsRules { RedDuration = 1.2f, RedTickSeconds = .4f, RedDamagePerTick = 3,
                RedIgnoresDefense = ignoresDefense, HatchedDefense = .5f };
            var enemy = Enemy(); var statistics = new RunStatistics();
            EnemyEffects.ApplyHit(enemy, 0, InkColor.Red, statistics, StructureKind.Mortar, rules);
            EnemyEffects.Advance(enemy, 10, statistics, rules);
            Assert.That(enemy.Health, Is.EqualTo(health).Within(.0001f));
            Assert.That(statistics.Damage(StructureKind.Mortar), Is.EqualTo(100 - health).Within(.0001f));
            EnemyEffects.Advance(enemy, 10, statistics, rules);
            Assert.That(enemy.Health, Is.EqualTo(health).Within(.0001f));
        }

        [Test] public void BalanceSnapshotOwnsItsEffectRulesAndLaterMutationCannotChangeMovement()
        {
            var source = GameBalance.CreateDefault(); source.Effects.BlueSpeedMultiplier = .4f;
            GameBalance copy = source.CreateSnapshot(); source.Effects.BlueSpeedMultiplier = .1f;
            var enemy = Enemy(); enemy.BlueRemaining = 1;
            Assert.That(EnemyEffects.MovementMultiplier(enemy, copy.Effects), Is.EqualTo(.4f));
            copy.Effects.BlueSpeedMultiplier = .8f;
            Assert.That(source.Effects.BlueSpeedMultiplier, Is.EqualTo(.1f));
        }

        [Test] public void EffectSnapshotRejectsANonFiniteTickInterval()
        {
            var rules = new EffectsRules { RedTickSeconds = float.NaN };
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.CreateSnapshot());
        }

        [Test] public void ProductionWeaponUsesTheSameConfiguredGreenAndDefenseRules()
        {
            var balance = GameBalance.CreateDefault(); balance.EnableEnemyMechanics = true;
            balance.Effects.HatchedDefense = .7f; balance.Effects.GreenDefenseReduction = .5f;
            balance.Structure(StructureKind.Gun).Damage = 10;
            var tower = new StructureState { Id = 1, Cell = new Cell(5, 5), Kind = StructureKind.Gun,
                Tier = MaterialTier.Pencil, Color = InkColor.Green, Health = 55, IsComplete = true };
            var enemy = Enemy();
            new WeaponSystem(balance).Fire(new List<StructureState> { tower }, new List<EnemyState> { enemy },
                new List<ShotEvent>(), 1, 1, 1f / 30, 1, new RunStatistics(), new SimulationEventBuffer(8));
            Assert.That(enemy.Health, Is.EqualTo(92).Within(.0001f));
        }

        [Test] public void TrialUsesConfiguredDefenseWithoutReplacingItsScenarioHealth()
        {
            var balance = GameBalance.CreateDefault(); balance.EnableEnemyMechanics = true;
            balance.Effects.HatchedDefense = .7f; balance.Effects.GreenDefenseReduction = .5f;
            var weapon = balance.Structure(StructureKind.Gun); weapon.Damage = 10; weapon.ShotInterval = 1000; weapon.Range = 100;
            var blueprint = new TowerBlueprint("effects", "Configured effects", ChassisKind.Standard, StructureKind.Gun);
            var report = TrialRunner.Run(blueprint, balance, TrialScenario.Armored, 40, color: InkColor.Green);
            Assert.That(report.Damage, Is.EqualTo(8).Within(.0001f));
            Assert.That(report.Frames[1].Enemies[0].MaxHealth, Is.EqualTo(140));
        }

        [Test] public void TrialUsesConfiguredBlueMovementFromTheProvidedBalance()
        {
            var balance = GameBalance.CreateDefault(); balance.EnableEnemyMechanics = true;
            var weapon = balance.Structure(StructureKind.Gun); weapon.Damage = 1; weapon.ShotInterval = 1000; weapon.Range = 100;
            balance.Effects.BlueDuration = 10; balance.Effects.BlueSpeedMultiplier = 0;
            var blueprint = new TowerBlueprint("freeze", "Configured blue", ChassisKind.Standard, StructureKind.Gun);
            var report = TrialRunner.Run(blueprint, balance, TrialScenario.Armored, 40, color: InkColor.Blue);
            Point2 first = report.Frames[1].Enemies[0].Position, later = report.Frames[2].Enemies[0].Position;
            Assert.That(later.X, Is.EqualTo(first.X), "The first target remains frozen between the recorded frames.");
            Assert.That(later.Y, Is.EqualTo(first.Y));
            Assert.That(balance.Effects.BlueSpeedMultiplier, Is.Zero);
            Assert.That(balance.EnableEnemyMechanics, Is.True);
        }
    }
}
