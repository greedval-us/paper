using System;
using System.Collections.Generic;
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

        [TestCase(EnemyFamily.Undead, NotebookInkChannel.Violet)]
        [TestCase(EnemyFamily.Clockwork, NotebookInkChannel.Copper)]
        public void OverviewDrawsTheNotebookGruntAndKeepsItsAccentAtBothZoomLevels(EnemyFamily family, NotebookInkChannel accent)
        {
            var enemy = PreparedGrunt();
            var theme = NotebookVisualThemeAsset.Default;
            var position = new Vector2(enemy.Position.X, enemy.Position.Y);
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh) { WorldUnitsPerPixel = enemy.VisualScale / 16, MinimumStrokePixels = 1.25f };
                foreach (NotebookEnemyMotion motion in Enum.GetValues(typeof(NotebookEnemyMotion)))
                {
                    pen.Clear();
                    InkSwarmEnemyArt.DrawDistantDoodle(pen, position, enemy.VisualScale, motion, .13f, theme, family);
                    Assert.That(pen.VertexCount, Is.InRange(12, 30), family + " " + motion);
                }

                pen.Clear();
                InkSwarmEnemyArt.DrawDistantDoodle(pen, position, enemy.VisualScale, NotebookEnemyMotion.Idle, 0, theme, family);
                pen.Upload();
                var distantShape = mesh.vertices;
                pen.Clear();
                DoodleGlyphs.Enemy(pen, enemy, 0, compact: true, theme: theme, horde: HordeView.Overview, family: family);
                pen.Upload();
                Assert.That(mesh.vertices, Is.EqualTo(distantShape), "A grunt at 16 pixels uses its notebook's distant silhouette.");
                Assert.That(mesh.colors32, Does.Contain((Color32)theme.Color(accent)));

                pen.Clear();
                pen.WorldUnitsPerPixel = enemy.VisualScale / 36;
                DoodleGlyphs.Enemy(pen, enemy, 0, compact: true, theme: theme, horde: HordeView.Overview, family: family);
                pen.Upload();
                Assert.That(mesh.vertexCount, Is.GreaterThan(30).And.LessThanOrEqualTo(160));
                Assert.That(mesh.colors32, Does.Contain((Color32)theme.Color(accent)));
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        static EnemyView PreparedGrunt()
        {
            var game = GameSession.CreatePrepared(GameBalance.CreateDefault(), 37,
                new[] { new PreparedEnemy(new Point2(6, 6), 100) });
            return new RunReadModel(game).Enemies[0];
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
                var positions = new List<Vector3>(160);
                var colors = new List<Color32>(160);
                var uv = new List<Vector2>(160);
                foreach (EnemyFamily family in Enum.GetValues(typeof(EnemyFamily)))
                foreach (EnemyKind kind in kinds)
                foreach (DrawingTier tier in Enum.GetValues(typeof(DrawingTier)))
                foreach (NotebookEnemyMotion motion in Enum.GetValues(typeof(NotebookEnemyMotion)))
                {
                    string id = NotebookAnimationState.EnemyId(kind, tier, motion, family);
                    var clip = theme.FindClip(id);
                    Assert.That(clip, Is.Not.Null, id);
                    Assert.That(clip.Frames.Count, Is.GreaterThanOrEqualTo(motion == NotebookEnemyMotion.Idle ? 1 : 3), id);
                    Assert.That(clip.Loop, Is.EqualTo(motion == NotebookEnemyMotion.Idle || motion == NotebookEnemyMotion.Walk), id);
                    foreach (var frame in clip.Frames)
                    {
                        Assert.That(clip.Sample(frame.Time), Is.SameAs(frame), id);
                        string compactId = frame.ShapeId + ".compact";
                        var shape = theme.FindShape(compactId);
                        Assert.That(shape, Is.Not.Null, compactId);
                        Assert.That(shape.Id, Is.EqualTo(compactId));
                        Assert.That(shape, Is.SameAs(InkSwarmEnemyArt.CreateCompact(frame.ShapeId)));
                        Assert.That(shape.Marks.Exists(mark => mark.Channel == NotebookInkChannel.Surface), Is.True, compactId);
                        Assert.That(shape.Marks.Exists(mark => mark.Channel == NotebookInkChannel.White || mark.Channel == NotebookInkChannel.Paper), Is.False, compactId);
                        pen.Clear();
                        Assert.That(DoodleGlyphs.DrawResourceEnemy(pen, kind, tier, Vector2.zero, motion, frame.Time,
                            theme, compact: true, family: family), Is.True, compactId);
                        pen.Upload();
                        Assert.That(mesh.vertexCount, Is.GreaterThan(0).And.LessThanOrEqualTo(160), compactId);
                        if (family != EnemyFamily.Ink && frame.Alpha == 1)
                        {
                            var accent = family == EnemyFamily.Undead ? NotebookInkChannel.Violet : NotebookInkChannel.Copper;
                            mesh.GetColors(colors);
                            Assert.That(colors.Contains((Color32)theme.Color(accent)), Is.True, compactId);
                        }
                        mesh.GetVertices(positions);
                        Assert.That(positions.Exists(v => float.IsNaN(v.x) || float.IsInfinity(v.x) ||
                            float.IsNaN(v.y) || float.IsInfinity(v.y)), Is.False, compactId);
                        mesh.GetUVs(1, uv);
                        Assert.That(uv.Count, Is.EqualTo(mesh.vertexCount), compactId);
                        Assert.That(uv.Exists(p => p != NotebookSpriteCatalog.WhiteUV), Is.False, compactId);
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

        [TestCase("undead.runner.sketch.idle", "undead.brute.sketch.idle")]
        [TestCase("clockwork.gnawer.sketch.idle", "clockwork.brute.sketch.idle")]
        [TestCase("runner.sketch.idle", "undead.runner.sketch.idle")]
        [TestCase("undead.spitter.sketch.idle", "clockwork.spitter.sketch.idle")]
        [TestCase("undead.spitter.sketch.attack", "undead.spitter.sketch.attack.b")]
        [TestCase("clockwork.gnawer.sketch.attack", "clockwork.gnawer.sketch.attack.b")]
        public void FamiliesRolesAndAttackPhasesStayDistinctInTheCrowd(string first, string second)
            => AssertDifferent("enemy." + first + ".compact", "enemy." + second + ".compact");

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

        [TestCase(EnemyFamily.Undead, NotebookInkChannel.Blue, false)]
        [TestCase(EnemyFamily.Undead, NotebookInkChannel.Blue, true)]
        [TestCase(EnemyFamily.Clockwork, NotebookInkChannel.Red, false)]
        [TestCase(EnemyFamily.Clockwork, NotebookInkChannel.Red, true)]
        public void FamilyPosesUseAnEightColorThemeInBattle(EnemyFamily family, NotebookInkChannel accent, bool nativeRevision)
        {
            var theme = NotebookThemeDefaults.CreateTheme();
            var mesh = new Mesh();
            try
            {
                var data = theme.Data;
                if (nativeRevision) data.Revision = InkSwarmArt.Revision;
                var palette = new Color32[data.Palette.Length];
                for (int i = 0; i < palette.Length; i++)
                {
                    palette[i] = new Color32((byte)(17 + i * 23), (byte)(239 - i * 13), (byte)(49 + i * 7), 255);
                    data.Palette[i] = palette[i];
                }
                theme.ReplaceData(data);
                var clip = theme.FindClip(NotebookAnimationState.EnemyId(EnemyKind.Doodle, DrawingTier.Sketch, NotebookEnemyMotion.Attack, family));
                Assert.That(clip, Is.Not.Null, "Old themes inherit a missing family attack.");
                Assert.That(clip.Frames.Count, Is.GreaterThan(1));
                var frame = clip.Frames[1];
                var shape = theme.FindShape(frame.ShapeId);
                Assert.That(shape, Is.Not.Null, frame.ShapeId);
                Assert.That(shape.Marks.Exists(mark => (int)mark.Channel >= palette.Length), Is.False, frame.ShapeId);

                var enemy = PreparedGrunt();
                var pen = new MeshPen(mesh) { WorldUnitsPerPixel = .04f, MinimumStrokePixels = 1.25f };
                Assert.That(DoodleGlyphs.DrawResourceEnemy(pen, enemy.Kind, enemy.DrawingTier, Vector2.zero,
                    NotebookEnemyMotion.Attack, frame.Time, theme, family: family), Is.True);
                pen.Upload();
                AssertUsesPalette(mesh, palette, accent);
                foreach (int pixelHeight in new[] { 36, 16 })
                {
                    pen.Clear();
                    pen.WorldUnitsPerPixel = enemy.VisualScale / pixelHeight;
                    DoodleGlyphs.Enemy(pen, enemy, 0, compact: true, theme: theme, horde: HordeView.Overview, family: family);
                    pen.Upload();
                    AssertUsesPalette(mesh, palette, accent);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(theme); }
        }

        static void AssertUsesPalette(Mesh mesh, Color32[] palette, NotebookInkChannel accent)
        {
            var colors = mesh.colors32;
            Assert.That(colors, Does.Contain(palette[(int)accent]));
            foreach (var color in colors)
            {
                Assert.That(Array.Exists(palette, ink => ink.r == color.r && ink.g == color.g && ink.b == color.b),
                    Is.True, "Unexpected vertex color " + color);
            }
        }

        [TestCase(EnemyFamily.Undead, BossRank.None)]
        [TestCase(EnemyFamily.Clockwork, BossRank.None)]
        [TestCase(EnemyFamily.Ink, BossRank.Midpoint)]
        [TestCase(EnemyFamily.Undead, BossRank.Finale)]
        [TestCase(EnemyFamily.Clockwork, BossRank.Finale)]
        public void MissingFamilyDeathClipsHonorReducedMotionAndPreserveTheirFade(EnemyFamily family, BossRank boss)
        {
            var custom = NotebookThemeDefaults.CreateTheme();
            try
            {
                custom.Data.ReducedMotion = true;
                string id = NotebookAnimationState.EnemyId(EnemyKind.Runner, DrawingTier.Sketch, NotebookEnemyMotion.Death, family, boss);
                var native = NotebookVisualThemeAsset.Default.FindClip(id);
                var reduced = custom.FindClip(id);
                Assert.That(reduced, Is.Not.Null, id);
                Assert.That(reduced.Frames.Count, Is.EqualTo(native.Frames.Count));
                for (int i = 0; i < native.Frames.Count; i++)
                {
                    var frame = reduced.Frames[i];
                    Assert.That(frame.ShapeId, Is.EqualTo(native.Frames[0].ShapeId));
                    Assert.That(frame.Rotation, Is.Zero);
                    Assert.That(frame.Time, Is.EqualTo(native.Frames[i].Time));
                    Assert.That(frame.Alpha, Is.EqualTo(native.Frames[i].Alpha));
                }
                Assert.That(native.Frames[native.Frames.Count - 1].ShapeId, Is.Not.EqualTo(native.Frames[0].ShapeId));
                Assert.That(reduced.Frames[reduced.Frames.Count - 1].Alpha, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(custom); }
        }

        [TestCase(EnemyFamily.Ink, BossRank.Midpoint)]
        [TestCase(EnemyFamily.Ink, BossRank.Finale)]
        [TestCase(EnemyFamily.Undead, BossRank.Midpoint)]
        [TestCase(EnemyFamily.Undead, BossRank.Finale)]
        [TestCase(EnemyFamily.Clockwork, BossRank.Midpoint)]
        [TestCase(EnemyFamily.Clockwork, BossRank.Finale)]
        public void BossPosesResolveAndAnimateInEachState(EnemyFamily family, BossRank rank)
        {
            var theme = NotebookVisualThemeAsset.Default;
            foreach (NotebookEnemyMotion motion in Enum.GetValues(typeof(NotebookEnemyMotion)))
            {
                string id = NotebookAnimationState.EnemyId(EnemyKind.Brute, DrawingTier.Hatched, motion, family, rank);
                var clip = theme.FindClip(id);
                Assert.That(clip, Is.Not.Null, id);
                Assert.That(clip.Duration, Is.Positive, id);
                Assert.That(clip.Loop, Is.EqualTo(motion == NotebookEnemyMotion.Idle || motion == NotebookEnemyMotion.Walk), id);
                var first = new List<Vector3>();
                bool changes = false;
                using (var mesh = new VertexHelper())
                {
                    foreach (var frame in clip.Frames)
                    {
                        Assert.That(clip.Sample(frame.Time), Is.SameAs(frame), id);
                        var shape = theme.FindShape(frame.ShapeId);
                        Assert.That(shape, Is.Not.Null, frame.ShapeId);
                        Assert.That(shape, Is.SameAs(theme.FindShape(frame.ShapeId)));
                        mesh.Clear();
                        Assert.That(NotebookArtRenderer.DrawUI(mesh, shape, frame.Offset, frame.Scale, frame.Rotation, theme: theme), Is.True, id);
                        Assert.That(mesh.currentVertCount, Is.InRange(3, 3000), id);
                        var vertex = UIVertex.simpleVert;
                        bool baseline = first.Count == 0;
                        for (int i = 0; i < mesh.currentVertCount; i++)
                        {
                            mesh.PopulateUIVertex(ref vertex, i);
                            if (baseline) first.Add(vertex.position);
                            else if (i >= first.Count || (vertex.position - first[i]).sqrMagnitude > .00001f) changes = true;
                        }
                    }
                }
                Assert.That(changes, Is.True, "Pose geometry must change for " + id);
                if (motion == NotebookEnemyMotion.Death) Assert.That(clip.Frames[clip.Frames.Count-1].Alpha, Is.Zero);
            }
            string idle = InkSwarmEnemyArt.BossId(family, rank, NotebookEnemyMotion.Idle);
            string ordinary = NotebookAnimationState.EnemyId(rank == BossRank.Finale ? EnemyKind.Spitter : EnemyKind.Brute,
                DrawingTier.Hatched, NotebookEnemyMotion.Idle, family);
            AssertDifferent(idle, ordinary);
            if (rank == BossRank.Finale) AssertDifferent(idle, InkSwarmEnemyArt.BossId(family, BossRank.Midpoint, NotebookEnemyMotion.Idle));
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
