using System;
using System.Collections.Generic;
using System.IO;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using PaperDefense.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PaperDefense.Editor
{
    /// <summary>Builds only the hub's owned assets; authored test scenes remain untouched.</summary>
    public static class MechanicsHubTools
    {
        public const string CatalogAssetPath = "Assets/PaperDefense/Resources/Validation/MechanicsSceneCatalog.asset";
        const string TextAssetPath = "Assets/PaperDefense/Resources/Text/ru.asset";
        static readonly string[] ArtIds = { "tower_art", "enemy_animation", "ink_effects" };
        static readonly string[] ArtTextIds = { "towers", "enemies", "effects" };
        static readonly HashSet<string> GroupKeys = new HashSet<string>(StringComparer.Ordinal) {
            "hub.group.workshop", "hub.group.weapons", "hub.group.modules",
            "hub.group.enemies", "hub.group.systems", "hub.group.art"
        };

        [MenuItem("Paper Defense/Validation/Generate mechanics hub")]
        public static void GenerateMissing()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextResourcesAsset>(TextAssetPath);
            if (text == null) throw new InvalidOperationException("hub.generator: text resource missing.");

            // Resolve and validate detached data before creating folders, changing the catalog or opening scenes.
            List<MechanicsSceneEntry> entries = BuildEntries();
            ValidateEntries(entries, text.CreateResolver());
            var catalog = AssetDatabase.LoadAssetAtPath<MechanicsSceneCatalog>(CatalogAssetPath);
            if (catalog == null && File.Exists(CatalogAssetPath))
                throw new InvalidOperationException("hub.generator: catalog path contains another asset.");
            bool hubExists = File.Exists(MechanicsSceneCatalog.HubScenePath);
            if (hubExists && AssetDatabase.LoadAssetAtPath<SceneAsset>(MechanicsSceneCatalog.HubScenePath) == null)
                throw new InvalidOperationException("hub.generator: hub path is not a valid scene asset.");

            if (catalog == null)
            {
                EnsureFolder(Path.GetDirectoryName(CatalogAssetPath).Replace('\\', '/'));
                catalog = ScriptableObject.CreateInstance<MechanicsSceneCatalog>();
                catalog.Scenes = entries;
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }
            else if (!SameEntries(catalog.Scenes, entries))
            {
                catalog.Scenes = entries;
                EditorUtility.SetDirty(catalog);
            }
            AssetDatabase.SaveAssetIfDirty(catalog);
            if (!hubExists) CreateHubScene(catalog, text);
        }

        [MenuItem("Paper Defense/Validation/Open mechanics hub")]
        public static void OpenHub()
        {
            if (EditorApplication.isPlaying)
            {
                var navigator = UnityEngine.Object.FindFirstObjectByType<MechanicsSceneNavigator>();
                if (navigator != null) navigator.ReturnToHub();
                else EditorSceneManager.LoadSceneAsyncInPlayMode(MechanicsSceneCatalog.HubScenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(MechanicsSceneCatalog.HubScenePath)) GenerateMissing();
            EditorSceneManager.OpenScene(MechanicsSceneCatalog.HubScenePath, OpenSceneMode.Single);
        }

        public static void ValidateCatalog(MechanicsSceneCatalog catalog, TextResourcesAsset text)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (text == null) throw new ArgumentNullException(nameof(text));
            ValidateEntries(catalog.Scenes, text.CreateResolver());
        }

        static List<MechanicsSceneEntry> BuildEntries()
        {
            if (MechanicsTestSceneTools.SceneNames.Length != MechanicsTestSceneTools.ScenarioIds.Length ||
                NotebookArtSceneTools.SceneNames.Length != ArtIds.Length)
                throw new InvalidOperationException("hub.generator: scene inventory does not match route IDs.");
            var entries = new List<MechanicsSceneEntry>(MechanicsTestSceneTools.SceneNames.Length + ArtIds.Length);
            for (int i = 0; i < MechanicsTestSceneTools.ScenarioIds.Length; i++)
            {
                string id = MechanicsTestSceneTools.ScenarioIds[i];
                string scenarioPath = MechanicsTestSceneTools.DataRoot + "/" + id + "/Scenario.asset";
                var scenario = AssetDatabase.LoadAssetAtPath<MechanicsTestScenarioAsset>(scenarioPath);
                if (scenario == null || scenario.scenarioId != id)
                    throw new InvalidOperationException("hub.generator: missing or mismatched scenario " + scenarioPath);
                entries.Add(new MechanicsSceneEntry {
                    Id = id, ScenePath = TestScenePath(MechanicsTestSceneTools.SceneNames[i]),
                    TitleKey = scenario.titleKey, DescriptionKey = scenario.instructionsKey, GroupKey = MechanicsGroup(id)
                });
            }
            for (int i = 0; i < ArtIds.Length; i++)
                entries.Add(new MechanicsSceneEntry {
                    Id = ArtIds[i], ScenePath = TestScenePath(NotebookArtSceneTools.SceneNames[i]),
                    TitleKey = "art.title." + ArtTextIds[i], DescriptionKey = "art.hint." + ArtTextIds[i], GroupKey = "hub.group.art"
                });
            return entries;
        }

        static void ValidateEntries(List<MechanicsSceneEntry> entries, UserTextResolver resolver)
        {
            if (entries == null || entries.Count != MechanicsTestSceneTools.SceneNames.Length + ArtIds.Length)
                throw new InvalidOperationException("hub.catalog: complete scene inventory required.");
            if (MechanicsTestSceneTools.SceneNames.Length != MechanicsTestSceneTools.ScenarioIds.Length ||
                NotebookArtSceneTools.SceneNames.Length != ArtIds.Length)
                throw new InvalidOperationException("hub.catalog: scene inventory does not match route IDs.");
            var expectedPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < MechanicsTestSceneTools.ScenarioIds.Length; i++)
                expectedPaths.Add(MechanicsTestSceneTools.ScenarioIds[i], TestScenePath(MechanicsTestSceneTools.SceneNames[i]));
            for (int i = 0; i < ArtIds.Length; i++)
                expectedPaths.Add(ArtIds[i], TestScenePath(NotebookArtSceneTools.SceneNames[i]));

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var textKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (MechanicsSceneEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id) ||
                    !expectedPaths.TryGetValue(entry.Id, out string expectedPath))
                    throw new InvalidOperationException("hub.catalog: null, duplicate or unknown route ID.");
                if (entry.ScenePath != expectedPath || !paths.Add(entry.ScenePath) ||
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.ScenePath) == null)
                    throw new InvalidOperationException("hub.catalog: missing, duplicate or mismatched scene path for " + entry.Id);
                if (string.IsNullOrWhiteSpace(entry.TitleKey) || string.IsNullOrWhiteSpace(entry.DescriptionKey) ||
                    !textKeys.Add(entry.TitleKey) || !textKeys.Add(entry.DescriptionKey))
                    throw new InvalidOperationException("hub.catalog: missing or duplicate route text keys for " + entry.Id);
                if (entry.GroupKey == null || !GroupKeys.Contains(entry.GroupKey) || entry.GroupKey != ExpectedGroup(entry.Id))
                    throw new InvalidOperationException("hub.catalog: unknown or mismatched route group for " + entry.Id);
                resolver.RequireContract(entry.TitleKey, 0);
                resolver.RequireContract(entry.DescriptionKey, 0);
                resolver.RequireContract(entry.GroupKey, 0);
            }
            foreach (string key in new[] { "hub.title", "hub.hint", "hub.all", "hub.back", "hub.loading" })
                resolver.RequireContract(key, 0);
            resolver.RequireContract("hub.unavailable", 1);
            resolver.RequireContract("hub.failed", 1);
            resolver.RequireContract("hub.count", 2);
        }

        static string TestScenePath(string sceneName) => MechanicsTestSceneTools.SceneRoot + "/" + sceneName + ".unity";

        static string ExpectedGroup(string id)
        {
            for (int i = 0; i < ArtIds.Length; i++) if (id == ArtIds[i]) return "hub.group.art";
            return MechanicsGroup(id);
        }

        static string MechanicsGroup(string id)
        {
            switch (id)
            {
                case "assembly": case "drawing": case "profile": return "hub.group.workshop";
                case "gun": case "piercer": case "mortar": case "fan": case "ricochet": case "stapler": case "blotter":
                    return "hub.group.weapons";
                case "armor": case "feed": case "tracking": case "rail": return "hub.group.modules";
                case "enemy_tiers": case "ranged": case "blots": case "wells": case "color": return "hub.group.enemies";
                case "construction": case "economy": case "integration": case "pressure": return "hub.group.systems";
                default: throw new InvalidOperationException("hub.catalog: unknown mechanics route " + id);
            }
        }

        static bool SameEntries(List<MechanicsSceneEntry> current, List<MechanicsSceneEntry> candidate)
        {
            if (current == null || current.Count != candidate.Count) return false;
            for (int i = 0; i < current.Count; i++)
            {
                MechanicsSceneEntry left = current[i], right = candidate[i];
                if (left == null || left.Id != right.Id || left.ScenePath != right.ScenePath || left.TitleKey != right.TitleKey ||
                    left.DescriptionKey != right.DescriptionKey || left.GroupKey != right.GroupKey) return false;
            }
            return true;
        }

        static void CreateHubScene(MechanicsSceneCatalog catalog, TextResourcesAsset text)
        {
            EnsureFolder(Path.GetDirectoryName(MechanicsSceneCatalog.HubScenePath).Replace('\\', '/'));
            Scene active = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var root = new GameObject("Mechanics Scene Hub", typeof(MechanicsSceneHub));
                SceneManager.MoveGameObjectToScene(root, scene);
                var hub = root.GetComponent<MechanicsSceneHub>();
                hub.catalog = catalog;
                hub.textResources = text;
                var cameraGo = new GameObject("Hub Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraGo, scene);
                cameraGo.tag = "MainCamera";
                var camera = cameraGo.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.975f, .961f, .9f);
                var lightGo = new GameObject("Hub Light", typeof(Light));
                SceneManager.MoveGameObjectToScene(lightGo, scene);
                lightGo.GetComponent<Light>().type = LightType.Directional;
                if (!EditorSceneManager.SaveScene(scene, MechanicsSceneCatalog.HubScenePath))
                    throw new IOException("hub.generator: scene save failed.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new IOException("hub.generator: could not create folder " + path);
        }
    }
}
