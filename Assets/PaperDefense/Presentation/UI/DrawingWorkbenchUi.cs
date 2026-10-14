using System;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    public delegate bool DrawingAidPurchase(string id, out string reason);

    /// <summary>Application callbacks are queried afresh: successful transactions may replace the read model.</summary>
    public sealed class DrawingEditorContext
    {
        public Func<TowerBlueprint, TowerPreview> Preview { get; set; }
        public Func<WorkshopReadModel> Workshop { get; set; }
        public DrawingAidPurchase Purchase { get; set; }
    }

    internal sealed class DrawingWorkbenchUi
    {
        readonly Font font;
        readonly UserTextResolver resolver;
        internal static readonly Color Selection = new Color(.79f, .88f, .96f);
        public DrawingWorkbenchUi(Font font, UserTextResolver resolver) { this.font = font; this.resolver = resolver; }
        public string Resolve(string value) => GameText.Resolve(resolver, value);
        public static RectTransform Box(Transform parent, string name, Rect box, Color color, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); Place(rect, box);
            var image = go.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = raycast; return rect;
        }
        public UnityEngine.UI.Text Label(Transform parent, string name, string value, Rect box, int size = 20, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text)); go.transform.SetParent(parent, false); Place(go.GetComponent<RectTransform>(), box);
            var text = go.GetComponent<UnityEngine.UI.Text>(); text.font = font; text.text = Resolve(value); text.fontSize = size; text.color = color ?? NotebookPalette.Navy;
            text.raycastTarget = false; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
        public UnityEngine.UI.Button Button(Transform parent, string name, Rect box, string caption, Action action, bool primary = false)
        {
            var rect = Box(parent, name, box, primary ? NotebookPalette.Blue : Color.white, true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.onClick.AddListener(() => action());
            var colors = button.colors; colors.disabledColor = new Color(.8f, .81f, .8f, .62f); button.colors = colors;
            var text = Label(rect, name + " label", caption, new Rect(6, 2, box.width - 12, box.height - 4), 19, primary ? Color.white : NotebookPalette.Navy);
            text.alignment = TextAnchor.MiddleCenter; return button;
        }
        public static void Place(RectTransform rect, Rect box)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(box.x, -box.y); rect.sizeDelta = box.size; }
    }
}
