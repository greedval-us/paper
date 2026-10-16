using System.Linq;
using System.Collections.Generic;
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
        public void StandardExpandedMountUsesItsAtlasIllustrationAndRealAim(int persistentKind)
        {
            var kind = (StructureKind)persistentKind;
            var bp = new TowerBlueprint("expanded-render", "Expanded mount", ChassisKind.Standard, kind);
            Assert.That(NotebookSpriteCatalog.TryGetBounds(bp.FirstWeaponId, out var bounds), Is.True, bp.FirstWeaponId);
            Point2 pivot = DrawingTemplateCatalog.Pivot(bp, DrawingBindings.Mount(0));
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
                Assert.That(actual.Length, Is.EqualTo(4), "The standard mount uses its new full atlas illustration.");
                var corners = new[] { new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMax, bounds.yMin),
                    new Vector2(bounds.xMax, bounds.yMax), new Vector2(bounds.xMin, bounds.yMax) };
                for (int i = 0; i < actual.Length; i++)
                {
                    Assert.That(actual[i].x, Is.EqualTo(10.5f + pivot.X + corners[i].y).Within(.0001f));
                    Assert.That(actual[i].y, Is.EqualTo(10.5f + pivot.Y - corners[i].x).Within(.0001f));
                }
                var atlas = new List<Vector2>(); mesh.GetUVs(1, atlas);
                Assert.That(atlas.Distinct().Count(), Is.EqualTo(4));
                Assert.That(atlas.All(value => value != NotebookSpriteCatalog.WhiteUV), Is.True);
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
