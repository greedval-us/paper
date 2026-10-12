using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class RicochetPersistenceTests
    {
        string folder, path;
        [SetUp] public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "paper-ricochet-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); path = Path.Combine(folder, "profile.json");
        }
        [TearDown] public void TearDown()
        {
            string absolute = Path.GetFullPath(folder);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(absolute.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(absolute).StartsWith("paper-ricochet-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
        }

        [Test] public void AuthoredMixedRicochetPageAndPermanentRightRoundTripWithoutAnotherMigration()
        {
            var profile = new WorkshopProfile(); profile.GrantGold(100);
            Assert.That(profile.Unlock("weapon.ricochet", out string reason), Is.True, reason);
            var drawing = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Mount1,
                new[] { new DrawingStroke(new[] { new DrawingPoint(-.2f, -.1f), new DrawingPoint(.3f, .4f) }) }) });
            var page = TowerBlueprint.NewDraft("gun-ricochet", "Цепочка", ChassisKind.Standard,
                StructureKind.Gun, StructureKind.Ricochet).WithDrawing(drawing);
            Assert.That(profile.SaveBlueprint(page, out reason), Is.True, reason);
            Assert.That(profile.AssignSlot(0, page.Id, out reason), Is.True, reason);
            var store = new WorkshopProfileStore(path); Assert.That(store.Save(profile).Success, Is.True);
            var loaded = new WorkshopProfileStore(path).Load();
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.Primary));
            Assert.That(loaded.Profile.Gold, Is.EqualTo(40)); Assert.That(loaded.Profile.IsUnlocked("weapon.ricochet"), Is.True);
            TowerBlueprint restored = loaded.Profile.GetLoadoutBlueprints()[0];
            Assert.That(restored.FirstWeapon, Is.EqualTo(StructureKind.Gun));
            Assert.That(restored.SecondWeapon, Is.EqualTo(StructureKind.Ricochet));
            Assert.That(restored.Origin, Is.EqualTo(DrawingOrigin.PlayerDrawing));
            Assert.That(restored.Drawing.GeometryHash, Is.EqualTo(drawing.GeometryHash));
            Assert.That(restored.Drawing.FormatVersion, Is.EqualTo(DrawingSnapshot.LegacyFormatVersion));
            Assert.That(loaded.Profile.ToData().Version, Is.EqualTo(5));
        }

        [Test] public void AssetResetAndMappingExposeIndependentRicochetJumpGeometry()
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.ResetToDefaults();
                var ricochet = asset.weapons.Single(w => w.kind == StructureKind.Ricochet);
                Assert.That(ricochet.jumpRange, Is.EqualTo(3)); Assert.That(ricochet.maxTargets, Is.EqualTo(4));
                ricochet.jumpRange = 2.25f; ricochet.maxTargets = 2;
                var balance = asset.CreateWorkshopBalance(); ricochet.jumpRange = 9; ricochet.maxTargets = 20;
                Assert.That(balance.Structure(StructureKind.Ricochet).JumpRange, Is.EqualTo(2.25f));
                Assert.That(balance.Structure(StructureKind.Ricochet).MaxTargets, Is.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }

        [Test] public void HistoricalAssetWithoutANewWeaponEntryUsesCoreRicochetDefaults()
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.ResetToDefaults(); asset.weapons = asset.weapons.Where(w => w.kind != StructureKind.Ricochet).ToArray();
                asset.weapons.Single(w => w.kind == StructureKind.Gun).cost = 35;
                var balance = asset.CreateWorkshopBalance();
                Assert.That(balance.Structure(StructureKind.Gun).Cost, Is.EqualTo(35));
                Assert.That(balance.Structure(StructureKind.Ricochet).Cost, Is.EqualTo(65));
                Assert.That(balance.Structure(StructureKind.Ricochet).JumpRange, Is.EqualTo(3));
                Assert.That(balance.Structure(StructureKind.Ricochet).MaxTargets, Is.EqualTo(4));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }
    }
}
