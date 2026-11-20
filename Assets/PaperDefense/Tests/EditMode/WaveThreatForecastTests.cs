using System;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class WaveThreatForecastTests
    {
        static WaveView Preview(WaveDefinition wave, bool tiers = true, bool ranged = true,
            Action<GameBalance> configure = null)
        {
            var balance = GameBalance.CreateDefault();
            balance.EnableEnemyMechanics = true;
            balance.Mechanics.EnemyDrawingTiers = tiers;
            balance.Mechanics.RangedEnemies = ranged;
            balance.Combat.MinimumSpawnInterval = .25f;
            balance.Waves.Clear();
            balance.Waves.Add(wave);
            configure?.Invoke(balance);
            return new RunReadModel(new GameSession(balance, starterDefenses: false)).Balance.Waves[0];
        }

        [TestCase(true, true, WaveThreat.Armor, .50f)]
        [TestCase(false, true, WaveThreat.Ranged, .15f)]
        [TestCase(false, false, WaveThreat.Runners, .35f)]
        public void MixedFormationReportsEnabledEnemyRolesAndIndependentDrawingTiers(
            bool tiers, bool ranged, WaveThreat expectedThreat, float expectedFraction)
        {
            var view = Preview(new WaveDefinition {
                Count = 160, Sides = 1, RunnerFraction = .35f, SpitterFraction = .15f,
                HatchedFraction = .20f, OutlinedFraction = .30f
            }, tiers, ranged);

            Assert.That(view.Forecast.Primary, Is.EqualTo(expectedThreat));
            Assert.That(view.Forecast.PrimaryFraction, Is.EqualTo(expectedFraction).Within(.00001f));
            Assert.That(view.Forecast.ArmoredFraction, Is.EqualTo(tiers ? .5f : 0).Within(.00001f));
            Assert.That(view.Forecast.RunnerFraction, Is.EqualTo(.35f).Within(.00001f));
            Assert.That(view.Forecast.OnlySpittersFireGlobs, Is.EqualTo(ranged));
        }

        [TestCase(true, WaveThreat.Ranged, .60f)]
        [TestCase(false, WaveThreat.Crowd, 0)]
        public void RangedThreatFollowsConfiguredAttacksAcrossFamilies(bool ranged, WaveThreat threat, float rangedShare)
        {
            var view = Preview(new WaveDefinition { Count = 160, Sides = 1, SpitterFraction = .4f }, ranged: ranged,
                configure: balance => {
                    balance.Enemy(EnemyKind.Doodle).Attack = EnemyAttackMode.Glob;
                    balance.Enemy(EnemyKind.Spitter).Attack = EnemyAttackMode.Contact;
                });

            Assert.That(view.Forecast.Primary, Is.EqualTo(threat));
            Assert.That(view.Forecast.RangedFraction, Is.EqualTo(rangedShare).Within(.00001f));
            Assert.That(view.Forecast.OnlySpittersFireGlobs, Is.False);
        }

        [Test]
        public void ForecastUsesAdmissibleSharesAndTheMinimumSpawnInterval()
        {
            var view = Preview(new WaveDefinition {
                Count = 160, Sides = 1, SpawnInterval = .01f,
                GnawerFraction = .90f, RunnerFraction = .75f, SpitterFraction = .50f
            });

            Assert.That(view.Forecast.Primary, Is.EqualTo(WaveThreat.Breach));
            Assert.That(view.Forecast.PrimaryFraction, Is.EqualTo(.9f).Within(.00001f));
            Assert.That(view.Forecast.RunnerFraction, Is.EqualTo(.1f).Within(.00001f));
            Assert.That(view.Composition.Fraction(EnemyKind.Spitter), Is.Zero.Within(.00001f));
            Assert.That(view.Forecast.MaximumArrivalsPerSecond, Is.EqualTo(4));
        }
    }
}
