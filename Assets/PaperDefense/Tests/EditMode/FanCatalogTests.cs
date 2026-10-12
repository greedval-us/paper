using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class FanCatalogTests
    {
        [Test] public void FanFitsTheTwoMountCatalogWithItsConfiguredLoadAndPermanentUnlock()
        {
            var balance = GameBalance.CreateDefault(); var fan = new TowerBlueprint("fan", "Веер", ChassisKind.Standard, StructureKind.Fan);
            Assert.That((int)StructureKind.Fan, Is.EqualTo(5)); Assert.That(TowerCatalog.IsMountPart(StructureKind.Fan), Is.True);
            Assert.That(TowerCatalog.Load(fan, balance), Is.EqualTo(5));
            Assert.That(TowerCatalog.Validate(fan, balance, out string reason), Is.True, reason);
            Assert.That(TowerCatalog.Cost(fan, balance, MaterialTier.Pencil), Is.EqualTo(70));
            Assert.That(TowerCatalog.WeaponRange(fan, balance, StructureKind.Fan), Is.EqualTo(6));
            var mixed = new TowerBlueprint("mixed", "Пулемёт и веер", ChassisKind.Standard, StructureKind.Gun, StructureKind.Fan);
            Assert.That(TowerCatalog.Load(mixed, balance), Is.EqualTo(9));
            Assert.That(TowerCatalog.Validate(mixed, balance, out reason), Is.True, reason);
            balance.Construction.FanLoad = 7;
            Assert.That(TowerCatalog.Validate(mixed, balance, out _), Is.False);
        }
        [Test] public void FanRequiresItsOwnRightAndChargesTheConfiguredUnlockOnce()
        {
            var balance = GameBalance.CreateDefault(); balance.Construction.FanUnlockGold = 17;
            var profile = new WorkshopProfile(); profile.GrantGold(50);
            var fan = new TowerBlueprint("fan", "Веер", ChassisKind.Standard, StructureKind.Fan);
            Assert.That(profile.SaveBlueprint(fan, out string reason, balance), Is.False); Assert.That(reason, Does.Contain("откройте"));
            Assert.That(profile.GetParts(balance).Single(p => p.Id == "weapon.fan").GoldCost, Is.EqualTo(17));
            Assert.That(profile.Unlock("weapon.fan", out reason, balance), Is.True, reason); Assert.That(profile.Gold, Is.EqualTo(33));
            Assert.That(profile.Unlock("weapon.fan", out reason, balance), Is.True, reason); Assert.That(profile.Gold, Is.EqualTo(33));
            Assert.That(profile.SaveBlueprint(fan, out reason, balance), Is.True, reason);
        }
        [Test] public void PartDescriptionsRemainImmutableAndSurviveConfiguredCostViews()
        {
            var profile = new WorkshopProfile(); var balance = GameBalance.CreateDefault();
            foreach (PartUnlock part in profile.Parts)
            {
                Assert.That(part.Description, Is.Not.Empty, part.Id);
                Assert.That(profile.GetParts(balance).Single(p => p.Id == part.Id).Description, Is.EqualTo(part.Description));
            }
            Assert.That(typeof(PartUnlock).GetProperty(nameof(PartUnlock.Description)).GetSetMethod(true), Is.Null);
        }
        [Test] public void SnapshotOwnsTheConfiguredConeGeometryAndPerActionTargetLimit()
        {
            var balance = GameBalance.CreateDefault(); var fan = balance.Structure(StructureKind.Fan);
            fan.ConeAngleDegrees = 42; fan.MaxTargets = 3;
            var copy = balance.CreateSnapshot(); fan.ConeAngleDegrees = 120; fan.MaxTargets = 20;
            Assert.That(copy.Structure(StructureKind.Fan).ConeAngleDegrees, Is.EqualTo(42));
            Assert.That(copy.Structure(StructureKind.Fan).MaxTargets, Is.EqualTo(3));
        }
        [Test] public void CurrentProfilesWriteVersionThreeAndHistoricalVersionTwoCannotClaimNewKinds()
        {
            var profile = new WorkshopProfile(); Assert.That(profile.ToData().Version, Is.EqualTo(3));
            var historical = HistoricalCatalogTestData.VersionTwo(); historical.Album[0].FirstWeapon = StructureKind.Fan;
            Assert.That(() => WorkshopProfile.FromData(historical), Throws.InstanceOf<ArgumentException>());
        }
        [Test] public void ReadViewCopiesConeGeometryAndTargetCapIntoReadonlyValues()
        {
            var balance = GameBalance.CreateDefault(); balance.Structure(StructureKind.Fan).ConeAngleDegrees = 55;
            balance.Structure(StructureKind.Fan).MaxTargets = 2;
            var read = new RunReadModel(new GameSession(balance, starterDefenses: false));
            StructureSpec spec = read.Balance.Structure(StructureKind.Fan);
            balance.Structure(StructureKind.Fan).ConeAngleDegrees = 120; balance.Structure(StructureKind.Fan).MaxTargets = 9;
            Assert.That(spec.ConeAngleDegrees, Is.EqualTo(55)); Assert.That(spec.MaxTargets, Is.EqualTo(2));
            Assert.That(typeof(StructureSpec).GetField(nameof(StructureSpec.ConeAngleDegrees)).IsInitOnly, Is.True);
            Assert.That(typeof(StructureSpec).GetField(nameof(StructureSpec.MaxTargets)).IsInitOnly, Is.True);
        }
        [Test] public void WaveReadViewExposesHistoricalFallbackManualRewardAndTargetPattern()
        {
            var balance = GameBalance.CreateDefault(); var read = new RunReadModel(new GameSession(balance, starterDefenses: false));
            Assert.That(read.Balance.Waves[0].InkGrant, Is.EqualTo(-1));
            balance.Waves[0].InkGrant = 321; Assert.That(read.Balance.Waves[0].InkGrant, Is.EqualTo(321));
            var target = new RunReadModel(new GameSession(GameBalancePresets.CreateTargetWorkshop(), starterDefenses: false));
            Assert.That(target.Balance.Waves[0].InkGrant, Is.EqualTo(140));
        }
    }
}
