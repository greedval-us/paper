using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class WorkshopStoreTests
    {
        string directory, path;
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "PaperStoreTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); path = Path.Combine(directory, "profile.json");
        }
        [TearDown] public void TearDown()
        {
            string absolute = Path.GetFullPath(directory);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(absolute.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(absolute).StartsWith("PaperStoreTests-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
        }
        static WorkshopProfile RichProfile()
        {
            var profile = new WorkshopProfile(); profile.GrantGold(471);
            Assert.That(profile.Unlock("chassis.heavy", out _), Is.True);
            Assert.That(profile.Unlock("module.tracking", out _), Is.True);
            Assert.That(profile.SaveBlueprint(new TowerBlueprint("custom", "Моя башня", ChassisKind.Heavy,
                StructureKind.Gun, modules: TowerModules.Tracking), out _), Is.True);
            Assert.That(profile.AssignSlot(0, "custom", out _), Is.True);
            return profile; // 321 gold after two permanent unlocks.
        }
        static string Json(WorkshopProfile profile) => JsonUtility.ToJson(profile.ToData(), true);

        [Test] public void CorruptPrimaryRecoversValidBackupAndSubsequentSavesPreserveRecoverableData()
        {
            string corrupt = "broken primary"; File.WriteAllText(path, corrupt);
            File.WriteAllText(path + ".bak", Json(RichProfile()));
            var store = new WorkshopProfileStore(path);
            ProfileLoadResult loaded = store.Load();
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.Backup));
            Assert.That(loaded.CanWrite, Is.True);
            Assert.That(loaded.Profile.Gold, Is.EqualTo(321));
            Assert.That(loaded.Profile.IsUnlocked("module.tracking"), Is.True);
            Assert.That(loaded.Profile.Loadout[0], Is.EqualTo("custom"));
            Assert.That(store.Save(loaded.Profile).Success, Is.True);
            Assert.That(new WorkshopProfileStore(path + ".bak").Load().Profile.Gold, Is.EqualTo(321));
            string[] quarantined = Directory.GetFiles(directory, "profile.json.corrupt-*");
            Assert.That(quarantined.Length, Is.EqualTo(1));
            Assert.That(File.ReadAllText(quarantined[0]), Is.EqualTo(corrupt));
            loaded.Profile.GrantGold(7);
            Assert.That(store.Save(loaded.Profile).Success, Is.True);
            Assert.That(store.Save(loaded.Profile).Success, Is.True); // quit uses the same protected commit path.
            Assert.That(new WorkshopProfileStore(path).Load().Profile.Gold, Is.EqualTo(328));
            Assert.That(new WorkshopProfileStore(path + ".bak").Load().Profile.Gold, Is.EqualTo(328));
        }

        [Test] public void MissingPrimaryRecoversBackupWithoutReplacingItWithStarterData()
        {
            File.WriteAllText(path + ".bak", Json(RichProfile()));
            var store = new WorkshopProfileStore(path); var loaded = store.Load();
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.Backup));
            Assert.That(loaded.Profile.Gold, Is.EqualTo(321));
            Assert.That(store.Save(loaded.Profile).Success, Is.True);
            Assert.That(new WorkshopProfileStore(path + ".bak").Load().Profile.Gold, Is.EqualTo(321));
        }

        [TestCase("bad primary", "bad backup")]
        [TestCase("{\"Version\":999,\"Gold\":9999}", "VALID_BACKUP")]
        [TestCase("bad primary", "{\"Version\":999,\"Gold\":9999}")]
        public void DamagedOrFutureSourcesRemainByteForByteProtected(string primary, string backup)
        {
            if (backup == "VALID_BACKUP") backup = Json(RichProfile());
            File.WriteAllText(path, primary); File.WriteAllText(path + ".bak", backup);
            var store = new WorkshopProfileStore(path); var loaded = store.Load();
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.RecoveryRequired));
            Assert.That(loaded.CanWrite, Is.False);
            Assert.That(loaded.Message, Is.Not.Empty);
            Assert.That(store.Save(new WorkshopProfile()).Success, Is.False);
            Assert.That(store.Save(loaded.Profile).Success, Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(primary));
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(backup));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(2));
        }

        [TestCase("right")]
        [TestCase("first-weapon")]
        [TestCase("second-weapon")]
        [TestCase("module")]
        [TestCase("chassis")]
        public void UnknownVersionThreePartsCannotRollBackToACompatibleBackup(string unknownPart)
        {
            var future = HistoricalProfileTestData.Create(RichProfile(), 3);
            if (unknownPart == "right")
            {
                var rights = new List<string>(future.Unlocked) { "weapon.future" };
                future.Unlocked = rights.ToArray();
            }
            if (unknownPart == "first-weapon") future.Album[0].FirstWeapon = (StructureKind)99;
            if (unknownPart == "second-weapon") { future.Album[0].HasSecondWeapon = true; future.Album[0].SecondWeapon = (StructureKind)99; }
            if (unknownPart == "module") future.Album[0].Modules |= (TowerModules)256;
            if (unknownPart == "chassis") future.Album[0].Chassis = (ChassisKind)99;
            File.WriteAllText(path, JsonUtility.ToJson(future, true), System.Text.Encoding.Unicode);
            File.WriteAllText(path + ".bak", Json(RichProfile()));
            byte[] primary = File.ReadAllBytes(path), backup = File.ReadAllBytes(path + ".bak");

            var store = new WorkshopProfileStore(path); var loaded = store.Load();
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.RecoveryRequired));
            Assert.That(loaded.CanWrite, Is.False); Assert.That(loaded.Message, Is.Not.Empty);
            Assert.That(store.Save(loaded.Profile).Success, Is.False);
            Assert.That(store.Save(new WorkshopProfile()).Success, Is.False);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(path + ".bak"), Is.EqualTo(backup));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(2));
        }

        [Test] public void AccessFailureIsProtectedRatherThanTreatedAsMissingOrBackupRecovery()
        {
            string primary = Json(RichProfile()); File.WriteAllText(path, primary);
            File.WriteAllText(path + ".bak", primary);
            var io = new FaultIO { DenyReadPath = path };
            var store = new WorkshopProfileStore(path, io); var loaded = store.Load();
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.RecoveryRequired));
            Assert.That(loaded.CanWrite, Is.False);
            io.DenyReadPath = null;
            Assert.That(store.Save(new WorkshopProfile()).Success, Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(primary));
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(primary));
        }

        [Test] public void MissingPrimaryWithUnreadableBackupCannotCreateAStarterFile()
        {
            string backup = Json(RichProfile()); File.WriteAllText(path + ".bak", backup);
            var store = new WorkshopProfileStore(path, new FaultIO { DenyReadPath = path + ".bak" });
            var loaded = store.Load();
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.RecoveryRequired));
            Assert.That(store.Save(loaded.Profile).Success, Is.False);
            Assert.That(File.Exists(path), Is.False);
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(backup));
        }

        [Test] public void ASourceThatChangesToFutureVersionBetweenLoadAndSaveIsProtected()
        {
            File.WriteAllText(path, Json(RichProfile()));
            var store = new WorkshopProfileStore(path); var loaded = store.Load();
            string future = "{\"Version\":99,\"Gold\":12345}"; File.WriteAllText(path, future);
            Assert.That(store.Save(loaded.Profile).Success, Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(future));
        }

        [Test] public void NoSourcesCreatesWritableNewProfileAndARealFirstSave()
        {
            var store = new WorkshopProfileStore(path); var loaded = store.Load();
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.NewProfile));
            Assert.That(loaded.CanWrite, Is.True);
            loaded.Profile.GrantGold(17);
            Assert.That(store.Save(loaded.Profile).Success, Is.True);
            Assert.That(new WorkshopProfileStore(path).Load().Profile.Gold, Is.EqualTo(17));
        }

        [Test] public void ValidPrimaryIgnoresBrokenBackup()
        {
            File.WriteAllText(path, Json(RichProfile())); File.WriteAllText(path + ".bak", "broken backup");
            var loaded = new WorkshopProfileStore(path).Load();
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.Primary));
            Assert.That(loaded.Profile.Gold, Is.EqualTo(321));
            Assert.That(loaded.CanWrite, Is.True);
        }

        [TestCase("write")]
        [TestCase("replace")]
        public void FailureBeforeCommitPreservesBothExistingFiles(string operation)
        {
            string initial = Json(RichProfile()); File.WriteAllText(path, initial); File.WriteAllText(path + ".bak", initial);
            var io = new FaultIO { Failure = operation }; var store = new WorkshopProfileStore(path, io);
            var candidate = store.Load().Profile; candidate.GrantGold(9);
            var result = store.Save(candidate);
            Assert.That(result.Success, Is.False); Assert.That(result.Message, Is.Not.Empty);
            Assert.That(File.ReadAllText(path), Is.EqualTo(initial));
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(initial));
        }

        [Test] public void FailedRecoveredCommitPreservesTheGoodBackupAndTheDamagedOriginal()
        {
            string corrupt = "damaged original", backup = Json(RichProfile());
            File.WriteAllText(path, corrupt); File.WriteAllText(path + ".bak", backup);
            var store = new WorkshopProfileStore(path, new FaultIO { Failure = "move" });
            var loaded = store.Load(); Assert.That(store.Save(loaded.Profile).Success, Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(corrupt));
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(backup));
        }

        [Test] public void MalformedTemporaryCandidateIsRejectedBeforeCommit()
        {
            string initial = Json(RichProfile()); File.WriteAllText(path, initial);
            var store = new WorkshopProfileStore(path, new FaultIO { CorruptTemporaryWrite = true });
            var candidate = store.Load().Profile; candidate.GrantGold(9);
            Assert.That(store.Save(candidate).Success, Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(initial));
        }

        [Test] public void OptionalCleanupFailureCannotReportFailureAfterConfirmedCommit()
        {
            File.WriteAllText(path, Json(RichProfile()));
            var store = new WorkshopProfileStore(path, new FaultIO { Failure = "delete" });
            var candidate = store.Load().Profile; candidate.GrantGold(9);
            Assert.That(store.Save(candidate).Success, Is.True);
            Assert.That(new WorkshopProfileStore(path).Load().Profile.Gold, Is.EqualTo(330));
        }

        [Test] public void CompleteAlbumAndWorkingCopyPreserveExactlyTheCurrentPagesWithoutAddingStarters()
        {
            var original = new WorkshopProfile();
            for (int i = 0; i < 4; i++)
            {
                var page = new TowerBlueprint("replacement." + i, "Replacement " + i, ChassisKind.Standard, StructureKind.Gun);
                Assert.That(original.SaveBlueprint(page, out _), Is.True);
                string old = original.Loadout[i]; Assert.That(original.AssignSlot(i, page.Id, out _), Is.True);
                Assert.That(original.RemoveBlueprint(old, out _), Is.True);
            }
            var copy = original.CreateWorkingCopy();
            Assert.That(copy.Album.Count, Is.EqualTo(4));
            Assert.That(copy.Loadout.Count, Is.EqualTo(ProfileLimits.LoadoutSlotCount));
            Assert.That(copy.Loadout.Take(ProfileLimits.HistoricalLoadoutSlotCount), Is.EqualTo(new[] { "replacement.0", "replacement.1", "replacement.2", "replacement.3" }));
            Assert.That(copy.Loadout.Skip(ProfileLimits.HistoricalLoadoutSlotCount), Is.All.Null);
            copy.GrantGold(100); Assert.That(copy.Unlock("module.feed", out _), Is.True);
            for (int i = 4; i < 64; i++)
                Assert.That(copy.SaveBlueprint(new TowerBlueprint("page." + i, "Page " + i, ChassisKind.Standard,
                    StructureKind.Gun), out _), Is.True);
            Assert.That(original.Gold, Is.Zero); Assert.That(original.IsUnlocked("module.feed"), Is.False);
            Assert.That(original.Album.Count, Is.EqualTo(4));
            var store = new WorkshopProfileStore(path); Assert.That(store.Save(copy).Success, Is.True);
            var loaded = new WorkshopProfileStore(path).Load().Profile;
            Assert.That(loaded.Album.Count, Is.EqualTo(64)); Assert.That(loaded.Gold, Is.EqualTo(40));
            Assert.That(loaded.Loadout, Is.EqualTo(copy.Loadout));
            Assert.That(loaded.Album[63].Id, Is.EqualTo("page.63"));
        }

        sealed class FaultIO : IWorkshopProfileIO
        {
            public string Failure, DenyReadPath;
            public bool CorruptTemporaryWrite;
            public string ReadAllText(string file)
            { if (file == DenyReadPath) throw new UnauthorizedAccessException("Injected read denial"); return File.ReadAllText(file); }
            public void CreateDirectory(string folder) => Directory.CreateDirectory(folder);
            public void WriteAllText(string file, string contents)
            { if (Failure == "write") throw new IOException("Injected write failure"); File.WriteAllText(file, CorruptTemporaryWrite ? "invalid temporary candidate" : contents); }
            public void Move(string source, string target)
            { if (Failure == "move") throw new IOException("Injected move failure"); File.Move(source, target); }
            public void Copy(string source, string target) => File.Copy(source, target, false);
            public void Replace(string source, string target, string backup)
            { if (Failure == "replace") throw new IOException("Injected replace failure"); File.Replace(source, target, backup); }
            public void Delete(string file)
            { if (Failure == "delete") throw new IOException("Injected cleanup failure"); File.Delete(file); }
        }
    }
}
