using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class CompositeSupportTests
    {
        static StructureState Support(AssemblyStructureFamily family, int id, int x, int y,
            MaterialTier material = MaterialTier.Pencil, ComponentCatalog catalog = null, string utility = "none")
        {
            string name = family.ToString().ToLowerInvariant();
            var recipe = new AssemblyRecipe(new StructureRecipe(family,
                AssemblyResolverFixture.Part("site." + name), AssemblyResolverFixture.Part("function." + name),
                AssemblyResolverFixture.Part("shell.standard"), AssemblyResolverFixture.Part("utility." + utility)));
            var resolved = AssemblyResolverFixture.Resolve(recipe, catalog, material: material);
            return new StructureState(resolved) { Id = id, Cell = new Cell(x, y), IsComplete = true, BuildRemaining = 0 };
        }

        static StructureState Target(int id, int x, int y, bool modern = true)
        {
            var target = modern ? Support(AssemblyStructureFamily.Wall, id, x, y) :
                new StructureState { Id = id, Cell = new Cell(x, y), Kind = StructureKind.Wall,
                    MaxHealth = 100, IsComplete = true };
            target.Health = 20;
            return target;
        }

        static EnemyState Enemy(Point2 position) => new EnemyState { Id = 1, Position = position, Health = 100, MaxHealth = 100 };

        static void Apply(EnemyState enemy, ColorEffectPayload effect)
            => EnemyEffects.ApplySlow(enemy, effect.Strength, effect.Duration);

        static GameBalance SessionRules()
        {
            var balance = GameBalance.CreateDefault();
            balance.StartingInk = 10000; balance.PassiveInkPerSecond = 0;
            balance.PlanningSeconds = 3600; balance.EnableInkWells = false;
            return balance;
        }

        static ResolvedEntry Starter(int index, GameBalance balance, MaterialTier material = MaterialTier.Pencil)
        {
            var catalog = AssemblyResolverFixture.Catalog();
            return AssemblyResolverFixture.Resolve(catalog.Starters[index].Snapshot.Recipe, catalog, balance, material);
        }

        static StructureState Build(GameSession session, ResolvedEntry entry, Cell cell, InkColor color = InkColor.None)
        {
            Assert.That(session.TryBuildEntry(new[] { cell }, entry, entry.Material, color, out string reason), Is.True, reason);
            return session.StructureAt(cell);
        }

        static void FinishConstruction(GameSession session)
        {
            session.Tick(session.Structures.Max(s => s.BuildRemaining) + .05f);
            Assert.That(session.Structures.All(s => s.IsComplete && s.BuildRemaining <= 0), Is.True);
        }

        [Test] public void SessionRepairCompletesAndTicksOnlyTheNearestDamagedTarget()
        {
            var balance = SessionRules(); var session = new GameSession(balance, starterDefenses: false);
            var post = Build(session, Starter(6, balance), new Cell(4, 4));
            var near = Build(session, Starter(4, balance), new Cell(5, 4));
            var far = Build(session, Starter(4, balance), new Cell(7, 4));
            Assert.That(post.IsComplete, Is.False);
            FinishConstruction(session);
            near.Health = far.Health = 20;
            session.Tick(.5f);
            Assert.That(near.Health, Is.EqualTo(23).Within(.0001f));
            Assert.That(far.Health, Is.EqualTo(20));
            Assert.That(post.Health, Is.EqualTo(post.MaxHealth));
        }

        [Test] public void SessionTwoModernPostsRepairOneTargetAtSixPerSecond()
        {
            var balance = SessionRules(); var session = new GameSession(balance, starterDefenses: false);
            Build(session, Starter(6, balance), new Cell(4, 4));
            Build(session, Starter(6, balance), new Cell(6, 4));
            var target = Build(session, Starter(4, balance), new Cell(5, 4));
            FinishConstruction(session);
            target.Health = 20;
            session.Tick(.5f);
            Assert.That(target.Health, Is.EqualTo(23).Within(.0001f));
        }

        [Test] public void SessionMixedRepairUsesEightPerSecondAndKeepsLegacyCoverageOfOtherTargets()
        {
            var balance = SessionRules(); var session = new GameSession(balance, starterDefenses: false);
            Assert.That(session.TryBuild(new[] { new Cell(4, 4) }, StructureKind.RepairPost, MaterialTier.Pencil, out string reason), Is.True, reason);
            Build(session, Starter(6, balance), new Cell(6, 4));
            var shared = Build(session, Starter(4, balance), new Cell(5, 4));
            var legacyOnly = Build(session, Starter(4, balance), new Cell(4, 6));
            FinishConstruction(session);
            shared.Health = legacyOnly.Health = 20;
            session.Tick(.5f);
            Assert.That(shared.Health, Is.EqualTo(24).Within(.0001f), "8 legacy and 6 modern must aggregate by maximum.");
            Assert.That(legacyOnly.Health, Is.EqualTo(24).Within(.0001f), "Legacy posts retain their existing all-target coverage.");
        }

        [Test] public void SessionInkMaterialAndDeepInkKeepRepairAtSixForAColoredTower()
        {
            var balance = SessionRules(); balance.Progression.UpgradeEveryWaves = 1;
            var session = GameSession.CreatePrepared(balance, 7, Array.Empty<PreparedEnemy>(), redInk: 100);
            var post = Build(session, Starter(6, balance, MaterialTier.Ink), new Cell(4, 4));
            var target = Build(session, Starter(0, balance, MaterialTier.Ink), new Cell(5, 4), InkColor.Red);
            session.Tick(.01f);
            Assert.That(session.PendingUpgrade, Is.True);
            session.ChooseUpgrade(RunUpgrade.DeepInk);
            FinishConstruction(session);
            Assert.That(post.MaxHealth, Is.EqualTo(183.75f).Within(.0001f));
            Assert.That(target.MaxHealth, Is.EqualTo(144.375f).Within(.0001f));
            Assert.That(target.Color, Is.EqualTo(InkColor.Red));
            target.Health = 20;
            session.Tick(.5f);
            Assert.That(target.Health, Is.EqualTo(23).Within(.0001f));
        }

        [Test] public void SessionSlowCompletesAndItsResolvedTailExpiresAfterTheEnemyLeaves()
        {
            var balance = SessionRules();
            var session = GameSession.CreatePrepared(balance, 7, new[] { new PreparedEnemy(new Point2(5.4f, 4.5f), 1000) });
            var slow = Build(session, Starter(5, balance), new Cell(4, 4));
            Assert.That(slow.IsComplete, Is.False);
            FinishConstruction(session);
            var enemy = session.Enemies.Single();
            Assert.That(EnemyEffects.MovementMultiplier(enemy, balance.Effects), Is.EqualTo(.6f));
            enemy.Position = new Point2(7.5f, 4.5f);
            session.Tick(.59f);
            Assert.That(EnemyEffects.MovementMultiplier(enemy, balance.Effects), Is.EqualTo(.6f));
            session.Tick(.02f);
            Assert.That(EnemyEffects.MovementMultiplier(enemy, balance.Effects), Is.EqualTo(1));
        }

        [Test] public void RepairSelectsOnlyTheNearestDamagedStructureAtSixHealthPerSecond()
        {
            var post = Support(AssemblyStructureFamily.Repair, 1, 0, 0);
            var near = Target(2, 1, 0); var far = Target(3, 2, 0);
            var rates = StructureSupportRules.RepairRates(new[] { post, far, near });
            Assert.That(rates.Count, Is.EqualTo(1));
            Assert.That(rates[near], Is.EqualTo(6));
            Assert.That(StructureSupportRules.RepairedHealth(near, rates[near], .5f), Is.EqualTo(23));
            Assert.That(near.Health, Is.EqualTo(20), "The helper returns a result without publishing health.");
            Assert.That(far.Health, Is.EqualTo(20));
        }

        [TestCase(false)] [TestCase(true)]
        public void EqualRepairDistancesChooseLowerIdRegardlessOfInputOrder(bool reverse)
        {
            var post = Support(AssemblyStructureFamily.Repair, 1, 0, 0);
            var lowerId = Target(7, 3, 4); var higherId = Target(9, 4, 3);
            var candidates = new[] { post, higherId, lowerId };
            if (reverse) Array.Reverse(candidates);
            var rates = StructureSupportRules.RepairRates(candidates);
            Assert.That(rates.Keys, Is.EquivalentTo(new[] { lowerId }));
        }

        [Test] public void RepairIncludesTheFiveCellBoundaryAndRejectsTargetsBeyondIt()
        {
            var post = Support(AssemblyStructureFamily.Repair, 1, 0, 0);
            var boundary = Target(2, 3, 4); var outside = Target(3, 4, 4);
            Assert.That(StructureSupportRules.RepairRates(new[] { post, boundary }).Keys, Is.EquivalentTo(new[] { boundary }));
            Assert.That(StructureSupportRules.RepairRates(new[] { post, outside }), Is.Empty);
        }

        [TestCase("self")] [TestCase("modernPost")] [TestCase("legacyPost")]
        [TestCase("dead")] [TestCase("incomplete")] [TestCase("pending")] [TestCase("healthy")]
        public void RepairRejectsIneligibleTargets(string kind)
        {
            var post = Support(AssemblyStructureFamily.Repair, 1, 0, 0);
            var invalid = Target(2, 1, 0);
            switch (kind)
            {
                case "self": invalid = post; invalid.Health = 10; break;
                case "modernPost": invalid = Support(AssemblyStructureFamily.Repair, 2, 1, 0); invalid.Health = 10; break;
                case "legacyPost": invalid = new StructureState { Id = 2, Cell = new Cell(1, 0), Kind = StructureKind.RepairPost,
                    Health = 10, MaxHealth = 100, IsComplete = true }; break;
                case "dead": invalid.Health = 0; break;
                case "incomplete": invalid.IsComplete = false; break;
                case "pending": invalid.BuildRemaining = .01f; break;
                case "healthy": invalid.Health = invalid.MaxHealth; break;
            }
            Assert.That(StructureSupportRules.RepairRates(new[] { post, invalid }), Is.Empty);
        }

        [TestCase("dead")] [TestCase("incomplete")] [TestCase("pending")]
        public void InactiveRepairPostsCannotProduceRates(string kind)
        {
            var post = Support(AssemblyStructureFamily.Repair, 1, 0, 0);
            if (kind == "dead") post.Health = 0;
            if (kind == "incomplete") post.IsComplete = false;
            if (kind == "pending") post.BuildRemaining = .01f;
            Assert.That(StructureSupportRules.RepairRates(new[] { post, Target(2, 1, 0) }), Is.Empty);
        }

        [Test] public void LegacyRepairSourcesStayOutsideTheModernHelper()
        {
            var legacy = new StructureState { Id = 1, Cell = new Cell(0, 0), Kind = StructureKind.RepairPost,
                Health = 100, MaxHealth = 100, IsComplete = true };
            Assert.That(StructureSupportRules.RepairRates(new[] { legacy, Target(2, 1, 0) }), Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void RepairCanTargetBothLegacyAndModernStructures(bool modern)
        {
            var post = Support(AssemblyStructureFamily.Repair, 1, 0, 0); var target = Target(2, 1, 0, modern);
            var rates = StructureSupportRules.RepairRates(new[] { post, target });
            Assert.That(rates[target], Is.EqualTo(6));
            Assert.That(StructureSupportRules.RepairedHealth(target, rates[target], 1), Is.EqualTo(26));
        }

        [TestCase(false)] [TestCase(true)]
        public void TwoPostsOnOneTargetUseTheStrongerRateWithoutAdding(bool reverse)
        {
            var standard = Support(AssemblyStructureFamily.Repair, 1, 0, 0);
            var extended = Support(AssemblyStructureFamily.Repair, 2, 2, 0, utility: "range");
            var target = Target(3, 1, 0);
            var sources = new[] { standard, extended, target };
            if (reverse) Array.Reverse(sources);
            var rates = StructureSupportRules.RepairRates(sources);
            Assert.That(rates.Count, Is.EqualTo(1));
            Assert.That(rates[target], Is.EqualTo(6));
            Assert.That(StructureSupportRules.RepairedHealth(target, rates[target], 1), Is.EqualTo(26));
        }

        [Test] public void EachPostChoosesItsOwnNearestTargetFromTheSameHealthSnapshot()
        {
            var first = Support(AssemblyStructureFamily.Repair, 1, 0, 0);
            var second = Support(AssemblyStructureFamily.Repair, 2, 10, 0);
            var left = Target(3, 1, 0); var right = Target(4, 9, 0);
            var rates = StructureSupportRules.RepairRates(new[] { first, second, right, left });
            Assert.That(rates.Count, Is.EqualTo(2));
            Assert.That(rates[left], Is.EqualTo(6)); Assert.That(rates[right], Is.EqualTo(6));
            Assert.That(left.Health, Is.EqualTo(20)); Assert.That(right.Health, Is.EqualTo(20));
        }

        [Test] public void RepairClampsAtTheTargetsActualMaximumHealth()
        {
            var target = Target(1, 0, 0);
            target.MaxHealth = 350; target.Health = 349;
            Assert.That(StructureSupportRules.RepairedHealth(target, 6, 2), Is.EqualTo(350));
            Assert.That(target.Health, Is.EqualTo(349));
            Assert.That(StructureSupportRules.RepairedHealth(target, float.MaxValue, float.MaxValue), Is.EqualTo(350));
        }

        [Test] public void ZeroTimeOrZeroRateCannotAddHealth()
        {
            var target = Target(1, 0, 0);
            Assert.That(StructureSupportRules.RepairedHealth(target, 6, 0), Is.EqualTo(20));
            Assert.That(StructureSupportRules.RepairedHealth(target, 0, 10), Is.EqualTo(20));
        }

        [TestCase(MaterialTier.Pencil, InkColor.Red)]
        [TestCase(MaterialTier.Ink, InkColor.Blue)]
        [TestCase(MaterialTier.Marker, InkColor.Green)]
        public void MaterialColorAndBoostedSourceHealthDoNotMultiplyRepairRate(MaterialTier material, InkColor color)
        {
            var post = Support(AssemblyStructureFamily.Repair, 1, 0, 0, material);
            post.Color = color; post.Health = post.MaxHealth = 1000;
            var target = Target(2, 1, 0);
            var rates = StructureSupportRules.RepairRates(new[] { post, target });
            Assert.That(StructureSupportRules.RepairedHealth(target, rates[target], .5f), Is.EqualTo(23));
        }

        [Test] public void ReturnedRepairRatesAreOwnedAndReadOnly()
        {
            var post = Support(AssemblyStructureFamily.Repair, 1, 0, 0); var target = Target(2, 1, 0);
            var input = new List<StructureState> { post, target };
            var rates = StructureSupportRules.RepairRates(input);
            input.Clear(); post.Health = 0;
            Assert.That(rates[target], Is.EqualTo(6));
            Assert.Throws<NotSupportedException>(() => ((IDictionary<StructureState, float>)rates)[target] = 99);
        }

        [Test] public void SlowHasOneCellRadiusAndCapturesTheResolvedStrengthAndDuration()
        {
            var slow = Support(AssemblyStructureFamily.Slow, 1, 4, 4);
            Assert.That(StructureSupportRules.TryGetSlow(slow, new Point2(5.5f, 4.5f), out var effect), Is.True);
            Assert.That(effect.Color, Is.EqualTo(InkColor.Blue));
            Assert.That(effect.Strength, Is.EqualTo(.6f)); Assert.That(effect.Duration, Is.EqualTo(.6f));
            Assert.That(StructureSupportRules.TryGetSlow(slow, new Point2(5.5001f, 4.5f), out _), Is.False);
        }

        [TestCase("dead")] [TestCase("incomplete")] [TestCase("pending")]
        public void InactiveSlowStructuresCannotProduceEffects(string kind)
        {
            var slow = Support(AssemblyStructureFamily.Slow, 1, 0, 0);
            if (kind == "dead") slow.Health = 0;
            if (kind == "incomplete") slow.IsComplete = false;
            if (kind == "pending") slow.BuildRemaining = .01f;
            Assert.That(StructureSupportRules.TryGetSlow(slow, new Point2(.5f, .5f), out _), Is.False);
        }

        [Test] public void LegacySlowAndOtherModernFamiliesStayOutsideSlowSelection()
        {
            var legacy = new StructureState { Kind = StructureKind.TarPit, Cell = new Cell(0, 0), Health = 100, IsComplete = true };
            Assert.That(StructureSupportRules.TryGetSlow(legacy, new Point2(.5f, .5f), out _), Is.False);
            Assert.That(StructureSupportRules.TryGetSlow(Support(AssemblyStructureFamily.Repair, 1, 0, 0), new Point2(.5f, .5f), out _), Is.False);
        }

        [Test] public void SlowDurationComesFromTheOwnedSupportSnapshot()
        {
            var input = AssemblyResolverFixture.Input();
            AssemblyResolverFixture.Set(input, "function.slow", AssemblyStat.SlowSeconds, ModifierOperation.Set, .17f);
            var slow = Support(AssemblyStructureFamily.Slow, 1, 0, 0, catalog: AssemblyResolverFixture.Modified(input));
            AssemblyResolverFixture.Set(input, "function.slow", AssemblyStat.SlowSeconds, ModifierOperation.Set, 9);
            Assert.That(StructureSupportRules.TryGetSlow(slow, new Point2(.5f, .5f), out var effect), Is.True);
            Assert.That(effect.Duration, Is.EqualTo(.17f));
        }

        [Test] public void RepeatedSlowDoesNotMultiplyAndTheStrongestRespectsThePointFourFloor()
        {
            var ordinary = Support(AssemblyStructureFamily.Slow, 1, 0, 0);
            var input = AssemblyResolverFixture.Input();
            AssemblyResolverFixture.Set(input, "function.slow", AssemblyStat.Slow, ModifierOperation.Set, .2f);
            AssemblyResolverFixture.Set(input, "function.slow", AssemblyStat.SlowSeconds, ModifierOperation.Set, .2f);
            var strong = Support(AssemblyStructureFamily.Slow, 2, 0, 0, catalog: AssemblyResolverFixture.Modified(input));
            var enemy = Enemy(new Point2(.5f, .5f)); var rules = new EffectsRules();
            Assert.That(StructureSupportRules.TryGetSlow(ordinary, enemy.Position, out var normalEffect), Is.True);
            Assert.That(StructureSupportRules.TryGetSlow(strong, enemy.Position, out var strongEffect), Is.True);
            Apply(enemy, normalEffect); Apply(enemy, normalEffect);
            Assert.That(EnemyEffects.MovementMultiplier(enemy, rules), Is.EqualTo(.6f));
            Apply(enemy, strongEffect);
            Assert.That(EnemyEffects.MovementMultiplier(enemy, rules), Is.EqualTo(.4f));
            EnemyEffects.Advance(enemy, .25f, null, rules);
            Assert.That(EnemyEffects.MovementMultiplier(enemy, rules), Is.EqualTo(.6f));
            EnemyEffects.Advance(enemy, .36f, null, rules);
            Assert.That(EnemyEffects.MovementMultiplier(enemy, rules), Is.EqualTo(1));
        }

        [Test] public void SlowKeepsItsResolvedTailAfterLeavingTheRadiusThenExpires()
        {
            var source = Support(AssemblyStructureFamily.Slow, 1, 0, 0);
            var enemy = Enemy(new Point2(.5f, .5f)); var rules = new EffectsRules();
            Assert.That(StructureSupportRules.TryGetSlow(source, enemy.Position, out var effect), Is.True);
            Apply(enemy, effect);
            enemy.Position = new Point2(2, .5f);
            Assert.That(StructureSupportRules.TryGetSlow(source, enemy.Position, out _), Is.False);
            EnemyEffects.Advance(enemy, .59f, null, rules);
            Assert.That(EnemyEffects.MovementMultiplier(enemy, rules), Is.EqualTo(.6f));
            EnemyEffects.Advance(enemy, .02f, null, rules);
            Assert.That(EnemyEffects.MovementMultiplier(enemy, rules), Is.EqualTo(1));
        }

        [Test] public void MissingRepairInputsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => StructureSupportRules.RepairRates(null));
            Assert.Throws<ArgumentNullException>(() => StructureSupportRules.RepairedHealth(null, 6, 1));
        }

        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidRepairTimeOrRateCannotPoisonHealth(float value)
        {
            var target = Target(1, 0, 0);
            Assert.Catch<ArgumentException>(() => StructureSupportRules.RepairedHealth(target, 6, value));
            Assert.Catch<ArgumentException>(() => StructureSupportRules.RepairedHealth(target, value, 1));
            Assert.That(target.Health, Is.EqualTo(20));
        }
    }
}
