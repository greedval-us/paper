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
        public const int CurrentVersion = 6;
        private static readonly IReadOnlyList<PartUnlock> parts = PartCatalog.HistoricalParts;
        private ContentCatalogSnapshot content;
        private readonly HashSet<string> unlocked = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<TowerBlueprint> album = new List<TowerBlueprint>();
        private readonly string[] loadout = new string[ProfileLimits.LoadoutSlotCount];
        private readonly ReadOnlyCollection<TowerBlueprint> albumView;
        private readonly ReadOnlyCollection<string> loadoutView;
        public int Gold { get; private set; }
        public IReadOnlyList<PartUnlock> Parts => GetParts(null);
        public IReadOnlyList<PartUnlock> GetParts(GameBalance balance)
        {
            ContentCatalogSnapshot catalog = balance != null ? balance.ResolveContent() : content;
            if (catalog == null) return PartCatalog.CreateParts(balance);
            var result = new List<PartUnlock>();
            foreach (var part in catalog.AllParts)
                if (part.Enabled) result.Add(new PartUnlock(part.Id, part.Name, part.UnlockGold, part.Description));
            return result.AsReadOnly();
        }
        public IReadOnlyList<TowerBlueprint> Album => albumView;
        public IReadOnlyList<string> Loadout => loadoutView;
        public WorkshopProfile(ContentCatalogSnapshot content = null, GameBalance preparedBalance = null)
        {
            albumView = album.AsReadOnly(); loadoutView = Array.AsReadOnly(loadout);
            if (content == null)
            {
                foreach (var part in PartCatalog.All) if (part.InitiallyUnlocked) unlocked.Add(part.Id);
                AddStarters();
                for (int i = 0; i < loadout.Length; i++) loadout[i] = album[i].Id;
                return;
            }
            ConfigureContent(content);
            foreach (var blueprint in content.StartupBlueprints) album.Add(blueprint);
            for (int i = 0; i < loadout.Length; i++)
            {
                loadout[i] = content.StartupSlotIds[i];
                if (loadout[i] == null) continue;
                var blueprint = Find(loadout[i]);
                // A catalog alone has no shared construction rules. Never guess them from historical defaults.
                bool valid = TowerCatalog.ValidateParts(blueprint, out string reason) &&
                    Available(blueprint, content, out reason) && ValidatePartRights(blueprint, out reason);
                if (valid && preparedBalance != null) valid = TowerCatalog.Validate(blueprint, preparedBalance, out reason);
                if (!valid) throw new ArgumentException("catalog.startupSlots[" + i + "]: " + reason);
            }
        }
        /// <summary>Preparation applies newly registered initial rights, never removes saved progress.</summary>
        public bool ConfigureContent(ContentCatalogSnapshot catalog)
        {
            content = catalog;
            if (catalog == null) return false;
            bool changed = false;
            foreach (var part in catalog.AllParts) if (part.InitiallyUnlocked) changed |= unlocked.Add(part.Id);
            return changed;
        }
        private void AddStarters()
        {
            AddStarter(new TowerBlueprint("starter.gun", UserText.Message("copy.0455469e8acac5ad"), ChassisKind.Standard, StructureKind.Gun));
            AddStarter(new TowerBlueprint("starter.double", UserText.Message("copy.fd5de0c30616fc84"), ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun));
            AddStarter(new TowerBlueprint("starter.piercer", UserText.Message("copy.bdf4c03725c66109"), ChassisKind.Standard, StructureKind.Piercer));
            AddStarter(new TowerBlueprint("starter.mortar", UserText.Message("copy.edfbe04a7d9f1438"), ChassisKind.Standard, StructureKind.Mortar, modules: TowerModules.Armor));
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
                    if (Gold < part.GoldCost) { reason = UserText.Message("copy.49e21f4e9cb1372b"); return false; }
                    Gold -= part.GoldCost; unlocked.Add(id); return true;
                }
            reason = UserText.Message("copy.9280ff03f0e48a54"); return false;
        }
        public bool ValidateOwned(TowerBlueprint blueprint, out string reason, GameBalance balance = null)
        {
            if (balance == null && content != null)
            {
                reason = "";
                if (!TowerCatalog.ValidateParts(blueprint, out reason)) return false;
                if (!Available(blueprint, content, out reason)) return false;
                blueprint = blueprint.WithContent(content);
            }
            if (!TowerCatalog.Validate(blueprint, balance, out reason)) return false;
            return ValidatePartRights(blueprint, out reason);
        }
        static bool Available(TowerBlueprint blueprint, ContentCatalogSnapshot catalog, out string reason)
        {
            reason = "";
            if (!catalog.TryGet(blueprint.ChassisId, out var chassis) || !chassis.Enabled)
            { reason = UserText.Message("copy.7c2f080a8b98d9ab"); return false; }
            foreach(var mount in blueprint.WeaponMounts)if(!catalog.TryGetWeapon(mount.WeaponId,out var weapon)||!weapon.Enabled)
            {reason=UserText.Message("copy.7c2f080a8b98d9ab");return false;}
            foreach (var selection in blueprint.ModuleSelections)
                if (!catalog.TryGet(selection.DefinitionId, out var module) || !module.Enabled)
                { reason = UserText.Message("copy.15821194b52b152a"); return false; }
            return true;
        }
        bool ValidatePartRights(TowerBlueprint blueprint, out string reason)
        {
            reason = "";
            if (!IsUnlocked(blueprint.ChassisId))
            { reason = UserText.Message("copy.25d3314a1878879d"); return false; }
            foreach(var mount in blueprint.WeaponMounts)if(!IsUnlocked(mount.WeaponId))
            {reason=UserText.Message("copy.25d3314a1878879d");return false;}
            foreach (var selection in blueprint.ModuleSelections)
                if (!IsUnlocked(selection.DefinitionId))
                { reason = UserText.Message("copy.25d3314a1878879d"); return false; }
            return true;
        }
        public bool SaveBlueprint(TowerBlueprint blueprint, out string reason, GameBalance balance = null)
        {
            if (!ValidateOwned(blueprint, out reason, balance)) return false;
            if (blueprint.Origin == DrawingOrigin.PlayerDrawing && blueprint.Drawing.IsEmpty)
            { reason = UserText.Message("copy.b9c49ccc73b9d0d4"); return false; }
            for (int i = 0; i < album.Count; i++) if (album[i].Id == blueprint.Id) { album[i] = blueprint; return true; }
            if (album.Count >= ProfileLimits.MaximumAlbumPages) { reason = UserText.Message("copy.9b39b0e67017068e", ProfileLimits.MaximumAlbumPages); return false; }
            album.Add(blueprint); return true;
        }
        public bool RemoveBlueprint(string id, out string reason)
        {
            reason = "";
            foreach (string selected in loadout) if (selected == id) { reason = UserText.Message("copy.c254f33cb940ff85"); return false; }
            TowerBlueprint blueprint = Find(id);
            if (blueprint == null) { reason = UserText.Message("copy.5850704bbcf92fc2"); return false; }
            album.Remove(blueprint); return true;
        }
        public bool AssignSlot(int slot, string id, out string reason, GameBalance balance = null)
        {
            reason = "";
            if (slot < 0 || slot >= loadout.Length) { reason = UserText.Message("copy.8d55609b15f3fd7c", (ProfileLimits.LoadoutSlotCount - 1)); return false; }
            TowerBlueprint blueprint = Find(id);
            if (blueprint == null || !ValidateOwned(blueprint, out reason, balance)) { if (reason.Length == 0) reason = UserText.Message("copy.5850704bbcf92fc2"); return false; }
            for (int i = 0; i < loadout.Length; i++) if (i != slot && loadout[i] == id) { reason = UserText.Message("copy.3ea8d535669110d4"); return false; }
            loadout[slot] = id; return true;
        }
        private TowerBlueprint Find(string id) { foreach (TowerBlueprint blueprint in album) if (blueprint.Id == id) return blueprint; return null; }
        public IReadOnlyList<TowerBlueprint> GetLoadoutBlueprints()
        { var result = new TowerBlueprint[loadout.Length]; for (int i = 0; i < result.Length; i++) result[i] = Find(loadout[i]); return Array.AsReadOnly(result); }
        public WorkshopProfileData ToData()
        {
            var data = new WorkshopProfileData { Version = CurrentVersion, Gold = Gold, Loadout = (string[])loadout.Clone(), Album = new BlueprintData[album.Count] };
            var ordered = new List<string>(unlocked); ordered.Sort(StringComparer.Ordinal);
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
            copy.content = content;
            return copy;
        }
        public static WorkshopProfile FromData(WorkshopProfileData data)
        {
            if (data != null && data.Version >= 2 && data.Version <= CurrentVersion) return FromStrictData(data);
            if (data != null && data.Version > CurrentVersion) throw new NotSupportedException(UserText.Message("copy.d4480b00d319b755"));
            if (data != null && data.Version != 1) throw new ArgumentException(UserText.Message("copy.0ecaed17b95a05bc"));
            var profile = new WorkshopProfile();
            if (data == null) return profile;
            profile.Gold = Math.Max(0, data.Gold);
            if (data.Unlocked != null) foreach (string id in data.Unlocked)
            {
                bool known = false;
                foreach (PartUnlock part in parts) if (part.Id == id) { known = true; break; }
                if (!known && !string.IsNullOrEmpty(id)) throw new NotSupportedException(UserText.Message("copy.3ee1a23fb7b9494a"));
                if (known && IsVersionTwoPart(id)) profile.unlocked.Add(id);
            }
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
            if (data.Gold < 0 || data.Unlocked == null || data.Album == null || (data.Version < 5 && data.Album.Length < ProfileLimits.LoadoutSlotCount) ||
                data.Album.Length > ProfileLimits.MaximumAlbumPages || data.Loadout == null || data.Loadout.Length != ProfileLimits.LoadoutSlotCount)
                throw new ArgumentException(UserText.Message("copy.bed3d028a2c52df6", data.Version));
            var profile = new WorkshopProfile(); profile.Gold = data.Gold;
            profile.unlocked.Clear(); profile.album.Clear();
            foreach (string id in data.Unlocked)
            {
                if (data.Version >= 5)
                {
                    ContentValidation.Id(id, "profile.unlocked");
                    if (!profile.unlocked.Add(id))
                        throw new ArgumentException(UserText.Message("copy.ecf4d32d5b25b819"));
                    continue;
                }
                bool known = false; foreach (PartUnlock part in parts) if (part.Id == id) { known = true; break; }
                if (!known && !string.IsNullOrEmpty(id))
                    throw new NotSupportedException(UserText.Message("copy.3ee1a23fb7b9494a"));
                if (!known || (data.Version == 2 && !IsVersionTwoPart(id)) || !profile.unlocked.Add(id)) throw new ArgumentException(UserText.Message("copy.ecf4d32d5b25b819"));
            }
            if (data.Version < 5) foreach (var part in PartCatalog.All)
                if (part.InitiallyUnlocked && !profile.IsUnlocked(part.Id)) throw new ArgumentException(UserText.Message("copy.5d5b9958756f67cd"));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (BlueprintData saved in data.Album)
            {
                if (saved == null) throw new ArgumentException(UserText.Message("copy.fd4cbb1be5995da3"));
                TowerBlueprint blueprint = saved.ToBlueprint(data.Version);
                if (!ids.Add(blueprint.Id)) throw new ArgumentException(UserText.Message("copy.38843999b54beb9a"));
                if (!TowerCatalog.ValidateParts(blueprint, out string reason) ||
                    (data.Version < 5 && !profile.ValidatePartRights(blueprint, out reason)))
                    throw new ArgumentException(UserText.Message("copy.336691bac4dcc798", reason));
                if (blueprint.Origin == DrawingOrigin.PlayerDrawing && blueprint.Drawing.IsEmpty)
                    throw new ArgumentException(UserText.Message("copy.a78634b793113f25"));
                profile.album.Add(blueprint);
            }
            var selected = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < profile.loadout.Length; i++)
            {
                string id = data.Loadout[i];
                if (data.Version >= 5 && string.IsNullOrEmpty(id)) { profile.loadout[i] = null; continue; }
                if (id == null || profile.Find(id) == null || !selected.Add(id)) throw new ArgumentException(UserText.Message("copy.dac3f5616ef85f95"));
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
