using System;

namespace PaperDefense.Core
{
    public enum ChassisKind { Standard, Heavy }
    [Flags] public enum TowerModules { None = 0, Armor = 1, Feed = 2, Tracking = 4 }

    /// <summary>A saved assembly is a value snapshot. Editing the album never refits deployed towers.</summary>
    public sealed class TowerBlueprint
    {
        public string Id { get; }
        public string Name { get; }
        public ChassisKind Chassis { get; }
        public StructureKind FirstWeapon { get; }
        public StructureKind? SecondWeapon { get; }
        public TowerModules Modules { get; }
        public TowerBlueprint(string id, string name, ChassisKind chassis, StructureKind firstWeapon,
            StructureKind? secondWeapon = null, TowerModules modules = TowerModules.None)
        { Id = id?.Trim() ?? ""; Name = name?.Trim() ?? ""; Chassis = chassis; FirstWeapon = firstWeapon; SecondWeapon = secondWeapon; Modules = modules; }
    }

    public static class TowerCatalog
    {
        public static int Capacity(ChassisKind chassis) => chassis == ChassisKind.Heavy ? 14 : 10;
        public static int Capacity(TowerBlueprint blueprint) => Capacity(blueprint.Chassis);
        public static int Load(TowerBlueprint blueprint)
        {
            int load = WeaponLoad(blueprint.FirstWeapon) + (blueprint.SecondWeapon.HasValue ? WeaponLoad(blueprint.SecondWeapon.Value) : 0);
            if ((blueprint.Modules & TowerModules.Armor) != 0) load += 3;
            if ((blueprint.Modules & TowerModules.Feed) != 0) load += 2;
            if ((blueprint.Modules & TowerModules.Tracking) != 0) load += 2;
            return load;
        }
        private static int WeaponLoad(StructureKind kind) => kind == StructureKind.Gun ? 4 : kind == StructureKind.Piercer ? 6 : kind == StructureKind.Mortar ? 7 : 100;
        public static bool Validate(TowerBlueprint blueprint, out string reason)
        {
            reason = "";
            if (blueprint == null) reason = "Выберите чертёж.";
            else if (blueprint.Id.Length == 0 || blueprint.Id.Length > 64 || blueprint.Name.Length == 0 || blueprint.Name.Length > 48)
                reason = "Нужны идентификатор и название до 48 знаков.";
            else if (!Enum.IsDefined(typeof(ChassisKind), blueprint.Chassis)) reason = "Неизвестное основание.";
            else if ((blueprint.Modules & ~(TowerModules.Armor | TowerModules.Feed | TowerModules.Tracking)) != 0) reason = "Неизвестный модуль.";
            else if (WeaponLoad(blueprint.FirstWeapon) > 7 || (blueprint.SecondWeapon.HasValue && WeaponLoad(blueprint.SecondWeapon.Value) > 7)) reason = "Крепления принимают пулемёт, пробивающую пушку или мортиру.";
            else if (Load(blueprint) > Capacity(blueprint))
                reason = "Не хватает " + (Load(blueprint) - Capacity(blueprint)) + " ед. нагрузки. Снимите оружие или выберите другое основание.";
            return reason.Length == 0;
        }
        public static int Cost(TowerBlueprint blueprint, GameBalance balance, MaterialTier tier)
        {
            int cost = blueprint.Chassis == ChassisKind.Heavy ? 28 : 16;
            cost += Math.Max(0, balance.Structure(blueprint.FirstWeapon).Cost - 16);
            if (blueprint.SecondWeapon.HasValue) cost += Math.Max(0, balance.Structure(blueprint.SecondWeapon.Value).Cost - 16);
            if ((blueprint.Modules & TowerModules.Armor) != 0) cost += 18;
            if ((blueprint.Modules & TowerModules.Feed) != 0) cost += 16;
            if ((blueprint.Modules & TowerModules.Tracking) != 0) cost += 14;
            return (int)Math.Ceiling(cost * Math.Max(0, balance.Material(tier).CostMultiplier));
        }
        public static float Health(TowerBlueprint blueprint, GameBalance balance, MaterialTier tier)
        {
            float health = 55;
            if (blueprint.Chassis == ChassisKind.Heavy) health *= 1.3f;
            if ((blueprint.Modules & TowerModules.Armor) != 0) health *= 1.5f;
            return Math.Max(1, health * balance.Material(tier).HealthMultiplier);
        }
        public static int ColorCost(TowerBlueprint blueprint,GameBalance balance)
            => Math.Max(2,(Cost(blueprint,balance,MaterialTier.Pencil)+7)/8);
        public static float BuildSeconds(TowerBlueprint blueprint, GameBalance balance, MaterialTier tier)
        {
            float seconds = balance.Structure(blueprint.FirstWeapon).BuildSeconds;
            if (blueprint.SecondWeapon.HasValue) seconds = Math.Max(seconds, balance.Structure(blueprint.SecondWeapon.Value).BuildSeconds) + .4f;
            if (blueprint.Chassis == ChassisKind.Heavy) seconds += .45f;
            if ((blueprint.Modules & TowerModules.Armor) != 0) seconds += .25f;
            if ((blueprint.Modules & TowerModules.Feed) != 0) seconds += .25f;
            if ((blueprint.Modules & TowerModules.Tracking) != 0) seconds += .25f;
            return Math.Max(0, seconds * balance.Material(tier).BuildTimeMultiplier);
        }
        public static float FeedMultiplier(TowerBlueprint blueprint)
            => Math.Min(1.15f, (blueprint.SecondWeapon.HasValue ? .8f : 1) * ((blueprint.Modules & TowerModules.Feed) != 0 ? 1.15f : 1));
        public static float AcquisitionSeconds(TowerBlueprint blueprint)
            => (blueprint.Modules & TowerModules.Tracking) != 0 ? .08f : .25f;
    }

    [Serializable] public sealed class BlueprintData
    {
        public string Id, Name;
        public ChassisKind Chassis;
        public StructureKind FirstWeapon, SecondWeapon;
        public bool HasSecondWeapon;
        public TowerModules Modules;
        public BlueprintData() { }
        public BlueprintData(TowerBlueprint blueprint)
        { Id = blueprint.Id; Name = blueprint.Name; Chassis = blueprint.Chassis; FirstWeapon = blueprint.FirstWeapon;
            HasSecondWeapon = blueprint.SecondWeapon.HasValue; SecondWeapon = blueprint.SecondWeapon ?? StructureKind.Gun; Modules = blueprint.Modules; }
        public TowerBlueprint ToBlueprint() => new TowerBlueprint(Id, Name, Chassis, FirstWeapon, HasSecondWeapon ? SecondWeapon : (StructureKind?)null, Modules);
    }
}
