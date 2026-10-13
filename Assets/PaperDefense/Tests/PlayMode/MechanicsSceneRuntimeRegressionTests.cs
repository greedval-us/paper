#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using PaperDefense.Validation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class MechanicsSceneRuntimeRegressionTests
    {
        const string SceneRoot = "Assets/PaperDefense/Scenes/Tests/";
        const int FrameLimit = 240;
        const int RenderFrames = 3;
        readonly List<string> fixtureDirectories = new List<string>();
        string[] profileFiles;
        string persistenceError;

        [SetUp] public void ObserveUserProfileMetadata()
        {
            // Contents are never opened. Only the stand's own temporary fixture may be written.
            profileFiles = ProfileMetadata();
            persistenceError = WorkshopPersistence.LastError;
            fixtureDirectories.Clear();
        }

        [UnityTearDown] public IEnumerator UnloadRuntimeAndCheckProfileIsolation()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path == MechanicsSceneCatalog.HubScenePath || scene.path.StartsWith(SceneRoot, StringComparison.Ordinal))
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("Mechanics runtime regression complete"));
                yield return SceneManager.UnloadSceneAsync(scene);
                yield return null;
            }
            foreach (string directory in fixtureDirectories)
                Assert.That(Directory.Exists(directory), Is.False, "The profile stand must dispose its temporary fixture: " + directory);
            Assert.That(ProfileMetadata(), Is.EqualTo(profileFiles), "Runtime mechanics tests must not alter the user's profile.");
            Assert.That(WorkshopPersistence.LastError, Is.EqualTo(persistenceError));
        }

        [UnityTest] public IEnumerator ConstructionRendersRangesWithWallsAndSpikesThroughUpgradeEraseAndReset()
        {
            yield return OpenStand("10_ConstructionNavigation.unity");
            var stand = CurrentStand();
            var controller = stand.Controller;
            var view = stand.Bootstrap.View;
            Assert.That(controller.Session.Structures.Any(value => value.Kind == StructureKind.Wall), Is.True);
            Assert.That(controller.Session.Structures.Any(value => value.Kind == StructureKind.Spikes), Is.True);
            view.ShowRanges = true;
            yield return AssertRenderedFrames(view, "Construction with its initial walls and spikes");

            var newWall = new Cell(19, 12);
            Assert.That(controller.Build(new[] { newWall }, StructureKind.Wall, MaterialTier.Pencil, out string reason), Is.True, reason);
            int remainingSteps = 240;
            while (!controller.Session.StructureAt(newWall).Value.IsComplete && remainingSteps-- > 0) stand.Step();
            Assert.That(controller.Session.StructureAt(newWall).Value.IsComplete, Is.True, "The wall must finish before upgrading.");
            Assert.That(controller.Upgrade(newWall, out reason), Is.True, reason);
            Assert.That(controller.Session.StructureAt(newWall).Value.Tier, Is.EqualTo(MaterialTier.Ink));
            yield return AssertRenderedFrames(view, "Construction after upgrading a wall");
            Assert.That(controller.Erase(newWall, out _), Is.True);
            Assert.That(controller.Session.StructureAt(newWall), Is.Null);
            yield return AssertRenderedFrames(view, "Construction after erasing a wall");

            stand.ResetScenario();
            Assert.That(stand.Prepared, Is.True, stand.PreparationError);
            Pause(stand);
            Assert.That(stand.Controller.Session.StructureAt(newWall), Is.Null, "Reset must discard the previous construction.");
            stand.Bootstrap.View.ShowRanges = true;
            yield return AssertRenderedFrames(stand.Bootstrap.View, "Construction after reset");
        }

        [UnityTest] public IEnumerator IntegrationRendersRangesAfterStartingBattleAndBuildingMixedDefenses()
        {
            yield return OpenStand("13_Integration.unity");
            var stand = CurrentStand();
            var controller = stand.Controller;
            Assert.That(controller.InWorkshop, Is.True);
            Assert.That(controller.StartRun(out string reason), Is.True, reason);
            Pause(stand);
            Assert.That(controller.Build(new[] { new Cell(18, 12) }, StructureKind.Wall, MaterialTier.Pencil, out reason), Is.True, reason);
            Assert.That(controller.Build(new[] { new Cell(18, 13) }, StructureKind.Spikes, MaterialTier.Pencil, out reason), Is.True, reason);
            Assert.That(controller.BuildBlueprint(new[] { new Cell(19, 12) }, 0, MaterialTier.Pencil, InkColor.None, out reason), Is.True, reason);
            Assert.That(controller.Session.Structures.Select(value => value.Kind), Does.Contain(StructureKind.Wall));
            Assert.That(controller.Session.Structures.Select(value => value.Kind), Does.Contain(StructureKind.Spikes));
            Assert.That(controller.Session.Structures.Any(value => value.Blueprint != null), Is.True);
            stand.Bootstrap.View.ShowRanges = true;
            yield return AssertRenderedFrames(stand.Bootstrap.View, "Integration with mixed defenses");
            for (int step = 0; step < 6; step++) stand.Step();
            yield return AssertRenderedFrames(stand.Bootstrap.View, "Integration after simulation steps");

            controller.OpenWorkshop();
            yield return AssertRenderedFrames(stand.Bootstrap.View, "Integration returning to the workshop");
            Assert.That(controller.Session.Structures, Is.Empty);
            Assert.That(controller.StartRun(out reason), Is.True, reason);
            Pause(stand);
            yield return AssertRenderedFrames(stand.Bootstrap.View, "Integration starting a fresh battle");
            Assert.That(controller.Session.Structures, Is.Empty, "The next battle must not retain old structures.");
        }

        [UnityTest] public IEnumerator EveryHubRouteCompletesRenderedFramesAfterItsMechanicRuns()
        {
            EditorSceneManager.LoadSceneInPlayMode(MechanicsSceneCatalog.HubScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return WaitForScene(MechanicsSceneCatalog.HubScenePath);
            var entries = MechanicsSceneCatalog.Default.Scenes.ToArray();
            Assert.That(entries.Length, Is.EqualTo(26));
            foreach (var entry in entries)
            {
                var hub = Object.FindFirstObjectByType<MechanicsSceneHub>();
                Click(hub, "Open test " + entry.Id);
                yield return WaitForScene(entry.ScenePath);
                var stand = Object.FindFirstObjectByType<MechanicsTestScene>();
                if (stand != null)
                {
                    Assert.That(stand.Prepared, Is.True, entry.Id + " " + stand.PreparationError);
                    Pause(stand);
                    if (stand.scenario.focus == MechanicsTestFocus.Profile)
                    {
                        fixtureDirectories.Add(stand.FixtureDirectory);
                        Assert.That(stand.SaveProfileVariant(), Is.True, entry.Id);
                        stand.ReloadFixture();
                        Assert.That(stand.Prepared, Is.True, entry.Id + " " + stand.PreparationError);
                    }
                    else
                    {
                        if (stand.Controller.InWorkshop)
                            Assert.That(stand.Controller.StartRun(out string reason), Is.True, entry.Id + " " + reason);
                        Pause(stand);
                        stand.Bootstrap.View.ShowRanges = true;
                        for (int step = 0; step < 6; step++) stand.Step();
                        if (stand.scenario.focus == MechanicsTestFocus.Feed || stand.scenario.focus == MechanicsTestFocus.Color)
                        {
                            stand.ChangeVariant();
                            Assert.That(stand.Prepared, Is.True, entry.Id + " " + stand.PreparationError);
                            Pause(stand);
                            stand.Bootstrap.View.ShowRanges = true;
                        }
                        if (stand.scenario.focus == MechanicsTestFocus.Wells)
                            Assert.That(stand.Controller.ActivateWell(0, out _), Is.True, entry.Id);
                        if (stand.scenario.focus == MechanicsTestFocus.Blots)
                            Assert.That(stand.Controller.Blotter(new Cell(24, 16), out _), Is.True, entry.Id);
                    }
                    yield return AssertRenderedFrames(stand.Bootstrap.View, entry.Id);
                }
                else
                {
                    var art = Object.FindFirstObjectByType<NotebookArtStand>();
                    Assert.That(art, Is.Not.Null, entry.Id);
                    art.SetPaused(true);
                    Click(art, "Step");
                    for (int frame = 0; frame < RenderFrames; frame++) yield return null;
                    Canvas.ForceUpdateCanvases();
                    var graphics = art.GetComponentsInChildren<NotebookArtGraphic>();
                    Assert.That(graphics.Length, Is.GreaterThan(0), entry.Id);
                    foreach (var graphic in graphics)
                    {
                        var mesh = graphic.canvasRenderer.GetMesh();
                        Assert.That(mesh, Is.Not.Null, entry.Id + " " + graphic.name);
                        Assert.That(mesh.vertexCount, Is.GreaterThan(0), entry.Id + " " + graphic.name);
                    }
                    LogAssert.NoUnexpectedReceived();
                }
                Object.FindFirstObjectByType<MechanicsSceneNavigator>().ReturnToHub();
                yield return WaitForScene(MechanicsSceneCatalog.HubScenePath);
                Assert.That(Object.FindObjectsByType<RunController>(FindObjectsSortMode.None), Is.Empty, entry.Id + " retained its run");
            }
        }

        static IEnumerator OpenStand(string name)
        {
            string path = SceneRoot + name;
            EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            yield return WaitForScene(path);
            var stand = CurrentStand();
            Assert.That(stand.Prepared, Is.True, stand.PreparationError);
            Pause(stand);
        }

        static MechanicsTestScene CurrentStand()
        {
            var stand = Object.FindFirstObjectByType<MechanicsTestScene>();
            Assert.That(stand, Is.Not.Null);
            return stand;
        }

        static void Pause(MechanicsTestScene stand) { if (!stand.Paused) stand.TogglePause(); }

        static IEnumerator AssertRenderedFrames(NotebookWorldView view, string context)
        {
            Assert.That(view, Is.Not.Null, context);
            Assert.That(view.isActiveAndEnabled, Is.True, context);
            int builds = view.MeshBuildCount;
            for (int frame = 0; frame < RenderFrames; frame++) yield return null;
            Assert.That(view.MeshBuildCount, Is.GreaterThanOrEqualTo(builds + RenderFrames - 1),
                context + " must complete real LateUpdate calls; loading and unloading immediately skips the rendering regression.");
            Assert.That(view.StaticGeometryRefreshCount, Is.GreaterThan(0), context);
            var meshes = view.GetComponentsInChildren<MeshFilter>().Select(filter => filter.sharedMesh).ToArray();
            Assert.That(meshes.Length, Is.EqualTo(5), context + " expected independently batched rendering layers");
            Assert.That(meshes.All(mesh => mesh != null), Is.True, context);
            Assert.That(meshes.Any(mesh => mesh.vertexCount > 0), Is.True, context);
            LogAssert.NoUnexpectedReceived();
        }

        static IEnumerator WaitForScene(string path)
        {
            bool ready = false;
            for (int frame = 0; frame < FrameLimit; frame++)
            {
                var navigator = Object.FindFirstObjectByType<MechanicsSceneNavigator>();
                if (SceneManager.GetActiveScene().path == path && navigator != null && !navigator.IsLoading)
                { ready = true; break; }
                yield return null;
            }
            Assert.That(ready, Is.True, "Scene did not become ready: " + path);
            yield return null;
        }

        static void Click(Component owner, string name)
        {
            Assert.That(owner, Is.Not.Null, name);
            var button = owner.GetComponentsInChildren<Button>(true).Single(value => value.name == name);
            Assert.That(button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True, name);
            button.onClick.Invoke();
        }

        static string[] ProfileMetadata()
        {
            string path = WorkshopPersistence.ProfilePath;
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory)) return Array.Empty<string>();
            return Directory.GetFiles(directory, Path.GetFileName(path) + "*")
                .Select(value => new FileInfo(value)).OrderBy(value => value.FullName, StringComparer.Ordinal)
                .Select(value => value.Name + ":" + value.Length + ":" + value.LastWriteTimeUtc.Ticks).ToArray();
        }
    }
}
#endif
