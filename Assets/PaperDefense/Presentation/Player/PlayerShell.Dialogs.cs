using System;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        RectTransform Sheet(string title, float desiredWidth, float desiredHeight, out float width)
        {
            width = Mathf.Min(desiredWidth, W - 48);
            float height = Mathf.Min(desiredHeight, H - 72);
            ui.Panel(page, "Modal input shield", new Rect(0, 0, W, H), new Color(.10f, .18f, .30f, .38f), true);
            var panel = ui.Panel(page, "Modal", new Rect((W - width) / 2, (H - height) / 2, width, height), PlayerUi.Card, true);
            ui.Label(panel, title, new Rect(28, 18, width - 56, 62), 38, title: true);
            return ui.Scroll(panel, "Sheet scroll", new Rect(24, 94, width - 48, height - 118), desiredHeight - 118);
        }

        void Pause()
        {
            var panel = Sheet("Пауза", 640, 540, out float width); width -= 48;
            ui.Label(panel, "Страница подождёт.\nРасстановка и ресурсы остаются на месте.", new Rect(12, 0, width - 24, 70), 22, PlayerUi.Muted);
            ui.Button(panel, "Resume", new Rect(12, 96, width - 24, 56), PlayerIcon.Play, "Продолжить оборону", game.Resume, true);
            ui.Button(panel, "Pause settings", new Rect(12, 166, width - 24, 50), PlayerIcon.Settings, "Настройки", () => game.OpenSettings());
            ui.Button(panel, "Restart battle", new Rect(12, 230, width - 24, 50), PlayerIcon.Restart, "Начать заново", () => Confirm("Начать миссию заново?", "Текущая расстановка и ход этого боя будут потеряны. Сохранённые чертежи и золото останутся.", game.Retry));
            ui.Button(panel, "Leave battle", new Rect(12, 294, width - 24, 50), PlayerIcon.Map, "Вернуться на карту", () => Confirm("Завершить эту попытку?", "Продолжить этот бой позже нельзя. Сохранённые чертежи и золото останутся в профиле.", game.ReturnToMap));
        }

        void Upgrades()
        {
            var panel = Sheet("Новый штрих к обороне", 1100, 620, out float width); width -= 48;
            ui.Label(panel, "Выбери одно усиление на текущую попытку.", new Rect(0, 0, width, 44), 21, PlayerUi.Muted);
            var rules = game.Run.Session.Balance.Progression;
            string[] details = {
                ui.T("upgrade_stats_0", Mathf.RoundToInt((rules.RapidFireMultiplier - 1) * 100)),
                ui.T("upgrade_stats_1", Mathf.RoundToInt((rules.DeepInkDamageMultiplier - 1) * 100), Mathf.RoundToInt((rules.DeepInkHealthMultiplier - 1) * 100)),
                ui.T("upgrade_stats_2", rules.SalvageImmediateInk, Mathf.RoundToInt((rules.SalvageWaveRewardMultiplier - 1) * 100), Mathf.RoundToInt(rules.SalvageRefundBonus * 100))
            };
            PlayerIcon[] icons = { PlayerIcon.Fast, PlayerIcon.Drop, PlayerIcon.Restart };
            float cardW = (width - 32) / 3;
            bool stacked = width < 800;
            if (stacked) { cardW = width; panel.sizeDelta = new Vector2(width, 64 + 3 * 260); }
            for (int i = 0; i < 3; i++)
            {
                int selected = i;
                var card = ui.Panel(panel, "Upgrade offer " + i, new Rect(stacked ? 0 : i * (cardW + 16), 64 + (stacked ? i * 260 : 0), cardW, stacked ? 244 : 400), PlayerUi.Wash);
                var icon = PlayerUi.Graphic<PlayerIconGraphic>(card, "Upgrade symbol", new Rect(24, 24, 52, 52)); icon.Icon = icons[i]; icon.color = PlayerUi.Blue;
                ui.Label(card, ui.T("upgrade_" + i).Replace('\n', ' '), new Rect(stacked ? 96 : 24, stacked ? 24 : 94, cardW - (stacked ? 120 : 48), 84), 26, title: true);
                ui.Label(card, details[i], new Rect(24, stacked ? 96 : 182, cardW - 48, stacked ? 82 : 130), 19, PlayerUi.Muted);
                ui.Button(card, "Choose upgrade " + i, new Rect(20, stacked ? 184 : 328, cardW - 40, 50), PlayerIcon.Plus, "Выбрать", () => game.ChooseUpgrade((RunUpgrade)selected), true);
            }
        }

        void Result()
        {
            bool win = game.Run.Session.Phase == RunPhase.Victory;
            var panel = Sheet(win ? "Страница защищена!" : "Попробуем другую линию?", 1040, statistics ? 820 : 610, out float width); width -= 48;
            var s = game.Run.Session; var stats = s.Statistics;
            ui.Art(panel, win ? "object.castle" : "object.castle.damaged", new Rect(12, 0, 200, 190));
            ui.Label(panel, ui.T("result", s.WaveIndex, s.Balance.Waves.Count, s.Kills, s.GoldEarned), new Rect(240, 12, width - 264, 160), 25);
            ui.Button(panel, "Result statistics", new Rect(12, 204, width - 24, 48), PlayerIcon.Book, statistics ? "Свернуть статистику миссии" : "Статистика миссии", () => { statistics = !statistics; Show(PlayerPage.Result); });
            float bottom = 286;
            if (statistics)
            {
                ui.Label(panel, ui.T("result_stats_left", stats.BuildInvestment + stats.UpgradeInvestment, stats.RefundedInk, stats.LostInvestment, stats.PeakAliveEnemies), new Rect(12, 272, width * .5f - 24, 180), 20);
                ui.Label(panel, ui.T("result_stats_right", Mathf.RoundToInt(stats.TotalDamage), stats.TotalStructuresBuilt, stats.DestroyedStructures, stats.Upgrades.Count), new Rect(width * .5f + 12, 272, width * .5f - 24, 180), 20);
                bottom = 486;
            }
            float buttonW = (width - 48) / 3;
            ui.Button(panel, "Result map", new Rect(12, bottom, buttonW, 56), PlayerIcon.Map, "На карту", game.ReturnToMap);
            ui.Button(panel, "Result retry", new Rect(24 + buttonW, bottom, buttonW, 56), PlayerIcon.Restart, "Ещё попытка", game.Retry, true);
            ui.Button(panel, "Result workshop", new Rect(36 + 2 * buttonW, bottom, buttonW, 56), PlayerIcon.Pencil, "Мастерская", () => { game.Run.OpenWorkshop(); game.Navigate(PlayerPage.Workshop); });
            ui.Label(panel, GameText.Resolve(game.Run.Workshop.SaveMessage), new Rect(12, bottom + 78, width - 24, 58), 17, PlayerUi.Muted);
            if (game.Run.Workshop.HasUnsavedChanges)
            {
                panel.sizeDelta = new Vector2(width, bottom + 200);
                ui.Button(panel, "Result retry save", new Rect(12, bottom + 140, width - 24, 44), PlayerIcon.Save, "Повторить сохранение", () => { game.Run.RetrySave(out _); Show(PlayerPage.Result); });
            }
        }

        void Help()
        {
            var panel = Sheet("Как держать линию", 1060, 780, out float width); width -= 48;
            string[] titles = { "01  Собери свою башню", "02  Подготовь оборону", "03  Следи за боем" };
            string[] body = {
                "Основа → Оружие → Рисунок → Проверка. Рисунок влияет на механику башни. Следи за вместимостью, бюджетом чернил и доступностью деталей. Проверь результат в полигоне и сохрани чертёж.",
                "Назначь четыре чертежа в набор. Выбери материал, размести башни и препятствия. Нельзя перекрывать все пути к замку. Цветные чернила и обычные штрихи — разные ресурсы.",
                "Запускай волны, используй ремонт, улучшение, окраску и промокашку. Колодцы дают цветные чернила, но нуждаются в защите. Усиление действует до конца попытки."
            };
            for (int i = 0; i < 3; i++)
            {
                ui.Label(panel, titles[i], new Rect(8, i * 156, width - 16, 42), 28, title: true);
                ui.Label(panel, body[i], new Rect(8, i * 156 + 48, width - 16, 98), 20, PlayerUi.Muted);
            }
            ui.Label(panel, "Esc / Пробел — пауза     N — следующая волна     F — скорость", new Rect(8, 478, width - 16, 60), 19, PlayerUi.Blue);
            ui.Button(panel, "Close help", new Rect(8, 564, 270, 52), PlayerIcon.Back, "Вернуться", game.CloseHelp, true);
        }

        public void Confirm(string title, string message, Action action)
        { confirmTitle = title; confirmMessage = message; confirmAction = action; BuildConfirmation(); }
        void BuildConfirmation()
        {
            if (confirmation != null) Destroy(confirmation);
            var shade = ui.Panel(page, "Confirmation shield", new Rect(0, 0, W, H), new Color(.08f, .15f, .25f, .48f), true); confirmation = shade.gameObject;
            float width = Mathf.Min(640, W - 40), height = Mathf.Min(360, H - 40);
            var card = ui.Panel(shade, "Confirmation", new Rect((W - width) / 2, (H - height) / 2, width, height), PlayerUi.Card, true);
            ui.Label(card, confirmTitle, new Rect(28, 24, width - 56, 76), 34, title: true);
            ui.Label(card, confirmMessage, new Rect(28, 112, width - 56, height - 210), 20, PlayerUi.Muted);
            ui.Button(card, "Cancel confirmation", new Rect(28, height - 78, (width - 68) / 2, 50), PlayerIcon.Back, "Остаться", CancelConfirmation);
            ui.Button(card, "Confirm action", new Rect(width / 2 + 6, height - 78, (width - 68) / 2, 50), PlayerIcon.Next, "Продолжить", () => { var action = confirmAction; CancelConfirmation(); action?.Invoke(); }, true);
            WorkshopModal.Open(confirmation, CancelConfirmation);
        }
        public void CancelConfirmation()
        { confirmAction = null; if (confirmation != null) DestroyImmediate(confirmation); confirmation = null; }
    }
}
