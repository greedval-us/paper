using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        void ConfigureContextHints(PlayerPage current)
        {
            if (current == PlayerPage.Workshop)
            {
                var panel = game.GetComponentInChildren<WorkshopHud>(true)?.Composite;
                var workshop = panel?.Root;
                if (workshop != null)
                {
                    var buttons = workshop.GetComponentsInChildren<Button>();
                    var next = buttons.FirstOrDefault(button => button.IsInteractable() &&
                        (button.name == "Sequence next" || button.name == "Structure next" || button.name == "Sequence save" || button.name == "Structure save"))
                        ?? buttons.FirstOrDefault(button => button.IsInteractable());
                    if (next != null) EventSystem.current?.SetSelectedGameObject(next.gameObject);
                    panel.HideHints();
                }
                return;
            }
            if (current == PlayerPage.Battle || current == PlayerPage.Album) return;
            contextHints.EnsureTooltip(page);
            foreach (var hint in page.GetComponentsInChildren<PlayerTooltip>())
                if (!string.IsNullOrWhiteSpace(hint.Message) && hint.Message != hint.GetComponentInChildren<Text>()?.text)
                    contextHints.Tip(hint.gameObject, hint.Message);
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != null) return;
            string primary = current == PlayerPage.Home ? "Open campaign" : current == PlayerPage.Campaign ? "Open mission" : current == PlayerPage.Map ? "Prepare mission" :
                current == PlayerPage.Preparation ? "Start battle" : null;
            var controls = page.GetComponentsInChildren<Button>();
            var target = controls.FirstOrDefault(button => button.name == primary && button.IsInteractable())
                ?? controls.FirstOrDefault(button => button.IsInteractable());
            if (target != null) EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        void ScreenHelp(string title, string message)
            => UiHelp.Show(page, title, message);

        void MenuHeader(string title, System.Action back, string step, string help)
        {
            float inset = Mathf.Min(32, W * .025f);
            var header = ui.Panel(page, "Notebook header", new Rect(0, 0, W, 72), PlayerUi.Card);
            NotebookAction(header, "Back", new Rect(inset, 12, 122, 48), PlayerIcon.Back, "Назад", back);
            NotebookLabel(header, title, new Rect(inset + 138, 8, Mathf.Max(100, W - 550), 32), 26);
            string guidance = W < 1100 && step.StartsWith("1.") ? "1. Подготовь оборону" :
                W < 1100 && step.StartsWith("2.") ? "2. Боевой набор" : step;
            var stepLabel = NotebookLabel(header, guidance, new Rect(inset + 138, 40, Mathf.Max(100, W - 550), 23), 16, PlayerUi.Muted);
            stepLabel.resizeTextMinSize = 15;
            gold = NotebookLabel(header, ui.T("gold", game.Run.Workshop.Gold), new Rect(W - inset - 324, 8, 184, 28), 20, alignment: TextAnchor.MiddleRight);
            gold.name = "Player gold";
            progress = NotebookLabel(header, ProgressCopy(), new Rect(W - inset - 346, 39, 206, 23), 16, alignment: TextAnchor.MiddleRight);
            progress.name = "Player progress";
            var helpButton = NotebookAction(header, "Screen help", new Rect(W - inset - 124, 12, 124, 48), PlayerIcon.Book, "Помощь",
                () => ScreenHelp(title, help));
            var helpCopy = helpButton.GetComponentInChildren<Text>();
            PlayerUi.Place(helpButton.GetComponentInChildren<PlayerIconGraphic>().rectTransform, new Rect(12, 12, 24, 24));
            PlayerUi.Place(helpCopy.rectTransform, new Rect(44, 10, 72, 28));
            helpCopy.resizeTextMinSize = 16; helpCopy.resizeTextMaxSize = 18;
        }
    }
}
