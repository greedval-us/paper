#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class AdaptiveWorkshopUiTests
    {
        AssemblyCommandFixture fixture;
        GameObject events, hudRoot;
        CompositeWorkshopPanel panel;

        [SetUp] public void SetUp()
        {
            fixture = new AssemblyCommandFixture(gold: 500, xp: 390);
            events = new GameObject("Adaptive workshop events", typeof(EventSystem));
            CreatePanel();
        }
        void CreatePanel()
        {
            if (hudRoot != null) Object.DestroyImmediate(hudRoot);
            hudRoot = new GameObject("Adaptive workshop active HUD");
            var hud = hudRoot.AddComponent<WorkshopHud>(); hud.Initialize(fixture.Run); panel = hud.Composite;
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(hudRoot); Object.DestroyImmediate(events); fixture.Dispose(); }
        T Find<T>(string name) where T : Component => panel.Root.GetComponentsInChildren<T>(true).Single(item => item.name == name);
        void Click(string name)
        {
            var button = Find<Button>(name); Assert.That(button.IsInteractable(), Is.True, name + ": " + panel.Status);
            button.onClick.Invoke(); Canvas.ForceUpdateCanvases();
        }
        static Rect Bounds(RectTransform target, RectTransform relative)
        {
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            var local = corners.Select(relative.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(point => point.x), local.Min(point => point.y), local.Max(point => point.x), local.Max(point => point.y));
        }
        static void AssertInside(RectTransform target, RectTransform parent)
        {
            var bounds = Bounds(target, parent);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(parent.rect.xMin - 1), target.name);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(parent.rect.yMin - 1), target.name);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(parent.rect.xMax + 1), target.name);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(parent.rect.yMax + 1), target.name);
        }

        [TestCase(800, 600)] [TestCase(1024, 768)] [TestCase(1280, 720)]
        public void WeaponDetailScrollAndInstallKeepSeparateVisibleRegions(int width, int height)
        {
            panel.Reflow(width, height); Click("Sequence next");
            var detail = Find<RectTransform>("Sequence part detail");
            var scroll = Find<ScrollRect>("Sequence specification scroll");
            var install = Find<Button>("Sequence install magazine.standard").transform as RectTransform;
            AssertInside(scroll.viewport, detail); AssertInside(install, detail); AssertInside(install, panel.Root);
            Assert.That(Bounds(scroll.viewport, detail).Overlaps(Bounds(install, detail)), Is.False);
            Assert.That(install.GetComponentInParent<ScrollRect>(), Is.Null, "Install remains visible while reading details.");
        }

        [Test] public void CompactSavedComponentCatalogueDoesNotCoverItsLiveMetricsAndKeepsScrollOnRefresh()
        {
            for (int i = 0; i < 18; i++)
                Assert.That(fixture.Commands.SaveLibraryEntry(fixture.Foundation("adaptive.foundation." + i), Guid.NewGuid()).Success, Is.True);
            panel.Reflow(800, 600); Canvas.ForceUpdateCanvases();
            var list = Find<ScrollRect>("Sequence saved components");
            var metrics = Find<Text>("Sequence live component metrics").rectTransform;
            var metricsScroll = metrics.GetComponentInParent<ScrollRect>(true);
            var metricsBounds = metricsScroll != null ? metricsScroll.viewport : metrics;
            Assert.That(Bounds(list.viewport, panel.Root).Overlaps(Bounds(metricsBounds, panel.Root)), Is.False);
            list.verticalNormalizedPosition = .25f; Canvas.ForceUpdateCanvases();
            panel.Refresh(); Canvas.ForceUpdateCanvases();
            Assert.That(Find<ScrollRect>("Sequence saved components").verticalNormalizedPosition, Is.EqualTo(.25f).Within(.02f));
            var last = Find<Button>("Sequence edit library adaptive.foundation.17").GetComponent<RectTransform>();
            UiScrollFocus.Reveal(last); list = Find<ScrollRect>("Sequence saved components");
            AssertInside(last, list.viewport);
        }

        [Test] public void LongInstalledPartNameKeepsThreeChoicesAndItsEffectsVisibleAt800()
        {
            const string longName = "Штатная многосекционная обойма для дальнего рубежа обороны с дополнительными кассетами";
            fixture.AssemblyAsset.catalog.Definitions.Single(part => part.Id == "magazine.standard").Name = longName;
            fixture.Restart(); CreatePanel();
            var source = fixture.Gun();
            var definitions = source.Snapshot.Definitions.Select(part => part.Id != "magazine.standard" ? part :
                new ComponentDefinition(part.Id, part.Revision, longName, part.Category, part.RequiredLevel, part.UnlockGold,
                    part.WeaponFamilies, part.StructureFamilies, part.Modifiers, part.RequiredParts));
            panel.OpenEntry(new AlbumEntry(source.Id, source.Name, source.Revision,
                new RecipeSnapshot(source.Snapshot.Recipe, source.Snapshot.BalanceRevision, definitions)));
            panel.Reflow(800, 600); Click("Sequence step 1"); Click("Sequence category Magazine");
            var list = Find<ScrollRect>("Sequence part list");
            int visible = list.content.GetComponentsInChildren<Button>().Count(button => {
                var bounds = Bounds((RectTransform)button.transform, list.viewport);
                return bounds.yMin >= list.viewport.rect.yMin - 1 && bounds.yMax <= list.viewport.rect.yMax + 1;
            });
            Assert.That(visible, Is.GreaterThanOrEqualTo(3), "Reading a long installed name must not consume the part catalogue.");
            var title = Find<Text>("Sequence inspected name");
            Assert.That(title.GetComponent<WorkshopTooltip>().Hint, Does.Contain(longName));
            var effects = Find<Text>("Sequence specification");
            Assert.That(effects.text, Is.Not.Empty);
            AssertInside(effects.rectTransform, Find<ScrollRect>("Sequence specification scroll").viewport);
            AssertInside(Find<Button>("Sequence install magazine.standard").transform as RectTransform, panel.Root);
            Assert.That(Find<Button>("Sequence install magazine.standard").interactable, Is.False);
            Assert.That(fixture.Store.SaveCalls, Is.Zero);
        }

        [Test] public void GrowingWeaponFamiliesKeepReadableControlsInsideABoundedCatalogue()
        {
            var source = fixture.AssemblyAsset.catalog.Definitions.First(part => part.Id == "core.gun");
            var additions = Enumerable.Range(0, 20).Select(index => {
                var copy = JsonUtility.FromJson<ComponentDefinitionInput>(JsonUtility.ToJson(source));
                copy.Id = "core.adaptive." + index; copy.Name = "Дальнобойный механизм " + index; return copy;
            });
            fixture.AssemblyAsset.catalog.Definitions = fixture.AssemblyAsset.catalog.Definitions.Concat(additions).ToArray();
            fixture.Restart(); CreatePanel(); panel.Reflow(800, 600); Click("Sequence next");
            var families = panel.Root.GetComponentsInChildren<Button>().Where(item => item.name.StartsWith("Sequence family ")).ToArray();
            Assert.That(families.Length, Is.GreaterThanOrEqualTo(20));
            foreach (var family in families)
            {
                Assert.That(((RectTransform)family.transform).rect.width, Is.GreaterThanOrEqualTo(120), family.name);
                Assert.That(family.GetComponentInParent<ScrollRect>(true), Is.Not.Null, family.name);
            }
            var last = (RectTransform)families.Last().transform; UiScrollFocus.Reveal(last);
            AssertInside(last, last.GetComponentInParent<ScrollRect>(true).viewport);
        }

        [Test] public void ExplanationsOpenByClickWithoutChangingTheDraft()
        {
            panel.Reflow(800, 600); var draft = panel.DraftSession;
            Assert.That(panel.Root.GetComponentsInChildren<Text>().Any(label => label.name == "Sequence library hint"), Is.False);
            Click("Sequence help");
            Assert.That(panel.Root.Find("Context help"), Is.Not.Null);
            Click("Close context help"); Assert.That(panel.DraftSession, Is.SameAs(draft));
            Click("Composite route structure"); Click("Structure help");
            Assert.That(panel.Root.Find("Context help"), Is.Not.Null);
        }

        [TestCase(800, 600)] [TestCase(1280, 720)]
        public void CompactAssemblyAndReviewKeepTheirScrollableBodiesBounded(int width, int height)
        {
            panel.Reflow(width, height); Click("Sequence next"); Click("Sequence next");
            var preview = Find<RectTransform>("Sequence assembled preview");
            Assert.That(preview.GetComponentsInChildren<ScrollRect>().Length, Is.GreaterThan(0));
            foreach (var scroll in panel.Root.GetComponentsInChildren<ScrollRect>()) AssertInside(scroll.viewport, scroll.transform.parent as RectTransform);
            Click("Sequence next");
            var stats = Find<RectTransform>("Sequence final stats");
            AssertInside(Find<ScrollRect>("Sequence final specification").viewport, stats);
            AssertInside(Find<Button>("Sequence save").transform as RectTransform, panel.Root);
        }

        [TestCase(false)] [TestCase(true)]
        public void AdvancingToReviewKeepsKeyboardFocusOnItsPrimarySaveAction(bool structure)
        {
            panel.Reflow(800, 600);
            if (structure) { Click("Composite route structure"); Click("Structure next"); }
            else { Click("Sequence next"); Click("Sequence next"); }
            string next = structure ? "Structure next" : "Sequence next";
            EventSystem.current.SetSelectedGameObject(Find<Button>(next).gameObject);
            Click(next);
            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.transform.IsChildOf(panel.Root), Is.True);
            Assert.That(selected.name, Is.EqualTo(structure ? "Structure save" : "Sequence save"));
            Assert.That(selected.GetComponent<Button>().IsInteractable(), Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void PreviouslyFittingPartDetailsStartAtTheTopAfterACompactReflow(bool weapon)
        {
            panel.Reflow(1600, 900);
            if (weapon) Click("Sequence next");
            Canvas.ForceUpdateCanvases();
            var scroll = Find<ScrollRect>("Sequence specification scroll");
            Assert.That(scroll.content.rect.height, Is.LessThanOrEqualTo(scroll.viewport.rect.height + 1));
            scroll.verticalNormalizedPosition = 0;
            panel.Reflow(800, 600); Canvas.ForceUpdateCanvases();
            Assert.That(Find<ScrollRect>("Sequence specification scroll").verticalNormalizedPosition, Is.EqualTo(1).Within(.005f));
        }
    }
}
#endif
