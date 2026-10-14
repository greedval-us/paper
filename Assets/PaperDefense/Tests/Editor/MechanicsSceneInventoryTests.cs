using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace PaperDefense.Tests
{
    public sealed class MechanicsSceneInventoryTests
    {
        public static readonly string[] SceneNames = {
            "01_Assembly", "02_Drawing", "03_Weapons/Gun", "03_Weapons/Piercer", "03_Weapons/Mortar",
            "03_Weapons/Fan", "03_Weapons/Ricochet", "03_Weapons/Stapler", "03_Weapons/Blotter",
            "04_Modules/Armor", "04_Modules/Feed", "04_Modules/Tracking", "04_Modules/Rail",
            "05_EnemyDrawingTiers", "06_RangedEnemies", "07_CorpseBlots", "08_InkWells",
            "09_ColorCartridges", "10_ConstructionNavigation", "11_WavesEconomyUpgrades",
            "12_ProfilePersistence", "13_Integration", "14_Pressure3000"
        };

        [Test] public void EveryFocusedMechanicHasAnIndependentlySavedScene()
        {
            foreach (string name in SceneNames)
            {
                string path = "Assets/PaperDefense/Scenes/Tests/" + name + ".unity";
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, path);
                string serialized = File.ReadAllText(path);
                Assert.That(serialized, Does.Contain("scenario:"), path + " must bind scenario data.");
            }
            foreach (string name in PaperDefense.Editor.NotebookArtSceneTools.SceneNames)
            {
                string path = "Assets/PaperDefense/Scenes/Tests/" + name + ".unity";
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, path);
                string serialized = File.ReadAllText(path);
                Assert.That(serialized, Does.Contain("preset:"), path + " must bind current tower content.");
                Assert.That(serialized, Does.Contain("textResources:"), path + " must bind resource text.");
                Assert.That(serialized, Does.Not.Contain("scenario:"), path + " is an independent art stand.");
            }
            Assert.That(AssetDatabase.FindAssets("t:Scene", new[] { "Assets/PaperDefense/Scenes/Tests" }).Length,
                Is.EqualTo(SceneNames.Length + PaperDefense.Editor.NotebookArtSceneTools.SceneNames.Length));
        }
    }
}
