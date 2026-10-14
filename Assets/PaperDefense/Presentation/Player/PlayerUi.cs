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
        public void OnSelect(BaseEventData e) => Display?.Invoke(Message);
        public void OnDeselect(BaseEventData e) => Display?.Invoke("");
        void OnDisable() => Display?.Invoke("");
    }

    /// <summary>Shared visual primitives for the player screens. Copy lives in a dedicated text asset.</summary>
    public sealed class PlayerUi
    {
        public static readonly Color Ink = new Color(.12f, .22f, .36f);
        public static readonly Color Blue = new Color(.16f, .38f, .63f);
        public static readonly Color Paper = new Color(.965f, .951f, .905f);
        public static readonly Color Card = new Color(.995f, .988f, .956f);
        public static readonly Color Muted = new Color(.40f, .47f, .52f);
        public static readonly Color Wash = new Color(.85f, .9f, .92f);
        readonly Font font, heading;
        readonly UserTextResolver copy;
        public bool NotebookStyle { get; }
        public Action<string> Tooltip;
        public PlayerUi(bool notebookStyle = false)
        {
            NotebookStyle = notebookStyle;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            heading = Resources.Load<Font>("NotebookHand") ?? font;
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
            var label = Graphic<Text>(parent, "Copy", bounds); label.text = text; label.font = title ? heading : font;
            label.fontSize = size; label.color = color ?? Ink; label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        public Button IconButton(Transform parent, string name, Rect bounds, PlayerIcon icon, string hint, Action action, bool primary = false, string art = null)
        {
            var panel = Panel(parent, name, bounds, primary ? Blue : Card, true);
            var button = panel.gameObject.AddComponent<Button>(); button.targetGraphic = panel.GetComponent<PlayerCardGraphic>();
            var colors = button.colors; colors.highlightedColor = new Color(.86f, .92f, 1); colors.pressedColor = new Color(.65f, .79f, .94f); colors.selectedColor = colors.normalColor; colors.disabledColor = new Color(.75f, .75f, .75f, .4f); button.colors = colors;
            button.onClick.AddListener(() => { Tooltip?.Invoke(""); action?.Invoke(); });
            float size = Mathf.Min(bounds.width, bounds.height) * .62f;
            Rect iconBounds = new Rect((bounds.width - size) / 2, (bounds.height - size) / 2, size, size);
            if (art != null) Art(panel, art, iconBounds);
            else { var symbol = Graphic<PlayerIconGraphic>(panel, "Icon", iconBounds); symbol.Icon = icon; symbol.Sketch = NotebookStyle; symbol.color = primary ? Card : Ink; }
            var tooltip = panel.gameObject.AddComponent<PlayerTooltip>(); tooltip.Message = hint; tooltip.Display = text => Tooltip?.Invoke(text);
            return button;
        }
        public NotebookArtGraphic Art(Transform parent, string id, Rect bounds)
        { var art = Graphic<NotebookArtGraphic>(parent, id, bounds); art.Bind(id); return art; }
        public BlueprintSketch Blueprint(Transform parent, TowerBlueprint value, Rect bounds)
        { var sketch = Graphic<BlueprintSketch>(parent, "Blueprint", bounds); sketch.SetBlueprint(value); return sketch; }
        public void MarkSelected(Button button, bool selected)
        { button.targetGraphic.color = selected ? new Color(.69f, .82f, .91f) : Card; }
    }
}
