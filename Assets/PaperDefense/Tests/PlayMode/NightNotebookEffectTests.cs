#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class NightNotebookEffectTests
    {
        static UIVertex[] Vertices(MeshPen pen)
        {
            using (var target = new VertexHelper())
            {
                pen.CopyTo(target, Vector2.zero, 1);
                var result = new UIVertex[target.currentVertCount];
                for (int i = 0; i < result.Length; i++) target.PopulateUIVertex(ref result[i], i);
                return result;
            }
        }

        static SimulationEvent Event(SimulationEventKind kind, AttackKind attack)
        {
            var events = new SimulationEventBuffer();
            events.Publish(1, kind, new Point2(1, 2), new Point2(5, 6), StructureKind.Mortar,
                radius: 2, attack: attack);
            return events[0];
        }

        static UIVertex[] Effect(SimulationEvent item, bool darkSurface, NotebookVisualThemeAsset theme = null)
        {
            var pen = new MeshPen(null);
            NotebookEffectRenderer.Draw(pen, item, new Vector2(1, 2), .025f, theme, darkSurface);
            return Vertices(pen);
        }

        static void AssertSameGeometryAndAlpha(UIVertex[] light, UIVertex[] dark)
        {
            Assert.That(light, Is.Not.Empty);
            Assert.That(dark.Select(vertex => vertex.position), Is.EqualTo(light.Select(vertex => vertex.position)));
            Assert.That(dark.Select(vertex => vertex.color.a), Is.EqualTo(light.Select(vertex => vertex.color.a)));
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
            float ink = Luminance(color), paper = Luminance(new Color32(20, 44, 38, 255));
            return (Mathf.Max(ink, paper) + .05f) / (Mathf.Min(ink, paper) + .05f);
        }

        [TestCase(SimulationEventKind.Shot, AttackKind.Single)]
        [TestCase(SimulationEventKind.Shot, AttackKind.Line)]
        [TestCase(SimulationEventKind.ProjectileImpact, AttackKind.Single)]
        public void DarkCombatStrokesGainContrastWithoutMovingOrChangingTheirFade(SimulationEventKind kind, AttackKind attack)
        {
            var item = Event(kind, attack);
            var light = Effect(item, false); var dark = Effect(item, true);
            AssertSameGeometryAndAlpha(light, dark);
            Assert.That(dark.All(vertex => Contrast(vertex.color) >= 4.5f), Is.True,
                "Default combat ink must remain readable against the dark squared sheet.");
            Assert.That(dark.Select(vertex => vertex.color), Is.Not.EqualTo(light.Select(vertex => vertex.color)));
        }

        [TestCase("fx.mortar.projectile")]
        [TestCase("fx.spitter.projectile")]
        public void DarkProjectileMarksKeepTheirExactGeometryAndLeaveTheSharedThemeUntouched(string id)
        {
            var theme = NotebookVisualThemeAsset.Default;
            var original = theme.Color(NotebookInkChannel.Navy);
            var lightPen = new MeshPen(null); var darkPen = new MeshPen(null);
            DoodleGlyphs.DrawResource(lightPen, id, new Vector2(3, 4), theme: theme);
            NightNotebookEffectPalette.DrawResource(darkPen, id, new Vector2(3, 4), theme, true);
            var light = Vertices(lightPen); var dark = Vertices(darkPen);
            AssertSameGeometryAndAlpha(light, dark);
            var body = (Color32)theme.Color(NotebookInkChannel.Surface);
            var contours = dark.Where(vertex => vertex.color.r != body.r || vertex.color.g != body.g || vertex.color.b != body.b).ToArray();
            Assert.That(contours.Length, Is.GreaterThanOrEqualTo(6), "Dark ink bodies need a visible contour.");
            Assert.That(contours.All(vertex => Contrast(vertex.color) >= 4.5f), Is.True,
                "Intentional dark fill is masked; every projectile contour must retain high contrast.");
            Assert.That(theme.Color(NotebookInkChannel.Navy), Is.EqualTo(original));
            var repeated = new MeshPen(null);
            DoodleGlyphs.DrawResource(repeated, id, new Vector2(3, 4), theme: theme);
            Assert.That(Vertices(repeated).Select(vertex => vertex.color), Is.EqualTo(light.Select(vertex => vertex.color)));
        }

        [Test] public void ExplicitThemeColorsArePreservedOnBothSurfaces()
        {
            var theme = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            try
            {
                theme.Data.Palette = Enumerable.Repeat(new Color(.91f, .07f, .83f, 1), 8).ToArray();
                var item = Event(SimulationEventKind.Shot, AttackKind.Single);
                var light = Effect(item, false, theme); var dark = Effect(item, true, theme);
                AssertSameGeometryAndAlpha(light, dark);
                Assert.That(dark.Select(vertex => vertex.color), Is.EqualTo(light.Select(vertex => vertex.color)));
                var lightPen = new MeshPen(null); var darkPen = new MeshPen(null);
                DoodleGlyphs.DrawResource(lightPen, "fx.mortar.projectile", Vector2.zero, theme: theme);
                NightNotebookEffectPalette.DrawResource(darkPen, "fx.mortar.projectile", Vector2.zero, theme, true);
                Assert.That(Vertices(darkPen).Select(vertex => vertex.color), Is.EqualTo(Vertices(lightPen).Select(vertex => vertex.color)));
            }
            finally { Object.DestroyImmediate(theme); }
        }

        [TestCase(SimulationEventKind.Shot, AttackKind.Single)]
        [TestCase(SimulationEventKind.Shot, AttackKind.Line)]
        public void ReducedMotionCopyRetainsTheOriginalDarkSurfaceInk(SimulationEventKind kind, AttackKind attack)
        {
            var original = NotebookVisualThemeAsset.Default;
            var copied = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            try
            {
                // Match the runtime accessibility theme copy. Object identity changes;
                // its authored colors and dark-surface policy must survive that copy.
                var data = JsonUtility.FromJson<NotebookArtSource>(JsonUtility.ToJson(original.Data));
                data.ReducedMotion = true;
                copied.ReplaceData(data);
                var item = Event(kind, attack);
                var expected = Effect(item, true, original);
                var actual = Effect(item, true, copied);
                AssertSameGeometryAndAlpha(expected, actual);
                Assert.That(actual.Select(vertex => vertex.color), Is.EqualTo(expected.Select(vertex => vertex.color)));
                Assert.That(actual.All(vertex => Contrast(vertex.color) >= 4.5f), Is.True);
            }
            finally { Object.DestroyImmediate(copied); }
        }
    }
}
#endif
