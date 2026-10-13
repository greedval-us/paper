using System;
using System.Collections.Generic;
using UnityEngine;

namespace PaperDefense.Presentation
{
    [Serializable] public sealed class MechanicsSceneEntry
    {
        public string Id, ScenePath, TitleKey, DescriptionKey, GroupKey;
    }

    /// <summary>Resource-owned routes and text keys for isolated development stands.</summary>
    [CreateAssetMenu(menuName = "Paper Defense/Validation/Scene Catalog")]
    public sealed class MechanicsSceneCatalog : ScriptableObject
    {
        public const string ResourcePath = "Validation/MechanicsSceneCatalog";
        public const string HubScenePath = "Assets/PaperDefense/Scenes/MechanicsHub.unity";
        public List<MechanicsSceneEntry> Scenes = new List<MechanicsSceneEntry>();
        public static MechanicsSceneCatalog Default => Resources.Load<MechanicsSceneCatalog>(ResourcePath);
        public MechanicsSceneEntry Find(string id) => Scenes.Find(entry => entry.Id == id);
        public bool ContainsPath(string path) => path == HubScenePath || Scenes.Exists(entry => entry.ScenePath == path);
    }
}
