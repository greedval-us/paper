using System;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        Rect NotebookPageBackdrop(string resource, string name = "Notebook landscape")
        {
            // The illustration is a whole notebook page. Extra window space is paper, never a
            // reason to crop the castle, the enemies or the pen drawings off the page.
            var paper = PlayerUi.Graphic<Image>(page, "Notebook paper extension", new Rect(0, 0, W, H));
            paper.color = PlayerUi.Paper;
            PlayerUi.Graphic<PlayerPaperGraphic>(page, "Notebook paper ruling", new Rect(0, 0, W, H));
            var sprite = Resources.Load<Sprite>(resource);
            var bounds = NotebookFitRect(sprite, new Rect(0, 0, W, H));
            if (resource.EndsWith("FoundationPageDecor", StringComparison.Ordinal)) bounds.y = H - bounds.height;
            NotebookSprite(page, name, sprite, bounds);
            return bounds;
        }

        Text NotebookLabel(Transform parent, string copy, Rect bounds, int size, Color? color = null,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = ui.Label(parent, copy, bounds, Mathf.Max(12, size), color, true, alignment);
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
            float symbol = bounds.height * .62f, inset = bounds.height * .22f;
            var graphic = button.GetComponentInChildren<PlayerIconGraphic>();
            PlayerUi.Place(graphic.rectTransform, new Rect(inset, (bounds.height - symbol) * .5f, symbol, symbol));
            var text = button.GetComponentInChildren<Text>();
            text.rectTransform.offsetMin = new Vector2(inset * 2 + symbol, bounds.height * .08f);
            text.rectTransform.offsetMax = new Vector2(-inset, -bounds.height * .08f);
            text.resizeTextMinSize = Mathf.Max(Mathf.CeilToInt(15 / Layout.Scale), Mathf.RoundToInt(bounds.height * .25f));
            text.resizeTextMaxSize = Mathf.Max(text.resizeTextMinSize, Mathf.Max(16, Mathf.RoundToInt(bounds.height * .55f)));
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
            if (symbol == null || !NotebookSpriteCatalog.TryGet(id, out var sprite)) return;
            Rect bounds = symbol.rectTransform.rect;
            NotebookSprite(symbol.transform, "Notebook icon illustration", sprite,
                new Rect(0, 0, bounds.width, bounds.height));
            // Retain the semantic native component for navigation and existing UI contracts.
            symbol.enabled = false;
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

        Graphic NotebookBlueprintIllustration(Transform parent, TowerBlueprint blueprint, Rect bounds)
        {
            int index = -1;
            // Art is only a skin for known standard assemblies. Every player drawing stays live geometry.
            if (blueprint != null && blueprint.Origin == DrawingOrigin.LegacyStandard && blueprint.Drawing.IsEmpty &&
                blueprint.Chassis == ChassisKind.Standard && (blueprint.ModuleSelections.Count == 0 ||
                    blueprint.Id == "starter.mortar" && blueprint.Modules == TowerModules.Armor))
            {
                if (blueprint.FirstWeapon == StructureKind.Gun)
                {
                    if (blueprint.WeaponMounts.Count == 1) index = 0;
                    else if (blueprint.WeaponMounts.Count == 2 && blueprint.SecondWeapon == StructureKind.Gun) index = 1;
                }
                else if (blueprint.WeaponMounts.Count == 1)
                    index = blueprint.FirstWeapon == StructureKind.Piercer ? 2 : blueprint.FirstWeapon == StructureKind.Mortar ? 3 : blueprint.FirstWeapon == StructureKind.Fan ? 4 : -1;
            }
            if (index >= 0)
            {
                string[] ids = { "tower.gun", "tower.double", "tower.piercer", "tower.mortar", "tower.fan" };
                if (NotebookSpriteCatalog.TryGet(ids[index], out var sprite))
                    return NotebookSprite(parent, "Preparation tower illustration", sprite, bounds);
            }
            var sketch = ui.Blueprint(parent, blueprint, bounds);
            sketch.name = "Preparation tower illustration";
            return sketch;
        }

        Image NotebookMissionArt(Transform parent, int index, Rect bounds)
        {
            string[] ids = { "hero.castle", "icon.ink-red", "icon.ink-blue", "icon.ink-green" };
            NotebookSpriteCatalog.TryGet(ids[index], out var sprite);
            return NotebookSprite(parent, index == 0 ? "Mission castle illustration" : "Ink bottle " + index,
                sprite, bounds);
        }

        static Image NotebookSprite(Transform parent, string name, Sprite sprite, Rect bounds)
        {
            bounds = NotebookFitRect(sprite, bounds);
            var image = PlayerUi.Graphic<Image>(parent, name, bounds);
            image.sprite = sprite; image.preserveAspect = true;
            image.color = sprite != null ? Color.white : Color.clear;
            return image;
        }

        static Rect NotebookFitRect(Sprite sprite, Rect bounds)
        {
            if (sprite == null) return bounds;
            float fit = Mathf.Min(bounds.width / sprite.rect.width, bounds.height / sprite.rect.height);
            Vector2 size = sprite.rect.size * fit;
            return new Rect(bounds.position + (bounds.size - size) * .5f, size);
        }
    }
}
