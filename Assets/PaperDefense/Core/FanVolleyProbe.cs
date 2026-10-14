using System;
using System.Collections.Generic;
namespace PaperDefense.Core
{
    public enum FanFormation { Single, Front, Split }
    public sealed class FanVolleyProbeReport
    {
        public IReadOnlyList<TrialFrame> Frames { get; }
        public int Hits { get; } public int Targets { get; } public int Pellets { get; } public int Cost { get; }
        public float Damage { get; } public float Range { get; } public bool Completed { get; }
        internal FanVolleyProbeReport(List<TrialFrame> frames,int hits,int targets,int pellets,int cost,float damage,float range,bool completed)
        { Frames=Array.AsReadOnly(frames.ToArray());Hits=hits;Targets=targets;Pellets=pellets;Cost=cost;Damage=damage;Range=range;Completed=completed; }
    }
    public static class FanVolleyProbe
    {
        public static FanVolleyProbeReport Run(TowerBlueprint source,int mount,FanFormation formation,GameBalance balance)
        {
            BalanceValidation.Validate(balance);
            if(!Enum.IsDefined(typeof(FanFormation),formation))throw new ArgumentOutOfRangeException(nameof(formation));
            if(!TowerCatalog.Validate(source,balance,out string reason))throw new ArgumentException(reason);
            var rules=balance.CreateSnapshot();source=source.WithContent(rules.ResolveContent());source.GetFanVolley(mount);
            var weapon=rules.ResolveContent().GetWeapon(source.GetWeaponId(mount));float range=TowerCatalog.WeaponRange(source,rules,weapon.Id,mount);
            var tower=new StructureState { Id=1,Cell=new Cell(10,10),Kind=source.FirstWeapon,ContentId=source.FirstWeaponId,Blueprint=source,Tier=MaterialTier.Pencil,IsComplete=true,Health=100,MaxHealth=100 };
            tower.GetMountState(mount).TargetId=1;
            var towers=new List<StructureState>{tower};var enemies=new List<EnemyState>();
            float distance=weapon.MinimumRange+(range-weapon.MinimumRange)*.55f;
            float[] angles=formation==FanFormation.Single?new[]{0f}:formation==FanFormation.Front?new[]{0f,-40,-30,-20,20,30,40}:new[]{0f,-30,-30,-30,30,30,30};
            for(int i=0;i<angles.Length;i++)
            {
                double a=angles[i]*Math.PI/180;float d=i==0?distance:Math.Min(range,distance+range*(formation==FanFormation.Split?.06f*((i-1)%3+1):.05f));
                enemies.Add(new EnemyState { Id=i+1,Position=new Point2(10.5f+(float)Math.Cos(a)*d,10.5f-(float)Math.Sin(a)*d),Health=100,MaxHealth=100 });
            }
            float quality=source.Origin==DrawingOrigin.LegacyStandard?100:source.Assessment.Quality;
            var system=new WeaponSystem(rules,quality);var events=new SimulationEventBuffer(256);var stats=new RunStatistics();var shots=new List<ShotEvent>();var frames=new List<TrialFrame>();
            frames.Add(new TrialFrame(0,enemies,towers,system.Projectiles,shots));bool completed=false;
            for(int tick=1;tick<=600;tick++)
            {
                shots.Clear();system.FireIsolatedMount(tower,mount,enemies,shots,1f/30,tick,stats,events,tick/30f);
                frames.Add(new TrialFrame(tick/30f,enemies,towers,system.Projectiles,shots));
                if(stats.Launches(weapon.Id)>0){completed=true;break;}
            }
            int hits=0;float damage=0;foreach(var enemy in enemies){if(enemy.Health<100)hits++;damage+=100-enemy.Health;}
            return new FanVolleyProbeReport(frames,hits,enemies.Count,shots.Count,TowerCatalog.Cost(source,rules,MaterialTier.Pencil),damage,range,completed);
        }
    }
}
