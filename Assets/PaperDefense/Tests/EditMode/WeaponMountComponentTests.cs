using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class WeaponMountComponentTests
    {
        [Serializable] sealed class FitEnvelope { public AssemblyFitCatalogInput fits; }
        static WeaponRecipe Weapon(string mount = null)
        {
            string mountJson = mount == null ? "" : ",\"Mount\":{\"Id\":\"" + mount + "\",\"Revision\":1}";
            return JsonUtility.FromJson<WeaponRecipeInput>("{\"Mechanism\":{\"Id\":\"core.gun\",\"Revision\":1},\"Head\":{\"Id\":\"head.gun\",\"Revision\":1},\"Sight\":{\"Id\":\"sight.iron\",\"Revision\":1},\"Magazine\":{\"Id\":\"magazine.standard\",\"Revision\":1},\"Trigger\":{\"Id\":\"trigger.standard\",\"Revision\":1}" + mountJson + "}").CreateSnapshot();
        }

        [Test] public void ConfiguredCatalogProvidesThreeMountChoicesAndMountedStarterWeapons()
        {
            var catalog = AssemblyResolverFixture.Catalog();
            Assert.That(catalog.Definitions.Where(part => part.Id.StartsWith("mount.")).Select(part => part.Id),
                Is.EquivalentTo(new[] { "mount.standard", "mount.light", "mount.stabilized" }));
            foreach (var starter in catalog.Starters.Where(entry => entry.Kind == AlbumEntryKind.Tower))
                foreach (var weapon in starter.Snapshot.Recipe.Tower.Mounts)
                    Assert.That(weapon.Weapon.Parts.Any(part => part.Id == "mount.standard"), Is.True, starter.Id);
        }

        [Test] public void OptionalMountSurvivesLibrarySnapshotRoundTrip()
        {
            var weapon = Weapon("mount.light");
            Assert.That(weapon.Parts.Select(part => part.Id), Does.Contain("mount.light"), "A weapon recipe must retain its selected sixth socket.");
            var definitions = AssemblyResolverFixture.Catalog().Definitions.Where(definition => weapon.Parts.Any(part => part.Id == definition.Id));
            var original = new ComponentLibraryEntry("mount.roundtrip", "Лёгкая установка", 1,
                new ComponentRecipeSnapshot(weapon, "album-v1.0", definitions));
            var restored = JsonUtility.FromJson<ComponentLibraryEntryData>(JsonUtility.ToJson(new ComponentLibraryEntryData(original))).CreateSnapshot();
            Assert.That(restored.Snapshot.Weapon.Parts.Select(part => part.Id), Is.EqualTo(weapon.Parts.Select(part => part.Id)));
            Assert.That(restored.Snapshot.Definitions.Count, Is.EqualTo(6));
        }

        [Test] public void LightMountModifiersAffectTheSharedResolverAndWeaponQuote()
        {
            var catalog = AssemblyResolverFixture.Catalog();
            var neutral = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(Weapon()), catalog);
            var light = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(Weapon("mount.light")), catalog);
            Assert.That(light.Cost, Is.LessThan(neutral.Cost));
            Assert.That(light.Load, Is.LessThan(neutral.Load));
            Assert.That(light.Weapons[0].Damage, Is.LessThan(neutral.Weapons[0].Damage));
            Assert.That(light.Weapons[0].AcquisitionSeconds, Is.GreaterThan(neutral.Weapons[0].AcquisitionSeconds));
            Assert.That(AssemblyResolver.WeaponCost(Weapon("mount.light"), catalog, GameBalance.CreateDefault(), MaterialTier.Pencil),
                Is.EqualTo(light.Cost - AssemblyResolver.FoundationCost(AssemblyResolverFixture.Foundation(), catalog, GameBalance.CreateDefault(), MaterialTier.Pencil)));
        }

        [Test] public void HistoricalFivePartWeaponRemainsNumericallyEqualToTheStandardMount()
        {
            var catalog = AssemblyResolverFixture.Catalog();
            var historical = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(Weapon()), catalog);
            var standard = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(Weapon("mount.standard")), catalog);
            Assert.That(Weapon().Parts.Count(), Is.EqualTo(5));
            Assert.That(standard.Cost, Is.EqualTo(historical.Cost));
            Assert.That(standard.Load, Is.EqualTo(historical.Load));
            Assert.That(standard.Weapons[0].Damage, Is.EqualTo(historical.Weapons[0].Damage));
            Assert.That(standard.Weapons[0].AcquisitionSeconds, Is.EqualTo(historical.Weapons[0].AcquisitionSeconds));
        }

        [Test] public void UnknownOptionalMountProtectsTheSavedSnapshot()
        {
            var catalog = AssemblyResolverFixture.Catalog();
            var weapon = Weapon();
            var snapshot = new ComponentRecipeSnapshot(weapon, catalog.BalanceRevision,
                catalog.Definitions.Where(definition => weapon.Parts.Any(part => part.Id == definition.Id)));
            var dto = new ComponentRecipeSnapshotData(snapshot);
            dto.Weapon.Mount = new PartRefInput { Id = "mount.future", Revision = 7 };
            string json = JsonUtility.ToJson(dto);
            Assert.Throws<NotSupportedException>(() => JsonUtility.FromJson<ComponentRecipeSnapshotData>(json).CreateSnapshot());
        }

        [Test] public void StabilizedMountTradesCostLoadAndCadenceForRangeAndTargeting()
        {
            var catalog = AssemblyResolverFixture.Catalog();
            var standard = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(Weapon("mount.standard")), catalog);
            var stabilized = AssemblyResolverFixture.Resolve(AssemblyResolverFixture.Tower(Weapon("mount.stabilized")), catalog);
            Assert.That(stabilized.Cost, Is.EqualTo(48));
            Assert.That(stabilized.Load, Is.EqualTo(5));
            Assert.That(stabilized.Weapons[0].Range, Is.EqualTo(8.64f).Within(.00001f));
            Assert.That(stabilized.Weapons[0].AcquisitionSeconds, Is.EqualTo(.175f).Within(.00001f));
            Assert.That(stabilized.Weapons[0].Interval, Is.GreaterThan(standard.Weapons[0].Interval));
        }

        [Test] public void MountSocketMustMatchTheFoundationSocketAndWeaponInterface()
        {
            var input = PaperDefense.Application.AssemblyFitResources.ReadInput();
            var recipe = AssemblyResolverFixture.Tower(Weapon("mount.standard"));
            Assert.That(AssemblyFitRules.Validate(recipe, new AssemblyFitCatalog(input)), Is.Empty);
            var mount = input.Parts.Single(part => part.Id == "mount.standard");
            mount.AttachType = mount.ExpectedSocketType = "gun.incompatible";
            Assert.That(AssemblyFitRules.Validate(recipe, new AssemblyFitCatalog(input)).Any(issue => issue.Code == "assembly.fit.socket_type"), Is.True);
        }

        [Test] public void WrongCategoryCannotOccupyTheMountSocket()
        {
            var result = AssemblyResolver.Resolve(AssemblyResolverFixture.Tower(Weapon("sight.iron")), AssemblyResolverFixture.Catalog(), GameBalance.CreateDefault(), MaterialTier.Pencil);
            Assert.That(result.Valid, Is.False);
            Assert.That(result.Diagnostics.Any(issue => issue.Code == "assembly.part.category"), Is.True);
        }

        [Test] public void ConfiguredStarterStructuresRetainPhysicalCompatibilityAfterMountsAreAdded()
        {
            var input = PaperDefense.Application.AssemblyFitResources.ReadInput();
            var fits = new AssemblyFitCatalog(input);
            foreach (var structure in AssemblyResolverFixture.Catalog().Starters.Where(entry => entry.Kind == AlbumEntryKind.Structure))
                Assert.That(AssemblyFitRules.Validate(structure.Snapshot.Recipe, fits), Is.Empty, structure.Id);
        }
    }
}
