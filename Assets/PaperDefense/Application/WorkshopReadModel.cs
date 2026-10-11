using System.Collections.Generic;
using PaperDefense.Core;

namespace PaperDefense.Application
{
    public sealed class WorkshopReadModel
    {
        readonly WorkshopProfile source;
        public int Gold=>source.Gold;
        public IReadOnlyList<TowerBlueprint> Album=>source.Album;
        public IReadOnlyList<PartUnlock> Parts=>source.Parts;
        public IReadOnlyList<TowerBlueprint> Loadout {get;private set;}
        public bool IsUnlocked(string id)=>source.IsUnlocked(id);
        internal WorkshopReadModel(WorkshopProfile profile){source=profile;Refresh();}
        internal void Refresh(){Loadout=source.GetLoadoutBlueprints();}
    }
    public readonly struct TowerPreview
    {
        public readonly int Load,Capacity,Cost,ColorCost;
        public readonly float Health,BuildSeconds,FireRate,Range,FeedMultiplier;
        public readonly bool Valid;
        public readonly string Reason;
        internal TowerPreview(TowerBlueprint bp,GameBalance balance,MaterialTier material)
        {
            Valid=TowerCatalog.Validate(bp,out string reason);Reason=reason;
            if(bp==null){Load=Capacity=Cost=ColorCost=0;Health=BuildSeconds=FireRate=Range=FeedMultiplier=0;return;}
            if(!WeaponValid(bp.FirstWeapon)||(bp.SecondWeapon.HasValue&&!WeaponValid(bp.SecondWeapon.Value)))
            {Load=Capacity=Cost=ColorCost=0;Health=BuildSeconds=FireRate=Range=FeedMultiplier=0;return;}
            Load=TowerCatalog.Load(bp);Capacity=TowerCatalog.Capacity(bp);Cost=TowerCatalog.Cost(bp,balance,material);
            ColorCost=TowerCatalog.ColorCost(bp,balance);Health=TowerCatalog.Health(bp,balance,material);
            BuildSeconds=TowerCatalog.BuildSeconds(bp,balance,material);FeedMultiplier=TowerCatalog.FeedMultiplier(bp);
            var first=balance.Structure(bp.FirstWeapon);Range=first.Range;FireRate=FeedMultiplier/System.Math.Max(.05f,first.ShotInterval);
            if(bp.SecondWeapon.HasValue){var second=balance.Structure(bp.SecondWeapon.Value);Range=System.Math.Max(Range,second.Range);FireRate+=FeedMultiplier/System.Math.Max(.05f,second.ShotInterval);}
        }
        static bool WeaponValid(StructureKind kind)=>kind==StructureKind.Gun||kind==StructureKind.Piercer||kind==StructureKind.Mortar;
    }
}
