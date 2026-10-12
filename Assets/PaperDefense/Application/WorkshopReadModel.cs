using System.Collections.Generic;
using PaperDefense.Core;

namespace PaperDefense.Application
{
    public sealed class WorkshopReadModel
    {
        readonly WorkshopProfile source;
        public int Gold=>source.Gold;
        public IReadOnlyList<TowerBlueprint> Album { get; private set; }
        public IReadOnlyList<PartUnlock> Parts { get; private set; }
        public ContentCatalogSnapshot Content { get; private set; }
        public IReadOnlyList<TowerBlueprint> Loadout {get;private set;}
        public ProfileSaveState SaveState { get; internal set; } = ProfileSaveState.MemoryOnly;
        public string SaveMessage { get; internal set; } = "Только в памяти; профиль не записывается.";
        public bool HasUnsavedChanges { get; internal set; }
        public string LastWriteError { get; internal set; } = "";
        public bool IsUnlocked(string id)=>source.IsUnlocked(id);
        internal WorkshopReadModel(WorkshopProfile profile, GameBalance balance=null){source=profile;Refresh(balance);}
        internal void Refresh(GameBalance balance=null)
        {
            Content=balance?.ResolveContent(); Parts=source.GetParts(balance);
            var album=new TowerBlueprint[source.Album.Count];
            for(int i=0;i<album.Length;i++)album[i]=PreparePage(source.Album[i]);
            Album=System.Array.AsReadOnly(album);
            var selected=source.GetLoadoutBlueprints();var loadout=new TowerBlueprint[selected.Count];
            for(int i=0;i<loadout.Length;i++)loadout[i]=PreparePage(selected[i]);
            Loadout=System.Array.AsReadOnly(loadout);
        }
        TowerBlueprint PreparePage(TowerBlueprint blueprint)
        {
            if(blueprint==null||Content==null||!Content.TryGetChassis(blueprint.ChassisId,out _)||
                !Content.TryGetWeapon(blueprint.FirstWeaponId,out _)||
                (blueprint.SecondWeaponId!=null&&!Content.TryGetWeapon(blueprint.SecondWeaponId,out _)))return blueprint;
            foreach(var selection in blueprint.ModuleSelections)if(!Content.TryGetModule(selection.DefinitionId,out _))return blueprint;
            return blueprint.WithContent(Content);
        }
    }
    public readonly struct TowerPreview
    {
        public readonly int Load,Capacity,Cost,ColorCost;
        public readonly float Health,Defense,BuildSeconds,FireRate,Range,FeedMultiplier;
        public readonly float DrawingQuality,QualityMultiplier;
        public readonly bool Valid;
        public readonly string Reason;
        internal TowerPreview(TowerBlueprint bp,GameBalance balance,MaterialTier material,MechanicsAvailability? mechanics=null)
        {
            DrawingQuality=0;QualityMultiplier=1;Defense=0;
            Valid=TowerCatalog.Validate(bp,balance,out string reason);Reason=reason;
            if(bp==null){Load=Capacity=Cost=ColorCost=0;Health=BuildSeconds=FireRate=Range=FeedMultiplier=0;return;}
            var content = balance.ResolveContent();
            bool known=content.TryGetChassis(bp.ChassisId,out _)&&content.TryGetWeapon(bp.FirstWeaponId,out _)&&
                (bp.SecondWeaponId==null||content.TryGetWeapon(bp.SecondWeaponId,out _));
            foreach(var selection in bp.ModuleSelections) known &= content.TryGetModule(selection.DefinitionId,out _);
            if(!known)
            {Load=Capacity=Cost=ColorCost=0;Health=BuildSeconds=FireRate=Range=FeedMultiplier=0;return;}
            bp=bp.WithContent(content);
            DrawingQuality=bp.Origin==DrawingOrigin.LegacyStandard?100:bp.Assessment.Quality;
            Defense=DamageRules.EffectiveDefense(TowerCatalog.Defense(bp,balance),balance.Effects.MaximumDefense);
            QualityMultiplier=TowerCatalog.QualityMultiplier(bp,balance,(mechanics??MechanicsAvailability.Resolve(balance)).DrawingQuality);
            Load=TowerCatalog.Load(bp,balance);Capacity=TowerCatalog.Capacity(bp,balance);Cost=TowerCatalog.Cost(bp,balance,material);
            ColorCost=TowerCatalog.ColorCost(bp,balance);Health=TowerCatalog.Health(bp,balance,material);
            BuildSeconds=TowerCatalog.BuildSeconds(bp,balance,material);FeedMultiplier=TowerCatalog.FeedMultiplier(bp,balance);
            Range=TowerCatalog.WeaponRange(bp,balance,bp.FirstWeaponId);FireRate=1/TowerCatalog.ActionInterval(bp,balance,bp.FirstWeaponId,FeedMultiplier,QualityMultiplier);
            if(bp.SecondWeaponId!=null){Range=System.Math.Max(Range,TowerCatalog.WeaponRange(bp,balance,bp.SecondWeaponId,1));FireRate+=1/TowerCatalog.ActionInterval(bp,balance,bp.SecondWeaponId,FeedMultiplier,QualityMultiplier,1);}
        }
    }
}
