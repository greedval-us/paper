using System;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed class PlayerTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public string Message;
        public Action<string> Display;
        public void OnPointerEnter(PointerEventData e) => Display?.Invoke(Message);
        public void OnPointerExit(PointerEventData e) => Display?.Invoke("");
        public void OnSelect(BaseEventData e)
        {
            bool keyboard = !(e is PointerEventData);
            SetFocus(keyboard);
            if (keyboard) UiScrollFocus.Reveal((RectTransform)transform);
            Display?.Invoke(Message);
        }
        public void OnDeselect(BaseEventData e) { SetFocus(false); Display?.Invoke(""); }
        void OnDisable() { SetFocus(false); Display?.Invoke(""); }
        void SetFocus(bool value)
        {
            var card = GetComponent<PlayerCardGraphic>();
            if (card == null || card.Focused == value) return;
            card.Focused = value; card.SetVerticesDirty();
        }
    }

    /// <summary>Keep keyboard navigation visible, including controls inside nested scroll views.</summary>
    public sealed class UiScrollFocus : MonoBehaviour, ISelectHandler
    {
        static readonly Vector3[] corners = new Vector3[4];
        public void OnSelect(BaseEventData eventData)
        {
            if (!(eventData is PointerEventData)) Reveal((RectTransform)transform);
        }
        public static void Reveal(RectTransform target)
        {
            Canvas.ForceUpdateCanvases();
            for (var parent = target.parent; parent != null; parent = parent.parent)
            {
                var scroll = parent.GetComponent<ScrollRect>();
                if (scroll == null || !scroll.isActiveAndEnabled || scroll.content == null || !target.IsChildOf(scroll.content)) continue;
                var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
                var control = BoundsIn(viewport, target);
                var content = BoundsIn(viewport, scroll.content);
                Rect view = viewport.rect;
                Vector2 position = scroll.normalizedPosition;
                float dx = control.min.x < view.xMin + 4 ? view.xMin + 4 - control.min.x :
                    control.max.x > view.xMax - 4 ? view.xMax - 4 - control.max.x : 0;
                float dy = control.max.y > view.yMax - 4 ? view.yMax - 4 - control.max.y :
                    control.min.y < view.yMin + 4 ? view.yMin + 4 - control.min.y : 0;
                if (scroll.horizontal && content.size.x > view.width)
                    position.x = Mathf.Clamp01(position.x - dx / (content.size.x - view.width));
                if (scroll.vertical && content.size.y > view.height)
                    position.y = Mathf.Clamp01(position.y - dy / (content.size.y - view.height));
                scroll.StopMovement(); scroll.normalizedPosition = position;
            }
            Canvas.ForceUpdateCanvases();
        }
        static Bounds BoundsIn(RectTransform viewport, RectTransform target)
        {
            target.GetWorldCorners(corners);
            var bounds = new Bounds(viewport.InverseTransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++) bounds.Encapsulate(viewport.InverseTransformPoint(corners[i]));
            return bounds;
        }
    }

    /// <summary>Uses the normal uGUI button lifecycle to redraw ink states without moving the control.</summary>
    public sealed class PlayerNotebookButton : UnityEngine.UI.Button
    {
        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (!(targetGraphic is PlayerCardGraphic card)) return;
            bool disabled = state == SelectionState.Disabled;
            bool hovered = state == SelectionState.Highlighted;
            bool pressed = state == SelectionState.Pressed;
            if (card.Disabled == disabled && card.Hovered == hovered && card.Pressed == pressed) return;
            card.Disabled = disabled; card.Hovered = hovered; card.Pressed = pressed;
            card.SetVerticesDirty();
        }
    }

    /// <summary>Shared visual primitives for the player screens. Copy lives in a dedicated text asset.</summary>
    public sealed class PlayerUi
    {
        public static readonly Color Ink = new Color32(21, 48, 105, 255);
        public static readonly Color Blue = new Color32(31, 70, 146, 255);
        public static readonly Color Paper = new Color32(247, 241, 222, 255);
        public static readonly Color Card = new Color32(252, 247, 231, 255);
        public static readonly Color Muted = new Color32(45, 68, 108, 255);
        public static readonly Color Wash = new Color32(235, 230, 211, 255);
        public static readonly Color Selected = new Color32(224, 234, 245, 255);
        public static readonly Color Grid = new Color32(104, 143, 174, 42);
        readonly Font font, heading;
        readonly UserTextResolver copy;
        public bool NotebookStyle { get; }
        public Action<string> Tooltip;
        public PlayerUi(bool notebookStyle = true)
        {
            NotebookStyle = notebookStyle;
            font = Resources.Load<Font>("NotebookNeucha") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            heading = font;
            copy = Resources.Load<TextResourcesAsset>("Text/player-ru").CreateResolver();
        }
        public string T(string key, params object[] args) => copy.Resolve(UserText.Message("player." + key, args));
        public static void Place(RectTransform rect, Rect bounds)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(bounds.x, -bounds.y); rect.sizeDelta = bounds.size;
        }
        public static T Graphic<T>(Transform parent, string name, Rect bounds) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            go.transform.SetParent(parent, false); Place(go.GetComponent<RectTransform>(), bounds);
            var graphic = go.GetComponent<T>(); graphic.raycastTarget = false; return graphic;
        }
        public RectTransform Panel(Transform parent, string name, Rect bounds, Color color, bool blocks = false)
        {
            var graphic = Graphic<PlayerCardGraphic>(parent, name, bounds); graphic.NotebookStyle = NotebookStyle;
            graphic.color = color; graphic.raycastTarget = blocks;
            return graphic.rectTransform;
        }
        public Text Label(Transform parent, string text, Rect bounds, int size = 20, Color? color = null, bool title = false, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            if (title) bounds.height = Mathf.Max(bounds.height, Mathf.Ceil(size * 1.5f));
            var label = Graphic<Text>(parent, "Copy", bounds); label.text = text; label.font = title ? heading : font;
            label.fontStyle = NotebookStyle || title ? FontStyle.Bold : FontStyle.Normal;
            label.fontSize = size; label.color = color ?? Ink; label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        public Button IconButton(Transform parent, string name, Rect bounds, PlayerIcon icon, string hint, Action action, bool primary = false, string art = null)
        {
            var panel = Panel(parent, name, bounds, primary ? Blue : Card, true);
            var card = panel.GetComponent<PlayerCardGraphic>(); card.Control = true;
            var button = panel.gameObject.AddComponent<PlayerNotebookButton>(); button.targetGraphic = card;
            var colors = button.colors; colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.92f, .96f, 1); colors.pressedColor = new Color(.80f, .86f, .96f);
            colors.selectedColor = Color.white; colors.disabledColor = new Color(.72f, .75f, .82f);
            colors.fadeDuration = 0; button.colors = colors;
            button.onClick.AddListener(() => { Tooltip?.Invoke(""); action?.Invoke(); });
            float size = Mathf.Min(bounds.width, bounds.height) * .62f;
            Rect iconBounds = new Rect((bounds.width - size) / 2, (bounds.height - size) / 2, size, size);
            if (art != null) Art(panel, art, iconBounds);
            else { var symbol = Graphic<PlayerIconGraphic>(panel, "Icon", iconBounds); symbol.Icon = icon; symbol.Sketch = NotebookStyle; symbol.color = primary ? Card : Ink; }
            var tooltip = panel.gameObject.AddComponent<PlayerTooltip>(); tooltip.Message = hint; tooltip.Display = text => Tooltip?.Invoke(text);
            return button;
        }
        public Button Button(Transform parent, string name, Rect bounds, PlayerIcon icon, string caption, Action action, bool primary = false)
        {
            var button = IconButton(parent, name, bounds, icon, caption, action, primary);
            var symbol = button.GetComponentInChildren<PlayerIconGraphic>();
            Place(symbol.rectTransform, new Rect(16, (bounds.height - 22) / 2, 22, 22));
            var label = Label(button.transform, caption, new Rect(50, 6, bounds.width - 62, bounds.height - 12), 18, primary ? Card : Ink);
            if (NotebookStyle) label.font = heading;
            label.fontStyle = FontStyle.Bold;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 16; label.resizeTextMaxSize = 22;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(50, 6); label.rectTransform.offsetMax = new Vector2(-12, -6);
            return button;
        }

        public RectTransform Scroll(Transform parent, string name, Rect bounds, float contentHeight)
        {
            var viewport = Panel(parent, name, bounds, Color.clear, true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false); Place(content, new Rect(0, 0, bounds.width, Mathf.Max(bounds.height, contentHeight)));
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
            var player = parent.GetComponentInParent<PlayerGameRoot>(); scroll.inertia = player == null || !player.ReducedMotion;
            var track = Graphic<Image>(viewport, "Scroll track", new Rect(bounds.width - 10, 4, 8, bounds.height - 8));
            track.color = new Color32(104, 143, 174, 48); track.raycastTarget = true;
            track.rectTransform.anchorMin = new Vector2(1, 0); track.rectTransform.anchorMax = new Vector2(1, 1);
            track.rectTransform.pivot = new Vector2(1, 1); track.rectTransform.offsetMin = new Vector2(-10, 4); track.rectTransform.offsetMax = new Vector2(-2, -4);
            var handle = Graphic<Image>(track.transform, "Scroll handle", new Rect(0, 0, 8, 40)); handle.color = new Color32(31, 70, 146, 210); handle.raycastTarget = true;
            var bar = track.gameObject.AddComponent<Scrollbar>(); bar.handleRect = handle.rectTransform; bar.targetGraphic = handle; bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }
        public NotebookArtGraphic Art(Transform parent, string id, Rect bounds)
        { var art = Graphic<NotebookArtGraphic>(parent, id, bounds); art.Bind(id); return art; }
        public BlueprintSketch Blueprint(Transform parent, TowerBlueprint value, Rect bounds)
        { var sketch = Graphic<BlueprintSketch>(parent, "Blueprint", bounds); sketch.SetBlueprint(value); return sketch; }
        public void MarkSelected(Button button, bool selected)
        {
            button.targetGraphic.color = selected ? Selected : Card;
            if (button.targetGraphic is PlayerCardGraphic card && card.Selected != selected)
            { card.Selected = selected; card.SetVerticesDirty(); }
        }

        public static void HorizontalScrollbar(ScrollRect scroll)
        {
            scroll.horizontal = true;
            var track = Graphic<Image>(scroll.transform, "Horizontal scroll track", new Rect(0, 0, 100, 8));
            track.color = new Color32(104, 143, 174, 48); track.raycastTarget = true;
            track.rectTransform.anchorMin = Vector2.zero; track.rectTransform.anchorMax = new Vector2(1, 0);
            track.rectTransform.offsetMin = new Vector2(4, 0); track.rectTransform.offsetMax = new Vector2(-14, 8);
            var handle = Graphic<Image>(track.transform, "Horizontal scroll handle", new Rect(0, 0, 40, 8));
            handle.color = new Color32(31, 70, 146, 210); handle.raycastTarget = true;
            var bar = track.gameObject.AddComponent<Scrollbar>(); bar.handleRect = handle.rectTransform; bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.LeftToRight; scroll.horizontalScrollbar = bar;
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }
    }
}
