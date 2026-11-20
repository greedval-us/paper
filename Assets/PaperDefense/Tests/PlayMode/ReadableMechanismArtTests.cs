#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    /// <summary>Mechanisms need visible material mass, in addition to the existing contour and fit checks.</summary>
    public sealed class ReadableMechanismArtTests
    {
        const string CatalogPath = "Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset";
        const byte MinimumBodyAlpha = 180;
        static readonly string[] WeaponFamilies = { "gun", "piercer", "mortar", "fan", "ricochet", "stapler", "blotter" };
        ComponentCatalog catalog;

        [SetUp] public void SetUp()
        {
            var asset = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(CatalogPath);
            Assert.That(asset, Is.Not.Null, "Readability must be checked against the production catalog.");
            catalog = asset.CreateSnapshot();
        }

        static IEnumerable<TestCaseData> SolidParts()
        {
            foreach (string family in WeaponFamilies)
                yield return new TestCaseData("core." + family, .12f);
            foreach (string variant in new[] { "wall", "wall.braced", "slow", "slow.diluted", "repair", "repair.focused",
                "generator", "generator.quick", "spikes", "spikes.barbed", "beacon", "beacon.focused" })
                yield return new TestCaseData("function." + variant, .10f);
            foreach (string variant in new[] { "standard", "light", "reinforced" })
                yield return new TestCaseData("hull." + variant, .12f);
            foreach (string variant in new[] { "standard", "boost" })
                yield return new TestCaseData("feed." + variant, .10f);
            foreach (string variant in new[] { "standard", "quick" })
                yield return new TestCaseData("turn." + variant, .10f);
            foreach (string variant in new[] { "standard", "drum", "belt" })
                yield return new TestCaseData("magazine." + variant, .15f);
            // A narrow barrel or open staple fork should retain its negative space.
            foreach (string variant in WeaponFamilies.Concat(new[] { "short", "long", "wide" }))
                yield return new TestCaseData("head." + variant, .08f);
        }

        static IEnumerable<TestCaseData> CompactParts()
        {
            var ids = WeaponFamilies.Select(family => "core." + family)
                .Concat(new[] { "function.wall", "function.slow", "function.repair", "function.generator",
                    "function.spikes", "function.beacon", "magazine.standard", "magazine.drum", "magazine.belt", "head.piercer", "head.stapler" });
            foreach (string id in ids)
                foreach (int pixels in new[] { 36, 64 })
                    yield return new TestCaseData(id, pixels);
            // A 96x64 battlefield gives the core only 6-9 real pixels. Exercise
            // that native thumbnail LOD, rather than testing only card-size art.
            foreach (string family in WeaponFamilies)
                foreach (int pixels in new[] { 6, 9 })
                    yield return new TestCaseData("core." + family, pixels);
        }

        PartRef ProductionPart(string id)
        {
            var definition = catalog.Definitions.SingleOrDefault(part => part.Id == id);
            Assert.That(definition, Is.Not.Null, id + " must be a real production part.");
            Assert.That(id.EndsWith(".none", StringComparison.Ordinal), Is.False);
            return new PartRef(definition.Id, definition.Revision);
        }

        [TestCaseSource(nameof(SolidParts))]
        public void ProductionSolidPartsHaveReadableForegroundBody(string id, float minimumArea)
        {
            var composition = AssemblyVisualComposer.ComposePart(ProductionPart(id));
            var pen = new MeshPen(null);
            composition.Draw(pen, Vector2.zero);
            using (var mesh = new VertexHelper())
            {
                pen.CopyTo(mesh, Vector2.zero, 1);
                var triangles = TriangleStream(mesh);
                float area = VisibleBodyArea(triangles, composition.Bounds, 96, 96);
                float proportion = area / (composition.Bounds.width * composition.Bounds.height);
                Assert.That(proportion, Is.GreaterThanOrEqualTo(minimumArea),
                    id + " has only " + proportion.ToString("P1") + " visible foreground body. " +
                    "A readable mechanism needs a substantial material silhouette, rather than a paper-colored mask or only thin contours.");
            }
        }

        [TestCaseSource(nameof(CompactParts))]
        public void NativePartPreviewKeepsMaterialMassAtActualSmallPixelSizes(string id, int pixels)
        {
            var owner = new GameObject("Readable mechanism compact preview", typeof(RectTransform), typeof(WeaponAssemblyGraphic));
            try
            {
                var graphic = owner.GetComponent<WeaponAssemblyGraphic>();
                graphic.rectTransform.sizeDelta = new Vector2(pixels, pixels);
                graphic.SetPart(ProductionPart(id));
                using (var mesh = new VertexHelper())
                {
                    typeof(WeaponAssemblyGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                        null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { mesh });
                    var triangles = TriangleStream(mesh);
                    var visibleBounds = Bounds(triangles);
                    // Sample actual pixel centers across the real UI rectangle. Normalize by
                    // the fitted drawing's bounds so tall barrels and wide magazines keep their proportions.
                    float area = VisibleBodyArea(triangles, graphic.rectTransform.rect, pixels, pixels);
                    float proportion = area / (visibleBounds.width * visibleBounds.height);
                    float minimumArea = id.StartsWith("head.", StringComparison.Ordinal) ? .07f : .10f;
                    Assert.That(proportion, Is.GreaterThanOrEqualTo(minimumArea),
                        id + " loses its material silhouette at " + pixels + " px (" + proportion.ToString("P1") + "). " +
                        "Removing fine detail must leave a visible solid body.");
                }
            }
            finally { Object.DestroyImmediate(owner); }
        }

        static List<UIVertex> TriangleStream(VertexHelper mesh)
        {
            var triangles = new List<UIVertex>();
            mesh.GetUIVertexStream(triangles);
            Assert.That(triangles, Is.Not.Empty, "The production renderer must produce visible geometry.");
            return triangles;
        }

        static Rect Bounds(List<UIVertex> triangles)
        {
            Vector2 low = triangles[0].position, high = low;
            foreach (var vertex in triangles)
            { low = Vector2.Min(low, vertex.position); high = Vector2.Max(high, vertex.position); }
            var bounds = Rect.MinMaxRect(low.x, low.y, high.x, high.y);
            Assert.That(bounds.width * bounds.height, Is.GreaterThan(0));
            return bounds;
        }

        static bool BodyVertex(UIVertex vertex)
        {
            Color32 material = NightNotebookArt.Body;
            return vertex.color.r == material.r && vertex.color.g == material.g &&
                vertex.color.b == material.b && vertex.color.a >= MinimumBodyAlpha;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static float VisibleBodyArea(List<UIVertex> triangles, Rect bounds, int columns, int rows)
        {
            Assert.That(bounds.width * bounds.height, Is.GreaterThan(0));
            var body = new float[columns * rows];
            float pixelWidth = bounds.width / columns, pixelHeight = bounds.height / rows;
            for (int first = 0; first < triangles.Count; first += 3)
            {
                var a = triangles[first]; var b = triangles[first + 1]; var c = triangles[first + 2];
                Vector2 pa = a.position, pb = b.position, pc = c.position;
                float determinant = Cross(pb - pa, pc - pa);
                if (Mathf.Abs(determinant) < .000000000001f) continue;
                bool material = BodyVertex(a) && BodyVertex(b) && BodyVertex(c);
                int left = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x)) - bounds.xMin) / pixelWidth), 0, columns - 1);
                int right = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x)) - bounds.xMin) / pixelWidth), 0, columns - 1);
                int bottom = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y)) - bounds.yMin) / pixelHeight), 0, rows - 1);
                int top = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y)) - bounds.yMin) / pixelHeight), 0, rows - 1);
                for (int y = bottom; y <= top; y++)
                    for (int x = left; x <= right; x++)
                    {
                        var point = new Vector2(bounds.xMin + (x + .5f) * pixelWidth, bounds.yMin + (y + .5f) * pixelHeight);
                        float wb = Cross(point - pa, pc - pa) / determinant;
                        float wc = Cross(pb - pa, point - pa) / determinant;
                        float wa = 1 - wb - wc;
                        if (wa < -.00001f || wb < -.00001f || wc < -.00001f) continue;
                        float alpha = (a.color.a * wa + b.color.a * wb + c.color.a * wc) / 255f;
                        int index = y * columns + x;
                        // Follow painter's order: paper knockouts or later surfaces hide a body.
                        // Low-alpha hatching cannot accumulate into qualifying body material.
                        body[index] = body[index] * (1 - alpha) + (material ? alpha : 0);
                    }
            }
            return body.Count(alpha => alpha >= MinimumBodyAlpha / 255f - .0001f) * pixelWidth * pixelHeight;
        }
    }
}
#endif
