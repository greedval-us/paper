using NUnit.Framework;
using UnityEditor;

namespace PaperDefense.Tests
{
    public sealed class PlayerSceneTests
    {
        const string PlayerScene = "Assets/PaperDefense/Scenes/Game/PaperDefenseGame.unity";

        [Test] public void PlayerEntryIsTheOnlySavedGameScene()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayerScene), Is.Not.Null,
                "The player journey needs its own entry scene.");
            var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/PaperDefense/Scenes" });
            var scenePaths = System.Array.ConvertAll(sceneGuids, AssetDatabase.GUIDToAssetPath);
            Assert.That(scenePaths, Is.EquivalentTo(new[] { PlayerScene }),
                "The project must keep only the player game scene, without development stands or legacy entry scenes.");
        }

        [Test] public void DefaultBuildContainsOnlyTheEnabledPlayerJourney()
        {
            Assert.That(EditorBuildSettings.scenes, Has.Length.EqualTo(1));
            Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(PlayerScene));
            Assert.That(EditorBuildSettings.scenes[0].enabled, Is.True);
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
