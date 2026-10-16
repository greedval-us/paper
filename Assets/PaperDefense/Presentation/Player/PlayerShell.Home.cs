using System;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        void BuildNotebookHome()
        {
            var backdrop = PlayerUi.Graphic<Image>(page, "Home illustration", new Rect(0, 0, W, H));
            backdrop.sprite = Resources.Load<Sprite>("Art/Player/HomeDefenseLandscape");
            backdrop.color = backdrop.sprite != null ? Color.white : PlayerUi.Paper;
            if (backdrop.sprite != null)
            {
                var fit = backdrop.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = backdrop.sprite.rect.width / backdrop.sprite.rect.height;
            }

            // Controls honour accessibility scale; only decorative branding yields when height is tight.
            float responsive = Mathf.Min(W / 1600f, H / 900f);
            float scale = responsive * game.InterfaceScale;
            float brandAvailable = H - 450 * scale - 135 * responsive;
            float brandScale = Mathf.Min(scale, Mathf.Max(0, brandAvailable) / 305);
            float brandHeight = 305 * brandScale;
            float gap = 29 * responsive;
            float blockHeight = brandHeight + gap + 450 * scale;
            float top = Mathf.Max(22 * responsive, (H - 60 * responsive - blockHeight) * .5f);
            float left = 44 * responsive;

            // A paper strip protects live controls when the landscape crops on narrower screens.
            if (W / H < 1.55f || game.InterfaceScale > 1)
            {
                float stripWidth = Mathf.Max(490 * responsive, left + 438 * scale);
                var strip = PlayerUi.Graphic<Image>(page, "Home control paper", new Rect(0, 0, stripWidth, H));
                strip.color = PlayerUi.Paper;
                PlayerUi.Graphic<PlayerPaperGraphic>(strip.transform, "Paper ruling", new Rect(0, 0, stripWidth, H));
            }

            var brand = ui.Panel(page, "Home wordmark", new Rect(32 * responsive, top, Mathf.Max(500 * brandScale, 402 * scale), brandHeight), Color.clear);
            if (brandScale < scale * .60f)
            {
                var compactTitle = HomeLettering(brand, "ДЕРЖИ ЛИНИЮ", new Rect(0, 0, 402 * scale, brandHeight), 54 * scale);
                compactTitle.fontStyle = FontStyle.Bold;
            }
            else
            {
                var first = HomeLettering(brand, "ДЕРЖИ", new Rect(0, 0, 465 * brandScale, 138 * brandScale), 122 * brandScale);
                first.fontStyle = FontStyle.Bold;
                var ribbon = ui.Panel(brand, "Wordmark ribbon", new Rect(-4 * brandScale, 114 * brandScale, 470 * brandScale, 126 * brandScale), PlayerUi.Card);
                var second = HomeLettering(ribbon, "ЛИНИЮ", new Rect(8 * brandScale, -10 * brandScale, 454 * brandScale, 140 * brandScale), 122 * brandScale);
                second.fontStyle = FontStyle.Bold;
                var flag = PlayerUi.Graphic<PlayerIconGraphic>(brand, "Notebook flag", new Rect(447 * brandScale, 76 * brandScale, 65 * brandScale, 107 * brandScale));
                flag.Icon = PlayerIcon.Flag; flag.Sketch = true; flag.color = PlayerUi.Ink;
                HomeLettering(brand, "Нарисуй свою оборону", new Rect(40 * brandScale, 250 * brandScale, 450 * brandScale, 52 * brandScale), 39 * brandScale);
            }

            var actions = ui.Panel(page, "Home actions", new Rect(left, top + brandHeight + gap, 402 * scale, 454 * scale), Color.clear);
            var flow = actions.gameObject.AddComponent<VerticalLayoutGroup>();
            flow.spacing = 8 * scale; flow.childAlignment = TextAnchor.UpperLeft;
            flow.childControlWidth = flow.childControlHeight = true;
            flow.childForceExpandWidth = flow.childForceExpandHeight = false;
            var play = HomeAction(actions, "Open campaign", PlayerIcon.Play, "Играть", () => game.Navigate(PlayerPage.Map), 402, 104, 60, scale, true);
            var workshop = HomeAction(actions, "workshop", PlayerIcon.Pencil, ui.T("workshop"), () => game.Navigate(PlayerPage.Workshop), 402, 92, 52, scale);
            var settings = HomeAction(actions, "settings", PlayerIcon.Settings, ui.T("settings"), () => game.OpenSettings(), 402, 84, 52, scale);
            var help = HomeAction(actions, "help", PlayerIcon.Book, "Помощь", () => game.OpenSettings(true), 402, 80, 50, scale);
            var exit = HomeAction(actions, "exit", PlayerIcon.Exit, "Выйти",
                () => Confirm("Выйти из игры?", "Сохранённые чертежи и золото останутся в профиле.", () => UnityEngine.Application.Quit()),
                270, 58, 38, scale);
            Button[] ordered = { play, workshop, settings, help, exit };
            for (int i = 0; i < ordered.Length; i++)
            {
                var navigation = ordered[i].navigation; navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = ordered[(i + ordered.Length - 1) % ordered.Length];
                navigation.selectOnDown = ordered[(i + 1) % ordered.Length];
                ordered[i].navigation = navigation;
            }

            int waves = game.Run.Session.Balance.Waves.Count;
            string waveCopy = waves == 8 ? "Восемь волн." : waves + " волн.";
            HomeLettering(page, "Четыре чертежа. " + waveCopy,
                new Rect(left, H - 50 * responsive, 360 * responsive, 38 * responsive), 20 * responsive);
        }

        Text HomeLettering(Transform parent, string copy, Rect bounds, float fontSize)
        {
            var label = ui.Label(parent, copy, bounds, Mathf.Max(12, Mathf.RoundToInt(fontSize)), PlayerUi.Ink, title: true);
            PlayerUi.Place(label.rectTransform, bounds);
            label.resizeTextForBestFit = true;
            label.resizeTextMaxSize = Mathf.Max(12, Mathf.RoundToInt(fontSize));
            label.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(fontSize * .7f));
            return label;
        }

        Button HomeAction(Transform parent, string name, PlayerIcon icon, string caption, Action action,
            float width, float height, float fontSize, float scale, bool primary = false)
        {
            var button = ui.Button(parent, name, new Rect(0, 0, width * scale, height * scale), icon, caption, action, primary);
            var size = button.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = width * scale;
            size.minHeight = size.preferredHeight = height * scale;
            float iconSize = (primary ? 68 : 62) * scale;
            var symbol = button.GetComponentInChildren<PlayerIconGraphic>();
            PlayerUi.Place(symbol.rectTransform, new Rect(22 * scale, (height * scale - iconSize) * .5f, iconSize, iconSize));
            var captionText = button.GetComponentInChildren<Text>();
            captionText.rectTransform.offsetMin = new Vector2(108 * scale, 6 * scale);
            captionText.rectTransform.offsetMax = new Vector2(-14 * scale, -6 * scale);
            captionText.resizeTextMinSize = Mathf.Max(12, Mathf.RoundToInt(fontSize * scale * .45f));
            captionText.resizeTextMaxSize = Mathf.Max(14, Mathf.RoundToInt(fontSize * scale));
            return button;
        }
    }
}
