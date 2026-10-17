using System;
using System.Collections.Generic;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Screen 05: current assembly, mount rail, seven-weapon catalog and actual adjustments.</summary>
    internal sealed class WizardWeaponPanel
    {
        readonly Transform parent;
        readonly WorkshopUi ui;
        readonly RunController run;
        readonly WorkshopWizard wizard;
        readonly Action<string, Action> purchase;
        readonly Action additions;
        readonly List<RectTransform> cards = new List<RectTransform>();
        readonly List<RectTransform> mounts = new List<RectTransform>();
        RectTransform root, assembly, rail, catalog, settings;
        int mount;
        float width = 1552, height = 676;

        internal WizardWeaponPanel(Transform parent, WorkshopUi ui, RunController run, WorkshopWizard wizard,
            Action<string, Action> purchase, Action additions)
        { this.parent = parent; this.ui = ui; this.run = run; this.wizard = wizard; this.purchase = purchase; this.additions = additions; }

        internal void Hide()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root.gameObject);
            root = null; cards.Clear(); mounts.Clear();
        }

        internal void Show(TowerBlueprint draft)
        {
            Hide();
            root = new GameObject("Wizard weapons", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false); root.SetSiblingIndex(2);
            Resource(root, "Weapon page decoration", "FoundationPageDecor", new Rect(0, 0, width, height));
            assembly = Region(root, "Weapon assembly sheet");
            Label(assembly, "Weapon assembly heading", "Твоя сборка", new Rect(12, 0, 300, 44), 29, true);
            Resource(assembly, "Weapon assembly scenery", "FoundationPreviewScenery", new Rect(0, 45, 400, 430));
            var illustration = WorkshopUi.Graphic<WeaponAssemblyGraphic>(assembly, "Weapon assembly illustration", new Rect(50, 45, 300, 390));
            illustration.SetBlueprint(draft);
            Label(assembly, "Weapon assembly name", run.Content.GetChassis(draft.ChassisId).Name, new Rect(12, 440, 390, 38), 23, true, TextAnchor.MiddleCenter);
            var stats = run.PreviewBlueprint(draft);
            var load = Sheet(assembly, "Weapon load bar", new Rect(12, 482, 390, 48));
            var meter = Detail(load, "Weapon load meter", FoundationDetailGraphic.Kind.LoadMeter, new Rect(150, 14, 130, 18));
            meter.Count = stats.Load; meter.Total = stats.Capacity;
            Label(load, "Weapon load caption", "Нагрузка:", new Rect(54, 4, 160, 40), 22, true);
            Label(load, "Weapon assembly load", stats.Load + " / " + stats.Capacity, new Rect(220, 4, 150, 40), 25, true, TextAnchor.MiddleRight);
            ui.Tip(load.gameObject, "Нагрузка всех деталей / вместимость выбранной основы. Итоговая проверка рисунка — на шаге «Проверка».");

            rail = Region(root, "Weapon mount rail");
            int count = Math.Max(run.Content.GetChassis(draft.ChassisId).MountCount, draft.WeaponMounts.Max(item => item.SlotIndex) + 1);
            mount = Mathf.Clamp(mount, 0, count - 1);
            for (int i = 0; i < count; i++) BuildMount(i, draft);
            if (mount > 0)
            {
                var remove = ui.Button(rail, "Wizard remove mount", new Rect(0, 490, 180, 42), "Убрать оружие", () =>
                {
                    // Selection is deliberately bypassed: obsolete slots remain removable after shrinking a chassis.
                    string focus = "Wizard mount " + mount;
                    wizard.SetDraft(wizard.Draft.WithWeaponMount(mount, null)); RestoreFocus(focus, "Wizard mount " + mount);
                }, WorkshopIcon.Trash);
                ui.Tip(remove.gameObject, "Очистить выбранное крепление. Первое крепление обязательно.");
            }

            catalog = Sheet(root, "Weapon catalog sheet", new Rect(800, 0, 750, 530));
            Label(catalog, "Weapon catalog heading", "Оружие · крепление " + (mount + 1), new Rect(18, 4, 710, 44), 28, true);
            var weapons = run.Content.Weapons.Where(part => part.Enabled).ToArray();
            foreach (var weapon in weapons) BuildCard(weapon, draft);
            settings = Sheet(root, "Weapon additions strip", new Rect(0, 550, width, 100));
            settings.Find("Weapon frame").GetComponent<FoundationDetailGraphic>().Fill = WorkshopUi.Paper;
            Label(settings, "Weapon additions heading", "Дополнения и настройки", new Rect(12, 0, width - 24, 34), 24, true, TextAnchor.MiddleCenter);
            BuildSettings(draft);
            Reflow(width, height);
        }

        void BuildMount(int slot, TowerBlueprint draft)
        {
            string id = draft.GetWeaponId(slot);
            bool excess = slot >= run.Content.GetChassis(draft.ChassisId).MountCount;
            var card = Sheet(rail, "Weapon mount card " + slot, new Rect(0, slot * 150, 180, 138), slot == mount);
            mounts.Add(card);
            Label(card, "Mount heading", "Крепление " + (slot + 1), new Rect(10, 0, 160, 38), 22, true, TextAnchor.MiddleCenter);
            if (id != null) Illustration(card, "Mount illustration", id, new Rect(16, 40, 148, 58));
            else { var plus = WorkshopUi.Graphic<WorkshopIconGraphic>(card, "Mount illustration", new Rect(64, 42, 50, 50)); plus.Icon = WorkshopIcon.Plus; plus.color = WorkshopUi.Blue; }
            Label(card, "Mount weapon name", excess ? "Лишнее крепление" : id == null ? "Пусто" : run.Content.GetWeapon(id).Name,
                new Rect(8, 100, 164, 32), 17, false, TextAnchor.MiddleCenter);
            var target = ui.Button(card, "Wizard mount " + slot, new Rect(0, 0, 180, 138), "", () =>
            { mount = slot; Show(wizard.Draft); RestoreFocus("Wizard mount " + slot); });
            Transparent(target, card); ui.SetSelected(target, slot == mount);
            ui.Tip(target.gameObject, excess ? "Основа больше не поддерживает это крепление. Выбери его и убери оружие." : "Выбрать крепление " + (slot + 1));
        }

        void BuildCard(WeaponDefinition part, TowerBlueprint draft)
        {
            bool owned = wizard.Owned(part), selected = draft.GetWeaponId(mount) == part.Id;
            var card = Sheet(catalog, "Wizard part " + part.Id, new Rect(0, 0, 170, 220), selected);
            cards.Add(card);
            Label(card, "Part name", part.Name, new Rect(8, 2, 154, 46), 23, true, TextAnchor.MiddleCenter);
            Illustration(card, "Part illustration", part.Id, new Rect(14, 48, 142, 118));
            Illustration(card, "Part cost icon", "icon.pencil", new Rect(20, 171, 24, 24));
            Label(card, "Part cost", part.Stats.Cost + " штр.", new Rect(48, 164, 114, 36), 20, true);
            int price = run.Workshop.Parts.FirstOrDefault(item => item.Id == part.Id)?.GoldCost ?? part.UnlockGold;
            var target = ui.Button(card, (owned ? "Wizard choose " : "Wizard unlock ") + part.Id,
                new Rect(8, 200, 154, 34), owned ? "" : WorkshopWizard.Copy("unlock", price), () =>
                {
                    if (owned)
                    {
                        wizard.SelectWeapon(mount, part.Id); RestoreFocus("Wizard choose " + part.Id);
                    }
                    else purchase(part.Id, () => { Show(wizard.Draft); RestoreFocus("Wizard choose " + part.Id, "Wizard unlock " + part.Id); });
                }, owned ? WorkshopIcon.None : WorkshopIcon.Cost);
            target.interactable = mount < run.Content.GetChassis(draft.ChassisId).MountCount && (owned || run.Workshop.Gold >= price);
            ui.SetSelected(target, selected);
            if (owned) Transparent(target, card);
            else ((WorkshopButton)target).SetDecoration(card.Find("Weapon frame").GetComponent<FoundationDetailGraphic>());
            ui.Tip(target.gameObject, ui.Resolve(part.Description) + "\nБазовая цена детали: " + part.Stats.Cost + " штрихов. Нагрузка: " + part.Load + ".");
            var mark = Detail(card, "Weapon selected mark", FoundationDetailGraphic.Kind.SelectedCorner, new Rect(134, 0, 36, 36));
            mark.gameObject.SetActive(selected);
        }

        void BuildSettings(TowerBlueprint draft)
        {
            var armor = Sheet(settings, "Weapon armor group", new Rect(12, 34, 370, 88));
            Illustration(armor, "Setting illustration", "module.armor", new Rect(8, 8, 76, 70));
            Label(armor, "Setting heading", "Броня башни", new Rect(94, 0, 250, 36), 22, true);
            Selector(armor, "Wizard weapon armor", additions);
            var feed = Sheet(settings, "Weapon feed group", new Rect(398, 34, 370, 88));
            Illustration(feed, "Setting illustration", "module.feed", new Rect(8, 8, 76, 70));
            Label(feed, "Setting heading", "Подача", new Rect(94, 0, 250, 36), 22, true);
            var feedChoice = Selector(feed, "Wizard additions", additions);
            ui.Tip(feedChoice.gameObject, "Выбрать подачу и другие улучшения. В окне доступны покупка, добавление и удаление модулей.");
            var form = Sheet(settings, "Weapon form group", new Rect(784, 34, 370, 88));
            Detail(form, "Setting illustration", FoundationDetailGraphic.Kind.Star, new Rect(8, 8, 76, 70));
            Label(form, "Setting heading", "Особая форма", new Rect(94, 0, 250, 36), 22, true);
            string id = draft.GetWeaponId(mount);
            var weapon = id == null ? null : run.Content.GetWeapon(id);
            bool special = weapon != null && (weapon.SupportsFootprint || weapon.SupportsVolley);
            var shape = Selector(form, "Wizard weapon geometry", () => wizard.OpenGeometry(mount));
            shape.interactable = special;
            ui.Tip(shape.gameObject, special ? "Настроить " + (weapon.SupportsFootprint ? "отпечаток заряда" : "направления залпа") + " выбранного крепления." : "У выбранного оружия нет редактора особой формы.");
            RefreshSettings(draft);
        }

        Button Selector(Transform owner, string name, Action action)
        {
            var button = ui.Button(owner, name, new Rect(94, 40, 260, 38), "", action, WorkshopIcon.Chevron);
            var frame = Detail(button.transform, "Selector frame", FoundationDetailGraphic.Kind.Frame, new Rect(0, 0, 260, 38));
            frame.Fill = WorkshopUi.Paper; frame.Radius = 5;
            frame.rectTransform.anchorMin = Vector2.zero; frame.rectTransform.anchorMax = Vector2.one;
            frame.rectTransform.offsetMin = frame.rectTransform.offsetMax = Vector2.zero; frame.transform.SetAsFirstSibling();
            button.GetComponent<WorkshopSurfaceGraphic>().canvasRenderer.SetAlpha(0);
            button.GetComponent<WorkshopSurfaceGraphic>().canvasRenderer.cullTransparentMesh = false;
            ((WorkshopButton)button).SetDecoration(frame);
            return button;
        }

        void RefreshSettings(TowerBlueprint draft)
        {
            var selectedArmor = draft.ModuleSelections.Select(item => run.Content.GetModule(item.DefinitionId)).FirstOrDefault(item => item.SlotKind == ModuleSlotKind.Armor);
            var selectedFeed = draft.ModuleSelections.Select(item => run.Content.GetModule(item.DefinitionId)).FirstOrDefault(item => item.Id == "module.feed");
            settings.Find("Weapon armor group/Wizard weapon armor/Caption").GetComponent<Text>().text = selectedArmor == null ? "Без брони" : ui.Resolve(selectedArmor.Name);
            settings.Find("Weapon feed group/Wizard additions/Caption").GetComponent<Text>().text = selectedFeed == null ? "Стандартная" : ui.Resolve(selectedFeed.Name);
            string id = draft.GetWeaponId(mount), form = "Без модификаций";
            var weapon = id == null ? null : run.Content.GetWeapon(id);
            if (weapon != null && weapon.SupportsFootprint)
            {
                var footprint = draft.GetMortarFootprint(mount);
                if (footprint.IsContour) form = "Свободный отпечаток";
                else if (!footprint.IsCircle) form = "Эллипс · " + footprint.AspectRatio.ToString("0.#") + ":1";
            }
            else if (weapon != null && weapon.SupportsVolley && draft.GetFanVolley(mount) != null) form = "Залп · " + draft.GetFanVolley(mount).Guides.Count + " направл.";
            settings.Find("Weapon form group/Wizard weapon geometry/Caption").GetComponent<Text>().text = form;
        }

        internal void Reflow(float availableWidth, float availableHeight)
        {
            width = availableWidth; height = availableHeight;
            if (root == null) return;
            bool compact = width < 1350;
            float gap = compact ? 12 : 20, settingsH = 132, mainH = height - settingsH - 12;
            float leftW = (width - gap) * .51f, catalogW = width - leftW - gap;
            float railW = leftW * .26f, previewW = leftW - railW - 12;
            WorkshopUi.Place(root, new Rect(0, 0, width, height));
            Place(root, "Weapon page decoration", new Rect(-24, height + 74 - (width + 48) / 3, width + 48, (width + 48) / 3));
            WorkshopUi.Place(assembly, new Rect(0, 0, previewW, mainH));
            Place(assembly, "Weapon assembly heading", new Rect(10, 0, previewW - 20, 44));
            float sceneH = mainH - 130;
            Place(assembly, "Weapon assembly scenery", new Rect(-12, 42, previewW + 24, sceneH));
            Place(assembly, "Weapon assembly illustration", new Rect(previewW * .11f, 54, previewW * .78f, sceneH - 10));
            Place(assembly, "Weapon assembly name", new Rect(8, mainH - 88, previewW - 16, 34));
            Place(assembly, "Weapon load bar", new Rect(8, mainH - 48, previewW - 16, 46));
            var load = assembly.Find("Weapon load bar");
            Place(load, "Weapon load caption", new Rect(10, 3, 108, 40));
            Place(load, "Weapon load meter", new Rect(120, 14, Mathf.Max(24, previewW - 238), 18));
            Place(load, "Weapon assembly load", new Rect(previewW - 112, 3, 86, 40));
            WorkshopUi.Place(rail, new Rect(previewW + 12, 0, railW, mainH));
            float removeH = mount > 0 ? 42 : 0;
            float slotH = Mathf.Min(170, (mainH - removeH - 10 - (mounts.Count - 1) * 12) / mounts.Count);
            for (int i = 0; i < mounts.Count; i++)
            {
                var card = mounts[i]; WorkshopUi.Place(card, new Rect(0, i * (slotH + 12) + 4, railW, slotH));
                Place(card, "Mount heading", new Rect(6, 0, railW - 12, 36));
                Place(card, "Mount illustration", new Rect(12, 36, railW - 24, Mathf.Max(24, slotH - 78)));
                Place(card, "Mount weapon name", new Rect(6, slotH - 38, railW - 12, 34));
                Place(card, "Wizard mount " + i, new Rect(0, 0, railW, slotH));
            }
            Place(rail, "Wizard remove mount", new Rect(0, mainH - 40, railW, 40));
            WorkshopUi.Place(catalog, new Rect(leftW + gap, 0, catalogW, mainH));
            Place(catalog, "Weapon catalog heading", new Rect(12, 0, catalogW - 24, 44));
            float cardGap = compact ? 8 : 12, cellW = (catalogW - 24 - cardGap * 3) / 4;
            float cellH = (mainH - 64 - cardGap) / 2;
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i]; WorkshopUi.Place(card, new Rect(12 + i % 4 * (cellW + cardGap), 52 + i / 4 * (cellH + cardGap), cellW, cellH));
                var title = card.Find("Part name").GetComponent<Text>(); title.resizeTextMinSize = 15; title.resizeTextMaxSize = compact ? 19 : 23;
                bool owned = card.GetComponentInChildren<Button>().name.StartsWith("Wizard choose ");
                float bottom = owned ? 38 : 78;
                Place(card, "Part name", new Rect(5, cellH - bottom - 44, cellW - 10, 44));
                Place(card, "Part illustration", new Rect(10, 10, cellW - 20, Mathf.Max(28, cellH - bottom - 58)));
                Place(card, "Part cost icon", new Rect(10, cellH - bottom + 4, 22, 22));
                Place(card, "Part cost", new Rect(37, cellH - bottom, cellW - 41, 32));
                var target = card.GetComponentInChildren<Button>();
                WorkshopUi.Place((RectTransform)target.transform, owned ? new Rect(0, 0, cellW, cellH) : new Rect(6, cellH - 40, cellW - 12, 34));
                if (!owned)
                {
                    var icon = target.transform.Find("Icon"); if (icon != null) icon.gameObject.SetActive(false);
                    var caption = target.GetComponentInChildren<Text>();
                    caption.rectTransform.offsetMin = new Vector2(3, 2); caption.rectTransform.offsetMax = new Vector2(-3, -2);
                    caption.resizeTextMinSize = 12; caption.resizeTextMaxSize = 17;
                    string id = card.name.Substring("Wizard part ".Length);
                    int price = run.Workshop.Parts.FirstOrDefault(part => part.Id == id)?.GoldCost ?? run.Content.GetWeapon(id).UnlockGold;
                    caption.text = cellW < 145 ? "Открыть\n" + price + " зол." : ui.Resolve(WorkshopWizard.Copy("unlock", price));
                }
                Place(card, "Weapon selected mark", new Rect(cellW - 34, 0, 34, 34));
            }
            float settingsW = Mathf.Min(1180, width * .88f);
            WorkshopUi.Place(settings, new Rect((width - settingsW) * .5f, mainH + 12, settingsW, settingsH));
            Place(settings, "Weapon additions heading", new Rect(12, 0, settingsW - 24, 34));
            float column = settingsW / 3;
            string[] groups = { "Weapon armor group", "Weapon feed group", "Weapon form group" };
            string[] actions = { "Wizard weapon armor", "Wizard additions", "Wizard weapon geometry" };
            for (int i = 0; i < 3; i++)
            {
                Place(settings, groups[i], new Rect(i * column + 10, 34, column - 20, 88));
                var group = settings.Find(groups[i]);
                float artW = Mathf.Min(76, column * .23f), valueX = artW + 18, valueW = column - valueX - 30;
                Place(group, "Setting illustration", new Rect(8, 8, artW, 70));
                Place(group, "Setting heading", new Rect(valueX, 0, valueW, 36));
                Place(group, actions[i], new Rect(valueX, 40, valueW, 38));
                var action = group.Find(actions[i]);
                Place(action, "Icon", new Rect(valueW - 28, 8, 22, 22));
                var caption = action.Find("Caption").GetComponent<Text>();
                caption.rectTransform.anchorMin = Vector2.zero; caption.rectTransform.anchorMax = Vector2.one;
                caption.rectTransform.offsetMin = new Vector2(8, 3); caption.rectTransform.offsetMax = new Vector2(-30, -3);
                caption.alignment = TextAnchor.MiddleLeft; caption.resizeTextMinSize = 13; caption.resizeTextMaxSize = 18;
            }
        }

        internal void Refresh(TowerBlueprint draft)
        {
            if (root == null) return;
            assembly.Find("Weapon assembly illustration").GetComponent<WeaponAssemblyGraphic>().SetBlueprint(draft);
            var stats = run.PreviewBlueprint(draft);
            var load = assembly.Find("Weapon load bar");
            load.Find("Weapon assembly load").GetComponent<Text>().text = stats.Load + " / " + stats.Capacity;
            var meter = load.Find("Weapon load meter").GetComponent<FoundationDetailGraphic>();
            meter.Count = stats.Load; meter.Total = stats.Capacity;
            RefreshSettings(draft);
        }

        void RestoreFocus(params string[] names)
        {
            if (root == null || EventSystem.current == null) return;
            var control = root.GetComponentsInChildren<Button>().FirstOrDefault(button => names.Contains(button.name) && button.IsInteractable());
            if (control != null) EventSystem.current.SetSelectedGameObject(control.gameObject);
        }
        RectTransform Region(Transform owner, string name)
        { var value = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); value.SetParent(owner, false); return value; }
        RectTransform Sheet(Transform owner, string name, Rect bounds, bool selected = false)
        {
            var panel = ui.Panel(owner, name, bounds, Color.clear);
            var frame = Detail(panel, "Weapon frame", FoundationDetailGraphic.Kind.Frame, new Rect(0, 0, bounds.width, bounds.height));
            frame.Selected = selected; frame.Fill = new Color(1, 1, 1, selected ? .13f : .035f);
            frame.rectTransform.anchorMin = Vector2.zero; frame.rectTransform.anchorMax = Vector2.one; frame.rectTransform.offsetMin = frame.rectTransform.offsetMax = Vector2.zero;
            return panel;
        }
        static void Transparent(Button target, RectTransform card)
        {
            var surface = target.GetComponent<WorkshopSurfaceGraphic>(); surface.canvasRenderer.cullTransparentMesh = false; surface.canvasRenderer.SetAlpha(0);
            foreach (var graphic in target.GetComponentsInChildren<Graphic>()) if (graphic.gameObject != target.gameObject) graphic.enabled = false;
            ((WorkshopButton)target).SetDecoration(card.Find("Weapon frame").GetComponent<FoundationDetailGraphic>());
        }
        Text Label(Transform owner, string name, string text, Rect bounds, int size, bool heading = false, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = ui.Label(owner, name, text, bounds, size, heading); WorkshopUi.Place(label.rectTransform, bounds);
            label.alignment = alignment; label.resizeTextForBestFit = true; label.resizeTextMinSize = 15; label.resizeTextMaxSize = size; return label;
        }
        static FoundationDetailGraphic Detail(Transform owner, string name, FoundationDetailGraphic.Kind kind, Rect bounds)
        { var detail = WorkshopUi.Graphic<FoundationDetailGraphic>(owner, name, bounds); detail.Shape = kind; detail.color = WorkshopUi.Blue; return detail; }
        static void Illustration(Transform owner, string name, string id, Rect bounds)
        { var image = WorkshopUi.Graphic<Image>(owner, name, bounds); if (NotebookSpriteCatalog.TryGet(id, out var sprite)) image.sprite = sprite; else image.enabled = false; image.preserveAspect = true; Center(image.rectTransform, bounds); }
        static void Resource(Transform owner, string name, string resource, Rect bounds)
        { var image = WorkshopUi.Graphic<Image>(owner, name, bounds); image.sprite = Resources.Load<Sprite>("Art/Player/" + resource); image.preserveAspect = true; Center(image.rectTransform, bounds); }
        static void Center(RectTransform item, Rect bounds)
        { item.pivot = new Vector2(.5f, .5f); item.anchoredPosition = new Vector2(bounds.center.x, -bounds.center.y); }
        static void Place(Transform owner, string name, Rect bounds)
        { var item = owner.Find(name) as RectTransform; if (item == null) return; WorkshopUi.Place(item, bounds); if (item.GetComponent<Image>() != null) Center(item, bounds); }
    }
}
