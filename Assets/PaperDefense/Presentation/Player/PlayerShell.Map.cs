using System.Collections.Generic;
using System.Linq;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        void Map()
        {
            bool assembly = game.Run.AssemblyCatalog != null;
            var mission = game.Run.CampaignEnabled ? game.Run.SelectedMission : null;
            bool completed = mission != null && game.Run.Campaign.Missions.First(item => item.Definition.Id == mission.Id).IsCompleted;
            bool modernization = game.Run.Session.Balance.Mechanics.FieldModernization;
            var session = game.Run.Session;
            bool flasks = session.Balance.Mechanics.WaveInkFlasks;
            bool desktop = W >= 1120;
            CompactMenuHeader("Карта", () => game.Navigate(mission != null ? PlayerPage.Campaign : PlayerPage.Home),
                mission != null ? desktop ? mission.NotebookTitle : mission.Title : ui.T("mission"),
                "Защити замок до конца всех волн. Направления подхода и свободная земля показаны на карте.\n\n" +
                (mission != null ? MissionBriefingHelp(mission, completed) + "\n\n" + MissionThreats(session.Balance) + "\n\n" : "") +
                (assembly ? "В подготовке выбери до восьми башен и сооружений. Нужна хотя бы одна атакующая башня; свободные места допустимы.\n\n" :
                    "Перед боем выбери четыре разных чертежа в боевой набор.\n\n") +
                (modernization ? "Все башни начинают бой с базовыми параметрами. Цвет — улучшение установленной башни; выбранный цвет сохраняется до конца боя." :
                    "Материал меняет цену и свойства строительства. Генератор строится только карандашом."));
            float margin = Mathf.Min(40, W * .025f), gap = desktop ? 16 : 10;
            float side = desktop ? Mathf.Clamp(W * .31f, 368, 510) : Mathf.Min(368, W * .44f);
            float fieldWidth = W - margin * 2 - side - gap;
            float bodyY = desktop ? 90 : 70, bodyHeight = H - bodyY - margin;
            var map = ui.Panel(page, "Mission map", new Rect(margin, bodyY, fieldWidth, bodyHeight), PlayerUi.Paper);
            float chartY = desktop ? 68 : 8;
            if (desktop)
                MissionSectionTitle(map, mission != null ? mission.Title : ui.T("mission"), new Rect(20, 10, fieldWidth - 40, 50), 32);
            var preview = PlayerUi.Graphic<NeutralMissionMapGraphic>(map, "Mission illustration", new Rect(8, chartY, fieldWidth - 16, bodyHeight - chartY - 60));
            preview.Bind(game.Run.Session);
            var info = ui.Panel(page, "Mission information", new Rect(margin + fieldWidth + gap, bodyY, side, bodyHeight), PlayerUi.Card);
            float pad = desktop ? 18 : 12, width = side - pad * 2;
            var content = ui.Scroll(info, "Mission details", new Rect(pad, desktop ? 16 : 8, width, bodyHeight - (desktop ? 92 : 68)), 426);
            width -= 10;
            MissionSectionTitle(content, "Цель", new Rect(0, 0, width, 28), desktop ? 22 : 18);
            var goal = NotebookLabel(content, "Выдержать все волны.\nЗамок должен уцелеть.", new Rect(0, 27, width, 43), 16);
            goal.name = "Mission objective"; goal.resizeTextMinSize = 16;
            float y = 78, row = desktop ? 38 : 30;
            float metricWidth = desktop ? (width - 8) * .5f : width;
            MissionMetric(content, "Mission waves", PlayerIcon.Flag, WaveCountLabel(session.Balance.Waves.Count), new Rect(0, y, metricWidth, row));
            if (!desktop) y += row;
            MissionMetric(content, "Mission castle health", PlayerIcon.Shield, session.Balance.BaseHealth + " HP замка", new Rect(desktop ? metricWidth + 8 : 0, y, metricWidth, row)); y += row;
            MissionMetric(content, "Mission starting ink", PlayerIcon.Pencil, "Старт · " + session.Balance.StartingInk + " штр.", new Rect(0, y, metricWidth, row));
            if (!desktop) y += row;
            if (assembly)
            {
                string count = "Набор · " + game.Run.Workshop.LoadoutEntries.Count(entry => entry != null) + " / " + ProfileLimits.LoadoutSlotCount;
                MissionMetric(content, "Mission album loadout", PlayerIcon.Book, count, new Rect(desktop ? metricWidth + 8 : 0, y, metricWidth, row));
                if (!desktop) y += row;
            }
            if (desktop) y += row;
            if (mission != null)
            {
                if (desktop) { MissionSectionTitle(content, "Награды", new Rect(0, y + 12, width, 28)); y += 44; }
                // The first-clear receipt can wrap at the compact sidebar width.
                float rewardsHeight = width < 350 ? 168 : 112;
                MissionRewardSummary(content, "Mission", mission, completed, new Rect(0, y + 8, width, rewardsHeight));
                y += rewardsHeight + 16;
            }
            string colorHeading = flasks ? "Между волнами · флакон на выбор" : modernization ? "Цвет башни · в бою" : "Чернильницы";
            var colorTitle = NotebookLabel(content, colorHeading, new Rect(0, y, width, 22), 14, PlayerUi.Muted);
            colorTitle.resizeTextMinSize = 14; y += 24;
            string[] colors = flasks ? new[] { "Красный", "Синий", "Зелёный" } : new[] { "Красная", "Синяя", "Зелёная" };
            string[] effects = { "Орудие наносит периодический урон.", "Орудие замедляет врагов.", "Орудие ослабляет защиту врагов." };
            float cell = width / 3;
            for (int i = 0; i < colors.Length; i++)
            {
                var ink = NotebookMissionArt(content, i + 1, new Rect(i * cell + 10, y, cell - 20, 34));
                contextHints.Tip(ink.gameObject, flasks
                    ? colors[i] + " флакон даёт " + session.InkPerFlask + " чернил. " + effects[i]
                    : colors[i] + " чернильница. " + (modernization
                        ? "Цвет устанавливается на башню во время боя и сохраняется до конца миссии." : "Открывает цвет строительства."));
                var colorName = NotebookLabel(content, colors[i], new Rect(i * cell, y + 35, cell, 20), 14,
                    alignment: TextAnchor.MiddleCenter);
                colorName.resizeTextMinSize = 14;
            }
            y += 64;
            if (desktop) y += MissionOpposition(content, session.Balance, 0, y, width);
            float viewportHeight = content.GetComponentInParent<ScrollRect>().viewport.rect.height;
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(viewportHeight, y));
            if (y <= viewportHeight) content.GetComponentInParent<ScrollRect>().verticalScrollbar.gameObject.SetActive(false);
            CompactMenuAction(info, "Prepare mission", new Rect(12, bodyHeight - 56, side - 24, 44), PlayerIcon.Next,
                "Подготовка", () => game.Navigate(PlayerPage.Preparation), true);
            if (assembly)
                CompactMenuAction(map, "Map album", new Rect(12, bodyHeight - 56, Mathf.Min(160, fieldWidth - 24), 44), PlayerIcon.Book,
                    "Альбом", game.OpenAlbum);
            else
                CompactMenuAction(map, "Map workshop", new Rect(12, bodyHeight - 56, Mathf.Min(160, fieldWidth - 24), 44), PlayerIcon.Pencil,
                    "Мастерская", () => game.Navigate(PlayerPage.Workshop));
        }

        static readonly EnemyKind[] MissionEnemyKinds = (EnemyKind[])System.Enum.GetValues(typeof(EnemyKind));

        static List<(string shape, string name)> MissionEnemies(GameBalanceView balance)
        {
            var enemies = MissionEnemyKinds.Where(kind => balance.Waves.Any(wave => wave.Count > 0 && wave.Composition.Fraction(kind) > 0))
                .Select(kind => (NotebookAnimationState.EnemyId(kind, DrawingTier.Sketch, NotebookEnemyMotion.Idle, balance.EnemyFamily),
                    EnemyFamilyNames.Plural(balance.EnemyFamily, kind).ToLowerInvariant())).ToList();
            foreach (var wave in balance.Waves)
                if (wave.Boss != BossRank.None)
                    enemies.Add((InkSwarmEnemyArt.BossId(balance.EnemyFamily, wave.Boss, NotebookEnemyMotion.Idle),
                        EnemyFamilyNames.BossName(balance.EnemyFamily, wave.Boss)));
            return enemies;
        }

        static string MissionThreats(GameBalanceView balance)
        {
            var enemies = MissionEnemies(balance);
            return enemies.Count == 0 ? "" : EnemyFamilyNames.Title(balance.EnemyFamily) + " · " +
                string.Join(", ", enemies.Select(enemy => enemy.name)) + ".";
        }

        Text MissionSectionTitle(Transform parent, string text, Rect bounds, int size = 20)
        {
            var label = NotebookLabel(parent, text, bounds, size);
            label.fontStyle = FontStyle.Bold;
            return label;
        }

        float MissionOpposition(Transform parent, GameBalanceView balance, float x, float y, float width)
        {
            var enemies = MissionEnemies(balance);
            MissionSectionTitle(parent, EnemyFamilyNames.Title(balance.EnemyFamily), new Rect(x, y, width, 28));
            if (enemies.Count == 0) return 32;
            int columns = Mathf.Min(enemies.Count, Mathf.Max(2, Mathf.FloorToInt(width / 84)));
            float cell = width / columns;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                float left = x + i % columns * cell, top = y + 32 + i / columns * 62;
                ui.Art(parent, enemy.shape, new Rect(left + 4, top, cell - 8, 38));
                NotebookLabel(parent, enemy.name, new Rect(left, top + 38, cell, 22), 14, PlayerUi.Muted, TextAnchor.MiddleCenter);
            }
            return 32 + Mathf.CeilToInt(enemies.Count / (float)columns) * 62;
        }

        void MissionMetric(Transform parent, string name, PlayerIcon icon, string copy, Rect bounds)
        {
            float width = bounds.width, height = bounds.height;
            var row = ui.Panel(parent, name + " row", bounds, Color.clear);
            var symbol = PlayerUi.Graphic<PlayerIconGraphic>(row, "Metric icon", new Rect(2, (height - 20) * .5f, 20, 20));
            symbol.Icon = icon; symbol.Sketch = true; symbol.color = PlayerUi.Ink;
            var caption = NotebookLabel(row, copy, new Rect(32, 0, width - 36, height), 16);
            caption.name = name; caption.resizeTextMinSize = 14;
            var line = PlayerUi.Graphic<Image>(row, "Ink rule", new Rect(0, height - 1, width, 1));
            line.color = PlayerUi.Grid;
        }
    }
}
