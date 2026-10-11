using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PaperDefense.Core
{
    public sealed class PartUnlock
    {
        public string Id { get; }
        public string Name { get; }
        public int GoldCost { get; }
        public PartUnlock(string id, string name, int goldCost) { Id = id; Name = name; GoldCost = goldCost; }
    }

    /// <summary>Permanent options, never a stockpile of consumable weapon instances.</summary>
    public sealed class WorkshopProfile
    {
        private static readonly ReadOnlyCollection<PartUnlock> parts = Array.AsReadOnly(new[]
        {
            new PartUnlock("chassis.standard", "Обычное основание", 0), new PartUnlock("weapon.gun", "Пулемёт", 0),
            new PartUnlock("weapon.piercer", "Пробивающая пушка", 0), new PartUnlock("weapon.mortar", "Мортира", 0),
            new PartUnlock("module.armor", "Бронепластины", 0), new PartUnlock("chassis.heavy", "Тяжёлое основание", 90),
            new PartUnlock("module.feed", "Улучшенная подача", 60), new PartUnlock("module.tracking", "Быстрое наведение", 60)
        });
        private readonly HashSet<string> unlocked = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<TowerBlueprint> album = new List<TowerBlueprint>();
        private readonly string[] loadout = new string[4];
        private readonly ReadOnlyCollection<TowerBlueprint> albumView;
        private readonly ReadOnlyCollection<string> loadoutView;
        public int Gold { get; private set; }
        public IReadOnlyList<PartUnlock> Parts => parts;
        public IReadOnlyList<TowerBlueprint> Album => albumView;
        public IReadOnlyList<string> Loadout => loadoutView;
        public WorkshopProfile()
        {
            albumView = album.AsReadOnly(); loadoutView = Array.AsReadOnly(loadout);
            foreach (PartUnlock part in parts) if (part.GoldCost == 0) unlocked.Add(part.Id);
            AddStarters();
            for (int i = 0; i < 4; i++) loadout[i] = album[i].Id;
        }
        private void AddStarters()
        {
            AddStarter(new TowerBlueprint("starter.gun", "Пулемёт", ChassisKind.Standard, StructureKind.Gun));
            AddStarter(new TowerBlueprint("starter.double", "Двойной пулемёт", ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun));
            AddStarter(new TowerBlueprint("starter.piercer", "Пробивающая пушка", ChassisKind.Standard, StructureKind.Piercer));
            AddStarter(new TowerBlueprint("starter.mortar", "Защищённая мортира", ChassisKind.Standard, StructureKind.Mortar, modules: TowerModules.Armor));
        }
        private void AddStarter(TowerBlueprint blueprint) { if (album.Count < 64 && Find(blueprint.Id) == null) album.Add(blueprint); }
        public void GrantGold(int amount) { if (amount > 0) Gold = (int)Math.Min(int.MaxValue, (long)Gold + amount); }
        public bool IsUnlocked(string id) => id != null && unlocked.Contains(id);
        public bool Unlock(string id, out string reason)
        {
            reason = "";
            foreach (PartUnlock part in parts)
                if (part.Id == id)
                {
                    if (IsUnlocked(id)) return true;
                    if (Gold < part.GoldCost) { reason = "Не хватает золота."; return false; }
                    Gold -= part.GoldCost; unlocked.Add(id); return true;
                }
            reason = "Неизвестная деталь."; return false;
        }
        public bool ValidateOwned(TowerBlueprint blueprint, out string reason)
        {
            if (!TowerCatalog.Validate(blueprint, out reason)) return false;
            if (!IsUnlocked(blueprint.Chassis == ChassisKind.Heavy ? "chassis.heavy" : "chassis.standard") ||
                !IsUnlocked(WeaponPart(blueprint.FirstWeapon)) ||
                (blueprint.SecondWeapon.HasValue && !IsUnlocked(WeaponPart(blueprint.SecondWeapon.Value))) ||
                ((blueprint.Modules & TowerModules.Armor) != 0 && !IsUnlocked("module.armor")) ||
                ((blueprint.Modules & TowerModules.Feed) != 0 && !IsUnlocked("module.feed")) ||
                ((blueprint.Modules & TowerModules.Tracking) != 0 && !IsUnlocked("module.tracking")))
            { reason = "Сначала откройте все детали сборки."; return false; }
            return true;
        }
        private static string WeaponPart(StructureKind kind) => kind == StructureKind.Gun ? "weapon.gun" : kind == StructureKind.Piercer ? "weapon.piercer" : "weapon.mortar";
        public bool SaveBlueprint(TowerBlueprint blueprint, out string reason)
        {
            if (!ValidateOwned(blueprint, out reason)) return false;
            for (int i = 0; i < album.Count; i++) if (album[i].Id == blueprint.Id) { album[i] = blueprint; return true; }
            if (album.Count >= 64) { reason = "В альбоме уже 64 чертежа."; return false; }
            album.Add(blueprint); return true;
        }
        public bool RemoveBlueprint(string id, out string reason)
        {
            reason = "";
            foreach (string selected in loadout) if (selected == id) { reason = "Сначала замените этот чертёж в наборе."; return false; }
            TowerBlueprint blueprint = Find(id);
            if (blueprint == null) { reason = "Чертёж не найден."; return false; }
            album.Remove(blueprint); return true;
        }
        public bool AssignSlot(int slot, string id, out string reason)
        {
            reason = "";
            if (slot < 0 || slot >= loadout.Length) { reason = "Номер слота от 0 до 3."; return false; }
            TowerBlueprint blueprint = Find(id);
            if (blueprint == null || !ValidateOwned(blueprint, out reason)) { if (reason.Length == 0) reason = "Чертёж не найден."; return false; }
            for (int i = 0; i < loadout.Length; i++) if (i != slot && loadout[i] == id) { reason = "Этот чертёж уже взят в другой слот."; return false; }
            loadout[slot] = id; return true;
        }
        private TowerBlueprint Find(string id) { foreach (TowerBlueprint blueprint in album) if (blueprint.Id == id) return blueprint; return null; }
        public IReadOnlyList<TowerBlueprint> GetLoadoutBlueprints()
        { var result = new TowerBlueprint[4]; for (int i = 0; i < 4; i++) result[i] = Find(loadout[i]); return Array.AsReadOnly(result); }
        public WorkshopProfileData ToData()
        {
            var data = new WorkshopProfileData { Gold = Gold, Loadout = (string[])loadout.Clone(), Album = new BlueprintData[album.Count] };
            var ordered = new List<string>(); foreach (PartUnlock part in parts) if (IsUnlocked(part.Id)) ordered.Add(part.Id);
            data.Unlocked = ordered.ToArray();
            for (int i = 0; i < album.Count; i++) data.Album[i] = new BlueprintData(album[i]);
            return data;
        }
        public static WorkshopProfile FromData(WorkshopProfileData data)
        {
            var profile = new WorkshopProfile();
            if (data == null) return profile;
            profile.Gold = Math.Max(0, data.Gold);
            if (data.Unlocked != null) foreach (string id in data.Unlocked) foreach (PartUnlock part in parts)
                if (part.Id == id) profile.unlocked.Add(id);
            profile.album.Clear();
            if (data.Album != null) foreach (BlueprintData saved in data.Album)
                if (saved != null) profile.SaveBlueprint(saved.ToBlueprint(), out _);
            // Starters are always available to repair a stale or damaged four-slot selection.
            profile.AddStarters();
            var selected = new HashSet<string>();
            for (int i = 0; i < 4; i++)
            {
                string id = data.Loadout != null && i < data.Loadout.Length ? data.Loadout[i] : null;
                if (profile.Find(id) == null || !selected.Add(id))
                    foreach (TowerBlueprint blueprint in profile.album)
                        if (selected.Add(blueprint.Id)) { id = blueprint.Id; break; }
                profile.loadout[i] = id;
            }
            return profile;
        }
    }
    [Serializable] public sealed class WorkshopProfileData
    {
        public int Version = 1, Gold;
        public string[] Unlocked, Loadout;
        public BlueprintData[] Album;
    }
}
