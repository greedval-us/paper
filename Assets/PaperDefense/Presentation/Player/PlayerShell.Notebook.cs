using System;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        static Sprite[] preparationTowerArt, missionArt;

        void NotebookPageBackdrop(string resource)
        {
            var image = PlayerUi.Graphic<Image>(page, "Notebook landscape", new Rect(0, 0, W, H));
            image.sprite = Resources.Load<Sprite>(resource);
            image.color = image.sprite != null ? Color.white : PlayerUi.Paper;
            if (image.sprite == null) return;
            image.rectTransform.pivot = new Vector2(.5f, .5f);
            var fit = image.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
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
            return button;
        }

        void NotebookScreenHeader(string title, Action back, string mission = null, bool settings = false)
        {
            float s = Mathf.Min(W / 1600f, H / 900f) * game.InterfaceScale;
            float height = 64 * s;
            var header = ui.Panel(page, "Notebook header", new Rect(0, 0, W, height), PlayerUi.Paper);
            float backWidth = (mission == null ? 180 : 245) * s;
            NotebookAction(header, "Back", new Rect(12 * s, 6 * s, backWidth, 50 * s), PlayerIcon.Back, title, back);
            float settingsWidth = settings ? 64 * s : 0;
            float goldWidth = 205 * s, goldX = W - goldWidth - settingsWidth - 12 * s;
            var currency = ui.Panel(header, "Notebook currency", new Rect(goldX, 6 * s, goldWidth, 50 * s), PlayerUi.Card);
            var coin = PlayerUi.Graphic<PlayerIconGraphic>(currency, "Gold symbol", new Rect(10 * s, 10 * s, 30 * s, 30 * s));
            coin.Icon = PlayerIcon.Drop; coin.Sketch = true; coin.color = new Color32(182, 116, 17, 255);
            gold = NotebookLabel(currency, ui.T("gold", game.Run.Workshop.Gold), new Rect(47 * s, 0, goldWidth - 56 * s, 50 * s), Mathf.RoundToInt(25 * s));
            gold.name = "Player gold";
            if (mission != null)
            {
                float x = backWidth + 34 * s, available = goldX - x - 18 * s;
                var tag = ui.Panel(header, "Mission heading", new Rect(x, 6 * s, available, 50 * s), PlayerUi.Card);
                NotebookLabel(tag, mission, new Rect(8 * s, 0, available - 16 * s, 50 * s), Mathf.RoundToInt(27 * s), alignment: TextAnchor.MiddleCenter);
            }
            if (settings) ui.IconButton(header, "Preparation settings", new Rect(W - 64 * s, 6 * s, 52 * s, 50 * s), PlayerIcon.Settings, "Настройки", () => game.OpenSettings());
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
                if (preparationTowerArt == null) preparationTowerArt = NotebookAtlas("Art/Player/PreparationTowers", new[] {
                    new Rect(22,195,407,290), new Rect(450,197,403,312), new Rect(877,191,407,342),
                    new Rect(1308,188,420,355), new Rect(1752,193,398,325) });
                if (preparationTowerArt != null) return NotebookSprite(parent, "Preparation tower illustration", preparationTowerArt[index], bounds);
            }
            var sketch = ui.Blueprint(parent, blueprint, bounds);
            sketch.name = "Preparation tower illustration";
            return sketch;
        }

        Image NotebookMissionArt(Transform parent, int index, Rect bounds)
        {
            if (missionArt == null) missionArt = NotebookAtlas("Art/Player/MissionIllustrations", new[] {
                new Rect(21,56,715,602), new Rect(806,108,355,442), new Rect(1300,107,358,444), new Rect(1771,110,354,441) });
            return NotebookSprite(parent, index == 0 ? "Mission castle illustration" : "Ink bottle " + index,
                missionArt == null ? null : missionArt[index], bounds);
        }

        static Sprite[] NotebookAtlas(string resource, Rect[] rectangles)
        {
            var texture = Resources.Load<Texture2D>(resource);
            if (texture == null) return null;
            var sprites = new Sprite[rectangles.Length];
            for (int i = 0; i < sprites.Length; i++)
                sprites[i] = Sprite.Create(texture, rectangles[i], new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            return sprites;
        }

        static Image NotebookSprite(Transform parent, string name, Sprite sprite, Rect bounds)
        {
            if (sprite != null)
            {
                float fit = Mathf.Min(bounds.width / sprite.rect.width, bounds.height / sprite.rect.height);
                Vector2 size = sprite.rect.size * fit;
                bounds = new Rect(bounds.position + (bounds.size - size) * .5f, size);
            }
            var image = PlayerUi.Graphic<Image>(parent, name, bounds);
            image.sprite = sprite; image.preserveAspect = true;
            image.color = sprite != null ? Color.white : Color.clear;
            return image;
        }
    }
}
