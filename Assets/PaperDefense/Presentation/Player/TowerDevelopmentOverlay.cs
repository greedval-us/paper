using System;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Screen-sized labels follow world towers; quotes are refreshed only when their inputs change.</summary>
    [DisallowMultipleComponent]
    public sealed class TowerDevelopmentOverlay : MonoBehaviour
    {
        enum BadgeStyle { Full, Narrow, Marker }
        const int MarkerSize = 16;

        sealed class Badge
        {
            internal int Id, Revision = -1, Budget = -1;
            internal bool Complete, Seen;
            internal bool StyleDirty, Selected;
            internal BadgeStyle Style;
            internal Rect Bounds;
            internal Vector2 Anchor;
            internal StructureView Tower;
            internal TowerDevelopmentState State;
            internal Button Button;
            internal Text Level, ColorRank;
            internal PlayerIconGraphic Status, Ink;
            internal RectTransform Progress, Track, Link;
            internal PlayerTooltip Hint;
        }

        readonly Dictionary<int, Badge> badges = new Dictionary<int, Badge>();
        readonly List<Badge> ordered = new List<Badge>();
        readonly List<int> retired = new List<int>();
        readonly Stack<Badge> pool = new Stack<Badge>();
        readonly List<Rect> occupied = new List<Rect>();
        PlayerGameRoot game;
        PlayerUi ui;
        PlayerLayout layout;
        RectTransform inspector, notice, reward;
        RectTransform[] approaches;
        Action refreshInspector;
        Guid runId;
        int ink = -1, red = -1, blue = -1, green = -1, budgetVersion;
        RunPhase phase;
        bool refreshing, positioning;
        Badge hintOwner;

        internal int QuoteRefreshCount { get; private set; }
        internal int ActiveBadgeCount => badges.Count;
        internal int VisibleBadgeCount { get; private set; }

        internal void Initialize(PlayerGameRoot owner, PlayerUi factory, PlayerLayout geometry,
            RectTransform context, RectTransform message, RectTransform[] entrances, Action refresh, RectTransform flaskReward = null)
        {
            game = owner; ui = factory; layout = geometry;
            inspector = context; notice = message; approaches = entrances; refreshInspector = refresh; reward = flaskReward;
            RefreshNow();
        }

        void OnEnable() => Canvas.willRenderCanvases += BeforeCanvas;
        void OnDisable() => Canvas.willRenderCanvases -= BeforeCanvas;
        void BeforeCanvas() { if (!refreshing && game != null) PositionBadges(); }
        void LateUpdate() => RefreshNow();

        internal void RefreshNow()
        {
            if (refreshing || game == null || ui == null || game.Run?.Session == null) return;
            refreshing = true;
            try
            {
                var run = game.Run.Session;
                if (runId != run.RunId)
                {
                    foreach (var pair in badges) Retire(pair.Value);
                    badges.Clear(); runId = run.RunId; ink = -1;
                }
                if (ink != run.Ink || red != run.ColoredInk(InkColor.Red) || blue != run.ColoredInk(InkColor.Blue)
                    || green != run.ColoredInk(InkColor.Green) || phase != run.Phase)
                {
                    ink = run.Ink; red = run.ColoredInk(InkColor.Red); blue = run.ColoredInk(InkColor.Blue);
                    green = run.ColoredInk(InkColor.Green); phase = run.Phase; budgetVersion++;
                }
                foreach (var pair in badges) pair.Value.Seen = false;
                bool created = false;
                foreach (var tower in run.Structures)
                {
                    if (tower.Health <= 0 || tower.ModernizationEntry == null) continue;
                    if (!badges.TryGetValue(tower.Id, out var badge))
                    {
                        badge = pool.Count > 0 ? pool.Pop() : CreateBadge();
                        badge.Id = tower.Id; badge.Revision = badge.Budget = -1;
                        badge.Button.name = "Tower development " + tower.Id;
                        badge.Level.name = "Tower level " + tower.Id;
                        badge.Ink.name = "Tower ink " + tower.Id;
                        badges.Add(tower.Id, badge); created = true;
                    }
                    badge.Seen = true; badge.Tower = tower;
                    if (badge.Revision != tower.Modernization.Revision || badge.Budget != budgetVersion || badge.Complete != tower.IsComplete)
                    {
                        badge.State = TowerDevelopmentState.Evaluate(run, tower); QuoteRefreshCount++;
                        badge.Revision = tower.Modernization.Revision; badge.Budget = budgetVersion; badge.Complete = tower.IsComplete;
                        WriteBadge(badge);
                    }
                    badge.Button.interactable = game.CanBuild;
                    bool working = tower.Modernization.IsWorking;
                    if (working)
                        badge.Progress.localScale = new Vector3(1 - Mathf.Clamp01(tower.Modernization.WorkRemaining /
                            Mathf.Max(.001f, tower.Modernization.WorkDuration)), 1, 1);
                }
                retired.Clear();
                foreach (var pair in badges) if (!pair.Value.Seen) retired.Add(pair.Key);
                foreach (int id in retired) { Retire(badges[id]); badges.Remove(id); }
                if (created) GetComponentInParent<BattleHudTheme>()?.Refresh();
                PositionBadges();
            }
            finally { refreshing = false; }
        }

        void Retire(Badge badge)
        {
            badge.Button.gameObject.SetActive(false); badge.Link.gameObject.SetActive(false); pool.Push(badge);
        }

        Badge CreateBadge()
        {
            var badge = new Badge();
            var link = PlayerUi.Graphic<Image>(transform, "Tower badge connector", new Rect(0, 0, 1, 1));
            link.color = new Color(.82f, .77f, .55f, .5f); badge.Link = link.rectTransform;
            badge.Link.SetAsFirstSibling();
            badge.Button = ui.IconButton(transform, "Tower development", new Rect(0, 0, 88, 32), PlayerIcon.Plus, "", () => {
                if (!game.CanBuild || game.Run.Session.RunId != runId) return;
                var current = game.Run.Session.StructureAt(badge.Tower.Cell);
                if (!current.HasValue || current.Value.Id != badge.Id) return;
                game.Drawing.SelectMode(DrawingMode.Upgrade);
                game.Drawing.Inspect(badge.Tower.Cell); refreshInspector?.Invoke();
            });
            badge.Status = badge.Button.GetComponentInChildren<PlayerIconGraphic>();
            badge.Status.Sketch = false;
            badge.Level = ui.Label(badge.Button.transform, "0/6", new Rect(23, 1, 32, 29), 14);
            badge.Ink = PlayerUi.Graphic<PlayerIconGraphic>(badge.Button.transform, "Tower ink", new Rect(55, 8, 14, 16));
            badge.Ink.Sketch = true;
            badge.ColorRank = ui.Label(badge.Button.transform, "", new Rect(70, 1, 17, 29), 14);
            var track = PlayerUi.Graphic<Image>(badge.Button.transform, "Tower modernization track", new Rect(4, 26, 80, 3));
            track.color = FieldMenuSkin.Border; badge.Track = track.rectTransform;
            var progress = PlayerUi.Graphic<Image>(badge.Button.transform, "Tower modernization progress", new Rect(4, 26, 80, 3));
            progress.color = FieldMenuSkin.Accent; badge.Progress = progress.rectTransform;
            badge.Hint = badge.Button.GetComponent<PlayerTooltip>();
            badge.Hint.Display = text => {
                if (!string.IsNullOrEmpty(text)) { hintOwner = badge; ui.Tooltip?.Invoke(text); }
                else if (hintOwner == badge) { hintOwner = null; ui.Tooltip?.Invoke(""); }
            };
            return badge;
        }

        void WriteBadge(Badge badge)
        {
            var state = badge.State;
            badge.StyleDirty = true;
            badge.Status.Icon = state.IsWorking || !state.IsComplete ? PlayerIcon.Pencil : state.CanImprove ? PlayerIcon.Plus : PlayerIcon.Lock;
            badge.Status.color = state.CanImprove ? FieldMenuSkin.Accent : FieldMenuSkin.Muted;
            badge.Status.SetVerticesDirty();
            badge.Ink.Icon = state.Color == InkColor.None ? PlayerIcon.Pencil : PlayerIcon.Drop;
            badge.Ink.color = state.Color == InkColor.Red ? NotebookPalette.Red : state.Color == InkColor.Blue ? NotebookPalette.Blue :
                state.Color == InkColor.Green ? NotebookPalette.Green : FieldMenuSkin.Muted;
            badge.Ink.SetVerticesDirty();
            badge.ColorRank.text = state.Color == InkColor.None ? "" : Rank(state.ColorRank);
            var ranks = badge.Tower.Modernization;
            string color = state.Color == InkColor.None ? "Без цвета" : (state.Color == InkColor.Red ? "Красный" : state.Color == InkColor.Blue ? "Синий" : "Зелёный") + " " + Rank(state.ColorRank) + " · закреплён";
            badge.Hint.Message = "Улучшения " + state.StepsUsed + "/6 · " + color +
                "\nМощность " + Rank(ranks.Rank(ModernizationDirection.Power)) + " · Механика " + Rank(ranks.Rank(ModernizationDirection.Mechanics)) +
                "\nДальность " + Rank(ranks.Rank(ModernizationDirection.Range)) + " · Стойкость " + Rank(ranks.Rank(ModernizationDirection.Resilience)) +
                "\n" + (!state.IsComplete ? "Строится" : state.IsWorking ? "Идёт улучшение · башня временно не стреляет" : state.CanImprove ? "Есть доступное усиление" : "Сейчас усиление недоступно · условия в панели") +
                "\nНажми, чтобы открыть панель этой башни. Цвет не занимает обычные шаги.";
        }

        static string Rank(int rank) => rank == 0 ? "0" : rank == 1 ? "I" : rank == 2 ? "II" : "III";

        void PositionBadges()
        {
            if (positioning) return;
            positioning = true;
            try { LayoutBadges(); }
            finally { positioning = false; }
        }

        void LayoutBadges()
        {
            if (game == null || game.World?.WorldCamera == null || !gameObject.activeInHierarchy) return;
            var camera = game.World.WorldCamera;
            var field = layout.Field;
            var run = game.Run.Session;
            bool hordeOverview = run != null && HordeWaveForecast.DisplayedSides(run) != 0 && camera.orthographic
                && camera.orthographicSize * 2 >= run.Balance.Height / 3f;
            ordered.Clear(); occupied.Clear(); VisibleBadgeCount = 0;
            // Selection wins collision placement, while all other identities retain stable ordering.
            Badge selected = null;
            foreach (var pair in badges)
                if (game.Drawing.InspectedCell.HasValue && pair.Value.Tower.Cell.Equals(game.Drawing.InspectedCell.Value)) selected = pair.Value;
                else ordered.Add(pair.Value);
            ordered.Sort((a, b) => a.Id.CompareTo(b.Id));
            if (selected != null) ordered.Insert(0, selected);
            foreach (var badge in ordered)
            {
                var cell = badge.Tower.Cell;
                var point = camera.WorldToViewportPoint(new Vector3(cell.X + .5f, cell.Y + .5f, 0));
                bool visible = !game.Drawing.IsPlacingTower && point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1;
                var anchor = new Vector2(point.x * field.width, (1 - point.y) * field.height);
                Rect rect = default;
                var style = hordeOverview && badge != selected ? BadgeStyle.Marker : BadgeStyle.Full;
                if (visible)
                {
                    var top = camera.WorldToViewportPoint(new Vector3(cell.X + .5f, cell.Y + 1.35f, 0));
                    int height = style == BadgeStyle.Marker ? MarkerSize : 32;
                    var preferred = new Vector2(anchor.x, (1 - top.y) * field.height - height - 3);
                    visible = TryPlace(preferred, style == BadgeStyle.Marker ? MarkerSize : 88, height, out rect);
                    if (!visible && style == BadgeStyle.Full)
                    {
                        style = BadgeStyle.Narrow;
                        visible = TryPlace(preferred, 44, height, out rect);
                    }
                }
                badge.Button.gameObject.SetActive(visible); badge.Link.gameObject.SetActive(visible);
                if (!visible) continue;
                occupied.Add(rect); VisibleBadgeCount++;
                if (badge.StyleDirty || badge.Style != style) ApplyStyle(badge, style, rect.width);
                if (badge.Selected != (badge == selected))
                {
                    badge.Selected = badge == selected;
                    ui.MarkSelected(badge.Button, badge.Selected);
                }
                if (badge.Bounds == rect && badge.Anchor == anchor) continue;
                badge.Bounds = rect; badge.Anchor = anchor;
                PlayerUi.Place((RectTransform)badge.Button.transform, rect);
                Vector2 start = new Vector2(Mathf.Clamp(anchor.x, rect.xMin + 3, rect.xMax - 3), rect.yMax);
                var delta = anchor - start;
                PlayerUi.Place(badge.Link, new Rect(start.x, start.y, delta.magnitude, 1));
                badge.Link.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(-delta.y, delta.x) * Mathf.Rad2Deg);
            }
        }

        static void ApplyStyle(Badge badge, BadgeStyle style, float width)
        {
            badge.Style = style; badge.StyleDirty = false;
            bool marker = style == BadgeStyle.Marker, narrow = style == BadgeStyle.Narrow;
            badge.Level.gameObject.SetActive(!marker);
            badge.Ink.gameObject.SetActive(!marker);
            badge.ColorRank.gameObject.SetActive(style == BadgeStyle.Full);
            badge.Track.gameObject.SetActive(!marker && badge.State.IsWorking);
            badge.Progress.gameObject.SetActive(!marker && badge.State.IsWorking);
            badge.Status.color = marker && badge.State.Color != InkColor.None ? badge.Ink.color :
                badge.State.CanImprove ? FieldMenuSkin.Accent : FieldMenuSkin.Muted;
            PlayerUi.Place(badge.Status.rectTransform, marker ? new Rect(2, 2, 12, 12) : new Rect(1, 5, narrow ? 16 : 22, 22));
            if (marker) return;
            PlayerUi.Place(badge.Level.rectTransform, new Rect(narrow ? 17 : 25, 0, narrow ? 12 : 30, 26));
            badge.Level.text = narrow ? badge.State.StepsUsed.ToString() : badge.State.StepsUsed + "/6";
            PlayerUi.Place(badge.Ink.rectTransform, new Rect(narrow ? 29 : 55, 8, narrow ? 12 : 14, 16));
            PlayerUi.Place(badge.Progress, new Rect(4, 26, width - 8, 3));
            PlayerUi.Place(badge.Track, new Rect(4, 26, width - 8, 3));
        }

        bool TryPlace(Vector2 preferred, float width, float height, out Rect result)
        {
            // A deterministic short leader keeps dense labels distinct without moving tower art.
            for (int ring = 0; ring < 8; ring++)
            for (int column = 0; column < 3; column++)
            {
                float x = preferred.x - width * .5f + (column == 0 ? 0 : column == 1 ? -width - 4 : width + 4);
                float row = ring % 2 == 1 ? -(ring + 1) / 2 : ring / 2;
                float y = preferred.y + row * (height + 3);
                var candidate = new Rect(Mathf.Clamp(x, 2, layout.Field.width - width - 2),
                    Mathf.Clamp(y, 2, layout.Field.height - height - 2), width, height);
                if (candidate.xMin < 2 || candidate.yMin < 2 || candidate.xMax > layout.Field.width - 2 || candidate.yMax > layout.Field.height - 2) continue;
                if (candidate.Overlaps(new Rect(0, 0, 370, 40))) continue;
                if (inspector != null && inspector.gameObject.activeSelf && candidate.Overlaps(LocalBounds(inspector))) continue;
                if (notice != null && notice.gameObject.activeSelf && candidate.Overlaps(LocalBounds(notice))) continue;
                if (reward != null && reward.gameObject.activeSelf && candidate.Overlaps(LocalBounds(reward))) continue;
                bool overlaps = false;
                if (approaches != null) foreach (var approach in approaches)
                    if (approach != null && approach.gameObject.activeSelf && candidate.Overlaps(LocalBounds(approach))) { overlaps = true; break; }
                if (overlaps) continue;
                foreach (var used in occupied)
                    if (candidate.Overlaps(new Rect(used.x - 2, used.y - 2, used.width + 4, used.height + 4))) { overlaps = true; break; }
                if (overlaps) continue;
                result = candidate; return true;
            }
            result = default; return false;
        }

        Rect LocalBounds(RectTransform rect) => new Rect(rect.anchoredPosition.x - layout.Field.x,
            -rect.anchoredPosition.y - layout.Field.y, rect.rect.width, rect.rect.height);
    }
}
