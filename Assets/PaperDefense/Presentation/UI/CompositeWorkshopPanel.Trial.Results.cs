using System;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class CompositeWorkshopPanel
    {
        const float CompactTrialRowHeight = 28;
        RectTransform compactTrialResults;
        ScrollRect compactTrialResultsScroll;

        sealed class CompactTrialMetric
        {
            public string Id, Caption, Hint;
            public double? First, Second;
            public bool? MoreIsBetter;
        }

        readonly struct CompactTrialQuote
        {
            public readonly double? UnitCost, Copies, Cost;
            public CompactTrialQuote(double? unitCost, double? copies, double? cost)
            { UnitCost = unitCost; Copies = copies; Cost = cost; }
        }

        void BuildCompactTrialResults(Transform parent, Rect bounds)
        {
            compactTrialResults = ui.Panel(parent, "Composite compact trial results", bounds, Color.clear);
            compactTrialResultsScroll = null;
            RefreshCompactTrialResults();
        }

        void RefreshCompactTrialResults()
        {
            if (compactTrialResults == null) return;
            float previousPosition = compactTrialResultsScroll != null &&
                compactTrialResultsScroll.content.rect.height > compactTrialResultsScroll.viewport.rect.height + 1
                ? compactTrialResultsScroll.verticalNormalizedPosition : 1;
            WorkshopUi.ClearViews(compactTrialResults);
            compactTrialResultsScroll = null;

            var first = LastAssemblyTrial;
            bool comparing = assemblyTrialComparisonSource != null;
            var second = comparing ? LastAssemblyTrialComparison : null;
            bool hasMeasurement = first != null || second != null;
            bool mixedState = comparing && ((first != null) != (second != null));
            float w = compactTrialResults.rect.width, h = compactTrialResults.rect.height;
            var heading = ui.Label(compactTrialResults, "Composite trial comparison heading",
                hasMeasurement ? "Результат" : "Расчёт", new Rect(0, 0, comparing ? w * .48f : w, 22), 16, true);
            WorkshopUi.Place(heading.rectTransform, new Rect(0, 0, comparing ? w * .48f : w, 22));
            ui.Tip(heading.gameObject, hasMeasurement ? "Итоги независимых прогонов. Значения относятся ко всему прогону." :
                "Цена и доступные копии рассчитаны по сохранённым параметрам. Измерения появятся после запуска.");
            if (comparing)
            {
                var legend = ui.Label(compactTrialResults, "Composite trial delta legend", "Δ = B − A",
                    new Rect(w * .48f, 0, w * .52f, 22), 14, color: WorkshopUi.Muted);
                legend.alignment = TextAnchor.MiddleRight;
                ui.Tip(legend.gameObject, "Разница: результат B минус результат A. Прочерк — измерение отсутствует или условия различаются.");
            }
            CompactTrialIdentity("Composite trial entry name", "A", first?.EntryName ?? assemblyTrialSource?.Name,
                24, w, first != null, mixedState);
            if (comparing)
                CompactTrialIdentity("Composite trial comparison entry name", "B", second?.EntryName ?? assemblyTrialComparisonSource.Name,
                    46, w, second != null, mixedState);

            float columnY = comparing ? 70 : 48;
            float scrollY = columnY + 24;
            float scrollHeight = Mathf.Max(28, h - scrollY);
            float tableWidth = w - 10;
            CompactTrialColumnHeader(compactTrialResults, columnY, tableWidth, comparing);
            var firstQuote = first != null || !hasMeasurement ? CompactQuote(assemblyTrialSource, first) : default;
            var secondQuote = comparing && (second != null || !hasMeasurement) ? CompactQuote(assemblyTrialComparisonSource, second) : default;
            var quotes = new List<CompactTrialMetric> {
                new CompactTrialMetric { Id = "UnitCost", Caption = "Цена копии", Hint = "Цена одной копии, штрихи.", First = firstQuote.UnitCost, Second = secondQuote.UnitCost },
                new CompactTrialMetric { Id = "Copies", Caption = "Копий", Hint = hasMeasurement ? "Фактическое число построенных копий." :
                    "Копии по бюджету и лимиту сооружения. Фактическое размещение фиксируется после запуска.", First = firstQuote.Copies, Second = secondQuote.Copies },
                new CompactTrialMetric { Id = "Cost", Caption = "Потрачено", Hint = hasMeasurement ? "Фактическая суммарная стоимость копий, штрихи." :
                    "Расчётная суммарная стоимость копий, штрихи.", First = firstQuote.Cost, Second = secondQuote.Cost }
            };
            var measurements = CompactMeasuredMetrics(first, second);
            float quoteHeight = quotes.Count * CompactTrialRowHeight;
            float sectionHeight = hasMeasurement ? 0 : 24;
            float measuredHeight = measurements.Count == 0 ? 48 : measurements.Count * CompactTrialRowHeight;
            float contentHeight = quoteHeight + sectionHeight + measuredHeight + 4;
            var list = new CatalogScrollList(compactTrialResults, "Composite trial report scroll",
                new Rect(0, scrollY, w, scrollHeight), 1, 1, contentHeight, 0);
            compactTrialResultsScroll = list.Scroll;
            compactTrialResultsScroll.scrollSensitivity = CompactTrialRowHeight * 2;
            compactTrialResultsScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            var quoteTable = ui.Panel(list.Content, "Composite trial quote table", new Rect(0, 0, tableWidth, quoteHeight), Color.clear);
            bool comparableReports = CompactReportsComparable(first, second);
            bool comparableQuotes = comparing && !mixedState && (!hasMeasurement || comparableReports);
            CompactTrialTable(quoteTable, "Composite trial quote", quotes, comparing, comparableQuotes);
            if (!hasMeasurement)
                ui.Label(list.Content, "Composite trial measured heading", "После запуска",
                    new Rect(0, quoteHeight + 2, tableWidth, sectionHeight - 2), 14, color: WorkshopUi.Muted);
            var measuredTable = ui.Panel(list.Content, "Composite trial measured table",
                new Rect(0, quoteHeight + sectionHeight, tableWidth, measuredHeight), Color.clear);
            if (measurements.Count == 0)
            {
                var hint = ui.Label(measuredTable, "Composite trial measured hint", "Нажми «Испытать», чтобы получить измерения.",
                    new Rect(0, 2, tableWidth, measuredHeight - 4), 14, color: WorkshopUi.Muted);
                hint.alignment = TextAnchor.UpperLeft;
            }
            else CompactTrialTable(measuredTable, "Composite trial measured", measurements, comparing, comparableReports);
            Canvas.ForceUpdateCanvases();
            compactTrialResultsScroll.StopMovement();
            compactTrialResultsScroll.verticalNormalizedPosition = contentHeight <= scrollHeight + 1 ? 1 : previousPosition;
        }

        void CompactTrialIdentity(string objectName, string lane, string entryName, float y, float w, bool measured, bool mixedState)
        {
            string fullName = ui.Resolve(entryName ?? "—");
            string caption = lane + " · " + (mixedState ? (measured ? "результат · " : "нет результата · ") : "") + fullName;
            var name = ui.Label(compactTrialResults, objectName, "", new Rect(0, y, w, 20), 14,
                color: lane == "A" ? WorkshopUi.Blue : WorkshopUi.Ink);
            SequenceFitLabel(name, caption);
            ui.Tip(name.gameObject, "Полоса " + lane + ": " + fullName + "\n" +
                (measured ? "Результат прогона" : mixedState ? "Результат не получен" : "Расчёт до запуска"));
        }

        void CompactTrialColumnHeader(Transform parent, float y, float w, bool comparing)
        {
            float labelWidth = CompactTrialLabelWidth(w, comparing);
            float columnWidth = (w - labelWidth) / (comparing ? 3 : 1);
            ui.Label(parent, "Composite trial metric legend", "Показатель", new Rect(0, y, labelWidth, 20), 14, color: WorkshopUi.Muted);
            for (int column = 0; column < (comparing ? 3 : 1); column++)
            {
                string caption = column == 0 ? "A" : column == 1 ? "B" : "Δ";
                var label = ui.Label(parent, "Composite trial column " + caption, caption,
                    new Rect(labelWidth + column * columnWidth, y, columnWidth, 20), 14,
                    color: column == 0 ? WorkshopUi.Blue : column == 1 ? WorkshopUi.Ink : WorkshopUi.Muted);
                label.alignment = TextAnchor.MiddleCenter;
            }
        }

        CompactTrialQuote CompactQuote(AlbumEntry source, AssemblyTrialReport report)
        {
            if (report != null) return new CompactTrialQuote(report.UnitCost, report.Copies, report.Cost);
            if (source == null) return default;
            var resolution = run.PreviewAlbumEntry(source, MaterialTier.Pencil);
            if (!resolution.Valid) return default;
            var resolved = resolution.Value;
            int placementCost = source.Kind == AlbumEntryKind.Structure ? resolved.Cost : Math.Max(1, resolved.Cost);
            int copies = placementCost > 0 ? assemblyTrialBudget / placementCost : 0;
            if (resolved.Support?.MaximumInstances > 0) copies = Math.Min(copies, resolved.Support.MaximumInstances);
            return new CompactTrialQuote(resolved.Cost, copies, copies * placementCost);
        }

        static bool CompactReportsComparable(AssemblyTrialReport first, AssemblyTrialReport second)
            => first != null && second != null && first.Scenario == second.Scenario && first.Budget == second.Budget && first.Seed == second.Seed;

        static List<CompactTrialMetric> CompactMeasuredMetrics(AssemblyTrialReport first, AssemblyTrialReport second)
        {
            var rows = new List<CompactTrialMetric>();
            void Add(string id, string caption, string hint, double? a, double? b, bool? moreIsBetter)
            {
                if (!a.HasValue && !b.HasValue) return;
                rows.Add(new CompactTrialMetric { Id = id, Caption = caption, Hint = hint, First = a, Second = b, MoreIsBetter = moreIsBetter });
            }
            Add("Damage", "Урон", "Фактически нанесённый урон всеми копиями за весь прогон.", first?.Damage, second?.Damage, true);
            Add("MarkedEnemies", "Помечено целей", "Число врагов, у которых сооружение ослабило броню.", first?.MarkedEnemies, second?.MarkedEnemies, true);
            Add("SlowedEnemies", "Замедлено целей", "Число врагов, попавших под синий импульс призмы.", first?.SlowedEnemies, second?.SlowedEnemies, true);
            Add("Leaks", "Пропущено", "Число врагов, прошедших полосу за прогон.", first?.Leaks, second?.Leaks, false);
            Add("UnresolvedEnemies", "Осталось целей", "Живые цели на момент завершения измерения.", first?.UnresolvedEnemies, second?.UnresolvedEnemies, false);
            Add("RepairedHealth", "Ремонт, HP", "Суммарно восстановленная прочность за прогон, HP.", first?.RepairedHealth, second?.RepairedHealth, true);
            Add("Income", "Доход, штр.", "Суммарный доход генераторов за прогон, штрихи.", first?.Income, second?.Income, true);
            Add("CompletedWaves", "Полных волн", "Полностью завершённые волны в измерении.", first?.CompletedWaves, second?.CompletedWaves, true);
            Add("PaidWaves", "Оплачено волн", "Количество волн, за которые генераторы получили доход.", first?.PaidWaves, second?.PaidWaves, true);
            Add("BlotsCleared", "Убрано клякс", "Фактически очищенные кляксы за прогон.", first?.BlotsCleared, second?.BlotsCleared, true);
            Add("UnresolvedBlots", "Осталось клякс", "Кляксы, оставшиеся к завершению измерения.", first?.UnresolvedBlots, second?.UnresolvedBlots, false);
            Add("PreventedMovement", "Сдержано, кл.", "Суммарное предотвращённое перемещение врагов относительно контрольного прогона, клетки.", first?.PreventedMovement, second?.PreventedMovement, true);
            Add("DurationSeconds", "Длительность, с", "Время до завершения прогона. Может быть короче из-за раннего окончания; меньшая длительность не всегда лучше.", first?.DurationSeconds, second?.DurationSeconds, null);
            return rows;
        }

        static float CompactTrialLabelWidth(float width, bool comparing) => width * (comparing ? .43f : .58f);
        string CompactTrialNumber(double value) => value.ToString("0.##", ui.TextResolver.Culture);
        string CompactTrialDifference(double value) => value.ToString("+0.##;−0.##;0", ui.TextResolver.Culture);

        void CompactTrialTable(RectTransform table, string prefix, IReadOnlyList<CompactTrialMetric> metrics, bool comparing, bool allowDelta)
        {
            float w = table.rect.width, labelWidth = CompactTrialLabelWidth(w, comparing);
            float columnWidth = (w - labelWidth) / (comparing ? 3 : 1);
            for (int index = 0; index < metrics.Count; index++)
            {
                var metric = metrics[index];
                var row = ui.Panel(table, prefix + " metric " + metric.Id,
                    new Rect(0, index * CompactTrialRowHeight, w, CompactTrialRowHeight), Color.clear);
                var label = ui.Label(row, prefix + " row " + index + " label", metric.Caption,
                    new Rect(0, 0, labelWidth - 4, CompactTrialRowHeight), 14);
                ui.Tip(label.gameObject, metric.Hint);
                Cell(prefix + " row " + index + " value", metric.First, 0, WorkshopUi.Blue);
                if (comparing)
                {
                    Cell(prefix + " comparison row " + index + " value", metric.Second, 1, WorkshopUi.Ink);
                    double? difference = allowDelta && metric.First.HasValue && metric.Second.HasValue ? metric.Second.Value - metric.First.Value : (double?)null;
                    Color deltaColor = WorkshopUi.Muted;
                    if (difference.HasValue && Math.Abs(difference.Value) > .0001 && metric.MoreIsBetter.HasValue)
                        deltaColor = (difference.Value > 0) == metric.MoreIsBetter.Value ? WorkshopUi.Success : WorkshopUi.Warning;
                    var delta = ui.Label(row, prefix + " delta row " + index + " value", difference.HasValue ? CompactTrialDifference(difference.Value) : "—",
                        new Rect(labelWidth + columnWidth * 2, 0, columnWidth - 2, CompactTrialRowHeight), 14, color: deltaColor);
                    delta.alignment = TextAnchor.MiddleCenter;
                    ui.Tip(delta.gameObject, difference.HasValue ? metric.Hint + "\nB − A: " + CompactTrialDifference(difference.Value) : "Разница не рассчитана: нет сопоставимых измерений с обеих сторон.");
                }
                var rule = WorkshopUi.Graphic<Image>(row, prefix + " row " + index + " rule", new Rect(0, CompactTrialRowHeight - 1, w, 1));
                rule.color = new Color(WorkshopUi.Ink.r, WorkshopUi.Ink.g, WorkshopUi.Ink.b, .18f);

                void Cell(string objectName, double? value, int column, Color color)
                {
                    var text = ui.Label(row, objectName, value.HasValue ? CompactTrialNumber(value.Value) : "—",
                        new Rect(labelWidth + columnWidth * column, 0, columnWidth - 2, CompactTrialRowHeight), 14, color: color);
                    text.alignment = TextAnchor.MiddleCenter;
                    ui.Tip(text.gameObject, value.HasValue ? (column == 0 ? "A: " : "B: ") + CompactTrialNumber(value.Value) + "\n" + metric.Hint : "Для этой сборки измерение отсутствует.");
                }
            }
        }
    }
}
