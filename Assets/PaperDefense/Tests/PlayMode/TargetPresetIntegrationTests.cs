using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class TargetPresetIntegrationTests
    {
        [Test] public void AssetChoosesTargetOnlyForWorkshopAndUsesConfiguredStartingInkAndTuning()
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.ResetToDefaults(); asset.workshopStartingInk = 555; asset.useTargetWorkshopPreset = true;
                asset.targetSettings.WaveInkBase = 111; asset.targetSettings.ArmorDefense = .35f;
                var historical = asset.CreateBalance(); var target = asset.CreateWorkshopBalance();
                Assert.That(historical.Preset, Is.EqualTo(GameBalancePreset.Historical)); Assert.That(historical.PassiveInkPerSecond, Is.EqualTo(1.8f));
                Assert.That(target.Preset, Is.EqualTo(GameBalancePreset.Target)); Assert.That(target.StartingInk, Is.EqualTo(555));
                Assert.That(target.Waves[0].InkGrant, Is.EqualTo(111)); Assert.That(target.Construction.ArmorDefense, Is.EqualTo(.35f));
                asset.useTargetWorkshopPreset = false;
                Assert.That(asset.CreateWorkshopBalance().Preset, Is.EqualTo(GameBalancePreset.Historical));
            }
            finally { Object.DestroyImmediate(asset); }
        }
        [Test] public void AssetMapsExplicitHistoricalWaveInkAndResetRestoresFallback()
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.ResetToDefaults(); asset.waves[0].inkGrant = 321;
                var balance = asset.CreateWorkshopBalance(); Assert.That(balance.Waves[0].InkGrant, Is.EqualTo(321));
                Assert.That(balance.CreateSnapshot().Waves[0].InkGrant, Is.EqualTo(321));
                asset.ResetToDefaults(); Assert.That(asset.waves[0].inkGrant, Is.EqualTo(-1));
            }
            finally { Object.DestroyImmediate(asset); }
        }
        [Test] public void NoAssetWorkshopUsesFiniteTargetAndArmorPreviewShowsDefense()
        {
            var root = new GameObject("Target no-asset controller"); root.SetActive(false);
            try
            {
                var run = root.AddComponent<RunController>(); run.InitializeWorkshop(false);
                var armor = run.Workshop.Album.Single(b => b.Id == "starter.mortar"); var preview = run.PreviewBlueprint(armor);
                Assert.That(preview.Defense, Is.EqualTo(.25f)); Assert.That(preview.Health, Is.EqualTo(55));
                Assert.That(run.StartRun(out string reason), Is.True, reason);
                int ink = run.Session.Ink;
                for (int i = 0; i < 30; i++) run.AdvanceFrame(FixedStepClock.Step, false, 1);
                Assert.That(run.Session.Ink, Is.EqualTo(ink), "Waiting in planning creates no target income.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
