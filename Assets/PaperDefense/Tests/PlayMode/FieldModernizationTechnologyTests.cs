#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class FieldModernizationTechnologyTests
    {
        static GameSession Session(RunController run) => (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run);
        static int Rank(RunController run) => (int)run.Session.Balance.Mechanics.GetType().GetProperty("MaximumModernizationRank").GetValue(run.Session.Balance.Mechanics);
        static void Finish(RunController run)
        {
            var session = Session(run);
            typeof(GameSession).GetProperty("CompletedWaves").SetValue(session, run.ActiveMission.WaveCount);
            typeof(GameSession).GetProperty("Phase").SetValue(session, RunPhase.Victory);
            run.StepSimulation();
        }

        [Test] public void OneEarnedFlaskCanInstallTheFirstColorOnAnyStartingWeapon()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var rules = MissionBalanceFactory.Create(fixture.Balance, CampaignCatalog.Default.Missions[0]);
                var weapons = fixture.Catalog.Starters.Where(entry => entry.Kind == AlbumEntryKind.Tower).ToArray();
                Assert.That(weapons, Is.Not.Empty);
                foreach (var weapon in weapons)
                {
                    var entry = TowerCatalog.ResolveEntry(weapon, rules).Value;
                    int firstColor = ModernizationRules.ColorCost(entry.Cost, 1);
                    Assert.That(firstColor, Is.InRange(1, rules.Mechanics.InkPerFlask), weapon.Id);
                    Assert.That(ModernizationRules.ColorCost(entry.Cost, 2), Is.EqualTo(2 * firstColor), weapon.Id);
                }
            }
        }

        [Test] public void UpgradingOnePiercerHelpsAgainstACrowdWhileTwoPositionsInterceptMoreRunners()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var entry = fixture.Catalog.Starters.Single(e => e.Id == "album.piercer");
                var rules = MissionBalanceFactory.Create(fixture.Balance, CampaignCatalog.Default.Missions[1]);
                var specialized = new TrialModernizationConfiguration(new ModernizationRanks(power: 2, mechanics: 2), 2);
                int budget = 2 * TowerCatalog.ResolveEntry(entry, rules).Value.Cost;
                AssemblyTrialReport TryLane(AssemblyTrialScenario lane, TrialModernizationConfiguration ranks) =>
                    AssemblyTrialRunner.Run(entry, rules, lane, ranks, budget, seed: 1337);

                var crowdPair = TryLane(AssemblyTrialScenario.Crowd, TrialModernizationConfiguration.Baseline);
                var crowdSingle = TryLane(AssemblyTrialScenario.Crowd, specialized);
                Assert.That(crowdPair.Copies, Is.EqualTo(2));
                Assert.That(crowdSingle.Copies, Is.EqualTo(1));
                Assert.That(crowdSingle.Cost, Is.LessThan(crowdPair.Cost));
                Assert.That(crowdSingle.Leaks.Value, Is.LessThan(crowdPair.Leaks.Value));
                var runnerPair = TryLane(AssemblyTrialScenario.Fast, TrialModernizationConfiguration.Baseline);
                var runnerSingle = TryLane(AssemblyTrialScenario.Fast, specialized);
                Assert.That(runnerPair.Leaks.Value, Is.LessThan(runnerSingle.Leaks.Value));
            }
        }

        [Test] public void SavedVictoriesUnlockTwoThenThreeOnlyForTheNextAttemptAndKeepV9()
        {
            using (var f = new AssemblyCommandFixture())
            {
                f.BalanceAsset.campaignEnabled = true; f.Restart();
                Assert.That(f.Run.StartRun(out _), Is.True); Assert.That(Rank(f.Run), Is.EqualTo(1));
                Finish(f.Run); Assert.That(Rank(f.Run), Is.EqualTo(1), "Current attempt technology is frozen.");
                f.Run.OpenWorkshop(); Assert.That(f.Run.SelectMission("notebook-1-page-2", out _), Is.True);
                Assert.That(f.Run.StartRun(out _), Is.True); Assert.That(Rank(f.Run), Is.EqualTo(2)); Finish(f.Run);
                f.Run.OpenWorkshop(); Assert.That(f.Run.SelectMission("notebook-1-page-3", out _), Is.True);
                Assert.That(f.Run.StartRun(out _), Is.True); Assert.That(Rank(f.Run), Is.EqualTo(2)); Finish(f.Run);
                f.Restart(); Assert.That(f.SavedData.Version, Is.EqualTo(WorkshopProfile.CurrentVersion));
                Assert.That(f.Run.StartRun(out _), Is.True); Assert.That(Rank(f.Run), Is.EqualTo(3));
                Assert.That(f.Run.Session.Structures.Count, Is.Zero, "Every attempt begins without carried field ranks.");
            }
        }

        [Test] public void FailedVictorySaveAndLockedPreviewDoNotGrantTechnologyRetryPublishesForNextRun()
        {
            using (var f = new AssemblyCommandFixture())
            {
                f.BalanceAsset.campaignEnabled = true; f.Restart();
                string before = f.State(); int saves = f.Store.SaveCalls;
                f.Run.CreateMissionPreview("notebook-1-page-3");
                Assert.That(f.Run.SelectMission("notebook-1-page-3", out _), Is.False);
                Assert.That(f.State(), Is.EqualTo(before)); Assert.That(f.Store.SaveCalls, Is.EqualTo(saves));
                Assert.That(f.Run.StartRun(out _), Is.True); f.Store.RefuseWrites = true; Finish(f.Run);
                Assert.That(f.Run.HasPendingRunRewards, Is.True); Assert.That(Rank(f.Run), Is.EqualTo(1));
                f.Run.OpenWorkshop(); Assert.That(f.Run.StartRun(out _), Is.True); Assert.That(Rank(f.Run), Is.EqualTo(1));
                f.Store.RefuseWrites = false; Assert.That(f.Run.RetryPendingRewards(out _), Is.True);
                Assert.That(Rank(f.Run), Is.EqualTo(1));
                f.Run.OpenWorkshop(); Assert.That(f.Run.StartRun(out _), Is.True); Assert.That(Rank(f.Run), Is.EqualTo(2));
            }
        }

        [Test] public void TrialAndBlueprintPreviewCannotPublishTechnologyOrProfileWrites()
        {
            using (var f = new AssemblyCommandFixture())
            {
                var entry = f.Gun(); string before = f.State(); int saves = f.Store.SaveCalls;
                f.Run.PreviewAlbumEntry(entry); f.Run.RunAssemblyTrial(entry, AssemblyTrialScenario.Single);
                Assert.That(f.State(), Is.EqualTo(before)); Assert.That(f.Store.SaveCalls, Is.EqualTo(saves));
                Assert.That(f.Run.StartRun(out _), Is.True); Assert.That(Rank(f.Run), Is.EqualTo(1));
            }
        }
    }
}
#endif

