using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        bool settingsFullscreen;
        bool audioSettingsOpen;
        GameObject audioSettingsOverlay;

        void Settings()
        {
            bool assembly = game.Run.AssemblyCatalog != null;
            NotebookPageBackdrop();
            MenuHeader("Настройки", game.CloseSettings, "Экран, звук и доступность",
                "Настройки действуют сразу. Размер окна меняется только по кнопке «Применить». Закрытие отменяет неприменённый размер.");
            page.Find("Notebook header/Back").name = "Settings back";
            const float contentHeight = 460, footerHeight = 52;
            bool desktop = W >= 1120;
            float total = Mathf.Min(desktop ? 1300 : 900, W - 48);
            float left = (W - total) * .5f, top = desktop ? Mathf.Max(96, Mathf.Round((H - 492) * .47f)) : 120;
            float previewWidth = desktop ? total * .33f : 0;
            float width = desktop ? total - previewWidth - 18 : total;
            float available = Mathf.Max(100, H - top - 16 - footerHeight - 24);
            bool scrolling = available < contentHeight;
            float panelHeight = Mathf.Min(available, contentHeight);
            float footerY = top + panelHeight + 16;
            var selected = game.DisplaySettings.SelectedResolution;
            if (desktop)
            {
                SettingsPreview(new Rect(left, top, previewWidth, panelHeight), selected);
                left += previewWidth + 18;
            }
            else
                NotebookLabel(page, "ДЕРЖИ ЛИНИЮ", new Rect(left, 80, width, 30), 18, PlayerUi.Muted);
            var panel = ui.Panel(page, "Display settings sheet", new Rect(left, top, width, panelHeight), PlayerUi.Card);
            RectTransform content = scrolling
                ? ui.Scroll(panel, "Settings responsive scroll", new Rect(0, 0, width, available), contentHeight)
                : ui.Panel(panel, "Settings content", new Rect(0, 0, width, contentHeight), Color.clear);
            float rowWidth = width - (scrolling ? 16 : 0);
            settingsFullscreen = Screen.fullScreen;
            SettingsToggleRow(content, "Toggle fullscreen", "Полный экран", "Развернуть игру на весь экран.",
                new Rect(0, 0, rowWidth, 64), PlayerIcon.Settings, settingsFullscreen,
                () => Screen.fullScreen = !Screen.fullScreen, displayIcon: true);

            var resolution = ui.Panel(content, "Resolution settings", new Rect(0, 76, rowWidth, 156), Color.clear);
            SettingsLabel(resolution, "Разрешение экрана", new Rect(14, 0, rowWidth - 28, 30), 23).fontStyle = FontStyle.Bold;
            var current = game.DisplaySettings.CurrentResolution;
            SettingsLabel(resolution, "Сейчас: " + current.x + " × " + current.y,
                new Rect(14, 32, rowWidth - 28, 28), 19, PlayerUi.Muted).name = "Current resolution";
            float controlsWidth = Mathf.Min(rowWidth - 28, 580), controlsX = (rowWidth - controlsWidth) * .5f;
            const float arrowWidth = 52, applyWidth = 160, gap = 10;
            float selectedWidth = controlsWidth - arrowWidth * 2 - applyWidth - gap * 3;
            var previous = ui.IconButton(resolution, "Resolution previous", new Rect(controlsX, 68, arrowWidth, 52),
                PlayerIcon.Back, "Предыдущее разрешение", () => { game.DisplaySettings.SelectRelative(-1); RefreshSetting("Resolution previous"); });
            var dial = ui.Panel(resolution, "Resolution readout", new Rect(controlsX + arrowWidth + gap, 68, selectedWidth, 52), FieldMenuSkin.Background);
            SettingsLabel(dial, selected.x + " × " + selected.y,
                new Rect(6, 0, selectedWidth - 12, 52), 24, alignment: TextAnchor.MiddleCenter).name = "Selected resolution";
            var next = ui.IconButton(resolution, "Resolution next",
                new Rect(controlsX + arrowWidth + selectedWidth + gap * 2, 68, arrowWidth, 52),
                PlayerIcon.Next, "Следующее разрешение", () => { game.DisplaySettings.SelectRelative(1); RefreshSetting("Resolution next"); });
            previous.interactable = next.interactable = game.DisplaySettings.Resolutions.Count > 1;
            bool canApply = game.DisplaySettings.CanApply(Screen.fullScreenMode);
            var apply = SettingsTextAction(resolution, "Apply resolution", "Применить",
                new Rect(controlsX + controlsWidth - applyWidth, 68, applyWidth, 52),
                () => { game.DisplaySettings.Apply(Screen.fullScreenMode); Show(PlayerPage.Settings); }, 22, true);
            apply.interactable = canApply;
            if (game.DisplaySettings.HasPendingChange && !canApply)
            {
                const string reason = "Чтобы применить этот размер окна, выключи полный экран.";
                apply.GetComponent<PlayerTooltip>().Message = reason;
                SettingsLabel(resolution, reason, new Rect(14, 124, rowWidth - 28, 28), 18, NotebookPalette.Red)
                    .name = "Resolution readiness";
            }

            SettingsToggleRow(content, "Toggle ranges", assembly ? JourneyCopy("settings.ranges_title") : "Зоны действия",
                assembly ? JourneyCopy("settings.ranges_body") : "Показывать секторы огня и радиусы поддержки.",
                new Rect(0, 244, rowWidth, 64), PlayerIcon.Target, game.World.ShowRanges,
                () => game.World.ShowRanges = !game.World.ShowRanges);
            SettingsToggleRow(content, "Reduce motion", "Меньше движения", "Убирает движение рисунков и инерцию прокрутки.",
                new Rect(0, 320, rowWidth, 64), PlayerIcon.Pause, game.ReducedMotion,
                () => game.SetReducedMotion(!game.ReducedMotion));
            float footerActionWidth = (rowWidth - 36) * .5f;
            SettingsTextAction(content, "Settings audio", "Звук", new Rect(12, 396, footerActionWidth, 52),
                OpenAudioSettings, 22, false);
            NotebookAction(content, "Open help", new Rect(24 + footerActionWidth, 396, footerActionWidth, 52),
                PlayerIcon.Help, "Управление", () => game.OpenSettings(true));
            var close = SettingsTextAction(page, "Close settings", "Готово",
                new Rect(desktop ? left + width - 232 : (W - 232) * .5f, footerY, 232, footerHeight), game.CloseSettings, 22, true);
            EventSystem.current?.SetSelectedGameObject(close.gameObject);
            if (audioSettingsOpen) OpenAudioSettings();
        }

        void OpenAudioSettings()
        {
            if (game.Page != PlayerPage.Settings || audioSettingsOverlay != null && audioSettingsOverlay.activeInHierarchy) return;
            audioSettingsOpen = true;
            var preferences = game.AudioSettings;
            var previous = page.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == "Settings audio");
            var shade = ui.Panel(page, "Audio settings", new Rect(0, 0, W, H), new Color(.08f, .12f, .18f, .65f), true);
            audioSettingsOverlay = shade.gameObject;
            float width = Mathf.Min(640, W - 48);
            const float height = 360;
            var sheet = ui.Panel(shade, "Audio settings sheet",
                new Rect((W - width) * .5f, (H - height) * .5f, width, height), PlayerUi.Card, true);
            NotebookLabel(sheet, "Звук", new Rect(24, 14, width - 48, 44), 30).fontStyle = FontStyle.Bold;
            SettingsLabel(sheet, "Громкость эффектов", new Rect(24, 74, width - 138, 32), 22);
            var value = SettingsLabel(sheet, "", new Rect(width - 112, 74, 88, 32), 22,
                alignment: TextAnchor.MiddleRight);
            value.name = "Audio settings value";

            var track = ui.Panel(sheet, "Audio settings volume", new Rect(32, 116, width - 64, 52), Color.clear, true);
            var slider = track.gameObject.AddComponent<Slider>();
            slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = true;
            var rail = PlayerUi.Graphic<Image>(track, "Audio volume rail", new Rect(12, 22, width - 88, 8));
            rail.color = FieldMenuSkin.Border;
            var fill = PlayerUi.Graphic<Image>(rail.transform, "Audio volume fill", new Rect(0, 0, width - 88, 8));
            fill.color = FieldMenuSkin.Accent;
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var handleArea = ui.Panel(track, "Audio volume handle area", new Rect(12, 0, width - 88, 52), Color.clear);
            var handle = PlayerUi.Graphic<Image>(handleArea, "Audio volume handle", new Rect(0, 8, 24, 36));
            handle.color = FieldMenuSkin.Accent;
            handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(.5f, .5f);
            handle.rectTransform.pivot = new Vector2(.5f, .5f); handle.rectTransform.anchoredPosition = Vector2.zero;
            // Slider stretches the handle vertically inside its 52 px area.
            handle.rectTransform.sizeDelta = new Vector2(24, -16);
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            var colors = slider.colors; colors.fadeDuration = 0; slider.colors = colors;
            SettingsLabel(sheet, "0%", new Rect(32, 170, 80, 26), 16, PlayerUi.Muted);
            SettingsLabel(sheet, "100%", new Rect(width - 112, 170, 80, 26), 16, PlayerUi.Muted, TextAnchor.MiddleRight);
            var mute = SettingsTextAction(sheet, "Audio settings mute", "", new Rect(24, 208, width - 48, 52),
                () => preferences.SetMuted(!preferences.Muted), 22, false);
            var muteCaption = mute.GetComponentInChildren<Text>();
            Action refreshAudio = () =>
            {
                slider.SetValueWithoutNotify(preferences.SfxVolume * 100);
                value.text = Mathf.RoundToInt(preferences.SfxVolume * 100) + "%";
                muteCaption.text = preferences.Muted ? "Включить звук" : "Выключить звук";
                ui.MarkSelected(mute, preferences.Muted);
            };
            slider.onValueChanged.AddListener(volume => preferences.SetVolume(volume / 100));
            preferences.Changed += refreshAudio;
            var lifetime = shade.gameObject.AddComponent<PlayerAudioSettingsDialogLifetime>();
            lifetime.Disabled = () =>
            {
                preferences.Changed -= refreshAudio;
                if (game == null || game.Page != PlayerPage.Settings) audioSettingsOpen = false;
            };
            Action close = () =>
            {
                audioSettingsOpen = false;
                audioSettingsOverlay = null;
                WorkshopUi.RemoveView(shade.gameObject);
            };
            SettingsLabel(sheet, "← / → — громкость\nEsc — назад", new Rect(24, 282, width - 256, 50), 16, PlayerUi.Muted);
            SettingsTextAction(sheet, "Audio settings close", "Готово", new Rect(width - 208, 284, 184, 52), close, 22, true);
            refreshAudio();
            FieldMenuTheme.ApplyTo(shade, false);
            WorkshopModal.Open(shade.gameObject, close, previous != null ? previous.gameObject : null);
        }

        void SettingsPreview(Rect bounds, Vector2Int resolution)
        {
            var sheet = ui.Panel(page, "Settings display preview", bounds, PlayerUi.Card);
            float inner = bounds.width - 48;
            NotebookLabel(sheet, "Предпросмотр экрана", new Rect(24, 16, inner, 36), 23);
            var stage = new Rect(24, 70, inner, bounds.height - 175);
            float aspect = resolution.x / (float)Mathf.Max(1, resolution.y);
            float screenW = Mathf.Min(stage.width, stage.height * aspect), screenH = screenW / aspect;
            var screen = ui.Panel(sheet, "Selected aspect ratio", new Rect(stage.center.x - screenW * .5f,
                stage.center.y - screenH * .5f, screenW, screenH), FieldMenuSkin.Background);
            var texture = FortressCover;
            if (texture != null)
            {
                float artW = Mathf.Min(screenW * .60f, screenH * 1.5f);
                var illustration = PlayerUi.Graphic<HomeCoverGraphic>(screen, "Cover in selected aspect ratio",
                    new Rect(screenW - artW, (screenH - artW / 1.5f) * .5f, artW, artW / 1.5f));
                illustration.texture = texture;
            }
            var title = NotebookLabel(screen, "ДЕРЖИ ЛИНИЮ", new Rect(10, 0, screenW * .40f, screenH), 18);
            title.font = FieldTypography.Semibold;
            var size = NotebookLabel(sheet, resolution.x + " × " + resolution.y,
                new Rect(24, bounds.height - 90, inner, 42), 32);
            size.font = FieldTypography.Semibold;
            NotebookLabel(sheet, game.DisplaySettings.HasPendingChange ? "Ожидает применения" : "Текущее разрешение",
                new Rect(24, bounds.height - 46, inner, 26), 16, PlayerUi.Muted);
        }

        void RefreshSetting(string controlName)
        {
            Show(PlayerPage.Settings);
            var control = page.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == controlName);
            if (control != null && control.IsInteractable()) EventSystem.current?.SetSelectedGameObject(control.gameObject);
        }

        UnityEngine.UI.Text SettingsLabel(Transform parent, string copy, Rect bounds, int size,
            Color? color = null, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = NotebookLabel(parent, copy, bounds, size, color, alignment);
            label.resizeTextMinSize = Mathf.CeilToInt(16 / Layout.Scale);
            label.resizeTextMaxSize = Mathf.Max(label.resizeTextMinSize, size);
            return label;
        }

        void SettingsToggleRow(Transform parent, string name, string title, string description, Rect bounds,
            PlayerIcon icon, bool value, Action change, bool displayIcon = false)
        {
            var button = ui.IconButton(parent, name, new Rect(bounds.x + 12, bounds.y, bounds.width - 88, bounds.height),
                icon, title + ": " + (value ? "включено" : "выключено"), () => { change(); RefreshSetting(name); });
            var symbol = button.GetComponentInChildren<PlayerIconGraphic>();
            var iconBounds = new Rect(14, 16, 32, 32);
            if (displayIcon)
            {
                symbol.enabled = false;
                PlayerUi.Graphic<PlayerSettingsDisplayGraphic>(button.transform, "Display illustration", iconBounds);
            }
            else PlayerUi.Place(symbol.rectTransform, iconBounds);
            float width = bounds.width - 88;
            const float switchWidth = 52, switchInset = 16, captionWidth = 112;
            float switchX = width - switchInset - switchWidth;
            float captionX = switchX - 12 - captionWidth;
            SettingsLabel(button.transform, title, new Rect(58, 8, captionX - 66, 48), 23).fontStyle = FontStyle.Bold;
            SettingsLabel(button.transform, value ? "Включено" : "Выключено", new Rect(captionX, 8, captionWidth, 48), 19,
                value ? PlayerUi.Ink : PlayerUi.Muted, TextAnchor.MiddleRight);
            var toggle = PlayerUi.Graphic<PlayerSettingsToggleGraphic>(button.transform, "Switch position", new Rect(switchX, 19, switchWidth, 26));
            toggle.Value = value;
            ui.IconButton(parent, name + " help", new Rect(bounds.xMax - 64, bounds.y + 6, 52, 52),
                PlayerIcon.Help, title + ": подробнее", () => ScreenHelp(title, description));
        }

        UnityEngine.UI.Button SettingsTextAction(Transform parent, string name, string caption, Rect bounds,
            Action action, int fontSize, bool primary)
        {
            var button = ui.IconButton(parent, name, bounds, PlayerIcon.Check, caption, action, primary);
            var symbol = button.GetComponentInChildren<PlayerIconGraphic>();
            symbol.gameObject.SetActive(false); Destroy(symbol.gameObject);
            SettingsLabel(button.transform, caption, new Rect(8, 0, bounds.width - 16, bounds.height), fontSize,
                primary ? PlayerUi.Card : PlayerUi.Ink, TextAnchor.MiddleCenter);
            return button;
        }
    }

    /// <summary>Retires UI subscriptions while preserving an open sound sheet across a page resize.</summary>
    sealed class PlayerAudioSettingsDialogLifetime : MonoBehaviour
    {
        public Action Disabled;
        void OnDisable() => Disabled?.Invoke();
    }
}
