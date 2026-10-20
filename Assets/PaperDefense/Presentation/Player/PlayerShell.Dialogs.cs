using System;
using System.Linq;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
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
            var panel = Sheet(assembly ? JourneyCopy("pause.title") : "Пауза", 640, 540, out float width); width -= 48;
            ui.Label(panel, assembly ? JourneyCopy("pause.body") : "Страница подождёт.\nРасстановка и ресурсы остаются на месте.", new Rect(12, 0, width - 24, 70), 22, PlayerUi.Muted);
            ui.Button(panel, "Resume", new Rect(12, 96, width - 24, 56), PlayerIcon.Play, "Продолжить оборону", game.Resume, true);
            ui.Button(panel, "Pause settings", new Rect(12, 166, width - 24, 50), PlayerIcon.Settings, "Настройки", () => game.OpenSettings());
            ui.Button(panel, "Restart battle", new Rect(12, 230, width - 24, 50), PlayerIcon.Restart, "Начать заново", () => Confirm(assembly ? JourneyCopy("pause.restart_title") : "Начать миссию заново?", assembly ? JourneyCopy("pause.restart_body") : "Текущая расстановка и ход этого боя будут потеряны. Сохранённые чертежи и золото останутся.", game.Retry));
            ui.Button(panel, "Leave battle", new Rect(12, 294, width - 24, 50), PlayerIcon.Map, "Вернуться на карту", () => Confirm(assembly ? JourneyCopy("pause.leave_title") : "Завершить эту попытку?", assembly ? JourneyCopy("pause.leave_body") : "Продолжить этот бой позже нельзя. Сохранённые чертежи и золото останутся в профиле.", game.ReturnToMap));
            if (assembly) NotebookLabel(panel, JourneyCopy("pause.resume_hint"), new Rect(12, 368, width - 24, 32), 19, PlayerUi.Muted, TextAnchor.MiddleCenter);
            ScopeSheet(panel, game.Resume);
        }

        void Upgrades()
        {
            var panel = Sheet("Новый штрих к обороне", 1100, 620, out float width); width -= 48;
            bool assembly = game.Run.AssemblyCatalog != null;
            ui.Label(panel, assembly ? JourneyCopy("result.waves", game.Run.Session.CompletedWaves, game.Run.Session.Balance.Waves.Count) + " · Выбери одно усиление" : "Выбери одно усиление на текущую попытку.", new Rect(0, 0, width, 44), 21, PlayerUi.Muted);
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
                if (assembly && !stacked)
                {
                    NotebookLabel(card, ui.T("upgrade_" + i).Replace('\n', ' '), new Rect(16, 10, cardW - 32, 64), 28, alignment: TextAnchor.MiddleCenter);
                    if (i < 2) ui.Art(card, i == 0 ? "ui.weapon.gun" : "ui.weapon.mortar", new Rect(24, 78, cardW - 48, 120));
                    else
                    {
                        var pen = PlayerUi.Graphic<PlayerIconGraphic>(card, "Upgrade symbol", new Rect((cardW - 108) * .5f, 78, 108, 120));
                        pen.Icon = PlayerIcon.Pencil; pen.Sketch = true; pen.color = PlayerUi.Blue;
                    }
                    NotebookLabel(card, details[i], new Rect(24, 210, cardW - 48, 106), 21, PlayerUi.Muted, TextAnchor.MiddleCenter);
                }
                else
                {
                    var icon = PlayerUi.Graphic<PlayerIconGraphic>(card, "Upgrade symbol", new Rect(24, 24, 52, 52)); icon.Icon = icons[i]; icon.color = PlayerUi.Blue;
                    ui.Label(card, ui.T("upgrade_" + i).Replace('\n', ' '), new Rect(stacked ? 96 : 24, stacked ? 24 : 94, cardW - (stacked ? 120 : 48), 84), 26, title: true);
                    ui.Label(card, details[i], new Rect(24, stacked ? 96 : 182, cardW - 48, stacked ? 82 : 130), 19, PlayerUi.Muted);
                }
                ui.Button(card, "Choose upgrade " + i, new Rect(20, stacked ? 184 : 328, cardW - 40, 50), PlayerIcon.Plus, "Выбрать", () => game.ChooseUpgrade((RunUpgrade)selected), true);
            }
            if (assembly) NotebookLabel(panel, JourneyCopy("result.generator_note"), new Rect(0, 468, width, 30), 17, PlayerUi.Muted, TextAnchor.MiddleCenter);
            ScopeSheet(panel, () => { });
        }

        void Result()
        {
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
            if (game.Run.AssemblyCatalog != null) { AlbumHelp(); return; }
            var panel = Sheet("Как держать линию", 1060, 780, out float width); width -= 48;
            string[] titles = { "01  Собери свою башню", "02  Подготовь оборону", "03  Следи за боем" };
            string[] body = {
                "Основа → Оружие → Проверить. Выбери готовые детали, проверь нагрузку и доступность. Испытай башню на полигоне и сохрани чертёж. Рисовать конструкцию не требуется.",
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
            ScopeSheet(panel, game.CloseHelp);
        }

        void AlbumHelp()
        {
            NotebookPageBackdrop("Art/Player/FoundationPageDecor");
            PlayerUi.Graphic<NotebookBindingGraphic>(page, "Help notebook binding", new Rect(0, 0, W, H));
            float margin = W < 1200 ? 48 : W * .115f, width = W - margin * 2, gap = 12;
            HelpAtlasIcon(page, "hero.castle", new Rect(W - margin * 1.25f, H * .22f, margin * 1.45f, H * .32f));
            var paper = ui.Panel(page, "Album help sheet", new Rect(margin, 24, width, H - 48), Color.clear, true);
            PlayerUi.Graphic<NotebookHelpPaperGraphic>(paper, "Help cut paper edge", new Rect(0, 0, width, H - 48));
            NotebookLabel(paper, JourneyCopy("help.title"), new Rect(138, 12, width - 170, 66), 38, alignment: TextAnchor.MiddleCenter);
            var underline = PlayerUi.Graphic<Image>(paper, "Help heading underline", new Rect(width * .32f, 76, width * .42f, 2));
            underline.color = PlayerUi.Blue;
            float logoWidth = Mathf.Clamp(W * .18f, 180, 300);
            var logoPaper = ui.Panel(page, "Help pasted title", new Rect(26, 8, logoWidth, logoWidth * .56f), Color.clear);
            PlayerUi.Graphic<NotebookHelpPaperGraphic>(logoPaper, "Help title paper edge", new Rect(0, 0, logoWidth, logoWidth * .56f));
            HelpAtlasIcon(logoPaper, "logo.home", new Rect(10, 9, logoWidth - 20, logoWidth * .44f));
            NotebookAction(paper, "Help back", new Rect(18, H - 102, 112, 50), PlayerIcon.Back, "Назад", game.CloseHelp);
            if (margin > 90)
                NotebookLabel(page, "Собирай. Строй.\nЗащищай.", new Rect(W - margin * 1.55f, 6, margin * 1.5f - 8, 80), 26,
                    alignment: TextAnchor.MiddleCenter);
            string[] keys = { "collect", "save", "slots", "defend" };
            var helpEntries = game.Run.RunLoadout?.Entries ?? game.Run.Workshop.LoadoutEntries;
            var example = helpEntries.FirstOrDefault(entry => entry?.Kind == AlbumEntryKind.Tower)
                ?? game.Run.AssemblyCatalog.Starters.First(entry => entry.Kind == AlbumEntryKind.Tower);
            float cardWidth = (width - 48 - gap * 3) / 4, cardHeight = Mathf.Clamp((H - 230) * .5f, 260, 300);
            float cardTop = Mathf.Max(120, logoWidth * .56f - 4);
            for (int i = 0; i < 4; i++)
            {
                var card = ui.Panel(paper, "Help step " + i, new Rect(24 + i * (cardWidth + gap), cardTop, cardWidth, cardHeight), PlayerUi.Card);
                NotebookLabel(card, JourneyCopy("help." + keys[i] + "_title"), new Rect(8, 6, cardWidth - 16, 44), 29, alignment: TextAnchor.MiddleCenter);
                var illustration = new Rect(10, 52, cardWidth - 20, 106);
                if (i == 0)
                {
                    var foundation = PlayerUi.Graphic<WeaponAssemblyGraphic>(card, "Help complete foundation", new Rect(24, 100, cardWidth - 48, 62));
                    foundation.SetComposition(AssemblyVisualComposer.ComposeFoundation(example.Snapshot.Recipe.Tower.Foundation));
                    var weapon = example.Snapshot.Recipe.Tower.Mounts[0].Weapon;
                    var mechanism = PlayerUi.Graphic<WeaponAssemblyGraphic>(card, "Help detached mechanism", new Rect(36, 50, cardWidth - 72, 76));
                    mechanism.SetPart(weapon.Mechanism);
                    var sight = PlayerUi.Graphic<WeaponAssemblyGraphic>(card, "Help detached sight", new Rect(8, 76, 44, 44));
                    sight.SetPart(weapon.Sight);
                    var magazine = PlayerUi.Graphic<WeaponAssemblyGraphic>(card, "Help detached magazine", new Rect(cardWidth - 52, 84, 44, 44));
                    magazine.SetPart(weapon.Magazine);
                }
                else if (i == 3)
                {
                    ui.Art(card, "object.castle", new Rect(12, 54, cardWidth - 60, 112));
                    for (int enemy = 0; enemy < 3; enemy++)
                    {
                        HelpAtlasIcon(card, "icon.skull", new Rect(cardWidth - 36, 58 + enemy * 31, 25, 25));
                        var arrow = PlayerUi.Graphic<PlayerIconGraphic>(card, "Help attack arrow " + enemy,
                            new Rect(cardWidth - 58, 64 + enemy * 31, 20, 17));
                        arrow.Icon = PlayerIcon.Back; arrow.Sketch = true; arrow.color = new Color32(203, 57, 51, 255);
                    }
                }
                else if (i == 1)
                {
                    var book = PlayerUi.Graphic<NotebookOpenBookGraphic>(card, "Save album illustration", illustration);
                    PreparationAssemblyIllustration(book.transform, example, new Rect(illustration.width * .10f, 6, illustration.width * .8f, 94));
                }
                else
                {
                    float slotWidth = (cardWidth - 46) / 4;
                    for (int slot = 0; slot < 8; slot++)
                    {
                        var cell = ui.Panel(card, "Help slot " + slot, new Rect(16 + slot % 4 * (slotWidth + 4), 54 + slot / 4 * 47, slotWidth, 42), PlayerUi.Card);
                        NotebookLabel(cell, (slot + 1).ToString(), new Rect(2, 0, 14, 15), 14);
                        var entry = helpEntries[slot] ?? game.Run.AssemblyCatalog.Starters[slot % game.Run.AssemblyCatalog.Starters.Count];
                        PreparationAssemblyIllustration(cell, entry, new Rect(5, 10, slotWidth - 10, 30));
                    }
                }
                var body = NotebookLabel(card, JourneyCopy("help." + keys[i] + "_body"), new Rect(10, 168, cardWidth - 20, cardHeight - 178), 18, PlayerUi.Ink);
                body.resizeTextMinSize = Mathf.CeilToInt(15 / Layout.Scale);
                if (i < 3)
                {
                    var arrow = PlayerUi.Graphic<PlayerIconGraphic>(paper, "Help sequence arrow " + i,
                        new Rect(24 + (i + 1) * cardWidth + i * gap - 3, cardTop + 94, gap + 6, 28));
                    arrow.Icon = PlayerIcon.Next; arrow.Sketch = true; arrow.color = PlayerUi.Blue;
                }
            }
            float infoY = cardTop + cardHeight + 12, infoHeight = H - infoY - 152, infoWidth = (width - 60) * .5f;
            string[] sections = { "resources", "controls" };
            for (int i = 0; i < 2; i++)
            {
                var info = ui.Panel(paper, "Help " + sections[i], new Rect(24 + i * (infoWidth + 12), infoY, infoWidth, infoHeight), PlayerUi.Card);
                NotebookLabel(info, JourneyCopy("help." + sections[i] + "_title"), new Rect(14, 6, infoWidth - 28, 40), 28);
                if (i == 0) HelpResources(info, infoWidth, infoHeight);
                else HelpControls(info, infoWidth, infoHeight);
            }
            NotebookLabel(paper, JourneyCopy("help.slots_footer"), new Rect(20, H - 138, width - 40, 30), 19, PlayerUi.Muted, TextAnchor.MiddleCenter);
            var closeHelp = NotebookAction(paper, "Close help", new Rect((width - 300) * .5f, H - 102, 300, 50), PlayerIcon.Check, "Понятно", game.CloseHelp, true);
            WorkshopModal.Open(paper.gameObject, game.CloseHelp);
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(closeHelp.gameObject);
        }

        void HelpAtlasIcon(Transform parent, string id, Rect bounds)
        {
            NotebookSpriteCatalog.TryGet(id, out var sprite);
            NotebookSprite(parent, "Help " + id + " illustration", sprite, bounds);
        }

        void HelpResources(Transform parent, float width, float height)
        {
            string[] lines = JourneyCopy("help.resources_body").Split('\n');
            float row = (height - 54) / lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                float y = 50 + i * row, iconSize = Mathf.Min(46, row - 3);
                var bounds = new Rect(16, y + (row - iconSize) * .5f, iconSize, iconSize);
                if (i == 0) HelpAtlasIcon(parent, "icon.pencil", bounds);
                else if (i < 3)
                {
                    var emblem = PlayerUi.Graphic<NotebookResultEmblemGraphic>(parent, "Help resource symbol " + i, bounds);
                    emblem.Kind = i == 1 ? NotebookResultEmblem.Coin : NotebookResultEmblem.XpStar;
                    if (i == 2) NotebookLabel(parent, "XP", bounds, 13, PlayerUi.Card, TextAnchor.MiddleCenter);
                }
                else
                {
                    string[] inks = { "red", "blue", "green" };
                    for (int ink = 0; ink < inks.Length; ink++)
                        HelpAtlasIcon(parent, "icon.ink-" + inks[ink], new Rect(12 + ink * 13, y + 2, 17, row - 4));
                }
                var label = NotebookLabel(parent, lines[i], new Rect(70, y, width - 82, row), 19);
                label.resizeTextMinSize = Mathf.CeilToInt(15 / Layout.Scale);
            }
        }

        void HelpControls(Transform parent, float width, float height)
        {
            string[] bindings = JourneyCopy("help.controls_body").Split(new[] { '\n' })
                .SelectMany(line => line.Split(new[] { " · " }, StringSplitOptions.None)).ToArray();
            int rows = Mathf.CeilToInt(bindings.Length / 2f);
            float column = (width - 34) * .5f, row = (height - 54) / rows;
            for (int i = 0; i < bindings.Length; i++)
            {
                string[] pair = bindings[i].Split(new[] { " — " }, StringSplitOptions.None);
                if (pair.Length < 2) continue;
                float x = 12 + i / rows * (column + 10), y = 50 + i % rows * row;
                float keyWidth = pair[0].Length > 5 ? 88 : pair[0].Length > 3 ? 51 : 39;
                float keyHeight = Mathf.Min(34, row - 2);
                var key = ui.Panel(parent, "Help key " + pair[0], new Rect(x, y + (row - keyHeight) * .5f, keyWidth, keyHeight), PlayerUi.Card);
                int keyFont = Mathf.Clamp(Mathf.FloorToInt(keyHeight * .72f), 12, 18);
                var keyLabel = NotebookLabel(key, pair[0], new Rect(2, 0, keyWidth - 4, keyHeight), keyFont, alignment: TextAnchor.MiddleCenter);
                keyLabel.fontSize = keyLabel.resizeTextMaxSize = keyFont;
                keyLabel.resizeTextMinSize = Mathf.Min(10, keyFont);
                keyLabel.resizeTextForBestFit = true;
                var label = NotebookLabel(parent, string.Join(" — ", pair.Skip(1)), new Rect(x + keyWidth + 6, y, column - keyWidth - 6, row), 18);
                label.resizeTextMinSize = Mathf.CeilToInt(14 / Layout.Scale);
            }
        }

        public void Confirm(string title, string message, Action action)
        { confirmTitle = title; confirmMessage = message; confirmAction = action; BuildConfirmation(); }
        void BuildConfirmation()
        {
            if (confirmation != null) { confirmation.SetActive(false); Destroy(confirmation); }
            var shade = ui.Panel(page, "Confirmation shield", new Rect(0, 0, W, H), new Color(.08f, .15f, .25f, .48f), true); confirmation = shade.gameObject;
            float width = Mathf.Min(640, W - 40), height = Mathf.Min(440, H - 40);
            var card = ui.Panel(shade, "Confirmation", new Rect((W - width) / 2, (H - height) / 2, width, height), PlayerUi.Card, true);
            bool leaving = game.Run.AssemblyCatalog != null && confirmTitle == JourneyCopy("pause.leave_title");
            var symbol = PlayerUi.Graphic<PlayerIconGraphic>(card, "Confirmation illustration", new Rect((width - 110) * .5f, 18, 110, 100));
            symbol.Icon = leaving ? PlayerIcon.Map : PlayerIcon.Help; symbol.Sketch = true; symbol.color = PlayerUi.Blue;
            NotebookLabel(card, confirmTitle, new Rect(28, 126, width - 56, 68), 34, alignment: TextAnchor.MiddleCenter);
            NotebookLabel(card, confirmMessage, new Rect(28, 204, width - 56, height - 294), 22, PlayerUi.Ink, TextAnchor.MiddleCenter);
            ui.Button(card, "Cancel confirmation", new Rect(28, height - 78, (width - 68) / 2, 50), PlayerIcon.Back, "Остаться", CancelConfirmation);
            ui.Button(card, "Confirm action", new Rect(width / 2 + 6, height - 78, (width - 68) / 2, 50), PlayerIcon.Next, leaving ? "Завершить бой" : "Продолжить", () => { var action = confirmAction; CancelConfirmation(); action?.Invoke(); }, true);
            WorkshopModal.Open(confirmation, CancelConfirmation);
        }
        public void CancelConfirmation()
        { confirmAction = null; if (confirmation != null) { confirmation.SetActive(false); Destroy(confirmation); } confirmation = null; }
    }

    /// <summary>A loose sheet around live help controls; its torn edge has no hit area.</summary>
    public sealed class NotebookHelpPaperGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear(); var pen = new MeshPen(null, 4096); Rect r = rectTransform.rect;
            if (r.width < 8 || r.height < 8) return;
            const int steps = 15;
            var edge = new Vector2[steps * 4];
            for (int side = 0; side < 4; side++)
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)steps;
                float tear = 2 + (Mathf.Sin(i * 2.7f + side * 4.3f) + 1) * 2.1f;
                edge[side * steps + i] = side == 0 ? new Vector2(r.xMin + t * r.width, r.yMax - tear)
                    : side == 1 ? new Vector2(r.xMax - tear, r.yMax - t * r.height)
                    : side == 2 ? new Vector2(r.xMax - t * r.width, r.yMin + tear)
                    : new Vector2(r.xMin + tear, r.yMin + t * r.height);
            }
            Color shadow = new Color(.20f, .20f, .16f, .18f);
            for (int i = 0; i < edge.Length; i++)
            {
                Vector2 a = edge[i], b = edge[(i + 1) % edge.Length], offset = new Vector2(3, -4);
                pen.Triangle(r.center + offset, a + offset, b + offset, shadow);
            }
            for (int i = 0; i < edge.Length; i++)
            {
                Vector2 a = edge[i], b = edge[(i + 1) % edge.Length];
                pen.Triangle(r.center, a, b, PlayerUi.Card);
                pen.Line(a, b, .8f, new Color(.43f, .42f, .33f, .35f));
            }
            Color rule = PlayerUi.Grid; rule.a *= .65f;
            for (float x = r.xMin + 22; x < r.xMax - 12; x += 20)
                pen.Line(new Vector2(x, r.yMin + 10), new Vector2(x, r.yMax - 10), .6f, rule);
            for (float y = r.yMin + 22; y < r.yMax - 12; y += 20)
                pen.Line(new Vector2(r.xMin + 10, y), new Vector2(r.xMax - 10, y), .6f, rule);
            pen.CopyTo(target, Vector2.zero, 1);
        }
    }
}
