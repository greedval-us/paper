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
            var specimens = new[] {
                (Id: "weapon.fan", Canvas: new Vector2(256, 288), Attach: new Vector2(92.16f, 23.04f),
                    Muzzle: new Vector2(211, 213), Diameter: 174.08f),
                (Id: "weapon.ricochet", Canvas: new Vector2(262, 234), Attach: new Vector2(81.22f, 18.72f),
                    Muzzle: new Vector2(150, 183), Diameter: 154.58f),
                (Id: "weapon.stapler", Canvas: new Vector2(287, 248), Attach: new Vector2(117.67f, 19.84f),
                    Muzzle: new Vector2(261, 188), Diameter: 192.29f),
                (Id: "weapon.blotter", Canvas: new Vector2(243, 267), Attach: new Vector2(87.48f, 21.36f),
                    Muzzle: new Vector2(130, 158), Diameter: 167.67f)
            };
            var source = specimens[persistentKind - 5];
            var kind = (StructureKind)persistentKind;
            var bp = new TowerBlueprint("expanded-render", "Expanded mount", ChassisKind.Standard, kind);
            Assert.That(bp.FirstWeaponId, Is.EqualTo(source.Id));
            Assert.That(NotebookSpriteCatalog.TryGet(source.Id, out var sprite), Is.True, source.Id);
            Assert.That(NotebookSpriteCatalog.TryGetBounds("chassis.standard", out var chassis), Is.True);
            // Standard socket 0 is fixed at (34%,78%) of the foundation, with a 29% pedestal footprint.
            Vector2 socket = new Vector2(10.5f, 10.5f) + chassis.min + Vector2.Scale(chassis.size, new Vector2(.34f, .78f));
            float scale = chassis.width * .29f / source.Diameter;
            var aim = new Point2(20, 10.54f);
            var balance = GameBalance.CreateDefault(); balance.StartingInk = 1000; balance.Structure(kind).BuildSeconds = 0;
            var game = new GameSession(balance, starterDefenses: false);
            Assert.That(game.TryBuildBlueprint(new[] { new Cell(10, 10) }, bp, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
            game.Tick(1f / 30);
            game.Structures[0].PrimaryAim = aim;
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh); DoodleGlyphs.StructureDynamic(pen, new RunReadModel(game).Structures[0], 1); pen.Upload();
                var actual = mesh.vertices;
                Assert.That(actual.Length, Is.EqualTo(4), "The standard mount uses its new full atlas illustration.");
                var atlas = new List<Vector2>(); mesh.GetUVs(1, atlas);
                Assert.That(atlas.Distinct().Count(), Is.EqualTo(4));
                Assert.That(atlas.All(value => value != NotebookSpriteCatalog.WhiteUV), Is.True);
                Rect area = sprite.rect;
                var expectedUvs = new[] {
                    new Vector2(area.xMin / sprite.texture.width, area.yMin / sprite.texture.height),
                    new Vector2(area.xMax / sprite.texture.width, area.yMin / sprite.texture.height),
                    new Vector2(area.xMax / sprite.texture.width, area.yMax / sprite.texture.height),
                    new Vector2(area.xMin / sprite.texture.width, area.yMax / sprite.texture.height)
                };
                for (int i = 0; i < expectedUvs.Length; i++)
                    Assert.That(Vector2.Distance(atlas[i], expectedUvs[i]), Is.LessThan(.000001f), source.Id + " corner " + i);

                Vector2 horizontal = actual[1] - actual[0], vertical = actual[3] - actual[0];
                Assert.That(horizontal.magnitude / source.Canvas.x, Is.EqualTo(scale).Within(.000001f));
                Assert.That(vertical.magnitude / source.Canvas.y, Is.EqualTo(scale).Within(.000001f),
                    "The source canvas must retain one uniform pixel scale.");
                Assert.That(Mathf.Abs(Vector2.Dot(horizontal.normalized, vertical.normalized)), Is.LessThan(.00001f));
                Vector2 AtPixel(Vector2 pixel) => (Vector2)actual[0]
                    + horizontal * (pixel.x / source.Canvas.x) + vertical * (pixel.y / source.Canvas.y);
                Vector2 attach = AtPixel(source.Attach), muzzle = AtPixel(source.Muzzle);
                Assert.That(Vector2.Distance(attach, socket), Is.LessThan(.00001f),
                    "The authored pedestal attachment must remain seated on socket 0 while the weapon aims.");
                Vector2 direction = (new Vector2(aim.X, aim.Y) - socket).normalized;
                Assert.That(Vector2.Distance((muzzle - attach).normalized, direction), Is.LessThan(.00001f),
                    "Aim follows the hand-measured attach-to-muzzle axis, not the canvas up axis.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
