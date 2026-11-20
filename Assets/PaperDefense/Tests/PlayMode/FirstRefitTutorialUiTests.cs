#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class FirstRefitTutorialUiTests : BattleHudThemeFixture
    {
        Button Action(string name) => root.GetComponentsInChildren<Button>().SingleOrDefault(button => button.name == name);
        CompositeWorkshopPanel Panel => root.GetComponentInChildren<WorkshopHud>(true).Composite;
        void Press(string name)
        {
            var button = Action(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.IsInteractable(), Is.True, name);
            button.onClick.Invoke(); Canvas.ForceUpdateCanvases();
        }
        void BeginGuide(int width = 1600, int height = 900)
        {
            Size(new Vector2Int(width, height));
            Assert.That(game.StartBattle(), Is.True);
            FinishVictory(); Press("Result first refit");
            Panel.Reflow(width, height); Canvas.ForceUpdateCanvases();
        }
        void InstallAndReview()
        {
            Press("Sequence install head.long");
            Press("Sequence next"); Press("Sequence next");
        }
        void RunComparison()
        {
            Press("Composite trial");
            Assert.That(Panel.LastAssemblyTrial, Is.Null, "Opening the range is not a completed trial.");
            Press("Composite trial run");
            Assert.That(Panel.LastAssemblyTrial, Is.Not.Null);
            Assert.That(Panel.LastAssemblyTrialComparison, Is.Not.Null);
            Press("Composite trial header return");
        }
        void FinishVictory()
        {
            var session = (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Run);
            typeof(GameSession).GetProperty("CompletedWaves").SetValue(session, game.Run.ActiveMission.WaveCount);
            typeof(GameSession).GetProperty("WaveIndex").SetValue(session, game.Run.ActiveMission.WaveCount);
            typeof(GameSession).GetProperty("Phase").SetValue(session, RunPhase.Victory);
            game.Run.StepSimulation(); game.Navigate(PlayerPage.Result); Canvas.ForceUpdateCanvases();
        }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void SavedFirstVictoryOffersTheUnlockedLongBarrelTutorial(int width, int height)
        {
            Size(new Vector2Int(width, height));
            Assert.That(game.StartBattle(), Is.True);
            FinishVictory();
            Assert.That(game.Run.IsCurrentMissionRewardSaved, Is.True);
            Assert.That(game.Run.Workshop.IsUnlocked("head.long"), Is.True);
            var invitation = Action("Result first refit");
            Assert.That(invitation, Is.Not.Null, "The first saved victory must explain how to use the unlocked barrel.");
            Assert.That(invitation.IsInteractable(), Is.True);
            Assert.That(invitation.GetComponentInChildren<Text>().text, Does.Contain("переделк"));
            Canvas.ForceUpdateCanvases();
            var copy = root.GetComponentsInChildren<Text>().Single(text => text.name == "First refit invitation");
            Assert.That(copy.preferredHeight, Is.LessThanOrEqualTo(copy.rectTransform.rect.height + .5f),
                "Both invitation lines must remain visible at compact resolutions.");
        }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void GuidedRefitUsesRealCommandsAndPreservesOriginalTowerAndGold(int width, int height)
        {
            BeginGuide(width, height);
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Workshop));
            Assert.That(Panel.FirstRefitActive, Is.True);
            var original = game.Run.Workshop.LoadoutEntries[0];
            string originalHead = original.Snapshot.Recipe.Tower.Mounts[0].Weapon.Head.Id;
            string draftId = Panel.DraftSession.Draft.Id;
            int gold = game.Run.Workshop.Gold, count = game.Run.Workshop.Entries.Count;
            Assert.That(draftId, Is.Not.EqualTo(original.Id));
            Assert.That(Panel.DraftSession.ActiveCategory, Is.EqualTo(ComponentCategory.Head));
            var guide = Panel.Root.Find("First refit guide") as RectTransform;
            Assert.That(guide, Is.Not.Null);
            var bounds = BattleHudThemeEvidence.Bounds(Panel.Root, guide);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(-.5f));
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(width + .5f));
            var body = Panel.Root.Find("Sequence content") as RectTransform;
            Assert.That(bounds.Overlaps(BattleHudThemeEvidence.Bounds(Panel.Root, body)), Is.False,
                "The guide must reserve space rather than cover the editor.");
            InstallAndReview();
            RunComparison();
            Press("Sequence save");
            Assert.That(game.Run.Workshop.Entries.Count, Is.EqualTo(count + 1));
            Assert.That(game.Run.Workshop.LoadoutEntries[0].Id, Is.EqualTo(original.Id));
            Press("First refit assign");
            Assert.That(game.Run.Workshop.LoadoutEntries[0].Id, Is.EqualTo(draftId));
            Assert.That(game.Run.Workshop.LoadoutEntries[0].Snapshot.Recipe.Tower.Mounts[0].Weapon.Head.Id, Is.EqualTo("head.long"));
            Assert.That(game.Run.Workshop.Entries.Single(entry => entry.Id == original.Id).Snapshot.Recipe.Tower.Mounts[0].Weapon.Head.Id,
                Is.EqualTo(originalHead));
            Assert.That(game.Run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(game.Run.FirstRefitTutorialHandled, Is.True);
        }

        [Test]
        public void SavingWithoutRunningTheTrialDoesNotCompleteTheGuide()
        {
            BeginGuide(); InstallAndReview();
            Press("Composite trial"); Press("Composite trial header return");
            Press("Sequence save");
            Assert.That(game.Run.FirstRefitTutorialHandled, Is.False);
            Assert.That(Action("First refit assign") == null || !Action("First refit assign").IsInteractable(), Is.True);
            RunComparison();
            // A previously saved recipe needs no duplicate variant after its trial.
            int count = game.Run.Workshop.Entries.Count;
            Press("Sequence save"); Press("First refit assign");
            Assert.That(game.Run.Workshop.Entries.Count, Is.EqualTo(count));
            Assert.That(game.Run.FirstRefitTutorialHandled, Is.True);
        }

        [Test]
        public void EditingAfterTheTrialRequiresTestingTheChangedRecipe()
        {
            BeginGuide(); InstallAndReview(); RunComparison(); Press("Sequence save");
            Press("Sequence back"); Press("Sequence back");
            Assert.That(Panel.DraftSession.SelectPart("sight.snap"), Is.True);
            Panel.Refresh(); Press("Sequence next"); Press("Sequence next"); Press("Sequence save");
            Assert.That(Action("First refit assign") == null || !Action("First refit assign").IsInteractable(), Is.True,
                "An old report must not approve a different recipe.");
            Assert.That(game.Run.FirstRefitTutorialHandled, Is.False);
            RunComparison(); Press("Sequence save"); Press("First refit assign");
            Assert.That(game.Run.FirstRefitTutorialHandled, Is.True);
        }

        [Test]
        public void SkippingIsOptionalAndDoesNotSaveOrAssignTheDraft()
        {
            BeginGuide();
            int count = game.Run.Workshop.Entries.Count;
            string original = game.Run.Workshop.LoadoutEntries[0].Id;
            Press("First refit skip");
            Assert.That(Panel.FirstRefitActive, Is.False);
            Assert.That(game.Run.FirstRefitTutorialHandled, Is.True);
            Assert.That(game.Run.Workshop.Entries.Count, Is.EqualTo(count));
            Assert.That(game.Run.Workshop.LoadoutEntries[0].Id, Is.EqualTo(original));
        }

        [Test]
        public void FailedRewardWriteHidesInvitationUntilSuccessfulRetry()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                game.Run.InitializeWorkshop(fixture.Store); game.Navigate(PlayerPage.Home);
                Assert.That(game.StartBattle(), Is.True);
                fixture.Store.RefuseWrites = true; FinishVictory();
                Assert.That(Action("Result first refit"), Is.Null);
                fixture.Store.RefuseWrites = false; Press("Result retry save");
                Assert.That(Action("Result first refit"), Is.Not.Null);
                Press("Result first refit");
                Assert.That(Panel.FirstRefitActive, Is.True);
            }
        }

        [Test]
        public void FailedComponentTowerAndSlotWritesKeepTheGuideRetryable()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                game.Run.InitializeWorkshop(fixture.Store); game.Navigate(PlayerPage.Home);
                BeginGuide(); Press("Sequence install head.long");
                fixture.Store.RefuseWrites = true; Press("Sequence next");
                Assert.That(Panel.Step, Is.EqualTo(1));
                fixture.Store.RefuseWrites = false; Press("Sequence next"); Press("Sequence next"); RunComparison();
                int count = game.Run.Workshop.Entries.Count;
                fixture.Store.RefuseWrites = true; Press("Sequence save");
                Assert.That(game.Run.Workshop.Entries.Count, Is.EqualTo(count));
                Assert.That(game.Run.FirstRefitTutorialHandled, Is.False);
                fixture.Store.RefuseWrites = false; Press("Sequence save");
                string original = game.Run.Workshop.LoadoutEntries[0].Id;
                fixture.Store.RefuseWrites = true; Press("First refit assign");
                Assert.That(game.Run.Workshop.LoadoutEntries[0].Id, Is.EqualTo(original));
                Assert.That(game.Run.FirstRefitTutorialHandled, Is.False);
                fixture.Store.RefuseWrites = false; Press("First refit assign");
                Assert.That(game.Run.FirstRefitTutorialHandled, Is.True);
                Assert.That(game.Run.Workshop.Entries.Count, Is.EqualTo(count + 1));
            }
        }

        [Test]
        public void GuideAndTrialSurviveRouteChangesResizeAndLeavingTheWorkshop()
        {
            BeginGuide(); InstallAndReview(); RunComparison(); Press("Sequence save");
            string draft = Panel.DraftSession.Draft.Id;
            Press("Composite route structure");
            Assert.That(Panel.FirstRefitActive, Is.False);
            Press("Composite route tower");
            Assert.That(Panel.FirstRefitActive, Is.True);
            Assert.That(Panel.DraftSession.Draft.Id, Is.EqualTo(draft));
            Panel.Reflow(800, 600); Canvas.ForceUpdateCanvases();
            Assert.That(Action("First refit assign"), Is.Not.Null);
            game.ReturnFromWorkshop(); game.Navigate(PlayerPage.Workshop);
            Assert.That(Panel.DraftSession.Draft.Id, Is.EqualTo(draft));
            Press("First refit assign");
            Assert.That(game.Run.FirstRefitTutorialHandled, Is.True);
        }

        [Test]
        public void StartedUiGuideCanFinishAfterReturningFromAnotherBattle()
        {
            BeginGuide(); InstallAndReview(); RunComparison(); Press("Sequence save");
            string draft = Panel.DraftSession.Draft.Id;
            game.ReturnFromWorkshop();
            Assert.That(game.StartBattle(), Is.True);
            game.ReturnToMap(); game.Navigate(PlayerPage.Workshop);
            Assert.That(Panel.FirstRefitActive, Is.True);
            Assert.That(Panel.DraftSession.Draft.Id, Is.EqualTo(draft));
            Press("First refit assign");
            Assert.That(game.Run.FirstRefitTutorialHandled, Is.True);
        }

        [Test]
        public void WrongComparisonDoesNotApproveTheRecipeAndExplainsHowToContinue()
        {
            BeginGuide(); InstallAndReview();
            Press("Composite trial"); Press("Composite trial compare"); Press("Composite trial run");
            Press("Composite trial header return"); Press("Sequence save");
            Assert.That(Action("First refit assign") == null || !Action("First refit assign").IsInteractable(), Is.True);
            var instruction = root.GetComponentsInChildren<Text>().Single(label => label.name == "First refit instruction");
            Assert.That(instruction.text, Does.Contain("B"));
            RunComparison(); Press("First refit assign");
            Assert.That(game.Run.FirstRefitTutorialHandled, Is.True);
        }

        [Test]
        public void MarkerFailureAfterSuccessfulAssignmentCanBeRetriedWithoutAnotherVariant()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                game.Run.InitializeWorkshop(fixture.Store); game.Navigate(PlayerPage.Home);
                BeginGuide(); InstallAndReview(); RunComparison(); Press("Sequence save");
                string id = Panel.DraftSession.Draft.Id;
                int count = game.Run.Workshop.Entries.Count;
                void RefuseAfterAssignment() => fixture.Store.RefuseWrites = true;
                game.Run.WorkshopChanged += RefuseAfterAssignment;
                try { Press("First refit assign"); }
                finally { game.Run.WorkshopChanged -= RefuseAfterAssignment; }
                Assert.That(game.Run.Workshop.LoadoutEntries[0].Id, Is.EqualTo(id));
                Assert.That(game.Run.FirstRefitTutorialHandled, Is.False);
                Assert.That(Panel.FirstRefitActive, Is.True);
                fixture.Store.RefuseWrites = false; Press("First refit assign");
                Assert.That(game.Run.FirstRefitTutorialHandled, Is.True);
                Assert.That(game.Run.Workshop.Entries.Count, Is.EqualTo(count));
            }
        }

        [Test]
        public void ReplacingAnUnrelatedSlotNeedsAnExplicitConfirmation()
        {
            BeginGuide(); InstallAndReview(); RunComparison(); Press("Sequence save");
            var source = game.Run.Workshop.LoadoutEntries[0];
            var another = game.Run.AssemblyCommands.SaveEntryAsVariant(source, System.Guid.NewGuid());
            Assert.That(another.Success, Is.True, another.Code);
            Assert.That(game.Run.AssemblyCommands.AssignSlot(0, another.ResultId, System.Guid.NewGuid()).Success, Is.True);
            Press("First refit assign");
            Assert.That(game.Run.Workshop.LoadoutEntries[0].Id, Is.EqualTo(another.ResultId),
                "A slot changed outside the guide must not be replaced without explaining the target.");
            Assert.That(game.Run.FirstRefitTutorialHandled, Is.False);
            Press("Workshop cancel confirmation");
            Assert.That(game.Run.Workshop.LoadoutEntries[0].Id, Is.EqualTo(another.ResultId));
            Press("First refit assign"); Press("Workshop confirm action");
            Assert.That(game.Run.Workshop.LoadoutEntries[0].Id, Is.EqualTo(Panel.DraftSession.Draft.Id));
            Assert.That(game.Run.FirstRefitTutorialHandled, Is.True);
        }
    }
}
#endif
