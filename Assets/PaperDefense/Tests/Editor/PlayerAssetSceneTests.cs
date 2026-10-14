using NUnit.Framework;
using PaperDefense.Presentation;
using UnityEditor;

namespace PaperDefense.Tests
{
    public sealed class PlayerAssetSceneTests
    {
        [Test] public void PlayerAssetsHaveAnIndependentSceneAndHubRoute()
        {
            const string path = "Assets/PaperDefense/Scenes/Tests/27_PlayerAssets.unity";
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null);
            var catalog = AssetDatabase.LoadAssetAtPath<MechanicsSceneCatalog>(PaperDefense.Editor.MechanicsHubTools.CatalogAssetPath);
            Assert.That(catalog.Scenes.Exists(entry => entry.Id == "player_assets" && entry.ScenePath == path), Is.True);
            foreach (var scene in EditorBuildSettings.scenes)
                Assert.That(scene.path, Is.Not.EqualTo(path), "Art experiments must stay outside the player build.");
        }
    }
}
