#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class AlbumBattleCameraTests
    {
        AssemblyCommandFixture fixture;
        GameObject owner;
        PlayerGameRoot game;
        PlayerShell shell;

        [UnitySetUp] public IEnumerator SetUp()
        {
            fixture = new AssemblyCommandFixture();
            owner = new GameObject("Isolated album focused camera"); owner.SetActive(false);
            game = owner.AddComponent<PlayerGameRoot>(); game.Configure(fixture.BalanceAsset, true);
            owner.SetActive(true); game.Run.AutomaticSimulation = false; game.Run.InitializeWorkshop(fixture.Store);
            game.enabled = false; shell = owner.GetComponentInChildren<PlayerShell>(); shell.enabled = false;
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            EventSystem.current?.SetSelectedGameObject(null);
            if (owner != null) Object.Destroy(owner);
            yield return null;
            fixture?.Dispose();
        }
        Button Overview() => owner.GetComponentsInChildren<Button>().SingleOrDefault(button => button.name == "Battle overview");

        [Test] public void ModernBattleOpensOnReadableCastleFocusWithAnOverviewControl()
        {
            Assert.That(game.StartBattle(), Is.True);
            Assert.That(game.Drawing.Zoom, Is.EqualTo(6).Within(.001f), "The readable default view respects the authored maximum of six.");
            var settings = game.Drawing.ViewSettings != null ? game.Drawing.ViewSettings : NotebookViewSettingsAsset.Default;
            Assert.That(game.Drawing.Zoom, Is.LessThanOrEqualTo(settings.MaximumZoom));
            var baseCell = game.Run.Session.BaseCell;
            Assert.That(Vector2.Distance(game.Drawing.CameraCenter, new Vector2(baseCell.X + .5f, baseCell.Y + .5f)), Is.LessThan(.01f));
            Assert.That(Overview(), Is.Not.Null);
        }

        [Test] public void OverviewAndCastleFocusPreserveTheAttemptResourcesAndFrozenPages()
        {
            Assert.That(game.StartBattle(), Is.True); var session = game.Run.Session;
            long tick = session.TickIndex; int ink = session.Ink; Guid runId = session.RunId;
            string[] pages = game.Run.RunLoadout.Entries.Select(entry => entry.Id).ToArray();
            var overview = Overview(); Assert.That(overview, Is.Not.Null, "The entire map must remain one click away.");
            overview.onClick.Invoke();
            Assert.That(game.Drawing.Zoom, Is.EqualTo(1));
            Assert.That(game.Drawing.CameraCenter, Is.EqualTo(new Vector2(session.Balance.Width * .5f, session.Balance.Height * .5f)));
            Overview().onClick.Invoke(); Assert.That(game.Drawing.Zoom, Is.EqualTo(6).Within(.001f));
            Assert.That(game.Run.Session, Is.SameAs(session)); Assert.That(session.RunId, Is.EqualTo(runId));
            Assert.That(session.TickIndex, Is.EqualTo(tick)); Assert.That(session.Ink, Is.EqualTo(ink));
            Assert.That(game.Run.RunLoadout.Entries.Select(entry => entry.Id), Is.EqualTo(pages));
            Assert.That(fixture.Store.SaveCalls, Is.Zero);
        }

        [Test] public void FocusedMapKeepsAllFourEnemyApproachesVisibleAtTheFieldEdges()
        {
            Assert.That(game.StartBattle(), Is.True); shell.ApplyLayout(1280, 720); Canvas.ForceUpdateCanvases();
            var indicators = owner.GetComponentsInChildren<RectTransform>().Where(rect => rect.name.StartsWith("Battle approach ", StringComparison.Ordinal)).ToArray();
            Assert.That(indicators.Length, Is.EqualTo(4), "Cropping the full map must keep the four approach directions visible.");
            var root = indicators[0].parent as RectTransform;
            foreach (var indicator in indicators)
            {
                Assert.That(indicator.gameObject.activeInHierarchy, Is.True);
                var corners = new Vector3[4]; indicator.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector2 point = root.InverseTransformPoint(corner);
                    var field = shell.Layout.Field;
                    Assert.That(point.x, Is.InRange(field.xMin, field.xMax));
                    Assert.That(-point.y, Is.InRange(field.yMin, field.yMax));
                }
            }
        }

        [Test] public void RealAssaultRetiresOnlyTheInitialPlanningInstruction()
        {
            Assert.That(game.StartBattle(), Is.True);
            string planning = game.Drawing.Message;
            Assert.That(planning, Does.Contain("начни волну"));
            game.Run.NextWave(); game.Run.AdvanceFrame(1f / 30, false, 1);
            Assert.That(game.Run.Session.Phase, Is.EqualTo(RunPhase.Assault));
            Assert.That(game.Run.Session.Enemies.Count + game.Run.Session.RemainingToSpawn, Is.GreaterThan(0));
            var hud = (PlayerBattleHud)typeof(PlayerShell).GetField("battle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell);
            hud.Refresh();
            Assert.That(owner.GetComponentsInChildren<Button>().Single(button => button.name == "Launch wave").interactable, Is.False);
            Assert.That(game.Drawing.Message, Is.Not.EqualTo(planning), "A real active wave cannot keep the initial instruction to start it.");
            Assert.That(owner.GetComponentsInChildren<Text>().Any(label => label.text == planning), Is.False);
            game.Drawing.SetMessage("Не хватает штрихов для постройки"); hud.Refresh();
            Assert.That(game.Drawing.Message, Is.EqualTo("Не хватает штрихов для постройки"), "Actual action and error messages remain visible during combat.");
            Assert.That(owner.GetComponentsInChildren<Text>().Any(label => label.text == game.Drawing.Message), Is.True);
        }

        [Test] public void RetiringTooltipsDoesNotRedrawAnInactiveHudHierarchy()
        {
            Assert.That(game.StartBattle(), Is.True);
            var overview = Overview();
            var tooltip = overview.GetComponent<PlayerTooltip>();
            tooltip.OnPointerEnter(new PointerEventData(EventSystem.current));
            var caption = owner.GetComponentsInChildren<Text>().Single(text => text.name == "Battle tool hint");
            string visibleHint = caption.text;
            Assert.That(visibleHint, Does.Contain("Колесо"), "The active tooltip must be showing before its owner retires.");
            var controls = owner.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Battle controls");
            controls.gameObject.SetActive(false);
            Assert.That(owner.activeInHierarchy, Is.True, "The battle canvas can retire while the game owner is still active.");
            Assert.That(caption.text, Is.EqualTo(visibleHint), "OnDisable clears hint state without rebuilding an inactive hierarchy.");
            controls.gameObject.SetActive(true);
            var hud = (PlayerBattleHud)typeof(PlayerShell).GetField("battle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell);
            hud.Refresh();
            Assert.That(caption.text, Does.Not.Contain("Колесо"), "The retired hint must not return when the owner becomes active.");
            Object.DestroyImmediate(owner); owner = null;
            Assert.That(fixture.Store.SaveCalls, Is.Zero);
        }

        [TestCase(true, .7f)] [TestCase(false, 1f)]
        public void EnemyBodyHealthAndDeathUseOnePresentationTransformWithoutChangingSimulation(bool modern, float expectedScale)
        {
            GameObject legacyOwner = null;
            var expectedMesh = new Mesh();
            try
            {
                NotebookWorldView view;
                IRunReadModel read;
                if (modern)
                {
                    Assert.That(game.StartBattle(), Is.True);
                    game.Run.NextWave(); game.Run.AdvanceFrame(1f / 30, false, 1);
                    view = game.World; read = game.Run.Session;
                }
                else
                {
                    var session = new GameSession(GameBalance.CreateDefault(), starterDefenses: false);
                    session.StartNextWave(); session.Tick(1f / 30);
                    read = new RunReadModel(session);
                    legacyOwner = new GameObject("Isolated legacy enemy renderer");
                    view = legacyOwner.AddComponent<NotebookWorldView>(); view.Initialize(read);
                }
                Assert.That(read.Enemies.Count, Is.GreaterThan(0), "Use an enemy spawned by the actual wave simulation.");
                var enemy = read.Enemies[0];
                var origin = new Vector3(enemy.Position.X, enemy.Position.Y, 0);
                var savedPosition = enemy.Position; float savedScale = enemy.VisualScale, savedHealth = enemy.Health;
                view.WorldCamera = null; // Include the real spawn cells outside the focused viewport.
                typeof(NotebookWorldView).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
                var actualMesh = view.GetComponentsInChildren<MeshFilter>().Single(filter => filter.sharedMesh.name == "Swarm, aiming weapons and construction").sharedMesh;
                var pen = new MeshPen(expectedMesh);
                DoodleGlyphs.Enemy(pen, enemy, (float)view.VisualSeconds, animation: view.AnimationState,
                    theme: view.VisualTheme, windupSeconds: read.Balance.Enemy(enemy.Kind).WindupSeconds);
                pen.Upload();
                Assert.That(expectedMesh.vertexCount, Is.GreaterThan(4), "The baseline includes the body and the full HP bar.");
                var baselineUv = new List<Vector2>(); var actualUv = new List<Vector2>();
                expectedMesh.GetUVs(1, baselineUv); actualMesh.GetUVs(1, actualUv);
                int first = -1;
                for (int offset = 0; offset + baselineUv.Count <= actualUv.Count; offset++)
                    if (actualUv.Skip(offset).Take(baselineUv.Count).SequenceEqual(baselineUv)) { first = offset; break; }
                Assert.That(first, Is.GreaterThanOrEqualTo(0), "Locate this actual atlas body followed by its unchanged HP geometry.");
                var actual = actualMesh.vertices; var baseline = expectedMesh.vertices;
                for (int i = 0; i < baseline.Length; i++)
                    Assert.That(Vector3.Distance(actual[first + i], origin + (baseline[i] - origin) * expectedScale), Is.LessThan(.0001f),
                        $"Body/HP vertex {i} must share the same view transform about the recorded position.");

                var events = new SimulationEventBuffer();
                events.Publish(read.TickIndex, SimulationEventKind.EnemyDied, enemy.Position, enemy.Position, default,
                    entityId: enemy.Id, enemyKind: enemy.Kind, enemyDrawingTier: enemy.DrawingTier,
                    enemyVisualScale: enemy.VisualScale, simulationSeconds: view.VisualSeconds);
                view.AddEvent(events[0]);
                typeof(NotebookWorldView).GetMethod("DrawWorld", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
                pen.Clear();
                DoodleGlyphs.DrawResourceEnemy(pen, enemy.Kind, enemy.DrawingTier, new Vector2(origin.x, origin.y),
                    NotebookEnemyMotion.Death, 0, view.VisualTheme, enemy.VisualScale); pen.Upload();
                actual = actualMesh.vertices; baseline = expectedMesh.vertices;
                Assert.That(baseline.Length, Is.EqualTo(4), "The death pose remains one atlas illustration.");
                for (int i = 0; i < baseline.Length; i++)
                    Assert.That(Vector3.Distance(actual[actual.Length - baseline.Length + i], origin + (baseline[i] - origin) * expectedScale), Is.LessThan(.0001f),
                        "The death tail must retain the same whole-enemy presentation scale.");
                var after = read.Enemies[0];
                Assert.That(after.Position, Is.EqualTo(savedPosition)); Assert.That(after.VisualScale, Is.EqualTo(savedScale));
                Assert.That(after.Health, Is.EqualTo(savedHealth));
            }
            finally
            {
                Object.DestroyImmediate(expectedMesh);
                if (legacyOwner != null) Object.DestroyImmediate(legacyOwner);
            }
        }

        [Test] public void ModernTrialWithoutAffordableCopiesKeepsTheBattleEnemyPresentationTransform()
        {
            var report = TrialRunner.Run(fixture.Gun(), fixture.Balance, TrialScenario.Crowd, budget: 0);
            Assert.That(report.Copies, Is.Zero);
            var frame = report.Frames.First(value => value.Enemies.Count > 0);
            var enemy = frame.Enemies[0]; var origin = new Vector3(enemy.Position.X, enemy.Position.Y, 0);
            var playback = new NotebookTrialPlayback(); playback.Bind(report); playback.Seek(frame.Seconds);
            var actualMesh = new Mesh(); var expectedMesh = new Mesh();
            try
            {
                var actualPen = new MeshPen(actualMesh);
                // This is the real modern lane route. Modern presentation cannot be inferred
                // from existing towers: an unaffordable trial has no tower snapshots at all.
                NotebookTrialRenderer.Draw(actualPen, playback, new Rect(8, 8, 42, 28)); actualPen.Upload();
                var expectedPen = new MeshPen(expectedMesh);
                DoodleGlyphs.Enemy(expectedPen, enemy, (float)playback.Seconds, animation: playback.Animation,
                    sampleSeconds: frame.Seconds); expectedPen.Upload();
                var expectedUv = new List<Vector2>(); var actualUv = new List<Vector2>();
                expectedMesh.GetUVs(1, expectedUv); actualMesh.GetUVs(1, actualUv);
                int first = -1;
                for (int offset = 0; offset + expectedUv.Count <= actualUv.Count; offset++)
                    if (actualUv.Skip(offset).Take(expectedUv.Count).SequenceEqual(expectedUv)) { first = offset; break; }
                Assert.That(first, Is.GreaterThanOrEqualTo(0));
                var actual = actualMesh.vertices; var baseline = expectedMesh.vertices;
                Assert.That(baseline.Length, Is.GreaterThan(4), "Body and health must both be present in the real recorded lane.");
                for (int i = 0; i < baseline.Length; i++)
                    Assert.That(Vector3.Distance(actual[first + i], origin + (baseline[i] - origin) * .7f), Is.LessThan(.0001f),
                        $"Modern trial body/HP vertex {i} must match the battle presentation even without copies.");
                Assert.That(frame.Enemies[0].Position, Is.EqualTo(enemy.Position));
                Assert.That(frame.Enemies[0].VisualScale, Is.EqualTo(enemy.VisualScale));
            }
            finally { Object.DestroyImmediate(actualMesh); Object.DestroyImmediate(expectedMesh); }
        }
    }
}
#endif
