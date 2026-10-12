using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class EffectsRulesIntegrationTests
    {
        [Test] public void AssetEffectRulesReachTrialAndSpawnAndAreOwnedByTheRun()
        {
            var root = new GameObject("Isolated effects application"); root.SetActive(false);
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.generateNewThreats = false; asset.planningSeconds = 1000; asset.passiveInkPerSecond = 0;
                asset.effects.HatchedHealthMultiplier = 3;
                asset.effects.HatchedDefense = .7f; asset.effects.GreenDefenseReduction = .5f;
                asset.weapons = new[] { new GameBalanceAsset.WeaponSettings { kind = StructureKind.Gun,
                    cost = 40, health = 55, buildSeconds = 0, range = 100, damage = 10, shotInterval = 1000 } };
                asset.enemies = new[] { new GameBalanceAsset.EnemySettings { kind = EnemyKind.Doodle,
                    health = 10, speed = 1, damage = 0, attackInterval = 1, reward = 0 } };
                asset.waves = new[] { new GameBalanceAsset.WaveSettings { count = 1, hatched = 1, sides = 1 } };
                var run = root.AddComponent<RunController>(); run.balanceAsset = asset; run.InitializeWorkshop(false);
                var blueprint = new TowerBlueprint("effects", "Configured effects", ChassisKind.Standard, StructureKind.Gun);
                var trial = run.RunTrial(blueprint, TrialScenario.Armored, budget: 40);
                Assert.That(trial.First.Damage, Is.EqualTo(3).Within(.0001f), "The colorless trial uses configured 70% defense.");
                Assert.That(run.StartRun(out var reason), Is.True, reason);
                asset.effects.HatchedHealthMultiplier = 9; asset.effects.HatchedDefense = 0;
                run.NextWave(); run.AdvanceFrame(1f / 30, false, 1);
                Assert.That(run.Session.Enemies.Count, Is.EqualTo(1));
                Assert.That(run.Session.Enemies[0].MaxHealth, Is.EqualTo(30));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(asset); }
        }
    }
}
