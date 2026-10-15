using System;
using System.Collections.Generic;
namespace PaperDefense.Core
{
    public enum DrawingProbeFormation { Single, Column, Front }
    public sealed class DrawingProbeReport
    {
        public IReadOnlyList<TrialFrame> Frames { get; }
        public int Hits { get; }
        public int Targets { get; }
        public int Launches { get; }
        public int Cost { get; }
        public float Damage { get; }
        public float Range { get; }
        public bool Completed { get; }
        public string Message { get; }
        internal DrawingProbeReport(List<TrialFrame> frames,int hits,int targets,int launches,int cost,float damage,float range,bool completed,string message)
        {Frames=frames.AsReadOnly();Hits=hits;Targets=targets;Launches=launches;Cost=cost;Damage=damage;Range=range;Completed=completed;Message=message;}
    }
    public static class DrawingProbe
    {
        public const float TargetHealth=1000,Step=1f/30,MaximumDistance=30;
        public static DrawingProbeReport Run(TowerBlueprint source,int mount,float distance,DrawingProbeFormation formation,GameBalance balance)
        {
            ValidateFixture(distance,formation);
            BalanceValidation.Validate(balance);
            if(!TowerCatalog.Validate(source,balance,out string reason))throw new ArgumentException(reason,nameof(source));
            var rules=balance.CreateSnapshot();source=source.WithContent(rules.ResolveContent());
            string id=source.GetWeaponId(mount);
            if(id==null)throw new ArgumentOutOfRangeException(nameof(mount));
            var weapon=rules.ResolveContent().GetWeapon(id);
            if(weapon.Attack==AttackKind.Cleanup)throw new ArgumentException(UserText.Message("drawing.probe.cleanup"),nameof(source));
            float range=TowerCatalog.WeaponRange(source,rules,id,mount);
            float health=TowerCatalog.Health(source,rules,MaterialTier.Pencil);
            var tower=Tower(source,health);var towers=new List<StructureState>{tower};
            var enemies=Targets(distance,formation);
            bool reachable=false;
            foreach(var enemy in enemies)
            {
                float x=enemy.Position.X-10.5f,y=enemy.Position.Y-10.5f,d=(float)Math.Sqrt(x*x+y*y);
                if(d>=weapon.MinimumRange&&d<=range)reachable=true;
            }
            float quality=source.Origin==DrawingOrigin.LegacyStandard?100:source.Assessment.Quality;
            var system=new WeaponSystem(rules,quality);var events=new SimulationEventBuffer(256);
            var stats=new RunStatistics();var shots=new List<ShotEvent>();var impacts=new List<SimulationEvent>();
            var frames=new List<TrialFrame>{new TrialFrame(0,enemies,towers,system.Projectiles,shots)};
            bool completed=false;
            for(int tick=1;tick<=600;tick++)
            {
                shots.Clear();
                // Once an action starts, only advance its projectiles. Keep the tower visible.
                system.FireIsolatedMount(stats.Launches(id)>0?null:tower,mount,enemies,shots,Step,tick,stats,events,tick*Step);
                foreach(var value in events)
                    if(value.Tick==tick&&value.Kind==SimulationEventKind.ProjectileImpact)impacts.Add(value);
                frames.Add(new TrialFrame(tick*Step,enemies,towers,system.Projectiles,shots,impacts:impacts));
                if(!reachable||(stats.Launches(id)>0&&!system.HasPendingProjectiles)){completed=true;break;}
            }
            int hits=0;float damage=0;
            foreach(var enemy in enemies){if(enemy.Health<TargetHealth)hits++;damage+=TargetHealth-Math.Max(0,enemy.Health);}
            string message=UserText.Message(!completed?"drawing.probe.timeout":!reachable?"drawing.probe.out_of_range":"drawing.probe.finished");
            return new DrawingProbeReport(frames,hits,enemies.Count,stats.Launches(id),TowerCatalog.Cost(source,rules,MaterialTier.Pencil),damage,range,completed,message);
        }
        public static TrialFrame Preview(TowerBlueprint source,float distance,DrawingProbeFormation formation)
        {
            ValidateFixture(distance,formation);
            return new TrialFrame(0,Targets(distance,formation),new[]{Tower(source,100)},Array.Empty<MortarProjectile>(),Array.Empty<ShotEvent>());
        }
        static StructureState Tower(TowerBlueprint source,float health)=>new StructureState
        {Id=1,Cell=new Cell(10,10),Kind=source.FirstWeapon,ContentId=source.FirstWeaponId,Blueprint=source,Tier=MaterialTier.Pencil,
            Color=InkColor.None,IsComplete=true,Health=health,MaxHealth=health};
        static List<EnemyState> Targets(float distance,DrawingProbeFormation formation)
        {
            var result=new List<EnemyState>();int count=formation==DrawingProbeFormation.Single?1:5;
            for(int i=0;i<count;i++)
            {
                float x=formation==DrawingProbeFormation.Column?i*.8f:0;
                float y=formation==DrawingProbeFormation.Front?(i==0?0:(i%2==1?1:-1)*((i+1)/2))*.8f:0;
                result.Add(new EnemyState{Id=i+1,Position=new Point2(10.5f+distance+x,10.5f+y),Health=TargetHealth,MaxHealth=TargetHealth});
            }
            return result;
        }
        static void ValidateFixture(float distance,DrawingProbeFormation formation)
        {
            if(float.IsNaN(distance)||distance<1||distance>MaximumDistance)throw new ArgumentOutOfRangeException(nameof(distance));
            if(!Enum.IsDefined(typeof(DrawingProbeFormation),formation))throw new ArgumentOutOfRangeException(nameof(formation));
        }
    }
}
