#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class NightNotebookPaperTests
    {
        GameObject root;

        [TearDown] public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [TestCase(24, 24)] [TestCase(1600, 900)] [TestCase(100000, 100000)]
        public void PaperIsOpaqueBoundedAndNoninteractiveWithACappedMesh(int width, int height)
        {
            root = new GameObject("Night paper mesh fixture", typeof(RectTransform));
            var paper = root.AddComponent<NightNotebookPaperGraphic>();
            paper.rectTransform.sizeDelta = new Vector2(width, height);
            Assert.That(paper.raycastTarget, Is.False);
            Assert.That(paper.mainTexture, Is.SameAs(Texture2D.whiteTexture));
            using (var mesh = new VertexHelper())
            {
                typeof(NightNotebookPaperGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(paper, new object[] { mesh });
                Assert.That(mesh.currentVertCount, Is.InRange(4, 1400));
                var vertex = new UIVertex();
                for (int i = 0; i < mesh.currentVertCount; i++)
                {
                    mesh.PopulateUIVertex(ref vertex, i);
                    Assert.That(float.IsNaN(vertex.position.x) || float.IsInfinity(vertex.position.x) ||
                        float.IsNaN(vertex.position.y) || float.IsInfinity(vertex.position.y), Is.False);
                    Assert.That(vertex.position.x, Is.InRange(paper.rectTransform.rect.xMin, paper.rectTransform.rect.xMax));
                    Assert.That(vertex.position.y, Is.InRange(paper.rectTransform.rect.yMin, paper.rectTransform.rect.yMax));
                    if (i < 4) Assert.That(vertex.color, Is.EqualTo(new Color32(20, 44, 38, 255)));
                }
            }
        }

        [Test] public void RefreshKeepsOneAlignedAssemblySheetAndALightLegacyWell()
        {
            root = new GameObject("Night paper menu fixture", typeof(RectTransform));
            var scope = (RectTransform)root.transform; scope.sizeDelta = new Vector2(800, 600);
            var assembly = PlayerUi.Graphic<WeaponAssemblyGraphic>(scope, "Modern assembly", new Rect(20, 35, 240, 160));
            var legacy = PlayerUi.Graphic<BlueprintSketch>(scope, "Legacy drawing", new Rect(300, 35, 240, 160));
            assembly.rectTransform.localScale = new Vector3(.9f, .8f, 1);
            assembly.rectTransform.localRotation = Quaternion.Euler(0, 0, 13);
            FieldMenuTheme.ApplyTo(scope, false);
            var first = scope.GetComponentsInChildren<NightNotebookPaperGraphic>().Single();
            var theme = scope.GetComponent<FieldMenuTheme>();
            assembly.rectTransform.sizeDelta += new Vector2(12, 18);
            assembly.rectTransform.anchoredPosition += new Vector2(3, 4);
            theme.Refresh(); theme.Refresh();

            var paper = scope.GetComponentsInChildren<NightNotebookPaperGraphic>().Single();
            Assert.That(paper, Is.SameAs(first));
            Assert.That(paper.raycastTarget, Is.False);
            Assert.That(paper.GetComponent<LayoutElement>().ignoreLayout, Is.True);
            Assert.That(paper.rectTransform.anchorMin, Is.EqualTo(assembly.rectTransform.anchorMin));
            Assert.That(paper.rectTransform.anchorMax, Is.EqualTo(assembly.rectTransform.anchorMax));
            Assert.That(paper.rectTransform.pivot, Is.EqualTo(assembly.rectTransform.pivot));
            Assert.That(paper.rectTransform.sizeDelta, Is.EqualTo(assembly.rectTransform.sizeDelta));
            Assert.That(paper.rectTransform.anchoredPosition3D, Is.EqualTo(assembly.rectTransform.anchoredPosition3D));
            Assert.That(paper.rectTransform.localScale, Is.EqualTo(assembly.rectTransform.localScale));
            Assert.That(paper.rectTransform.localRotation, Is.EqualTo(assembly.rectTransform.localRotation));
            Assert.That(paper.transform.GetSiblingIndex(), Is.LessThan(assembly.transform.GetSiblingIndex()));
            var light = scope.GetComponentsInChildren<Image>().Single(image => image.name.StartsWith("Field schematic backing"));
            Assert.That(light, Is.Not.Null);
            Assert.That(light.color, Is.EqualTo(FieldMenuSkin.PreviewPaper));
            Assert.That(light.raycastTarget, Is.False);
            Assert.That(scope.GetComponentsInChildren<Graphic>().Count(graphic => graphic.name.StartsWith("Field schematic backing")), Is.EqualTo(2));
        }

        [Test] public void CurrentCastleSharesTheDarkResultSheetAfterRepeatedRefresh()
        {
            root = new GameObject("Standard result paper fixture", typeof(RectTransform));
            var scope = (RectTransform)root.transform; scope.sizeDelta = new Vector2(800, 600);
            var result = new GameObject("Result composition", typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(scope, false); result.sizeDelta = scope.sizeDelta;
            PlayerUi.Graphic<WeaponAssemblyGraphic>(result, "Result modern assembly", new Rect(20, 35, 240, 160));
            var castle = PlayerUi.Graphic<NotebookArtGraphic>(result, "object.castle", new Rect(200, 35, 180, 180));
            castle.Bind("object.castle");
            FieldMenuTheme.ApplyTo(scope, false);
            var theme = scope.GetComponent<FieldMenuTheme>(); theme.Refresh(); theme.Refresh();
            Assert.That(result.GetComponentsInChildren<NightNotebookPaperGraphic>(), Has.Length.EqualTo(1));
            Assert.That(result.GetComponentsInChildren<Image>().Where(image => image.name.StartsWith("Field schematic backing")), Is.Empty,
                "The current light-ink castle must use the same dark sheet as the standard assembly.");
        }

        [Test] public void MixedResultKeepsItsLegacyIslandBelowEveryAssemblyAfterRefresh()
        {
            root = new GameObject("Night paper result fixture", typeof(RectTransform));
            var scope = (RectTransform)root.transform; scope.sizeDelta = new Vector2(800, 600);
            var result = new GameObject("Result composition", typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(scope, false); result.sizeDelta = scope.sizeDelta;
            var assembly = PlayerUi.Graphic<WeaponAssemblyGraphic>(result, "Result modern assembly", new Rect(20, 35, 240, 160));
            var source = new WorkshopProfile().GetLoadoutEntries().First(entry => entry != null).LegacyBlueprint;
            var edit = new DrawingEditSession(source);
            Assert.That(edit.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.3f, -.3f),
                new DrawingPoint(.3f, .3f), out var reason), Is.True, reason);
            var reference = PlayerUi.Graphic<BlueprintSketch>(result, "Authored result drawing", new Rect(200, 35, 180, 180));
            reference.SetBlueprint(source.WithDrawing(edit.Snapshot));
            FieldMenuTheme.ApplyTo(scope, false);
            var theme = scope.GetComponent<FieldMenuTheme>();
            theme.Refresh(); theme.Refresh();

            var paper = result.GetComponentsInChildren<NightNotebookPaperGraphic>().Single();
            Assert.That(paper.name, Is.EqualTo("Field schematic backing result"));
            var light = result.GetComponentsInChildren<Image>().Single(image => image.name.StartsWith("Field schematic backing"));
            Assert.That(light, Is.Not.Null);
            Assert.That(light.color, Is.EqualTo(FieldMenuSkin.PreviewPaper));
            Assert.That(paper.transform.GetSiblingIndex(), Is.LessThan(light.transform.GetSiblingIndex()));
            Assert.That(light.transform.GetSiblingIndex(), Is.LessThan(assembly.transform.GetSiblingIndex()));
            Assert.That(light.transform.GetSiblingIndex(), Is.LessThan(reference.transform.GetSiblingIndex()));
            Assert.That(result.GetComponentsInChildren<Graphic>().Count(graphic => graphic.name.StartsWith("Field schematic backing")), Is.EqualTo(2));
        }
    }
}
#endif
