using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class StandardMountLayoutTests
    {
        [TestCase(18f, 14f)]
        [TestCase(30f, 10.5f)]
        public void FanAndExtendedRayEffectsStartAtTheRenderedMountTip(float effectX, float effectY)
        {
            var balance = GameBalance.CreateDefault(); balance.StartingInk = 10000;
            var game = new GameSession(balance, starterDefenses: false);
            var blueprint = new TowerBlueprint("fan-ray", "Fan ray", ChassisKind.Standard, StructureKind.Fan);
            Assert.That(game.TryBuildBlueprint(new[] { new Cell(10, 10) }, blueprint, MaterialTier.Pencil,
                InkColor.None, out string reason), Is.True, reason);
            game.Structures[0].GetMountState(0).Aim = new Point2(20, 10.5f);
            var tower = new RunReadModel(game).Structures[0];
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh);
                NotebookTowerComposition.DrawMounts(pen, blueprint, new Vector2(10.5f, 10.5f), MaterialTier.Pencil,
                    InkColor.None, tower, true, null, tower.Id, 0);
                pen.Upload();
                // Independently recover the authored fan mouth (211,213) from its actual atlas quad.
                Vector2 tip = (Vector2)mesh.vertices[0] + (Vector2)(mesh.vertices[1] - mesh.vertices[0]) * (211f / 256)
                    + (Vector2)(mesh.vertices[3] - mesh.vertices[0]) * (213f / 288);
                Vector2 muzzle = NotebookTowerComposition.Muzzle(tower, 0, new Point2(effectX, effectY), 0, null, null);
                Assert.That(Vector2.Distance(muzzle, tip), Is.LessThan(.00001f),
                    "Different rays in one shot must share the visible mount pose, while retaining their endpoints.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [TestCase("\"order\":0", "\"order\":9")]
        [TestCase("\"trim\":{\"x\":0", "\"trim\":{\"x\":-1")]
        public void BrokenMountMetadataIsRejectedInsteadOfSilentlyHidingAWeapon(string before, string after)
        {
            string json = "{\"schemaVersion\":1,\"revision\":\"fixture\",\"foundations\":[{\"id\":\"base\",\"sockets\":[" +
                "{\"slot\":0,\"order\":0,\"position\":{\"x\":0.5,\"y\":0.8},\"diameter\":0.3}]}],\"weapons\":[" +
                "{\"id\":\"gun\",\"canvas\":{\"x\":100,\"y\":200},\"trim\":{\"x\":0,\"y\":0,\"width\":100,\"height\":200}," +
                "\"attach\":{\"x\":50,\"y\":10},\"muzzle\":{\"x\":60,\"y\":180},\"diameter\":70}]}";
            Assert.Throws<System.FormatException>(() => NotebookMountLayout.Validate(json.Replace(before, after)));
        }

        [Test]
        public void AtlasTrimChangesUvsWithoutChangingTheAuthorCanvasTransform()
        {
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh);
                // The authored pixel (60,80) must still land at world (5,6), after a trim and a quarter-turn.
                NotebookSpriteCatalog.TryDrawFrame(pen, "weapon.gun", new Rect(20, 30, 100, 160),
                    new Rect(60, 80, 10, 20), new Vector2(13, 0), .1f, Mathf.PI * .5f, 1, Color.white);
                pen.Upload();
                Assert.That(Vector2.Distance(mesh.vertices[0], new Vector2(5, 6)), Is.LessThan(.00001));
                var uv = new List<Vector2>(); mesh.GetUVs(1, uv);
                NotebookSpriteCatalog.TryGet("weapon.gun", out var sprite);
                Assert.That(uv[0].x, Is.EqualTo((sprite.rect.x + sprite.rect.width * .4f) / sprite.texture.width).Within(.000001));
                Assert.That(uv[0].y, Is.EqualTo((sprite.rect.y + sprite.rect.height * .3125f) / sprite.texture.height).Within(.000001));
            }
            finally { Object.DestroyImmediate(mesh); }
        }
        static TowerBlueprint Battery(bool neighbour = false) => new TowerBlueprint("socket-test", "Battery", "chassis.battery",
            neighbour ? new[] { new WeaponMountSelection(0, "weapon.gun"), new WeaponMountSelection(2, "weapon.fan") }
                : new[] { new WeaponMountSelection(0, "weapon.gun") });

        // These regressions fail if the world uses the drawing pivot or if mount indices are compacted.
        [Test]
        public void BatteryPedestalIsSeatedOnTheRoofInsteadOfBesideTheBody()
        {
            Vector2 anchor = GunAnchor(Battery());
            Assert.That(anchor.x, Is.EqualTo(-.13894263f).Within(.00001f));
            Assert.That(anchor.y, Is.EqualTo(.1358782f).Within(.00001f));
        }

        [Test]
        public void RemovingABatteryNeighbourDoesNotMoveOrResizeTheRemainingWeapon()
        {
            Assert.That(Vector2.Distance(GunAnchor(Battery()), GunAnchor(Battery(true))), Is.LessThan(.00001f));
        }

        [Test]
        public void LargeWeaponPreviewAndAlbumUseTheSameNormalizedGeometry()
        {
            var root = new GameObject("mount-preview", typeof(RectTransform));
            using (var actual = new VertexHelper())
            using (var expected = new VertexHelper())
            {
                try
                {
                    var graphic = root.AddComponent<WeaponAssemblyGraphic>();
                    graphic.rectTransform.sizeDelta = new Vector2(500, 400);
                    graphic.SetBlueprint(Battery(true));
                    typeof(WeaponAssemblyGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                        null, new[] { typeof(VertexHelper) }, null)
                        .Invoke(graphic, new object[] { actual });
                    NotebookTowerComposition.DrawUI(expected, Battery(true), Vector2.zero, 1);
                    var a = Normalize(actual); var b = Normalize(expected);
                    Assert.That(a.Count, Is.EqualTo(b.Count));
                    for (int i = 0; i < a.Count; i++)
                        Assert.That(Vector2.Distance(a[i], b[i]), Is.LessThan(.00001f), "vertex " + i);
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

        static Vector2 GunAnchor(TowerBlueprint blueprint)
        {
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh);
                NotebookTowerComposition.DrawMounts(pen, blueprint, Vector2.zero, MaterialTier.Ink, InkColor.None,
                    default, false, null, 0, 0);
                pen.Upload();
                Assert.That(NotebookSpriteCatalog.TryGet("weapon.gun", out var sprite), Is.True);
                var uv = new List<Vector2>(); mesh.GetUVs(1, uv);
                Rect area = sprite.rect;
                Vector2 anchorUv = new Vector2((area.x + area.width * .53f) / sprite.texture.width,
                    (area.y + area.height * .08f) / sprite.texture.height);
                for (int i = 0; i + 3 < uv.Count; i += 4)
                {
                    var mid = (uv[i] + uv[i + 2]) * .5f;
                    if (!area.Contains(new Vector2(mid.x * sprite.texture.width, mid.y * sprite.texture.height))) continue;
                    return (Vector2)mesh.vertices[i] + (Vector2)(mesh.vertices[i + 1] - mesh.vertices[i]) *
                        ((anchorUv.x - uv[i].x) / (uv[i + 1].x - uv[i].x)) +
                        (Vector2)(mesh.vertices[i + 3] - mesh.vertices[i]) * ((anchorUv.y - uv[i].y) / (uv[i + 3].y - uv[i].y));
                }
                Assert.Fail("Gun atlas geometry was not emitted."); return default;
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        static List<Vector2> Normalize(VertexHelper source)
        {
            var vertices = new List<UIVertex>(); source.GetUIVertexStream(vertices);
            var min = new Vector2(float.MaxValue, float.MaxValue); var max = -min;
            foreach (var vertex in vertices) { min = Vector2.Min(min, vertex.position); max = Vector2.Max(max, vertex.position); }
            var result = new List<Vector2>();
            foreach (var vertex in vertices) result.Add(((Vector2)vertex.position - (min + max) * .5f) / (max - min).magnitude);
            return result;
        }
    }
}
