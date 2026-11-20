using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class ExpandedMountRenderingTests
    {
        [TestCase(5, "weapon.fan")]
        [TestCase(6, "weapon.ricochet")]
        [TestCase(7, "weapon.stapler")]
        [TestCase(8, "weapon.blotter")]
        public void StandardExpandedMountUsesNativeGeometryAtItsLogicalPivotAndRealAim(int persistentKind, string expectedId)
        {
            var kind = (StructureKind)persistentKind;
            var blueprint = new TowerBlueprint("expanded-render", "Expanded mount", ChassisKind.Standard, kind);
            Assert.That(blueprint.FirstWeaponId, Is.EqualTo(expectedId));
            var local = DrawingTemplateCatalog.Pivot(blueprint, DrawingBindings.Mount0);
            const float heading = 30;
            Vector2 offset = Quaternion.Euler(0, 0, heading - 90) * new Vector2(local.X, local.Y);
            Vector2 socket = new Vector2(10.5f, 10.5f) + offset;
            var aim = new Point2(20, 10.54f);
            var balance = GameBalance.CreateDefault(); balance.StartingInk = 1000; balance.Structure(kind).BuildSeconds = 0;
            var game = new GameSession(balance, starterDefenses: false);
            Assert.That(game.TryBuildBlueprint(new[] { new Cell(10, 10) }, blueprint, MaterialTier.Pencil, InkColor.None, out var reason, facingDegrees: heading), Is.True, reason);
            game.Tick(1f / 30); game.Structures[0].PrimaryAim = aim;
            var mesh = new Mesh();
            try
            {
                var tower = new RunReadModel(game).Structures[0];
                var pen = new MeshPen(mesh); DoodleGlyphs.StructureDynamic(pen, tower, 1); pen.Upload();
                Assert.That(mesh.vertexCount, Is.GreaterThan(4), "A complete standard weapon contains fill and outline geometry.");
                var coordinates = new List<Vector2>(); mesh.GetUVs(1, coordinates);
                Assert.That(coordinates, Is.All.EqualTo(new Vector2(.5f, .5f)));
                var theme = NotebookVisualThemeAsset.Default;
                var source = theme.FindShape(expectedId);
                var forward = (new Vector2(aim.X, aim.Y) - socket).normalized;
                var side = new Vector2(forward.y, -forward.x);
                // Approved drawings may start with structural strokes before their first fill.
                // Compare the complete native source, preserving its actual primitive order.
                using (var localGeometry = new VertexHelper())
                {
                    Assert.That(NotebookArtRenderer.DrawUI(localGeometry, source, Vector2.zero, 1, theme: theme), Is.True);
                    Assert.That(mesh.vertexCount, Is.EqualTo(localGeometry.currentVertCount),
                        "The complete source must stay attached to this logical mount.");
                    var point = new UIVertex();
                    for (int vertex = 0; vertex < localGeometry.currentVertCount; vertex++)
                    {
                        localGeometry.PopulateUIVertex(ref point, vertex);
                        Vector2 expected = socket + side * point.position.x + forward * point.position.y;
                        Assert.That(Vector2.Distance(mesh.vertices[vertex], expected), Is.LessThan(.00001f), expectedId + " vertex " + vertex);
                    }
                }
                var muzzle = NotebookTowerComposition.Muzzle(tower, 0, aim, 0, null, theme);
                Assert.That(Vector2.Distance(muzzle, socket + forward * .35f), Is.LessThan(.00001f),
                    "The muzzle and native barrel use the same logical mount and real aim direction.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
