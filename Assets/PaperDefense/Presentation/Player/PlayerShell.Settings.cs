using System;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        bool settingsFullscreen;

        void Settings()
        {
            NotebookPageBackdrop("Art/Player/SettingsLandscape");
            float s = Mathf.Min(W / 1600f, H / 900f);
            var composition = ui.Panel(page, "Settings composition",
                new Rect((W - 1600 * s) * .5f, (H - 900 * s) * .5f, 1600 * s, 900 * s), Color.clear);
            NotebookAction(composition, "Settings back", new Rect(24 * s, 16 * s, 270 * s, 54 * s),
                PlayerIcon.Back, "Настройки", game.CloseSettings);
            if (NotebookSpriteCatalog.TryGet("logo.settings", out var wordmark))
                NotebookSprite(composition, "Settings wordmark", wordmark, new Rect(360 * s, 16 * s, 880 * s, 214 * s));
            else
            {
                var title = NotebookLabel(composition, "ДЕРЖИ ЛИНИЮ", new Rect(360 * s, 78 * s, 880 * s, 104 * s),
                    Mathf.RoundToInt(84 * s), alignment: TextAnchor.MiddleCenter);
                title.fontStyle = FontStyle.Bold;
                NotebookLabel(composition, "БАШНИ ЗАЩИЩАЮТ ГЛАВНОЕ", new Rect(533 * s, 184 * s, 534 * s, 42 * s),
                    Mathf.RoundToInt(24 * s), alignment: TextAnchor.MiddleCenter);
            }

            var panel = ui.Panel(composition, "Display settings sheet", new Rect(375 * s, 246 * s, 850 * s, 428 * s), PlayerUi.Card);
            settingsFullscreen = Screen.fullScreen;
            SettingsToggleRow(panel, "Toggle fullscreen", "Полный экран", "Развернуть игру на весь экран",
                new Rect(20 * s, 14 * s, 810 * s, 88 * s), PlayerIcon.Target, settingsFullscreen,
                () => Screen.fullScreen = !Screen.fullScreen, s, true);

            var resolution = ui.Panel(panel, "Resolution settings", new Rect(20 * s, 116 * s, 810 * s, 96 * s), Color.clear);
            var displayIcon = PlayerUi.Graphic<PlayerSettingsDisplayGraphic>(resolution, "Resolution display", new Rect(10 * s, 19 * s, 62 * s, 62 * s));
            displayIcon.color = PlayerUi.Ink;
            NotebookLabel(resolution, "Разрешение экрана", new Rect(86 * s, 6 * s, 260 * s, 40 * s), Mathf.RoundToInt(26 * s));
            bool canApply = game.DisplaySettings.CanApply(Screen.fullScreenMode);
            var current = game.DisplaySettings.CurrentResolution;
            var currentCaption = NotebookLabel(resolution, "Сейчас: " + current.x + " × " + current.y,
                new Rect(86 * s, 47 * s, 260 * s, 43 * s), Mathf.RoundToInt(22 * s), PlayerUi.Muted);
            currentCaption.name = "Current resolution";
            // TextGenerator rounds font sizes after CanvasScaler; leave one pixel above the
            // readability floor so a nominal 14 does not render just below 14 screen pixels.
            currentCaption.resizeTextMinSize = Mathf.CeilToInt(15 / Layout.Scale);
            currentCaption.resizeTextMaxSize = Mathf.Max(currentCaption.resizeTextMinSize, currentCaption.resizeTextMaxSize);
            var previous = ui.IconButton(resolution, "Resolution previous", new Rect(348 * s, 22 * s, 42 * s, 52 * s),
                PlayerIcon.Back, "Предыдущее разрешение", () => { game.DisplaySettings.SelectRelative(-1); Show(PlayerPage.Settings); });
            var selected = game.DisplaySettings.SelectedResolution;
            var caption = NotebookLabel(resolution, selected.x + " × " + selected.y, new Rect(396 * s, 18 * s, 156 * s, 60 * s),
                Mathf.RoundToInt(25 * s), alignment: TextAnchor.MiddleCenter);
            caption.name = "Selected resolution"; caption.resizeTextMinSize = Mathf.CeilToInt(15 / Layout.Scale);
            caption.resizeTextMaxSize = Mathf.Max(caption.resizeTextMinSize, caption.resizeTextMaxSize);
            var next = ui.IconButton(resolution, "Resolution next", new Rect(558 * s, 22 * s, 42 * s, 52 * s),
                PlayerIcon.Next, "Следующее разрешение", () => { game.DisplaySettings.SelectRelative(1); Show(PlayerPage.Settings); });
            previous.interactable = next.interactable = game.DisplaySettings.Resolutions.Count > 1;
            var apply = SettingsTextAction(resolution, "Apply resolution", "Применить", new Rect(610 * s, 22 * s, 180 * s, 52 * s),
                () => { game.DisplaySettings.Apply(Screen.fullScreenMode); Show(PlayerPage.Settings); }, Mathf.RoundToInt(23 * s), true, s);
            apply.interactable = canApply;
            if (game.DisplaySettings.HasPendingChange && !canApply)
                apply.GetComponent<PlayerTooltip>().Message = "Монитор не сообщил этот полный экранный режим. Выключи полный экран, чтобы применить размер окна.";

            SettingsToggleRow(panel, "Toggle ranges", "Радиусы башен", "Показывать дальность построек",
                new Rect(20 * s, 226 * s, 810 * s, 88 * s), PlayerIcon.Target, game.World.ShowRanges,
                () => game.World.ShowRanges = !game.World.ShowRanges, s);
            SettingsToggleRow(panel, "Reduce motion", "Меньше движения", "Спокойные рисунки и панели",
                new Rect(20 * s, 324 * s, 810 * s, 88 * s), PlayerIcon.Pause, game.ReducedMotion,
                () => game.SetReducedMotion(!game.ReducedMotion), s);
            NotebookAction(composition, "Open help", new Rect(375 * s, 690 * s, 850 * s, 64 * s),
                PlayerIcon.Help, "Управление", () => game.OpenSettings(true));
            SettingsTextAction(composition, "Close settings", "Готово", new Rect(650 * s, 788 * s, 300 * s, 70 * s),
                game.CloseSettings, Mathf.RoundToInt(44 * s), true, s);
        }

        void SettingsToggleRow(Transform parent, string name, string title, string description, Rect bounds,
            PlayerIcon icon, bool value, Action change, float s, bool monitor = false)
        {
            var button = ui.IconButton(parent, name, bounds, icon, title + ": " + (value ? "включено" : "выключено"),
                () => { change(); Show(PlayerPage.Settings); });
            var symbol = button.GetComponentInChildren<PlayerIconGraphic>();
            if (monitor)
            {
                symbol.gameObject.SetActive(false); Destroy(symbol.gameObject);
                var screen = PlayerUi.Graphic<PlayerSettingsDisplayGraphic>(button.transform, "Display symbol", new Rect(10 * s, 13 * s, 62 * s, 62 * s));
                screen.color = PlayerUi.Ink;
            }
            else PlayerUi.Place(symbol.rectTransform, new Rect(10 * s, 13 * s, 62 * s, 62 * s));
            NotebookLabel(button.transform, title, new Rect(86 * s, 5 * s, 510 * s, 42 * s), Mathf.RoundToInt(31 * s));
            NotebookLabel(button.transform, description, new Rect(86 * s, 48 * s, 510 * s, 30 * s),
                Mathf.RoundToInt(21 * s), PlayerUi.Muted);
            var toggle = PlayerUi.Graphic<PlayerSettingsToggleGraphic>(button.transform, "Setting switch", new Rect(676 * s, 20 * s, 110 * s, 50 * s));
            toggle.Value = value;
        }

        Button SettingsTextAction(Transform parent, string name, string caption, Rect bounds, Action action, int fontSize, bool primary, float scale)
        {
            var button = ui.IconButton(parent, name, bounds, PlayerIcon.Check, caption, action, primary);
            var symbol = button.GetComponentInChildren<PlayerIconGraphic>();
            symbol.gameObject.SetActive(false); Destroy(symbol.gameObject);
            float inset = 8 * scale;
            var label = NotebookLabel(button.transform, caption, new Rect(inset, 0, bounds.width - inset * 2, bounds.height), fontSize,
                primary ? PlayerUi.Card : PlayerUi.Ink, TextAnchor.MiddleCenter);
            label.resizeTextMinSize = Mathf.CeilToInt(15 / Layout.Scale);
            label.resizeTextMaxSize = Mathf.Max(label.resizeTextMinSize, fontSize);
            return button;
        }
    }
}
