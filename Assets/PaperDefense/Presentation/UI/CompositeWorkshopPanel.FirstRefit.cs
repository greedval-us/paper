using System;
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
        string firstRefitDraftId, firstRefitTrialRecipe;
        AlbumEntry firstRefitBaseline;
        int firstRefitSlot;
        bool firstRefitFinished, firstRefitNeedsBaseline;
        float firstRefitOriginalRange;

        bool FirstRefitScoped => firstRefitDraftId != null && DraftSession?.Draft?.Id == firstRefitDraftId && Route == AlbumEntryKind.Tower;
        public bool FirstRefitActive => FirstRefitScoped && !firstRefitFinished;

        public void BeginFirstRefit(AlbumEntry source, int sourceSlot)
        {
            if (!sequentialFlow || !run.InWorkshop || !run.CanOfferFirstRefitTutorial || source?.Kind != AlbumEntryKind.Tower ||
                sourceSlot < 0 || sourceSlot >= ProfileLimits.LoadoutSlotCount ||
                !source.Snapshot.Recipe.Tower.Mounts.Any(item => item.SlotIndex == 0)) return;
            var barrel = DraftSession.Catalog.Definitions.Where(part => part.Id == "head.long")
                .OrderByDescending(part => part.Revision).FirstOrDefault();
            if (barrel == null || !run.Workshop.IsUnlocked(barrel.Id)) return;
            if (!run.BeginFirstRefitTutorial(out _)) return;
            ClearFirstRefit();
            firstRefitBaseline = source;
            firstRefitSlot = sourceSlot;
            var baseline = new WorkshopDraftSession(run, DraftSession.Catalog);
            baseline.Begin(source);
            firstRefitOriginalRange = baseline.Resolved?.Weapons.FirstOrDefault(weapon => weapon.SlotIndex == 0)?.Range ?? 0;
            var copy = new AlbumEntry("entry." + Guid.NewGuid().ToString("N"), "Дальний дозор", 1, source.Snapshot, source.LibraryReferences);
            firstRefitDraftId = copy.Id;
            OpenEntry(copy, barrel, 0);
            var install = Root.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == "Sequence install head.long");
            if (install != null && install.IsInteractable()) EventSystem.current?.SetSelectedGameObject(install.gameObject);
        }

        void ClearFirstRefit()
        {
            firstRefitDraftId = firstRefitTrialRecipe = null;
            firstRefitBaseline = null;
            firstRefitFinished = firstRefitNeedsBaseline = false;
        }

        static string FirstRefitRecipeKey(AssemblyRecipe recipe)
        {
            if (recipe?.Tower == null) return "";
            string Parts(System.Collections.Generic.IEnumerable<PartRef> parts)
                => string.Join(";", parts.Select(part => part.Id + "@" + part.Revision));
            return "foundation:" + Parts(recipe.Tower.Foundation.Parts) + "|" + string.Join("|", recipe.Tower.Mounts
                .OrderBy(item => item.SlotIndex).Select(item => "mount:" + item.SlotIndex + ":" + Parts(item.Weapon.Parts)));
        }

        bool FirstRefitHasBarrel => DraftSession.PreviewRecipe?.Tower?.Mounts
            .Any(item => item.SlotIndex == 0 && item.Weapon.Head.Id == "head.long") == true;

        bool FirstRefitWasTested => FirstRefitHasBarrel && firstRefitTrialRecipe != null &&
            firstRefitTrialRecipe == FirstRefitRecipeKey(DraftSession.PreviewRecipe);

        bool FirstRefitIsSaved()
        {
            if (!FirstRefitActive || DraftSession.IsComponentOpen || !DraftSession.CanSave) return false;
            var saved = run.Workshop.Entries.FirstOrDefault(entry => entry.Id == DraftSession.Draft.Id);
            return saved != null && saved.Name == DraftSession.Name &&
                FirstRefitRecipeKey(saved.Snapshot.Recipe) == FirstRefitRecipeKey(DraftSession.PreviewRecipe);
        }

        void RefreshFirstRefitState()
        {
            if (!FirstRefitActive) return;
            if (firstRefitTrialRecipe != null && firstRefitTrialRecipe != FirstRefitRecipeKey(DraftSession.PreviewRecipe))
                firstRefitTrialRecipe = null;
            var guide = Root.Find("First refit guide");
            var instruction = guide?.Find("First refit instruction")?.GetComponent<Text>();
            if (instruction != null) instruction.text = FirstRefitCopy();
            var assign = guide?.Find("First refit assign")?.GetComponent<Button>();
            if (assign != null) assign.interactable = FirstRefitWasTested && FirstRefitIsSaved();
        }

        void RecordFirstRefitTrial()
        {
            if (!FirstRefitActive || !FirstRefitHasBarrel || LastAssemblyTrial == null) return;
            string recipe = FirstRefitRecipeKey(DraftSession.PreviewRecipe);
            firstRefitNeedsBaseline = LastAssemblyTrialComparison == null ||
                FirstRefitRecipeKey(LastAssemblyTrialComparison.Resolved?.Snapshot?.Recipe) != FirstRefitRecipeKey(firstRefitBaseline.Snapshot.Recipe);
            if (FirstRefitRecipeKey(LastAssemblyTrial.Resolved?.Snapshot?.Recipe) == recipe &&
                !firstRefitNeedsBaseline)
                firstRefitTrialRecipe = recipe;
        }

        void FirstRefitEntrySaved(AssemblyCommandResult result)
        {
            if (firstRefitDraftId != null && !firstRefitFinished && result.Success)
                firstRefitDraftId = result.ResultId;
        }

        string FirstRefitInstruction()
        {
            if (firstRefitFinished) return "4/4 · Готово! Башня в боевом наборе.";
            if (Step == 0) return "1/4 · Нажми «Оружие», затем установи длинный ствол.";
            if (!FirstRefitHasBarrel) return Step == 1 ? "1/4 · Установи длинный ствол." : "1/4 · Назад к оружию → длинный ствол.";
            if (DraftSession.IsComponentOpen) return "1/4 · Ствол установлен. Нажми «Сборка».";
            if (Step == 2) return "2/4 · Нажми «Проверить», затем «Полигон».";
            if (!FirstRefitWasTested && firstRefitNeedsBaseline) return "2/4 · В полигоне верни исходный Дозор в B и испытай.";
            if (!FirstRefitWasTested) return "2/4 · Полигон → Испытать. Сравни с исходным Дозором.";
            if (!FirstRefitIsSaved()) return "3/4 · Испытание готово. Нажми «В альбом».";
            return "4/4 · Назначь сохранённую башню в боевой набор.";
        }

        string FirstRefitCopy()
        {
            float currentRange = DraftSession.Resolved?.Weapons.FirstOrDefault(weapon => weapon.SlotIndex == 0)?.Range ?? 0;
            return FirstRefitInstruction() + "\nДальность: " + firstRefitOriginalRange.ToString("0.#") + " → " + currentRange.ToString("0.#") + " кл.";
        }

        void BuildFirstRefitGuide(float railHeight)
        {
            RefreshFirstRefitState();
            float inset = WorkshopInset, guideWidth = width - inset * 2;
            float guideHeight = WideWorkbench ? 48 : 44;
            var guide = ui.Panel(Root, "First refit guide", new Rect(inset, railHeight + 7, guideWidth, guideHeight), WorkshopUi.Surface);
            bool assign = FirstRefitActive && FirstRefitWasTested && FirstRefitIsSaved();
            float skipWidth = firstRefitFinished ? 0 : WideWorkbench ? 120 : 90;
            float actionWidth = firstRefitFinished || assign ? 184 : 0;
            float textWidth = guideWidth - skipWidth - actionWidth - 24;
            ui.Label(guide, "First refit instruction", FirstRefitCopy(), new Rect(10, 0, textWidth, guideHeight), 14);
            if (!firstRefitFinished)
                ui.CompactButton(guide, "First refit skip", new Rect(guideWidth - skipWidth - 6, 4, skipWidth, guideHeight - 8), "Пропустить", () => {
                    if (run.DismissFirstRefitTutorial(out var reason)) ClearFirstRefit();
                    else feedback = ui.Resolve(reason);
                    Refresh();
                });
            if (assign)
                ui.CompactButton(guide, "First refit assign", new Rect(guideWidth - skipWidth - actionWidth - 12, 4, actionWidth, guideHeight - 8),
                    "Назначить в место " + (firstRefitSlot + 1), AssignFirstRefit, WorkshopIcon.None, true);
            if (firstRefitFinished)
                ui.CompactButton(guide, "First refit done", new Rect(guideWidth - actionWidth - 6, 4, actionWidth, guideHeight - 8), "К подготовке", () => {
                    ClearFirstRefit(); Suspend(); prepare?.Invoke();
                }, WorkshopIcon.Next, true);
        }

        void AssignFirstRefit()
        {
            if (!FirstRefitActive || !FirstRefitWasTested || !FirstRefitIsSaved()) return;
            string occupantId = run.Workshop.LoadoutEntries[firstRefitSlot]?.Id;
            if (occupantId != null && occupantId != firstRefitBaseline.Id && occupantId != DraftSession.Draft.Id)
            {
                string name = ui.Resolve(run.Workshop.LoadoutEntries[firstRefitSlot].Name);
                WorkshopConfirm.Show(Root, ui, "Заменить башню в месте " + (firstRefitSlot + 1) + "?",
                    "«" + name + "» будет заменена на «" + DraftSession.Name + "». Оба чертежа останутся в альбоме.",
                    "Назначить", () => CommitFirstRefitAssignment(occupantId));
                return;
            }
            CommitFirstRefitAssignment(occupantId);
        }

        void CommitFirstRefitAssignment(string expectedOccupant)
        {
            if (!FirstRefitActive || !FirstRefitWasTested || !FirstRefitIsSaved()) return;
            if (run.Workshop.LoadoutEntries[firstRefitSlot]?.Id != expectedOccupant)
            { feedback = "Состав места изменился. Проверь его и повтори назначение."; Refresh(); return; }
            if (expectedOccupant != DraftSession.Draft.Id)
            {
                var result = run.AssemblyCommands.AssignSlot(firstRefitSlot, DraftSession.Draft.Id, Guid.NewGuid());
                if (!result.Success) { feedback = Diagnostic(result.Code); Refresh(); return; }
            }
            if (!run.CompleteFirstRefitTutorial(DraftSession.Draft.Id, out var reason))
            { feedback = ui.Resolve(reason); Refresh(); return; }
            firstRefitFinished = true;
            feedback = "«" + DraftSession.Name + "» сохранена и назначена в место " + (firstRefitSlot + 1) + ".";
            Refresh();
        }
    }
}
