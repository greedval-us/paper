using System;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        void CompactMenuHeader(string title, Action back, string step, string help)
        {
            float inset = Mathf.Min(32, W * .025f);
            bool wide = W >= 1120;
            float height = wide ? 72 : 56;
            var header = ui.Panel(page, "Notebook header", new Rect(0, 0, W, height), PlayerUi.Paper);
            ui.IconButton(header, "Back", new Rect(inset, (height - 40) * .5f, 40, 40), PlayerIcon.Back, "Назад", back);
            float titleWidth = W - inset * 2 - 320;
            var heading = NotebookLabel(header, title, new Rect(inset + 56, wide ? 12 : 4, wide ? 186 : titleWidth, wide ? 48 : 29), wide ? 32 : 24);
            heading.fontStyle = FontStyle.Bold; heading.resizeTextMinSize = 18;
            var subtitle = NotebookLabel(header, step, wide ? new Rect(inset + 260, 16, W - 720, 40) : new Rect(inset + 56, 33, titleWidth, 20), wide ? 18 : 15, PlayerUi.Muted);
            subtitle.resizeTextMinSize = 14;
            contextHints.Tip(subtitle.gameObject, step);
            gold = NotebookLabel(header, ui.T("gold", game.Run.Workshop.Gold),
                wide ? new Rect(W - inset - 410, 15, 172, 42) : new Rect(W - inset - 244, 4, 196, 24), 16, alignment: TextAnchor.MiddleRight);
            gold.name = "Player gold"; gold.resizeTextMinSize = 14;
            progress = NotebookLabel(header, ProgressCopy(), wide ? new Rect(W - inset - 226, 15, 174, 42) : new Rect(W - inset - 244, 29, 196, 22), 15,
                PlayerUi.Muted, TextAnchor.MiddleRight);
            progress.name = "Player progress"; progress.resizeTextMinSize = 14;
            ui.IconButton(header, "Screen help", new Rect(W - inset - 40, (height - 40) * .5f, 40, 40), PlayerIcon.Help,
                "Условия и подсказки", () => ScreenHelp(title, help));
        }

        Button CompactMenuAction(Transform parent, string name, Rect bounds, PlayerIcon icon, string caption,
            Action action, bool primary = false)
        {
            var button = NotebookAction(parent, name, bounds, icon, caption, action, primary);
            var symbol = button.GetComponentInChildren<PlayerIconGraphic>();
            PlayerUi.Place(symbol.rectTransform, new Rect(10, (bounds.height - 22) * .5f, 22, 22));
            var copy = button.GetComponentInChildren<Text>();
            PlayerUi.Place(copy.rectTransform, new Rect(40, 2, bounds.width - 50, bounds.height - 4));
            copy.resizeTextMinSize = 14; copy.resizeTextMaxSize = 18;
            return button;
        }
    }
}
