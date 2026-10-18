using System;
using System.Collections.Generic;
using System.Linq;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class CompositeWorkshopPanel
    {
        const int AssemblyTrialSeed = 1337;
        bool assemblyTrialOpen, assemblyTrialPaused;
        float assemblyTrialSeconds;
        int assemblyTrialBudget = 400;
        AssemblyTrialScenario assemblyTrialScenario;
        AlbumEntry assemblyTrialSource;
        GameObject assemblyTrialOverlay;
        TrialLaneSketch assemblyTrialLane;
        Text assemblyTrialConditions, assemblyTrialQuote, assemblyTrialMeasured;
        Text assemblyTrialEmpty;
        Button assemblyTrialPause, assemblyTrialRestart;
        public AssemblyTrialReport LastAssemblyTrial { get; private set; }
        string TrialText(string key, params object[] args) => ui.Resolve(UserText.Message("assembly.trial." + key, args));

        static bool IsCleaningTrial(ResolvedEntry entry) => entry != null && entry.Weapons.Count > 0 &&
            entry.Weapons.All(weapon => weapon.Family == AssemblyWeaponFamily.Blotter);

        AssemblyTrialScenario DefaultAssemblyScenario()
        {
            var choices = AssemblyTrialRunner.ScenariosFor(DraftSession.PreviewEntry);
            if (DraftSession.Resolved?.Support?.Kind == AssemblyStructureFamily.Generator) return AssemblyTrialScenario.Generator;
            if (DraftSession.Resolved?.Support?.Kind == AssemblyStructureFamily.Repair) return AssemblyTrialScenario.Repair;
            if (IsCleaningTrial(DraftSession.Resolved) && choices.Contains(AssemblyTrialScenario.Blots)) return AssemblyTrialScenario.Blots;
            return choices.Contains(AssemblyTrialScenario.Single) ? AssemblyTrialScenario.Single : choices[0];
        }

        void ResetAssemblyTrial()
        {
            CloseAssemblyTrial();
            LastAssemblyTrial = null; LastTrial = null; assemblyTrialSource = null;
            assemblyTrialSeconds = 0; assemblyTrialPaused = false;
        }

        void CaptureAssemblyTrialPlayback()
        {
            if (!assemblyTrialOpen || assemblyTrialLane == null) return;
            assemblyTrialSeconds = assemblyTrialLane.PlaybackSeconds;
            assemblyTrialPaused = assemblyTrialLane.PlaybackPaused;
        }

        void OpenTrial()
        {
            if (!DraftSession.CanTrial || Route == AlbumEntryKind.Legacy) return;
            ResetAssemblyTrial();
            var draft = DraftSession.PreviewEntry;
            assemblyTrialSource = new AlbumEntry(draft.Id, DraftSession.Name, draft.Revision, draft.Snapshot, draft.LibraryReferences);
            assemblyTrialScenario = DefaultAssemblyScenario();
            assemblyTrialBudget = 400;
            assemblyTrialOpen = true;
            BuildAssemblyTrialSurface();
        }

        void CloseAssemblyTrial()
        {
            assemblyTrialOpen = false;
            if (assemblyTrialOverlay != null) WorkshopUi.RemoveView(assemblyTrialOverlay);
            assemblyTrialOverlay = null; assemblyTrialLane = null;
        }

        public TrialReport RunTrial()
        {
            if (!DraftSession.CanTrial || Route == AlbumEntryKind.Legacy) return null;
            var source = assemblyTrialOpen ? assemblyTrialSource : new AlbumEntry(DraftSession.Draft.Id,
                DraftSession.Name, DraftSession.Draft.Revision, DraftSession.Draft.Snapshot, DraftSession.Draft.LibraryReferences);
            if (!assemblyTrialOpen) assemblyTrialScenario = DefaultAssemblyScenario();
            LastAssemblyTrial = run.RunAssemblyTrial(source, assemblyTrialScenario, assemblyTrialBudget, AssemblyTrialSeed);
            LastTrial = LastAssemblyTrial?.Recording;
            assemblyTrialSeconds = 0; assemblyTrialPaused = false;
            return LastTrial;
        }

        void BuildAssemblyTrialSurface()
        {
            if (!assemblyTrialOpen || assemblyTrialSource == null) return;
            var shade = ui.Panel(Root, "Composite trial overlay", new Rect(0, 0, width, height), new Color(.08f, .15f, .25f, .45f), true);
            assemblyTrialOverlay = shade.gameObject;
            float w = width - 32, h = height - 24;
            var sheet = ui.Panel(shade, "Composite trial sheet", new Rect(16, 12, w, h), WorkshopUi.Paper, true);
            float rightWidth = Mathf.Clamp(w * .30f, 294, 390), leftWidth = w - rightWidth - 40;
            ui.Button(sheet, "Composite trial header return", new Rect(12, 8, 206, 48), TrialText("return"), CloseAssemblyTrial, WorkshopIcon.Back);
            var title = ui.Label(sheet, "Composite trial title", TrialText("title"), new Rect(230, 6, w - 460, 52), 31, true);
            title.alignment = TextAnchor.MiddleCenter;
            var scenarios = AssemblyTrialRunner.ScenariosFor(assemblyTrialSource).OrderBy(ScenarioOrder).ToArray();
            float tabWidth = (leftWidth - 6 * (scenarios.Length - 1)) / scenarios.Length;
            for (int i = 0; i < scenarios.Length; i++)
            {
                var scenario = scenarios[i];
                var tab = ui.Button(sheet, "Composite trial scenario " + scenario,
                    new Rect(12 + i * (tabWidth + 6), 76, tabWidth, 42), TrialText("scenario." + scenario), () => {
                        assemblyTrialScenario = scenario; InvalidateAssemblyTrial(); RebuildAssemblyTrialSurface();
                    });
                ui.SetSelected(tab, scenario == assemblyTrialScenario);
            }
            ui.Button(sheet, "Composite trial budget", new Rect(12, 130, 194, 48), TrialText("budget", assemblyTrialBudget), () => {
                var choices = run.Session.Balance.Trials.BudgetChoices;
                int index = choices.IndexOf(assemblyTrialBudget);
                assemblyTrialBudget = choices[(index + 1) % choices.Count];
                InvalidateAssemblyTrial(); RebuildAssemblyTrialSurface();
            }, WorkshopIcon.Cost);
            assemblyTrialConditions = ui.Label(sheet, "Composite trial conditions", "", new Rect(220, 124, leftWidth - 208, 62), 17, color: WorkshopUi.Muted);
            var field = ui.Panel(sheet, "Composite trial field", new Rect(12, 194, leftWidth, h - 206), WorkshopUi.Surface, true);
            field.gameObject.AddComponent<RectMask2D>();
            assemblyTrialLane = WorkshopUi.Graphic<TrialLaneSketch>(field, "Composite trial lane", new Rect(0, 0, leftWidth, h - 206));
            assemblyTrialEmpty = ui.Label(field, "Composite trial field hint", TrialText("unstarted"), new Rect(28, (h - 206) * .42f, leftWidth - 56, 96), 25, true, WorkshopUi.Muted);
            assemblyTrialEmpty.alignment = TextAnchor.MiddleCenter;

            float cardHeight = h - 88;
            var card = ui.Panel(sheet, "Composite trial measurements", new Rect(28 + leftWidth, 76, rightWidth, cardHeight), WorkshopUi.Surface, true);
            ui.Label(card, "Composite trial comparison heading", TrialText("comparison"), new Rect(14, 8, rightWidth - 28, 36), 23, true);
            var name = ui.Label(card, "Composite trial entry name", assemblyTrialSource.Name, new Rect(14, 48, rightWidth - 28, 44), 19, true);
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 15; name.resizeTextMaxSize = 19;
            assemblyTrialQuote = ui.Label(card, "Composite trial quote", "", new Rect(14, 98, rightWidth - 28, 84), 18);
            var theory = ui.Label(card, "Composite trial theory", AssemblyTrialTheory(), new Rect(14, 188, rightWidth - 28, 56), 17, color: WorkshopUi.Muted);
            theory.alignment = TextAnchor.UpperLeft;
            assemblyTrialMeasured = ui.Label(card, "Composite trial measured result", "", new Rect(14, 250, rightWidth - 28, cardHeight - 432), 17);
            assemblyTrialMeasured.alignment = TextAnchor.UpperLeft;
            float actionY = cardHeight - 176;
            ui.Button(card, "Composite trial run", new Rect(14, actionY, rightWidth - 28, 46), TrialText("run"), RecordAssemblyTrial, WorkshopIcon.Target, true);
            float half = (rightWidth - 36) / 2;
            assemblyTrialPause = ui.Button(card, "Composite trial pause", new Rect(14, actionY + 54, half, 42), TrialText(assemblyTrialPaused ? "resume" : "pause"), () => {
                if (LastTrial == null) return;
                assemblyTrialLane.PlaybackPaused = !assemblyTrialLane.PlaybackPaused;
                CaptureAssemblyTrialPlayback();
                assemblyTrialPause.GetComponentInChildren<Text>().text = TrialText(assemblyTrialPaused ? "resume" : "pause");
            });
            assemblyTrialRestart = ui.Button(card, "Composite trial restart", new Rect(22 + half, actionY + 54, half, 42), TrialText("restart"), RecordAssemblyTrial);
            ui.Button(card, "Composite close trial", new Rect(14, actionY + 104, rightWidth - 28, 46), TrialText("return"), CloseAssemblyTrial, WorkshopIcon.Back);
            var noRewards = ui.Label(card, "Composite trial no rewards", TrialText("no_rewards"), new Rect(14, cardHeight - 22, rightWidth - 28, 18), 14, color: WorkshopUi.Muted);
            noRewards.alignment = TextAnchor.MiddleCenter;
            RefreshAssemblyTrialReport();
            var reviewButton = Root.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == "Composite trial");
            WorkshopModal.Open(shade.gameObject, CloseAssemblyTrial, reviewButton?.gameObject);
            ui.HideTooltip();
        }

        static int ScenarioOrder(AssemblyTrialScenario scenario)
        {
            switch (scenario)
            {
                case AssemblyTrialScenario.Single: return 0;
                case AssemblyTrialScenario.Crowd: return 1;
                case AssemblyTrialScenario.Line: return 2;
                case AssemblyTrialScenario.Fast: return 3;
                case AssemblyTrialScenario.Armored: return 4;
                default: return 5;
            }
        }

        string AssemblyTrialTheory()
        {
            var resolved = DraftSession.Resolved;
            if (resolved?.Support?.Kind == AssemblyStructureFamily.Generator) return TrialText("generator_local", resolved.Support.MaximumInstances);
            if (resolved?.Support?.Kind == AssemblyStructureFamily.Repair) return TrialText("repair_local", resolved.Support.RepairPerSecond.ToString("0.##"));
            if (resolved?.Support != null) return TrialText("support_theory");
            return IsCleaningTrial(resolved) ? TrialText("blotter_theory") : TrialText("theory", (resolved?.Dps ?? 0).ToString("0.##"));
        }

        void RebuildAssemblyTrialSurface()
        {
            if (assemblyTrialOverlay != null) WorkshopUi.RemoveView(assemblyTrialOverlay);
            assemblyTrialOverlay = null; assemblyTrialLane = null;
            BuildAssemblyTrialSurface();
        }

        void InvalidateAssemblyTrial()
        {
            LastAssemblyTrial = null; LastTrial = null; assemblyTrialSeconds = 0; assemblyTrialPaused = false;
        }

        void RecordAssemblyTrial()
        {
            RunTrial(); RefreshAssemblyTrialReport();
        }

        void RefreshAssemblyTrialReport()
        {
            if (assemblyTrialLane == null) return;
            assemblyTrialConditions.text = TrialText("conditions", TrialText("scenario." + assemblyTrialScenario), assemblyTrialBudget, AssemblyTrialSeed);
            var resolved = DraftSession.Resolved;
            int unitCost = LastAssemblyTrial?.UnitCost ?? resolved?.Cost ?? 0;
            int copies = unitCost > 0 ? assemblyTrialBudget / unitCost : 0;
            if (resolved?.Support?.MaximumInstances > 0) copies = Math.Min(copies, resolved.Support.MaximumInstances);
            copies = LastAssemblyTrial?.Copies ?? copies;
            assemblyTrialQuote.text = TrialText("quote", unitCost, copies, LastAssemblyTrial?.Cost ?? copies * unitCost);
            assemblyTrialMeasured.text = LastAssemblyTrial == null ? TrialText("unstarted") : AssemblyTrialMeasurements();
            assemblyTrialEmpty.gameObject.SetActive(LastAssemblyTrial == null);
            assemblyTrialPause.interactable = assemblyTrialRestart.interactable = LastTrial != null;
            assemblyTrialLane.SetResult(LastTrial, Time.unscaledTime - assemblyTrialSeconds / assemblyTrialLane.PlaybackSpeed, LastTrial?.DurationSeconds ?? 1);
            assemblyTrialLane.PlaybackPaused = assemblyTrialPaused;
            assemblyTrialPause.GetComponentInChildren<Text>().text = TrialText(assemblyTrialPaused ? "resume" : "pause");
        }

        string AssemblyTrialMeasurements()
        {
            var report = LastAssemblyTrial;
            var lines = new List<string>();
            if (report.Damage.HasValue) lines.Add(TrialText("damage", report.Damage.Value.ToString("0.##")));
            if (report.Leaks.HasValue) lines.Add(TrialText("leaks", report.Leaks.Value));
            if (report.UnresolvedEnemies.HasValue) lines.Add(TrialText("unresolved", report.UnresolvedEnemies.Value));
            if (report.RepairedHealth.HasValue) lines.Add(TrialText("repaired", report.RepairedHealth.Value.ToString("0.##")));
            if (report.Income.HasValue) lines.Add(TrialText("income", report.Income.Value));
            if (report.CompletedWaves.HasValue) lines.Add(TrialText("waves", report.CompletedWaves.Value, report.PaidWaves ?? 0));
            if (report.BlotsCleared.HasValue) lines.Add(TrialText("cleared", report.BlotsCleared.Value));
            if (report.UnresolvedBlots.HasValue) lines.Add(TrialText("blots_remaining", report.UnresolvedBlots.Value));
            if (report.PreventedMovement.HasValue) lines.Add(TrialText("control", report.PreventedMovement.Value.ToString("0.##")));
            lines.Add(TrialText("duration", report.DurationSeconds.ToString("0.##")));
            return string.Join("\n", lines);
        }
    }
}
