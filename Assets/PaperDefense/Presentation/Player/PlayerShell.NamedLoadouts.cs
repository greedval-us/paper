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
    public sealed partial class PlayerShell
    {
        enum NamedLoadoutMode { Browse, Create, Rename, Update, Delete }
        bool namedLoadoutsOpen;
        GameObject namedLoadoutsOverlay;
        string namedLoadoutSelected, namedLoadoutName = "", namedLoadoutFeedback = "";
        float namedLoadoutScroll = 1;
        NamedLoadoutMode namedLoadoutMode;
        Text namedLoadoutStatus;
        Button namedLoadoutCommit;

        void OpenNamedLoadouts()
        {
            if (game.Page != PlayerPage.Preparation || !game.Run.InWorkshop) return;
            namedLoadoutsOpen = true; namedLoadoutMode = NamedLoadoutMode.Browse; namedLoadoutFeedback = "";
            BuildNamedLoadouts();
        }

        NamedLoadout SelectedNamedLoadout()
            => game.Run.Workshop.NamedLoadouts.FirstOrDefault(value => value.Id == namedLoadoutSelected);

        void CloseNamedLoadouts()
        {
            namedLoadoutsOpen = false; namedLoadoutMode = NamedLoadoutMode.Browse; namedLoadoutName = "";
            if (namedLoadoutsOverlay != null) WorkshopUi.RemoveView(namedLoadoutsOverlay);
            namedLoadoutsOverlay = null;
        }

        void BuildNamedLoadouts()
        {
            if (!namedLoadoutsOpen || game.Page != PlayerPage.Preparation) return;
            var saved = game.Run.Workshop.NamedLoadouts;
            if (SelectedNamedLoadout() == null)
            {
                // A pending operation belongs to one identity. Rebuilding the list must never
                // turn an obsolete rename/update/delete into an operation on its next neighbour.
                if (namedLoadoutMode != NamedLoadoutMode.Create && namedLoadoutMode != NamedLoadoutMode.Browse)
                {
                    namedLoadoutMode = NamedLoadoutMode.Browse; namedLoadoutName = "";
                    namedLoadoutFeedback = NamedLoadoutCopy("profile.loadout.missing");
                }
                namedLoadoutSelected = saved.FirstOrDefault()?.Id;
            }
            string focus = EventSystem.current?.currentSelectedGameObject?.name;
            if (namedLoadoutsOverlay != null) WorkshopUi.RemoveView(namedLoadoutsOverlay);
            var previous = page.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == "Preparation named loadouts");
            var shade = ui.Panel(page, "Named loadouts", new Rect(0, 0, W, H), new Color(.08f, .12f, .18f, .65f), true);
            namedLoadoutsOverlay = shade.gameObject;
            var lifetime = shade.gameObject.AddComponent<NamedLoadoutsDialogLifetime>();
            lifetime.Disabled = () =>
            {
                if (game == null || game.Page != PlayerPage.Preparation)
                { namedLoadoutsOpen = false; namedLoadoutMode = NamedLoadoutMode.Browse; namedLoadoutName = ""; }
            };
            float width = Mathf.Min(960, W - 48), height = Mathf.Min(620, H - 48);
            var sheet = ui.Panel(shade, "Named loadouts sheet", new Rect((W - width) * .5f, (H - height) * .5f, width, height), PlayerUi.Card, true);
            NamedLoadoutLabel(sheet, "Named loadouts title", "Боевые наборы", new Rect(20, 12, width - 300, 38), 26, true);
            var save = NamedLoadoutAction(sheet, "Named loadouts save", "Сохранить текущий", new Rect(width - 272, 12, 204, 38), () =>
            { namedLoadoutMode = NamedLoadoutMode.Create; namedLoadoutName = ""; namedLoadoutFeedback = ""; BuildNamedLoadouts(); });
            bool full = saved.Count >= ProfileLimits.MaximumNamedLoadouts;
            save.interactable = !full && namedLoadoutMode == NamedLoadoutMode.Browse;
            save.GetComponent<PlayerTooltip>().Message = full ? NamedLoadoutCopy("profile.loadout.full") : "Сохранить текущие восемь мест под новым именем";
            ui.IconButton(sheet, "Named loadouts close", new Rect(width - 56, 12, 36, 38), PlayerIcon.Close, "Закрыть боевые наборы", CloseNamedLoadouts);
            var match = saved.FirstOrDefault(value => NamedLoadoutMatchesCurrent(value));
            NamedLoadoutLabel(sheet, "Named loadouts match", match == null ? "Текущий состав не совпадает с сохранёнными наборами." :
                "Совпадает с «" + match.Name + "»", new Rect(20, 58, width - 40, 38), 15);

            const float listWidth = 210, gap = 16;
            float rightX = 20 + listWidth + gap, rightWidth = width - rightX - 20;
            var list = ui.Scroll(sheet, "Named loadouts list", new Rect(20, 104, listWidth, height - 166), Mathf.Max(100, saved.Count * 66));
            var scroll = list.GetComponentInParent<ScrollRect>();
            float savedScroll = namedLoadoutScroll;
            for (int i = 0; i < saved.Count; i++)
            {
                var entry = saved[i];
                var button = NamedLoadoutAction(list, "Named loadouts choose " + entry.Id, entry.Name,
                    new Rect(0, i * 66, listWidth - 14, 58), () =>
                    { namedLoadoutSelected = entry.Id; namedLoadoutFeedback = ""; BuildNamedLoadouts(); });
                button.interactable = namedLoadoutMode == NamedLoadoutMode.Browse;
                ui.MarkSelected(button, entry.Id == namedLoadoutSelected);
                var text = button.GetComponentInChildren<Text>(); text.fontSize = 17;
                PlayerUi.Place(text.rectTransform, new Rect(8, 5, listWidth - 30, 48));
                PreparationAssemblyFitName(text, entry.Name);
                button.GetComponent<PlayerTooltip>().Message = entry.Name + " · " + entry.Slots.Count(id => id != null) + " / 8";
            }
            if (saved.Count == 0)
                NamedLoadoutLabel(list, "Named loadouts empty", "Пока нет наборов.\nСохрани текущий состав, чтобы выбрать его перед другим боем.", new Rect(8, 8, listWidth - 30, 144), 17);
            NamedLoadoutLabel(sheet, "Named loadouts count", saved.Count + " / " + ProfileLimits.MaximumNamedLoadouts + " сохранено",
                new Rect(20, height - 52, listWidth, 36), 15);
            BuildNamedLoadoutDetails(sheet, new Rect(rightX, 104, rightWidth, height - 124), full);
            FieldMenuTheme.ApplyTo(shade, false);
            WorkshopModal.Open(shade.gameObject, NamedLoadoutBack, previous != null ? previous.gameObject : null);
            var input = shade.GetComponentInChildren<InputField>();
            if (input != null) { input.Select(); input.ActivateInputField(); }
            else
            {
                var restored = shade.GetComponentsInChildren<Selectable>().FirstOrDefault(control => control.name == focus && control.IsInteractable());
                if (restored != null) EventSystem.current?.SetSelectedGameObject(restored.gameObject);
            }
            scroll.verticalNormalizedPosition = savedScroll;
            scroll.onValueChanged.AddListener(_ => namedLoadoutScroll = scroll.verticalNormalizedPosition);
        }

        void BuildNamedLoadoutDetails(RectTransform sheet, Rect area, bool full)
        {
            var selected = SelectedNamedLoadout();
            bool naming = namedLoadoutMode == NamedLoadoutMode.Create || namedLoadoutMode == NamedLoadoutMode.Rename;
            bool confirm = namedLoadoutMode == NamedLoadoutMode.Update || namedLoadoutMode == NamedLoadoutMode.Delete;
            string title = namedLoadoutMode == NamedLoadoutMode.Create ? "Сохранить текущий набор" :
                namedLoadoutMode == NamedLoadoutMode.Rename ? "Новое имя набора" :
                namedLoadoutMode == NamedLoadoutMode.Update ? "Обновить сохранённый набор?" :
                namedLoadoutMode == NamedLoadoutMode.Delete ? "Удалить набор?" : selected?.Name ?? "Текущий состав";
            var caption = NamedLoadoutLabel(sheet, "Named loadouts selected", title, new Rect(area.x, area.y, area.width, 42), 21, true);
            PreparationAssemblyFitName(caption, title);
            float previewY = area.y + 52;
            namedLoadoutCommit = null;
            if (naming)
            {
                var fieldRoot = ui.Panel(sheet, "Named loadouts name", new Rect(area.x, previewY, area.width, 42), PlayerUi.Card, true);
                var field = fieldRoot.gameObject.AddComponent<InputField>();
                field.targetGraphic = fieldRoot.GetComponent<Graphic>(); field.characterLimit = ProfileLimits.MaximumLoadoutNameLength;
                field.lineType = InputField.LineType.SingleLine;
                field.textComponent = NamedLoadoutLabel(fieldRoot, "Named loadouts name value", "", new Rect(10, 3, area.width - 20, 36), 20);
                field.textComponent.supportRichText = false;
                field.placeholder = NamedLoadoutLabel(fieldRoot, "Named loadouts name placeholder", "Например: Против брони", new Rect(10, 3, area.width - 20, 36), 17);
                field.SetTextWithoutNotify(namedLoadoutName);
                field.onValueChanged.AddListener(value => { namedLoadoutName = value; namedLoadoutFeedback = ""; RefreshNamedLoadoutName(); });
                NamedLoadoutLabel(sheet, "Named loadouts name hint", "До " + ProfileLimits.MaximumLoadoutNameLength + " символов. Имя должно отличаться от остальных.",
                    new Rect(area.x, previewY + 46, area.width, 36), 14);
                previewY += 88;
            }
            else if (confirm)
            {
                string message = namedLoadoutMode == NamedLoadoutMode.Update ?
                    "В «" + selected.Name + "» будет сохранён текущий состав всех восьми мест, включая пустые." :
                    "«" + selected.Name + "» исчезнет из списка. Текущие боевые места и чертежи в альбоме сохранятся.";
                NamedLoadoutLabel(sheet, "Named loadouts confirmation", message, new Rect(area.x, previewY, area.width, 72), 17);
                previewY += 80;
            }
            float footerY = area.yMax - 40;
            float statusY = footerY - 66;
            float previewBottom = statusY - (naming || confirm ? 10 : 54);
            IReadOnlyList<string> slots = namedLoadoutMode == NamedLoadoutMode.Create || namedLoadoutMode == NamedLoadoutMode.Update || selected == null
                ? game.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id).ToArray() : selected.Slots;
            BuildNamedLoadoutPreview(sheet, slots, new Rect(area.x, previewY, area.width, Mathf.Max(100, previewBottom - previewY)));
            namedLoadoutStatus = NamedLoadoutLabel(sheet, "Named loadouts status", namedLoadoutFeedback,
                new Rect(area.x, statusY, area.width, 58), 15);
            if (naming)
            {
                NamedLoadoutAction(sheet, "Named loadouts cancel", "Отмена", new Rect(area.x, footerY, 120, 40), CancelNamedLoadoutEdit);
                namedLoadoutCommit = NamedLoadoutAction(sheet, "Named loadouts commit", "Сохранить", new Rect(area.xMax - 158, footerY, 158, 40), CommitNamedLoadoutName, true);
                RefreshNamedLoadoutName();
            }
            else if (confirm)
            {
                NamedLoadoutAction(sheet, "Named loadouts cancel", "Отмена", new Rect(area.x, footerY, 120, 40), CancelNamedLoadoutEdit);
                NamedLoadoutAction(sheet, "Named loadouts confirm", namedLoadoutMode == NamedLoadoutMode.Update ? "Обновить" : "Удалить",
                    new Rect(area.xMax - 158, footerY, 158, 40), ConfirmNamedLoadoutChange, true);
            }
            else
            {
                float actionY = statusY - 46, small = (area.width - 16) / 3;
                var rename = NamedLoadoutAction(sheet, "Named loadouts rename", "Имя", new Rect(area.x, actionY, small, 38), () =>
                { namedLoadoutMode = NamedLoadoutMode.Rename; namedLoadoutName = SelectedNamedLoadout()?.Name ?? ""; namedLoadoutFeedback = ""; BuildNamedLoadouts(); });
                var update = NamedLoadoutAction(sheet, "Named loadouts update", "Обновить", new Rect(area.x + small + 8, actionY, small, 38), () =>
                { namedLoadoutMode = NamedLoadoutMode.Update; namedLoadoutFeedback = ""; BuildNamedLoadouts(); });
                update.GetComponent<PlayerTooltip>().Message = "Заменить сохранённый состав текущими восемью местами";
                var delete = NamedLoadoutAction(sheet, "Named loadouts delete", "Удалить", new Rect(area.x + (small + 8) * 2, actionY, small, 38), () =>
                { namedLoadoutMode = NamedLoadoutMode.Delete; namedLoadoutFeedback = ""; BuildNamedLoadouts(); });
                rename.interactable = update.interactable = delete.interactable = selected != null;
                var check = selected == null ? null : game.Run.AssemblyCommands.CheckNamedLoadout(selected.Id);
                var apply = NamedLoadoutAction(sheet, "Named loadouts apply", "Применить", new Rect(area.xMax - 158, footerY, 158, 40), ApplyNamedLoadout, true);
                apply.interactable = check?.Success == true;
                if (string.IsNullOrEmpty(namedLoadoutFeedback))
                    namedLoadoutStatus.text = check?.Success == false ? NamedLoadoutResult(check) : full ? NamedLoadoutCopy("profile.loadout.full") :
                        selected == null ? "Сохрани текущий состав кнопкой сверху." : "Применение заменит все восемь боевых мест. Пустые места тоже сохраняются.";
                if (!apply.interactable) apply.GetComponent<PlayerTooltip>().Message = namedLoadoutStatus.text;
            }
        }

        void BuildNamedLoadoutPreview(Transform parent, IReadOnlyList<string> slots, Rect bounds)
        {
            var preview = ui.Panel(parent, "Named loadouts preview", bounds, Color.clear);
            const float gap = 6;
            float width = (bounds.width - gap * 3) / 4, height = (bounds.height - gap) / 2;
            for (int index = 0; index < ProfileLimits.LoadoutSlotCount; index++)
            {
                string id = index < slots.Count ? slots[index] : null;
                var entry = id == null ? null : game.Run.Workshop.Entries.FirstOrDefault(value => value.Id == id);
                var card = ui.Panel(preview, "Named loadouts slot " + index,
                    new Rect(index % 4 * (width + gap), index / 4 * (height + gap), width, height), PlayerUi.Selected);
                NamedLoadoutLabel(card, "Named loadouts number " + index, (index + 1).ToString(), new Rect(5, 2, 18, 20), 14, true);
                if (entry != null) PreparationAssemblyIllustration(card, entry, new Rect(22, 2, width - 27, Mathf.Max(20, height - 40)));
                string name = entry == null ? (id == null ? "Пусто" : "Нет чертежа") : GameText.Resolve(entry.Name);
                var label = NamedLoadoutLabel(card, "Named loadouts slot name " + index, name, new Rect(5, height - 36, width - 10, 32), 14);
                label.alignment = TextAnchor.MiddleCenter; PreparationAssemblyFitName(label, name);
                contextHints.Tip(card.gameObject, "Место " + (index + 1) + ": " + name);
            }
        }

        void NamedLoadoutBack()
        { if (namedLoadoutMode == NamedLoadoutMode.Browse) CloseNamedLoadouts(); else CancelNamedLoadoutEdit(); }
        void CancelNamedLoadoutEdit()
        { namedLoadoutMode = NamedLoadoutMode.Browse; namedLoadoutName = ""; namedLoadoutFeedback = ""; BuildNamedLoadouts(); }
        void RefreshNamedLoadoutName()
        {
            string reason = NamedLoadoutNameIssue();
            if (namedLoadoutCommit != null) namedLoadoutCommit.interactable = reason == null;
            if (namedLoadoutStatus != null) namedLoadoutStatus.text = !string.IsNullOrEmpty(namedLoadoutFeedback) ? namedLoadoutFeedback : reason ?? "Состав сохранится после нажатия «Сохранить».";
        }
        string NamedLoadoutNameIssue()
        {
            string rawName = namedLoadoutName ?? "", name = rawName.Trim();
            if (name.Length == 0 || name.Length > ProfileLimits.MaximumLoadoutNameLength || rawName.Any(character => char.IsControl(character) || character == '<' || character == '>'))
                return NamedLoadoutCopy("profile.loadout.name");
            if (game.Run.Workshop.NamedLoadouts.Any(value => value.Id != (namedLoadoutMode == NamedLoadoutMode.Rename ? namedLoadoutSelected : null) &&
                string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase))) return NamedLoadoutCopy("profile.loadout.duplicate_name");
            if (namedLoadoutMode == NamedLoadoutMode.Create && game.Run.Workshop.NamedLoadouts.Count >= ProfileLimits.MaximumNamedLoadouts)
                return NamedLoadoutCopy("profile.loadout.full");
            return null;
        }
        void CommitNamedLoadoutName()
        {
            if (NamedLoadoutNameIssue() != null) { RefreshNamedLoadoutName(); return; }
            var result = namedLoadoutMode == NamedLoadoutMode.Create
                ? game.Run.AssemblyCommands.SaveNamedLoadout(namedLoadoutName, Guid.NewGuid())
                : game.Run.AssemblyCommands.RenameNamedLoadout(namedLoadoutSelected, namedLoadoutName, Guid.NewGuid());
            if (result.Success)
            {
                namedLoadoutSelected = result.ResultId ?? namedLoadoutSelected;
                namedLoadoutMode = NamedLoadoutMode.Browse; namedLoadoutName = "";
                namedLoadoutFeedback = "Набор сохранён.";
            }
            else namedLoadoutFeedback = NamedLoadoutResult(result);
            BuildNamedLoadouts();
        }
        void ConfirmNamedLoadoutChange()
        {
            bool deleting = namedLoadoutMode == NamedLoadoutMode.Delete;
            var result = deleting ? game.Run.AssemblyCommands.DeleteNamedLoadout(namedLoadoutSelected, Guid.NewGuid()) :
                game.Run.AssemblyCommands.UpdateNamedLoadout(namedLoadoutSelected, Guid.NewGuid());
            if (result.Success)
            {
                namedLoadoutMode = NamedLoadoutMode.Browse;
                if (deleting) namedLoadoutSelected = null;
                namedLoadoutFeedback = deleting ? "Набор удалён. Боевые места и чертежи сохранены." : "Сохранённый набор обновлён текущим составом.";
            }
            else namedLoadoutFeedback = NamedLoadoutResult(result);
            BuildNamedLoadouts();
        }
        void ApplyNamedLoadout()
        {
            var selected = SelectedNamedLoadout(); if (selected == null) return;
            var result = game.Run.AssemblyCommands.ApplyNamedLoadout(selected.Id, Guid.NewGuid());
            if (!result.Success) { namedLoadoutFeedback = NamedLoadoutResult(result); BuildNamedLoadouts(); return; }
            string name = selected.Name;
            CloseNamedLoadouts(); Show(PlayerPage.Preparation); SetStatus("Применён набор «" + name + "».");
            var entry = page.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == "Preparation named loadouts");
            if (entry != null) EventSystem.current?.SetSelectedGameObject(entry.gameObject);
        }
        bool NamedLoadoutMatchesCurrent(NamedLoadout saved)
        {
            if (saved.Slots.Count != ProfileLimits.LoadoutSlotCount) return false;
            for (int slot = 0; slot < ProfileLimits.LoadoutSlotCount; slot++)
                if (!string.Equals(saved.Slots[slot], game.Run.Workshop.LoadoutEntries[slot]?.Id, StringComparison.Ordinal)) return false;
            return true;
        }
        static string NamedLoadoutCopy(string code) => GameText.Resolve(UserText.Message(code));
        static string NamedLoadoutResult(AssemblyCommandResult result)
        {
            var issue = result.Diagnostics.FirstOrDefault();
            object[] arguments = issue?.Arguments.Select(value => (object)value).ToArray() ?? Array.Empty<object>();
            string message = GameText.Resolve(UserText.Message(result.Code, arguments));
            if (result.Code == "profile.write_failed" && issue?.Arguments.Count > 0) message += "\n" + GameText.Resolve(issue.Arguments[0]);
            return message;
        }
        Text NamedLoadoutLabel(Transform parent, string name, string copy, Rect bounds, int size, bool bold = false)
        {
            var label = NotebookLabel(parent, copy, bounds, size); label.name = name;
            label.supportRichText = false; label.resizeTextForBestFit = false;
            if (bold) label.fontStyle = FontStyle.Bold;
            return label;
        }
        Button NamedLoadoutAction(Transform parent, string name, string copy, Rect bounds, Action action, bool primary = false)
        {
            var button = ui.IconButton(parent, name, bounds, PlayerIcon.Check, copy, action, primary);
            WorkshopUi.RemoveView(button.GetComponentInChildren<PlayerIconGraphic>().gameObject);
            var caption = NamedLoadoutLabel(button.transform, name + " caption", copy, new Rect(6, 1, bounds.width - 12, bounds.height - 2), 17);
            caption.alignment = TextAnchor.MiddleCenter;
            caption.color = primary ? PlayerUi.Card : PlayerUi.Ink;
            return button;
        }
    }

    sealed class NamedLoadoutsDialogLifetime : MonoBehaviour
    {
        public Action Disabled;
        void OnDisable() => Disabled?.Invoke();
    }
}
