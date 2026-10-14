using System.IO;
using PaperDefense.Application;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PaperDefense.Editor
{
    public static class PlayerGameSceneTools
    {
        public const string ScenePath = "Assets/PaperDefense/Scenes/Game/PaperDefenseGame.unity";
        public const string BalancePath = "Assets/PaperDefense/Configuration/Game/PlayerBalance.asset";

        // Existing snapshots are never overwritten by scene creation or builds.
        public static void CreateConfigurationIfMissing()
        {
            const string folder = "Assets/PaperDefense/Configuration/Game";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            if (!File.Exists(folder + "/PlayerContent.asset")) AssetDatabase.CopyAsset("Assets/PaperDefense/Configuration/Content/NotebookContent.asset", folder + "/PlayerContent.asset");
            if (!File.Exists(folder + "/PlayerRules.asset")) AssetDatabase.CopyAsset("Assets/PaperDefense/Configuration/Content/Target.asset", folder + "/PlayerRules.asset");
            if (!File.Exists(BalancePath)) AssetDatabase.CopyAsset("Assets/PaperDefense/Configuration/TargetNotebookBalance.asset", BalancePath);
            var catalog = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(folder + "/PlayerContent.asset");
            var rules = AssetDatabase.LoadAssetAtPath<BalancePresetAsset>(folder + "/PlayerRules.asset");
            var balance = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(BalancePath);
            catalog.presets = new[] { rules }; rules.database = catalog; balance.dataPreset = rules;
            EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(rules); EditorUtility.SetDirty(balance);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Paper Defense/Game/Open player game")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void CreateIfMissing()
        {
            CreateConfigurationIfMissing();
            if (File.Exists(ScenePath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var root = new GameObject("Paper Defense — Player Game");
            SceneManager.MoveGameObjectToScene(root, scene);
            var game = root.AddComponent<PlayerGameRoot>();
            var serialized = new SerializedObject(game);
            serialized.FindProperty("balance").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(BalancePath);
            serialized.FindProperty("textResources").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextResourcesAsset>("Assets/PaperDefense/Resources/Text/ru.asset");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var camera = new GameObject("Main Camera", typeof(Camera)); camera.tag = "MainCamera"; camera.transform.SetParent(root.transform);
            camera.GetComponent<Camera>().orthographic = true; camera.transform.position = new Vector3(48, 32, -10);
            var light = new GameObject("Scene light", typeof(Light)); light.transform.SetParent(root.transform); light.GetComponent<Light>().type = LightType.Directional;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Paper Defense/Game/Build player game")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/PlayerGame");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = "Builds/PlayerGame/PaperDefense.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            File.WriteAllText("Builds/PlayerGame/build-report.txt",
                "Result: " + report.summary.result + "\nScene: " + ScenePath +
                "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings +
                "\nSize: " + report.summary.totalSize + "\nDuration: " + report.summary.totalTime +
                "\nFinished UTC: " + System.DateTime.UtcNow.ToString("O") + "\n");
            if (report.summary.result != BuildResult.Succeeded) throw new System.InvalidOperationException("Player game build failed: " + report.summary.result);
            Debug.Log("Player game built: " + report.summary.outputPath);
        }
    }
}
