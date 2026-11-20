using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    /// <summary>Presentation events must describe work performed by the real session.</summary>
    public sealed class StructureFeedbackEventTests
    {
        static GameBalance Rules()
        {
            var rules = GameBalance.CreateDefault();
            rules.Width = 40; rules.Height = 24; rules.StartingInk = 10000;
            rules.BaseHealth = 10000; rules.EnableEnemyMechanics = true; rules.EnableInkWells = false;
            rules.PlanningSeconds = rules.IntermissionSeconds = 3600;
            rules.PassiveInkPerSecond = rules.EarlyLaunchInkPerSecond = 0;
            rules.MaxEarlyLaunchInk = rules.WaveInkGrant = 0; rules.Progression.UpgradeEveryWaves = 0;
            rules.Waves = new List<WaveDefinition> {
                new WaveDefinition { Count = 0, InkGrant = 0 },
                new WaveDefinition { Count = 0, InkGrant = 0 },
                new WaveDefinition { Count = 0, InkGrant = 0 } };
            foreach (var definition in rules.StructureDefinitions) definition.BuildSeconds = 0;
            return rules;
        }

        static ResolvedEntry Support(AssemblyStructureFamily family, GameBalance rules, string utility = "none")
        {
            string name = family.ToString().ToLowerInvariant();
            return AssemblyResolverFixture.Resolve(new AssemblyRecipe(new StructureRecipe(family,
                AssemblyResolverFixture.Part("site." + name), AssemblyResolverFixture.Part("function." + name),
                AssemblyResolverFixture.Part("shell.standard"), AssemblyResolverFixture.Part("utility." + utility))), balance: rules);
        }

        static StructureState Build(GameSession session, ResolvedEntry entry, Cell cell)
        {
            Assert.That(session.TryBuildEntry(new[] { cell }, entry, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
            return session.StructureAt(cell);
        }

        static void Finish(GameSession session)
        {
            session.Tick(session.Structures.Max(item => item.BuildRemaining) + .05f);
            Assert.That(session.Structures.All(item => item.IsComplete), Is.True);
        }

        static Point2 Center(Cell cell) => new Point2(cell.X + .5f, cell.Y + .5f);
        static SimulationEvent[] Events(GameSession session, SimulationEventKind kind, long after = 0)
            => session.EventBuffer.Where(item => item.Kind == kind && item.Sequence > after).ToArray();

        static void Endpoints(SimulationEvent item, int entity, int source, Point2 from, Point2 to)
        {
            Assert.That(item.EntityId, Is.EqualTo(entity)); Assert.That(item.SourceStructureId, Is.EqualTo(source));
            Assert.That(item.From, Is.EqualTo(from)); Assert.That(item.To, Is.EqualTo(to));
            Assert.That(item.SimulationSeconds, Is.GreaterThan(0));
        }

        [TestCase(3f)] [TestCase(100f)]
        public void RealMeleeHitReportsOnlyTheHealthActuallyRemovedBeforeDestruction(float health)
        {
            var rules = Rules(); rules.Enemy(EnemyKind.Doodle).Damage = 10;
            rules.Enemy(EnemyKind.Doodle).StructureDamageMultiplier = 1;
            rules.Structure(StructureKind.Wall).MaxHealth = 1000;
            var from = new Point2(17.7f, 12.5f);
            var session = GameSession.CreatePrepared(rules, 7, new[] { new PreparedEnemy(from, 100) });
            // A closed ring forces the ordinary flow/frontage path to attack its west wall.
            var ring = new List<Cell>();
            for (int x = 18; x <= 22; x++) { ring.Add(new Cell(x, 10)); ring.Add(new Cell(x, 14)); }
            for (int y = 11; y <= 13; y++) { ring.Add(new Cell(18, y)); ring.Add(new Cell(22, y)); }
            Assert.That(session.TryBuild(ring, StructureKind.Wall, MaterialTier.Pencil, out var reason), Is.True, reason);
            var wall = session.StructureAt(new Cell(18, 12)); wall.Health = health;
            session.Tick(.01f);
            Assert.That(wall.Health, Is.LessThan(health), "The fixture must execute an actual frontline attack.");
            var item = Events(session, SimulationEventKind.StructureHit).Single();
            Endpoints(item, wall.Id, 0, from, Center(wall.Cell));
            Assert.That(item.Magnitude, Is.EqualTo(health - wall.Health).Within(.0001));
            Assert.That(item.Magnitude, Is.LessThanOrEqualTo(health));
            Assert.That(session.StructureAt(wall.Cell) == null, Is.EqualTo(wall.Health <= 0));
            long sequence = session.EventBuffer.LatestSequence; session.Tick(0);
            Assert.That(session.EventBuffer.LatestSequence, Is.EqualTo(sequence), "A paused tick cannot invent another hit.");
        }

        [TestCase(2f)] [TestCase(100f)]
        public void ARealSpitterLaunchAndImpactPreserveSourceAndClampedStructureLoss(float health)
        {
            var rules = Rules(); var spitter = rules.Enemy(EnemyKind.Spitter);
            spitter.Damage = 10; spitter.Speed = 0; spitter.Range = 12.5f;
            spitter.WindupSeconds = .1f; spitter.ProjectileSeconds = .2f; spitter.AttackInterval = 10;
            var from = new Point2(8.5f, 12.5f);
            var session = GameSession.CreatePrepared(rules, 7, new[] { new PreparedEnemy(new Point2(35.5f, 4.5f), 100, EnemyKind.Spitter) });
            var wall = Build(session, Support(AssemblyStructureFamily.Wall, rules), new Cell(12, 12));
            Finish(session);
            session.Enemies.Single().Position = from;
            wall.Health = health;
            long sequence = session.EventBuffer.LatestSequence;
            for (int i = 0; i < 150 && Events(session, SimulationEventKind.StructureHit, sequence).Length == 0; i++) session.Tick(.01f);
            Assert.That(wall.Health, Is.LessThan(health));
            var item = Events(session, SimulationEventKind.StructureHit, sequence).Single();
            Endpoints(item, wall.Id, 0, from, Center(wall.Cell));
            Assert.That(item.Magnitude, Is.EqualTo(health - wall.Health).Within(.0001));
            Assert.That(session.EventBuffer.Any(value => value.Kind == SimulationEventKind.EnemyGlobLaunched), Is.True);
            Assert.That(session.EventBuffer.Any(value => value.Kind == SimulationEventKind.EnemyGlobImpact), Is.True);
        }

        [Test] public void RepairReportsTheWinningSourceAndActualClampedHealthGain()
        {
            var rules = Rules(); var session = new GameSession(rules, starterDefenses: false);
            var first = Build(session, Support(AssemblyStructureFamily.Repair, rules), new Cell(4, 4));
            Build(session, Support(AssemblyStructureFamily.Repair, rules), new Cell(6, 4));
            var wall = Build(session, Support(AssemblyStructureFamily.Wall, rules), new Cell(5, 4)); Finish(session);
            wall.Health = wall.MaxHealth - .02f; float before = wall.Health;
            long sequence = session.EventBuffer.LatestSequence; session.Tick(.01f);
            Assert.That(wall.Health, Is.EqualTo(wall.MaxHealth));
            var item = Events(session, SimulationEventKind.RepairApplied, sequence).Single();
            Endpoints(item, wall.Id, first.Id, Center(first.Cell), Center(wall.Cell));
            Assert.That(item.Magnitude, Is.EqualTo(wall.Health - before).Within(.00001));
            sequence = session.EventBuffer.LatestSequence; session.Tick(1);
            Assert.That(Events(session, SimulationEventKind.RepairApplied, sequence), Is.Empty, "Full health is not repair work.");
        }

        [TestCase("dead")] [TestCase("building")] [TestCase("pending")]
        public void InactiveRepairSourcesProduceNeitherHealingNorFeedback(string state)
        {
            var rules = Rules(); var session = new GameSession(rules, starterDefenses: false);
            var post = Build(session, Support(AssemblyStructureFamily.Repair, rules), new Cell(4, 4));
            var wall = Build(session, Support(AssemblyStructureFamily.Wall, rules), new Cell(5, 4)); Finish(session);
            if (state == "dead") post.Health = 0;
            else { post.BuildRemaining = 100; if (state == "building") post.IsComplete = false; }
            wall.Health = wall.MaxHealth / 2; float before = wall.Health;
            long sequence = session.EventBuffer.LatestSequence; session.Tick(.5f);
            Assert.That(wall.Health, Is.EqualTo(before));
            Assert.That(Events(session, SimulationEventKind.RepairApplied, sequence), Is.Empty);
        }

        [Test] public void ContinuousRealHealingKeepsMechanicsPerTickButFeedbackAtMostFourTimesPerSecond()
        {
            var rules = Rules(); var session = new GameSession(rules, starterDefenses: false);
            var entry = Support(AssemblyStructureFamily.Repair, rules);
            var post = Build(session, entry, new Cell(4, 4));
            var wall = Build(session, Support(AssemblyStructureFamily.Wall, rules), new Cell(5, 4)); Finish(session);
            wall.Health = wall.MaxHealth / 2; float before = wall.Health;
            long sequence = session.EventBuffer.LatestSequence;
            for (int i = 0; i < 100; i++) session.Tick(.01f);
            Assert.That(wall.Health - before, Is.EqualTo(entry.Support.RepairPerSecond).Within(.005));
            var events = Events(session, SimulationEventKind.RepairApplied, sequence);
            Assert.That(events.Length, Is.InRange(2, 4));
            Assert.That(events.All(item => item.SourceStructureId == post.Id && item.Magnitude > 0), Is.True);
            for (int i = 1; i < events.Length; i++)
                Assert.That(events[i].SimulationSeconds - events[i - 1].SimulationSeconds, Is.GreaterThanOrEqualTo(.25 - .00001));
        }

        static (GameBalance rules, GameSession session, StructureState field, EnemyState enemy) SlowFixture()
        {
            var rules = Rules();
            var session = GameSession.CreatePrepared(rules, 7, new[] { new PreparedEnemy(new Point2(15.5f, 4.5f), 100) });
            var field = Build(session, Support(AssemblyStructureFamily.Slow, rules), new Cell(4, 4)); Finish(session);
            var enemy = session.Enemies.Single(); enemy.Position = new Point2(5.4f, 4.5f);
            return (rules, session, field, enemy);
        }

        [Test] public void SlowFeedbackIdentifiesTheActualTargetAndDoesNotRepeatForMereDurationRefresh()
        {
            var fixture = SlowFixture(); var session = fixture.session; var enemy = fixture.enemy;
            long sequence = session.EventBuffer.LatestSequence; session.Tick(.01f);
            float actual = EnemyEffects.MovementMultiplier(enemy, fixture.rules.Effects);
            Assert.That(actual, Is.LessThan(1));
            var item = Events(session, SimulationEventKind.SlowApplied, sequence).Single();
            Endpoints(item, enemy.Id, fixture.field.Id, Center(fixture.field.Cell), enemy.Position);
            Assert.That(item.Magnitude, Is.EqualTo(actual).Within(.00001));
            sequence = session.EventBuffer.LatestSequence; session.Tick(.8f);
            Assert.That(Events(session, SimulationEventKind.SlowApplied, sequence), Is.Empty,
                "Refreshing an equal slow's duration does not improve movement strength.");
        }

        [TestCase("dead")] [TestCase("building")] [TestCase("stronger")]
        public void InactiveOrWeakerSlowDoesNotInventFeedback(string state)
        {
            var fixture = SlowFixture();
            if (state == "dead") fixture.field.Health = 0;
            else if (state == "building") { fixture.field.IsComplete = false; fixture.field.BuildRemaining = 100; }
            else EnemyEffects.ApplySlow(fixture.enemy, .4f, 10);
            float before = EnemyEffects.MovementMultiplier(fixture.enemy, fixture.rules.Effects);
            long sequence = fixture.session.EventBuffer.LatestSequence; fixture.session.Tick(.1f);
            Assert.That(EnemyEffects.MovementMultiplier(fixture.enemy, fixture.rules.Effects), Is.EqualTo(before));
            Assert.That(Events(fixture.session, SimulationEventKind.SlowApplied, sequence), Is.Empty);
        }

        [Test] public void AFieldSlowsEveryFreshTargetWhileItsVisualFeedbackRemainsBoundedPerSource()
        {
            var rules = Rules();
            var targets = Enumerable.Range(0, 20).Select(index => new PreparedEnemy(
                new Point2(15.5f + index % 5, 3.5f + index / 5), 100)).ToArray();
            var session = GameSession.CreatePrepared(rules, 7, targets);
            var entry = Support(AssemblyStructureFamily.Slow, rules);
            var field = Build(session, entry, new Cell(4, 4)); Finish(session);
            long sequence = session.EventBuffer.LatestSequence;
            for (int index = 0; index < targets.Length; index++)
            {
                var enemy = session.Enemies[index]; enemy.Position = new Point2(5.4f, 4.5f);
                session.Tick(.05f);
                Assert.That(EnemyEffects.MovementMultiplier(enemy, rules.Effects),
                    Is.EqualTo(Math.Max(.4f, entry.Support.SlowSpeedMultiplier)).Within(.00001),
                    "Feedback throttling must never throttle the underlying slow.");
            }
            var events = Events(session, SimulationEventKind.SlowApplied, sequence);
            Assert.That(events.Length, Is.InRange(2, 4));
            Assert.That(events.All(item => item.SourceStructureId == field.Id && item.Magnitude < 1), Is.True);
            for (int i = 1; i < events.Length; i++)
                Assert.That(events[i].SimulationSeconds - events[i - 1].SimulationSeconds, Is.GreaterThanOrEqualTo(.25 - .00001));
        }

        [Test] public void SpikesWaitForConstructionThenPulseAgainstEveryEnemyInsideTheirRadius()
        {
            var rules = Rules();
            var session = GameSession.CreatePrepared(rules, 21, new[] {
                new PreparedEnemy(new Point2(10.15f, 10.25f), 100),
                new PreparedEnemy(new Point2(10.15f, 10.75f), 100),
                new PreparedEnemy(new Point2(15.5f, 10.5f), 100) });
            var entry = Support(AssemblyStructureFamily.Spikes, rules);
            var trap = Build(session, entry, new Cell(9, 10));
            Assert.That(ConstructionToolRules.IsTrap(trap.Kind), Is.True);
            session.Tick(.01f);
            Assert.That(session.Statistics.TotalDamage, Is.Zero);
            Finish(session);
            float damage = entry.Support.SpikeDamage;
            Assert.That(session.Enemies[0].Health, Is.EqualTo(100 - damage).Within(.001f));
            Assert.That(session.Enemies[1].Health, Is.EqualTo(100 - damage).Within(.001f));
            Assert.That(session.Enemies[2].Health, Is.EqualTo(100));
            session.Tick(.7f);
            Assert.That(Events(session, SimulationEventKind.SpikesTriggered).Count(), Is.EqualTo(1));
            session.Tick(.4f);
            Assert.That(Events(session, SimulationEventKind.SpikesTriggered).Count(), Is.EqualTo(2));
            Assert.That(session.Statistics.Damage(trap.ContentId), Is.EqualTo(4 * damage).Within(.001f));
        }

        [Test] public void PrismCyclesBurnSlowAndArmorReductionOnlyWhenEnemiesAreInRange()
        {
            var rules = Rules();
            rules.Effects.HatchedDefense = .5f;
            var session = GameSession.CreatePrepared(rules, 21, new[] {
                new PreparedEnemy(new Point2(15.3f, 10.3f), 100, drawingTier: DrawingTier.Hatched),
                new PreparedEnemy(new Point2(15.7f, 10.7f), 100),
                new PreparedEnemy(new Point2(16.5f, 10.5f), 100) });
            var entry = Support(AssemblyStructureFamily.Prism, rules);
            var prism = Build(session, entry, new Cell(9, 10));
            Finish(session);
            session.Tick(5);
            Assert.That(Events(session, SimulationEventKind.PrismPulsed), Is.Empty);
            var armored = session.Enemies[0]; var plain = session.Enemies[1]; var outside = session.Enemies[2];
            armored.Position = new Point2(10.5f, 10.5f);
            plain.Position = new Point2(9.5f, 10.5f + entry.Support.Radius);
            outside.Position = new Point2(9.5f, 10.52f + entry.Support.Radius);
            session.Tick(.01f);
            Assert.That(armored.RedRemaining, Is.Positive);
            Assert.That(plain.RedRemaining, Is.Positive);
            Assert.That(outside.RedRemaining, Is.Zero);
            session.Tick(2.05f);
            Assert.That(armored.Health, Is.EqualTo(100 - entry.Support.BurnDamagePerSecond * 2).Within(.001f));
            Assert.That(plain.Health, Is.EqualTo(armored.Health));
            Assert.That(EnemyEffects.MovementMultiplier(armored), Is.EqualTo(entry.Support.SlowSpeedMultiplier));
            Assert.That(session.Statistics.TotalEnemiesSlowedByPrism, Is.EqualTo(2));
            session.Tick(2.05f);
            Assert.That(EnemyEffects.Defense(armored, rules.Effects), Is.EqualTo(.5f - entry.Support.ArmorReduction).Within(.001f));
            Assert.That(plain.GreenRemaining, Is.Zero);
            Assert.That(session.Statistics.TotalEnemiesMarked, Is.EqualTo(1));
            session.Tick(2.05f);
            var pulses = Events(session, SimulationEventKind.PrismPulsed);
            CollectionAssert.AreEqual(new[] { InkColor.Red, InkColor.Blue, InkColor.Green, InkColor.Red }, pulses.Select(e => e.ColorEffect.Color));
            Assert.That(pulses.All(e => e.SourceStructureId == prism.Id && e.WeaponId == prism.ContentId
                && e.Radius == entry.Support.Radius && e.From.Equals(Center(prism.Cell))), Is.True);
            Assert.That(outside.Health, Is.EqualTo(100));
        }

        [Test] public void OverlappingPrismsShareStatusStrengthAndLeaveOnlyTheLastPulseAfterErasure()
        {
            var rules = Rules();
            var session = GameSession.CreatePrepared(rules, 21, new[] {
                new PreparedEnemy(new Point2(11.5f, 10.5f), 100, drawingTier: DrawingTier.Hatched) });
            var entry = Support(AssemblyStructureFamily.Prism, rules);
            var first = Build(session, entry, new Cell(9, 10));
            var second = Build(session, entry, new Cell(10, 9));
            session.Tick(.01f);
            Assert.That(Events(session, SimulationEventKind.PrismPulsed), Is.Empty, "Unfinished prisms must wait.");
            Finish(session);
            session.Tick(4.1f);
            var enemy = session.Enemies.Single();
            Assert.That(session.Statistics.Damage(first.ContentId), Is.EqualTo(2 * entry.Support.BurnDamagePerSecond).Within(.001f));
            Assert.That(session.Statistics.TotalEnemiesSlowedByPrism, Is.EqualTo(1));
            Assert.That(session.Statistics.TotalEnemiesMarked, Is.EqualTo(1));
            Assert.That(session.TryErase(first.Cell, out _), Is.True);
            Assert.That(session.TryErase(second.Cell, out _), Is.True);
            long cursor = session.EventBuffer.LatestSequence;
            session.Tick(2.1f);
            Assert.That(Events(session, SimulationEventKind.PrismPulsed, cursor), Is.Empty);
            Assert.That(enemy.GreenRemaining, Is.Zero);
            Assert.That(EnemyEffects.Defense(enemy, rules.Effects), Is.EqualTo(rules.Effects.HatchedDefense));
        }

        [Test] public void OverlappingBeaconsWeakenArmorOnceBeforeTheSpikePulseAndExpireAfterErasure()
        {
            var rules = Rules();
            rules.Effects.HatchedDefense = .5f;
            var session = GameSession.CreatePrepared(rules, 21, new[] {
                new PreparedEnemy(new Point2(15.5f, 10.5f), 100, drawingTier: DrawingTier.Hatched) });
            var trap = Build(session, Support(AssemblyStructureFamily.Spikes, rules), new Cell(9, 10));
            var beacon = Support(AssemblyStructureFamily.Beacon, rules);
            var first = Build(session, beacon, new Cell(9, 9));
            var second = Build(session, beacon, new Cell(10, 9));
            Finish(session);
            var enemy = session.Enemies.Single();
            enemy.Position = new Point2(10.15f, 10.5f);
            session.Tick(.01f);
            float defense = rules.Effects.HatchedDefense - beacon.Support.ArmorReduction;
            Assert.That(EnemyEffects.Defense(enemy, rules.Effects), Is.EqualTo(defense).Within(.001f));
            Assert.That(enemy.Health, Is.EqualTo(100 - trap.ResolvedEntry.Support.SpikeDamage * (1 - defense)).Within(.001f));
            Assert.That(Events(session, SimulationEventKind.BeaconMarked).Single().SourceStructureId, Is.EqualTo(first.Id));
            Assert.That(session.TryErase(first.Cell, out _), Is.True);
            Assert.That(session.TryErase(second.Cell, out _), Is.True);
            session.Tick(.4f);
            Assert.That(EnemyEffects.Defense(enemy, rules.Effects), Is.EqualTo(defense).Within(.001f));
            session.Tick(.7f);
            Assert.That(EnemyEffects.Defense(enemy, rules.Effects), Is.EqualTo(rules.Effects.HatchedDefense).Within(.001f));
        }

        [Test] public void ABeaconCountsEachArmoredTargetOnceEvenWhenTheTrapImmediatelyKillsTheGroup()
        {
            var rules = Rules();
            var session = GameSession.CreatePrepared(rules, 21, new[] {
                new PreparedEnemy(new Point2(15.3f, 10.35f), 1, drawingTier: DrawingTier.Hatched),
                new PreparedEnemy(new Point2(15.3f, 10.65f), 1, drawingTier: DrawingTier.Hatched),
                new PreparedEnemy(new Point2(15.3f, 11.3f), 1) });
            var spikes = Support(AssemblyStructureFamily.Spikes, rules);
            Build(session, spikes, new Cell(9, 10));
            Build(session, spikes, new Cell(10, 9));
            Build(session, Support(AssemblyStructureFamily.Beacon, rules), new Cell(9, 9));
            Finish(session);
            foreach (var enemy in session.Enemies) enemy.Position = new Point2(10.15f, 10.5f);
            session.Tick(.01f);
            Assert.That(session.Enemies, Is.Empty);
            Assert.That(session.Statistics.TotalEnemiesMarked, Is.EqualTo(2));
            Assert.That(Events(session, SimulationEventKind.BeaconMarked), Has.Length.EqualTo(1));
            Assert.That(Events(session, SimulationEventKind.SpikesTriggered), Has.Length.EqualTo(1));
            Assert.That(Events(session, SimulationEventKind.EnemyDied), Has.Length.EqualTo(3));
            Assert.That(session.Statistics.Damage("function.spikes"), Is.EqualTo(3));
        }

        [TestCase("none")] [TestCase("economy")]
        public void GeneratorPaysAndPublishesItsResolvedIncomeOnceForEachEligibleWave(string utility)
        {
            var rules = Rules(); var session = new GameSession(rules, starterDefenses: false);
            var entry = Support(AssemblyStructureFamily.Generator, rules, utility);
            var generator = Build(session, entry, new Cell(4, 4)); Finish(session);
            int ink = session.Ink; long sequence = session.EventBuffer.LatestSequence;
            for (int wave = 1; wave <= 2; wave++)
            {
                session.StartNextWave(); session.Tick(.01f);
                var items = Events(session, SimulationEventKind.GeneratorPaid, sequence);
                Assert.That(items.Length, Is.EqualTo(wave));
                var item = items.Last(); Endpoints(item, generator.Id, generator.Id, Center(generator.Cell), Center(generator.Cell));
                Assert.That(item.Magnitude, Is.EqualTo(entry.Support.IncomePerWave));
                Assert.That(session.Ink - ink, Is.EqualTo(wave * entry.Support.IncomePerWave));
                Assert.That(generator.GeneratorPaidWaves, Is.EqualTo(wave));
                session.Tick(.5f);
                Assert.That(Events(session, SimulationEventKind.GeneratorPaid, sequence).Length, Is.EqualTo(wave));
            }
        }

        [Test] public void AGeneratorBuiltDuringAnAssaultEarnsNoPaymentEventUntilItsFirstFullWave()
        {
            var rules = Rules(); var session = new GameSession(rules, starterDefenses: false);
            session.StartNextWave();
            var entry = Support(AssemblyStructureFamily.Generator, rules);
            var generator = Build(session, entry, new Cell(4, 4)); long sequence = session.EventBuffer.LatestSequence;
            Finish(session); int ink = session.Ink;
            Assert.That(Events(session, SimulationEventKind.GeneratorPaid, sequence), Is.Empty);
            Assert.That(generator.GeneratorPaidWaves, Is.Zero);
            session.StartNextWave(); session.Tick(.01f);
            Assert.That(Events(session, SimulationEventKind.GeneratorPaid, sequence).Single().Magnitude, Is.EqualTo(entry.Support.IncomePerWave));
            Assert.That(session.Ink - ink, Is.EqualTo(entry.Support.IncomePerWave));
        }

        [Test] public void AGeneratorThatDoesNotSurviveTheWaveCannotPublishOrCreditPayment()
        {
            var rules = Rules(); var session = new GameSession(rules, starterDefenses: false);
            var generator = Build(session, Support(AssemblyStructureFamily.Generator, rules), new Cell(4, 4)); Finish(session);
            session.StartNextWave(); generator.Health = 0;
            int ink = session.Ink; long sequence = session.EventBuffer.LatestSequence; session.Tick(.01f);
            Assert.That(session.CompletedWaves, Is.EqualTo(1));
            Assert.That(session.Ink, Is.EqualTo(ink)); Assert.That(generator.GeneratorPaidWaves, Is.Zero);
            Assert.That(Events(session, SimulationEventKind.GeneratorPaid, sequence), Is.Empty);
        }
    }
}
