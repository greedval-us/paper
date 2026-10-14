using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Player-only action presentation over the shared workshop's existing commands.</summary>
    public static class PlayerWorkshopActions
    {
        static readonly Dictionary<string, PlayerIcon> Icons = new Dictionary<string, PlayerIcon> {
            { "New blueprint", PlayerIcon.Plus }, { "Save blueprint", PlayerIcon.Save },
            { "Delete blueprint", PlayerIcon.Trash }, { "Save blueprint as new", PlayerIcon.Copy },
            { "Open drawing editor", PlayerIcon.Pencil }, { "Open trial", PlayerIcon.Target },
            { "Album previous", PlayerIcon.Back }, { "Album next", PlayerIcon.Next },
            { "Start run", PlayerIcon.Play }, { "Retry profile save", PlayerIcon.Save },
            { "Run trial", PlayerIcon.Play }, { "Close trial", PlayerIcon.Back }
        };

        public static void Apply(WorkshopHud workshop, PlayerUi ui)
        {
            foreach (var button in workshop.GetComponentsInChildren<Button>(true))
            {
                if (!Icons.TryGetValue(button.name, out var icon)) continue;
                var label = button.GetComponentInChildren<Text>(true);
                string hint = button.name == "Start run" ? ui.T("prepare") : label != null ? label.text : "";
                if (label != null) label.gameObject.SetActive(false);
                var bounds = ((RectTransform)button.transform).rect;
                float size = Mathf.Min(bounds.width, bounds.height) * .72f;
                var graphic = PlayerUi.Graphic<PlayerIconGraphic>(button.transform, "Action icon",
                    new Rect((bounds.width - size) * .5f, (bounds.height - size) * .5f, size, size));
                graphic.Icon = icon; graphic.Sketch = ui.NotebookStyle; graphic.color = PlayerUi.Ink;
                var tooltip = button.gameObject.AddComponent<PlayerTooltip>();
                tooltip.Message = hint; tooltip.Display = value => ui.Tooltip?.Invoke(value);
            }
        }
    }
}
