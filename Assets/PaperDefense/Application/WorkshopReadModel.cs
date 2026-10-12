using System.Collections.Generic;
using PaperDefense.Core;

namespace PaperDefense.Application
{
    public sealed class WorkshopReadModel
    {
        readonly WorkshopProfile source;
        public int Gold=>source.Gold;
        public IReadOnlyList<TowerBlueprint> Album=>source.Album;
        public IReadOnlyList<PartUnlock> Parts { get; private set; }
        public IReadOnlyList<TowerBlueprint> Loadout {get;private set;}
        public ProfileSaveState SaveState { get; internal set; } = ProfileSaveState.MemoryOnly;
        public string SaveMessage { get; internal set; } = "Только в памяти; профиль не записывается.";
        public bool HasUnsavedChanges { get; internal set; }
        public string LastWriteError { get; internal set; } = "";
        public bool IsUnlocked(string id)=>source.IsUnlocked(id);
        internal WorkshopReadModel(WorkshopProfile profile, GameBalance balance=null){source=profile;Refresh(balance);}
        internal void Refresh(GameBalance balance=null){Loadout=source.GetLoadoutBlueprints();Parts=source.GetParts(balance);}
    }
    public readonly struct TowerPreview
    {
        public readonly int Load,Capacity,Cost,ColorCost;
        public readonly float Health,Defense,BuildSeconds,FireRate,Range,FeedMultiplier;
        public readonly float DrawingQuality,QualityMultiplier;
        public readonly bool Valid;
        public readonly string Reason;
        internal TowerPreview(TowerBlueprint bp,GameBalance balance,MaterialTier material)
        {
            DrawingQuality=0;QualityMultiplier=1;Defense=0;
            Valid=TowerCatalog.Validate(bp,balance,out string reason);Reason=reason;
            if(bp==null){Load=Capacity=Cost=ColorCost=0;Health=BuildSeconds=FireRate=Range=FeedMultiplier=0;return;}
            if(!WeaponValid(bp.FirstWeapon)||(bp.SecondWeapon.HasValue&&!WeaponValid(bp.SecondWeapon.Value)))
            {Load=Capacity=Cost=ColorCost=0;Health=BuildSeconds=FireRate=Range=FeedMultiplier=0;return;}
            DrawingQuality=bp.Origin==DrawingOrigin.LegacyStandard?100:bp.Assessment.Quality;
            Defense=DamageRules.EffectiveDefense(TowerCatalog.Defense(bp,balance),balance.Effects.MaximumDefense);
            QualityMultiplier=TowerCatalog.QualityMultiplier(bp,balance);
            Load=TowerCatalog.Load(bp,balance);Capacity=TowerCatalog.Capacity(bp,balance);Cost=TowerCatalog.Cost(bp,balance,material);
            ColorCost=TowerCatalog.ColorCost(bp,balance);Health=TowerCatalog.Health(bp,balance,material);
            BuildSeconds=TowerCatalog.BuildSeconds(bp,balance,material);FeedMultiplier=TowerCatalog.FeedMultiplier(bp,balance);
            Range=TowerCatalog.WeaponRange(bp,balance,bp.FirstWeapon);FireRate=1/TowerCatalog.ActionInterval(bp,balance,bp.FirstWeapon,FeedMultiplier,QualityMultiplier);
            if(bp.SecondWeapon.HasValue){Range=System.Math.Max(Range,TowerCatalog.WeaponRange(bp,balance,bp.SecondWeapon.Value));FireRate+=1/TowerCatalog.ActionInterval(bp,balance,bp.SecondWeapon.Value,FeedMultiplier,QualityMultiplier);}
        }
        static bool WeaponValid(StructureKind kind)=>TowerCatalog.IsMountPart(kind);
    }
}
