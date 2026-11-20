#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class PreciseInkUiScaleTests
    {
        static PartRef P(string id) => new PartRef(id, 1);
        static WeaponRecipe Gun() => new WeaponRecipe(P("core.gun"), P("head.gun"), P("sight.scope"),
            P("magazine.drum"), P("trigger.standard"), P("mount.standard"));

        [Test] public void CompactWeaponPreviewKeepsItsContourWithLessFineInk()
        {
            var go = new GameObject("Precise ink UI scale probe", typeof(RectTransform), typeof(WeaponAssemblyGraphic));
            try
            {
                var graphic = go.GetComponent<WeaponAssemblyGraphic>();
                graphic.SetComposition(AssemblyVisualComposer.ComposeWeapon(Gun()));
                int large = GraphicVertices(graphic, 600), small = GraphicVertices(graphic, 36);
                Assert.That(small, Is.GreaterThan(0));
                Assert.That(small, Is.LessThan(large), "Compact UI must omit fine ink rather than shrinking every hatch into noise.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase("gun")] [TestCase("piercer")] [TestCase("mortar")]
        [TestCase("fan")] [TestCase("ricochet")] [TestCase("stapler")] [TestCase("blotter")]
        public void FamilyThumbnailDropsFineInkAndRestoresItWhenEnlarged(string family)
        {
            var go = new GameObject("Family thumbnail detail probe", typeof(RectTransform), typeof(NotebookArtGraphic));
            try
            {
                var graphic = go.GetComponent<NotebookArtGraphic>();
                graphic.Bind("ui.weapon." + family);
                int large = GraphicVertices(graphic, 600), small = GraphicVertices(graphic, 36);
                Assert.That(small, Is.GreaterThan(0));
                Assert.That(small, Is.LessThan(large), "Family thumbnails must use the same detail reduction as recipe previews.");
                Assert.That(GraphicVertices(graphic, 600), Is.EqualTo(large),
                    "Resizing the same card back up must restore its full authored detail.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        static int GraphicVertices(MaskableGraphic graphic, float size)
        {
            graphic.rectTransform.sizeDelta = new Vector2(size, size);
            using (var target = new VertexHelper())
            {
                graphic.GetType().GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { target });
                return target.currentVertCount;
            }
        }

        [Test] public void CompactWholeTowerUsesTheSameScaleAwareDetailPolicy()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>("Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
            var snapshot = catalog.Starters.Single(entry => entry.Id == "album.guard").Snapshot;
            var pose = AssemblyPreviewPose.For(snapshot);
            int large, small;
            using (var target = new VertexHelper())
            {
                NotebookTowerComposition.DrawRecipeUI(target, snapshot, pose, Vector2.zero, 1200);
                large = target.currentVertCount;
            }
            using (var target = new VertexHelper())
            {
                NotebookTowerComposition.DrawRecipeUI(target, snapshot, pose, Vector2.zero, 40);
                small = target.currentVertCount;
            }
            Assert.That(small, Is.GreaterThan(0));
            Assert.That(small, Is.LessThan(large), "Whole-tower previews need detail reduction at their actual displayed scale.");
        }
    }
}
#endif
