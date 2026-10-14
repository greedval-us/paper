using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PaperDefense.Editor
{
    public static class PaperDefenseSetup
    {
        public const string ScenePath = "Assets/PaperDefense/Scenes/PaperDefense.unity";

        [MenuItem("Paper Defense/Открыть игру")]
        public static void CreateOrOpen()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogWarning("Остановите Play Mode перед открытием игры."); return; }
            if (!File.Exists(ScenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            { Debug.LogError("Не найдена основная сцена игры: " + ScenePath + ". Восстановите сохранённую сцену из проекта."); return; }
            if (SceneManager.GetActiveScene().path == ScenePath) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Paper Defense/Проверить орду 3000")]
        public static void Stress3000() => PressureValidation.Run();
    }
}
