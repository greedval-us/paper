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
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class MechanicsHubNavigationTests
    {
        const string TestSceneRoot = "Assets/PaperDefense/Scenes/Tests/";
        const int FrameLimit = 240;
        readonly List<string> fixtureDirectories = new List<string>();
        MechanicsSceneHub hub;
        string[] profileFiles;
        string persistenceError;

        [UnitySetUp] public IEnumerator OpenSavedHub()
        {
            // Observe metadata only; the test never opens or writes the user's profile.
            profileFiles = ProfileMetadata();
            persistenceError = WorkshopPersistence.LastError;
            fixtureDirectories.Clear();
            EditorSceneManager.LoadSceneInPlayMode(MechanicsSceneCatalog.HubScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return WaitForScene(MechanicsSceneCatalog.HubScenePath);
            hub = Object.FindFirstObjectByType<MechanicsSceneHub>();
            AssertHubIsolation();
        }

        [UnityTearDown] public IEnumerator CloseSceneWithoutProfileChanges()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path == MechanicsSceneCatalog.HubScenePath || scene.path.StartsWith(TestSceneRoot, StringComparison.Ordinal))
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("Mechanics hub navigation verified"));
                var unload = SceneManager.UnloadSceneAsync(scene);
                for (int frame = 0; unload != null && !unload.isDone && frame < FrameLimit; frame++) yield return null;
                Assert.That(unload == null || unload.isDone, Is.True, "Navigation scene cleanup timed out.");
                yield return null;
            }
            foreach (string directory in fixtureDirectories)
                Assert.That(Directory.Exists(directory), Is.False, "Leaving the profile stand must dispose its temporary fixture: " + directory);
            Assert.That(ProfileMetadata(), Is.EqualTo(profileFiles), "Hub browsing must leave persistent profile files unchanged.");
            Assert.That(WorkshopPersistence.LastError, Is.EqualTo(persistenceError), "Navigation must not invoke the user's profile store.");
        }

        [UnityTest] public IEnumerator SavedHubListsEveryRouteWithResolvedReadableCardsAndWorkingGroupButtons()
        {
            Assert.That(hub.catalog, Is.Not.Null, "The saved hub must bind its resource catalog.");
            Assert.That(hub.catalog, Is.SameAs(MechanicsSceneCatalog.Default));
            Assert.That(hub.textResources, Is.Not.Null, "The saved hub must bind its text resource.");
            var entries = hub.catalog.Scenes;
            string[] savedPaths = AssetDatabase.FindAssets("t:Scene", new[] { TestSceneRoot.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Assert.That(entries.Count, Is.EqualTo(27));
            Assert.That(entries.Select(entry => entry.ScenePath), Is.EquivalentTo(savedPaths),
                "Every independently saved mechanics and art stand must be reachable from the hub.");
            Assert.That(entries.Select(entry => entry.Id).Distinct().Count(), Is.EqualTo(entries.Count));
            Assert.That(entries.All(entry => !string.IsNullOrWhiteSpace(entry.GroupKey)), Is.True);
            AssertCards(entries);

            var resolver = hub.textResources.CreateResolver();
            string[] labels = VisibleLabels(hub).Select(label => label.text).ToArray();
            foreach (var entry in entries)
            {
                Assert.That(labels, Does.Contain(resolver.Resolve(UserText.Message(entry.TitleKey))), entry.Id + " title");
                Assert.That(labels, Does.Contain(resolver.Resolve(UserText.Message(entry.DescriptionKey))), entry.Id + " description");
            }
            AssertReadableCardButtons();

            foreach (string group in entries.Select(entry => entry.GroupKey).Distinct())
            {
                Click(hub, "Hub group " + group);
                yield return null; // Let the layout and active card state settle.
                Assert.That(hub.SelectedGroupKey, Is.EqualTo(group));
                AssertCards(entries.Where(entry => entry.GroupKey == group));
                AssertHubIsolation();
            }
            Click(hub, "Hub group all");
            yield return null;
            AssertCards(entries);
            AssertReadableCardButtons();
        }

        [UnityTest] public IEnumerator EveryHubCardOpensItsSavedStandAndReturnsWithOneSceneAndNoRetainedRuntime()
        {
            var entries = hub.catalog.Scenes.ToArray();
            Assert.That(entries.Length, Is.EqualTo(27));
            foreach (var entry in entries)
            {
                var outgoingRoots = SceneManager.GetActiveScene().GetRootGameObjects();
                Click(hub, "Open test " + entry.Id);
                yield return WaitForScene(entry.ScenePath);
                AssertDestroyed(outgoingRoots, entry.Id);
                AssertNavigationIsolation(entry.ScenePath);

                string fixtureDirectory = null;
                var mechanics = Object.FindObjectsByType<MechanicsTestScene>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var art = Object.FindObjectsByType<NotebookArtStand>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (mechanics.Length == 1)
                {
                    Assert.That(art, Is.Empty, entry.Id);
                    var stand = mechanics[0];
                    Assert.That(stand.Prepared, Is.True, entry.Id + " " + stand.PreparationError);
                    Assert.That(Object.FindObjectsByType<RunController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                        Is.EqualTo(1), entry.Id);
                    Assert.That(stand.SimulationDriverCount, Is.EqualTo(1), entry.Id);
                    Assert.That(stand.Controller.AutomaticSimulation, Is.False, entry.Id);
                    if (!stand.Paused) stand.TogglePause();
                    if (stand.scenario.focus == MechanicsTestFocus.Profile)
                    {
                        fixtureDirectory = Path.GetFullPath(stand.FixtureDirectory);
                        fixtureDirectories.Add(fixtureDirectory);
                        string ownedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PaperDefenseMechanics")) + Path.DirectorySeparatorChar;
                        Assert.That(fixtureDirectory.StartsWith(ownedRoot, StringComparison.OrdinalIgnoreCase), Is.True);
                        Assert.That(stand.ProfileLoadCalls, Is.GreaterThan(0));
                        Assert.That(stand.SaveProfileVariant(), Is.True, "The profile stand must save only through its isolated fixture.");
                        Assert.That(stand.ProfileSaveCalls, Is.GreaterThan(0));
                    }
                    else
                    {
                        Assert.That(stand.Controller.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly), entry.Id);
                        Assert.That(stand.ProfileLoadCalls + stand.ProfileSaveCalls, Is.Zero, entry.Id);
                    }
                }
                else
                {
                    Assert.That(mechanics, Is.Empty, entry.Id);
                    int playerArt = Object.FindObjectsByType<PlayerAssetStand>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                    Assert.That(art.Length + playerArt, Is.EqualTo(1), entry.Id);
                    Assert.That(Object.FindObjectsByType<RunController>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty, entry.Id);
                    Assert.That(Object.FindObjectsByType<PaperDefenseBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty, entry.Id);
                }

                var standRoots = SceneManager.GetActiveScene().GetRootGameObjects();
                if (art.Length == 1) Click(art[0], "Return to mechanics hub");
                else Object.FindFirstObjectByType<MechanicsSceneNavigator>().ReturnToHub(); // The mechanics IMGUI button calls this same action.
                yield return WaitForScene(MechanicsSceneCatalog.HubScenePath);
                AssertDestroyed(standRoots, entry.Id + " return");
                if (fixtureDirectory != null) Assert.That(Directory.Exists(fixtureDirectory), Is.False, entry.Id);
                hub = Object.FindFirstObjectByType<MechanicsSceneHub>();
                AssertHubIsolation();
                AssertCards(entries);
                Assert.That(ProfileMetadata(), Is.EqualTo(profileFiles), entry.Id + " must not change the user's profile.");
            }
        }

        [UnityTest] public IEnumerator UnknownRouteStaysInHubAndRapidSecondOpenCannotStartAnotherLoad()
        {
            var navigator = Object.FindFirstObjectByType<MechanicsSceneNavigator>();
            const string missing = TestSceneRoot + "NotCatalogued.unity";
            var originalHandle = SceneManager.GetActiveScene().handle;
            Assert.That(navigator.OpenScene(missing), Is.False);
            Assert.That(navigator.IsLoading, Is.False);
            string expected = hub.textResources.CreateResolver().Resolve(UserText.Message("hub.unavailable", missing));
            Assert.That(navigator.LastError, Is.EqualTo(expected));
            AssertResolved(navigator.LastError, "Unavailable route message");
            yield return null;
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(originalHandle));
            AssertHubIsolation();

            var entry = hub.catalog.Scenes.First(value => value.ScenePath.EndsWith("/03_Weapons/Gun.unity", StringComparison.Ordinal));
            Assert.That(navigator.OpenScene(entry.ScenePath), Is.True);
            Assert.That(navigator.IsLoading, Is.True, "The first click must lock navigation before the frame ends.");
            Assert.That(navigator.OpenScene(entry.ScenePath), Is.False, "A second click must not create another load operation.");
            yield return WaitForScene(entry.ScenePath);
            AssertNavigationIsolation(entry.ScenePath);
            Assert.That(Object.FindObjectsByType<RunController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Object.FindFirstObjectByType<MechanicsSceneNavigator>().ReturnToHub();
            yield return WaitForScene(MechanicsSceneCatalog.HubScenePath);
            hub = Object.FindFirstObjectByType<MechanicsSceneHub>();
            AssertHubIsolation();
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
            Assert.That(ready, Is.True, "Scene navigation did not finish within " + FrameLimit + " frames: " + path);
            yield return null; // Allow the old scene's queued runtime destruction to finish.
        }

        void AssertHubIsolation()
        {
            AssertNavigationIsolation(MechanicsSceneCatalog.HubScenePath);
            Assert.That(Object.FindObjectsByType<MechanicsSceneHub>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<MechanicsTestScene>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<NotebookArtStand>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<RunController>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<PaperDefenseBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
        }

        static void AssertNavigationIsolation(string path)
        {
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(path));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1), path + " must replace the previous scene.");
            Assert.That(Object.FindObjectsByType<MechanicsSceneNavigator>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1), path);
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1), path);
            Assert.That(Object.FindObjectsByType<MechanicsSceneHub>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                Is.EqualTo(path == MechanicsSceneCatalog.HubScenePath ? 1 : 0), path);
        }

        void AssertCards(IEnumerable<MechanicsSceneEntry> expected)
        {
            var entries = expected.ToArray();
            Assert.That(hub.VisibleSceneCount, Is.EqualTo(entries.Length));
            var buttons = hub.GetComponentsInChildren<Button>(true)
                .Where(button => button.gameObject.activeInHierarchy && button.name.StartsWith("Open test ", StringComparison.Ordinal)).ToArray();
            Assert.That(buttons.Select(button => button.name), Is.EquivalentTo(entries.Select(entry => "Open test " + entry.Id)));
            foreach (var label in VisibleLabels(hub)) AssertResolved(label.text, label.transform.parent.name);
        }

        void AssertReadableCardButtons()
        {
            Canvas.ForceUpdateCanvases();
            var buttons = hub.GetComponentsInChildren<Button>()
                .Where(button => button.name.StartsWith("Open test ", StringComparison.Ordinal)).ToArray();
            var bounds = buttons.Select(button => WorldBounds(button.GetComponent<RectTransform>())).ToArray();
            for (int index = 0; index < buttons.Length; index++)
            {
                var rect = buttons[index].GetComponent<RectTransform>().rect;
                Assert.That(rect.width, Is.GreaterThanOrEqualTo(100), buttons[index].name + " readable width");
                Assert.That(rect.height, Is.GreaterThanOrEqualTo(32), buttons[index].name + " pointer target height");
                AssertOnScreen(bounds[index], buttons[index].name);
                var title = buttons[index].GetComponentsInChildren<Text>().Single(label => label.name == "Scene title");
                Assert.That(title.preferredHeight, Is.LessThanOrEqualTo(title.rectTransform.rect.height + .5f),
                    buttons[index].name + " must show its complete title.");
                for (int other = index + 1; other < buttons.Length; other++)
                    Assert.That(bounds[index].Overlaps(bounds[other]), Is.False, buttons[index].name + " overlaps " + buttons[other].name);
            }
            var status = hub.GetComponentsInChildren<Text>().Single(label => label.name == "Hub status");
            AssertOnScreen(WorldBounds(status.rectTransform), status.name);
        }

        static void AssertOnScreen(Rect bounds, string context)
        {
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(-.5f), context);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(-.5f), context);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(Screen.width + .5f), context);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(Screen.height + .5f), context);
        }

        static Rect WorldBounds(RectTransform transform)
        {
            var corners = new Vector3[4];
            transform.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners.Min(corner => corner.x), corners.Min(corner => corner.y),
                corners.Max(corner => corner.x), corners.Max(corner => corner.y));
        }

        static IEnumerable<Text> VisibleLabels(Component owner) => owner.GetComponentsInChildren<Text>(true)
            .Where(label => label.gameObject.activeInHierarchy);

        static void Click(Component owner, string name)
        {
            var matches = owner.GetComponentsInChildren<Button>(true).Where(button => button.name == name).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), name);
            var button = matches[0];
            Assert.That(button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True, name);
            Assert.That(button.targetGraphic, Is.Not.Null, name);
            Assert.That(button.targetGraphic.raycastTarget, Is.True, name + " must accept pointer input.");
            button.onClick.Invoke();
        }

        static void AssertResolved(string text, string context)
        {
            Assert.That(string.IsNullOrWhiteSpace(text), Is.False, context);
            Assert.That(text, Does.Not.Contain("\u001ePD1:"), context);
            Assert.That(text, Does.Not.Contain("[missing:"), context);
            Assert.That(text, Does.Not.Contain("[invalid:"), context);
            Assert.That(text.StartsWith("hub.", StringComparison.Ordinal), Is.False, context);
        }

        static void AssertDestroyed(IEnumerable<GameObject> roots, string context)
        {
            foreach (var root in roots) Assert.That(root == null, Is.True, context + " retained an object from the previous scene.");
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
