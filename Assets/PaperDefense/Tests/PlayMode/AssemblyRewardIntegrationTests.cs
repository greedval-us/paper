#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class AssemblyRewardIntegrationTests
    {
        static void PrepareCampaign(AssemblyCommandFixture fixture, bool liveSixth = false)
        {
            var rules = fixture.Preset.rules;
            rules.PlanningSeconds = rules.IntermissionSeconds = 10000;
            rules.PassiveInkPerSecond = rules.EarlyLaunchInkPerSecond = 0;
            rules.Progression.UpgradeEveryWaves = 0;
            rules.Waves = Enumerable.Range(0, 8).Select(i => new WaveDefinition
                { Count = liveSixth && i == 5 ? 1 : 0, InkGrant = 0 }).ToList();
        }

        static void Start(AssemblyCommandFixture fixture)
            => Assert.That(fixture.Run.StartRun(out var reason), Is.True, reason);

        static void FinishWave(RunController run)
        {
            run.NextWave(); run.AdvanceFrame(1f / 30, false, 1);
        }

        static void AssertReward(AssemblyCommandFixture fixture, int gold, int xp, Guid runId, int waves, bool victory)
        {
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(xp));
            var data = fixture.SavedData;
            Assert.That(data.Gold, Is.EqualTo(gold)); Assert.That(data.TotalXp, Is.EqualTo(xp));
            var receipt = data.Rewards.Single(item => item.RunId == runId.ToString("N"));
            Assert.That(receipt.Waves, Is.EqualTo(waves)); Assert.That(receipt.VictoryBonus, Is.EqualTo(victory));
        }

        [Test] public void EightCompletedWavesPublish94Gold120XpAndOneDurableReceipt()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                PrepareCampaign(fixture); Start(fixture); Guid runId = fixture.Run.Session.RunId;
                for (int i = 0; i < 8; i++) FinishWave(fixture.Run);
                Assert.That(fixture.Run.Session.Phase, Is.EqualTo(RunPhase.Victory));
                Assert.That(fixture.Run.Session.CompletedWaves, Is.EqualTo(8));
                AssertReward(fixture, 94, 120, runId, 8, true);
                int writes = fixture.Store.SaveCalls;
                for (int i = 0; i < 30; i++) fixture.Run.AdvanceFrame(.1f, false, 1);
                fixture.Run.OpenWorkshop();
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
                Assert.That(fixture.Run.RunRewards.Apply(runId, 8, true).Success, Is.True);
                fixture.Restart();
                Assert.That(fixture.Run.RunRewards.Apply(runId, 8, true).Success, Is.True);
                AssertReward(fixture, 94, 120, runId, 8, true);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void ExitAfterFiveOrDuringSixthPaysOnlyFiveCompletedWaves(bool startSixth)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                PrepareCampaign(fixture, liveSixth: true); Start(fixture); var runId = fixture.Run.Session.RunId;
                for (int i = 0; i < 5; i++) FinishWave(fixture.Run);
                if (startSixth) fixture.Run.NextWave();
                Assert.That(fixture.Run.Session.CompletedWaves, Is.EqualTo(5));
                fixture.Run.OpenWorkshop(); AssertReward(fixture, 40, 50, runId, 5, false);
                Assert.That(fixture.Run.StartRun(out _), Is.True);
                Assert.That(fixture.Run.Session.RunId, Is.Not.EqualTo(runId));
                Assert.That(fixture.Run.Session.CompletedWaves, Is.Zero);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(40));
            }
        }

        [Test] public void DefeatInSixthPreservesFiveWaveRewards()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                PrepareCampaign(fixture, liveSixth: true); Start(fixture); var runId = fixture.Run.Session.RunId;
                for (int i = 0; i < 5; i++) FinishWave(fixture.Run);
                fixture.Run.NextWave();
                var session = (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fixture.Run);
                typeof(GameSession).GetMethod("DamageObjective", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(session, new object[] { -1, 100000f });
                fixture.Run.AdvanceFrame(1f / 30, false, 1);
                Assert.That(fixture.Run.Session.Phase, Is.EqualTo(RunPhase.Defeat));
                AssertReward(fixture, 40, 50, runId, 5, false);
            }
        }

        [Test] public void PausingAndAbandoningTheFirstAssaultDoesNotCreateAReceipt()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                PrepareCampaign(fixture); Start(fixture); fixture.Run.NextWave();
                fixture.Run.AdvanceFrame(1, true, 4);
                Assert.That(fixture.Run.Session.CompletedWaves, Is.Zero);
                fixture.Run.OpenWorkshop();
                Assert.That(fixture.Run.Workshop.Gold, Is.Zero); Assert.That(fixture.Run.Workshop.TotalXp, Is.Zero);
                Assert.That(fixture.SavedData.Rewards, Is.Empty); Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void FailedAutomaticAwardIsCapturedOnceAndCanBeRetriedAfterMenuAndPurchase()
        {
            using (var fixture = new AssemblyCommandFixture(gold: 94, xp: 390))
            {
                PrepareCampaign(fixture, liveSixth: true); Start(fixture); var runId = fixture.Run.Session.RunId;
                fixture.Store.RefuseWrites = true; FinishWave(fixture.Run);
                Assert.That(fixture.Run.HasPendingRunRewards, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(94)); Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(390));
                int writes = fixture.Store.SaveCalls;
                for (int i = 0; i < 20; i++) fixture.Run.AdvanceFrame(.1f, false, 1);
                fixture.Run.OpenWorkshop();
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
                fixture.Store.RefuseWrites = false;
                Assert.That(fixture.Commands.PurchasePart("head.long", Guid.NewGuid()).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(14));
                Assert.That(fixture.Run.Workshop.HasUnsavedChanges, Is.True);
                Assert.That(fixture.Run.RetrySave(out var reason), Is.True, reason);
                AssertReward(fixture, 22, 400, runId, 1, false);
                Assert.That(fixture.Run.Workshop.IsUnlocked("head.long"), Is.True);
                Assert.That(fixture.Run.HasPendingRunRewards, Is.False);
                Assert.That(fixture.Run.Workshop.HasUnsavedChanges, Is.False);
            }
        }

        [Test] public void TrialDoesNotPublishProfileRewardsOrChangeExistingReceipts()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var runId = Guid.NewGuid(); Assert.That(fixture.Run.RunRewards.Apply(runId, 5, false).Success, Is.True);
                byte[] before = fixture.SavedBytes; string state = fixture.State(); int writes = fixture.Store.SaveCalls;
                var result = TrialRunner.Run(fixture.Gun(), fixture.Balance, TrialScenario.Crowd, budget: 400);
                Assert.That(result, Is.Not.Null);
                Assert.That(fixture.State(), Is.EqualTo(state)); Assert.That(fixture.SavedBytes, Is.EqualTo(before));
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
            }
        }
    }
}
#endif
