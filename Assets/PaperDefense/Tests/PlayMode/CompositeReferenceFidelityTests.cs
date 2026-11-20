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
    public sealed class CompositeReferenceFidelityTests
    {
        AssemblyCommandFixture fixture;
        GameObject canvas;
        CompositeWorkshopPanel panel;
        [SetUp] public void SetUp()
        {
            fixture = new AssemblyCommandFixture(500, 390);
            canvas = new GameObject("Reference composition", typeof(RectTransform), typeof(Canvas));
            panel = new CompositeWorkshopPanel(canvas.transform, fixture.Run, null, () => { }, sequentialFlow: true);
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(canvas); fixture.Dispose(); }
        Text Label(string name) => panel.Root.GetComponentsInChildren<Text>().Single(label => label.name == name);
        Button Button(string name) => panel.Root.GetComponentsInChildren<Button>().Single(button => button.name == name);
        void Click(string name)
        {
            var button = Button(name); Assert.That(button.IsInteractable(), Is.True, name + ": " + panel.Status);
            button.onClick.Invoke(); Canvas.ForceUpdateCanvases();
        }

        [TestCase(1024, 768)][TestCase(1600, 900)]
        public void FoundationInspectionShowsTheSelectedCurrentPartAndItsActualMaterial(float width, float height)
        {
            panel.Reflow(width, height);
            Click("Sequence category Frame");
            var frame = panel.DraftSession.PreviewRecipe.Tower.Foundation.Frame;
            AssertPartDrawing(frame, ComponentCategory.Frame);
            Assert.That(Button("Sequence inspect " + frame.Id).GetComponent<UiInteractionFeedback>().SelectionAmount, Is.EqualTo(1));
            Assert.That(Label("Sequence specification").text, Does.Contain("Мест для орудий"));
            Assert.That(Label("Sequence specification").text, Does.Contain("Допустимый вес"));
            Assert.That(Label("Sequence specification").color, Is.EqualTo(FieldMenuSkin.Ink));
            Assert.That(fixture.Store.SaveCalls, Is.Zero, "Inspecting the current component does not publish a draft.");
        }

        [TestCase(1024, 768)][TestCase(1600, 900)]
        public void WeaponFamiliesAndCategorySocketsKeepTheInspectedArtworkBoundToTheRecipe(float width, float height)
        {
            panel.Reflow(width, height); Click("Sequence next");
            int writes = fixture.Store.SaveCalls;
            var families = panel.Root.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Sequence family ")).ToArray();
            Assert.That(families.Length, Is.EqualTo(fixture.Catalog.Definitions.Where(part => part.Category == ComponentCategory.Mechanism)
                .Select(part => part.Id).Distinct().Count()));
            foreach (var category in new[] { ComponentCategory.Magazine, ComponentCategory.Head, ComponentCategory.Sight, ComponentCategory.Mount })
            {
                Click("Sequence category " + category);
                Assert.That(panel.DraftSession.IsComponentOpen, Is.True);
                Assert.That(panel.DraftSession.ActiveCategory, Is.EqualTo(category));
                var drawing = panel.Root.GetComponentsInChildren<WeaponAssemblyGraphic>().Single(art => art.name == "Sequence part drawing");
                Assert.That(drawing.Part.HasValue, Is.True);
                AssertPartDrawing(drawing.Part.Value, category);
            }
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes), "Opening category controls does not publish another component.");
        }

        [TestCase(1024, 768)][TestCase(1600, 900)]
        public void ReviewShowsTheActualRecipeTotalsAndSharedNativeIllustration(float width, float height)
        {
            panel.Reflow(width, height); Click("Sequence next"); Click("Sequence next"); Click("Sequence next");
            var actual = panel.DraftSession.Resolved;
            var totals = Label("Sequence totals");
            Assert.That(totals.text, Does.Contain("Цена " + actual.Cost + " штр. · " + actual.BuildSeconds.ToString("0.##") + " с"));
            Assert.That(totals.text, Does.Contain("вес " + actual.Load + "/" + actual.Capacity));
            Assert.That(totals.text, Does.Contain("HP " + actual.Health.ToString("0.#")));
            Assert.That(totals.text, Does.Contain("Урон/с " + actual.Dps.ToString("0.##")));
            Assert.That(totals.color, Is.EqualTo(FieldMenuSkin.Ink));
            Assert.That(totals.preferredHeight, Is.LessThanOrEqualTo(totals.rectTransform.rect.height + 1));
            var drawing = panel.Root.GetComponentsInChildren<WeaponAssemblyGraphic>().Single(art => art.name == "Sequence tower drawing");
            Assert.That(drawing.Recipe.Recipe, Is.EqualTo(panel.DraftSession.PreviewRecipe));
            AssertNotebookSurface(drawing);
            AssertVisibleMaterial(drawing);
            Assert.That(Button("Composite trial").IsInteractable(), Is.EqualTo(panel.DraftSession.CanTrial));
        }

        void AssertPartDrawing(PartRef expected, ComponentCategory category)
        {
            var drawing = panel.Root.GetComponentsInChildren<WeaponAssemblyGraphic>().Single(art => art.name == "Sequence part drawing");
            Assert.That(drawing.Part, Is.EqualTo(expected));
            Assert.That(fixture.Catalog.Definitions.Single(part => part.Id == expected.Id && part.Revision == expected.Revision).Category, Is.EqualTo(category));
            AssertNotebookSurface(drawing);
            AssertVisibleMaterial(drawing);
        }

        static void AssertNotebookSurface(WeaponAssemblyGraphic drawing)
        {
            var source = drawing.rectTransform;
            // The sequential screen uses FieldMenuTheme's shared preview factory. Its
            // backing is a sibling sheet, rather than a StandardIllustrationSurface owner.
            var sheets = source.parent.GetComponentsInChildren<NightNotebookPaperGraphic>(true)
                .Where(sheet => sheet.transform.parent == source.parent &&
                    sheet.rectTransform.anchoredPosition3D == source.anchoredPosition3D &&
                    sheet.rectTransform.sizeDelta == source.sizeDelta).ToArray();
            Assert.That(sheets.Length, Is.EqualTo(1), drawing.name + " needs one matching dark notebook sheet.");
            var paper = sheets[0]; var target = paper.rectTransform;
            Assert.That(paper.isActiveAndEnabled, Is.True, "The actual backing must be rendered.");
            Assert.That(paper.raycastTarget, Is.False, "The illustration backing cannot intercept editor input.");
            Assert.That(target.GetSiblingIndex(), Is.LessThan(source.GetSiblingIndex()), "The sheet must sit below its artwork.");
            Assert.That(target.anchorMin, Is.EqualTo(source.anchorMin));
            Assert.That(target.anchorMax, Is.EqualTo(source.anchorMax));
            Assert.That(target.pivot, Is.EqualTo(source.pivot));
            Assert.That(target.localRotation, Is.EqualTo(source.localRotation));
            Assert.That(target.localScale, Is.EqualTo(source.localScale));
            using (var vertices = new VertexHelper())
            {
                typeof(NightNotebookPaperGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(paper, new object[] { vertices });
                Assert.That(vertices.currentVertCount, Is.GreaterThanOrEqualTo(4));
                for (int i = 0; i < 4; i++)
                {
                    var vertex = new UIVertex(); vertices.PopulateUIVertex(ref vertex, i);
                    Assert.That(vertex.color, Is.EqualTo((Color32)NightNotebookArt.Paper), "The preview needs opaque dark paper under the sage material.");
                }
            }
        }

        static void AssertVisibleMaterial(WeaponAssemblyGraphic drawing)
        {
            using (var vertices = new VertexHelper())
            {
                typeof(WeaponAssemblyGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(drawing, new object[] { vertices });
                Assert.That(vertices.currentVertCount, Is.GreaterThan(0));
                bool body = false, contour = false;
                Color32 material = NightNotebookArt.Body, ink = NightNotebookArt.Ink;
                for (int i = 0; i < vertices.currentVertCount; i++)
                {
                    var vertex = new UIVertex(); vertices.PopulateUIVertex(ref vertex, i);
                    body |= vertex.color.r == material.r && vertex.color.g == material.g && vertex.color.b == material.b && vertex.color.a >= 180;
                    contour |= vertex.color.r == ink.r && vertex.color.g == ink.g && vertex.color.b == ink.b && vertex.color.a >= 180;
                }
                Assert.That(body, Is.True, "The inspected current recipe must retain solid sage material.");
                Assert.That(contour, Is.True, "The inspected current recipe must retain its warm ivory silhouette.");
            }
        }
    }
}
#endif
