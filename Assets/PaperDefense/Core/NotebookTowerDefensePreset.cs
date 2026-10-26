using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>Canonical main-map authoring recipe. Legacy target fixtures keep their original open fields.</summary>
    public static class NotebookTowerDefensePreset
    {
        public static GameBalance Create()=>Apply(GameBalancePresets.CreateTargetWorkshop());
        public static GameBalance Apply(GameBalance source)
        {
            var b=source.CreateSnapshot();b.Width=96;b.Height=64;b.WaveHealthGrowth=.18f;
            b.EnableEnemyMechanics=true;b.EnableInkWells=true;b.MaxEnemiesPerCell=3;
            b.Battlefield=CreateBattlefield();
            b.Crowd = new CrowdMovementRules { Enabled = true };
            var wall=b.Structure(StructureKind.Wall);wall.Cost=14;wall.MaxHealth=240;
            b.Structure(StructureKind.Spikes).Cost=22;
            EnsureTool(b,new StructureDefinition{Kind=StructureKind.TarPit,Cost=30,MaxHealth=55,BuildSeconds=.8f,Radius=.7f,SlowSpeedMultiplier=.65f,SlowDuration=.3f});
            EnsureTool(b,new StructureDefinition{Kind=StructureKind.RepairPost,Cost=80,MaxHealth=70,BuildSeconds=2,Range=5,RepairPerSecond=8});
            SetEnemy(b,EnemyKind.Doodle,60,2.5f);SetEnemy(b,EnemyKind.Runner,42,2.3f);
            SetEnemy(b,EnemyKind.Gnawer,180,3.0f);SetEnemy(b,EnemyKind.Spitter,110,2.8f);
            if(!b.EnemyDefinitions.Exists(d=>d.Kind==EnemyKind.Brute))b.EnemyDefinitions.Add(new EnemyDefinition{Kind=EnemyKind.Brute,Speed=1,Damage=22,AttackInterval=1.2f,Reward=4});
            if(!b.EnemyDefinitions.Exists(d=>d.Kind==EnemyKind.Bulwark))b.EnemyDefinitions.Add(new EnemyDefinition{Kind=EnemyKind.Bulwark,Speed=1.6f,Damage=12,AttackInterval=1.1f,Reward=3});
            SetEnemy(b,EnemyKind.Brute,420,4.0f);SetEnemy(b,EnemyKind.Bulwark,260,3.5f);
            int[] counts={28,38,50,64,80,100,125,155};
            float[] runners={0,.22f,.22f,.24f,.25f,.25f,.25f,.25f};
            float[] gnawers={0,0,.08f,.10f,.12f,.13f,.15f,.16f};
            float[] spitters={0,0,.04f,.06f,.08f,.10f,.12f,.12f};
            float[] brutes={0,0,0,.04f,.05f,.06f,.08f,.10f};
            float[] bulwarks={0,0,0,0,.06f,.08f,.10f,.12f};
            string[] titles={UserText.Message("copy.7b6e6edcf1f81892"),UserText.Message("td.wave.runners"),UserText.Message("copy.97fb351f5258f593"),UserText.Message("copy.f700d2fda189e135"),UserText.Message("copy.27d0e35cfbb3ac62"),UserText.Message("copy.e920868abb4dc2fc"),UserText.Message("copy.80ac62fb73e6f66c"),UserText.Message("copy.9c32157f34fdaef1")};
            for(int i=0;i<b.Waves.Count;i++)
            {
                int j=Math.Min(i,7);var w=b.Waves[i];w.Count=counts[j];w.Sides=15;w.SpawnInterval=Math.Max(.22f,.55f-i*.045f);
                w.RunnerFraction=runners[j];w.GnawerFraction=gnawers[j];w.SpitterFraction=spitters[j];w.BruteFraction=brutes[j];w.BulwarkFraction=bulwarks[j];
                w.OutlinedFraction=Math.Min(.35f,.06f*i);w.HatchedFraction=i>=4?Math.Min(.16f,.04f*(i-3)):0;w.Title=titles[j];
            }
            BalanceValidation.Validate(b);return b;
        }
        static void EnsureTool(GameBalance b,StructureDefinition d){b.StructureDefinitions.RemoveAll(s=>s.Kind==d.Kind);b.StructureDefinitions.Add(d);}
        static void SetEnemy(GameBalance b,EnemyKind kind,float hp,float scale){var e=b.Enemy(kind);e.MaxHealth=hp;e.VisualScale=scale;}
        public static BattlefieldDefinition CreateBattlefield()
        {
            // The old road recipe shares the scenery, but roads and marked sites are optional legacy rules.
            var d=CreateFixedRoadBattlefield();d.Mode=BattlefieldMode.OpenTerrain;
            d.Roads.Clear();d.BarricadeSites.Clear();d.Entrances.Clear();
            foreach(int y in new[]{8,24,40,56})
            {d.Entrances.Add(new BattlefieldCell(0,y));d.Entrances.Add(new BattlefieldCell(95,y));}
            foreach(int x in new[]{12,36,60,84})
            {d.Entrances.Add(new BattlefieldCell(x,63));d.Entrances.Add(new BattlefieldCell(x,0));}
            // These small islands belong to the real open-terrain map, so their
            // visible pine silhouettes and construction/navigation footprints agree.
            // The central build field and five-cell cardinal approaches stay clear.
            Pine(38,35);Pine(57,35);Pine(38,27);Pine(57,27);
            return d;
            void Pine(int x,int y)=>d.Props.Add(new TerrainPropDefinition{Kind=TerrainPropKind.Pine,X=x,Y=y,Width=2,Height=3});
        }
        public static BattlefieldDefinition CreateFixedRoadBattlefield()
        {
            var d=new BattlefieldDefinition{Enabled=true};var road=new HashSet<Cell>();
            AddPath(0,16,19,16,19,30,34,30,34,20,48,20,48,32);
            AddPath(95,16,76,16,76,30,62,30,62,44,48,44,48,32);
            AddPath(48,63,48,53,34,53,34,44,48,44);
            foreach(var c in road)d.Roads.Add(new BattlefieldCell(c.X,c.Y));
            // Stable serialization independent of HashSet iteration and runtime version.
            d.Roads.Sort((a,b)=>a.Y==b.Y?a.X.CompareTo(b.X):a.Y.CompareTo(b.Y));
            d.Entrances.Add(new BattlefieldCell(0,16));d.Entrances.Add(new BattlefieldCell(95,16));d.Entrances.Add(new BattlefieldCell(48,63));
            foreach(var c in new[]{new Cell(12,16),new Cell(19,25),new Cell(34,25),new Cell(42,20),new Cell(48,26),new Cell(83,16),new Cell(76,25),new Cell(62,37),new Cell(55,44),new Cell(48,38),new Cell(48,58),new Cell(40,53),new Cell(34,48)})d.BarricadeSites.Add(new BattlefieldCell(c.X,c.Y));
            Prop(TerrainPropKind.Mountain,5,44,7,5);Prop(TerrainPropKind.Mountain,14,49,6,5);Prop(TerrainPropKind.Mountain,79,46,8,6);Prop(TerrainPropKind.Mountain,84,39,6,5);
            Prop(TerrainPropKind.Pine,7,32,2,3);Prop(TerrainPropKind.Pine,10,35,2,3);Prop(TerrainPropKind.Pine,13,31,2,3);Prop(TerrainPropKind.Pine,23,39,2,3);Prop(TerrainPropKind.Pine,25,43,2,3);Prop(TerrainPropKind.Pine,65,50,2,3);Prop(TerrainPropKind.Pine,68,54,2,3);
            Prop(TerrainPropKind.Broadleaf,7,7,3,3);Prop(TerrainPropKind.Broadleaf,11,9,3,3);Prop(TerrainPropKind.Broadleaf,25,7,3,3);Prop(TerrainPropKind.Broadleaf,66,7,3,3);Prop(TerrainPropKind.Broadleaf,83,7,3,3);Prop(TerrainPropKind.Broadleaf,88,10,3,3);
            Prop(TerrainPropKind.Rock,28,35,2,1);Prop(TerrainPropKind.Rock,54,12,2,1);Prop(TerrainPropKind.Rock,69,36,2,1);Prop(TerrainPropKind.Rock,56,55,2,1);
            Prop(TerrainPropKind.Shrub,30,12,1,1);Prop(TerrainPropKind.Shrub,58,51,1,1);Prop(TerrainPropKind.Shrub,70,22,1,1);Prop(TerrainPropKind.Shrub,23,22,1,1);
            return d;
            void AddPath(params int[] points)
            {
                for(int i=0;i+3<points.Length;i+=2)
                {
                    int x=points[i],y=points[i+1],tx=points[i+2],ty=points[i+3];
                    if(x!=tx&&y!=ty)throw new ArgumentException("Roads must be cardinal.");
                    road.Add(new Cell(x,y));while(x!=tx||y!=ty){x+=Math.Sign(tx-x);y+=Math.Sign(ty-y);road.Add(new Cell(x,y));}
                }
            }
            void Prop(TerrainPropKind kind,int x,int y,int w,int h)=>d.Props.Add(new TerrainPropDefinition{Kind=kind,X=x,Y=y,Width=w,Height=h});
        }
    }
}
