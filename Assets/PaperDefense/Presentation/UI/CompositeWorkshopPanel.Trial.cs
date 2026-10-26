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
        bool assemblyTrialOpen, assemblyTrialPaused, assemblyTrialOverview;
        float assemblyTrialSeconds;
        int assemblyTrialBudget = 400;
        AssemblyTrialScenario assemblyTrialScenario;
        AlbumEntry assemblyTrialSource, assemblyTrialComparisonSource;
        GameObject assemblyTrialOverlay;
        TrialLaneSketch assemblyTrialLane, assemblyTrialComparisonLane;
        Text assemblyTrialConditions;
        RectTransform assemblyTrialQuoteRows, assemblyTrialMeasuredRows;
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
            if (IsCleaningTrial(DraftSession.Resolved) && choices.Contains(AssemblyTrialScenario.Blots)) return AssemblyTrialScenario.Blots;
            return choices.Contains(AssemblyTrialScenario.Single) ? AssemblyTrialScenario.Single : choices[0];
        }

        void ResetAssemblyTrial()
        {
            CloseAssemblyTrial();
            LastAssemblyTrial = null; LastAssemblyTrialComparison = null; LastTrial = null;
            assemblyTrialSource = null; assemblyTrialComparisonSource = null;
            assemblyTrialSeconds = 0; assemblyTrialPaused = false;
            assemblyTrialOverview = false;
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
            assemblyTrialComparisonSource = ComparisonCandidates().FirstOrDefault();
            assemblyTrialOpen = true;
            BuildAssemblyTrialSurface();
        }

        void CloseAssemblyTrial()
        {
            assemblyTrialOpen = false;
            if (assemblyTrialOverlay != null) WorkshopUi.RemoveView(assemblyTrialOverlay);
            assemblyTrialOverlay = null; assemblyTrialLane = null; assemblyTrialComparisonLane = null;
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
            assemblyTrialSeconds = 0; assemblyTrialPaused = false;
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
            float rightWidth = Mathf.Clamp(w * .30f, comparing ? 346 : 294, 410), leftWidth = w - rightWidth - 40;
            ui.Button(sheet, "Composite trial header return", new Rect(12, 8, 206, 48), TrialText("return"), CloseAssemblyTrial, WorkshopIcon.Back);
            var title = ui.Label(sheet, "Composite trial title", TrialText("title"), new Rect(230, 6, w - 460, 52), 31, true);
            title.alignment = TextAnchor.MiddleCenter;
            var scenarios = AssemblyTrialRunner.ScenariosFor(assemblyTrialSource).OrderBy(ScenarioOrder).ToArray();
            float tabWidth = (leftWidth - 6 * (scenarios.Length - 1)) / scenarios.Length;
            for (int i = 0; i < scenarios.Length; i++)
            {
                var scenario = scenarios[i];
                var tab = ui.Button(sheet, "Composite trial scenario " + scenario,
                    new Rect(12 + i * (tabWidth + 6), 76, tabWidth, 36), TrialText("scenario." + scenario), () => {
                        assemblyTrialScenario = scenario;
                        if (assemblyTrialComparisonSource != null && !ComparisonCandidates().Any(entry => entry.Id == assemblyTrialComparisonSource.Id))
                            assemblyTrialComparisonSource = ComparisonCandidates().FirstOrDefault();
                        InvalidateAssemblyTrial(); RebuildAssemblyTrialSurface();
                    });
                ui.SetSelected(tab, scenario == assemblyTrialScenario);
            }
            ui.Button(sheet, "Composite trial budget", new Rect(w - 206, 8, 194, 48), TrialText("budget", assemblyTrialBudget), () => {
                var choices = run.Session.Balance.Trials.BudgetChoices;
                int index = choices.IndexOf(assemblyTrialBudget);
                assemblyTrialBudget = choices[(index + 1) % choices.Count];
                InvalidateAssemblyTrial(); RebuildAssemblyTrialSurface();
            }, WorkshopIcon.Cost);
            assemblyTrialConditions = ui.Label(sheet, "Composite trial conditions", "", new Rect(232, 54, w - 464, 20), 14, color: WorkshopUi.Muted);
            assemblyTrialConditions.alignment = TextAnchor.MiddleCenter;
            float fieldHeight = h - 130;
            var field = ui.Panel(sheet, "Composite trial field", new Rect(12, 118, leftWidth, fieldHeight), WorkshopUi.Surface, true);
            field.gameObject.AddComponent<RectMask2D>();
            TrialPerimeter(field, leftWidth, fieldHeight);
            float laneHeight = comparing ? (fieldHeight - 10) * .5f : fieldHeight;
            var firstViewport = ui.Panel(field, "Composite trial first viewport", new Rect(0, 26, leftWidth, laneHeight - 26), Color.clear);
            firstViewport.gameObject.AddComponent<RectMask2D>();
            assemblyTrialLane = WorkshopUi.Graphic<TrialLaneSketch>(firstViewport, "Composite trial lane", new Rect(0, 0, leftWidth, laneHeight - 26));
            assemblyTrialLane.FrameRecording = true;
            assemblyTrialLane.QuietGrid = true;
            assemblyTrialLane.CombatFocus = !assemblyTrialOverview;
            if (comparing)
            {
                ui.Label(field, "Composite trial first lane name", assemblyTrialSource.Name, new Rect(14, 0, leftWidth - 218, 26), 18, true);
                ui.Label(field, "Composite trial second lane name", assemblyTrialComparisonSource.Name, new Rect(14, laneHeight + 10, leftWidth - 28, 26), 18, true);
                var secondViewport = ui.Panel(field, "Composite trial second viewport", new Rect(0, laneHeight + 36, leftWidth, laneHeight - 26), Color.clear);
                secondViewport.gameObject.AddComponent<RectMask2D>();
                assemblyTrialComparisonLane = WorkshopUi.Graphic<TrialLaneSketch>(secondViewport, "Composite trial comparison lane", new Rect(0, 0, leftWidth, laneHeight - 26));
                assemblyTrialComparisonLane.FrameRecording = true;
                assemblyTrialComparisonLane.QuietGrid = true;
                assemblyTrialComparisonLane.CombatFocus = !assemblyTrialOverview;
            }
            assemblyTrialView = ui.Button(field, "Composite trial overview", new Rect(leftWidth - 202, 0, 190, 26),
                TrialText(assemblyTrialOverview ? "combat_focus" : "overview"), () => {
                    CaptureAssemblyTrialPlayback(); assemblyTrialOverview = !assemblyTrialOverview; RebuildAssemblyTrialSurface();
                }, WorkshopIcon.Target);
            assemblyTrialEmpty = ui.Label(field, "Composite trial field hint", TrialText("unstarted"), new Rect(28, fieldHeight * .42f, leftWidth - 56, 96), 25, true, WorkshopUi.Muted);
            assemblyTrialEmpty.alignment = TextAnchor.MiddleCenter;

            float cardHeight = h - 88;
            var card = ui.Panel(sheet, "Composite trial measurements", new Rect(28 + leftWidth, 76, rightWidth, cardHeight), WorkshopUi.Surface, true);
            ui.Label(card, "Composite trial comparison heading", TrialText(comparing ? "comparison_heading" : "comparison"), new Rect(14, 8, rightWidth - 28, 36), 23, true);
            float tableWidth = rightWidth - 28, labelWidth = tableWidth * .45f;
            float columnWidth = (tableWidth - labelWidth - 8) / (comparing ? 2 : 1), firstX = 14 + labelWidth + 8;
            var name = ui.Label(card, "Composite trial entry name", assemblyTrialSource.Name, new Rect(firstX, 48, columnWidth, 44), 18, true);
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 15; name.resizeTextMaxSize = 19;
            name.alignment = TextAnchor.MiddleCenter;
            assemblyTrialQuoteRows = ui.Panel(card, "Composite trial quote table", new Rect(14, 98, tableWidth, 84), Color.clear);
            if (comparing)
            {
                var otherName = ui.Label(card, "Composite trial comparison entry name", assemblyTrialComparisonSource.Name, new Rect(firstX + columnWidth, 48, columnWidth, 44), 18, true);
                otherName.resizeTextForBestFit = true; otherName.resizeTextMinSize = 15; otherName.resizeTextMaxSize = 19;
                otherName.alignment = TextAnchor.MiddleCenter;
            }
            var theory = ui.Label(card, "Composite trial theory", AssemblyTrialTheory(), new Rect(14, 188, rightWidth - 28, 56), 17, color: WorkshopUi.Muted);
            theory.alignment = TextAnchor.UpperLeft;
            assemblyTrialMeasuredRows = ui.Panel(card, "Composite trial measured table", new Rect(14, 250, tableWidth, cardHeight - 486), Color.clear);
            float actionY = cardHeight - 176;
            ui.Button(card, "Composite trial compare", new Rect(14, actionY - 54, rightWidth - 28, 46),
                TrialText("compare_with", assemblyTrialComparisonSource?.Name ?? TrialText("compare_none")), CycleComparison, WorkshopIcon.Book);
            ui.Button(card, "Composite trial run", new Rect(14, actionY, rightWidth - 28, 46), TrialText("run"), RecordAssemblyTrial, WorkshopIcon.Target, true);
            float half = (rightWidth - 36) / 2;
            assemblyTrialPause = ui.Button(card, "Composite trial pause", new Rect(14, actionY + 54, half, 42), TrialText(assemblyTrialPaused ? "resume" : "pause"), () => {
                if (LastTrial == null) return;
                assemblyTrialLane.PlaybackPaused = !assemblyTrialLane.PlaybackPaused;
                if (assemblyTrialComparisonLane != null) assemblyTrialComparisonLane.PlaybackPaused = assemblyTrialLane.PlaybackPaused;
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
            assemblyTrialOverlay = null; assemblyTrialLane = null; assemblyTrialComparisonLane = null;
            BuildAssemblyTrialSurface();
        }

        void InvalidateAssemblyTrial()
        {
            LastAssemblyTrial = null; LastAssemblyTrialComparison = null; LastTrial = null; assemblyTrialSeconds = 0; assemblyTrialPaused = false;
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
            string firstQuote = TrialText("quote", unitCost, copies, LastAssemblyTrial?.Cost ?? copies * unitCost), comparisonQuote = null;
            if (assemblyTrialComparisonLane != null)
            {
                var second = LastAssemblyTrialComparison;
                if (second != null) comparisonQuote = TrialText("quote", second.UnitCost, second.Copies, second.Cost);
                else
                {
                    var gate = new PaperDefense.Application.WorkshopDraftSession(run, DraftSession.Catalog); gate.Begin(assemblyTrialComparisonSource);
                    int cost = gate.Resolved?.Cost ?? 0, number = cost > 0 ? assemblyTrialBudget / cost : 0;
                    if (gate.Resolved?.Support?.MaximumInstances > 0) number = Math.Min(number, gate.Resolved.Support.MaximumInstances);
                    comparisonQuote = TrialText("quote", cost, number, number * cost);
                }
                assemblyTrialComparisonLane.SetResult(second?.Recording, Time.unscaledTime - assemblyTrialSeconds / assemblyTrialComparisonLane.PlaybackSpeed, second?.Recording?.DurationSeconds ?? 1);
                assemblyTrialComparisonLane.PlaybackPaused = assemblyTrialPaused;
            }
            assemblyTrialEmpty.gameObject.SetActive(LastAssemblyTrial == null);
            assemblyTrialPause.interactable = assemblyTrialRestart.interactable = LastTrial != null;
            assemblyTrialView.interactable = LastTrial != null;
            assemblyTrialLane.SetResult(LastTrial, Time.unscaledTime - assemblyTrialSeconds / assemblyTrialLane.PlaybackSpeed, LastTrial?.DurationSeconds ?? 1);
            assemblyTrialLane.PlaybackPaused = assemblyTrialPaused;
            assemblyTrialPause.GetComponentInChildren<Text>().text = TrialText(assemblyTrialPaused ? "resume" : "pause");
            TrialMetricTable(assemblyTrialQuoteRows, "Composite trial quote", firstQuote, comparisonQuote);
            TrialMetricTable(assemblyTrialMeasuredRows, "Composite trial measured", LastAssemblyTrial == null ? null : AssemblyTrialMeasurements(LastAssemblyTrial),
                LastAssemblyTrialComparison == null ? null : AssemblyTrialMeasurements(LastAssemblyTrialComparison));
        }

        void TrialMetricTable(RectTransform table, string prefix, string first, string comparison)
        {
            WorkshopUi.ClearViews(table);
            if (first == null)
            {
                var hint = ui.Label(table, prefix + " hint", TrialText("unstarted"), new Rect(0, 0, table.rect.width, table.rect.height), 18, color: WorkshopUi.Muted);
                hint.alignment = TextAnchor.UpperLeft;
                return;
            }
            var rows = first.Split('\n');
            var otherRows = comparison?.Split('\n') ?? Array.Empty<string>();
            string Title(string row) { int colon = row.IndexOf(':'); return colon < 0 ? row : row.Substring(0, colon); }
            string Value(string row) { int colon = row.IndexOf(':'); return colon < 0 ? "" : row.Substring(colon + 1).Trim(); }
            var firstValues = rows.ToDictionary(Title, Value);
            var otherValues = otherRows.ToDictionary(Title, Value);
            var titles = rows.Concat(otherRows).Select(Title).Distinct().ToArray();
            bool comparing = assemblyTrialComparisonSource != null;
            float w = table.rect.width, labelWidth = w * .45f, columnWidth = (w - labelWidth - 8) / (comparing ? 2 : 1);
            float pitch = Mathf.Min(30, table.rect.height / Mathf.Max(1, titles.Length));
            int size = Mathf.Clamp(Mathf.FloorToInt(pitch * .8f), 15, 20);
            for (int column = 0; column < (comparing ? 2 : 1); column++)
            {
                float x = column == 0 ? labelWidth + 4 : labelWidth + 8 + columnWidth;
                var divider = ReferenceDetail(table, prefix + " column " + column + " rule", new Rect(x, 0, 1, titles.Length * pitch), FoundationDetailGraphic.Kind.Divider);
                divider.color = new Color(WorkshopUi.Ink.r, WorkshopUi.Ink.g, WorkshopUi.Ink.b, .18f);
            }
            for (int i = 0; i < titles.Length; i++)
            {
                string title = titles[i];
                string value = firstValues.TryGetValue(title, out var actual) ? actual : "—";
                var rule = ReferenceDetail(table, prefix + " row " + i + " rule", new Rect(0, (i + 1) * pitch - 1, w, 1), FoundationDetailGraphic.Kind.Divider);
                rule.color = new Color(WorkshopUi.Ink.r, WorkshopUi.Ink.g, WorkshopUi.Ink.b, .18f);
                var label = ui.Label(table, prefix + " row " + i + " label", title, new Rect(0, i * pitch, labelWidth, pitch - 1), size);
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 14; label.resizeTextMaxSize = size;
                var number = ui.Label(table, prefix + " row " + i + " value", value, new Rect(labelWidth + 8, i * pitch, columnWidth - 3, pitch - 1), size, color: WorkshopUi.Blue);
                number.alignment = TextAnchor.MiddleCenter;
                number.resizeTextForBestFit = true; number.resizeTextMinSize = 14; number.resizeTextMaxSize = size;
                if (comparing)
                {
                    var otherNumber = ui.Label(table, prefix + " comparison row " + i + " value", otherValues.TryGetValue(title, out var other) ? other : "—",
                        new Rect(labelWidth + 8 + columnWidth, i * pitch, columnWidth - 3, pitch - 1), size);
                    otherNumber.alignment = TextAnchor.MiddleCenter;
                    otherNumber.resizeTextForBestFit = true; otherNumber.resizeTextMinSize = 14; otherNumber.resizeTextMaxSize = size;
                }
            }
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

        string AssemblyTrialMeasurements(AssemblyTrialReport report)
        {
            var lines = new List<string>();
            if (report.Damage.HasValue) lines.Add(TrialText("damage", report.Damage.Value.ToString("0.##")));
            if (report.Leaks.HasValue) lines.Add(TrialText("leaks", report.Leaks.Value));
            if (report.UnresolvedEnemies.HasValue) lines.Add(TrialText("unresolved", report.UnresolvedEnemies.Value));
            if (report.RepairedHealth.HasValue) lines.Add(TrialText("repaired", report.RepairedHealth.Value.ToString("0.##")));
            if (report.Income.HasValue) lines.Add(TrialText("income", report.Income.Value));
            if (report.CompletedWaves.HasValue)
                lines.AddRange(TrialText("waves", report.CompletedWaves.Value, report.PaidWaves ?? 0).Split('·').Select(line => line.Trim()));
            if (report.BlotsCleared.HasValue) lines.Add(TrialText("cleared", report.BlotsCleared.Value));
            if (report.UnresolvedBlots.HasValue) lines.Add(TrialText("blots_remaining", report.UnresolvedBlots.Value));
            if (report.PreventedMovement.HasValue) lines.Add(TrialText("control", report.PreventedMovement.Value.ToString("0.##")));
            lines.Add(TrialText("duration", report.DurationSeconds.ToString("0.##")));
            return string.Join("\n", lines);
        }
    }
}
