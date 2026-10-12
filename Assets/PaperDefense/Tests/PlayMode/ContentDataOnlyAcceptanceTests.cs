using NUnit.Framework;
using PaperDefense.Application;
using UnityEditor;

namespace PaperDefense.Tests
{
    public sealed class ContentDataOnlyAcceptanceTests
    {
        [Test] public void RegisteredAcceptanceDataPassesTheSameHarnessAsADevelopmentPlayer()
        {
            const string path = "Assets/PaperDefense/Content/Acceptance/ContentAcceptanceManifest.asset";
            var manifest = AssetDatabase.LoadAssetAtPath<ContentAcceptanceManifestAsset>(path);
            if (manifest == null) Assert.Ignore("The separate data-only acceptance stage has not created its manifest yet.");
            var result = ContentAcceptanceRunner.Run(manifest);
            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.TemporaryProfileRemoved, Is.True);
            Assert.That(result.Checks.Count, Is.EqualTo(5));
            Assert.That(result.BattleLaunches, Is.GreaterThan(0));
            Assert.That(result.BattleDamage, Is.GreaterThan(0));
        }
    }
}
