using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class GeneratorWaveTests
    {
        static ResolvedEntry GeneratorEntry(bool economy = false, GameBalance balance = null)
        {
            var recipe = new AssemblyRecipe(new StructureRecipe(AssemblyStructureFamily.Generator,
                AssemblyResolverFixture.Part("site.generator"), AssemblyResolverFixture.Part("function.generator"),
                AssemblyResolverFixture.Part("shell.standard"), AssemblyResolverFixture.Part(economy ? "utility.economy" : "utility.none")));
            return AssemblyResolverFixture.Resolve(recipe, balance: balance);
        }

        static StructureState Generator(int id = 1, bool economy = false)
            => new StructureState(GeneratorEntry(economy))
            { Id = id, Cell = new Cell(4 + id * 2, 4), IsComplete = true, BuildRemaining = 0 };

        static GameBalance SessionRules(int waves = 8)
        {
            var balance = GameBalance.CreateDefault();
            balance.StartingInk = 1000; balance.PlanningSeconds = balance.IntermissionSeconds = 3600;
            balance.PassiveInkPerSecond = balance.EarlyLaunchInkPerSecond = 0; balance.MaxEarlyLaunchInk = 0;
            balance.WaveInkGrant = 0; balance.EnableInkWells = false; balance.Progression.UpgradeEveryWaves = 0;
            balance.Waves.Clear();
            for (int i = 0; i < waves; i++) balance.Waves.Add(new WaveDefinition { Count = 0, InkGrant = 0 });
            return balance;
        }

        static StructureState Build(GameSession session, ResolvedEntry entry, Cell cell)
        {
            Assert.That(session.TryBuildEntry(new[] { cell }, entry, MaterialTier.Pencil, InkColor.None, out string reason), Is.True, reason);
            return session.StructureAt(cell);
        }

        static void FinishConstruction(GameSession session)
            => session.Tick(session.Structures.Max(s => s.BuildRemaining) + .05f);

        static void CompleteEmptyWave(GameSession session)
        { session.StartNextWave(); session.Tick(.01f); }

        [TestCase(false, 60, 80, 12)] [TestCase(true, 72, 68, 15)]
        public void AGeneratorPaysItsResolvedIncomeAfterEachFullySurvivedWave(bool economy, int cost, float health, int income)
        {
            var generator = Generator(economy: economy); var ledger = new GeneratorWaveLedger(); var run = Guid.NewGuid();
            Assert.That(generator.ResolvedEntry.Cost, Is.EqualTo(cost)); Assert.That(generator.MaxHealth, Is.EqualTo(health));
            Assert.That(generator.ResolvedEntry.Support.PaybackWaves(cost), Is.EqualTo(5));
            int earned = 0;
            for (int wave = 1; wave <= 5; wave++)
            {
                ledger.BeginWave(run, wave, new[] { generator });
                Assert.That(ledger.CompleteWave(run, wave, new[] { generator }), Is.EqualTo(income));
                Assert.That(ledger.CompleteWave(run, wave, new[] { generator }), Is.Zero);
                earned += income;
                if (wave < 5) Assert.That(earned, Is.LessThan(cost));
            }
            Assert.That(earned, Is.GreaterThanOrEqualTo(cost));
        }

        [Test] public void TwoVariantsPayOnlyInkAndEachEntityIsCountedOnce()
        {
            var ledger = new GeneratorWaveLedger(); var run = Guid.NewGuid();
            var standard = Generator(); var economy = Generator(2, true);
            var duplicated = new[] { standard, economy, standard, economy };
            ledger.BeginWave(run, 1, duplicated);
            Assert.That(ledger.CompleteWave(run, 1, duplicated), Is.EqualTo(27));
            Assert.That(standard.Health, Is.EqualTo(80)); Assert.That(economy.Health, Is.EqualTo(68));
            Assert.That(standard.IsComplete && economy.IsComplete, Is.True);
        }

        [TestCase("unfinished")] [TestCase("remaining")] [TestCase("dead")] [TestCase("negativeHealth")]
        [TestCase("nanHealth")] [TestCase("infiniteHealth")] [TestCase("zeroId")]
        [TestCase("legacy")] [TestCase("wall")] [TestCase("null")]
        public void BeginWaveIgnoresAnythingExceptAnAliveCompletedModernGenerator(string state)
        {
            var generator = Generator(); var ledger = new GeneratorWaveLedger(); var run = Guid.NewGuid();
            switch (state)
            {
                case "unfinished": generator.IsComplete = false; break;
                case "remaining": generator.BuildRemaining = .01f; break;
                case "dead": generator.Health = 0; break;
                case "negativeHealth": generator.Health = -1; break;
                case "nanHealth": generator.Health = float.NaN; break;
                case "infiniteHealth": generator.Health = float.PositiveInfinity; break;
                case "zeroId": generator.Id = 0; break;
                case "legacy": generator = new StructureState { Id = 1, Kind = StructureKind.Generator, Health = 80, IsComplete = true }; break;
                case "wall": generator = new StructureState(AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Catalog().Starters[4].Snapshot.Recipe))
                    { Id = 1, IsComplete = true, BuildRemaining = 0 }; break;
                case "null": generator = null; break;
            }
            ledger.BeginWave(run, 1, new[] { generator });
            if (generator != null) { generator.Health = generator.MaxHealth; generator.IsComplete = true; generator.BuildRemaining = 0; }
            Assert.That(ledger.CompleteWave(run, 1, new[] { generator }), Is.Zero);
        }

        [TestCase("removed")] [TestCase("dead")] [TestCase("unfinished")] [TestCase("remaining")]
        [TestCase("nanHealth")] [TestCase("infiniteHealth")] [TestCase("changedId")] [TestCase("replacement")]
        public void CompleteWaveRejectsEntitiesThatDidNotSurviveAsTheSameCompletedGenerator(string state)
        {
            var generator = Generator(); var ledger = new GeneratorWaveLedger(); var run = Guid.NewGuid();
            ledger.BeginWave(run, 1, new[] { generator });
            IReadOnlyList<StructureState> survivors = new[] { generator };
            switch (state)
            {
                case "removed": survivors = Array.Empty<StructureState>(); break;
                case "dead": generator.Health = 0; break;
                case "unfinished": generator.IsComplete = false; break;
                case "remaining": generator.BuildRemaining = .01f; break;
                case "nanHealth": generator.Health = float.NaN; break;
                case "infiniteHealth": generator.Health = float.PositiveInfinity; break;
                case "changedId": generator.Id = 2; break;
                case "replacement": survivors = new[] { Generator() }; break;
            }
            Assert.That(ledger.CompleteWave(run, 1, survivors), Is.Zero);
            generator.Health = generator.MaxHealth; generator.Id = 1; generator.IsComplete = true; generator.BuildRemaining = 0;
            Assert.That(ledger.CompleteWave(run, 1, new[] { generator }), Is.Zero);
        }

        [Test] public void BeginWaveOwnsItsSnapshotAndNewGeneratorsCannotBeInsertedDuringTheWave()
        {
            var first = Generator(); var late = Generator(2, true); var structures = new List<StructureState> { first };
            var ledger = new GeneratorWaveLedger(); var run = Guid.NewGuid();
            ledger.BeginWave(run, 1, structures); structures.Add(late);
            ledger.BeginWave(run, 1, structures);
            Assert.That(ledger.CompleteWave(run, 1, structures), Is.EqualTo(12));
        }

        [Test] public void BeginningTheSameWaveTwiceCannotReplaceItsOriginalEmptySnapshot()
        {
            var ledger = new GeneratorWaveLedger(); var run = Guid.NewGuid(); var generator = Generator();
            ledger.BeginWave(run, 1, Array.Empty<StructureState>());
            ledger.BeginWave(run, 1, new[] { generator });
            Assert.That(ledger.CompleteWave(run, 1, new[] { generator }), Is.Zero);
        }

        [Test] public void IncomeIsFrozenWhenTheWaveBegins()
        {
            var ledger = new GeneratorWaveLedger(); var run = Guid.NewGuid(); var generator = Generator();
            ledger.BeginWave(run, 1, new[] { generator }); generator.ResolvedEntry = GeneratorEntry(true);
            Assert.That(ledger.CompleteWave(run, 1, new[] { generator }), Is.EqualTo(12));
        }

        [Test] public void CompletionWithoutABeginDoesNotInventAPlayedWave()
        {
            var ledger = new GeneratorWaveLedger(); var run = Guid.NewGuid(); var generator = Generator();
            Assert.That(ledger.CompleteWave(run, 1, new[] { generator }), Is.Zero);
            ledger.BeginWave(run, 1, new[] { generator });
            Assert.That(ledger.CompleteWave(run, 1, new[] { generator }), Is.EqualTo(12));
        }

        [Test] public void CompletingAnotherWaveDoesNotConsumeTheActiveSnapshot()
        {
            var ledger = new GeneratorWaveLedger(); var run = Guid.NewGuid(); var generator = Generator();
            ledger.BeginWave(run, 2, new[] { generator });
            Assert.That(ledger.CompleteWave(run, 1, new[] { generator }), Is.Zero);
            Assert.That(ledger.CompleteWave(run, 3, new[] { generator }), Is.Zero);
            Assert.That(ledger.CompleteWave(run, 2, new[] { generator }), Is.EqualTo(12));
        }

        [Test] public void ANewerWaveRetiresAnAbandonedWaveAndOldEventsCannotReopenIt()
        {
            var ledger = new GeneratorWaveLedger(); var run = Guid.NewGuid(); var generator = Generator();
            ledger.BeginWave(run, 1, new[] { generator }); ledger.BeginWave(run, 2, new[] { generator });
            Assert.That(ledger.CompleteWave(run, 1, new[] { generator }), Is.Zero);
            Assert.That(ledger.CompleteWave(run, 2, new[] { generator }), Is.EqualTo(12));
            ledger.BeginWave(run, 1, new[] { generator }); ledger.BeginWave(run, 2, new[] { generator });
            Assert.That(ledger.CompleteWave(run, 1, new[] { generator }), Is.Zero);
            Assert.That(ledger.CompleteWave(run, 2, new[] { generator }), Is.Zero);
            ledger.BeginWave(run, 3, new[] { generator });
            Assert.That(ledger.CompleteWave(run, 3, new[] { generator }), Is.EqualTo(12));
        }

        [Test] public void OneSessionLedgerCannotBeReboundByEventsFromAnotherRun()
        {
            var ledger = new GeneratorWaveLedger(); var run = Guid.NewGuid(); var other = Guid.NewGuid(); var generator = Generator();
            ledger.BeginWave(run, 1, new[] { generator }); ledger.BeginWave(other, 1, new[] { generator });
            Assert.That(ledger.CompleteWave(other, 1, new[] { generator }), Is.Zero);
            Assert.That(ledger.CompleteWave(run, 1, new[] { generator }), Is.EqualTo(12));
            ledger.BeginWave(other, 2, new[] { generator });
            Assert.That(ledger.CompleteWave(other, 2, new[] { generator }), Is.Zero);
            ledger.BeginWave(run, 2, new[] { generator });
            Assert.That(ledger.CompleteWave(run, 2, new[] { generator }), Is.EqualTo(12));
        }

        [Test] public void LedgerDoesNotImposeAnEightWaveCampaignLimit()
        {
            var ledger = new GeneratorWaveLedger(); var run = Guid.NewGuid(); var generator = Generator();
            for (int wave = 1; wave <= 100; wave++)
            {
                ledger.BeginWave(run, wave, new[] { generator });
                Assert.That(ledger.CompleteWave(run, wave, new[] { generator }), Is.EqualTo(12));
            }
            ledger.BeginWave(run, 1, new[] { generator });
            Assert.That(ledger.CompleteWave(run, 1, new[] { generator }), Is.Zero);
        }

        [TestCase(true)] [TestCase(false)]
        public void InvalidArgumentsAreRejectedBeforeBindingOrConsumingTheLedger(bool begin)
        {
            var ledger = new GeneratorWaveLedger(); var run = Guid.NewGuid(); var generator = Generator();
            Action<Guid, int, IReadOnlyList<StructureState>> call = (id, wave, structures) =>
            { if (begin) ledger.BeginWave(id, wave, structures); else ledger.CompleteWave(id, wave, structures); };
            Assert.Throws<ArgumentException>(() => call(Guid.Empty, 1, new[] { generator }));
            Assert.Throws<ArgumentOutOfRangeException>(() => call(run, 0, new[] { generator }));
            Assert.Throws<ArgumentOutOfRangeException>(() => call(run, -1, new[] { generator }));
            Assert.Throws<ArgumentNullException>(() => call(run, 1, null));
            ledger.BeginWave(run, 1, new[] { generator });
            Assert.Throws<ArgumentNullException>(() => call(run, 1, null));
            Assert.That(ledger.CompleteWave(run, 1, new[] { generator }), Is.EqualTo(12));
        }

        [Test] public void EachNewSessionGetsANewRunIdentityAndOnlyActuallyCompletedWavesAreCounted()
        {
            var first = new GameSession(SessionRules(), starterDefenses: false);
            var second = new GameSession(SessionRules(), starterDefenses: false);
            Assert.That(first.RunId, Is.Not.EqualTo(Guid.Empty)); Assert.That(first.RunId, Is.Not.EqualTo(second.RunId));
            Assert.That(first.CompletedWaves, Is.Zero); first.StartNextWave();
            Assert.That(first.WaveIndex, Is.EqualTo(1)); Assert.That(first.CompletedWaves, Is.Zero);
            first.Tick(0); Assert.That(first.CompletedWaves, Is.Zero);
            first.Tick(.01f); Assert.That(first.CompletedWaves, Is.EqualTo(1));
            first.Tick(1); Assert.That(first.CompletedWaves, Is.EqualTo(1));
            first.StartNextWave(); Assert.That(first.CompletedWaves, Is.EqualTo(1));
            first.Tick(.01f); Assert.That(first.CompletedWaves, Is.EqualTo(2));
            Assert.That(second.CompletedWaves, Is.Zero);
        }

        [TestCase(false, 12)] [TestCase(true, 15)]
        public void SessionPaysGeneratorsOncePerCompleteWaveWithoutChangingMetaGold(bool economy, int income)
        {
            var balance = SessionRules(); var session = new GameSession(balance, starterDefenses: false);
            Build(session, GeneratorEntry(economy, balance), new Cell(4, 4)); FinishConstruction(session);
            int ink = session.Ink; session.Tick(10); Assert.That(session.Ink, Is.EqualTo(ink));
            CompleteEmptyWave(session);
            Assert.That(session.Ink, Is.EqualTo(ink + income)); Assert.That(session.GoldEarned, Is.EqualTo(8));
            Assert.That(session.CompletedWaves, Is.EqualTo(1)); session.Tick(10);
            Assert.That(session.Ink, Is.EqualTo(ink + income)); Assert.That(session.GoldEarned, Is.EqualTo(8));
            CompleteEmptyWave(session); Assert.That(session.Ink, Is.EqualTo(ink + income * 2));
            Assert.That(session.GoldEarned, Is.EqualTo(16)); Assert.That(session.CompletedWaves, Is.EqualTo(2));
        }

        [Test] public void SessionDoesNotPayAGeneratorBuiltDuringTheWaveUntilTheNextFullWave()
        {
            var balance = SessionRules(); var session = new GameSession(balance, starterDefenses: false);
            session.StartNextWave(); Build(session, GeneratorEntry(balance: balance), new Cell(4, 4)); FinishConstruction(session);
            Assert.That(session.CompletedWaves, Is.EqualTo(1)); Assert.That(session.Ink, Is.EqualTo(940));
            CompleteEmptyWave(session); Assert.That(session.Ink, Is.EqualTo(952));
        }

        [Test] public void FinishingConstructionOnTheCompletionTickDoesNotEarnAFullWave()
        {
            var balance = SessionRules(); var session = new GameSession(balance, starterDefenses: false);
            var generator = Build(session, GeneratorEntry(balance: balance), new Cell(4, 4)); generator.BuildRemaining = .001f;
            session.StartNextWave(); session.Tick(.01f);
            Assert.That(generator.IsComplete, Is.True); Assert.That(session.Ink, Is.EqualTo(940));
            CompleteEmptyWave(session); Assert.That(session.Ink, Is.EqualTo(952));
        }

        [Test] public void SellingAGeneratorBeforeCompletionPaysNeitherIncomeNorRefund()
        {
            var balance = SessionRules(); var session = new GameSession(balance, starterDefenses: false); var cell = new Cell(4, 4);
            Build(session, GeneratorEntry(balance: balance), cell); FinishConstruction(session); session.StartNextWave();
            Assert.That(session.TryErase(cell, out int refund), Is.True); Assert.That(refund, Is.Zero);
            session.Tick(.01f); Assert.That(session.Ink, Is.EqualTo(940)); Assert.That(session.CompletedWaves, Is.EqualTo(1));
        }

        [Test] public void TwoGeneratorsShareOneLimitAcrossVariantsAndADestroyedGeneratorReleasesCapacity()
        {
            var balance = SessionRules(); var session = new GameSession(balance, starterDefenses: false);
            var standard = GeneratorEntry(balance: balance); var economy = GeneratorEntry(true, balance);
            var first = Build(session, standard, new Cell(4, 4)); Build(session, economy, new Cell(6, 4));
            int ink = session.Ink;
            Assert.That(session.TryBuildEntry(new[] { new Cell(8, 4) }, economy, MaterialTier.Pencil, InkColor.None, out _), Is.False);
            Assert.That(session.Ink, Is.EqualTo(ink)); first.Health = 0;
            Assert.That(session.TryBuildEntry(new[] { new Cell(8, 4) }, economy, MaterialTier.Pencil, InkColor.None, out string reason), Is.True, reason);
            Assert.That(session.Ink, Is.EqualTo(ink - 72));
        }

        [TestCase(MaterialTier.Ink)] [TestCase(MaterialTier.Marker)]
        public void GeneratorRejectsOtherMaterialsWithoutSpendingInk(MaterialTier material)
        {
            var balance = SessionRules(); var session = new GameSession(balance, starterDefenses: false);
            Assert.That(session.TryBuildEntry(new[] { new Cell(4, 4) }, GeneratorEntry(balance: balance), material, InkColor.None, out _), Is.False);
            Assert.That(session.Ink, Is.EqualTo(1000)); Assert.That(session.Structures, Is.Empty);
        }

        [Test] public void RunUpgradesDoNotMultiplyGeneratorIncomeOrCreateARefund()
        {
            var balance = SessionRules(); balance.Progression.UpgradeEveryWaves = 1;
            var session = new GameSession(balance, starterDefenses: false); var cell = new Cell(4, 4);
            Build(session, GeneratorEntry(balance: balance), cell); FinishConstruction(session);
            CompleteEmptyWave(session); Assert.That(session.PendingUpgrade, Is.True); session.ChooseUpgrade(RunUpgrade.DeepInk);
            int ink = session.Ink; CompleteEmptyWave(session); Assert.That(session.Ink, Is.EqualTo(ink + 12));
            Assert.That(session.PendingUpgrade, Is.True); session.ChooseUpgrade(RunUpgrade.Salvage); ink = session.Ink;
            CompleteEmptyWave(session); Assert.That(session.Ink, Is.EqualTo(ink + 12));
            Assert.That(session.TryErase(cell, out int refund), Is.True); Assert.That(refund, Is.Zero);
        }

        [Test] public void EmptyCampaignCannotInventGeneratorIncomeOrCompletedWaves()
        {
            var balance = SessionRules(0); var session = new GameSession(balance, starterDefenses: false);
            Build(session, GeneratorEntry(balance: balance), new Cell(4, 4)); FinishConstruction(session);
            session.StartNextWave(); session.Tick(100);
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Victory)); Assert.That(session.CompletedWaves, Is.Zero);
            Assert.That(session.GoldEarned, Is.Zero); Assert.That(session.Ink, Is.EqualTo(940));
        }
    }
}
