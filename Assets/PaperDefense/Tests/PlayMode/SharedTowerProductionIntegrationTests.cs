using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class SharedTowerProductionIntegrationTests
    {
        [Test] public void ConfiguredSharedSupplyReachesPreviewTrialAndThePaidBattle()
        {
            var root = new GameObject("Isolated shared tower production"); root.SetActive(false);
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.useTargetWorkshopPreset = false;
                asset.construction.DynamicFeed = true; asset.construction.DynamicFeedCapacity = .6f;
                asset.generateNewThreats = false; asset.passiveInkPerSecond = 0; asset.planningSeconds = 1000;
                asset.weapons = new[] { new GameBalanceAsset.WeaponSettings { kind = StructureKind.Gun,
                    cost = 40, health = 55, buildSeconds = 0, range = 100, damage = 0, shotInterval = .42f } };
                asset.waves = new[] { new GameBalanceAsset.WaveSettings { count = 1, sides = 1 } };
                var run = root.AddComponent<RunController>(); run.balanceAsset = asset; run.InitializeWorkshop(false);
                var blueprint = new TowerBlueprint("supply", "Shared supply", ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun);
                Assert.That(run.SaveBlueprint(blueprint, out var reason), Is.True, reason);
                Assert.That(run.AssignBlueprint(0, blueprint.Id, out reason), Is.True, reason);
                var preview = run.PreviewBlueprint(blueprint);
                Assert.That(preview.FeedMultiplier, Is.EqualTo(.3f).Within(.000001f));
                Assert.That(preview.FireRate, Is.EqualTo(2f / 1.4f).Within(.00001f));
                int gold = run.Workshop.Gold, ink = run.Session.Ink; long tick = run.Session.TickIndex;
                var trial = run.RunTrial(blueprint, TrialScenario.Crowd, budget: 64);
                float trialCooldown = -1;
                foreach (var frame in trial.First.Frames)
                    if (frame.Towers[0].Cooldown > 0) { trialCooldown = frame.Towers[0].Cooldown; break; }
                Assert.That(trialCooldown, Is.InRange(1.25f, 1.4f), "Recorded readiness comes from the configured 1.4-second shared interval.");
                Assert.That(run.Workshop.Gold, Is.EqualTo(gold)); Assert.That(run.Session.Ink, Is.EqualTo(ink));
                Assert.That(run.Session.TickIndex, Is.EqualTo(tick));
                Assert.That(run.StartRun(out reason), Is.True, reason);
                int before = run.Session.Ink;
                Assert.That(run.BuildBlueprint(new[] { new Cell(30, 20) }, 0, MaterialTier.Pencil, InkColor.None, out reason), Is.True, reason);
                Assert.That(before - run.Session.Ink, Is.EqualTo(preview.Cost));
                for (int i = 0; i < 30; i++) run.AdvanceFrame(1f / 30, false, 1);
                Assert.That(run.Session.Structures[0].IsComplete, Is.True);
                float battleCooldown = -1;
                run.SimulationEventReceived += item =>
                { if (item.Kind == SimulationEventKind.Shot && battleCooldown < 0) battleCooldown = run.Session.Structures[0].Cooldown; };
                run.NextWave(); for (int i = 0; i < 12; i++) run.AdvanceFrame(1f / 30, false, 1);
                Assert.That(battleCooldown, Is.EqualTo(1.4f).Within(.00001f));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(asset); }
        }
    }
}
