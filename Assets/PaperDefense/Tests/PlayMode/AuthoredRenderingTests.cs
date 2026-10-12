using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AuthoredRenderingTests
    {
        static DrawingStroke Stroke(params DrawingPoint[] points) => new DrawingStroke(points);
        static DrawingSnapshot Drawing() => new DrawingSnapshot(new[]
        {
            new DrawingPart(DrawingPartKey.Base, new[] { Stroke(new DrawingPoint(-.3f, -.2f), new DrawingPoint(.09f, .32f), new DrawingPoint(.28f, -.1f)) }),
            new DrawingPart(DrawingPartKey.Mount0, new[] { Stroke(new DrawingPoint(0, 0), new DrawingPoint(.07f, .3f)) }),
            new DrawingPart(DrawingPartKey.Mount1, new[] { Stroke(new DrawingPoint(.02f, -.01f), new DrawingPoint(-.05f, .18f), new DrawingPoint(.08f, .39f)) }),
            new DrawingPart(DrawingPartKey.Decoration, new[] { Stroke(new DrawingPoint(-.3f, .2f), new DrawingPoint(-.2f, .3f)) })
        });

        [SetUp] public void SetUp() { AuthoredGeometryCache.Clear(); }

        [Test] public void RepeatedCopiesAndDeepCopiesShareOnePreparedGeometryWithoutMutableStorage()
        {
            var drawing = Drawing(); var first = AuthoredGeometryCache.For(drawing);
            for (int i = 0; i < 100; i++) Assert.That(AuthoredGeometryCache.For(drawing), Is.SameAs(first));
            Assert.That(AuthoredGeometryCache.For(drawing.DeepCopy()), Is.SameAs(first));
            Assert.That(AuthoredGeometryCache.BuildCount, Is.EqualTo(1));
            var vertices = first.GetPart(DrawingPartKey.Base).Vertices;
            Assert.Throws<NotSupportedException>(() => ((IList<Vector2>)vertices)[0] = Vector2.zero);
        }

        [Test] public void RigidAppendPreservesTheCrookedLineAndDoesNotMutateStoredPoints()
        {
            var drawing = Drawing(); string hash = drawing.GeometryHash;
            var part = AuthoredGeometryCache.For(drawing).GetPart(DrawingPartKey.Mount1);
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh); pen.Append(part, new Vector2(10, 20), Mathf.PI * .5f, Color.blue); pen.Upload();
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Assert.That(vertices[i].x, Is.EqualTo(10 - part.Vertices[i].y).Within(.00001f));
                    Assert.That(vertices[i].y, Is.EqualTo(20 + part.Vertices[i].x).Within(.00001f));
                }
                Assert.That(drawing.GeometryHash, Is.EqualTo(hash));
                Assert.That(drawing.GetPart(DrawingPartKey.Mount1).Strokes[0].Points[1].X, Is.EqualTo(new DrawingPoint(-.05f, .18f).X));
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        static StructureView View(DrawingSnapshot drawing, Cell cell, Point2 primary, Point2 secondary)
        {
            var balance = GameBalance.CreateDefault(); balance.StartingInk = 1000;
            foreach (var definition in balance.StructureDefinitions) definition.BuildSeconds = 0;
            var game = new GameSession(balance, starterDefenses: false);
            var blueprint = new TowerBlueprint("authored", "Crooked double", ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun,
                drawing: drawing, origin: DrawingOrigin.PlayerDrawing);
            Assert.That(game.TryBuildBlueprint(new[] { cell }, blueprint, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
            for (int i = 0; i < 30; i++) game.Tick(1f / 30);
            var state = game.Structures[0];
            Assert.That(state.IsComplete, Is.True, "Render the completed silhouette separately from construction indicators.");
            state.PrimaryAim = primary; state.SecondaryAim = secondary;
            return new RunReadModel(game).Structures[0];
        }

        [Test] public void AuthoredBaseContainsOnlySavedStaticPartsAndCopiesKeepExactlyTheSameShape()
        {
            var drawing = Drawing(); var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh); DoodleGlyphs.StructureBase(pen, View(drawing, new Cell(10, 10), default, default)); pen.Upload();
                var first = mesh.vertices;
                Assert.That(first.Length, Is.EqualTo(12), "Eight base and four decorative vertices; no procedural silhouette is substituted.");
                pen.Clear(); DoodleGlyphs.StructureBase(pen, View(drawing, new Cell(20, 20), default, default)); pen.Upload();
                var second = mesh.vertices;
                Assert.That(second.Length, Is.EqualTo(first.Length));
                for (int i = 0; i < first.Length; i++)
                {
                    Assert.That(second[i].x - first[i].x, Is.EqualTo(10).Within(.00001f));
                    Assert.That(second[i].y - first[i].y, Is.EqualTo(10).Within(.00001f));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test] public void EachAuthoredBarrelRotatesAboutItsOwnMountTowardItsOwnTarget()
        {
            var drawing = Drawing(); var mesh = new Mesh();
            try
            {
                var view = View(drawing, new Cell(10, 10), new Point2(20, 10.54f), new Point2(10.67f, 20));
                var pen = new MeshPen(mesh); DoodleGlyphs.StructureDynamic(pen, view, 1); pen.Upload();
                var geometry = AuthoredGeometryCache.For(drawing); var vertices = mesh.vertices;
                Assert.That(vertices.Length, Is.EqualTo(12));
                var first = geometry.GetPart(DrawingPartKey.Mount0);
                var second = geometry.GetPart(DrawingPartKey.Mount1);
                for (int i = 0; i < first.Vertices.Count; i++)
                {
                    Assert.That(vertices[i].x, Is.EqualTo(10.33f + first.Vertices[i].y).Within(.0001f));
                    Assert.That(vertices[i].y, Is.EqualTo(10.54f - first.Vertices[i].x).Within(.0001f));
                }
                for (int i = 0; i < second.Vertices.Count; i++)
                {
                    Assert.That(vertices[first.Vertices.Count + i].x, Is.EqualTo(10.67f + second.Vertices[i].x).Within(.0001f));
                    Assert.That(vertices[first.Vertices.Count + i].y, Is.EqualTo(10.54f + second.Vertices[i].y).Within(.0001f));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
    }
}
