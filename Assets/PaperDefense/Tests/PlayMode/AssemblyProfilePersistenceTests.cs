#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class AssemblyProfilePersistenceTests
    {
        string directory, path;
        ComponentCatalog catalog;

        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "PaperAssemblyProfileTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "profile.json");
            var asset = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(
                "Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset");
            Assert.That(asset, Is.Not.Null);
            catalog = asset.CreateSnapshot();
        }

        [TearDown] public void TearDown()
        {
            string target = Path.GetFullPath(directory);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(target.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(target).StartsWith("PaperAssemblyProfileTests-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(target)) Directory.Delete(target, true);
        }

        WorkshopProfileStore Store(IWorkshopProfileIO io = null)
            => new WorkshopProfileStore(path, io, catalog);

        static WorkshopProfileData HistoricalVersionSeven()
        {
            var profile = new WorkshopProfile();
            profile.GrantGold(321);
            Assert.That(profile.Unlock("module.tracking", out var reason), Is.True, reason);
            var data = profile.ToData();
            data.Version = 7;
            data.Entries = null;
            data.Library = null;
            data.Rewards = null;
            data.Operations = null;
            data.Loadout = data.Loadout.Take(4).ToArray();
            return data;
        }

        byte[] WriteHistorical(string file = null)
        {
            File.WriteAllText(file ?? path, JsonUtility.ToJson(HistoricalVersionSeven(), true), new UnicodeEncoding(false, true));
            return File.ReadAllBytes(file ?? path);
        }

        [Test] public void NewConfiguredStoreRoundTripPreservesAllEightOwnedStarterSnapshots()
        {
            var store = Store();
            var loaded = store.Load();
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.NewProfile));
            Assert.That(loaded.CanWrite, Is.True, loaded.Message);
            Assert.That(loaded.Profile.Entries.Count, Is.EqualTo(8));
            Assert.That(loaded.Profile.Loadout, Is.EqualTo(catalog.Starters.Select(entry => entry.Id)));
            Assert.That(loaded.Profile.TotalXp, Is.Zero);
            Assert.That(File.Exists(path), Is.False);
            string expected = JsonUtility.ToJson(loaded.Profile.ToData());
            Assert.That(store.Save(loaded.Profile).Success, Is.True, store.LastDiagnostic);

            var restarted = Store().Load();
            Assert.That(restarted.CanWrite, Is.True, restarted.Message);
            Assert.That(restarted.RequiresMigration, Is.False);
            Assert.That(restarted.Source, Is.EqualTo(ProfileLoadSource.Primary));
            Assert.That(JsonUtility.ToJson(restarted.Profile.ToData()), Is.EqualTo(expected));
            Assert.That(restarted.Profile.ToData().Version, Is.EqualTo(8));
        }

        [TestCase(false)] [TestCase(true)]
        public void HistoricalSourceLoadsWithoutWritingAndCommitKeepsExactOriginalBytes(bool fromBackup)
        {
            var historical = HistoricalVersionSeven();
            byte[] original = WriteHistorical(fromBackup ? path + ".bak" : path);
            var store = Store();
            var loaded = store.Load();
            Assert.That(loaded.CanWrite, Is.True, loaded.Message);
            Assert.That(loaded.Source, Is.EqualTo(fromBackup ? ProfileLoadSource.Backup : ProfileLoadSource.Primary));
            Assert.That(loaded.RequiresMigration, Is.True);
            Assert.That(loaded.Profile.Gold, Is.EqualTo(historical.Gold));
            Assert.That(loaded.Profile.IsUnlocked("module.tracking"), Is.True);
            Assert.That(loaded.Profile.TotalXp, Is.Zero);
            Assert.That(loaded.Profile.Loadout.Take(4), Is.EqualTo(historical.Loadout));
            Assert.That(loaded.Profile.Loadout.Skip(4), Is.All.Null);
            Assert.That(File.ReadAllBytes(fromBackup ? path + ".bak" : path), Is.EqualTo(original));
            Assert.That(File.Exists(path + ".v7-original"), Is.False);
            Assert.That(store.Save(loaded.Profile).Success, Is.True, store.LastDiagnostic);
            Assert.That(File.ReadAllBytes(path + ".v7-original"), Is.EqualTo(original));
            Assert.That(Store().Load().RequiresMigration, Is.False);
        }

        [Test] public void VersionEightRoundTripPreservesXpLibraryAndReceiptsTogether()
        {
            var data = WorkshopProfile.CreateNew(catalog).ToData();
            var page = data.Entries[0];
            data.TotalXp = 390;
            data.Library = new[] { new ComponentLibraryEntryData {
                Id = "library.saved.foundation", Name = "Saved foundation", Revision = 1, Kind = ComponentLibraryKind.Foundation,
                Snapshot = new ComponentRecipeSnapshotData { Kind = ComponentLibraryKind.Foundation,
                    Foundation = page.Snapshot.Recipe.Foundation, BalanceRevision = page.Snapshot.BalanceRevision,
                    Definitions = page.Snapshot.Definitions.Where(definition => (int)definition.Category <= (int)ComponentCategory.Turn).ToArray() }
            } };
            page.LibraryReferences = new[] { new ComponentLibraryReferenceData {
                Id = data.Library[0].Id, Revision = 1, Kind = ComponentLibraryKind.Foundation, SlotIndex = -1 } };
            data.Rewards = new[] { new RunRewardReceiptData {
                RunId = Guid.NewGuid().ToString("N"), Waves = 8, VictoryBonus = true } };
            data.Operations = new[] { new OperationReceiptData {
                OperationId = Guid.NewGuid().ToString("N"), Type = "save-entry", ResultId = page.Id } };
            var profile = WorkshopProfile.FromData(data, catalog);
            string expected = JsonUtility.ToJson(profile.ToData());
            var store = Store();
            Assert.That(store.Save(profile).Success, Is.True, store.LastDiagnostic);

            var restarted = Store().Load();
            Assert.That(restarted.CanWrite, Is.True, restarted.Message);
            Assert.That(restarted.Profile.TotalXp, Is.EqualTo(390));
            Assert.That(restarted.Profile.Library.Count, Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(restarted.Profile.ToData()), Is.EqualTo(expected));
        }

        [TestCase("copy")] [TestCase("original-move")] [TestCase("replace")]
        public void HistoricalCommitFailureKeepsSourceAndBackupAndCanBeRetried(string failure)
        {
            byte[] original = WriteHistorical();
            File.Copy(path, path + ".bak");
            var io = new FaultIO { Failure = failure };
            var store = Store(io);
            var loaded = store.Load();
            loaded.Profile.GrantGold(8);

            Assert.That(store.Save(loaded.Profile).Success, Is.False);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(path + ".bak"), Is.EqualTo(original));
            if (failure == "replace")
                Assert.That(File.ReadAllBytes(path + ".v7-original"), Is.EqualTo(original), "An interrupted commit must retain the completed historical backup.");
            Assert.That(Store().Load().Profile.Gold, Is.EqualTo(HistoricalVersionSeven().Gold));

            io.Failure = null;
            Assert.That(store.Save(loaded.Profile).Success, Is.True, store.LastDiagnostic);
            Assert.That(Store().Load().Profile.Gold, Is.EqualTo(loaded.Profile.Gold));
            Assert.That(File.ReadAllBytes(path + ".v7-original"), Is.EqualTo(original));
        }

        [Test] public void ExistingHistoricalEvidenceWithAnotherEncodingDoesNotReplaceTheExactSourceBackup()
        {
            byte[] original = WriteHistorical();
            string sameJson = File.ReadAllText(path);
            File.WriteAllText(path + ".v7-original", sameJson, new UTF8Encoding(false));
            byte[] previousEvidence = File.ReadAllBytes(path + ".v7-original");
            var store = Store();
            var loaded = store.Load();
            Assert.That(store.Save(loaded.Profile).Success, Is.True, store.LastDiagnostic);
            Assert.That(File.ReadAllBytes(path + ".v7-original"), Is.EqualTo(previousEvidence));
            Assert.That(Directory.GetFiles(directory, "profile.json.v7-original-*")
                .Any(file => File.ReadAllBytes(file).SequenceEqual(original)), Is.True);
        }

        [Test] public void UnsupportedEmbeddedPartRevisionProtectsPrimaryWithoutRollingBackToValidBackup()
        {
            var profile = WorkshopProfile.CreateNew(catalog);
            var data = profile.ToData();
            string valid = JsonUtility.ToJson(data, true);
            // Change the recipe reference while keeping its owned definition at the known revision.
            data.Entries[0].Snapshot.Recipe.Foundation.Frame.Revision = 999;
            string future = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, future, new UnicodeEncoding(false, true));
            File.WriteAllText(path + ".bak", valid);
            byte[] primary = File.ReadAllBytes(path), backup = File.ReadAllBytes(path + ".bak");
            var store = Store();

            var loaded = store.Load();
            Assert.That(loaded.CanWrite, Is.False);
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.RecoveryRequired));
            Assert.That(loaded.Message, Is.Not.Empty);
            Assert.That(store.Save(profile).Success, Is.False);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(path + ".bak"), Is.EqualTo(backup));
        }

        [TestCase("root")] [TestCase("entry")] [TestCase("snapshot")]
        public void FutureSchemaProtectsPrimaryAndBackupWithoutReset(string field)
        {
            var profile = WorkshopProfile.CreateNew(catalog);
            var data = profile.ToData();
            string valid = JsonUtility.ToJson(data, true);
            if (field == "root") data.Version = 999;
            if (field == "entry") data.Entries[0].Version = 999;
            if (field == "snapshot") data.Entries[0].Snapshot.Version = 999;
            File.WriteAllText(path, JsonUtility.ToJson(data, true), new UnicodeEncoding(false, true));
            File.WriteAllText(path + ".bak", valid);
            byte[] primary = File.ReadAllBytes(path), backup = File.ReadAllBytes(path + ".bak");
            var store = Store();

            var loaded = store.Load();
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.RecoveryRequired));
            Assert.That(loaded.CanWrite, Is.False);
            Assert.That(loaded.Message, Is.Not.Empty);
            Assert.That(store.Save(profile).Success, Is.False);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(path + ".bak"), Is.EqualTo(backup));
        }

        [TestCase("entry")] [TestCase("library")] [TestCase("reward")] [TestCase("operation")] [TestCase("album")]
        public void MalformedFirstEntryCannotHideLaterFutureMetadataAndPermitBackupRollback(string field)
        {
            var profile = WorkshopProfile.CreateNew(catalog);
            var data = profile.ToData();
            string valid = JsonUtility.ToJson(data, true);
            data.Entries[0].Version = 0;
            if (field == "entry") data.Entries[1].Version = 999;
            if (field == "library")
            {
                var page = data.Entries[1];
                data.Library = new[] { new ComponentLibraryEntryData {
                    Version = 999, Id = "library.future.foundation", Name = "Future foundation", Revision = 1,
                    Kind = ComponentLibraryKind.Foundation,
                    Snapshot = new ComponentRecipeSnapshotData { Kind = ComponentLibraryKind.Foundation,
                        Foundation = page.Snapshot.Recipe.Foundation, BalanceRevision = page.Snapshot.BalanceRevision,
                        Definitions = page.Snapshot.Definitions.Where(definition => (int)definition.Category <= (int)ComponentCategory.Turn).ToArray() }
                } };
            }
            if (field == "reward") data.Rewards = new[] { new RunRewardReceiptData {
                Version = 999, RunId = Guid.NewGuid().ToString("N"), Waves = 1, VictoryBonus = false } };
            if (field == "operation") data.Operations = new[] { new OperationReceiptData {
                Version = 999, OperationId = Guid.NewGuid().ToString("N"), Type = "save-entry", ResultId = data.Entries[1].Id } };
            if (field == "album") data.Album[1].ContentMetadataVersion = 999;
            string malformed = JsonUtility.ToJson(data, true);
            Assert.That(JsonUtility.FromJson<WorkshopProfileData>(malformed).Entries[0].Version, Is.Zero);
            File.WriteAllText(path, malformed, new UnicodeEncoding(false, true));
            File.WriteAllText(path + ".bak", valid);
            byte[] primary = File.ReadAllBytes(path), backup = File.ReadAllBytes(path + ".bak");
            var store = Store();

            var loaded = store.Load();
            Assert.That(loaded.CanWrite, Is.False, "An earlier malformed field cannot make later future metadata eligible for backup rollback.");
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.RecoveryRequired));
            Assert.That(loaded.Message, Is.Not.Empty);
            Assert.That(store.Save(profile).Success, Is.False);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(path + ".bak"), Is.EqualTo(backup));
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(2));
        }

        [Test] public void MissingLibraryEntryStillUsesThePagesOwnSnapshotAfterRestart()
        {
            var data = WorkshopProfile.CreateNew(catalog).ToData();
            data.Entries[0].LibraryReferences = new[] { new ComponentLibraryReferenceData
                { Id = "library.deleted.foundation", Revision = 1, Kind = ComponentLibraryKind.Foundation, SlotIndex = -1 } };
            data.Library = Array.Empty<ComponentLibraryEntryData>();
            string savedPage = JsonUtility.ToJson(data.Entries[0]);
            var original = TowerCatalog.ResolveEntry(catalog.Starters[0], GameBalance.CreateDefault());
            Assert.That(original.Valid, Is.True);
            File.WriteAllText(path, JsonUtility.ToJson(data, true));

            var store = Store();
            var loaded = store.Load();
            Assert.That(loaded.CanWrite, Is.True, loaded.Message);
            var resolved = TowerCatalog.ResolveEntry(loaded.Profile.Entries[0], GameBalance.CreateDefault());
            Assert.That(resolved.Valid, Is.True, string.Join(",", resolved.Diagnostics.Select(issue => issue.Code)));
            Assert.That(resolved.Value.Cost, Is.EqualTo(original.Value.Cost));
            Assert.That(resolved.Value.Health, Is.EqualTo(original.Value.Health));
            Assert.That(JsonUtility.ToJson(loaded.Profile.ToData().Entries[0]), Is.EqualTo(savedPage));
            Assert.That(store.Save(loaded.Profile).Success, Is.True, store.LastDiagnostic);
            Assert.That(JsonUtility.ToJson(Store().Load().Profile.ToData().Entries[0]), Is.EqualTo(savedPage));
        }

        [Test] public void MigrationPendingOnPrimaryIsUnsavedAndQuitCommitsWithoutAnotherEdit()
        {
            var profile = new WorkshopProfile();
            profile.ConfigureContent(LegacyContentCatalog.Create(GameBalancePresets.CreateTargetWorkshop()));
            var store = new CountingStore(new ProfileLoadResult(profile, ProfileLoadSource.Primary, "", true, true));
            var root = new GameObject("Isolated migration pending profile");
            root.SetActive(false);
            try
            {
                var controller = root.AddComponent<RunController>();
                controller.AutomaticSimulation = false;
                controller.InitializeWorkshop(store, allowLegacyFixtureDefaults: true);
                Assert.That(controller.Workshop.SaveState, Is.EqualTo(ProfileSaveState.Unsaved));
                Assert.That(controller.Workshop.HasUnsavedChanges, Is.True);
                root.SetActive(true);
                root.SendMessage("OnApplicationQuit", SendMessageOptions.RequireReceiver);
                Assert.That(store.SaveCalls, Is.EqualTo(1));
                Assert.That(controller.Workshop.SaveState, Is.EqualTo(ProfileSaveState.Saved));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false)] [TestCase(true)]
        public void FreshConfiguredControllerOwnsAndSelectsAllEightPagesIncludingFourStructures(bool memoryOnly)
        {
            var balance = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(
                "Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            Assert.That(balance, Is.Not.Null, "Use the real player configuration and its referenced assembly catalog.");
            var store = new CountingStore(new ProfileLoadResult(new WorkshopProfile(), ProfileLoadSource.NewProfile, "", true));
            var root = new GameObject("Isolated fresh configured assembly profile");
            root.SetActive(false);
            try
            {
                var controller = root.AddComponent<RunController>();
                controller.AutomaticSimulation = false;
                controller.balanceAsset = balance;
                if (memoryOnly) controller.InitializeWorkshop(persistent: false);
                else controller.InitializeWorkshop(store);

                Assert.That(controller.InWorkshop, Is.True);
                Assert.That(controller.Workshop.Entries, Has.Count.EqualTo(8));
                Assert.That(controller.Workshop.Entries.Count(entry => entry.Kind == AlbumEntryKind.Structure), Is.EqualTo(4));
                Assert.That(controller.Workshop.LoadoutEntries, Has.Count.EqualTo(8));
                Assert.That(controller.Workshop.LoadoutEntries, Is.All.Not.Null);
                Assert.That(controller.Workshop.LoadoutEntries.Select(entry => entry.Id),
                    Is.EquivalentTo(controller.Workshop.Entries.Select(entry => entry.Id)));
                Assert.That(controller.Workshop.LoadoutEntries.Select(entry => entry.Id).Distinct().Count(), Is.EqualTo(8));
                Assert.That(controller.Workshop.SaveState, Is.EqualTo(memoryOnly ? ProfileSaveState.MemoryOnly : ProfileSaveState.Unsaved));
                Assert.That(store.LoadCalls, Is.EqualTo(memoryOnly ? 0 : 1));
                Assert.That(store.SaveCalls, Is.Zero, "Creating the initial candidate does not write a profile during initialization.");
                Assert.That(File.Exists(path), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        sealed class CountingStore : IWorkshopProfileStore
        {
            readonly ProfileLoadResult loaded;
            internal int LoadCalls, SaveCalls;
            internal CountingStore(ProfileLoadResult loaded) { this.loaded = loaded; }
            public ProfileLoadResult Load() { LoadCalls++; return loaded; }
            public ProfileSaveResult Save(WorkshopProfile profile) { SaveCalls++; return new ProfileSaveResult(true, ""); }
        }

        sealed class FaultIO : IWorkshopProfileIO
        {
            public string Failure;
            readonly WorkshopProfileFileIO files = new WorkshopProfileFileIO();
            public string ReadAllText(string file) => files.ReadAllText(file);
            public void CreateDirectory(string folder) => files.CreateDirectory(folder);
            public void WriteAllText(string file, string contents) => files.WriteAllText(file, contents);
            public void Move(string source, string target)
            {
                if (Failure == "original-move" && target.Contains(".v7-original")) throw new IOException("Injected original backup commit failure");
                files.Move(source, target);
            }
            public void Copy(string source, string target)
            {
                if (Failure == "copy") throw new IOException("Injected original copy failure");
                files.Copy(source, target);
            }
            public void Replace(string source, string target, string backup)
            {
                if (Failure == "replace") throw new IOException("Injected interruption after historical backup");
                files.Replace(source, target, backup);
            }
            public void Delete(string file) => files.Delete(file);
        }
    }
}
#endif
