using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class BlotterCatalogTests
    {
        [Test] public void GunBlotterAndArmorFitTheStandardFoundationExactly()
        {
            var balance = GameBalance.CreateDefault();
            var page = new TowerBlueprint("gun-blotter", "Пулемёт с промокашкой", ChassisKind.Standard,
                StructureKind.Gun, StructureKind.Blotter, TowerModules.Armor);
            Assert.That((int)StructureKind.Blotter, Is.EqualTo(8));
            Assert.That(TowerCatalog.IsMountPart(StructureKind.Blotter), Is.True);
            Assert.That(TowerCatalog.Load(page, balance), Is.EqualTo(10));
            Assert.That(TowerCatalog.Validate(page, balance, out string reason), Is.True, reason);
            balance.Construction.BlotterLoad = 4;
            Assert.That(TowerCatalog.Validate(page, balance, out _), Is.False);
        }

        [Test] public void BlotterAssemblyUsesCleanupGeometryWithZeroDamage()
        {
            var balance = GameBalance.CreateDefault();
            var page = new TowerBlueprint("blotter", "Промокашка", ChassisKind.Standard, StructureKind.Blotter);
            var weapon = balance.Structure(StructureKind.Blotter);
            Assert.That(TowerCatalog.Cost(page, balance, MaterialTier.Pencil), Is.EqualTo(30));
            Assert.That(TowerCatalog.Health(page, balance, MaterialTier.Pencil), Is.EqualTo(55));
            Assert.That(TowerCatalog.BuildSeconds(page, balance, MaterialTier.Pencil), Is.EqualTo(1.2f));
            Assert.That(TowerCatalog.WeaponRange(page, balance, StructureKind.Blotter), Is.EqualTo(9));
            Assert.That(weapon.Damage, Is.Zero); Assert.That(weapon.ShotInterval, Is.EqualTo(2));
            Assert.That(weapon.Radius, Is.EqualTo(1.5f));
        }

        [Test] public void BlotterRightIsPermanentAndHasItsOwnConfiguredCostAndPurpose()
        {
            var balance = GameBalance.CreateDefault(); balance.Construction.BlotterUnlockGold = 19;
            var profile = new WorkshopProfile(); profile.GrantGold(50);
            var page = new TowerBlueprint("blotter", "Промокашка", ChassisKind.Standard, StructureKind.Blotter);
            Assert.That(profile.SaveBlueprint(page, out _, balance), Is.False);
            PartUnlock part = profile.GetParts(balance).Single(p => p.Id == "weapon.blotter");
            Assert.That(part.GoldCost, Is.EqualTo(19)); Assert.That(TestText.Resolve(part.Description), Does.Contain("клякс"));
            Assert.That(profile.Unlock(part.Id, out string reason, balance), Is.True, reason);
            Assert.That(profile.Gold, Is.EqualTo(31));
            Assert.That(profile.Unlock(part.Id, out reason, balance), Is.True, reason);
            Assert.That(profile.Gold, Is.EqualTo(31));
            Assert.That(profile.SaveBlueprint(page, out reason, balance), Is.True, reason);
        }

        [Test] public void VersionTwoCannotClaimTheNewCleanupWeapon()
        {
            var data = HistoricalCatalogTestData.VersionTwo(); data.Album[0].FirstWeapon = StructureKind.Blotter;
            Assert.That(() => WorkshopProfile.FromData(data), Throws.InstanceOf<ArgumentException>());
        }
    }
}
