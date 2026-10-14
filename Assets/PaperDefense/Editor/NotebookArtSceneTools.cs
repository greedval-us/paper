using System.IO;
using PaperDefense.Application;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PaperDefense.Editor
{
    public static class NotebookArtSceneTools
    {
        public static readonly string[] SceneNames = { "24_TowerArt", "25_EnemyAnimation", "26_InkEffects", "27_PlayerAssets" };
        [MenuItem("Paper Defense/Art/Generate art test scenes")]
        public static void GenerateMissing()
        {
            var active = SceneManager.GetActiveScene();
            for (int i = 0; i < SceneNames.Length; i++)
            {
                string path = "Assets/PaperDefense/Scenes/Tests/" + SceneNames[i] + ".unity";
                if (File.Exists(path)) continue;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                try
                {
                    var root = new GameObject(SceneNames[i]); SceneManager.MoveGameObjectToScene(root, scene);
                    var preset = AssetDatabase.LoadAssetAtPath<BalancePresetAsset>("Assets/PaperDefense/Configuration/Content/Target.asset");
                    var texts = AssetDatabase.LoadAssetAtPath<TextResourcesAsset>("Assets/PaperDefense/Resources/Text/ru.asset");
                    if (i < 3)
                    {
                        var stand = root.AddComponent<NotebookArtStand>(); stand.mode = (NotebookArtStandMode)i;
                        stand.preset = preset; stand.textResources = texts;
                    }
                    else
                    {
                        var stand = root.AddComponent<PlayerAssetStand>(); stand.preset = preset; stand.textResources = texts;
                    }
                    var cameraGo = new GameObject("Art Camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraGo, scene);
                    cameraGo.tag = "MainCamera"; var camera = cameraGo.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 5;
                    camera.transform.position = new Vector3(0, 0, -10); camera.backgroundColor = new Color(.975f, .961f, .9f);
                    var lightGo = new GameObject("Art Light", typeof(Light)); SceneManager.MoveGameObjectToScene(lightGo, scene);
                    lightGo.GetComponent<Light>().type = LightType.Directional;
                    EditorSceneManager.SaveScene(scene, path);
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            AssetDatabase.SaveAssets();
        }
    }
}
