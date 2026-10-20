using System;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class CompositeWorkshopPanel
    {
        void Weapons(Transform parent, float w, float h)
        {
            ui.Label(parent, "Composite weapon heading", "Штатное оружие · без основы", new Rect(18, 4, w - 36, 42), 27, true);
            var tower = DraftSession.PreviewRecipe.Tower;
            var frame = DraftSession.PreviewEntry.Snapshot.Definitions.First(part => part.Id == tower.Foundation.Frame.Id);
            int count = Mathf.Clamp((int)frame.Get(AssemblyStat.MountCount, ModifierOperation.Set), 1, 3);
            mount = Mathf.Clamp(mount, 0, count - 1);
            var edit = ui.Button(parent, "Composite edit weapon", new Rect(18, h - 44, w - 36 - (mount > 0 ? 64 : 0), 32), "Собрать оружие", () => { DraftSession.OpenWeapon(mount); Refresh(); }, WorkshopIcon.Settings);
            edit.interactable = tower.Mounts.Any(item => item.SlotIndex == mount);
            if (mount > 0) ui.IconButton(parent, "Composite remove weapon", new Rect(w - 70, h - 44, 52, 32), WorkshopIcon.Trash, T("remove_weapon", mount + 1), () => { DraftSession.RemoveWeapon(mount); Refresh(); });
            var categories = new[] { ComponentCategory.Mechanism, ComponentCategory.Head, ComponentCategory.Sight, ComponentCategory.Magazine, ComponentCategory.Trigger };
            WorkshopIcon[] icons = { WorkshopIcon.Settings, WorkshopIcon.Weapon, WorkshopIcon.Target, WorkshopIcon.Rectangle, WorkshopIcon.Plus };
            float categoryWidth = (w - 60) / 5;
            for (int i = 0; i < categories.Length; i++)
            {
                var category = categories[i];
                var categoryButton = ui.Button(parent, "Composite weapon category " + category,
                    new Rect(18 + i * (categoryWidth + 6), h - 84, categoryWidth, 34),
                    category == ComponentCategory.Head ? "Ствол" : ui.Resolve(UserText.Message("assembly.component.category." + category)), () => {
                        DraftSession.OpenWeapon(mount); DraftSession.SelectCategory(category); Refresh();
                    }, categoryWidth < 112 ? WorkshopIcon.None : icons[i]);
                categoryButton.interactable = edit.interactable;
            }
            var mechanisms = DraftSession.Catalog.Definitions.Where(part => part.Category == ComponentCategory.Mechanism).ToArray();
            int columns = w >= 520 ? 4 : 3;
            float cardHeight = Mathf.Max(128, (h - 162) * .5f);
            var list = new CatalogScrollList(parent, "Composite weapon catalog", new Rect(18, 54, w - 36, h - 150), mechanisms.Length, columns, cardHeight, 12);
            for (int i = 0; i < mechanisms.Length; i++)
            {
                int rowColumns = columns == 4 && i >= 4 ? 3 : columns;
                int column = columns == 4 && i >= 4 ? i - 4 : i % columns;
                float cardWidth = (w - 48 - 12 * (rowColumns - 1)) / rowColumns;
                var part = mechanisms[i]; var card = ui.Panel(list.Content, "Composite weapon " + part.Id, new Rect(column * (cardWidth + 12), i / columns * (cardHeight + 12), cardWidth, cardHeight), WorkshopUi.Paper);
                var weapon = WeaponPreset(part);
                bool owned = DraftSession.IsOwned(part.Id);
                var choose = ui.Button(card, "Composite choose weapon " + part.Id, new Rect(0, 0, cardWidth, cardHeight), "", () => {
                    if (owned)
                    {
                        var recipe = new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] { new WeaponMountRecipe(0, WeaponPreset(part)) }));
                        DraftSession.ApplyPreset(Preset(recipe, part.Name), mount);
                    }
                    else if (tower.Mounts.Any(item => item.SlotIndex == mount)) DraftSession.OpenWeapon(mount);
                    feedback = ""; Refresh();
                }, WorkshopIcon.None);
                choose.GetComponentInChildren<Text>().enabled = false;
                bool selected = tower.Mounts.Any(item => item.SlotIndex == mount && item.Weapon.Mechanism.Id == part.Id);
                ui.SetSelected(choose, selected);
                WeaponPicture(card, weapon, "Composite weapon illustration", new Rect(8, 6, cardWidth - 16, cardHeight - 72));
                var name = ui.Label(card, "Composite weapon name", part.Name, new Rect(8, cardHeight - 65, cardWidth - 16, 40), 21);
                name.fontStyle = FontStyle.Bold;
                name.resizeTextForBestFit = true; name.resizeTextMinSize = 15; name.resizeTextMaxSize = 21; name.alignment = TextAnchor.MiddleCenter;
                var candidate = new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] { new WeaponMountRecipe(0, weapon) }));
                var snapshot = new RecipeSnapshot(candidate, DraftSession.Catalog.BalanceRevision, DraftSession.Catalog.Definitions);
                var price = ui.Label(card, "Composite weapon price", run.PreviewWeaponCost(snapshot) + " штр.",
                    new Rect(8, cardHeight - 24, cardWidth - 16, 20), 18); price.alignment = TextAnchor.MiddleCenter;
                if (!owned)
                {
                    var locked = ui.Label(card, "Composite weapon locked", "Ур. " + part.RequiredLevel,
                        new Rect(cardWidth - 74, 4, 68, 26), 15); locked.alignment = TextAnchor.MiddleRight;
                }
            }
        }

        void WeaponSummary(Transform parent, Rect bounds)
        {
            var sheet = ui.Panel(parent, "Composite summary", bounds, Color.clear);
            float w = bounds.width, h = bounds.height;
            ui.Label(sheet, "Composite preview title", DraftSession.Name, new Rect(18, 4, w - 36, 40), 27, true);
            DrawRecipePreview(sheet, DraftSession.PreviewRecipe, w, Mathf.Max(150, h - 182));
            var tower = DraftSession.PreviewRecipe.Tower;
            var frame = DraftSession.PreviewEntry.Snapshot.Definitions.First(part => part.Id == tower.Foundation.Frame.Id);
            int count = Mathf.Clamp((int)frame.Get(AssemblyStat.MountCount, ModifierOperation.Set), 1, 3);
            float slotWidth = (w - 36 - (count - 1) * 8) / count;
            for (int i = 0; i < count; i++)
            {
                int slot = i;
                var button = ui.Button(sheet, "Composite mount " + i, new Rect(18 + i * (slotWidth + 8), h - 118, slotWidth, 32),
                    T("select_mount", i + 1), () => { mount = slot; Refresh(); });
                ui.SetSelected(button, mount == i);
            }
            var resolved = DraftSession.Resolved;
            var metrics = DraftSession.PreviewMetrics;
            string loadCost = resolved != null ? T("load_cost", resolved.Load, resolved.Capacity, resolved.Cost)
                : metrics != null ? T("load_cost", metrics.Load, metrics.Capacity, metrics.Cost) : T("clearance");
            ui.Label(sheet, "Composite load cost", loadCost, new Rect(18, h - 81, w - 36, 28), 21);
            string health = resolved != null ? T("health", resolved.Health.ToString("0.#"), resolved.BuildSeconds.ToString("0.##"))
                : metrics != null ? T("health", metrics.Health.ToString("0.#"), metrics.BuildSeconds.ToString("0.##")) : "";
            ui.Label(sheet, "Composite health", health, new Rect(18, h - 53, w * .72f - 18, 24), 15);
            var effect = ui.Label(sheet, "Composite effect", resolved == null ? "" : "DPS " + resolved.Dps.ToString("0.##"),
                new Rect(w * .72f, h - 53, w * .28f - 18, 24), 17);
            effect.alignment = TextAnchor.MiddleRight;
            var composition = ui.Label(sheet, "Composite selected composition", CompositionText(true), new Rect(18, h - 29, w - 36, 24), 13);
            composition.alignment = TextAnchor.MiddleLeft;
            composition.raycastTarget = true;
            ui.Tip(composition.gameObject, CompositionText(true));
        }

    }
}
