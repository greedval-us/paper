#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class NightNotebookWorkshopTests
    {
        AssemblyCommandFixture fixture;
        GameObject events;
        CompositeWorkshopPanel panel;

        [SetUp] public void SetUp()
        {
            fixture = new AssemblyCommandFixture(gold: 500, xp: 390);
            events = new GameObject("Night notebook workshop events", typeof(EventSystem));
            var hud = fixture.Root.AddComponent<WorkshopHud>(); hud.Initialize(fixture.Run); panel = hud.Composite;
        }

        [TearDown] public void TearDown() { Object.DestroyImmediate(events); fixture.Dispose(); }

        T Find<T>(string name) where T : Component
            => panel.Root.GetComponentsInChildren<T>(true).Single(item => item.name == name);

        void Click(string name)
        {
            var button = Find<Button>(name); Assert.That(button.IsInteractable(), Is.True, name + ": " + panel.Status);
            button.onClick.Invoke(); Canvas.ForceUpdateCanvases();
        }

        static void AssertNativeDrawing(WeaponAssemblyGraphic drawing)
        {
            Assert.That(drawing.raycastTarget, Is.False);
            Assert.That(drawing.rectTransform.rect.width, Is.GreaterThan(0));
            Assert.That(drawing.rectTransform.rect.height, Is.GreaterThan(0));
            using (var mesh = new VertexHelper())
            {
                typeof(WeaponAssemblyGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(drawing, new object[] { mesh });
                Assert.That(mesh.currentVertCount, Is.GreaterThan(20), drawing.name);
                bool ivory = false;
                var vertex = new UIVertex();
                for (int i = 0; i < mesh.currentVertCount; i++)
                {
                    mesh.PopulateUIVertex(ref vertex, i);
                    ivory |= vertex.color.r == 241 && vertex.color.g == 231 && vertex.color.b == 203 && vertex.color.a > 0;
                    Assert.That(vertex.position.x, Is.InRange(drawing.rectTransform.rect.xMin - .1f, drawing.rectTransform.rect.xMax + .1f));
                    Assert.That(vertex.position.y, Is.InRange(drawing.rectTransform.rect.yMin - .1f, drawing.rectTransform.rect.yMax + .1f));
                }
                Assert.That(ivory, Is.True, drawing.name + " uses the approved native ivory drawing.");
            }
            Assert.That(drawing.transform.parent.GetComponentsInChildren<NightNotebookPaperGraphic>(true), Is.Not.Empty);
        }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void FoundationWeaponAndInspectedPartsUseNativeArtInsideExistingScrollableRegions(int width, int height)
        {
            panel.Reflow(width, height); Canvas.ForceUpdateCanvases();
            var foundation = Find<WeaponAssemblyGraphic>("Sequence component drawing");
            AssertNativeDrawing(foundation);
            Assert.That(foundation.GetComponentInParent<ScrollRect>(true).name, Is.EqualTo("Sequence component metrics scroll"));
            var part = Find<WeaponAssemblyGraphic>("Sequence part drawing");
            Assert.That(part.Part.Value.Id, Is.EqualTo("hull.standard")); AssertNativeDrawing(part);
            Click("Sequence next");
            AssertNativeDrawing(Find<WeaponAssemblyGraphic>("Sequence component drawing"));
            AssertNativeDrawing(Find<WeaponAssemblyGraphic>("Sequence part drawing"));
            Assert.That(Find<WeaponAssemblyGraphic>("Sequence part drawing").Part.Value.Id, Is.EqualTo("magazine.standard"));
            Assert.That(Find<Button>("Sequence install magazine.standard").GetComponentInParent<ScrollRect>(), Is.Null);
        }

        [Test] public void AssemblyAndReviewDrawTheLiveRecipeAndRefreshKeepsOneDrawingPerWell()
        {
            Click("Sequence next"); Click("Sequence next");
            var drawing = Find<WeaponAssemblyGraphic>("Sequence tower drawing");
            Assert.That(drawing.Recipe.Recipe, Is.SameAs(panel.DraftSession.PreviewRecipe)); AssertNativeDrawing(drawing);
            panel.Refresh(); Canvas.ForceUpdateCanvases();
            AssertNativeDrawing(Find<WeaponAssemblyGraphic>("Sequence tower drawing"));
            Click("Sequence next");
            AssertNativeDrawing(Find<WeaponAssemblyGraphic>("Sequence tower drawing"));
            Assert.That(Find<Button>("Sequence save").IsInteractable(), Is.True);
        }

        [TestCase(AssemblyStructureFamily.Wall)] [TestCase(AssemblyStructureFamily.Slow)]
        [TestCase(AssemblyStructureFamily.Repair)] [TestCase(AssemblyStructureFamily.Generator)]
        public void SupportFamilyEditorAndReviewShowTheActualComposedStructure(AssemblyStructureFamily family)
        {
            Click("Composite route structure"); Click("Structure family " + family);
            var drawing = Find<WeaponAssemblyGraphic>("Structure drawing");
            Assert.That(drawing.Recipe.Recipe.Structure.Family, Is.EqualTo(family)); AssertNativeDrawing(drawing);
            Click("Structure next"); AssertNativeDrawing(Find<WeaponAssemblyGraphic>("Structure drawing"));
            AssertNativeDrawing(Find<WeaponAssemblyGraphic>("Structure part drawing"));
            Click("Structure next"); AssertNativeDrawing(Find<WeaponAssemblyGraphic>("Structure drawing"));
            Assert.That(Find<Button>("Structure save").IsInteractable(), Is.True);
        }

        [Test] public void InvalidMountDraftDrawsItsFoundationAndRemainsEditableUntilTheExtraWeaponIsRemoved()
        {
            var tower = fixture.Gun().Snapshot.Recipe.Tower;
            var frame = fixture.Catalog.Definitions.First(part => part.Category == ComponentCategory.Frame && part.Get(AssemblyStat.MountCount, ModifierOperation.Set) == 1);
            Assert.That(fixture.Commands.PurchasePart(frame.Id, System.Guid.NewGuid()).Success, Is.True);
            var foundation = new FoundationRecipe(new PartRef(frame.Id, frame.Revision), tower.Foundation.Hull, tower.Foundation.Armor, tower.Foundation.Feed, tower.Foundation.Turn);
            var recipe = new AssemblyRecipe(new TowerRecipe(foundation, new[] { new WeaponMountRecipe(0, tower.Mounts[0].Weapon), new WeaponMountRecipe(1, tower.Mounts[0].Weapon) }));
            Assert.That(AssemblyFitRules.Validate(recipe, AssemblyVisualComposer.Fits), Is.Not.Empty);
            Assert.DoesNotThrow(() => panel.OpenEntry(new AlbumEntry("night.invalid.mount", "Лишнее орудие", 1,
                new RecipeSnapshot(recipe, fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions))));
            Click("Sequence step 2");
            var drawing = Find<WeaponAssemblyGraphic>("Sequence tower drawing");
            Assert.That(drawing.Recipe, Is.Null, "An invalid whole recipe must not reach the fit-validating compositor.");
            AssertNativeDrawing(drawing);
            Click("Sequence mount 1"); Click("Sequence remove weapon");
            Assert.That(panel.DraftSession.PreviewRecipe.Tower.Mounts.Select(item => item.SlotIndex), Is.EqualTo(new[] { 0 }));
            AssertNativeDrawing(Find<WeaponAssemblyGraphic>("Sequence tower drawing"));
        }
    }
}
#endif
