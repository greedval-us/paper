using System;
using System.Collections;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PaperDefense.Presentation
{
    /// <summary>Scene-owned navigation: a stand replaces its predecessor and owns no persistent runtime.</summary>
    public sealed class MechanicsSceneNavigator : MonoBehaviour
    {
        public bool IsLoading { get; private set; }
        public string LastError { get; private set; } = "";
        public event Action StatusChanged;
        UserTextResolver text;
        public MechanicsSceneCatalog Catalog => MechanicsSceneCatalog.Default;
        void Awake() => text = TextResourcesAsset.Default;
        public static MechanicsSceneNavigator Attach(GameObject owner)
        {
            var existing = owner.GetComponent<MechanicsSceneNavigator>();
            return existing != null ? existing : owner.AddComponent<MechanicsSceneNavigator>();
        }
        public void ReturnToHub() => OpenScene(MechanicsSceneCatalog.HubScenePath);
        public bool OpenScene(string path)
        {
            if (IsLoading) return false;
            if (Catalog == null || !Catalog.ContainsPath(path))
            { LastError = text.Resolve(UserText.Message("hub.unavailable", path ?? "")); StatusChanged?.Invoke(); return false; }
#if !UNITY_EDITOR
            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(path))
            { LastError = text.Resolve(UserText.Message("hub.unavailable", path)); StatusChanged?.Invoke(); return false; }
#endif
            IsLoading = true; LastError = ""; StatusChanged?.Invoke();
            StartCoroutine(Load(path)); return true;
        }
        IEnumerator Load(string path)
        {
            // Let the click finish before its owning scene is replaced, and reject a second click immediately.
            yield return null;
            var operation = BeginLoad(path);
            if (operation != null) yield return operation;
            IsLoading = false; StatusChanged?.Invoke();
        }
        AsyncOperation BeginLoad(string path)
        {
            try
            {
#if UNITY_EDITOR
                return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                    new LoadSceneParameters(LoadSceneMode.Single));
#else
                return SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
#endif
            }
            catch (Exception exception)
            { LastError = text.Resolve(UserText.Message("hub.failed", exception.Message)); return null; }
        }
    }
}
