using System;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        Rect NotebookPageBackdrop(string name = "Neutral page background")
        {
            var paper = PlayerUi.Graphic<Image>(page, name, new Rect(0, 0, W, H));
            paper.color = PlayerUi.Paper;
            PlayerUi.Graphic<PlayerPaperGraphic>(page, "Notebook paper ruling", new Rect(0, 0, W, H));
            return new Rect(0, 0, W, H);
        }

        Text NotebookLabel(Transform parent, string copy, Rect bounds, int size, Color? color = null,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = ui.Label(parent, copy, bounds, Mathf.Max(12, size), color, size >= 26, alignment);
            PlayerUi.Place(text.rectTransform, bounds);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(size * .45f));
            text.resizeTextMaxSize = Mathf.Max(12, size);
            return text;
        }

        Button NotebookAction(Transform parent, string name, Rect bounds, PlayerIcon icon, string caption,
            Action action, bool primary = false)
        {
            var button = ui.Button(parent, name, bounds, icon, caption, action, primary);
            bool hero = primary && bounds.height >= 64;
            float symbol = Mathf.Min(bounds.height * .62f, hero ? 32 : PlayerUi.ActionIconSize);
            float inset = Mathf.Min(16, bounds.height * .22f);
            var graphic = button.GetComponentInChildren<PlayerIconGraphic>();
            PlayerUi.Place(graphic.rectTransform, new Rect(inset, (bounds.height - symbol) * .5f, symbol, symbol));
            var text = button.GetComponentInChildren<Text>();
            text.rectTransform.offsetMin = new Vector2(inset * 2 + symbol, bounds.height * .08f);
            text.rectTransform.offsetMax = new Vector2(-inset, -bounds.height * .08f);
            text.resizeTextMinSize = Mathf.Max(Mathf.CeilToInt(15 / Layout.Scale), Mathf.RoundToInt(bounds.height * .25f));
            text.resizeTextMaxSize = Mathf.Max(text.resizeTextMinSize, hero ? 30 : PlayerUi.ActionCaptionMaxSize);
            if (!primary)
                NotebookControlIcon(button, name == "Map workshop" || name == "Edit album" ? "icon.workshop" : NotebookIconId(icon));
            return button;
        }

        static string NotebookIconId(PlayerIcon icon)
            => "icon." + icon.ToString().ToLowerInvariant();

        static void NotebookControlIcon(Button button, string id)
        {
            var symbol = button.GetComponentInChildren<PlayerIconGraphic>();
            NotebookInkIcon(symbol, id);
        }

        static void NotebookInkIcon(PlayerIconGraphic symbol, string id)
        {
            if (symbol != null) symbol.enabled = true;
        }

        void NotebookScreenHeader(string title, Action back, string mission = null, bool settings = false, Rect? frame = null, bool currencyCoin = false)
        {
            Rect bounds = frame ?? new Rect(0, 0, W, H);
            float width = bounds.width;
            float s = Mathf.Min(width / 1600f, bounds.height / 900f);
            float height = 64 * s;
            var header = ui.Panel(page, "Notebook header", new Rect(bounds.x, bounds.y, width, height), PlayerUi.Paper);
            float backWidth = (mission == null ? 180 : 245) * s;
            NotebookAction(header, "Back", new Rect(12 * s, 6 * s, backWidth, 50 * s), PlayerIcon.Back, title, back);
            float settingsWidth = settings ? 64 * s : 0;
            float goldWidth = 205 * s, goldX = width - goldWidth - settingsWidth - 12 * s;
            var currency = ui.Panel(header, "Notebook currency", new Rect(goldX, 6 * s, goldWidth, 50 * s), PlayerUi.Card);
            var coin = PlayerUi.Graphic<PlayerIconGraphic>(currency, "Gold symbol", new Rect(10 * s, 10 * s, 30 * s, 30 * s));
            coin.Icon = PlayerIcon.Drop; coin.Sketch = true; coin.color = new Color32(182, 116, 17, 255);
            if (currencyCoin)
            {
                // The atlas has no gold sprite. Use the existing native coin drawing on
                // the repaired mission pages rather than rendering an ink drop as money.
                coin.enabled = false;
                var money = PlayerUi.Graphic<WorkshopIconGraphic>(currency, "Gold coin illustration", new Rect(10 * s, 10 * s, 30 * s, 30 * s));
                money.Icon = WorkshopIcon.Cost; money.color = new Color32(182, 116, 17, 255);
                money.raycastTarget = false;
            }
            else NotebookInkIcon(coin, "icon.gold");
            gold = NotebookLabel(currency, ui.T("gold", game.Run.Workshop.Gold), new Rect(47 * s, 0, goldWidth - 56 * s, 50 * s), Mathf.RoundToInt(25 * s));
            gold.name = "Player gold";
            if (mission != null)
            {
                float available = Mathf.Min(420 * s, goldX - backWidth - 52 * s);
                float x = Mathf.Clamp((width - available) * .5f, backWidth + 26 * s, goldX - available - 12 * s);
                var tag = ui.Panel(header, "Mission heading", new Rect(x, 6 * s, available, 50 * s), PlayerUi.Card);
                NotebookLabel(tag, mission, new Rect(8 * s, 0, available - 16 * s, 50 * s), Mathf.RoundToInt(27 * s), alignment: TextAnchor.MiddleCenter);
            }
            if (settings)
            {
                var button = ui.IconButton(header, "Preparation settings", new Rect(width - 64 * s, 6 * s, 52 * s, 50 * s), PlayerIcon.Settings, "Настройки", () => game.OpenSettings());
                NotebookControlIcon(button, "icon.settings");
            }
        }

        void NotebookBlueprintIllustration(Transform parent, TowerBlueprint blueprint, Rect bounds)
        {
            if (blueprint == null) return;
            StandardAssemblyIllustration.Draw(parent, new AlbumEntry(blueprint.Id, GameText.Resolve(blueprint.Name), 1, blueprint),
                bounds, "Preparation tower illustration");
        }

        NotebookArtGraphic NotebookMissionArt(Transform parent, int index, Rect bounds)
        {
            string[] ids = { "object.castle", "object.well.red", "object.well.blue", "object.well.green" };
            var art = ui.Art(parent, ids[index], bounds);
            art.name = index == 0 ? "Mission castle illustration" : "Ink bottle " + index;
            return art;
        }

    }
}
