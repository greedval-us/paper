using System;
using System.Globalization;
using System.Linq;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        bool defeatReviewOpen;
        Guid defeatReviewRunId;
        DefeatReport defeatReviewReport;
        GameObject defeatReviewOverlay;
        UnityEngine.UI.ScrollRect defeatReviewScrollView;
        float defeatReviewScroll = 1;

        void ShowDefeatReviewInvitation(RectTransform sheet, NeutralMissionMapGraphic preview,
            float leftWidth, float bodyHeight, bool desktop)
        {
            if (game.Run.Session.Phase != RunPhase.Defeat || sheet.Find("Result defeat review") != null) return;
            PlayerUi.Place(preview.rectTransform, new Rect(10, 184, leftWidth - 20,
                bodyHeight - (desktop ? 216 : 194) - 94));
            var invitation = CampaignText(sheet, "Посмотри потери по волнам\nи отмеченные позиции на карте.",
                new Rect(18, 87, leftWidth - 36, 42), 16, PlayerUi.Ink);
            invitation.name = "Defeat review invitation";
            invitation.alignment = TextAnchor.UpperLeft;
            NotebookAction(sheet, "Result defeat review", new Rect(18, 130, leftWidth - 36, 44),
                PlayerIcon.Book, "Разбор поражения", OpenDefeatReview, true);
        }

        void RestoreDefeatReview()
        {
            if (game.Run.Session.Phase != RunPhase.Defeat || game.Run.Session.RunId != defeatReviewRunId)
            {
                defeatReviewOpen = false;
                defeatReviewReport = null;
                defeatReviewScrollView = null;
                defeatReviewScroll = 1;
                return;
            }
            if (defeatReviewOpen) OpenDefeatReview();
        }

        void OpenDefeatReview()
        {
            var session = game.Run.Session;
            if (game.Page != PlayerPage.Result || session.Phase != RunPhase.Defeat) return;
            if (defeatReviewOverlay != null && defeatReviewOverlay.activeInHierarchy) return;
            if (defeatReviewScrollView != null) defeatReviewScroll = defeatReviewScrollView.verticalNormalizedPosition;
            if (defeatReviewReport == null || defeatReviewRunId != session.RunId)
            {
                defeatReviewReport = DefeatAnalysis.Analyze(session);
                defeatReviewRunId = session.RunId;
                defeatReviewScroll = 1;
            }
            defeatReviewOpen = true;
            var report = defeatReviewReport;
            var previous = page.GetComponentsInChildren<UnityEngine.UI.Button>()
                .FirstOrDefault(button => button.name == "Result defeat review");
            var shade = ui.Panel(page, "Defeat review", new Rect(0, 0, W, H), new Color(.08f, .12f, .18f, .65f), true);
            defeatReviewOverlay = shade.gameObject;
            float w = Mathf.Min(1200, W - 32), h = Mathf.Min(720, H - 32);
            bool wide = w >= 1000;
            float pad = wide ? 20 : 16, gap = wide ? 20 : 16;
            var sheet = ui.Panel(shade, "Defeat review sheet", new Rect((W - w) * .5f, (H - h) * .5f, w, h), PlayerUi.Card, true);
            var title = NotebookLabel(sheet, "Разбор поражения", new Rect(pad, 10, w - pad * 2, 40), wide ? 30 : 26);
            title.fontStyle = FontStyle.Bold;
            NotebookLabel(sheet, "Наблюдения этой попытки — что проверить перед повтором", new Rect(pad, 48, w - pad * 2, 22), 15, PlayerUi.Muted);

            float bodyHeight = h - 220;
            float mapWidth = wide ? 700 : Mathf.Min(360, (w - pad * 2 - gap) * .422f);
            float factsWidth = w - pad * 2 - gap - mapWidth;
            var map = ui.Panel(sheet, "Defeat review map panel", new Rect(pad, 72, mapWidth, bodyHeight), PlayerUi.Wash);
            NotebookLabel(map, "Отмеченные места", new Rect(12, 4, mapWidth - 24, 28), wide ? 20 : 18);
            var chart = PlayerUi.Graphic<NeutralMissionMapGraphic>(map, "Defeat review map",
                new Rect(4, 36, mapWidth - 8, bodyHeight - 106));
            chart.Bind(session, report);
            var legend = NotebookLabel(map, "Красная точка — источник атак\nКрест — разрушенная постройка\nЖёлтое кольцо — без выстрелов",
                new Rect(12, bodyHeight - 66, mapWidth - 24, 60), wide ? 16 : 14, PlayerUi.Muted);
            legend.alignment = TextAnchor.UpperLeft;
            legend.resizeTextMinSize = wide ? 16 : 14;

            var facts = ui.Scroll(sheet, "Defeat review findings", new Rect(pad + mapWidth + gap, 72, factsWidth, bodyHeight), bodyHeight);
            float textWidth = factsWidth - 20, y = 0;
            int textSize = wide ? 18 : 16;
            DefeatReviewSection(facts, "Defeat review wave", "Потери замка по волнам", DefeatWaveCopy(report), textWidth, textSize, ref y);
            DefeatReviewSection(facts, "Defeat review approach", report.TelemetryTruncated || report.UnattributedBaseDamage > 0 ? "Отмеченная точка атак" : "Точка наибольшего урона", DefeatApproachCopy(report), textWidth, textSize, ref y);
            DefeatReviewSection(facts, "Defeat review positions", "Позиции без выстрелов", DefeatPositionsCopy(report), textWidth, textSize, ref y);
            if (report.TelemetryTruncated)
                DefeatReviewSection(facts, "Defeat review limits", "Данные неполные",
                    "Показаны сохранённые наблюдения; часть данных не вошла в отчёт.", textWidth, textSize, ref y);
            facts.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(bodyHeight, y));
            defeatReviewScrollView = facts.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            defeatReviewScrollView.verticalNormalizedPosition = defeatReviewScroll;

            var advice = ui.Label(sheet, "Перед повтором: " + DefeatAdviceCopy(report),
                new Rect(pad, h - 136, w - pad * 2, 60), textSize, alignment: TextAnchor.UpperLeft);
            advice.name = "Defeat review advice";

            Action close = () => {
                defeatReviewOpen = false;
                if (defeatReviewScrollView != null) defeatReviewScroll = defeatReviewScrollView.verticalNormalizedPosition;
                defeatReviewScrollView = null;
                defeatReviewOverlay = null;
                WorkshopUi.RemoveView(shade.gameObject);
            };
            float buttonWidth = (w - pad * 2 - gap * 2) / 3;
            var closeButton = NotebookAction(sheet, "Defeat review close", new Rect(pad, h - 64, buttonWidth, 48), PlayerIcon.Back, "Закрыть", close);
            NotebookAction(sheet, "Defeat review prepare", new Rect(pad + buttonWidth + gap, h - 64, buttonWidth, 48), PlayerIcon.Pencil,
                "К подготовке", () => { close(); game.ReturnToPreparation(); }, true);
            NotebookAction(sheet, "Defeat review retry", new Rect(pad + (buttonWidth + gap) * 2, h - 64, buttonWidth, 48), PlayerIcon.Restart,
                "Повторить", () => { close(); game.Retry(); });
            WorkshopModal.Open(shade.gameObject, close, previous != null ? previous.gameObject : null);
            FieldMenuTheme.ApplyTo(shade, false);
            EventSystem.current?.SetSelectedGameObject(closeButton.gameObject);
        }

        void DefeatReviewSection(Transform parent, string name, string title, string copy, float width, int size, ref float y)
        {
            var heading = ui.Label(parent, title, new Rect(0, y, width, 30), size + 2, title: true);
            heading.fontStyle = FontStyle.Bold;
            float headingHeight = Mathf.Max(30, Mathf.Ceil(heading.preferredHeight) + 4);
            PlayerUi.Place(heading.rectTransform, new Rect(0, y, width, headingHeight));
            y += headingHeight + 4;
            var body = ui.Label(parent, copy, new Rect(0, y, width, 1), size, alignment: TextAnchor.UpperLeft);
            body.name = name;
            float bodyHeight = Mathf.Max(size * 1.5f, Mathf.Ceil(body.preferredHeight) + 4);
            PlayerUi.Place(body.rectTransform, new Rect(0, y, width, bodyHeight));
            y += bodyHeight + 22;
        }

        static string DefeatNumber(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
        static string DefeatCell(Cell cell) => "(" + (cell.X + 1) + "; " + (cell.Y + 1) + ")";

        static string DefeatWaveCopy(DefeatReport report)
        {
            if (!report.WorstWave.HasValue) return "Недостаточно данных о потерях HP замка по волнам.";
            var wave = report.WorstWave.Value;
            return "Волна " + wave.WaveIndex + ": замок потерял " + DefeatNumber(wave.BaseDamage) + " HP.\n" +
                "Разрушено построек: " + wave.DestroyedStructures + ". Потеряно " + wave.LostInvestment + " штр.";
        }

        static string DefeatApproachCopy(DefeatReport report)
        {
            if (!report.DangerousApproach.HasValue)
                return report.HasRecordedDamage
                    ? "Источник атак не записан. Известно: " + DefeatNumber(report.TotalBaseDamage) + " HP урона замку."
                    : "Недостаточно данных: точки атак по замку не зафиксированы.";
            var point = report.DangerousApproach.Value;
            string copy = "Точка " + DefeatCell(point.Cell) + ": " + DefeatNumber(point.BaseDamage) + " HP урона замку, попаданий — " + point.Hits + ".\n" +
                "Отсюда враги атаковали замок.";
            if (report.UnattributedBaseDamage > 0)
                copy += "\nЕщё " + DefeatNumber(report.UnattributedBaseDamage) + " HP — без записанного источника.";
            return copy;
        }

        static string DefeatPositionsCopy(DefeatReport report)
        {
            if (report.IdlePositions.Count == 0)
                return "Позиций с 15 сек. готовности без выстрелов не зафиксировано.";
            return string.Join("\n", report.IdlePositions.Select(position => "Позиция " + DefeatCell(position.Cell) +
                " · готова " + DefeatNumber(position.ActiveSeconds) + " сек. · 0 выстрелов")) +
                "\nПроверь дальность и сектор этих позиций. Враги могли пройти вне дальности или сектора.";
        }

        static string DefeatAdviceCopy(DefeatReport report)
        {
            switch (report.Advice)
            {
                case DefeatAdviceKind.DefendObservedApproach:
                    return "Попробуй прикрыть отмеченную точку огнём. Проверь дальность и сектор башен между этой точкой и замком.";
                case DefeatAdviceKind.RepositionUnfiredTower:
                    return "Проверь дальность и сектор отмеченных позиций. Если они не перекрывают подход врагов, попробуй другое место.";
                case DefeatAdviceKind.ReviewLossWave:
                    return "Перед волной " + report.WorstWave.Value.WaveIndex + " проверь направления атаки и подготовь запас на восстановление обороны.";
                default:
                    return "Недостаточно данных для конкретного совета. Перед повтором проверь направления волн и перекрытие дальности.";
            }
        }
    }
}
