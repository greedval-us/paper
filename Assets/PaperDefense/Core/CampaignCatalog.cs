using System;
using System.Collections.Generic;
using System.Linq;

namespace PaperDefense.Core
{
    public sealed class CampaignMissionDefinition
    {
        public string Id
        { get; }
        public string NotebookId => "notebook-" + NotebookIndex;
        public EnemyFamily EnemyFamily => NotebookIndex switch
        {
            1 => EnemyFamily.Ink,
            2 => EnemyFamily.Undead,
            3 => EnemyFamily.Clockwork,
            _ => throw new ArgumentOutOfRangeException(nameof(NotebookIndex))
        };
        public string NotebookTitle
        { get; }
        public string Title
        { get; }
        public string Subtitle
        { get; }
        public string Description
        { get; }
        public string Tactics
        { get; }
        public int NotebookIndex
        { get; }
        public int PageIndex
        { get; }
        public int WaveCount
        { get; }
        public int Index => (NotebookIndex - 1) * 3 + PageIndex - 1;
        public int Revision => 1;
        public int Seed => 4109 + Index * 7919;
        public IReadOnlyList<string> UnlockPartIds
        { get; }
        public int GoldPerWave => 6 + NotebookIndex;
        public int XpPerWave => 8 + NotebookIndex * 2;
        public int CompletionGold => 10 + NotebookIndex * 5 + (PageIndex == 3 ? 10 : 0);
        public int CompletionXp => 15 + NotebookIndex * 5;
        public int FirstClearGold => 20 + Index * 5;
        public int FirstClearXp => 20 + Index * 5;
        internal CampaignMissionDefinition(int notebook, int page, int waves, string book, string title,
            string subtitle, string description, string tactics, params string[] unlocks)
        {
            NotebookIndex = notebook;
            PageIndex = page;
            WaveCount = waves;
            NotebookTitle = book;
            Id = "notebook-" + notebook + "-page-" + page;
            Title = title;
            Subtitle = subtitle;
            Description = description;
            Tactics = tactics;
            UnlockPartIds = Array.AsReadOnly(unlocks);
        }
    }

    public sealed class CampaignCatalog
    {
        public static CampaignCatalog Default { get; } = new CampaignCatalog();
        public IReadOnlyList<CampaignMissionDefinition> Missions
        { get; }
        CampaignCatalog()
        {
            Missions = Array.AsReadOnly(new[] {
                new CampaignMissionDefinition(1,1,4,"Первая линия","Первый разворот","Четыре орды · оборона замка",
                    "Первый удар придёт с запада, второй — с востока. Затем орды сомкнутся с севера и юга, а в финале пойдут со всех четырёх сторон.","Перед каждой волной проверь направления атаки. Прикрой новый фланг дозором и поставь мортиру так, чтобы она накрывала скопление перед стеной.","head.long","sight.snap"),
                new CampaignMissionDefinition(1,2,5,"Первая линия","Длинный прострел","Северный перевал · два входа",
                    "Замок стоит у нижнего края листа. Орды спускаются с севера по двум сторонам каменной гряды.","Следи за стрелками на верхнем краю. Простреливай колонны вдоль спуска и оставь второй сектор там, где обе дороги сходятся у замка.","sight.scope","sight.survey"),
                new CampaignMissionDefinition(1,3,8,"Первая линия","Край страницы","Крест дорог · четыре рукава",
                    "Четыре скальных массива оставляют к замку проходы в форме креста. Орды меняют рукава от волны к волне.","Пробивная пушка простреливает целый рукав. Мортирой на перекрёстке можно накрыть тех, кто миновал первую баррикаду.","core.fan","head.fan","head.fan.focus"),
                new CampaignMissionDefinition(2,1,5,"Полевые заметки","Чернильный ручей","Чернильная река · две переправы",
                    "Замок на восточном берегу. Нежить с запада и юга добирается до него по двум мостам.","Поставь замедление на выходе с моста. Прикрой обе переправы так, чтобы мортира била по скоплению на дальнем берегу.","core.stapler","head.stapler","trigger.economy"),
                new CampaignMissionDefinition(2,2,6,"Полевые заметки","Живой редут","Полевой редут · бреши в стенах",
                    "С юга и востока к замку подходят осадные отряды. У редута уцелели стены, но между ними открыты бреши.","Выдвинь дозор к южным воротам, а восточную брешь прикрой огнём сбоку. Оставь место за укреплениями для ремонта.","shell.light","feed.boost","hull.ribbed","frame.outpost"),
                new CampaignMissionDefinition(2,3,9,"Полевые заметки","Четыре поля","Четыре поля · живые изгороди",
                    "Сады и изгороди делят лист на четыре поля. С разных краёв к замку ведут отдельные просеки.","Ставь орудия у концов изгородей. Следующую батарею направляй в открывающуюся просеку.","core.ricochet","head.ricochet","head.ricochet.long","magazine.quick"),
                new CampaignMissionDefinition(3,1,6,"Чернильный архив","Складка","Косая складка · два седла",
                    "Гряда пересекает лист наискось. Колонны механизмов идут с запада и севера к замку внизу справа.","Прикрой разрывы гряды с внутренней стороны. Скорострельная башня у замка подберёт пружинников, проскочивших под первым залпом.","core.blotter","head.blotter","head.absorb"),
                new CampaignMissionDefinition(3,2,6,"Чернильный архив","Глухая печать","Каменные ворота · восточные колонны",
                    "Панцирники выходят из двух восточных ущелий. За каменным мысом их дороги сходятся к замку на западе.","Поставь пробивное орудие вдоль атакуемого ущелья. Баррикада у западного конца мыса задержит броню на линии огня.","armor.plate","turn.quick","turn.deliberate","magazine.reserve"),
                new CampaignMissionDefinition(3,3,10,"Чернильный архив","Последний разворот","Цитадель · два кольца обороны",
                    "Два старых кольца укреплений стоят между архивом и ордами. У каждого кольца свои проломы, а подходы идут со всех четырёх сторон.","Прикрой проломы наружной стены. Внутренние орудия направь поперёк проходов, чтобы они подхватили врагов, прорвавшихся через первый рубеж.","frame.heavy","frame.siege")
            });
        }
        public CampaignMissionDefinition Find(string id) => Missions.FirstOrDefault(mission => mission.Id == id);
        public CampaignMissionDefinition Next(string id)
        {
            var mission = Find(id);
            return mission != null && mission.Index + 1 < Missions.Count ? Missions[mission.Index + 1] : null;
        }
    }

