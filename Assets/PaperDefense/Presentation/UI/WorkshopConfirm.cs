using System;
using UnityEngine;

namespace PaperDefense.Presentation
{
    internal static class WorkshopConfirm
    {
        public static void Show(Transform parent, WorkshopUi ui, string title, string message, string actionText, Action action, bool enabled = true)
        {
            var rect = parent as RectTransform;
            float width = rect != null && rect.rect.width > 0 ? rect.rect.width : 1600;
            float height = rect != null && rect.rect.height > 0 ? rect.rect.height : 1000;
            float cardWidth = Mathf.Min(720, width - 48), cardHeight = Mathf.Min(440, height - 48);
            var shade = ui.Panel(parent, "Workshop confirmation overlay", new Rect(0, 0, width, height), new Color(.08f, .15f, .25f, .45f), true);
            var card = ui.Panel(shade, "Workshop confirmation", new Rect((width - cardWidth) / 2, (height - cardHeight) / 2, cardWidth, cardHeight), Color.white, true);
            ui.Label(card, "Confirmation title", title, new Rect(32, 24, cardWidth - 64, 72), 34, true);
            ui.Label(card, "Confirmation message", message, new Rect(32, 116, cardWidth - 64, cardHeight - 236), 21);
            Action close = () => { if (shade != null) UnityEngine.Object.DestroyImmediate(shade.gameObject); };
            float buttonWidth = (cardWidth - 88) / 2;
            ui.Button(card, "Workshop cancel confirmation", new Rect(32, cardHeight - 88, buttonWidth, 52), "Вернуться", close, WorkshopIcon.Back);
            ui.Button(card, "Workshop confirm action", new Rect(56 + buttonWidth, cardHeight - 88, buttonWidth, 52), actionText, () => { close(); action(); }, WorkshopIcon.Check, true).interactable = enabled;
            WorkshopModal.Open(shade.gameObject, close);
        }
    }
}
