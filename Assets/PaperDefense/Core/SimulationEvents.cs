using System;
using System.Collections;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public enum SimulationEventKind
    {
        Shot = 0, ProjectileLaunched = 1, ProjectileImpact = 2, StructureDestroyed = 3,
        WeaponActionStarted = 4, EnemyAttack = 5, EnemyHit = 6, EnemyDied = 7,
        BlotSpawned = 8, BlotExpired = 9, BlotCleared = 10, BlotAbsorbed = 11,
        EnemyGlobLaunched = 12, EnemyGlobImpact = 13, BaseHit = 14, InkWellHit = 15, InkWellActivated = 16
    }

    public readonly struct SimulationEvent
    {
        public readonly long Sequence, Tick;
        public readonly SimulationEventKind Kind;
        public readonly Point2 From, To;
        public readonly StructureKind Weapon;
        public readonly string WeaponId;
        public readonly float Radius, TravelSeconds;
        public readonly int EntityId;
        public readonly int SourceStructureId, MountSlotIndex;
        public readonly long ActionId;
        public readonly double SimulationSeconds;
        public readonly EnemyKind EnemyKind;
        public readonly DrawingTier EnemyDrawingTier;
        public readonly FootprintGeometry? ImpactFootprint;
        internal SimulationEvent(long sequence,long tick,SimulationEventKind kind,Point2 from,Point2 to,
            StructureKind weapon,float radius,int entityId,float travelSeconds,FootprintGeometry? impactFootprint = null,string weaponId=null,
            int sourceStructureId=0,int mountSlotIndex=-1,long actionId=0,double simulationSeconds=0,
            EnemyKind enemyKind=default,DrawingTier enemyDrawingTier=default)
        {Sequence=sequence;Tick=tick;Kind=kind;From=from;To=to;Weapon=weapon;WeaponId=weaponId??LegacyContentIds.Weapon(weapon);Radius=radius;EntityId=entityId;TravelSeconds=travelSeconds;ImpactFootprint=impactFootprint;
            SourceStructureId=sourceStructureId;MountSlotIndex=mountSlotIndex;ActionId=actionId;SimulationSeconds=simulationSeconds;EnemyKind=enemyKind;EnemyDrawingTier=enemyDrawingTier;}
    }

    /// <summary>Bounded visual history. Losing old marks never changes simulation outcomes.</summary>
    public sealed class SimulationEventBuffer : IReadOnlyList<SimulationEvent>
    {
        readonly SimulationEvent[] items;
        int start,count;
        public long LatestSequence {get;private set;}
        public long DroppedEventCount {get;private set;}
        public int Count=>count;
        public int Capacity=>items.Length;
        public IReadOnlyList<SimulationEvent> Items=>this;
        public SimulationEventBuffer(int capacity=512) {items=new SimulationEvent[Math.Max(1,capacity)];}
        public SimulationEvent this[int index]
        {get {if(index<0||index>=count)throw new ArgumentOutOfRangeException(nameof(index));return items[(start+index)%items.Length];}}
        public void Publish(long tick,SimulationEventKind kind,Point2 from,Point2 to,StructureKind weapon,
            float radius=0,int entityId=0,float travelSeconds=0,FootprintGeometry? impactFootprint = null,string weaponId=null,
            int sourceStructureId=0,int mountSlotIndex=-1,long actionId=0,double simulationSeconds=0,
            EnemyKind enemyKind=default,DrawingTier enemyDrawingTier=default)
        {
            var value=new SimulationEvent(++LatestSequence,tick,kind,from,to,weapon,radius,entityId,travelSeconds,impactFootprint,weaponId,
                sourceStructureId,mountSlotIndex,actionId,simulationSeconds,enemyKind,enemyDrawingTier);
            if(count<items.Length) items[(start+count++)%items.Length]=value;
            else {items[start]=value;start=(start+1)%items.Length;DroppedEventCount++;}
        }
        public IEnumerator<SimulationEvent> GetEnumerator(){for(int i=0;i<count;i++)yield return this[i];}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
}
