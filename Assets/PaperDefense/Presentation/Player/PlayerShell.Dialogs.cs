using System;
using System.Linq;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        CanvasGroup pausePresentation;

        string JourneyCopy(string key, params object[] args)
            => GameText.Resolve(UserText.Message("assembly.player." + key, args));

        void ScopeSheet(RectTransform content, Action close)
            => WorkshopModal.Open(content.parent.parent.gameObject, close);

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
            bool assembly = game.Run.AssemblyCatalog != null;
            if (assembly) { AlbumPause(); return; }
            var panel = Sheet(assembly ? JourneyCopy("pause.title") : "Пауза", 640, 540, out float width); width -= 48;
            ui.Label(panel, assembly ? JourneyCopy("pause.body") : "Страница подождёт.\nРасстановка и ресурсы остаются на месте.", new Rect(12, 0, width - 24, 70), 22, PlayerUi.Muted);
            ui.Button(panel, "Resume", new Rect(12, 96, width - 24, 56), PlayerIcon.Play, "Продолжить оборону", game.Resume, true);
            ui.Button(panel, "Pause settings", new Rect(12, 166, width - 24, 50), PlayerIcon.Settings, "Настройки", () => game.OpenSettings());
            ui.Button(panel, "Restart battle", new Rect(12, 230, width - 24, 50), PlayerIcon.Restart, "Начать заново", () => Confirm(assembly ? JourneyCopy("pause.restart_title") : "Начать миссию заново?", assembly ? JourneyCopy("pause.restart_body") : "Текущая расстановка и ход этого боя будут потеряны. Сохранённые чертежи и золото останутся.", game.Retry));
            ui.Button(panel, "Leave battle", new Rect(12, 294, width - 24, 50), PlayerIcon.Map, "Вернуться на карту", () => Confirm(assembly ? JourneyCopy("pause.leave_title") : "Завершить эту попытку?", assembly ? JourneyCopy("pause.leave_body") : "Продолжить этот бой позже нельзя. Сохранённые чертежи и золото останутся в профиле.", game.ReturnToMap));
            if (assembly) NotebookLabel(panel, JourneyCopy("pause.resume_hint"), new Rect(12, 368, width - 24, 32), 19, PlayerUi.Muted, TextAnchor.MiddleCenter);
            ScopeSheet(panel, game.Resume);
        }

        void AlbumPause()
        {
            var shield = ui.Panel(page, "Pause presentation", new Rect(0, 0, W, H), new Color(.05f, .09f, .09f, .63f), true);
            pausePresentation = shield.gameObject.AddComponent<CanvasGroup>();
            float width = Mathf.Min(420, W - 48);
            const float height = 492, pad = 28;
            float x = W >= 1100 ? Mathf.Min(128, W * .08f) : (W - width) * .5f;
            var paper = ui.Panel(shield, "Pause paper", new Rect(x, (H - height) * .5f, width, height), Color.clear, true);
            NotebookPaperArt.Apply(paper, "Pause illustrated paper");
            var symbol = PlayerUi.Graphic<PlayerIconGraphic>(paper, "Pause heading icon", new Rect(pad, 28, 28, 28));
            symbol.Icon = PlayerIcon.Pause; symbol.Sketch = true; symbol.color = PlayerUi.Blue;
            float inner = width - pad * 2;
            NotebookLabel(paper, JourneyCopy("pause.title"), new Rect(68, 15, width - 94, 48), 36);
            string mission = game.Run.ActiveMission?.Title;
            string waves = "Волны  " + game.Run.Session.CompletedWaves + " / " + game.Run.Session.Balance.Waves.Count;
            NotebookLabel(paper, string.IsNullOrEmpty(mission) ? waves : mission + "   ·   " + waves,
                new Rect(pad, 66, inner, 28), 16, FieldMenuSkin.Muted);
            PauseAction(paper, "Resume", new Rect(pad, 108, inner, 54), PlayerIcon.Play, "Продолжить", game.Resume, true);
            BuildPauseMissionContinuation(paper, new Rect(pad, 172, inner, 44), new Rect(pad, 224, inner, 46));
            PauseAction(paper, "Pause settings", new Rect(pad, 280, inner, 44), PlayerIcon.Settings, "Настройки", () => game.OpenSettings());
            PauseAction(paper, "Restart battle", new Rect(pad, 331, inner, 44), PlayerIcon.Restart, "Начать заново",
                () => Confirm(JourneyCopy("pause.restart_title"), JourneyCopy("pause.restart_body"), game.Retry));
            PauseAction(paper, "Leave battle", new Rect(pad, 382, inner, 44), PlayerIcon.Map, "Вернуться на карту",
                () => Confirm(JourneyCopy("pause.leave_title"), JourneyCopy("pause.leave_body"), game.ReturnToMap));
            var resumeHint = NotebookLabel(paper, JourneyCopy("pause.resume_hint"), new Rect(pad, 444, inner, 28), 15, PlayerUi.Muted);
            resumeHint.resizeTextMinSize = 14;
            WorkshopModal.Open(shield.gameObject, game.Resume);
        }

        Button PauseAction(Transform parent, string name, Rect bounds, PlayerIcon icon, string caption, Action action, bool primary = false)
        {
            var button = ui.Button(parent, name, bounds, icon, caption, action, primary);
            PlayerUi.Place(button.GetComponentInChildren<PlayerIconGraphic>().rectTransform, new Rect(16, (bounds.height - 24) * .5f, 24, 24));
            var label = button.GetComponentInChildren<Text>();
            label.rectTransform.offsetMin = new Vector2(52, 6); label.rectTransform.offsetMax = new Vector2(-12, -6);
            label.fontSize = label.resizeTextMaxSize = 20; label.resizeTextMinSize = 16;
            return button;
        }

        void Upgrades()
        {
            if (game.Run.AssemblyCatalog != null) { AlbumUpgrades(); return; }
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
            ScopeSheet(panel, () => { });
        }

        void AlbumUpgrades()
        {
            var shield = ui.Panel(page, "Upgrade choice shield", new Rect(0, 0, W, H), new Color(.10f, .18f, .30f, .30f), true);
            float width = Mathf.Min(1000, W - 48), cardWidth = (width - 24) / 3;
            float height = Mathf.Min(620, H - 48), top = (H - height) * .5f;
            float x = (W - width) * .5f, cardTop = top + 112, cardHeight = height - 156;
            ui.Panel(shield, "Upgrade choice sheet", new Rect(x - 12, top, width + 24, height), PlayerUi.Paper, true);
            NotebookLabel(shield, "Выбери усиление", new Rect(x, top + 8, width, 48), 32, alignment: TextAnchor.MiddleCenter);
            var waveNote = ui.Panel(shield, "Upgrade completed-wave paper", new Rect(x, top + 64, width, 32), PlayerUi.Card);
            NotebookLabel(waveNote, JourneyCopy("result.waves", game.Run.Session.CompletedWaves, game.Run.Session.Balance.Waves.Count) + " · Выбери одно до конца попытки",
                new Rect(8, 0, width - 16, 32), 18, PlayerUi.Ink, TextAnchor.MiddleCenter);
            var rules = game.Run.Session.Balance.Progression;
            string[] details = {
                "+" + Mathf.RoundToInt((rules.RapidFireMultiplier - 1) * 100) + "%\nТемп стрельбы",
                ui.T("upgrade_stats_1", Mathf.RoundToInt((rules.DeepInkDamageMultiplier - 1) * 100), Mathf.RoundToInt((rules.DeepInkHealthMultiplier - 1) * 100)),
                ui.T("upgrade_stats_2", rules.SalvageImmediateInk, Mathf.RoundToInt((rules.SalvageWaveRewardMultiplier - 1) * 100), Mathf.RoundToInt(rules.SalvageRefundBonus * 100))
            };
            for (int i = 0; i < 3; i++)
            {
                int selected = i;
                var card = ui.Panel(shield, "Upgrade offer " + i, new Rect(x + i * (cardWidth + 12), cardTop, cardWidth, cardHeight), PlayerUi.Card, true);
                NotebookLabel(card, ui.T("upgrade_" + i).Split('\n')[0], new Rect(12, 12, cardWidth - 24, 64), 25, alignment: TextAnchor.MiddleCenter);
                var effects = NotebookLabel(card, details[i], new Rect(16, 88, cardWidth - 32, cardHeight - 164), 21, PlayerUi.Ink, TextAnchor.MiddleCenter);
                effects.name = "Upgrade effect " + i; effects.resizeTextMinSize = 18;
                ui.Button(card, "Choose upgrade " + i, new Rect(16, cardHeight - 64, cardWidth - 32, 48), PlayerIcon.Plus, "Выбрать", () => game.ChooseUpgrade((RunUpgrade)selected));
            }
            NotebookLabel(shield, JourneyCopy("result.generator_note"), new Rect(x, top + height - 34, width, 28), 18, PlayerUi.Muted, TextAnchor.MiddleCenter);
            WorkshopModal.Open(shield.gameObject, () => { });
        }

        void Result()
        {
            if (game.Run.CampaignEnabled) { CampaignResult(); return; }
            if (game.Run.AssemblyCatalog != null) AlbumResult();
            else LegacyResult();
        }

        void LegacyResult()
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
            bool assembly = game.Run.AssemblyCatalog != null;
            bool modernization = game.Run.Session.Balance.Mechanics.FieldModernization;
            bool flasks = game.Run.Session.Balance.Mechanics.WaveInkFlasks;
            NotebookPageBackdrop();
            MenuHeader("Как держать линию", game.CloseHelp, "Правила и управление",
                "Прокрути инструкции до управления. Кнопка «Понятно» возвращает на предыдущий экран.");
            page.Find("Notebook header/Back").name = "Help back";
            float width = Mathf.Min(980, W - 48), footerY = H - 76;
            float viewportHeight = Mathf.Max(100, footerY - 16 - 96);
            var content = ui.Scroll(page, "Help responsive scroll", new Rect((W - width) * .5f, 96, width, viewportHeight), viewportHeight);
            float chunkWidth = width - 20, y = 0;
            string[] titles = { "01  Собери", "02  Сохрани", "03  Подготовь", "04  Защищай", "Ресурсы", "Управление" };
            PlayerIcon[] symbols = { PlayerIcon.Pencil, PlayerIcon.Book, PlayerIcon.Map, PlayerIcon.Shield, PlayerIcon.Drop, PlayerIcon.Settings };
            string[] copy = {
                assembly
                    ? "Основа → Оружие → Сборка → Проверка. Выбери детали, сохрани компоненты. Проверка покажет, чего не хватает башне."
                    : "Основа → Оружие → Проверить. Выбери готовые детали, проверь нагрузку и доступность.",
                "Испытай конструкцию и сохрани в альбом — она появится в подготовке к бою.",
                modernization
                    ? "До 8 чертежей из альбома. Все копии начинают бой с базовыми параметрами. Пустые места можно заполнить через альбом."
                    : assembly
                    ? "Назначь до 8 страниц в набор. Выбери начальный материал. Пустой слот можно заполнить через альбом."
                    : "Назначь четыре страницы в набор и выбери начальный материал. Альбом позволяет заменить выбранную страницу.",
                modernization
                    ? "Развивай мощность, механику, дальность и стойкость башни отдельно: шесть обычных шагов, одно направление III. Ранги II и III открываются после сохранённых побед на первой и третьей страницах. Во время работ башня не стреляет и не очищает поле."
                    : "Защищай замок, запускай волны и выбирай усиления. Оставляй врагам путь к замку. Строй и ремонтируй за штрихи.",
                flasks
                    ? "Штрихи идут на строительство. После каждой волны, кроме последней, выбери красный, синий или зелёный флакон и получи " + game.Run.Session.InkPerFlask + " чернил. Накопленные флаконы можно открыть, пока идёт бой. " +
                      (modernization ? "Нажми U и выбери башню, чтобы установить цвет. Первый цвет закрепляется за башней." : "Цветные чернила нужны для окраски башен.")
                    : modernization ? "Штрихи — строительство и обычное развитие. Цветные чернила — цветные ранги. Первый цвет закрепляется за копией; отмена подтверждения бесплатна. Новый бой сбрасывает развитие. Сохранённый чертёж не меняется."
                    : assembly ? JourneyCopy("help.resources_body")
                    : "Штрихи — строительство и ремонт. Золото — покупки в мастерской. Цветные чернила — отдельный ресурс для окраски.",
                modernization ? "Пробел — тактическая пауза · N — волна · F — скорость\n1–8 — чертёж · E — снос · B — промокашка\nЛКМ — выбрать клетку башни. Наведи сектор мышью. Второй щелчок строит башню и фиксирует направление до сноса. ПКМ / Esc — отмена. Tab — сектора всех башен.\nU → ЛКМ по башне → + с ценой — улучшение или цвет. Esc — меню паузы. Меню и подтверждения блокируют приказы."
                    : assembly ? JourneyCopy("help.controls_body")
                    : "Esc / Пробел — пауза\nN — следующая волна · F — скорость\nQ — Материал · C — Цвет · E — Снос\nПервый щелчок выбирает клетку башни. Наведи сектор мышью. Второй щелчок строит башню и фиксирует направление до сноса. ПКМ / Esc — отмена."
            };
            var chunks = new RectTransform[titles.Length];
            var bodies = new Text[titles.Length];
            for (int i = 0; i < titles.Length; i++)
            {
                string region = i == 4 ? "Help resources" : i == 5 ? "Help controls" : "Help step " + i;
                var chunk = ui.Panel(content, region, new Rect(0, y, chunkWidth, 1), PlayerUi.Card);
                chunks[i] = chunk;
                var symbol = PlayerUi.Graphic<PlayerIconGraphic>(chunk, region + " illustration", new Rect(15, 8, 28, 28));
                symbol.Icon = symbols[i];
                symbol.Sketch = true;
                symbol.color = FieldMenuSkin.Accent;
                var title = NotebookLabel(chunk, titles[i], new Rect(52, 8, chunkWidth - 68, 28), 21);
                title.fontStyle = FontStyle.Bold;
                title.resizeTextForBestFit = false;
                var body = ui.Label(chunk, copy[i], new Rect(16, 42, chunkWidth - 32, 1), 18, PlayerUi.Ink);
                body.name = region + " text";
                body.resizeTextForBestFit = false;
                bodies[i] = body;
            }
            var close = NotebookAction(page, "Close help", new Rect((W - 180) * .5f, footerY, 180, 44),
                PlayerIcon.Check, "Понятно", game.CloseHelp, true);
            // Settle the complete page's canvas and dynamic font before measuring wrapped copy.
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                float bodyHeight = Mathf.Ceil(body.preferredHeight) + 8;
                PlayerUi.Place(body.rectTransform, new Rect(16, 42, chunkWidth - 32, bodyHeight));
                float height = 50 + bodyHeight;
                PlayerUi.Place(chunks[i], new Rect(0, y, chunkWidth, height));
                y += height + 8;
            }
            content.sizeDelta = new Vector2(width, Mathf.Max(viewportHeight, y));
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(close.gameObject);
        }

        public void Confirm(string title, string message, Action action)
        { confirmTitle = title; confirmMessage = message; confirmAction = action; game.Run.SetModalSuspended(true); game.Drawing.Cancel(); BuildConfirmation(); }
        void BuildConfirmation()
        {
            if (confirmation != null) { confirmation.SetActive(false); Destroy(confirmation); }
            var shade = ui.Panel(page, "Confirmation shield", new Rect(0, 0, W, H), new Color(.08f, .15f, .25f, .48f), true); confirmation = shade.gameObject;
            bool leaving = game.Run.AssemblyCatalog != null && confirmTitle == JourneyCopy("pause.leave_title");
            if (leaving && game.Page == PlayerPage.Pause && pausePresentation != null)
                pausePresentation.alpha = 0;
            float width = Mathf.Min(leaving ? 560 : 640, W - 40), height = Mathf.Min(leaving ? 384 : 440, H - 40);
            var card = ui.Panel(shade, "Confirmation", new Rect((W - width) / 2, (H - height) / 2, width, height), leaving ? Color.clear : PlayerUi.Card, true);
            if (leaving)
            {
                NotebookPaperArt.Apply(card, "Leave illustrated paper");
                ui.IconButton(card, "Close confirmation", new Rect(width - 56, 14, 40, 40), PlayerIcon.Close, "Закрыть подтверждение и остаться в бою", CancelConfirmation);
                var map = PlayerUi.Graphic<PlayerIconGraphic>(card, "Confirmation map illustration", new Rect((width - 144) * .5f, 22, 144, 104));
                map.Icon = PlayerIcon.Map; map.Sketch = true; map.color = PlayerUi.Blue;
                NotebookLabel(card, confirmTitle, new Rect(26, 134, width - 52, 52), 34, alignment: TextAnchor.MiddleCenter);
                NotebookLabel(card, confirmMessage, new Rect(30, 194, width - 60, height - 278), 22, PlayerUi.Ink, TextAnchor.MiddleCenter);
                var stay = ui.Button(card, "Cancel confirmation", new Rect(28, height - 78, (width - 68) / 2, 50), PlayerIcon.Back, "Остаться", CancelConfirmation, true);
                ui.Button(card, "Confirm action", new Rect(width / 2 + 6, height - 78, (width - 68) / 2, 50), PlayerIcon.Next, "Завершить бой", () => { var action = confirmAction; CancelConfirmation(); action?.Invoke(); });
                FieldMenuTheme.ApplyTo(shade, false);
                WorkshopModal.Open(confirmation, CancelConfirmation);
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(stay.gameObject);
                return;
            }
            var symbol = PlayerUi.Graphic<PlayerIconGraphic>(card, "Confirmation illustration", new Rect((width - 110) * .5f, 18, 110, 100));
            symbol.Icon = leaving ? PlayerIcon.Map : PlayerIcon.Help; symbol.Sketch = true; symbol.color = PlayerUi.Blue;
            NotebookLabel(card, confirmTitle, new Rect(28, 126, width - 56, 68), 34, alignment: TextAnchor.MiddleCenter);
            NotebookLabel(card, confirmMessage, new Rect(28, 204, width - 56, height - 294), 22, PlayerUi.Ink, TextAnchor.MiddleCenter);
            ui.Button(card, "Cancel confirmation", new Rect(28, height - 78, (width - 68) / 2, 50), PlayerIcon.Back, "Остаться", CancelConfirmation);
            ui.Button(card, "Confirm action", new Rect(width / 2 + 6, height - 78, (width - 68) / 2, 50), PlayerIcon.Next, leaving ? "Завершить бой" : "Продолжить", () => { var action = confirmAction; CancelConfirmation(); action?.Invoke(); }, true);
            // Confirmations are siblings of the field controls, so they need their
            // own scoped menu treatment even when opened directly on the battle.
            FieldMenuTheme.ApplyTo(shade, false);
            WorkshopModal.Open(confirmation, CancelConfirmation);
        }
        public void CancelConfirmation()
        {
            confirmAction = null;
            game.Run.SetModalSuspended(false);
            if (pausePresentation != null) pausePresentation.alpha = 1;
            if (confirmation != null) { confirmation.SetActive(false); Destroy(confirmation); }
            confirmation = null;
        }
    }

}
