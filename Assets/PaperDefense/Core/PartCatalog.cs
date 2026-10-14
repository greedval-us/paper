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
            if (input == null) throw new ArgumentException(UserText.Message("copy.0d4d7b289ab81920"));
            Id = ContentValidation.Id(input.Id,"part.id");
            if (string.IsNullOrWhiteSpace(input.Name)) throw new ArgumentException(UserText.Message("copy.6fae05a12b0b9c9c", Id));
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
            new WorkshopPartDefinition("chassis.standard", UserText.Message("copy.23594dc19f042e25"), UserText.Message("copy.9a506c0c03ef87ad"), UserText.Message("copy.9a506c0c03ef87ad"),
                UserText.Message("copy.dce2c709ff0364d2"), initiallyUnlocked: true, availableInVersionTwo: true, chassis: ChassisKind.Standard),
            new WorkshopPartDefinition("weapon.gun", UserText.Message("copy.0455469e8acac5ad"), UserText.Message("copy.44a0adab8741198c"), UserText.Message("copy.44a0adab8741198c"),
                UserText.Message("copy.97e0e9a86383c954"), initiallyUnlocked: true, availableInVersionTwo: true, combatMount: true, mountKind: StructureKind.Gun),
            new WorkshopPartDefinition("weapon.piercer", UserText.Message("copy.bdf4c03725c66109"), UserText.Message("copy.ff6266cb2f14b37e"), UserText.Message("copy.ff6266cb2f14b37e"),
                UserText.Message("copy.9af795787dec868e"), initiallyUnlocked: true, availableInVersionTwo: true, combatMount: true, mountKind: StructureKind.Piercer),
            new WorkshopPartDefinition("weapon.mortar", UserText.Message("copy.fa0b7d5e4f6c9cf5"), UserText.Message("copy.560aad97684eb06d"), UserText.Message("copy.560aad97684eb06d"),
                UserText.Message("copy.5e380659f0e02a3c"), initiallyUnlocked: true, availableInVersionTwo: true, combatMount: true, mountKind: StructureKind.Mortar),
            new WorkshopPartDefinition("module.armor", UserText.Message("copy.e41e849ff6126cff"), UserText.Message("copy.bdb837326a8ee7a4"), UserText.Message("copy.bdb837326a8ee7a4"),
                UserText.Message("copy.67c0fbfcdc72c470"), initiallyUnlocked: true, availableInVersionTwo: true, module: TowerModules.Armor),
            new WorkshopPartDefinition("chassis.heavy", UserText.Message("copy.f02c63429561970f"), UserText.Message("copy.142451a857d3af5c"), UserText.Message("copy.142451a857d3af5c"),
                UserText.Message("copy.a960b7a201d90dc3"), availableInVersionTwo: true, chassis: ChassisKind.Heavy),
            new WorkshopPartDefinition("module.feed", UserText.Message("copy.cb15511fd2171fe6"), UserText.Message("copy.6e4cee2bfd96ebbf"), UserText.Message("copy.6e4cee2bfd96ebbf"),
                UserText.Message("copy.85e8357ef616e683"), availableInVersionTwo: true, module: TowerModules.Feed),
            new WorkshopPartDefinition("module.tracking", UserText.Message("copy.0bed12b803f3c741"), UserText.Message("copy.d31f6b535fce1f84"), UserText.Message("copy.d31f6b535fce1f84"),
                UserText.Message("copy.e1a6f049cdfd3171"), availableInVersionTwo: true, module: TowerModules.Tracking),
            new WorkshopPartDefinition("weapon.fan", UserText.Message("copy.9313857873da220d"), UserText.Message("copy.886b6efc80c27cb0"), UserText.Message("copy.886b6efc80c27cb0"),
                UserText.Message("copy.3e13a8b46352dd2e"), combatMount: true, mountKind: StructureKind.Fan),
            new WorkshopPartDefinition("weapon.ricochet", UserText.Message("copy.380591554457ff85"), UserText.Message("copy.971cdbaaacebf86a"), UserText.Message("copy.971cdbaaacebf86a"),
                UserText.Message("copy.84d2b3f9004a9523"), combatMount: true, mountKind: StructureKind.Ricochet),
            new WorkshopPartDefinition("weapon.stapler", UserText.Message("copy.cf20e35bd8154ac3"), UserText.Message("copy.3e41b8998eb674a9"), UserText.Message("copy.3e41b8998eb674a9"),
                UserText.Message("copy.98b5c9986b4b4a70"), combatMount: true, mountKind: StructureKind.Stapler),
            new WorkshopPartDefinition("weapon.blotter", UserText.Message("copy.f136d298cf83917b"), UserText.Message("copy.0d97060d88e2638e"), UserText.Message("copy.0d97060d88e2638e"),
                UserText.Message("copy.21bc5a4c9d329e7c"), mountKind: StructureKind.Blotter),
            new WorkshopPartDefinition("module.rail", UserText.Message("copy.215d555ed53c9640"), UserText.Message("copy.47bfb21a514332d0"), UserText.Message("copy.82d9894b407a611a"),
                UserText.Message("copy.37bd083cb72a15b3"), module: TowerModules.Rail)
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