    public static class MissionBalanceFactory
    {
        static readonly int[] OriginalOpeningInk = { 220, 244, 268, 440, 520, 720, 780, 840, 960 };
        // Extra construction ink buys three, four or five starter guards across the notebooks.
        static readonly int[] NotebookOpeningBonus = { 120, 160, 200 };

        public static GameBalance Create(GameBalance baseline, CampaignMissionDefinition mission)
        {
            if (baseline == null || mission == null)
                throw new ArgumentNullException();
            var b = baseline.CreateSnapshot();
            int index = mission.Index;
            b.EnemyFamily = mission.EnemyFamily;
            b.Width = 96;
            b.Height = 64;
            // New approach directions and ranged pressure need a paid opening battery.
            // No free starter entities: these budgets are exercised by CampaignPlayabilityScenario.
            b.StartingInk = OriginalOpeningInk[index] + NotebookOpeningBonus[mission.NotebookIndex - 1];
            b.PlanningSeconds = 45;
            b.IntermissionSeconds = 28;
            b.WaveHealthGrowth = .045f + index * .006f;
            b.WaveGold = mission.GoldPerWave;
            b.VictoryGold = mission.CompletionGold;
            b.Progression.LastUpgradeWave = Math.Max(1, mission.WaveCount - 1);
            b.WaveInkGrant = 48 + index * 5;
            b.Battlefield = CampaignBattlefields.Create(b, index);
            b.Waves.Clear();
            CampaignHordes.Populate(b, mission);
            CampaignHordes.FundReinforcements(b, mission, b.ResolveBattlefield());
            BalanceValidation.Validate(b);
            return b;
        }

        /// <summary>Reconstructs the balance that wrote a checkpoint before the new opening budgets.</summary>
        public static GameBalance CreatePreviousOpeningSnapshot(GameBalance current, CampaignMissionDefinition mission)
        {
            var previous = current.CreateSnapshot();
            previous.StartingInk = OriginalOpeningInk[mission.Index];
            return previous;
        }

        /// <summary>Reconstructs the earlier campaign payouts and wave timing for checkpoint verification.</summary>
        public static GameBalance CreatePreviousReinforcementSnapshot(GameBalance current, CampaignMissionDefinition mission)
        {
            var previous = CreatePreviousOpeningSnapshot(current, mission);
            previous.IntermissionSeconds = 16;
            for (int i = 0; i < previous.Waves.Count; i++)
                previous.Waves[i].InkGrant = CampaignHordes.PreviousGrant(previous, mission.Index, i);
            if (mission.Index == 2) previous.Waves[1].SpawnInterval = 19f / previous.Waves[1].Count;
            return previous;
        }

    }
}
