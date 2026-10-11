using System;
using System.Collections;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>
    /// Live read access on the simulation thread, between steps. Indexed reads return value snapshots.
    /// Do not enumerate a live collection while a step is running; no whole-swarm copy is made.
    /// </summary>
    public interface IRunReadModel
    {
        GameBalanceView Balance {get;}
        IReadOnlyList<StructureView> Structures {get;}
        IReadOnlyList<EnemyView> Enemies {get;}
        IReadOnlyList<ProjectileView> Projectiles {get;}
        RunStatisticsView Statistics {get;}
        Cell BaseCell {get;}
        int Ink {get;} int Kills {get;} int WaveIndex {get;} int RemainingToSpawn {get;}
        int TopologyVersion {get;} int StructureVisualVersion {get;}
        float BaseHealth {get;} float Countdown {get;} float Elapsed {get;}
        long TickIndex {get;}
        RunPhase Phase {get;}
        bool PendingUpgrade {get;}
        StructureView? StructureAt(Cell cell);
        BuildQuote QuoteBuild(IReadOnlyList<Cell> cells,StructureKind kind,MaterialTier tier);
    }

    public readonly struct StructureView
    {
        public readonly int Id;
        public readonly Cell Cell;
        public readonly StructureKind Kind;
        public readonly MaterialTier Tier;
        public readonly float Health,MaxHealth,BuildRemaining,Cooldown;
        public readonly bool IsComplete,WaitingForClearance;
        public readonly Point2 Aim;
        public readonly TargetPriority Priority;
        internal StructureView(StructureState s)
        {Id=s.Id;Cell=s.Cell;Kind=s.Kind;Tier=s.Tier;Health=s.Health;MaxHealth=s.MaxHealth;BuildRemaining=s.BuildRemaining;
            Cooldown=s.Cooldown;IsComplete=s.IsComplete;WaitingForClearance=s.WaitingForClearance;Aim=s.Aim;Priority=s.Priority;}
    }
    public readonly struct EnemyView
    {
        public readonly int Id;
        public readonly EnemyKind Kind;
        public readonly Point2 Position;
        public readonly float Health,MaxHealth;
        internal EnemyView(EnemyState s){Id=s.Id;Kind=s.Kind;Position=s.Position;Health=s.Health;MaxHealth=s.MaxHealth;}
    }
    public readonly struct ProjectileView
    {
        public readonly int Id;
        public readonly Point2 From,To;
        public readonly float Remaining,Duration,Radius;
        public readonly StructureKind Weapon;
        internal ProjectileView(MortarProjectile p)
        {Id=p.Id;From=p.From;To=p.To;Remaining=p.Remaining;Duration=p.Duration;Radius=p.Radius;Weapon=p.Weapon;}
    }

    public sealed class RunReadModel : IRunReadModel
    {
        readonly GameSession source;
        public GameBalanceView Balance {get;}
        public IReadOnlyList<StructureView> Structures {get;}
        public IReadOnlyList<EnemyView> Enemies {get;}
        public IReadOnlyList<ProjectileView> Projectiles {get;}
        public RunStatisticsView Statistics {get;}
        public RunReadModel(GameSession source)
        {
            this.source=source??throw new ArgumentNullException(nameof(source));
            Balance=new GameBalanceView(source.Balance);Statistics=new RunStatisticsView(source.Statistics);
            Structures=new ProjectedList<StructureState,StructureView>(source.Structures,s=>new StructureView(s));
            Enemies=new ProjectedList<EnemyState,EnemyView>(source.Enemies,e=>new EnemyView(e));
            Projectiles=new ProjectedList<MortarProjectile,ProjectileView>(source.Projectiles,p=>new ProjectileView(p));
        }
        public Cell BaseCell=>source.BaseCell;
        public int Ink=>source.Ink;public int Kills=>source.Kills;public int WaveIndex=>source.WaveIndex;
        public int RemainingToSpawn=>source.RemainingToSpawn;
        public int TopologyVersion=>source.TopologyVersion;public int StructureVisualVersion=>source.StructureVisualVersion;
        public float BaseHealth=>source.BaseHealth;public float Countdown=>source.Countdown;public float Elapsed=>source.Elapsed;
        public long TickIndex=>source.TickIndex;public RunPhase Phase=>source.Phase;public bool PendingUpgrade=>source.PendingUpgrade;
        public StructureView? StructureAt(Cell cell){var value=source.StructureAt(cell);return value==null?(StructureView?)null:new StructureView(value);}
        public BuildQuote QuoteBuild(IReadOnlyList<Cell> cells,StructureKind kind,MaterialTier tier)=>source.QuoteBuild(cells,kind,tier);
    }

    internal sealed class ProjectedList<TSource,TValue> : IReadOnlyList<TValue>
    {
        readonly IReadOnlyList<TSource> source;readonly Func<TSource,TValue> project;
        internal ProjectedList(IReadOnlyList<TSource> source,Func<TSource,TValue> project){this.source=source;this.project=project;}
        public int Count=>source.Count;
        public TValue this[int index]=>project(source[index]);
        public IEnumerator<TValue> GetEnumerator(){for(int i=0;i<Count;i++)yield return this[i];}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }

    public readonly struct StructureSpec
    {
        public readonly StructureKind Kind;public readonly int Cost,MaxPierce;
        public readonly float MaxHealth,BuildSeconds,Range,Damage,ShotInterval,Radius,FlightSeconds;
        internal StructureSpec(StructureDefinition d){Kind=d.Kind;Cost=d.Cost;MaxPierce=d.MaxPierce;MaxHealth=d.MaxHealth;
            BuildSeconds=d.BuildSeconds;Range=d.Range;Damage=d.Damage;ShotInterval=d.ShotInterval;Radius=d.Radius;FlightSeconds=d.FlightSeconds;}
    }
    public readonly struct MaterialSpec
    {
        public readonly float CostMultiplier,HealthMultiplier,DamageMultiplier,RefundFraction,BuildTimeMultiplier;
        internal MaterialSpec(MaterialDefinition d){CostMultiplier=d.CostMultiplier;HealthMultiplier=d.HealthMultiplier;
            DamageMultiplier=d.DamageMultiplier;RefundFraction=d.RefundFraction;BuildTimeMultiplier=d.BuildTimeMultiplier;}
    }
    public readonly struct EnemySpec
    {
        public readonly EnemyKind Kind;public readonly float MaxHealth,Speed,Damage,AttackInterval;public readonly int Reward;
        internal EnemySpec(EnemyDefinition d){Kind=d.Kind;MaxHealth=d.MaxHealth;Speed=d.Speed;Damage=d.Damage;AttackInterval=d.AttackInterval;Reward=d.Reward;}
    }
    public readonly struct WaveView
    {
        public readonly int Count,Sides;public readonly float SpawnInterval,RunnerFraction,GnawerFraction;public readonly string Title;
        internal WaveView(WaveDefinition d){Count=d.Count;Sides=d.Sides;SpawnInterval=d.SpawnInterval;RunnerFraction=d.RunnerFraction;GnawerFraction=d.GnawerFraction;Title=d.Title;}
    }
    public sealed class GameBalanceView
    {
        readonly GameBalance source;
        public IReadOnlyList<WaveView> Waves {get;}
        internal GameBalanceView(GameBalance source){this.source=source;Waves=new ProjectedList<WaveDefinition,WaveView>(source.Waves,w=>new WaveView(w));}
        public int Width=>source.Width;public int Height=>source.Height;public int StartingInk=>source.StartingInk;
        public float BaseHealth=>source.BaseHealth;public float PlanningSeconds=>source.PlanningSeconds;public float IntermissionSeconds=>source.IntermissionSeconds;
        public float SimulationStep=>source.SimulationStep;public int MaxEnemiesPerCell=>source.MaxEnemiesPerCell;
        public float PassiveInkPerSecond=>source.PassiveInkPerSecond;public int WaveInkGrant=>source.WaveInkGrant;
        public StructureSpec Structure(StructureKind kind)=>new StructureSpec(source.Structure(kind));
        public MaterialSpec Material(MaterialTier tier)=>new MaterialSpec(source.Material(tier));
        public EnemySpec Enemy(EnemyKind kind)=>new EnemySpec(source.Enemy(kind));
    }
}
