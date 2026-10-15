using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class AuthoredWorkshopTests
    {
        GameObject root, events;
        RunController run;
        WorkshopHud hud;
        string folder, path;
        [SetUp] public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "PaperAuthoredUI-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); path = Path.Combine(folder, "profile.json");
            CreateSession();
            events = new GameObject("Isolated drawing events", typeof(EventSystem));
        }
        void CreateSession(GameBalanceAsset asset = null)
        {
            root = new GameObject("Isolated authored workshop"); root.SetActive(false);
            run = root.AddComponent<RunController>(); run.balanceAsset = asset;
            run.InitializeWorkshop(new WorkshopProfileStore(path), allowLegacyFixtureDefaults: asset == null);
            hud = root.AddComponent<WorkshopHud>(); hud.Initialize(run);
        }
        [TearDown] public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(events);
            string absolute = Path.GetFullPath(folder);
            Assert.That(absolute.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(absolute).StartsWith("PaperAuthoredUI-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
        }
        Button Find(string name) => root.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
        void Click(string name) => Find(name).onClick.Invoke();

        [Test] public void BlankNewPageStaysDraftAndDoesNotOverwriteLegacyPage()
        {
            int count = run.Workshop.Album.Count;
            Click("New blueprint"); Click("Save blueprint");
            Assert.That(run.Workshop.Album.Count, Is.EqualTo(count));
            Assert.That(hud.Draft.Origin, Is.EqualTo(DrawingOrigin.Freeform));
            Assert.That(hud.Status, Does.Contain("корпус"));
        }

        [Test] public void MouseStrokesSurviveEditorAlbumReloadAndSeveralFieldCopies()
        {
            Click("New blueprint"); Click("Second Gun"); Click("Open drawing editor");
            var editor = hud.DrawingEditor;
            // Pointer event handlers are the same path used by InputSystemUIInputModule.
            root.SetActive(true); Canvas.ForceUpdateCanvases();
            Stroke(editor.Canvas, DrawingPartKey.Mount0, new Vector2(-.03f, .03f), new Vector2(.02f, .24f), new Vector2(-.08f, .42f));
            Stroke(editor.Canvas, DrawingPartKey.Mount1, new Vector2(.04f, .01f), new Vector2(.12f, .18f), new Vector2(.07f, .39f));
            FreeformUiTestDrawing.Complete(hud.DrawingEditor.Session); Click("Apply drawing");
            var drawing = hud.Draft.Drawing;
            Assert.That(drawing.GetPart(DrawingPartKey.Mount0).Strokes[0].Points[1].X,
                Is.Not.EqualTo(drawing.GetPart(DrawingPartKey.Mount1).Strokes[0].Points[1].X));
            Click("Save blueprint"); string id = hud.Draft.Id;
            Click("Assign slot 0");
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
            run.OpenWorkshop(); Click("First Piercer");
            Assert.That(first.Blueprint.FirstWeapon, Is.EqualTo(StructureKind.Gun));
            Assert.That(first.Blueprint.Drawing.GeometryHash, Is.EqualTo(drawing.GeometryHash));
        }

        [Test] public void RealHudRestartPreservesPointerDrawingProgressAndSharesQualityWithTrialAndBattle()
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

                Click("New blueprint"); Click("Second Gun"); Click("Open drawing editor");
                root.SetActive(true); Canvas.ForceUpdateCanvases();
                Stroke(hud.DrawingEditor.Canvas, DrawingPartKey.Mount0, new Vector2(-.03f, .03f), new Vector2(.02f, .24f), new Vector2(-.08f, .42f));
                Stroke(hud.DrawingEditor.Canvas, DrawingPartKey.Mount1, new Vector2(.04f, .01f), new Vector2(.12f, .18f), new Vector2(.07f, .39f));
                FreeformUiTestDrawing.Complete(hud.DrawingEditor.Session); Click("Apply drawing"); Click("Save blueprint"); string id = hud.Draft.Id; Click("Assign slot 0");
                string hash = hud.Draft.Drawing.GeometryHash;
                float quality = hud.Draft.Assessment.Quality; var beforeRestart = run.PreviewBlueprint(hud.Draft);
                Assert.That(hud.Draft.FreeformDesign.IsComplete, Is.True);
                var oldRun = run; var oldHud = hud;
                UnityEngine.Object.DestroyImmediate(root);
                Assert.That(oldRun == null && oldHud == null, Is.True, "The old controller and HUD must actually be destroyed.");
                CreateSession(asset);
                Assert.That(ReferenceEquals(run, oldRun), Is.False); Assert.That(ReferenceEquals(hud, oldHud), Is.False);
                Assert.That(run.Workshop.Gold, Is.EqualTo(290));
                Assert.That(run.Workshop.IsUnlocked("chassis.heavy") && run.Workshop.IsUnlocked("module.feed") && run.Workshop.IsUnlocked("module.rail"), Is.True);
                Assert.That(oldPages.All(old => run.Workshop.Album.Any(p => p.Id == old)), Is.True);
                Assert.That(run.Workshop.Loadout[0].Id, Is.EqualTo(id));
                int albumIndex = run.Workshop.Album.ToList().FindIndex(p => p.Id == id); Click("Album drawing " + albumIndex);
                Assert.That(hud.Draft.Drawing.GeometryHash, Is.EqualTo(hash));
                Assert.That(hud.Draft.Drawing.GetPart(DrawingPartKey.Mount0).Strokes[0].Points[1].X,
                    Is.Not.EqualTo(hud.Draft.Drawing.GetPart(DrawingPartKey.Mount1).Strokes[0].Points[1].X));
                var preview = run.PreviewBlueprint(hud.Draft);
                Assert.That(preview.DrawingQuality, Is.EqualTo(quality));
                byte[] saved = File.ReadAllBytes(path); int ink = run.Session.Ink; long tick = run.Session.TickIndex;
                var trial = run.RunTrial(hud.Draft, TrialScenario.Crowd);
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
                Assert.That(observedCooldown, Is.EqualTo(TowerCatalog.ActionInterval(hud.Draft, asset.CreateWorkshopBalance(), hud.Draft.FirstWeaponId, preview.FeedMultiplier, 0)).Within(.00001f));
                var deployedBattle = run.Session;
                run.OpenWorkshop(); Click("First Piercer"); Click("Save blueprint");
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

        [Test] public void SaveAsNewKeepsOriginalPageAndEditingOneMountKeepsOtherStrokes()
        {
            Click("New blueprint"); Click("Second Gun"); Click("Open drawing editor");
            var e = hud.DrawingEditor;
            Assert.That(e.Session.AddStroke(DrawingPartKey.Mount0, new[] { new DrawingPoint(0, 0), new DrawingPoint(0, .4f) }, out _), Is.True);
            Assert.That(e.Session.AddStroke(DrawingPartKey.Mount1, new[] { new DrawingPoint(.1f, 0), new DrawingPoint(.1f, .4f) }, out _), Is.True);
            FreeformUiTestDrawing.Complete(hud.DrawingEditor.Session); Click("Apply drawing"); Click("Save blueprint");
            string originalId = hud.Draft.Id, originalHash = hud.Draft.Drawing.GeometryHash;
            Click("Save blueprint as new");
            Assert.That(hud.Draft.Id, Is.Not.EqualTo(originalId));
            Assert.That(run.Workshop.Album.Single(p => p.Id == originalId).Drawing.GeometryHash, Is.EqualTo(originalHash));
            Click("First Piercer");
            Assert.That(hud.Draft.Drawing.GetPart(DrawingPartKey.Mount0).Strokes.Count, Is.EqualTo(1), "Changing ammunition preserves the invented barrel contour.");
            Assert.That(hud.Draft.Drawing.GetPart(DrawingPartKey.Mount1).Strokes.Count, Is.EqualTo(1));
        }

        [Test] public void LeavingViewportCancelsUntilFreshPressAndOutsideEraserCannotChangeDrawing()
        {
            Click("New blueprint"); Click("Open drawing editor");
            root.SetActive(true); Canvas.ForceUpdateCanvases();
            var editor = hud.DrawingEditor; var canvas = editor.Canvas;
            string initial = editor.Session.Snapshot.GeometryHash;
            Assert.That(editor.Session.CanUndo, Is.False);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            pointer.position = canvas.LocalPointToScreen(new Vector2(.1f, .2f)); canvas.OnPointerDown(pointer);
            pointer.position = canvas.LocalPointToScreen(new Vector2(.1f, .3f)); canvas.OnDrag(pointer);
            pointer.position = canvas.LocalPointToScreen(new Vector2(.1f, 1.3f));
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(canvas.rectTransform, pointer.position, null), Is.False);
            canvas.OnDrag(pointer);
            pointer.position = canvas.LocalPointToScreen(new Vector2(.3f, .3f)); canvas.OnDrag(pointer);
            pointer.position = canvas.LocalPointToScreen(new Vector2(.4f, .3f)); canvas.OnDrag(pointer); canvas.OnPointerUp(pointer);
            Assert.That(editor.Session.Snapshot.GeometryHash, Is.EqualTo(initial), "Leaving the sheet cancels the whole pending gesture; re-entry cannot start a stroke without a fresh press.");
            Assert.That(editor.Session.Snapshot.StrokeCount, Is.Zero);
            Assert.That(editor.Session.CanUndo, Is.False);
            Assert.That(editor.Session.CanRedo, Is.False);
            Assert.That(canvas.HasPendingGesture, Is.False);
            pointer.position = canvas.LocalPointToScreen(new Vector2(.3f, .3f)); canvas.OnPointerDown(pointer);
            pointer.position = canvas.LocalPointToScreen(new Vector2(.4f, .3f)); canvas.OnDrag(pointer); canvas.OnPointerUp(pointer);
            var strokes = editor.Session.Snapshot.GetPart(DrawingPartKey.Base).Strokes;
            Assert.That(strokes.Count, Is.EqualTo(1), "Only the fresh press creates a stroke.");
            Assert.That(strokes[0].Points, Is.EqualTo(new[] { new DrawingPoint(.3f, .3f), new DrawingPoint(.4f, .3f) }));
            string freshStroke = editor.Session.Snapshot.GeometryHash;
            Assert.That(editor.Session.Undo(), Is.True);
            Assert.That(editor.Session.Snapshot.GeometryHash, Is.EqualTo(initial));
            Assert.That(editor.Session.CanUndo, Is.False, "The fresh gesture creates exactly one command.");
            Assert.That(editor.Session.Redo(), Is.True);
            Assert.That(editor.Session.Snapshot.GeometryHash, Is.EqualTo(freshStroke));
            // Imported local geometry may lie outside the current zoomed viewport.
            Assert.That(editor.Session.AddStroke(DrawingPartKey.Base, new[] { new DrawingPoint(.1f, .88f), new DrawingPoint(.1f, .92f) }, out _), Is.True);
            string before = editor.Session.Snapshot.GeometryHash;
            canvas.Zoom = 2; canvas.Eraser = true; pointer.position = canvas.LocalPointToScreen(new Vector2(.1f, .9f));
            canvas.OnPointerDown(pointer); canvas.OnDrag(pointer); canvas.OnPointerUp(pointer);
            Assert.That(editor.Session.Snapshot.GeometryHash, Is.EqualTo(before));
            Assert.That(editor.Session.Undo(), Is.True);
            Assert.That(editor.Session.Snapshot.GeometryHash, Is.EqualTo(freshStroke), "Outside erasing must not add history: undo removes only the imported stroke.");
        }

        void Stroke(DrawingCanvasGraphic canvas, DrawingPartKey part, params Vector2[] points)
        {
            canvas.SelectedPart = part;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            pointer.position = canvas.LocalPointToScreen(points[0]); canvas.OnPointerDown(pointer);
            for (int i = 1; i < points.Length; i++)
            { pointer.position = canvas.LocalPointToScreen(points[i]); canvas.OnDrag(pointer); }
            canvas.OnPointerUp(pointer);
        }
    }
}
