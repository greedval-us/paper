#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using PaperDefense.Validation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class NotebookArtPressureTests
    {
        static void DeliverEvents(PressureScenario scenario, NotebookWorldView view, ref long cursor)
        {
            foreach (var item in scenario.Session.EventBuffer)
                if (item.Sequence > cursor) view.AddEvent(item);
            cursor = scenario.Session.EventBuffer.LatestSequence;
        }

        static Mesh WorldMesh(GameObject root) => root.GetComponentsInChildren<MeshFilter>()
            .Single(filter => filter.name == "Swarm, aiming weapons and construction").sharedMesh;

        static bool ContainsBody(Vector3[] batch, Vector3[] body)
        {
            if (body.Length == 0) return false;
            for (int start = 0; start <= batch.Length - body.Length; start++)
            {
                if ((batch[start] - body[0]).sqrMagnitude > .00000001f) continue;
                bool matches = true;
                for (int i = 1; i < body.Length; i++)
                    if ((batch[start + i] - body[i]).sqrMagnitude > .00000001f) { matches = false; break; }
                if (matches) return true;
            }
            return false;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ConfiguredNotebookArtRendersTheNaturalThreeThousandEnemyPeakWithoutChangingTheBattle()
        {
            var configured = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/TargetNotebookBalance.asset");
            Assert.That(configured, Is.Not.Null);
            var theme = NotebookVisualThemeAsset.Default;
            Assert.That(theme, Is.Not.Null, "The pressure check requires the imported notebook art, not missing-resource fallback glyphs.");
            // Physical spacing needs enough arena area for 3000 full-size bodies.
            var scenario = new PressureScenario(new PressureScenarioConfig { Width = 192, Height = 128 }, configured.CreateBalance());
            Assert.That(scenario.ReadModel.Battlefield, Is.Null, "The dedicated four-sided ring benchmark owns its open-field arena.");
            Assert.That(configured.CreateBalance().Battlefield, Is.Not.Null, "Preparing the benchmark must preserve the actual production map.");
            Assert.That(scenario.Report.FailedCommands, Is.Zero, "Configured Target rules must accept the pressure fixture's purchased outer wall ring.");
            while (scenario.ReadModel.Enemies.Count < 300 && !scenario.Finished && scenario.ReadModel.TickIndex < 1800) scenario.Step();
            Assert.That(scenario.ReadModel.Enemies.Count, Is.InRange(300, 1000), "First sample the detailed resource path before compact crowd rendering begins.");

            var root = new GameObject("Isolated notebook art pressure renderer");
            var expectedMesh = new Mesh();
            try
            {
                var cameraObject = new GameObject("Pressure viewport", typeof(Camera)); cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
                camera.orthographicSize = scenario.Config.Height * .5f + 2;
                camera.aspect = scenario.Config.Width / (float)scenario.Config.Height;
                camera.transform.position = new Vector3(scenario.Config.Width * .5f, scenario.Config.Height * .5f, -10);
                var view = root.AddComponent<NotebookWorldView>(); view.WorldCamera = camera; view.VisualTheme = theme; view.Paused = true;
                view.Initialize(scenario.ReadModel);
                long cursor = scenario.Session.EventBuffer.LatestSequence;
                yield return null;
                scenario.Step(); DeliverEvents(scenario, view, ref cursor);
                yield return null;

                var firstEnemy = scenario.ReadModel.Enemies[0];
                var motion = view.AnimationState.Observe(firstEnemy, view.VisualSeconds, out float phase, theme);
                Assert.That(motion, Is.EqualTo(NotebookEnemyMotion.Walk), "The detailed sample must exercise authored movement poses from real core motion.");
                // Compare the same resource at the same screen-space ink width as the world batch.
                var worldPen = (MeshPen)typeof(NotebookWorldView).GetField("worldPen",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(view);
                var expectedPen = new MeshPen(expectedMesh)
                {
                    WorldUnitsPerPixel = worldPen.WorldUnitsPerPixel,
                    MinimumStrokePixels = worldPen.MinimumStrokePixels
                };
                Assert.That(DoodleGlyphs.DrawResourceEnemy(expectedPen, firstEnemy.Kind, firstEnemy.DrawingTier,
                    new Vector2(firstEnemy.Position.X, firstEnemy.Position.Y), motion, phase, theme, firstEnemy.VisualScale), Is.True);
                expectedPen.Upload();
                Assert.That(ContainsBody(WorldMesh(root).vertices, expectedMesh.vertices), Is.True,
                    "The world batch must contain the actual resource triangles for the sampled moving enemy.");
                int detailedChildren = root.GetComponentsInChildren<Transform>(true).Length;
                int detailedBatches = root.GetComponentsInChildren<MeshRenderer>(true).Length;

                double coreMilliseconds = 0, maximumCoreMilliseconds = 0; int coreSamples = 0;
                while (scenario.ReadModel.Enemies.Count < scenario.Config.EnemyCount && !scenario.Finished && scenario.ReadModel.TickIndex < 1800)
                {
                    long started = System.Diagnostics.Stopwatch.GetTimestamp();
                    scenario.Step();
                    double milliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    coreMilliseconds += milliseconds; maximumCoreMilliseconds = System.Math.Max(maximumCoreMilliseconds, milliseconds); coreSamples++;
                    DeliverEvents(scenario, view, ref cursor);
                }
                Assert.That(scenario.ReadModel.Enemies.Count, Is.EqualTo(3000),
                    "The Target-configured pressure fixture must reach its natural live peak; no enemy state is injected or rewritten.");
                Assert.That(scenario.Report.PeakAliveEnemies, Is.EqualTo(3000));
                var crowdRules = scenario.Session.Balance.Crowd;
                Assert.That(crowdRules.Enabled, Is.True, "The pressure fixture must exercise production crowd movement.");
                for (int i = 0; i < scenario.Session.Enemies.Count; i++)
                    for (int j = i + 1; j < scenario.Session.Enemies.Count; j++)
                    {
                        var a = scenario.Session.Enemies[i]; var b = scenario.Session.Enemies[j];
                        float dx = a.Position.X - b.Position.X, dy = a.Position.Y - b.Position.Y;
                        float minimum = (a.VisualScale + b.VisualScale) * crowdRules.RadiusPerVisualScale;
                        if (dx * dx + dy * dy < minimum * minimum - .001f) Assert.Fail("Pressure bodies overlap: " + a.Id + "/" + b.Id);
                    }
                TestContext.WriteLine("Natural crowd core: samples={0}, average={1:F3}ms, max={2:F3}ms", coreSamples, coreMilliseconds / System.Math.Max(1, coreSamples), maximumCoreMilliseconds);
                Assert.That(scenario.Report.FailedCommands, Is.Zero);
                yield return null; yield return null; // Warm the compact path and any already-started effect shapes.

                string fingerprint = scenario.Fingerprint();
                long tick = scenario.ReadModel.TickIndex;
                float elapsed = scenario.ReadModel.Elapsed;
                int staticBuilds = view.StaticGeometryRefreshCount;
                int authoredBuilds = AuthoredGeometryCache.BuildCount, resourceBuilds = NotebookArtRenderer.GeometryBuildCount;
                int warmedMeshes = view.MeshBuildCount;
                double beforeMeshMilliseconds = view.TotalMeshBuildMilliseconds, maximumMeshMilliseconds = 0;
                int compactVertices = WorldMesh(root).vertexCount;
                Assert.That(compactVertices, Is.GreaterThan(3000 * 3), "The live crowd must contribute batched geometry.");
                int compactPerEnemy = compactVertices / scenario.ReadModel.Enemies.Count;
                Assert.That(compactPerEnemy, Is.LessThan(expectedMesh.vertexCount), "The large crowd must use the compact path while the detailed resource path remains available.");
                for (int frame = 0; frame < 30; frame++)
                {
                    yield return null;
                    maximumMeshMilliseconds = System.Math.Max(maximumMeshMilliseconds, view.LastMeshBuildMilliseconds);
                    Assert.That(view.ActiveVisualEffects, Is.LessThanOrEqualTo(theme.MaximumEffects + theme.MaximumDeaths));
                    Assert.That(view.AnimationState.MountCount, Is.LessThanOrEqualTo(NotebookAnimationState.MaximumMounts));
                    Assert.That(view.AnimationState.EnemyCount, Is.LessThanOrEqualTo(NotebookAnimationState.MaximumEnemies));
                }
                Assert.That(view.MeshBuildCount - warmedMeshes, Is.GreaterThanOrEqualTo(30), "Normal LateUpdate must render every sampled frame.");
                Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(detailedChildren), "Crowd size cannot create per-enemy GameObjects.");
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length, Is.EqualTo(detailedBatches), "The same mesh layers render both 300 and 3000 enemies.");
                Assert.That(view.StaticGeometryRefreshCount, Is.EqualTo(staticBuilds), "Presentation-only frames must reuse the static layer.");
                Assert.That(AuthoredGeometryCache.BuildCount, Is.EqualTo(authoredBuilds));
                Assert.That(NotebookArtRenderer.GeometryBuildCount, Is.EqualTo(resourceBuilds), "Warmed immutable resource geometry must not rebuild each frame.");
                Assert.That(scenario.ReadModel.TickIndex, Is.EqualTo(tick)); Assert.That(scenario.ReadModel.Elapsed, Is.EqualTo(elapsed));
                Assert.That(scenario.Fingerprint(), Is.EqualTo(fingerprint), "Rendering and animation are observers of the exact same core state.");
                int samples = view.MeshBuildCount - warmedMeshes;
                TestContext.WriteLine("Notebook art pressure: alive={0}, renderedFrames={1}, batches={2}, compactVertices={3}, meanMeshMs={4:F3}, maximumMeshMs={5:F3}, tick={6}, fingerprint={7}",
                    scenario.ReadModel.Enemies.Count, samples, detailedBatches, compactVertices,
                    (view.TotalMeshBuildMilliseconds - beforeMeshMilliseconds) / samples, maximumMeshMilliseconds, tick, fingerprint);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(expectedMesh); }
        }
    }
}
#endif
