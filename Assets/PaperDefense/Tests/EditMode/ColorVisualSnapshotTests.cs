using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class ColorVisualSnapshotTests
    {
        static GameBalance Rules()
        {
            var rules = GameBalance.CreateDefault(); rules.Mechanics.FieldModernization = true;
            rules.Mechanics.MaximumModernizationRank = 3; rules.Construction.AcquisitionSeconds = 0;
            rules.StartingInk = 10000; rules.PlanningSeconds = 1000; return rules;
        }
        static StructureState Tower(GameBalance rules, string family)
        {
            var entry = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(family)), balance: rules);
            return new StructureState(entry) { Id = 1, Cell = new Cell(10, 10), IsComplete = true, BuildRemaining = 0 };
        }
        static EnemyState Enemy(int id, float x) => new EnemyState { Id = id, Position = new Point2(x, 10.5f), Health = 1000, MaxHealth = 1000 };
        static ColorEffectPayload Payload(GameBalance rules, InkColor color, int rank)
            => ModernizationRules.ResolveColor(ColorEffectPayload.Capture(color, rules.ResolveContent()), rank);
        static void AssertPayload(ColorEffectPayload actual, ColorEffectPayload expected)
        {
            Assert.That(actual.Color, Is.EqualTo(expected.Color));
            Assert.That(actual.Strength, Is.EqualTo(expected.Strength)); Assert.That(actual.Duration, Is.EqualTo(expected.Duration));
            Assert.That(actual.TickSeconds, Is.EqualTo(expected.TickSeconds)); Assert.That(actual.IgnoresDefense, Is.EqualTo(expected.IgnoresDefense));
        }

        [TestCase("gun", InkColor.Red)] [TestCase("piercer", InkColor.Blue)]
        [TestCase("mortar", InkColor.Green)] [TestCase("fan", InkColor.Red)]
        [TestCase("ricochet", InkColor.Blue)] [TestCase("stapler", InkColor.Green)]
        public void AttackStartEveryBeamSegmentAndImpactCarryTheActiveImmutablePayload(string family, InkColor color)
        {
            var rules = Rules(); var tower = Tower(rules, family);
            ModernizationRules.InitializeModernizationForTrial(tower, new ModernizationRanks(color: color, colorRank: 2), 3, rules);
            var expected = Payload(rules, color, 2); var weapons = new WeaponSystem(rules);
            var towers = new List<StructureState> { tower };
            var enemies = new List<EnemyState> { Enemy(1, family == "mortar" ? 18.5f : 12.5f), Enemy(2, family == "mortar" ? 18.7f : 13) };
            var shots = new List<ShotEvent>(); var events = new SimulationEventBuffer(); var stats = new RunStatistics();
            weapons.Fire(towers, enemies, shots, 1, 1, .01f, 1, stats, events, .01);
            if (family == "mortar") { towers.Clear(); weapons.Fire(towers, enemies, shots, 1, 1, .79f, 2, stats, events, .8); }
            var fire = events.Where(value => value.Kind == SimulationEventKind.WeaponActionStarted || value.Kind == SimulationEventKind.Shot ||
                value.Kind == SimulationEventKind.ProjectileLaunched || value.Kind == SimulationEventKind.ProjectileImpact).ToArray();
            Assert.That(fire, Is.Not.Empty); Assert.That(fire.Any(value => value.Kind == SimulationEventKind.WeaponActionStarted), Is.True);
            Assert.That(events.Any(value => value.Kind == SimulationEventKind.EnemyHit), Is.True);
            foreach (var value in fire.Concat(events.Where(value => value.Kind == SimulationEventKind.EnemyHit))) AssertPayload(value.ColorEffect, expected);
            foreach (var shot in shots) AssertPayload(shot.ColorEffect, expected);
            if (family != "mortar") Assert.That(shots, Is.Not.Empty);
            if (family == "ricochet" || family == "fan") Assert.That(shots.Count, Is.GreaterThan(1), "Each segment needs the same action snapshot.");
        }

        [TestCase(InkColor.Red)] [TestCase(InkColor.Blue)] [TestCase(InkColor.Green)]
        public void FlyingShellAndRecordedViewsKeepLaunchColorAndStrengthAfterSourceUpgradeOrRemoval(InkColor color)
        {
            var rules = Rules(); var tower = Tower(rules, "mortar");
            ModernizationRules.InitializeModernizationForTrial(tower, new ModernizationRanks(color: color, colorRank: 1), 3, rules);
            var expected = Payload(rules, color, 1); var weapons = new WeaponSystem(rules);
            var towers = new List<StructureState> { tower }; var enemies = new List<EnemyState> { Enemy(1, 18.5f) };
            var shots = new List<ShotEvent>(); var events = new SimulationEventBuffer(); var stats = new RunStatistics();
            weapons.Fire(towers, enemies, shots, 1, 1, .01f, 1, stats, events, .01);
            var launch = events.Single(value => value.Kind == SimulationEventKind.ProjectileLaunched);
            var frame = new TrialFrame(.01f, enemies, towers, weapons.Projectiles, shots, events: events.ToArray());
            AssertPayload(frame.Projectiles.Single().ColorEffect, expected);
            tower.ModernizationState.Ranks = new ModernizationRanks(color: color, colorRank: 3);
            tower.Color = color == InkColor.Red ? InkColor.Blue : InkColor.Red; // A later source cannot recolor an already launched shell.
            towers.Clear();
            weapons.Fire(towers, enemies, shots, 1, 1, .79f, 2, stats, events, .8);
            var impact = events.Single(value => value.Kind == SimulationEventKind.ProjectileImpact);
            Assert.That(impact.ActionId, Is.EqualTo(launch.ActionId));
            AssertPayload(launch.ColorEffect, expected); AssertPayload(impact.ColorEffect, expected);
            AssertPayload(frame.Projectiles.Single().ColorEffect, expected);
            AssertPayload(frame.Events.Single(value => value.Kind == SimulationEventKind.ProjectileLaunched).ColorEffect, expected);
            Assert.That(weapons.Projectiles, Is.Empty);
        }

        [Test] public void FirstPaidColorStaysInactiveUntilItsWorkCompletesAndOldNeutralShotsStayNeutral()
        {
            var rules = Rules(); var session = new GameSession(rules, starterDefenses: false);
            var entry = Tower(rules, "gun").ResolvedEntry; var cell = new Cell(10, 10);
            Assert.That(session.TryBuildEntry(new[] { cell }, entry, MaterialTier.Pencil, InkColor.None, out var reason, facingDegrees: 0), Is.True, reason);
            session.Tick(entry.BuildSeconds + .1f); var tower = session.StructureAt(cell);
            var enemies = new List<EnemyState> { Enemy(1, 12.5f) }; var towers = new List<StructureState> { tower };
            var shots = new List<ShotEvent>(); var events = new SimulationEventBuffer(); var weapons = new WeaponSystem(rules); var stats = new RunStatistics();
            weapons.Fire(towers, enemies, shots, 1, 1, .01f, 1, stats, events);
            var neutral = shots.Single(); Assert.That(neutral.ColorEffect.Color, Is.EqualTo(InkColor.None));
            ((int[])typeof(GameSession).GetField("coloredInk", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session))[(int)InkColor.Red] = 1000;
            var quote = session.QuoteColorModernization(tower.Id, InkColor.Red);
            Assert.That(session.TryModernize(quote, out reason), Is.True, reason);
            Assert.That(tower.Color, Is.EqualTo(InkColor.Red)); Assert.That(tower.Modernization.ColorRank, Is.Zero);
            shots.Clear(); var during = new SimulationEventBuffer();
            weapons.Fire(towers, enemies, shots, 1, 1, .1f, 2, stats, during);
            Assert.That(shots, Is.Empty); Assert.That(during.Any(value => value.Kind == SimulationEventKind.WeaponActionStarted), Is.False);
            session.Tick(quote.WorkSeconds + .1f);
            Assert.That(tower.Modernization.IsWorking, Is.False);
            Assert.That(tower.Modernization.ColorRank, Is.EqualTo(1));
            // The completion step deliberately remains blocked. A subsequent session
            // step clears that gate; the original shot interval then resumes rather
            // than being reset to an immediately ready weapon by the installation.
            session.Tick(.01f);
            shots.Clear(); var completed = new SimulationEventBuffer();
            for (int step = 0; step < 200 && shots.Count == 0; step++)
                weapons.Fire(towers, enemies, shots, 1, 1, .01f, 3 + step, stats, completed);
            Assert.That(shots, Is.Not.Empty);
            foreach (var shot in shots) AssertPayload(shot.ColorEffect, Payload(rules, InkColor.Red, 1));
            foreach (var action in completed.Where(value => value.Kind == SimulationEventKind.WeaponActionStarted || value.Kind == SimulationEventKind.Shot))
                AssertPayload(action.ColorEffect, Payload(rules, InkColor.Red, 1));
            Assert.That(neutral.ColorEffect.Color, Is.EqualTo(InkColor.None));
        }

        [TestCase(InkColor.Red)] [TestCase(InkColor.Blue)] [TestCase(InkColor.Green)]
        public void TrialRecordingRetainsShotAndEventPayloadWhenReusingInput(InkColor color)
        {
            var rules = Rules(); rules.Trials.MaximumTicks = 180; rules.Trials.CloseTowerX = 20;
            var report = TrialRunner.Run(AssemblyResolverFixture.Catalog().Starters[0], rules, TrialScenario.Crowd,
                new TrialModernizationConfiguration(new ModernizationRanks(color: color, colorRank: 2), 3), 100);
            var shots = report.Frames.SelectMany(frame => frame.Shots).ToArray();
            var fire = report.Frames.SelectMany(frame => frame.Events).Where(value => value.Kind == SimulationEventKind.Shot).ToArray();
            Assert.That(shots, Is.Not.Empty); Assert.That(fire, Is.Not.Empty);
            var expected = Payload(rules, color, 2);
            rules.Effects.RedDamagePerTick = 999; rules.Effects.BlueSpeedMultiplier = .9f;
            foreach (var shot in shots) AssertPayload(shot.ColorEffect, expected);
            foreach (var value in fire) AssertPayload(value.ColorEffect, expected);
            foreach (var shot in report.Frames.SelectMany(frame => frame.Shots)) AssertPayload(shot.ColorEffect, expected);
        }

        [Test] public void RedPulseRetainsItsActualExpiredSourcePayloadInHitAndDeathEvents()
        {
            var rules = new EffectsRules(); var payload = new ColorEffectPayload(InkColor.Red, .2f, 20, .2f, true);
            var enemy = Enemy(1, 1); enemy.Health = 10; var events = new SimulationEventBuffer(); var stats = new RunStatistics();
            EnemyEffects.ApplyHit(enemy, 0, payload, stats, StructureKind.Gun, rules);
            EnemyEffects.Advance(enemy, .3f, stats, rules, events: events, tick: 1, simulationSeconds: .3);
            Assert.That(events.Count(value => value.Kind == SimulationEventKind.EnemyHit || value.Kind == SimulationEventKind.EnemyDied), Is.EqualTo(2));
            foreach (var value in events) AssertPayload(value.ColorEffect, payload);
        }
    }
}
