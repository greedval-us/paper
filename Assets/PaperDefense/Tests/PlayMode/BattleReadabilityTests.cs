using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using PaperDefense.Application;
using UnityEngine.UI;
#endif

namespace PaperDefense.Tests
{
    public sealed class BattleReadabilityTests
    {
        static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static GameSession Game(int count = 1)
        {
            var rules = GameBalance.CreateDefault();
            rules.Width = 96; rules.Height = 64; rules.EnableInkWells = false; rules.EnableEnemyMechanics = true;
            rules.Mechanics.RangedEnemies = true; rules.Mechanics.EnemyDrawingTiers = true;
            var targets = new PreparedEnemy[count];
            int columns = count > 1000 ? 80 : 24; float spacing = count > 1000 ? 1 : 3;
            for (int i = 0; i < count; i++)
                targets[i] = new PreparedEnemy(new Point2(6 + i % columns * spacing, 6 + i / columns * spacing), 100,
                    i % 3 == 0 ? EnemyKind.Spitter : i % 3 == 1 ? EnemyKind.Runner : EnemyKind.Doodle,
                    drawingTier: i % 3 == 2 ? DrawingTier.Outlined : DrawingTier.Sketch);
            return GameSession.CreatePrepared(rules, 771, targets);
        }
        static object Selector()
        {
            var type = typeof(NotebookWorldView).Assembly.GetType("PaperDefense.Presentation.BattleReadability");
            Assert.That(type, Is.Not.Null, "Missing bounded battle readability selector.");
            return Activator.CreateInstance(type);
        }
        static T Value<T>(object target, string name)
        {
            var property = target.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, "Missing readability property: " + name);
            return (T)property.GetValue(target);
        }
        static void Refresh(object selector, GameSession game, Rect viewport, float units)
            => selector.GetType().GetMethod("Refresh").Invoke(selector, new object[] { new RunReadModel(game), viewport, units });
        static object Mark(object selector, string method, int index)
            => selector.GetType().GetMethod(method).Invoke(selector, new object[] { index });
        static void Draw(object selector, MeshPen pen, NotebookVisualThemeAsset theme)
            => selector.GetType().GetMethod("Draw").Invoke(selector, new object[] { pen, theme });
        static Vector2 Home(GameSession game) => new Vector2(game.BaseCell.X + .5f, game.BaseCell.Y + .5f);

        [TestCase(360)] [TestCase(3000)]
        public void DenseCrowdsKeepBoundedSeparatedRoleMarks(int enemies)
        {
            var game = Game(enemies); var selector = Selector();
            var viewport = new Rect(0, 0, 96, 64);
            Refresh(selector, game, viewport, .15f);
            int count = Value<int>(selector, "RoleMarkerCount");
            Assert.That(count, Is.InRange(3, 24), "A crowd must show useful roles without adding a label for every enemy.");
            for (int i = 0; i < count; i++)
            {
                Rect bounds = Value<Rect>(Mark(selector, "GetRoleMarker", i), "WorldRect");
                Assert.That(viewport.Contains(bounds.min) && viewport.Contains(bounds.max), Is.True);
                for (int j = 0; j < i; j++)
                {
                    Rect other = Value<Rect>(Mark(selector, "GetRoleMarker", j), "WorldRect");
                    Assert.That(Vector2.Distance(bounds.center, other.center) / .15f, Is.GreaterThanOrEqualTo(17.99f));
                }
            }
            Assert.That(Value<int>(selector, "ThreatBracketCount"), Is.LessThanOrEqualTo(3));
        }

