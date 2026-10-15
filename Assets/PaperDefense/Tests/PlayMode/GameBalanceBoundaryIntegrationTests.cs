using System;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class GameBalanceBoundaryIntegrationTests
    {
        [TestCase("base health")]
        [TestCase("weapon damage")]
        [TestCase("material refund")]
        [TestCase("enemy speed")]
        [TestCase("wave interval")]
        public void AssetRejectsNonfiniteInputAfterItsEstablishedAdaptation(string field)
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.ResetToDefaults();
                switch (field)
                {
                    case "base health": asset.baseHealth = float.NaN; break;
                    case "weapon damage": asset.weapons[1].damage = float.NaN; break;
                    case "material refund": asset.materials[0].refund = float.NaN; break;
                    case "enemy speed": asset.enemies[0].speed = float.PositiveInfinity; break;
                    case "wave interval": asset.waves[0].spawnInterval = float.NaN; break;
                }
                Assert.That(() => asset.CreateBalance(), Throws.InstanceOf<ArgumentException>());
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void AssetKeepsFiniteClampsDuplicateOverrideOrderAndPresetPrecedence()
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.ResetToDefaults(); asset.width = 3; asset.height = 3; asset.startingInk = -1;
                asset.planningSeconds = -7;
                asset.weapons = new[]
                {
                    new GameBalanceAsset.WeaponSettings { kind = StructureKind.Gun, cost = 41, damage = 2 },
                    new GameBalanceAsset.WeaponSettings { kind = StructureKind.Gun, cost = 63, damage = 17 }
                };
                asset.workshopStartingInk = 555; asset.useTargetWorkshopPreset = true;
                asset.construction.ArmorDefense = .1f; asset.targetSettings.ArmorDefense = .35f;
                asset.waves[0].inkGrant = 321;
                var historical = asset.CreateBalance(); var target = asset.CreateWorkshopBalance();
                Assert.That(historical.Width, Is.EqualTo(20)); Assert.That(historical.Height, Is.EqualTo(16));
                Assert.That(historical.StartingInk, Is.Zero); Assert.That(historical.PlanningSeconds, Is.Zero);

                Assert.That(historical.Structure(StructureKind.Gun).Cost, Is.EqualTo(63));
                Assert.That(historical.Structure(StructureKind.Gun).Damage, Is.EqualTo(17));
                Assert.That(historical.Construction.ArmorDefense, Is.EqualTo(.1f));
                Assert.That(target.Width, Is.EqualTo(96)); Assert.That(target.Height, Is.EqualTo(64));
                Assert.That(target.StartingInk, Is.EqualTo(555)); Assert.That(target.Construction.ArmorDefense, Is.EqualTo(.35f));
                Assert.That(target.Waves[0].InkGrant, Is.EqualTo(321)); Assert.That(target.Waves[1].InkGrant, Is.EqualTo(160));
                Assert.That(asset.construction.ArmorDefense, Is.EqualTo(.1f));
            }
            finally { Object.DestroyImmediate(asset); }
        }
    }
}
