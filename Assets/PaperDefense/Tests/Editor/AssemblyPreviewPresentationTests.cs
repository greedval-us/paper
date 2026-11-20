using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AssemblyPreviewPresentationTests
    {
        const float DisplayAngle = -Mathf.PI / 4;

        static ComponentCatalog Catalog() => AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(
            "Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
        static PartRef P(string id) => new PartRef(id, 1);
        static RecipeSnapshot Snapshot(string family, bool battery = false)
        {
            var catalog = Catalog();
            var foundation = new FoundationRecipe(P(battery ? "frame.battery" : "frame.standard"),
                P("hull.standard"), P("armor.none"), P("feed.standard"), P("turn.standard"));
            var weapon = new WeaponRecipe(P("core." + family), P("head." + family), P("sight.iron"),
                P("magazine.standard"), P("trigger.standard"));
            var recipe = new AssemblyRecipe(new TowerRecipe(foundation, Enumerable.Range(0, battery ? 3 : 1)
                .Select(slot => new WeaponMountRecipe(slot, weapon))));
            Assert.That(AssemblyFitRules.Validate(recipe, AssemblyVisualComposer.Fits), Is.Empty);
            return new RecipeSnapshot(recipe, catalog.BalanceRevision, catalog.Definitions);
        }

        [TestCase("gun")][TestCase("piercer")][TestCase("mortar")][TestCase("fan")]
        [TestCase("ricochet")][TestCase("stapler")][TestCase("blotter")]
        public void DefaultStaticPreview_UsesForwardRightPoseAndTheActualSharedMuzzle(string family)
        {
            var snapshot = Snapshot(family);
            var expectedPose = new AssemblyVisualPose(new[] { new AssemblyMountPose(0, DisplayAngle) });
            var expected = AssemblyVisualComposer.Compose(snapshot, expectedPose);
            var rest = AssemblyVisualComposer.Compose(snapshot, new AssemblyVisualPose());
            var go = new GameObject("Static assembly preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(WeaponAssemblyGraphic));
            try
            {
                var graphic = go.GetComponent<WeaponAssemblyGraphic>(); graphic.SetRecipe(snapshot);
                Assert.That(graphic.Pose.Mounts.Count, Is.EqualTo(1), "A static card must have an explicit shared display pose.");
                Assert.That(graphic.Pose.Mounts[0].Radians, Is.EqualTo(DisplayAngle).Within(.000001f));
                Assert.That(graphic.Pose.Mounts[0].Recoil, Is.Zero);
                var actual = AssemblyVisualComposer.Compose(snapshot, graphic.Pose);
                SameFragments(actual, expected);
                Assert.That(Vector2.Distance(actual.Anchors.Single().Muzzle, expected.Anchors.Single().Muzzle), Is.LessThan(.000001f));
                Assert.That(actual.Anchors.Single().Muzzle.x, Is.GreaterThan(actual.Anchors.Single().RotationCenter.x));
                Assert.That(actual.Anchors.Single().Muzzle.y, Is.GreaterThan(actual.Anchors.Single().RotationCenter.y));
                foreach (var fixedPart in rest.Fragments.Where(fragment => fragment.SlotIndex < 0))
                {
                    var shown = actual.Fragments.Single(fragment => Key(fragment) == Key(fixedPart));
                    Assert.That(shown.Origin, Is.EqualTo(fixedPart.Origin), fixedPart.PartId);
                    Assert.That(shown.Radians, Is.EqualTo(fixedPart.Radians), fixedPart.PartId);
                }
                SameFragments(AssemblyVisualComposer.Compose(snapshot, new AssemblyVisualPose()), rest);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase("gun")][TestCase("piercer")][TestCase("mortar")][TestCase("fan")]
        [TestCase("ricochet")][TestCase("stapler")][TestCase("blotter")]
        public void StandaloneWeaponPose_UsesEveryRealRecipeFragmentAndMuzzle(string family)
        {
            var snapshot = Snapshot(family); var weapon = snapshot.Recipe.Tower.Mounts.Single().Weapon;
            var pose = new AssemblyVisualPose(new[] { new AssemblyMountPose(0, DisplayAngle) });
            var actual = AssemblyVisualComposer.ComposeWeapon(weapon, pose);
            var full = AssemblyVisualComposer.Compose(snapshot, pose); var anchor = full.Anchors.Single();
            Assert.That(actual.Fragments.Count, Is.EqualTo(full.Fragments.Count(fragment => fragment.SlotIndex == 0)));
            foreach (var fragment in actual.Fragments)
            {
                var expected = full.Fragments.Single(item => Key(item) == Key(fragment));
                Assert.That(fragment.Scale, Is.EqualTo(expected.Scale)); Assert.That(fragment.Radians, Is.EqualTo(expected.Radians));
                Assert.That(Vector2.Distance(fragment.Origin, expected.Origin - anchor.RotationCenter), Is.LessThan(.000001f));
            }
            Assert.That(Vector2.Distance(actual.Anchors.Single().Muzzle, anchor.Muzzle - anchor.RotationCenter), Is.LessThan(.000001f));
            Assert.That(actual.Anchors.Single().RotationCenter, Is.EqualTo(Vector2.zero));
        }

        [Test] public void ThreeBatteryWeapons_ReceiveTheSameStaticDisplayPose()
        {
            var snapshot = Snapshot("gun", true);
            var go = new GameObject("Battery preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(WeaponAssemblyGraphic));
            try
            {
                var graphic = go.GetComponent<WeaponAssemblyGraphic>(); graphic.SetRecipe(snapshot);
                Assert.That(graphic.Pose.Mounts.Select(mount => mount.SlotIndex), Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(graphic.Pose.Mounts.All(mount => Mathf.Abs(mount.Radians - DisplayAngle) < .000001f && mount.Recoil == 0), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test] public void ExplicitPose_RemainsExactIncludingRecoil()
        {
            var snapshot = Snapshot("gun");
            var pose = new AssemblyVisualPose(new[] { new AssemblyMountPose(0, .81f, -.04f) });
            var go = new GameObject("Recorded pose", typeof(RectTransform), typeof(CanvasRenderer), typeof(WeaponAssemblyGraphic));
            try
            {
                var graphic = go.GetComponent<WeaponAssemblyGraphic>(); graphic.SetRecipe(snapshot, pose);
                Assert.That(graphic.Pose, Is.SameAs(pose));
                SameFragments(AssemblyVisualComposer.Compose(snapshot, graphic.Pose), AssemblyVisualComposer.Compose(snapshot, pose));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test] public void StaticFit_UsesTheActualPenBoundsOfTheWholeTree()
        {
            var snapshot = Snapshot("gun");
            var pose = new AssemblyVisualPose(new[] { new AssemblyMountPose(0, DisplayAngle) });
            var composition = AssemblyVisualComposer.Compose(snapshot, pose);
            Rect expected = DrawnBounds(composition);
            Assert.That(expected.width, Is.GreaterThan(0));
            Assert.That(expected.height, Is.GreaterThan(0));
            var go = new GameObject("Tight full tree", typeof(RectTransform), typeof(CanvasRenderer), typeof(WeaponAssemblyGraphic));
            try
            {
                var graphic = go.GetComponent<WeaponAssemblyGraphic>(); graphic.SetRecipe(snapshot, pose);
                var field = typeof(WeaponAssemblyGraphic).GetField("bounds", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                Rect actual = (Rect)field.GetValue(graphic);
                Assert.That(Vector2.Distance(actual.min, expected.min), Is.LessThan(.000001f));
                Assert.That(Vector2.Distance(actual.max, expected.max), Is.LessThan(.000001f));
                SameFragments(AssemblyVisualComposer.Compose(snapshot, graphic.Pose), composition);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static Rect DrawnBounds(AssemblyVisualComposition composition)
        {
            var pen = new MeshPen(null);
            composition.Draw(pen, Vector2.zero);
            Assert.That(pen.TryGetBounds(out var bounds), Is.True);
            return bounds;
        }
        static string Key(AssemblyVisualFragment fragment) => fragment.PartId + "/" + fragment.SpriteId + "/" + fragment.SlotIndex + "/" + fragment.Layer;
        static void SameFragments(AssemblyVisualComposition actual, AssemblyVisualComposition expected)
        {
            Assert.That(actual.Fragments.Count, Is.EqualTo(expected.Fragments.Count));
            for (int i = 0; i < actual.Fragments.Count; i++)
            {
                var a = actual.Fragments[i]; var b = expected.Fragments[i]; Assert.That(Key(a), Is.EqualTo(Key(b)));
                Assert.That(a.Scale, Is.EqualTo(b.Scale)); Assert.That(a.Radians, Is.EqualTo(b.Radians));
                Assert.That(Vector2.Distance(a.Origin, b.Origin), Is.LessThan(.000001f));
            }
        }
    }
}
