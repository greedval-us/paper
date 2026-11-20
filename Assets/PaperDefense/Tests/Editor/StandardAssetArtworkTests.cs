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
    public sealed class StandardAssetArtworkTests
    {
        [TestCase("weapon.gun")] [TestCase("weapon.piercer")] [TestCase("weapon.mortar")]
        [TestCase("weapon.fan")] [TestCase("weapon.ricochet")] [TestCase("weapon.stapler")]
        [TestCase("weapon.blotter")] [TestCase("weapon.rail")]
        [TestCase("chassis.standard")] [TestCase("chassis.heavy")]
        [TestCase("module.armor")] [TestCase("module.range")]
        [TestCase("object.wall.0")] [TestCase("object.wall.15")]
        [TestCase("object.spikes")] [TestCase("object.tar")] [TestCase("object.repair")]
        [TestCase("object.generator")] [TestCase("object.beacon")]
        public void StockSemanticArtworkUsesTheApprovedNativeStandardWithoutWhiteBoxes(string id)
        {
            var shape = NotebookVisualThemeAsset.Default.FindShape(id);
            Assert.That(shape, Is.Not.Null, id);
            Assert.That(shape.TemplateId, Does.StartWith(AssemblyVisualComposer.Revision + "/"),
                "Stock objects must identify the shared standard artwork source: " + id);
            Assert.That(shape.Marks.Any(mark => mark.Channel == NotebookInkChannel.White && mark.Alpha > .7f), Is.False,
                "A missing atlas symbol must not become an opaque white placeholder: " + id);
            using (var target = new VertexHelper())
            {
                Assert.That(NotebookArtRenderer.DrawUI(target, shape, Vector2.zero, 1), Is.True);
                Assert.That(target.currentVertCount, Is.InRange(36, 12000), "A bounded, recognizable drawing is required: " + id);
                var vertex = new UIVertex();
                for (int index = 0; index < target.currentVertCount; index++)
                {
                    target.PopulateUIVertex(ref vertex, index);
                    Assert.That(float.IsNaN(vertex.position.x) || float.IsInfinity(vertex.position.x) ||
                        float.IsNaN(vertex.position.y) || float.IsInfinity(vertex.position.y), Is.False);
                }
            }
            Assert.That(NotebookVisualThemeAsset.Default.FindShape(id), Is.SameAs(shape), "Semantic shapes are cached after construction.");
        }

        [TestCase("module.tracking")] [TestCase("weapon.ricochet")] [TestCase("weapon.mortar")]
        public void EveryOpaqueIvoryContourRetainsStrokeIntentForTheWorldPixelFloor(string id)
        {
            var shape = NotebookVisualThemeAsset.Default.FindShape(id);
            Assert.That(shape, Is.Not.Null, id);
            var contours = shape.Marks.Where(mark => mark.Channel == NotebookInkChannel.Ivory && mark.Alpha >= .98f).ToArray();
            Assert.That(contours.Length, Is.GreaterThanOrEqualTo(12), "Exercise the actual circular contours: " + id);
            Assert.That(contours.All(mark => mark.Primitive == NotebookArtPrimitive.Polyline), Is.True,
                "Every opaque ivory rim must retain its stroke centerline and width, so the world pixel floor applies at small scales: " + id);
        }

        [TestCase("weapon.gun", "ui.weapon.gun")]
        [TestCase("chassis.standard", "ui.chassis.standard")]
        [TestCase("module.armor", "ui.module.armor")]
        [TestCase("object.wall.0", "ui.tool.wall")]
        [TestCase("object.spikes", "ui.tool.spikes")]
        [TestCase("object.tar", "ui.tool.tar")]
        [TestCase("object.repair", "ui.tool.repair")]
        [TestCase("object.wall.0", "ui.structure.wall")]
        [TestCase("object.tar", "ui.structure.slow")]
        [TestCase("object.repair", "ui.structure.repair")]
        [TestCase("object.generator", "ui.structure.generator")]
        [TestCase("object.spikes", "ui.structure.spikes")]
        [TestCase("object.beacon", "ui.structure.beacon")]
        public void LegacyAndUiFamilyAliasesShowTheSameStandardDrawing(string world, string ui)
        {
            var a = NotebookVisualThemeAsset.Default.FindShape(world);
            var b = NotebookVisualThemeAsset.Default.FindShape(ui);
            Assert.That(a, Is.Not.Null); Assert.That(b, Is.Not.Null);
            Assert.That(a.TemplateId, Is.Not.Null.And.EqualTo(b.TemplateId));
            Assert.That(Fingerprint(a), Is.EqualTo(Fingerprint(b)), "UI and world aliases must not drift into different assemblies.");
        }

        [TestCase("object.missing-art")] [TestCase("weapon.future-unknown")]
        [TestCase("ui.tool.missing")] [TestCase("chassis.unknown")] [TestCase("module.unknown")]
        public void UnknownArtworkIsExplicitlyAbsentInsteadOfAnUnlabelledWrongAssembly(string id)
            => Assert.That(NotebookVisualThemeAsset.Default.FindShape(id), Is.Null);

        [TestCase("object.tar", NotebookInkChannel.GlassInk)]
        [TestCase("object.repair", NotebookInkChannel.Danger)]
        [TestCase("object.generator", NotebookInkChannel.Copper)]
        [TestCase("object.spikes", NotebookInkChannel.BladeSteel)]
        [TestCase("object.beacon", NotebookInkChannel.Clean)]
        public void StockSupportRetainsTheWorkingMaterialColorFromItsRecipe(string id, NotebookInkChannel channel)
        {
            var shape = NotebookVisualThemeAsset.Default.FindShape(id);
            Assert.That(shape.Marks.Any(mark => mark.Channel == channel && mark.Alpha > .8f), Is.True, id);
        }

        [Test] public void StockSupportRolesRemainVisuallyDistinct()
        {
            var prints = new[] { "object.wall.0", "object.spikes", "object.tar", "object.repair", "object.generator", "object.beacon" }
                .Select(id => Fingerprint(NotebookVisualThemeAsset.Default.FindShape(id))).ToArray();
            Assert.That(prints.Distinct().Count(), Is.EqualTo(prints.Length), "Each support role needs its own drawing.");
        }

        [Test] public void LegacyWeaponsRetainTheApprovedPartGeometryAtTheirDeclaredMuzzle()
        {
            foreach (string family in new[] { "gun", "piercer", "mortar", "fan", "ricochet", "stapler", "blotter" })
            {
                var weapon = new WeaponRecipe(new PartRef("core." + family, 1), new PartRef("head." + family, 1),
                    new PartRef("sight.iron", 1), new PartRef("magazine.standard", 1), new PartRef("trigger.standard", 1));
                var approved = AssemblyVisualComposer.ComposeWeapon(weapon);
                var pen = new MeshPen(null); approved.Draw(pen, Vector2.zero);
                using (var native = new VertexHelper())
                using (var semantic = new VertexHelper())
                {
                    pen.CopyTo(native, Vector2.zero, 1);
                    var shape = NotebookVisualThemeAsset.Default.FindShape("weapon." + family);
                    Assert.That(shape.Marks.Any(mark => mark.Primitive == NotebookArtPrimitive.Polyline), Is.True,
                        "Stock contours must retain stroke intent for the world pixel floor.");
                    NotebookArtRenderer.DrawUI(semantic, shape, Vector2.zero, 1);
                    var expected = new List<UIVertex>(); var actual = new List<UIVertex>();
                    native.GetUIVertexStream(expected); semantic.GetUIVertexStream(actual);
                    var leadingInk = expected.Where(vertex => vertex.color.Equals((Color32)NightNotebookArt.Ink)).ToArray();
                    float front = leadingInk.Max(vertex => vertex.position.y);
                    var cap = leadingInk.Where(vertex => Mathf.Abs(vertex.position.y - front) < .000001f).ToArray();
                    Vector3 offset = new Vector2(-(cap.Min(vertex => vertex.position.x) + cap.Max(vertex => vertex.position.x)) * .5f, .35f - front);
                    Assert.That(actual.Count, Is.EqualTo(expected.Count), family);
                    for (int index = 0; index < expected.Count; index++)
                    {
                        Assert.That(Vector3.Distance(actual[index].position, expected[index].position + offset), Is.LessThan(.00001f), family);
                        Assert.That(actual[index].color.r, Is.EqualTo(expected[index].color.r), family);
                        Assert.That(actual[index].color.g, Is.EqualTo(expected[index].color.g), family);
                        Assert.That(actual[index].color.b, Is.EqualTo(expected[index].color.b), family);
                        Assert.That(Mathf.Abs(actual[index].color.a - expected[index].color.a), Is.LessThanOrEqualTo(1), family);
                    }
                }
            }
        }

        [TestCase("weapon.gun")] [TestCase("chassis.standard")] [TestCase("object.repair")]
        public void StandardMaterialSurvivesSemanticAliasAndThemeRoundTrip(string id)
        {
            var restored = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            var mesh = new Mesh();
            try
            {
                restored.ReplaceData(JsonUtility.FromJson<NotebookArtSource>(
                    JsonUtility.ToJson(NotebookVisualThemeAsset.Default.Data)));
                var shape = restored.FindShape(id);
                Assert.That(shape.Marks.Any(mark => mark.Channel == NotebookInkChannel.Body), Is.True,
                    "Standard material must retain its own semantic channel instead of becoming ivory.");
                var pen = new MeshPen(mesh);
                NotebookArtRenderer.Draw(pen, shape, Vector2.zero, theme: restored);
                pen.Upload();
                using (var ui = new VertexHelper())
                {
                    NotebookArtRenderer.DrawUI(ui, shape, Vector2.zero, 1, theme: restored);
                    var vertices = new List<UIVertex>(); ui.GetUIVertexStream(vertices);
                    Color32 body = NightNotebookArt.Body;
                    Assert.That(vertices.Any(vertex => vertex.color.r == body.r && vertex.color.g == body.g &&
                        vertex.color.b == body.b && vertex.color.a >= 180), Is.True,
                        "Card rendering must preserve the opaque sage material after serialization.");
                    Assert.That(mesh.colors32.Any(color => color.r == body.r && color.g == body.g &&
                        color.b == body.b && color.a >= 180), Is.True,
                        "World aliases and cards must render the same material.");
                }
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(restored); }
        }

        [Test] public void EightChannelCustomThemesRetainTheirOwnColorsAndSuppliedShapes()
        {
            var custom = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            try
            {
                var owned = new NotebookArtShape { Id = "weapon.gun", Marks = { new NotebookArtMark {
                    Primitive = NotebookArtPrimitive.Polyline, Channel = NotebookInkChannel.Navy,
                    Width = .025f, Points = new[] { new Vector2(-.3f, -.1f), new Vector2(.23f, .41f) } } } };
                var palette = Enumerable.Repeat(Color.magenta, 8).ToArray();
                palette[0] = Color.cyan;
                var data = new NotebookArtSource { Revision = "caller-owned", Palette = palette }; data.Shapes.Add(owned);
                custom.ReplaceData(data);
                Assert.That(custom.FindShape(owned.Id), Is.SameAs(owned));
                Assert.That(owned.Marks[0].Points[1], Is.EqualTo(new Vector2(.23f, .41f)));
                var fallback = custom.FindShape("chassis.standard");
                Assert.That(fallback, Is.Not.Null);
                Assert.That(fallback.Marks.All(mark => (int)mark.Channel < 8), Is.True,
                    "Missing symbols must adapt to the caller's existing palette instead of adding white fallback channels.");
                string standardBefore = Fingerprint(NotebookVisualThemeAsset.Default.FindShape("chassis.standard"));
                fallback.Marks[0].Points[0] += Vector2.one;
                Assert.That(Fingerprint(NotebookVisualThemeAsset.Default.FindShape("chassis.standard")), Is.EqualTo(standardBefore),
                    "Editing a caller-owned fallback must not mutate the shared standard source.");
                using (var target = new VertexHelper())
                {
                    NotebookArtRenderer.DrawUI(target, owned, Vector2.zero, 1, theme: custom);
                    var vertex = new UIVertex(); target.PopulateUIVertex(ref vertex, 0);
                    Assert.That((Color)vertex.color, Is.EqualTo(Color.cyan));
                }
                Assert.That(custom.Data.Palette, Is.SameAs(palette));
            }
            finally { Object.DestroyImmediate(custom); }
        }

        [Test] public void AuthoredHistoricalLinesRemainExactWhenStockArtIsReplaced()
        {
            var snapshot = new DrawingSnapshot(new[] {
                new DrawingPart(DrawingBindings.Base, new[] { new DrawingStroke(new[] {
                    new DrawingPoint(-.4f, -.3f), new DrawingPoint(.2f, -.3f), new DrawingPoint(.1f, .45f) }) }),
                new DrawingPart(DrawingBindings.Mount0, new[] { new DrawingStroke(new[] {
                    new DrawingPoint(-.08f, 0), new DrawingPoint(.12f, .24f), new DrawingPoint(.03f, .61f) }) }) });
            var blueprint = new TowerBlueprint("authored.stock-check", "Saved drawing", ChassisKind.Standard,
                StructureKind.Gun, drawing: snapshot, origin: DrawingOrigin.PlayerDrawing);
            string before = JsonUtility.ToJson(new BlueprintData(blueprint));
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh);
                NotebookTowerComposition.DrawStatic(pen, blueprint, Vector2.zero, MaterialTier.Pencil, InkColor.None);
                pen.Upload();
                Assert.That(mesh.vertexCount, Is.EqualTo(8), "Only the player's two body segments should be rendered.");
                var canonicalStart = new DrawingPoint(-.4f, -.3f); var canonicalEnd = new DrawingPoint(.2f, -.3f);
                Assert.That(Vector2.Distance((mesh.vertices[0] + mesh.vertices[3]) * .5f, new Vector2(canonicalStart.X, canonicalStart.Y)), Is.LessThan(.00001f));
                Assert.That(Vector2.Distance((mesh.vertices[1] + mesh.vertices[2]) * .5f, new Vector2(canonicalEnd.X, canonicalEnd.Y)), Is.LessThan(.00001f));
                Assert.That(JsonUtility.ToJson(new BlueprintData(blueprint)), Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        static string Fingerprint(NotebookArtShape shape)
            => string.Join("|", shape.Marks.Select(mark => ((int)mark.Primitive) + ":" + ((int)mark.Channel) + ":" + mark.Width + ":" + mark.Alpha + ":" +
                string.Join(";", mark.Points.Select(point => point.x + "," + point.y))));
    }
}
