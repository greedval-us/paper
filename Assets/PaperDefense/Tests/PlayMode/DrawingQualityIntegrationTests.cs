using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class DrawingQualityIntegrationTests
    {
        [Test] public void PreviewTrialAndBattleIgnoreRetiredQualityPenaltyWithoutSpendingProfile()
        {
            var root = new GameObject("Isolated quality application"); root.SetActive(false);
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.drawingMinimumRate = .9f; asset.drawingMaximumRate = 1;
                asset.passiveInkPerSecond = 0; asset.planningSeconds = 1000;
                asset.weapons = new[] { new GameBalanceAsset.WeaponSettings { kind = StructureKind.Gun, cost = 40,
                    health = 55, buildSeconds = 0, range = 200, damage = 0, shotInterval = .42f } };
                asset.waves = new[] { new GameBalanceAsset.WaveSettings { count = 1, sides = 1 } };
                var run = root.AddComponent<RunController>(); run.balanceAsset = asset; run.InitializeWorkshop(false);
                var blueprint = TowerBlueprint.NewDraft("quality", "Only decoration", ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun)
                    .WithDrawing(new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Decoration, new[] {
                        new DrawingStroke(new[] { new DrawingPoint(.7f, .7f), new DrawingPoint(.8f, .8f) }) }) }));
                Assert.That(run.SaveBlueprint(blueprint, out var reason), Is.True, reason);
                Assert.That(run.AssignBlueprint(0, blueprint.Id, out reason), Is.True, reason);
                var preview = run.PreviewBlueprint(blueprint); long tick = run.Session.TickIndex;
                int gold = run.Workshop.Gold, ink = run.Session.Ink;
                Assert.That(preview.DrawingQuality, Is.EqualTo(0));
                Assert.That(preview.QualityMultiplier, Is.EqualTo(1));
                Assert.That(preview.FireRate, Is.EqualTo(2 / (.42f / .8f)).Within(.00001f));
                var trial = run.RunQualityTrial(blueprint, TrialScenario.Crowd);
                Assert.That(trial.First.QualityMultiplier, Is.EqualTo(preview.QualityMultiplier));
                Assert.That(trial.Second.DrawingQuality, Is.EqualTo(100));
                Assert.That(trial.First.Frames[0].Towers[0].MaxHealth, Is.EqualTo(preview.Health));
                Assert.That(run.Session.TickIndex, Is.EqualTo(tick)); Assert.That(run.Session.Ink, Is.EqualTo(ink));
                Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
                Assert.That(run.StartRun(out reason), Is.True, reason);
                int before = run.Session.Ink;
                Assert.That(run.BuildBlueprint(new[] { new Cell(30, 20) }, 0, MaterialTier.Pencil, InkColor.None, out reason), Is.True, reason);
                Assert.That(before - run.Session.Ink, Is.EqualTo(preview.Cost));
                Assert.That(run.Session.Structures[0].MaxHealth, Is.EqualTo(preview.Health));
                Assert.That(run.Session.Structures[0].Blueprint.Drawing.GeometryHash, Is.EqualTo(blueprint.Drawing.GeometryHash));
                for (int i = 0; i < 30; i++) run.AdvanceFrame(1f / 30, false, 1);
                Assert.That(run.Session.Structures[0].IsComplete, Is.True);
                float observedCooldown = -1;
                run.SimulationEventReceived += item =>
                {
                    if (item.Kind == SimulationEventKind.Shot && observedCooldown < 0)
                        observedCooldown = run.Session.Structures[0].Cooldown;
                };
                run.NextWave();
                for (int i = 0; i < 12; i++) run.AdvanceFrame(1f / 30, false, 1);
                Assert.That(observedCooldown, Is.EqualTo(2 / preview.FireRate).Within(.00001f), "Real launch cooldown uses the same single final quality factor.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(asset); }
        }
    }
}
