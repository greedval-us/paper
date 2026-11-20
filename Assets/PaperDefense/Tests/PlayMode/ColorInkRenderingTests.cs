#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class ColorInkRenderingTests
    {
        static IEnumerable<TestCaseData> Attacks()
        {
            foreach (var color in new[] { InkColor.Red, InkColor.Blue, InkColor.Green })
            {
                foreach (var attack in new[] { AttackKind.Single, AttackKind.Line, AttackKind.Cone, AttackKind.Ricochet, AttackKind.Radial, AttackKind.Cleanup })
                    yield return new TestCaseData(SimulationEventKind.Shot, attack, color);
                yield return new TestCaseData(SimulationEventKind.ProjectileImpact, AttackKind.Single, color);
                yield return new TestCaseData(SimulationEventKind.WeaponActionStarted, AttackKind.Single, color);
            }
        }
        static UIVertex[] Vertices(MeshPen pen)
        {
            using (var helper = new VertexHelper())
            {
                pen.CopyTo(helper, Vector2.zero, 1);
                var result = new UIVertex[helper.currentVertCount];
                for (int i = 0; i < result.Length; i++) helper.PopulateUIVertex(ref result[i], i);
                return result;
            }
        }
        static SimulationEvent Event(SimulationEventKind kind, AttackKind attack, InkColor color)
        {
            var events = new SimulationEventBuffer();
            events.Publish(1, kind, new Point2(1, 2), new Point2(5, 6), StructureKind.Gun,
                radius: 1.5f, attack: attack, colorEffect: ColorEffectPayload.Capture(color, new EffectsRules()));
            return events[0];
        }
        static UIVertex[] Draw(SimulationEvent item, bool dark)
        {
            var pen = new MeshPen(null);
            NotebookEffectRenderer.Draw(pen, item, new Vector2(1, 2), .025f, null, dark);
            return Vertices(pen);
        }
        static bool SameRgb(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

        [TestCaseSource(nameof(Attacks))]
        public void AllAttackShapesCarryCapturedInkWithoutMovingTheirHitOrChangingFade(SimulationEventKind kind, AttackKind attack, InkColor color)
        {
            var baseline = Draw(Event(kind, attack, InkColor.None), true);
            var colored = Draw(Event(kind, attack, color), true);
            Assert.That(colored, Is.Not.Empty);
            Assert.That(colored.Select(v => v.position), Is.EqualTo(baseline.Select(v => v.position)));
            Assert.That(colored.Select(v => v.color.a), Is.EqualTo(baseline.Select(v => v.color.a)));
            var expected = (Color32)NightNotebookEffectPalette.ColorInk(color);
            Assert.That(colored.All(v => SameRgb(v.color, expected)), Is.True, "Muzzle, trail and hit must share the active cartridge color.");
            var repeated = Draw(Event(kind, attack, InkColor.None), true);
            Assert.That(repeated.Select(v => v.color), Is.EqualTo(baseline.Select(v => v.color)), "Colored shots cannot mutate cached artwork.");
        }

        [TestCase(InkColor.Red)] [TestCase(InkColor.Blue)] [TestCase(InkColor.Green)]
        public void AirborneShellKeepsItsBodyAndColorsTrailContourAndTargetFootprint(InkColor color)
        {
            var pen = new MeshPen(null);
            NotebookEffectRenderer.DrawProjectile(pen, Vector2.zero, new Vector2(5, 2), .45f, null, 1.5f, 1, color, null, true);
            var vertices = Vertices(pen);
            Color32 ink = NightNotebookEffectPalette.ColorInk(color);
            Color32 body = NotebookVisualThemeAsset.Default.Color(NotebookInkChannel.Surface);
            Assert.That(vertices.Any(v => SameRgb(v.color, body)), Is.True);
            Assert.That(vertices.Count(v => SameRgb(v.color, ink)), Is.GreaterThan(30));
            Assert.That(vertices.All(v => SameRgb(v.color, ink) || SameRgb(v.color, body)), Is.True);
        }

        [TestCase(InkColor.Red)] [TestCase(InkColor.Blue)] [TestCase(InkColor.Green)]
        public void SelectedInkRemainsReadableOnBothPapersAndInReducedMotion(InkColor color)
        {
            var reduced = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            try
            {
                var data = JsonUtility.FromJson<NotebookArtSource>(JsonUtility.ToJson(NotebookVisualThemeAsset.Default.Data));
                data.ReducedMotion = true; reduced.ReplaceData(data);
                foreach (bool dark in new[] { false, true })
                {
                    Color tone = NightNotebookEffectPalette.ColorInk(color, null, dark);
                    Color paper = dark ? NightNotebookArt.Paper : NotebookPalette.Paper;
                    float a = Luminance(tone), b = Luminance(paper);
                    Assert.That((Mathf.Max(a, b) + .05f) / (Mathf.Min(a, b) + .05f), Is.GreaterThanOrEqualTo(4.5f));
                    Assert.That(NightNotebookEffectPalette.ColorInk(color, reduced, dark), Is.EqualTo(tone));
                }
            }
            finally { Object.DestroyImmediate(reduced); }
        }
        static float Luminance(Color c)
        {
            float Linear(float v) => v <= .04045f ? v / 12.92f : Mathf.Pow((v + .055f) / 1.055f, 2.4f);
            return Linear(c.r) * .2126f + Linear(c.g) * .7152f + Linear(c.b) * .0722f;
        }
    }
}
#endif
