using System;
using System.Collections.Generic;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Screen 04: a catalog and a foundation-only preview, with live content values.</summary>
    internal sealed class WizardFoundationPanel
    {
        readonly Transform parent;
        readonly WorkshopUi ui;
        readonly RunController run;
        readonly WorkshopWizard wizard;
        readonly Action<string, Action> purchase;
        readonly List<RectTransform> cards = new List<RectTransform>();
        RectTransform root, catalog, preview, characteristics;
        CatalogScrollList list;
        GridLayoutGroup grid;
        int mountCount;
        float width = 1552, height = 780;

        internal WizardFoundationPanel(Transform parent, WorkshopUi ui, RunController run,
            WorkshopWizard wizard, Action<string, Action> purchase)
        { this.parent = parent; this.ui = ui; this.run = run; this.wizard = wizard; this.purchase = purchase; }

        internal void Hide()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root.gameObject);
            root = null; cards.Clear();
        }

        internal void Show(TowerBlueprint draft)
        {
            Hide();
            root = new GameObject("Wizard foundation", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.SetSiblingIndex(2);
            ResourceIllustration(root, "Foundation page decoration", "FoundationPageDecor", new Rect(-24, height - 110, width + 48, 220));
            catalog = Sheet(root, "Foundation catalog sheet", new Rect(0, 0, 850, height));
            Label(catalog, "Foundation heading", "Выбери основу башни", new Rect(20, 12, 810, 48), 32, true);
            Label(catalog, "Foundation hint", "Основа определяет число креплений и вместимость башни.", new Rect(20, 62, 810, 42), 18);
            var parts = run.Content.Chassis.Where(p => p.Enabled).ToArray();
            list = new CatalogScrollList(catalog, "Foundation catalog", new Rect(18, 114, 814, height - 132), parts.Length, 2, 300, 14);
            grid = list.Content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
            grid.spacing = new Vector2(14, 14); grid.childAlignment = TextAnchor.UpperLeft;
            foreach (var part in parts) BuildCard(part, draft);

            preview = Sheet(root, "Foundation preview sheet", new Rect(870, 0, 682, height));
            Label(preview, "Foundation preview heading", "Предпросмотр", new Rect(20, 12, 642, 48), 32, true);
            Label(preview, "Foundation preview hint", "Так выглядит выбранная основа.", new Rect(20, 62, 642, 30), 18);
            ResourceIllustration(preview, "Foundation preview scenery", "FoundationPreviewScenery", new Rect(10, 90, 662, 420));
            Illustration(preview, "Foundation preview illustration", draft.ChassisId, new Rect(80, 100, 522, 400));
            var selected = run.Content.GetChassis(draft.ChassisId);
            mountCount = selected.MountCount;
            for (int i = 0; i < mountCount; i++)
            {
                Label(preview, "Foundation mount callout " + i, "Крепление " + (i + 1), new Rect(80 + i * 170, 98, 150, 26), 17);
                Detail(preview, "Foundation mount arrow " + i, FoundationDetailGraphic.Kind.Arrow, new Rect(100 + i * 170, 126, 50, 54));
            }
            Label(preview, "Foundation preview name", selected.Name, new Rect(20, 500, 642, 40), 25, true, TextAnchor.MiddleCenter);
            characteristics = Sheet(preview, "Foundation characteristics", new Rect(18, 554, 646, 208));
            Label(characteristics, "Foundation characteristics heading", "Характеристики", new Rect(16, 8, 614, 38), 24, true);
            Label(characteristics, "Foundation mount heading", "Крепления", new Rect(16, 52, 150, 30), 20, true);
            Label(characteristics, "Foundation mount value", selected.MountCount.ToString(), new Rect(16, 90, 150, 48), 36, true);
            Label(characteristics, "Foundation mount hint", "Слотов для оружия", new Rect(16, 144, 150, 48), 17);
            Label(characteristics, "Foundation capacity heading", "Вместимость", new Rect(190, 52, 158, 30), 20, true);
            Label(characteristics, "Foundation capacity value", selected.Capacity.ToString(), new Rect(190, 90, 158, 48), 36, true);
            Label(characteristics, "Foundation capacity hint", "Предел нагрузки деталей", new Rect(190, 144, 158, 48), 17);
            Label(characteristics, "Foundation body heading", "Корпус", new Rect(370, 52, 260, 30), 20, true);
            Label(characteristics, "Foundation body description", selected.Description, new Rect(370, 88, 260, 104), 18);
            Illustration(characteristics, "Foundation mount icon", "icon.settings", new Rect(16, 52, 32, 32));
            Illustration(characteristics, "Foundation capacity icon", "module.armor", new Rect(190, 52, 32, 32));
            Illustration(characteristics, "Foundation body icon", "icon.shield", new Rect(370, 52, 32, 32));
            Detail(characteristics, "Foundation stat divider 0", FoundationDetailGraphic.Kind.Divider, new Rect(175, 52, 1, 140));
            Detail(characteristics, "Foundation stat divider 1", FoundationDetailGraphic.Kind.Divider, new Rect(355, 52, 1, 140));
            Reflow(width, height);
        }

        void BuildCard(ChassisDefinition part, TowerBlueprint draft)
        {
            bool owned = wizard.Owned(part), selected = draft.ChassisId == part.Id;
            var card = Sheet(list.Content, "Wizard part " + part.Id, new Rect(0, 0, 400, 300), true, selected);
            cards.Add(card);
            Label(card, "Part name", part.Name, new Rect(16, 8, 344, 42), 26, true);
            string description = ui.Resolve(part.Description);
            int sentenceEnd = description.IndexOf('.');
            Label(card, "Part description", sentenceEnd >= 0 ? description.Substring(0, sentenceEnd + 1) : description, new Rect(16, 54, 170, 150), 18);
            Illustration(card, "Part illustration", part.Id, new Rect(190, 58, 194, 150));
            Illustration(card, "Part ground rock", "terrain.rock", new Rect(180, 180, 54, 36));
            Illustration(card, "Part ground shrub", "terrain.shrub", new Rect(340, 177, 36, 32));
            Label(card, "Part values", "Крепления:", new Rect(16, 214, 120, 30), 18);
            Label(card, "Part capacity", "Вместимость: " + part.Capacity, new Rect(250, 214, 134, 30), 18);
            var pips = Detail(card, "Part mount pips", FoundationDetailGraphic.Kind.MountPips, new Rect(130, 218, 80, 22));
            pips.Count = part.MountCount; pips.Total = TowerBlueprint.MaximumWeaponMounts;
            var badge = Detail(card, "Foundation selected mark", FoundationDetailGraphic.Kind.SelectedCorner, new Rect(352, 0, 48, 48));
            badge.gameObject.SetActive(selected);
            int price = run.Workshop.Parts.FirstOrDefault(p => p.Id == part.Id)?.GoldCost ?? part.UnlockGold;
            var choose = ui.Button(card, (owned ? "Wizard choose " : "Wizard unlock ") + part.Id,
                new Rect(16, 252, 368, 36), owned ? WorkshopWizard.Copy(selected ? "selected" : "choose") : WorkshopWizard.Copy("unlock", price), () =>
                {
                    if (owned)
                    {
                        bool restore = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.name == "Wizard choose " + part.Id;
                        wizard.SelectChassis(part.Id);
                        if (restore) RestoreChoiceFocus(part.Id);
                    }
                    else purchase(part.Id, () => { Show(wizard.Draft); RestoreChoiceFocus(part.Id); });
                }, owned ? WorkshopIcon.Check : WorkshopIcon.Cost);
            choose.interactable = owned || run.Workshop.Gold >= price;
            ui.SetSelected(choose, selected);
            ((WorkshopButton)choose).SetDecoration(card.Find("Foundation frame").GetComponent<FoundationDetailGraphic>());
            if (owned)
            {
                choose.GetComponent<WorkshopSurfaceGraphic>().canvasRenderer.SetAlpha(0);
                choose.GetComponent<WorkshopSurfaceGraphic>().canvasRenderer.cullTransparentMesh = false;
                foreach (var graphic in choose.GetComponentsInChildren<Graphic>()) if (graphic.gameObject != choose.gameObject) graphic.enabled = false;
                choose.GetComponentInChildren<Text>(true).text = "";
                ui.Tip(choose.gameObject, part.Description);
            }
            else
            {
                choose.GetComponentInChildren<WorkshopIconGraphic>().enabled = false;
                Detail(choose.transform, "Foundation purchase gold", FoundationDetailGraphic.Kind.GoldStack, new Rect(12, 3, 28, 28));
                var frame = Detail(choose.transform, "Foundation purchase frame", FoundationDetailGraphic.Kind.Frame, new Rect(0, 0, 368, 36));
                frame.rectTransform.anchorMin = Vector2.zero; frame.rectTransform.anchorMax = Vector2.one; frame.rectTransform.offsetMin = frame.rectTransform.offsetMax = Vector2.zero;
            }
            ui.Tip(card.gameObject, part.Description);
        }

        void RestoreChoiceFocus(string id)
        {
            if (root == null || UnityEngine.EventSystems.EventSystem.current == null) return;
            var control = root.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "Wizard choose " + id || b.name == "Wizard unlock " + id);
            if (control != null && control.IsInteractable()) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(control.gameObject);
        }

        RectTransform Sheet(Transform owner, string name, Rect bounds, bool blocks = false, bool selected = false)
        {
            var panel = ui.Panel(owner, name, bounds, Color.clear, blocks);
            panel.GetComponent<WorkshopSurfaceGraphic>().canvasRenderer.cullTransparentMesh = false;
            var frame = Detail(panel, "Foundation frame", FoundationDetailGraphic.Kind.Frame, new Rect(0, 0, bounds.width, bounds.height));
            frame.Selected = selected; frame.Fill = new Color(1, 1, 1, selected ? .12f : .035f);
            frame.rectTransform.anchorMin = Vector2.zero; frame.rectTransform.anchorMax = Vector2.one; frame.rectTransform.offsetMin = frame.rectTransform.offsetMax = Vector2.zero;
            return panel;
        }

        static FoundationDetailGraphic Detail(Transform owner, string name, FoundationDetailGraphic.Kind kind, Rect bounds)
        {
            var graphic = WorkshopUi.Graphic<FoundationDetailGraphic>(owner, name, bounds);
            graphic.Shape = kind; graphic.color = WorkshopUi.Blue; return graphic;
        }

        static void ResourceIllustration(Transform parent, string name, string resource, Rect bounds)
        {
            var image = WorkshopUi.Graphic<Image>(parent, name, bounds);
            image.sprite = Resources.Load<Sprite>("Art/Player/" + resource);
            image.preserveAspect = true; CenterImage(image.rectTransform, bounds);
        }

        Text Label(Transform parent, string name, string text, Rect bounds, int size, bool heading = false,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = ui.Label(parent, name, text, bounds, size, heading);
            WorkshopUi.Place(label.rectTransform, bounds);
            label.alignment = alignment;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = heading ? 17 : 15; label.resizeTextMaxSize = size;
            return label;
        }

        static void Illustration(Transform parent, string name, string id, Rect bounds)
        {
            var image = WorkshopUi.Graphic<Image>(parent, name, bounds);
            if (NotebookSpriteCatalog.TryGet(id, out var sprite)) image.sprite = sprite;
            image.preserveAspect = true;
            CenterImage(image.rectTransform, bounds);
        }

        static void Place(Transform parent, string name, Rect bounds)
        {
            var item = parent.Find(name) as RectTransform;
            if (item != null)
            {
                WorkshopUi.Place(item, bounds);
                if (item.GetComponent<Image>() != null) CenterImage(item, bounds);
            }
        }

        static void CenterImage(RectTransform item, Rect bounds)
        {
            item.pivot = new Vector2(.5f, .5f);
            item.anchoredPosition = new Vector2(bounds.center.x, -bounds.center.y);
        }

        internal void Reflow(float availableWidth, float availableHeight)
        {
            width = availableWidth; height = availableHeight;
            if (root == null) return;
            bool compact = width < 1350;
            float gap = compact ? 12 : 18, leftW = (width - gap) * .56f, rightW = width - gap - leftW;
            WorkshopUi.Place(root, new Rect(0, 0, width, height));
            Place(root, "Foundation page decoration", new Rect(-24, height + 104 - (width + 48) / 3, width + 48, (width + 48) / 3));
            WorkshopUi.Place(catalog, new Rect(0, 0, leftW, height));
            Place(catalog, "Foundation heading", new Rect(18, 10, compact ? leftW - 36 : leftW * .48f, compact ? 40 : 46));
            Place(catalog, "Foundation hint", compact ? new Rect(18, 52, leftW - 36, 36) : new Rect(leftW * .50f, 12, leftW * .48f, 42));
            var viewport = list.Scroll.viewport;
            float listTop = compact ? 94 : 66;
            float listH = height - listTop - 10, cellW = (leftW - 36 - 14) / 2;
            float cellH = Mathf.Max(190, (listH - 14) / 2);
            WorkshopUi.Place(viewport, new Rect(18, listTop, leftW - 36, listH));
            grid.cellSize = new Vector2(cellW, cellH);
            list.Content.sizeDelta = new Vector2(0, Mathf.Max(listH, Mathf.Ceil(cards.Count / 2f) * (cellH + 14) - 14));
            list.Scroll.vertical = list.Content.sizeDelta.y > listH + 1;
            foreach (var card in cards)
            {
                float descriptionW = cellW * (compact ? .50f : .43f);
                Place(card, "Part name", new Rect(12, 6, cellW - 54, compact ? 44 : 48));
                var title = card.Find("Part name").GetComponent<Text>();
                title.font = compact ? ui.BodyFont : ui.HeadingFont;
                title.resizeTextMinSize = compact ? 15 : 17; title.resizeTextMaxSize = compact ? 21 : 26;
                Place(card, "Part description", new Rect(12, compact ? 48 : 64, descriptionW, cellH - (compact ? 118 : 144)));
                Place(card, "Part illustration", new Rect(descriptionW + 20, compact ? 52 : 60, cellW - descriptionW - 30, cellH - (compact ? 130 : 146)));
                Place(card, "Part ground rock", new Rect(descriptionW + 20, cellH - 109, compact ? 28 : 48, compact ? 20 : 34));
                Place(card, "Part ground shrub", new Rect(cellW - 47, cellH - 108, compact ? 24 : 34, compact ? 20 : 32));
                Place(card, "Part values", new Rect(12, cellH - 78, cellW * .27f, 32));
                card.Find("Part values").GetComponent<Text>().text = cellW < 270 ? "Слоты:" : "Крепления:";
                Place(card, "Part mount pips", new Rect(cellW * .29f, cellH - 71, cellW * .20f, 22));
                Place(card, "Part capacity", new Rect(cellW * .52f, cellH - 78, cellW * .46f - 8, 32));
                var capacity = card.Find("Part capacity").GetComponent<Text>();
                int capacityValue = run.Content.GetChassis(card.name.Substring("Wizard part ".Length)).Capacity;
                capacity.text = (cellW < 270 ? "Вместимость:\n" : "Вместимость: ") + capacityValue;
                if (cellW < 270) Place(card, "Part capacity", new Rect(cellW * .52f, cellH - 80, cellW * .46f - 8, 38));
                Place(card, "Foundation selected mark", new Rect(cellW - 44, 0, 44, 44));
                var action = card.GetComponentInChildren<Button>();
                WorkshopUi.Place((RectTransform)action.transform, action.name.StartsWith("Wizard choose ") ? new Rect(0, 0, cellW, cellH) : new Rect(12, cellH - 42, cellW - 24, 32));
            }
            WorkshopUi.Place(preview, new Rect(leftW + gap, 0, rightW, height));
            Place(preview, "Foundation preview heading", new Rect(18, 10, compact ? rightW - 36 : rightW * .44f, 46));
            Place(preview, "Foundation preview hint", compact ? new Rect(18, 58, rightW - 36, 40) : new Rect(rightW * .46f, 14, rightW * .50f, 38));
            float statsH = 206, statsY = height - statsH - 18;
            float sceneTop = compact ? 100 : 64, sceneH = Mathf.Max(90, statsY - sceneTop - 48);
            Place(preview, "Foundation preview scenery", new Rect(10, sceneTop, rightW - 20, sceneH));
            Place(preview, "Foundation preview illustration", new Rect(rightW * .20f, sceneTop + sceneH * .20f, rightW * .60f, sceneH * .74f));
            for (int i = 0; i < mountCount; i++)
            {
                float fraction = mountCount == 1 ? .5f : mountCount == 2 ? (i == 0 ? .30f : .72f) : .24f + i * .26f;
                float calloutY = sceneTop + (i == 1 && mountCount == 3 ? 0 : sceneH * .10f);
                Place(preview, "Foundation mount callout " + i, new Rect(rightW * fraction - 40, calloutY, compact ? 100 : 130, 24));
                Place(preview, "Foundation mount arrow " + i, new Rect(rightW * fraction + 8, calloutY + 26, compact ? 26 : 42, sceneH * .20f));
            }
            Place(preview, "Foundation preview name", new Rect(18, statsY - 48, rightW - 36, 40));
            WorkshopUi.Place(characteristics, new Rect(18, statsY, rightW - 36, statsH));
            float statsW = rightW - 36, column = (statsW - 32) * .30f, bodyX = 16 + column * 2;
            Place(characteristics, "Foundation characteristics heading", new Rect(16, 8, statsW - 32, 36));
            string[] prefixes = { "mount", "capacity" };
            for (int i = 0; i < 2; i++)
            {
                float x = 16 + i * column;
                string headingName = "Foundation " + prefixes[i] + " heading";
                Place(characteristics, headingName, new Rect(x, 52, column - 8, 40));
                var heading = characteristics.Find(headingName).GetComponent<Text>();
                heading.font = ui.BodyFont; heading.resizeTextMinSize = 15; heading.resizeTextMaxSize = 18;
                Place(characteristics, "Foundation " + prefixes[i] + " value", new Rect(x, 96, column - 8, 48));
                Place(characteristics, "Foundation " + prefixes[i] + " hint", new Rect(x, 144, column - 8, statsH - 152));
                if (i == 1) characteristics.Find("Foundation capacity hint").GetComponent<Text>().text = compact ? "Предел нагрузки" : "Предел нагрузки деталей";
                Place(characteristics, "Foundation " + prefixes[i] + " icon", new Rect(x, 54, compact ? 22 : 32, compact ? 22 : 32));
                Place(characteristics, headingName, compact ? new Rect(x, 77, column - 8, 32) : new Rect(x + 38, 52, column - 44, 40));
                if (compact)
                {
                    Place(characteristics, "Foundation " + prefixes[i] + " value", new Rect(x, 107, column - 8, 44));
                    Place(characteristics, "Foundation " + prefixes[i] + " hint", new Rect(x, 154, column - 8, statsH - 162));
                }
                Place(characteristics, "Foundation stat divider " + i, new Rect(x + column - 7, 52, 1, 140));
            }
            Place(characteristics, "Foundation body icon", new Rect(bodyX, 54, compact ? 22 : 32, compact ? 22 : 32));
            Place(characteristics, "Foundation body heading", new Rect(bodyX + (compact ? 25 : 38), 52, statsW - bodyX - (compact ? 41 : 54), 30));
            Place(characteristics, "Foundation body description", new Rect(bodyX, 84, statsW - bodyX - 16, statsH - 96));
            LayoutRebuilder.ForceRebuildLayoutImmediate(list.Content);
        }
    }
}
