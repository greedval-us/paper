using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class NeutralGraphicsTests
    {
        [TestCase("object.castle")]
        [TestCase("enemy.doodle.sketch.walk")]
        [TestCase("object.well.red")]
        public void EssentialBattleSymbolsProduceVisibleGeometryUsingOnlyTheWhiteTexture(string id)
        {
            var shape = NotebookVisualThemeAsset.Default.FindShape(id);
            Assert.That(shape, Is.Not.Null, id);
            using (var mesh = new VertexHelper())
            {
                Assert.That(NotebookArtRenderer.DrawUI(mesh, shape, Vector2.zero, 1), Is.True, id);
                Assert.That(mesh.currentVertCount, Is.GreaterThan(4), "A symbol is live geometry, independent of the removed atlas.");
                Assert.That(NotebookSpriteCatalog.Texture, Is.SameAs(Texture2D.whiteTexture));
                var vertex = new UIVertex();
                for (int i = 0; i < mesh.currentVertCount; i++)
                {
                    mesh.PopulateUIVertex(ref vertex, i);
                    Assert.That(Vector2.Distance(vertex.uv0, new Vector2(.5f, .5f)), Is.LessThan(.00001f));
                }
            }
        }

        [Test] public void PaperPanelsRemainVisibleWithoutLoadingACardSprite()
        {
            var owner = new GameObject("Neutral panel", typeof(RectTransform));
            try
            {
                Assert.That(NotebookPaperArt.Apply(owner.transform, "Panel surface"), Is.True);
                var image = owner.transform.Find("Panel surface").GetComponent<Image>();
                Assert.That(image.sprite, Is.Null);
                Assert.That(image.color.a, Is.GreaterThan(.9f));
                Assert.That(image.raycastTarget, Is.False);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test] public void RotatingAPlaceholderWeaponMovesItsMuzzleAndKeepsItsMountIdentity()
        {
            var weapon = new WeaponRecipe(P("core.gun"), P("head.gun"), P("sight.iron"), P("magazine.standard"), P("trigger.standard"));
            var rest = AssemblyVisualComposer.ComposeWeapon(weapon);
            var aimed = AssemblyVisualComposer.ComposeWeapon(weapon,
                new AssemblyVisualPose(new[] { new AssemblyMountPose(0, Mathf.PI * .5f) }));
            Assert.That(rest.Anchors.Count, Is.EqualTo(1));
            Assert.That(aimed.Anchors[0].SlotIndex, Is.EqualTo(0));
            Assert.That(aimed.Anchors[0].RotationCenter, Is.EqualTo(rest.Anchors[0].RotationCenter));
            Assert.That(Vector2.Distance(rest.Anchors[0].Muzzle, aimed.Anchors[0].Muzzle), Is.GreaterThan(.01f));
            var pen = new MeshPen(null);
            aimed.Draw(pen, Vector2.zero);
            using (var mesh = new VertexHelper())
            {
                pen.CopyTo(mesh, Vector2.zero, 1);
                Assert.That(mesh.currentVertCount, Is.GreaterThan(0));
                Assert.That(NotebookSpriteCatalog.Texture, Is.SameAs(Texture2D.whiteTexture));
            }
        }

        static PartRef P(string id) => new PartRef(id, 1);
    }
}
