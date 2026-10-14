using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    [Serializable] public sealed class BattlefieldCell
    {
        public int X,Y;
        public BattlefieldCell() { }
        public BattlefieldCell(int x,int y){X=x;Y=y;}
        internal Cell Cell=>new Cell(X,Y);
        internal BattlefieldCell Copy()=>new BattlefieldCell(X,Y);
    }
    public enum TerrainPropKind { Mountain=0,Pine=1,Broadleaf=2,Rock=3,Shrub=4 }
    public enum BattlefieldMode { FixedRoads=0, OpenTerrain=1 }
    [Serializable] public sealed class TerrainPropDefinition
    {
        public TerrainPropKind Kind;
        public int X,Y,Width=1,Height=1;
        internal TerrainPropDefinition Copy()=>(TerrainPropDefinition)MemberwiseClone();
    }
    public readonly struct TerrainPropView
    {
        public readonly TerrainPropKind Kind;
        public readonly Cell Cell;
        public readonly int Width,Height;
        internal TerrainPropView(TerrainPropDefinition d){Kind=d.Kind;Cell=new Cell(d.X,d.Y);Width=d.Width;Height=d.Height;}
    }
    /// <summary>Optional authoring data. Enabled is explicit so older serialized presets remain open fields.</summary>
    [Serializable] public sealed class BattlefieldDefinition
    {
        public bool Enabled;
        public BattlefieldMode Mode;
        public int TowerRoadDistance = 6;
        public List<BattlefieldCell> Roads=new List<BattlefieldCell>();
        public List<BattlefieldCell> Entrances=new List<BattlefieldCell>();
        public List<BattlefieldCell> BarricadeSites=new List<BattlefieldCell>();
        public List<TerrainPropDefinition> Props=new List<TerrainPropDefinition>();
        public BattlefieldDefinition CreateSnapshot()
        {
            if(Roads==null||Entrances==null||BarricadeSites==null||Props==null)throw new ArgumentException("Battlefield lists are required.");
            var copy=new BattlefieldDefinition{Enabled=Enabled,Mode=Mode,TowerRoadDistance=TowerRoadDistance};
            foreach(var c in Roads)copy.Roads.Add(c?.Copy()??throw new ArgumentException("Battlefield.Roads contains null."));
            foreach(var c in Entrances)copy.Entrances.Add(c?.Copy()??throw new ArgumentException("Battlefield.Entrances contains null."));
            foreach(var c in BarricadeSites)copy.BarricadeSites.Add(c?.Copy()??throw new ArgumentException("Battlefield.BarricadeSites contains null."));
            foreach(var p in Props)copy.Props.Add(p?.Copy()??throw new ArgumentException("Battlefield.Props contains null."));
            return copy;
        }
        public BattlefieldView Resolve(int width,int height,Cell castle,IReadOnlyList<Cell> objectives=null)
            => Enabled?new BattlefieldView(this,width,height,castle,objectives):null;
    }
    /// <summary>Immutable resolved grid; presentation and simulation consume the same placement data.</summary>
    public sealed class BattlefieldView
    {
        readonly int width,height;
        readonly bool[] roads,terrain,barricades,buildGround;
        readonly int[] castleDistance;
        public BattlefieldMode Mode {get;}
        public bool IsOpenTerrain=>Mode==BattlefieldMode.OpenTerrain;
        public IReadOnlyList<Cell> Roads {get;}
        public IReadOnlyList<Cell> Entrances {get;}
        public IReadOnlyList<Cell> BarricadeSites {get;}
        public IReadOnlyList<TerrainPropView> Props {get;}
        internal BattlefieldView(BattlefieldDefinition input,int width,int height,Cell castle,IReadOnlyList<Cell> objectives)
        {
            if(width<=0||height<=0||(long)width*height>int.MaxValue)throw new ArgumentException("Invalid battlefield size.");
            this.width=width;this.height=height;roads=new bool[width*height];buildGround=new bool[roads.Length];terrain=new bool[roads.Length];barricades=new bool[roads.Length];
            var d=input.CreateSnapshot();
            if(!Enum.IsDefined(typeof(BattlefieldMode),d.Mode))throw new ArgumentException("Invalid battlefield mode.");
            Mode=d.Mode;
            if(!IsOpenTerrain&&(d.TowerRoadDistance<1||d.TowerRoadDistance>Math.Max(width,height)))throw new ArgumentException("Invalid tower distance from roads.");
            Roads=ReadCells(d.Roads,roads,"Roads");Entrances=ReadCells(d.Entrances,null,"Entrances");BarricadeSites=ReadCells(d.BarricadeSites,barricades,"BarricadeSites");
            var props=new List<TerrainPropView>();
            foreach(var p in d.Props)
            {
                if(!Enum.IsDefined(typeof(TerrainPropKind),p.Kind)||p.Width<=0||p.Height<=0||p.X<0||p.Y<0||(long)p.X+p.Width>width||(long)p.Y+p.Height>height)throw new ArgumentException("Invalid terrain footprint.");
                for(int y=p.Y;y<p.Y+p.Height;y++)for(int x=p.X;x<p.X+p.Width;x++)
                {var c=new Cell(x,y);int i=y*width+x;if(roads[i]||terrain[i]||Near(c,castle,1))throw new ArgumentException("Terrain overlaps a road, terrain or castle.");terrain[i]=true;}
                props.Add(new TerrainPropView(p));
            }
            Props=props.AsReadOnly();
            if(Entrances.Count==0||!IsWalkable(castle))throw new ArgumentException("Battlefield needs entrances and a walkable castle cell.");
            foreach(var c in Entrances)if(!IsWalkable(c)||Side(c)==0)throw new ArgumentException("Battlefield entrance must be a walkable border cell.");
            foreach(var c in BarricadeSites)if(!IsWalkable(c)||Side(c)!=0||Near(c,castle,1))throw new ArgumentException("Barricades must mark interior walkable cells away from the castle.");
            if(IsOpenTerrain)
            {
                for(int i=0;i<buildGround.Length;i++)buildGround[i]=!terrain[i];
            }
            else
            {
            var roadDistance=new int[roads.Length];for(int i=0;i<roadDistance.Length;i++)roadDistance[i]=int.MaxValue;
            var frontier=new Queue<Cell>();foreach(var c in Roads){roadDistance[c.Y*width+c.X]=0;frontier.Enqueue(c);}
            while(frontier.Count>0)
            {
                var c=frontier.Dequeue();int next=roadDistance[c.Y*width+c.X]+1;if(next>d.TowerRoadDistance)continue;
                Ground(new Cell(c.X+1,c.Y),next);Ground(new Cell(c.X-1,c.Y),next);Ground(new Cell(c.X,c.Y+1),next);Ground(new Cell(c.X,c.Y-1),next);
            }
            for(int i=0;i<buildGround.Length;i++)buildGround[i]=!roads[i]&&!terrain[i]&&roadDistance[i]<=d.TowerRoadDistance;
            void Ground(Cell c,int value){if(!Inside(c))return;int i=c.Y*width+c.X;if(value>=roadDistance[i])return;roadDistance[i]=value;frontier.Enqueue(c);}
            }
            castleDistance=new int[roads.Length];for(int i=0;i<castleDistance.Length;i++)castleDistance[i]=int.MaxValue;
            castleDistance[castle.Y*width+castle.X]=0;
            var reached=new bool[roads.Length];var queue=new Queue<Cell>();queue.Enqueue(castle);reached[castle.Y*width+castle.X]=true;
            while(queue.Count>0)
            {
                var c=queue.Dequeue();Visit(new Cell(c.X+1,c.Y),c);Visit(new Cell(c.X-1,c.Y),c);Visit(new Cell(c.X,c.Y+1),c);Visit(new Cell(c.X,c.Y-1),c);
            }
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                if(IsWalkable(new Cell(x,y))&&!reached[y*width+x])throw new ArgumentException("Battlefield has disconnected walkable ground.");
            if(objectives!=null)foreach(var c in objectives)
            {
                if(!IsWalkable(c)||!reached[c.Y*width+c.X])throw new ArgumentException("Battlefield objective is not connected to the castle.");
            }
            void Visit(Cell c,Cell parent){if(IsWalkable(c)&&!reached[c.Y*width+c.X]){reached[c.Y*width+c.X]=true;castleDistance[c.Y*width+c.X]=castleDistance[parent.Y*width+parent.X]+1;queue.Enqueue(c);}}
        }
        IReadOnlyList<Cell> ReadCells(List<BattlefieldCell> input,bool[] mask,string name)
        {
            var list=new List<Cell>();var seen=new HashSet<Cell>();
            foreach(var d in input){var c=d.Cell;if(!Inside(c)||!seen.Add(c))throw new ArgumentException("Invalid or duplicate Battlefield."+name+" cell.");if(mask!=null)mask[c.Y*width+c.X]=true;list.Add(c);}
            return list.AsReadOnly();
        }
        bool Inside(Cell c)=>c.X>=0&&c.Y>=0&&c.X<width&&c.Y<height;
        static bool Near(Cell c,Cell target,int radius)=>Math.Abs(c.X-target.X)<=radius&&Math.Abs(c.Y-target.Y)<=radius;
        public bool IsRoad(Cell c)=>Inside(c)&&roads[c.Y*width+c.X];
        public bool IsWalkable(Cell c)=>Inside(c)&&(IsOpenTerrain?!terrain[c.Y*width+c.X]:roads[c.Y*width+c.X]);
        public bool IsReservedTerrain(Cell c)=>Inside(c)&&terrain[c.Y*width+c.X];
        public bool IsBarricadeSite(Cell c)=>Inside(c)&&barricades[c.Y*width+c.X];
        public bool CanPlace(Cell c,StructureKind kind)
            => Inside(c)&&!IsReservedTerrain(c)&&(IsOpenTerrain|| (kind==StructureKind.Wall?IsBarricadeSite(c):
                kind==StructureKind.Spikes||kind==StructureKind.TarPit?IsRoad(c):buildGround[c.Y*width+c.X]));
        public int DistanceToCastle(Cell c)=>IsWalkable(c)?castleDistance[c.Y*width+c.X]:int.MaxValue;
        public Cell NextTowardCastle(Cell c)
        {
            if(!IsWalkable(c))return c;
            var best=c;int distance=DistanceToCastle(c);
            Consider(new Cell(c.X+1,c.Y));Consider(new Cell(c.X-1,c.Y));Consider(new Cell(c.X,c.Y+1));Consider(new Cell(c.X,c.Y-1));
            return best;
            void Consider(Cell neighbor){int value=DistanceToCastle(neighbor);if(value<distance){distance=value;best=neighbor;}}
        }
        public int Side(Cell c)=>c.X==0?1:c.X==width-1?2:c.Y==height-1?4:c.Y==0?8:0;
        public bool HasEntrance(int side){foreach(var c in Entrances)if(Side(c)==side)return true;return false;}
    }
    public sealed partial class GameBalance
    {
        public BattlefieldDefinition Battlefield;
        public float WaveHealthGrowth;
        public BattlefieldView ResolveBattlefield()
        {
            var objectives=new List<Cell>();
            if(MechanicsAvailability.Resolve(this).InkWells)foreach(var w in WellPlacements)objectives.Add(w.Resolve(Width,Height));
            var map=Battlefield?.Resolve(Width,Height,new Cell(Width/2,Height/2),objectives);
            if(map!=null&&MechanicsAvailability.Resolve(this).InkWells)
                foreach(var w in WellPlacements)
                {
                    var c=w.Resolve(Width,Height);int radius=w.ReservedRadius;
                    for(int y=Math.Max(0,c.Y-radius);y<=Math.Min(Height-1,c.Y+radius);y++)
                        for(int x=Math.Max(0,c.X-radius);x<=Math.Min(Width-1,c.X+radius);x++)
                            if(map.IsReservedTerrain(new Cell(x,y))||map.IsBarricadeSite(new Cell(x,y)))throw new ArgumentException("Terrain or barricade site overlaps a reserved objective footprint.");
                }
            return map;
        }
    }
}
