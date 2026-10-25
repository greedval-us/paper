using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class NotebookWeaponPreviewTests
    {
        GameObject owner;
        NotebookArtGraphic graphic;

        [SetUp] public void SetUp()
        {
            owner = new GameObject("Notebook weapon preview regression", typeof(RectTransform));
            graphic = owner.AddComponent<NotebookArtGraphic>();
            graphic.rectTransform.sizeDelta = new Vector2(320, 180);
        }

        [TearDown] public void TearDown() { Object.DestroyImmediate(owner); }

        [TestCase("gun")]
        [TestCase("piercer")]
        [TestCase("mortar")]
        [TestCase("fan")]
        [TestCase("ricochet")]
        [TestCase("stapler")]
        [TestCase("blotter")]
        public void KnownWeaponIllustrationDrawsEveryModernWeaponCategoryInsteadOfLegacyAtlasQuad(string family)
        {
            graphic.Bind("ui.weapon." + family);
            Assert.That(graphic.ShapeId, Is.EqualTo("ui.weapon." + family));
            AssertModernMesh(family);
        }

        [Test] public void RebindingFromOneFamilyToAnotherReplacesEveryPreviousFragment()
        {
            graphic.Bind("ui.weapon.gun");
            using (var first = Populate()) Assert.That(first.currentVertCount, Is.GreaterThan(4));
            graphic.Bind("ui.weapon.stapler");
            AssertModernMesh("stapler");
        }

        [Test] public void RebindingToNonWeaponArtOrUnknownShapeClearsTheComposedPreview()
        {
            graphic.Bind("ui.weapon.mortar");
            graphic.Bind("terrain.pine");
            AssertLegacyMesh("terrain.pine");
            graphic.Bind("ui.weapon.gun");
            graphic.Bind("ui.weapon.unregistered");
            using (var mesh = Populate()) Assert.That(mesh.currentVertCount, Is.Zero,
                "An unknown ID must not retain the previously composed weapon.");
        }

        [Test] public void ExplicitAnimationRetainsTheOriginalClipFramesAfterAComposedPreview()
        {
            const string clipId = "enemy.doodle.sketch.walk";
            var clip = NotebookVisualThemeAsset.Default.FindClip(clipId);
            Assert.That(clip, Is.Not.Null, "The production animation is part of this compatibility regression.");
            graphic.Bind("ui.weapon.fan");
            graphic.Bind("ui.weapon.fan", clipId);
            graphic.SetTime(clip.Duration * .6f);
            Assert.That(graphic.ClipId, Is.EqualTo(clipId));
            AssertLegacyMesh(clip.Sample(graphic.Seconds).ShapeId);
            graphic.Bind("ui.weapon.ricochet");
            AssertModernMesh("ricochet");
        }

        void AssertModernMesh(string family)
        {
            var recipe = new WeaponRecipe(Part("core." + family), Part("head." + family),
                Part("sight.iron"), Part("magazine.standard"), Part("trigger.standard"));
            var composition = AssemblyVisualComposer.ComposeWeapon(recipe);
            var parts = composition.Fragments.Select(fragment => fragment.PartId).Distinct().ToArray();
            Assert.That(parts, Is.EquivalentTo(new[] { "core." + family, "head." + family,
                "sight.iron", "magazine.standard", "trigger.standard", "turn.standard" }),
                "The expected illustration includes all five actual weapon categories and their canonical fixed turn support.");
            var pen = new MeshPen(null);
            composition.Draw(pen, Vector2.zero);
            using (var expected = new VertexHelper())
            using (var actual = Populate())
            {
                pen.CopyTo(expected, Vector2.zero, 1);
                Assert.That(expected.currentVertCount, Is.GreaterThan(4));
                AssertSameUvStream(actual, expected);
                var vertex = new UIVertex();
                for (int i = 0; i < actual.currentVertCount; i++)
                {
                    actual.PopulateUIVertex(ref vertex, i);
                    Assert.That(graphic.rectTransform.rect.Contains(vertex.position), Is.True,
                        "Every real component must remain inside the existing card rectangle.");
                }
            }
        }

        void AssertLegacyMesh(string shapeId)
        {
            var shape = NotebookVisualThemeAsset.Default.FindShape(shapeId);
            Assert.That(shape, Is.Not.Null, shapeId);
            using (var expected = new VertexHelper())
            using (var actual = Populate())
            {
                Assert.That(NotebookArtRenderer.DrawUI(expected, shape, Vector2.zero, 1), Is.True);
                AssertSameUvStream(actual, expected);
            }
        }

        static void AssertSameUvStream(VertexHelper actual, VertexHelper expected)
        {
            Assert.That(actual.currentVertCount, Is.EqualTo(expected.currentVertCount),
                "The actual rendered mesh must contain the complete ordered fragment mesh, not the old single atlas quad.");
            Assert.That(actual.currentIndexCount, Is.EqualTo(expected.currentIndexCount));
            var a = new UIVertex(); var b = new UIVertex();
            for (int i = 0; i < expected.currentVertCount; i++)
            {
                actual.PopulateUIVertex(ref a, i); expected.PopulateUIVertex(ref b, i);
                Assert.That(Vector2.Distance(a.uv0, b.uv0), Is.LessThan(1e-6f),
                    "Native crop UV differs at vertex " + i);
            }
        }

        VertexHelper Populate()
        {
            var result = new VertexHelper();
            typeof(NotebookArtGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { result });
            return result;
        }

        static PartRef Part(string id) => new PartRef(id, 1);
    }
}
