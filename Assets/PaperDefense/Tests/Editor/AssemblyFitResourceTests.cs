using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;

namespace PaperDefense.Tests
{
    public sealed class AssemblyFitResourceTests
    {
        [Test] public void ConfigurationResourceCertifiesEveryProductionStarter()
        {
            var fits = AssemblyFitResources.Load();
            var asset = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            Assert.That(asset, Is.Not.Null);
            var catalog = asset.dataPreset.database.assemblyCatalog.CreateSnapshot();
            foreach (var template in catalog.Templates)
                Assert.That(AssemblyFitRules.Validate(template.Snapshot.Recipe, fits), Is.Empty, template.Id);
        }
    }
}
