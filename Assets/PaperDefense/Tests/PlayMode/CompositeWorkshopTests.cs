#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class CompositeWorkshopTests
    {
        static string Snapshot(AlbumEntry entry) => JsonUtility.ToJson(new AlbumEntryData(entry));

        [TestCase(false)] [TestCase(true)]
        public void ChoosingAFoundationPreservesUnrelatedArchivedWeaponDefinitions(bool library)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var page = fixture.Gun(); var snapshot = new RecipeSnapshotData(page.Snapshot);
                snapshot.Definitions.Single(d => d.Id == "core.gun").Modifiers.Single(m => m.Stat == AssemblyStat.Damage).Value = 999;
                page = new AlbumEntry(page.Id, page.Name, page.Revision, snapshot.CreateSnapshot());
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(page);
                Assert.That(draft.Resolved.Weapons.Single().Damage, Is.EqualTo(999), "Opening an archived page preserves its dependency snapshot.");
                if (library) Assert.That(draft.ApplyLibrary(fixture.Foundation("archive.foundation", plated: true), -1), Is.True);
                else
                {
                    var preset = fixture.Plated(fixture.Gun("preset.foundation"));
                    Assert.That(preset.Snapshot.Definitions.Count, Is.GreaterThan(10), "The input also carries definitions outside its foundation.");
                    Assert.That(draft.ApplyPreset(preset, -1), Is.True);
                }
                Assert.That(draft.PreviewRecipe.Tower.Foundation.Armor.Id, Is.EqualTo("armor.plate"));
                Assert.That(draft.Resolved.Weapons.Single().Damage, Is.EqualTo(999));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void OptionalMountCanBeAddedAndRemovedWithoutRemovingTheMandatoryFirstWeapon()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(fixture.Gun());
                Assert.That(draft.ApplyPreset(fixture.Gun(), 1), Is.True);
                Assert.That(draft.PreviewRecipe.Tower.Mounts.Select(m => m.SlotIndex), Is.EqualTo(new[] { 0, 1 }));
                Assert.That(draft.RemoveWeapon(0), Is.False);
                Assert.That(draft.RemoveWeapon(1), Is.True);
                Assert.That(draft.PreviewRecipe.Tower.Mounts.Select(m => m.SlotIndex), Is.EqualTo(new[] { 0 }));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void BurstAndDrumRemainAnInvalidCandidateUntilTheExplicitStandardMagazineChoice()
        {
            using (var fixture = new AssemblyCommandFixture(gold: 1000, xp: 9999))
            {
                Assert.That(fixture.Commands.PurchasePart("magazine.drum", Guid.NewGuid()).Success, Is.True);
                Assert.That(fixture.Commands.PurchasePart("trigger.burst", Guid.NewGuid()).Success, Is.True);
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(fixture.Gun()); draft.OpenWeapon(0);
                Assert.That(draft.SelectPart("magazine.drum"), Is.True);
                Assert.That(draft.SelectPart("trigger.burst"), Is.True);
                var weapon = draft.PreviewRecipe.Tower.Mounts.Single().Weapon;
                Assert.That(weapon.Magazine.Id, Is.EqualTo("magazine.drum"));
                Assert.That(weapon.Trigger.Id, Is.EqualTo("trigger.burst"));
                Assert.That(draft.Diagnostics.Any(d => d.Code == "assembly.part.required"), Is.True);
                Assert.That(draft.SuggestedPartId, Is.EqualTo("magazine.standard"));
                Assert.That(draft.CanSaveComponent, Is.False); Assert.That(draft.Resolved, Is.Null);
                Assert.That(draft.SelectPart(draft.SuggestedPartId), Is.True);
                Assert.That(draft.PreviewRecipe.Tower.Mounts.Single().Weapon.Trigger.Id, Is.EqualTo("trigger.burst"));
                Assert.That(draft.Resolved, Is.Not.Null);
            }
        }

        [Test] public void SelectingAMechanismUsesOwnedDefaultsOnlyForIncompatibleParts()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(fixture.Gun()); draft.OpenWeapon(0);
                var before = draft.PreviewRecipe.Tower.Mounts.Single().Weapon;
                Assert.That(draft.SelectPart("core.piercer"), Is.True);
                var after = draft.PreviewRecipe.Tower.Mounts.Single().Weapon;
                Assert.That(after.Mechanism.Id, Is.EqualTo("core.piercer"));
                Assert.That(after.Head.Id, Is.EqualTo("head.piercer"));
                Assert.That(after.Sight.Id, Is.EqualTo(before.Sight.Id));
                Assert.That(after.Magazine.Id, Is.EqualTo(before.Magazine.Id));
                Assert.That(after.Trigger.Id, Is.EqualTo(before.Trigger.Id));
                Assert.That(draft.ReplacedPartIds, Is.EqualTo(new[] { "head.piercer" }));
                Assert.That(draft.Draft.Snapshot.Recipe.Tower.Mounts.Single().Weapon.Mechanism.Id, Is.EqualTo("core.gun"));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [TestCase("foundation")] [TestCase("weapon")] [TestCase("structure")]
        public void CancelNestedEdit_PreservesParent(string component)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var source = component == "structure" ? fixture.Catalog.Starters.First(e => e.Kind == AlbumEntryKind.Structure) : fixture.Gun();
                var draft = new WorkshopDraftSession(fixture.Run);
                draft.Begin(source); var parent = draft.Draft; string before = Snapshot(parent), profile = fixture.State();
                if (component == "foundation") { draft.OpenFoundation(); draft.SelectPart("armor.plate"); }
                else if (component == "weapon") { draft.OpenWeapon(0); draft.SelectPart("head.gun"); }
                else { draft.OpenStructure(); draft.SelectPart("utility.none"); }
                Assert.That(draft.IsComponentOpen, Is.True);
                draft.CancelComponent();
                Assert.That(draft.Draft, Is.SameAs(parent));
                Assert.That(Snapshot(draft.Draft), Is.EqualTo(before));
                Assert.That(fixture.State(), Is.EqualTo(profile));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [TestCase("foundation", 5)] [TestCase("weapon", 5)] [TestCase("structure", 4)]
        public void NestedEditorHasTheRequiredCategoriesAndSharedPreview(string component, int count)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var source = component == "structure" ? fixture.Catalog.Starters.First(e => e.Kind == AlbumEntryKind.Structure) : fixture.Gun();
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(source);
                if (component == "foundation") draft.OpenFoundation();
                else if (component == "weapon") draft.OpenWeapon(0);
                else draft.OpenStructure();
                Assert.That(draft.Categories.Count, Is.EqualTo(count));
                Assert.That(draft.Categories.Distinct().Count(), Is.EqualTo(count));
                var expected = AssemblyResolver.Resolve(draft.PreviewRecipe, fixture.Catalog, fixture.Balance, MaterialTier.Pencil);
                Assert.That(draft.Resolved.Cost, Is.EqualTo(expected.Value.Cost));
                Assert.That(draft.Resolved.Load, Is.EqualTo(expected.Value.Load));
                Assert.That(draft.Resolved.Dps, Is.EqualTo(expected.Value.Dps));
                Assert.That(draft.CanSave, Is.False, "The parent cannot bypass its nested component editor.");
                Assert.That(draft.CanTrial, Is.False);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void SaveAndSelectPublishesTheParentOnlyAfterTheLibraryCommit(bool weapon)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(fixture.Gun()); var parent = draft.Draft;
                if (weapon) { draft.OpenWeapon(0); Assert.That(draft.SelectPart("head.gun"), Is.True); }
                else { draft.OpenFoundation(); Assert.That(draft.SelectPart("armor.plate"), Is.True); }
                Assert.That(draft.Draft, Is.SameAs(parent));
                Assert.That(draft.CanSaveComponent, Is.True);
                var operation = Guid.NewGuid(); var saved = draft.SaveComponent(operation);
                Assert.That(saved.Success, Is.True, saved.Code);
                Assert.That(draft.IsComponentOpen, Is.False);
                Assert.That(draft.Draft, Is.Not.SameAs(parent));
                var reference = draft.Draft.LibraryReferences.Single();
                Assert.That(reference.SlotIndex, Is.EqualTo(weapon ? 0 : -1));
                Assert.That(fixture.Run.Workshop.Library.Single(c => c.Id == saved.ResultId).Revision, Is.EqualTo(reference.Revision));
                Assert.That(fixture.Run.Workshop.Entries.Any(e => e.Id == parent.Id), Is.False, "Save and select does not save the parent album page.");
                Assert.That(draft.SaveComponent(operation).Success, Is.True, "Retrying the same successful component operation is idempotent.");
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(1));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void ComponentWriteFailureKeepsTheInnerDraftParentAndProfile(bool weapon)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(fixture.Gun()); var parent = draft.Draft;
                if (weapon) { draft.OpenWeapon(0); draft.SelectPart("head.gun"); }
                else { draft.OpenFoundation(); draft.SelectPart("armor.plate"); }
                string inner = Snapshot(draft.PreviewEntry), profile = fixture.State(); byte[] disk = fixture.SavedBytes;
                fixture.Store.RefuseWrites = true; var operation = Guid.NewGuid();
                var refused = draft.SaveComponent(operation);
                Assert.That(refused.Success, Is.False); Assert.That(refused.Code, Is.EqualTo("profile.write_failed"));
                Assert.That(draft.IsComponentOpen, Is.True); Assert.That(draft.Draft, Is.SameAs(parent));
                Assert.That(Snapshot(draft.PreviewEntry), Is.EqualTo(inner));
                Assert.That(fixture.State(), Is.EqualTo(profile)); Assert.That(fixture.SavedBytes, Is.EqualTo(disk));
                fixture.Store.RefuseWrites = false;
                Assert.That(draft.SaveComponent(operation).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Library.Count, Is.EqualTo(1));
            }
        }

        [Test] public void StructureAcceptsItsFourPartDraftLocallyWithoutInventingALibraryRecord()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var draft = new WorkshopDraftSession(fixture.Run);
                draft.Begin(fixture.Catalog.Starters.First(e => e.Kind == AlbumEntryKind.Structure));
                draft.OpenStructure(); Assert.That(draft.SelectPart("utility.none"), Is.True);
                Assert.That(draft.SaveComponent(Guid.NewGuid()).Success, Is.True);
                Assert.That(draft.IsComponentOpen, Is.False); Assert.That(draft.Draft.Kind, Is.EqualTo(AlbumEntryKind.Structure));
                Assert.That(fixture.Store.SaveCalls, Is.Zero); Assert.That(fixture.Run.Workshop.Library, Is.Empty);
            }
        }

        [TestCase(48, true)] [TestCase(49, false)]
        public void NameLengthUsesTheSameSaveBoundaryAsTheAlbum(int length, bool accepted)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(fixture.Gun());
                Assert.That(draft.Rename(new string('Я', length)), Is.EqualTo(accepted));
                var result = draft.SaveEntry(Guid.NewGuid());
                Assert.That(result.Success, Is.EqualTo(accepted));
                Assert.That(fixture.Run.Workshop.Entries.Any(e => e.Id == draft.Draft.Id), Is.EqualTo(accepted));
                if (accepted) Assert.That(fixture.Run.Workshop.Entries.Single(e => e.Id == draft.Draft.Id).Name.Length, Is.EqualTo(48));
                else Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void SixtyFifthPageIsRejectedWithoutLosingTheDraft()
        {
            using (var fixture = new AssemblyCommandFixture(configure: data => {
                var pages = data.Entries.ToList(); var template = data.Entries.First(e => e.Kind == AlbumEntryKind.Tower);
                for (int i = pages.Count; i < 64; i++)
                {
                    var page = JsonUtility.FromJson<AlbumEntryData>(JsonUtility.ToJson(template)); page.Id = "full.page-" + i; pages.Add(page);
                }
                data.Entries = pages.ToArray();
            }))
            {
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(fixture.Gun()); var before = draft.Draft;
                var result = draft.SaveEntry(Guid.NewGuid());
                Assert.That(result.Success, Is.False); Assert.That(result.Code, Is.EqualTo("profile.entry.full"));
                Assert.That(draft.Draft, Is.SameAs(before)); Assert.That(fixture.Run.Workshop.Entries.Count, Is.EqualTo(64));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void ApplyingALibraryComponentCopiesItsRevisionAndDeletingItDoesNotBreakTheDraft()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var component = fixture.Foundation("library.copy", plated: true);
                Assert.That(fixture.Commands.SaveLibraryEntry(component, Guid.NewGuid()).Success, Is.True);
                component = fixture.Run.Workshop.Library.Single();
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(fixture.Gun());
                int writes = fixture.Store.SaveCalls;
                Assert.That(draft.ApplyLibrary(component, -1), Is.True);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(writes), "Choosing a component changes only the local parent draft.");
                Assert.That(draft.Draft.LibraryReferences.Single().Revision, Is.EqualTo(component.Revision));
                Assert.That(draft.Draft.Snapshot.Recipe.Tower.Foundation.Armor.Id, Is.EqualTo("armor.plate"));
                string before = Snapshot(draft.Draft);
                Assert.That(fixture.Commands.DeleteLibraryEntry(component.Id, Guid.NewGuid()).Success, Is.True);
                Assert.That(Snapshot(draft.Draft), Is.EqualTo(before)); Assert.That(draft.CanSave, Is.True);
                Assert.That(draft.SaveEntry(Guid.NewGuid()).Success, Is.True);
            }
        }

        [Test] public void SaveAsVariantKeepsTheOriginalPageAndUsesTheCommittedIdentity()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var source = fixture.Run.Workshop.Entries.First(e => e.Kind == AlbumEntryKind.Tower && e.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.gun");
                string before = Snapshot(source); var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(source); draft.Rename("Мой вариант");
                var operation = Guid.NewGuid(); var result = draft.SaveEntry(operation, true);
                Assert.That(result.Success, Is.True); Assert.That(draft.Draft.Id, Is.EqualTo(result.ResultId));
                Assert.That(draft.Draft.Id, Is.Not.EqualTo(source.Id));
                Assert.That(Snapshot(fixture.Run.Workshop.Entries.Single(e => e.Id == source.Id)), Is.EqualTo(before));
                Assert.That(draft.SaveEntry(operation, true).ResultId, Is.EqualTo(result.ResultId));
                Assert.That(fixture.Run.Workshop.Entries.Count, Is.EqualTo(9));
            }
        }

        [Test] public void LegacyPageDoesNotBecomeAnEditableCompositeByOpeningANestedEditor()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var blueprint = fixture.Run.Workshop.Album.First();
                var legacy = new AlbumEntry(blueprint.Id, blueprint.Name, 1, blueprint);
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(legacy); draft.OpenFoundation();
                Assert.DoesNotThrow(() => { var resolution = draft.Resolution; Assert.That(resolution.Value, Is.Not.Null); });
                Assert.That(draft.IsComponentOpen, Is.False); Assert.That(draft.CanSave, Is.False); Assert.That(draft.CanTrial, Is.False);
                Assert.That(draft.SaveEntry(Guid.NewGuid()).Success, Is.False); Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }
    }
}
#endif
