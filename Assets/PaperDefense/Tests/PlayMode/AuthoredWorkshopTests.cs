using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    // Saved authored profiles remain readable after their interactive editor retires.
    public sealed class AuthoredWorkshopTests
    {
        GameObject root;
        RunController run;
        string folder, path;
        [SetUp] public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "PaperAuthoredUI-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); path = Path.Combine(folder, "profile.json");
            CreateSession();
        }
        void CreateSession(GameBalanceAsset asset = null)
        {
            root = new GameObject("Isolated historical authored profile"); root.SetActive(false);
            run = root.AddComponent<RunController>(); run.balanceAsset = asset;
            run.InitializeWorkshop(new WorkshopProfileStore(path), allowLegacyFixtureDefaults: asset == null);
        }
        [TearDown] public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
            string absolute = Path.GetFullPath(folder);
            Assert.That(absolute.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(absolute).StartsWith("PaperAuthoredUI-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
        }
        TowerBlueprint HistoricalPage()
        {
            var page = new TowerBlueprint("historical-authored", "Imported two barrels", ChassisKind.Standard,
                StructureKind.Gun, StructureKind.Gun).WithContent(run.Content).AsFreeform();
            var session = new DrawingEditSession(page, page.Drawing);
            Assert.That(session.AddStroke(DrawingPartKey.Mount0, new[] {
                new DrawingPoint(-.03f,.03f), new DrawingPoint(.02f,.24f), new DrawingPoint(-.08f,.42f) }, out _), Is.True);
            Assert.That(session.AddStroke(DrawingPartKey.Mount1, new[] {
                new DrawingPoint(.04f,.01f), new DrawingPoint(.12f,.18f), new DrawingPoint(.07f,.39f) }, out _), Is.True);
            FreeformUiTestDrawing.Complete(session);
            return page.WithFreeformDrawing(session.Snapshot);
        }
        void SaveAndAssign(TowerBlueprint page)
        {
            Assert.That(run.SaveBlueprint(page, out string reason), Is.True, reason);
            Assert.That(run.AssignBlueprint(0, page.Id, out reason), Is.True, reason);
        }
        [Test] public void BlankHistoricalPageStaysDraftAndDoesNotOverwriteLegacyPage()
        {
            int count = run.Workshop.Album.Count;
            var blank = new TowerBlueprint("blank-import", "Blank", ChassisKind.Standard, StructureKind.Gun).AsFreeform();
            Assert.That(run.SaveBlueprint(blank, out string reason), Is.False);
            Assert.That(run.Workshop.Album.Count, Is.EqualTo(count));
            Assert.That(blank.Origin, Is.EqualTo(DrawingOrigin.Freeform));
            Assert.That(TextResourcesAsset.Default.Resolve(reason), Does.Contain("корпус"));
        }
        [Test] public void HistoricalStrokesSurviveAlbumReloadAndSeveralFieldCopies()
        {
            var draft = HistoricalPage();
            var drawing = draft.Drawing;
            Assert.That(drawing.GetPart(DrawingPartKey.Mount0).Strokes[0].Points[1].X,
                Is.Not.EqualTo(drawing.GetPart(DrawingPartKey.Mount1).Strokes[0].Points[1].X));
            SaveAndAssign(draft); string id = draft.Id;
            Assert.That(File.Exists(path), Is.True);
            var loaded = new WorkshopProfileStore(path).Load();
            Assert.That(loaded.Source, Is.EqualTo(ProfileLoadSource.Primary));
            Assert.That(loaded.Profile.Album.Single(p => p.Id == id).Drawing.GeometryHash, Is.EqualTo(drawing.GeometryHash));
            run.InitializeWorkshop(new WorkshopProfileStore(path), allowLegacyFixtureDefaults: true);
            Assert.That(run.StartRun(out _), Is.True);
            Assert.That(run.BuildBlueprint(new[] { new Cell(20, 20), new Cell(22, 20) }, 0, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
            var first = run.Session.StructureAt(new Cell(20, 20)).Value;
            var second = run.Session.StructureAt(new Cell(22, 20)).Value;
            Assert.That(first.Id, Is.Not.EqualTo(second.Id));
            Assert.That(first.Blueprint.Drawing.GeometryHash, Is.EqualTo(drawing.GeometryHash));
            Assert.That(second.Blueprint.Drawing.GeometryHash, Is.EqualTo(drawing.GeometryHash));
            run.OpenWorkshop(); draft = draft.WithWeaponMount(0, "weapon.piercer");
            Assert.That(first.Blueprint.FirstWeapon, Is.EqualTo(StructureKind.Gun));
            Assert.That(first.Blueprint.Drawing.GeometryHash, Is.EqualTo(drawing.GeometryHash));
        }
        [Test] public void HistoricalProfileRestartPreservesProgressAndSharesQualityWithTrialAndBattle()
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.useTargetWorkshopPreset = true;
                asset.construction.SecondaryBuildSeconds = 0; asset.planningSeconds = 1000;
                asset.weapons = new[] { new GameBalanceAsset.WeaponSettings { kind = StructureKind.Gun, cost = 40,
                    health = 55, buildSeconds = 0, range = 200, damage = 0, shotInterval = .42f } };
                asset.waves = new[] { new GameBalanceAsset.WaveSettings { count = 1, sides = 1 } };
                var progress = new WorkshopProfile(); progress.GrantGold(500);
                Assert.That(progress.Unlock("chassis.heavy", out _), Is.True); Assert.That(progress.Unlock("module.feed", out _), Is.True);
                Assert.That(progress.Unlock("module.rail", out _), Is.True);
                string[] oldPages = progress.Album.Select(p => p.Id).ToArray();
                Assert.That(new WorkshopProfileStore(path).Save(progress).Success, Is.True);
                UnityEngine.Object.DestroyImmediate(root); CreateSession(asset);

                var draft = HistoricalPage(); SaveAndAssign(draft); string id = draft.Id;
                string hash = draft.Drawing.GeometryHash;
                float quality = draft.Assessment.Quality; var beforeRestart = run.PreviewBlueprint(draft);
                Assert.That(draft.FreeformDesign.IsComplete, Is.True);
                var oldRun = run;
                UnityEngine.Object.DestroyImmediate(root);
                Assert.That(oldRun == null, Is.True, "The old controller must actually be destroyed.");
                CreateSession(asset);
                Assert.That(ReferenceEquals(run, oldRun), Is.False);
                Assert.That(run.Workshop.Gold, Is.EqualTo(290));
                Assert.That(run.Workshop.IsUnlocked("chassis.heavy") && run.Workshop.IsUnlocked("module.feed") && run.Workshop.IsUnlocked("module.rail"), Is.True);
                Assert.That(oldPages.All(old => run.Workshop.Album.Any(p => p.Id == old)), Is.True);
                Assert.That(run.Workshop.Loadout[0].Id, Is.EqualTo(id));
                draft = run.Workshop.Album.Single(p => p.Id == id);
                Assert.That(draft.Drawing.GeometryHash, Is.EqualTo(hash));
                Assert.That(draft.Drawing.GetPart(DrawingPartKey.Mount0).Strokes[0].Points[1].X,
                    Is.Not.EqualTo(draft.Drawing.GetPart(DrawingPartKey.Mount1).Strokes[0].Points[1].X));
                var preview = run.PreviewBlueprint(draft);
                Assert.That(preview.DrawingQuality, Is.EqualTo(quality));
                byte[] saved = File.ReadAllBytes(path); int ink = run.Session.Ink; long tick = run.Session.TickIndex;
                var trial = run.RunTrial(draft, TrialScenario.Crowd);
                Assert.That(trial.First.DrawingQuality, Is.EqualTo(quality));
                Assert.That(run.Session.Ink, Is.EqualTo(ink)); Assert.That(run.Session.TickIndex, Is.EqualTo(tick));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(saved)); Assert.That(run.Workshop.Gold, Is.EqualTo(290));

                Assert.That(run.StartRun(out string reason), Is.True, reason);
                var cells = new[] { new Cell(20, 20), new Cell(22, 20), new Cell(24, 20) };
                int beforeBuild = run.Session.Ink;
                Assert.That(run.BuildBlueprint(cells, 0, MaterialTier.Pencil, InkColor.None, out reason), Is.True, reason);
                Assert.That(beforeBuild - run.Session.Ink, Is.EqualTo(preview.Cost * 3));
                for (int i = 0; i < 30; i++) run.AdvanceFrame(1f / 30, false, 1);
                Assert.That(cells.Select(c => run.Session.StructureAt(c).Value.Id).Distinct().Count(), Is.EqualTo(3));
                Assert.That(cells.All(c => run.Session.StructureAt(c).Value.IsComplete), Is.True);
                int firstId = run.Session.StructureAt(cells[0]).Value.Id; float observedCooldown = -1;
                run.SimulationEventReceived += item =>
                {
                    if (item.Kind == SimulationEventKind.Shot && item.EntityId == firstId && observedCooldown < 0)
                        observedCooldown = run.Session.StructureAt(cells[0]).Value.Cooldown;
                };
                run.NextWave(); for (int i = 0; i < 12; i++) run.AdvanceFrame(1f / 30, false, 1);
                Assert.That(observedCooldown, Is.EqualTo(TowerCatalog.ActionInterval(draft, asset.CreateWorkshopBalance(), draft.FirstWeaponId, preview.FeedMultiplier, 0)).Within(.00001f));
                var deployedBattle = run.Session;
                run.OpenWorkshop(); draft = draft.WithWeaponMount(0, "weapon.piercer");
                Assert.That(run.SaveBlueprint(draft, out reason), Is.True, reason);
                Assert.That(run.Workshop.Album.Single(p => p.Id == id).FirstWeapon, Is.EqualTo(StructureKind.Piercer));
                foreach (Cell cell in cells)
                {
                    TowerBlueprint deployed = deployedBattle.StructureAt(cell).Value.Blueprint;
                    Assert.That(deployed.FirstWeapon, Is.EqualTo(StructureKind.Gun)); Assert.That(deployed.Drawing.GeometryHash, Is.EqualTo(hash));
                    Assert.That(deployed.Assessment.Quality, Is.EqualTo(quality));
                }
                Assert.That(new WorkshopProfileStore(path).Load().Profile.Album.Single(p => p.Id == id).FirstWeapon, Is.EqualTo(StructureKind.Piercer));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }
        [Test] public void HistoricalVariantKeepsOriginalPageAndOtherMountStrokes()
        {
            var original = HistoricalPage(); SaveAndAssign(original);
            string originalHash = original.Drawing.GeometryHash;
            var variant = original.AsNew("historical-variant", "Historical variant");
            Assert.That(run.SaveBlueprint(variant, out string reason), Is.True, reason);
            Assert.That(variant.Id, Is.Not.EqualTo(original.Id));
            Assert.That(run.Workshop.Album.Single(p => p.Id == original.Id).Drawing.GeometryHash, Is.EqualTo(originalHash));
            variant = variant.WithWeaponMount(0, "weapon.piercer");
            Assert.That(variant.Drawing.GetPart(DrawingPartKey.Mount0).Strokes.Count, Is.EqualTo(1), "Changing ammunition preserves the invented barrel contour.");
            Assert.That(variant.Drawing.GetPart(DrawingPartKey.Mount1).Strokes.Count, Is.EqualTo(1));
        }
    }
}
