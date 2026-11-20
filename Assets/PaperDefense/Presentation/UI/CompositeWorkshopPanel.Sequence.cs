using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class CompositeWorkshopPanel
    {
        int sequenceReached;
        bool sequenceVariant = true;
        string sequenceInspected;
        string sequenceComponentBaseline;
        static readonly string[] SequenceStages = { "Основа", "Оружие", "Сборка", "Проверка" };

        void RefreshSequence()
        {
            rebuilding = true;
            try
            {
                CaptureAssemblyTrialPlayback();
                var focus = EventSystem.current?.currentSelectedGameObject;
                string focusName = focus != null && focus.transform.IsChildOf(Root) ? focus.name : null;
                if (Step < 2 && !DraftSession.IsComponentOpen) OpenSequenceComponent();
                CaptureSequenceScroll();
                ui.HideTooltip(); WorkshopUi.ClearViews(Root);
                WorkshopMasthead(false);
                var bounds = WorkbenchBounds;
                var body = ui.Panel(Root, "Sequence content", bounds, Color.clear);
                if (Step < 2) SequenceEditor(body, bounds.width, bounds.height);
                else if (Step == 2) SequenceAssembly(body, bounds.width, bounds.height);
                else SequenceReview(body, bounds.width, bounds.height);
                WorkshopCommandBar("Sequence");
                SequenceFooter(); RefreshSequenceStatus();
                if (assemblyTrialOpen) BuildAssemblyTrialSurface();
                ui.EnsureTooltip(Root);
                FieldMenuTheme.ApplyTo(Root);
                RestoreSequenceFocus(focusName);
                RestoreSequenceScroll();
                ui.HideTooltip();
            }
            finally { rebuilding = false; }
        }

        bool WideWorkbench => width >= 1100;
        int SequenceWeaponSlot => DraftSession.PreviewRecipe.Tower.Mounts.Any(item => item.SlotIndex == mount) ? mount : 0;
        float WorkshopInset => WideWorkbench ? 28 : 16;
        Rect WorkbenchBounds
        {
            get
            {
                float top = WideWorkbench ? 132 : 112;
                return new Rect(WorkshopInset, top, width - 2 * WorkshopInset, height - top - 78);
            }
        }

        void WorkshopMasthead(bool structure)
        {
            string prefix = structure ? "Structure" : "Sequence";
            float inset = WorkshopInset, railHeight = WideWorkbench ? 68 : 56;
            ui.Panel(Root, "Workshop navigation rail", new Rect(0, 0, width, railHeight), WorkshopUi.Paper);
            float buttonY = (railHeight - 36) * .5f;
            float titleX = inset;
            if (BackRequested != null)
            {
                ui.IconButton(Root, "Composite return", new Rect(inset, buttonY, 36, 36), WorkshopIcon.Back, "Назад", () => { Suspend(); BackRequested(); });
                titleX += 46;
            }
            float routeX = WideWorkbench ? titleX + 258 : titleX + 148;
            ui.Label(Root, prefix + " title", "Мастерская", new Rect(titleX, 0, routeX - titleX - 8, railHeight), WideWorkbench ? 30 : 22, true);
            var tower = ui.CompactButton(Root, "Composite route tower", new Rect(routeX, buttonY, 98, 36), "Башни", () => { if (structure) SwitchRoute(AlbumEntryKind.Tower); }, WorkshopIcon.Tower);
            var supports = ui.CompactButton(Root, "Composite route structure", new Rect(routeX + 104, buttonY, 137, 36), "Сооружения", () => { if (!structure) SwitchRoute(AlbumEntryKind.Structure); }, WorkshopIcon.Shield);
            FieldMenuSkin.Tab(tower); FieldMenuSkin.Tab(supports);
            ui.SetSelected(tower, !structure); ui.SetSelected(supports, structure);
            float end = width - inset, toolsX = end - 80;
            float currencyX = Mathf.Max(routeX + 249, toolsX - 296);
            var coin = WorkshopUi.Graphic<WorkshopIconGraphic>(Root, prefix + " resource icon", new Rect(currencyX, buttonY + 8, 20, 20));
            coin.Icon = WorkshopIcon.Cost; coin.color = FieldMenuSkin.Accent;
            var resources = ui.Label(Root, prefix + " resources", "", new Rect(currencyX + 26, 1, toolsX - currencyX - 34, railHeight - 2), 14);
            resources.alignment = TextAnchor.MiddleLeft;
            ui.IconButton(Root, prefix + " help", new Rect(toolsX, buttonY, 36, 36), WorkshopIcon.Help, structure ? "Справка по сооружениям" : "Справка по сборке",
                () => { if (structure) StructureHelp(); else SequenceHelp(); });
            ui.IconButton(Root, "Composite preparation", new Rect(end - 36, buttonY, 36, 36), WorkshopIcon.Book, "К подготовке", () => { Suspend(); prepare?.Invoke(); });

            if (!structure && FirstRefitScoped) { BuildFirstRefitGuide(railHeight); return; }

            var stages = structure ? StructureStages : SequenceStages;
            float gap = WideWorkbench ? 12 : 6;
            float stepWidth = (width - 2 * inset - (stages.Length - 1) * gap) / stages.Length;
            var y = railHeight + 7;
            for (int i = 0; i < stages.Length; i++)
            {
                int target = i;
                var step = ui.CompactButton(Root, prefix + " step " + i, new Rect(inset + i * (stepWidth + gap), y, stepWidth, WideWorkbench ? 44 : 36),
                    (i + 1).ToString("00") + "   " + stages[i], () => { if (structure) StructureNavigate(target); else SequenceNavigate(target); },
                    i < Step ? WorkshopIcon.Check : WorkshopIcon.None);
                FieldMenuSkin.Tab(step); ui.SetSelected(step, Step == i); step.interactable = i <= sequenceReached;
            }
        }

        void WorkshopCommandBar(string prefix)
        {
            ui.Panel(Root, "Workshop command rail", new Rect(0, height - 67, width, 67), WorkshopUi.Paper);
            ui.Label(Root, prefix + " notice", "", new Rect(134, height - 56, width - Mathf.Min(280, width * .36f) - 160, 44), 15, color: WorkshopUi.Muted);
        }

        void OpenSequenceComponent()
        {
            var kind = Step == 0 ? ComponentLibraryKind.Foundation : ComponentLibraryKind.Weapon;
            var reference = DraftSession.Draft.LibraryReferences.FirstOrDefault(item => item.Kind == kind && item.SlotIndex == (Step == 0 ? -1 : SequenceWeaponSlot));
            sequenceVariant = reference == null || IsStarterComponent(reference.Id);
            if (Step == 0) DraftSession.OpenFoundation(); else DraftSession.OpenWeapon(SequenceWeaponSlot);
            DraftSession.SelectCategory(Step == 0 ? ComponentCategory.Hull : ComponentCategory.Magazine);
            if (reference == null) DraftSession.RenameComponent(Step == 0 ? "Моя основа" : "Моё оружие");
            sequenceInspected = null;
            sequenceComponentBaseline = SequenceComponentSignature();
        }

        static bool IsStarterComponent(string id) => id == "library.foundation.standard" || id == "library.weapon.gun" || id == "library.weapon.piercer" || id == "library.weapon.mortar";

        void SequenceNavigate(int target)
        {
            if (target < 0 || target > sequenceReached || target == Step) return;
            if (DraftSession.IsComponentOpen && !DraftSession.SaveComponent(Guid.NewGuid(), sequenceVariant).Success)
            { RefreshSequenceStatus(); return; }
            Step = target; feedback = ""; sequenceInspected = null; Refresh();
        }

        void SequenceFooter()
        {
            ui.IconButton(Root, "Sequence back", new Rect(16, height - 56, 44, 44), WorkshopIcon.Back, "Назад",
                () => { if (Step == 0) { Suspend(); BackRequested?.Invoke(); } else SequenceNavigate(Step - 1); });
            float actionWidth = Mathf.Min(280, width * .36f);
            var bounds = new Rect(width - actionWidth - 16, height - 56, actionWidth, 44);
            if (Step < 2)
            {
                ui.IconButton(Root, "Sequence component variant", new Rect(68, height - 56, 44, 44), WorkshopIcon.Copy, "Сохранить копию компонента",
                    () => SaveSequenceComponent(false, true)).interactable = DraftSession.CanSaveComponent;
                var next = ui.CompactButton(Root, "Sequence next", bounds, Step == 0 ? "Оружие" : "Сборка",
                    () => SaveSequenceComponent(true, sequenceVariant), WorkshopIcon.Next, true);
                next.interactable = DraftSession.CanSaveComponent;
                ui.Tip(next.gameObject, Step == 0 ? "Сохранить основу → Оружие" : "Сохранить оружие → Сборка");
            }
            else if (Step == 2)
                ui.CompactButton(Root, "Sequence next", bounds, "Проверить", () => {
                    if (!DraftSession.CanSave) return; Step = sequenceReached = 3; feedback = ""; Refresh();
                }, WorkshopIcon.Next, true).interactable = DraftSession.CanSave;
            else
            {
                ui.IconButton(Root, "Sequence variant", new Rect(68, height - 56, 44, 44), WorkshopIcon.Copy, "Сохранить как вариант", () => SaveSequenceEntry(true)).interactable = DraftSession.CanSave;
                var save = ui.CompactButton(Root, "Sequence save", bounds, "В альбом", () => SaveSequenceEntry(false), WorkshopIcon.Save, true);
                save.interactable = DraftSession.CanSave; ui.Tip(save.gameObject, "Сохранить башню в альбом");
            }
        }

        void SaveSequenceComponent(bool advance, bool variant)
        {
            var result = DraftSession.SaveComponent(Guid.NewGuid(), variant);
            if (result.Success)
            {
                if (advance) { Step++; sequenceReached = Math.Max(sequenceReached, Step); sequenceVariant = true; feedback = ""; }
                else { sequenceVariant = false; feedback = "Компонент сохранён в библиотеку."; }
            }
            Refresh();
        }

        void SaveSequenceEntry(bool variant)
        {
            var result = DraftSession.SaveEntry(Guid.NewGuid(), variant);
            FirstRefitEntrySaved(result);
            feedback = result.Success ? "Башня сохранена. Выбери её в боевом наборе перед запуском." : "";
            Refresh();
        }

        void SequenceEditor(Transform parent, float w, float h)
        {
            float libraryWidth = WideWorkbench ? Mathf.Clamp(w * .34f, 325, 560) : Mathf.Clamp(w * .23f, 156, 238);
            float gap = WideWorkbench ? 18 : 10;
            var library = ui.Panel(parent, "Sequence library", new Rect(0, 0, libraryWidth, h), WorkshopUi.Surface);
            SequenceComponentLibrary(library, libraryWidth, h);
            float ew = w - libraryWidth - gap;
            var editor = ui.Panel(parent, "Sequence editor", new Rect(libraryWidth + gap, 0, ew, h), WorkshopUi.Surface);
            ui.Label(editor, "Sequence editor title", Step == 0 ? "Основа" : "Оружие", new Rect(12, 6, 84, 36), 18, true);
            bool legacyTrigger = Step == 1 && DraftSession.PreviewRecipe.Tower.Mounts.First(item => item.SlotIndex == SequenceWeaponSlot).Weapon.Trigger.Id != "trigger.standard";
            var name = SequenceName(editor, "Sequence component name", DraftSession.ComponentName, new Rect(100, 6, ew - 112 - (legacyTrigger ? 42 : 0), 36));
            name.onValueChanged.AddListener(value => { DraftSession.RenameComponent(value); feedback = ""; RefreshSequenceStatus(); });
            if (legacyTrigger) ui.IconButton(editor, "Sequence reset trigger", new Rect(ew - 48, 6, 36, 36), WorkshopIcon.Settings, "Вернуть обычный режим", () => {
                DraftSession.SelectPart("trigger.standard"); feedback = "Штатный режим совместим с разными обоймами."; Refresh();
            });
            float categoryY = Step == 0 ? 52 : 100;
            if (Step == 1) SequenceFamilies(editor, ew, 50);
            var categories = Step == 0
                ? new[] { ComponentCategory.Hull, ComponentCategory.Armor, ComponentCategory.Frame, ComponentCategory.Feed, ComponentCategory.Turn }
                : new[] { ComponentCategory.Magazine, ComponentCategory.Head, ComponentCategory.Sight, ComponentCategory.Trigger, ComponentCategory.Mount };
            if (!categories.Contains(DraftSession.ActiveCategory)) DraftSession.SelectCategory(categories[0]);
            WorkshopCategoryTabs(editor, ew, categoryY, categories, false);
            float listY = categoryY + 46, listWidth = WideWorkbench ? Mathf.Clamp(ew * .36f, 228, 344) : Mathf.Clamp(ew * .32f, 150, 220);
            float listHeight = h - listY - 10;
            var parts = DraftSession.CompatibleParts.OrderBy(part => part.RequiredLevel).ThenBy(part => part.UnlockGold).ThenBy(part => part.Id, StringComparer.Ordinal).ToArray();
            var activeParts = Step == 0 ? DraftSession.PreviewRecipe.Tower.Foundation.Parts : DraftSession.PreviewRecipe.Tower.Mounts.First(item => item.SlotIndex == SequenceWeaponSlot).Weapon.Parts;
            string equipped = activeParts.Select(reference => Part(reference)).FirstOrDefault(part => part?.Category == DraftSession.ActiveCategory)?.Id;
            var inspected = parts.FirstOrDefault(part => part.Id == sequenceInspected) ?? parts.FirstOrDefault(part => part.Id == equipped) ?? parts.FirstOrDefault();
            sequenceInspected = inspected?.Id;
            var list = new CatalogScrollList(editor, "Sequence part list", new Rect(12, listY, listWidth, listHeight), parts.Length, 1, 64, 6);
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i]; bool owned = DraftSession.IsOwned(part.Id);
                var card = ui.CompactButton(list.Content, "Sequence inspect " + part.Id, new Rect(0, i * 70, listWidth - 12, 64), "", () => { sequenceInspected = part.Id; Refresh(); });
                ui.SetSelected(card, part.Id == inspected?.Id);
                float textX = 8;
                var reference = new PartRef(part.Id, part.Revision);
                if (listWidth >= 220 && AssemblyVisualComposer.HasVisualMapping(reference))
                {
                    var art = WorkshopUi.Graphic<WeaponAssemblyGraphic>(card.transform, "Catalogue drawing " + part.Id, new Rect(8, 10, 52, 42));
                    art.SetPart(reference); textX = 70;
                }
                var label = ui.Label(card.transform, "Sequence part name", "", new Rect(textX, 3, listWidth - textX - 20, 38), 16);
                label.fontStyle = FontStyle.Bold;
                SequenceFitLabel(label, ui.Resolve(part.Name));
                string availability = part.Id == equipped ? "Установлено" : owned ? "Куплено" : "Ур. " + part.RequiredLevel + " · " + part.UnlockGold;
                ui.Label(card.transform, "Sequence part ownership", availability, new Rect(textX, 42, listWidth - textX - 20, 18), 14, color: owned ? WorkshopUi.Muted : WorkshopUi.Warning);
                ui.Tip(card.gameObject, ui.Resolve(part.Name) + "\n" + (owned ? availability : "Нужен уровень " + part.RequiredLevel + " · " + part.UnlockGold + " золота"));
            }
            RevealSequenceRow(list, Array.FindIndex(parts, part => part.Id == sequenceInspected), 70, listHeight);
            float detailX = 24 + listWidth, detailWidth = ew - detailX - 12;
            var detail = ui.Panel(editor, "Sequence part detail", new Rect(detailX, listY, detailWidth, listHeight), WorkshopUi.Paper);
            if (inspected != null) SequencePartDetail(detail, inspected, equipped, detailWidth, listHeight);
        }

        void WorkshopCategoryTabs(Transform parent, float w, float y, ComponentCategory[] categories, bool structure)
        {
            string prefix = structure ? "Structure" : "Sequence";
            bool captions = w >= categories.Length * 126 + 24;
            float pitch = captions ? (w - 20) / categories.Length : 42;
            float x = captions ? 12 : w - 12 - categories.Length * pitch;
            if (!captions)
            {
                string active = structure ? StructureCategoryCaption(DraftSession.ActiveCategory) : CategoryCaption(DraftSession.ActiveCategory);
                ui.Label(parent, prefix + " active category", active, new Rect(12, y, x - 20, 36), 16, true);
            }
            for (int i = 0; i < categories.Length; i++)
            {
                var category = categories[i];
                string caption = structure ? StructureCategoryCaption(category) : CategoryCaption(category);
                string name = prefix + " category " + category;
                var rect = new Rect(x + i * pitch, y, pitch - 6, 36);
                Action choose = () => { DraftSession.SelectCategory(category); sequenceInspected = null; feedback = ""; Refresh(); };
                var tab = captions ? ui.CompactButton(parent, name, rect, caption, choose, SequenceCategoryIcon(category))
                    : ui.IconButton(parent, name, rect, SequenceCategoryIcon(category), caption, choose);
                FieldMenuSkin.Tab(tab); ui.SetSelected(tab, DraftSession.ActiveCategory == category);
            }
        }

        static WorkshopIcon SequenceCategoryIcon(ComponentCategory category)
        {
            switch (category)
            {
                case ComponentCategory.Hull: return WorkshopIcon.Tower;
                case ComponentCategory.Armor: return WorkshopIcon.Armor;
                case ComponentCategory.Frame: return WorkshopIcon.Plus;
                case ComponentCategory.Feed: return WorkshopIcon.Settings;
                case ComponentCategory.Turn: return WorkshopIcon.Chevron;
                case ComponentCategory.Magazine: return WorkshopIcon.Magazine;
                case ComponentCategory.Head: return WorkshopIcon.Weapon;
                case ComponentCategory.Sight: return WorkshopIcon.Target;
                case ComponentCategory.Trigger: return WorkshopIcon.Settings;
                case ComponentCategory.Mount: return WorkshopIcon.Pin;
                case ComponentCategory.Site: return WorkshopIcon.Grid;
                case ComponentCategory.Function: return WorkshopIcon.Settings;
                case ComponentCategory.Shell: return WorkshopIcon.Shield;
                default: return WorkshopIcon.Plus;
            }
        }

        static void SequenceFitLabel(Text label, string full)
        {
            label.text = full; int length = full.Length;
            while (length > 1 && label.preferredHeight > label.rectTransform.rect.height + .1f)
                label.text = full.Substring(0, --length).TrimEnd() + "…";
        }

        void SequenceComponentLibrary(Transform parent, float w, float h)
        {
            var kind = Step == 0 ? ComponentLibraryKind.Foundation : ComponentLibraryKind.Weapon;
            var selectedReference = DraftSession.Draft.LibraryReferences.FirstOrDefault(reference => reference.Kind == kind && reference.SlotIndex == (Step == 0 ? -1 : SequenceWeaponSlot));
            var entries = run.Workshop.Library.Where(entry => entry.Kind == kind).ToArray();
            float inset = WideWorkbench ? 16 : 10;
            ui.Label(parent, "Sequence preview heading", Step == 0 ? "Чертёж основы" : "Чертёж оружия", new Rect(inset, 4, w - 2 * inset, 40), WideWorkbench ? 24 : 18, true);
            float libraryY = WideWorkbench ? Mathf.Clamp(h * .62f, 250, 470) : Mathf.Clamp(h * .49f, 180, 275);
            var preview = SequenceBodyScroll(parent, "Sequence component metrics scroll", new Rect(inset, 50, w - 2 * inset, libraryY - 58));
            float drawingWidth = w - 2 * inset - 12;
            var metrics = ComponentReadings();
            int columns = ReadingColumns(drawingWidth, metrics.Length);
            float metricHeight = Mathf.CeilToInt(metrics.Length / (float)columns) * ReadingRowHeight(drawingWidth);
            float previewHeight = preview.Scroll.viewport.rect.height;
            float drawingHeight = Mathf.Max(42, Mathf.Min(previewHeight * .74f, previewHeight - metricHeight - 12));
            var drawing = WorkshopUi.Graphic<WeaponAssemblyGraphic>(preview.Content, "Sequence component drawing", new Rect(0, 0, drawingWidth, drawingHeight));
            var tower = DraftSession.PreviewRecipe.Tower;
            if (Step == 0) SequenceFoundationDrawing(drawing, tower.Foundation); else SequenceWeaponDrawing(drawing, tower.Mounts.First(item => item.SlotIndex == SequenceWeaponSlot).Weapon);
            ui.Label(parent, "Sequence library title", Step == 0 ? "Мои основы" : "Моё оружие", new Rect(inset, libraryY, w - 2 * inset, 32), 18, true);
            float libraryHeight = h - libraryY - 54;
            var list = new CatalogScrollList(parent, "Sequence saved components", new Rect(inset, libraryY + 38, w - 2 * inset, libraryHeight), entries.Length, 1, 52, 6);
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var button = ui.CompactButton(list.Content, "Sequence edit library " + entry.Id, new Rect(0, i * 58, w - 2 * inset - 10, 52), "", () => RequestSequenceComponent(entry));
                var caption = button.transform.Find("Caption").GetComponent<Text>();
                caption.resizeTextForBestFit = false; caption.fontSize = 14; caption.alignment = TextAnchor.MiddleLeft;
                caption.rectTransform.offsetMin = new Vector2(7, 5); caption.rectTransform.offsetMax = new Vector2(-7, -5);
                SequenceFitLabel(caption, ui.Resolve(entry.Name));
                ui.Tip(button.gameObject, ui.Resolve(entry.Name)); ui.SetSelected(button, selectedReference?.Id == entry.Id);
            }
            RevealSequenceRow(list, Array.FindIndex(entries, entry => selectedReference?.Id == entry.Id), 58, libraryHeight);
            if (metrics.Length == 0)
            {
                var warning = SequenceCopy(preview.Content, "Sequence live component metrics", "Боевые параметры недоступны: исправь состав.",
                    new Rect(2, drawingHeight + 8, drawingWidth - 4, 1), 14, WorkshopUi.Warning);
                metricHeight = warning.rectTransform.rect.height;
            }
            else DrawReadings(preview.Content, metrics, drawingWidth, drawingHeight + 8, columns, "Sequence live component metrics");
            SequenceScrollExtent(preview, drawingHeight + 8 + metricHeight + 2);
        }

        (WorkshopIcon icon, string caption, string value)[] ComponentReadings()
        {
            var culture = ui.TextResolver.Culture;
            if (Step == 0)
            {
                var metrics = DraftSession.PreviewMetrics;
                var frame = DraftSession.PreviewRecipe.Tower.Foundation.Frame;
                return new[] {
                    (WorkshopIcon.Heart, "Прочность", (metrics?.Health ?? 0).ToString("0.#", culture)),
                    (WorkshopIcon.Weapon, "Крепления", Part(frame).Get(AssemblyStat.MountCount, ModifierOperation.Set).ToString("0", culture)),
                    (WorkshopIcon.Weight, "Вес", (metrics?.Load ?? 0) + "/" + (metrics?.Capacity ?? 0)),
                    (WorkshopIcon.Ink, "Постройка", run.PreviewFoundationCost(DraftSession.PreviewEntry.Snapshot) + " штр.")
                };
            }
            var resolved = DraftSession.Resolved;
            var weapon = resolved?.Weapons.FirstOrDefault(item => item.SlotIndex == SequenceWeaponSlot);
            if (weapon == null) return Array.Empty<(WorkshopIcon, string, string)>();
            return new[] {
                (WorkshopIcon.Damage, "Урон", weapon.Damage.ToString("0.#", culture)),
                (WorkshopIcon.Range, "Дальность", (weapon.MinimumRange > 0 ? weapon.MinimumRange.ToString("0.#", culture) + "–" : "") + weapon.Range.ToString("0.#", culture)),
                (WorkshopIcon.FiringArc, "Сектор обстрела", weapon.FiringArcDegrees.ToString("0.#", culture) + "°"),
                (WorkshopIcon.Magazine, "Обойма", weapon.MagazineSize.ToString(culture)),
                (WorkshopIcon.Reload, "Перезарядка", weapon.ReloadAtFeed(resolved.FeedMultiplier).ToString("0.##", culture) + " с"),
                (WorkshopIcon.Target, "Урон/с", weapon.DpsAtFeed(resolved.FeedMultiplier).ToString("0.##", culture))
            };
        }

        static int ReadingColumns(float w, int count)
        {
            if (count == 0) return 1;
            int capacity = w < 240 ? 2 : Mathf.Max(1, Mathf.FloorToInt(w / (w < 400 ? 106 : 92)));
            int rows = Mathf.CeilToInt(count / (float)capacity);
            return Mathf.CeilToInt(count / (float)rows);
        }

        static float ReadingRowHeight(float w) => w < 240 ? 22 : 50;

        void DrawReadings(Transform parent, (WorkshopIcon icon, string caption, string value)[] readings, float w, float y, int columns, string name)
        {
            bool compact = w < 240;
            float cellWidth = w / columns, rowHeight = ReadingRowHeight(w), iconSize = compact ? 15 : 19;
            for (int i = 0; i < readings.Length; i++)
            {
                var stat = readings[i];
                float x = i % columns * cellWidth, top = y + i / columns * rowHeight;
                float valueY = compact ? top : top + 18;
                if (!compact)
                    ui.Label(parent, "Reading caption " + stat.caption, stat.caption, new Rect(x, top, cellWidth - 3, 18), 13, color: FieldMenuSkin.Muted);
                var icon = WorkshopUi.Graphic<WorkshopIconGraphic>(parent, "Reading icon " + stat.caption,
                    new Rect(x, valueY + (compact ? 3 : 4), iconSize, iconSize));
                icon.Icon = stat.icon; icon.color = FieldMenuSkin.Accent;
                float valueX = iconSize + (compact ? 4 : 7);
                var value = ui.Label(parent, i == 0 ? name : "Reading value " + stat.caption, stat.value.Replace(" ", "\u00a0"),
                    new Rect(x + valueX, valueY, cellWidth - valueX - 2, compact ? 20 : 28), compact ? 14 : 20, color: FieldMenuSkin.Ink);
                value.font = ui.HeadingFont;
                value.resizeTextForBestFit = true; value.resizeTextMinSize = compact ? 12 : 14; value.resizeTextMaxSize = compact ? 14 : 20;
                value.raycastTarget = true; ui.Tip(value.gameObject, stat.caption + ": " + stat.value);
            }
        }

        static void RevealSequenceRow(CatalogScrollList list, int index, float stride, float visibleHeight)
        {
            float overflow = list.Content.sizeDelta.y - visibleHeight;
            if (index >= 0 && overflow > 0) list.Scroll.verticalNormalizedPosition = 1 - Mathf.Clamp(index * stride - (visibleHeight - stride) * .5f, 0, overflow) / overflow;
        }

        string SequenceComponentSignature()
        {
            if (!DraftSession.IsComponentOpen) return "";
            var parts = DraftSession.ActiveComponent == WorkshopComponentKind.Foundation ? DraftSession.PreviewRecipe.Tower.Foundation.Parts
                : DraftSession.PreviewRecipe.Tower.Mounts.First(item => item.SlotIndex == DraftSession.ActiveMount).Weapon.Parts;
            return DraftSession.ComponentName + "|" + string.Join(";", parts.Select(part => part.Id + "@" + part.Revision));
        }

        void RequestSequenceComponent(ComponentLibraryEntry entry)
        {
            Action replace = () => {
                // Library records are validated before a local authoring selection; final fit belongs to stage 3.
                if (entry.Snapshot.Definitions.Any(part => !DraftSession.IsOwned(part.Id))) { feedback = "В компоненте есть закрытые детали."; RefreshSequenceStatus(); return; }
                DraftSession.CancelComponent();
                if (!DraftSession.ApplyLibrary(entry, entry.Kind == ComponentLibraryKind.Foundation ? -1 : SequenceWeaponSlot)) { Refresh(); return; }
                sequenceVariant = IsStarterComponent(entry.Id); OpenSequenceComponent();
                if (sequenceVariant) DraftSession.RenameComponent(ui.Resolve(entry.Name) + " · вариант");
                sequenceComponentBaseline = SequenceComponentSignature(); feedback = ""; Refresh();
            };
            if (SequenceComponentSignature() != sequenceComponentBaseline)
                WorkshopConfirm.Show(Root, ui, "Заменить текущий черновик?", "Несохранённые детали и название будут заменены. Вернись к работе, чтобы сначала сохранить копию.", "Заменить", replace);
            else replace();
        }

        void SequenceFamilies(Transform parent, float w, float top)
        {
            var mechanisms = DraftSession.Catalog.Definitions.Where(part => part.Category == ComponentCategory.Mechanism)
                .GroupBy(part => part.Id).Select(group => group.OrderByDescending(part => part.Revision).First()).ToArray();
            // All families remain data-driven; the first three are supplied as free standard weapons.
            const float buttonWidth = 124, gap = 6;
            var content = new PlayerUi().Scroll(parent, "Sequence family catalogue", new Rect(12, top, w - 24, 44), 44);
            content.sizeDelta = new Vector2(Mathf.Max(w - 24, mechanisms.Length * (buttonWidth + gap) - gap), 44);
            var scroll = content.parent.GetComponent<ScrollRect>();
            scroll.horizontal = content.rect.width > scroll.viewport.rect.width + .1f; scroll.vertical = false;
            var vertical = scroll.verticalScrollbar;
            if (vertical != null) { scroll.verticalScrollbar = null; vertical.gameObject.SetActive(false); }
            if (scroll.horizontal) PlayerUi.HorizontalScrollbar(scroll);
            string selected = DraftSession.PreviewRecipe.Tower.Mounts.First(item => item.SlotIndex == SequenceWeaponSlot).Weapon.Mechanism.Id;
            for (int i = 0; i < mechanisms.Length; i++)
            {
                var part = mechanisms[i];
                var button = ui.CompactButton(content, "Sequence family " + part.Id, new Rect(i * (buttonWidth + gap), 0, buttonWidth, 36),
                    ui.Resolve(part.Name), () => {
                        if (DraftSession.IsOwned(part.Id)) DraftSession.SelectPart(part.Id);
                        else { var purchase = DraftSession.PurchasePart(part.Id, Guid.NewGuid()); if (purchase.Success) DraftSession.SelectPart(part.Id); }
                        sequenceInspected = null; feedback = ""; Refresh();
                    });
                var caption = button.transform.Find("Caption").GetComponent<Text>();
                caption.resizeTextMinSize = 14; caption.resizeTextMaxSize = 16;
                button.interactable = DraftSession.IsOwned(part.Id) || run.Workshop.Level >= part.RequiredLevel && run.Workshop.Gold >= part.UnlockGold;
                ui.SetSelected(button, selected == part.Id);
                ui.Tip(button.gameObject, ui.Resolve(part.Name) + " · Ур. " + part.RequiredLevel + " · " + part.UnlockGold + " золота");
            }
        }

        void SequencePartDetail(Transform parent, ComponentDefinition part, string equipped, float w, float h)
        {
            const float actionHeight = 40;
            var scroll = SequenceBodyScroll(parent, "Sequence specification scroll", new Rect(8, 6, w - 16, h - actionHeight - 18));
            float textWidth = w - 32;
            var title = ui.Label(scroll.Content, "Sequence inspected name", "", new Rect(4, 0, textWidth - (WideWorkbench ? 8 : 76), 40), WideWorkbench ? 23 : 16, true);
            SequenceFitLabel(title, ui.Resolve(part.Name));
            var artBounds = InspectedPartBounds(textWidth, h);
            var drawing = WorkshopUi.Graphic<WeaponAssemblyGraphic>(scroll.Content, "Sequence part drawing", artBounds);
            var reference = new PartRef(part.Id, part.Revision); if (AssemblyVisualComposer.HasVisualMapping(reference)) drawing.SetPart(reference);
            var installed = SequenceInstalledPart(part.Category);
            string identity = "Установлено: " + (installed == null ? "—" : ui.Resolve(installed.Name)) + "\nВыбрано: " + ui.Resolve(part.Name);
            ui.Tip(title.gameObject, identity); title.raycastTarget = true;
            bool owned = DraftSession.IsOwned(part.Id);
            string conditions = owned ? "" : "Ур. " + part.RequiredLevel + " · " + part.UnlockGold + " золота";
            float y = WideWorkbench ? artBounds.yMax + 14 : 48;
            var condition = SequenceCopy(scroll.Content, "Sequence conditions", conditions, new Rect(0, y, textWidth, 1), 14, WorkshopUi.Warning);
            if (conditions.Length > 0) y += condition.rectTransform.sizeDelta.y + 4;
            var label = SequenceCopy(scroll.Content, "Sequence specification", SequenceModifierComparison(installed, part), new Rect(4, y, textWidth - 8, 1), WideWorkbench ? 16 : 14);
            ui.Tip(label.gameObject, identity); label.raycastTarget = true;
            SequenceScrollExtent(scroll, y + label.rectTransform.sizeDelta.y + 6);
            string caption = part.Id == equipped ? "Установлено" : owned ? "Установить" : part.UnlockGold + " золота";
            string reason = owned ? "" : run.Workshop.Level < part.RequiredLevel ? "Нужен уровень " + part.RequiredLevel + " (твой: " + run.Workshop.Level + ")" :
                run.Workshop.Gold < part.UnlockGold ? "Нужно " + part.UnlockGold + " золота (есть " + run.Workshop.Gold + ")" : "Открыть деталь навсегда за " + part.UnlockGold + " золота";
            var install = ui.CompactButton(parent, "Sequence install " + part.Id, new Rect(8, h - actionHeight - 6, w - 16, actionHeight), caption, () => {
                if (!DraftSession.IsOwned(part.Id)) { var result = DraftSession.PurchasePart(part.Id, Guid.NewGuid()); if (!result.Success) { feedback = ""; Refresh(); return; } }
                DraftSession.SelectPart(part.Id); feedback = ""; Refresh();
            }, owned ? WorkshopIcon.Check : WorkshopIcon.Cost, true);
            install.interactable = part.Id != equipped && (owned || run.Workshop.Level >= part.RequiredLevel && run.Workshop.Gold >= part.UnlockGold);
            ui.Tip(install.gameObject, identity + (reason.Length == 0 ? "" : "\n" + reason));
        }

        Rect InspectedPartBounds(float w, float h) => WideWorkbench
            ? new Rect(4, 48, w - 8, Mathf.Clamp(h * .32f, 74, 188))
            : new Rect(w - 68, 0, 64, 42);

        ComponentDefinition SequenceInstalledPart(ComponentCategory category)
        {
            var recipe = DraftSession.PreviewRecipe;
            var refs = recipe.Tower == null ? recipe.Structure.Parts : Step == 0 ? recipe.Tower.Foundation.Parts : recipe.Tower.Mounts.First(item => item.SlotIndex == SequenceWeaponSlot).Weapon.Parts;
            return refs.Select(Part).FirstOrDefault(value => value?.Category == category);
        }

        static string SequenceModifierComparison(ComponentDefinition current, ComponentDefinition selected)
        {
            string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
            var modifiers = (current?.Modifiers ?? Array.Empty<ComponentModifier>()).Concat(selected.Modifiers);
            var lines = new List<string> { "Модификаторы детали", "Установлено → Выбрано" };
            foreach (var key in modifiers.Select(value => (value.Stat, value.Operation)).Distinct())
            {
                var before = current?.Modifiers.LastOrDefault(value => value.Stat == key.Stat && value.Operation == key.Operation);
                var after = selected.Modifiers.LastOrDefault(value => value.Stat == key.Stat && value.Operation == key.Operation);
                float neutral = key.Operation == ModifierOperation.Multiply ? 1 : 0;
                float a = before?.Value ?? neutral, b = after?.Value ?? neutral;
                if (a == neutral && b == neutral) continue;
                string Mark(float value) => (key.Operation == ModifierOperation.Multiply ? "×" : key.Operation == ModifierOperation.Add && value > 0 ? "+" : "") + Number(value);
                string delta = Math.Abs(b - a) < .0001f ? "" : " (" + (b - a).ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture) + ")";
                lines.Add(StatCaption(key.Stat) + ": " + Mark(a) + " → " + Mark(b) + delta);
            }
            if (lines.Count == 2) lines.Add("Без дополнительных модификаторов.");
            return string.Join("\n", lines);
        }

        ComponentDefinition Part(PartRef reference)
            => DraftSession.PreviewEntry.Snapshot.Definitions.FirstOrDefault(part => part.Id == reference.Id && part.Revision == reference.Revision)
                ?? DraftSession.Catalog.Definitions.FirstOrDefault(part => part.Id == reference.Id && part.Revision == reference.Revision);

        static string CategoryCaption(ComponentCategory category)
        {
            switch (category)
            {
                case ComponentCategory.Hull: return "Корпус";
                case ComponentCategory.Armor: return "Щиты";
                case ComponentCategory.Frame: return "Места орудий";
                case ComponentCategory.Feed: return "Подача";
                case ComponentCategory.Turn: return "Поворот";
                case ComponentCategory.Magazine: return "Обойма";
                case ComponentCategory.Head: return "Ствол";
                case ComponentCategory.Sight: return "Прицел";
                case ComponentCategory.Trigger: return "Спуск";
                case ComponentCategory.Mount: return "Крепление";
                default: return category.ToString();
            }
        }

        static string StatCaption(AssemblyStat stat)
        {
            switch (stat)
            {
                case AssemblyStat.Cost: return "Цена постройки, штр.";
                case AssemblyStat.Load: return "Вес";
                case AssemblyStat.Capacity: return "Допустимый вес";
                case AssemblyStat.MountCount: return "Мест для орудий";
                case AssemblyStat.Health: return "Прочность";
                case AssemblyStat.Defense: return "Защита";
                case AssemblyStat.FrontDefense: return "Защита спереди";
                case AssemblyStat.Feed: return "Подача боеприпасов";
                case AssemblyStat.BuildSeconds: return "Время постройки, с";
                case AssemblyStat.AcquisitionSeconds: return "Наведение, с";
                case AssemblyStat.Damage: return "Урон";
                case AssemblyStat.Interval: return "Интервал выстрелов, с";
                case AssemblyStat.Range: return "Дальность";
                case AssemblyStat.MinimumRange: return "Мёртвая зона";
                case AssemblyStat.MagazineSize: case AssemblyStat.MagazineOverride: return "Ёмкость обоймы";
                case AssemblyStat.ReloadSeconds: return "Перезарядка, с";
                case AssemblyStat.TargetLimit: return "Поражаемых целей";
                case AssemblyStat.EvaluationTargets: return "Расчётных целей";
                case AssemblyStat.Radius: return "Радиус взрыва";
                case AssemblyStat.FlightSeconds: return "Полёт снаряда, с";
                case AssemblyStat.JumpRange: return "Дальность отскока";
                case AssemblyStat.SupportRate: return "Сила поддержки";
                case AssemblyStat.ConeDegrees: return "Угол залпа";
                case AssemblyStat.FiringArcDegrees: return "Сектор обстрела, °";
                case AssemblyStat.Slow: return "Скорость цели";
                case AssemblyStat.SlowSeconds: return "Длительность замедления, с";
                default: return stat.ToString();
            }
        }

        static string PartStats(ComponentDefinition part)
        {
            var lines = new List<string>();
            foreach (var modifier in part.Modifiers)
            {
                if (modifier.Operation == ModifierOperation.Multiply && modifier.Value == 1 || modifier.Operation != ModifierOperation.Multiply && modifier.Value == 0) continue;
                string amount = modifier.Operation == ModifierOperation.Multiply ? "×" + modifier.Value.ToString("0.##", CultureInfo.InvariantCulture)
                    : modifier.Operation == ModifierOperation.Add ? modifier.Value.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture) : modifier.Value.ToString("0.##", CultureInfo.InvariantCulture);
                bool lowIsGood = modifier.Stat == AssemblyStat.Cost || modifier.Stat == AssemblyStat.Load || modifier.Stat == AssemblyStat.BuildSeconds || modifier.Stat == AssemblyStat.AcquisitionSeconds
                    || modifier.Stat == AssemblyStat.Slow || modifier.Stat == AssemblyStat.Interval || modifier.Stat == AssemblyStat.ReloadSeconds || modifier.Stat == AssemblyStat.MinimumRange || modifier.Stat == AssemblyStat.FlightSeconds;
                float delta = modifier.Value - (modifier.Operation == ModifierOperation.Multiply ? 1 : 0);
                string role = modifier.Operation == ModifierOperation.Set || delta == 0 ? "" : (delta > 0 != lowIsGood ? "Плюс: " : "Минус: ");
                lines.Add(role + StatCaption(modifier.Stat) + ": " + amount);
            }
            if (lines.Count == 0) lines.Add("Штатная деталь. Без дополнительных бонусов и штрафов.");
            if (part.RequiredParts.Count > 0) lines.Add("Требует: " + string.Join(", ", part.RequiredParts.Select(reference => reference.Id)));
            return string.Join("\n", lines);
        }

        InputField SequenceName(Transform parent, string name, string value, Rect bounds)
        {
            var fieldRoot = ui.Panel(parent, name, bounds, Color.white, true);
            var field = fieldRoot.gameObject.AddComponent<InputField>(); field.characterLimit = ProfileLimits.MaximumBlueprintNameLength;
            field.textComponent = ui.Label(fieldRoot, name + " value", "", new Rect(8, 2, bounds.width - 16, bounds.height - 4), 16);
            field.SetTextWithoutNotify(ui.Resolve(value)); return field;
        }

        void SequenceAssembly(Transform parent, float w, float h)
        {
            float leftWidth = (w - 12) * .42f;
            var left = ui.Panel(parent, "Sequence assembled preview", new Rect(0, 0, leftWidth, h), WorkshopUi.Surface);
            ui.Label(left, "Sequence assembly title", "Чертёж башни", new Rect(14, 2, leftWidth - 28, 40), WideWorkbench ? 24 : 18, true);
            var preview = SequenceBodyScroll(left, "Sequence assembly preview scroll", new Rect(10, 44, leftWidth - 20, h - 56));
            float diagramHeight = WideWorkbench ? Mathf.Clamp(h * .53f, 150, 410) : Mathf.Clamp(h * .40f, 120, 240);
            SequenceDiagram(preview.Content, new Rect(4, 0, leftWidth - 42, diagramHeight), false);
            float totalsY = diagramHeight + 10, totalsWidth = leftWidth - 42;
            var totals = AssemblyReadings();
            int columnsForTotals = ReadingColumns(totalsWidth, totals.Length);
            if (totals.Length == 0)
                SequenceScrollExtent(preview, SequenceTotals(preview.Content, leftWidth - 20, totalsY) + 8);
            else
            {
                DrawReadings(preview.Content, totals, totalsWidth, totalsY, columnsForTotals, "Sequence assembly reading");
                SequenceScrollExtent(preview, totalsY + Mathf.CeilToInt(totals.Length / (float)columnsForTotals) * ReadingRowHeight(totalsWidth));
            }
            float rw = w - leftWidth - 12;
            var right = ui.Panel(parent, "Sequence fitting library", new Rect(leftWidth + 12, 0, rw, h), WorkshopUi.Surface);
            ui.Label(right, "Sequence fitting heading", "Компоновка башни", new Rect(12, 2, rw - 24, 38), WideWorkbench ? 24 : 18, true);
            var foundations = run.Workshop.Library.Where(entry => entry.Kind == ComponentLibraryKind.Foundation).ToArray();
            int foundationColumns = Mathf.Clamp(Mathf.FloorToInt((rw - 24) / 192), 1, Mathf.Clamp(foundations.Length, 1, 3));
            float foundationWidth = (rw - 40 - (foundationColumns - 1) * 6) / foundationColumns;
            var foundationList = new CatalogScrollList(right, "Sequence assembly foundations", new Rect(12, 42, rw - 24, 44), foundations.Length, foundationColumns, 36, 6);
            for (int i = 0; i < foundations.Length; i++)
            {
                var entry = foundations[i]; var issues = DraftSession.LibraryCompatibility(entry, -1);
                var button = ui.CompactButton(foundationList.Content, "Sequence choose foundation " + entry.Id, new Rect(i % foundationColumns * (foundationWidth + 6), i / foundationColumns * 42, foundationWidth, 36), ui.Resolve(entry.Name),
                    () => { DraftSession.TryApplyCompatibleLibrary(entry, -1); mount = 0; feedback = ""; Refresh(); }, WorkshopIcon.Tower);
                button.interactable = issues.Count == 0; ui.Tip(button.gameObject, issues.Count == 0 ? "Установить основу «" + ui.Resolve(entry.Name) + "»" : SequenceIssues(issues));
                ui.SetSelected(button, DraftSession.Draft.LibraryReferences.Any(reference => reference.Id == entry.Id && reference.Kind == ComponentLibraryKind.Foundation));
            }
            int count = (int)Part(DraftSession.PreviewRecipe.Tower.Foundation.Frame).Get(AssemblyStat.MountCount, ModifierOperation.Set);
            int shownCount = Math.Max(count, DraftSession.PreviewRecipe.Tower.Mounts.Max(item => item.SlotIndex) + 1); mount = Mathf.Clamp(mount, 0, shownCount - 1);
            float slotWidth = (rw - 24 - (shownCount - 1) * 6 - (mount > 0 ? 42 : 0)) / shownCount;
            for (int i = 0; i < shownCount; i++)
            {
                int slot = i;
                string slotCaption = slotWidth > 124 ? "Место " + (i + 1) : (i + 1).ToString();
                var button = ui.CompactButton(right, "Sequence mount " + i, new Rect(12 + i * (slotWidth + 6), 96, slotWidth, 36), slotCaption, () => { mount = slot; Refresh(); }, WorkshopIcon.Weapon);
                FieldMenuSkin.Tab(button);
                ui.SetSelected(button, mount == i); ui.Tip(button.gameObject, i < count ? "Место оружия " + (i + 1) : "Лишнее орудие " + (i + 1));
            }
            if (mount > 0) ui.IconButton(right, "Sequence remove weapon", new Rect(rw - 48, 96, 36, 36), WorkshopIcon.Trash, "Освободить выбранное место", () => { DraftSession.RemoveWeapon(mount); Refresh(); });
            var weapons = run.Workshop.Library.Where(entry => entry.Kind == ComponentLibraryKind.Weapon).ToArray();
            int columns = WideWorkbench && rw >= 610 ? 2 : 1;
            float cardHeight = WideWorkbench ? 90 : 64, gap = WideWorkbench ? 10 : 6;
            float cardWidth = (rw - 40 - (columns - 1) * gap) / columns;
            var weaponList = new CatalogScrollList(right, "Sequence fitting weapons", new Rect(12, 142, rw - 24, h - 154), weapons.Length, columns, cardHeight, gap);
            for (int i = 0; i < weapons.Length; i++)
            {
                var entry = weapons[i]; var issues = DraftSession.LibraryCompatibility(entry, mount);
                var bounds = new Rect(i % columns * (cardWidth + gap), i / columns * (cardHeight + gap), cardWidth, cardHeight);
                var card = ui.CompactButton(weaponList.Content, "Sequence equip " + entry.Id, bounds, "", () => { DraftSession.TryApplyCompatibleLibrary(entry, mount); feedback = ""; Refresh(); });
                card.interactable = issues.Count == 0;
                float thumbnailWidth = WideWorkbench ? 92 : 54, textX = thumbnailWidth + 18;
                var art = WorkshopUi.Graphic<WeaponAssemblyGraphic>(card.transform, "Weapon library drawing " + entry.Id, new Rect(8, 7, thumbnailWidth, cardHeight - 14));
                SequenceWeaponDrawing(art, entry.Snapshot.Weapon);
                var label = ui.Label(card.transform, "Sequence equip name", "", new Rect(textX, WideWorkbench ? 12 : 1, cardWidth - textX - 10, 40), 16);
                label.fontStyle = FontStyle.Bold; SequenceFitLabel(label, ui.Resolve(entry.Name));
                ui.Label(card.transform, "Sequence compatibility", issues.Count == 0 ? "Совместимо" : "Несовместимо", new Rect(textX, cardHeight - 26, cardWidth - textX - 10, 20), 14, color: issues.Count == 0 ? WorkshopUi.Success : WorkshopUi.Warning);
                ui.Tip(card.gameObject, ui.Resolve(entry.Name) + "\n" + (issues.Count == 0 ? "Установить в место " + (mount + 1) : SequenceIssues(issues)));
                ui.SetSelected(card, DraftSession.Draft.LibraryReferences.Any(reference => reference.Id == entry.Id && reference.SlotIndex == mount));
            }
        }

        void SequenceDiagram(Transform parent, Rect rect, bool weaponOnly)
        {
            var panel = ui.Panel(parent, "Sequence schematic", rect, WorkshopUi.Paper);
            var tower = DraftSession.PreviewRecipe.Tower;
            var drawing = WorkshopUi.Graphic<WeaponAssemblyGraphic>(panel, "Sequence tower drawing", new Rect(0, 0, rect.width, rect.height));
            if (weaponOnly)
                SequenceWeaponDrawing(drawing, tower.Mounts.First(item => item.SlotIndex == SequenceWeaponSlot).Weapon);
            else if (AssemblyFitRules.Validate(DraftSession.PreviewRecipe, AssemblyVisualComposer.Fits).Count == 0)
                drawing.SetRecipe(DraftSession.PreviewEntry.Snapshot);
            else
                // A temporarily incompatible draft stays editable; standalone foundation art does not require mounted weapons to fit.
                SequenceFoundationDrawing(drawing, tower.Foundation);
        }

        static void SequenceFoundationDrawing(WeaponAssemblyGraphic drawing, FoundationRecipe foundation)
        {
            if (foundation.Parts.All(AssemblyVisualComposer.HasVisualMapping) &&
                AssemblyVisualComposer.Fits.TryGetFrame(foundation.Frame.Id, foundation.Frame.Revision, out _))
                drawing.SetComposition(AssemblyVisualComposer.ComposeFoundation(foundation));
            else SequenceMappedPartDrawing(drawing, foundation.Parts);
        }

        static void SequenceWeaponDrawing(WeaponAssemblyGraphic drawing, WeaponRecipe weapon)
        {
            if (weapon.Parts.All(AssemblyVisualComposer.HasVisualMapping) &&
                AssemblyVisualComposer.Fits.TryGetWeapon(weapon.Mechanism.Id, weapon.Mechanism.Revision, out _))
                drawing.SetComposition(AssemblyVisualComposer.ComposeWeapon(weapon, AssemblyPreviewPose.ForWeapon()));
            else SequenceMappedPartDrawing(drawing, weapon.Parts);
        }

        static void SequenceMappedPartDrawing(WeaponAssemblyGraphic drawing, IEnumerable<PartRef> parts)
        {
            foreach (var part in parts)
                if (!part.Id.EndsWith(".none", StringComparison.Ordinal) && AssemblyVisualComposer.HasVisualMapping(part))
                { drawing.SetPart(part); return; }
        }

        (WorkshopIcon icon, string caption, string value)[] AssemblyReadings()
        {
            var resolved = DraftSession.Resolved; var preview = DraftSession.PreviewMetrics;
            if (resolved == null && preview == null) return Array.Empty<(WorkshopIcon, string, string)>();
            var culture = ui.TextResolver.Culture;
            var readings = new List<(WorkshopIcon, string, string)> {
                (WorkshopIcon.Ink, "Постройка", (resolved?.Cost ?? preview.Cost) + " штр."),
                (WorkshopIcon.Clock, "Время", (resolved?.BuildSeconds ?? preview.BuildSeconds).ToString("0.##", culture) + " с"),
                (WorkshopIcon.Heart, "Прочность", (resolved?.Health ?? preview.Health).ToString("0.#", culture)),
                (WorkshopIcon.Weight, "Вес", (resolved?.Load ?? preview.Load) + "/" + (resolved?.Capacity ?? preview.Capacity))
            };
            if (resolved != null)
            {
                readings.Add((WorkshopIcon.Target, "Урон/с", resolved.Dps.ToString("0.##", culture)));
                readings.Add((WorkshopIcon.Shield, "Защита", (resolved.Defense * 100).ToString("0.#", culture) + "%"));
            }
            return readings.ToArray();
        }

        float SequenceTotals(Transform parent, float w, float y)
        {
            var resolved = DraftSession.Resolved; var metrics = DraftSession.PreviewMetrics;
            string text = resolved == null && metrics == null ? "Параметры недоступны: исправь состав." :
                "Цена " + (resolved?.Cost ?? metrics?.Cost ?? 0) + " штр. · " + (resolved?.BuildSeconds ?? metrics?.BuildSeconds ?? 0).ToString("0.##") + " с\nHP " +
                (resolved?.Health ?? metrics?.Health ?? 0).ToString("0.#") + " · вес " + (resolved?.Load ?? metrics?.Load ?? 0) + "/" + (resolved?.Capacity ?? metrics?.Capacity ?? 0);
            if (resolved != null) text += "\nУрон/с " + resolved.Dps.ToString("0.##") + " · защита " + (resolved.Defense * 100).ToString("0.#") + "%";
            var label = SequenceCopy(parent, "Sequence totals", text, new Rect(8, y, w - 32, 1), WideWorkbench ? 18 : 16); return y + label.rectTransform.sizeDelta.y;
        }

        void SequenceReview(Transform parent, float w, float h)
        {
            float leftWidth = (w - 12) * .46f;
            var left = ui.Panel(parent, "Sequence final diagram", new Rect(0, 0, leftWidth, h), WorkshopUi.Surface);
            ui.Label(left, "Sequence final title", "Чертёж башни", new Rect(12, 2, leftWidth - 24, 40), WideWorkbench ? 24 : 20, true);
            var preview = SequenceBodyScroll(left, "Sequence final preview scroll", new Rect(10, 44, leftWidth - 20, h - 100));
            float diagramHeight = Mathf.Max(150, preview.Scroll.viewport.rect.height); SequenceDiagram(preview.Content, new Rect(4, 0, leftWidth - 42, diagramHeight), false); SequenceScrollExtent(preview, diagramHeight);
            ui.CompactButton(left, "Composite trial", new Rect(10, h - 50, leftWidth - 20, 40), "Полигон", OpenTrial, WorkshopIcon.Target).interactable = DraftSession.CanTrial;
            float rw = w - leftWidth - 12;
            var right = ui.Panel(parent, "Sequence final stats", new Rect(leftWidth + 12, 0, rw, h), WorkshopUi.Surface);
            ui.Label(right, "Sequence final name label", "Название башни", new Rect(12, 4, rw - 24, 28), 16, true);
            var name = SequenceName(right, "Sequence tower name", DraftSession.Name, new Rect(12, 38, rw - 24, 36));
            name.onValueChanged.AddListener(value => { DraftSession.Rename(value); feedback = ""; RefreshSequenceStatus(); });
            string specification = CompositionText(); var resolved = DraftSession.Resolved;
            if (resolved != null) foreach (var weapon in resolved.Weapons)
                specification += "\n\nОрудие " + (weapon.SlotIndex + 1) + ": урон " + weapon.Damage.ToString("0.#") + " · дальность " + weapon.Range.ToString("0.#")
                    + " кл. · сектор обстрела " + weapon.FiringArcDegrees.ToString("0.#") + "°"
                    + "\nОбойма " + weapon.MagazineSize + " · интервал " + weapon.IntervalAtFeed(resolved.FeedMultiplier).ToString("0.##") + " с · перезарядка " + weapon.ReloadAtFeed(resolved.FeedMultiplier).ToString("0.##") + " с";
            var scroll = SequenceBodyScroll(right, "Sequence final specification", new Rect(12, 86, rw - 24, h - 98));
            float bottom = SequenceTotals(scroll.Content, rw - 24, 0);
            float compositionY = bottom + (WideWorkbench ? 22 : 10);
            var composition = SequenceCopy(scroll.Content, "Sequence final composition", specification, new Rect(8, compositionY, rw - 48, 1), WideWorkbench ? 16 : 14);
            SequenceScrollExtent(scroll, compositionY + composition.rectTransform.sizeDelta.y + 8);
        }

        string SequenceIssues(IEnumerable<AssemblyDiagnostic> issues)
            => string.Join(" · ", issues.Take(2).Select(issue => Diagnostic(issue.Code) + (issue.Arguments.Count == 0 ? "" : " (" + string.Join(" / ", issue.Arguments) + ")")));

        void RefreshSequenceStatus()
        {
            RefreshFirstRefitState();
            var resources = Root.Find("Sequence resources")?.GetComponent<Text>();
            if (resources != null) resources.text = "Золото: " + run.Workshop.Gold + " · Уровень " + run.Workshop.Level + " · Опыт " + run.Workshop.TotalXp;
            var issues = DraftSession.IsComponentOpen ? DraftSession.ComponentDiagnostics : DraftSession.Diagnostics;
            Status = !string.IsNullOrEmpty(feedback) ? feedback : DraftSession.LastResult?.Success == false ? Diagnostic(DraftSession.LastResult.Code)
                : issues.Count > 0 ? SequenceIssues(issues) : Step < 2 ? "Компонент совместим." : "Состав совместим.";
            SetWorkshopNotice("Sequence", DraftSession.LastResult?.Success == false || issues.Count > 0);
            foreach (var button in Root.GetComponentsInChildren<Button>())
            {
                if (button.name == "Sequence next") button.interactable = Step < 2 ? DraftSession.CanSaveComponent : DraftSession.CanSave;
                if (button.name == "Sequence component variant") button.interactable = DraftSession.CanSaveComponent;
                if (button.name == "Sequence save" || button.name == "Sequence variant") button.interactable = DraftSession.CanSave;
            }
        }

        void SetWorkshopNotice(string prefix, bool problem)
        {
            var notice = Root.Find(prefix + " notice")?.GetComponent<Text>();
            if (notice == null) return;
            notice.text = Status;
            notice.color = FieldMenuSkin.TextColor(problem ? WorkshopUi.Warning : WorkshopUi.Muted);
        }
    }
}
