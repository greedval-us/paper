#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    internal sealed class AssemblyCommandFixture : IDisposable
    {
        internal readonly string DirectoryPath, ProfilePath;
        internal readonly GameBalanceAsset BalanceAsset;
        internal readonly BalancePresetAsset Preset;
        internal readonly ContentDatabaseAsset Database;
        internal readonly AssemblyCatalogAsset AssemblyAsset;
        internal readonly ComponentCatalog Catalog;
        internal readonly GameBalance Balance;
        internal readonly RecordingStore Store;
        internal GameObject Root;
        internal RunController Run;
        internal AssemblyCommands Commands => Run.AssemblyCommands;

        internal AssemblyCommandFixture(int gold = 0, int xp = 0, Action<WorkshopProfileData> configure = null)
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "PaperAssemblyTransactionTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath); ProfilePath = Path.Combine(DirectoryPath, "profile.json");
            var source = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            Assert.That(source, Is.Not.Null);
            BalanceAsset = Object.Instantiate(source); Preset = Object.Instantiate(source.dataPreset);
            Database = Object.Instantiate(Preset.database); AssemblyAsset = Object.Instantiate(Database.assemblyCatalog);
            BalanceAsset.dataPreset = Preset; Preset.database = Database; Database.assemblyCatalog = AssemblyAsset;
            Catalog = AssemblyAsset.CreateSnapshot(); Balance = BalanceAsset.CreateWorkshopBalance();
            var data = WorkshopProfile.CreateNew(Catalog, Balance.ResolveContent(), Balance).ToData();
            data.Gold = gold; data.TotalXp = xp; configure?.Invoke(data);
            var initial = WorkshopProfile.FromData(data, Catalog, Balance);
            var disk = new WorkshopProfileStore(ProfilePath, catalog: Catalog, legacyBalance: Balance);
            Assert.That(disk.Save(initial).Success, Is.True, disk.LastDiagnostic);
            Store = new RecordingStore(disk);
            Restart();
        }

        internal void Restart()
        {
            if (Root != null) Object.DestroyImmediate(Root);
            Root = new GameObject("Isolated assembly command owner"); Root.SetActive(false);
            Run = Root.AddComponent<RunController>(); Run.balanceAsset = BalanceAsset; Run.AutomaticSimulation = false;
            Run.InitializeWorkshop(Store);
        }

        internal WorkshopProfileData SavedData => new WorkshopProfileStore(ProfilePath, catalog: Catalog, legacyBalance: Balance).Load().Profile.ToData();
        internal byte[] SavedBytes => File.ReadAllBytes(ProfilePath);
        internal AlbumEntry Gun(string id = "draft.gun", string name = "Saved gun", int revision = 1)
        {
            var starter = Catalog.Starters.First(entry => entry.Kind == AlbumEntryKind.Tower &&
                entry.Snapshot.Recipe.Tower.Mounts.Count == 1 && entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.gun");
            return new AlbumEntry(id, name, revision, starter.Snapshot);
        }
        internal AlbumEntry Plated(AlbumEntry source)
        {
            var tower = source.Snapshot.Recipe.Tower; var old = tower.Foundation;
            var foundation = new FoundationRecipe(old.Frame, old.Hull, new PartRef("armor.plate", 1), old.Feed, old.Turn);
            var recipe = new AssemblyRecipe(new TowerRecipe(foundation, tower.Mounts));
            return new AlbumEntry(source.Id, source.Name + " plated", source.Revision,
                new RecipeSnapshot(recipe, Catalog.BalanceRevision, Catalog.Definitions), source.LibraryReferences);
        }
        internal ComponentLibraryEntry Foundation(string id, bool plated = false, int revision = 1)
        {
            var page = Gun(); if (plated) page = Plated(page);
            var recipe = page.Snapshot.Recipe.Tower.Foundation;
            var ids = recipe.Parts.Select(part => (part.Id, part.Revision)).ToArray();
            return new ComponentLibraryEntry(id, plated ? "Plated foundation" : "Standard foundation", revision,
                new ComponentRecipeSnapshot(recipe, Catalog.BalanceRevision,
                    Catalog.Definitions.Where(definition => ids.Contains((definition.Id, definition.Revision)))));
        }
        internal string State()
        {
            var view = Run.Workshop;
            var rights = Catalog.Definitions.Select(part => part.Id).Concat(view.Parts.Select(part => part.Id)).Distinct().OrderBy(id => id);
            return view.Gold + "|" + view.TotalXp + "|" +
                string.Join("|", view.Entries.Select(entry => JsonUtility.ToJson(new AlbumEntryData(entry)))) + "|" +
                string.Join("|", view.Library.Select(entry => JsonUtility.ToJson(new ComponentLibraryEntryData(entry)))) + "|" +
                string.Join(",", view.LoadoutEntries.Select(entry => entry?.Id ?? "")) + "|" +
                string.Join(",", rights.Where(view.IsUnlocked));
        }
        public void Dispose()
        {
            if (Root != null) Object.DestroyImmediate(Root);
            Object.DestroyImmediate(BalanceAsset); Object.DestroyImmediate(Preset);
            Object.DestroyImmediate(Database); Object.DestroyImmediate(AssemblyAsset);
            string target = Path.GetFullPath(DirectoryPath);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(target.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(target).StartsWith("PaperAssemblyTransactionTests-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(target)) Directory.Delete(target, true);
        }

        internal sealed class RecordingStore : IWorkshopProfileStore
        {
            readonly IWorkshopProfileStore disk;
            internal bool RefuseWrites;
            internal int LoadCalls, SaveCalls;
            internal RecordingStore(IWorkshopProfileStore disk) { this.disk = disk; }
            public ProfileLoadResult Load() { LoadCalls++; return disk.Load(); }
            public ProfileSaveResult Save(WorkshopProfile profile)
            {
                SaveCalls++;
                return RefuseWrites ? new ProfileSaveResult(false, "Injected assembly transaction write refusal") : disk.Save(profile);
            }
        }
    }

    public sealed class AssemblyTransactionTests
    {
        [Test] public void BuyLongBarrelTwice_ChargesOnce()
        {
            using (var fixture = new AssemblyCommandFixture(gold: 94, xp: 390))
            {
                var operation = Guid.NewGuid();
                string pages = string.Join("|", fixture.Run.Workshop.Entries.Select(entry => JsonUtility.ToJson(new AlbumEntryData(entry))));
                Assert.That(fixture.Run.Workshop.Level, Is.EqualTo(4));
                Assert.That(fixture.Commands.PurchasePart("head.long", operation).Success, Is.True);
                Assert.That(fixture.Commands.PurchasePart("head.long", operation).Success, Is.True);
                Assert.That(fixture.Commands.PurchasePart("head.long", Guid.NewGuid()).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(14));
                Assert.That(fixture.Run.Workshop.IsUnlocked("head.long"), Is.True);
                Assert.That(string.Join("|", fixture.Run.Workshop.Entries.Select(entry => JsonUtility.ToJson(new AlbumEntryData(entry)))), Is.EqualTo(pages),
                    "Buying a permanent right does not install that part in an existing page.");
                fixture.Restart();
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(14));
                Assert.That(fixture.Commands.PurchasePart("head.long", operation).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(14));
            }
        }

        [TestCase(389, 94)] [TestCase(390, 79)]
        public void LevelOrGoldRejectionDoesNotWriteOrChangeTheProfile(int xp, int gold)
        {
            using (var fixture = new AssemblyCommandFixture(gold, xp))
            {
                string before = fixture.State(); byte[] disk = fixture.SavedBytes;
                var result = fixture.Commands.PurchasePart("head.long", Guid.NewGuid());
                Assert.That(result.Success, Is.False);
                Assert.That(result.Code, Is.Not.Empty);
                Assert.That(fixture.State(), Is.EqualTo(before));
                Assert.That(fixture.SavedBytes, Is.EqualTo(disk));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void RefusedPurchaseKeepsAllStateAndTheSameOperationCanBeRetriedOnce()
        {
            using (var fixture = new AssemblyCommandFixture(gold: 94, xp: 390))
            {
                string before = fixture.State(); byte[] disk = fixture.SavedBytes; var operation = Guid.NewGuid();
                fixture.Store.RefuseWrites = true;
                Assert.That(fixture.Commands.PurchasePart("head.long", operation).Success, Is.False);
                Assert.That(fixture.State(), Is.EqualTo(before));
                Assert.That(fixture.SavedBytes, Is.EqualTo(disk));
                Assert.That(fixture.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.WriteFailed));
                fixture.Store.RefuseWrites = false;
                Assert.That(fixture.Commands.PurchasePart("head.long", operation).Success, Is.True);
                Assert.That(fixture.Commands.PurchasePart("head.long", operation).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(14));
                Assert.That(fixture.SavedData.Operations.Count(receipt => receipt.OperationId == operation.ToString("N")), Is.EqualTo(1));
                fixture.Restart();
                Assert.That(fixture.Commands.PurchasePart("head.long", operation).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(14));
            }
        }

        [Test] public void RefusedSaveKeepsDraftAndPublishesOnlyAfterTheSuccessfulCommit()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var draft = fixture.Gun(); var operation = Guid.NewGuid();
                string draftBefore = JsonUtility.ToJson(new AlbumEntryData(draft)), before = fixture.State();
                byte[] disk = fixture.SavedBytes; int published = 0;
                fixture.Run.WorkshopChanged += () => published++;
                fixture.Store.RefuseWrites = true;
                Assert.That(fixture.Commands.SaveEntry(draft, operation).Success, Is.False);
                Assert.That(fixture.State(), Is.EqualTo(before));
                Assert.That(JsonUtility.ToJson(new AlbumEntryData(draft)), Is.EqualTo(draftBefore));
                Assert.That(fixture.SavedBytes, Is.EqualTo(disk)); Assert.That(published, Is.Zero);
                fixture.Store.RefuseWrites = false;
                Assert.That(fixture.Commands.SaveEntry(draft, operation).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Entries.Single(entry => entry.Id == draft.Id).Revision, Is.EqualTo(1));
                Assert.That(published, Is.EqualTo(1));
                Assert.That(fixture.SavedData.Operations.Single(receipt => receipt.OperationId == operation.ToString("N")).ResultId, Is.EqualTo(draft.Id));
            }
        }

        [TestCase("assign")] [TestCase("delete")]
        public void SlotAndDeleteTransactionsPublishTheirClearedSlotsOnlyAfterCommit(string command)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                string id = fixture.Run.Workshop.LoadoutEntries[0].Id, before = fixture.State();
                byte[] disk = fixture.SavedBytes; var operation = Guid.NewGuid();
                fixture.Store.RefuseWrites = true;
                var rejected = command == "assign" ? fixture.Commands.AssignSlot(7, id, operation) : fixture.Commands.DeleteEntry(id, operation);
                Assert.That(rejected.Success, Is.False);
                Assert.That(fixture.State(), Is.EqualTo(before)); Assert.That(fixture.SavedBytes, Is.EqualTo(disk));
                fixture.Store.RefuseWrites = false;
                var accepted = command == "assign" ? fixture.Commands.AssignSlot(7, id, operation) : fixture.Commands.DeleteEntry(id, operation);
                Assert.That(accepted.Success, Is.True);
                Assert.That(fixture.Run.Workshop.LoadoutEntries[0], Is.Null);
                if (command == "assign") Assert.That(fixture.Run.Workshop.LoadoutEntries[7].Id, Is.EqualTo(id));
                else Assert.That(fixture.Run.Workshop.Entries.Any(entry => entry.Id == id), Is.False);
                fixture.Restart();
                Assert.That(fixture.Run.Workshop.LoadoutEntries[0], Is.Null);
            }
        }

        [Test] public void VariantOperationReturnsItsPersistedIdentityAfterRepeatedCallsAndRestart()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var source = fixture.Run.Workshop.Entries[0]; var operation = Guid.NewGuid();
                int originalCount = fixture.Run.Workshop.Entries.Count;
                var first = fixture.Commands.SaveEntryAsVariant(source, operation);
                Assert.That(first.Success, Is.True); Assert.That(first.ResultId, Is.Not.Empty.And.Not.EqualTo(source.Id));
                var repeated = fixture.Commands.SaveEntryAsVariant(source, operation);
                Assert.That(repeated.Success, Is.True); Assert.That(repeated.ResultId, Is.EqualTo(first.ResultId));
                Assert.That(fixture.Run.Workshop.Entries.Count, Is.EqualTo(originalCount + 1));
                fixture.Restart();
                Assert.That(fixture.Commands.SaveEntryAsVariant(source, operation).ResultId, Is.EqualTo(first.ResultId));
                Assert.That(fixture.Run.Workshop.Entries.Count, Is.EqualTo(originalCount + 1));
            }
        }

        [Test] public void MissingPhysicalCoverageAndImpossibleMountCountAreRejectedBeforeWriting()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var unsupported = fixture.Catalog.Starters.First(entry => entry.Kind == AlbumEntryKind.Tower &&
                    entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.piercer");
                Assert.That(AssemblyFitRules.Validate(unsupported.Snapshot.Recipe, AssemblyVisualComposer.Fits), Is.Not.Empty);
                var draft = new AlbumEntry("draft.unsupported", "Unsupported physical fit", 1, unsupported.Snapshot);
                Assert.That(fixture.Commands.SaveEntry(draft, Guid.NewGuid()).Success, Is.False);
                var gun = fixture.Gun(); var tower = gun.Snapshot.Recipe.Tower;
                var invalidRecipe = new AssemblyRecipe(new TowerRecipe(tower.Foundation,
                    Enumerable.Range(0, 3).Select(slot => new WeaponMountRecipe(slot, tower.Mounts[0].Weapon))));
                var invalid = new AlbumEntry("draft.too-many", "Impossible third mount", 1,
                    new RecipeSnapshot(invalidRecipe, fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions));
                Assert.That(fixture.Commands.SaveEntry(invalid, Guid.NewGuid()).Success, Is.False);
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
                Assert.That(fixture.Run.Workshop.Entries.Count, Is.EqualTo(8));
            }
        }

        [Test] public void LibraryRevisionDoesNotChangeAnOwnedPageUntilExplicitApply()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var component = fixture.Foundation("library.foundation");
                Assert.That(fixture.Commands.SaveLibraryEntry(component, Guid.NewGuid()).Success, Is.True);
                var draft = fixture.Gun("linked.gun");
                draft = new AlbumEntry(draft.Id, draft.Name, 1, draft.Snapshot,
                    new[] { new ComponentLibraryReference(component.Id, 1, ComponentLibraryKind.Foundation, -1) });
                Assert.That(fixture.Commands.SaveEntry(draft, Guid.NewGuid()).Success, Is.True);
                string before = JsonUtility.ToJson(new AlbumEntryData(fixture.Run.Workshop.Entries.Single(entry => entry.Id == draft.Id)));
                Assert.That(fixture.Commands.SaveLibraryEntry(fixture.Foundation(component.Id, plated: true), Guid.NewGuid()).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Library.Single(entry => entry.Id == component.Id).Revision, Is.EqualTo(2));
                Assert.That(JsonUtility.ToJson(new AlbumEntryData(fixture.Run.Workshop.Entries.Single(entry => entry.Id == draft.Id))), Is.EqualTo(before));
                Assert.That(fixture.Commands.ApplyLibraryEntry(draft.Id, component.Id, -1, Guid.NewGuid()).Success, Is.True);
                var applied = fixture.Run.Workshop.Entries.Single(entry => entry.Id == draft.Id);
                Assert.That(applied.Revision, Is.EqualTo(2));
                Assert.That(applied.Snapshot.Recipe.Tower.Foundation.Armor.Id, Is.EqualTo("armor.plate"));
                Assert.That(applied.LibraryReferences.Single().Revision, Is.EqualTo(2));
                string owned = JsonUtility.ToJson(new AlbumEntryData(applied));
                Assert.That(fixture.Commands.DeleteLibraryEntry(component.Id, Guid.NewGuid()).Success, Is.True);
                fixture.Restart();
                var restored = fixture.Run.Workshop.Entries.Single(entry => entry.Id == draft.Id);
                Assert.That(JsonUtility.ToJson(new AlbumEntryData(restored)), Is.EqualTo(owned));
                Assert.That(TowerCatalog.ResolveEntry(restored, fixture.Balance).Valid, Is.True);
            }
        }

        [Test] public void WorkshopSubscriberFailurePropagatesWithoutDisguisingACommittedPurchaseAsRejection()
        {
            using (var fixture = new AssemblyCommandFixture(gold: 94, xp: 390))
            {
                var operation = Guid.NewGuid();
                var failure = new ArgumentException("Injected post-commit workshop subscriber failure");
                Action subscriber = () => throw failure;
                fixture.Run.WorkshopChanged += subscriber;
                try
                {
                    var thrown = Assert.Throws<ArgumentException>(() => fixture.Commands.PurchasePart("head.long", operation));
                    Assert.That(thrown, Is.SameAs(failure));
                }
                finally { fixture.Run.WorkshopChanged -= subscriber; }
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(14));
                Assert.That(fixture.Run.Workshop.IsUnlocked("head.long"), Is.True);
                Assert.That(fixture.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.Saved));
                Assert.That(fixture.SavedData.Operations.Count(receipt => receipt.OperationId == operation.ToString("N")), Is.EqualTo(1));
                int saves = fixture.Store.SaveCalls;
                Assert.That(fixture.Commands.PurchasePart("head.long", operation).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(14));
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(saves), "Replaying the committed receipt does not write or charge again.");
            }
        }

        [Test] public void SavingAnEditableGunWithAnUnownedPartRejectsBeforeAnyWrite()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Assert.That(fixture.Run.Workshop.IsUnlocked("head.long"), Is.False);
                var page = fixture.Gun("draft.unowned"); var tower = page.Snapshot.Recipe.Tower;
                var originalWeapon = tower.Mounts.Single().Weapon;
                var weapon = new WeaponRecipe(originalWeapon.Mechanism, new PartRef("head.long", 1), originalWeapon.Sight,
                    originalWeapon.Magazine, originalWeapon.Trigger);
                var recipe = new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] { new WeaponMountRecipe(0, weapon) }));
                var draft = new AlbumEntry(page.Id, page.Name, 1, new RecipeSnapshot(recipe, fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions));
                string before = fixture.State(); byte[] disk = fixture.SavedBytes;

                var result = fixture.Commands.SaveEntry(draft, Guid.NewGuid());
                Assert.That(result.Success, Is.False);
                Assert.That(result.Code, Is.EqualTo("profile.part.locked"));
                Assert.That(fixture.State(), Is.EqualTo(before));
                Assert.That(fixture.SavedBytes, Is.EqualTo(disk));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void EditingAGunRebindsForgedSnapshotNumbersToTheAuthoritativeCatalog()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var current = fixture.Run.Workshop.Entries.First(entry => entry.Kind == AlbumEntryKind.Tower &&
                    entry.Snapshot.Recipe.Tower.Mounts.Count == 1 && entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.gun");
                var snapshot = new RecipeSnapshotData(current.Snapshot);
                snapshot.Definitions.Single(part => part.Id == "core.gun").Modifiers.Single(modifier => modifier.Stat == AssemblyStat.Damage).Value = 999;
                var draft = new AlbumEntry(current.Id, current.Name, current.Revision, snapshot.CreateSnapshot());
                string draftBefore = JsonUtility.ToJson(new AlbumEntryData(draft));
                var forged = TowerCatalog.ResolveEntry(draft, fixture.Balance);
                Assert.That(forged.Valid, Is.True);
                Assert.That(forged.Value.Weapons.Single().Damage, Is.EqualTo(999));
                var authoritative = AssemblyResolver.Resolve(draft.Snapshot.Recipe, fixture.Catalog, fixture.Balance, MaterialTier.Pencil);
                Assert.That(authoritative.Valid, Is.True);

                Assert.That(fixture.Commands.SaveEntry(draft, Guid.NewGuid()).Success, Is.True);
                var saved = fixture.Run.Workshop.Entries.Single(entry => entry.Id == current.Id);
                Assert.That(saved.Revision, Is.EqualTo(current.Revision + 1));
                Assert.That(TowerCatalog.ResolveEntry(saved, fixture.Balance).Value.Weapons.Single().Damage,
                    Is.EqualTo(authoritative.Value.Weapons.Single().Damage));
                Assert.That(JsonUtility.ToJson(new AlbumEntryData(draft)), Is.EqualTo(draftBefore), "Canonicalizing an accepted candidate does not mutate its input draft.");
                fixture.Restart();
                Assert.That(TowerCatalog.ResolveEntry(fixture.Run.Workshop.Entries.Single(entry => entry.Id == current.Id), fixture.Balance)
                    .Value.Weapons.Single().Damage, Is.EqualTo(authoritative.Value.Weapons.Single().Damage));
            }
        }
    }
}
#endif
