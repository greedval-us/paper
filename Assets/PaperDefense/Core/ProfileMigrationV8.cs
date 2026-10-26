using System;
using System.Collections.Generic;
using System.Linq;

namespace PaperDefense.Core
{
    public sealed class ProfileMigrationResult
    {
        public bool Success => Candidate != null;
        public WorkshopProfile Candidate { get; }
        public IReadOnlyList<AssemblyDiagnostic> Diagnostics { get; }
        public bool IsUnsupported { get; }
        internal ProfileMigrationResult(WorkshopProfile candidate, IEnumerable<AssemblyDiagnostic> diagnostics, bool unsupported = false)
        { Candidate = candidate; Diagnostics = new List<AssemblyDiagnostic>(diagnostics).AsReadOnly(); IsUnsupported = unsupported; }
    }

    /// <summary>Historical readers build a separate candidate. No page is displaced to install new starters.</summary>
    public static class ProfileMigrationV8
    {
        public static ProfileMigrationResult Migrate(WorkshopProfileData source, ComponentCatalog catalog)
            => Migrate(source, catalog, null);
        public static ProfileMigrationResult Migrate(WorkshopProfileData source, ComponentCatalog catalog, GameBalance legacyBalance)
        {
            if (source != null && (source.Version < 1 || source.Version > WorkshopProfile.CurrentVersion))
                return new ProfileMigrationResult(null, new[] { new AssemblyDiagnostic("profile.version.unsupported", "Version") }, true);
            try
            {
                AssemblyProfileDataCopy.GuardFuture(source);
                if (source == null) return new ProfileMigrationResult(WorkshopProfile.CreateNew(catalog), Array.Empty<AssemblyDiagnostic>());
                if (source.Version == WorkshopProfile.CurrentVersion)
                    return new ProfileMigrationResult(WorkshopProfile.ReadVersionEight(source, catalog, legacyBalance), Array.Empty<AssemblyDiagnostic>());
                var candidate = WorkshopProfile.ReadHistorical(source);
                candidate.PrepareMigratedEntries(catalog, legacyBalance);
                return new ProfileMigrationResult(candidate, Array.Empty<AssemblyDiagnostic>());
            }
            catch (NotSupportedException)
            { return new ProfileMigrationResult(null, new[] { new AssemblyDiagnostic("profile.data.unsupported", "profile") }, true); }
            catch (AssemblyCatalogException error) { return new ProfileMigrationResult(null, error.Diagnostics); }
            catch (ArgumentException) { return new ProfileMigrationResult(null, new[] { new AssemblyDiagnostic("profile.data.invalid", "profile") }); }
        }

