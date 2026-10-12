using System;
using System.IO;
using PaperDefense.Application;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PaperDefense.Editor
{
    public static class ContentAcceptanceBuild
    {
        public const string ManifestPath="Assets/PaperDefense/Content/Acceptance/ContentAcceptanceManifest.asset";
        public const string ScenePath="Assets/PaperDefense/Content/Acceptance/ContentAcceptance.unity";
        // Only the new validation scene is saved. Existing open scenes and their unsaved edits stay intact.
        [MenuItem("Paper Defense/Content/Prepare development acceptance scene")]
        public static void PrepareScene()
        {
            var manifest=AssetDatabase.LoadAssetAtPath<ContentAcceptanceManifestAsset>(ManifestPath);
            if(manifest==null)throw new InvalidOperationException("Создайте отдельный проверочный manifest: "+ManifestPath);
            manifest.preset.CreateBalance();
            if(File.Exists(ScenePath))throw new InvalidOperationException("Проверочная сцена уже существует; не перезаписываю её автоматически.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                var camera=new GameObject("Validation Camera",typeof(Camera)); SceneManager.MoveGameObjectToScene(camera,scene);
                var light=new GameObject("Validation Light",typeof(Light)); light.GetComponent<Light>().type=LightType.Directional; SceneManager.MoveGameObjectToScene(light,scene);
                var entry=new GameObject("Content Acceptance",typeof(ContentAcceptanceProbe)); SceneManager.MoveGameObjectToScene(entry,scene);
                entry.GetComponent<ContentAcceptanceProbe>().manifest=manifest;
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new InvalidOperationException("Не удалось сохранить проверочную сцену.");
            }
            finally { EditorSceneManager.CloseScene(scene,true); }
        }
    }
}
