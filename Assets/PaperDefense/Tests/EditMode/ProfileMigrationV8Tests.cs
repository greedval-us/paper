using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class ProfileMigrationV8Tests
    {
        static ComponentCatalog Catalog() => AssemblyResolverFixture.Catalog();
        static WorkshopProfileData Historical(int version = 7)
        {
            var profile = new WorkshopProfile(); profile.GrantGold(471);
            Assert.That(profile.Unlock("chassis.heavy", out _), Is.True);
            var data = profile.ToData(); data.Version = version;
            data.Loadout = data.Loadout.Take(4).ToArray();
            if (version <= 3)
                foreach (var p in data.Album)
                {
                    p.Drawing.FormatVersion = 1; p.Drawing.BindingVersion = 1;
                    p.Drawing.Parts = p.Drawing.Parts.Where(x => (int)x.Key <= 6).ToArray();
                }
            return data;
        }

        [Test] public void V7_PreservesFourSlotsAndAddsFourEmpty()
        {
            var source = Historical(); string before = JsonUtility.ToJson(source);
            var result = ProfileMigrationV8.Migrate(source, Catalog());
            Assert.That(result.Success, Is.True);
            var candidate = result.Candidate;
            Assert.That(candidate.Loadout.Count, Is.EqualTo(8));
            Assert.That(candidate.Loadout.Take(4), Is.EqualTo(source.Loadout));
            Assert.That(candidate.Loadout.Skip(4), Is.All.Null);
            Assert.That(candidate.Gold, Is.EqualTo(source.Gold));
            Assert.That(candidate.TotalXp, Is.Zero);
            Assert.That(candidate.IsUnlocked("chassis.heavy"), Is.True);
            Assert.That(candidate.Entries.Select(e => e.Id), Is.EqualTo(source.Album.Select(p => p.Id)));
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(before));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void HistoricalReadersUseExactlyFourSlots(int version)
        {
            var result = ProfileMigrationV8.Migrate(Historical(version), Catalog());
            Assert.That(result.Success, Is.True, string.Join(",", result.Diagnostics.Select(d => d.Code)));
            Assert.That(result.Candidate.Loadout.Skip(4), Is.All.Null);
            Assert.That(result.Candidate.ToData().Version, Is.EqualTo(8));
            var malformed = Historical(version); malformed.Loadout = new string[8];
            if (version >= 2) Assert.That(ProfileMigrationV8.Migrate(malformed, Catalog()).Success, Is.False);
        }

        [Test] public void NewInstallationGetsEightOwnedPagesAndEightAssignments()
        {
            var fresh = WorkshopProfile.CreateNew(Catalog());
            Assert.That(fresh.Entries.Count, Is.EqualTo(8));
            Assert.That(fresh.Loadout, Is.EqualTo(fresh.Entries.Select(e => e.Id)));
            Assert.That(fresh.Entries.Count(e => e.Kind == AlbumEntryKind.Structure), Is.EqualTo(4));
            Assert.That(fresh.Gold, Is.Zero); Assert.That(fresh.TotalXp, Is.Zero);
            Assert.That(fresh.Library, Is.Empty);
        }

        [Test] public void LegacySaveCannotReplaceAnOwnedAssemblySnapshot()
        {
            var fresh = WorkshopProfile.CreateNew(Catalog());
            string before = JsonUtility.ToJson(fresh.ToData());
            var projection = fresh.Album[0];
            Assert.That(fresh.SaveBlueprint(projection, out var reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(JsonUtility.ToJson(fresh.ToData()), Is.EqualTo(before));
            Assert.That(fresh.Entries[0].Kind, Is.EqualTo(AlbumEntryKind.Tower));
            Assert.That(fresh.Entries[0].Snapshot, Is.Not.Null);
        }

        [TestCase(1)] [TestCase(7)] public void SixtyFourPagesArePreservedAndFreeImportWaitsForSpace(int version)
        {
            var source = Historical(version);
            source.Album = Enumerable.Range(0,64).Select(i => new BlueprintData(new TowerBlueprint("page." + i, "Page " + i, ChassisKind.Standard, StructureKind.Gun))).ToArray();
            source.Loadout = source.Album.Take(4).Select(p => p.Id).ToArray();
            var migration = ProfileMigrationV8.Migrate(source, Catalog());
            Assert.That(migration.Success, Is.True);
            var candidate = migration.Candidate;
            Assert.That(candidate.Entries.Select(e => e.Id), Is.EqualTo(source.Album.Select(p => p.Id)));
            string freeId = Catalog().Starters.First(e => e.Kind == AlbumEntryKind.Structure).Id;
            Assert.That(candidate.ImportStarter(freeId, Catalog(), out _), Is.False);
            Assert.That(candidate.Entries.Count, Is.EqualTo(64));
            Assert.That(candidate.RemoveBlueprint("page.63", out _), Is.True);
            Assert.That(candidate.ImportStarter(freeId, Catalog(), out _), Is.True);
            Assert.That(candidate.Entries.Count, Is.EqualTo(64));
            Assert.That(candidate.Entries.Last().Id, Is.EqualTo(freeId));
        }

        [Test] public void HistoricalStandardWithDifferentNumbersRemainsLegacy()
        {
            var candidate = ProfileMigrationV8.Migrate(Historical(), Catalog(), GameBalance.CreateDefault()).Candidate;
            Assert.That(candidate.Entries, Is.All.Matches<AlbumEntry>(e => e.Kind == AlbumEntryKind.Legacy));
            Assert.That(candidate.Entries[0].LegacyBlueprint.FirstWeapon, Is.EqualTo(StructureKind.Gun));
        }

        [Test] public void UnknownFutureVersionCannotProduceDefaultCandidate()
        {
            var source = Historical(); source.Version = 99;
            var result = ProfileMigrationV8.Migrate(source, Catalog());
            Assert.That(result.Success, Is.False); Assert.That(result.Candidate, Is.Null);
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("profile.version.unsupported"));
        }

        [Test] public void V8RoundTripAndWorkingCopyPreserveOwnedEntriesXpAndReceipts()
        {
            var fresh = WorkshopProfile.CreateNew(Catalog()); var data = fresh.ToData(); data.TotalXp = 389;
            data.Rewards = new[] { new RunRewardReceiptData { RunId = Guid.NewGuid().ToString("N"), Waves = 8, VictoryBonus = true } };
            data.Operations = new[] { new OperationReceiptData { OperationId = Guid.NewGuid().ToString("N"), Type = "SaveEntry", ResultId = fresh.Entries[0].Id } };
            var restored = WorkshopProfile.FromData(JsonUtility.FromJson<WorkshopProfileData>(JsonUtility.ToJson(data)), Catalog());
            Assert.That(restored.TotalXp, Is.EqualTo(389));
            Assert.That(JsonUtility.ToJson(restored.CreateWorkingCopy().ToData()), Is.EqualTo(JsonUtility.ToJson(restored.ToData())));
            restored.CreateWorkingCopy().GrantGold(100);
            Assert.That(restored.Gold, Is.Zero);
            Assert.That(restored.ToData().Rewards[0].Waves, Is.EqualTo(8));
        }

        [Test] public void MissingEmbeddedRevisionAndInvalidReceiptGuidAreRejected()
        {
            var data = WorkshopProfile.CreateNew(Catalog()).ToData();
            data.Entries[0].Snapshot.Recipe.Mounts[0].Weapon.Mechanism.Revision = 99;
            Assert.Throws<NotSupportedException>(() => WorkshopProfile.FromData(data, Catalog()));
            data = WorkshopProfile.CreateNew(Catalog()).ToData();
            data.Operations = new[] { new OperationReceiptData { OperationId = Guid.NewGuid().ToString("D"), Type = "SaveEntry", ResultId = "saved" } };
            Assert.Throws<ArgumentException>(() => WorkshopProfile.FromData(data, Catalog()));
        }
    }
}
