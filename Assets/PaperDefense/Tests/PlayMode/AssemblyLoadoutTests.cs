#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AssemblyLoadoutTests
    {
        [Test] public void EightDifferentPagesShareTheSlotsAndMovingOneClearsItsPreviousAssignment()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var slots = fixture.Run.Workshop.LoadoutEntries;
                Assert.That(slots.Count, Is.EqualTo(8));
                Assert.That(slots.Select(entry => entry.Id).Distinct().Count(), Is.EqualTo(8));
                string moved = slots[0].Id;
                Assert.That(fixture.Commands.AssignSlot(7, moved, Guid.NewGuid()).Success, Is.True);
                Assert.That(fixture.Run.Workshop.LoadoutEntries[0], Is.Null);
                Assert.That(fixture.Run.Workshop.LoadoutEntries[7].Id, Is.EqualTo(moved));
                Assert.That(fixture.Run.Workshop.LoadoutEntries.Count(entry => entry?.Id == moved), Is.EqualTo(1));
                Assert.That(fixture.Commands.DeleteEntry(moved, Guid.NewGuid()).Success, Is.True);
                Assert.That(fixture.Run.Workshop.LoadoutEntries[7], Is.Null);
                fixture.Restart();
                Assert.That(fixture.Run.Workshop.LoadoutEntries[0], Is.Null);
                Assert.That(fixture.Run.Workshop.LoadoutEntries[7], Is.Null);
            }
        }

        [TestCase("empty")] [TestCase("support")] [TestCase("blotter")]
        public void ALoadoutWithoutAnAttackingTowerCannotStart(string kind)
        {
            using (var fixture = new AssemblyCommandFixture(configure: data => {
                data.Loadout = new string[8];
                if (kind == "support")
                {
                    var supports = data.Entries.Where(entry => entry.Kind == AlbumEntryKind.Structure).ToArray();
                    for (int slot = 0; slot < supports.Length; slot++) data.Loadout[slot] = supports[slot].Id;
                }
                if (kind == "blotter")
                {
                    var source = data.Entries.First(entry => entry.Kind == AlbumEntryKind.Tower);
                    var recipe = JsonUtility.FromJson<AssemblyRecipeData>(JsonUtility.ToJson(source.Snapshot.Recipe));
                    recipe.Mounts = recipe.Mounts.Take(1).ToArray();
                    recipe.Mounts[0].Weapon.Mechanism.Id = "core.blotter";
                    recipe.Mounts[0].Weapon.Head.Id = "head.blotter";
                    var all = UnityEditor.AssetDatabase.LoadAssetAtPath<PaperDefense.Application.AssemblyCatalogAsset>(
                        "Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
                    var page = new AlbumEntry("only.blotter", "Cleanup without attack", 1,
                        new RecipeSnapshot(recipe.CreateSnapshot(), all.BalanceRevision, all.Definitions));
                    data.Entries = data.Entries.Concat(new[] { new AlbumEntryData(page) }).ToArray();
                    data.Loadout[7] = page.Id;
                    data.Unlocked = data.Unlocked.Concat(page.Snapshot.Recipe.Parts.Select(part => part.Id)).Distinct().ToArray();
                }
            }))
            {
                Assert.That(fixture.Run.StartRun(out var reason), Is.False);
                Assert.That(reason, Is.Not.Empty);
                Assert.That(fixture.Run.InWorkshop, Is.True);
            }
        }

        [Test] public void OneAttackingPageInTheLastSlotCanStartWithSevenEmptySlots()
        {
            using (var fixture = new AssemblyCommandFixture(configure: data => {
                string gun = data.Entries.First(entry => entry.Kind == AlbumEntryKind.Tower && entry.Snapshot.Recipe.Mounts.Length == 1 &&
                    entry.Snapshot.Recipe.Mounts[0].Weapon.Mechanism.Id == "core.gun").Id;
                data.Loadout = new string[8]; data.Loadout[7] = gun;
            }))
            {
                Assert.That(fixture.Run.StartRun(out var reason), Is.True, reason);
                Assert.That(fixture.Run.InWorkshop, Is.False);
                Assert.That(fixture.Run.RunLoadout.Resolved.Count, Is.EqualTo(8));
                for (int slot = 0; slot < 7; slot++) Assert.That(fixture.Run.RunLoadout.ResolveSlot(slot, MaterialTier.Pencil), Is.Null);
                Assert.That(fixture.Run.RunLoadout.ResolveSlot(7, MaterialTier.Pencil).Weapons.Single().Damage, Is.GreaterThan(0));
            }
        }

        [Test] public void SavedPageAndAuthoringChangesCannotMutateTheSnapshotOfTheStartedRun()
        {
            using (var fixture = new AssemblyCommandFixture(configure: data => {
                string gun = data.Entries.First(entry => entry.Kind == AlbumEntryKind.Tower && entry.Snapshot.Recipe.Mounts.Length == 1 &&
                    entry.Snapshot.Recipe.Mounts[0].Weapon.Mechanism.Id == "core.gun").Id;
                data.Loadout = new string[8]; data.Loadout[0] = gun;
            }))
            {
                var expectedInk = TowerCatalog.ResolveEntry(fixture.Run.Workshop.LoadoutEntries[0], fixture.Balance, MaterialTier.Ink);
                Assert.That(expectedInk.Valid, Is.True);
                Assert.That(fixture.Run.StartRun(out var reason), Is.True, reason);
                var frozen = fixture.Run.RunLoadout;
                var beforePencil = frozen.ResolveSlot(0, MaterialTier.Pencil);
                var beforeMarker = frozen.ResolveSlot(0, MaterialTier.Marker);
                string id = fixture.Run.Workshop.LoadoutEntries[0].Id;
                fixture.Run.OpenWorkshop();
                var current = fixture.Run.Workshop.Entries.Single(entry => entry.Id == id);
                Assert.That(fixture.Commands.SaveEntry(fixture.Plated(current), Guid.NewGuid()).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Entries.Single(entry => entry.Id == id).Revision, Is.EqualTo(2));
                fixture.AssemblyAsset.catalog.Definitions.Single(part => part.Id == "core.gun").Modifiers.Single(modifier => modifier.Stat == AssemblyStat.Damage).Value = 999;
                fixture.Preset.rules.MaterialDefinitions[(int)MaterialTier.Marker].CostMultiplier = 100;
                fixture.Preset.rules.MaterialDefinitions[(int)MaterialTier.Marker].DamageMultiplier = 100;
                fixture.Preset.rules.MaterialDefinitions[(int)MaterialTier.Ink].CostMultiplier = 100;
                fixture.Preset.rules.MaterialDefinitions[(int)MaterialTier.Ink].DamageMultiplier = 100;

                var afterPencil = frozen.ResolveSlot(0, MaterialTier.Pencil);
                var afterMarker = frozen.ResolveSlot(0, MaterialTier.Marker);
                var firstInkResolution = frozen.ResolveSlot(0, MaterialTier.Ink);
                Assert.That(afterPencil.Cost, Is.EqualTo(beforePencil.Cost));
                Assert.That(afterPencil.Defense, Is.EqualTo(beforePencil.Defense));
                Assert.That(afterPencil.Health, Is.EqualTo(beforePencil.Health));
                Assert.That(afterPencil.Weapons.Single().Damage, Is.EqualTo(beforePencil.Weapons.Single().Damage));
                Assert.That(afterMarker.Cost, Is.EqualTo(beforeMarker.Cost));
                Assert.That(afterMarker.Weapons.Single().Damage, Is.EqualTo(beforeMarker.Weapons.Single().Damage));
                Assert.That(firstInkResolution.Cost, Is.EqualTo(expectedInk.Value.Cost), "A material first resolved after authoring changes still uses the captured balance.");
                Assert.That(firstInkResolution.Weapons.Single().Damage, Is.EqualTo(expectedInk.Value.Weapons.Single().Damage));
            }
        }

        [TestCase("workshop")] [TestCase("ordinary")] [TestCase("memory")]
        [TestCase("store")] [TestCase("null-store")]
        public void FailedPreparationPreservesTheAlreadyPublishedRunAndFrozenLoadout(string preparation)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Assert.That(fixture.Run.StartRun(out var reason), Is.True, reason);
                var snapshot = fixture.Run.RunLoadout;
                var session = fixture.Run.Session; var content = fixture.Run.Content; var workshop = fixture.Run.Workshop;
                string before = fixture.State(); int loads = fixture.Store.LoadCalls, saves = fixture.Store.SaveCalls;
                fixture.BalanceAsset.baseHealth = float.NaN;
                fixture.Preset.rules.BaseHealth = float.NaN;

                if (preparation == "workshop") Assert.Catch<ArgumentException>(() => fixture.Run.OpenWorkshop());
                else if (preparation == "ordinary") Assert.Catch<ArgumentException>(() => fixture.Run.Initialize());
                else if (preparation == "memory") Assert.Catch<ArgumentException>(() => fixture.Run.InitializeWorkshop(false));
                else if (preparation == "store") Assert.Catch<ArgumentException>(() => fixture.Run.InitializeWorkshop(fixture.Store));
                else Assert.Catch<ArgumentException>(() => fixture.Run.InitializeWorkshop((PaperDefense.Application.IWorkshopProfileStore)null));

                Assert.That(fixture.Run.RunLoadout, Is.SameAs(snapshot), "Preparation failure must not clear the running party's frozen selection.");
                Assert.That(fixture.Run.Session, Is.SameAs(session));
                Assert.That(fixture.Run.Content, Is.SameAs(content));
                Assert.That(fixture.Run.Workshop, Is.SameAs(workshop));
                Assert.That(fixture.Run.InWorkshop, Is.False);
                Assert.That(fixture.State(), Is.EqualTo(before));
                Assert.That(fixture.Store.LoadCalls, Is.EqualTo(loads));
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(saves));
            }
        }

        [Test] public void SessionSubscriberFailurePropagatesAfterTheStartedRunHasBeenPublished()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var previous = fixture.Run.Session;
                var failure = new ArgumentException("Injected post-publication session subscriber failure");
                Action<IRunReadModel> subscriber = _ => throw failure;
                fixture.Run.SessionChanged += subscriber;
                try
                {
                    var thrown = Assert.Throws<ArgumentException>(() => fixture.Run.StartRun(out _));
                    Assert.That(thrown, Is.SameAs(failure));
                    Assert.That(fixture.Run.RunLoadout, Is.Not.Null);
                    Assert.That(fixture.Run.Session, Is.Not.SameAs(previous));
                    Assert.That(fixture.Run.InWorkshop, Is.False);
                }
                finally { fixture.Run.SessionChanged -= subscriber; }
            }
        }
    }
}
#endif
