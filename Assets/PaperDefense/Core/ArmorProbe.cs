using System;
using System.Collections.Generic;
namespace PaperDefense.Core
{
    public readonly struct ArmorProbeHit
    {
        public readonly int WorldSide;
        public readonly float Raw, Damage, RemainingHealth;
        public ArmorProbeHit(int side,float raw,float damage,float health){WorldSide=side;Raw=raw;Damage=damage;RemainingHealth=health;}
    }
    public sealed class ArmorProbeReport
    {
        public IReadOnlyList<ArmorProbeHit> Hits { get; }
        public float InitialHealth { get; }
        public int Cost { get; }
        internal ArmorProbeReport(List<ArmorProbeHit> hits,float health,int cost){Hits=Array.AsReadOnly(hits.ToArray());InitialHealth=health;Cost=cost;}
    }
    public static class ArmorProbe
    {
        public const float RawDamage=20;
        public static ArmorProbeReport Run(TowerBlueprint source,GameBalance balance,int worldSide=-1)
        {
            if(worldSide< -1||worldSide>=ArmorDistribution.SectorCount)throw new ArgumentOutOfRangeException(nameof(worldSide));
            BalanceValidation.Validate(balance);
            if(!TowerCatalog.Validate(source,balance,out string reason))throw new ArgumentException(reason);
            var rules=balance.CreateSnapshot();source=source.WithContent(rules.ResolveContent());
            var tower=new StructureState{Cell=new Cell(10,10),Blueprint=source,Health=TowerCatalog.Health(source,rules,MaterialTier.Pencil)};
            float initial=tower.Health;var hits=new List<ArmorProbeHit>();
            for(int side=0;side<ArmorDistribution.SectorCount;side++)
            {
                if(worldSide>=0&&side!=worldSide)continue;
                var direction=Direction(side);var origin=new Point2(10.5f+direction.X*3,10.5f+direction.Y*3);
                float damage=Math.Min(tower.Health,DamageRules.StructureDamage(RawDamage,tower,rules,origin));
                tower.Health=Math.Max(0,tower.Health-damage);hits.Add(new ArmorProbeHit(side,RawDamage,damage,tower.Health));
            }
            return new ArmorProbeReport(hits,initial,TowerCatalog.Cost(source,rules,MaterialTier.Pencil));
        }
        public static Point2 Direction(int worldSide)
        {
            if(worldSide<0||worldSide>3)throw new ArgumentOutOfRangeException(nameof(worldSide));
            return worldSide==0?new Point2(0,1):worldSide==1?new Point2(1,0):worldSide==2?new Point2(0,-1):new Point2(-1,0);
        }
    }
}