        [TestCase(.04f)] [TestCase(.15f)]
        public void RoleMarksUseScreenPixelsAndExcludeActorsOutsideTheCamera(float units)
        {
            var game = Game(3); var selector = Selector();
            game.Enemies[0].Position = new Point2(10, 10);
            game.Enemies[1].Position = new Point2(100, 100);
            game.Enemies[2].Position = new Point2(-100, 10);
            var viewport = new Rect(0, 0, 20, 20);
            Refresh(selector, game, viewport, units);
            Assert.That(Value<int>(selector, "RoleMarkerCount"), Is.EqualTo(1));
            var marker = Mark(selector, "GetRoleMarker", 0);
            Assert.That(Value<int>(marker, "EnemyId"), Is.EqualTo(game.Enemies[0].Id));
            Rect bounds = Value<Rect>(marker, "WorldRect");
            Assert.That(bounds.width / units, Is.InRange(8, 10.01f));
            Assert.That(bounds.height / units, Is.InRange(8, 10.01f));
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh) { WorldUnitsPerPixel = units };
                Draw(selector, pen, NotebookVisualThemeAsset.Default); pen.Upload();
                Assert.That(mesh.vertexCount, Is.GreaterThan(3), "The selected mark must actually reach the mesh.");
                Assert.That(mesh.bounds.size.x / units, Is.InRange(8, 12));
                Assert.That(mesh.bounds.size.y / units, Is.InRange(8, 12));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ArmorMeansDrawingTierAndCanCoexistWithTheEnemyRole()
        {
            var game = Game(3); var selector = Selector();
            game.Enemies[0].Kind = EnemyKind.Bulwark; game.Enemies[0].DrawingTier = DrawingTier.Sketch;
            game.Enemies[1].Kind = EnemyKind.Runner; game.Enemies[1].DrawingTier = DrawingTier.Hatched;
            game.Enemies[2].Kind = EnemyKind.Spitter; game.Enemies[2].DrawingTier = DrawingTier.Outlined;
            Refresh(selector, game, new Rect(0, 0, 96, 64), .1f);
            Assert.That(Value<int>(selector, "RoleMarkerCount"), Is.EqualTo(2), "Bulwark kind alone must not claim tier armor.");
            for (int i = 0; i < 2; i++)
            {
                var mark = Mark(selector, "GetRoleMarker", i);
                string roles = Value<object>(mark, "Roles").ToString();
                Assert.That(roles, Does.Contain("Armor"));
                Assert.That(roles, Does.Contain(Value<int>(mark, "EnemyId") == game.Enemies[1].Id ? "Runner" : "Spitter"));
            }
        }

        [Test]
        public void CastleCueCountsLivingNearbyEnemiesAndKeepsOnlyThreeNearestBrackets()
        {
            var game = Game(6); var selector = Selector(); Vector2 home = Home(game);
            for (int i = 0; i < 6; i++) game.Enemies[i].Position = new Point2(home.x + i + 1, home.y);
            game.Enemies[5].Health = 0;
            Refresh(selector, game, new Rect(0, 0, 96, 64), .1f);
            Assert.That(Value<int>(selector, "NearCastleCount"), Is.EqualTo(5));
            Assert.That(Value<int>(selector, "ThreatBracketCount"), Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
                Assert.That(Value<int>(Mark(selector, "GetThreatBracket", i), "EnemyId"), Is.EqualTo(game.Enemies[i].Id));
            Assert.That(Value<bool>(selector, "LowCastleHealth"), Is.False);
            typeof(GameSession).GetMethod("DamageObjective", Hidden).Invoke(game, new object[] { -1, game.BaseHealth * .7f, null });
            Refresh(selector, game, new Rect(0, 0, 96, 64), .1f);
            Assert.That(Value<bool>(selector, "LowCastleHealth"), Is.True, "Thirty percent health is included in the low-health cue.");
        }

        [Test]
        public void DeathAndNewRunRemoveOldMarkersAndCastleWarnings()
        {
            var game = Game(); var selector = Selector(); Vector2 home = Home(game);
            game.Enemies[0].Position = new Point2(home.x + 2, home.y);
            Refresh(selector, game, new Rect(0, 0, 96, 64), .1f);
            Assert.That(Value<int>(selector, "NearCastleCount"), Is.EqualTo(1));
            game.Enemies[0].Health = 0;
            Refresh(selector, game, new Rect(0, 0, 96, 64), .1f);
            Assert.That(Value<int>(selector, "RoleMarkerCount"), Is.Zero);
            Assert.That(Value<int>(selector, "ThreatBracketCount"), Is.Zero);
            Assert.That(Value<int>(selector, "NearCastleCount"), Is.Zero);
            var next = Game(); next.Enemies[0].Health = 0;
            Refresh(selector, next, new Rect(0, 0, 96, 64), .1f);
            Assert.That(Value<Guid>(selector, "RunId"), Is.EqualTo(next.RunId));
            Assert.That(Value<int>(selector, "NearCastleCount"), Is.Zero);
        }

        [Test]
        public void ReducedMotionMakesTheActualWindupWarningGeometryStatic()
        {
            var game = Game(); game.Enemies[0].WindupRemaining = .5f;
            var enemy = new RunReadModel(game).Enemies[0];
            var mesh = new Mesh(); var theme = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
            try
            {
                theme.Data.ReducedMotion = true;
                var pen = new MeshPen(mesh);
                var draw = typeof(DoodleGlyphs).GetMethod("EnemyMarks", BindingFlags.NonPublic | BindingFlags.Static);
                draw.Invoke(null, new object[] { pen, enemy, 0f, theme }); pen.Upload();
                var vertices = mesh.vertices; var colors = mesh.colors32;
                pen.Clear(); draw.Invoke(null, new object[] { pen, enemy, .1f, theme }); pen.Upload();
                CollectionAssert.AreEqual(vertices, mesh.vertices, "Reduced motion must stop the procedural warning pulse too.");
                CollectionAssert.AreEqual(colors, mesh.colors32);
                Assert.That(vertices.Length, Is.GreaterThan(3), "Accessibility must keep the warning visible.");
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(theme); }
        }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void WorldIntegrationUsesExistingBatchesAndDoesNotChangeSimulation(int width, int height)
        {
            var root = new GameObject("Battle readability fixture"); root.SetActive(false);
            var target = new RenderTexture(width, height, 0);
            try
            {
                var camera = root.AddComponent<Camera>(); camera.orthographic = true; camera.targetTexture = target;
                camera.orthographicSize = 34; camera.transform.position = new Vector3(48, 32, -10);
                var game = Game(360); var read = new RunReadModel(game);
                var world = root.AddComponent<NotebookWorldView>(); world.WorldCamera = camera; world.ModernEnemyPresentation = true;
                world.Initialize(read);
                int children = root.GetComponentsInChildren<Transform>(true).Length;
                int batches = root.GetComponentsInChildren<MeshRenderer>(true).Length;
                var positions = new Point2[game.Enemies.Count];
                for (int i = 0; i < positions.Length; i++) positions[i] = game.Enemies[i].Position;
                long tick = read.TickIndex; float elapsed = read.Elapsed, health = read.BaseHealth;
                int ink = read.Ink, topology = read.TopologyVersion;
                typeof(NotebookWorldView).GetMethod("LateUpdate", Hidden).Invoke(world, null);
                var selector = Value<object>(world, "Readability");
                Assert.That(Value<int>(selector, "RoleMarkerCount"), Is.InRange(3, 24));
                camera.orthographicSize *= .5f;
                typeof(NotebookWorldView).GetMethod("LateUpdate", Hidden).Invoke(world, null);
                Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(children));
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length, Is.EqualTo(batches));
                Assert.That(read.TickIndex, Is.EqualTo(tick)); Assert.That(read.Elapsed, Is.EqualTo(elapsed));
                Assert.That(read.BaseHealth, Is.EqualTo(health)); Assert.That(read.Ink, Is.EqualTo(ink));
                Assert.That(read.TopologyVersion, Is.EqualTo(topology));
                for (int i = 0; i < positions.Length; i++) Assert.That(game.Enemies[i].Position, Is.EqualTo(positions[i]));
                world.Bind(null);
                Assert.That(Value<int>(selector, "RoleMarkerCount"), Is.Zero);
                Assert.That(Value<int>(selector, "NearCastleCount"), Is.Zero);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(target); }
        }
    }

