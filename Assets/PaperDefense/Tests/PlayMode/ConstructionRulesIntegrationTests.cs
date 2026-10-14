using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class ConstructionRulesIntegrationTests
    {
        readonly List<Object> owned = new List<Object>();
        [TearDown] public void TearDown() { foreach (var item in owned) Object.DestroyImmediate(item); owned.Clear(); }
        GameBalanceAsset Asset()
        { var asset = ScriptableObject.CreateInstance<GameBalanceAsset>(); owned.Add(asset); asset.ResetToDefaults(); return asset; }
        RunController Controller(GameBalanceAsset asset, WorkshopProfile profile = null)
        {
            var root = new GameObject("Construction rules controller"); root.SetActive(false); owned.Insert(0, root);
            var run = root.AddComponent<RunController>(); run.balanceAsset = asset;
            if (profile == null) run.InitializeWorkshop(false); else run.InitializeWorkshop(new MemoryStore(profile));
            return run;
        }
        [Test] public void AssetConstructionRulesAreOwnedAndSharedByPreviewTrialAndBattle()
        {
            var asset = Asset(); asset.construction.StandardBaseCost = 26;
            asset.construction.FoundationHealth = 123; asset.construction.GunLoad = 3;
            asset.construction.StandardCapacity = 12; asset.construction.DualFeedMultiplier = .5f;
            var run = Controller(asset); var page = run.Workshop.Loadout[1]; var preview = run.PreviewBlueprint(page);
            Assert.That(preview.Load, Is.EqualTo(6)); Assert.That(preview.Capacity, Is.EqualTo(12));
            Assert.That(preview.Cost, Is.EqualTo(74)); Assert.That(preview.Health, Is.EqualTo(123));
            Assert.That(preview.FeedMultiplier, Is.EqualTo(.5f));
            var trial = run.RunTrial(page, TrialScenario.Crowd).First;
            Assert.That(trial.Copies, Is.EqualTo(5)); Assert.That(trial.Cost, Is.EqualTo(370));
            Assert.That(trial.Frames[0].Towers[0].MaxHealth, Is.EqualTo(123));
            Assert.That(run.StartRun(out string reason), Is.True, reason);
            int before = run.Session.Ink; var cell = new Cell(12, 12);
            Assert.That(run.BuildBlueprint(new[] { cell }, 1, MaterialTier.Pencil, InkColor.None, out reason), Is.True, reason);
            Assert.That(before - run.Session.Ink, Is.EqualTo(74));
            Assert.That(run.Session.StructureAt(cell).Value.MaxHealth, Is.EqualTo(123));
        }
        [Test] public void CurrentCapacityReachesSaveSlotTrialAndBattleQuote()
        {
            var asset = Asset(); asset.construction.StandardCapacity = 20;
            var run = Controller(asset);
            var page = new TowerBlueprint("wide", "Два разных ствола", ChassisKind.Standard, StructureKind.Gun, StructureKind.Mortar);
            Assert.That(run.SaveBlueprint(page, out string reason), Is.True, reason);
            Assert.That(run.AssignBlueprint(0, page.Id, out reason), Is.True, reason);
            Assert.That(run.RunTrial(page, TrialScenario.Crowd), Is.Not.Null);
            Assert.That(run.StartRun(out reason), Is.True, reason);
            Assert.That(run.BuildBlueprint(new[] { new Cell(12, 12) }, 0, MaterialTier.Pencil, InkColor.None, out reason), Is.True, reason);
        }
        [Test] public void ConfiguredUnlockPriceIsThePriceShownAndChargedByTheController()
        {
            var asset = Asset(); asset.construction.HeavyUnlockGold = 7;
            var profile = new WorkshopProfile(); profile.GrantGold(10); var run = Controller(asset, profile);
            Assert.That(run.Workshop.Parts.Single(p => p.Id == "chassis.heavy").GoldCost, Is.EqualTo(7));
            Assert.That(run.UnlockPart("chassis.heavy", out string reason), Is.True, reason);
            Assert.That(run.Workshop.Gold, Is.EqualTo(3));
        }
        [Test] public void AssetRulesAndCoreSnapshotsDoNotShareMutableTuning()
        {
            var asset = Asset(); asset.construction.StandardCapacity = 17;
            var first = asset.CreateWorkshopBalance(); var second = first.CreateSnapshot();
            asset.construction.StandardCapacity = 22; first.Construction.StandardCapacity = 33;
            Assert.That(second.Construction.StandardCapacity, Is.EqualTo(17));
        }
        [Test] public void EnemyStructureDamageIsMappedClonedAndResetWithItsDefinition()
        {
            var asset = Asset();
            Assert.That(asset.enemies.Single(e => e.kind == EnemyKind.Gnawer).structureDamageMultiplier, Is.EqualTo(1.5f));
            Assert.That(asset.enemies.Single(e => e.kind == EnemyKind.Doodle).structureDamageMultiplier, Is.EqualTo(1));
            asset.enemies.Single(e => e.kind == EnemyKind.Gnawer).structureDamageMultiplier = 3;
            var balance = asset.CreateWorkshopBalance(); var snapshot = balance.CreateSnapshot();
            balance.Enemy(EnemyKind.Gnawer).StructureDamageMultiplier = 5;
            Assert.That(snapshot.Enemy(EnemyKind.Gnawer).StructureDamageMultiplier, Is.EqualTo(3));
            asset.ResetToDefaults();
            Assert.That(asset.enemies.Single(e => e.kind == EnemyKind.Gnawer).structureDamageMultiplier, Is.EqualTo(1.5f));
        }
        sealed class MemoryStore : IWorkshopProfileStore
        {
            WorkshopProfile profile;
            public MemoryStore(WorkshopProfile profile) { this.profile = profile; }
            public ProfileLoadResult Load() => new ProfileLoadResult(profile, ProfileLoadSource.Primary, "", true);
            public ProfileSaveResult Save(WorkshopProfile candidate) { profile = candidate; return new ProfileSaveResult(true, ""); }
        }
    }
}
