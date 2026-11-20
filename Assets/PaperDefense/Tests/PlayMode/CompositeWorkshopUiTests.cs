#if UNITY_EDITOR
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
        GameObject events;
        CompositeWorkshopPanel panel;
        int preparations;

        [SetUp] public void SetUp()
        {
            fixture = new AssemblyCommandFixture(gold: 500, xp: 390);
            events = new GameObject("Released composite workshop events", typeof(EventSystem));
            var hud = fixture.Root.AddComponent<WorkshopHud>();
            hud.StartRequested = () => preparations++;
            hud.Initialize(fixture.Run); panel = hud.Composite;
            fixture.Root.SetActive(true);
            UiMenuMotion.FinishIn(panel.Root);
            Canvas.ForceUpdateCanvases();
            Assert.That(panel.Root.gameObject.activeInHierarchy, Is.True, "Exercise the visible released workshop host.");
        }

        [TearDown] public void TearDown()
        { Object.DestroyImmediate(events); fixture.Dispose(); }

        Button Button(string name) => panel.Root.GetComponentsInChildren<Button>(true).Single(button => button.name == name);
        void Click(string name)
        {
            var button = Button(name);
            Assert.That(button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True, name + ": " + panel.Status);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Canvas.ForceUpdateCanvases();
        }
        void ReviewTower() { Click("Sequence next"); Click("Sequence next"); Click("Sequence next"); }

        [Test] public void SaveVariantButtonCreatesIndependentPageAndKeepsTheOriginal()
        {
            ReviewTower(); Click("Sequence save");
            string original = panel.DraftSession.Draft.Id;
            var saved = fixture.Run.Workshop.Entries.Single(entry => entry.Id == original);
            string snapshot = JsonUtility.ToJson(new AlbumEntryData(saved));
            int count = fixture.Run.Workshop.Entries.Count;
            Click("Sequence variant");
            Assert.That(fixture.Run.Workshop.Entries.Count, Is.EqualTo(count + 1));
            Assert.That(panel.DraftSession.Draft.Id, Is.Not.EqualTo(original));
            Assert.That(JsonUtility.ToJson(new AlbumEntryData(fixture.Run.Workshop.Entries.Single(entry => entry.Id == original))), Is.EqualTo(snapshot));
        }

        [TestCase(false)] [TestCase(true)]
        public void RefusedSavePreservesTheNamedReviewThroughReflowAndAnExplicitRetry(bool variant)
        {
            ReviewTower(); Click("Sequence save");
            string originalId = panel.DraftSession.Draft.Id;
            int count = fixture.Run.Workshop.Entries.Count, gold = fixture.Run.Workshop.Gold;
            var name = panel.Root.GetComponentInChildren<InputField>();
            name.text = "Северный дозор после ошибки"; name.onEndEdit.Invoke(name.text);
            string recipe = JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.PreviewEntry));
            fixture.Store.RefuseWrites = true;
            string action = variant ? "Sequence variant" : "Sequence save";
            Click(action);
            Assert.That(panel.DraftSession.LastResult.Code, Is.EqualTo("profile.write_failed"));
            int attempts = fixture.Store.SaveCalls;
            panel.Reflow(800, 600); panel.Refresh(); Canvas.ForceUpdateCanvases();
            Assert.That(panel.Step, Is.EqualTo(3));
            Assert.That(panel.DraftSession.Name, Is.EqualTo("Северный дозор после ошибки"));
            Assert.That(JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.PreviewEntry)), Is.EqualTo(recipe));
            Assert.That(panel.DraftSession.Draft.Id, Is.EqualTo(originalId));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(attempts), "Reflow cannot replay a rejected save.");
            Assert.That(fixture.Run.Workshop.Entries.Count, Is.EqualTo(count));
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(gold));
            fixture.Store.RefuseWrites = false; Click(action);
            Assert.That(panel.DraftSession.LastResult.Success, Is.True);
            Assert.That(panel.DraftSession.Draft.Id == originalId, Is.EqualTo(!variant));
            Assert.That(fixture.Run.Workshop.Entries.Count, Is.EqualTo(count + (variant ? 1 : 0)));
            Assert.That(fixture.Run.Workshop.Entries.Single(entry => entry.Id == panel.DraftSession.Draft.Id).Name,
                Is.EqualTo("Северный дозор после ошибки"));
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(gold));
        }

        [Test] public void HistoricalPageIsReadOnlyAndNewRecipeActionRemainsAvailable()
        {
            panel.OpenEntry(new WorkshopProfile().Entries.First());
            Assert.That(Button("Composite save").IsInteractable(), Is.False);
            Assert.That(Button("Composite variant").IsInteractable(), Is.False);
            int writes = fixture.Store.SaveCalls;
            Click("Composite route tower");
            Assert.That(panel.DraftSession.Draft.Kind, Is.EqualTo(AlbumEntryKind.Tower));
            Assert.That(panel.Step, Is.Zero);
            Assert.That(Button("Sequence next").IsInteractable(), Is.True);
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
        }

        [Test] public void PreparationButtonSuspendsEditorAndCallsTheHostOnce()
        {
            Click("Composite preparation");
            Assert.That(preparations, Is.EqualTo(1));
            Assert.That(panel.Root.gameObject.activeSelf, Is.False);
            panel.Resume(); Assert.That(panel.Root.gameObject.activeSelf, Is.True);
        }

        [Test] public void OverloadedRecipeShowsDiagnosticsAndBlocksFinalSaveAndTrial()
        {
            var source = fixture.Catalog.Starters.First(entry => entry.Kind == AlbumEntryKind.Tower &&
                entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.mortar");
            var tower = source.Snapshot.Recipe.Tower;
            var recipe = new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] {
                new WeaponMountRecipe(0, tower.Mounts[0].Weapon), new WeaponMountRecipe(1, tower.Mounts[0].Weapon) }));
            panel.OpenEntry(new AlbumEntry("ui.overloaded", "Перегруз", 1,
                new RecipeSnapshot(recipe, fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions)));
            Assert.That(panel.DraftSession.Diagnostics.Any(issue => issue.Code == "assembly.load.exceeded"), Is.True);
            Assert.That(panel.Status, Is.Not.Empty);
            Assert.That(Button("Sequence save").IsInteractable(), Is.False);
            Assert.That(Button("Composite trial").IsInteractable(), Is.False);
            Assert.That(panel.RunTrial(), Is.Null);
        }

        [Test] public void ClickingActiveRouteKeepsTheUnsavedDraftAndPendingName()
        {
            ReviewTower(); panel.DraftSession.Rename("Черновик без сохранения"); panel.Refresh();
            var draft = panel.DraftSession.Draft;
            int writes = fixture.Store.SaveCalls;
            Click("Composite route tower");
            Assert.That(panel.DraftSession.Draft, Is.SameAs(draft));
            Assert.That(panel.DraftSession.Name, Is.EqualTo("Черновик без сохранения"));
            Assert.That(panel.Step, Is.EqualTo(3));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
        }
    }
}
#endif
