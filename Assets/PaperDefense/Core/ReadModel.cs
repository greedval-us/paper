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
        BattlefieldView Battlefield {get;}
        IReadOnlyList<StructureView> Structures {get;}
        IReadOnlyList<EnemyView> Enemies {get;}
        IReadOnlyList<ProjectileView> Projectiles {get;}
        IReadOnlyList<BlotView> Blots {get;}
        IReadOnlyList<EnemyGlobView> EnemyProjectiles {get;}
        IReadOnlyList<InkWellView> Wells {get;}
        RunStatisticsView Statistics {get;}
        Cell BaseCell {get;}
        int Ink {get;} int Kills {get;} int WaveIndex {get;} int RemainingToSpawn {get;}
        int TopologyVersion {get;} int StructureVisualVersion {get;}
        float BaseHealth {get;} float Countdown {get;} float Elapsed {get;}
        long TickIndex {get;}
        RunPhase Phase {get;}
        bool PendingUpgrade {get;}
        int GoldEarned {get;}
        Guid RunId {get;}
        int CompletedWaves {get;}
        float BlotterCooldown {get;}
        int ColoredInk(InkColor color);
        StructureView? StructureAt(Cell cell);
        BuildQuote QuoteBuild(IReadOnlyList<Cell> cells,StructureKind kind,MaterialTier tier);
        BuildQuote QuoteBlueprint(IReadOnlyList<Cell> cells,TowerBlueprint blueprint,MaterialTier tier,InkColor color);
        BuildQuote QuoteEntry(IReadOnlyList<Cell> cells,ResolvedEntry entry,MaterialTier tier,InkColor color);
    }

    public readonly struct WeaponMountView
    {
        public readonly Point2 Aim;
        public readonly int TargetId;
        public readonly float Cooldown,ActionRemaining,AcquisitionRemaining;
        public readonly WeaponCycleState Cycle;
        public int RoundsRemaining => Cycle.RoundsRemaining;
        public bool IsReloading => Cycle.IsReloading;
        public double CycleRemainingWork => Cycle.RemainingWork;
        internal WeaponMountView(WeaponMountState state){Aim=state.Aim;TargetId=state.TargetId;Cooldown=state.Cooldown;ActionRemaining=state.ActionRemaining;AcquisitionRemaining=state.AcquisitionRemaining;Cycle=state.Cycle;}
    }
    public readonly struct StructureView
    {
        public readonly int Id;
        public readonly Cell Cell;
        public readonly StructureKind Kind;
        public readonly string ContentId;
        public readonly MaterialTier Tier;
        public readonly float Health,MaxHealth,BuildRemaining,Cooldown;
        public readonly bool IsComplete,WaitingForClearance;
        public readonly Point2 Aim;
        public readonly Point2 PrimaryAim, SecondaryAim;
        public readonly int PrimaryTargetId, SecondaryTargetId;
        public readonly TargetPriority Priority;
        public readonly TowerBlueprint Blueprint;
        public readonly ResolvedEntry ResolvedEntry;
        public readonly InkColor Color;
        readonly WeaponMountView mount0,mount1,mount2;
        public WeaponMountView GetMountState(int mount)=>mount==0?mount0:mount==1?mount1:mount==2?mount2:throw new ArgumentOutOfRangeException(nameof(mount));
        public Point2 GetMountAim(int mount)=>GetMountState(mount).Aim;
        public int GetMountTargetId(int mount)=>GetMountState(mount).TargetId;
        internal StructureView(StructureState s)
        {Id=s.Id;Cell=s.Cell;Kind=s.Kind;ContentId=s.ContentId??LegacyContentIds.Weapon(s.Kind);Tier=s.Tier;Health=s.Health;MaxHealth=s.MaxHealth;BuildRemaining=s.BuildRemaining;
            Cooldown=s.Cooldown;IsComplete=s.IsComplete;WaitingForClearance=s.WaitingForClearance;Aim=s.Aim;Priority=s.Priority;
            Blueprint=s.Blueprint;ResolvedEntry=s.ResolvedEntry;Color=s.Color;PrimaryAim=s.PrimaryAim;SecondaryAim=s.SecondaryAim;
            PrimaryTargetId=s.PrimaryTargetId;SecondaryTargetId=s.SecondaryTargetId;
            mount0=new WeaponMountView(s.GetMountState(0));mount1=new WeaponMountView(s.GetMountState(1));mount2=new WeaponMountView(s.GetMountState(2));}
    }
    public readonly struct EnemyView
    {
        public readonly int Id;
        public readonly EnemyKind Kind;
        public readonly Point2 Position;
        public readonly float Health,MaxHealth;
        public readonly DrawingTier DrawingTier;
        public readonly float VisualScale;
        public readonly float BlotRemaining,RedRemaining,BlueRemaining,GreenRemaining,WindupRemaining;
        internal EnemyView(EnemyState s){Id=s.Id;Kind=s.Kind;Position=s.Position;Health=s.Health;MaxHealth=s.MaxHealth;
            VisualScale=s.VisualScale;DrawingTier=s.DrawingTier;BlotRemaining=s.BlotRemaining;RedRemaining=s.RedRemaining;BlueRemaining=s.BlueRemaining;
            GreenRemaining=s.GreenRemaining;WindupRemaining=s.WindupRemaining;}
    }

    public readonly struct BlotView
    {
        public readonly Cell Cell;public readonly float Remaining;
        internal BlotView(BlotState b){Cell=b.Cell;Remaining=b.Remaining;}
    }
    public readonly struct EnemyGlobView
    {
        public readonly int Id;public readonly Point2 From,To;public readonly float Remaining,Duration;
        internal EnemyGlobView(EnemyGlob g){Id=g.Id;From=g.From;To=g.To;Remaining=g.Remaining;Duration=g.Duration;}
    }
    public readonly struct InkWellView
    {
        public readonly int Id;public readonly Cell Cell;public readonly InkColor Color;
        public readonly float Health,MaxHealth;public readonly bool Active,Threatened;
        internal InkWellView(InkWellState w){Id=w.Id;Cell=w.Cell;Color=w.Color;Health=w.Health;MaxHealth=w.MaxHealth;Active=w.Active;Threatened=w.Threatened;}
    }
    public readonly struct ProjectileView
    {
        public readonly int Id;
        public readonly Point2 From,To;
        public readonly float Remaining,Duration,Radius;
        public readonly StructureKind Weapon;
        public readonly string WeaponId;
        public readonly int SourceStructureId,MountSlotIndex;
        public readonly long ActionId;
        public readonly FootprintGeometry? ImpactFootprint;
        internal ProjectileView(MortarProjectile p)
        {Id=p.Id;From=p.From;To=p.To;Remaining=p.Remaining;Duration=p.Duration;Radius=p.Radius;Weapon=p.Weapon;WeaponId=p.WeaponId;ImpactFootprint=p.ImpactFootprint;SourceStructureId=p.SourceStructureId;MountSlotIndex=p.MountSlotIndex;ActionId=p.ActionId;}
    }

    public sealed class RunReadModel : IRunReadModel
    {
        readonly GameSession source;
        public GameBalanceView Balance {get;}
        public BattlefieldView Battlefield=>source.Battlefield;
        public IReadOnlyList<StructureView> Structures {get;}
        public IReadOnlyList<EnemyView> Enemies {get;}
        public IReadOnlyList<ProjectileView> Projectiles {get;}
        public IReadOnlyList<BlotView> Blots {get;}
        public IReadOnlyList<EnemyGlobView> EnemyProjectiles {get;}
        public IReadOnlyList<InkWellView> Wells {get;}
        public RunStatisticsView Statistics {get;}
        public RunReadModel(GameSession source)
        {
            this.source=source??throw new ArgumentNullException(nameof(source));
            Balance=new GameBalanceView(source.OwnedBalance, source.Mechanics);Statistics=new RunStatisticsView(source.Statistics);
            Structures=new ProjectedList<StructureState,StructureView>(source.Structures,s=>new StructureView(s));
            Enemies=new ProjectedList<EnemyState,EnemyView>(source.Enemies,e=>new EnemyView(e));
            Projectiles=new ProjectedList<MortarProjectile,ProjectileView>(source.Projectiles,p=>new ProjectileView(p));
            Blots=new ProjectedList<BlotState,BlotView>(source.Blots,b=>new BlotView(b));
            EnemyProjectiles=new ProjectedList<EnemyGlob,EnemyGlobView>(source.EnemyProjectiles,g=>new EnemyGlobView(g));
            Wells=new ProjectedList<InkWellState,InkWellView>(source.Wells,w=>new InkWellView(w));
        }
        public Cell BaseCell=>source.BaseCell;
        public int Ink=>source.Ink;public int Kills=>source.Kills;public int WaveIndex=>source.WaveIndex;
        public int RemainingToSpawn=>source.RemainingToSpawn;
        public int TopologyVersion=>source.TopologyVersion;public int StructureVisualVersion=>source.StructureVisualVersion;
        public float BaseHealth=>source.BaseHealth;public float Countdown=>source.Countdown;public float Elapsed=>source.Elapsed;
        public long TickIndex=>source.TickIndex;public RunPhase Phase=>source.Phase;public bool PendingUpgrade=>source.PendingUpgrade;
        public int GoldEarned=>source.GoldEarned;public float BlotterCooldown=>source.BlotterCooldownRemaining;
        public Guid RunId=>source.RunId;public int CompletedWaves=>source.CompletedWaves;
        public int ColoredInk(InkColor color)=>source.ColoredInk(color);
        public StructureView? StructureAt(Cell cell){var value=source.StructureAt(cell);return value==null?(StructureView?)null:new StructureView(value);}
        public BuildQuote QuoteBuild(IReadOnlyList<Cell> cells,StructureKind kind,MaterialTier tier)=>source.QuoteBuild(cells,kind,tier);
        public BuildQuote QuoteBlueprint(IReadOnlyList<Cell> cells,TowerBlueprint blueprint,MaterialTier tier,InkColor color)=>source.QuoteBlueprint(cells,blueprint,tier,color);
        public BuildQuote QuoteEntry(IReadOnlyList<Cell> cells,ResolvedEntry entry,MaterialTier tier,InkColor color)=>source.QuoteEntry(cells,entry,tier,color);
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
        public readonly StructureKind Kind;public readonly int Cost,MaxPierce,MaxTargets;
        public readonly float MaxHealth,BuildSeconds,Range,Damage,ShotInterval,Radius,FlightSeconds,ConeAngleDegrees,JumpRange,SlowSpeedMultiplier,SlowDuration,RepairPerSecond;
        internal StructureSpec(StructureDefinition d){Kind=d.Kind;Cost=d.Cost;MaxPierce=d.MaxPierce;MaxHealth=d.MaxHealth;
            BuildSeconds=d.BuildSeconds;Range=d.Range;Damage=d.Damage;ShotInterval=d.ShotInterval;Radius=d.Radius;FlightSeconds=d.FlightSeconds;
            ConeAngleDegrees=d.ConeAngleDegrees;MaxTargets=d.MaxTargets;JumpRange=d.JumpRange;SlowSpeedMultiplier=d.SlowSpeedMultiplier;SlowDuration=d.SlowDuration;RepairPerSecond=d.RepairPerSecond;}
    }
    public readonly struct MaterialSpec
    {
        public readonly float CostMultiplier,HealthMultiplier,DamageMultiplier,RefundFraction,BuildTimeMultiplier;
        internal MaterialSpec(MaterialDefinition d){CostMultiplier=d.CostMultiplier;HealthMultiplier=d.HealthMultiplier;
            DamageMultiplier=d.DamageMultiplier;RefundFraction=d.RefundFraction;BuildTimeMultiplier=d.BuildTimeMultiplier;}
    }
    public readonly struct EnemySpec
    {
        public readonly EnemyKind Kind;public readonly float MaxHealth,Speed,Damage,AttackInterval,Range,WindupSeconds,ProjectileSeconds,StructureDamageMultiplier,VisualScale;public readonly int Reward;
        internal EnemySpec(EnemyDefinition d){Kind=d.Kind;MaxHealth=d.MaxHealth;Speed=d.Speed;Damage=d.Damage;AttackInterval=d.AttackInterval;Reward=d.Reward;Range=d.Range;WindupSeconds=d.WindupSeconds;ProjectileSeconds=d.ProjectileSeconds;StructureDamageMultiplier=d.StructureDamageMultiplier;VisualScale=d.VisualScale;}
    }
    public readonly struct WaveView
    {
        public readonly int Count,Sides,InkGrant;public readonly float SpawnInterval,RunnerFraction,GnawerFraction,SpitterFraction,BruteFraction,BulwarkFraction,OutlinedFraction,HatchedFraction;public readonly string Title;
        internal WaveView(WaveDefinition d){Count=d.Count;Sides=d.Sides;InkGrant=d.InkGrant;SpawnInterval=d.SpawnInterval;RunnerFraction=d.RunnerFraction;GnawerFraction=d.GnawerFraction;SpitterFraction=d.SpitterFraction;BruteFraction=d.BruteFraction;BulwarkFraction=d.BulwarkFraction;OutlinedFraction=d.OutlinedFraction;HatchedFraction=d.HatchedFraction;Title=d.Title;}
    }
    public readonly struct RunUpgradeRulesView
    {
        public readonly float RapidFireMultiplier, DeepInkDamageMultiplier, DeepInkHealthMultiplier;
        public readonly float SalvageWaveRewardMultiplier, SalvageRefundBonus;
        public readonly int SalvageImmediateInk;
        internal RunUpgradeRulesView(ProgressionRules rules)
        {
            RapidFireMultiplier = rules.RapidFireMultiplier; DeepInkDamageMultiplier = rules.DeepInkDamageMultiplier;
            DeepInkHealthMultiplier = rules.DeepInkHealthMultiplier; SalvageWaveRewardMultiplier = rules.SalvageWaveRewardMultiplier;
            SalvageRefundBonus = rules.SalvageRefundBonus; SalvageImmediateInk = rules.SalvageImmediateInk;
        }
    }

    public sealed class GameBalanceView
    {
        readonly GameBalance source;
        public BattlefieldView Battlefield {get;}
        public MechanicsAvailability Mechanics {get;}
        public ContentCatalogSnapshot Content {get;}
        public TrialRules Trials {get;}
        public IReadOnlyList<WaveView> Waves {get;}
        internal GameBalanceView(GameBalance source, MechanicsAvailability? mechanics = null)
        {this.source=source;Battlefield=source.ResolveBattlefield();Content=source.ResolveContent();Trials=source.Trials.CreateSnapshot();Mechanics=mechanics??MechanicsAvailability.Resolve(source);Waves=new ProjectedList<WaveDefinition,WaveView>(source.Waves,w=>new WaveView(w));}
        public int Width=>source.Width;public int Height=>source.Height;public int StartingInk=>source.StartingInk;
        public float BaseHealth=>source.BaseHealth;public float PlanningSeconds=>source.PlanningSeconds;public float IntermissionSeconds=>source.IntermissionSeconds;
        public float SimulationStep=>source.SimulationStep;public int MaxEnemiesPerCell=>source.MaxEnemiesPerCell;
        public float PassiveInkPerSecond=>source.PassiveInkPerSecond;public int WaveInkGrant=>source.WaveInkGrant;
        public int BlotterCost=>source.BlotterCost;public float BlotterRadius=>source.BlotterRadius;
        public float BlotterCooldown=>source.BlotterCooldown;public float BlotLifetime=>source.BlotLifetime;
        public int WellActivationCost=>source.WellActivationCost;public int WellInkPerWave=>source.WellInkPerWave;
        public bool EnableEnemyMechanics=>source.EnableEnemyMechanics;public bool EnableInkWells=>source.EnableInkWells;
        public GameBalancePreset Preset=>source.Preset;
        public bool DynamicFeed=>source.Construction.DynamicFeed;
        public RunUpgradeRulesView Progression=>new RunUpgradeRulesView(source.Progression);
        public bool LimitTowerColor=>source.Effects.LimitTowerColor;
        public float ColorWindowSeconds=>source.Effects.ColorWindowSeconds;
        public int ColorTargetsPerWindow=>source.Effects.ColorTargetsPerWindow;
        public int ChassisCapacity(ChassisKind chassis)=>Content.GetChassis(LegacyContentIds.Chassis(chassis)).Capacity;
        public int ChassisCapacity(string chassisId)=>Content.GetChassis(chassisId).Capacity;
        public int ModuleLoad(TowerModules modules)=>source.Construction.ModuleLoad(modules);
        public int ToolCost(StructureKind kind, MaterialTier tier)=>TowerCatalog.ToolCost(source,kind,tier);
        public bool HasStructure(StructureKind kind)=>source.StructureDefinitions.Exists(d=>d.Kind==kind);
        public StructureSpec Structure(StructureKind kind)=>new StructureSpec(source.Structure(kind));
        public float WeaponRange(TowerBlueprint blueprint,StructureKind kind)=>TowerCatalog.WeaponRange(blueprint,source,kind);
        public float WeaponRange(TowerBlueprint blueprint,string id,int mount=0)=>TowerCatalog.WeaponRange(blueprint,source,id,mount);
        public float WeaponMinimumRange(TowerBlueprint blueprint,string id,int mount=0)=>Content.GetWeapon(id).MinimumRange;
        public float BuildSeconds(TowerBlueprint blueprint,MaterialTier tier)=>TowerCatalog.BuildSeconds(blueprint,source,tier);
        public MaterialSpec Material(MaterialTier tier)=>new MaterialSpec(source.Material(tier));
        public EnemySpec Enemy(EnemyKind kind)=>new EnemySpec(source.Enemy(kind));
    }
}
