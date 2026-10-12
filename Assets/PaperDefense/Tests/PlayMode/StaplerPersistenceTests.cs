using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class StaplerPersistenceTests
    {
        string folder, path;
        [SetUp] public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "paper-stapler-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); path = Path.Combine(folder, "profile.json");
        }
        [TearDown] public void TearDown()
        {
            string absolute = Path.GetFullPath(folder);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(absolute.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(absolute).StartsWith("paper-stapler-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
        }

        [Test] public void AuthoredMortarStaplerAssemblyAndRightRoundTripInVersionThree()
        {
            var profile = new WorkshopProfile(); profile.GrantGold(100);
            Assert.That(profile.Unlock("weapon.stapler", out string reason), Is.True, reason);
            var drawing = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Mount1,
                new[] { new DrawingStroke(new[] { new DrawingPoint(-.3f, -.1f), new DrawingPoint(.2f, .3f) }) }) });
            var page = TowerBlueprint.NewDraft("mortar-stapler", "Мортира со степлером", ChassisKind.Standard,
                StructureKind.Mortar, StructureKind.Stapler).WithDrawing(drawing);
            Assert.That(profile.SaveBlueprint(page, out reason), Is.True, reason);
            Assert.That(profile.AssignSlot(0, page.Id, out reason), Is.True, reason);
            Assert.That(new WorkshopProfileStore(path).Save(profile).Success, Is.True);
            var loaded = new WorkshopProfileStore(path).Load();
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.Primary));
            Assert.That(loaded.Profile.Gold, Is.EqualTo(60)); Assert.That(loaded.Profile.IsUnlocked("weapon.stapler"), Is.True);
            TowerBlueprint restored = loaded.Profile.GetLoadoutBlueprints()[0];
            Assert.That(restored.FirstWeapon, Is.EqualTo(StructureKind.Mortar));
            Assert.That(restored.SecondWeapon, Is.EqualTo(StructureKind.Stapler));
            Assert.That(restored.Origin, Is.EqualTo(DrawingOrigin.PlayerDrawing));
            Assert.That(restored.Drawing.GeometryHash, Is.EqualTo(drawing.GeometryHash));
            Assert.That(restored.Drawing.FormatVersion, Is.EqualTo(DrawingSnapshot.LegacyFormatVersion)); Assert.That(restored.Drawing.BindingVersion, Is.EqualTo(1));
            Assert.That(loaded.Profile.ToData().Version, Is.EqualTo(5));
        }

        [Test] public void AssetResetAndMappingKeepTheConfiguredStaplerSlowIndependent()
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.ResetToDefaults(); var stapler = asset.weapons.Single(w => w.kind == StructureKind.Stapler);
                Assert.That(stapler.slowSpeedMultiplier, Is.EqualTo(.8f)); Assert.That(stapler.slowDuration, Is.EqualTo(2));
                stapler.slowSpeedMultiplier = .73f; stapler.slowDuration = 3.25f;
                var balance = asset.CreateWorkshopBalance(); stapler.slowSpeedMultiplier = .2f; stapler.slowDuration = 20;
                Assert.That(balance.Structure(StructureKind.Stapler).SlowSpeedMultiplier, Is.EqualTo(.73f));
                Assert.That(balance.Structure(StructureKind.Stapler).SlowDuration, Is.EqualTo(3.25f));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }

        [Test] public void ExistingAssetWithoutStaplerEntryKeepsItsCoreDefaults()
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.ResetToDefaults(); asset.weapons = asset.weapons.Where(w => w.kind != StructureKind.Stapler).ToArray();
                var balance = asset.CreateWorkshopBalance(); var stapler = balance.Structure(StructureKind.Stapler);
                Assert.That(stapler.Cost, Is.EqualTo(35)); Assert.That(stapler.SlowSpeedMultiplier, Is.EqualTo(.8f));
                Assert.That(stapler.SlowDuration, Is.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }
    }
}
