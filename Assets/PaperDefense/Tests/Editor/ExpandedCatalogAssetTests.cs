using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Editor;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class ExpandedCatalogAssetTests
    {
        [Test] public void ExpansionOnlyAppendsMissingNewWeaponsAndPreservesEveryExistingSetting()
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                var gun = new GameBalanceAsset.WeaponSettings { kind = StructureKind.Gun, cost = 83, range = 19, damage = 7, shotInterval = .7f };
                var ricochet = new GameBalanceAsset.WeaponSettings { kind = StructureKind.Ricochet, cost = 91, jumpRange = 7, maxTargets = 2 };
                asset.weapons = new[] { gun, ricochet };
                asset.startingInk = 123; asset.workshopStartingInk = 987; asset.passiveInkPerSecond = 9;
                asset.useTargetWorkshopPreset = true; asset.targetSettings.WaveInkBase = 777;
                asset.construction.StandardCapacity = 19; asset.construction.RailRangeMultiplier = 1.9f;
                var construction = asset.construction; var targetSettings = asset.targetSettings;
                asset.waves = new[] { new GameBalanceAsset.WaveSettings { count = 321, inkGrant = 456 } };
                var waves = asset.waves;
                string originalGun = JsonUtility.ToJson(gun), originalRicochet = JsonUtility.ToJson(ricochet);

                Assert.That(ExpandedCatalogAssetTools.EnsureExpandedCatalog(asset), Is.EqualTo(3));
                Assert.That(asset.weapons.Select(w => w.kind), Is.EqualTo(new[] { StructureKind.Gun, StructureKind.Ricochet, StructureKind.Fan, StructureKind.Stapler, StructureKind.Blotter }));
                Assert.That(asset.weapons[0], Is.SameAs(gun)); Assert.That(asset.weapons[1], Is.SameAs(ricochet));
                Assert.That(JsonUtility.ToJson(gun), Is.EqualTo(originalGun)); Assert.That(JsonUtility.ToJson(ricochet), Is.EqualTo(originalRicochet));
                Assert.That(asset.weapons.Single(w => w.kind == StructureKind.Fan).coneAngleDegrees, Is.EqualTo(90));
                Assert.That(asset.weapons.Single(w => w.kind == StructureKind.Stapler).slowSpeedMultiplier, Is.EqualTo(.8f));
                Assert.That(asset.weapons.Single(w => w.kind == StructureKind.Blotter).damage, Is.Zero);
                Assert.That(asset.startingInk, Is.EqualTo(123)); Assert.That(asset.workshopStartingInk, Is.EqualTo(987));
                Assert.That(asset.passiveInkPerSecond, Is.EqualTo(9)); Assert.That(asset.useTargetWorkshopPreset, Is.True);
                Assert.That(asset.construction, Is.SameAs(construction)); Assert.That(asset.construction.StandardCapacity, Is.EqualTo(19));
                Assert.That(asset.construction.RailRangeMultiplier, Is.EqualTo(1.9f));
                Assert.That(asset.targetSettings, Is.SameAs(targetSettings)); Assert.That(asset.targetSettings.WaveInkBase, Is.EqualTo(777));
                Assert.That(asset.waves, Is.SameAs(waves)); Assert.That(asset.waves[0].count, Is.EqualTo(321)); Assert.That(asset.waves[0].inkGrant, Is.EqualTo(456));
                var expanded = asset.weapons;
                Assert.That(ExpandedCatalogAssetTools.EnsureExpandedCatalog(asset), Is.Zero);
                Assert.That(asset.weapons, Is.SameAs(expanded));
            }
            finally { Object.DestroyImmediate(asset); }
        }
    }
}
