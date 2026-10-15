using System;
using UnityEngine;

namespace PaperDefense.Presentation
{
    internal static class WorkshopConfirm
    {
        public static void Show(Transform parent, WorkshopUi ui, string title, string message, string actionText, Action action, bool enabled = true)
        {
            var shade = ui.Panel(parent, "Workshop confirmation overlay", new Rect(0, 0, 1600, 1000), new Color(.08f, .15f, .25f, .45f), true);
            var card = ui.Panel(shade, "Workshop confirmation", new Rect(440, 280, 720, 440), Color.white, true);
            ui.Label(card, "Confirmation title", title, new Rect(32, 24, 656, 72), 34, true);
            ui.Label(card, "Confirmation message", message, new Rect(32, 116, 656, 204), 21);
            Action close = () => { if (shade != null) UnityEngine.Object.DestroyImmediate(shade.gameObject); };
            ui.Button(card, "Workshop cancel confirmation", new Rect(32, 352, 308, 52), "Вернуться", close, WorkshopIcon.Back);
            ui.Button(card, "Workshop confirm action", new Rect(364, 352, 324, 52), actionText, () => { close(); action(); }, WorkshopIcon.Check, true).interactable = enabled;
            WorkshopModal.Open(shade.gameObject, close);
        }
    }
}
