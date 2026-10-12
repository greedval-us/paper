using System;
using System.Collections;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public enum SimulationEventKind { Shot = 0, ProjectileLaunched = 1, ProjectileImpact = 2, StructureDestroyed = 3 }

    public readonly struct SimulationEvent
    {
        public readonly long Sequence, Tick;
        public readonly SimulationEventKind Kind;
        public readonly Point2 From, To;
        public readonly StructureKind Weapon;
        public readonly float Radius, TravelSeconds;
        public readonly int EntityId;
        public readonly FootprintGeometry? ImpactFootprint;
        internal SimulationEvent(long sequence,long tick,SimulationEventKind kind,Point2 from,Point2 to,
            StructureKind weapon,float radius,int entityId,float travelSeconds,FootprintGeometry? impactFootprint = null)
        {Sequence=sequence;Tick=tick;Kind=kind;From=from;To=to;Weapon=weapon;Radius=radius;EntityId=entityId;TravelSeconds=travelSeconds;ImpactFootprint=impactFootprint;}
    }

    /// <summary>Bounded visual history. Losing old marks never changes simulation outcomes.</summary>
    public sealed class SimulationEventBuffer : IReadOnlyList<SimulationEvent>
    {
        readonly SimulationEvent[] items;
        int start,count;
        public long LatestSequence {get;private set;}
        public int Count=>count;
        public int Capacity=>items.Length;
        public IReadOnlyList<SimulationEvent> Items=>this;
        public SimulationEventBuffer(int capacity=512) {items=new SimulationEvent[Math.Max(1,capacity)];}
        public SimulationEvent this[int index]
        {get {if(index<0||index>=count)throw new ArgumentOutOfRangeException(nameof(index));return items[(start+index)%items.Length];}}
        public void Publish(long tick,SimulationEventKind kind,Point2 from,Point2 to,StructureKind weapon,
            float radius=0,int entityId=0,float travelSeconds=0,FootprintGeometry? impactFootprint = null)
        {
            var value=new SimulationEvent(++LatestSequence,tick,kind,from,to,weapon,radius,entityId,travelSeconds,impactFootprint);
            if(count<items.Length) items[(start+count++)%items.Length]=value;
            else {items[start]=value;start=(start+1)%items.Length;}
        }
        public IEnumerator<SimulationEvent> GetEnumerator(){for(int i=0;i<count;i++)yield return this[i];}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
}
