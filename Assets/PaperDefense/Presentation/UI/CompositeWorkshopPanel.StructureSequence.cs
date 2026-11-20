using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PaperDefense.Presentation
{
    public sealed partial class CompositeWorkshopPanel
    {
        int structureAssignmentSlot = 7;
        AlbumEntry inspectedStructureOffer;
        static readonly ComponentCategory[] StructureCategories = { ComponentCategory.Site, ComponentCategory.Function, ComponentCategory.Shell, ComponentCategory.Utility };
        static readonly string[] StructureStages = { "Семейство", "Детали", "Проверка" };

        void RefreshStructureSequence()
        {
            rebuilding = true;
            try
            {
                CaptureAssemblyTrialPlayback();
                var focus = EventSystem.current?.currentSelectedGameObject;
                string focusName = focus != null && focus.transform.IsChildOf(Root) ? focus.name : null;
                if (Step == 1 && !DraftSession.IsComponentOpen) DraftSession.OpenStructure();
                CaptureSequenceScroll();
                ui.HideTooltip(); WorkshopUi.ClearViews(Root);
                WorkshopMasthead(true);
                var bounds = WorkbenchBounds;
                var body = ui.Panel(Root, "Structure content", bounds, Color.clear);
                if (Step == 0) StructureFamilies(body, bounds.width, bounds.height);
                else if (Step == 1) StructureEditor(body, bounds.width, bounds.height);
                else StructureSequenceReview(body, bounds.width, bounds.height);
                WorkshopCommandBar("Structure");
                StructureSequenceFooter(); RefreshStructureSequenceStatus();
                if (assemblyTrialOpen) BuildAssemblyTrialSurface();
                ui.EnsureTooltip(Root);
                FieldMenuTheme.ApplyTo(Root);
                RestoreSequenceFocus(focusName);
                RestoreSequenceScroll();
                ui.HideTooltip();
            }
            finally { rebuilding = false; }
        }

        void StructureNavigate(int target)
        {
            if (target < 0 || target > 2 || target == Step) return;
            if (Step == 0 && inspectedStructureOffer != null) return;
            if (target == 2 && DraftSession.IsComponentOpen && !DraftSession.SaveComponent(Guid.NewGuid()).Success)
            { RefreshStructureSequenceStatus(); return; }
            if (target == 2 && !DraftSession.CanSave) { RefreshStructureSequenceStatus(); return; }
            Step = target; sequenceReached = Math.Max(sequenceReached, target); sequenceInspected = null; feedback = ""; Refresh();
        }

        void StructureSequenceFooter()
        {
            ui.IconButton(Root, "Structure back", new Rect(16, height - 56, 44, 44), WorkshopIcon.Back, "Назад", () => {
                if (Step == 0) { Suspend(); BackRequested?.Invoke(); } else StructureNavigate(Step - 1);
            });
            float actionWidth = Mathf.Min(280, width * .36f); var bounds = new Rect(width - actionWidth - 16, height - 56, actionWidth, 44);
            if (Step == 0 && inspectedStructureOffer != null)
            {
                var source = inspectedStructureOffer;
                var offer = run.Workshop.GetStructureOffer(source.Snapshot.Recipe.Structure.Family);
                ui.CompactButton(Root, "Structure purchase family", bounds, "Купить за " + offer.GoldCost + " зол.",
                    () => PurchaseStructureOffer(source), WorkshopIcon.Cost, true).interactable = offer.CanPurchase;
            }
            else if (Step < 2)
                ui.CompactButton(Root, "Structure next", bounds, Step == 0 ? "Детали" : "Проверить", () => StructureNavigate(Step + 1), WorkshopIcon.Next, true);
            else
            {
                ui.IconButton(Root, "Structure variant", new Rect(68, height - 56, 44, 44), WorkshopIcon.Copy, "Сохранить как вариант", () => SaveStructureSequence(true));
                var save = ui.CompactButton(Root, "Structure save", bounds, "В альбом", () => SaveStructureSequence(false), WorkshopIcon.Save, true); ui.Tip(save.gameObject, "Сохранить сооружение в альбом");
            }
        }

        void SaveStructureSequence(bool variant)
        {
            var result = DraftSession.SaveEntry(Guid.NewGuid(), variant);
            feedback = result.Success ? "Сооружение сохранено. Теперь назначь его в боевой набор." : ""; Refresh();
        }

        void StructureFamilies(Transform parent, float w, float h)
        {
            float listWidth = (w - 16) * .58f, previewWidth = w - listWidth - 16;
            var library = ui.Panel(parent, "Structure family library", new Rect(0, 0, listWidth, h), WorkshopUi.Surface);
            ui.Label(library, "Structure family heading", "Семейство сооружений", new Rect(14, 2, listWidth - 28, 40), WideWorkbench ? 24 : 18, true);
            var families = DraftSession.Catalog.Templates.Where(entry => entry.Kind == AlbumEntryKind.Structure).ToArray();
            int columns = WideWorkbench ? 2 : Mathf.Max(1, Mathf.FloorToInt((listWidth - 24) / 192));
            float cardWidth = (listWidth - 40 - (columns - 1) * 12) / columns;
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)families.Length / columns));
            float cardHeight = WideWorkbench ? Mathf.Clamp((h - 60 - (rows - 1) * 12) / rows, 130, 260) : 104;
            var list = new CatalogScrollList(library, "Structure families", new Rect(14, 48, listWidth - 28, h - 60), families.Length, columns, cardHeight, 12);
            for (int i = 0; i < families.Length; i++)
            {
                var entry = families[i]; var family = entry.Snapshot.Recipe.Structure.Family;
                var offer = run.Workshop.GetStructureOffer(family);
                var card = ui.CompactButton(list.Content, "Structure family " + family,
                    new Rect(i % columns * (cardWidth + 12), i / columns * (cardHeight + 12), cardWidth, cardHeight), "", () => InspectStructureFamily(entry));
                var selected = inspectedStructureOffer?.Snapshot.Recipe.Structure.Family ?? DraftSession.PreviewRecipe.Structure.Family;
                ui.SetSelected(card, selected == family);
                var nameRect = WideWorkbench ? new Rect(12, cardHeight - 83, cardWidth - 24, 32) : new Rect(10, 4, cardWidth - 70, 38);
                var artRect = WideWorkbench ? new Rect(8, 7, cardWidth - 16, cardHeight - 92) : new Rect(cardWidth - 56, 5, 46, 36);
                var familyName = ui.Label(card.transform, "Structure family name", "", nameRect, WideWorkbench ? 20 : 16, true);
                SequenceFitLabel(familyName, StructureFamilyCaption(family));
                StandardAssemblyIllustration.Draw(card.transform, entry, artRect, "Structure family thumbnail " + family);
                var role = ui.Label(card.transform, "Structure family purpose", StructureRole(family),
                    new Rect(12, WideWorkbench ? cardHeight - 52 : 42, cardWidth - 24, WideWorkbench ? 25 : 36), 14, color: WorkshopUi.Muted);
                role.alignment = TextAnchor.UpperLeft;
                var state = WorkshopUi.Graphic<WorkshopIconGraphic>(card.transform, "Family ownership icon", new Rect(12, cardHeight - 23, 15, 15));
                state.Icon = offer.Owned ? WorkshopIcon.Check : WorkshopIcon.Lock;
                state.color = offer.Owned ? WorkshopUi.Muted : WorkshopUi.Warning;
                string availability = offer.Owned ? (offer.GoldCost == 0 ? "Доступно" : "Куплено") :
                    offer.GoldCost + " зол. · ур. " + offer.RequiredLevel;
                ui.Label(card.transform, "Family ownership", availability, new Rect(33, cardHeight - 29, cardWidth - 43, 25), 14, color: state.color);
                ui.Tip(card.gameObject, StructureFamilyCaption(family) + "\n" + StructurePurpose(family) + "\n" + availability);
            }
            var preview = ui.Panel(parent, "Structure selected family", new Rect(listWidth + 16, 0, previewWidth, h), WorkshopUi.Surface);
            if (inspectedStructureOffer == null) StructurePreview(preview, previewWidth, h);
            else StructureOfferPreview(preview, previewWidth, h, inspectedStructureOffer);
        }

        void InspectStructureFamily(AlbumEntry source)
        {
            feedback = "";
            var family = source.Snapshot.Recipe.Structure.Family;
            inspectedStructureOffer = run.Workshop.GetStructureOffer(family).Owned ? null : source;
            if (inspectedStructureOffer != null || DraftSession.PreviewRecipe.Structure.Family == family) Refresh();
            else RequestStructureFamily(source);
        }

        void PurchaseStructureOffer(AlbumEntry source)
        {
            var family = source.Snapshot.Recipe.Structure.Family;
            if (!run.Workshop.GetStructureOffer(family).CanPurchase) return;
            var result = DraftSession.PurchaseStructureFamily(family, Guid.NewGuid());
            if (!result.Success) { feedback = Diagnostic(result.Code); Refresh(); return; }
            inspectedStructureOffer = null;
            feedback = "Открыто сооружение «" + StructureFamilyCaption(family) + "».";
            RequestStructureFamily(source);
        }

        void StructureOfferPreview(Transform parent, float w, float h, AlbumEntry source)
        {
            var family = source.Snapshot.Recipe.Structure.Family;
            var offer = run.Workshop.GetStructureOffer(family);
            var title = ui.Label(parent, "Structure offer name", "", new Rect(14, 2, w - 28, 40), WideWorkbench ? 24 : 18, true);
            SequenceFitLabel(title, StructureFamilyCaption(family));
            float diagramHeight = Mathf.Clamp(h * .36f, 78, 260);
            var diagram = ui.Panel(parent, "Structure offer schematic", new Rect(12, 46, w - 24, diagramHeight), WorkshopUi.Paper);
            StandardAssemblyIllustration.Draw(diagram, source, new Rect(0, 0, w - 24, diagramHeight), "Structure offer drawing");
            float top = 56 + diagramHeight;
            var list = SequenceBodyScroll(parent, "Structure offer details", new Rect(14, top, w - 28, h - top - 10));
            float textWidth = w - 48;
            string terms = offer.GoldCost + " золота · уровень " + offer.RequiredLevel;
            if (run.Workshop.Level < offer.RequiredLevel) terms += "\nТвой уровень — " + run.Workshop.Level + ".";
            if (run.Workshop.Gold < offer.GoldCost) terms += "\nНе хватает " + (offer.GoldCost - run.Workshop.Gold) + " зол.";
            var price = SequenceCopy(list.Content, "Structure offer terms", terms, new Rect(0, 0, textWidth, 1), WideWorkbench ? 18 : 16,
                offer.CanPurchase ? WorkshopUi.Ink : WorkshopUi.Warning, true);
            float y = price.rectTransform.sizeDelta.y + 8;
            var purpose = SequenceCopy(list.Content, "Structure offer purpose", StructurePurpose(family), new Rect(0, y, textWidth, 1), WideWorkbench ? 18 : 16);
            y += purpose.rectTransform.sizeDelta.y + 12;
            var resolution = run.PreviewAlbumEntry(source);
            if (resolution.Valid)
            {
                var stats = SequenceCopy(list.Content, "Structure offer statistics", StructureTotalsCopy(resolution.Value),
                    new Rect(0, y, textWidth, 1), WideWorkbench ? 16 : 14, WorkshopUi.Muted);
                y += stats.rectTransform.sizeDelta.y;
            }
            SequenceScrollExtent(list, y + 8);
        }

        void RequestStructureFamily(AlbumEntry source)
        {
            var current = DraftSession.PreviewRecipe.Structure;
            if (current.Family == source.Snapshot.Recipe.Structure.Family) return;
            Action replace = () => {
                string name = DraftSession.Name; var draft = DraftSession.Draft;
                DraftSession.Begin(new AlbumEntry(draft.Id, draft.Name, draft.Revision, source.Snapshot));
                DraftSession.Rename(name); sequenceInspected = null; Refresh();
            };
            var standard = DraftSession.Catalog.Templates.FirstOrDefault(entry => entry.Kind == AlbumEntryKind.Structure && entry.Snapshot.Recipe.Structure.Family == current.Family);
            if (standard == null || !current.Parts.SequenceEqual(standard.Snapshot.Recipe.Structure.Parts))
            {
                Refresh();
                WorkshopConfirm.Show(Root, ui, "Сменить семейство?", "Текущий состав будет заменён стандартными деталями нового семейства. Название и купленные детали сохранятся.", "Заменить состав", replace);
            }
            else replace();
        }

        void StructureEditor(Transform parent, float w, float h)
        {
            float previewWidth = WideWorkbench ? Mathf.Clamp(w * .34f, 325, 560) : Mathf.Clamp(w * .25f, 180, 238);
            float editorWidth = w - previewWidth - 16;
            var preview = ui.Panel(parent, "Structure live preview", new Rect(0, 0, previewWidth, h), WorkshopUi.Surface);
            StructurePreview(preview, previewWidth, h);
            var editor = ui.Panel(parent, "Structure editor", new Rect(previewWidth + 16, 0, editorWidth, h), WorkshopUi.Surface);
            ui.Label(editor, "Structure editor heading", "Сборка", new Rect(12, 6, 76, 36), 18, true);
            StructureName(editor, new Rect(90, 6, editorWidth - 102, 36));
            WorkshopCategoryTabs(editor, editorWidth, 50, StructureCategories, true);
            float partsY = 94, partsHeight = h - partsY - 12;
            float listWidth = WideWorkbench ? Mathf.Clamp(editorWidth * .36f, 228, 344) : (editorWidth - 44) * .39f;
            var parts = DraftSession.CompatibleParts.OrderBy(part => part.RequiredLevel).ThenBy(part => part.UnlockGold).ThenBy(part => part.Id, StringComparer.Ordinal).ToArray();
            string equipped = DraftSession.PreviewRecipe.Structure.Parts.Select(Part).First(part => part.Category == DraftSession.ActiveCategory).Id;
            var inspected = parts.FirstOrDefault(part => part.Id == sequenceInspected) ?? parts.FirstOrDefault(part => part.Id == equipped) ?? parts.FirstOrDefault();
            sequenceInspected = inspected?.Id;
            var list = new CatalogScrollList(editor, "Structure parts", new Rect(12, partsY, listWidth, partsHeight), parts.Length, 1, 64, 6);
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i]; bool owned = DraftSession.IsOwned(part.Id);
                float cardWidth = listWidth - 12;
                var card = ui.CompactButton(list.Content, "Structure inspect " + part.Id, new Rect(0, i * 70, cardWidth, 64), "", () => { sequenceInspected = part.Id; Refresh(); });
                float artWidth = cardWidth >= 212 ? 52 : 32;
                var thumbnail = WorkshopUi.Graphic<WeaponAssemblyGraphic>(card.transform, "Structure part thumbnail " + part.Id, new Rect(6, 8, artWidth, artWidth == 52 ? 44 : 32));
                var reference = new PartRef(part.Id, part.Revision);
                if (AssemblyVisualComposer.HasVisualMapping(reference)) thumbnail.SetPart(reference);
                float textX = artWidth + 14;
                var name = ui.Label(card.transform, "Structure part name", "", new Rect(textX, 3, cardWidth - textX - 26, 36), WideWorkbench ? 16 : 14);
                name.fontStyle = FontStyle.Bold;
                SequenceFitLabel(name, ui.Resolve(part.Name));
                float statusX = WideWorkbench ? textX : 8;
                ui.Label(card.transform, "Structure part availability", part.Id == equipped ? "Установлено" : owned ? "Куплено" : part.UnlockGold + " зол. · ур. " + part.RequiredLevel,
                    new Rect(statusX, 43, cardWidth - statusX - 8, 18), 14, color: owned ? WorkshopUi.Muted : WorkshopUi.Warning);
                if (!owned || part.Id == equipped)
                {
                    var state = WorkshopUi.Graphic<WorkshopIconGraphic>(card.transform, "Structure part state " + part.Id, new Rect(cardWidth - 22, 8, 16, 16));
                    state.Icon = part.Id == equipped ? WorkshopIcon.Check : WorkshopIcon.Lock;
                    state.color = owned ? WorkshopUi.Blue : WorkshopUi.Warning;
                }
                ui.Tip(card.gameObject, ui.Resolve(part.Name) + "\n" + StructurePartCondition(part, equipped));
                ui.SetSelected(card, part.Id == inspected?.Id);
            }
            RevealSequenceRow(list, Array.FindIndex(parts, part => part.Id == sequenceInspected), 70, partsHeight);
            float detailWidth = editorWidth - listWidth - 44;
            var detail = ui.Panel(editor, "Structure part detail", new Rect(listWidth + 28, partsY, detailWidth, partsHeight), WorkshopUi.Paper);
            if (inspected != null) StructurePartDetail(detail, inspected, equipped, detailWidth, partsHeight);
        }

        void StructurePartDetail(Transform parent, ComponentDefinition part, string equipped, float w, float h)
        {
            bool owned = DraftSession.IsOwned(part.Id);
            const float actionHeight = 36;
            var scroll = SequenceBodyScroll(parent, "Structure consequences", new Rect(8, 6, w - 16, h - actionHeight - 18));
            float textWidth = w - 34;
            var title = ui.Label(scroll.Content, "Structure inspected name", "", new Rect(4, 0, textWidth - (WideWorkbench ? 8 : 76), 40), WideWorkbench ? 23 : 16, true);
            SequenceFitLabel(title, ui.Resolve(part.Name)); ui.Tip(title.gameObject, ui.Resolve(part.Name));
            var artRect = InspectedPartBounds(textWidth, h);
            float y = WideWorkbench ? artRect.yMax + 14 : 48;
            var drawing = WorkshopUi.Graphic<WeaponAssemblyGraphic>(scroll.Content, "Structure part drawing", artRect);
            var reference = new PartRef(part.Id, part.Revision);
            if (AssemblyVisualComposer.HasVisualMapping(reference)) drawing.SetPart(reference);
            string conditions = owned ? "" : part.UnlockGold + " зол. · ур. " + part.RequiredLevel;
            var condition = SequenceCopy(scroll.Content, "Structure conditions", conditions, new Rect(0, y, textWidth, 1), 14, WorkshopUi.Warning);
            if (conditions.Length > 0) ui.Tip(condition.gameObject, StructurePartCondition(part, equipped));
            else condition.gameObject.SetActive(false);
            if (conditions.Length > 0) y += condition.rectTransform.sizeDelta.y + 4;
            var candidate = StructureCandidate(part);
            string effects = StructurePartEffects(part), totals = candidate.Valid ? StructureDeltaCopy(DraftSession.Resolved, candidate.Value) : "Установка несовместима:\n" + SequenceIssues(candidate.Diagnostics);
            var totalsLabel = SequenceCopy(scroll.Content, "Structure candidate totals", totals, new Rect(4, y, textWidth - 8, 1), WideWorkbench ? 16 : 14, WorkshopUi.Blue);
            // The surface shows resolved consequences; raw modifiers remain in the full receipt.
            SequenceScrollExtent(scroll, y + totalsLabel.rectTransform.sizeDelta.y + 8);
            ui.IconButton(parent, "Structure candidate details", new Rect(8, h - actionHeight - 6, 36, 36), WorkshopIcon.Help,
                "Полные условия и параметры выбранной детали", () => UiHelp.Show(Root, ui.Resolve(part.Name),
                    StructurePartCondition(part, equipped) + "\n\n" + totals + (candidate.Valid ? "\n\nПосле установки:\n" + StructureTotalsCopy(candidate.Value) : "") + "\n\n" + effects));
            string caption = part.Id == equipped ? "Установлено" : owned ? "Установить" : "Купить · " + part.UnlockGold;
            if (!owned && run.Workshop.Level < part.RequiredLevel) caption = "Нужен ур. " + part.RequiredLevel;
            else if (!owned && run.Workshop.Gold < part.UnlockGold) caption = "Нужно " + part.UnlockGold + " зол.";
            var install = ui.CompactButton(parent, "Structure install " + part.Id, new Rect(52, h - actionHeight - 6, w - 60, actionHeight), caption, () => {
                if (!DraftSession.IsOwned(part.Id) && !DraftSession.PurchasePart(part.Id, Guid.NewGuid()).Success) { feedback = ""; Refresh(); return; }
                DraftSession.SelectPart(part.Id); feedback = ""; Refresh();
            }, owned ? WorkshopIcon.Check : WorkshopIcon.Cost, true);
            ui.Tip(install.gameObject, StructurePartCondition(part, equipped));
            install.interactable = part.Id != equipped && candidate.Valid && (owned || run.Workshop.Level >= part.RequiredLevel && run.Workshop.Gold >= part.UnlockGold);
        }

        string StructurePartCondition(ComponentDefinition part, string equipped)
            => part.Id == equipped ? "Установлено" : DraftSession.IsOwned(part.Id) ? "Куплено навсегда · можно установить" :
                "Уровень " + part.RequiredLevel + " (твой: " + run.Workshop.Level + ")\nЦена: " + part.UnlockGold + " золота (есть " + run.Workshop.Gold + ")";

        AssemblyResolution StructureCandidate(ComponentDefinition selected)
        {
            var current = DraftSession.PreviewRecipe.Structure;
            var parts = current.Parts.Select(reference => Part(reference).Category == selected.Category ? new PartRef(selected.Id, selected.Revision) : reference).ToArray();
            var recipe = new AssemblyRecipe(new StructureRecipe(current.Family, parts[0], parts[1], parts[2], parts[3]));
            var definitions = DraftSession.PreviewEntry.Snapshot.Definitions.Concat(new[] { selected }).GroupBy(part => (part.Id, part.Revision)).Select(group => group.Last());
            var snapshot = new RecipeSnapshot(recipe, DraftSession.PreviewEntry.Snapshot.BalanceRevision, definitions);
            var preview = new AlbumEntry(DraftSession.Draft.Id, DraftSession.Draft.Name, DraftSession.Draft.Revision, snapshot);
            return run.PreviewAlbumEntry(preview);
        }

        string StructureDeltaCopy(ResolvedEntry current, ResolvedEntry candidate)
        {
            if (current == null) return "Сначала исправь текущий состав.";
            var changes = new List<string>();
            string Number(float value) => value.ToString("0.###", ui.TextResolver.Culture);
            void Difference(string caption, float before, float after)
            {
                if (Math.Abs(after - before) < .0001f) return;
                string delta = (after - before).ToString("+0.###;-0.###;0", ui.TextResolver.Culture);
                changes.Add(caption + ": " + Number(before) + " → " + Number(after) + " (" + delta + ")");
            }
            Difference("Цена, штр.", current.Cost, candidate.Cost);
            Difference("Прочность, HP", current.Health, candidate.Health);
            Difference("Постройка, с", current.BuildSeconds, candidate.BuildSeconds);
            Difference("Вес", current.Load, candidate.Load);
            Difference("Допустимый вес", current.Capacity, candidate.Capacity);
            Difference("Защита, %", current.Defense * 100, candidate.Defense * 100);
            Difference("Спереди, %", current.FrontDefense * 100, candidate.FrontDefense * 100);
            if (current.Support != null && candidate.Support != null && current.Support.Kind == candidate.Support.Kind)
            {
                Difference("Радиус", current.Support.Radius, candidate.Support.Radius);
                Difference("Ремонт, HP/с", current.Support.RepairPerSecond, candidate.Support.RepairPerSecond);
                Difference("Урон шипов", current.Support.SpikeDamage, candidate.Support.SpikeDamage);
                Difference("Урон ожога в секунду", current.Support.BurnDamagePerSecond, candidate.Support.BurnDamagePerSecond);
                Difference("Ослабление брони, п. п.", current.Support.ArmorReduction * 100, candidate.Support.ArmorReduction * 100);
                Difference("Доход, штр./волну", current.Support.IncomePerWave, candidate.Support.IncomePerWave);
                Difference("Скорость врага, %", current.Support.SlowSpeedMultiplier * 100, candidate.Support.SlowSpeedMultiplier * 100);
                Difference("Эффект, с", current.Support.SlowSeconds, candidate.Support.SlowSeconds);
                if (current.Support.Kind == AssemblyStructureFamily.Generator)
                    Difference("Окупаемость, волн", current.Support.PaybackWaves(current.Cost), candidate.Support.PaybackWaves(candidate.Cost));
            }
            return changes.Count == 0 ? "Параметры не меняются." : "Текущая → выбранная\n" + string.Join("\n", changes);
        }

        string StructurePartEffects(ComponentDefinition part)
        {
            string effects = PartStats(part);
            var family = DraftSession.PreviewRecipe.Structure.Family;
            if (family == AssemblyStructureFamily.Repair) effects = effects.Replace("Сила поддержки", "Ремонт, HP/с");
            if (family == AssemblyStructureFamily.Spikes) effects = effects.Replace("Сила поддержки", "Урон шипов");
            if (family == AssemblyStructureFamily.Beacon) effects = effects.Replace("Сила поддержки", "Ослабление брони (доля)");
            if (family == AssemblyStructureFamily.Prism) effects = effects.Replace("Сила поддержки", "Урон ожога в секунду");
            if (family == AssemblyStructureFamily.Generator) effects = effects.Replace("Сила поддержки", "Доход, штр./волну");
            return effects.Replace("Дальность:", "Радиус действия:");
        }

        void StructureName(Transform parent, Rect bounds)
        {
            var input = SequenceName(parent, "Structure name", DraftSession.Name, bounds);
            input.onValueChanged.AddListener(value => { DraftSession.Rename(value); feedback = ""; RefreshStructureSequenceStatus(); });
            ui.Tip(input.gameObject, "Название сооружения в альбоме и боевом наборе");
        }

        void StructurePreview(Transform parent, float w, float h)
        {
            var family = DraftSession.PreviewRecipe.Structure.Family;
            int titleSize = WideWorkbench ? 24 : 18;
            var heading = ui.Label(parent, "Structure preview heading", StructureFamilyCaption(family), new Rect(14, 2, w - 28, 40), titleSize, true);
            heading.resizeTextForBestFit = true; heading.resizeTextMinSize = 14; heading.resizeTextMaxSize = titleSize;
            float diagramHeight = WideWorkbench ? Mathf.Clamp(h * .53f, 150, 410) : Mathf.Clamp(h * .32f, 76, 160);
            StructureDiagram(parent, new Rect(12, 46, w - 24, diagramHeight));
            string copy = StructureTotalsCopy(DraftSession.Resolved);
            var scroll = SequenceBodyScroll(parent, "Structure live statistics", new Rect(14, diagramHeight + 60, w - 28, h - diagramHeight - 72));
            var totals = SequenceCopy(scroll.Content, "Structure totals", copy, new Rect(0, 0, w - 44, 1), WideWorkbench ? 18 : 16);
            SequenceScrollExtent(scroll, totals.rectTransform.sizeDelta.y + 8);
        }

        void StructureDiagram(Transform parent, Rect bounds)
        {
            var diagram = ui.Panel(parent, "Structure schematic", bounds, WorkshopUi.Paper);
            var drawing = WorkshopUi.Graphic<WeaponAssemblyGraphic>(diagram, "Structure drawing", new Rect(0, 0, bounds.width, bounds.height));
            if (AssemblyFitRules.Validate(DraftSession.PreviewRecipe, AssemblyVisualComposer.Fits).Count == 0)
                drawing.SetRecipe(DraftSession.PreviewEntry.Snapshot);
            else
                // The function remains inspectable while the full support recipe is being corrected.
                SequenceMappedPartDrawing(drawing, new[] { DraftSession.PreviewRecipe.Structure.Function }.Concat(DraftSession.PreviewRecipe.Structure.Parts));
        }

        void StructureSequenceReview(Transform parent, float w, float h)
        {
            float leftWidth = (w - 16) * .44f, rightWidth = w - leftWidth - 16;
            var left = ui.Panel(parent, "Structure review preview", new Rect(0, 0, leftWidth, h), WorkshopUi.Surface);
            StructurePreview(left, leftWidth, h - 52);
            ui.CompactButton(left, "Composite trial", new Rect(12, h - 44, leftWidth - 24, 32),
                "Полигон", OpenTrial, WorkshopIcon.Target).interactable = DraftSession.CanTrial;
            var right = ui.Panel(parent, "Structure review actions", new Rect(leftWidth + 16, 0, rightWidth, h), WorkshopUi.Surface);
            ui.Label(right, "Structure final name caption", "Название", new Rect(14, 4, rightWidth - 28, 28), 18, true);
            StructureName(right, new Rect(14, 38, rightWidth - 28, 36));
            string composition = string.Join("\n", DraftSession.PreviewRecipe.Structure.Parts.Select(reference => {
                var part = Part(reference); return StructureCategoryCaption(part.Category) + ": " + ui.Resolve(part.Name);
            }));
            float compositionHeight = Mathf.Max(60, h - 260);
            var specification = SequenceBodyScroll(right, "Structure final specification", new Rect(14, 84, rightWidth - 28, compositionHeight));
            var compositionLabel = SequenceCopy(specification.Content, "Structure final composition", composition, new Rect(0, 0, rightWidth - 46, 1), WideWorkbench ? 17 : 14);
            SequenceScrollExtent(specification, compositionLabel.rectTransform.sizeDelta.y + 8);
            float slotsY = h - 164;
            ui.Label(right, "Structure assignment heading", "Место в наборе", new Rect(14, slotsY, rightWidth - 28, 24), 16, true);
            float slotWidth = (rightWidth - 52) / 4;
            for (int i = 0; i < ProfileLimits.LoadoutSlotCount; i++)
            {
                int slot = i; var entry = run.Workshop.LoadoutEntries[i];
                var button = ui.CompactButton(right, "Structure slot " + i, new Rect(14 + i % 4 * (slotWidth + 8), slotsY + 30 + i / 4 * 38, slotWidth, 32),
                    (i + 1).ToString(), () => { structureAssignmentSlot = slot; Refresh(); }, entry == null ? WorkshopIcon.Plus : WorkshopIcon.Book);
                if (slotWidth >= 155)
                {
                    var caption = button.transform.Find("Caption").GetComponent<UnityEngine.UI.Text>();
                    SequenceFitLabel(caption, (i + 1) + " · " + (entry == null ? "Свободно" : ui.Resolve(entry.Name)));
                }
                ui.SetSelected(button, structureAssignmentSlot == i);
                ui.Tip(button.gameObject, entry == null ? "Свободное место " + (i + 1) : "Заменить «" + ui.Resolve(entry.Name) + "» в месте " + (i + 1));
            }
            ui.CompactButton(right, "Structure assign", new Rect(14, h - 46, rightWidth - 28, 38), "Назначить в место " + (structureAssignmentSlot + 1), () => {
                if (!StructureDraftIsSaved()) return;
                var result = run.AssemblyCommands.AssignSlot(structureAssignmentSlot, DraftSession.Draft.Id, Guid.NewGuid());
                feedback = result.Success ? "Сооружение назначено в место " + (structureAssignmentSlot + 1) + ". Можно перейти к подготовке." : Diagnostic(result.Code); Refresh();
            }, WorkshopIcon.Check, true).interactable = StructureDraftIsSaved();
        }

        bool StructureDraftIsSaved()
        {
            if (!DraftSession.CanSave) return false;
            var saved = run.Workshop.Entries.FirstOrDefault(entry => entry.Id == DraftSession.Draft.Id);
            var recipe = DraftSession.PreviewRecipe.Structure;
            return saved?.Kind == AlbumEntryKind.Structure && saved.Name == DraftSession.Name &&
                saved.Snapshot.Recipe.Structure.Family == recipe.Family && saved.Snapshot.Recipe.Structure.Parts.SequenceEqual(recipe.Parts);
        }

        static string StructureFamilyCaption(AssemblyStructureFamily family)
        {
            switch (family)
            {
                case AssemblyStructureFamily.Wall: return "Баррикада";
                case AssemblyStructureFamily.Slow: return "Замедлитель";
                case AssemblyStructureFamily.Repair: return "Ремонтная мастерская";
                case AssemblyStructureFamily.Generator: return "Насос штрихов";
                case AssemblyStructureFamily.Spikes: return "Шиповая ловушка";
                case AssemblyStructureFamily.Beacon: return "Маяк наводки";
                case AssemblyStructureFamily.Prism: return "Чернильная призма";
                default: return family.ToString();
            }
        }

        static string StructureCategoryCaption(ComponentCategory category)
        {
            switch (category)
            {
                case ComponentCategory.Site: return "Площадка";
                case ComponentCategory.Function: return "Механизм";
                case ComponentCategory.Shell: return "Оболочка";
                case ComponentCategory.Utility: return "Оснащение";
                default: return category.ToString();
            }
        }

        static string StructurePurpose(AssemblyStructureFamily family)
        {
            switch (family)
            {
                case AssemblyStructureFamily.Wall: return "Плюс: прочное заграждение удерживает врагов.\nМинус: само не наносит урон.";
                case AssemblyStructureFamily.Slow: return "Плюс: замедляет врагов рядом.\nМинус: небольшая зона и низкая прочность.";
                case AssemblyStructureFamily.Repair: return "Плюс: чинит ближайшее повреждённое сооружение.\nМинус: работает только в своём радиусе; не чинит ремонтные посты.";
                case AssemblyStructureFamily.Generator: return "Плюс: приносит штрихи после волны.\nМинус: только карандаш; максимум два; без возврата цены.";
                case AssemblyStructureFamily.Spikes: return "Раз в секунду бьёт всех врагов рядом. Поставь ловушку на пути группы рядом с замедлителем, чтобы шипы успели ударить повторно.";
                case AssemblyStructureFamily.Beacon: return "Помечает бронированных врагов. Поставь маяк так, чтобы его поле перекрывало сектор обстрела башни. В общей зоне нескольких маяков действует сильнейшая метка.";
                case AssemblyStructureFamily.Prism: return "Каждые две секунды меняет цвет импульса. Красный поджигает врагов, синий замедляет, зелёный ослабляет броню. Удерживай колонну в радиусе призмы, чтобы она попала под несколько импульсов.";
                default: return "";
            }
        }

        static string StructureRole(AssemblyStructureFamily family)
        {
            switch (family)
            {
                case AssemblyStructureFamily.Wall: return "Удерживает врагов";
                case AssemblyStructureFamily.Slow: return "Замедляет врагов рядом";
                case AssemblyStructureFamily.Repair: return "Чинит ближайшее сооружение";
                case AssemblyStructureFamily.Generator: return "Приносит штрихи после волны";
                case AssemblyStructureFamily.Spikes: return "Бьёт по скоплению врагов";
                case AssemblyStructureFamily.Beacon: return "Ослабляет броню врагов";
                case AssemblyStructureFamily.Prism: return "Чередует три цветных импульса";
                default: return "";
            }
        }

        string StructureTotalsCopy(ResolvedEntry resolved)
        {
            if (resolved == null) return "Состав несовместим. Проверь вес и детали.";
            string Number(float value) => value.ToString("0.###", ui.TextResolver.Culture);
            string text = "Постройка: " + resolved.Cost + " штр. · " + Number(resolved.BuildSeconds) + " с\nПрочность: " + Number(resolved.Health) + " HP\nВес: " + resolved.Load + " / " + resolved.Capacity + " · Защита: " + Number(resolved.Defense * 100) + "%";
            var support = resolved.Support;
            if (support == null) return text;
            if (support.Kind == AssemblyStructureFamily.Repair) text += "\nРемонт: " + Number(support.RepairPerSecond) + " HP/с\nРадиус: " + Number(support.Radius) + " клеток";
            if (support.Kind == AssemblyStructureFamily.Slow) text += "\nСкорость врага: ×" + Number(support.SlowSpeedMultiplier) + "\nРадиус: " + Number(support.Radius) + " · Эффект: " + Number(support.SlowSeconds) + " с";
            if (support.Kind == AssemblyStructureFamily.Generator) text += "\nДоход: " + support.IncomePerWave + " штр./волну\nОкупаемость: " + support.PaybackWaves(resolved.Cost) + " волн · Лимит: " + support.MaximumInstances;
            if (support.Kind == AssemblyStructureFamily.Wall) text += "\nРоль: блокирует путь врагам";
            if (support.Kind == AssemblyStructureFamily.Spikes) text += "\nУрон раз в секунду: " + Number(support.SpikeDamage) + "\nРадиус: " + Number(support.Radius) + " клеток";
            if (support.Kind == AssemblyStructureFamily.Beacon) text += "\nОслабление брони: " + Number(support.ArmorReduction * 100) + " п. п.\nРадиус: " + Number(support.Radius) + " клеток";
            if (support.Kind == AssemblyStructureFamily.Prism) text += "\n" + ui.Resolve(GameText.TrapOrBeaconEffect(support));
            return text;
        }

        void RefreshStructureSequenceStatus()
        {
            var resources = Root.Find("Structure resources")?.GetComponent<UnityEngine.UI.Text>();
            if (resources != null) resources.text = "Золото: " + run.Workshop.Gold + " · Уровень " + run.Workshop.Level + " · Опыт " + run.Workshop.TotalXp;
            var issues = DraftSession.IsComponentOpen ? DraftSession.ComponentDiagnostics : DraftSession.Diagnostics;
            Status = !string.IsNullOrEmpty(feedback) ? feedback : DraftSession.LastResult?.Success == false ? Diagnostic(DraftSession.LastResult.Code) :
                issues.Count > 0 ? SequenceIssues(issues) : Step == 0 && inspectedStructureOffer != null ? "Покупка навсегда откроет семейство в мастерской." :
                Step == 0 ? "Выбери семейство." : "Состав совместим.";
            SetWorkshopNotice("Structure", DraftSession.LastResult?.Success == false || issues.Count > 0);
            foreach (var button in Root.GetComponentsInChildren<UnityEngine.UI.Button>())
            {
                if (button.name == "Structure next") button.interactable = Step == 0 || DraftSession.CanSaveComponent;
                if (button.name == "Structure save" || button.name == "Structure variant") button.interactable = DraftSession.CanSave;
                if (button.name == "Structure assign") button.interactable = StructureDraftIsSaved();
                if (button.name == "Structure step 1" && Step == 0 && inspectedStructureOffer != null) button.interactable = false;
                if (button.name == "Structure step 2") button.interactable = inspectedStructureOffer == null && sequenceReached >= 2 && (DraftSession.IsComponentOpen ? DraftSession.CanSaveComponent : DraftSession.CanSave);
            }
        }
    }
}
