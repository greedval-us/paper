#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class MenuSelectionClarityTests
    {
        AssemblyCommandFixture fixture;
        GameObject canvas, events;
        CompositeWorkshopPanel panel;

        [SetUp] public void SetUp()
        {
            fixture = new AssemblyCommandFixture(gold: 500, xp: 390);
            events = new GameObject("Selection clarity events", typeof(EventSystem));
            canvas = new GameObject("Selection clarity canvas", typeof(RectTransform), typeof(Canvas));
            panel = new CompositeWorkshopPanel(canvas.transform, fixture.Run, null, () => { }, sequentialFlow: true);
        }

        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(canvas); Object.DestroyImmediate(events); fixture.Dispose();
        }

        Button Find(string name) => panel.Root.GetComponentsInChildren<Button>().Single(button => button.name == name);
        ComponentLibraryEntry Library(string id) => fixture.Run.Workshop.Library.Single(entry => entry.Id == id);
        void Click(string name)
        {
            var button = Find(name);
            Assert.That(button.IsInteractable(), Is.True, name + ": " + panel.Status);
            button.onClick.Invoke(); Canvas.ForceUpdateCanvases();
        }

        static Color32[] SurfaceColors(Button button)
        {
            var surface = button.GetComponent<WorkshopSurfaceGraphic>();
            using (var vertices = new VertexHelper())
            {
                typeof(WorkshopSurfaceGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(surface, new object[] { vertices });
                Assert.That(vertices.currentVertCount, Is.GreaterThan(0));
                var colors = new Color32[vertices.currentVertCount];
                for (int i = 0; i < colors.Length; i++)
                {
                    var vertex = new UIVertex(); vertices.PopulateUIVertex(ref vertex, i); colors[i] = vertex.color;
                }
                return colors;
            }
        }

        [Test] public void SecondSlotOnlyLibraryReferenceDoesNotSelectOrRevealAWeaponInFirstSlotEditor()
        {
            var gun = Library("library.weapon.gun");
            for (int i = 0; i < 5; i++)
            {
                var saved = fixture.Commands.SaveLibraryEntry(new ComponentLibraryEntry("library.selection-scroll." + i,
                    "Оружие для прокрутки " + i, 1, gun.Snapshot), Guid.NewGuid());
                Assert.That(saved.Success, Is.True, saved.Code);
            }
            int writes = fixture.Store.SaveCalls;
            panel.Reflow(800, 600);
            panel.DraftSession.CancelComponent();
            Assert.That(panel.DraftSession.ApplyLibrary(Library("library.weapon.piercer"), 1), Is.True);
            var source = panel.DraftSession.Draft;
            Assert.That(source.LibraryReferences.Single().SlotIndex, Is.EqualTo(1));
            panel.OpenEntry(source); Click("Sequence step 1");

            Assert.That(panel.DraftSession.ActiveMount, Is.Zero);
            Assert.That(SurfaceColors(Find("Sequence edit library library.weapon.piercer"))
                .SequenceEqual(SurfaceColors(Find("Sequence edit library library.weapon.gun"))), Is.True,
                "A library reference used only by the second mount must remain unselected while editing the first mount.");
            var list = panel.Root.GetComponentsInChildren<ScrollRect>().Single(scroll => scroll.name == "Sequence saved components");
            Assert.That(list.content.rect.height, Is.GreaterThan(list.viewport.rect.height + 1),
                "This regression needs actual library overflow to observe an unrelated reveal.");
            Assert.That(list.verticalNormalizedPosition, Is.EqualTo(1).Within(.001f),
                "The first mount has no saved component to reveal; another mount must not scroll this library.");
            Assert.That(panel.DraftSession.Draft, Is.SameAs(source));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
        }

        [Test] public void FirstSlotSelectionUsesItsSourceIdWithoutUpgradingAHistoricalRevision()
        {
            var gun = Library("library.weapon.gun");
            panel.DraftSession.CancelComponent();
            Assert.That(panel.DraftSession.ApplyLibrary(gun, 0), Is.True);
            Assert.That(panel.DraftSession.ApplyLibrary(Library("library.weapon.piercer"), 1), Is.True);
            var source = panel.DraftSession.Draft;
            var update = fixture.Commands.SaveLibraryEntry(new ComponentLibraryEntry(gun.Id, "Updated library gun", gun.Revision, gun.Snapshot), Guid.NewGuid());
            Assert.That(update.Success, Is.True, update.Code);
            Assert.That(Library(gun.Id).Revision, Is.EqualTo(2));
            int writes = fixture.Store.SaveCalls;
            panel.OpenEntry(source); Click("Sequence step 1");

            Assert.That(SurfaceColors(Find("Sequence edit library library.weapon.gun"))
                .SequenceEqual(SurfaceColors(Find("Sequence edit library library.weapon.piercer"))), Is.False,
                "The first mount's source stays selected even when the library now holds a newer revision.");
            Assert.That(panel.DraftSession.Draft, Is.SameAs(source));
            Assert.That(panel.DraftSession.Draft.LibraryReferences.Single(reference => reference.SlotIndex == 0).Revision, Is.EqualTo(1));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
        }

        [Test] public void InspectingHoveringAndFocusingAnotherPartKeepsTheInstalledPartAndProfile()
        {
            string installed = panel.DraftSession.PreviewRecipe.Tower.Foundation.Hull.Id;
            string inspected = panel.DraftSession.CompatibleParts.First(part => part.Id != installed).Id;
            string before = JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.PreviewEntry));
            string profile = fixture.State(); int writes = fixture.Store.SaveCalls;
            var card = Find("Sequence inspect " + inspected);
            ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            EventSystem.current.SetSelectedGameObject(card.gameObject);
            Click("Sequence inspect " + inspected);
            card = Find("Sequence inspect " + inspected);
            ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);

            Assert.That(Find("Sequence inspect " + installed).transform.Find("Sequence part ownership").GetComponent<Text>().text,
                Is.EqualTo("Установлено"));
            Assert.That(card.transform.Find("Sequence part ownership").GetComponent<Text>().text, Is.Not.EqualTo("Установлено"));
            Assert.That(Find("Sequence install " + inspected), Is.Not.Null, "Inspection opens the explicit install action.");
            Assert.That(JsonUtility.ToJson(new AlbumEntryData(panel.DraftSession.PreviewEntry)), Is.EqualTo(before));
            Assert.That(fixture.State(), Is.EqualTo(profile));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes));
        }
    }
}
#endif
