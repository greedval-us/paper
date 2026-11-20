using System;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class InkSwarmArtTests
    {
        [Test] public void DistantUnarmoredHordeImpKeepsItsFeetAndCrestWithinThirtyVertices()
        {
            var pen = new MeshPen(null);
            InkSwarmEnemyArt.DrawDistantDoodle(pen, Vector2.zero, 1, NotebookEnemyMotion.Walk, .13f, NotebookVisualThemeAsset.Default);
            Assert.That(pen.VertexCount, Is.InRange(12, 30));
            Assert.That(pen.TryGetBounds(out var bounds), Is.True);
            Assert.That(bounds.yMin, Is.InRange(-.44f, -.36f));
            Assert.That(bounds.yMax, Is.InRange(.45f, .51f));
            Assert.That(bounds.width, Is.InRange(.61f, .68f));
        }

        [Test] public void CompactEnemiesKeepSixFiniteNativeRolesWithinTheWorldVertexBudget()
        {
            var theme = NotebookVisualThemeAsset.Default;
            var kinds = (EnemyKind[])Enum.GetValues(typeof(EnemyKind));
            var mesh = new Mesh();
            var historical = NotebookThemeDefaults.CreateTheme();
            try
            {
                // Exercise the real world line commands and their AA strips, not UI triangle counts.
                var pen = new MeshPen(mesh) { WorldUnitsPerPixel = .08f, MinimumStrokePixels = 1.25f };
                foreach (EnemyKind kind in kinds)
                foreach (DrawingTier tier in Enum.GetValues(typeof(DrawingTier)))
                foreach (NotebookEnemyMotion motion in Enum.GetValues(typeof(NotebookEnemyMotion)))
                {
                    string id = NotebookAnimationState.EnemyId(kind, tier, motion);
                    foreach (var frame in theme.FindClip(id).Frames)
                    {
                        string compactId = frame.ShapeId + ".compact";
                        var shape = theme.FindShape(compactId);
                        Assert.That(shape, Is.Not.Null, compactId);
                        Assert.That(shape.Id, Is.EqualTo(compactId));
                        Assert.That(shape, Is.SameAs(InkSwarmEnemyArt.CreateCompact(frame.ShapeId)));
                        Assert.That(shape.Marks.Exists(mark => mark.Channel == NotebookInkChannel.Surface), Is.True, compactId);
                        Assert.That(shape.Marks.Exists(mark => mark.Channel == NotebookInkChannel.White || mark.Channel == NotebookInkChannel.Paper), Is.False, compactId);
                        pen.Clear();
                        Assert.That(DoodleGlyphs.DrawResourceEnemy(pen, kind, tier, Vector2.zero, motion, frame.Time,
                            theme, compact: true), Is.True, compactId);
                        pen.Upload();
                        Assert.That(mesh.vertexCount, Is.GreaterThan(0).And.LessThanOrEqualTo(160), compactId);
                        foreach (var vertex in mesh.vertices)
                            Assert.That(float.IsNaN(vertex.x) || float.IsInfinity(vertex.x) || float.IsNaN(vertex.y) || float.IsInfinity(vertex.y), Is.False, compactId);
                        var uv = new System.Collections.Generic.List<Vector2>(); mesh.GetUVs(1, uv);
                        Assert.That(uv, Is.All.EqualTo(new Vector2(.5f, .5f)), compactId);
                    }
                }
                for (int a = 0; a < kinds.Length; a++) for (int b = a + 1; b < kinds.Length; b++)
                    AssertDifferent(NotebookAnimationState.EnemyId(kinds[a], DrawingTier.Sketch, NotebookEnemyMotion.Idle) + ".compact",
                        NotebookAnimationState.EnemyId(kinds[b], DrawingTier.Sketch, NotebookEnemyMotion.Idle) + ".compact");

                var constructor = typeof(EnemyView).GetConstructor(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                    null, new[] { typeof(EnemyState) }, null);
                foreach (EnemyKind kind in kinds)
                {
                    // Include real health/status drawing. Spitter also retains its existing windup warning.
                    var state = new EnemyState { Id = 11, Kind = kind, Health = 10, MaxHealth = 10,
                        DrawingTier = DrawingTier.Hatched, Position = new Point2(0, 0), RedRemaining = 1,
                        BlueRemaining = kind == EnemyKind.Spitter ? 0 : 1, GreenRemaining = kind == EnemyKind.Spitter ? 0 : 1,
                        WindupRemaining = kind == EnemyKind.Spitter ? .1f : 0 };
                    var enemy = (EnemyView)constructor.Invoke(new object[] { state });
                    pen.Clear(); DoodleGlyphs.Enemy(pen, enemy, .08f, compact: true, theme: theme); pen.Upload();
                    Assert.That(mesh.vertexCount, Is.LessThanOrEqualTo(256), kind + " body, health and status/windup marks");
                }

                pen.Clear(); DoodleGlyphs.DrawResourceEnemy(pen, EnemyKind.Runner, DrawingTier.Sketch, Vector2.zero,
                    NotebookEnemyMotion.Walk, .05f, theme, compact: true);
                int builds = NotebookArtRenderer.GeometryBuildCount;
                for (int i = 0; i < 8; i++)
                {
                    pen.Clear(); DoodleGlyphs.DrawResourceEnemy(pen, EnemyKind.Runner, DrawingTier.Sketch, Vector2.zero,
                        NotebookEnemyMotion.Walk, .05f, theme, compact: true);
                }
                Assert.That(NotebookArtRenderer.GeometryBuildCount, Is.EqualTo(builds), "Warmed crowd poses reuse cached geometry.");

                pen.Clear(); DoodleGlyphs.DrawResourceEnemy(pen, EnemyKind.Brute, DrawingTier.Sketch, Vector2.zero,
                    NotebookEnemyMotion.Idle, 0, historical); pen.Upload(); var legacy = mesh.vertices;
                pen.Clear(); DoodleGlyphs.DrawResourceEnemy(pen, EnemyKind.Brute, DrawingTier.Sketch, Vector2.zero,
                    NotebookEnemyMotion.Idle, 0, historical, compact: true); pen.Upload();
                Assert.That(mesh.vertices, Is.EqualTo(legacy), "Historical custom themes retain their existing geometry.");
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(historical); }
        }

        [TestCase("doodle", "gnawer")]
        [TestCase("doodle", "spitter")]
        [TestCase("gnawer", "spitter")]
        [TestCase("runner", "brute")]
        [TestCase("brute", "bulwark")]
        [TestCase("doodle", "runner")]
        [TestCase("doodle", "brute")]
        [TestCase("doodle", "bulwark")]
        [TestCase("runner", "gnawer")]
        [TestCase("runner", "spitter")]
        [TestCase("runner", "bulwark")]
        [TestCase("gnawer", "brute")]
        [TestCase("gnawer", "bulwark")]
        [TestCase("spitter", "brute")]
        [TestCase("spitter", "bulwark")]
        public void RolesHaveDifferentStructuralInkAtSmallScale(string a, string b)
            => AssertDifferent("enemy." + a + ".sketch.idle", "enemy." + b + ".sketch.idle");

        [TestCase("terrain.pine", "terrain.oak")]
        [TestCase("terrain.rock", "terrain.shrub")]
        [TestCase("terrain.mountain", "terrain.ridge")]
        [TestCase("object.castle", "object.castle.damaged")]
        [TestCase("object.castle.damaged", "object.castle.ruined")]
        [TestCase("fx.muzzle", "fx.spitter.impact")]
        [TestCase("fx.structure.destroy", "fx.cleanup")]
        public void DifferentBattleObjectsDoNotReuseOnePlaceholder(string a, string b) => AssertDifferent(a, b);

        [Test] public void NativeEnemyInkIsWarmAndItsBodyDoesNotBecomeAWhitePolygon()
        {
            var theme = NotebookVisualThemeAsset.Default;
            var shape = theme.FindShape("enemy.doodle.sketch.idle");
            using (var mesh = new VertexHelper())
            {
                NotebookArtRenderer.DrawUI(mesh, shape, Vector2.zero, 1, theme: theme);
                int warm = 0, dark = 0;
                var v = new UIVertex();
                for (int i = 0; i < mesh.currentVertCount; i++)
                {
                    mesh.PopulateUIVertex(ref v, i);
                    if (v.color.a < 180) continue;
                    if (v.color.r > 210 && v.color.r > v.color.g && v.color.g > v.color.b) warm++;
                    if (v.color.r < 70 && v.color.g < 90 && v.color.b < 80) dark++;
                }
                Assert.That(warm, Is.GreaterThan(12), "Warm ivory contours must stand out on dark notebook paper.");
                Assert.That(dark, Is.GreaterThan(6), "Body fill must be dark ink, not opaque white.");
            }
        }

        [Test] public void EveryRoleTierMotionAndFrameProducesFiniteCachedGeometry()
        {
            var theme = NotebookVisualThemeAsset.Default;
            foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
            foreach (DrawingTier tier in Enum.GetValues(typeof(DrawingTier)))
            foreach (NotebookEnemyMotion motion in Enum.GetValues(typeof(NotebookEnemyMotion)))
            {
                string id = NotebookAnimationState.EnemyId(kind, tier, motion);
                var clip = theme.FindClip(id);
                Assert.That(clip, Is.Not.Null, id);
                Assert.That(clip.Duration, Is.GreaterThan(0).And.LessThanOrEqualTo(1.2f), id);
                Assert.That(clip.Loop, Is.EqualTo(motion == NotebookEnemyMotion.Idle || motion == NotebookEnemyMotion.Walk), id);
                foreach (var frame in clip.Frames)
                {
                    var shape = theme.FindShape(frame.ShapeId);
                    Assert.That(shape, Is.SameAs(theme.FindShape(frame.ShapeId)), frame.ShapeId);
                    using (var mesh = new VertexHelper())
                    {
                        Assert.That(NotebookArtRenderer.DrawUI(mesh, shape, Vector2.zero, frame.Scale, theme: theme), Is.True, id);
                        Assert.That(mesh.currentVertCount, Is.GreaterThan(0).And.LessThan(3000), id);
                        var v = new UIVertex();
                        for (int i = 0; i < mesh.currentVertCount; i++)
                        { mesh.PopulateUIVertex(ref v, i); Assert.That(float.IsNaN(v.position.x) || float.IsInfinity(v.position.x) || float.IsNaN(v.position.y) || float.IsInfinity(v.position.y), Is.False, id); }
                    }
                }
            }
        }

        [Test] public void HistoricalPaletteIndicesAndCustomThemeFallbackRemainIntact()
        {
            var historical = NotebookThemeDefaults.CreateTheme();
            try
            {
                for (int i = 0; i < 8; i++)
                    Assert.That(NotebookVisualThemeAsset.Default.Color((NotebookInkChannel)i), Is.EqualTo(historical.Color((NotebookInkChannel)i)));
                var shape = historical.FindShape("enemy.doodle.sketch.idle");
                Assert.That(shape.Marks.Exists(m => (int)m.Channel >= 8), Is.False, "Custom/old themes retain ownership of their existing eight palette channels.");
            }
            finally { UnityEngine.Object.DestroyImmediate(historical); }
        }

        [Test] public void ReducedMotionThemeCopyResolvesTheSameNativeBodies()
        {
            var copy = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            try
            {
                var data = JsonUtility.FromJson<NotebookArtSource>(JsonUtility.ToJson(NotebookVisualThemeAsset.Default.Data));
                foreach (var clip in data.Clips) foreach (var frame in clip.Frames)
                { frame.Offset = Vector2.zero; frame.Rotation = 0; frame.Scale = 1; }
                copy.ReplaceData(data);
                var normal = NotebookVisualThemeAsset.Default.FindShape("enemy.spitter.sketch.attack");
                var reduced = copy.FindShape("enemy.spitter.sketch.attack");
                Assert.That(reduced.Marks.Count, Is.EqualTo(normal.Marks.Count));
                Assert.That(reduced.Marks.Exists(mark => mark.Channel == NotebookInkChannel.Ivory), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        static void AssertDifferent(string a, string b)
        {
            var first = Raster(a); var second = Raster(b);
            int union = 0, difference = 0;
            for (int i = 0; i < first.Length; i++)
            { if (first[i] || second[i]) union++; if (first[i] != second[i]) difference++; }
            Assert.That(union, Is.GreaterThan(4), a);
            Assert.That((float)difference / union, Is.GreaterThan(.12f), a + " and " + b + " need different readable structures.");
        }
        static bool[] Raster(string id)
        {
            const int size = 32;
            var result = new bool[size * size];
            using (var mesh = new VertexHelper())
            {
                var shape = NotebookVisualThemeAsset.Default.FindShape(id);
                Assert.That(shape, Is.Not.Null, id);
                NotebookArtRenderer.DrawUI(mesh, shape, Vector2.zero, 1);
                var points = new Vector2[mesh.currentVertCount]; var bright = new bool[points.Length];
                var vertex = new UIVertex(); Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
                for (int i = 0; i < points.Length; i++)
                {
                    mesh.PopulateUIVertex(ref vertex, i); points[i] = vertex.position;
                    bright[i] = vertex.color.a > 30 && Mathf.Max(vertex.color.r, Mathf.Max(vertex.color.g, vertex.color.b)) > 100;
                    min = Vector2.Min(min, points[i]); max = Vector2.Max(max, points[i]);
                }
                Vector2 center = (min + max) * .5f; float span = Mathf.Max(max.x - min.x, max.y - min.y) * 1.04f;
                for (int i = 0; i < points.Length; i++) points[i] = (points[i] - center) / span + Vector2.one * .5f;
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + .5f) / size, (y + .5f) / size);
                    for (int i = 0; i + 2 < points.Length; i += 3)
                        if (bright[i] && Inside(p, points[i], points[i + 1], points[i + 2])) { result[y * size + x] = true; break; }
                }
            }
            return result;
        }
        static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
            if (Mathf.Abs(Cross(b-a,c-a)) < .0000001f) return false;
            float x = Cross(b-a,p-a), y = Cross(c-b,p-b), z = Cross(a-c,p-c);
            return (x >= 0 && y >= 0 && z >= 0) || (x <= 0 && y <= 0 && z <= 0);
        }
    }
}
