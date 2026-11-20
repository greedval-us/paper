using System;
using System.Text;
using PaperDefense.Core;

namespace PaperDefense.Presentation
{
    internal static class HordeWaveForecast
    {
        static int Percent(float fraction) => (int)Math.Round(100 * fraction);

        internal static string Headline(WaveThreatForecast forecast, EnemyFamily family)
        {
            switch (forecast.Primary)
            {
                case WaveThreat.Armor: return "Броня ≈" + Percent(forecast.PrimaryFraction) + "%";
                case WaveThreat.Runners: return EnemyFamilyNames.Plural(family, EnemyKind.Runner) + " ≈" + Percent(forecast.PrimaryFraction) + "%";
                case WaveThreat.Breach: return EnemyFamilyNames.Plural(family, EnemyKind.Gnawer) + " ≈" + Percent(forecast.PrimaryFraction) + "%";
                case WaveThreat.Ranged: return (forecast.OnlySpittersFireGlobs ? EnemyFamilyNames.Plural(family, EnemyKind.Spitter) : "Стрелки") +
                    " ≈" + Percent(forecast.PrimaryFraction) + "%";
                default: return "Плотная толпа";
            }
        }

        internal static string Advice(WaveThreatForecast forecast, GameBalanceView balance)
            => Advice(forecast.Primary, balance, forecast.OnlySpittersFireGlobs);

        static string Advice(WaveThreat threat, GameBalanceView balance, bool onlySpitters)
        {
            switch (threat)
            {
                case WaveThreat.Armor: return "Пробивная пушка обходит половину брони." +
                    (balance.Mechanics.ColorCartridges ? " Зелёная краска ослабляет защиту." : "");
                case WaveThreat.Runners: return "Повысь темп огня пулемёта. Быстрый указатель ускорит наведение.";
                case WaveThreat.Breach: return "Прикрой стены огнём и поставь за ними ремонтный пост.";
                case WaveThreat.Ranged:
                    string target = onlySpitters ? balance.EnemyFamily switch
                    {
                        EnemyFamily.Undead => "звонарей",
                        EnemyFamily.Clockwork => "метателей",
                        _ => "плевунов"
                    } : "стрелков";
                    return "Увеличь дальность, чтобы достать " + target + " за передним рядом.";
                default: return "Поставь мортиру у прохода. Широкий ствол увеличит радиус взрыва.";
            }
        }

        internal static string MissionBriefing(GameBalanceView balance, Func<WaveView, string> direction)
        {
            var text = new StringBuilder(EnemyFamilyNames.Title(balance.EnemyFamily)).AppendLine();
            var threats = new System.Collections.Generic.List<WaveThreat>();
            bool onlySpitters = true;
            for (int i = 0; i < balance.Waves.Count; i++)
            {
                var wave = balance.Waves[i];
                if (!wave.IsHorde) continue;
                var forecast = wave.Forecast;
                text.Append(i + 1).Append(". ").Append(wave.Count).Append(" ").Append(direction(wave))
                    .Append(" · ").Append(Headline(forecast, balance.EnemyFamily));
                int runners = Percent(forecast.RunnerFraction), armor = Percent(forecast.ArmoredFraction);
                var threat = forecast.Primary;
                if (threat == WaveThreat.Crowd && wave.Count > 0)
                    text.Append(" · до ").Append((int)Math.Round(forecast.MaximumArrivalsPerSecond)).Append(" врагов/с");
                if (runners >= 20 && threat != WaveThreat.Runners)
                    text.Append(" · ").Append(EnemyFamilyNames.Plural(balance.EnemyFamily, EnemyKind.Runner).ToLowerInvariant())
                        .Append(" ≈").Append(runners).Append("%");
                if (armor >= 20 && threat != WaveThreat.Armor) text.Append(" · броня ≈").Append(armor).Append("%");
                if (wave.Boss != BossRank.None) text.Append(" · Босс ").Append(EnemyFamilyNames.BossName(balance.EnemyFamily, wave.Boss));
                text.AppendLine();
                if (!threats.Contains(threat)) threats.Add(threat);
                if (threat == WaveThreat.Ranged && !forecast.OnlySpittersFireGlobs) onlySpitters = false;
            }
            foreach (var threat in threats) text.AppendLine().Append(Advice(threat, balance, onlySpitters)).AppendLine();
            return text.ToString().TrimEnd();
        }

        // WaveIndex counts launched waves. While building, the next index is the one to forecast.
        internal static int DisplayedSides(IRunReadModel session) => DisplayedWave(session).Sides;
        internal static int DisplayedBands(IRunReadModel session) => DisplayedWave(session).EntranceBands;

        internal static string Directions(PlayerUi ui, WaveView wave)
        {
            int sides = wave.Sides & 15;
            if (wave.EntranceBands == 1 || wave.EntranceBands == 2)
            {
                bool first = wave.EntranceBands == 1;
                switch (sides)
                {
                    case 1: return ui.T(first ? "horde_west_lower" : "horde_west_upper");
                    case 2: return ui.T(first ? "horde_east_lower" : "horde_east_upper");
                    case 4: return ui.T(first ? "horde_north_left" : "horde_north_right");
                    case 8: return ui.T(first ? "horde_south_left" : "horde_south_right");
                }
            }
            if (sides == 15) return ui.T("horde_all_sides");
            var names = new System.Collections.Generic.List<string>(3);
            for (int side = 0; side < 4; side++)
                if ((sides & (1 << side)) != 0) names.Add(ui.T("horde_side_" + side));
            return ui.T("horde_from", string.Join(ui.T("horde_and"), names));
        }

        internal static WaveView DisplayedWave(IRunReadModel session)
        {
            if (session.Phase == RunPhase.Victory || session.Phase == RunPhase.Defeat) return default;
            int index = session.WaveIndex - (session.Phase == RunPhase.Assault ? 1 : 0);
            var waves = session.Balance.Waves;
            return index >= 0 && index < waves.Count && waves[index].IsHorde ? waves[index] : default;
        }
    }
}
