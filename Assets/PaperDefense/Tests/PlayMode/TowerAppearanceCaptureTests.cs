#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    /// <summary>Isolated presentation evidence from production recipes and real recorded combat.</summary>
    public sealed class TowerAppearanceCaptureTests : BattleHudThemeFixture
    {
        const string Folder = "docs/validation/2026-11-03-tower-appearance/screens";
        static readonly Vector2Int Size = new Vector2Int(1600, 900);
        readonly List<CaptureItem> evidence = new List<CaptureItem>();
        readonly PlayerUi ui = new PlayerUi();

        [UnityTest, Explicit("Captures direction silhouettes and immutable colored fire through the production renderer; isolated trial ranks, no profile writes.")]
        public IEnumerator CaptureTowerAppearance()
        {
            evidence.Clear();
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(
                "Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
            var rules = balance.CreateBalance();
            rules.Mechanics.FieldModernization = true;
            rules.Mechanics.ColorCartridges = true;
            rules.Mechanics.MaximumModernizationRank = 3;
            rules.Trials.MaximumTicks = 1200;
            var gun = catalog.Starters.First(e => e.Kind == AlbumEntryKind.Tower &&
                TowerCatalog.ResolveEntry(e, rules).Value.Weapons.Any(w => w.Attack == AttackKind.Single));
            var mortar = catalog.Starters.First(e => e.Kind == AlbumEntryKind.Tower &&
                TowerCatalog.ResolveEntry(e, rules).Value.Weapons.Any(w => w.Attack == AttackKind.AreaProjectile));
            // The fixture owns a memory-only player root. Hide it during the diagnostic
            // sheet; no campaign data, active game objects or editor scene are modified.
            root.SetActive(false);
            GameObject gallery = null;
            try
            {
                var states = new StructureView[10];
                for (int row = 0; row < 2; row++)
                    for (int column = 0; column < 5; column++)
                    {
                        int rank = row == 0 ? 1 : 3;
                        var ranks = new ModernizationRanks(power: column == 1 ? rank : 0,
                            mechanics: column == 2 ? rank : 0, range: column == 3 ? rank : 0,
                            resilience: column == 4 ? rank : 0);
                        states[row * 5 + column] = Trial(gun, rules, ranks).Frames[0].Towers.Single();
                    }
                Rect shared = Bounds(p => Tower(p, states[0]));
                foreach (var state in states) shared = Union(shared, Bounds(p => Tower(p, state)));
                shared = Pad(shared, .35f);
                gallery = Gallery("ПОЛЕВАЯ МОДЕРНИЗАЦИЯ", "Одна сборка · одинаковый масштаб · завершённые улучшения");
                string[] headings = { "БАЗОВАЯ", "МОЩНОСТЬ", "МЕХАНИКА", "ДАЛЬНОСТЬ", "СТОЙКОСТЬ" };
                for (int i = 0; i < states.Length; i++)
                {
                    var state = states[i];
                    string caption = headings[i % 5] + (i % 5 == 0 ? "" : i < 5 ? " · I" : " · III");
                    Tile(gallery, i, 5, 2, caption, shared, p => Tower(p, state));
                    evidence.Add(new CaptureItem { sheet = "tower-directions", label = caption, entry = gun.Id,
                        ranks = Ranks(state.Modernization.Ranks), color = state.Color.ToString(), seconds = 0 });
                }
                Save("tower-directions"); Object.DestroyImmediate(gallery); gallery = null;

                var colors = new[] { InkColor.Red, InkColor.Blue, InkColor.Green };
                var names = new[] { "КРАСНЫЕ ЧЕРНИЛА", "СИНИЕ ЧЕРНИЛА", "ЗЕЛЁНЫЕ ЧЕРНИЛА" };
                var gunReports = colors.Select(color => Trial(gun, rules, new ModernizationRanks(color: color, colorRank: 1))).ToArray();
                shared = Pad(Bounds(p => Tower(p, gunReports[0].Frames[0].Towers.Single())), .35f);
                gallery = Gallery("ЦВЕТ ОРУЖИЯ И ВЫСТРЕЛА", "Камера оружия → трассер → попадание · реальные события полигона");
                for (int column = 0; column < colors.Length; column++)
                {
                    var report = gunReports[column];
                    var state = report.Frames[0].Towers.Single();
                    Tile(gallery, column, 3, 2, names[column] + " · I", shared, p => Tower(p, state));
                    var shot = report.Frames.SelectMany(frame => frame.Events).First(e => e.Kind == SimulationEventKind.Shot);
                    float seconds = (float)shot.SimulationSeconds + .035f;
                    TrialTile(gallery, column + 3, names[column], report, seconds, "tower-color-fire");
                }
                Save("tower-color-fire"); Object.DestroyImmediate(gallery); gallery = null;

                gallery = Gallery("ЦВЕТ СНАРЯДА И РАЗРЫВА", "Верхний ряд — полёт · нижний — попадание · реальные события полигона");
                for (int column = 0; column < colors.Length; column++)
                {
                    var report = Trial(mortar, rules, new ModernizationRanks(color: colors[column], colorRank: 1));
                    var events = report.Frames.SelectMany(frame => frame.Events).ToArray();
                    var launch = events.First(e => e.Kind == SimulationEventKind.ProjectileLaunched);
                    var impact = events.First(e => e.Kind == SimulationEventKind.ProjectileImpact && e.EntityId == launch.EntityId);
                    TrialTile(gallery, column, names[column], report,
                        (float)launch.SimulationSeconds + launch.TravelSeconds * .5f, "tower-color-projectiles");
                    TrialTile(gallery, column + 3, names[column], report,
                        (float)impact.SimulationSeconds + .08f, "tower-color-projectiles");
                }
                Save("tower-color-projectiles");
                File.WriteAllText(Path.Combine(Folder, "tower-appearance-manifest.json"), JsonUtility.ToJson(new CaptureManifest {
                    capturedUtc = DateTime.UtcNow.ToString("O"),
                    note = "Presentation-only isolated TrialRunner configurations at ranks I and III. Production catalog, balance snapshot, targeting, event payloads and shared battlefield/trial renderers. Exactly one tower per trial; no profiles, unlocks, campaign wins or rewards written. Direction plates use the same scale. Combat plates fit actual recorded bounds and are not scale comparisons.",
                    items = evidence.ToArray()
                }, true));
                Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
            }
            finally { if (gallery != null) Object.DestroyImmediate(gallery); }
            yield return null;
        }

        static TrialReport Trial(AlbumEntry entry, GameBalance rules, ModernizationRanks ranks)
        {
            var config = new TrialModernizationConfiguration(ranks, 3);
            int cost = TowerCatalog.ResolveEntry(entry, rules).Value.Cost;
            int budget = cost;
            foreach (ModernizationDirection direction in Enum.GetValues(typeof(ModernizationDirection)))
                for (int rank = 1; rank <= ranks.Rank(direction); rank++)
                    budget += ModernizationRules.OrdinaryCost(cost, direction, rank);
            var report = TrialRunner.Run(entry, rules, TrialScenario.Crowd, config, budget);
            Assert.That(report.Copies, Is.EqualTo(1));
            return report;
        }
        static void Tower(MeshPen pen, StructureView state)
        {
            DoodleGlyphs.StructureBase(pen, state);
            DoodleGlyphs.StructureDynamic(pen, state, 1);
        }
        void TrialTile(GameObject gallery, int index, string caption, TrialReport report, float seconds, string sheet)
        {
            var playback = new NotebookTrialPlayback(); playback.Bind(report); playback.Seek(seconds);
            var framing = NotebookTrialRenderer.CombatBounds(report, NotebookTrialRenderer.RecordingBounds(report));
            Tile(gallery, index, 3, 2, caption, framing,
                pen => NotebookTrialRenderer.Draw(pen, playback, framing, darkSurface: true));
            Assert.That(playback.Effects.Count + playback.Projectiles.Count, Is.GreaterThan(0));
            evidence.Add(new CaptureItem { sheet = sheet, label = caption, entry = report.BlueprintId,
                color = report.Color.ToString(), ranks = Ranks(report.Modernization.Ranks), seconds = seconds,
                recordedEvents = report.Frames.Sum(f => f.Events.Count), activeEffects = playback.Effects.Count,
                activeProjectiles = playback.Projectiles.Count });
        }
        GameObject Gallery(string title, string subtitle)
        {
            var gallery = new GameObject("Tower appearance evidence", typeof(RectTransform), typeof(Canvas));
            var canvas = gallery.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 999;
            var content = new GameObject("Sheet", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(gallery.transform, false); content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f);
            content.sizeDelta = Size;
            PlayerUi.Graphic<NightNotebookPaperGraphic>(content, "Dark field notebook", new Rect(0, 0, Size.x, Size.y));
            ui.Label(content, title, new Rect(24, 18, Size.x - 48, 40), 27, FieldMenuSkin.Ink, alignment: TextAnchor.MiddleCenter);
            ui.Label(content, subtitle, new Rect(24, 62, Size.x - 48, 28), 16, FieldMenuSkin.Muted, alignment: TextAnchor.MiddleCenter);
            return gallery;
        }
        void Tile(GameObject gallery, int index, int columns, int rows, string caption, Rect bounds, Action<MeshPen> draw)
        {
            float width = (Size.x - 48 - (columns - 1) * 14) / (float)columns;
            float height = (Size.y - 124 - (rows - 1) * 18) / (float)rows;
            var rect = new Rect(24 + index % columns * (width + 14), 102 + index / columns * (height + 18), width, height);
            var tile = ui.Panel(gallery.transform.Find("Sheet"), caption, rect, FieldMenuSkin.Panel);
            tile.GetComponent<PlayerCardGraphic>().FieldStyle = true;
            tile.gameObject.AddComponent<RectMask2D>();
            ui.Label(tile, caption, new Rect(10, 8, width - 20, 30), 16, FieldMenuSkin.Ink, alignment: TextAnchor.MiddleCenter);
            var picture = PlayerUi.Graphic<TowerAppearanceCaptureGraphic>(tile, "Production drawing", new Rect(12, 46, width - 24, height - 60));
            picture.Configure(bounds, draw);
        }
        static void Save(string stem)
        {
            Canvas.ForceUpdateCanvases();
            string path = ReleaseVisualCapture.Capture(stem, Size.x, Size.y, Folder);
            Assert.That(File.Exists(path), Is.True);
        }
        static Rect Bounds(Action<MeshPen> draw)
        {
            var pen = new MeshPen(null); draw(pen);
            Assert.That(pen.TryGetBounds(out var bounds), Is.True);
            return bounds;
        }
        static Rect Pad(Rect bounds, float amount) => Rect.MinMaxRect(bounds.xMin - amount, bounds.yMin - amount, bounds.xMax + amount, bounds.yMax + amount);
        static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        static int[] Ranks(ModernizationRanks ranks) => new[] { ranks.Power, ranks.Mechanics, ranks.Range, ranks.Resilience, ranks.ColorRank };
        [Serializable] sealed class CaptureManifest { public string capturedUtc, note; public CaptureItem[] items; }
        [Serializable] sealed class CaptureItem
        {
            public string sheet, label, entry, color;
            public int[] ranks; public float seconds; public int recordedEvents, activeEffects, activeProjectiles;
        }
    }

    /// <summary>Captures native MeshPen output, without substituting independently authored artwork.</summary>
    public sealed class TowerAppearanceCaptureGraphic : NotebookAtlasGraphic
    {
        readonly MeshPen pen = new MeshPen(null);
        Rect bounds; Action<MeshPen> draw;
        public void Configure(Rect framing, Action<MeshPen> render) { bounds = framing; draw = render; raycastTarget = false; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); if (draw == null || bounds.width <= 0 || bounds.height <= 0) return;
            var rect = rectTransform.rect;
            float scale = Mathf.Min(rect.width / bounds.width, rect.height / bounds.height);
            pen.Clear(); using (pen.WithUiDetail(scale)) draw(pen);
            pen.CopyTo(helper, rect.center - bounds.center * scale, scale);
        }
    }
}
#endif
