using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class StaplerCatalogTests
    {
        [Test] public void MortarAndStaplerUseExactlyTheStandardFoundationCapacity()
        {
            var balance = GameBalance.CreateDefault();
            var page = new TowerBlueprint("mortar-stapler", "Мортира со степлером", ChassisKind.Standard,
                StructureKind.Mortar, StructureKind.Stapler);
            Assert.That((int)StructureKind.Stapler, Is.EqualTo(7));
            Assert.That(TowerCatalog.IsMountPart(StructureKind.Stapler), Is.True);
            Assert.That(TowerCatalog.Load(page, balance), Is.EqualTo(10));
            Assert.That(TowerCatalog.Validate(page, balance, out string reason), Is.True, reason);
            balance.Construction.StaplerLoad = 4;
            Assert.That(TowerCatalog.Validate(page, balance, out _), Is.False);
        }

        [Test] public void StaplerAssemblyExposesItsOwnDamageAndIntrinsicSlowTuning()
        {
            var balance = GameBalance.CreateDefault();
            var page = new TowerBlueprint("stapler", "Степлер", ChassisKind.Standard, StructureKind.Stapler);
            var weapon = balance.Structure(StructureKind.Stapler);
            Assert.That(TowerCatalog.Cost(page, balance, MaterialTier.Pencil), Is.EqualTo(35));
            Assert.That(TowerCatalog.Health(page, balance, MaterialTier.Pencil), Is.EqualTo(55));
            Assert.That(TowerCatalog.BuildSeconds(page, balance, MaterialTier.Pencil), Is.EqualTo(1.2f));
            Assert.That(TowerCatalog.WeaponRange(page, balance, StructureKind.Stapler), Is.EqualTo(9));
            Assert.That(weapon.Damage, Is.EqualTo(4)); Assert.That(weapon.ShotInterval, Is.EqualTo(.65f));
            Assert.That(weapon.SlowSpeedMultiplier, Is.EqualTo(.8f)); Assert.That(weapon.SlowDuration, Is.EqualTo(2));
        }

        [Test] public void StaplerRequiresItsOwnPermanentRightWithAConfiguredPrice()
        {
            var balance = GameBalance.CreateDefault(); balance.Construction.StaplerUnlockGold = 17;
            var profile = new WorkshopProfile(); profile.GrantGold(50);
            var page = new TowerBlueprint("stapler", "Степлер", ChassisKind.Standard, StructureKind.Stapler);
            Assert.That(profile.SaveBlueprint(page, out _, balance), Is.False);
            PartUnlock part = profile.GetParts(balance).Single(p => p.Id == "weapon.stapler");
            Assert.That(part.GoldCost, Is.EqualTo(17)); Assert.That(part.Description, Is.Not.Empty);
            Assert.That(profile.Unlock(part.Id, out string reason, balance), Is.True, reason);
            Assert.That(profile.Gold, Is.EqualTo(33));
            Assert.That(profile.Unlock(part.Id, out reason, balance), Is.True, reason);
            Assert.That(profile.Gold, Is.EqualTo(33));
            Assert.That(profile.SaveBlueprint(page, out reason, balance), Is.True, reason);
        }

        [Test] public void SnapshotAndReadonlySpecOwnIntrinsicSlowStrengthAndDuration()
        {
            var balance = GameBalance.CreateDefault(); var weapon = balance.Structure(StructureKind.Stapler);
            weapon.SlowSpeedMultiplier = .73f; weapon.SlowDuration = 3.25f;
            var copy = balance.CreateSnapshot();
            StructureSpec spec = new RunReadModel(new GameSession(copy, starterDefenses: false)).Balance.Structure(StructureKind.Stapler);
            weapon.SlowSpeedMultiplier = .2f; weapon.SlowDuration = 20;
            Assert.That(copy.Structure(StructureKind.Stapler).SlowSpeedMultiplier, Is.EqualTo(.73f));
            Assert.That(copy.Structure(StructureKind.Stapler).SlowDuration, Is.EqualTo(3.25f));
            Assert.That(spec.SlowSpeedMultiplier, Is.EqualTo(.73f)); Assert.That(spec.SlowDuration, Is.EqualTo(3.25f));
            Assert.That(typeof(StructureSpec).GetField(nameof(StructureSpec.SlowDuration)).IsInitOnly, Is.True);
        }

        [Test] public void VersionTwoCannotClaimStapler()
        {
            var data = HistoricalCatalogTestData.VersionTwo(); data.Album[0].FirstWeapon = StructureKind.Stapler;
            Assert.That(() => WorkshopProfile.FromData(data), Throws.InstanceOf<ArgumentException>());
        }
    }
}
