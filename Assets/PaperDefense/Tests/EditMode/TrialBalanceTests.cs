using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class TrialBalanceTests
    {
        static TowerBlueprint Gun => new TowerBlueprint("configured-gun", "Пулемёт", ChassisKind.Standard, StructureKind.Gun);

        static GameBalance Snapshot(GameBalance source) => source.CreateSnapshot();

        static TrialReport Run(TowerBlueprint blueprint, GameBalance balance, TrialScenario scenario = TrialScenario.Crowd,
            int budget = 400, int seed = 1337, bool counterfire = false, InkColor color = InkColor.None)
            => TrialRunner.Run(blueprint, balance, scenario, budget, seed, counterfire, color);

        static TrialComparison Compare(TowerBlueprint first, TowerBlueprint second, GameBalance balance, TrialScenario scenario,
            int budget = 400, int seed = 1337, bool counterfire = false, InkColor color = InkColor.None)
            => TrialRunner.Compare(first, second, balance, scenario, budget, seed, counterfire, color);

        static void AssertCopiedFields(object source, object copy)
        {
            Assert.That(copy, Is.Not.SameAs(source));
            foreach (FieldInfo field in source.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object original=field.GetValue(source), copied=field.GetValue(copy);
                if (original is IList list)
                {
                    var owned=(IList)copied; Assert.That(owned,Is.Not.SameAs(list),field.Name); Assert.That(owned.Count,Is.EqualTo(list.Count),field.Name);
                    for(int i=0;i<list.Count;i++)
                        if(list[i]!=null && list[i].GetType().IsClass && !(list[i] is string)) AssertCopiedFields(list[i],owned[i]);
                        else Assert.That(owned[i],Is.EqualTo(list[i]),field.Name);
                }
                else if(original!=null && original.GetType().IsClass && !(original is string)) AssertCopiedFields(original,copied);
                else Assert.That(copied,Is.EqualTo(original),field.Name);
            }
        }

        [Test]
        public void SnapshotOwnsEveryDefinitionAndWaveAndPreservesAllScalarTuning()
        {
            var source = GameBalance.CreateDefault();
            int value = 1;
            foreach (FieldInfo field in typeof(GameBalance).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(int)) field.SetValue(source, value++ * 11);
                else if (field.FieldType == typeof(float)) field.SetValue(source, value++ * .37f);
                else if (field.FieldType == typeof(bool)) field.SetValue(source, true);
            }
            source.Structure(StructureKind.Mortar).FlightSeconds = 3.4f;
            source.Enemy(EnemyKind.Spitter).WindupSeconds = 1.7f;
            source.Waves[2].HatchedFraction = .23f;
            GameBalance copy = Snapshot(source);

            Assert.That(copy, Is.Not.SameAs(source));
            foreach (FieldInfo field in typeof(GameBalance).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object original = field.GetValue(source), copied = field.GetValue(copy);
                if (!(original is IList definitions))
                {
                    if (original != null && original.GetType().IsClass && !(original is string)) AssertCopiedFields(original, copied);
                    else Assert.That(copied, Is.EqualTo(original), field.Name);
                    continue;
                }
                var copiedDefinitions = (IList)copied;
                Assert.That(copiedDefinitions, Is.Not.SameAs(definitions), field.Name);
                Assert.That(copiedDefinitions.Count, Is.EqualTo(definitions.Count), field.Name);
                for (int i = 0; i < definitions.Count; i++) AssertCopiedFields(definitions[i], copiedDefinitions[i]);
            }
            int configuredStructureCount = source.StructureDefinitions.Count;
            source.StructureDefinitions.Clear();
            source.MaterialDefinitions[0].DamageMultiplier = 0;
            source.EnemyDefinitions[0].MaxHealth = 999;
            source.Waves[0].Count = 999;
            Assert.That(copy.StructureDefinitions.Count, Is.EqualTo(configuredStructureCount));
            Assert.That(copy.MaterialDefinitions[0].DamageMultiplier, Is.EqualTo(1));
            Assert.That(copy.EnemyDefinitions[0].MaxHealth, Is.EqualTo(22));
            Assert.That(copy.Waves[0].Count, Is.EqualTo(80));
            copy.MaterialDefinitions[0].DamageMultiplier = 7;
            Assert.That(source.MaterialDefinitions[0].DamageMultiplier, Is.Zero);
        }

        [Test]
        public void ConfiguredGunPriceIsChargedForEveryTrialCopy()
        {
            var balance = GameBalance.CreateDefault();
            balance.Structure(StructureKind.Gun).Cost = 99;
            TrialReport report = Run(Gun, balance);
            Assert.That(report.Copies, Is.EqualTo(4));
            Assert.That(report.Cost, Is.EqualTo(396));
            Assert.That(report.Damage, Is.GreaterThan(0));
        }

        [Test]
        public void DefaultEqualBudgetComparisonKeepsTheEstablishedCopyCosts()
        {
            var dual = new TowerBlueprint("dual", "Два ствола", ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun);
            TrialComparison result = Compare(Gun, dual, GameBalance.CreateDefault(), TrialScenario.Crowd, 400, 42);
            Assert.That(result.First.Copies, Is.EqualTo(10));
            Assert.That(result.First.Cost, Is.EqualTo(400));
            Assert.That(result.Second.Copies, Is.EqualTo(6));
            Assert.That(result.Second.Cost, Is.EqualTo(384));
            Assert.That(result.First.Seed, Is.EqualTo(result.Second.Seed));
            Assert.That(result.First.Frames[1].Enemies[0].Position.X, Is.EqualTo(result.Second.Frames[1].Enemies[0].Position.X));
            Assert.That(result.First.Frames[1].Enemies[0].Position.Y, Is.EqualTo(result.Second.Frames[1].Enemies[0].Position.Y));
        }

        [TestCase("damage")]
        [TestCase("range")]
        [TestCase("material damage")]
        public void ConfiguredWeaponAndMaterialParametersChangeRealTrialDamage(string parameter)
        {
            var balance = GameBalance.CreateDefault();
            Assert.That(Run(Gun, balance, budget: 40).Damage, Is.GreaterThan(0));
            if (parameter == "damage") balance.Structure(StructureKind.Gun).Damage = 0;
            else if (parameter == "range") balance.Structure(StructureKind.Gun).Range = 0;
            else balance.Material(MaterialTier.Pencil).DamageMultiplier = 0;
            Assert.That(Run(Gun, balance, budget: 40).Damage, Is.Zero);
        }

        [Test]
        public void ConfiguredMaterialCostAndHealthReachTheTrialBattery()
        {
            var balance = GameBalance.CreateDefault();
            balance.Material(MaterialTier.Pencil).CostMultiplier = 2;
            balance.Material(MaterialTier.Pencil).HealthMultiplier = 3;
            TrialReport report = Run(Gun, balance);
            Assert.That(report.Copies, Is.EqualTo(5));
            Assert.That(report.Cost, Is.EqualTo(400));
            Assert.That(report.Frames[0].Towers[0].MaxHealth, Is.EqualTo(165));
            Assert.That(report.Frames[0].Towers[0].Health, Is.EqualTo(165));
        }

        [Test]
        public void ConfiguredMortarFlightAndRadiusReachRecordedProjectiles()
        {
            var balance = GameBalance.CreateDefault();
            balance.Structure(StructureKind.Mortar).FlightSeconds = 2.5f;
            balance.Structure(StructureKind.Mortar).Radius = 4;
            var mortar = new TowerBlueprint("mortar", "Мортира", ChassisKind.Standard, StructureKind.Mortar);
            TrialReport report = Run(mortar, balance, budget: 90);
            bool found = false;
            foreach (TrialFrame frame in report.Frames)
                foreach (ProjectileView projectile in frame.Projectiles)
                {
                    found = true;
                    Assert.That(projectile.Duration, Is.EqualTo(2.5f));
                    Assert.That(projectile.Radius, Is.EqualTo(4));
                }
            Assert.That(found, Is.True);
        }

        [Test]
        public void TrialHonorsTheProvidedEnemyMechanicsRuleForArmoredTargets()
        {
            var balance = GameBalance.CreateDefault();
            balance.Structure(StructureKind.Gun).Damage = 10;
            balance.Structure(StructureKind.Gun).ShotInterval = 1000;
            balance.EnableEnemyMechanics = false;
            TrialReport plain = Run(Gun, balance, TrialScenario.Armored, 40);
            balance.EnableEnemyMechanics = true;
            TrialReport armored = Run(Gun, balance, TrialScenario.Armored, 40);
            Assert.That(plain.Damage, Is.EqualTo(10));
            Assert.That(armored.Damage, Is.EqualTo(8.5f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RunningBothLanesPreservesCallerBalanceIncludingRuleFlags(bool enemyMechanics)
        {
            var balance = GameBalance.CreateDefault();
            balance.EnableEnemyMechanics = enemyMechanics;
            balance.EnableInkWells = true;
            GameBalance before = Snapshot(balance);
            TrialComparison comparison = Compare(Gun, Gun, balance, TrialScenario.Armored, 80, 72, true, InkColor.Blue);
            Assert.That(comparison.First.Damage, Is.EqualTo(comparison.Second.Damage));
            Assert.That(comparison.First.Leaks, Is.EqualTo(comparison.Second.Leaks));
            Assert.That(comparison.First.SurvivingTowers, Is.EqualTo(comparison.Second.SurvivingTowers));
            foreach (FieldInfo field in typeof(GameBalance).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object original = field.GetValue(before), after = field.GetValue(balance);
                if (!(original is IList definitions))
                {
                    if (original != null && original.GetType().IsClass && !(original is string)) AssertCopiedFields(original, after);
                    else Assert.That(after, Is.EqualTo(original), field.Name);
                    continue;
                }
                var afterDefinitions = (IList)after;
                Assert.That(afterDefinitions.Count, Is.EqualTo(definitions.Count), field.Name);
                for (int i = 0; i < definitions.Count; i++) AssertCopiedFields(definitions[i], afterDefinitions[i]);
            }
        }

        [Test]
        public void TrialKeepsItsFiniteFixtureWhenCampaignEnemyAndWaveDataChanges()
        {
            var balance = GameBalance.CreateDefault();
            balance.Enemy(EnemyKind.Doodle).MaxHealth = 9999;
            balance.Enemy(EnemyKind.Doodle).Speed = 999;
            balance.Waves.Clear();
            TrialReport report = Run(Gun, balance, budget: 0);
            EnemyView enemy = report.Frames[1].Enemies[0];
            Assert.That(enemy.MaxHealth, Is.EqualTo(22));
            Assert.That(enemy.Position.X, Is.EqualTo(10 + 2.3f / 3).Within(.0001f));
            Assert.That(report.Leaks, Is.EqualTo(240));
            Assert.That(report.UnresolvedEnemies, Is.Zero);
        }
    }
}
