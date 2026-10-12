using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PaperDefense.Core
{
    /// <summary>Stable part identity and role. Numeric tuning remains in TowerConstructionRules.</summary>
    public class WorkshopPartDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public string ButtonLabel { get; }
        public string UnlockLabel { get; }
        public bool InitiallyUnlocked { get; }
        public bool AvailableInVersionTwo { get; }
        public bool IsCombatMount { get; }
        public StructureKind? MountKind { get; }
        public TowerModules Module { get; }
        public ChassisKind? Chassis { get; }
        public PartCategory Category { get; }
        public bool Enabled { get; } = true;
        public int Order { get; }
        public int UnlockGold { get; }
        public string TemplateId { get; }
        public DrawingVisualStyle VisualStyle { get; }

        protected WorkshopPartDefinition(PartDefinitionInput input, PartCategory category, bool combatMount = false,
            StructureKind? mountKind = null, TowerModules module = TowerModules.None, ChassisKind? chassis = null)
        {
            if (input == null) throw new ArgumentException("catalog: пустая запись");
            Id = ContentValidation.Id(input.Id,"part.id");
            if (string.IsNullOrWhiteSpace(input.Name)) throw new ArgumentException(Id + ".name: требуется имя");
            ContentValidation.NonNegative(input.UnlockGold,Id + ".unlockGold");
            Name=input.Name;Description=input.Description ?? "";ButtonLabel=input.ButtonLabel ?? input.Name;UnlockLabel=input.UnlockLabel ?? input.Name;
            TemplateId=input.TemplateId;Enabled=input.Enabled;Order=input.Order;UnlockGold=input.UnlockGold;InitiallyUnlocked=input.InitiallyUnlocked;VisualStyle=input.VisualStyle;
            Category=category;IsCombatMount=combatMount;MountKind=mountKind;Module=module;Chassis=chassis;
        }

        internal WorkshopPartDefinition(string id, string name, string buttonLabel, string unlockLabel, string description,
            bool initiallyUnlocked = false, bool availableInVersionTwo = false, bool combatMount = false,
            StructureKind? mountKind = null, TowerModules module = TowerModules.None, ChassisKind? chassis = null)
        {
            Id = id; Name = name; ButtonLabel = buttonLabel; UnlockLabel = unlockLabel; Description = description;
            InitiallyUnlocked = initiallyUnlocked; AvailableInVersionTwo = availableInVersionTwo; IsCombatMount = combatMount;
            MountKind = mountKind; Module = module; Chassis = chassis;
            Category = chassis.HasValue ? PartCategory.Chassis : module != TowerModules.None ? PartCategory.Module : PartCategory.Weapon;
        }
    }

    public static class PartCatalog
    {
        static readonly ReadOnlyCollection<WorkshopPartDefinition> definitions = Array.AsReadOnly(new[]
        {
            new WorkshopPartDefinition("chassis.standard", "Обычное основание", "ОБЫЧНОЕ", "ОБЫЧНОЕ",
                "Два крепления для деталей с общей ограниченной нагрузкой.", initiallyUnlocked: true, availableInVersionTwo: true, chassis: ChassisKind.Standard),
            new WorkshopPartDefinition("weapon.gun", "Пулемёт", "ПУЛЕМЁТ", "ПУЛЕМЁТ",
                "Частый одиночный огонь. Каждое попадание поражает одну цель.", initiallyUnlocked: true, availableInVersionTwo: true, combatMount: true, mountKind: StructureKind.Gun),
            new WorkshopPartDefinition("weapon.piercer", "Пробивающая пушка", "ПРОБОЙ", "ПРОБОЙ",
                "Линейный выстрел поражает конечное число целей; защита учитывается.", initiallyUnlocked: true, availableInVersionTwo: true, combatMount: true, mountKind: StructureKind.Piercer),
            new WorkshopPartDefinition("weapon.mortar", "Мортира", "МОРТИРА", "МОРТИРА",
                "Удар по зафиксированной точке с задержкой. Быстрая цель может уйти.", initiallyUnlocked: true, availableInVersionTwo: true, combatMount: true, mountKind: StructureKind.Mortar),
            new WorkshopPartDefinition("module.armor", "Бронепластины", "БРОНЯ", "БРОНЯ",
                "Повышают стойкость основания к вражескому урону.", initiallyUnlocked: true, availableInVersionTwo: true, module: TowerModules.Armor),
            new WorkshopPartDefinition("chassis.heavy", "Тяжёлое основание", "ТЯЖЁЛОЕ", "ТЯЖЁЛОЕ",
                "Вмещает большую нагрузку; дороже и дольше строится.", availableInVersionTwo: true, chassis: ChassisKind.Heavy),
            new WorkshopPartDefinition("module.feed", "Улучшенная подача", "ПОДАЧА", "ПОДАЧА",
                "Увеличивает общую производительность подачи к оружию.", availableInVersionTwo: true, module: TowerModules.Feed),
            new WorkshopPartDefinition("module.tracking", "Быстрое наведение", "НАВЕДЕНИЕ", "НАВЕДЕНИЕ",
                "Сокращает время захвата новой цели.", availableInVersionTwo: true, module: TowerModules.Tracking),
            new WorkshopPartDefinition("weapon.fan", "Веерный ствол", "ВЕЕР", "ВЕЕР",
                "Короткий широкий конус с ограниченным числом целей. Требует близкого размещения.", combatMount: true, mountKind: StructureKind.Fan),
            new WorkshopPartDefinition("weapon.ricochet", "Рикошет", "РИКОШЕТ", "РИКОШЕТ",
                "Цепочка попаданий между близкими целями. Каждая цель поражается один раз; защита учитывается.", combatMount: true, mountKind: StructureKind.Ricochet),
            new WorkshopPartDefinition("weapon.stapler", "Степлер", "СКРЕПКИ", "СКРЕПКИ",
                "Небольшой урон и собственное замедление цели. С синими чернилами действует сильнейшее замедление.", combatMount: true, mountKind: StructureKind.Stapler),
            new WorkshopPartDefinition("weapon.blotter", "Промокашка", "ПРОМОКАШКА", "ПРОМОКАШКА",
                "Автоматически очищает кляксы в выбранной области. Не наносит урон врагам и действует отдельно от ручной промокашки.", mountKind: StructureKind.Blotter),
            new WorkshopPartDefinition("module.rail", "Направляющая", "НАПРАВЛ.", "НАПРАВЛЯЮЩАЯ",
                "Увеличивает дальность установленных деталей, но увеличивает интервал между действиями. Занимает нагрузку основания.", module: TowerModules.Rail)
        });
        static readonly ReadOnlyCollection<PartUnlock> historicalParts = CreateParts(new TowerConstructionRules());

        public static IReadOnlyList<WorkshopPartDefinition> All => definitions;
        public static IReadOnlyList<PartUnlock> HistoricalParts => historicalParts;
        public static bool TryGet(string id, out WorkshopPartDefinition definition)
        {
            for (int i = 0; i < definitions.Count; i++)
                if (definitions[i].Id == id) { definition = definitions[i]; return true; }
            definition = null; return false;
        }
        public static bool TryGetMount(StructureKind kind, out WorkshopPartDefinition definition)
        {
            for (int i = 0; i < definitions.Count; i++)
                if (definitions[i].MountKind == kind) { definition = definitions[i]; return true; }
            definition = null; return false;
        }
        public static bool TryGetModule(TowerModules module, out WorkshopPartDefinition definition)
        {
            if (module != TowerModules.None)
                for (int i = 0; i < definitions.Count; i++)
                    if (definitions[i].Module == module) { definition = definitions[i]; return true; }
            definition = null; return false;
        }
        public static bool TryGetChassis(ChassisKind chassis, out WorkshopPartDefinition definition)
        {
            for (int i = 0; i < definitions.Count; i++)
                if (definitions[i].Chassis == chassis) { definition = definitions[i]; return true; }
            definition = null; return false;
        }
        public static string GetMountId(StructureKind kind) => TryGetMount(kind, out var definition) ? definition.Id : null;
        public static bool IsHistorical(string id) => TryGet(id, out var definition) && definition.AvailableInVersionTwo;

        public static int GetUnlockGold(WorkshopPartDefinition definition, TowerConstructionRules rules)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            switch (definition.Id)
            {
                case "chassis.heavy": return rules.HeavyUnlockGold;
                case "module.feed": return rules.FeedUnlockGold;
                case "module.tracking": return rules.TrackingUnlockGold;
                case "weapon.fan": return rules.FanUnlockGold;
                case "weapon.ricochet": return rules.RicochetUnlockGold;
                case "weapon.stapler": return rules.StaplerUnlockGold;
                case "weapon.blotter": return rules.BlotterUnlockGold;
                case "module.rail": return rules.RailUnlockGold;
                default: return 0;
            }
        }

        public static IReadOnlyList<PartUnlock> CreateParts(GameBalance balance)
        {
            if (balance?.Content == null) return balance == null ? historicalParts : CreateParts(balance.Construction);
            var result = new List<PartUnlock>();
            foreach (var item in balance.Content.AllParts) if (item.Enabled) result.Add(new PartUnlock(item.Id,item.Name,item.UnlockGold,item.Description));
            return result.AsReadOnly();
        }
        static ReadOnlyCollection<PartUnlock> CreateParts(TowerConstructionRules rules)
        {
            var result = new PartUnlock[definitions.Count];
            for (int i = 0; i < result.Length; i++)
            {
                var item = definitions[i];
                result[i] = new PartUnlock(item.Id, item.Name, GetUnlockGold(item, rules), item.Description);
            }
            return Array.AsReadOnly(result);
        }
    }
}
