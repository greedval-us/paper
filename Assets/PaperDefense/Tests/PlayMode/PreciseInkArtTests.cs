#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    /// <summary>Structural readability of the approved precise ink drawings in their native view.</summary>
    public sealed class PreciseInkArtTests
    {
        static PartRef P(string id) => new PartRef(id, 1);

        static ComponentCatalog Catalog()
        {
            var asset = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(
                "Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset");
            Assert.That(asset, Is.Not.Null);
            return asset.CreateSnapshot();
        }

        static WeaponRecipe Gun(string head, string magazine, string sight)
            => new WeaponRecipe(P("core.gun"), P(head), P(sight), P(magazine),
                P("trigger.standard"), P("mount.standard"));

        static UIVertex[] Draw(AssemblyVisualComposition composition)
        {
            var pen = new MeshPen(null);
            composition.Draw(pen, Vector2.zero);
            using (var target = new VertexHelper())
            {
                pen.CopyTo(target, Vector2.zero, 1);
                var vertices = new UIVertex[target.currentVertCount];
                for (int i = 0; i < vertices.Length; i++) target.PopulateUIVertex(ref vertices[i], i);
                return vertices;
            }
        }

        static bool IsIvory(UIVertex vertex)
        {
            Color32 ink = NightNotebookArt.Ink;
            return vertex.color.r == ink.r && vertex.color.g == ink.g && vertex.color.b == ink.b;
        }

        static Rect ContourBounds(string id)
        {
            var vertices = Draw(AssemblyVisualComposer.ComposePart(P(id)))
                .Where(vertex => IsIvory(vertex) && vertex.color.a >= 250).ToArray();
            Assert.That(vertices, Is.Not.Empty, id + " needs a visible ivory contour.");
            return Rect.MinMaxRect(vertices.Min(vertex => vertex.position.x), vertices.Min(vertex => vertex.position.y),
                vertices.Max(vertex => vertex.position.x), vertices.Max(vertex => vertex.position.y));
        }

        static float[] StrokeWidths(IEnumerable<UIVertex> source)
        {
            // Ivory line primitives are emitted as four corners. Paper masks and pencil hatching
            // are excluded by the caller so their triangles cannot masquerade as thin seams.
            var vertices = source.ToArray();
            Assert.That(vertices.Length % 4, Is.Zero);
            return Enumerable.Range(0, vertices.Length / 4)
                .Select(index => Vector3.Distance(vertices[index * 4].position, vertices[index * 4 + 3].position))
                .ToArray();
        }

        // Frozen native mechanical envelopes. Art must fit these bands; resizing a part's
        // Region to accommodate a drawing would change the compositor's mechanical contract.
        static Rect Envelope(ComponentCategory category)
        {
            switch (category)
            {
                case ComponentCategory.Frame: return new Rect(-.45f, -.3f, .9f, .12f);
                case ComponentCategory.Hull: return new Rect(-.38f, -.22f, .76f, .2f);
                case ComponentCategory.Armor: return new Rect(-.2f, -.22f, .4f, .12f);
                case ComponentCategory.Feed: return new Rect(-.05f, -.22f, .1f, .13f);
                case ComponentCategory.Turn: case ComponentCategory.Mount: return new Rect(-.065f, -.065f, .13f, .13f);
                case ComponentCategory.Mechanism: return new Rect(-.09f, -.07f, .18f, .16f);
                case ComponentCategory.Head: return new Rect(-.035f, .04f, .07f, .26f);
                case ComponentCategory.Sight: return new Rect(-.13f, .03f, .055f, .055f);
                case ComponentCategory.Magazine: return new Rect(-.12f, -.09f, .09f, .08f);
                case ComponentCategory.Trigger: return new Rect(.07f, -.04f, .04f, .07f);
                case ComponentCategory.Site: return new Rect(-.4f, -.35f, .8f, .13f);
                case ComponentCategory.Function: return new Rect(-.25f, -.2f, .5f, .45f);
                case ComponentCategory.Shell: return new Rect(-.3f, -.25f, .6f, .11f);
                case ComponentCategory.Utility: return new Rect(-.12f, -.12f, .24f, .24f);
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        static void AssertVisibleFiniteAndInside(UIVertex[] vertices, Rect envelope, string subject)
        {
            Assert.That(vertices, Is.Not.Empty, subject);
            Assert.That(vertices.Any(vertex => vertex.color.a > 0), Is.True, subject);
            foreach (var vertex in vertices)
            {
                var position = vertex.position;
                Assert.That(float.IsNaN(position.x) || float.IsInfinity(position.x) ||
                    float.IsNaN(position.y) || float.IsInfinity(position.y) ||
                    float.IsNaN(position.z) || float.IsInfinity(position.z), Is.False, subject);
                Assert.That(position.x, Is.InRange(envelope.xMin - .00001f, envelope.xMax + .00001f), subject);
                Assert.That(position.y, Is.InRange(envelope.yMin - .00001f, envelope.yMax + .00001f), subject);
            }
        }

        static void AssertEnvelope(Rect actual, Rect expected, string subject)
        {
            // A union reconstructs width/height by subtraction, adding ordinary float roundoff.
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(.000001f), subject + " x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(.000001f), subject + " y");
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(.000001f), subject + " width");
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(.000001f), subject + " height");
        }

        static IEnumerable<TestCaseData> GunCombinations()
        {
            foreach (var head in new[] { "head.gun", "head.short", "head.long" })
                foreach (var magazine in new[] { "magazine.standard", "magazine.drum", "magazine.belt" })
                    foreach (var sight in new[] { "sight.iron", "sight.scope", "sight.tracking" })
                        yield return new TestCaseData(head, magazine, sight);
        }

        static (Vector2 a, Vector2 b)[] NormalizedContour(string id, ComponentCategory category)
        {
            var vertices = Draw(AssemblyVisualComposer.ComposePart(P(id)))
                .Where(vertex => IsIvory(vertex) && vertex.color.a >= 250).ToArray();
            Assert.That(vertices, Is.Not.Empty, id);
            Assert.That(vertices.Length % 4, Is.Zero, id);
            var bounds = Envelope(category);
            Vector2 Normalize(Vector3 point) => new Vector2((point.x - bounds.xMin) / bounds.width,
                (point.y - bounds.yMin) / bounds.height);
            return Enumerable.Range(0, vertices.Length / 4).Select(index =>
            {
                int first = index * 4;
                return (Normalize((vertices[first].position + vertices[first + 3].position) * .5f),
                    Normalize((vertices[first + 1].position + vertices[first + 2].position) * .5f));
            }).ToArray();
        }

        static float DistanceToSegment(Vector2 point, (Vector2 a, Vector2 b) segment)
        {
            var span = segment.b - segment.a;
            float t = span.sqrMagnitude == 0 ? 0 : Mathf.Clamp01(Vector2.Dot(point - segment.a, span) / span.sqrMagnitude);
            return Vector2.Distance(point, segment.a + span * t);
        }

        static float ContourDifference((Vector2 a, Vector2 b)[] first, (Vector2 a, Vector2 b)[] second)
        {
            // Sample the actual contour paths, independent of stroke order and subdivision.
            // A new hatch or a reordered vertex list cannot create a distinct silhouette here.
            float Distance((Vector2 a, Vector2 b)[] source, (Vector2 a, Vector2 b)[] target)
                => source.SelectMany(segment => new[] { segment.a, (segment.a + segment.b) * .5f, segment.b })
                    .Max(point => target.Min(segment => DistanceToSegment(point, segment)));
            return Mathf.Max(Distance(first, second), Distance(second, first));
        }

        static Vector2[][] ClosedContours((Vector2 a, Vector2 b)[] segments)
        {
            var loops = new List<Vector2[]>();
            var points = new List<Vector2>();
            foreach (var segment in segments)
            {
                if (points.Count == 0 || Vector2.Distance(points[points.Count - 1], segment.a) > .00001f)
                {
                    points.Clear();
                    points.Add(segment.a);
                }
                points.Add(segment.b);
                if (points.Count >= 4 && Vector2.Distance(points[0], segment.b) <= .00001f)
                {
                    loops.Add(points.Take(points.Count - 1).ToArray());
                    points.Clear();
                }
            }
            Assert.That(loops, Is.Not.Empty, "A magazine needs a closed body contour, independent of its open connector rails.");
            return loops.ToArray();
        }

        static bool Inside(Vector2[] polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, previous = polygon.Length - 1; i < polygon.Length; previous = i++)
            {
                var a = polygon[i]; var b = polygon[previous];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        static float SilhouetteDifference((Vector2 a, Vector2 b)[] first, (Vector2 a, Vector2 b)[] second)
        {
            var a = ClosedContours(first); var b = ClosedContours(second);
            int union = 0, different = 0;
            // Curvature can distinguish a drum from a tapered box while the two boundaries
            // remain nearby. Compare the area enclosed by the actual ivory contours instead
            // of requiring an arbitrary single-point separation. Open shared ports add no area.
            const int samples = 64;
            for (int y = 0; y < samples; y++)
                for (int x = 0; x < samples; x++)
                {
                    var point = new Vector2((x + .5f) / samples, (y + .5f) / samples);
                    bool inA = a.Any(polygon => Inside(polygon, point));
                    bool inB = b.Any(polygon => Inside(polygon, point));
                    if (inA || inB) union++;
                    if (inA != inB) different++;
                }
            Assert.That(union, Is.GreaterThan(0));
            return different / (float)union;
        }

        [Test] public void ScopeHasALongCappedTubeSilhouetteAlongTheNativeForwardAxis()
        {
            var scope = ContourBounds("sight.scope");
            Assert.That(scope.height, Is.GreaterThan(scope.width * 1.5f),
                "A scope must read as a narrow tube pointing along +Y, even without hatching or enlarged preview art.");
        }

        [Test] public void PrecisionFrameHasANoticeablyNarrowerDrawnFootprintThanTheStandardFrame()
        {
            var precision = ContourBounds("frame.precision");
            var standard = ContourBounds("frame.standard");
            Assert.That(precision.width, Is.LessThan(standard.width * .88f),
                "The precision foundation needs its own narrow silhouette inside the unchanged mechanical band.");
        }

        [Test] public void MechanismDarkSeamsStayVisibleOnMaterialAndMuchThinnerThanItsIvoryContour()
        {
            var drawing = Draw(AssemblyVisualComposer.ComposePart(P("core.gun")));
            Color32 paper = NightNotebookArt.Paper, material = NightNotebookArt.Body;
            var contours = StrokeWidths(drawing.Where(vertex => IsIvory(vertex) && vertex.color.a >= 250));
            var seams = StrokeWidths(drawing.Where(vertex => vertex.color.r == paper.r &&
                vertex.color.g == paper.g && vertex.color.b == paper.b && vertex.color.a >= 225 && vertex.color.a <= 245));
            Assert.That(drawing.Any(vertex => vertex.color.r == material.r && vertex.color.g == material.g &&
                vertex.color.b == material.b && vertex.color.a >= 180), Is.True,
                "Dark mechanical seams need a visible material body beneath them.");
            Assert.That(contours, Is.Not.Empty);
            Assert.That(seams, Is.Not.Empty,
                "Internal mechanical seams need visible dark paper strokes on the light material body.");
            var contourWidth = contours.OrderBy(width => width).ElementAt(contours.Length / 2);
            Assert.That(seams.Max(), Is.LessThan(contourWidth * .55f),
                "Dark internal seams must stay subordinate to the ivory outer silhouette.");
        }

        [Test] public void EveryDrawableCatalogPartProducesFiniteVisibleInkInsideItsUnchangedNativeEnvelope()
        {
            var definitions = Catalog().Definitions.Where(definition =>
                !definition.Id.EndsWith(".none", StringComparison.Ordinal)).ToArray();
            Assert.That(definitions, Is.Not.Empty);
            foreach (var definition in definitions)
            {
                var reference = new PartRef(definition.Id, definition.Revision);
                Assert.That(AssemblyVisualComposer.HasVisualMapping(reference), Is.True, definition.Id);
                var composition = AssemblyVisualComposer.ComposePart(reference);
                Assert.That(composition.Fragments.Count, Is.EqualTo(1), definition.Id);
                var fragment = composition.Fragments.Single();
                var envelope = Envelope(definition.Category);
                AssertEnvelope(fragment.Region, envelope, definition.Id + " Region");
                AssertEnvelope(fragment.Trim, envelope, definition.Id + " Trim");
                AssertEnvelope(composition.Bounds, envelope, definition.Id + " aggregate bounds");
                AssertVisibleFiniteAndInside(Draw(composition), envelope, definition.Id);
            }
        }

        [TestCaseSource(nameof(GunCombinations))]
        public void InterchangeableGunPartsKeepTheirFitCategoriesRootsAndMuzzle(string head, string magazine, string sight)
        {
            var catalog = Catalog();
            var weapon = Gun(head, magazine, sight);
            var foundation = new FoundationRecipe(P("frame.standard"), P("hull.standard"), P("armor.none"),
                P("feed.standard"), P("turn.standard"));
            var recipe = new AssemblyRecipe(new TowerRecipe(foundation, new[] { new WeaponMountRecipe(0, weapon) }));
            Assert.That(catalog.ValidateRecipe(recipe), Is.Empty);
            Assert.That(AssemblyFitRules.Validate(recipe, AssemblyVisualComposer.Fits), Is.Empty);

            var composition = AssemblyVisualComposer.ComposeWeapon(weapon);
            Assert.That(composition.Fragments.Select(fragment => fragment.PartId), Is.EqualTo(new[] {
                "turn.standard", "core.gun", head, sight, magazine, "trigger.standard", "mount.standard" }));
            Assert.That(composition.Fragments.Select(fragment => fragment.Layer), Is.EqualTo(new[] { 5, 11, 12, 13, 14, 15, 20 }));
            foreach (var fragment in composition.Fragments)
            {
                Assert.That(fragment.SlotIndex, Is.Zero, fragment.PartId);
                Assert.That(fragment.Origin, Is.EqualTo(Vector2.zero), fragment.PartId);
                Assert.That(fragment.Radians, Is.Zero, fragment.PartId);
                Assert.That(fragment.Scale, Is.EqualTo(1), fragment.PartId);
                var pen = new MeshPen(null);
                fragment.Draw(pen, Vector2.zero, 1, Color.white);
                using (var target = new VertexHelper())
                {
                    pen.CopyTo(target, Vector2.zero, 1);
                    var vertices = new UIVertex[target.currentVertCount];
                    for (int i = 0; i < vertices.Length; i++) target.PopulateUIVertex(ref vertices[i], i);
                    AssertVisibleFiniteAndInside(vertices, fragment.Region, fragment.PartId);
                }
            }
            Assert.That(composition.Anchors.Count, Is.EqualTo(1));
            var anchor = composition.Anchors.Single();
            Assert.That(anchor.SlotIndex, Is.Zero);
            Assert.That(Vector2.Distance(anchor.Attach, new Vector2(0, -.158f)), Is.LessThan(.00001f));
            Assert.That(anchor.RotationCenter, Is.EqualTo(Vector2.zero));
            Assert.That(Vector2.Distance(anchor.Muzzle, new Vector2(0, .30f)), Is.LessThan(.00001f));
        }

        [TestCase("sight.iron", "sight.scope", ComponentCategory.Sight)]
        [TestCase("sight.iron", "sight.tracking", ComponentCategory.Sight)]
        [TestCase("sight.scope", "sight.tracking", ComponentCategory.Sight)]
        [TestCase("magazine.standard", "magazine.drum", ComponentCategory.Magazine)]
        [TestCase("magazine.standard", "magazine.belt", ComponentCategory.Magazine)]
        [TestCase("magazine.drum", "magazine.belt", ComponentCategory.Magazine)]
        public void InterchangeableSightsAndMagazinesHaveDistinctStructuralContours(string first, string second, ComponentCategory category)
        {
            var a = NormalizedContour(first, category); var b = NormalizedContour(second, category);
            if (category == ComponentCategory.Magazine)
                Assert.That(SilhouetteDifference(a, b), Is.GreaterThan(.15f),
                    first + " and " + second + " need visibly different body silhouettes, independent of shared ports and internal hatching.");
            else
                Assert.That(ContourDifference(a, b), Is.GreaterThan(.10f),
                    first + " and " + second + " must differ structurally by more than a small detail or hatch.");
        }
    }
}
#endif
