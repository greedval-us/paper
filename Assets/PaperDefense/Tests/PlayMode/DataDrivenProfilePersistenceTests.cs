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
    public sealed class DataDrivenProfilePersistenceTests
    {
        string directory, path;
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "PaperContentProfileTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); path = Path.Combine(directory, "profile.json");
        }
        [TearDown] public void TearDown()
        {
            string target = Path.GetFullPath(directory);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(target.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(target).StartsWith("PaperContentProfileTests-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(target)) Directory.Delete(target, true);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void EveryHistoricalRootVersionMigratesWithoutLosingProgressAndKeepsExactSourceBytes(int version)
        {
            var profile = new WorkshopProfile(); profile.GrantGold(321); profile.Unlock("module.tracking", out _);
            var data = HistoricalProfileTestData.Create(profile, version);
            if (version == 4)
            {
                data = profile.ToData(); data.Version = 4;
            }
            File.WriteAllText(path, JsonUtility.ToJson(data, true), new UnicodeEncoding(false, true));
            byte[] source = File.ReadAllBytes(path);
            var store = new WorkshopProfileStore(path); var loaded = store.Load();
            Assert.That(loaded.CanWrite, Is.True, loaded.Message);
            Assert.That(loaded.Profile.Gold, Is.EqualTo(profile.Gold));
            Assert.That(loaded.Profile.IsUnlocked("module.tracking"), Is.True);
            Assert.That(store.Save(loaded.Profile).Success, Is.True);
            Assert.That(File.ReadAllBytes(path + ".v" + version + "-original"), Is.EqualTo(source));
            var restored = new WorkshopProfileStore(path).Load();
            Assert.That(restored.Profile.ToData().Version, Is.EqualTo(5));
            Assert.That(restored.Profile.Gold, Is.EqualTo(profile.Gold));
            Assert.That(restored.Profile.Loadout, Is.EqualTo(profile.Loadout));
        }

        [TestCase(false)] [TestCase(true)]
        public void VersionFiveEmptyAndPartiallyAssignedAlbumsSurviveSaveRestartAndRewardWrite(bool assignOne)
        {
            var data = new WorkshopProfile().ToData();
            var page = data.Album[0];
            data.Album = assignOne ? new[] { page } : Array.Empty<BlueprintData>();
            data.Loadout = new string[ProfileLimits.LoadoutSlotCount];
            if (assignOne) data.Loadout[2] = page.Id;
            data.Gold = 27;
            var profile = WorkshopProfile.FromData(data);
            var store = new WorkshopProfileStore(path);
            Assert.That(store.Load().CanWrite, Is.True);
            Assert.That(store.Save(profile).Success, Is.True);
            var restarted = new WorkshopProfileStore(path);
            var loaded = restarted.Load();
            Assert.That(loaded.CanWrite, Is.True, loaded.Message);
            Assert.That(loaded.Profile.Album.Count, Is.EqualTo(assignOne ? 1 : 0));
            var slots = loaded.Profile.GetLoadoutBlueprints();
            Assert.That(slots[0], Is.Null); Assert.That(slots[1], Is.Null); Assert.That(slots[3], Is.Null);
            Assert.That(slots[2]?.Id, Is.EqualTo(assignOne ? page.Id : null));
            loaded.Profile.GrantGold(3);
            Assert.That(restarted.Save(loaded.Profile).Success, Is.True);
            var final = new WorkshopProfileStore(path).Load();
            Assert.That(final.Profile.Gold, Is.EqualTo(30));
            Assert.That(final.Profile.Loadout, Is.EqualTo(data.Loadout));
            Assert.That(final.Profile.Album.Count, Is.EqualTo(assignOne ? 1 : 0));
        }

        [Test] public void UnknownStableContentAndItsAuthoredLinesSurviveSaveRestartAndRewardWrite()
        {
            var drawing = new DrawingSnapshot(new[] {
                new DrawingPart(DrawingBindings.Mount0, new[] { new DrawingStroke(new[] { new DrawingPoint(-.2f,0), new DrawingPoint(.1f,.3f) }) }),
                new DrawingPart(DrawingBindings.Module("future-slot"), new[] { new DrawingStroke(new[] { new DrawingPoint(-.3f,.2f), new DrawingPoint(.4f,0) }) })
            });
            var page = new TowerBlueprint("unknown.page", "Unknown preserved", "chassis.future", "weapon.future",
                modules: new[] { new ModuleSelection("future-slot", "module.future") }, drawing: drawing, origin: DrawingOrigin.PlayerDrawing);
            var data = new WorkshopProfile().ToData();
            data.Album = data.Album.Concat(new[] { new BlueprintData(page) }).ToArray();
            data.Unlocked = data.Unlocked.Concat(new[] { "chassis.future", "weapon.future", "module.future" }).ToArray();
            data.Loadout[0] = page.Id; data.Gold = 217;
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            var store = new WorkshopProfileStore(path); var loaded = store.Load();
            Assert.That(loaded.CanWrite, Is.True, loaded.Message);
            var actual = loaded.Profile.GetLoadoutBlueprints()[0];
            Assert.That(loaded.Profile.ValidateOwned(actual, out _), Is.False);
            Assert.That(actual.FirstWeapon, Is.EqualTo((StructureKind)(-1)));
            loaded.Profile.GrantGold(8);
            Assert.That(store.Save(loaded.Profile).Success, Is.True);
            var restored = new WorkshopProfileStore(path).Load();
            Assert.That(restored.Profile.Gold, Is.EqualTo(225));
            actual = restored.Profile.GetLoadoutBlueprints()[0];
            Assert.That(actual.ChassisId, Is.EqualTo("chassis.future"));
            Assert.That(actual.FirstWeaponId, Is.EqualTo("weapon.future"));
            Assert.That(actual.ModuleSelections[0].DefinitionId, Is.EqualTo("module.future"));
            Assert.That(actual.Drawing.GeometryHash, Is.EqualTo(drawing.GeometryHash));
            Assert.That(actual.Assessment.Quality, Is.EqualTo(page.Assessment.Quality));
            Assert.That(restored.Profile.IsUnlocked("module.future"), Is.True);
        }

        [TestCase(1, "right")] [TestCase(1, "weapon")] [TestCase(1, "module")]
        [TestCase(2, "right")] [TestCase(2, "weapon")] [TestCase(2, "module")]
        public void UnknownHistoricalContractsRemainProtectedInsteadOfRollingBackToOlderBackup(int version, string field)
        {
            var data = HistoricalProfileTestData.Create(new WorkshopProfile(), version);
            if (field == "right") data.Unlocked = data.Unlocked.Concat(new[] { "weapon.future" }).ToArray();
            if (field == "weapon") data.Album[0].FirstWeapon = (StructureKind)999;
            if (field == "module") data.Album[0].Modules |= (TowerModules)1024;
            AssertProtected(data);
        }

        [TestCase("root")] [TestCase("content")] [TestCase("evaluation")] [TestCase("drawing")] [TestCase("origin")]
        public void FutureVersionFiveSchemaCannotOverwriteSourceOrRollBackToCompatibleBackup(string field)
        {
            var data = new WorkshopProfile().ToData();
            if (field == "root") data.Version = WorkshopProfile.CurrentVersion + 1;
            if (field == "content") data.Album[0].ContentMetadataVersion = 2;
            if (field == "evaluation") data.Album[0].EvaluationMetadataVersion = 2;
            if (field == "drawing") data.Album[0].Drawing.FormatVersion = 99;
            if (field == "origin") data.Album[0].Origin = (DrawingOrigin)99;
            AssertProtected(data);
        }

        void AssertProtected(WorkshopProfileData data)
        {
            File.WriteAllText(path, JsonUtility.ToJson(data, true), new UnicodeEncoding(false, true));
            File.WriteAllText(path + ".bak", JsonUtility.ToJson(new WorkshopProfile().ToData(), true));
            byte[] source = File.ReadAllBytes(path), backup = File.ReadAllBytes(path + ".bak");
            var store = new WorkshopProfileStore(path); var loaded = store.Load();
            Assert.That(loaded.CanWrite, Is.False);
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.RecoveryRequired));
            Assert.That(store.Save(loaded.Profile).Success, Is.False);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(source));
            Assert.That(File.ReadAllBytes(path + ".bak"), Is.EqualTo(backup));
        }
    }
}
