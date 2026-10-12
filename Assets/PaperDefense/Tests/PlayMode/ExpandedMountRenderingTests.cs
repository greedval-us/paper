using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class ExpandedMountRenderingTests
    {
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        public void StandardExpandedMountUsesItsDistinctTemplateAndRealAim(int persistentKind)
        {
            var kind = (StructureKind)persistentKind;
            var bp = new TowerBlueprint("expanded-render", "Expanded mount", ChassisKind.Standard, kind);
            var template = DrawingTemplateCatalog.Create(bp).Single(p => p.Key == DrawingPartKey.Mount0);
            var geometry = AuthoredGeometryCache.For(new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Mount0, template.Contours) })).GetPart(DrawingPartKey.Mount0);
            var balance = GameBalance.CreateDefault(); balance.StartingInk = 1000; balance.Structure(kind).BuildSeconds = 0;
            var game = new GameSession(balance, starterDefenses: false);
            Assert.That(game.TryBuildBlueprint(new[] { new Cell(10, 10) }, bp, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
            game.Tick(1f / 30);
            game.Structures[0].PrimaryAim = new Point2(20, 10.54f);
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh); DoodleGlyphs.StructureDynamic(pen, new RunReadModel(game).Structures[0], 1); pen.Upload();
                var actual = mesh.vertices;
                Assert.That(actual.Length, Is.EqualTo(geometry.Vertices.Count));
                for (int i = 0; i < actual.Length; i++)
                {
                    Assert.That(actual[i].x, Is.EqualTo(10.5f + geometry.Vertices[i].y).Within(.0001f));
                    Assert.That(actual[i].y, Is.EqualTo(10.54f - geometry.Vertices[i].x).Within(.0001f));
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
