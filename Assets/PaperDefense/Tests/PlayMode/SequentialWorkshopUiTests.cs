#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class SequentialWorkshopUiTests
    {
        AssemblyCommandFixture fixture;
        GameObject events;
        CompositeWorkshopPanel panel;

        [SetUp] public void SetUp()
        {
            fixture = new AssemblyCommandFixture(gold: 500, xp: 390);
            events = new GameObject("Sequential workshop events", typeof(EventSystem));
            var hud = fixture.Root.AddComponent<WorkshopHud>(); hud.Initialize(fixture.Run);
            panel = hud.Composite;
        }
        [TearDown] public void TearDown()
        { Object.DestroyImmediate(events); fixture.Dispose(); }
        Button Find(string name) => panel.Root.GetComponentsInChildren<Button>(true).Single(item => item.name == name);
        void Click(string name)
        {
            var button = Find(name); Assert.That(button.IsInteractable(), Is.True, name + ": " + panel.Status);
            button.onClick.Invoke(); Canvas.ForceUpdateCanvases();
        }

        [Test] public void PlayerEntranceShowsFourOrderedStagesAndTheFoundationPartsImmediately()
        {
            Assert.That(panel.Root.GetComponentsInChildren<Button>(true).Count(button => button.name.StartsWith("Sequence step ")), Is.EqualTo(4));
            Assert.That(panel.DraftSession.ActiveComponent, Is.EqualTo(PaperDefense.Application.WorkshopComponentKind.Foundation));
            Assert.That(Find("Sequence category Armor"), Is.Not.Null);
            Assert.That(Find("Sequence step 3").interactable, Is.False, "A new recipe reaches review through assembly.");
        }

        [Test] public void RouteTabsUseSelectionWhileNextAndSaveKeepPrimaryEmphasis()
        {
            AssertRouteEmphasis(false, "Sequence next");
            Click("Sequence next"); Click("Sequence next"); Click("Sequence next");
            AssertRouteEmphasis(false, "Sequence save");
            Click("Composite route structure"); AssertRouteEmphasis(true, "Structure next");
            Click("Structure next"); Click("Structure next");
            AssertRouteEmphasis(true, "Structure save");
        }

        void AssertRouteEmphasis(bool structure, string primaryAction)
        {
            foreach (string route in new[] { "Composite route tower", "Composite route structure" })
            {
                var surface = Find(route).GetComponent<WorkshopSurfaceGraphic>();
                Assert.That(surface.FieldPrimary, Is.False, "Route selection must not compete with " + primaryAction);
                var chosen = (bool)typeof(WorkshopSurfaceGraphic).GetField("Chosen",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(surface);
                Assert.That(chosen, Is.EqualTo(structure == (route == "Composite route structure")), route);
            }
            Assert.That(Find(primaryAction).GetComponent<WorkshopSurfaceGraphic>().FieldPrimary, Is.True, primaryAction);
        }

        [Test] public void SaveFoundationThenWeaponThenAssemblyPublishesTheFinalTower()
        {
            Click("Sequence next"); Assert.That(panel.Step, Is.EqualTo(1), panel.Status);
            Assert.That(fixture.Run.Workshop.Library.Any(entry => entry.Kind == ComponentLibraryKind.Foundation), Is.True);
            Assert.That(Find("Sequence category Mount"), Is.Not.Null);
            Click("Sequence next"); Assert.That(panel.Step, Is.EqualTo(2), panel.Status);
            Assert.That(panel.DraftSession.IsComponentOpen, Is.False);
            Click("Sequence next"); Assert.That(panel.Step, Is.EqualTo(3));
            var input = panel.Root.GetComponentsInChildren<InputField>().Single();
            input.text = "Моя составная башня"; input.onEndEdit.Invoke(input.text);
            Click("Sequence save");
            Assert.That(fixture.Run.Workshop.Entries.Single(entry => entry.Id == panel.DraftSession.Draft.Id).Name, Is.EqualTo("Моя составная башня"));
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(500));
        }

        [Test] public void FailedComponentWriteKeepsTheCurrentStageAndEditableParts()
        {
            var before = panel.DraftSession.PreviewRecipe;
            fixture.Store.RefuseWrites = true; Click("Sequence next");
            Assert.That(panel.Step, Is.Zero);
            Assert.That(panel.DraftSession.IsComponentOpen, Is.True);
            Assert.That(panel.DraftSession.PreviewRecipe, Is.SameAs(before));
            Assert.That(panel.Status, Is.Not.Empty);
            fixture.Store.RefuseWrites = false; Click("Sequence next");
            Assert.That(panel.Step, Is.EqualTo(1), panel.Status);
        }

        [Test] public void InspectedWeaponPurchaseCommitsTheRightAndInstallsOnlyTheLocalDraft()
        {
            Click("Sequence next"); Click("Sequence category Head"); Click("Sequence inspect head.long");
            Assert.That(fixture.Run.Workshop.IsUnlocked("head.long"), Is.False);
            string saved = JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.Draft));
            int gold = fixture.Run.Workshop.Gold, writes = fixture.Store.SaveCalls;
            Click("Sequence install head.long");
            Assert.That(fixture.Run.Workshop.IsUnlocked("head.long"), Is.True);
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(gold - 80));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes + 1));
            Assert.That(panel.DraftSession.PreviewRecipe.Tower.Mounts[0].Weapon.Head.Id, Is.EqualTo("head.long"));
            Assert.That(JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.Draft)), Is.EqualTo(saved),
                "Installing a purchased part edits the open component, not the saved parent page.");
            Assert.That(Find("Sequence install head.long").IsInteractable(), Is.False, "The installed choice cannot be charged twice.");
        }

        [Test] public void RefusedWeaponPurchaseKeepsGoldRightsAndTheEquippedPartUntilExplicitRetry()
        {
            Click("Sequence next"); Click("Sequence category Head"); Click("Sequence inspect head.long");
            int gold = fixture.Run.Workshop.Gold;
            string recipe = JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.PreviewEntry));
            fixture.Store.RefuseWrites = true; Click("Sequence install head.long");
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(fixture.Run.Workshop.IsUnlocked("head.long"), Is.False);
            Assert.That(JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.PreviewEntry)), Is.EqualTo(recipe));
            Assert.That(panel.Step, Is.EqualTo(1)); Assert.That(panel.Status, Is.Not.Empty);
            Assert.That(Find("Sequence install head.long").IsInteractable(), Is.True);
            fixture.Store.RefuseWrites = false; Click("Sequence install head.long");
            Assert.That(fixture.Run.Workshop.IsUnlocked("head.long"), Is.True);
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(gold - 80));
            Assert.That(panel.DraftSession.PreviewRecipe.Tower.Mounts[0].Weapon.Head.Id, Is.EqualTo("head.long"));
        }

        [Test] public void InspectingAWeaponPartExplainsItsModifiersWithoutInstallingOrSavingIt()
        {
            Click("Sequence next"); Click("Sequence category Head");
            var recipe = panel.DraftSession.PreviewRecipe;
            string draft = JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.PreviewEntry));
            string state = fixture.State(); byte[] saved = fixture.SavedBytes;
            int writes = fixture.Store.SaveCalls;
            Click("Sequence inspect head.long");
            var comparison = panel.Root.GetComponentsInChildren<Text>().Single(item => item.name == "Sequence specification").text;
            Assert.That(comparison, Does.StartWith("Модификаторы детали"));
            Assert.That(comparison, Does.Contain("×1 → ×1.25").And.Contain("+0.25"), "Show the actual range multiplier change, not a fictitious completed-weapon DPS estimate.");
            Assert.That(panel.DraftSession.PreviewRecipe, Is.SameAs(recipe));
            Assert.That(JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.PreviewEntry)), Is.EqualTo(draft));
            Assert.That(fixture.State(), Is.EqualTo(state)); Assert.That(fixture.SavedBytes, Is.EqualTo(saved));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
        }

        [Test] public void SwitchingRoutesPreservesReachedStagesAndTheSavedComponentName()
        {
            var field = panel.Root.GetComponentsInChildren<InputField>().Single();
            field.text = "Северная основа";
            Click("Sequence next"); Click("Sequence next"); Click("Sequence next");
            Click("Composite route structure"); Click("Composite route tower");
            Assert.That(panel.Step, Is.EqualTo(3));
            Click("Sequence back"); Assert.That(panel.Step, Is.EqualTo(2));
            Click("Sequence step 0");
            Assert.That(panel.DraftSession.ComponentName, Is.EqualTo("Северная основа"));
        }

        [Test] public void ChoosingAnotherComponentRequiresAnExplicitDecisionForUnsavedEdits()
        {
            var field = panel.Root.GetComponentsInChildren<InputField>().Single(); field.text = "Несохранённая основа";
            Click("Sequence edit library library.foundation.standard");
            Assert.That(panel.DraftSession.ComponentName, Is.EqualTo("Несохранённая основа"));
            Click("Workshop cancel confirmation");
            Assert.That(panel.DraftSession.ComponentName, Is.EqualTo("Несохранённая основа"));
            Assert.That(fixture.Store.SaveCalls, Is.Zero);
        }

        [Test] public void AssemblyCanRemoveAnOccupiedSlotThatTheNewFrameDoesNotProvide()
        {
            var source = fixture.Gun(); var tower = source.Snapshot.Recipe.Tower;
            var frame = fixture.Catalog.Definitions.First(part => part.Category == ComponentCategory.Frame && part.Get(AssemblyStat.MountCount, ModifierOperation.Set) == 1);
            Assert.That(fixture.Commands.PurchasePart(frame.Id, System.Guid.NewGuid()).Success, Is.True);
            var foundation = new FoundationRecipe(new PartRef(frame.Id, frame.Revision), tower.Foundation.Hull, tower.Foundation.Armor, tower.Foundation.Feed, tower.Foundation.Turn);
            var recipe = new AssemblyRecipe(new TowerRecipe(foundation, new[] { new WeaponMountRecipe(0, tower.Mounts[0].Weapon), new WeaponMountRecipe(1, tower.Mounts[0].Weapon) }));
            panel.OpenEntry(new AlbumEntry("unavailable.mount", "Лишнее орудие", 1, new RecipeSnapshot(recipe, fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions)));
            Click("Sequence step 2"); Click("Sequence mount 1"); Click("Sequence remove weapon");
            Assert.That(panel.DraftSession.PreviewRecipe.Tower.Mounts.Select(item => item.SlotIndex), Is.EqualTo(new[] { 0 }));
        }

        [Test] public void HistoricalBurstWeaponCanReturnToOrdinaryModeBeforeUsingADifferentMagazine()
        {
            fixture.Dispose(); fixture = new AssemblyCommandFixture(gold: 500, xp: 9999);
            var hud = fixture.Root.AddComponent<WorkshopHud>(); hud.Initialize(fixture.Run); panel = hud.Composite;
            Assert.That(fixture.Commands.PurchasePart("trigger.burst", System.Guid.NewGuid()).Success, Is.True);
            var source = fixture.Gun(); var tower = source.Snapshot.Recipe.Tower; var stock = tower.Mounts[0].Weapon;
            var burst = new WeaponRecipe(stock.Mechanism, stock.Head, stock.Sight, stock.Magazine, new PartRef("trigger.burst", 1), stock.Mount);
            var recipe = new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] { new WeaponMountRecipe(0, burst) }));
            panel.OpenEntry(new AlbumEntry("legacy.burst", "Очередь", 1, new RecipeSnapshot(recipe, fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions)));
            Click("Sequence step 1"); Click("Sequence reset trigger");
            Assert.That(panel.DraftSession.PreviewRecipe.Tower.Mounts[0].Weapon.Trigger.Id, Is.EqualTo("trigger.standard"));
        }

        [TestCase(1024,768)] [TestCase(800,600)]
        public void UnsavedDraftConfirmationFitsTheWorkshopAtCompactSizes(int width, int height)
        {
            panel.Reflow(width, height);
            panel.Root.GetComponentsInChildren<InputField>().Single().text = "Изменённая основа";
            Click("Sequence edit library library.foundation.standard");
            var card = panel.Root.Find("Workshop confirmation overlay/Workshop confirmation") as RectTransform;
            Assert.That(card, Is.Not.Null);
            var corners = new Vector3[4]; card.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var local = panel.Root.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(panel.Root.rect.xMin, panel.Root.rect.xMax));
                Assert.That(local.y, Is.InRange(panel.Root.rect.yMin, panel.Root.rect.yMax));
            }
        }
    }
}
#endif
