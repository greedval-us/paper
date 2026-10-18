using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class RailPresentationTests
    {
        const TowerModules Rail = (TowerModules)8;

        [Test] public void WorkshopExplainsRangeTradeoffAndOffersSeparateDrawingLayer()
        {
            var root = new GameObject("Isolated rail workshop"); root.SetActive(false);
            try
            {
                var run = root.AddComponent<RunController>(); run.InitializeWorkshop(false, allowLegacyFixtureDefaults: true);
                var hud = root.AddComponent<WorkshopHud>(); hud.Initialize(run);
                var button = root.GetComponentsInChildren<Button>(true).SingleOrDefault(b => b.name == "Module Rail");
                Assert.That(button, Is.Not.Null);
                button.onClick.Invoke();
                Assert.That(hud.Draft.Modules & Rail, Is.EqualTo(Rail));
                Assert.That(hud.Status.ToLowerInvariant(), Does.Contain("дальн"));
                hud.DrawingEditor.Open(hud.Draft);
                var layer = root.GetComponentsInChildren<Button>(true).SingleOrDefault(b => b.name == "Draw part Rail");
                Assert.That(layer, Is.Null, "Equipment modifiers no longer add tracing roles.");
                var roles = root.GetComponentsInChildren<Button>(true).Where(b => b.name.StartsWith("Draw part ")).ToArray();
                Assert.That(roles.Any(b => b.GetComponentInChildren<Text>().text == "КОРПУС"), Is.True);
                Assert.That(roles.Any(b => b.GetComponentInChildren<Text>().text == "СТВОЛ 1"), Is.True);
                Assert.That(roles.Any(b => b.GetComponentInChildren<Text>().text == "ДЕКОР"), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(1, 4)]
        [TestCase(8, 2)]
        public void FieldRangeRingUsesEffectiveAssemblyRange(int kindValue, float rawRange)
        {
            var kind = (StructureKind)kindValue;
            var balance = GameBalance.CreateDefault(); balance.StartingInk = 1000;
            balance.Structure(kind).Range = rawRange; balance.Structure(kind).BuildSeconds = 0;
            var game = new GameSession(balance, starterDefenses: false);
            var bp = new TowerBlueprint("rail-ring", "Rail", ChassisKind.Standard, kind, modules: Rail);
            var cell = new Cell(10, 10);
            Assert.That(game.TryBuildBlueprint(new[] { cell }, bp, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
            var root = new GameObject("Isolated rail range mesh");
            try
            {
                var view = root.AddComponent<NotebookWorldView>(); view.ShowRanges = true; view.Initialize(new RunReadModel(game));
                var mesh = root.GetComponentsInChildren<MeshFilter>().Single(f => f.name == "Castle, walls and weapon bases").sharedMesh;
                var vertices = mesh.vertices;
                // A ring is emitted first. The midpoint of its first edge is its exact +X start.
                var start = (vertices[0] + vertices[3]) * .5f;
                Assert.That(start.x - (cell.X + .5f), Is.EqualTo(rawRange * 1.25f).Within(.0001f));
                Assert.That(start.y, Is.EqualTo(cell.Y + .5f).Within(.0001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void StandardPageShowsRailInWorldAndAlbum()
        {
            var balance = GameBalance.CreateDefault(); balance.StartingInk = 1000;
            var game = new GameSession(balance, starterDefenses: false);
            var plain = new TowerBlueprint("plain", "Plain", ChassisKind.Standard, StructureKind.Gun);
            var rail = new TowerBlueprint("rail", "Rail", ChassisKind.Standard, StructureKind.Gun, modules: Rail);
            Assert.That(game.TryBuildBlueprint(new[] { new Cell(10, 10) }, plain, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
            Assert.That(game.TryBuildBlueprint(new[] { new Cell(12, 10) }, rail, MaterialTier.Pencil, InkColor.None, out reason), Is.True, reason);
            for (int i = 0; i < 90; i++) game.Tick(1f / 30);
            var model = new RunReadModel(game); var mesh = new Mesh();
            var root = new GameObject("Isolated rail thumbnail", typeof(RectTransform), typeof(CanvasRenderer), typeof(BlueprintSketch));
            try
            {
                Assert.That(NotebookSpriteCatalog.TryGet("module.rail", out var sprite), Is.True);
                var pen = new MeshPen(mesh);
                DoodleGlyphs.StructureBase(pen, model.Structures[0]); DoodleGlyphs.StructureDynamic(pen, model.Structures[0], 1); pen.Upload();
                var worldUvs = new List<Vector2>(); mesh.GetUVs(1, worldUvs);
                Assert.That(ContainsSpriteQuad(worldUvs, sprite), Is.False, "The plain world assembly has no rail illustration.");
                pen.Clear(); DoodleGlyphs.StructureBase(pen, model.Structures[1]); DoodleGlyphs.StructureDynamic(pen, model.Structures[1], 1); pen.Upload();
                worldUvs.Clear(); mesh.GetUVs(1, worldUvs);
                Assert.That(ContainsSpriteQuad(worldUvs, sprite), Is.True, "The world assembly must display the actual rail atlas quad.");
                var sketch = root.GetComponent<BlueprintSketch>();
                var populate = typeof(BlueprintSketch).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                using (var vh = new VertexHelper())
                {
                    sketch.SetBlueprint(plain); populate.Invoke(sketch, new object[] { vh });
                    Assert.That(ContainsSpriteQuad(UiUvs(vh), sprite), Is.False, "The plain album assembly has no rail illustration.");
                    sketch.SetBlueprint(rail); populate.Invoke(sketch, new object[] { vh });
                    Assert.That(ContainsSpriteQuad(UiUvs(vh), sprite), Is.True, "The album assembly must display the actual rail atlas quad.");
                }
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(root); }
        }

        static Vector2[] UiUvs(VertexHelper mesh)
        {
            var result = new Vector2[mesh.currentVertCount]; var vertex = new UIVertex();
            for (int i = 0; i < result.Length; i++) { mesh.PopulateUIVertex(ref vertex, i); result[i] = (Vector2)vertex.uv0; }
            return result;
        }

        static bool ContainsSpriteQuad(IReadOnlyList<Vector2> coordinates, Sprite sprite)
        {
            var r = sprite.rect; float width = sprite.texture.width, height = sprite.texture.height;
            var corners = new[] { new Vector2(r.xMin / width, r.yMin / height), new Vector2(r.xMax / width, r.yMin / height),
                new Vector2(r.xMax / width, r.yMax / height), new Vector2(r.xMin / width, r.yMax / height) };
            for (int first = 0; first + 3 < coordinates.Count; first++)
            {
                bool match = true;
                for (int corner = 0; corner < 4; corner++)
                    match &= Vector2.Distance(coordinates[first + corner], corners[corner]) < .000001f;
                if (match) return true;
            }
            return false;
        }

        [Test] public void DormantRailStrokesStayStoredButDoNotAppearWithoutTheModule()
        {
            var stroke = new DrawingStroke(new[] { new DrawingPoint(-.7f, -.4f), new DrawingPoint(.6f, .5f) });
            var drawing = new DrawingSnapshot(new[] { new DrawingPart((DrawingPartKey)7, new[] { stroke }) });
            var bp = new TowerBlueprint("dormant-rail", "Dormant", ChassisKind.Standard, StructureKind.Gun,
                drawing: drawing, origin: DrawingOrigin.PlayerDrawing);
            var balance = GameBalance.CreateDefault(); balance.StartingInk = 1000;
            var game = new GameSession(balance, starterDefenses: false);
            Assert.That(game.TryBuildBlueprint(new[] { new Cell(10, 10) }, bp, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
            for (int i = 0; i < 90; i++) game.Tick(1f / 30);
            var mesh = new Mesh();
            var root = new GameObject("Dormant rail thumbnail", typeof(RectTransform), typeof(CanvasRenderer), typeof(BlueprintSketch));
            try
            {
                var pen = new MeshPen(mesh); DoodleGlyphs.StructureBase(pen, new RunReadModel(game).Structures[0]); pen.Upload();
                Assert.That(mesh.vertexCount, Is.Zero, "Disabled equipment has no rendered static geometry.");
                var sketch = root.GetComponent<BlueprintSketch>(); sketch.SetBlueprint(bp);
                using (var vh = new VertexHelper())
                {
                    typeof(BlueprintSketch).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Invoke(sketch, new object[] { vh });
                    Assert.That(vh.currentVertCount, Is.Zero);
                }
                Assert.That(bp.Drawing.PointCount, Is.EqualTo(2), "Rendering must not rewrite stored strokes.");
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(root); }
        }
    }
}
