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
    public sealed class ComponentPurchaseUiTests
    {
        GameObject canvas;
        ComponentAssemblyPanel Panel(AssemblyCommandFixture fixture, WorkshopDraftSession draft)
        {
            if (canvas == null) canvas = new GameObject("Isolated component catalogue", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(1024, 768);
            var panel = new ComponentAssemblyPanel(canvas.transform, fixture.Run, draft, TextResourcesAsset.Default, () => { });
            panel.Reflow(976, 538); panel.Show(); return panel;
        }
        static WorkshopDraftSession Weapon(AssemblyCommandFixture fixture)
        { var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(fixture.Gun()); draft.OpenWeapon(0); draft.SelectCategory(ComponentCategory.Head); return draft; }
        Button Button(string name) => canvas.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
        [TearDown] public void TearDown() { if (canvas != null) Object.DestroyImmediate(canvas); }

        [Test] public void NativeSubmitInstallsThePartWithoutDestroyingTheExecutingButton()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390))
            {
                Assert.That(fixture.Commands.PurchasePart("head.long", Guid.NewGuid()).Success, Is.True);
                var draft = Weapon(fixture); var panel = Panel(fixture, draft); panel.Catalog.InspectPart("head.long");
                var events = new GameObject("Isolated catalogue submit events", typeof(EventSystem));
                try
                {
                    var button = Button("Component select part");
                    EventSystem.current.SetSelectedGameObject(button.gameObject);
                    ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    Assert.That(draft.PreviewRecipe.Tower.Mounts.Single().Weapon.Head.Id, Is.EqualTo("head.long"));
                    UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
                }
                finally { Object.DestroyImmediate(events); }
            }
        }

        [Test] public void LockedPartShowsConditionsAndCannotBeInstalled()
        {
            using (var fixture = new AssemblyCommandFixture(94, 0))
            {
                var draft = Weapon(fixture); var before = draft.PreviewRecipe;
                var panel = Panel(fixture, draft); panel.Catalog.SetShowUnavailable(true); panel.Catalog.InspectPart("head.long");
                Assert.That(Button("Component select part").interactable, Is.False);
                Assert.That(Button("Component purchase part").interactable, Is.False);
                string text = string.Join("|", canvas.GetComponentsInChildren<Text>(true).Select(t => t.text));
                Assert.That(text, Does.Contain("4")); Assert.That(text, Does.Contain("80"));
                Assert.That(draft.PreviewRecipe, Is.SameAs(before));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void MagazineShowsCapacityAndCycleTimingsFromTheResolver()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var draft = Weapon(fixture); draft.SelectCategory(ComponentCategory.Magazine);
                Panel(fixture, draft);
                var weapon = draft.Resolved.Weapons.Single();
                string expected = TextResourcesAsset.Default.Resolve(UserText.Message("assembly.component.magazine_stats",
                    weapon.MagazineSize, weapon.Interval, weapon.ReloadSeconds));
                Assert.That(canvas.GetComponentsInChildren<Text>(true).Any(t => t.text == expected), Is.True);
            }
        }

        [Test] public void CatalogueOpensWithTheFirstOwnedPartFullyInsideItsViewport()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var panel = Panel(fixture, Weapon(fixture)); panel.Catalog.SetShowUnavailable(true); Canvas.ForceUpdateCanvases();
                var first = Button("Component inspect head.gun").GetComponent<RectTransform>();
                var viewport = (RectTransform)first.parent.parent;
                var corners = new Vector3[4]; first.GetWorldCorners(corners);
                float top = viewport.InverseTransformPoint(corners[1]).y;
                float bottom = viewport.InverseTransformPoint(corners[0]).y;
                Assert.That(top, Is.LessThanOrEqualTo(viewport.rect.yMax + .1f));
                Assert.That(bottom, Is.GreaterThanOrEqualTo(viewport.rect.yMin - .1f));
            }
        }

        [Test] public void PurchaseRequiresConfirmationAndNeverInstallsThePart()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390))
            {
                var draft = Weapon(fixture); var panel = Panel(fixture, draft);
                panel.Catalog.SetShowUnavailable(true); panel.Catalog.InspectPart("head.long");
                Button("Component purchase part").onClick.Invoke();
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(94));
                Assert.That(draft.PreviewRecipe.Tower.Mounts.Single().Weapon.Head.Id, Is.EqualTo("head.gun"));
                Button("Component purchase confirm").onClick.Invoke();
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(14));
                Assert.That(fixture.Run.Workshop.IsUnlocked("head.long"), Is.True);
                Assert.That(draft.PreviewRecipe.Tower.Mounts.Single().Weapon.Head.Id, Is.EqualTo("head.gun"));
                Assert.That(Button("Component select part").interactable, Is.True);
                Button("Component select part").onClick.Invoke();
                Assert.That(draft.PreviewRecipe.Tower.Mounts.Single().Weapon.Head.Id, Is.EqualTo("head.long"));
                Assert.That(draft.Draft.Snapshot.Recipe.Tower.Mounts.Single().Weapon.Head.Id, Is.EqualTo("head.gun"));
            }
        }

        [Test] public void PurchasedRightIsVisibleInAnotherCompatibleCatalogue()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390))
            {
                var first = Weapon(fixture); var panel = Panel(fixture, first);
                panel.Catalog.SetShowUnavailable(true); panel.Catalog.InspectPart("head.long");
                Button("Component purchase part").onClick.Invoke(); Button("Component purchase confirm").onClick.Invoke();
                panel.Hide(); Object.DestroyImmediate(panel.Root.gameObject);
                var second = Weapon(fixture); var reopened = Panel(fixture, second); reopened.Catalog.InspectPart("head.long");
                Assert.That(Button("Component select part").interactable, Is.True);
                Assert.That(Button("Component purchase part").gameObject.activeSelf, Is.False);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(14));
                Assert.That(second.PreviewRecipe.Tower.Mounts.Single().Weapon.Head.Id, Is.EqualTo("head.gun"));
            }
        }

        [Test] public void UnavailableToggleKeepsCompatibilityFiltering()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var panel = Panel(fixture, Weapon(fixture));
                Assert.That(canvas.transform.Find("Component assembly/Component catalog/Component catalogue list/Component catalogue list content/Component inspect head.long"), Is.Null);
                panel.Catalog.SetShowUnavailable(true);
                Assert.That(canvas.GetComponentsInChildren<Button>(true).Any(b => b.name == "Component inspect head.long"), Is.True);
                Assert.That(canvas.GetComponentsInChildren<Button>(true).Any(b => b.name == "Component inspect head.mortar"), Is.False);
            }
        }

        [Test] public void BurstAndDrumOfferAnExplicitStockMagazineAction()
        {
            using (var fixture = new AssemblyCommandFixture(1000, 700))
            {
                Assert.That(fixture.Commands.PurchasePart("magazine.drum", Guid.NewGuid()).Success, Is.True);
                Assert.That(fixture.Commands.PurchasePart("trigger.burst", Guid.NewGuid()).Success, Is.True);
                var draft = Weapon(fixture); draft.SelectPart("magazine.drum"); draft.SelectPart("trigger.burst");
                var panel = Panel(fixture, draft);
                Assert.That(draft.CanSaveComponent, Is.False);
                Assert.That(draft.PreviewRecipe.Tower.Mounts.Single().Weapon.Magazine.Id, Is.EqualTo("magazine.drum"));
                Button("Component apply suggested part").onClick.Invoke();
                Assert.That(draft.PreviewRecipe.Tower.Mounts.Single().Weapon.Magazine.Id, Is.EqualTo("magazine.standard"));
                Assert.That(draft.PreviewRecipe.Tower.Mounts.Single().Weapon.Trigger.Id, Is.EqualTo("trigger.burst"));
                Assert.That(draft.Resolved, Is.Not.Null);
                Assert.That(draft.Diagnostics.Any(d => d.Code == "assembly.part.required"), Is.False);
                Assert.That(draft.CanSaveComponent, Is.False, "The burst trigger still needs physical fit coverage beyond the current gun pilot.");
                Assert.That(draft.Diagnostics.Any(d => d.Code.Contains("fit")), Is.True);
            }
        }

        [Test] public void RefusedPurchaseKeepsCatalogueAndNestedDraftOpen()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390))
            {
                var draft = Weapon(fixture); var panel = Panel(fixture, draft); var before = draft.PreviewRecipe;
                panel.Catalog.SetShowUnavailable(true); panel.Catalog.InspectPart("head.long"); fixture.Store.RefuseWrites = true;
                Button("Component purchase part").onClick.Invoke(); Button("Component purchase confirm").onClick.Invoke();
                Assert.That(panel.Root.gameObject.activeSelf, Is.True); Assert.That(draft.IsComponentOpen, Is.True);
                Assert.That(draft.PreviewRecipe, Is.SameAs(before)); Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(94));
                Assert.That(canvas.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("Injected assembly transaction write refusal")), Is.True);
            }
        }
    }
}
#endif
