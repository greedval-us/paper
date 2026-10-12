using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class FanPersistenceTests
    {
        string folder, path;
        [SetUp] public void SetUp() { folder = Path.Combine(Path.GetTempPath(), "paper-fan-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); path = Path.Combine(folder, "profile.json"); }
        [TearDown] public void TearDown()
        {
            string absolute = Path.GetFullPath(folder);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(absolute.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(absolute).StartsWith("paper-fan-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
        }
        static DrawingSnapshot Lines() => new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Mount0,
            new[] { new DrawingStroke(new[] { new DrawingPoint(-.1f, -.2f), new DrawingPoint(.2f, .4f) }) }) });
        static WorkshopProfile HistoricalProfile()
        {
            var profile = new WorkshopProfile(); profile.GrantGold(777); profile.Unlock("chassis.heavy", out _);
            var page = TowerBlueprint.NewDraft("old-authored", "Неровная старая", ChassisKind.Standard, StructureKind.Gun).WithDrawing(Lines());
            Assert.That(profile.SaveBlueprint(page, out string reason), Is.True, reason);
            Assert.That(profile.AssignSlot(0, page.Id, out reason), Is.True, reason);
            Assert.That(profile.RemoveBlueprint("starter.gun", out reason), Is.True, reason);
            return profile;
        }
        static string V2Json(WorkshopProfile profile) => JsonUtility.ToJson(HistoricalProfileTestData.Create(profile, 2), true);
        [TestCase(false)] [TestCase(true)]
        public void VersionTwoMigrationPreservesActualOriginalBytesAndAllOwnedPagesBeforeCommit(bool backup)
        {
            var old = HistoricalProfile(); string source = backup ? path + ".bak" : path;
            File.WriteAllText(source, V2Json(old), Encoding.Unicode); byte[] original = File.ReadAllBytes(source);
            if (backup) File.WriteAllText(path, "broken primary");
            var store = new WorkshopProfileStore(path); var loaded = store.Load();
            Assert.That(loaded.CanWrite, Is.True); Assert.That(loaded.Profile.Gold, Is.EqualTo(old.Gold));
            Assert.That(loaded.Profile.Album.Select(p => p.Id), Is.EqualTo(old.Album.Select(p => p.Id)));
            Assert.That(loaded.Profile.Loadout, Is.EqualTo(old.Loadout));
            Assert.That(loaded.Profile.Album.Single(p => p.Id == "old-authored").Drawing.GeometryHash, Is.EqualTo(Lines().GeometryHash));
            Assert.That(store.Save(loaded.Profile).Success, Is.True);
            Assert.That(File.ReadAllBytes(path + ".v2-original"), Is.EqualTo(original));
            Assert.That(JsonUtility.FromJson<WorkshopProfileData>(File.ReadAllText(path)).Version, Is.EqualTo(WorkshopProfile.CurrentVersion));
            loaded.Profile.GrantGold(1); Assert.That(store.Save(loaded.Profile).Success, Is.True);
            Assert.That(File.ReadAllBytes(path + ".v2-original"), Is.EqualTo(original));
            Assert.That(new WorkshopProfileStore(path).Load().Profile.Gold, Is.EqualTo(old.Gold + 1));
        }
        [Test] public void FailedVersionTwoOriginalCopyLeavesBothProgressFilesExact()
        {
            string old = V2Json(HistoricalProfile()); File.WriteAllText(path, old); File.WriteAllText(path + ".bak", old);
            byte[] primary = File.ReadAllBytes(path), backup = File.ReadAllBytes(path + ".bak");
            var store = new WorkshopProfileStore(path, new RejectCopyIO()); var loaded = store.Load(); loaded.Profile.GrantGold(1);
            Assert.That(store.Save(loaded.Profile).Success, Is.False);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(primary)); Assert.That(File.ReadAllBytes(path + ".bak"), Is.EqualTo(backup));
        }
        [Test] public void FanPageAndPermanentRightRoundTripThroughVersionThree()
        {
            var profile = new WorkshopProfile(); profile.GrantGold(100); Assert.That(profile.Unlock("weapon.fan", out _), Is.True);
            var page = TowerBlueprint.NewDraft("fan", "Кривой веер", ChassisKind.Standard, StructureKind.Fan).WithDrawing(Lines());
            Assert.That(profile.SaveBlueprint(page, out string reason), Is.True, reason); Assert.That(profile.AssignSlot(0, page.Id, out reason), Is.True, reason);
            var store = new WorkshopProfileStore(path); Assert.That(store.Save(profile).Success, Is.True);
            var restored = new WorkshopProfileStore(path).Load(); Assert.That(restored.Source, Is.EqualTo(ProfileLoadSource.Primary));
            Assert.That(restored.Profile.IsUnlocked("weapon.fan"), Is.True); Assert.That(restored.Profile.Gold, Is.EqualTo(40));
            Assert.That(restored.Profile.GetLoadoutBlueprints()[0].FirstWeapon, Is.EqualTo(StructureKind.Fan));
            Assert.That(restored.Profile.GetLoadoutBlueprints()[0].Drawing.GeometryHash, Is.EqualTo(page.Drawing.GeometryHash));
        }
        [Test] public void AssetMapsAndResetsTheNewGeometryWithoutChangingExistingWeaponValues()
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.ResetToDefaults(); Assert.That(asset.weapons.Any(w => w.kind == StructureKind.Fan), Is.True);
                var fan = asset.weapons.Single(w => w.kind == StructureKind.Fan); fan.coneAngleDegrees = 55; fan.maxTargets = 2;
                var balance = asset.CreateWorkshopBalance(); Assert.That(balance.Structure(StructureKind.Fan).ConeAngleDegrees, Is.EqualTo(55));
                Assert.That(balance.Structure(StructureKind.Fan).MaxTargets, Is.EqualTo(2));
                Assert.That(balance.Structure(StructureKind.Gun).Cost, Is.EqualTo(40)); Assert.That(balance.Structure(StructureKind.Mortar).Range, Is.EqualTo(16));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }
        sealed class RejectCopyIO : IWorkshopProfileIO
        {
            readonly WorkshopProfileFileIO actual = new WorkshopProfileFileIO();
            public string ReadAllText(string file) => actual.ReadAllText(file);
            public void CreateDirectory(string directory) => actual.CreateDirectory(directory);
            public void WriteAllText(string file, string content) => actual.WriteAllText(file, content);
            public void Move(string source, string target) => actual.Move(source, target);
            public void Copy(string source, string target) => throw new IOException("Injected v2-original copy failure");
            public void Replace(string source, string target, string backup) => actual.Replace(source, target, backup);
            public void Delete(string file) => actual.Delete(file);
        }
    }
}
