using System;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Shared, maskable uGUI primitives for the guided workshop's 1600 by 1000 canvas.</summary>
    public sealed class WorkshopUi
    {
        public static readonly Color Paper = PlayerUi.Paper;
        public static readonly Color Surface = PlayerUi.Wash;
        public static readonly Color Ink = PlayerUi.Ink;
        public static readonly Color Blue = PlayerUi.Blue;
        public static readonly Color Muted = PlayerUi.Muted;
        public static readonly Color Selected = PlayerUi.Selected;
        public static readonly Color Line = new Color32(206, 202, 187, 255);
        public static readonly Color Success = new Color32(90, 144, 123, 255);
        public static readonly Color Warning = new Color32(165, 101, 39, 255);

        readonly UserTextResolver resolver;
        WorkshopTooltipOverlay tooltip;
        public Font BodyFont { get; }
        public Font HeadingFont { get; }
        public UserTextResolver TextResolver => resolver;

        public WorkshopUi(UserTextResolver resolver = null)
        {
            this.resolver = resolver ?? TextResourcesAsset.Default;
            BodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            HeadingFont = Resources.Load<Font>("NotebookHand") ?? BodyFont;
        }

        public string Resolve(string value) => GameText.Resolve(resolver, value);

        public static void Place(RectTransform rect, Rect bounds)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(bounds.x, -bounds.y);
            rect.sizeDelta = bounds.size;
        }

        public static T Graphic<T>(Transform parent, string name, Rect bounds) where T : UnityEngine.UI.Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), bounds);
            var graphic = go.GetComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        public RectTransform Panel(Transform parent, string name, Rect bounds, Color color, bool blocks = false)
        {
            var panel = Graphic<WorkshopSurfaceGraphic>(parent, name, bounds);
            panel.color = color == Color.white ? PlayerUi.Card : color;
            panel.raycastTarget = blocks;
            return panel.rectTransform;
        }

        public UnityEngine.UI.Text Label(Transform parent, string name, string value, Rect bounds,
            int size = 20, bool heading = false, Color? color = null)
        {
            if (heading) bounds.height = Mathf.Max(bounds.height, Mathf.Ceil(size * 1.5f));
            var label = Graphic<UnityEngine.UI.Text>(parent, name, bounds);
            label.font = heading ? HeadingFont : BodyFont;
            label.text = Resolve(value);
            label.fontSize = size;
            label.color = color ?? Ink;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.supportRichText = false;
            return label;
        }

        public UnityEngine.UI.Button Button(Transform parent, string name, Rect bounds, string caption,
            Action action, WorkshopIcon icon = WorkshopIcon.None, bool primary = false)
        {
            var surface = Panel(parent, name, bounds, primary ? Blue : Surface, true);
            var background = surface.GetComponent<WorkshopSurfaceGraphic>();
            background.Radius = 12;
            var button = surface.gameObject.AddComponent<WorkshopButton>();
            float padding = Mathf.Min(16, bounds.width * .08f);
            float iconSize = icon == WorkshopIcon.None ? 0 : Mathf.Min(24, Mathf.Max(0, bounds.height - 16));
            float captionX = padding + (iconSize > 0 ? iconSize + 9 : 0);
            var label = Label(surface, "Caption", caption,
                new Rect(captionX, 4, Mathf.Max(0, bounds.width - captionX - padding), Mathf.Max(0, bounds.height - 8)),
                19, color: primary ? Paper : Ink);
            label.alignment = icon == WorkshopIcon.None ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 15;
            label.resizeTextMaxSize = 19;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(captionX, 4); label.rectTransform.offsetMax = new Vector2(-padding, -4);
            WorkshopIconGraphic symbol = null;
            if (iconSize > 0)
            {
                symbol = Graphic<WorkshopIconGraphic>(surface, "Icon", new Rect(padding, (bounds.height - iconSize) * .5f, iconSize, iconSize));
                symbol.Icon = icon;
            }
            button.Configure(background, label, symbol, primary);
            button.onClick.AddListener(() => { HideTooltip(); action?.Invoke(); });
            Tip(surface.gameObject, caption);
            surface.GetComponent<WorkshopTooltip>().FollowCaption(label);
            return button;
        }

        public UnityEngine.UI.Button IconButton(Transform parent, string name, Rect bounds, WorkshopIcon icon,
            string hint, Action action)
        {
            var surface = Panel(parent, name, bounds, Surface, true);
            var background = surface.GetComponent<WorkshopSurfaceGraphic>();
            background.Radius = 11;
            var button = surface.gameObject.AddComponent<WorkshopButton>();
            float size = Mathf.Clamp(Mathf.Min(bounds.width, bounds.height) - 16, 0, 27);
            var symbol = Graphic<WorkshopIconGraphic>(surface, "Icon",
                new Rect((bounds.width - size) * .5f, (bounds.height - size) * .5f, size, size));
            symbol.Icon = icon;
            button.Configure(background, null, symbol, false);
            button.onClick.AddListener(() => { HideTooltip(); action?.Invoke(); });
            Tip(surface.gameObject, hint);
            return button;
        }

        public void SetSelected(UnityEngine.UI.Button button, bool selected)
        {
            if (button is WorkshopButton workshopButton) workshopButton.SetChosen(selected);
            else if (button != null && button.targetGraphic != null) button.targetGraphic.color = selected ? Selected : Surface;
        }

        public void Tip(GameObject target, string hint)
        {
            if (target == null) return;
            var trigger = target.GetComponent<WorkshopTooltip>() ?? target.AddComponent<WorkshopTooltip>();
            trigger.Bind(this, Resolve(hint));
        }

        public void EnsureTooltip(Transform overlayRoot)
        {
            if (overlayRoot == null || !(overlayRoot is RectTransform)) return;
            if (tooltip != null)
            {
                if (tooltip.transform.parent != overlayRoot)
                {
                    tooltip.Hide();
                    tooltip.transform.SetParent(overlayRoot, false);
                }
                return;
            }
            var panel = Panel(overlayRoot, "WorkshopTooltip", new Rect(0, 0, 240, 56), Ink);
            panel.GetComponent<WorkshopSurfaceGraphic>().Radius = 12;
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var label = Label(panel, "Hint", "", new Rect(14, 11, 212, 34), 18, color: Paper);
            label.alignment = TextAnchor.UpperLeft;
            tooltip = panel.gameObject.AddComponent<WorkshopTooltipOverlay>();
            tooltip.Initialize(label);
            tooltip.Hide();
        }

        internal void ShowTooltip(WorkshopTooltip source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.Hint)) return;
            if (tooltip == null)
            {
                var canvas = source.GetComponentInParent<Canvas>();
                if (canvas != null) EnsureTooltip(canvas.rootCanvas.transform);
            }
            if (tooltip != null) tooltip.Show(source);
        }

        internal void HideTooltip(WorkshopTooltip source)
        {
            if (tooltip != null && tooltip.Source == source) tooltip.Hide();
        }

        public void HideTooltip()
        {
            if (tooltip != null) tooltip.Hide();
        }
    }

    /// <summary>Rounded geometry, including an antialiased edge; no sprite or shader dependency.</summary>
    public sealed class WorkshopSurfaceGraphic : UnityEngine.UI.MaskableGraphic
    {
        const int Segments = 8;
        const int PointCount = (Segments + 1) * 4;
        [SerializeField] float radius = 4;
        float outlineWidth;
        Color outlineColor;

        public float Radius { get => radius; set { if (radius == value) return; radius = value; SetVerticesDirty(); } }
        internal void Outline(float width, Color tint)
        {
            if (outlineWidth == width && outlineColor == tint) return;
            outlineWidth = width;
            outlineColor = tint;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            Rect bounds = rectTransform.rect;
            if (bounds.width <= 0 || bounds.height <= 0) return;
            if (outlineWidth <= 0 && bounds.width > 250 && bounds.height > 150 && color.a > 0)
            { NotebookPaperSurface.Draw(vh, bounds, color); return; }
            float r = Mathf.Clamp(radius, 0, Mathf.Min(bounds.width, bounds.height) * .5f);
            float aa = Mathf.Min(.75f, Mathf.Min(bounds.width, bounds.height) * .25f);
            float stroke = Mathf.Clamp(outlineWidth, 0, Mathf.Min(bounds.width, bounds.height) * .25f);
            Rect solid = Inset(bounds, aa + stroke);
            Fan(vh, solid, Mathf.Max(0, r - aa - stroke), color);
            Color edge = stroke > 0 ? outlineColor : color;
            if (stroke > 0)
                Ring(vh, Inset(bounds, aa), Mathf.Max(0, r - aa), edge, solid, Mathf.Max(0, r - aa - stroke), edge);
            Color transparent = edge;
            transparent.a = 0;
            Ring(vh, bounds, r, transparent, Inset(bounds, aa), Mathf.Max(0, r - aa), edge);
        }

        static Rect Inset(Rect rect, float amount) => new Rect(rect.x + amount, rect.y + amount,
            Mathf.Max(0, rect.width - amount * 2), Mathf.Max(0, rect.height - amount * 2));

        static Vector2 Point(Rect rect, float r, int index)
        {
            int corner = index / (Segments + 1);
            float angle = (corner * 90 + index % (Segments + 1) * 90f / Segments) * Mathf.Deg2Rad;
            float x = corner == 0 || corner == 3 ? rect.xMax - r : rect.xMin + r;
            float y = corner < 2 ? rect.yMax - r : rect.yMin + r;
            return new Vector2(x + Mathf.Cos(angle) * r, y + Mathf.Sin(angle) * r);
        }

        static void Fan(UnityEngine.UI.VertexHelper vh, Rect rect, float r, Color tint)
        {
            int first = vh.currentVertCount;
            vh.AddVert(rect.center, tint, Vector2.zero);
            for (int i = 0; i < PointCount; i++) vh.AddVert(Point(rect, r, i), tint, Vector2.zero);
            for (int i = 0; i < PointCount; i++) vh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % PointCount);
        }

        static void Ring(UnityEngine.UI.VertexHelper vh, Rect outer, float outerR, Color outerColor,
            Rect inner, float innerR, Color innerColor)
        {
            int first = vh.currentVertCount;
            for (int i = 0; i < PointCount; i++)
            {
                vh.AddVert(Point(outer, outerR, i), outerColor, Vector2.zero);
                vh.AddVert(Point(inner, innerR, i), innerColor, Vector2.zero);
            }
            for (int i = 0; i < PointCount; i++)
            {
                int a = first + i * 2, b = first + (i + 1) % PointCount * 2;
                vh.AddTriangle(a, b, a + 1);
                vh.AddTriangle(b, b + 1, a + 1);
            }
        }

        public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
        {
            if (!base.Raycast(screenPoint, eventCamera) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 point)) return false;
            Rect rect = rectTransform.rect;
            float r = Mathf.Clamp(radius, 0, Mathf.Min(rect.width, rect.height) * .5f);
            Vector2 nearest = new Vector2(Mathf.Clamp(point.x, rect.xMin + r, rect.xMax - r),
                Mathf.Clamp(point.y, rect.yMin + r, rect.yMax - r));
            return rect.Contains(point) && (point - nearest).sqrMagnitude <= r * r;
        }
    }

    /// <summary>The Selectable state machine updates every visual together, including disabled captions.</summary>
    internal sealed class WorkshopButton : UnityEngine.UI.Button
    {
        WorkshopSurfaceGraphic background;
        UnityEngine.UI.Text caption;
        WorkshopIconGraphic symbol;
        bool primary, chosen;
        FoundationDetailGraphic decoration;
        internal void SetDecoration(FoundationDetailGraphic value)
        {
            if (decoration != null) { decoration.Focused = false; decoration.Hovered = false; }
            decoration = value; DoStateTransition(currentSelectionState, true);
        }

        internal void Configure(WorkshopSurfaceGraphic surface, UnityEngine.UI.Text label, WorkshopIconGraphic icon, bool prominent)
        {
            background = surface;
            caption = label;
            symbol = icon;
            primary = prominent;
            targetGraphic = surface;
            transition = Transition.None;
            DoStateTransition(currentSelectionState, true);
        }

        internal void SetChosen(bool value)
        {
            chosen = value;
            DoStateTransition(currentSelectionState, true);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (background == null) return;
            bool disabled = state == SelectionState.Disabled;
            bool focused = state == SelectionState.Selected;
            bool hovered = state == SelectionState.Highlighted;
            bool pressed = state == SelectionState.Pressed;
            if (decoration != null) { decoration.Focused = !disabled && focused; decoration.Hovered = !disabled && hovered; }
            Color fill = primary ? WorkshopUi.Blue : chosen ? WorkshopUi.Selected : WorkshopUi.Surface;
            Color foreground = primary ? WorkshopUi.Paper : chosen ? WorkshopUi.Blue : WorkshopUi.Ink;
            if (disabled)
            {
                fill = WorkshopUi.Surface;
                foreground = new Color32(135, 148, 168, 255);
            }
            else if (pressed) fill = primary ? new Color32(34, 72, 180, 255) : new Color32(197, 216, 249, 255);
            else if (hovered || focused) fill = primary ? new Color32(41, 83, 202, 255) : new Color32(224, 234, 251, 255);
            background.color = fill;
            background.Outline(!disabled && (focused || chosen) ? 2 : 0, WorkshopUi.Ink);
            if (caption != null) caption.color = foreground;
            if (symbol != null) symbol.color = foreground;
        }
    }
}