        internal static AlbumEntry TryConvert(TowerBlueprint blueprint, ComponentCatalog catalog, GameBalance balance)
        {
            if (catalog == null || balance == null || blueprint.Origin != DrawingOrigin.LegacyStandard || blueprint.Reinforcement != null
                || blueprint.WeaponMounts.Any(m => m.Footprint.IsContour || Math.Abs(m.Footprint.AspectRatio - 1) > .00001f || m.Volley != null)) return null;
            try
            {
                string frame = blueprint.ChassisId switch { "chassis.standard" => "frame.standard", "chassis.heavy" => "frame.heavy",
                    "chassis.precision" => "frame.precision", "chassis.battery" => "frame.battery", _ => null };
                if (frame == null || blueprint.ModuleSelections.Any(m => m.DefinitionId != "module.armor" && m.DefinitionId != "module.feed" && m.DefinitionId != "module.tracking")) return null;
                bool Has(string id) => blueprint.ModuleSelections.Any(m => m.DefinitionId == id);
                PartRef P(string id) { if (!catalog.TryGet(id, 1, out _)) throw new ArgumentException("Missing conversion part."); return new PartRef(id, 1); }
                var foundation = new FoundationRecipe(P(frame), P("hull.standard"), P(Has("module.armor") ? "armor.plate" : "armor.none"),
                    P(Has("module.feed") ? "feed.boost" : "feed.standard"), P(Has("module.tracking") ? "turn.quick" : "turn.standard"));
                var mounts = new List<WeaponMountRecipe>();
                foreach (var selected in blueprint.WeaponMounts)
                {
                    string family = selected.WeaponId switch { "weapon.gun" => "gun", "weapon.piercer" => "piercer", "weapon.mortar" => "mortar",
                        "weapon.fan" => "fan", "weapon.ricochet" => "ricochet", "weapon.stapler" => "stapler", "weapon.blotter" => "blotter", _ => null };
                    if (family == null) return null;
                    mounts.Add(new WeaponMountRecipe(selected.SlotIndex, new WeaponRecipe(P("core." + family), P("head." + family), P("sight.iron"), P("magazine.standard"), P("trigger.standard"))));
                }
                var recipe = new AssemblyRecipe(new TowerRecipe(foundation, mounts));
                var used = recipe.Parts.GroupBy(p => (p.Id, p.Revision)).Select(g => { catalog.TryGet(g.Key.Id, g.Key.Revision, out var d); return d; });
                var snapshot = new RecipeSnapshot(recipe, catalog.BalanceRevision, used);
                var old = new AlbumEntry(blueprint.Id, blueprint.Name, 1, blueprint);
                foreach (MaterialTier material in Enum.GetValues(typeof(MaterialTier)))
                {
                    var previous = LegacyAlbumAdapter.Resolve(old, balance, material); var next = AssemblyResolver.Resolve(snapshot, balance, material);
                    if (!previous.Valid || !next.Valid || !Equivalent(previous.Value, next.Value, blueprint, balance)) return null;
                }
                return new AlbumEntry(blueprint.Id, blueprint.Name, 1, snapshot);
            }
            catch (ArgumentException) { return null; }
        }
        static bool Near(float a, float b) => Math.Abs(a - b) <= .00001f * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));
        static bool Equivalent(ResolvedEntry a, ResolvedEntry b, TowerBlueprint blueprint, GameBalance balance)
        {
            if (a.Cost != b.Cost || a.Load != b.Load || a.Capacity != b.Capacity || a.Weapons.Count != b.Weapons.Count
                || !Near(a.Health,b.Health) || !Near(a.BuildSeconds,b.BuildSeconds) || !Near(a.Defense,b.Defense)
                || !Near(a.FrontDefense,b.FrontDefense) || !Near(a.RefundFraction,b.RefundFraction) || !Near(a.Dps,b.Dps)) return false;
            for (int i = 0; i < a.Weapons.Count; i++)
            {
                var x = a.Weapons[i]; var y = b.Weapons[i];
                if (x.SlotIndex != y.SlotIndex || x.Attack != y.Attack || x.TargetLimit != y.TargetLimit || !Near(x.Damage,y.Damage)
                    || !Near(x.Range,y.Range) || !Near(x.MinimumRange,y.MinimumRange) || !Near(x.AcquisitionSeconds,y.AcquisitionSeconds)
                    || !Near(x.IntervalAtFeed(a.FeedMultiplier),y.IntervalAtFeed(b.FeedMultiplier)) || !Near(x.Radius,y.Radius)
                    || !Near(x.FlightSeconds,y.FlightSeconds) || !Near(x.JumpRange,y.JumpRange) || !Near(x.ConeDegrees,y.ConeDegrees)
                    || !Near(x.SlowSpeedMultiplier,y.SlowSpeedMultiplier) || !Near(x.SlowSeconds,y.SlowSeconds)) return false;
                // Historical guns fire continuously. A new magazine is equivalent only when every
                // shot gap, including reload, stays the same for every active-mount feed state.
                for (int active = 1; active <= a.Weapons.Count; active++)
                {
                    float oldFeed = TowerCatalog.FeedMultiplier(blueprint, balance, active);
                    float interval = TowerCatalog.ActionInterval(blueprint, balance, blueprint.GetWeaponId(x.SlotIndex), oldFeed, x.SlotIndex);
                    float newFeed = b.FeedForActiveMounts(active);
                    if (!Near(interval,y.IntervalAtFeed(newFeed)) || !Near(interval,y.ReloadAtFeed(newFeed))
                        || !Near(x.Damage / interval,y.DpsAtFeed(newFeed))) return false;
                }
            }
            return true;
        }
    }

    public sealed partial class WorkshopProfile
    {
        readonly List<AlbumEntry> entries = new List<AlbumEntry>();
        readonly List<ComponentLibraryEntry> library = new List<ComponentLibraryEntry>();
        readonly List<RunRewardReceiptData> rewards = new List<RunRewardReceiptData>();
        readonly List<OperationReceiptData> operations = new List<OperationReceiptData>();
        public IReadOnlyList<AlbumEntry> Entries => entries.AsReadOnly();
        public IReadOnlyList<ComponentLibraryEntry> Library => library.AsReadOnly();
        public int TotalXp { get; private set; }
        public IReadOnlyList<AlbumEntry> GetLoadoutEntries()
            => Array.AsReadOnly(loadout.Select(id => entries.FirstOrDefault(e => e.Id == id)).ToArray());

        public static WorkshopProfile CreateNew(ComponentCatalog catalog, ContentCatalogSnapshot content = null, GameBalance preparedBalance = null)
        {
            var profile = new WorkshopProfile(content, preparedBalance);
            if (catalog == null) return profile;
            if (catalog.Starters.Count != ProfileLimits.LoadoutSlotCount) throw new ArgumentException("Eight assembly starter pages are required.");
            profile.entries.Clear(); profile.album.Clear(); Array.Clear(profile.loadout, 0, profile.loadout.Length);
            profile.GrantInitialComponents(catalog);
            int index = 0;
            foreach (var entry in catalog.Starters) { profile.AddOwnedEntry(entry); profile.loadout[index++] = entry.Id; }
            return profile;
        }
        public static WorkshopProfile FromData(WorkshopProfileData data, ComponentCatalog catalog = null, GameBalance legacyBalance = null)
        {
            var result = ProfileMigrationV8.Migrate(data, catalog, legacyBalance);
            if (result.Success) return result.Candidate;
            if (result.IsUnsupported) throw new NotSupportedException("Unsupported profile version or saved metadata.");
            throw new ArgumentException("Invalid profile: " + string.Join(",", result.Diagnostics.Select(d => d.Code)));
        }
        internal static WorkshopProfile ReadVersionEight(WorkshopProfileData data, ComponentCatalog catalog, GameBalance balance)
        {
            if (data.Version != CurrentVersion || data.Gold < 0 || data.TotalXp < 0 || data.Entries == null || data.Entries.Length > ProfileLimits.MaximumAlbumPages
                || data.Unlocked == null || data.Loadout == null || data.Loadout.Length != ProfileLimits.LoadoutSlotCount
                || data.Library == null || data.Rewards == null || data.Operations == null) throw new ArgumentException("Invalid version eight profile.");
            var profile = new WorkshopProfile(); profile.entries.Clear(); profile.album.Clear(); profile.unlocked.Clear();
            Array.Clear(profile.loadout, 0, profile.loadout.Length); profile.Gold = data.Gold; profile.TotalXp = data.TotalXp;
            foreach (string id in data.Unlocked)
            { ContentValidation.Id(id, "profile.unlocked"); if (!profile.unlocked.Add(id)) throw new ArgumentException("Duplicate saved right."); }
            var entryIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var saved in data.Entries)
            {
                if (saved == null) throw new ArgumentException("Missing saved page.");
                var entry = saved.CreateSnapshot();
                if (!entryIds.Add(entry.Id)) throw new ArgumentException("Duplicate saved page.");
                profile.AddOwnedEntry(entry);
            }
            // The legacy Album DTO is retained as a compatibility projection, never a substitute for Entries.
            // Validate any supplied projection too, so future legacy metadata cannot be silently ignored.
            var projectionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var saved in data.Album ?? Array.Empty<BlueprintData>())
            {
                if (saved == null || !entryIds.Contains(saved.Id) || !projectionIds.Add(saved.Id)) throw new ArgumentException("Invalid legacy album projection.");
                saved.ToBlueprint(7);
            }
            var selected = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < data.Loadout.Length; i++)
            {
                string id = data.Loadout[i]; if (string.IsNullOrEmpty(id)) continue;
                if (!entryIds.Contains(id) || !selected.Add(id)) throw new ArgumentException("Unknown or duplicate loadout page.");
                profile.loadout[i] = id;
            }
            var libraryIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var saved in data.Library)
            {
                if (saved == null) throw new ArgumentException("Missing saved library component.");
                var item = saved.CreateSnapshot(); if (!libraryIds.Add(item.Id)) throw new ArgumentException("Duplicate library ID.");
                profile.library.Add(item);
            }
            var runs = new HashSet<Guid>(); var ops = new HashSet<Guid>();
            foreach (var saved in data.Rewards)
            {
                if (saved == null) throw new ArgumentException("Missing reward receipt."); AssemblyProfileDataCopy.Version(saved.Version);
                var id = ReceiptGuid(saved.RunId); if (!runs.Add(id) || saved.Waves < 0 || saved.Waves > 8 || (saved.VictoryBonus && saved.Waves != 8)) throw new ArgumentException("Invalid reward receipt.");
                profile.rewards.Add(Copy(saved));
            }
            foreach (var saved in data.Operations)
            {
                if (saved == null) throw new ArgumentException("Missing operation receipt."); AssemblyProfileDataCopy.Version(saved.Version);
                var id = ReceiptGuid(saved.OperationId); if (!ops.Add(id) || string.IsNullOrWhiteSpace(saved.Type) || saved.Type.Length > 64) throw new ArgumentException("Invalid operation receipt.");
                // JsonUtility writes optional null strings as empty strings. Both represent no result ID.
                if (!string.IsNullOrEmpty(saved.ResultId)) ContentValidation.Id(saved.ResultId, "receipt.result");
                profile.operations.Add(Copy(saved));
            }
            profile.content = balance?.ResolveContent();
            return profile;
        }
        static Guid ReceiptGuid(string id)
        { if (!Guid.TryParseExact(id, "N", out var result) || result == Guid.Empty) throw new ArgumentException("Receipt GUID must use N format."); return result; }
        static RunRewardReceiptData Copy(RunRewardReceiptData r) => new RunRewardReceiptData { Version=r.Version, RunId=r.RunId, Waves=r.Waves, VictoryBonus=r.VictoryBonus };
        static OperationReceiptData Copy(OperationReceiptData r) => new OperationReceiptData { Version=r.Version, OperationId=r.OperationId, Type=r.Type, ResultId=string.IsNullOrEmpty(r.ResultId) ? null : r.ResultId };
        void GrantInitialComponents(ComponentCatalog catalog)
        { if (catalog != null) foreach (var part in catalog.Definitions) if (part.RequiredLevel == 1 && part.UnlockGold == 0) unlocked.Add(part.Id); }
        internal void PrepareMigratedEntries(ComponentCatalog catalog, GameBalance balance)
        {
            GrantInitialComponents(catalog);
            // Explicit right aliases retain purchased capabilities; historical IDs and drawing aids remain owned.
            var aliases = new Dictionary<string,string>(StringComparer.Ordinal) {
                {"chassis.standard","frame.standard"},{"chassis.heavy","frame.heavy"},{"chassis.precision","frame.precision"},{"chassis.battery","frame.battery"},
                {"weapon.gun","core.gun"},{"weapon.piercer","core.piercer"},{"weapon.mortar","core.mortar"},{"weapon.fan","core.fan"},
                {"weapon.ricochet","core.ricochet"},{"weapon.stapler","core.stapler"},{"weapon.blotter","core.blotter"},
                {"module.armor","armor.plate"},{"module.feed","feed.boost"},{"module.tracking","turn.quick"}
            };
            foreach (var pair in aliases) if (unlocked.Contains(pair.Key) && catalog != null && catalog.TryGet(pair.Value,1,out _)) unlocked.Add(pair.Value);
            entries.Clear();
            foreach (var blueprint in album) entries.Add(ProfileMigrationV8.TryConvert(blueprint, catalog, balance) ?? new AlbumEntry(blueprint.Id,blueprint.Name,1,blueprint));
        }
        void ResetEntriesFromLegacy()
        { entries.Clear(); foreach (var p in album) entries.Add(new AlbumEntry(p.Id,p.Name,1,p)); }
        void StoreLegacyEntry(TowerBlueprint blueprint)
        {
            int index = entries.FindIndex(e => e.Id == blueprint.Id);
            var entry = new AlbumEntry(blueprint.Id,blueprint.Name,index < 0 ? 1 : entries[index].Revision,blueprint);
            if (index < 0) entries.Add(entry); else entries[index] = entry;
        }
        void AddOwnedEntry(AlbumEntry entry)
        {
            entries.Add(entry);
            var blueprint = entry.Kind == AlbumEntryKind.Legacy ? entry.LegacyBlueprint : CompatibilityBlueprint(entry);
            if (blueprint != null) album.Add(blueprint);
        }
        static TowerBlueprint CompatibilityBlueprint(AlbumEntry entry)
        {
            if (entry.Snapshot.Recipe.Tower == null) return null;
            var t = entry.Snapshot.Recipe.Tower;
            string frame = t.Foundation.Frame.Id switch { "frame.standard"=>"chassis.standard","frame.heavy"=>"chassis.heavy","frame.precision"=>"chassis.precision","frame.battery"=>"chassis.battery",_=>null };
            if (frame == null) return null;
            var mounts = new List<WeaponMountSelection>();
            foreach (var m in t.Mounts)
            {
                string weapon = m.Weapon.Mechanism.Id switch {"core.gun"=>"weapon.gun","core.piercer"=>"weapon.piercer","core.mortar"=>"weapon.mortar","core.fan"=>"weapon.fan","core.ricochet"=>"weapon.ricochet","core.stapler"=>"weapon.stapler","core.blotter"=>"weapon.blotter",_=>null};
                if (weapon == null) return null; mounts.Add(new WeaponMountSelection(m.SlotIndex,weapon));
            }
            var modules = new List<ModuleSelection>();
            if (t.Foundation.Armor.Id == "armor.plate") modules.Add(new ModuleSelection("armor","module.armor"));
            if (t.Foundation.Feed.Id == "feed.boost") modules.Add(new ModuleSelection("feed","module.feed"));
            if (t.Foundation.Turn.Id == "turn.quick") modules.Add(new ModuleSelection("tracking","module.tracking"));
            return new TowerBlueprint(entry.Id,entry.Name,frame,mounts,modules);
        }
        public bool ImportStarter(string starterId, ComponentCatalog catalog, out string reason)
        {
            reason = "";
            var starter = catalog?.Starters.FirstOrDefault(e => e.Id == starterId);
            if (starter == null) { reason = "profile.starter.unknown"; return false; }
            if (entries.Any(e => e.Id == starterId)) return true;
            if (entries.Count >= ProfileLimits.MaximumAlbumPages) { reason = "profile.album.full"; return false; }
            AddOwnedEntry(starter); GrantInitialComponents(catalog); return true;
        }
        void WriteAssemblyData(WorkshopProfileData data)
        {
            data.TotalXp = TotalXp; data.Entries = entries.Select(e => new AlbumEntryData(e)).ToArray();
            data.Library = library.Select(e => new ComponentLibraryEntryData(e)).ToArray();
            data.Rewards = rewards.Select(Copy).ToArray(); data.Operations = operations.Select(Copy).ToArray();
        }
        void CopyAssemblyStateTo(WorkshopProfile copy)
        {
            copy.TotalXp=TotalXp; copy.entries.Clear(); copy.entries.AddRange(entries); copy.library.Clear(); copy.library.AddRange(library);
            copy.rewards.Clear(); copy.rewards.AddRange(rewards.Select(Copy)); copy.operations.Clear(); copy.operations.AddRange(operations.Select(Copy));
        }
    }
}
