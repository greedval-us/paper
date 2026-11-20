#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class FirstRefitProgressTests
    {
        // Reflection keeps the initial red test compilable before the new public API exists.
        static bool Flag(RunController run, string name)
        {
            var property = typeof(RunController).GetProperty(name);
            Assert.That(property, Is.Not.Null, "Missing first-refit progress API: " + name);
            return (bool)property.GetValue(run);
        }
        static bool Offered(RunController run) => Flag(run, "CanOfferFirstRefitTutorial");
        static bool Handled(RunController run) => Flag(run, "FirstRefitTutorialHandled");
        static bool Begin(RunController run, out string reason)
        {
            var method = typeof(RunController).GetMethod("BeginFirstRefitTutorial");
            Assert.That(method, Is.Not.Null, "Missing first-refit activation command.");
            var arguments = new object[] { null };
            bool success = (bool)method.Invoke(run, arguments);
            reason = (string)arguments[0];
            return success;
        }
        static bool Dismiss(RunController run, out string reason)
        {
            var method = typeof(RunController).GetMethod("DismissFirstRefitTutorial");
            Assert.That(method, Is.Not.Null, "Missing first-refit dismissal command.");
            var arguments = new object[] { null };
            bool success = (bool)method.Invoke(run, arguments);
            reason = (string)arguments[0];
            return success;
        }
        static bool Complete(RunController run, string entryId, out string reason)
        {
            var method = typeof(RunController).GetMethod("CompleteFirstRefitTutorial");
            Assert.That(method, Is.Not.Null, "Missing first-refit completion command.");
            var arguments = new object[] { entryId, null };
            bool success = (bool)method.Invoke(run, arguments);
            reason = (string)arguments[1];
            return success;
        }
        static void EnableCampaign(AssemblyCommandFixture fixture)
        {
            fixture.BalanceAsset.campaignEnabled = true;
            fixture.Restart();
        }
        static void Finish(RunController run)
        {
            // Presentation/progression fixture; this does not claim to play through enemy combat.
            var session = (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run);
            typeof(GameSession).GetProperty("CompletedWaves").SetValue(session, run.ActiveMission.WaveCount);
            typeof(GameSession).GetProperty("Phase").SetValue(session, RunPhase.Victory);
            run.StepSimulation();
        }
        static void WinFirst(AssemblyCommandFixture fixture)
        {
            EnableCampaign(fixture);
            Assert.That(fixture.Run.StartRun(out var reason), Is.True, reason);
            Finish(fixture.Run);
            Assert.That(fixture.Run.IsCurrentMissionRewardSaved, Is.True);
        }
        static AlbumEntry LongHeadVariant(AssemblyCommandFixture fixture)
        {
            var source = fixture.Run.Workshop.LoadoutEntries.First(entry => entry?.Kind == AlbumEntryKind.Tower &&
                entry.Snapshot.Recipe.Tower.Mounts.Count == 1 && entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.gun");
            var tower = source.Snapshot.Recipe.Tower;
            var old = tower.Mounts[0].Weapon;
            var head = fixture.Catalog.Definitions.Single(part => part.Id == "head.long");
            var weapon = new WeaponRecipe(old.Mechanism, new PartRef(head.Id, head.Revision), old.Sight, old.Magazine, old.Trigger, old.Mount);
            var recipe = new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] { new WeaponMountRecipe(0, weapon) }));
            return new AlbumEntry(source.Id, "Первое переоснащение", source.Revision,
                new RecipeSnapshot(recipe, fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions));
        }
        static string SaveAssignedVariant(AssemblyCommandFixture fixture)
        {
            var saved = fixture.Commands.SaveEntryAsVariant(LongHeadVariant(fixture), Guid.NewGuid());
            Assert.That(saved.Success, Is.True, saved.Code);
            var assigned = fixture.Commands.AssignSlot(0, saved.ResultId, Guid.NewGuid());
            Assert.That(assigned.Success, Is.True, assigned.Code);
            return saved.ResultId;
        }

        [Test] public void OfferRequiresCommittedFirstVictoryAndReadingItDoesNotWriteOrPayRewards()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                EnableCampaign(fixture);
                Assert.That(Offered(fixture.Run), Is.False);
                Assert.That(Handled(fixture.Run), Is.False);
                Assert.That(fixture.Run.StartRun(out _), Is.True);
                fixture.Store.RefuseWrites = true;
                Finish(fixture.Run);
                Assert.That(fixture.Run.MissionResult.FirstClear, Is.True, "A failed reward still has a first-clear preview.");
                Assert.That(Offered(fixture.Run), Is.False);
                Assert.That(Dismiss(fixture.Run, out _), Is.False);
                fixture.Store.RefuseWrites = false;
                Assert.That(fixture.Run.RetryPendingRewards(out _), Is.True);
                Assert.That(fixture.Run.Workshop.IsUnlocked("head.long"), Is.True);
                int writes = fixture.Store.SaveCalls;
                string state = fixture.State();
                Assert.That(Offered(fixture.Run), Is.True);
                Assert.That(Offered(fixture.Run), Is.True);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
                Assert.That(fixture.State(), Is.EqualTo(state));
            }
        }

        [Test] public void ReplayLaterFirstClearAndReloadedVeteranDoNotOfferFirstRefit()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                WinFirst(fixture);
                Assert.That(Offered(fixture.Run), Is.True);
                fixture.Run.OpenWorkshop();
                Assert.That(fixture.Run.StartRun(out _), Is.True);
                Finish(fixture.Run);
                Assert.That(fixture.Run.MissionResult.FirstClear, Is.False);
                Assert.That(Offered(fixture.Run), Is.False);
                fixture.Run.OpenWorkshop();
                Assert.That(fixture.Run.SelectMission("notebook-1-page-2", out _), Is.True);
                Assert.That(fixture.Run.StartRun(out _), Is.True);
                Finish(fixture.Run);
                Assert.That(fixture.Run.MissionResult.FirstClear, Is.True);
                Assert.That(Offered(fixture.Run), Is.False);
                fixture.Restart();
                Assert.That(Offered(fixture.Run), Is.False);
            }
        }

        [Test] public void DismissalIsIdempotentSurvivesReloadAndKeepsV9RewardsAndRecipes()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                WinFirst(fixture);
                string state = fixture.State();
                var before = fixture.SavedData;
                Assert.That(Dismiss(fixture.Run, out var reason), Is.True, reason);
                Assert.That(Handled(fixture.Run), Is.True);
                Assert.That(Offered(fixture.Run), Is.False);
                int writes = fixture.Store.SaveCalls;
                Assert.That(Dismiss(fixture.Run, out reason), Is.True, reason);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
                Assert.That(fixture.State(), Is.EqualTo(state));
                Assert.That(fixture.SavedData.Version, Is.EqualTo(WorkshopProfile.CurrentVersion));
                Assert.That(JsonUtility.ToJson(fixture.SavedData.Campaign), Is.EqualTo(JsonUtility.ToJson(before.Campaign)));
                Assert.That(fixture.SavedData.CampaignRewards.Select(item => JsonUtility.ToJson(item)),
                    Is.EqualTo(before.CampaignRewards.Select(item => JsonUtility.ToJson(item))));
                fixture.Restart();
                Assert.That(Handled(fixture.Run), Is.True);
                Assert.That(Offered(fixture.Run), Is.False);
            }
        }

        [Test] public void FailedDismissalDoesNotPublishMarkerAndCanBeRetried()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                WinFirst(fixture);
                byte[] bytes = fixture.SavedBytes;
                fixture.Store.RefuseWrites = true;
                Assert.That(Dismiss(fixture.Run, out var reason), Is.False);
                Assert.That(reason, Is.Not.Empty);
                Assert.That(Handled(fixture.Run), Is.False);
                Assert.That(Offered(fixture.Run), Is.True);
                Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
                fixture.Store.RefuseWrites = false;
                Assert.That(Dismiss(fixture.Run, out reason), Is.True, reason);
                Assert.That(Handled(fixture.Run), Is.True);
            }
        }

        [Test] public void CompletionRequiresWorkshopSavedLongHeadTowerAndSuccessfulSlotAssignment()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                WinFirst(fixture);
                Assert.That(Complete(fixture.Run, "album.guard", out _), Is.False, "Result screen is not a workshop edit session.");
                fixture.Run.OpenWorkshop();
                int writes = fixture.Store.SaveCalls;
                Assert.That(Complete(fixture.Run, "missing-draft", out _), Is.False);
                Assert.That(Complete(fixture.Run, "album.guard", out _), Is.False, "An unchanged assigned starter does not complete refitting.");
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
                var save = fixture.Commands.SaveEntryAsVariant(LongHeadVariant(fixture), Guid.NewGuid());
                Assert.That(save.Success, Is.True, save.Code);
                Assert.That(Complete(fixture.Run, save.ResultId, out _), Is.False, "Saving a variant alone does not equip it.");
                fixture.Store.RefuseWrites = true;
                Assert.That(fixture.Commands.AssignSlot(0, save.ResultId, Guid.NewGuid()).Success, Is.False);
                Assert.That(Complete(fixture.Run, save.ResultId, out _), Is.False);
                Assert.That(Handled(fixture.Run), Is.False);
                fixture.Store.RefuseWrites = false;
                Assert.That(fixture.Commands.AssignSlot(0, save.ResultId, Guid.NewGuid()).Success, Is.True);
                string state = fixture.State();
                Assert.That(Complete(fixture.Run, save.ResultId, out var reason), Is.True, reason);
                Assert.That(Handled(fixture.Run), Is.True);
                Assert.That(fixture.State(), Is.EqualTo(state));
                writes = fixture.Store.SaveCalls;
                Assert.That(Complete(fixture.Run, save.ResultId, out reason), Is.True, reason);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
                fixture.Restart();
                Assert.That(Handled(fixture.Run), Is.True);
                Assert.That(fixture.Run.Workshop.LoadoutEntries[0].Id, Is.EqualTo(save.ResultId));
            }
        }

        [Test] public void FailedCompletionWriteKeepsMarkerPendingAndAssignedRecipeIntact()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                WinFirst(fixture); fixture.Run.OpenWorkshop();
                string entryId = SaveAssignedVariant(fixture);
                byte[] bytes = fixture.SavedBytes;
                fixture.Store.RefuseWrites = true;
                Assert.That(Complete(fixture.Run, entryId, out var reason), Is.False);
                Assert.That(reason, Is.Not.Empty);
                Assert.That(Handled(fixture.Run), Is.False);
                Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
                Assert.That(fixture.Run.Workshop.LoadoutEntries[0].Id, Is.EqualTo(entryId));
                fixture.Store.RefuseWrites = false;
                Assert.That(Complete(fixture.Run, entryId, out reason), Is.True, reason);
                Assert.That(Handled(fixture.Run), Is.True);
            }
        }

        [Test] public void FutureProfileCannotOfferOrWriteTutorialProgress()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                WinFirst(fixture);
                var data = fixture.SavedData; data.Version = WorkshopProfile.CurrentVersion + 1;
                File.WriteAllText(fixture.ProfilePath, JsonUtility.ToJson(data));
                fixture.Restart();
                Assert.That(fixture.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.RecoveryRequired));
                byte[] bytes = fixture.SavedBytes;
                int writes = fixture.Store.SaveCalls;
                Assert.That(Offered(fixture.Run), Is.False);
                Assert.That(Dismiss(fixture.Run, out _), Is.False);
                Assert.That(Complete(fixture.Run, "album.guard", out _), Is.False);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
                Assert.That(fixture.SavedBytes, Is.EqualTo(bytes));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void StartedGuideCanBeDismissedOrCompletedAfterAnotherAttemptReplacesTheResult(bool complete)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                WinFirst(fixture); fixture.Run.OpenWorkshop();
                int writes = fixture.Store.SaveCalls;
                Assert.That(Begin(fixture.Run, out var reason), Is.True, reason);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes), "Starting the guide must not write or change rewards.");
                Assert.That(fixture.Run.StartRun(out _), Is.True);
                Assert.That(Offered(fixture.Run), Is.False);
                Assert.That(Dismiss(fixture.Run, out _), Is.False, "A retained guide cannot change progress during a battle.");
                fixture.Run.OpenWorkshop();
                Assert.That(Offered(fixture.Run), Is.False, "A replay does not become a second first-clear invitation.");
                if (complete)
                {
                    string entryId = SaveAssignedVariant(fixture);
                    Assert.That(Complete(fixture.Run, entryId, out reason), Is.True, reason);
                }
                else Assert.That(Dismiss(fixture.Run, out reason), Is.True, reason);
                Assert.That(Handled(fixture.Run), Is.True);
                fixture.Restart();
                Assert.That(Handled(fixture.Run), Is.True);
            }
        }

        [Test] public void ActivationCannotBeStartedBeforeVictoryOrTransferredToAnotherProfile()
        {
            using (var fixture = new AssemblyCommandFixture())
            using (var other = new AssemblyCommandFixture())
            {
                EnableCampaign(fixture);
                Assert.That(Begin(fixture.Run, out _), Is.False);
                WinFirst(fixture); fixture.Run.OpenWorkshop();
                Assert.That(Begin(fixture.Run, out var reason), Is.True, reason);
                WinFirst(other); other.Run.OpenWorkshop();
                fixture.Run.InitializeWorkshop(other.Store);
                Assert.That(Offered(fixture.Run), Is.False);
                Assert.That(Dismiss(fixture.Run, out _), Is.False,
                    "Another profile has a different first-clear receipt, even if it owns the same reward part.");
                Assert.That(Handled(fixture.Run), Is.False);
            }
        }
    }
}
#endif
