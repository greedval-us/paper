using System;

namespace PaperDefense.Core
{
    internal static class CampaignHordes
    {
        const int W = 1, E = 2, N = 4, S = 8, All = W | E | N | S;

        // Kind weights are percentages; the remaining bodies are doodles. Drawing tiers are
        // rolled separately for every kind and provide actual defense against incoming fire.
        sealed class Roster
        {
            readonly int seconds, runners, gnawers, spitters, brutes, bulwarks, outlined, hatched;

            public Roster(int seconds, int runners = 0, int gnawers = 0, int spitters = 0,
                int brutes = 0, int bulwarks = 0, int outlined = 0, int hatched = 0)
            {
                this.seconds = seconds;
                this.runners = runners;
                this.gnawers = gnawers;
                this.spitters = spitters;
                this.brutes = brutes;
                this.bulwarks = bulwarks;
                this.outlined = outlined;
                this.hatched = hatched;
            }

            public WaveDefinition Wave(int count, int sides, string title) => new WaveDefinition {
                Title = title, Count = count, Sides = sides, SpawnInterval = (float)seconds / count,
                RunnerFraction = runners / 100f, GnawerFraction = gnawers / 100f,
                SpitterFraction = spitters / 100f, BruteFraction = brutes / 100f,
                BulwarkFraction = bulwarks / 100f, OutlinedFraction = outlined / 100f, HatchedFraction = hatched / 100f
            };
        }

        static readonly Roster Opening = new Roster(15);
        static readonly Roster Scouts = new Roster(15, runners: 28);
        static readonly Roster Reserves = new Roster(15, runners: 20);
        static readonly Roster Swarm = new Roster(14, runners: 4);
        static readonly Roster Flood = new Roster(13, runners: 7, gnawers: 4, brutes: 1);
        static readonly Roster Pursuit = new Roster(19, runners: 48, gnawers: 2);
        static readonly Roster Dash = new Roster(17, runners: 64, gnawers: 2);
        static readonly Roster Breach = new Roster(20, runners: 8, gnawers: 18, brutes: 3, outlined: 22, hatched: 8);
        static readonly Roster Escort = new Roster(21, runners: 12, gnawers: 4, bulwarks: 4, outlined: 35, hatched: 15);
        static readonly Roster Convoy = new Roster(23, runners: 5, gnawers: 3, brutes: 3, bulwarks: 9, outlined: 38, hatched: 30);
        static readonly Roster Skirmishers = new Roster(20, runners: 36, spitters: 8, outlined: 12);
        static readonly Roster Battery = new Roster(22, runners: 8, gnawers: 4, spitters: 11, bulwarks: 3, outlined: 26, hatched: 10);
        static readonly Roster LastGuard = new Roster(18, runners: 32, gnawers: 6, spitters: 3,
            brutes: 3, bulwarks: 5, outlined: 29, hatched: 17);

        // Paired entries bind a formation to the actual approach on that wave.
        static readonly (int Sides, Roster Roster)[][] Assaults = {
            new[] { (W, Opening), (E, Scouts), (N | S, Swarm), (All, Reserves) },
            new[] { (N, Pursuit), (N, Swarm), (N, Pursuit), (N, Dash), (N, Flood) },
            new[] { (W, Swarm), (N, Pursuit), (E, Swarm), (S, Scouts), (W | E, Flood), (N | S, Pursuit), (N | W | E, Swarm), (All, Escort) },
            new[] { (W, Swarm), (S, Pursuit), (W, Flood), (W | S, Escort), (W | S, Flood) },
            new[] { (E, Breach), (S, Escort), (E | S, Flood), (S, Breach), (E, Convoy), (E | S, Breach) },
            new[] { (S, Dash), (W, Swarm), (N, Pursuit), (E, Skirmishers), (W | S, Dash), (N | E, Escort), (W | E, Pursuit), (N | S, Flood), (All, Dash) },
            new[] { (N, Skirmishers), (W, Battery), (N | W, Pursuit), (W, Battery), (N, Breach), (W | N, Skirmishers) },
            new[] { (E, Escort), (E, Convoy), (E, Breach), (E, Convoy), (E, Escort), (E, Convoy) },
            new[] { (N, Swarm), (E, Dash), (S, Escort), (W, Battery), (W | E, Pursuit), (N | S, Flood), (N | E, Convoy), (W | S, Dash), (N | W | E, Breach), (All, LastGuard) }
        };

        static readonly int[] FirstCount = { 360, 320, 300, 340, 330, 320, 350, 320, 360 };
        static readonly int[] Growth = { 120, 80, 60, 90, 74, 56, 84, 88, 54 };
        static readonly int[] NorthGates = { 1, 2, 3, 2, 3 };
        static readonly int[] EastGates = { 1, 2, 1, 3, 2, 3 };
        static readonly string[] NorthNames = { "Западная тропа", "Восточная тропа", "Два спуска", "Бегуны на востоке", "Северный обвал" };
        static readonly string[] EastNames = { "Нижние ворота", "Верхние ворота", "Нижняя колонна", "Двойной конвой", "Верхний бастион", "Бронированный клин" };
        static readonly string[] OpeningNames = { "Чернильная орда", "", "Западное плечо", "Западный брод", "Стены редута", "Южное поле", "Северная складка", "", "Северный рубеж" };
        static readonly string[] FinaleNames = { "Кольцо вокруг замка", "", "Перекрестие", "Половодье", "Штурм редута", "Четыре поля", "В сгибе листа", "", "Последнее кольцо" };
        static readonly int[] OpeningRewards = { 14, 20, 26, 40 };
        static readonly int[] OpeningGrants = { 96, 120, 144, 78 };

        public static void Populate(GameBalance balance, CampaignMissionDefinition mission)
        {
            int page = mission.Index;
            var assaults = Assaults[page];
            if (assaults.Length != mission.WaveCount)
                throw new InvalidOperationException("Missing assaults for " + mission.Id);
            for (int i = 0; i < assaults.Length; i++)
            {
                int count = FirstCount[page] + i * Growth[page];
                var assault = assaults[i];
                var wave = assault.Roster.Wave(count, assault.Sides, Title(page, i));
                wave.Horde = new HordeWaveSettings {
                    Enabled = true, BodyScale = .42f, HealthScale = .075f + Math.Max(0, page - 2) * .005f,
                    ObjectiveDamageScale = page == 0 ? (i == 0 ? .06f : i == 3 ? .03f : .035f) : (page == 2 ? .05f : .035f),
                    KillInkBudget = page == 0 ? OpeningRewards[i] : 18 + 4 * i
                };
                wave.InkGrant = page == 0 ? OpeningGrants[i] :
                    Math.Min(280, 96 + page * 8 + i * 12) + (page >= 4 && balance.Mechanics.FieldModernization ? 40 : 0);
                if (page == 1 || page == 7) wave.EntranceBands = (page == 1 ? NorthGates : EastGates)[i];
                balance.Waves.Add(wave);
            }
        }

        static string Title(int page, int wave)
        {
            if (page == 1) return NorthNames[wave];
            if (page == 7) return EastNames[wave];
            if (wave == 0) return OpeningNames[page];
            if (wave == Assaults[page].Length - 1) return FinaleNames[page];
            int side = Assaults[page][wave].Sides;
            if (side == W) return "Западный натиск";
            if (side == E) return "Восточный прорыв";
            if (side == N) return "Северный вал";
            if (side == S) return "Южный натиск";
            return side == (W | E) || side == (N | S) ? "Встречные потоки" : "Охват флангов";
        }

    }
}
