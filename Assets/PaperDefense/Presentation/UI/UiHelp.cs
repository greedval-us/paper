using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Click-accessible explanations share the existing modal and keyboard focus lifecycle.</summary>
    public static class UiHelp
    {
        public static GameObject Show(Transform parent, string title, string message)
        {
            var root = (RectTransform)parent;
            var ui = new WorkshopUi();
            float width = root.rect.width, height = root.rect.height;
            var shade = ui.Panel(parent, "Context help", new Rect(0, 0, width, height), new Color(.08f, .12f, .18f, .55f), true);
            float w = Mathf.Min(680, width - 32), h = Mathf.Min(520, height - 32);
            var sheet = ui.Panel(shade, "Context help sheet", new Rect((width - w) * .5f, (height - h) * .5f, w, h), WorkshopUi.Paper, true);
            ui.Label(sheet, "Context help title", title, new Rect(20, 12, w - 40, 48), 27, true);
            float bodyWidth = w - 48;
            var copy = ui.Label(sheet, "Context help text", message, new Rect(4, 0, bodyWidth - 8, 1), 19);
            float copyHeight = Mathf.Ceil(copy.preferredHeight) + 12;
            var body = new CatalogScrollList(sheet, "Context help scroll", new Rect(20, 72, w - 40, h - 152), 1, 1, copyHeight, 0);
            copy.transform.SetParent(body.Content, false);
            WorkshopUi.Place(copy.rectTransform, new Rect(4, 0, bodyWidth - 8, copyHeight));
            body.Content.sizeDelta = new Vector2(0, Mathf.Max(h - 152, copyHeight));
            System.Action close = () => WorkshopUi.RemoveView(shade.gameObject);
            ui.Button(sheet, "Close context help", new Rect(w - 176, h - 64, 156, 44), "Понятно", close, WorkshopIcon.Check, true);
            WorkshopModal.Open(shade.gameObject, close, EventSystem.current?.currentSelectedGameObject);
            if (FieldMenuTheme.IsThemed(parent)) FieldMenuTheme.ApplyTo(shade, false);
            return shade.gameObject;
        }
    }
}
