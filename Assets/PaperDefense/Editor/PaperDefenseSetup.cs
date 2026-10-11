using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PaperDefense.Editor
{
    public static class PaperDefenseSetup
    {
        public const string ScenePath = "Assets/PaperDefense/Scenes/PaperDefense.unity";
        public const string BalancePath = "Assets/PaperDefense/Configuration/NotebookBalance.asset";
        const string PrefabPath = "Assets/PaperDefense/Prefabs/NotebookGame.prefab";

        [MenuItem("Paper Defense/Создать или открыть прототип")]
        public static void CreateOrOpen()
        {
            if (EditorApplication.isPlaying) { UnityEngine.Debug.LogWarning("Остановите Play Mode перед сборкой сцены."); return; }
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            Directory.CreateDirectory("Assets/PaperDefense/Scenes");
            Directory.CreateDirectory("Assets/PaperDefense/Configuration");
            Directory.CreateDirectory("Assets/PaperDefense/Prefabs");
            AssetDatabase.Refresh();
            var balance=AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(BalancePath);
            if(balance==null)
            {balance=ScriptableObject.CreateInstance<GameBalanceAsset>();balance.ResetToDefaults();AssetDatabase.CreateAsset(balance,BalancePath);}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Paper Defense — Composition Root");
            var bootstrap=root.AddComponent<PaperDefenseBootstrap>();bootstrap.balance=balance;
            var cameraGo=new GameObject("Notebook Camera",typeof(Camera),typeof(AudioListener));cameraGo.transform.SetParent(root.transform,false);
            cameraGo.tag="MainCamera";var camera=cameraGo.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=22;
            camera.transform.position=new Vector3(balance.width*.5f,balance.height*.5f,-10);camera.backgroundColor=new Color(.975f,.961f,.9f);
            camera.clearFlags=CameraClearFlags.SolidColor;bootstrap.worldCamera=camera;
            var lightGo=new GameObject("Main Light",typeof(Light));lightGo.transform.SetParent(root.transform,false);
            var light=lightGo.GetComponent<Light>();light.type=LightType.Directional;light.intensity=.7f;lightGo.transform.rotation=Quaternion.Euler(50,-30,0);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root,PrefabPath,InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene,ScenePath);
            var scenes=new List<EditorBuildSettingsScene>{new EditorBuildSettingsScene(ScenePath,true)};
            foreach(var other in EditorBuildSettings.scenes) if(other.path!=ScenePath) scenes.Add(other);
            EditorBuildSettings.scenes=scenes.ToArray();
            AssetDatabase.SaveAssets();Selection.activeGameObject=root;
            SceneView.lastActiveSceneView?.LookAt(new Vector3(balance.width*.5f,balance.height*.5f,0),Quaternion.identity,24,true);
            UnityEngine.Debug.Log("Paper Defense: сцена готова. Нажмите Play. Баланс находится в " + BalancePath);
        }

        [MenuItem("Paper Defense/Проверить орду 3000")]
        public static void Stress3000() => PressureValidation.Run();
    }
}
