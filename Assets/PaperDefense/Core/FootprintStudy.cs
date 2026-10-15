using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public enum FootprintStudyShape { Compact, Longitudinal, Transverse }
    public enum FootprintStudyScenario { Column, Dispersed, Turning }

    public sealed class FootprintStudySample
    {
        public FootprintProbeReport Recording { get; }
        public float Damage { get; }
        public int Launches { get; }
        public float TurnSeconds { get; }
        internal FootprintStudySample(FootprintProbeReport recording,float damage,int launches,float turnSeconds)
        { Recording=recording;Damage=damage;Launches=launches;TurnSeconds=turnSeconds; }
    }
    public sealed class FootprintStudyCase
    {
        public FootprintStudyScenario Scenario { get; }
        public FootprintStudyShape Form { get; }
        public MortarFootprint Footprint { get; }
        public IReadOnlyList<FootprintStudySample> Samples { get; }
        public int Hits { get; }
        public float Damage { get; }
        public bool Completed { get; }
        internal FootprintStudyCase(FootprintStudyScenario scenario,FootprintStudyShape form,MortarFootprint footprint,FootprintStudySample[] samples)
        {
            Scenario=scenario;Form=form;Footprint=footprint;Samples=Array.AsReadOnly(samples);Completed=true;
            foreach(var sample in samples){Hits+=sample.Recording.HitCount;Damage+=sample.Damage;Completed&=sample.Recording.Completed;}
        }
    }
    public sealed class FootprintStudyReport
    {
        public const int Revision = 1;
        public TowerBlueprint Source { get; }
        public int Mount { get; }
        public int Budget { get; }
        public float Radius { get; }
        public float FlightSeconds { get; }
        public IReadOnlyList<FootprintStudyCase> Cases { get; }
        internal FootprintStudyReport(TowerBlueprint source,int mount,int budget,float radius,float flight,FootprintStudyCase[] cases)
        { Source=source;Mount=mount;Budget=budget;Radius=radius;FlightSeconds=flight;Cases=Array.AsReadOnly(cases); }
        public FootprintStudyCase Find(FootprintStudyScenario scenario,FootprintStudyShape form)
        {
            if(!Enum.IsDefined(typeof(FootprintStudyScenario),scenario)||!Enum.IsDefined(typeof(FootprintStudyShape),form))throw new ArgumentOutOfRangeException();
            return Cases[(int)scenario*3+(int)form];
        }
    }
    public static class FootprintStudy
    {
        public const int Repeats = 3;
        public const int Targets = 9;
        public const float Delta = 1f/30;
        public static MortarFootprint CreateShape(FootprintStudyShape form,float maximumAspect=MortarFootprint.MaximumAspectRatio)
        {
            if(!Enum.IsDefined(typeof(FootprintStudyShape),form))throw new ArgumentOutOfRangeException(nameof(form));
            if(float.IsNaN(maximumAspect)||float.IsInfinity(maximumAspect)||maximumAspect<1||maximumAspect>MortarFootprint.StructuralMaximumAspectRatio)throw new ArgumentOutOfRangeException(nameof(maximumAspect));
            if(form==FootprintStudyShape.Compact||maximumAspect==1)return MortarFootprint.Circle;
            double along=Math.Sqrt(Math.Min(2.4f,maximumAspect)),across=1/along;
            var points=new Point2[64];
            for(int i=0;i<points.Length;i++)
            {
                double angle=i*Math.PI*2/points.Length;float x=(float)(Math.Cos(angle)*across),y=(float)(Math.Sin(angle)*along);
                points[i]=form==FootprintStudyShape.Transverse?new Point2(y,-x):new Point2(x,y);
            }
            return MortarFootprint.FromContour(points,maximumAspect);
        }
        public static FootprintStudyReport Run(TowerBlueprint source,int mount,GameBalance balance)
        {
            BalanceValidation.Validate(balance);
            if(!TowerCatalog.Validate(source,balance,out string reason))throw new ArgumentException(reason,nameof(source));
            var rules=balance.CreateSnapshot();source=source.WithContent(rules.ResolveContent());source.GetMortarFootprint(mount);
            var weapon=rules.ResolveContent().GetWeapon(source.GetWeaponId(mount));
            if(weapon.Stats.Radius<=0||weapon.Stats.FlightSeconds<Delta*4)
                throw new ArgumentException(UserText.Message("study.unsupported"));
            int budget=TowerCatalog.Cost(source,rules,MaterialTier.Pencil);
            var cases=new FootprintStudyCase[9];
            for(int scenario=0;scenario<3;scenario++)for(int form=0;form<3;form++)
            {
                var footprint=CreateShape((FootprintStudyShape)form,weapon.MaximumFootprintAspectRatio);
                var samples=new FootprintStudySample[Repeats];
                for(int repeat=0;repeat<Repeats;repeat++)samples[repeat]=Shoot(source,mount,footprint,rules,(FootprintStudyScenario)scenario,repeat,budget);
                cases[scenario*3+form]=new FootprintStudyCase((FootprintStudyScenario)scenario,(FootprintStudyShape)form,footprint,samples);
            }
            return new FootprintStudyReport(source,mount,budget,weapon.Stats.Radius,weapon.Stats.FlightSeconds,cases);
        }
        static FootprintStudySample Shoot(TowerBlueprint source,int mount,MortarFootprint footprint,GameBalance rules,FootprintStudyScenario scenario,int repeat,int cost)
        {
            var selected=rules.ResolveContent().GetWeapon(source.GetWeaponId(mount));
            var blueprint=source.WithMortarFootprint(mount,footprint);
            var tower=new StructureState{Id=1,Cell=new Cell(10,10),Kind=source.FirstWeapon,ContentId=source.FirstWeaponId,Blueprint=blueprint,
                Tier=MaterialTier.Pencil,Color=InkColor.None,IsComplete=true,Health=TowerCatalog.Health(source,rules,MaterialTier.Pencil)};
            tower.MaxHealth=tower.Health;
            // All candidates start with the same ready mount and acquired nearest target.
            tower.GetMountState(mount).TargetId=1;
            var towers=new List<StructureState>{tower};var enemies=new List<EnemyState>(Targets);
            float radius=selected.Stats.Radius;
            float range=TowerCatalog.WeaponRange(source,rules,selected.Id,mount);
            float distance=selected.MinimumRange+(range-selected.MinimumRange)*.55f;
            var anchor=new Point2(10.5f+distance,10.5f);
            var starts=new Point2[Targets];
            for(int i=0;i<Targets;i++)
            {
                var offset=Offset(scenario,i,repeat);
                starts[i]=new Point2(anchor.X+offset.X*radius,anchor.Y+offset.Y*radius);
                enemies.Add(new EnemyState{Id=i+1,Position=starts[i],Health=100,MaxHealth=100});
            }
            float flight=selected.Stats.FlightSeconds;
            float turn=Delta+flight*(repeat==0?.35f:repeat==1?.65f:.85f);
            // Normalized movement per flight makes this a reproducible geometry experiment,
            // not a forecast of campaign movement or a modification of enemy balance.
            float speed=radius*(scenario==FootprintStudyScenario.Turning?.8f:scenario==FootprintStudyScenario.Column?.4f:.2f)/Math.Max(.1f,flight);
            float quality=source.Origin==DrawingOrigin.LegacyStandard?100:source.Assessment.Quality;
            var weapons=new WeaponSystem(rules);var events=new SimulationEventBuffer(32);var statistics=new RunStatistics();
            var shots=new List<ShotEvent>();var frames=new List<TrialFrame>();var impacts=new List<SimulationEvent>();
            frames.Add(new TrialFrame(0,enemies,towers,weapons.Projectiles,shots));int launches=0;
            for(int tick=1;tick<=600;tick++)
            {
                float time=tick*Delta;
                float dx=-speed*(scenario==FootprintStudyScenario.Turning?Math.Min(time,turn):time);
                float dy=scenario==FootprintStudyScenario.Turning?-speed*Math.Max(0,time-turn):0;
                for(int i=0;i<Targets;i++)enemies[i].Position=new Point2(starts[i].X+dx,starts[i].Y+dy);
                shots.Clear();
                weapons.FireIsolatedMount(towers.Count==0?null:tower,mount,enemies,shots,Delta,tick,statistics,events,time);
                foreach(var value in events)if(value.Tick==tick)
                {
                    if(value.Kind==SimulationEventKind.ProjectileLaunched)launches++;
                    if(value.Kind==SimulationEventKind.ProjectileImpact)impacts.Add(value);
                }
                frames.Add(new TrialFrame(time,enemies,towers,weapons.Projectiles,shots,impacts:impacts));
                if(weapons.HasPendingProjectiles)towers.Clear();
                if(impacts.Count>0)break;
            }
            int hits=0;float damage=0;
            foreach(var enemy in enemies){if(enemy.Health<enemy.MaxHealth)hits++;damage+=enemy.MaxHealth-enemy.Health;}
            bool complete=impacts.Count==1&&launches==1;
            return new FootprintStudySample(new FootprintProbeReport(frames,hits,cost,quality,complete,
                UserText.Message(complete?"copy.8d5b727e93d4fa3b":"copy.47ed21b8c7abce75")),damage,launches,turn);
        }
        static Point2 Offset(FootprintStudyScenario scenario,int index,int repeat)
        {
            if(index==0)return default;
            if(scenario==FootprintStudyScenario.Column)return new Point2(index*(.32f+repeat*.03f),(index%2==0?1:-1)*.04f);
            if(scenario==FootprintStudyScenario.Dispersed)
            {
                var points=new[]{new Point2(.45f,-.7f),new Point2(.45f,.7f),new Point2(1.2f,-1.4f),new Point2(1.2f,1.4f),
                    new Point2(.15f,-1.5f),new Point2(.15f,1.5f),new Point2(1.4f,-.2f),new Point2(1.4f,.2f)};
                var p=points[index-1];return new Point2(p.X,p.Y*(.95f+repeat*.05f));
            }
            // A compact stream bends while the shell is airborne; no target is teleported.
            var group=new[]{new Point2(0,-.55f),new Point2(0,.55f),new Point2(.45f,-.55f),new Point2(.45f,0),
                new Point2(.45f,.55f),new Point2(.9f,-.55f),new Point2(.9f,0),new Point2(.9f,.55f)};
            return group[index-1];
        }
    }
}
