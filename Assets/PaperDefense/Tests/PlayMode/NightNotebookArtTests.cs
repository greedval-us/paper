#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    /// <summary>Approved notebook art must stay legible and obey the existing mechanical view contract.</summary>
    public sealed class NightNotebookArtTests
    {
        static PartRef P(string id) => new PartRef(id, 1);
        static WeaponRecipe Weapon(string family) => new WeaponRecipe(P("core." + family), P("head." + family),
            P("sight.iron"), P("magazine.standard"), P("trigger.standard"));

        static UIVertex[] Draw(AssemblyVisualComposition composition, float opacity = 1,
            Vector2? at = null, float scale = 1)
        {
            var pen = new MeshPen(null);
            composition.Draw(pen, at ?? Vector2.zero, opacity, uniformScale: scale);
            using (var mesh = new VertexHelper())
            {
                pen.CopyTo(mesh, Vector2.zero, 1);
                var vertices = new UIVertex[mesh.currentVertCount];
                for (int i = 0; i < vertices.Length; i++) mesh.PopulateUIVertex(ref vertices[i], i);
                return vertices;
            }
        }

        static void AssertWarmPen(UIVertex[] vertices, string subject)
        {
            Assert.That(vertices, Is.Not.Empty, subject);
            Assert.That(vertices.Any(vertex => vertex.color.r == 241 && vertex.color.g == 231 &&
                vertex.color.b == 203 && vertex.color.a > 0), Is.True,
                subject + " must use the approved warm ivory pen, not only neutral solid rectangles.");
        }

        [TestCase("frame.standard")][TestCase("frame.heavy")]
        [TestCase("head.gun")][TestCase("head.mortar")]
        [TestCase("magazine.standard")][TestCase("magazine.drum")]
        public void CatalogPartsUseTheApprovedWarmNotebookPen(string id)
        {
            Assert.That(AssemblyVisualComposer.HasVisualMapping(P(id)), Is.True);
            AssertWarmPen(Draw(AssemblyVisualComposer.ComposePart(P(id))), id);
        }

        [TestCase("gun")][TestCase("piercer")][TestCase("mortar")][TestCase("fan")]
        [TestCase("ricochet")][TestCase("stapler")][TestCase("blotter")]
        public void EveryWeaponFamilyUsesTheSameNotebookPen(string family)
            => AssertWarmPen(Draw(AssemblyVisualComposer.ComposeWeapon(Weapon(family))), family);

        [TestCase("head.gun", "head.mortar")]
        [TestCase("magazine.standard", "magazine.drum")]
        [TestCase("frame.standard", "frame.heavy")]
        [TestCase("head.mortar", "head.short")]
        [TestCase("head.fan", "head.wide")]
        [TestCase("head.piercer", "head.long")]
        public void InterchangeableVariantsHaveDifferentVisibleDrawings(string first, string second)
        {
            var a = Draw(AssemblyVisualComposer.ComposePart(P(first))).Select(vertex => vertex.position).ToArray();
            var b = Draw(AssemblyVisualComposer.ComposePart(P(second))).Select(vertex => vertex.position).ToArray();
            Assert.That(a, Is.Not.Empty); Assert.That(b, Is.Not.Empty);
            Assert.That(a.SequenceEqual(b), Is.False,
                first + " and " + second + " must be distinguishable by their actual drawn shape.");
        }

        [Test] public void ThreeAimedRecoilingMountsKeepTheirIdentityAndAllInkInsideTheMechanicalViewBounds()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(
                "Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
            var foundation = new FoundationRecipe(P("frame.battery"), P("hull.standard"), P("armor.none"),
                P("feed.standard"), P("turn.standard"));
            var weapon = Weapon("gun");
            var recipe = new AssemblyRecipe(new TowerRecipe(foundation,
                Enumerable.Range(0, 3).Select(slot => new WeaponMountRecipe(slot, weapon))));
            Assert.That(AssemblyFitRules.Validate(recipe, AssemblyVisualComposer.Fits), Is.Empty);
            var snapshot = new RecipeSnapshot(recipe, catalog.BalanceRevision, catalog.Definitions);
            Assert.That(AssemblyVisualComposer.Fits.TryGetWeapon("core.gun", 1, out var fit), Is.True);
            var rest = AssemblyVisualComposer.Compose(snapshot, new AssemblyVisualPose());
            var posed = AssemblyVisualComposer.Compose(snapshot, new AssemblyVisualPose(new[] {
                new AssemblyMountPose(0, fit.MinRadians * .7f, -fit.RecoilLimit * .5f),
                new AssemblyMountPose(1, fit.MaxRadians * .7f, -fit.RecoilLimit * .25f),
                new AssemblyMountPose(2, 0)
            }));
            Assert.That(posed.Anchors.Select(anchor => anchor.SlotIndex), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(posed.Fragments.Where(fragment => fragment.SlotIndex < 0)
                .Select(fragment => (fragment.PartId, fragment.Origin, fragment.Radians)),
                Is.EqualTo(rest.Fragments.Where(fragment => fragment.SlotIndex < 0)
                    .Select(fragment => (fragment.PartId, fragment.Origin, fragment.Radians))),
                "Aiming and recoil cannot move or replace the fixed foundation parts.");
            foreach (var anchor in posed.Anchors)
            {
                var original = rest.Anchors.Single(item => item.SlotIndex == anchor.SlotIndex);
                Assert.That(anchor.Attach, Is.EqualTo(original.Attach));
                Assert.That(anchor.RotationCenter, Is.EqualTo(original.RotationCenter));
                if (anchor.SlotIndex < 2)
                    Assert.That(Vector2.Distance(anchor.Muzzle, original.Muzzle), Is.GreaterThan(.001f));
            }

            var origin = new Vector2(3, -4); const float scale = 1.7f;
            var low = origin + posed.Bounds.min * scale;
            var high = origin + posed.Bounds.max * scale;
            var vertices = Draw(posed, at: origin, scale: scale);
            Assert.That(vertices, Is.Not.Empty);
            foreach (var vertex in vertices)
            {
                Assert.That(float.IsNaN(vertex.position.x) || float.IsInfinity(vertex.position.x) ||
                    float.IsNaN(vertex.position.y) || float.IsInfinity(vertex.position.y), Is.False);
                Assert.That(vertex.position.x, Is.InRange(low.x - .00001f, high.x + .00001f));
                Assert.That(vertex.position.y, Is.InRange(low.y - .00001f, high.y + .00001f));
            }
        }

        [Test] public void FadingNotebookArtScalesEveryStrokeAlphaWithoutChangingItsGeometry()
        {
            var composition = AssemblyVisualComposer.ComposeWeapon(Weapon("gun"),
                new AssemblyVisualPose(new[] { new AssemblyMountPose(0, -.6f, -.02f) }));
            var opaque = Draw(composition); var faded = Draw(composition, .25f);
            Assert.That(opaque, Is.Not.Empty); Assert.That(faded.Length, Is.EqualTo(opaque.Length));
            Assert.That(faded.Any(vertex => vertex.color.a > 0), Is.True);
            for (int i = 0; i < opaque.Length; i++)
            {
                Assert.That(faded[i].position, Is.EqualTo(opaque[i].position));
                Assert.That(faded[i].color.a, Is.EqualTo(opaque[i].color.a * .25f).Within(1));
                Assert.That(faded[i].color.a, Is.LessThanOrEqualTo(64));
            }
        }
    }
}
#endif
