#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class SequentialWorkshopDomainTests
    {
        static ComponentLibraryEntry Weapon(AssemblyCommandFixture fixture, WeaponRecipe weapon, string id = "library.weapon")
        {
            var selected = new HashSet<(string, int)>(weapon.Parts.Select(p => (p.Id, p.Revision)));
            return new ComponentLibraryEntry(id, "Independent weapon", 1, new ComponentRecipeSnapshot(weapon,
                fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions.Where(d => selected.Contains((d.Id, d.Revision)))));
        }

        static ComponentCatalog HeavyCatalog(AssemblyCommandFixture fixture)
        {
            var definitions = fixture.Catalog.Definitions.Select(d => d.Id != "core.gun" ? d :
                new ComponentDefinition(d.Id, d.Revision, d.Name, d.Category, d.RequiredLevel, d.UnlockGold,
                    d.WeaponFamilies, d.StructureFamilies, d.Modifiers.Select(m => m.Stat == AssemblyStat.Load
                        ? new ComponentModifier(m.Stat, m.Operation, 12) : m), d.RequiredParts));
            return new ComponentCatalog(fixture.Catalog.BalanceRevision, definitions);
        }

        static void RenameComponent(WorkshopDraftSession draft, string name)
        {
            var method = typeof(WorkshopDraftSession).GetMethod("RenameComponent", new[] { typeof(string) });
            Assert.That(method, Is.Not.Null, "Components need their own editable name.");
            Assert.That((bool)method.Invoke(draft, new object[] { name }), Is.True);
        }

        static IReadOnlyList<AssemblyDiagnostic> Compatibility(WorkshopDraftSession draft, ComponentLibraryEntry entry, int slot)
        {
            var method = typeof(WorkshopDraftSession).GetMethod("LibraryCompatibility", new[] { typeof(ComponentLibraryEntry), typeof(int) });
            Assert.That(method, Is.Not.Null, "Joining must expose a pure compatibility query.");
            return (IReadOnlyList<AssemblyDiagnostic>)method.Invoke(draft, new object[] { entry, slot });
        }

        [Test] public void FoundationCanSaveItsOwnNameWhileTheParentIsOverloaded()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var mortar = fixture.Catalog.Starters.First(e => e.Kind == AlbumEntryKind.Tower &&
                    e.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.mortar").Snapshot.Recipe.Tower.Mounts[0].Weapon;
                var recipe = new AssemblyRecipe(new TowerRecipe(fixture.Gun().Snapshot.Recipe.Tower.Foundation,
                    new[] { new WeaponMountRecipe(0, mortar), new WeaponMountRecipe(1, mortar) }));
                var page = new AlbumEntry("independent.foundation", "Parent name", 1,
                    new RecipeSnapshot(recipe, fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions));
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(page); draft.OpenFoundation();
                Assert.That(draft.Resolution.Valid, Is.False, "The two mortars exceed the standard foundation capacity.");
                Assert.That(draft.CanSaveComponent, Is.True, "A foundation is saved independently of mounted weapons.");
                RenameComponent(draft, "My foundation");
                var saved = draft.SaveComponent(Guid.NewGuid());
                Assert.That(saved.Success, Is.True, saved.Code);
                Assert.That(fixture.Run.Workshop.Library.Single(e => e.Id == saved.ResultId).Name, Is.EqualTo("My foundation"));
                Assert.That(draft.Draft.Name, Is.EqualTo("Parent name"));
                Assert.That(draft.CanSave, Is.False, "Saving a component does not certify an overloaded final join.");
            }
        }

        [Test] public void HeavyWeaponLibrarySaveDoesNotBorrowStandardFoundationCapacity()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var catalog = HeavyCatalog(fixture);
                var weapon = fixture.Gun().Snapshot.Recipe.Tower.Mounts[0].Weapon;
                var selected = new HashSet<(string, int)>(weapon.Parts.Select(p => (p.Id, p.Revision)));
                var input = new ComponentLibraryEntry("independent.heavy", "Heavy gun", 1,
                    new ComponentRecipeSnapshot(weapon, catalog.BalanceRevision, catalog.Definitions.Where(d => selected.Contains((d.Id, d.Revision)))));
                var result = new AssemblyCommands(fixture.Run, catalog).SaveLibraryEntry(input, Guid.NewGuid());
                Assert.That(result.Success, Is.True, result.Code);
                Assert.That(fixture.Run.Workshop.Library.Single(e => e.Id == result.ResultId).Name, Is.EqualTo("Heavy gun"));
            }
        }

        [Test] public void CompatibilityRejectsUnavailableSocketsWithoutChangingTheDraftOrProfile()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(fixture.Gun());
                var component = Weapon(fixture, fixture.Gun().Snapshot.Recipe.Tower.Mounts[0].Weapon);
                string before = JsonUtility.ToJson(new AlbumEntryData(draft.Draft)), profile = fixture.State();
                var issues = Compatibility(draft, component, 2);
                Assert.That(issues.Any(d => d.Code == "assembly.mount.unavailable" || d.Code == "assembly.fit.socket"), Is.True);
                Assert.That(JsonUtility.ToJson(new AlbumEntryData(draft.Draft)), Is.EqualTo(before));
                Assert.That(fixture.State(), Is.EqualTo(profile)); Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void SafeJoinRejectsAnOverloadedWeaponWithoutChangingTheSelectedFoundation()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var mortar = fixture.Catalog.Starters.First(e => e.Kind == AlbumEntryKind.Tower &&
                    e.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.mortar");
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(mortar);
                var component = Weapon(fixture, mortar.Snapshot.Recipe.Tower.Mounts[0].Weapon);
                var parent = draft.Draft;
                Assert.That(Compatibility(draft, component, 1).Any(d => d.Code == "assembly.load.exceeded"), Is.True);
                var method = typeof(WorkshopDraftSession).GetMethod("TryApplyCompatibleLibrary", new[] { typeof(ComponentLibraryEntry), typeof(int) });
                Assert.That(method, Is.Not.Null);
                Assert.That((bool)method.Invoke(draft, new object[] { component, 1 }), Is.False);
                Assert.That(draft.Draft, Is.SameAs(parent)); Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void ComponentVariantKeepsTheExistingNamedLibraryRevision()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(fixture.Gun()); draft.OpenFoundation();
                RenameComponent(draft, "Original foundation");
                var original = draft.SaveComponent(Guid.NewGuid()); Assert.That(original.Success, Is.True, original.Code);
                draft.OpenFoundation(); RenameComponent(draft, "Foundation variant");
                var method = typeof(WorkshopDraftSession).GetMethod("SaveComponent", new[] { typeof(Guid), typeof(bool) });
                Assert.That(method, Is.Not.Null, "Saving a component as a variant must be explicit.");
                var operation = Guid.NewGuid();
                var variant = (AssemblyCommandResult)method.Invoke(draft, new object[] { operation, true });
                Assert.That(variant.Success, Is.True, variant.Code);
                Assert.That(variant.ResultId, Is.Not.EqualTo(original.ResultId));
                var previous = fixture.Run.Workshop.Library.Single(e => e.Id == original.ResultId);
                Assert.That(previous.Name, Is.EqualTo("Original foundation")); Assert.That(previous.Revision, Is.EqualTo(1));
                Assert.That(fixture.Run.Workshop.Library.Single(e => e.Id == variant.ResultId).Name, Is.EqualTo("Foundation variant"));
            }
        }
    }
}
#endif
