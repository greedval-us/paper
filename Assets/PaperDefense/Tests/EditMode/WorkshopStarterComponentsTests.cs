using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class WorkshopStarterComponentsTests
    {
        static readonly string[] StarterIds =
        {
            "library.foundation.standard", "library.weapon.gun", "library.weapon.piercer", "library.weapon.mortar"
        };

        static ComponentCatalog Catalog() => AssemblyResolverFixture.Catalog();

        static ComponentLibraryEntry Foundation(string id, int revision, string name)
        {
            var source = Catalog().Starters.First(e => e.Snapshot?.Recipe.Tower?.Foundation.Frame.Id == "frame.standard").Snapshot;
            var foundation = source.Recipe.Tower.Foundation;
            var selected = foundation.Parts.Select(p => (p.Id, p.Revision)).ToArray();
            return new ComponentLibraryEntry(id, name, revision, new ComponentRecipeSnapshot(foundation,
                source.BalanceRevision, source.Definitions.Where(d => selected.Contains((d.Id, d.Revision)))));
        }

        [Test]
        public void NewProfileIncludesReusableStandardFoundationAndThreeWeapons()
        {
            var profile = WorkshopProfile.CreateNew(Catalog());

            Assert.That(profile.Library.Select(e => e.Id), Is.EquivalentTo(StarterIds));
            Assert.That(profile.Library.Count(e => e.Kind == ComponentLibraryKind.Foundation), Is.EqualTo(1));
            Assert.That(profile.Library.Where(e => e.Kind == ComponentLibraryKind.Weapon)
                .Select(e => e.Snapshot.Weapon.Mechanism.Id), Is.EquivalentTo(new[] { "core.gun", "core.piercer", "core.mortar" }));
            Assert.That(profile.Library.Single(e => e.Kind == ComponentLibraryKind.Foundation)
                .Snapshot.Foundation.Parts.Select(p => p.Id), Is.EqualTo(new[]
                { "frame.standard", "hull.standard", "armor.none", "feed.standard", "turn.standard" }));
            foreach (var item in profile.Library)
            {
                var parts = item.Kind == ComponentLibraryKind.Foundation ? item.Snapshot.Foundation.Parts : item.Snapshot.Weapon.Parts;
                Assert.That(parts.All(p => profile.IsUnlocked(p.Id)), Is.True, item.Id + " must be usable without buying parts");
                Assert.That(item.Snapshot.Definitions.Select(d => d.Id), Is.EquivalentTo(parts.Select(p => p.Id)));
            }
            Assert.That(profile.Entries.Count, Is.EqualTo(8));
            Assert.That(profile.Loadout, Is.EqualTo(profile.Entries.Select(e => e.Id)));
            Assert.That(profile.Gold, Is.Zero);
            Assert.That(profile.TotalXp, Is.Zero);
        }

        [Test]
        public void ExistingProfileAddsMissingStartersOnceAndPreservesCustomAndRevisedComponents()
        {
            var catalog = Catalog();
            var source = WorkshopProfile.CreateNew(catalog).ToData();
            source.Gold = 713; source.TotalXp = 489;
            source.Unlocked = new[] { "old.custom.right", "frame.heavy" };
            source.Library = new[]
            {
                new ComponentLibraryEntryData(Foundation("my.foundation", 3, "My foundation")),
                new ComponentLibraryEntryData(Foundation("library.foundation.standard", 7, "My revised standard"))
            };
            string before = JsonUtility.ToJson(source);

            var restored = WorkshopProfile.FromData(source, catalog);

            Assert.That(restored.Library.Count, Is.EqualTo(5));
            Assert.That(restored.Library.Single(e => e.Id == "my.foundation").Revision, Is.EqualTo(3));
            var revised = restored.Library.Single(e => e.Id == "library.foundation.standard");
            Assert.That(revised.Revision, Is.EqualTo(7));
            Assert.That(revised.Name, Is.EqualTo("My revised standard"));
            Assert.That(restored.ToData().Library.Take(2).Select(JsonUtility.ToJson),
                Is.EqualTo(source.Library.Select(JsonUtility.ToJson)));
            Assert.That(restored.Gold, Is.EqualTo(713));
            Assert.That(restored.TotalXp, Is.EqualTo(489));
            Assert.That(restored.IsUnlocked("old.custom.right"), Is.True);
            Assert.That(restored.IsUnlocked("frame.heavy"), Is.True);
            Assert.That(restored.Library.Where(e => e.Kind == ComponentLibraryKind.Weapon)
                .SelectMany(e => e.Snapshot.Weapon.Parts).All(p => restored.IsUnlocked(p.Id)), Is.True);
            Assert.That(restored.Loadout, Is.EqualTo(source.Loadout));
            Assert.That(restored.Entries.Select(e => e.Id), Is.EqualTo(source.Entries.Select(e => e.Id)));
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(before));

            string saved = JsonUtility.ToJson(restored.ToData());
            var reloaded = WorkshopProfile.FromData(JsonUtility.FromJson<WorkshopProfileData>(saved), catalog);
            Assert.That(JsonUtility.ToJson(reloaded.ToData()), Is.EqualTo(saved));
        }

        [Test]
        public void HistoricalProfileGetsReusableStartersWithoutDisplacingPagesOrRights()
        {
            var old = new WorkshopProfile(); old.GrantGold(415);
            Assert.That(old.Unlock("chassis.heavy", out _), Is.True);
            var source = old.ToData(); source.Version = 7;
            source.Loadout = source.Loadout.Take(4).ToArray();
            int gold = source.Gold;

            var migration = ProfileMigrationV8.Migrate(source, Catalog());

            Assert.That(migration.Success, Is.True);
            Assert.That(migration.Candidate.Library.Select(e => e.Id), Is.EquivalentTo(StarterIds));
            Assert.That(migration.Candidate.Gold, Is.EqualTo(gold));
            Assert.That(migration.Candidate.IsUnlocked("chassis.heavy"), Is.True);
            Assert.That(migration.Candidate.Entries.Select(e => e.Id), Is.EqualTo(source.Album.Select(e => e.Id)));
            Assert.That(migration.Candidate.Loadout.Take(4), Is.EqualTo(source.Loadout));
            Assert.That(migration.Candidate.Loadout.Skip(4), Is.All.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InvalidOrFutureProfilesCannotBeRepairedByStarterInstallation(bool future)
        {
            var source = WorkshopProfile.CreateNew(Catalog()).ToData();
            source.Library = Array.Empty<ComponentLibraryEntryData>();
            if (future) source.Version = WorkshopProfile.CurrentVersion + 1;
            else source.Gold = -1;
            string before = JsonUtility.ToJson(source);

            var migration = ProfileMigrationV8.Migrate(source, Catalog());

            Assert.That(migration.Success, Is.False);
            Assert.That(migration.Candidate, Is.Null);
            Assert.That(migration.IsUnsupported, Is.EqualTo(future));
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(before));
        }
    }
}
