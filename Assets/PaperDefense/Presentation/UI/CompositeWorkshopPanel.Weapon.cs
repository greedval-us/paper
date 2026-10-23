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
            bool singleDetailRow = w >= 830;
            float detailsHeight = singleDetailRow ? 98 : 136;
            float detailsY = h - detailsHeight - 12;
            var details = ui.Panel(parent, "Composite weapon details", new Rect(14, detailsY, w - 28, detailsHeight), WorkshopUi.Paper);
            ReferenceDetail(details, "Composite weapon details frame", new Rect(0, 0, w - 28, detailsHeight), FoundationDetailGraphic.Kind.Frame);
            var detailsTitle = ui.Label(details, "Composite weapon details title", "Детали оружия", new Rect(12, 0, w - 52, 28), 19, true);
            detailsTitle.alignment = TextAnchor.MiddleCenter;
            float innerWidth = w - 52, actionWidth = singleDetailRow ? 208 : innerWidth;
            float actionX = singleDetailRow ? w - 40 - actionWidth : 12, actionY = singleDetailRow ? 34 : 84;
            var edit = ui.Button(details, "Composite edit weapon", new Rect(actionX, actionY, actionWidth - (mount > 0 ? 54 : 0), 44), "Собрать оружие", () => { DraftSession.OpenWeapon(mount); Refresh(); }, WorkshopIcon.Settings, true);
            edit.interactable = tower.Mounts.Any(item => item.SlotIndex == mount);
            if (mount > 0) ui.IconButton(details, "Composite remove weapon", new Rect(actionX + actionWidth - 48, actionY, 48, 44), WorkshopIcon.Trash, T("remove_weapon", mount + 1), () => { DraftSession.RemoveWeapon(mount); Refresh(); });
            var categories = new[] { ComponentCategory.Mechanism, ComponentCategory.Head, ComponentCategory.Sight, ComponentCategory.Magazine, ComponentCategory.Trigger };
            WorkshopIcon[] icons = { WorkshopIcon.Settings, WorkshopIcon.Weapon, WorkshopIcon.Target, WorkshopIcon.Rectangle, WorkshopIcon.Plus };
            float categoryWidth = (innerWidth - (singleDetailRow ? actionWidth + 14 : 0) - 24) / 5;
            for (int i = 0; i < categories.Length; i++)
            {
                var category = categories[i];
                var categoryButton = ui.Button(details, "Composite weapon category " + category,
                    new Rect(12 + i * (categoryWidth + 6), singleDetailRow ? 30 : 28, categoryWidth, singleDetailRow ? 52 : 50),
                    category == ComponentCategory.Head ? "Ствол" : ui.Resolve(UserText.Message("assembly.component.category." + category)), () => {
                        DraftSession.OpenWeapon(mount); DraftSession.SelectCategory(category); Refresh();
                    }, icons[i]);
                categoryButton.interactable = edit.interactable;
                var symbol = categoryButton.GetComponentInChildren<WorkshopIconGraphic>();
                var caption = categoryButton.GetComponentInChildren<Text>();
                WorkshopUi.Place(symbol.rectTransform, singleDetailRow ? new Rect(8, 11, 30, 30) : new Rect((categoryWidth - 28) * .5f, 2, 28, 28));
                WorkshopUi.Place(caption.rectTransform, singleDetailRow ? new Rect(42, 3, categoryWidth - 48, 46) : new Rect(3, 30, categoryWidth - 6, 20));
                caption.alignment = TextAnchor.MiddleCenter;
                caption.resizeTextMinSize = singleDetailRow ? 15 : 13; caption.resizeTextMaxSize = singleDetailRow ? 19 : 17;
            }
            var mechanisms = DraftSession.Catalog.Definitions.Where(part => part.Category == ComponentCategory.Mechanism).ToArray();
            int columns = w >= 520 ? 4 : 3;
            float catalogHeight = detailsY - 58;
            float cardHeight = Mathf.Max(132, (catalogHeight - 12) * .5f);
            var list = new CatalogScrollList(parent, "Composite weapon catalog", new Rect(18, 48, w - 36, catalogHeight), mechanisms.Length, columns, cardHeight, 12);
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
                var outline = ReferenceDetail(card, "Composite weapon border", new Rect(0, 0, cardWidth, cardHeight), FoundationDetailGraphic.Kind.Frame);
                outline.Selected = selected; ((WorkshopButton)choose).SetDecoration(outline);
                bool shortWideCard = cardHeight < 180 && cardWidth >= 145;
                float textX = shortWideCard ? cardWidth * .42f + 8 : 8, textWidth = cardWidth - textX - 8;
                WeaponPicture(card, weapon, "Composite weapon illustration", shortWideCard
                    ? new Rect(8, 8, textX - 16, cardHeight - 16) : new Rect(8, 8, cardWidth - 16, cardHeight - 86));
                var name = ui.Label(card, "Composite weapon name", part.Name,
                    new Rect(textX, shortWideCard ? owned ? 24 : 40 : cardHeight - 75, textWidth, 44), 21);
                name.fontStyle = FontStyle.Bold;
                name.resizeTextForBestFit = true; name.resizeTextMinSize = 14; name.resizeTextMaxSize = 21; name.alignment = TextAnchor.MiddleCenter;
                var candidate = new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] { new WeaponMountRecipe(0, weapon) }));
                var snapshot = new RecipeSnapshot(candidate, DraftSession.Catalog.BalanceRevision, DraftSession.Catalog.Definitions);
                var price = ui.Label(card, "Composite weapon price", run.PreviewWeaponCost(snapshot) + " штр.",
                    new Rect(textX, cardHeight - 30, textWidth, 26), 20); price.alignment = TextAnchor.MiddleCenter;
                if (!owned)
                {
                    float badgeWidth = Mathf.Min(94, shortWideCard ? textWidth + 8 : cardWidth - 10);
                    var badge = ReferenceDetail(card, "Composite weapon unavailable badge", new Rect(cardWidth - badgeWidth - 4, 4, badgeWidth, 32), FoundationDetailGraphic.Kind.Frame);
                    badge.Fill = WorkshopUi.Paper; badge.Radius = 6; badge.StrokeWidth = 1.8f;
                    var locked = ui.Label(card, "Composite weapon locked", "Ур. " + part.RequiredLevel,
                        new Rect(cardWidth - badgeWidth, 5, badgeWidth - 8, 30), 19); locked.alignment = TextAnchor.MiddleCenter;
                }
            }
        }

        void WeaponSummary(Transform parent, Rect bounds)
        {
            var sheet = ui.Panel(parent, "Composite summary", bounds, Color.clear);
            float w = bounds.width, h = bounds.height;
            ui.Label(sheet, "Composite preview title", DraftSession.Name, new Rect(18, 4, w - 36, 40), 27, true);
            DrawRecipePreview(sheet, DraftSession.PreviewRecipe, w, Mathf.Max(150, h - 260));
            var tower = DraftSession.PreviewRecipe.Tower;
            var frame = DraftSession.PreviewEntry.Snapshot.Definitions.First(part => part.Id == tower.Foundation.Frame.Id);
            int count = Mathf.Clamp((int)frame.Get(AssemblyStat.MountCount, ModifierOperation.Set), 1, 3);
            float slotWidth = (w - 36 - (count - 1) * 8) / count;
            for (int i = 0; i < count; i++)
            {
                int slot = i;
                var button = ui.Button(sheet, "Composite mount " + i, new Rect(18 + i * (slotWidth + 8), h - 188, slotWidth, 40),
                    T("select_mount", i + 1), () => { mount = slot; Refresh(); });
                ui.SetSelected(button, mount == i);
            }
            var resolved = DraftSession.Resolved;
            var metrics = DraftSession.PreviewMetrics;
            int load = resolved?.Load ?? metrics?.Load ?? 0, capacity = resolved?.Capacity ?? metrics?.Capacity ?? 0;
            var statistics = ui.Panel(sheet, "Composite weapon tower statistics", new Rect(18, h - 142, w - 36, 102), WorkshopUi.Paper);
            ReferenceDetail(statistics, "Composite weapon statistics frame", new Rect(0, 0, w - 36, 102), FoundationDetailGraphic.Kind.Frame);
            ui.Label(statistics, "Composite weapon load title", "Нагрузка", new Rect(12, 4, 94, 34), 21);
            var meter = ReferenceDetail(statistics, "Composite weapon load meter", new Rect(110, 11, Mathf.Max(28, w - 252), 22), FoundationDetailGraphic.Kind.LoadMeter);
            meter.Count = load; meter.Total = capacity;
            var loadValue = ui.Label(statistics, "Composite load cost", metrics == null && resolved == null ? "—" : load + " / " + capacity,
                new Rect(w - 136, 4, 88, 34), 23, color: load > capacity ? WorkshopUi.Warning : WorkshopUi.Ink);
            loadValue.alignment = TextAnchor.MiddleRight;
            ui.Label(statistics, "Composite weapon tower price", "Цена башни", new Rect(12, 38, w * .6f - 12, 30), 21);
            var price = ui.Label(statistics, "Composite weapon tower price value", (resolved?.Cost ?? metrics?.Cost ?? 0) + " штр.", new Rect(w * .6f, 38, w * .4f - 48, 30), 23);
            price.alignment = TextAnchor.MiddleRight;
            string health = resolved != null ? T("health", resolved.Health.ToString("0.#"), resolved.BuildSeconds.ToString("0.##"))
                : metrics != null ? T("health", metrics.Health.ToString("0.#"), metrics.BuildSeconds.ToString("0.##")) : "";
            ui.Label(statistics, "Composite health", health, new Rect(12, 72, w * .72f - 30, 24), 15);
            var effect = ui.Label(statistics, "Composite effect", resolved == null ? "" : "DPS " + resolved.Dps.ToString("0.##"),
                new Rect(w * .72f - 18, 72, w * .28f - 30, 24), 17);
            effect.alignment = TextAnchor.MiddleRight;
            var composition = ui.Label(sheet, "Composite selected composition", CompositionText(true), new Rect(18, h - 34, w - 36, 28), 14, color: WorkshopUi.Muted);
            composition.alignment = TextAnchor.MiddleLeft;
            composition.raycastTarget = true;
            ui.Tip(composition.gameObject, CompositionText(true));
        }

    }
}
