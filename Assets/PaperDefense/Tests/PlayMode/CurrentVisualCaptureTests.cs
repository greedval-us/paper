#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    /// <summary>Current production visual evidence; historical screenshots are never regenerated.</summary>
    public sealed partial class CurrentVisualCaptureTests : BattleHudThemeFixture
    {
        static string Folder => SessionState.GetString("PaperDefense.VisualCaptureFolder", "Temp/VisualCapture/current/screens");
        static readonly Vector2Int[] Sizes = { new Vector2Int(1600, 900), new Vector2Int(800, 600) };
        readonly List<Evidence> captures = new List<Evidence>();
        readonly List<SimulationEvent> events = new List<SimulationEvent>();
        CompositeWorkshopPanel Workshop => root.GetComponentInChildren<WorkshopHud>(true).Composite;

        [UnityTest, Explicit("Captures representative current menus, four-stage workshop, recorded trial and real production combat.")]
        public IEnumerator CaptureProductionScreensTrialAndBattle()
        {
            captures.Clear();
            for (int index = 0; index < Sizes.Length; index++)
            {
                if (index > 0) { yield return TearDown(); yield return SetUp(); }
                var size = Sizes[index]; shell.ApplyLayout(size.x, size.y);
                game.ReturnToHome(); Capture("home", size);
                Press("Open campaign"); Capture("campaign", size);
                Press("Campaign notebook 2"); Capture("campaign-notebook-2", size);
                Press("Campaign notebook 3"); Capture("campaign-notebook-3", size);
                Press("Campaign notebook 1"); Press("Open mission"); Capture("map", size);
                Press("Prepare mission"); Capture("preparation", size);
                game.OpenAlbum(); shell.Album.SetFilter(AlbumFilter.All);
                shell.Album.SelectEntry(game.Run.Workshop.LoadoutEntries[0].Id); Capture("album-all", size);
                game.EditAlbumEntry(null); Capture("foundation", size);
                Press("Sequence next"); Capture("weapon", size);
                Press("Sequence next"); Capture("assembly", size);
                Press("Sequence next"); Capture("tower-review", size);
                Press("Composite trial"); Capture("trial-ready", size);
                Press("Composite trial run"); PrepareTrialFiring(); Capture("trial-firing", size);
                Press("Composite trial compare"); Press("Composite trial run"); PrepareTrialFiring(); Capture("trial-comparison", size);
                Press("Composite trial header return");
                Press("Composite route structure"); Press("Structure step 0");
                Capture("structure-family", size);
                Press("Structure next"); Capture("structure-parts", size);
                Press("Structure next"); Capture("structure-review", size);
                game.ReturnFromWorkshop(); game.OpenSettings(); Capture("settings", size);
                game.CloseSettings(); game.OpenSettings(true); Capture("help", size); game.CloseHelp();
                Assert.That(game.StartBattle(), Is.True, game.Status); Hud.Refresh();
                Capture("battle-planning", size);
                var (slot, tower) = TowerSite();
                yield return CapturePlacement(slot, tower, size);
                for (int step = 0; step < 1000 && game.Run.Session.Structures.Any(item => !item.IsComplete); step++) game.Run.StepSimulation();
                Assert.That(game.Run.Session.Structures.Single(item => item.Cell == tower).IsComplete, Is.True);
                game.Drawing.Inspect(tower); Hud.Refresh(); Capture("battle-inspector", size);
                game.Drawing.Inspect(null); Hud.ShowHint(LongHint); Capture("battle-long-hint", size); Hud.ShowHint("");
                int actualShots = SampleAssault(); Capture("battle-active", size, actualShots: actualShots);
                var shot = events.First(item => item.Kind == SimulationEventKind.Shot || item.Kind == SimulationEventKind.ProjectileLaunched);
                var focus = new Cell(Mathf.FloorToInt((shot.From.X + shot.To.X) * .5f),
                    Mathf.FloorToInt((shot.From.Y + shot.To.Y) * .5f));
                Capture("battle-combat", size, actualShots: actualShots, focus: focus);
                game.Drawing.Inspect(tower); game.Drawing.SetBattleView(true); game.Drawing.Focus(tower);
                Capture("battle-sector-in-action", size, actualShots: actualShots, preserveBattleView: true);
                game.Drawing.Inspect(null);
                game.Pause(); Capture("pause", size);
                game.Resume(); game.Run.TogglePause(); Capture("battle-tactical-pause", size); game.Run.TogglePause();
                SetOutcome(RunPhase.Victory); Capture("victory", size, presentationOnly: true);
                SetOutcome(RunPhase.Defeat); Capture("defeat", size, presentationOnly: true);
                Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
            }
            Assert.That(captures.Select(item => item.page), Does.Not.Contain(PlayerPage.Upgrades.ToString()),
                "The production battle no longer offers global wave upgrades.");
            WriteManifest("screen-manifest.json");
        }

        [UnityTest, Explicit("Writes diagnostic galleries from all production parts and standard frozen entries; no unlocking, balance changes or profile writes.")]
        public IEnumerator CaptureProductionCatalog()
        {
            captures.Clear();
            foreach (var size in Sizes)
            {
                shell.ApplyLayout(size.x, size.y); game.ReturnToHome();
                var parts = game.Run.AssemblyCatalog.Definitions.Where(part => !part.Id.EndsWith(".none", StringComparison.Ordinal))
                    .OrderBy(part => part.Category).ThenBy(part => part.Id, StringComparer.Ordinal).ToArray();
                const int columns = 4, rows = 3, capacity = columns * rows;
                for (int page = 0; page * capacity < parts.Length; page++)
                {
                    var gallery = Gallery("Каталог стандартных деталей", size);
                    for (int index = page * capacity; index < Mathf.Min((page + 1) * capacity, parts.Length); index++)
                    {
                        var tile = GalleryTile(gallery.transform.Find("Diagnostic content"), index % capacity, columns, rows, size);
                        var bounds = ((RectTransform)tile.transform).rect;
                        var drawing = PlayerUi.Graphic<WeaponAssemblyGraphic>(tile.transform, parts[index].Id,
                            new Rect(5, 2, bounds.width - 10, bounds.height - 60));
                        drawing.SetPart(new PartRef(parts[index].Id, parts[index].Revision));
                        GalleryCaption(tile, GameText.Resolve(parts[index].Name));
                    }
                    Capture("diagnostic-parts-" + (page + 1), size, diagnostic: true);
                    Object.DestroyImmediate(gallery);
                }
                var families = Gallery("Стандартные башни и сооружения", size);
                int slot = 0;
                foreach (var entry in game.Run.AssemblyCatalog.Starters)
                {
                    var tile = GalleryTile(families.transform.Find("Diagnostic content"), slot++, 4, 2, size);
                    var bounds = ((RectTransform)tile.transform).rect;
                    StandardAssemblyIllustration.Draw(tile.transform, entry, new Rect(5, 2, bounds.width - 10, bounds.height - 60), entry.Id);
                    GalleryCaption(tile, GameText.Resolve(entry.Name));
                }
                Capture("diagnostic-standard-families", size, diagnostic: true);
                Object.DestroyImmediate(families);
                CaptureNativeArt(size);
            }
            WriteManifest("catalog-manifest.json"); yield return null;
        }

        GameObject Gallery(string heading, Vector2Int size)
        {
            var gallery = new GameObject("Standard art diagnostic gallery", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            gallery.transform.SetParent(root.transform, false);
            var canvas = gallery.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 999;
            var content = new GameObject("Diagnostic content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(gallery.transform, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f); content.sizeDelta = size;
            PlayerUi.Graphic<NightNotebookPaperGraphic>(content, "Diagnostic dark notebook", new Rect(0, 0, size.x, size.y));
            new PlayerUi().Label(content, heading, new Rect(18, 8, size.x - 36, 38), 25,
                FieldMenuSkin.Ink, alignment: TextAnchor.MiddleCenter);
            return gallery;
        }

        void GalleryCaption(RectTransform tile, string caption)
        {
            var bounds = tile.rect;
            var label = new PlayerUi().Label(tile, caption, new Rect(5, bounds.height - 56, bounds.width - 10, 52),
                14, FieldMenuSkin.Ink, alignment: TextAnchor.MiddleCenter);
            label.name = "Diagnostic caption " + caption;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 12; label.resizeTextMaxSize = 14;
        }

        RectTransform GalleryTile(Transform parent, int index, int columns, int rows, Vector2Int size)
        {
            float width = (size.x - 36 - (columns - 1) * 10) / (float)columns;
            float height = (size.y - 70 - (rows - 1) * 10) / (float)rows;
            var tile = new PlayerUi().Panel(parent, "Diagnostic tile " + index,
                new Rect(18 + index % columns * (width + 10), 54 + index / columns * (height + 10), width, height), FieldMenuSkin.Panel);
            var card = tile.GetComponent<PlayerCardGraphic>(); card.FieldStyle = true;
            return tile;
        }

        void PrepareTrialFiring()
        {
            bool shotFound = false;
            foreach (var lane in Workshop.Root.GetComponentsInChildren<TrialLaneSketch>())
            {
                var report = lane.Playback.Report; if (report == null) continue;
                var firing = report.Frames.SelectMany(frame => frame.Events)
                    .Where(item => item.Kind == SimulationEventKind.Shot || item.Kind == SimulationEventKind.ProjectileLaunched)
                    .Select(item => (SimulationEvent?)item).FirstOrDefault();
                if (!firing.HasValue) continue;
                // Frames collect an interval of events. Their snapshot timestamp can be
                // later than a short-lived tracer, so sample the actual firing timestamp.
                float seconds = (float)firing.Value.SimulationSeconds + .03f;
                lane.SetResult(report, Time.unscaledTime - seconds / lane.PlaybackSpeed, report.Frames.Last().Seconds);
                lane.PlaybackPaused = true; lane.SendMessage("Update");
                var playback = lane.Playback;
                Assert.That(playback.Seconds, Is.EqualTo(seconds).Within(.001f));
                bool active = playback.Effects.Concat(playback.Projectiles).Any(item =>
                    item.Sequence == firing.Value.Sequence && item.Kind == firing.Value.Kind &&
                    item.SimulationSeconds == firing.Value.SimulationSeconds);
                Assert.That(active, Is.True, "The sampled recorded shot/projectile must still be active at capture time.");
                var pen = new MeshPen(null);
                NotebookTrialRenderer.DrawEffects(pen, playback, darkSurface: true);
                NotebookTrialRenderer.DrawProjectiles(pen, playback, darkSurface: true);
                Assert.That(pen.VertexCount, Is.GreaterThan(0), "Active trial firing must emit actual effect/projectile geometry.");
                shotFound = true;
            }
            Assert.That(shotFound, Is.True, "Trial evidence uses a genuine recorded firing frame.");
        }

        Cell PlaceTower()
        {
            var (slot, cell) = TowerSite();
            Assert.That(game.Run.BuildEntry(new[] { cell }, slot, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
            return cell;
        }

        (int slot, Cell cell) TowerSite()
        {
            int slot = Enumerable.Range(0, game.Run.RunLoadout.Entries.Count).First(index => game.Run.RunLoadout.Entries[index]?.Kind == AlbumEntryKind.Tower);
            var home = game.Run.Session.BaseCell;
            var candidates = new List<Cell>();
            for (int y = 2; y < game.Run.Session.Balance.Height - 2; y++)
            for (int x = 2; x < game.Run.Session.Balance.Width - 2; x++) candidates.Add(new Cell(x, y));
            foreach (var cell in candidates.OrderBy(item => (item.X - home.X) * (item.X - home.X) + (item.Y - home.Y) * (item.Y - home.Y)))
            {
                if (!game.Run.QuoteEntry(new[] { cell }, slot, MaterialTier.Pencil, InkColor.None).Valid) continue;
                return (slot, cell);
            }
            Assert.Fail("Production loadout must afford a legal tower."); return default;
        }

        IEnumerator CapturePlacement(int slot, Cell cell, Vector2Int size)
        {
            using (new NativeInputIsolation())
            {
                var previous = InputSystem.settings;
                var flags = previous.hideFlags;
                var settings = Object.Instantiate(previous);
                settings.hideFlags = HideFlags.HideAndDontSave;
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                Mouse mouse = null;
                try
                {
                    if (flags == HideFlags.HideAndDontSave) previous.hideFlags = HideFlags.None;
                    InputSystem.settings = settings;
                    previous.hideFlags = flags;
                    mouse = InputSystem.AddDevice<Mouse>("PlacementCaptureMouse");
                    game.Drawing.SelectSlot(slot);
                    game.Drawing.SetBattleView(false); game.Drawing.Focus(cell);
                    yield return null;
                    var center = new Vector3(cell.X + .5f, cell.Y + .5f, 0);
                    IEnumerator PointAt(Vector3 point, bool held)
                    {
                        var screen = (Vector2)game.World.WorldCamera.WorldToScreenPoint(point);
                        mouse.MakeCurrent();
                        InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left, held));
                        yield return null; yield return null;
                    }
                    int ink = game.Run.Session.Ink;
                    yield return PointAt(center, true);
                    yield return PointAt(center, false);
                    Assert.That(game.Drawing.PlacementCell, Is.EqualTo(cell));
                    yield return PointAt(center + Vector3.up * 1.5f, false);
                    Assert.That(game.Drawing.HasHover, Is.True, "The north preview must point inside the playable field.");
                    Capture("battle-placement-north", size, preserveBattleView: true);
                    var target = center + new Vector3(-5, 2);
                    yield return PointAt(target, false);
                    Capture("battle-placement-aim", size, preserveBattleView: true);
                    Assert.That(game.Run.Session.StructureAt(cell), Is.Null);
                    Assert.That(game.Run.Session.Ink, Is.EqualTo(ink));
                    yield return PointAt(target, true);
                    yield return PointAt(target, false);
                    var placed = game.Run.Session.StructureAt(cell);
                    Assert.That(placed.HasValue, Is.True);
                    Assert.That(placed.Value.FacingDegrees, Is.EqualTo(Mathf.Atan2(2, -5) * Mathf.Rad2Deg).Within(1));
                }
                finally
                {
                    if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                    InputSystem.settings = previous; previous.hideFlags = flags;
                    Object.Destroy(settings);
                }
            }
        }

        int SampleAssault()
        {
            Action<SimulationEvent> observe = item => events.Add(item); int shots = 0;
            game.Run.SimulationEventReceived += observe;
            try
            {
                game.Run.NextWave();
                for (int step = 0; step < 10000 && game.Run.Session.Phase == RunPhase.Assault; step++)
                {
                    events.Clear(); game.Run.StepSimulation();
                    shots = events.Count(item => item.Kind == SimulationEventKind.Shot || item.Kind == SimulationEventKind.ProjectileLaunched);
                    if (shots > 0 && game.Run.Session.Enemies.Count > 0) break;
                }
            }
            finally { game.Run.SimulationEventReceived -= observe; }
            Assert.That(shots, Is.GreaterThan(0)); Assert.That(game.Run.Session.Enemies.Count, Is.GreaterThan(0)); return shots;
        }

        void SetOutcome(RunPhase phase)
        {
            // Outcome pages are presentation fixtures, isolated from the real user profile.
            // Use separate attempts so a committed victory is never rewritten as a defeat.
            if (game.Run.Session.Phase == RunPhase.Victory)
            { game.Run.OpenWorkshop(); Assert.That(game.StartBattle(), Is.True); }
            var session = typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Run);
            typeof(GameSession).GetProperty("CompletedWaves").SetValue(session, phase == RunPhase.Victory ? game.Run.Session.Balance.Waves.Count : 3);
            typeof(GameSession).GetProperty("WaveIndex").SetValue(session, phase == RunPhase.Victory ? game.Run.Session.Balance.Waves.Count : 4);
            typeof(GameSession).GetProperty("Phase").SetValue(session, phase);
            game.Run.StepSimulation();
            game.Navigate(PlayerPage.Result);
        }

        T Find<T>(string name) where T : Component
        {
            var item = root.GetComponentsInChildren<T>(true).FirstOrDefault(value => value.name == name && value.gameObject.activeInHierarchy);
            Assert.That(item, Is.Not.Null, "Missing screen control: " + name); return item;
        }
        void Press(string name)
        { var button = Find<Button>(name); Assert.That(button.IsInteractable(), Is.True, name); button.onClick.Invoke(); Canvas.ForceUpdateCanvases(); }

        void Capture(string name, Vector2Int size, bool presentationOnly = false, bool diagnostic = false,
            int actualShots = 0, Cell? focus = null, bool settleMotion = true, bool preserveBattleView = false)
        {
            Directory.CreateDirectory(Folder);
            if (game.Page == PlayerPage.Workshop)
            {
                var board = (RectTransform)Workshop.Root.parent;
                if (board.sizeDelta != (Vector2)size)
                { board.sizeDelta = size; Workshop.Reflow(size.x, size.y); }
                if (name.StartsWith("trial-", StringComparison.Ordinal) && name != "trial-ready") PrepareTrialFiring();
            }
            if (settleMotion) UiMenuMotion.FinishIn(root.transform);
            foreach (var scaler in root.GetComponentsInChildren<CanvasScaler>(true))
            { scaler.referenceResolution = size; scaler.enabled = false; }
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.scaleFactor = 1;
            Hud.Refresh();
            foreach (var theme in root.GetComponentsInChildren<FieldMenuTheme>()) theme.Refresh();
            foreach (var theme in root.GetComponentsInChildren<BattleHudTheme>()) theme.Refresh();
            Canvas.ForceUpdateCanvases();
            var bootstrap = root.GetComponentInChildren<PaperDefenseBootstrap>(); bootstrap.ViewportOverride = shell.Layout.NormalizedField;
            var camera = game.World.WorldCamera; var oldTarget = camera.targetTexture;
            var framing = new RenderTexture(size.x, size.y, 24);
            try
            {
                camera.targetTexture = framing;
                if (game.Page == PlayerPage.Battle && !preserveBattleView)
                {
                    game.Drawing.SetBattleView(!focus.HasValue);
                    if (focus.HasValue) game.Drawing.Focus(focus.Value);
                }
                bootstrap.SendMessage("LateUpdate");
                // SessionChanged owns Bind and the live event subscription. Rebinding here
                // would erase the firing animation that this capture is meant to verify.
                game.World.SendMessage("LateUpdate");
                foreach (var overlay in root.GetComponentsInChildren<TowerDevelopmentOverlay>()) overlay.RefreshNow();
                Canvas.ForceUpdateCanvases();
                string stem = name + "-" + size.x + "x" + size.y;
                string path = ReleaseVisualCapture.Capture(stem, size.x, size.y, Folder);
                Assert.That(File.Exists(path), Is.True);
                var visible = root.GetComponentsInChildren<Graphic>().Where(item => item.enabled && item.gameObject.activeInHierarchy && !item.canvasRenderer.cull).ToArray();
                var evidence = new Evidence {
                    name = stem, path = path, pixelSize = size, page = game.Page.ToString(), phase = game.Run.Session.Phase.ToString(),
                    capturedUtc = DateTime.UtcNow.ToString("O"), memoryOnly = game.Run.Workshop.SaveState == ProfileSaveState.MemoryOnly,
                    presentationOnly = presentationOnly, diagnostic = diagnostic, outcomeInjected = game.Page == PlayerPage.Result,
                    actualFiringEvents = actualShots, visibleEnemies = game.Page == PlayerPage.Battle ?
                        game.Run.Session.Enemies.Count(enemy => InView(camera, enemy.Position)) : 0,
                    visibleNativeAssemblies = visible.OfType<WeaponAssemblyGraphic>().Count(),
                    authoredDrawings = visible.OfType<BlueprintSketch>().Count(), darkPaperSheets = visible.OfType<NightNotebookPaperGraphic>().Count(),
                    trialPlaybacks = visible.OfType<TrialLaneSketch>().Where(lane => lane.HasRecording).Select(lane => new TrialEvidence {
                        seconds = lane.Playback.Seconds, activeEffects = lane.Playback.Effects.Count,
                        activeProjectiles = lane.Playback.Projectiles.Count, paused = lane.PlaybackPaused
                    }).ToArray(),
                    visibleText = visible.OfType<Text>().Select(item => item.text).ToArray(),
                    clippedText = visible.OfType<Text>().Where(item => !string.IsNullOrWhiteSpace(item.text) && item.cachedTextGenerator.characterCountVisible < item.text.Length)
                        .Select(item => item.name + ": " + item.text).ToArray(),
                    assemblyRecipes = visible.OfType<WeaponAssemblyGraphic>().Where(item => item.Recipe != null)
                        .Select(item => item.name + " | " + string.Join(",", item.Recipe.Recipe.Parts.Select(part => part.Id + "@" + part.Revision))).ToArray()
                };
                captures.Add(evidence); File.WriteAllText(Folder + "/" + stem + "-technical.json", JsonUtility.ToJson(evidence, true));
                if (actualShots > 0)
                    Assert.That(evidence.visibleEnemies, Is.GreaterThan(0), "Combat evidence must show a living production enemy in the camera viewport.");
                if (diagnostic)
                    Assert.That(evidence.clippedText, Is.Empty, "Diagnostic titles and full part names must fit at " + size);
            }
            finally { camera.targetTexture = oldTarget; framing.Release(); Object.DestroyImmediate(framing); }
        }

        static bool InView(Camera camera, Point2 point)
        {
            var viewport = camera.WorldToViewportPoint(new Vector3(point.X, point.Y, 0));
            return viewport.z > 0 && viewport.x > 0 && viewport.x < 1 && viewport.y > 0 && viewport.y < 1;
        }

        void WriteManifest(string name)
            => File.WriteAllText(Folder + "/" + name, JsonUtility.ToJson(new Manifest {
                capturedUtc = DateTime.UtcNow.ToString("O"), artRevision = NightNotebookArt.Revision,
                productionBalance = "Assets/PaperDefense/Configuration/Game/PlayerBalance.asset", captures = captures.ToArray(),
                note = "Native production UI and memory-only profiles. Victory/defeat outcomes are injected presentation checks; trial and battle firing are real recorded events. Galleries are explicitly diagnostic."
            }, true));

        [Serializable] sealed class Manifest
        { public string capturedUtc, artRevision, productionBalance, note; public Evidence[] captures; }
        [Serializable] sealed class Evidence
        {
            public string name, path, page, phase, capturedUtc;
            public Vector2Int pixelSize;
            public bool memoryOnly, presentationOnly, diagnostic, outcomeInjected;
            public int actualFiringEvents, visibleEnemies, visibleNativeAssemblies, authoredDrawings, darkPaperSheets;
            public string[] visibleText, clippedText, assemblyRecipes;
            public TrialEvidence[] trialPlaybacks;
        }
        [Serializable] sealed class TrialEvidence
        {
            public double seconds;
            public int activeEffects, activeProjectiles;
            public bool paused;
        }
    }
}
#endif
