#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class CompositeWorkshopUiTests
    {
        AssemblyCommandFixture fixture;
        GameObject canvas, events;
        CompositeWorkshopPanel panel;
        int preparations;

        [SetUp] public void SetUp()
        {
            fixture = new AssemblyCommandFixture(gold: 500, xp: 390);
            canvas = new GameObject("Isolated composite workshop canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(1600, 1000);
            events = new GameObject("Isolated composite workshop events", typeof(EventSystem));
            panel = new CompositeWorkshopPanel(canvas.transform, fixture.Run, null, () => preparations++);
            Canvas.ForceUpdateCanvases();
        }

        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(canvas); Object.DestroyImmediate(events); fixture.Dispose();
        }

        Button Button(string name) => panel.Root.GetComponentsInChildren<Button>(true).Single(button => button.name == name);
        void Click(string name)
        {
            var button = Button(name);
            Assert.That(button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True, name);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Canvas.ForceUpdateCanvases();
        }
        void ReviewTower() { Click("Composite next"); Click("Composite next"); }

        [Test] public void TowerButtonsReachReviewWithoutProfileWrites()
        {
            int writes = fixture.Store.SaveCalls;
            Assert.That(panel.DraftSession.Draft.Kind, Is.EqualTo(AlbumEntryKind.Tower));
            ReviewTower();
            Assert.That(panel.Step, Is.EqualTo(2));
            Assert.That(Button("Composite save").IsInteractable(), Is.True, panel.Status);
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
        }

        [Test] public void StructureRouteReachesReviewAndShowsAuthoritativeFitDiagnostic()
        {
            Click("Composite route structure");
            Assert.That(panel.DraftSession.Draft.Kind, Is.EqualTo(AlbumEntryKind.Structure));
            Click("Composite next"); Click("Composite next");
            Assert.That(panel.Step, Is.EqualTo(2));
            Assert.That(panel.DraftSession.PreviewEntry.Kind, Is.EqualTo(AlbumEntryKind.Structure));
            Assert.That(Button("Composite trial").IsInteractable(), Is.EqualTo(panel.DraftSession.CanTrial));
            if (!panel.DraftSession.CanSave)
            {
                Assert.That(panel.Status, Is.Not.Empty);
                Assert.That(Button("Composite save").IsInteractable(), Is.False);
            }
        }

        [Test] public void FoundationCompositionOpensFiveCategoriesAndCancelKeepsParent()
        {
            string before = JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.Draft));
            Click("Composite edit foundation");
            Assert.That(panel.DraftSession.IsComponentOpen, Is.True);
            Assert.That(panel.DraftSession.Categories.Count, Is.EqualTo(5));
            Click("Composite cancel component");
            Assert.That(panel.DraftSession.IsComponentOpen, Is.False);
            Assert.That(JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.Draft)), Is.EqualTo(before));
        }

        [Test] public void ReviewNameAndSaveButtonPersistTheCompositeEntry()
        {
            ReviewTower();
            var field = panel.Root.GetComponentsInChildren<InputField>().Single();
            field.text = "Северный дозор"; field.onEndEdit.Invoke(field.text);
            Click("Composite save");
            Assert.That(fixture.Run.Workshop.Entries.Single(entry => entry.Id == panel.DraftSession.Draft.Id).Name,
                Is.EqualTo("Северный дозор"));
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(500));
            Assert.That(panel.Step, Is.EqualTo(2));
        }

        [Test] public void SaveVariantButtonCreatesIndependentPage()
        {
            ReviewTower(); Click("Composite save"); string original = panel.DraftSession.Draft.Id;
            int count = fixture.Run.Workshop.Entries.Count;
            Click("Composite variant");
            Assert.That(fixture.Run.Workshop.Entries.Count, Is.EqualTo(count + 1));
            Assert.That(panel.DraftSession.Draft.Id, Is.Not.EqualTo(original));
            Assert.That(fixture.Run.Workshop.Entries.Any(entry => entry.Id == original), Is.True);
        }

        [Test] public void RefusedSaveLeavesReviewAndDraftAvailableForRetry()
        {
            ReviewTower(); string before = JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.Draft));
            fixture.Store.RefuseWrites = true; int count = fixture.Run.Workshop.Entries.Count;
            Click("Composite save");
            Assert.That(panel.Root.gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.Step, Is.EqualTo(2));
            Assert.That(panel.Status, Is.Not.Empty);
            Assert.That(JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.Draft)), Is.EqualTo(before));
            Assert.That(fixture.Run.Workshop.Entries.Count, Is.EqualTo(count));
            fixture.Store.RefuseWrites = false; Click("Composite save failure retry");
            Assert.That(fixture.Run.Workshop.Entries.Any(entry => entry.Id == panel.DraftSession.Draft.Id), Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void DismissingWriteFailureSurvivesReflowAndRetryPreservesTheNamedDraft(bool variant)
        {
            ReviewTower();
            var name = panel.Root.GetComponentsInChildren<InputField>().Single();
            name.text = "Северный дозор после ошибки"; name.onEndEdit.Invoke(name.text);
            string originalId = panel.DraftSession.Draft.Id;
            string recipe = JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.PreviewEntry));
            int entries = fixture.Run.Workshop.Entries.Count, gold = fixture.Run.Workshop.Gold;
            fixture.Store.RefuseWrites = true;
            Click(variant ? "Composite variant" : "Composite save");
            var failure = panel.DraftSession.LastResult;
            Assert.That(failure.Code, Is.EqualTo("profile.write_failed"));
            Assert.That(panel.Root.Find("Composite save failure overlay"), Is.Not.Null,
                "A refused write needs a dismissible error sheet over the unchanged recipe review.");
            Assert.That(panel.Root.GetComponentsInChildren<Text>().Any(label => label.name == "Composite compatibility check"), Is.True);
            int attempts = fixture.Store.SaveCalls;
            Click("Composite save failure continue");
            Assert.That(panel.DraftSession.LastResult, Is.SameAs(failure), "Continue dismisses presentation only.");
            Assert.That(panel.Root.Find("Composite save failure overlay"), Is.Null);
            panel.Reflow(1024, 768); panel.Refresh(); Canvas.ForceUpdateCanvases();
            Assert.That(panel.Root.Find("Composite save failure overlay"), Is.Null, "Reflow must not reopen a dismissed failure.");
            Assert.That(panel.DraftSession.LastResult, Is.SameAs(failure));
            Assert.That(panel.DraftSession.Name, Is.EqualTo("Северный дозор после ошибки"));
            Assert.That(JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.PreviewEntry)), Is.EqualTo(recipe));
            Assert.That(panel.DraftSession.Draft.Id, Is.EqualTo(originalId));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(attempts), "Dismiss and reflow cannot retry or write.");
            Assert.That(fixture.Run.Workshop.Entries.Count, Is.EqualTo(entries));
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(gold));
            Click(variant ? "Composite variant" : "Composite save");
            Assert.That(panel.Root.Find("Composite save failure overlay"), Is.Not.Null, "A new refused attempt can show its error.");
            fixture.Store.RefuseWrites = false;
            Click("Composite save failure retry");
            Assert.That(panel.DraftSession.LastResult.Success, Is.True);
            Assert.That(panel.Root.Find("Composite save failure overlay"), Is.Null);
            Assert.That(fixture.Run.Workshop.Entries.Count, Is.EqualTo(entries + 1));
            Assert.That(panel.DraftSession.Name, Is.EqualTo("Северный дозор после ошибки"));
            Assert.That(panel.DraftSession.Draft.Id == originalId, Is.EqualTo(!variant), "Retry retains the failed save versus variant command.");
            Assert.That(panel.Step, Is.EqualTo(2));
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(gold));
        }

        [Test] public void FailedSavesRetainTheirOwnRetryIntentAndDismissalWhenSwitchingRoutes()
        {
            ReviewTower();
            panel.DraftSession.Rename("Вариант северной башни"); panel.Refresh();
            var towerSession = panel.DraftSession;
            string towerId = towerSession.Draft.Id;
            int entries = fixture.Run.Workshop.Entries.Count, gold = fixture.Run.Workshop.Gold;
            fixture.Store.RefuseWrites = true;
            Click("Composite variant"); var towerFailure = towerSession.LastResult;
            Assert.That(towerFailure.Code, Is.EqualTo("profile.write_failed"));
            Click("Composite save failure continue");

            Click("Composite route structure"); ReviewTower();
            panel.DraftSession.Rename("Сохранение южного поста"); panel.Refresh();
            var structureSession = panel.DraftSession;
            string structureId = structureSession.Draft.Id;
            Assert.That(structureSession.CanSave, Is.True, panel.Status);
            Click("Composite save"); var structureFailure = structureSession.LastResult;
            Assert.That(structureFailure.Code, Is.EqualTo("profile.write_failed"));
            Click("Composite save failure continue");

            Click("Composite route tower"); panel.Reflow(1024, 768);
            Assert.That(panel.DraftSession, Is.SameAs(towerSession));
            Assert.That(panel.DraftSession.LastResult, Is.SameAs(towerFailure));
            Assert.That(panel.Root.Find("Composite save failure overlay"), Is.Null, "Tower Continue remains dismissed after another route fails.");
            fixture.Store.RefuseWrites = false;
            Click("Composite save");
            Assert.That(towerSession.LastResult.Success, Is.True);
            Assert.That(towerSession.Draft.Id, Is.Not.EqualTo(towerId), "The visible retry action must retain the tower's failed Variant command.");
            Assert.That(towerSession.Name, Is.EqualTo("Вариант северной башни"));

            Click("Composite route structure"); panel.Reflow(1280, 720);
            Assert.That(panel.DraftSession, Is.SameAs(structureSession));
            Assert.That(panel.DraftSession.LastResult, Is.SameAs(structureFailure));
            Assert.That(panel.Root.Find("Composite save failure overlay"), Is.Null, "Structure Continue remains dismissed after the tower retry succeeds.");
            Click("Composite save");
            Assert.That(structureSession.LastResult.Success, Is.True);
            Assert.That(structureSession.Draft.Id, Is.EqualTo(structureId), "The structure retains its own normal Save command.");
            Assert.That(structureSession.Name, Is.EqualTo("Сохранение южного поста"));
            Assert.That(fixture.Run.Workshop.Entries.Count, Is.EqualTo(entries + 2));
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(gold));
        }

        [Test] public void HistoricalPageIsReadOnlyAndNewRecipeActionRemainsAvailable()
        {
            var old = new WorkshopProfile().Entries.First();
            panel.OpenEntry(old);
            Assert.That(panel.Step, Is.EqualTo(2));
            Assert.That(Button("Composite save").IsInteractable(), Is.False);
            Assert.That(Button("Composite variant").IsInteractable(), Is.False);
            Click("Composite route tower");
            Assert.That(panel.DraftSession.Draft.Kind, Is.EqualTo(AlbumEntryKind.Tower));
            Assert.That(panel.Step, Is.Zero);
        }

        [TestCase(1024, 768)] [TestCase(1920, 1080)]
        public void MainActionsRemainOutsideCatalogViewportAndInsidePanel(float width, float height)
        {
            panel.Reflow(width, height); Canvas.ForceUpdateCanvases();
            var footer = panel.Root.Find("Composite footer").GetComponent<RectTransform>();
            var next = Button("Composite next").GetComponent<RectTransform>();
            Assert.That(next.IsChildOf(footer), Is.True);
            Assert.That(next.GetComponentInParent<ScrollRect>(), Is.Null);
            var corners = new Vector3[4]; next.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector2 local = panel.Root.InverseTransformPoint(corner);
                Assert.That(panel.Root.rect.Contains(local), Is.True, local.ToString());
            }
            Assert.That(panel.Root.Find("Composite content/Composite summary"), Is.Not.Null);
            Assert.That(panel.Root.GetComponentsInChildren<Text>().Single(text => text.name == "Composite load cost").text, Is.Not.Empty);
        }

        [Test] public void PreparationButtonSuspendsEditorAndCallsShellOnce()
        {
            Click("Composite preparation");
            Assert.That(preparations, Is.EqualTo(1));
            Assert.That(panel.Root.gameObject.activeSelf, Is.False);
            panel.Resume(); Assert.That(panel.Root.gameObject.activeSelf, Is.True);
        }

        [Test] public void LibraryChooseAndConfirmedDeletePreserveTheDraftsOwnedSnapshot()
        {
            var component = fixture.Foundation("ui.library.foundation");
            Assert.That(fixture.Commands.SaveLibraryEntry(component, Guid.NewGuid()).Success, Is.True);
            panel.OpenEntry(fixture.Gun()); int writes = fixture.Store.SaveCalls;
            Click("Composite library"); Click("Composite apply library " + component.Id);
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes), "Choosing a component edits only this draft.");
            Assert.That(panel.DraftSession.Draft.LibraryReferences.Single().Id, Is.EqualTo(component.Id));
            string owned = JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.Draft));
            Click("Composite library"); Click("Composite delete library " + component.Id); Click("Composite cancel delete");
            Assert.That(fixture.Run.Workshop.Library.Any(entry => entry.Id == component.Id), Is.True);
            Assert.That(JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.Draft)), Is.EqualTo(owned));
            Click("Composite delete library " + component.Id); Click("Composite confirm delete");
            Assert.That(fixture.Run.Workshop.Library.Any(entry => entry.Id == component.Id), Is.False);
            Assert.That(JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.Draft)), Is.EqualTo(owned), "A library deletion cannot mutate an owned page recipe.");
            Click("Composite library back"); Assert.That(panel.Step, Is.EqualTo(2));
        }

        [Test] public void TrialButtonUsesTheRealFiringLaneWithoutChangingProfileOrDraft()
        {
            ReviewTower(); string before = fixture.State(); string draft = JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.Draft));
            Assert.That(panel.LastTrial, Is.Null);
            Click("Composite trial");
            Assert.That(panel.LastTrial, Is.Null, "Opening the trial leaves measurements unstarted.");
            Click("Composite trial run");
            Assert.That(panel.LastTrial, Is.Not.Null);
            Assert.That(panel.LastTrial.Budget, Is.EqualTo(400));
            Assert.That(panel.LastTrial.Copies, Is.EqualTo(400 / panel.DraftSession.Resolved.Cost));
            Assert.That(panel.LastTrial.Frames.Count, Is.GreaterThan(0));
            Assert.That(fixture.State(), Is.EqualTo(before), "Trials cannot award gold, XP, operation receipts, or modify pages.");
            Click("Composite close trial");
            Assert.That(JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.Draft)), Is.EqualTo(draft));
            Assert.That(panel.Step, Is.EqualTo(2));
        }

        [Test] public void OverloadedRecipeShowsDiagnosticsAndBlocksSaveAndTrialButtons()
        {
            var source = fixture.Catalog.Starters.First(entry => entry.Kind == AlbumEntryKind.Tower &&
                entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.mortar");
            var tower = source.Snapshot.Recipe.Tower;
            var recipe = new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] {
                new WeaponMountRecipe(0, tower.Mounts[0].Weapon), new WeaponMountRecipe(1, tower.Mounts[0].Weapon) }));
            var overloaded = new AlbumEntry("ui.overloaded", "Перегруз", 1, new RecipeSnapshot(recipe, fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions));
            panel.OpenEntry(overloaded);
            Assert.That(panel.DraftSession.Diagnostics.Any(issue => issue.Code == "assembly.load.exceeded"), Is.True);
            Assert.That(panel.Status, Is.Not.Empty);
            Assert.That(Button("Composite save").IsInteractable(), Is.False);
            Assert.That(Button("Composite trial").IsInteractable(), Is.False);
            Assert.That(panel.RunTrial(), Is.Null);
        }

        [Test] public void ClickingActiveRouteKeepsTheUnsavedDraftAndPendingName()
        {
            ReviewTower(); panel.DraftSession.Rename("Черновик без сохранения"); panel.Refresh();
            var draft = panel.DraftSession.Draft;
            Click("Composite route tower");
            Assert.That(panel.DraftSession.Draft, Is.SameAs(draft));
            Assert.That(panel.DraftSession.Name, Is.EqualTo("Черновик без сохранения"));
            Assert.That(panel.Step, Is.EqualTo(2));
        }

        [Test] public void NestedComponentRequiresSaveOrCancelBeforeChangingRoute()
        {
            Click("Composite edit foundation");
            Assert.That(Button("Composite route tower").IsInteractable(), Is.False);
            Assert.That(Button("Composite route structure").IsInteractable(), Is.False);
            Assert.That(Button("Composite library").IsInteractable(), Is.False);
        }

        [Test] public void SwitchingRoutesKeepsBothUnsavedSessionsAndPendingNames()
        {
            ReviewTower(); panel.DraftSession.Rename("Черновик башни");
            var towerSession = panel.DraftSession; var tower = towerSession.Draft;
            Click("Composite route structure"); Click("Composite next"); Click("Composite next");
            panel.DraftSession.Rename("Черновик сооружения");
            var structureSession = panel.DraftSession; var structure = structureSession.Draft;
            Click("Composite route tower");
            Assert.That(panel.DraftSession, Is.SameAs(towerSession));
            Assert.That(panel.DraftSession.Draft, Is.SameAs(tower));
            Assert.That(panel.DraftSession.Name, Is.EqualTo("Черновик башни"));
            Assert.That(panel.Step, Is.EqualTo(2));
            Click("Composite route structure");
            Assert.That(panel.DraftSession, Is.SameAs(structureSession));
            Assert.That(panel.DraftSession.Draft, Is.SameAs(structure));
            Assert.That(panel.DraftSession.Name, Is.EqualTo("Черновик сооружения"));
            Assert.That(panel.Step, Is.EqualTo(2));
        }

        [TestCase(0)] [TestCase(49)]
        public void ChangingStructureFamilyKeepsAnInvalidPendingNameWithoutThrowing(int length)
        {
            Click("Composite route structure"); Click("Composite next"); Click("Composite next");
            var field = panel.Root.GetComponentsInChildren<InputField>().Single();
            string pending = new string('Я', length);
            field.text = pending; field.onEndEdit.Invoke(pending);
            Click("Composite back"); Click("Composite back");
            Click("Composite choose family Generator");
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Family, Is.EqualTo(AssemblyStructureFamily.Generator));
            Assert.That(panel.DraftSession.Name, Is.EqualTo(pending));
            Assert.That(panel.DraftSession.CanSave, Is.False);
        }

        [Test] public void OverloadReviewRetainsTheActualPriceLoadAndNamedContributors()
        {
            var source = fixture.Catalog.Starters.First(entry => entry.Kind == AlbumEntryKind.Tower &&
                entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.mortar");
            var tower = source.Snapshot.Recipe.Tower;
            var recipe = new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] {
                new WeaponMountRecipe(0, tower.Mounts[0].Weapon), new WeaponMountRecipe(1, tower.Mounts[0].Weapon) }));
            panel.OpenEntry(new AlbumEntry("ui.overload.metrics", "Перегруз", 1,
                new RecipeSnapshot(recipe, fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions)));
            var labels = panel.Root.GetComponentsInChildren<Text>();
            string metrics = labels.Single(label => label.name == "Composite load cost").text;
            // This starter also carries plate armor: 2 load and 14 cost beyond the two mortar cores.
            Assert.That(metrics, Does.Contain("16/10").And.Contain("168"));
            string contributors = labels.Single(label => label.name == "Composite load contributors").text;
            Assert.That(contributors, Does.Contain(fixture.Catalog.Definitions.First(part => part.Id == "core.mortar").Name).And.Contain("7"));
        }

        [Test] public void CoveredLongBarrelUsesItsCompleteRecipeInsteadOfAFamilySchematic()
        {
            var page = fixture.Gun(); panel.OpenEntry(page);
            var pilot = panel.Root.GetComponentInChildren<WeaponAssemblyGraphic>();
            Assert.That(pilot, Is.Not.Null);
            Assert.That(pilot.Recipe.Recipe.Parts.Select(part => part.Id), Is.EqualTo(page.Snapshot.Recipe.Parts.Select(part => part.Id)));
            var tower = page.Snapshot.Recipe.Tower; var stock = tower.Mounts[0].Weapon;
            var longHead = new WeaponRecipe(stock.Mechanism, new PartRef("head.long", 1), stock.Sight, stock.Magazine, stock.Trigger);
            var recipe = new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] { new WeaponMountRecipe(0, longHead) }));
            panel.OpenEntry(new AlbumEntry("ui.long.schematic", "Длинный ствол", 1,
                new RecipeSnapshot(recipe, fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions)));
            var exact = panel.Root.GetComponentInChildren<WeaponAssemblyGraphic>();
            Assert.That(exact, Is.Not.Null);
            Assert.That(exact.Recipe.Recipe.Parts.Select(part => part.Id), Is.EqualTo(recipe.Parts.Select(part => part.Id)));
            Assert.That(panel.Root.GetComponentsInChildren<Text>().Any(text => text.name == "Composite schematic caption"), Is.False);
        }
    }
}
#endif
