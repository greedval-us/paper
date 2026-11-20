#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class AdaptiveAlbumUiTests
    {
        GameObject canvas;

        AlbumPanel Panel(AssemblyCommandFixture fixture, int width, int height)
        {
            canvas = new GameObject("Adaptive album", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
            var panel = new AlbumPanel(canvas.transform, fixture.Run, TextResourcesAsset.Default, _ => { }, () => { });
            panel.Reflow(width, height);
            return panel;
        }

        T Named<T>(string name) where T : Component
            => canvas.GetComponentsInChildren<T>().Single(component => component.name == name);

        static Rect Bounds(RectTransform target, RectTransform parent)
        {
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            var first = parent.InverseTransformPoint(corners[1]);
            var last = parent.InverseTransformPoint(corners[3]);
            return new Rect(first.x - parent.rect.xMin, parent.rect.yMax - first.y, last.x - first.x, first.y - last.y);
        }

        [TearDown] public void TearDown() { if (canvas != null) Object.DestroyImmediate(canvas); }

        [TestCase(800, 600)] [TestCase(1024, 768)] [TestCase(1280, 720)] [TestCase(1600, 900)]
        public void SelectedEntrySummaryKeepsItsNameStatisticsAndActionsSeparate(int width, int height)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var panel = Panel(fixture, width, height); panel.SelectEntry("album.guard"); Canvas.ForceUpdateCanvases();
                var inspector = Named<RectTransform>("Album inspector");
                var name = Bounds(Named<Text>("Album selected name").rectTransform, inspector);
                var health = Bounds(Named<Text>("Album metric label Health").rectTransform, inspector);
                var cost = Bounds(Named<Text>("Album metric label Cost").rectTransform, inspector);
                var edit = Bounds(Named<Button>("Album edit").GetComponent<RectTransform>(), inspector);
                Assert.That(health.yMin, Is.GreaterThanOrEqualTo(name.yMax + 2), "Name overlaps the first statistic");
                Assert.That(edit.yMin, Is.GreaterThanOrEqualTo(cost.yMax + 2), "Statistics overlap the edit action");
                foreach (var control in inspector.GetComponentsInChildren<Button>())
                {
                    var box = Bounds(control.GetComponent<RectTransform>(), inspector);
                    Assert.That(box.xMin, Is.GreaterThanOrEqualTo(0));
                    Assert.That(box.xMax, Is.LessThanOrEqualTo(inspector.rect.width + 1));
                }
            }
        }

        [Test] public void CatalogUsesReadableCardWidthsInsteadOfFourColumnsAt1024()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Panel(fixture, 1024, 768); Canvas.ForceUpdateCanvases();
                var cards = canvas.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Album choose ", StringComparison.Ordinal));
                Assert.That(cards.All(card => card.GetComponent<RectTransform>().rect.width >= 190), Is.True,
                    "Cards must retain a readable minimum width when a sidebar is present");
            }
        }

        [Test] public void GrowingCatalogKeepsItsViewportAndScrollAfterSelectionAndRefresh()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390, data => {
                var source = data.Entries[0].CreateSnapshot();
                data.Entries = Enumerable.Range(0, 40).Select(index => new AlbumEntryData(
                    new AlbumEntry("adaptive." + index, "Страница " + index, 1, source.Snapshot))).ToArray();
                data.Album = Array.Empty<BlueprintData>(); data.Loadout = new string[8];
            }))
            {
                var panel = Panel(fixture, 800, 600); Canvas.ForceUpdateCanvases();
                var scroll = Named<ScrollRect>("Album catalogue");
                var viewport = Bounds(scroll.viewport, panel.Root);
                Assert.That(scroll.content.rect.height, Is.GreaterThan(viewport.height * 3));
                scroll.verticalNormalizedPosition = .35f;
                Named<Button>("Album choose adaptive.20").onClick.Invoke(); Canvas.ForceUpdateCanvases();
                scroll = Named<ScrollRect>("Album catalogue");
                Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(.35f).Within(.005f));
                panel.Refresh(); Canvas.ForceUpdateCanvases();
                scroll = Named<ScrollRect>("Album catalogue");
                Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(.35f).Within(.005f));
                Assert.That(Bounds(scroll.viewport, panel.Root), Is.EqualTo(viewport));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void AlbumRespectsAViewportBelowThePreviousMinimum()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var panel = Panel(fixture, 640, 480); Canvas.ForceUpdateCanvases();
                Assert.That(panel.Root.rect.width, Is.EqualTo(640));
                Assert.That(panel.Root.rect.height, Is.EqualTo(480));
                foreach (string name in new[] { "Album new", "Album close", "Album restore" })
                {
                    var box = Bounds(Named<Button>(name).GetComponent<RectTransform>(), panel.Root);
                    Assert.That(box.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(box.yMin, Is.GreaterThanOrEqualTo(0));
                    Assert.That(box.xMax, Is.LessThanOrEqualTo(640)); Assert.That(box.yMax, Is.LessThanOrEqualTo(480));
                }
            }
        }

        [Test] public void APreviouslyFittingCatalogueStartsAtTheTopWhenTheWindowBecomesCompact()
        {
            using (var fixture = new AssemblyCommandFixture(configure: AddPagesForReflow))
            {
                var panel = Panel(fixture, 1600, 900); Canvas.ForceUpdateCanvases();
                Named<ScrollRect>("Album catalogue").verticalNormalizedPosition = 0;
                Assert.That(panel.CatalogScrollPosition, Is.EqualTo(1));
                panel.Reflow(800, 600); Canvas.ForceUpdateCanvases();
                Assert.That(Named<ScrollRect>("Album catalogue").verticalNormalizedPosition, Is.EqualTo(1).Within(.005f));
            }
        }

        [Test] public void CatalogThatFitsAWidePageStartsAtTheTopAfterCompactReflow()
        {
            using (var fixture = new AssemblyCommandFixture(configure: AddPagesForReflow))
            {
                var panel = Panel(fixture, 1600, 900); Canvas.ForceUpdateCanvases();
                var scroll = Named<ScrollRect>("Album catalogue");
                Assert.That(scroll.content.rect.height, Is.LessThanOrEqualTo(scroll.viewport.rect.height + 1));
                Assert.That(panel.CatalogScrollPosition, Is.EqualTo(1), "A fitting list has no bottom position to remember");
                panel.Reflow(800, 600); Canvas.ForceUpdateCanvases();
                scroll = Named<ScrollRect>("Album catalogue");
                Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
                Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1).Within(.005f));
                var firstCard = Bounds(Named<Button>("Album choose album.guard").GetComponent<RectTransform>(), scroll.viewport);
                Assert.That(firstCard.yMin, Is.GreaterThanOrEqualTo(-1));
                Assert.That(firstCard.yMax, Is.LessThanOrEqualTo(scroll.viewport.rect.height + 1));
            }
        }

        static void AddPagesForReflow(WorkshopProfileData data)
        {
            var source = data.Entries[0].CreateSnapshot();
            data.Entries = data.Entries.Concat(Enumerable.Range(0, 8).Select(index => new AlbumEntryData(
                new AlbumEntry("reflow." + index, "Дополнительная страница " + index, 1, source.Snapshot)))).ToArray();
        }

        [Test] public void SelectedEntryOffersClickAccessibleFullDetails()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var panel = Panel(fixture, 800, 600); panel.SelectEntry("album.guard");
                var details = canvas.GetComponentsInChildren<Button>().SingleOrDefault(button => button.name == "Album details");
                Assert.That(details, Is.Not.Null, "Full details must be available by click");
                details.onClick.Invoke();
                Assert.That(canvas.GetComponentsInChildren<ScrollRect>().Length, Is.GreaterThan(1));
                var close = canvas.GetComponentsInChildren<Button>().SingleOrDefault(button => button.name == "Close context help");
                Assert.That(close, Is.Not.Null); close.onClick.Invoke();
                Assert.That(panel.SelectedId, Is.EqualTo("album.guard"));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }
    }
}
#endif