#if UNITY_EDITOR
    public sealed class BattleReadabilityHudTests : BattleHudThemeFixture
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        Text Caption(string field) => (Text)typeof(PlayerBattleHud).GetField(field, Hidden).GetValue(Hud);

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void CastleCuesFitBesideTheLiveCountAndSurviveResize(int width, int height)
        {
            StartBattle(); Size(new Vector2Int(width, height));
            game.Run.NextWave();
            for (int i = 0; i < 120 && game.Run.Session.Enemies.Count == 0; i++) game.Run.StepSimulation();
            var session = (GameSession)typeof(RunController).GetField("session", Hidden).GetValue(game.Run);
            Assert.That(session.Enemies.Count, Is.EqualTo(1), "One real spawned enemy is the proximity fixture.");
            session.Enemies[0].Position = new Point2(session.BaseCell.X + 2.5f, session.BaseCell.Y + .5f);
            typeof(GameSession).GetMethod("DamageObjective", Hidden).Invoke(session, new object[] { -1, session.BaseHealth * .75f, null });
            game.World.SendMessage("LateUpdate"); Hud.Refresh(); Canvas.ForceUpdateCanvases();
            foreach (var size in new[] { new Vector2Int(width, height), new Vector2Int(height == 600 ? 1600 : 800, height == 600 ? 900 : 600) })
            {
                Size(size);
                var phase = Caption("albumPhase"); var health = Caption("albumHealth");
                Assert.That(phase.text, Does.Contain("Рядом с замком: 1"));
                Assert.That(phase.text.Split('\n')[0], Does.Contain("1"), "Keep the live enemy count above the cue.");
                Assert.That(health.text, Does.Contain("Мало HP"));
                Assert.That(health.text, Does.Contain(Mathf.CeilToInt(session.BaseHealth).ToString()));
                foreach (var label in new[] { phase, health })
                {
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1), label.text);
                    Assert.That(label.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(label.text.Length), label.text);
                }
            }
            session.Enemies[0].Health = 0;
            game.World.SendMessage("LateUpdate"); Hud.Refresh();
            Assert.That(Caption("albumPhase").text, Does.Not.Contain("Рядом с замком"));
        }
    }
#endif
}
