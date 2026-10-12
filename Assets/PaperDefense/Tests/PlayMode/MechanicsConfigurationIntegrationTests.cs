using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class MechanicsConfigurationIntegrationTests
    {
        static void DisableAll(MechanicsConfiguration mechanics)
        {
            mechanics.EnemyDrawingTiers = mechanics.CorpseBlots = mechanics.RangedEnemies = false;
            mechanics.InkWells = mechanics.ColorCartridges = mechanics.RunUpgrades = mechanics.DrawingQuality = false;
        }

        static void AssertDisabled(MechanicsAvailability mechanics)
        {
            Assert.That(mechanics.EnemyDrawingTiers, Is.False); Assert.That(mechanics.CorpseBlots, Is.False);
            Assert.That(mechanics.RangedEnemies, Is.False); Assert.That(mechanics.InkWells, Is.False);
            Assert.That(mechanics.ColorCartridges, Is.False); Assert.That(mechanics.RunUpgrades, Is.False);
            Assert.That(mechanics.DrawingQuality, Is.False);
        }

        static TowerBlueprint AuthoredGun() => new TowerBlueprint("integration.authored", "Только декор", ChassisKind.Standard,
            StructureKind.Gun, drawing: new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Decoration, new[] {
                new DrawingStroke(new[] { new DrawingPoint(-.8f, -.8f), new DrawingPoint(-.7f, -.7f) }) }) }),
            origin: DrawingOrigin.PlayerDrawing);

        [TestCase(false)]
        [TestCase(true)]
        public void AssetSnapshotsDeveloperMechanicsAndTheTargetPresetDoesNotReenableThem(bool target)
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.enemyMechanics = asset.inkWells = true; asset.useTargetWorkshopPreset = target;
                DisableAll(asset.mechanics);
                var ordinary = asset.CreateBalance(); var workshop = asset.CreateWorkshopBalance();
                Assert.That(ordinary.Mechanics, Is.Not.SameAs(asset.mechanics));
                Assert.That(workshop.Mechanics, Is.Not.SameAs(asset.mechanics));
                Assert.That(workshop.Mechanics, Is.Not.SameAs(ordinary.Mechanics));
                AssertDisabled(MechanicsAvailability.Resolve(ordinary));
                AssertDisabled(MechanicsAvailability.Resolve(workshop));
                asset.mechanics.EnemyDrawingTiers = asset.mechanics.CorpseBlots = asset.mechanics.RangedEnemies = true;
                asset.mechanics.InkWells = asset.mechanics.ColorCartridges = asset.mechanics.RunUpgrades = asset.mechanics.DrawingQuality = true;
                AssertDisabled(MechanicsAvailability.Resolve(ordinary));
                AssertDisabled(MechanicsAvailability.Resolve(workshop));
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void LegacyAssetWithoutTheNewConfigurationReceivesEnabledMechanicsDefaults()
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.mechanics = null; asset.enemyMechanics = asset.inkWells = true;
                var result = MechanicsAvailability.Resolve(asset.CreateWorkshopBalance());
                Assert.That(result.EnemyDrawingTiers, Is.True); Assert.That(result.CorpseBlots, Is.True);
                Assert.That(result.RangedEnemies, Is.True); Assert.That(result.InkWells, Is.True);
                Assert.That(result.ColorCartridges, Is.True); Assert.That(result.RunUpgrades, Is.True); Assert.That(result.DrawingQuality, Is.True);
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void ResetToDefaultsRestoresEveryDeveloperMechanicFlag()
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                DisableAll(asset.mechanics); asset.ResetToDefaults();
                var result = MechanicsAvailability.Resolve(asset.CreateWorkshopBalance());
                Assert.That(result.EnemyDrawingTiers, Is.True); Assert.That(result.CorpseBlots, Is.True);
                Assert.That(result.RangedEnemies, Is.True); Assert.That(result.InkWells, Is.True);
                Assert.That(result.ColorCartridges, Is.True); Assert.That(result.RunUpgrades, Is.True); Assert.That(result.DrawingQuality, Is.True);
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void WorkshopStartCapturesTheLatestDeveloperConfigurationWithoutChangingAlbumPages()
        {
            var root = new GameObject("Isolated mechanics workshop"); root.SetActive(false);
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.enemyMechanics = asset.inkWells = true; asset.generateNewThreats = false;
                var run = root.AddComponent<RunController>(); run.balanceAsset = asset; run.InitializeWorkshop(false);
                var firstPage = run.Workshop.Album[0]; int pages = run.Workshop.Album.Count;
                var profileField = typeof(RunController).GetField("profile", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(profileField, Is.Not.Null);
                var profile = (WorkshopProfile)profileField.GetValue(run);
                var storedPage = profile.Album[0];
                DisableAll(asset.mechanics);
                Assert.That(run.StartRun(out string reason), Is.True, reason);
                AssertDisabled(run.Session.Balance.Mechanics);
                Assert.That(run.Workshop.Album.Count, Is.EqualTo(pages));
                Assert.That(profile.Album[0], Is.SameAs(storedPage));
                var prepared = run.Workshop.Loadout[0];
                Assert.That(run.Workshop.Album[0].Id, Is.EqualTo(firstPage.Id));
                Assert.That(prepared.Id, Is.EqualTo(firstPage.Id));
                Assert.That(prepared.ChassisId, Is.EqualTo(firstPage.ChassisId));
                Assert.That(prepared.FirstWeaponId, Is.EqualTo(firstPage.FirstWeaponId));
                Assert.That(prepared.SecondWeaponId, Is.EqualTo(firstPage.SecondWeaponId));
                Assert.That(prepared.Drawing.GeometryHash, Is.EqualTo(firstPage.Drawing.GeometryHash));
                Assert.That(prepared.EvaluationStamp.RuleVersion, Is.EqualTo(firstPage.EvaluationStamp.RuleVersion));
                Assert.That(prepared.EvaluationStamp.ReferenceSignature, Is.EqualTo(firstPage.EvaluationStamp.ReferenceSignature));
                Assert.That(prepared.EvaluationStamp.ConstructionHash, Is.EqualTo(firstPage.EvaluationStamp.ConstructionHash));
                Assert.That(prepared.EvaluationStamp.Quality, Is.EqualTo(firstPage.EvaluationStamp.Quality));
                Assert.That(prepared.Content, Is.SameAs(run.Content));
                var loadoutField = typeof(RunController).GetField("runLoadout", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(loadoutField, Is.Not.Null);
                var actualLoadout = (System.Collections.Generic.IReadOnlyList<TowerBlueprint>)loadoutField.GetValue(run);
                Assert.That(actualLoadout[0].Content, Is.SameAs(run.Content));
                Assert.That(actualLoadout[0].Drawing.GeometryHash, Is.EqualTo(storedPage.Drawing.GeometryHash));
                Assert.That(run.Session.Wells, Is.Empty);
                float frozenRange = run.Content.GetWeapon(prepared.FirstWeaponId).Stats.Range;
                asset.weapons = new[] { new GameBalanceAsset.WeaponSettings { kind = StructureKind.Gun, cost = 40,
                    health = 55, range = frozenRange + 19, damage = 12, shotInterval = .42f } };
                asset.mechanics.InkWells = asset.mechanics.RunUpgrades = true;
                Assert.That(run.Session.Balance.Mechanics.InkWells, Is.False);
                Assert.That(run.Session.Balance.Mechanics.RunUpgrades, Is.False);
                Assert.That(run.Content.GetWeapon(prepared.FirstWeaponId).Stats.Range, Is.EqualTo(frozenRange));
                Assert.That(actualLoadout[0].Content.GetWeapon(prepared.FirstWeaponId).Stats.Range, Is.EqualTo(frozenRange));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(asset); }
        }

        [Test]
        public void WorkshopPreviewAndQualityTrialUseTheDisabledQualityRule()
        {
            var root = new GameObject("Isolated neutral quality workshop"); root.SetActive(false);
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.mechanics.DrawingQuality = false; asset.generateNewThreats = false;
                var run = root.AddComponent<RunController>(); run.balanceAsset = asset; run.InitializeWorkshop(false);
                var blueprint = AuthoredGun(); var preview = run.PreviewBlueprint(blueprint);
                Assert.That(preview.DrawingQuality, Is.Zero); Assert.That(preview.QualityMultiplier, Is.EqualTo(1));
                var trial = run.RunQualityTrial(blueprint, TrialScenario.Fast, budget: 40);
                Assert.That(trial, Is.Not.Null);
                Assert.That(trial.First.QualityMultiplier, Is.EqualTo(1)); Assert.That(trial.Second.QualityMultiplier, Is.EqualTo(1));
                Assert.That(trial.First.Damage, Is.EqualTo(trial.Second.Damage).Within(.0001f));
                Assert.That(trial.First.Leaks, Is.EqualTo(trial.Second.Leaks));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(asset); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ControllerPreviewUsesFrozenSessionQualityAfterItsOwnedConfigurationChanges(bool enabled)
        {
            var root = new GameObject("Isolated preview mechanics snapshot"); root.SetActive(false);
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.mechanics.DrawingQuality = enabled;
                var run = root.AddComponent<RunController>(); run.balanceAsset = asset; run.InitializeWorkshop(false);
                var blueprint = AuthoredGun(); var before = run.PreviewBlueprint(blueprint);
                var sessionField = typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(sessionField, Is.Not.Null);
                var session = (GameSession)sessionField.GetValue(run);
                session.Balance.Mechanics.DrawingQuality = !enabled;
                var after = run.PreviewBlueprint(blueprint);
                Assert.That(after.QualityMultiplier, Is.EqualTo(enabled ? .85f : 1).Within(.0001f));
                Assert.That(after.QualityMultiplier, Is.EqualTo(before.QualityMultiplier));
                Assert.That(after.FireRate, Is.EqualTo(before.FireRate));
                Assert.That(run.Session.Balance.Mechanics.DrawingQuality, Is.EqualTo(enabled));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(asset); }
        }

        [Test]
        public void ControllerCompletesARealThreeWaveRunWhenEveryOptionalMechanicIsDisabled()
        {
            var root = new GameObject("Isolated all-disabled campaign"); root.SetActive(false);
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                DisableAll(asset.mechanics); asset.enemyMechanics = asset.inkWells = true;
                asset.generateNewThreats = false; asset.useTargetWorkshopPreset = true;
                asset.planningSeconds = asset.intermissionSeconds = 1000;
                asset.progression.UpgradeEveryWaves = 1; asset.progression.LastUpgradeWave = 99;
                asset.weapons = new[] { new GameBalanceAsset.WeaponSettings { kind = StructureKind.Gun, cost = 40,
                    health = 55, buildSeconds = 0, range = 100, damage = 1000, shotInterval = .05f } };
                asset.enemies = new[] { new GameBalanceAsset.EnemySettings { kind = EnemyKind.Doodle,
                    health = 1, speed = 0, damage = 0, attackInterval = 1, reward = 0 } };
                asset.waves = new[] {
                    new GameBalanceAsset.WaveSettings { count = 1, spawnInterval = .005f, sides = 1, spitters = 1, hatched = 1 },
                    new GameBalanceAsset.WaveSettings { count = 1, spawnInterval = .005f, sides = 1, spitters = 1, hatched = 1 },
                    new GameBalanceAsset.WaveSettings { count = 1, spawnInterval = .005f, sides = 1, spitters = 1, hatched = 1 } };
                var run = root.AddComponent<RunController>(); run.balanceAsset = asset; run.InitializeWorkshop(false);
                Assert.That(run.StartRun(out string reason), Is.True, reason);
                Assert.That(run.BuildBlueprint(new[] { new Cell(6, 6) }, 0, MaterialTier.Pencil, InkColor.None, out reason), Is.True, reason);
                for (int wave = 1; wave <= 3; wave++)
                {
                    run.NextWave();
                    for (int frame = 0; frame < 12; frame++) run.AdvanceFrame(FixedStepClock.Step, false, 1);
                    Assert.That(run.Session.WaveIndex, Is.EqualTo(wave));
                    Assert.That(run.Session.Kills, Is.EqualTo(wave));
                    Assert.That(run.Session.PendingUpgrade, Is.False);
                }
                Assert.That(run.Session.Phase, Is.EqualTo(RunPhase.Victory));
                Assert.That(run.Session.Wells, Is.Empty); Assert.That(run.Session.Blots, Is.Empty);
                Assert.That(run.Session.EnemyProjectiles, Is.Empty);
                Assert.That(run.Workshop.Gold, Is.GreaterThan(0), "Permanent reward delivery remains independent of run upgrades.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(asset); }
        }
    }
}
