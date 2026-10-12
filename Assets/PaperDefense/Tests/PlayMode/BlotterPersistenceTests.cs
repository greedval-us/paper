using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class BlotterPersistenceTests
    {
        string folder, path;
        [SetUp] public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "paper-blotter-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); path = Path.Combine(folder, "profile.json");
        }
        [TearDown] public void TearDown()
        {
            string absolute = Path.GetFullPath(folder);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(absolute.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(absolute).StartsWith("paper-blotter-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
        }

        [Test] public void AuthoredGunBlotterArmorPageAndRightRoundTripInVersionThree()
        {
            var profile = new WorkshopProfile(); profile.GrantGold(100);
            Assert.That(profile.Unlock("weapon.blotter", out string reason), Is.True, reason);
            var drawing = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Mount1,
                new[] { new DrawingStroke(new[] { new DrawingPoint(-.3f, -.2f), new DrawingPoint(.3f, .2f) }) }) });
            var page = TowerBlueprint.NewDraft("gun-blotter", "Защищённая промокашка", ChassisKind.Standard,
                StructureKind.Gun, StructureKind.Blotter, TowerModules.Armor).WithDrawing(drawing);
            Assert.That(profile.SaveBlueprint(page, out reason), Is.True, reason);
            Assert.That(profile.AssignSlot(0, page.Id, out reason), Is.True, reason);
            Assert.That(new WorkshopProfileStore(path).Save(profile).Success, Is.True);
            var loaded = new WorkshopProfileStore(path).Load();
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.Primary));
            Assert.That(loaded.Profile.Gold, Is.EqualTo(60)); Assert.That(loaded.Profile.IsUnlocked("weapon.blotter"), Is.True);
            TowerBlueprint restored = loaded.Profile.GetLoadoutBlueprints()[0];
            Assert.That(restored.FirstWeapon, Is.EqualTo(StructureKind.Gun));
            Assert.That(restored.SecondWeapon, Is.EqualTo(StructureKind.Blotter));
            Assert.That(restored.Modules, Is.EqualTo(TowerModules.Armor));
            Assert.That(restored.Origin, Is.EqualTo(DrawingOrigin.PlayerDrawing));
            Assert.That(restored.Drawing.GeometryHash, Is.EqualTo(drawing.GeometryHash));
            Assert.That(loaded.Profile.ToData().Version, Is.EqualTo(5));
        }

        [Test] public void AssetResetMapsCleanupRadiusAndKeepsManualBlotterIndependent()
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.ResetToDefaults(); var blotter = asset.weapons.Single(w => w.kind == StructureKind.Blotter);
                Assert.That(blotter.radius, Is.EqualTo(1.5f)); Assert.That(blotter.shotInterval, Is.EqualTo(2));
                blotter.radius = 2.25f; blotter.range = 7; blotter.shotInterval = 3;
                asset.blotterRadius = 4; asset.blotterCooldown = 5;
                var balance = asset.CreateWorkshopBalance(); blotter.radius = 9;
                StructureSpec spec = new RunReadModel(new GameSession(balance, starterDefenses: false)).Balance.Structure(StructureKind.Blotter);
                Assert.That(spec.Radius, Is.EqualTo(2.25f)); Assert.That(spec.Range, Is.EqualTo(7)); Assert.That(spec.ShotInterval, Is.EqualTo(3));
                Assert.That(balance.BlotterRadius, Is.EqualTo(4)); Assert.That(balance.BlotterCooldown, Is.EqualTo(5));
                Assert.That(spec.Damage, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }

        [Test] public void ExistingAssetWithoutBlotterEntryUsesTheCoreCleanupDefinition()
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.ResetToDefaults(); asset.weapons = asset.weapons.Where(w => w.kind != StructureKind.Blotter).ToArray();
                var blotter = asset.CreateWorkshopBalance().Structure(StructureKind.Blotter);
                Assert.That(blotter.Cost, Is.EqualTo(30)); Assert.That(blotter.Damage, Is.Zero);
                Assert.That(blotter.Range, Is.EqualTo(9)); Assert.That(blotter.Radius, Is.EqualTo(1.5f));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }
    }
}
