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
        public string Description { get; }
        public PartUnlock(string id, string name, int goldCost, string description = "") { Id = id; Name = name; GoldCost = goldCost; Description = description ?? ""; }
    }

    /// <summary>Permanent options, never a stockpile of consumable weapon instances.</summary>
    public sealed class WorkshopProfile
    {
        public const int CurrentVersion = 4;
        private static readonly IReadOnlyList<PartUnlock> parts = PartCatalog.HistoricalParts;
        private readonly HashSet<string> unlocked = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<TowerBlueprint> album = new List<TowerBlueprint>();
        private readonly string[] loadout = new string[ProfileLimits.LoadoutSlotCount];
        private readonly ReadOnlyCollection<TowerBlueprint> albumView;
        private readonly ReadOnlyCollection<string> loadoutView;
        public int Gold { get; private set; }
        public IReadOnlyList<PartUnlock> Parts => parts;
        public IReadOnlyList<PartUnlock> GetParts(GameBalance balance) => PartCatalog.CreateParts(balance);
        public IReadOnlyList<TowerBlueprint> Album => albumView;
        public IReadOnlyList<string> Loadout => loadoutView;
        public WorkshopProfile()
        {
            albumView = album.AsReadOnly(); loadoutView = Array.AsReadOnly(loadout);
            foreach (var part in PartCatalog.All) if (part.InitiallyUnlocked) unlocked.Add(part.Id);
            AddStarters();
            for (int i = 0; i < loadout.Length; i++) loadout[i] = album[i].Id;
        }
        private void AddStarters()
        {
            AddStarter(new TowerBlueprint("starter.gun", "Пулемёт", ChassisKind.Standard, StructureKind.Gun));
            AddStarter(new TowerBlueprint("starter.double", "Двойной пулемёт", ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun));
            AddStarter(new TowerBlueprint("starter.piercer", "Пробивающая пушка", ChassisKind.Standard, StructureKind.Piercer));
            AddStarter(new TowerBlueprint("starter.mortar", "Защищённая мортира", ChassisKind.Standard, StructureKind.Mortar, modules: TowerModules.Armor));
        }
        private void AddStarter(TowerBlueprint blueprint) { if (album.Count < ProfileLimits.MaximumAlbumPages && Find(blueprint.Id) == null) album.Add(blueprint); }
        public void GrantGold(int amount) { if (amount > 0) Gold = (int)Math.Min(int.MaxValue, (long)Gold + amount); }
        public bool IsUnlocked(string id) => id != null && unlocked.Contains(id);
        public bool Unlock(string id, out string reason, GameBalance balance = null)
        {
            reason = "";
            foreach (PartUnlock part in GetParts(balance))
                if (part.Id == id)
                {
                    if (IsUnlocked(id)) return true;
                    if (Gold < part.GoldCost) { reason = "Не хватает золота."; return false; }
                    Gold -= part.GoldCost; unlocked.Add(id); return true;
                }
            reason = "Неизвестная деталь."; return false;
        }
        public bool ValidateOwned(TowerBlueprint blueprint, out string reason, GameBalance balance = null)
        {
            if (!TowerCatalog.Validate(blueprint, balance, out reason)) return false;
            return ValidatePartRights(blueprint, out reason);
        }
        bool ValidatePartRights(TowerBlueprint blueprint, out string reason)
        {
            reason = "";
            if (!PartCatalog.TryGetChassis(blueprint.Chassis, out var chassis) || !IsUnlocked(chassis.Id) ||
                !IsUnlocked(PartCatalog.GetMountId(blueprint.FirstWeapon)) ||
                (blueprint.SecondWeapon.HasValue && !IsUnlocked(PartCatalog.GetMountId(blueprint.SecondWeapon.Value))))
            { reason = "Сначала откройте все детали сборки."; return false; }
            foreach (var part in PartCatalog.All)
                if (part.Module != TowerModules.None && (blueprint.Modules & part.Module) != 0 && !IsUnlocked(part.Id))
                { reason = "Сначала откройте все детали сборки."; return false; }
            return true;
        }
        public bool SaveBlueprint(TowerBlueprint blueprint, out string reason, GameBalance balance = null)
        {
            if (!ValidateOwned(blueprint, out reason, balance)) return false;
            if (blueprint.Origin == DrawingOrigin.PlayerDrawing && blueprint.Drawing.IsEmpty)
            { reason = "Пустая страница остаётся черновиком. Нарисуйте хотя бы один штрих."; return false; }
            for (int i = 0; i < album.Count; i++) if (album[i].Id == blueprint.Id) { album[i] = blueprint; return true; }
            if (album.Count >= ProfileLimits.MaximumAlbumPages) { reason = "В альбоме уже " + ProfileLimits.MaximumAlbumPages + " чертежа."; return false; }
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
        public bool AssignSlot(int slot, string id, out string reason, GameBalance balance = null)
        {
            reason = "";
            if (slot < 0 || slot >= loadout.Length) { reason = "Номер слота от 0 до " + (ProfileLimits.LoadoutSlotCount - 1) + "."; return false; }
            TowerBlueprint blueprint = Find(id);
            if (blueprint == null || !ValidateOwned(blueprint, out reason, balance)) { if (reason.Length == 0) reason = "Чертёж не найден."; return false; }
            for (int i = 0; i < loadout.Length; i++) if (i != slot && loadout[i] == id) { reason = "Этот чертёж уже взят в другой слот."; return false; }
            loadout[slot] = id; return true;
        }
        private TowerBlueprint Find(string id) { foreach (TowerBlueprint blueprint in album) if (blueprint.Id == id) return blueprint; return null; }
        public IReadOnlyList<TowerBlueprint> GetLoadoutBlueprints()
        { var result = new TowerBlueprint[loadout.Length]; for (int i = 0; i < result.Length; i++) result[i] = Find(loadout[i]); return Array.AsReadOnly(result); }
        public WorkshopProfileData ToData()
        {
            var data = new WorkshopProfileData { Version = CurrentVersion, Gold = Gold, Loadout = (string[])loadout.Clone(), Album = new BlueprintData[album.Count] };
            var ordered = new List<string>(); foreach (PartUnlock part in parts) if (IsUnlocked(part.Id)) ordered.Add(part.Id);
            data.Unlocked = ordered.ToArray();
            for (int i = 0; i < album.Count; i++) data.Album[i] = new BlueprintData(album[i]);
            return data;
        }
        /// <summary>Copy current valid state exactly; loader repairs never run during a transaction.</summary>
        public WorkshopProfile CreateWorkingCopy()
        {
            var copy = new WorkshopProfile();
            copy.Gold = Gold;
            copy.unlocked.Clear();
            foreach (string id in unlocked) copy.unlocked.Add(id);
            copy.album.Clear();
            // Blueprint instances are immutable value snapshots, so sharing them is safe.
            copy.album.AddRange(album);
            Array.Copy(loadout, copy.loadout, loadout.Length);
            return copy;
        }
        public static WorkshopProfile FromData(WorkshopProfileData data)
        {
            if (data != null && (data.Version == 2 || data.Version == 3 || data.Version == 4)) return FromStrictData(data);
            if (data != null && data.Version > CurrentVersion) throw new NotSupportedException("Профиль создан более новой версией игры.");
            if (data != null && data.Version != 1) throw new ArgumentException("Неизвестная версия профиля.");
            var profile = new WorkshopProfile();
            if (data == null) return profile;
            profile.Gold = Math.Max(0, data.Gold);
            if (data.Unlocked != null) foreach (string id in data.Unlocked) foreach (PartUnlock part in parts)
                if (part.Id == id && IsVersionTwoPart(id)) profile.unlocked.Add(id);
            profile.album.Clear();
            if (data.Album != null) foreach (BlueprintData saved in data.Album)
                if (saved != null) profile.SaveBlueprint(saved.ToBlueprint(1), out _);
            // Starters are always available to repair a stale or damaged four-slot selection.
            profile.AddStarters();
            var selected = new HashSet<string>();
            for (int i = 0; i < profile.loadout.Length; i++)
            {
                string id = data.Loadout != null && i < data.Loadout.Length ? data.Loadout[i] : null;
                if (profile.Find(id) == null || !selected.Add(id))
                    foreach (TowerBlueprint blueprint in profile.album)
                        if (selected.Add(blueprint.Id)) { id = blueprint.Id; break; }
                profile.loadout[i] = id;
            }
            return profile;
        }
        static WorkshopProfile FromStrictData(WorkshopProfileData data)
        {
            if (data.Gold < 0 || data.Unlocked == null || data.Album == null || data.Album.Length < ProfileLimits.LoadoutSlotCount ||
                data.Album.Length > ProfileLimits.MaximumAlbumPages || data.Loadout == null || data.Loadout.Length != ProfileLimits.LoadoutSlotCount)
                throw new ArgumentException("Повреждённые данные профиля версии " + data.Version + ".");
            var profile = new WorkshopProfile(); profile.Gold = data.Gold;
            profile.unlocked.Clear(); profile.album.Clear();
            foreach (string id in data.Unlocked)
            {
                bool known = false; foreach (PartUnlock part in parts) if (part.Id == id) { known = true; break; }
                if (data.Version >= 3 && !known && !string.IsNullOrEmpty(id))
                    throw new NotSupportedException("Профиль содержит неизвестную этой версии игры деталь.");
                if (!known || (data.Version == 2 && !IsVersionTwoPart(id)) || !profile.unlocked.Add(id)) throw new ArgumentException("Повреждённый список открытий.");
            }
            foreach (var part in PartCatalog.All)
                if (part.InitiallyUnlocked && !profile.IsUnlocked(part.Id)) throw new ArgumentException("Отсутствует базовая деталь.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (BlueprintData saved in data.Album)
            {
                if (saved == null) throw new ArgumentException("Пустая запись альбома.");
                TowerBlueprint blueprint = saved.ToBlueprint(data.Version);
                if (!ids.Add(blueprint.Id)) throw new ArgumentException("Страница альбома повторяется.");
                if (!TowerCatalog.ValidateParts(blueprint, out string reason) || !profile.ValidatePartRights(blueprint, out reason))
                    throw new ArgumentException("Повреждённая страница альбома: " + reason);
                if (blueprint.Origin == DrawingOrigin.PlayerDrawing && blueprint.Drawing.IsEmpty)
                    throw new ArgumentException("Пустой авторский чертёж в альбоме.");
                profile.album.Add(blueprint);
            }
            var selected = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < profile.loadout.Length; i++)
            {
                string id = data.Loadout[i];
                if (id == null || profile.Find(id) == null || !selected.Add(id)) throw new ArgumentException("Повреждённый набор страниц.");
                profile.loadout[i] = id;
            }
            return profile;
        }
        static bool IsVersionTwoPart(string id) => PartCatalog.IsHistorical(id);
    }
    [Serializable] public sealed class WorkshopProfileData
    {
        public int Version = 1, Gold;
        public string[] Unlocked, Loadout;
        public BlueprintData[] Album;
    }
}
