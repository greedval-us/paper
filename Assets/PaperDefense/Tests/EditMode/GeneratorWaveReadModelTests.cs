using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class GeneratorWaveReadModelTests
    {
        static ResolvedEntry Generator() => AssemblyResolverFixture.Resolve(new AssemblyRecipe(new StructureRecipe(
            AssemblyStructureFamily.Generator, AssemblyResolverFixture.Part("site.generator"),
            AssemblyResolverFixture.Part("function.generator"), AssemblyResolverFixture.Part("shell.standard"),
            AssemblyResolverFixture.Part("utility.none"))));
        static StructureState Entity(int id) => new StructureState(Generator())
            { Id = id, IsComplete = true, BuildRemaining = 0, Cell = new Cell(4 + id * 2, 4) };

        [Test] public void PaidWavesBelongToEachSurvivingGeneratorAndDuplicateCompletionDoesNotIncrement()
        {
            var ledger = new GeneratorWaveLedger(); var run = Guid.NewGuid();
            var first = Entity(1); var late = Entity(2);
            ledger.BeginWave(run, 1, new[] { first });
            Assert.That(ledger.CompleteWave(run, 1, new[] { first, late }), Is.EqualTo(12));
            Assert.That(first.GeneratorPaidWaves, Is.EqualTo(1));
            Assert.That(late.GeneratorPaidWaves, Is.Zero);
            Assert.That(ledger.CompleteWave(run, 1, new[] { first, late }), Is.Zero);
            Assert.That(first.GeneratorPaidWaves, Is.EqualTo(1));
            ledger.BeginWave(run, 2, new[] { first, late });
            Assert.That(ledger.CompleteWave(run, 2, new[] { first, late }), Is.EqualTo(24));
            Assert.That(first.GeneratorPaidWaves, Is.EqualTo(2));
            Assert.That(late.GeneratorPaidWaves, Is.EqualTo(1));
        }

        [Test] public void AReplacementWithTheSameIdCannotInheritTheOriginalGeneratorsPaidWaves()
        {
            var ledger = new GeneratorWaveLedger(); var run = Guid.NewGuid(); var original = Entity(1);
            ledger.BeginWave(run, 1, new[] { original }); ledger.CompleteWave(run, 1, new[] { original });
            ledger.BeginWave(run, 2, new[] { original }); var replacement = Entity(1);
            Assert.That(ledger.CompleteWave(run, 2, new[] { replacement }), Is.Zero);
            Assert.That(original.GeneratorPaidWaves, Is.EqualTo(1));
            Assert.That(replacement.GeneratorPaidWaves, Is.Zero);
        }

        [Test] public void TheReadModelReportsPaidWavesAsAValueSnapshot()
        {
            var rules = GameBalance.CreateDefault(); rules.StartingInk = 1000;
            rules.PassiveInkPerSecond = 0; rules.EnableInkWells = false;
            rules.PlanningSeconds = rules.IntermissionSeconds = 3600; rules.Progression.UpgradeEveryWaves = 0;
            rules.Waves.Clear();
            for (int i = 0; i < 8; i++) rules.Waves.Add(new WaveDefinition { Count = 0, InkGrant = 0 });
            var session = new GameSession(rules, starterDefenses: false); var cell = new Cell(4, 4);
            Assert.That(session.TryBuildEntry(new[] { cell }, Generator(), MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
            session.Tick(session.Structures.Single().BuildRemaining + .05f);
            var model = new RunReadModel(session);
            Assert.That(model.StructureAt(cell).Value.GeneratorPaidWaves, Is.Zero);
            session.StartNextWave(); session.Tick(.01f);
            var first = model.StructureAt(cell).Value;
            Assert.That(first.GeneratorPaidWaves, Is.EqualTo(1));
            session.StartNextWave(); session.Tick(.01f);
            Assert.That(model.StructureAt(cell).Value.GeneratorPaidWaves, Is.EqualTo(2));
            Assert.That(first.GeneratorPaidWaves, Is.EqualTo(1));
        }
    }
}
