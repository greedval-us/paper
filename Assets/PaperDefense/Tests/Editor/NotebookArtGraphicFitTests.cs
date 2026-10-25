using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class NotebookArtGraphicFitTests
    {
        [TestCase("object.castle", 500, 240)]
        [TestCase("terrain.pine", 240, 360)]
        [TestCase("ui.weapon.piercer", 320, 110)]
        [TestCase("object.repair", 360, 140)]
        public void AuthoredBoundsFillTheAvailableRectangleWithoutStretchingOrOriginBias(string id, int width, int height)
        {
            var owner = new GameObject("Isolated artwork fit", typeof(RectTransform));
            try
            {
                var graphic = owner.AddComponent<NotebookArtGraphic>();
                graphic.rectTransform.sizeDelta = new Vector2(width, height); graphic.Bind(id);
                Assert.That(NotebookSpriteCatalog.TryGetBounds(id, out var authored), Is.True);
                if (id == "ui.weapon.piercer")
                    authored = AssemblyPreviewPose.VisibleBounds(AssemblyVisualComposer.ComposeWeapon(new WeaponRecipe(
                        new PartRef("core.piercer", 1), new PartRef("head.piercer", 1), new PartRef("sight.iron", 1),
                        new PartRef("magazine.standard", 1), new PartRef("trigger.standard", 1)), AssemblyPreviewPose.ForWeapon()));
                using (var mesh = new VertexHelper())
                {
                    typeof(NotebookArtGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null)
                        .Invoke(graphic, new object[] { mesh });
                    Assert.That(mesh.currentVertCount, Is.GreaterThan(0));
                    var vertex = new UIVertex(); mesh.PopulateUIVertex(ref vertex, 0);
                    Vector2 low = vertex.position, high = low;
                    for (int i = 1; i < mesh.currentVertCount; i++)
                    {
                        mesh.PopulateUIVertex(ref vertex, i);
                        low = Vector2.Min(low, vertex.position); high = Vector2.Max(high, vertex.position);
                    }
                    var bounds = Rect.MinMaxRect(low.x, low.y, high.x, high.y);
                    Assert.That(Vector2.Distance(bounds.center, graphic.rectTransform.rect.center), Is.LessThan(.01f),
                        "The authored pivot must not displace a static card illustration.");
                    Assert.That(bounds.width / bounds.height, Is.EqualTo(authored.width / authored.height).Within(.0001));
                    Assert.That(bounds.width, Is.LessThanOrEqualTo(width * .921f));
                    Assert.That(bounds.height, Is.LessThanOrEqualTo(height * .921f));
                    Assert.That(Mathf.Max(bounds.width / width, bounds.height / height), Is.GreaterThan(.89f),
                        "Rectangular art must fill a card; square-extent fitting makes castles and spears too small.");
                }
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
