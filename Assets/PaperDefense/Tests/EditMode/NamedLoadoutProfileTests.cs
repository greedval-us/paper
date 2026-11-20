using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class NamedLoadoutProfileTests
    {
        static Type RequiredType(string name = "NamedLoadout")
        {
            var type = typeof(WorkshopProfile).Assembly.GetType("PaperDefense.Core." + name);
            Assert.That(type, Is.Not.Null, "Missing named-loadout profile API: " + name);
            return type;
        }
        static WorkshopProfile Profile()
        { RequiredType(); return WorkshopProfile.CreateNew(AssemblyResolverFixture.Catalog()); }
        static string Id() => Guid.NewGuid().ToString("N");
        static object Field(object target, string name)
        {
            var field = target.GetType().GetField(name);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
            return field.GetValue(target);
        }
        static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
            field.SetValue(target, value);
        }
        static T Value<T>(object target, string name)
        {
            var property = target.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, target.GetType().Name + "." + name);
            return (T)property.GetValue(target);
        }
        static object[] Sets(WorkshopProfile profile) => Value<IEnumerable>(profile, "NamedLoadouts").Cast<object>().ToArray();
        static string[] Slots(object set) => Value<IEnumerable>(set, "Slots").Cast<string>().ToArray();
        static bool Command(WorkshopProfile profile, string method, params object[] parameters)
        {
            var command = typeof(WorkshopProfile).GetMethod(method);
            Assert.That(command, Is.Not.Null, "Missing named-loadout command: " + method);
            var args = parameters.Concat(new object[] { null }).ToArray();
            return (bool)command.Invoke(profile, args);
        }
        static void Save(WorkshopProfile profile, string id, string name = "Против брони")
            => Assert.That(Command(profile, "TrySaveNamedLoadout", id, name), Is.True);
        static WorkshopProfile RoundTrip(WorkshopProfile profile)
            => WorkshopProfile.FromData(JsonUtility.FromJson<WorkshopProfileData>(JsonUtility.ToJson(profile.ToData())), AssemblyResolverFixture.Catalog());
        static void Clear(WorkshopProfile profile)
        { for (int i = 0; i < 8; i++) Assert.That(profile.TryAssignAssemblySlot(i, null, out _), Is.True); }

        [Test]
        public void SaveCapturesEightReferencesAndApplyingReplacesEverySlotIncludingEmptyOnes()
        {
            var profile = Profile(); var original = profile.Loadout.ToArray(); string id = Id();
            profile.TryAssignAssemblySlot(2, null, out _); original[2] = null;
            Save(profile, id, "  Против брони  ");
            var saved = Sets(profile).Single();
            Assert.That(Value<string>(saved, "Name"), Is.EqualTo("Против брони"));
            Assert.That(Slots(saved), Is.EqualTo(original));
            Clear(profile); profile.TryAssignAssemblySlot(2, original[0], out _);
            Assert.That(Slots(saved), Is.EqualTo(original), "Saved slots own their array.");
            Assert.That(Command(profile, "TryApplyNamedLoadout", id), Is.True);
            Assert.That(profile.Loadout, Is.EqualTo(original));
            Assert.That(profile.Entries.Count, Is.EqualTo(8), "Saving a named set must not copy album pages.");
        }

        [Test]
        public void EmptyAndIncompleteSetsAreAllowedButRetainEightPositions()
        {
            var profile = Profile(); Clear(profile); string id = Id(); Save(profile, id);
            Assert.That(Slots(Sets(profile).Single()), Is.EqualTo(new string[8]));
            profile.TryAssignAssemblySlot(7, profile.Entries[0].Id, out _);
            Assert.That(Command(profile, "TryUpdateNamedLoadout", id), Is.True);
            Assert.That(Slots(Sets(profile).Single()).Take(7), Is.All.Null);
            Assert.That(Slots(Sets(profile).Single())[7], Is.EqualTo(profile.Entries[0].Id));
        }

        [Test]
        public void EditingAnAlbumEntryKeepsReferencesAndFrozenRunSnapshotsRemainUnchanged()
        {
            var profile = Profile(); string id = Id(); Save(profile, id);
            var frozen = new RunLoadoutSnapshot(profile.GetLoadoutEntries(), GameBalance.CreateDefault());
            var original = profile.Entries[0];
            var edited = new AlbumEntry(original.Id, "Новая версия", original.Revision, original.Snapshot);
            Assert.That(profile.TrySaveAssemblyEntry(edited, false, Guid.NewGuid(), out var saved, out _), Is.True);
            Assert.That(Command(profile, "TryApplyNamedLoadout", id), Is.True);
            Assert.That(profile.GetLoadoutEntries()[0], Is.SameAs(saved));
            Assert.That(frozen.Entries[0], Is.SameAs(original));
            Assert.That(Slots(Sets(profile).Single())[0], Is.EqualTo(original.Id));
        }

        [Test]
        public void RenameUpdateAndDeleteAreExplicitAndDoNotModifyCurrentSlots()
        {
            var profile = Profile(); string id = Id(); Save(profile, id);
            var current = profile.Loadout.ToArray();
            Assert.That(Command(profile, "TryRenameNamedLoadout", id, "  Мосты  "), Is.True);
            Assert.That(Value<string>(Sets(profile).Single(), "Name"), Is.EqualTo("Мосты"));
            Assert.That(Command(profile, "TryDeleteNamedLoadout", id), Is.True);
            Assert.That(Sets(profile), Is.Empty); Assert.That(profile.Loadout, Is.EqualTo(current));
            string before = JsonUtility.ToJson(profile.ToData());
            foreach (string command in new[] { "TryApplyNamedLoadout", "TryUpdateNamedLoadout", "TryDeleteNamedLoadout" })
                Assert.That(Command(profile, command, id), Is.False);
            Assert.That(JsonUtility.ToJson(profile.ToData()), Is.EqualTo(before));
        }

        [TestCase("")] [TestCase("   ")] [TestCase("Имя\nстрока")] [TestCase("<b>Набор</b>")] [TestCase("Набор>")]
        public void InvalidNamesCannotMutateTheCandidate(string name)
        {
            var profile = Profile(); string before = JsonUtility.ToJson(profile.ToData());
            Assert.That(Command(profile, "TrySaveNamedLoadout", Id(), name), Is.False);
            Assert.That(JsonUtility.ToJson(profile.ToData()), Is.EqualTo(before));
        }

        [Test]
        public void NameAndCollectionLimitsAndCaseInsensitiveUniquenessAreEnforced()
        {
            var profile = Profile(); string first = Id(); Save(profile, first, "Мосты");
            Assert.That(Command(profile, "TrySaveNamedLoadout", Id(), "  МОСТЫ  "), Is.False);
            Assert.That(Command(profile, "TrySaveNamedLoadout", Id(), new string('я', 49)), Is.False);
            for (int i = 1; i < 12; i++) Save(profile, Id(), "Набор " + i);
            Assert.That(Command(profile, "TrySaveNamedLoadout", Id(), "Тринадцатый"), Is.False);
            Assert.That(Command(profile, "TryRenameNamedLoadout", first, new string('я', 48)), Is.True);
            Assert.That(Sets(profile).Length, Is.EqualTo(12));
        }

        [TestCase("not-guid")] [TestCase("00000000000000000000000000000000")]
        public void InvalidIdentityAndDuplicateIdentityNeverOverwriteASet(string invalid)
        {
            var profile = Profile(); string id = Id(); Save(profile, id);
            string before = JsonUtility.ToJson(profile.ToData());
            Assert.That(Command(profile, "TrySaveNamedLoadout", invalid, "Другой"), Is.False);
            Assert.That(Command(profile, "TrySaveNamedLoadout", id, "Другой"), Is.False);
            Assert.That(JsonUtility.ToJson(profile.ToData()), Is.EqualTo(before));
        }

        [Test]
        public void DeletingAnAssemblyPageClearsReferencesInEverySetWithoutCompacting()
        {
            var profile = Profile(); string entryId = profile.Loadout[1];
            Save(profile, Id(), "Первый"); Save(profile, Id(), "Второй");
            var before = profile.Loadout.ToArray();
            Assert.That(profile.TryDeleteAssemblyEntry(entryId, out _), Is.True);
            before[1] = null;
            Assert.That(profile.Loadout, Is.EqualTo(before));
            foreach (var set in Sets(profile)) Assert.That(Slots(set), Is.EqualTo(before));
            Assert.That(Sets(RoundTrip(profile)).Length, Is.EqualTo(2));
        }

        [Test]
        public void DeletingAnUnassignedLegacyPageAlsoClearsSavedReferences()
        {
            RequiredType(); var profile = new WorkshopProfile(); string entryId = profile.Loadout[0];
            string id = Id(); Save(profile, id);
            Assert.That(profile.TryAssignAssemblySlot(0, null, out _), Is.True);
            Assert.That(profile.RemoveBlueprint(entryId, out _), Is.True);
            Assert.That(Slots(Sets(profile).Single())[0], Is.Null);
        }

        [Test]
        public void RoundTripAndWorkingCopiesOwnTheirNamedSetArrays()
        {
            var profile = Profile(); string id = Id(); Save(profile, id);
            var copy = profile.CreateWorkingCopy(); var original = Slots(Sets(profile).Single());
            Clear(copy); Assert.That(Command(copy, "TryUpdateNamedLoadout", id), Is.True);
            Assert.That(Slots(Sets(profile).Single()), Is.EqualTo(original));
            var data = profile.ToData(); var dto = ((IEnumerable)Field(data, "NamedLoadouts")).Cast<object>().Single();
            ((string[])Field(dto, "Slots"))[0] = null;
            Assert.That(Slots(Sets(profile).Single()), Is.EqualTo(original));
            Assert.That(JsonUtility.ToJson(RoundTrip(profile).ToData()), Is.EqualTo(JsonUtility.ToJson(profile.ToData())));
        }

        [TestCase(8)] [TestCase(9)]
        public void HistoricalEightSlotProfilesGainNoSetsAndKeepEveryCurrentSlot(int version)
        {
            var profile = Profile(); profile.GrantGold(137); profile.TryAssignAssemblySlot(6, null, out _);
            var data = profile.ToData(); data.Version = version; Set(data, "NamedLoadouts", null);
            var before = JsonUtility.ToJson(data); var slots = data.Loadout.ToArray();
            var restored = WorkshopProfile.FromData(data, AssemblyResolverFixture.Catalog());
            Assert.That(restored.ToData().Version, Is.EqualTo(WorkshopProfile.CurrentVersion));
            Assert.That(restored.Loadout, Is.EqualTo(slots)); Assert.That(restored.Gold, Is.EqualTo(137));
            Assert.That(Sets(restored), Is.Empty); Assert.That(JsonUtility.ToJson(data), Is.EqualTo(before));
        }

        [TestCase("unknown")] [TestCase("duplicate-slot")] [TestCase("short")] [TestCase("null-slots")]
        [TestCase("duplicate-id")] [TestCase("duplicate-name")] [TestCase("null-item")]
        public void InvalidSavedSetsAreRejectedInsteadOfSilentlyChangingThePlayersSelection(string defect)
        {
            var profile = Profile(); Save(profile, Id()); var data = profile.ToData();
            var sets = (Array)Field(data, "NamedLoadouts"); var dto = sets.GetValue(0);
            var slots = (string[])Field(dto, "Slots");
            if (defect == "unknown") slots[0] = "page.missing";
            if (defect == "duplicate-slot") slots[1] = slots[0];
            if (defect == "short") Set(dto, "Slots", new string[7]);
            if (defect == "null-slots") Set(dto, "Slots", null);
            if (defect == "null-item") sets.SetValue(null, 0);
            if (defect.StartsWith("duplicate-", StringComparison.Ordinal) && defect != "duplicate-slot")
            {
                var copy = JsonUtility.FromJson(JsonUtility.ToJson(dto), RequiredType("NamedLoadoutData"));
                Set(copy, "Id", defect == "duplicate-id" ? Field(dto, "Id") : Id());
                Set(copy, "Name", defect == "duplicate-name" ? "ПРОТИВ БРОНИ" : "Другой");
                var pair = Array.CreateInstance(RequiredType("NamedLoadoutData"), 2); pair.SetValue(dto, 0); pair.SetValue(copy, 1);
                Set(data, "NamedLoadouts", pair);
            }
            Assert.Throws<ArgumentException>(() => WorkshopProfile.FromData(data, AssemblyResolverFixture.Catalog()));
        }

        [Test]
        public void FutureNamedSetMetadataProtectsTheSourceEvenWithAMalformedSibling()
        {
            var profile = Profile(); Save(profile, Id()); var data = profile.ToData();
            var dto = ((Array)Field(data, "NamedLoadouts")).GetValue(0); Set(dto, "Version", 2);
            data.Entries[0] = null;
            Assert.Throws<NotSupportedException>(() => WorkshopProfile.FromData(data, AssemblyResolverFixture.Catalog()));
        }
    }
}
