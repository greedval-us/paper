using System;
using System.Linq;
using System.Text;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerShell
    {
        static readonly Guid MissionRewardPreviewId = new Guid("b461e6af-2d9e-48b8-9663-bfdf203dc522");

        static string WaveCountLabel(int count)
        {
            int last = count % 10;
            if (count % 100 >= 11 && count % 100 <= 14) return count + " волн";
            if (last == 1) return count + " волна";
            return count + (last >= 2 && last <= 4 ? " волны" : " волн");
        }

        static CampaignMissionResult MissionRewardQuote(CampaignMissionDefinition mission, bool completed)
            => CampaignMissionResult.Preview(mission, MissionRewardPreviewId, mission.WaveCount, true, 1, !completed);

        void MissionScoutButton(Transform parent, string name, Rect bounds, GameBalanceView balance)
        {
            var first = balance.Waves[0];
            var forecast = first.Forecast;
            string caption = "Волна 1 · " + HordeWaveForecast.Headline(forecast);
            var button = CompactMenuAction(parent, name, bounds, PlayerIcon.Flag, caption, () =>
                UiHelp.Show(page, "Разведка по волнам", HordeWaveForecast.MissionBriefing(balance,
                    wave => HordeWaveForecast.Directions(ui, wave))));
            contextHints.Tip(button.gameObject, "Посмотреть состав всех волн\n" + HordeWaveForecast.Advice(forecast, balance.Mechanics));
        }

        void MissionRewardSummary(Transform parent, string prefix, CampaignMissionDefinition mission, bool completed, Rect bounds)
        {
            var quote = MissionRewardQuote(mission, completed);
            var ordinary = MissionRewardQuote(mission, true);
            var root = ui.Panel(parent, prefix + " rewards", bounds, Color.clear);
            float rowHeight = bounds.height / 4;
            string breakdown = MissionRewardBreakdown(mission, completed);
            RewardRow("ordinary", PlayerIcon.Trophy, "Обычная победа: " + ordinary.Gold + " зол. · " + ordinary.TotalXp + " XP", 0, PlayerUi.Ink, breakdown);
            RewardRow("first", completed ? PlayerIcon.Check : PlayerIcon.Plus,
                completed ? "Первый бонус уже получен" : "Первый бонус: +" + quote.FirstClearGold + " зол. · +" + quote.FirstClearXp + " XP", 1,
                completed ? PlayerUi.Muted : PlayerUi.Ink, breakdown);
            RewardRow("total", PlayerIcon.Check, "Итого при победе: " + quote.Gold + " зол. · " + quote.TotalXp + " XP", 2, PlayerUi.Blue, breakdown);
            RewardRow("unlocks", PlayerIcon.Book, MissionUnlockSummary(mission, completed), 3, PlayerUi.Muted, MissionUnlockDetails(mission, completed));

            void RewardRow(string id, PlayerIcon icon, string copy, int index, Color color, string hint)
            {
                var glyph = PlayerUi.Graphic<PlayerIconGraphic>(root, prefix + " reward " + id + " icon", new Rect(0, index * rowHeight + (rowHeight - 18) * .5f, 18, 18));
                glyph.Icon = icon; glyph.Sketch = true; glyph.color = color;
                var label = ui.Label(root, copy, new Rect(26, index * rowHeight, bounds.width - 26, rowHeight), 14, color);
                label.name = prefix + " reward " + id;
                contextHints.Tip(label.gameObject, hint);
            }
        }

        string MissionRewardBreakdown(CampaignMissionDefinition mission, bool completed)
        {
            var ordinary = MissionRewardQuote(mission, true);
            var available = MissionRewardQuote(mission, completed);
            return "За завершённую волну: " + mission.GoldPerWave + " зол. · " + mission.XpPerWave + " XP\n" +
                "За волны (" + mission.WaveCount + "): " + ordinary.WaveGold + " зол. · " + ordinary.WaveXp + " XP\n" +
                "Бонус победы: " + ordinary.CompletionGold + " зол. · " + ordinary.CompletionXp + " XP\n" +
                (completed ? "Первый бонус уже получен; повторно не начисляется." : "Первый бонус: +" + available.FirstClearGold + " зол. · +" + available.FirstClearXp + " XP") +
                "\nИтого за полную победу в новой попытке: " + available.Gold + " зол. · " + available.TotalXp + " XP\n" +
                "При поражении остаются награды только за завершённые волны. Победный и первый бонусы требуют победы; открытия публикуются после успешного сохранения.";
        }

        string MissionUnlockSummary(CampaignMissionDefinition mission, bool completed)
        {
            if (completed) return "Открытия первого прохождения получены";
            int count = mission.UnlockPartIds.Count(id => !game.Run.Workshop.IsUnlocked(id));
            int technology = MissionTechnologyUnlock(mission);
            bool newTechnology = technology > game.Run.UnlockedModernizationRank;
            string parts = count == 0 ? "Детали уже открыты" : "Открытия: " + count + (count == 1 ? " деталь" : count < 5 ? " детали" : " деталей");
            if (newTechnology) return parts + " · Тех. " + (technology == 2 ? "II" : "III");
            return parts + (CampaignCatalog.Default.Next(mission.Id) == null ? " · финал" : " · след. страница");
        }

        string MissionUnlockDetails(CampaignMissionDefinition mission, bool completed)
        {
            var text = new StringBuilder(completed ? "Первое прохождение завершено. Повторная победа не повторяет открытия." : "За первую сохранённую победу:");
            var definitions = game.Run.AssemblyCatalog?.Definitions;
            foreach (string id in mission.UnlockPartIds)
            {
                var part = definitions?.FirstOrDefault(item => item.Id == id);
                string name = part == null ? "Деталь недоступна в текущем каталоге" : GameText.Resolve(part.Name);
                text.Append("\n• ").Append(name).Append(game.Run.Workshop.IsUnlocked(id) ? " — уже открыта" : completed ? " — проверь каталог" : " — открывается");
            }
            int technology = MissionTechnologyUnlock(mission);
            if (technology > 1)
                text.Append("\nТехнология ").Append(technology == 2 ? "II" : "III")
                    .Append(game.Run.UnlockedModernizationRank >= technology ? " уже открыта." : " открывается после сохранённой победы.")
                    .Append(" Применяется с новой боевой попытки.");
            var next = CampaignCatalog.Default.Next(mission.Id);
            text.Append(next == null ? "\nЭто последняя страница кампании." : "\nСледующая страница: «" + next.Title + "». Победы с любой оценкой достаточно.");
            return text.ToString();
        }

        static int MissionTechnologyUnlock(CampaignMissionDefinition mission)
            => mission.Id == "notebook-1-page-1" ? 2 : mission.Id == "notebook-1-page-3" ? 3 : 1;

        string MissionBriefingHelp(CampaignMissionDefinition mission, bool completed)
            => mission.Description + "\n\n" + mission.Tactics + "\n\nЦель: сохранить замок до конца миссии. Длительность — " + WaveCountLabel(mission.WaveCount) + ".\n\n" +
                MissionRewardBreakdown(mission, completed) + "\n\n" + MissionUnlockDetails(mission, completed) +
                "\n\nОценка зависит от оставшегося HP замка: 3 звезды — от 90%, 2 — от 60%, 1 — за победу с меньшим запасом. Для следующей страницы достаточно любой победы. Штрихи — ресурс текущего боя; золото и XP — награды профиля.";
    }
}
