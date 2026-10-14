using NUnit.Framework;
using UnityEditor;

namespace PaperDefense.Tests
{
    public sealed class PlayerSceneTests
    {
        const string PlayerScene = "Assets/PaperDefense/Scenes/Game/PaperDefenseGame.unity";

        [Test] public void PlayerEntryExistsSeparatelyFromDevelopmentScenes()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayerScene), Is.Not.Null,
                "The player journey needs its own entry scene.");
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/PaperDefense/Scenes/PaperDefense.unity"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/PaperDefense/Scenes/MechanicsHub.unity"), Is.Not.Null);
        }

        [Test] public void DefaultBuildStartsWithPlayerJourneyAndExcludesTestStands()
        {
            Assert.That(EditorBuildSettings.scenes.Length, Is.GreaterThan(0));
            Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(PlayerScene));
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled) Assert.That(scene.path, Does.Not.Contain("/Tests/"));
        }

        [Test] public void PlayerBalanceAndCatalogAreIndependentFromDevelopmentAssets()
        {
            var balance = AssetDatabase.LoadAssetAtPath<PaperDefense.Application.GameBalanceAsset>("Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            Assert.That(balance, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(balance.dataPreset), Does.StartWith("Assets/PaperDefense/Configuration/Game/"));
            Assert.That(AssetDatabase.GetAssetPath(balance.dataPreset.database), Does.StartWith("Assets/PaperDefense/Configuration/Game/"));
            Assert.That(balance.CreateWorkshopBalance().Waves.Count, Is.EqualTo(8));
        }
    }
}
