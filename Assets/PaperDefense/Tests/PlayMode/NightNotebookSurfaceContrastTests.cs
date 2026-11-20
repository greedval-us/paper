#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class NightNotebookSurfaceContrastTests
    {
        static UIVertex[] Vertices(MeshPen pen)
        {
            using (var target = new VertexHelper())
            {
                pen.CopyTo(target, Vector2.zero, 1);
                var vertices = new UIVertex[target.currentVertCount];
                for (int i = 0; i < vertices.Length; i++) target.PopulateUIVertex(ref vertices[i], i);
                return vertices;
            }
        }

        static float Luminance(Color32 color)
        {
            float Linear(byte value)
            {
                float channel = value / 255f;
                return channel <= .04045f ? channel / 12.92f : Mathf.Pow((channel + .055f) / 1.055f, 2.4f);
            }
            return Linear(color.r) * .2126f + Linear(color.g) * .7152f + Linear(color.b) * .0722f;
        }

        static float Contrast(Color32 color)
        {
            float ink = Luminance(color), paper = Luminance(NightNotebookArt.Paper);
            return (Mathf.Max(ink, paper) + .05f) / (Mathf.Min(ink, paper) + .05f);
        }

        static void AssertSameGeometryAndAlpha(UIVertex[] light, UIVertex[] dark)
        {
            Assert.That(light, Is.Not.Empty);
            Assert.That(dark.Select(vertex => vertex.position), Is.EqualTo(light.Select(vertex => vertex.position)));
            Assert.That(dark.Select(vertex => vertex.color.a), Is.EqualTo(light.Select(vertex => vertex.color.a)));
        }

        static BattlefieldView Map(TerrainPropKind? prop = null, bool road = false)
        {
            var map = new BattlefieldDefinition { Enabled = true, Mode = BattlefieldMode.OpenTerrain };
            map.Entrances.Add(new BattlefieldCell(0, 5));
            if (prop.HasValue) map.Props.Add(new TerrainPropDefinition { Kind = prop.Value, X = 2, Y = 2, Width = 2, Height = 2 });
            if (road) map.Roads.Add(new BattlefieldCell(8, 2));
            return map.Resolve(12, 12, new Cell(6, 6));
        }

        static UIVertex[] Battlefield(BattlefieldView map, bool darkSurface, NotebookVisualThemeAsset theme = null)
        {
            var pen = new MeshPen(null);
            NotebookBattlefieldRenderer.Draw(pen, map, theme, darkSurface);
            return Vertices(pen);
        }

        [TestCase(TerrainPropKind.Mountain)] [TestCase(TerrainPropKind.Pine)]
        [TestCase(TerrainPropKind.Broadleaf)] [TestCase(TerrainPropKind.Rock)] [TestCase(TerrainPropKind.Shrub)]
        public void DarkTerrainKeepsItsOccupiedSilhouetteAndBrightensOnlyDefaultInk(TerrainPropKind kind)
        {
            var theme = NotebookVisualThemeAsset.Default;
            Color32 original = theme.Color(NotebookInkChannel.Navy);
            var map = Map(kind);
            var light = Battlefield(map, false); var dark = Battlefield(map, true);
            AssertSameGeometryAndAlpha(light, dark);
            Color32 ivory = theme.Color(NotebookInkChannel.Ivory);
            var ink = Enumerable.Range(0, light.Length).Where(i => light[i].color.r == original.r &&
                light[i].color.g == original.g && light[i].color.b == original.b ||
                light[i].color.r == ivory.r && light[i].color.g == ivory.g && light[i].color.b == ivory.b).ToArray();
            Assert.That(ink, Is.Not.Empty);
            Assert.That(ink.All(i => Contrast(dark[i].color) >= 4.5f), Is.True,
                "Occupied terrain must stay visible on the dark playable sheet.");
            Assert.That(theme.Color(NotebookInkChannel.Navy), Is.EqualTo((Color)original));
            Assert.That(Battlefield(map, false).Select(vertex => vertex.color), Is.EqualTo(light.Select(vertex => vertex.color)));
            Assert.That(map.IsReservedTerrain(new Cell(2, 2)), Is.True);
        }

        [Test] public void DarkRoadsKeepTheirGeometryAndTheLightMapDefaultRemainsUnchanged()
        {
            var map = Map(road: true);
            var light = Battlefield(map, false); var dark = Battlefield(map, true);
            AssertSameGeometryAndAlpha(light, dark);
            Assert.That(dark.Select(vertex => vertex.color), Is.Not.EqualTo(light.Select(vertex => vertex.color)));
            var defaultPen = new MeshPen(null);
            NotebookBattlefieldRenderer.Draw(defaultPen, map, null);
            Assert.That(Vertices(defaultPen).Select(vertex => vertex.color), Is.EqualTo(light.Select(vertex => vertex.color)));
        }

        [Test] public void PersistentTrialBlotsGainContrastWithoutChangingTheirShapeOrAlpha()
        {
            var blot = new BlotState();
            typeof(BlotState).GetProperty(nameof(BlotState.Cell)).SetValue(blot, new Cell(12, 12));
            typeof(BlotState).GetProperty(nameof(BlotState.Remaining)).SetValue(blot, 6f);
            var frame = (TrialFrame)typeof(TrialFrame).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single()
                .Invoke(new object[] { 0f, Array.Empty<EnemyState>(), Array.Empty<StructureState>(),
                    Array.Empty<MortarProjectile>(), Array.Empty<ShotEvent>(),
                    new[] { blot }, null, null, 0L, TrialModernizationConfiguration.Baseline });
            var report = new TrialReport { DurationSeconds = 10 };
            typeof(TrialReport).GetProperty(nameof(TrialReport.Frames)).SetValue(report, new[] { frame });
            var playback = new NotebookTrialPlayback(); playback.Bind(report); playback.Seek(0);
            var lightPen = new MeshPen(null); var darkPen = new MeshPen(null);
            NotebookTrialRenderer.Draw(lightPen, playback, new Rect(8, 8, 10, 10), 0);
            NotebookTrialRenderer.Draw(darkPen, playback, new Rect(8, 8, 10, 10), 0, true);
            var light = Vertices(lightPen); var dark = Vertices(darkPen);
            AssertSameGeometryAndAlpha(light, dark);
            var visible = dark.Where(vertex => vertex.color.a > 0).ToArray();
            Assert.That(visible, Is.Not.Empty);
            Color32 body = NotebookVisualThemeAsset.Default.Color(NotebookInkChannel.Surface);
            var contour = visible.Where(vertex => vertex.color.r != body.r || vertex.color.g != body.g || vertex.color.b != body.b).ToArray();
            Assert.That(contour.Length, Is.GreaterThanOrEqualTo(6), "Persistent dark ink needs a visible edge.");
            Assert.That(contour.All(vertex => Contrast(vertex.color) >= 4.5f), Is.True,
                "The persistent cleanup target must be readable after its transient birth effect ends.");
            Assert.That(visible.Any(vertex => vertex.color.r == body.r && vertex.color.g == body.g && vertex.color.b == body.b), Is.True,
                "A dark organic pool keeps its ink body instead of brightening every fill.");
        }

        [Test] public void ExplicitTerrainAndBlotThemesKeepTheirColorsOnBothSurfaces()
        {
            var theme = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            try
            {
                theme.Data.Palette = Enumerable.Repeat(new Color(.91f, .07f, .83f, 1), 8).ToArray();
                var map = Map(TerrainPropKind.Pine, true);
                var light = Battlefield(map, false, theme); var dark = Battlefield(map, true, theme);
                AssertSameGeometryAndAlpha(light, dark);
                Assert.That(dark.Select(vertex => vertex.color), Is.EqualTo(light.Select(vertex => vertex.color)));
                var lightPen = new MeshPen(null); var darkPen = new MeshPen(null);
                DoodleGlyphs.DrawResource(lightPen, "object.blot", Vector2.zero, opacity: .47f, theme: theme);
                NightNotebookEffectPalette.DrawResource(darkPen, "object.blot", Vector2.zero, theme, true, opacity: .47f);
                light = Vertices(lightPen); dark = Vertices(darkPen);
                AssertSameGeometryAndAlpha(light, dark);
                Assert.That(dark.Select(vertex => vertex.color), Is.EqualTo(light.Select(vertex => vertex.color)));
            }
            finally { Object.DestroyImmediate(theme); }
        }
    }
}
#endif
