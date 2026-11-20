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

        [Test] public void RadialOnlyBlueprintPreviewKeepsThePlacedFoundationOrientation()
        {
            var balance = GameBalance.CreateDefault();
            balance.StartingInk = 1000;
            var input = LegacyContentCatalog.CreateInput(balance);
            const string template = "template.radial-preview";
            input.Templates.Add(new DrawingTemplateDefinitionInput { Id = template, DisplayName = "Preview", Weight = 0 });
            foreach (var part in input.Chassis) part.TemplateId = template;
            foreach (var part in input.Weapons) part.TemplateId = template;
            foreach (var part in input.Modules) part.TemplateId = template;
            var weapon = input.Weapons.Single(w => w.Id == "weapon.gun");
            weapon.Attack = AttackKind.Radial;
            weapon.Stats.Radius = 3;
            balance.Content = input.CreateSnapshot();
            var game = new GameSession(balance, starterDefenses: false);
            var cell = new Cell(10, 10);
            var blueprint = new TowerBlueprint("radial", "Radial", "chassis.standard", weapon.Id, content: balance.Content);
            Assert.That(game.TryBuildBlueprint(new[] { cell }, blueprint, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
            var model = new RunReadModel(game);
            Assert.That(model.StructureAt(cell).Value.HasDirectionalWeapon, Is.False);
            Assert.That(NotebookMountLayout.BodyRadians(model.StructureAt(cell).Value), Is.Zero);
            var root = new GameObject("Radial foundation preview");
            try
            {
                var view = root.AddComponent<NotebookWorldView>();
                view.Initialize(model);
                var mesh = root.GetComponentsInChildren<MeshFilter>().Single(filter => filter.name == "Pencil building preview").sharedMesh;
                void Show(float? heading) => view.SetPreview(new[] { cell }, StructureKind.Gun, MaterialTier.Pencil, true,
                    blueprint: blueprint, facingDegrees: heading);
                Show(90); // Authored upright reference pose.
                var upright = mesh.vertices;
                Assert.That(upright.Length, Is.GreaterThan(4));
                Show(null);
                Assert.That(mesh.vertices, Is.EqualTo(upright), "Hover must match the upright placed foundation.");
                Show(233);
                Assert.That(mesh.vertices, Is.EqualTo(upright), "A radial-only mount has no selectable heading.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(1, 4)]
        [TestCase(8, 2)]
        public void FieldRangeSectorUsesEffectiveAssemblyRange(int kindValue, float rawRange)
        {
            var kind = (StructureKind)kindValue;
            var balance = GameBalance.CreateDefault(); balance.StartingInk = 1000;
            balance.Structure(kind).Range = rawRange; balance.Structure(kind).BuildSeconds = 0;
            var game = new GameSession(balance, starterDefenses: false);
            var bp = new TowerBlueprint("rail-ring", "Rail", ChassisKind.Standard, kind, modules: Rail);
            var cell = new Cell(10, 10);
            const float heading = 237;
            Assert.That(game.TryBuildBlueprint(new[] { cell }, bp, MaterialTier.Pencil, InkColor.None, out var reason, heading), Is.True, reason);
            var root = new GameObject("Isolated rail range mesh");
            try
            {
                var view = root.AddComponent<NotebookWorldView>(); view.ShowRanges = true; view.Initialize(new RunReadModel(game));
                var mesh = root.GetComponentsInChildren<MeshFilter>().Single(f => f.name == "Castle, walls and weapon bases").sharedMesh;
                var vertices = mesh.vertices;
                var weapon = game.Balance.Content.GetWeapon(bp.FirstWeaponId);
                Vector2 origin = new Vector2(cell.X + .5f, cell.Y + .5f);
                // The first stroke is the lower edge of the firing sector. Averaging across
                // its thickness recovers its true outer endpoint even with anti-aliasing.
                Vector2 outer = (vertices[1] + vertices[2]) * .5f;
                Vector2 edge = outer - origin;
                Assert.That(edge.magnitude, Is.EqualTo(rawRange * 1.25f).Within(.0001f));
                float edgeDegrees = Mathf.Atan2(edge.y, edge.x) * Mathf.Rad2Deg;
                Assert.That(Mathf.DeltaAngle(heading - weapon.FiringArcDegrees * .5f, edgeDegrees),
                    Is.EqualTo(0f).Within(.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void UpgradedLegacyTowerShowsItsNewRangeInBothFieldOverlays()
        {
            var balance = GameBalance.CreateDefault();
            balance.Mechanics.FieldModernization = true;
            balance.StartingInk = 1000;
            var game = new GameSession(balance, starterDefenses: false);
            var blueprint = new TowerBlueprint("rail-gun", "Rail gun", ChassisKind.Standard, StructureKind.Gun, modules: Rail);
            var cell = new Cell(10, 10);
            Assert.That(game.TryBuildBlueprint(new[] { cell }, blueprint, MaterialTier.Pencil, InkColor.None, out var reason, 237), Is.True, reason);
            var tower = game.StructureAt(cell);
            game.Tick(tower.BuildRemaining + .1f);
            var purchase = game.QuoteModernization(tower.Id, ModernizationDirection.Range);
            Assert.That(game.TryModernize(purchase, out reason), Is.True, purchase.Reason);
            game.Tick(purchase.WorkSeconds + .1f);
            float original = purchase.Before.Weapons[0].Range;
            float upgraded = purchase.After.Weapons[0].Range;
            Assert.That(upgraded, Is.GreaterThan(original));

            var root = new GameObject("Legacy range inspection");
            try
            {
                var view = root.AddComponent<NotebookWorldView>();
                view.ShowRanges = true;
                view.Initialize(new RunReadModel(game));
                var origin = new Vector2(cell.X + .5f, cell.Y + .5f);
                float VisibleRange(string layer)
                {
                    var mesh = root.GetComponentsInChildren<MeshFilter>().Single(f => f.name == layer).sharedMesh;
                    var vertices = mesh.vertices;
                    Vector2 edge = (vertices[1] + vertices[2]) * .5f;
                    return Vector2.Distance(origin, edge);
                }
                Assert.That(VisibleRange("Castle, walls and weapon bases"), Is.EqualTo(upgraded).Within(.001f));
                view.SetInspectedSector(cell);
                Assert.That(VisibleRange("Pencil building preview"), Is.EqualTo(upgraded).Within(.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void StandardPageShowsTheEnabledRailGeometryInWorldAndAlbum()
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
                var pen = new MeshPen(mesh);
                DoodleGlyphs.StructureBase(pen, model.Structures[0]); DoodleGlyphs.StructureDynamic(pen, model.Structures[0], 1); pen.Upload();
                int plainWorld = mesh.vertexCount;
                pen.Clear(); DoodleGlyphs.StructureBase(pen, model.Structures[1]); DoodleGlyphs.StructureDynamic(pen, model.Structures[1], 1); pen.Upload();
                int extraWorld = mesh.vertexCount - plainWorld;
                Assert.That(extraWorld, Is.GreaterThan(0), "An enabled rail contributes native module geometry beyond the plain assembly.");
                var worldUvs = new List<Vector2>(); mesh.GetUVs(1, worldUvs);
                Assert.That(worldUvs, Is.All.EqualTo(new Vector2(.5f, .5f)));
                var sketch = root.GetComponent<BlueprintSketch>();
                var populate = typeof(BlueprintSketch).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                using (var vh = new VertexHelper())
                {
                    sketch.SetBlueprint(plain); populate.Invoke(sketch, new object[] { vh });
                    int plainAlbum = vh.currentVertCount;
                    sketch.SetBlueprint(rail); populate.Invoke(sketch, new object[] { vh });
                    Assert.That(vh.currentVertCount - plainAlbum, Is.EqualTo(extraWorld),
                        "The album and world must both retain the same complete additional rail geometry.");
                    Assert.That(UiUvs(vh), Is.All.EqualTo(new Vector2(.5f, .5f)));
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
