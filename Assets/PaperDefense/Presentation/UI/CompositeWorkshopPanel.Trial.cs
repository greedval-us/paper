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
        bool assemblyTrialOpen, assemblyTrialPaused, assemblyTrialComparisonPaused, assemblyTrialOverview;
        float assemblyTrialSeconds, assemblyTrialComparisonSeconds;
        int assemblyTrialBudget = 400;
        AssemblyTrialScenario assemblyTrialScenario;
        AlbumEntry assemblyTrialSource, assemblyTrialComparisonSource;
        GameObject assemblyTrialOverlay;
        TrialLaneSketch assemblyTrialLane, assemblyTrialComparisonLane;
        Text assemblyTrialEmpty;
        Button assemblyTrialPause, assemblyTrialRestart, assemblyTrialView;
        public AssemblyTrialReport LastAssemblyTrial { get; private set; }
        public AssemblyTrialReport LastAssemblyTrialComparison { get; private set; }
        string TrialText(string key, params object[] args) => ui.Resolve(UserText.Message("assembly.trial." + key, args));

        static bool IsCleaningTrial(ResolvedEntry entry) => entry != null && entry.Weapons.Count > 0 &&
            entry.Weapons.All(weapon => weapon.Family == AssemblyWeaponFamily.Blotter);

        AssemblyTrialScenario DefaultAssemblyScenario()
        {
            var choices = AssemblyTrialRunner.ScenariosFor(DraftSession.PreviewEntry);
            if (DraftSession.Resolved?.Support?.Kind == AssemblyStructureFamily.Generator) return AssemblyTrialScenario.Generator;
            if (DraftSession.Resolved?.Support?.Kind == AssemblyStructureFamily.Repair) return AssemblyTrialScenario.Repair;
            if (DraftSession.Resolved?.Support?.Kind == AssemblyStructureFamily.Spikes) return AssemblyTrialScenario.Crowd;
            if (DraftSession.Resolved?.Support?.Kind == AssemblyStructureFamily.Beacon) return AssemblyTrialScenario.Armored;
            if (IsCleaningTrial(DraftSession.Resolved) && choices.Contains(AssemblyTrialScenario.Blots)) return AssemblyTrialScenario.Blots;
            return choices.Contains(AssemblyTrialScenario.Single) ? AssemblyTrialScenario.Single : choices[0];
        }

        void ResetAssemblyTrial()
        {
            CloseAssemblyTrial();
            LastAssemblyTrial = null; LastAssemblyTrialComparison = null; LastTrial = null;
            assemblyTrialSource = null; assemblyTrialComparisonSource = null;
            assemblyTrialSeconds = assemblyTrialComparisonSeconds = 0;
            assemblyTrialPaused = assemblyTrialComparisonPaused = false;
            assemblyTrialOverview = false;
        }

        void CaptureAssemblyTrialPlayback()
        {
            if (!assemblyTrialOpen) return;
            if (assemblyTrialLane != null)
            { assemblyTrialSeconds = assemblyTrialLane.PlaybackSeconds; assemblyTrialPaused = assemblyTrialLane.PlaybackPaused; }
            if (assemblyTrialComparisonLane != null)
            { assemblyTrialComparisonSeconds = assemblyTrialComparisonLane.PlaybackSeconds; assemblyTrialComparisonPaused = assemblyTrialComparisonLane.PlaybackPaused; }
        }

        void OpenTrial()
        {
            if (!DraftSession.CanTrial || Route == AlbumEntryKind.Legacy) return;
            ResetAssemblyTrial();
            var draft = DraftSession.PreviewEntry;
            assemblyTrialSource = new AlbumEntry(draft.Id, DraftSession.Name, draft.Revision, draft.Snapshot, draft.LibraryReferences);
            assemblyTrialScenario = DefaultAssemblyScenario();
            assemblyTrialBudget = 400;
            assemblyTrialComparisonSource = ComparisonCandidates().FirstOrDefault();
            if (FirstRefitActive) assemblyTrialComparisonSource = firstRefitBaseline;
            assemblyTrialOpen = true;
            BuildAssemblyTrialSurface();
        }

        void CloseAssemblyTrial()
        {
            assemblyTrialOpen = false;
            if (assemblyTrialOverlay != null) WorkshopUi.RemoveView(assemblyTrialOverlay);
            assemblyTrialOverlay = null; assemblyTrialLane = null; assemblyTrialComparisonLane = null;
        }

        void ReturnFromAssemblyTrial()
        {
            CloseAssemblyTrial();
            if (FirstRefitActive) Refresh();
        }

        public TrialReport RunTrial()
        {
            if (!DraftSession.CanTrial || Route == AlbumEntryKind.Legacy) return null;
            var source = assemblyTrialOpen ? assemblyTrialSource : new AlbumEntry(DraftSession.Draft.Id,
                DraftSession.Name, DraftSession.Draft.Revision, DraftSession.Draft.Snapshot, DraftSession.Draft.LibraryReferences);
            if (!assemblyTrialOpen) assemblyTrialScenario = DefaultAssemblyScenario();
            LastAssemblyTrial = run.RunAssemblyTrial(source, assemblyTrialScenario, assemblyTrialBudget, AssemblyTrialSeed);
            LastAssemblyTrialComparison = assemblyTrialOpen && assemblyTrialComparisonSource != null
                ? run.RunAssemblyTrial(assemblyTrialComparisonSource, assemblyTrialScenario, assemblyTrialBudget, AssemblyTrialSeed) : null;
            LastTrial = LastAssemblyTrial?.Recording;
            RecordFirstRefitTrial();
            assemblyTrialSeconds = assemblyTrialComparisonSeconds = 0;
            assemblyTrialPaused = assemblyTrialComparisonPaused = false;
            return LastTrial;
        }

        AlbumEntry[] ComparisonCandidates()
        {
            if (assemblyTrialSource == null) return Array.Empty<AlbumEntry>();
            bool DifferentRecipe(AlbumEntry entry) => !entry.Snapshot.Recipe.Parts.Select(part => (part.Id, part.Revision))
                .SequenceEqual(assemblyTrialSource.Snapshot.Recipe.Parts.Select(part => (part.Id, part.Revision)));
            bool Eligible(AlbumEntry entry)
            {
                if (entry.Id == assemblyTrialSource.Id || entry.Kind != assemblyTrialSource.Kind || entry.Kind == AlbumEntryKind.Legacy ||
                    !AssemblyTrialRunner.ScenariosFor(entry).Contains(assemblyTrialScenario)) return false;
                var gate = new PaperDefense.Application.WorkshopDraftSession(run, DraftSession.Catalog);
                gate.Begin(entry); return gate.CanTrial;
            }
            return run.Workshop.Entries.Where(Eligible).OrderByDescending(DifferentRecipe).ThenBy(entry => entry.Name).ToArray();
        }

        void CycleComparison()
        {
            var choices = ComparisonCandidates();
            int index = Array.FindIndex(choices, item => item.Id == assemblyTrialComparisonSource?.Id);
            assemblyTrialComparisonSource = index + 1 < choices.Length ? choices[index + 1] : null;
            InvalidateAssemblyTrial(); RebuildAssemblyTrialSurface();
        }

        void BuildAssemblyTrialSurface()
        {
            if (!assemblyTrialOpen || assemblyTrialSource == null) return;
            var shade = ui.Panel(Root, "Composite trial overlay", new Rect(0, 0, width, height), new Color(.08f, .15f, .25f, .45f), true);
            assemblyTrialOverlay = shade.gameObject;
            float w = width - 32, h = height - 24;
            var sheet = ui.Panel(shade, "Composite trial sheet", new Rect(16, 12, w, h), WorkshopUi.Paper, true);
            bool comparing = assemblyTrialComparisonSource != null;
            float rightWidth = Mathf.Clamp(w * .36f, 304, 430), leftWidth = w - rightWidth - 36;
            ui.IconButton(sheet, "Composite trial header return", new Rect(10, 8, 36, 36), WorkshopIcon.Back, TrialText("return"), ReturnFromAssemblyTrial);
            ui.Label(sheet, "Composite trial title", "Полигон", new Rect(56, 3, w - 304, 40), 26, true);
            var budget = ui.CompactButton(sheet, "Composite trial budget", new Rect(w - 204, 8, 148, 36), assemblyTrialBudget + " штр.", () => {
                var choices = run.Session.Balance.Trials.BudgetChoices;
                int index = choices.IndexOf(assemblyTrialBudget); assemblyTrialBudget = choices[(index + 1) % choices.Count];
                InvalidateAssemblyTrial(); RebuildAssemblyTrialSurface();
            }, WorkshopIcon.Pencil);
            ui.Tip(budget.gameObject, "Бюджет испытания: " + assemblyTrialBudget + " штрихов. Нажми, чтобы изменить.");
            ui.IconButton(sheet, "Composite trial help", new Rect(w - 48, 8, 36, 36), WorkshopIcon.Help, "Условия и пояснения", () =>
                UiHelp.Show(Root, "Условия полигона", AssemblyTrialHelp()));
            var scenarios = AssemblyTrialRunner.ScenariosFor(assemblyTrialSource).OrderBy(ScenarioOrder).ToArray();
            float tabWidth = (w - 24 - 6 * (scenarios.Length - 1)) / scenarios.Length;
            for (int i = 0; i < scenarios.Length; i++)
            {
                var scenario = scenarios[i];
                var tab = ui.CompactButton(sheet, "Composite trial scenario " + scenario,
                    new Rect(12 + i * (tabWidth + 6), 56, tabWidth, 36), TrialText("scenario." + scenario), () => {
                        assemblyTrialScenario = scenario;
                        if (assemblyTrialComparisonSource != null && !ComparisonCandidates().Any(entry => entry.Id == assemblyTrialComparisonSource.Id))
                            assemblyTrialComparisonSource = ComparisonCandidates().FirstOrDefault();
                        InvalidateAssemblyTrial(); RebuildAssemblyTrialSurface();
                    });
                ui.SetSelected(tab, scenario == assemblyTrialScenario); ui.Tip(tab.gameObject, TrialText("scenario." + scenario));
            }
            float fieldHeight = h - 112;
            var field = ui.Panel(sheet, "Composite trial field", new Rect(12, 100, leftWidth, fieldHeight), WorkshopUi.Surface, true);
            field.gameObject.AddComponent<RectMask2D>(); TrialPerimeter(field, leftWidth, fieldHeight);
            float laneHeight = comparing ? (fieldHeight - 8) * .5f : fieldHeight;
            BuildAssemblyLaneName(field, "Composite trial first lane name", "A", assemblyTrialSource, new Rect(10, 0, leftWidth - 20, 36), WorkshopUi.Blue);
            var first = ui.Panel(field, "Composite trial first viewport", new Rect(0, 36, leftWidth, laneHeight - 36), Color.clear);
            first.gameObject.AddComponent<RectMask2D>();
            assemblyTrialLane = WorkshopUi.Graphic<TrialLaneSketch>(first, "Composite trial lane", new Rect(0, 0, leftWidth, laneHeight - 36));
            ConfigureAssemblyLane(assemblyTrialLane);
            if (comparing)
            {
                BuildAssemblyLaneName(field, "Composite trial second lane name", "B", assemblyTrialComparisonSource, new Rect(10, laneHeight + 8, leftWidth - 20, 36), WorkshopUi.Ink);
                var second = ui.Panel(field, "Composite trial second viewport", new Rect(0, laneHeight + 44, leftWidth, laneHeight - 36), Color.clear);
                second.gameObject.AddComponent<RectMask2D>();
                assemblyTrialComparisonLane = WorkshopUi.Graphic<TrialLaneSketch>(second, "Composite trial comparison lane", new Rect(0, 0, leftWidth, laneHeight - 36));
                ConfigureAssemblyLane(assemblyTrialComparisonLane);
            }
            float hintHeight = Mathf.Min(72, Mathf.Max(28, first.rect.height - 16));
            assemblyTrialEmpty = ui.Label(first, "Composite trial field hint", TrialText("unstarted"), new Rect(12, (first.rect.height - hintHeight) * .5f, leftWidth - 24, hintHeight), 18, true, FieldMenuSkin.Ink);
            assemblyTrialEmpty.alignment = TextAnchor.MiddleCenter;
            assemblyTrialEmpty.resizeTextForBestFit = true; assemblyTrialEmpty.resizeTextMinSize = 16; assemblyTrialEmpty.resizeTextMaxSize = 18;

            var card = ui.Panel(sheet, "Composite trial measurements", new Rect(leftWidth + 24, 100, rightWidth, fieldHeight), WorkshopUi.Surface, true);
            BuildCompactTrialResults(card, new Rect(10, 8, rightWidth - 20, fieldHeight - 114));
            float selectorY = fieldHeight - 100, controlsY = fieldHeight - 52;
            var compare = ui.CompactButton(card, "Composite trial compare", new Rect(10, selectorY, rightWidth - 20, 40),
                "B · " + (assemblyTrialComparisonSource == null ? "Без сравнения" : ui.Resolve(assemblyTrialComparisonSource.Name)), CycleComparison, WorkshopIcon.Compare);
            ui.Tip(compare.gameObject, "Сборка B для сравнения\n" + (assemblyTrialComparisonSource == null ? "Не выбрана" : ui.Resolve(assemblyTrialComparisonSource.Name)) + "\nНажми, чтобы выбрать следующую сохранённую сборку. Условия A и B одинаковы.");
            float runWidth = rightWidth - 146;
            ui.CompactButton(card, "Composite trial run", new Rect(10, controlsY, runWidth, 40), TrialText("run"), RecordAssemblyTrial, WorkshopIcon.Target, true);
            assemblyTrialPause = ui.IconButton(card, "Composite trial pause", new Rect(16 + runWidth, controlsY, 36, 40), assemblyTrialPaused ? WorkshopIcon.Play : WorkshopIcon.Pause,
                TrialText(assemblyTrialPaused ? "resume" : "pause"), ToggleAssemblyTrialPause);
            assemblyTrialRestart = ui.IconButton(card, "Composite trial restart", new Rect(58 + runWidth, controlsY, 36, 40), WorkshopIcon.Restart, TrialText("restart"), RecordAssemblyTrial);
            assemblyTrialView = ui.IconButton(card, "Composite trial overview", new Rect(100 + runWidth, controlsY, 36, 40), assemblyTrialOverview ? WorkshopIcon.Target : WorkshopIcon.Fit,
                TrialText(assemblyTrialOverview ? "combat_focus" : "overview"), () => {
                    CaptureAssemblyTrialPlayback(); assemblyTrialOverview = !assemblyTrialOverview; RebuildAssemblyTrialSurface();
                });
            RefreshAssemblyTrialReport();
            var review = Root.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == "Composite trial");
            WorkshopModal.Open(shade.gameObject, ReturnFromAssemblyTrial, review?.gameObject); ui.HideTooltip();
        }

        void ConfigureAssemblyLane(TrialLaneSketch lane)
        { lane.FrameRecording = true; lane.QuietGrid = true; lane.CombatFocus = !assemblyTrialOverview; }

        void BuildAssemblyLaneName(Transform parent, string name, string marker, AlbumEntry entry, Rect bounds, Color color)
        {
            string full = marker + " · " + ui.Resolve(entry.Name);
            var label = ui.Label(parent, name, "", bounds, 16, true, color); SequenceFitLabel(label, full); ui.Tip(label.gameObject, full);
        }

        void ToggleAssemblyTrialPause()
        {
            if (LastTrial == null) return;
            bool paused = !assemblyTrialLane.PlaybackPaused; assemblyTrialLane.PlaybackPaused = paused;
            if (assemblyTrialComparisonLane != null) assemblyTrialComparisonLane.PlaybackPaused = paused;
            CaptureAssemblyTrialPlayback(); RefreshAssemblyTrialPauseControl();
        }

        void RefreshAssemblyTrialPauseControl()
        {
            if (assemblyTrialPause == null) return;
            assemblyTrialPause.GetComponentInChildren<WorkshopIconGraphic>().Icon = assemblyTrialPaused ? WorkshopIcon.Play : WorkshopIcon.Pause;
            ui.Tip(assemblyTrialPause.gameObject, TrialText(assemblyTrialPaused ? "resume" : "pause"));
        }

        string AssemblyTrialHelp() => TrialText("conditions", TrialText("scenario." + assemblyTrialScenario), assemblyTrialBudget, AssemblyTrialSeed) +
            "\n\n" + TrialText("no_rewards") + ". Испытание не меняет профиль, чертёж или боевую попытку.\n\nA: " + AssemblyTrialTheory() +
            "\n\nA — выбранная сборка, B — сохранённая сборка для сравнения. Измерения получены в одинаковом сценарии с одинаковым бюджетом и seed. Теория описывает расчётные параметры, а не гарантированный результат боя.";

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
            var preview = assemblyTrialSource == null ? null : run.PreviewAlbumEntry(assemblyTrialSource);
            var resolved = preview?.Valid == true ? preview.Value : null;
            if (resolved == null) return "Расчёт недоступен для выбранного снимка сборки.";
            if (resolved.Support?.Kind == AssemblyStructureFamily.Generator) return TrialText("generator_local", resolved.Support.MaximumInstances);
            if (resolved.Support?.Kind == AssemblyStructureFamily.Repair) return TrialText("repair_local", resolved.Support.RepairPerSecond.ToString("0.##"));
            if (GameText.TrapOrBeaconEffect(resolved.Support) is string fieldEffect) return ui.Resolve(fieldEffect);
            if (resolved.Support != null) return TrialText("support_theory");
            return IsCleaningTrial(resolved) ? TrialText("blotter_theory") : TrialText("theory", resolved.Dps.ToString("0.##"));
        }

        void RebuildAssemblyTrialSurface()
        {
            if (assemblyTrialOverlay != null) WorkshopUi.RemoveView(assemblyTrialOverlay);
            assemblyTrialOverlay = null; assemblyTrialLane = null; assemblyTrialComparisonLane = null;
            BuildAssemblyTrialSurface();
        }

        void InvalidateAssemblyTrial()
        {
            LastAssemblyTrial = null; LastAssemblyTrialComparison = null; LastTrial = null;
            assemblyTrialSeconds = assemblyTrialComparisonSeconds = 0; assemblyTrialPaused = assemblyTrialComparisonPaused = false;
        }

        void RecordAssemblyTrial()
        {
            RunTrial(); RefreshAssemblyTrialReport();
        }

        void RefreshAssemblyTrialReport()
        {
            if (assemblyTrialLane == null) return;
            if (assemblyTrialComparisonLane != null)
            {
                var recording = LastAssemblyTrialComparison?.Recording;
                assemblyTrialComparisonLane.SetResult(recording, Time.unscaledTime - assemblyTrialComparisonSeconds / assemblyTrialComparisonLane.PlaybackSpeed, recording?.DurationSeconds ?? 1);
                assemblyTrialComparisonLane.PlaybackPaused = assemblyTrialComparisonPaused;
            }
            assemblyTrialEmpty.gameObject.SetActive(LastAssemblyTrial == null);
            assemblyTrialPause.interactable = assemblyTrialRestart.interactable = LastTrial != null;
            assemblyTrialView.interactable = LastTrial != null;
            assemblyTrialLane.SetResult(LastTrial, Time.unscaledTime - assemblyTrialSeconds / assemblyTrialLane.PlaybackSpeed, LastTrial?.DurationSeconds ?? 1);
            assemblyTrialLane.PlaybackPaused = assemblyTrialPaused;
            RefreshAssemblyTrialPauseControl();
            RefreshCompactTrialResults();
            Root.GetComponentInParent<FieldMenuTheme>(true)?.Refresh();
        }

        void TrialPerimeter(Transform parent, float w, float h)
        {
            float tree = Mathf.Clamp(h * .12f, 42, 92), rock = Mathf.Clamp(h * .065f, 30, 48);
            Art("left pine", "terrain.pine", new Rect(3, 32, tree * .62f, tree));
            Art("right pine", "terrain.pine", new Rect(w - tree * .62f - 4, 30, tree * .62f, tree));
            Art("lower left pine", "terrain.pine", new Rect(8, h - tree - 5, tree * .62f, tree));
            Art("lower right pine", "terrain.pine", new Rect(w - tree * .62f - 9, h - tree - 4, tree * .62f, tree));
            Art("left rocks", "terrain.rock", new Rect(tree * .55f, h - rock - 4, rock * 1.4f, rock));
            Art("right rocks", "terrain.rock", new Rect(w - tree * .55f - rock * 1.4f, h - rock - 4, rock * 1.4f, rock));
            Art("left shrub", "terrain.shrub", new Rect(tree * .65f, 34, 34, 30));
            Art("right shrub", "terrain.shrub", new Rect(w - tree * .65f - 34, 34, 34, 30));
            void Art(string name, string id, Rect bounds)
            {
                var image = WorkshopUi.Graphic<NotebookArtGraphic>(parent, "Composite trial perimeter " + name, bounds);
                image.Bind(id); image.color = new Color(1, 1, 1, .62f);
            }
        }

    }
}
