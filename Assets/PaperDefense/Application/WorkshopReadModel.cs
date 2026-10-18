using System.Collections.Generic;
using PaperDefense.Core;

namespace PaperDefense.Application
{
    public sealed class WorkshopReadModel
    {
        readonly WorkshopProfile source;
        public int Gold=>source.Gold;
        public int TotalXp => source.TotalXp;
        public int Level => MetaProgression.LevelForXp(source.TotalXp);
        public IReadOnlyList<AlbumEntry> Entries => source.Entries;
        public IReadOnlyList<ComponentLibraryEntry> Library => source.Library;
        public IReadOnlyList<AlbumEntry> LoadoutEntries => source.GetLoadoutEntries();
        public IReadOnlyList<TowerBlueprint> Album { get; private set; }
        public IReadOnlyList<PartUnlock> Parts { get; private set; }
        public IReadOnlyList<DrawingUnlockDefinition> DrawingUnlocks { get; private set; }
        public ContentCatalogSnapshot Content { get; private set; }
        public IReadOnlyList<TowerBlueprint> Loadout {get;private set;}
        public ProfileSaveState SaveState { get; internal set; } = ProfileSaveState.MemoryOnly;
        public string SaveMessage { get; internal set; } = UserText.Message("copy.0780dfa419e08bc0");
        public bool HasUnsavedChanges { get; internal set; }
        public string LastWriteError { get; internal set; } = "";
        public bool IsUnlocked(string id)=>source.IsUnlocked(id);
        public RunRewardReceipt FindRunReward(System.Guid runId) => source.FindRunReward(runId);
        /// <summary>Composite previews consume the page's owned snapshot; legacy pages keep historical resolution.</summary>
        public AssemblyResolution PreviewAssembly(AlbumEntry entry,GameBalance balance,MaterialTier material=MaterialTier.Pencil)
            =>TowerCatalog.ResolveEntry(entry,balance,material);
        public bool IsDrawingUnlocked(string id)
        {foreach(var definition in DrawingUnlocks)if(definition.Id==id)return definition.InitiallyUnlocked||source.IsUnlocked(id);return false;}
        internal WorkshopReadModel(WorkshopProfile profile, GameBalance balance=null){source=profile;Refresh(balance);}
        internal void Refresh(GameBalance balance=null)
        {
            Content=balance?.ResolveContent(); Parts=source.GetParts(balance);DrawingUnlocks=source.GetDrawingUnlocks(balance);
            var album=new TowerBlueprint[source.Album.Count];
            for(int i=0;i<album.Length;i++)album[i]=PreparePage(source.Album[i]);
            Album=System.Array.AsReadOnly(album);
            var selected=source.GetLoadoutBlueprints();var loadout=new TowerBlueprint[selected.Count];
            for(int i=0;i<loadout.Length;i++)loadout[i]=PreparePage(selected[i]);
            Loadout=System.Array.AsReadOnly(loadout);
        }
        TowerBlueprint PreparePage(TowerBlueprint blueprint)
        {
            if(blueprint==null||Content==null||!Content.TryGetChassis(blueprint.ChassisId,out _))return blueprint;
            foreach(var mount in blueprint.WeaponMounts)if(!Content.TryGetWeapon(mount.WeaponId,out _))return blueprint;
            foreach(var selection in blueprint.ModuleSelections)if(!Content.TryGetModule(selection.DefinitionId,out _))return blueprint;
            return blueprint.WithContent(Content);
        }
    }
    public readonly struct TowerPreview
    {
        public readonly int Load,Capacity,Cost,ColorCost;
        public readonly float Health,Defense,BuildSeconds,FireRate,Range,FeedMultiplier;
        public readonly float DrawingQuality;
        public readonly bool Valid;
        public readonly string Reason;
        public readonly IReadOnlyList<WeaponPreview> Weapons;
        public readonly FreeformTowerDesign FreeformDesign;
        internal TowerPreview(TowerBlueprint bp,GameBalance balance,MaterialTier material)
        {
            DrawingQuality=0;Defense=0;Weapons=System.Array.Empty<WeaponPreview>();FreeformDesign=null;
            Valid=TowerCatalog.Validate(bp,balance,out string reason);Reason=reason;
            if(bp==null){Load=Capacity=Cost=ColorCost=0;Health=BuildSeconds=FireRate=Range=FeedMultiplier=0;return;}
            var content = balance.ResolveContent();
            bool known=content.TryGetChassis(bp.ChassisId,out _);
            foreach(var mount in bp.WeaponMounts)known&=content.TryGetWeapon(mount.WeaponId,out _);
            foreach(var selection in bp.ModuleSelections) known &= content.TryGetModule(selection.DefinitionId,out _);
            if(!known)
            {Load=Capacity=Cost=ColorCost=0;Health=BuildSeconds=FireRate=Range=FeedMultiplier=0;return;}
            bp=bp.WithContent(content);
            Valid=TowerCatalog.Validate(bp,balance,out reason);Reason=reason;
            FreeformDesign=bp.FreeformDesign;
            DrawingQuality=bp.Origin==DrawingOrigin.LegacyStandard?100:bp.Assessment.Quality;
            try
            {
            Defense=DamageRules.EffectiveDefense(TowerCatalog.Defense(bp,balance),balance.Effects.MaximumDefense);
            Load=TowerCatalog.Load(bp,balance);Capacity=TowerCatalog.Capacity(bp,balance);Cost=TowerCatalog.Cost(bp,balance,material);
            ColorCost=TowerCatalog.ColorCost(bp,balance);Health=TowerCatalog.Health(bp,balance,material);
            BuildSeconds=TowerCatalog.BuildSeconds(bp,balance,material);FeedMultiplier=TowerCatalog.FeedMultiplier(bp,balance);
            Range=FireRate=0;
            var weapons=new List<WeaponPreview>();
            foreach(var mount in bp.WeaponMounts)
            {
                float range=TowerCatalog.WeaponRange(bp,balance,mount.WeaponId,mount.SlotIndex);
                float interval=TowerCatalog.ActionInterval(bp,balance,mount.WeaponId,FeedMultiplier,mount.SlotIndex);
                weapons.Add(new WeaponPreview(mount.SlotIndex,range,interval,TowerCatalog.WeaponDamage(bp,balance,mount.WeaponId,material,mount.SlotIndex),FeedMultiplier));
                Range=System.Math.Max(Range,range);FireRate+=1/interval;
            }
            Weapons=weapons.AsReadOnly();
            }
            catch(System.ArgumentException error)
            {
                Valid=false;Reason=error.Message;Weapons=System.Array.Empty<WeaponPreview>();
                Load=Capacity=Cost=ColorCost=0;Health=Defense=BuildSeconds=FireRate=Range=FeedMultiplier=0;
            }
        }
    }
    public readonly struct WeaponPreview
    {
        public readonly int SlotIndex;
        public readonly float Damage,Range,FireRate,Interval,FeedMultiplier;
        internal WeaponPreview(int slot,float range,float interval,float damage,float feed)
        {SlotIndex=slot;Range=range;Interval=interval;Damage=damage;FeedMultiplier=feed;FireRate=1/interval;}
    }
}
