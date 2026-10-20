using System;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        void BuildNotebookHome()
        {
            Rect composition = NotebookPageBackdrop("Art/Player/HomeDefenseLandscape", "Home illustration");

            // Layout follows the size of the complete landscape page, without a second UI scale.
            float responsive = Mathf.Min(composition.width / 1600f, composition.height / 900f);
            float scale = responsive;
            bool assembly = game.Run.AssemblyCatalog != null;
            float actionHeight = assembly ? 458 : 454, actionWidth = assembly ? 426 : 402;
            float brandAvailable = composition.height - actionHeight * scale - 135 * responsive;
            float brandScale = Mathf.Min(scale, Mathf.Max(0, brandAvailable) / 305);
            float brandHeight = 305 * brandScale;
            float gap = 29 * responsive;
            float blockHeight = brandHeight + gap + actionHeight * scale;
            float top = composition.y + Mathf.Max(22 * responsive, (composition.height - 60 * responsive - blockHeight) * .5f);
            float left = composition.x + 44 * responsive;

            var brand = ui.Panel(page, "Home wordmark", new Rect(composition.x + 32 * responsive, top, Mathf.Max(500 * brandScale, actionWidth * scale), brandHeight), Color.clear);
            if (NotebookSpriteCatalog.TryGet("logo.home", out var wordmark))
            {
                NotebookSprite(brand, "Home title illustration", wordmark,
                    new Rect(-4 * brandScale, 0, 512 * brandScale, 240 * brandScale));
                HomeLettering(brand, "Собери свою оборону", new Rect(40 * brandScale, 250 * brandScale, 450 * brandScale, 52 * brandScale), 39 * brandScale);
            }
            else if (brandScale < scale * .60f)
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
                HomeLettering(brand, "Собери свою оборону", new Rect(40 * brandScale, 250 * brandScale, 450 * brandScale, 52 * brandScale), 39 * brandScale);
            }

            var actions = ui.Panel(page, "Home actions", new Rect(left, top + brandHeight + gap, actionWidth * scale, actionHeight * scale), Color.clear);
            var flow = actions.gameObject.AddComponent<VerticalLayoutGroup>();
            flow.spacing = 8 * scale; flow.childAlignment = TextAnchor.UpperLeft;
            flow.childControlWidth = flow.childControlHeight = true;
            flow.childForceExpandWidth = flow.childForceExpandHeight = false;
            var play = HomeAction(actions, "Open campaign", PlayerIcon.Play, "Играть", () => game.Navigate(PlayerPage.Map), actionWidth, 104, 60, scale, true);
            Button album = null;
            if (assembly) album = HomeAction(actions, "Open album", PlayerIcon.Book,
                GameText.Resolve(PaperDefense.Core.UserText.Message("assembly.player.album")), game.OpenAlbum, actionWidth, 64, 44, scale);
            var workshop = HomeAction(actions, "workshop", PlayerIcon.Pencil, ui.T("workshop"), () => game.Navigate(PlayerPage.Workshop), actionWidth, assembly ? 72 : 92, 50, scale);
            var settings = HomeAction(actions, "settings", PlayerIcon.Settings, ui.T("settings"), () => game.OpenSettings(), actionWidth, assembly ? 68 : 84, 50, scale);
            var help = HomeAction(actions, "help", PlayerIcon.Book, "Помощь", () => game.OpenSettings(true), actionWidth, assembly ? 64 : 80, 48, scale);
            var exit = HomeAction(actions, "exit", PlayerIcon.Exit, "Выйти",
                () => Confirm("Выйти из игры?", "Сохранённые чертежи и золото останутся в профиле.", () => UnityEngine.Application.Quit()),
                270, assembly ? 46 : 58, 36, scale);
            Button[] ordered = assembly ? new[] { play, album, workshop, settings, help, exit } : new[] { play, workshop, settings, help, exit };
            for (int i = 0; i < ordered.Length; i++)
            {
                var navigation = ordered[i].navigation; navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = ordered[(i + ordered.Length - 1) % ordered.Length];
                navigation.selectOnDown = ordered[(i + 1) % ordered.Length];
                ordered[i].navigation = navigation;
            }

            int waves = game.Run.Session.Balance.Waves.Count;
            string waveCopy = waves == 8 ? "Восемь волн." : waves + " волн.";
            HomeLettering(page, assembly ? GameText.Resolve(PaperDefense.Core.UserText.Message("assembly.player.tagline", waveCopy)) : "Четыре чертежа.\n" + waveCopy,
                new Rect(left, composition.yMax - 74 * responsive, 260 * responsive, 64 * responsive),
                Mathf.Max(14 / Layout.Scale, 20 * responsive));
            if (assembly)
            {
                ui.Panel(page, "Home profile", new Rect(W - 284, 8, 270, 60), PlayerUi.Paper);
                gold = NotebookLabel(page, ui.T("gold", game.Run.Workshop.Gold), new Rect(W - 276, 9, 254, 26), 20, alignment: TextAnchor.MiddleRight);
                gold.name = "Player gold";
                PlayerProgress(new Rect(W - 276, 36, 254, 26));
            }
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
            float iconSize = Mathf.Min(68, height * .76f) * scale;
            var symbol = button.GetComponentInChildren<PlayerIconGraphic>();
            PlayerUi.Place(symbol.rectTransform, new Rect(22 * scale, (height * scale - iconSize) * .5f, iconSize, iconSize));
            if (!primary)
                NotebookControlIcon(button, name == "workshop" ? "icon.workshop" : name == "help" ? "icon.help" : NotebookIconId(icon));
            var captionText = button.GetComponentInChildren<Text>();
            captionText.rectTransform.offsetMin = new Vector2(108 * scale, 6 * scale);
            captionText.rectTransform.offsetMax = new Vector2(-14 * scale, -6 * scale);
            captionText.resizeTextMinSize = Mathf.Max(12, Mathf.RoundToInt(fontSize * scale * .45f));
            captionText.resizeTextMaxSize = Mathf.Max(14, Mathf.RoundToInt(fontSize * scale));
            return button;
        }
    }
}
