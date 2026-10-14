using System;
using System.Collections.Generic;
namespace PaperDefense.Core
{
    public enum FootprintFormation { Group, Column, Line }
    public sealed class FootprintProbeComparison
    {
        public FootprintProbeReport First { get; }
        public FootprintProbeReport Second { get; }
        internal FootprintProbeComparison(FootprintProbeReport first, FootprintProbeReport second) { First=first; Second=second; }
    }
    public sealed class FootprintProbeReport
    {
        public IReadOnlyList<TrialFrame> Frames { get; }
        public int HitCount { get; }
        public int MissedCount => 9-HitCount;
        public int Cost { get; }
        public float DrawingQuality { get; }
        public bool Completed { get; }
        public string Message { get; }
        internal FootprintProbeReport(List<TrialFrame> frames,int hits,int cost,float quality,bool completed,string message)
        { Frames=Array.AsReadOnly(frames.ToArray()); HitCount=hits; Cost=cost; DrawingQuality=quality; Completed=completed; Message=message; }
    }
    public static class MortarFootprintProbe
    {
        public static FootprintProbeComparison Compare(TowerBlueprint blueprint,int mount,MortarFootprint first,MortarFootprint second,GameBalance balance,FootprintFormation formation)
        {
            BalanceValidation.Validate(balance);
            if (!TowerCatalog.Validate(blueprint,balance,out string reason)) throw new ArgumentException(reason,nameof(blueprint));
            var rules=balance.CreateSnapshot();
            blueprint=blueprint.WithContent(rules.ResolveContent());
            blueprint.GetMortarFootprint(mount);
            if(first==null) throw new ArgumentNullException(nameof(first));
            if(second==null) throw new ArgumentNullException(nameof(second));
            if(!Enum.IsDefined(typeof(FootprintFormation),formation)) throw new ArgumentOutOfRangeException(nameof(formation));
            var weapon=rules.ResolveContent().GetWeapon(blueprint.GetWeaponId(mount));
            if(!first.FitsAspectLimit(weapon.MaximumFootprintAspectRatio)||!second.FitsAspectLimit(weapon.MaximumFootprintAspectRatio))throw new ArgumentException(UserText.Message("copy.a630b0b26793f9fd"));
            float quality=blueprint.Origin==DrawingOrigin.LegacyStandard?100:blueprint.Assessment.Quality;
            int cost=TowerCatalog.Cost(blueprint,rules,MaterialTier.Pencil);
            return new FootprintProbeComparison(Run(blueprint,mount,first,rules,formation,cost,quality),Run(blueprint,mount,second,rules,formation,cost,quality));
        }

        static FootprintProbeReport Run(TowerBlueprint source,int mount,MortarFootprint footprint,GameBalance balance,FootprintFormation formation,int cost,float quality)
        {
            // Isolate the selected barrel while retaining equipment and art. Its re-evaluated
            // drawing assessment must not change cadence: use the source page's quality.
            var isolated=source.WithMortarFootprint(mount,footprint);
            var selected=balance.ResolveContent().GetWeapon(source.GetWeaponId(mount));
            float health=TowerCatalog.Health(source,balance,MaterialTier.Pencil);
            var towers=new List<StructureState> { new StructureState { Id=1,Cell=new Cell(10,10),Kind=isolated.FirstWeapon,ContentId=isolated.FirstWeaponId,
                Blueprint=isolated,Tier=MaterialTier.Pencil,Color=InkColor.None,IsComplete=true,Health=health,MaxHealth=health } };
            var enemies=new List<EnemyState>(9);
            float radius=selected.Stats.Radius;
            // Central nearest anchor naturally wins ordinary nearest-target acquisition.
            var anchor=new Point2(10.5f+Math.Max(4,selected.MinimumRange),10.5f);
            for(int i=0;i<9;i++)
            {
                float x=formation==FootprintFormation.Group?(i/3)*.3f*radius:i*.2f*radius;
                float y=formation==FootprintFormation.Group?(i%3-1)*.7f*radius:0;
                if(formation==FootprintFormation.Line){x=0;y=(i==0?0:(i%2==1?1:-1)*((i+1)/2))*.4f*radius;}
                enemies.Add(new EnemyState { Id=i+1,Position=new Point2(anchor.X+x,anchor.Y+y),Health=100,MaxHealth=100 });
            }
            var frames=new List<TrialFrame>(601);
            var weapons=new WeaponSystem(balance,quality);
            var events=new SimulationEventBuffer(16);
            var stats=new RunStatistics(); var shots=new List<ShotEvent>();
            var impacts=new List<SimulationEvent>();
            frames.Add(new TrialFrame(0,enemies,towers,weapons.Projectiles,shots));
            const float delta=1f/30;
            bool completed=false;
            for(int tick=1;tick<=600;tick++)
            {
                shots.Clear();
                weapons.FireIsolatedMount(towers.Count==0?null:towers[0],mount,enemies,shots,delta,tick,stats,events,tick*delta);
                foreach(var value in events)
                    if(value.Tick==tick && value.Kind==SimulationEventKind.ProjectileImpact) impacts.Add(value);
                frames.Add(new TrialFrame(tick*delta,enemies,towers,weapons.Projectiles,shots,impacts:impacts));
                if(weapons.HasPendingProjectiles) towers.Clear(); // Launched shells outlive their source.
                if(impacts.Count>0) { completed=true; break; }
            }
            int hits=0;
            foreach(var enemy in enemies) if(enemy.Health<enemy.MaxHealth) hits++;
            return new FootprintProbeReport(frames,hits,cost,quality,completed,completed
                ? UserText.Message("copy.8d5b727e93d4fa3b")
                : UserText.Message("copy.47ed21b8c7abce75"));
        }
    }
}

