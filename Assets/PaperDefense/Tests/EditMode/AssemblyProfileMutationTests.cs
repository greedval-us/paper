using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AssemblyProfileMutationTests
    {
        static ComponentCatalog Catalog() => AssemblyResolverFixture.Catalog();
        static WorkshopProfile Profile(int gold = 0, int xp = 0)
        {
            var catalog = Catalog(); var data = WorkshopProfile.CreateNew(catalog).ToData();
            data.Gold = gold; data.TotalXp = xp;
            return WorkshopProfile.FromData(data, catalog);
        }
        static AlbumEntry Page(string id, int revision = 1, string name = "Saved tower")
            => new AlbumEntry(id, name, revision, Catalog().Starters[0].Snapshot);
        static ComponentLibraryEntry Component(string id, int revision = 1, string name = "Saved foundation")
        {
            var snapshot = Catalog().Starters[0].Snapshot; var foundation = snapshot.Recipe.Tower.Foundation;
            var ids = foundation.Parts.Select(p => (p.Id, p.Revision)).ToArray();
            return new ComponentLibraryEntry(id, name, revision,
                new ComponentRecipeSnapshot(foundation, snapshot.BalanceRevision, snapshot.Definitions.Where(d => ids.Contains((d.Id, d.Revision)))));
        }

        [Test] public void Purchase_RequiresLevelThenGoldAndDebitsOnce()
        {
            Catalog().TryGet("frame.heavy", 1, out var heavy);
            var underLevel = Profile(100, 0);
            Assert.That(underLevel.TryPurchaseAssemblyPart(heavy, out var code), Is.False);
            Assert.That(code, Is.EqualTo("profile.part.level")); Assert.That(underLevel.Gold, Is.EqualTo(100));
            var poor = Profile(89, 230);
            Assert.That(poor.TryPurchaseAssemblyPart(heavy, out code), Is.False);
            Assert.That(code, Is.EqualTo("profile.part.gold")); Assert.That(poor.IsUnlocked(heavy.Id), Is.False);
            var profile = Profile(100, 230);
            Assert.That(profile.TryPurchaseAssemblyPart(heavy, out code), Is.True, code);
            Assert.That(profile.Gold, Is.EqualTo(10)); Assert.That(profile.IsUnlocked(heavy.Id), Is.True);
            Assert.That(profile.TryPurchaseAssemblyPart(heavy, out code), Is.True, code);
            Assert.That(profile.Gold, Is.EqualTo(10));
        }

        [Test] public void PreviouslyOwnedPart_BypassesNewLevelAndPrice()
        {
            Catalog().TryGet("frame.heavy", 1, out var heavy);
            var source = Profile().ToData(); source.Unlocked = source.Unlocked.Concat(new[] { heavy.Id }).ToArray();
            var profile = WorkshopProfile.FromData(source, Catalog());
            Assert.That(profile.TryPurchaseAssemblyPart(heavy, out var code), Is.True, code);
            Assert.That(profile.Gold, Is.Zero);
        }

        [Test] public void SavingEntry_NormalizesNewRevisionAndRebuildsLegacyProjection()
        {
            var profile = Profile(); var input = Page("page.saved", 12);
            Assert.That(profile.TrySaveAssemblyEntry(input, false, Guid.NewGuid(), out var saved, out var code), Is.True, code);
            Assert.That(saved.Revision, Is.EqualTo(1)); Assert.That(input.Revision, Is.EqualTo(12));
            Assert.That(profile.Entries.Single(e => e.Id == input.Id), Is.SameAs(saved));
            Assert.That(profile.Album.Single(e => e.Id == input.Id).FirstWeaponId, Is.EqualTo("weapon.gun"));
            Assert.That(profile.Gold, Is.Zero);
        }

        [TestCase("length")] [TestCase("trim")] [TestCase("control")]
        public void SavingEntry_RejectsInvalidStableIdentityBeforeChangingCandidate(string malformed)
        {
            string id = malformed == "length" ? new string('a', 65) : malformed == "trim" ? " page.saved " : "page.\u0001saved";
            var profile = Profile(); string before = JsonUtility.ToJson(profile.ToData());
            Assert.That(profile.TrySaveAssemblyEntry(Page(id), false, Guid.NewGuid(), out var saved, out var code), Is.False);
            Assert.That(code, Is.EqualTo("profile.entry.identity")); Assert.That(saved, Is.Null);
            Assert.That(JsonUtility.ToJson(profile.ToData()), Is.EqualTo(before), "Rejected identity must leave the entire candidate unchanged.");
        }

        [Test] public void EditingEntry_UsesOptimisticRevisionAndLeavesOldSnapshotImmutable()
        {
            var profile = Profile(); var original = profile.Entries[0];
            var edit = new AlbumEntry(original.Id, "Renamed tower", original.Revision, original.Snapshot);
            Assert.That(profile.TrySaveAssemblyEntry(edit, false, Guid.NewGuid(), out var saved, out var code), Is.True, code);
            Assert.That(saved.Revision, Is.EqualTo(2)); Assert.That(original.Revision, Is.EqualTo(1));
            Assert.That(original.Name, Is.Not.EqualTo(saved.Name));
            Assert.That(profile.Album.Single(e => e.Id == original.Id).Name, Is.EqualTo("Renamed tower"));
            Assert.That(profile.TrySaveAssemblyEntry(edit, false, Guid.NewGuid(), out _, out code), Is.False);
            Assert.That(code, Is.EqualTo("profile.entry.revision"));
            Assert.That(profile.Entries.Single(e => e.Id == original.Id), Is.SameAs(saved));
        }

        [Test] public void VariantEntry_UsesOperationIdentityAndRejectsUnreceiptedCollision()
        {
            var profile = Profile(); var input = profile.Entries[0]; var operation = Guid.NewGuid(); int count = profile.Entries.Count;
            Assert.That(profile.TrySaveAssemblyEntry(input, true, operation, out var saved, out var code), Is.True, code);
            Assert.That(saved.Id, Is.EqualTo(operation.ToString("N"))); Assert.That(saved.Revision, Is.EqualTo(1));
            Assert.That(profile.Entries.Count, Is.EqualTo(count + 1));
            Assert.That(profile.TrySaveAssemblyEntry(input, true, operation, out _, out code), Is.False);
            Assert.That(code, Is.EqualTo("profile.entry.conflict")); Assert.That(profile.Entries.Count, Is.EqualTo(count + 1));
        }

        [Test] public void FullAlbum_AllowsEditButRejectsNewPageAndVariant()
        {
            var profile = Profile();
            for (int i = profile.Entries.Count; i < 64; i++)
                Assert.That(profile.TrySaveAssemblyEntry(Page("page." + i), false, Guid.NewGuid(), out _, out var addCode), Is.True, addCode);
            var original = profile.Entries[0];
            Assert.That(profile.TrySaveAssemblyEntry(new AlbumEntry(original.Id, "Edited full album", original.Revision, original.Snapshot),
                false, Guid.NewGuid(), out _, out var code), Is.True, code);
            Assert.That(profile.TrySaveAssemblyEntry(Page("overflow"), false, Guid.NewGuid(), out _, out code), Is.False);
            Assert.That(code, Is.EqualTo("profile.entry.full"));
            Assert.That(profile.TrySaveAssemblyEntry(original, true, Guid.NewGuid(), out _, out code), Is.False);
            Assert.That(code, Is.EqualTo("profile.entry.full")); Assert.That(profile.Entries.Count, Is.EqualTo(64));
        }

        [Test] public void LegacyPages_CannotBeSavedThroughModernMutator()
        {
            var profile = Profile(); var blueprint = new TowerBlueprint("legacy.page", "Legacy", ChassisKind.Standard, StructureKind.Gun);
            Assert.That(profile.TrySaveAssemblyEntry(new AlbumEntry(blueprint.Id, blueprint.Name, 1, blueprint), false, Guid.NewGuid(), out _, out var code), Is.False);
            Assert.That(code, Is.EqualTo("profile.entry.legacy_readonly")); Assert.That(profile.Entries.Count, Is.EqualTo(8));
        }

        [Test] public void LibrarySave_EditAndVariantUseIndependentRevisions()
        {
            var profile = Profile(); var input = Component("library.saved", 15); int pages = profile.Entries.Count;
            Assert.That(profile.TrySaveLibraryComponent(input, false, Guid.NewGuid(), out var saved, out var code), Is.True, code);
            Assert.That(saved.Revision, Is.EqualTo(1)); Assert.That(input.Revision, Is.EqualTo(15));
            var edit = new ComponentLibraryEntry(saved.Id, "Edited foundation", saved.Revision, saved.Snapshot);
            Assert.That(profile.TrySaveLibraryComponent(edit, false, Guid.NewGuid(), out var updated, out code), Is.True, code);
            Assert.That(updated.Revision, Is.EqualTo(2)); Assert.That(saved.Name, Is.EqualTo("Saved foundation"));
            Assert.That(profile.TrySaveLibraryComponent(edit, false, Guid.NewGuid(), out _, out code), Is.False);
            Assert.That(code, Is.EqualTo("profile.library.revision"));
            var operation = Guid.NewGuid();
            Assert.That(profile.TrySaveLibraryComponent(updated, true, operation, out var variant, out code), Is.True, code);
            Assert.That(variant.Id, Is.EqualTo(operation.ToString("N"))); Assert.That(variant.Revision, Is.EqualTo(1));
            Assert.That(profile.TrySaveLibraryComponent(updated, true, operation, out _, out code), Is.False);
            Assert.That(code, Is.EqualTo("profile.library.conflict")); Assert.That(profile.Entries.Count, Is.EqualTo(pages));
        }

        [Test] public void DeletingSelectedEntry_ClearsSlotsAndCompatibilityProjection()
        {
            var profile = Profile(); string id = profile.Loadout[0];
            Assert.That(profile.TryDeleteAssemblyEntry(id, out var code), Is.True, code);
            Assert.That(profile.Loadout[0], Is.Null);
            Assert.That(profile.Entries.Any(e => e.Id == id), Is.False); Assert.That(profile.Album.Any(e => e.Id == id), Is.False);
            Assert.That(profile.TryDeleteAssemblyEntry(id, out code), Is.False); Assert.That(code, Is.EqualTo("profile.entry.missing"));
        }

        [Test] public void DeletingLibraryComponent_DoesNotTouchSavedPageSnapshotOrReference()
        {
            var profile = Profile(); var component = Component("library.saved");
            Assert.That(profile.TrySaveLibraryComponent(component, false, Guid.NewGuid(), out _, out var code), Is.True, code);
            var input = new AlbumEntry("page.references", "Independent page", 1, Catalog().Starters[0].Snapshot,
                new[] { new ComponentLibraryReference(component.Id, 1, ComponentLibraryKind.Foundation, -1) });
            Assert.That(profile.TrySaveAssemblyEntry(input, false, Guid.NewGuid(), out var saved, out code), Is.True, code);
            Assert.That(profile.TryDeleteLibraryComponent(component.Id, out code), Is.True, code);
            Assert.That(profile.Library.Count, Is.Zero); Assert.That(profile.Entries.Single(e => e.Id == saved.Id), Is.SameAs(saved));
            Assert.That(saved.LibraryReferences[0].Id, Is.EqualTo(component.Id));
            Assert.That(AssemblyResolver.Resolve(saved.Snapshot, GameBalance.CreateDefault(), MaterialTier.Pencil).Value.Cost, Is.EqualTo(40));
        }

        [Test] public void AssigningSlot_MovesAnyOwnedEntryAndNullClears()
        {
            var profile = Profile(); string structure = profile.Loadout[5];
            Assert.That(profile.TryAssignAssemblySlot(0, structure, out var code), Is.True, code);
            Assert.That(profile.Loadout[0], Is.EqualTo(structure)); Assert.That(profile.Loadout[5], Is.Null);
            Assert.That(profile.TryAssignAssemblySlot(0, null, out code), Is.True, code); Assert.That(profile.Loadout[0], Is.Null);
            var before = profile.Loadout.ToArray();
            Assert.That(profile.TryAssignAssemblySlot(8, structure, out code), Is.False); Assert.That(code, Is.EqualTo("profile.slot.invalid"));
            Assert.That(profile.TryAssignAssemblySlot(0, "missing", out code), Is.False); Assert.That(code, Is.EqualTo("profile.entry.missing"));
            Assert.That(profile.Loadout, Is.EqualTo(before));
            var legacy = new WorkshopProfile(); string legacyId = legacy.Entries[0].Id;
            Assert.That(legacy.TryAssignAssemblySlot(7, legacyId, out code), Is.True, code);
            Assert.That(legacy.Loadout[0], Is.Null); Assert.That(legacy.Loadout[7], Is.EqualTo(legacyId));
        }

        [Test] public void Receipts_AreCopiedAndDuplicateOperationIsRejected()
        {
            var profile = Profile(); var id = Guid.NewGuid();
            Assert.That(profile.FindOperation(id), Is.Null);
            profile.RecordOperation(id, "save", "page.saved");
            var receipt = profile.FindOperation(id);
            Assert.That(receipt.OperationId, Is.EqualTo(id.ToString("N"))); Assert.That(receipt.Type, Is.EqualTo("save"));
            receipt.ResultId = "changed";
            Assert.That(profile.FindOperation(id).ResultId, Is.EqualTo("page.saved"));
            Assert.Catch<ArgumentException>(() => profile.RecordOperation(id, "delete", "page.other"));
            Assert.That(profile.FindOperation(id).Type, Is.EqualTo("save"));
            Assert.Catch<ArgumentException>(() => profile.RecordOperation(Guid.Empty, "save", "page.saved"));
        }

        [Test] public void FindOperationRecognizesAnUppercaseNReceiptAfterVersionEightRead()
        {
            var id = Guid.ParseExact("0123456789abcdef0123456789abcdef", "N");
            var source = Profile().ToData();
            source.Operations = new[] { new OperationReceiptData
                { OperationId = id.ToString("N").ToUpperInvariant(), Type = "save", ResultId = "page.saved" } };
            var restored = WorkshopProfile.FromData(JsonUtility.FromJson<WorkshopProfileData>(JsonUtility.ToJson(source)), Catalog());
            var receipt = restored.FindOperation(id);
            Assert.That(receipt, Is.Not.Null, "N-format receipts accepted by the reader must retain their GUID identity regardless of letter case.");
            Assert.That(receipt.ResultId, Is.EqualTo("page.saved"));
        }

        [Test] public void EmptyOperationIds_AreRejectedWithoutPublishingSave()
        {
            var profile = Profile();
            Assert.That(profile.TrySaveAssemblyEntry(Page("page.rejected"), false, Guid.Empty, out _, out var code), Is.False);
            Assert.That(code, Is.EqualTo("profile.operation.invalid")); Assert.That(profile.Entries.Count, Is.EqualTo(8));
            Assert.That(profile.TrySaveLibraryComponent(Component("library.rejected"), false, Guid.Empty, out _, out code), Is.False);
            Assert.That(code, Is.EqualTo("profile.operation.invalid")); Assert.That(profile.Library.Count, Is.Zero);
        }
    }
}
