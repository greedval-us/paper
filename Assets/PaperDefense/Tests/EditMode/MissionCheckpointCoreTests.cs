using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class MissionCheckpointCoreTests
    {
        static MethodInfo CaptureMethod()
        {
            var method = typeof(GameSession).GetMethod("TryCaptureIntermissionCheckpoint");
            Assert.That(method, Is.Not.Null, "Missing intermission checkpoint capture API.");
            return method;
        }
        static GameBalance Rules()
        {
            CaptureMethod();
            var rules = GameBalance.CreateDefault();
            rules.StartingInk = 10000; rules.PlanningSeconds = rules.IntermissionSeconds = 3600;
            rules.EnableInkWells = false; rules.EarlyLaunchInkPerSecond = 0;
            rules.PassiveInkPerSecond = .37f; rules.Progression.UpgradeEveryWaves = 0;
            rules.Waves = new List<WaveDefinition> {
                new WaveDefinition { Count = 0, InkGrant = 7 },
                new WaveDefinition { Count = 0, InkGrant = 11 },
                new WaveDefinition { Count = 0, InkGrant = 13 }
            };
            return rules;
        }
        static GameSession Intermission(GameBalance rules = null, int seed = 989)
        {
            var session = new GameSession(rules ?? Rules(), seed, false);
            session.StartNextWave(); session.Tick(.01f);
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Intermission));
            return session;
        }
        static bool Capture(GameSession session, out object data, out string reason)
        {
            var arguments = new object[] { null, null };
            bool captured = (bool)CaptureMethod().Invoke(session, arguments);
            data = arguments[0]; reason = (string)arguments[1]; return captured;
        }
        static object Capture(GameSession session)
        {
            Assert.That(Capture(session, out var data, out var reason), Is.True, reason);
            Assert.That(data, Is.Not.Null); return data;
        }
        static bool Restore(GameBalance rules, object data, out GameSession session, out string reason)
        {
            var method = typeof(GameSession).GetMethod("TryRestoreIntermissionCheckpoint", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Missing intermission checkpoint restore API.");
            var arguments = new object[] { rules, data, null, null };
            bool restored = (bool)method.Invoke(null, arguments);
            session = (GameSession)arguments[2]; reason = (string)arguments[3]; return restored;
        }
        static GameSession Restore(GameBalance rules, object data)
        {
            Assert.That(Restore(rules, data, out var session, out var reason), Is.True, reason);
            Assert.That(session, Is.Not.Null); return session;
        }
        static object JsonCopy(object data) => JsonUtility.FromJson(JsonUtility.ToJson(data), data.GetType());
        static FieldInfo Field(object target, string name)
        {
            var field = target.GetType().GetField(name);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name); return field;
        }
        static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target);
        static System.Random Rng(GameSession session) => (System.Random)typeof(GameSession)
            .GetField("random", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
        static double MixedDraw(System.Random random, int index)
        {
            switch (index % 6)
            {
                case 0: return random.Next();
                case 1: return random.Next(19);
                case 2: return random.Next(2, 37);
                case 3: return random.NextDouble();
                case 4: return random.Next(0);
                default: return random.Next(4, 4);
            }
        }
        static ResolvedEntry Tower(GameBalance rules) => AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(), balance: rules);
        static StructureState Build(GameSession session, ResolvedEntry entry, Cell cell)
        {
            Assert.That(session.TryBuildEntry(new[] { cell }, entry, entry.Material, InkColor.None, out var reason,
                sourceAlbumEntryId: "checkpoint.page", facingDegrees: 37), Is.True, reason);
            return session.StructureAt(cell);
        }
        static void EqualEconomy(GameSession a, GameSession b)
        {
            Assert.That(b.RunId, Is.EqualTo(a.RunId)); Assert.That(b.Phase, Is.EqualTo(a.Phase));
            Assert.That(b.Ink, Is.EqualTo(a.Ink)); Assert.That(b.GoldEarned, Is.EqualTo(a.GoldEarned));
            Assert.That(b.BaseHealth, Is.EqualTo(a.BaseHealth)); Assert.That(b.WaveIndex, Is.EqualTo(a.WaveIndex));
            Assert.That(b.CompletedWaves, Is.EqualTo(a.CompletedWaves)); Assert.That(b.TickIndex, Is.EqualTo(a.TickIndex));
            Assert.That(b.Elapsed, Is.EqualTo(a.Elapsed)); Assert.That(b.Countdown, Is.EqualTo(a.Countdown));
            foreach (InkColor color in Enum.GetValues(typeof(InkColor))) Assert.That(b.ColoredInk(color), Is.EqualTo(a.ColoredInk(color)));
        }

        [TestCase(RunPhase.Planning)] [TestCase(RunPhase.Assault)]
        [TestCase(RunPhase.Victory)] [TestCase(RunPhase.Defeat)]
        public void OnlyIntermissionCanBeCaptured(RunPhase phase)
        {
            var rules = Rules(); var session = new GameSession(rules, starterDefenses: false);
            if (phase == RunPhase.Assault) session.StartNextWave();
            if (phase == RunPhase.Victory)
            {
                for (int wave = 0; wave < rules.Waves.Count; wave++) { session.StartNextWave(); session.Tick(.01f); }
            }
            if (phase == RunPhase.Defeat)
            {
                session.StartNextWave(); session.DamageObjective(-1, 100000); session.Tick(.01f);
            }
            Assert.That(session.Phase, Is.EqualTo(phase));
            Assert.That(Capture(session, out var data, out var reason), Is.False);
            Assert.That(data, Is.Null); Assert.That(reason, Is.Not.Empty);
        }

        [Test]
        public void JsonRoundTripPreservesIdentityCountdownAndFractionalPassiveIncomeWithoutOfflineProgress()
        {
            var rules = Rules(); var original = Intermission(rules); original.Tick(.73f);
            original.DamageObjective(-1, 13, new Point2(6.5f, 7.5f));
            var data = Capture(original); var restored = Restore(rules, JsonCopy(data));
            EqualEconomy(original, restored);
            Assert.That(restored.EventBuffer.Count, Is.Zero, "Loading must not replay combat feedback.");
            for (int tick = 0; tick < 500; tick++) { original.Tick(1f / 30); restored.Tick(1f / 30); }
            EqualEconomy(original, restored);
            Set(data, "Ink", 0);
            Assert.That(restored.Ink, Is.GreaterThan(0), "Restored state must not alias the mutable DTO.");
        }

        [Test]
        public void SeededMixedRandomCallsRemainCompatibleAndResumeAtExactlyTheNextDraw()
        {
            const int seed = -1777; var rules = Rules(); var original = Intermission(rules, seed);
            var baseline = new System.Random(seed); var live = Rng(original);
            for (int i = 0; i < 1000; i++) Assert.That(MixedDraw(live, i), Is.EqualTo(MixedDraw(baseline, i)), "draw " + i);
            var data = Capture(original);
            Assert.That(Get<long>(data, "RandomDrawCount"), Is.EqualTo(1000));
            var restored = Restore(rules, JsonCopy(data));
            for (int i = 1000; i < 1600; i++)
            {
                double expected = MixedDraw(baseline, i);
                Assert.That(MixedDraw(live, i), Is.EqualTo(expected));
                Assert.That(MixedDraw(Rng(restored), i), Is.EqualTo(expected), "restored draw " + i);
            }
        }

        [Test]
        public void NextWaveUsesTheSameSpawnSequenceIdsAndStatisticsAfterResume()
        {
            var rules = Rules(); rules.Waves[1].Count = 30; rules.Waves[1].Sides = 15;
            rules.Waves[1].RunnerFraction = .3f; rules.Waves[1].GnawerFraction = .2f;
            rules.Waves[1].SpawnInterval = .06f;
            var original = Intermission(rules);
            for (int i = 0; i < 41; i++) MixedDraw(Rng(original), i);
            var restored = Restore(rules, JsonCopy(Capture(original)));
            original.StartNextWave(); restored.StartNextWave();
            for (int tick = 0; tick < 70; tick++) { original.Tick(1f / 30); restored.Tick(1f / 30); }
            EqualEconomy(original, restored);
            Assert.That(restored.Enemies.Count, Is.EqualTo(original.Enemies.Count));
            for (int i = 0; i < original.Enemies.Count; i++)
            {
                var a = original.Enemies[i]; var b = restored.Enemies[i];
                Assert.That(b.Id, Is.EqualTo(a.Id)); Assert.That(b.Kind, Is.EqualTo(a.Kind));
                Assert.That(b.Position, Is.EqualTo(a.Position)); Assert.That(b.Health, Is.EqualTo(a.Health));
            }
            Assert.That(restored.Statistics.TotalSpawnedEnemies, Is.EqualTo(original.Statistics.TotalSpawnedEnemies));
        }

        [Test]
        public void ConstructionHealthFacingPriorityAndPaidRefundSurviveRatherThanRebuildingTheTower()
        {
            var rules = Rules(); var original = Intermission(rules); var cell = new Cell(5, 5);
            var tower = Build(original, Tower(rules), cell); original.Tick(.1f);
            tower.Health *= .4f; original.SetPriority(cell, TargetPriority.Strongest);
            var restored = Restore(rules, JsonCopy(Capture(original))); var copy = restored.StructureAt(cell);
            Assert.That(copy.Id, Is.EqualTo(tower.Id)); Assert.That(copy.SourceAlbumEntryId, Is.EqualTo("checkpoint.page"));
            Assert.That(copy.BuildRemaining, Is.EqualTo(tower.BuildRemaining)); Assert.That(copy.Health, Is.EqualTo(tower.Health));
            Assert.That(copy.FacingDegrees, Is.EqualTo(37)); Assert.That(copy.Priority, Is.EqualTo(TargetPriority.Strongest));
            Assert.That(copy.ResolvedEntry.Weapons[0].Damage, Is.EqualTo(tower.ResolvedEntry.Weapons[0].Damage));
            Assert.That(original.TryErase(cell, out int a), Is.True); Assert.That(restored.TryErase(cell, out int b), Is.True);
            Assert.That(b, Is.EqualTo(a)); EqualEconomy(original, restored);
            var aNew = Build(original, Tower(rules), cell); var bNew = Build(restored, Tower(rules), cell);
            Assert.That(bNew.Id, Is.EqualTo(aNew.Id), "Removed entity IDs must not be reused after resume.");
        }

        [Test]
        public void PendingColorModernizationCompletesOnceWithPreservedMagazineAndNoSecondPayment()
        {
            var rules = Rules(); rules.Mechanics.FieldModernization = true; rules.Mechanics.MaximumModernizationRank = 3;
            rules.Mechanics.WaveInkFlasks = true;
            var original = Intermission(rules); var tower = Build(original, Tower(rules), new Cell(5, 5));
            original.Tick(tower.BuildRemaining + .1f);
            Assert.That(original.TryClaimInkFlask(original.RunId, 1, InkColor.Red, out var reason), Is.True, reason);
            var quote = original.QuoteColorModernization(tower.Id, InkColor.Red);
            Assert.That(original.TryModernize(quote, out reason), Is.True, reason);
            tower.GetMountState(0).Cycle = new WeaponCycleState(0, true, .625);
            original.Tick(.17f);
            var restored = Restore(rules, JsonCopy(Capture(original))); var copy = restored.StructureAt(tower.Cell);
            Assert.That(copy.Color, Is.EqualTo(InkColor.Red)); Assert.That(copy.Modernization.ColorRank, Is.Zero);
            Assert.That(copy.Modernization.WorkRemaining, Is.EqualTo(tower.Modernization.WorkRemaining));
            Assert.That(copy.GetMountState(0).Cycle.RemainingWork, Is.EqualTo(.625));
            Assert.That(restored.TryClaimInkFlask(restored.RunId, 1, InkColor.Blue, out _), Is.False);
            int paidColor = original.ColoredInk(InkColor.Red);
            for (int tick = 0; tick < 150; tick++) { original.Tick(1f / 30); restored.Tick(1f / 30); }
            Assert.That(copy.Modernization.ColorRank, Is.EqualTo(1)); Assert.That(copy.Modernization.IsWorking, Is.False);
            Assert.That(restored.ColoredInk(InkColor.Red), Is.EqualTo(paidColor)); EqualEconomy(original, restored);
            Assert.That(copy.GetMountState(0).Cycle.RemainingWork, Is.EqualTo(tower.GetMountState(0).Cycle.RemainingWork));
        }

        [Test]
        public void GeneratorPaidWavesAndRepairCarryForwardWithoutPayingTheSavedWaveAgain()
        {
            var rules = Rules(); var original = new GameSession(rules, 989, false);
            var generator = AssemblyResolverFixture.Resolve(new AssemblyRecipe(new StructureRecipe(AssemblyStructureFamily.Generator,
                AssemblyResolverFixture.Part("site.generator"), AssemblyResolverFixture.Part("function.generator"),
                AssemblyResolverFixture.Part("shell.standard"), AssemblyResolverFixture.Part("utility.none"))), balance: rules);
            var generating = Build(original, generator, new Cell(5, 5));
            Assert.That(original.TryBuild(new[] { new Cell(8, 5) }, StructureKind.RepairPost, MaterialTier.Pencil, out var reason), Is.True, reason);
            original.Tick(10); generating.Health *= .5f;
            original.StartNextWave(); original.Tick(.01f);
            var restored = Restore(rules, JsonCopy(Capture(original))); var copy = restored.StructureAt(generating.Cell);
            Assert.That(copy.GeneratorPaidWaves, Is.EqualTo(1));
            for (int tick = 0; tick < 30; tick++) { original.Tick(1f / 30); restored.Tick(1f / 30); }
            Assert.That(copy.Health, Is.EqualTo(generating.Health)); Assert.That(copy.GeneratorPaidWaves, Is.EqualTo(1));
            original.StartNextWave(); restored.StartNextWave(); original.Tick(.01f); restored.Tick(.01f);
            Assert.That(copy.GeneratorPaidWaves, Is.EqualTo(2)); EqualEconomy(original, restored);
            Assert.That(restored.Statistics.TotalGeneratorIncome, Is.EqualTo(original.Statistics.TotalGeneratorIncome));
            Assert.That(restored.Statistics.TotalRepairedHealth, Is.EqualTo(original.Statistics.TotalRepairedHealth));
        }

        [Test]
        public void CorpseMarksAndManualCleanupCooldownKeepTheirRemainingTimes()
        {
            var rules = Rules(); rules.EnableEnemyMechanics = true;
            var original = GameSession.CreatePrepared(rules, 989, Array.Empty<PreparedEnemy>(),
                new[] { new PreparedBlot(new Cell(3, 3), 8), new PreparedBlot(new Cell(20, 20), 9) });
            original.Tick(.01f);
            Assert.That(original.TryBlotter(new Cell(3, 3), out var reason), Is.True, reason); original.Tick(.21f);
            var restored = Restore(rules, JsonCopy(Capture(original)));
            Assert.That(restored.Blots.Single().Cell, Is.EqualTo(new Cell(20, 20)));
            Assert.That(restored.Blots.Single().Remaining, Is.EqualTo(original.Blots.Single().Remaining));
            Assert.That(restored.BlotterCooldownRemaining, Is.EqualTo(original.BlotterCooldownRemaining));
            Assert.That(restored.TryBlotter(new Cell(20, 20), out _), Is.False);
            for (int tick = 0; tick < 300; tick++) { original.Tick(1f / 30); restored.Tick(1f / 30); }
            Assert.That(restored.Blots, Is.Empty); EqualEconomy(original, restored);
        }

        [Test]
        public void DefeatEvidenceAndEconomicStatisticsAreOwnedAndAccumulateAfterResume()
        {
            var rules = Rules(); var original = Intermission(rules);
            original.DamageObjective(-1, 9, new Point2(4.5f, 6.5f));
            original.Statistics.RecordPositionActive(1, new Cell(7, 7), "weapon.gun", 17);
            original.Statistics.RecordLoss(new Cell(8, 8), original.TickIndex, original.Elapsed, StructureKind.Wall, MaterialTier.Pencil, 12, waveIndex: 1);
            var data = Capture(original); var restored = Restore(rules, JsonCopy(data));
            original.DamageObjective(-1, 3, new Point2(4.5f, 6.5f)); restored.DamageObjective(-1, 3, new Point2(4.5f, 6.5f));
            Assert.That(restored.Statistics.BaseDamageTaken, Is.EqualTo(12));
            Assert.That(restored.Statistics.ApproachLosses.Single().Hits, Is.EqualTo(2));
            Assert.That(restored.Statistics.PositionActivities.Single().ActiveSeconds, Is.EqualTo(17));
            Assert.That(restored.Statistics.WaveLosses.Single().DestroyedStructures, Is.EqualTo(1));
            Assert.That(restored.Statistics.Breaches.Single().Investment, Is.EqualTo(12));
            var again = Restore(rules, data);
            Assert.That(again.Statistics.BaseDamageTaken, Is.EqualTo(9), "Saved aggregates must not alias a running session.");
        }

        [Test]
        public void PendingGlobalUpgradeAndChosenMultipliersContinueWithoutGrantingSalvageTwice()
        {
            var rules = Rules(); rules.Progression.UpgradeEveryWaves = 1; rules.Progression.LastUpgradeWave = 3;
            var original = Intermission(rules);
            Assert.That(original.PendingUpgrade, Is.True);
            var restored = Restore(rules, JsonCopy(Capture(original)));
            restored.StartNextWave(); Assert.That(restored.Phase, Is.EqualTo(RunPhase.Intermission));
            original.ChooseUpgrade(RunUpgrade.Salvage); restored.ChooseUpgrade(RunUpgrade.Salvage);
            EqualEconomy(original, restored);
            restored = Restore(rules, JsonCopy(Capture(restored)));
            restored.ChooseUpgrade(RunUpgrade.Salvage); EqualEconomy(original, restored);
            original.StartNextWave(); restored.StartNextWave(); original.Tick(.01f); restored.Tick(.01f);
            EqualEconomy(original, restored);
        }

        [Test]
        public void LegacyBlueprintKeepsItsOwnedDrawingAndMountCooldown()
        {
            var rules = Rules(); var original = Intermission(rules);
            var blueprint = new WorkshopProfile().GetLoadoutBlueprints().First(value => value != null);
            Assert.That(original.TryBuildBlueprint(new[] { new Cell(5, 5) }, blueprint, MaterialTier.Pencil,
                InkColor.None, out var reason, facingDegrees: 123), Is.True, reason);
            var tower = original.StructureAt(new Cell(5, 5)); tower.Cooldown = .77f;
            var restored = Restore(rules, JsonCopy(Capture(original))); var copy = restored.StructureAt(tower.Cell);
            Assert.That(copy.Blueprint.Id, Is.EqualTo(tower.Blueprint.Id));
            Assert.That(copy.Blueprint.Drawing.GeometryHash, Is.EqualTo(tower.Blueprint.Drawing.GeometryHash));
            Assert.That(copy.Cooldown, Is.EqualTo(.77f)); Assert.That(copy.FacingDegrees, Is.EqualTo(123));
            for (int i = 0; i < 100; i++) { original.Tick(1f / 30); restored.Tick(1f / 30); }
            Assert.That(copy.Cooldown, Is.EqualTo(tower.Cooldown)); Assert.That(copy.BuildRemaining, Is.EqualTo(tower.BuildRemaining));
        }

        [Test]
        public void SharedColorAllowanceRetainsSpentTargetsUntilTheExistingWindowExpires()
        {
            var rules = Rules(); rules.Effects.LimitTowerColor = true; rules.Effects.ColorWindowSeconds = 1; rules.Effects.ColorTargetsPerWindow = 3;
            var original = GameSession.CreatePrepared(rules, 989, new[] {
                new PreparedEnemy(new Point2(3.5f, 3.5f), 1), new PreparedEnemy(new Point2(4.5f, 3.5f), 1)
            }, redInk: 100);
            foreach (var enemy in original.Enemies) enemy.Health = 0;
            original.Tick(.01f);
            var tower = Build(original, Tower(rules), new Cell(5, 5));
            tower.ColorBudget = new TowerColorBudget(1, 3);
            Assert.That(tower.ColorBudget.TryApply(1, original.Elapsed), Is.True);
            var restored = Restore(rules, JsonCopy(Capture(original))); var copy = restored.StructureAt(tower.Cell);
            Assert.That(copy.ColorBudget.TryApply(1, original.Elapsed + .1), Is.False, "Resume cannot refresh a spent target allowance.");
            Assert.That(copy.ColorBudget.TryApply(2, original.Elapsed + .1), Is.True);
            Assert.That(copy.ColorBudget.TryApply(3, original.Elapsed + .1), Is.True);
            Assert.That(copy.ColorBudget.TryApply(4, original.Elapsed + .1), Is.False);
            Assert.That(copy.ColorBudget.TryApply(1, original.Elapsed + 1.01), Is.True);
        }

        [Test]
        public void PrismResumesItsExistingPulseDeadlineAndNextColorInsteadOfFiringAgainImmediately()
        {
            var rules = Rules();
            var original = GameSession.CreatePrepared(rules, 989,
                new[] { new PreparedEnemy(new Point2(6.5f, 5.5f), 10000) });
            var prism = AssemblyResolverFixture.Resolve(new AssemblyRecipe(new StructureRecipe(AssemblyStructureFamily.Prism,
                AssemblyResolverFixture.Part("site.prism"), AssemblyResolverFixture.Part("function.prism"),
                AssemblyResolverFixture.Part("shell.standard"), AssemblyResolverFixture.Part("utility.none"))), balance: rules);
            var tower = Build(original, prism, new Cell(5, 5));
            original.Tick(tower.BuildRemaining + .1f);
            Assert.That(tower.NextPrismColor, Is.EqualTo(InkColor.Blue), "A real first pulse spends red.");
            foreach (var enemy in original.Enemies) enemy.Health = 0;
            original.Tick(.01f); Assert.That(original.Phase, Is.EqualTo(RunPhase.Intermission));
            var restored = Restore(rules, JsonCopy(Capture(original))); var copy = restored.StructureAt(tower.Cell);
            var weapons = new WeaponSystem(rules); var events = new SimulationEventBuffer(); var shots = new List<ShotEvent>();
            var targets = new List<EnemyState> { new EnemyState { Id = 100, Position = new Point2(6.5f, 5.5f), Health = 10000, MaxHealth = 10000 } };
            weapons.Fire(new List<StructureState> { copy }, targets, shots, 1, 1, .01f, 1, new RunStatistics(), events,
                tower.NextPrismPulseAt - .01);
            Assert.That(events.Any(item => item.Kind == SimulationEventKind.PrismPulsed), Is.False);
            weapons.Fire(new List<StructureState> { copy }, targets, shots, 1, 1, .01f, 2, new RunStatistics(), events,
                tower.NextPrismPulseAt + .01);
            Assert.That(events.Count(item => item.Kind == SimulationEventKind.PrismPulsed), Is.EqualTo(1));
            Assert.That(copy.NextPrismColor, Is.EqualTo(InkColor.Green), "The resumed pulse uses blue, not a reset red pulse.");
        }

        [Test]
        public void CleanupMountsMayRetainNegativeCellTargetsAtTheEndOfARealAction()
        {
            var rules = Rules(); rules.EnableEnemyMechanics = true;
            rules.StructureDefinitions.First(value => value.Kind == StructureKind.Blotter).BuildSeconds = 0;
            var original = GameSession.CreatePrepared(rules, 989, Array.Empty<PreparedEnemy>(),
                new[] { new PreparedBlot(new Cell(7, 5), 8) });
            Assert.That(original.TryBuild(new[] { new Cell(5, 5) }, StructureKind.Blotter, MaterialTier.Pencil,
                out var reason, facingDegrees: 0), Is.True, reason);
            original.Tick(.01f); Assert.That(original.Phase, Is.EqualTo(RunPhase.Intermission));
            var tower = original.StructureAt(new Cell(5, 5));
            Assert.That(tower.PrimaryTargetId, Is.LessThan(0)); Assert.That(original.Blots, Is.Empty);
            var restored = Restore(rules, JsonCopy(Capture(original))); var copy = restored.StructureAt(tower.Cell);
            Assert.That(copy.PrimaryTargetId, Is.EqualTo(tower.PrimaryTargetId));
            original.Tick(.02f); restored.Tick(.02f); Assert.That(copy.Cooldown, Is.EqualTo(tower.Cooldown));
        }

        [TestCase("future")] [TestCase("phase")] [TestCase("wave")]
        [TestCase("nan")] [TestCase("random_limit")] [TestCase("identity")]
        [TestCase("missing_structures")] [TestCase("duplicate_structure")]
        [TestCase("mount_cycle")] [TestCase("hidden_recipe")] [TestCase("outside_cell")]
        public void CorruptOrFutureStateIsRejectedWithoutPublishingAPartialSession(string corruption)
        {
            var rules = Rules(); var original = Intermission(rules); Build(original, Tower(rules), new Cell(5, 5));
            var data = JsonCopy(Capture(original));
            switch (corruption)
            {
                case "future": Set(data, "Version", 2); break;
                case "phase": Set(data, "Phase", RunPhase.Assault); break;
                case "wave": Set(data, "WaveIndex", -1); break;
                case "nan": Set(data, "Countdown", float.NaN); break;
                case "random_limit": Set(data, "RandomDrawCount", long.MaxValue); break;
                case "identity": Set(data, "RunId", "not-a-run"); break;
                case "missing_structures": Set(data, "Structures", null); break;
                case "outside_cell": Set(Get<Array>(data, "Structures").GetValue(0), "X", rules.Width); break;
                case "hidden_recipe": Set(Get<Array>(data, "Structures").GetValue(0), "HasRecipe", false); break;
                case "mount_cycle":
                    var mounts = Get<Array>(Get<Array>(data, "Structures").GetValue(0), "Mounts");
                    Set(mounts.GetValue(0), "RemainingWork", double.PositiveInfinity); break;
                case "duplicate_structure":
                    var items = Get<Array>(data, "Structures"); var duplicates = Array.CreateInstance(items.GetType().GetElementType(), 2);
                    duplicates.SetValue(items.GetValue(0), 0); duplicates.SetValue(items.GetValue(0), 1); Set(data, "Structures", duplicates); break;
            }
            Assert.That(Restore(rules, data, out var session, out var reason), Is.False);
            Assert.That(session, Is.Null); Assert.That(reason, Is.Not.Empty);
            Assert.That(original.Structures.Count, Is.EqualTo(1));
        }
    }
}
