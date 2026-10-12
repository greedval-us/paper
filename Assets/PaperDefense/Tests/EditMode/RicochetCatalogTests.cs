using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class RicochetCatalogTests
    {
        [Test] public void RicochetUsesItsOwnConfiguredLoadAndFitsAlongsideAGun()
        {
            var balance = GameBalance.CreateDefault();
            var page = new TowerBlueprint("ricochet", "Рикошет", ChassisKind.Standard, StructureKind.Gun, StructureKind.Ricochet);
            Assert.That((int)StructureKind.Ricochet, Is.EqualTo(6));
            Assert.That(TowerCatalog.IsMountPart(StructureKind.Ricochet), Is.True);
            Assert.That(TowerCatalog.Load(page, balance), Is.EqualTo(9));
            Assert.That(TowerCatalog.Validate(page, balance, out string reason), Is.True, reason);
            balance.Construction.RicochetLoad = 7;
            Assert.That(TowerCatalog.Validate(page, balance, out _), Is.False);
        }

        [Test] public void RicochetDefaultAssemblyExposesTheTunableShortChainHypothesis()
        {
            var balance = GameBalance.CreateDefault();
            var page = new TowerBlueprint("ricochet", "Рикошет", ChassisKind.Standard, StructureKind.Ricochet);
            var weapon = balance.Structure(StructureKind.Ricochet);
            Assert.That(TowerCatalog.Cost(page, balance, MaterialTier.Pencil), Is.EqualTo(65));
            Assert.That(TowerCatalog.Health(page, balance, MaterialTier.Pencil), Is.EqualTo(55));
            Assert.That(TowerCatalog.BuildSeconds(page, balance, MaterialTier.Pencil), Is.EqualTo(1.6f));
            Assert.That(TowerCatalog.WeaponRange(page, balance, StructureKind.Ricochet), Is.EqualTo(10));
            Assert.That(weapon.Damage, Is.EqualTo(12)); Assert.That(weapon.ShotInterval, Is.EqualTo(.85f));
            Assert.That(weapon.JumpRange, Is.EqualTo(3)); Assert.That(weapon.MaxTargets, Is.EqualTo(4));
        }

        [Test] public void RicochetRightChargesConfiguredGoldOnceAndKeepsItsDescription()
        {
            var balance = GameBalance.CreateDefault(); balance.Construction.RicochetUnlockGold = 23;
            var profile = new WorkshopProfile(); profile.GrantGold(80);
            var page = new TowerBlueprint("ricochet", "Рикошет", ChassisKind.Standard, StructureKind.Ricochet);
            Assert.That(profile.SaveBlueprint(page, out _, balance), Is.False);
            PartUnlock part = profile.GetParts(balance).Single(p => p.Id == "weapon.ricochet");
            Assert.That(part.GoldCost, Is.EqualTo(23)); Assert.That(part.Description, Is.Not.Empty);
            Assert.That(profile.Parts.Single(p => p.Id == part.Id).Description, Is.EqualTo(part.Description));
            Assert.That(profile.Unlock(part.Id, out string reason, balance), Is.True, reason);
            Assert.That(profile.Gold, Is.EqualTo(57));
            Assert.That(profile.Unlock(part.Id, out reason, balance), Is.True, reason);
            Assert.That(profile.Gold, Is.EqualTo(57));
            Assert.That(profile.SaveBlueprint(page, out reason, balance), Is.True, reason);
        }

        [Test] public void SnapshotAndReadonlySpecKeepTheirOwnJumpGeometryAndTargetCap()
        {
            var balance = GameBalance.CreateDefault(); var weapon = balance.Structure(StructureKind.Ricochet);
            weapon.JumpRange = 2.25f; weapon.MaxTargets = 3;
            var snapshot = balance.CreateSnapshot();
            var read = new RunReadModel(new GameSession(snapshot, starterDefenses: false));
            StructureSpec spec = read.Balance.Structure(StructureKind.Ricochet);
            weapon.JumpRange = 9; weapon.MaxTargets = 20;
            Assert.That(snapshot.Structure(StructureKind.Ricochet).JumpRange, Is.EqualTo(2.25f));
            Assert.That(snapshot.Structure(StructureKind.Ricochet).MaxTargets, Is.EqualTo(3));
            Assert.That(spec.JumpRange, Is.EqualTo(2.25f)); Assert.That(spec.MaxTargets, Is.EqualTo(3));
            Assert.That(typeof(StructureSpec).GetField(nameof(StructureSpec.JumpRange)).IsInitOnly, Is.True);
        }

        [Test] public void VersionTwoCannotClaimRicochetWhileCurrentProfilesRemainVersionThree()
        {
            var data = new WorkshopProfile().ToData(); Assert.That(data.Version, Is.EqualTo(4));
            data = HistoricalCatalogTestData.VersionTwo(); data.Album[0].FirstWeapon = StructureKind.Ricochet;
            Assert.That(() => WorkshopProfile.FromData(data), Throws.InstanceOf<ArgumentException>());
        }
    }
}
