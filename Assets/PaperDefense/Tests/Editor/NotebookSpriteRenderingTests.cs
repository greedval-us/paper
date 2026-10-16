using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class NotebookSpriteRenderingTests
    {
        [Test]
        public void BattleAtlasIsAnImportedRuntimeResource()
        {
            Assert.That(Resources.Load<Texture2D>("Art/NotebookAtlas"), Is.Not.Null,
                "The battle must use the new blue-pen atlas rather than retired vector artwork.");
            Assert.That(Resources.Load<TextAsset>("Art/NotebookSprites"), Is.Not.Null,
                "The atlas requires explicit semantic bindings and local drawing bounds.");
        }

        [Test]
        public void DestroyedCachedSpriteIsRecreatedWithTheSameAtlasRegion()
        {
            NotebookSpriteCatalog.ResetCache();
            try
            {
                Assert.That(NotebookSpriteCatalog.TryGet("logo.settings", out var original), Is.True);
                Assert.That(original != null, Is.True, "The initial Unity Sprite must be alive.");
                Rect rect = original.rect; var texture = original.texture;
                Object.DestroyImmediate(original);
                Assert.That(original == null, Is.True, "The native Sprite was destroyed while its managed wrapper remains cached.");
                Assert.That(NotebookSpriteCatalog.TryGet("logo.settings", out var recreated), Is.True);
                Assert.That(recreated != null, Is.True, "TryGet must never report success with a destroyed Unity Sprite.");
                Assert.That(recreated, Is.Not.SameAs(original));
                Assert.That(recreated.rect, Is.EqualTo(rect));
                Assert.That(recreated.texture, Is.SameAs(texture));
            }
            finally { NotebookSpriteCatalog.ResetCache(); }
        }

        [Test]
        public void EveryPublishedProductionShapeHasAnAtlasBindingAndNoRetiredMarks()
        {
            NotebookSpriteCatalog.ResetCache();
            var theme = NotebookVisualThemeAsset.Default;
            Assert.That(theme, Is.Not.Null);
            Assert.That(theme.Data.Shapes, Is.Not.Empty);
            foreach (var shape in theme.Data.Shapes)
            {
                Assert.That(shape.Marks, Is.Empty, shape.Id + " must not retain the retired vector artwork.");
                Assert.That(NotebookSpriteCatalog.TryGetBounds(shape.Id, out _), Is.True, shape.Id);
            }
        }

        [Test]
        public void NativeStrokesKeepTheirGeometryAndAntialiasingBesideAtlasCoordinates()
        {
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh) { WorldUnitsPerPixel = .1f, MinimumStrokePixels = 1.25f };
                pen.Line(new Vector2(2, 3), new Vector2(4, 3), .04f, Color.blue);
                pen.Upload();
                var expected = new[] {
                    new Vector3(2, 2.8375f, 0), new Vector3(4, 2.8375f, 0),
                    new Vector3(4, 3.1625f, 0), new Vector3(2, 3.1625f, 0) };
                Assert.That(mesh.vertexCount, Is.EqualTo(expected.Length));
                for (int i = 0; i < expected.Length; i++)
                    Assert.That(Vector3.Distance(mesh.vertices[i], expected[i]), Is.LessThan(.00001));
                var stroke = new List<Vector3>(); var atlas = new List<Vector2>();
                mesh.GetUVs(0, stroke); mesh.GetUVs(1, atlas);
                Assert.That(stroke.Count, Is.EqualTo(4));
                Assert.That(stroke[0].x, Is.EqualTo(.0625f).Within(.00001));
                Assert.That(stroke.All(value => value.z == 1), Is.True);
                Assert.That(atlas, Is.All.EqualTo(NotebookSpriteCatalog.WhiteUV));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void MixedStrokeAndRasterStreamPreservesWorldGeometryAndTextureCoordinatesInCanvas()
        {
            var mesh = new Mesh();
            using (var target = new VertexHelper())
            {
                try
                {
                    var pen = new MeshPen(mesh);
                    pen.Line(new Vector2(2, 3), new Vector2(4, 3), .04f, Color.blue);
                    pen.TexturedQuad(new Vector2(5, 6), new Vector2(7, 6), new Vector2(7, 9), new Vector2(5, 9),
                        new Rect(.2f, .3f, .4f, .5f), Color.white);
                    pen.Upload(); pen.CopyTo(target, new Vector2(10, 20), 2);
                    var atlas = new List<Vector2>(); mesh.GetUVs(1, atlas);
                    var vertex = new UIVertex();
                    for (int i = 0; i < mesh.vertexCount; i++)
                    {
                        target.PopulateUIVertex(ref vertex, i);
                        Assert.That((Vector2)vertex.position, Is.EqualTo((Vector2)mesh.vertices[i] * 2 + new Vector2(10, 20)));
                        Assert.That((Vector2)vertex.uv0, Is.EqualTo(atlas[i]));
                    }
                    Assert.That(atlas.Skip(4), Is.EqualTo(new[] { new Vector2(.2f, .3f), new Vector2(.6f, .3f),
                        new Vector2(.6f, .8f), new Vector2(.2f, .8f) }));
                }
                finally { Object.DestroyImmediate(mesh); }
            }
        }

        [Test]
        public void RasterBindingPreservesItsLogicalBoundsRotationStretchAndOpacity()
        {
            var theme = NotebookVisualThemeAsset.Default;
            var shape = theme?.FindShape("object.castle");
            Assert.That(shape, Is.Not.Null);
            Assert.That(NotebookSpriteCatalog.TryGetBounds(shape.Id, out var bounds), Is.True);
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh);
                Assert.That(NotebookArtRenderer.Draw(pen, shape, new Vector2(3, 4), 2, Mathf.PI * .5f,
                    opacity: .4f, localScale: new Vector2(1.5f, .75f)), Is.True);
                pen.Upload();
                Assert.That(mesh.vertexCount, Is.EqualTo(4));
                Vector2 expected = new Vector2(3 - bounds.yMin * 1.5f, 4 + bounds.xMin * 3);
                Assert.That(Vector2.Distance(mesh.vertices[0], expected), Is.LessThan(.00001));
                Assert.That(mesh.colors32.All(value => value.a == 102), Is.True);
                var atlas = new List<Vector2>(); mesh.GetUVs(1, atlas);
                Assert.That(atlas.Distinct().Count(), Is.EqualTo(4));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void CanvasOverflowBatchKeepsTheFinalRasterQuadAndItsUvs()
        {
            var pen = new MeshPen(null);
            int quadsInFirstBatch = MeshPen.MaximumUiBatchVertices / 6;
            for (int i = 0; i < quadsInFirstBatch; i++)
                pen.TexturedQuad(Vector2.zero, Vector2.right, Vector2.one, Vector2.up, new Rect(.1f, .1f, .2f, .2f), Color.white);
            pen.TexturedQuad(Vector2.zero, Vector2.right, Vector2.one, Vector2.up, new Rect(.6f, .6f, .2f, .2f), Color.white);
            Assert.That(pen.UiBatchCount, Is.EqualTo(2));
            using (var target = new VertexHelper())
            {
                pen.CopyUiBatchTo(target, Vector2.zero, 1, 1);
                Assert.That(target.currentVertCount, Is.EqualTo(6));
                var vertex = new UIVertex(); target.PopulateUIVertex(ref vertex, 0);
                Assert.That((Vector2)vertex.uv0, Is.EqualTo(new Vector2(.6f, .6f)));
                target.PopulateUIVertex(ref vertex, 2);
                Assert.That((Vector2)vertex.uv0, Is.EqualTo(new Vector2(.8f, .8f)));
            }
        }

        [TestCase("{\"id\":\"alias\",\"alias\":\"missing\"}", "missing")]
        [TestCase("{\"id\":\"alias\",\"alias\":\"alias\"}", "cycle")]
        [TestCase("{\"id\":\"part\",\"x\":30,\"y\":0,\"width\":4,\"height\":4,\"bounds\":{\"x\":-0.5,\"y\":-0.5,\"width\":1,\"height\":1}}", "clipped")]
        [TestCase("{\"id\":\"part\",\"x\":0,\"y\":0,\"width\":4,\"height\":4,\"bounds\":{\"x\":0,\"y\":0,\"width\":0,\"height\":1}}", "bounds")]
        public void InvalidAtlasBindingsAreRejectedBeforeRendering(string entries, string diagnostic)
        {
            string json = "{\"revision\":\"fixture\",\"whitePixel\":{\"x\":0,\"y\":0},\"sprites\":[" + entries + "]}";
            var error = Assert.Throws<FormatException>(() => NotebookSpriteCatalog.Validate(json, 32, 32));
            Assert.That(error.Message, Does.Contain(diagnostic));
        }

        [Test]
        public void AliasesCanShareAnIllustrationWithoutDuplicatingPixels()
        {
            const string json = "{\"revision\":\"fixture\",\"whitePixel\":{\"x\":0,\"y\":0},\"sprites\":[" +
                "{\"id\":\"base\",\"x\":8,\"y\":8,\"width\":8,\"height\":8,\"bounds\":{\"x\":-1,\"y\":-0.5,\"width\":2,\"height\":1}}," +
                "{\"id\":\"first\",\"alias\":\"second\"},{\"id\":\"second\",\"alias\":\"base\"}]}";
            Assert.DoesNotThrow(() => NotebookSpriteCatalog.Validate(json, 32, 32));
        }

        [Test]
        public void StandardConstructionUsesAtlasArtworkInsteadOfReferenceDrawingContours()
        {
            var balance = GameBalance.CreateDefault(); balance.StartingInk = 10000;
            balance.Structure(StructureKind.Fan).BuildSeconds = 10;
            var session = new GameSession(balance, starterDefenses: false);
            var blueprint = new TowerBlueprint("construction-art", "Construction art", ChassisKind.Standard, StructureKind.Fan);
            Assert.That(session.TryBuildBlueprint(new[] { new Cell(10, 10) }, blueprint, MaterialTier.Ink, InkColor.None, out var reason), Is.True, reason);
            var structure = new RunReadModel(session).Structures[0];
            Assert.That(structure.IsComplete, Is.False);
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh);
                NotebookTowerComposition.DrawConstruction(pen, structure, new Vector2(10.5f, 10.5f), .5f);
                pen.Upload();
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                var atlas = new List<Vector2>(); mesh.GetUVs(1, atlas);
                Assert.That(atlas.All(value => value != NotebookSpriteCatalog.WhiteUV), Is.True,
                    "A standard tower's construction must use its new illustrations, not retired drawing-reference strokes.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

    }
}
