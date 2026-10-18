using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class RunLoadoutSnapshotTests
    {
        static AlbumEntry[] Slots(params AlbumEntry[] occupied)
        {
            var result = new AlbumEntry[ProfileLimits.LoadoutSlotCount];
            Array.Copy(occupied, result, occupied.Length);
            return result;
        }

        static RecipeSnapshot OwnedRecipe(AssemblyRecipe recipe, ComponentCatalog catalog)
            => new RecipeSnapshot(recipe, catalog.BalanceRevision, recipe.Parts.GroupBy(part => (part.Id, part.Revision)).Select(group =>
            {
                catalog.TryGet(group.Key.Id, group.Key.Revision, out var definition);
                return definition;
            }));

        static AlbumEntry Tower(string id, string core = "gun", string head = null)
        {
            var catalog = AssemblyResolverFixture.Catalog();
            return new AlbumEntry(id, id, 1, OwnedRecipe(AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(core, head)), catalog));
        }

        [Test] public void EightMixedStarterPagesResolveNumericallyWithoutRequiringPilotFitCoverage()
        {
            var entries = AssemblyResolverFixture.Catalog().Starters;
            var snapshot = new RunLoadoutSnapshot(entries, GameBalance.CreateDefault());
            Assert.That(snapshot.Entries.Select(entry => entry.Id), Is.EqualTo(entries.Select(entry => entry.Id)));
            Assert.That(snapshot.Resolved.Count, Is.EqualTo(ProfileLimits.LoadoutSlotCount));
            Assert.That(snapshot.Resolved.Take(4).Select(entry => entry.Cost), Is.EqualTo(new[] { 40, 64, 60, 99 }));
            Assert.That(snapshot.Resolved.Skip(4).Select(entry => entry.Cost), Is.EqualTo(new[] { 16, 22, 65, 60 }));
            Assert.That(snapshot.Resolved, Is.All.Matches<ResolvedEntry>(entry => entry.Material == MaterialTier.Pencil));
        }

        [Test] public void EmptyPositionsArePreservedBesideAnAttackingPage()
        {
            var entries = Slots(Tower("one.gun"));
            var snapshot = new RunLoadoutSnapshot(entries, GameBalance.CreateDefault());
            Assert.That(snapshot.Entries.Count, Is.EqualTo(ProfileLimits.LoadoutSlotCount));
            Assert.That(snapshot.Entries.Skip(1), Is.All.Null);
            Assert.That(snapshot.Resolved.Skip(1), Is.All.Null);
            Assert.That(snapshot.ResolveSlot(7, MaterialTier.Marker), Is.Null);
        }

        [TestCase(0)] [TestCase(4)] [TestCase(7)] [TestCase(9)]
        public void AnyCountOtherThanEightIsRejected(int count)
        {
            var entries = new AlbumEntry[count];
            if (count != 0) entries[0] = Tower("one.gun");
            Assert.Throws<ArgumentException>(() => new RunLoadoutSnapshot(entries, GameBalance.CreateDefault()));
        }

        [Test] public void MissingEntriesOrBalanceIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new RunLoadoutSnapshot(null, GameBalance.CreateDefault()));
            Assert.Throws<ArgumentNullException>(() => new RunLoadoutSnapshot(Slots(Tower("one.gun")), null));
        }

        [Test] public void DuplicatePageIdsAreRejectedEvenForDifferentEntryInstances()
        {
            var first = Tower("duplicate");
            var second = new AlbumEntry(first.Id, "Another name", 2, first.Snapshot);
            Assert.Throws<ArgumentException>(() => new RunLoadoutSnapshot(Slots(first, second), GameBalance.CreateDefault()));
        }

        [TestCase("empty")] [TestCase("support")] [TestCase("blotter")] [TestCase("supportAndBlotter")]
        public void ALoadoutWithoutAnEnemyDamagingWeaponIsRejected(string kind)
        {
            var entries = Slots();
            if (kind == "support" || kind == "supportAndBlotter") entries[0] = AssemblyResolverFixture.Catalog().Starters[4];
            if (kind == "blotter" || kind == "supportAndBlotter") entries[1] = Tower("cleanup", "blotter");
            Assert.Throws<ArgumentException>(() => new RunLoadoutSnapshot(entries, GameBalance.CreateDefault()));
        }

        [Test] public void AZeroDamageGunDoesNotSatisfyTheAttackingWeaponRequirement()
        {
            var input = AssemblyResolverFixture.Input();
            AssemblyResolverFixture.Set(input, "core.gun", AssemblyStat.Damage, ModifierOperation.Set, 0);
            var catalog = AssemblyResolverFixture.Modified(input);
            var entry = new AlbumEntry("zero.damage", "Zero damage", 1, OwnedRecipe(AssemblyResolverFixture.Tower(), catalog));
            Assert.Throws<ArgumentException>(() => new RunLoadoutSnapshot(Slots(entry), GameBalance.CreateDefault()));
        }

        [Test] public void LegacyBlotterStillCannotAttackWhenItsRawConfiguredDamageIsPositive()
        {
            var balance = GameBalance.CreateDefault();
            balance.Structure(StructureKind.Blotter).Damage = 100;
            var blueprint = new TowerBlueprint("legacy.cleanup", "Legacy cleanup", ChassisKind.Standard, StructureKind.Blotter);
            var entry = new AlbumEntry(blueprint.Id, blueprint.Name, 1, blueprint);
            var resolved = TowerCatalog.ResolveEntry(entry, balance);
            Assert.That(resolved.Valid, Is.True);
            Assert.That(resolved.Value.Weapons[0].Damage, Is.GreaterThan(0), "This fixture must exercise cleanup semantics rather than a zero damage shortcut.");
            Assert.Throws<ArgumentException>(() => new RunLoadoutSnapshot(Slots(entry), balance));
        }

        [Test] public void LegacyAttackPageCanAccompanySupportAndCleanupWithoutPurchasingParts()
        {
            var blueprint = new TowerBlueprint("legacy.gun", "Legacy gun", ChassisKind.Standard, StructureKind.Gun);
            var entry = new AlbumEntry(blueprint.Id, blueprint.Name, 1, blueprint);
            var snapshot = new RunLoadoutSnapshot(Slots(AssemblyResolverFixture.Catalog().Starters[7], Tower("cleanup", "blotter"), entry), GameBalance.CreateDefault());
            Assert.That(snapshot.Resolved[2].Kind, Is.EqualTo(AlbumEntryKind.Legacy));
            Assert.That(snapshot.Resolved[2].Weapons[0].LegacyContinuous, Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void NumericallyInvalidOccupiedPagesAreRejected(bool legacy)
        {
            AlbumEntry invalid;
            if (legacy)
            {
                var blueprint = new TowerBlueprint("legacy.invalid", "Invalid third mount", "chassis.standard",
                    new[] { new WeaponMountSelection(0, "weapon.gun"), new WeaponMountSelection(2, "weapon.gun") });
                invalid = new AlbumEntry(blueprint.Id, blueprint.Name, 1, blueprint);
            }
            else
            {
                var input = AssemblyResolverFixture.Input();
                AssemblyResolverFixture.Set(input, "core.gun", AssemblyStat.Interval, ModifierOperation.Set, 0);
                invalid = new AlbumEntry("invalid.interval", "Invalid interval", 1,
                    OwnedRecipe(AssemblyResolverFixture.Tower(), AssemblyResolverFixture.Modified(input)));
            }
            Assert.Throws<ArgumentException>(() => new RunLoadoutSnapshot(Slots(Tower("valid.gun"), invalid), GameBalance.CreateDefault()));
        }

        [Test] public void OwnedPaidRecipeSurvivesItsMutableCatalogInputBeingReplaced()
        {
            var input = AssemblyResolverFixture.Input(); var catalog = input.CreateSnapshot();
            var recipe = AssemblyResolverFixture.Tower(AssemblyResolverFixture.Weapon(head: "long"));
            var entry = new AlbumEntry("owned.long", "Owned long barrel", 1, OwnedRecipe(recipe, catalog));
            var snapshot = new RunLoadoutSnapshot(Slots(entry), GameBalance.CreateDefault());
            input.Definitions = Array.Empty<ComponentDefinitionInput>(); input.Starters = Array.Empty<AssemblyStarterInput>();
            Assert.That(snapshot.ResolveSlot(0, MaterialTier.Pencil).Weapons[0].Damage, Is.EqualTo(13.2f).Within(.0001f));
            Assert.That(snapshot.Entries[0].Snapshot.Recipe.Tower.Mounts[0].Weapon.Head.Id, Is.EqualTo("head.long"));
        }

        [TestCase(false)] [TestCase(true)]
        public void SourceListBalanceAndReturnedBalanceCopiesCannotChangeFrozenNumbers(bool legacy)
        {
            var balance = GameBalance.CreateDefault();
            var blueprint = new TowerBlueprint("legacy.frozen", "Legacy frozen", ChassisKind.Standard, StructureKind.Gun);
            var entry = legacy ? new AlbumEntry(blueprint.Id, blueprint.Name, 1, blueprint) : Tower("modern.frozen");
            var entries = Slots(entry);
            var expectedMarker = TowerCatalog.ResolveEntry(entry, balance, MaterialTier.Marker).Value;
            var snapshot = new RunLoadoutSnapshot(entries, balance);
            int initialCost = snapshot.Resolved[0].Cost; float initialDamage = snapshot.Resolved[0].Weapons[0].Damage;
            entries[0] = null;
            balance.Material(MaterialTier.Pencil).CostMultiplier = 99;
            balance.Material(MaterialTier.Marker).DamageMultiplier = 99;
            balance.Construction.FoundationHealth = 999;
            balance.Structure(StructureKind.Gun).Damage = 999;
            var exportedBalance = snapshot.Balance;
            exportedBalance.Material(MaterialTier.Marker).CostMultiplier = 99;
            exportedBalance.Combat.MinimumActionInterval = 99;
            Assert.That(snapshot.Entries[0].Id, Is.EqualTo(entry.Id));
            Assert.That(snapshot.Resolved[0].Cost, Is.EqualTo(initialCost));
            Assert.That(snapshot.Resolved[0].Weapons[0].Damage, Is.EqualTo(initialDamage));
            var marker = snapshot.ResolveSlot(0, MaterialTier.Marker);
            Assert.That(marker.Cost, Is.EqualTo(expectedMarker.Cost));
            Assert.That(marker.Health, Is.EqualTo(expectedMarker.Health));
            Assert.That(marker.Weapons[0].Damage, Is.EqualTo(expectedMarker.Weapons[0].Damage));
            Assert.That(snapshot.Balance.Material(MaterialTier.Marker).CostMultiplier, Is.Not.EqualTo(99));
            Assert.Throws<NotSupportedException>(() => ((IList<AlbumEntry>)snapshot.Entries)[0] = null);
            Assert.Throws<NotSupportedException>(() => ((IList<ResolvedEntry>)snapshot.Resolved)[0] = null);
        }

        [TestCase(-1)] [TestCase(8)]
        public void ResolvingAnUnknownSlotIsRejected(int slot)
        {
            var snapshot = new RunLoadoutSnapshot(Slots(Tower("one.gun")), GameBalance.CreateDefault());
            Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.ResolveSlot(slot, MaterialTier.Pencil));
        }

        [TestCase(0)] [TestCase(7)]
        public void ResolvingAnUnknownMaterialIsRejectedEvenForAnEmptySlot(int slot)
        {
            var snapshot = new RunLoadoutSnapshot(Slots(Tower("one.gun")), GameBalance.CreateDefault());
            Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.ResolveSlot(slot, (MaterialTier)99));
        }

        [Test] public void AGeneratorRetainsItsPencilOnlyMaterialRuleAfterSnapshotCreation()
        {
            var snapshot = new RunLoadoutSnapshot(AssemblyResolverFixture.Catalog().Starters, GameBalance.CreateDefault());
            Assert.Throws<ArgumentException>(() => snapshot.ResolveSlot(7, MaterialTier.Marker));
            Assert.That(snapshot.ResolveSlot(7, MaterialTier.Pencil).Support.IncomePerWave, Is.EqualTo(12));
        }
    }
}
