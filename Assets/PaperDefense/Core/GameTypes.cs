using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X, Y;
        public Cell(int x, int y) { X = x; Y = y; }
        public bool Equals(Cell other) => X == other.X && Y == other.Y;
        public override bool Equals(object other) => other is Cell cell && Equals(cell);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => $"({X}, {Y})";
        public static bool operator ==(Cell a, Cell b) => a.Equals(b);
        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);
    }

    public readonly struct Point2
    {
        public readonly float X, Y;
        public Point2(float x, float y) { X = x; Y = y; }
        public override string ToString() => $"({X:0.00}, {Y:0.00})";
    }

    public enum MaterialTier { Pencil = 0, Ink = 1, Marker = 2 }
    public enum StructureKind { Wall = 0, Gun = 1, Piercer = 2, Mortar = 3, Spikes = 4, Fan = 5, Ricochet = 6, Stapler = 7, Blotter = 8, TarPit = 9, RepairPost = 10 }
    public enum EnemyKind { Doodle = 0, Runner = 1, Gnawer = 2, Spitter = 3, Brute = 4, Bulwark = 5 }
    public enum DrawingTier { Sketch = 0, Outlined = 1, Hatched = 2 }
    public enum InkColor { None = 0, Red = 1, Blue = 2, Green = 3 }
    public enum RunPhase { Planning = 0, Assault = 1, Intermission = 2, Victory = 3, Defeat = 4 }
    public enum TargetPriority { Nearest = 0, Strongest = 1, Siege = 2 }
    public enum RunUpgrade { RapidFire = 0, DeepInk = 1, Salvage = 2 }
    public enum GameBalancePreset { Historical = 0, Target = 1 }

    [Serializable] public sealed class StructureDefinition
    {
        public StructureKind Kind;
        public int Cost;
        public float MaxHealth, BuildSeconds, Range, Damage, ShotInterval, Radius, FlightSeconds = .8f;
        public int MaxPierce;
        public float ConeAngleDegrees = 90;
        public int MaxTargets = 8;
        public float JumpRange;
        public float SlowSpeedMultiplier = 1, SlowDuration;
        public float RepairPerSecond;
    }

    [Serializable] public sealed class MaterialDefinition
    {
        public float CostMultiplier = 1, HealthMultiplier = 1, DamageMultiplier = 1;
        public float RefundFraction = 0.8f, BuildTimeMultiplier = 1;
    }

    [Serializable] public sealed class EnemyDefinition
    {
        public EnemyKind Kind;
        public float MaxHealth, Speed, Damage, AttackInterval;
        public float Range = 12.5f, WindupSeconds = .8f, ProjectileSeconds = .65f;
        public float StructureDamageMultiplier = 1;
        public float VisualScale = 1;
        public int Reward;
    }

    [Serializable] public sealed class WaveDefinition
    {
        public int InkGrant = -1;
        public int Count = 80;
        public float SpawnInterval = 0.14f, RunnerFraction, GnawerFraction, SpitterFraction, BruteFraction, BulwarkFraction, OutlinedFraction, HatchedFraction;
        /// <summary>Spawn edges: west 1, east 2, north 4, south 8.</summary>
        public int Sides = 1;
        public string Title = UserText.Message("copy.a2d0b3c493f760f6");
    }

    /// <summary>Mutable tuning data. Configure before constructing a session.</summary>
    public sealed partial class GameBalance
    {
        [NonSerialized] public ContentCatalogSnapshot Content;
        public ContentCatalogSnapshot ResolveContent() => Content ?? LegacyContentCatalog.Create(this);
        public GameBalancePreset Preset;
        public MechanicsConfiguration Mechanics = new MechanicsConfiguration();
        public TowerConstructionRules Construction = new TowerConstructionRules();
        public int Width = 64, Height = 40, StartingInk = 360;
        public float BaseHealth = 250, PlanningSeconds = 35, IntermissionSeconds = 20;
        public float PassiveInkPerSecond = 1.8f, EarlyLaunchInkPerSecond = 0.8f;
        public int MaxEarlyLaunchInk = 30, WaveInkGrant = 75;
        public float SimulationStep = 1f / 30f;
        public int MaxEnemiesPerCell = 6;
        public bool EnableEnemyMechanics, EnableInkWells;
        public float BlotLifetime = 8, BlotBuffSeconds = 4, BlotSpeedBonus = .15f, BlotReduction = .15f;
        public int BlotterCost = 8, WellActivationCost = 45, WellInkPerWave = 12, WaveGold = 8, VictoryGold = 30;
        public float BlotterCooldown = 3, BlotterRadius = 3, WellHealth = 160;
        public List<StructureDefinition> StructureDefinitions = new List<StructureDefinition>
        {
            new StructureDefinition { Kind = StructureKind.Wall, Cost = 3, MaxHealth = 65, BuildSeconds = 0.35f },
            new StructureDefinition { Kind = StructureKind.Gun, Cost = 40, MaxHealth = 55, BuildSeconds = 1.4f, Range = 11, Damage = 12, ShotInterval = 0.42f, MaxPierce = 1 },
            new StructureDefinition { Kind = StructureKind.Piercer, Cost = 65, MaxHealth = 50, BuildSeconds = 1.9f, Range = 14, Damage = 18, ShotInterval = 1.05f, Radius = 0.38f, MaxPierce = 7 },
            new StructureDefinition { Kind = StructureKind.Mortar, Cost = 90, MaxHealth = 60, BuildSeconds = 2.4f, Range = 16, Damage = 25, ShotInterval = 1.8f, Radius = 2.4f, MaxPierce = 1 },
            new StructureDefinition { Kind = StructureKind.Spikes, Cost = 8, MaxHealth = 35, BuildSeconds = 0.5f, Range = 0.7f, Damage = 9, ShotInterval = 0.65f, Radius = 0.65f },
            new StructureDefinition { Kind = StructureKind.Fan, Cost = 70, MaxHealth = 55, BuildSeconds = 1.6f, Range = 6, Damage = 10,
                ShotInterval = 1, ConeAngleDegrees = 90, MaxTargets = 8, MaxPierce = 1 },
            new StructureDefinition { Kind = StructureKind.Ricochet, Cost = 65, MaxHealth = 55, BuildSeconds = 1.6f, Range = 10,
                Damage = 12, ShotInterval = .85f, JumpRange = 3, MaxTargets = 4, MaxPierce = 1 },
            new StructureDefinition { Kind = StructureKind.Stapler, Cost = 35, MaxHealth = 55, BuildSeconds = 1.2f, Range = 9,
                Damage = 4, ShotInterval = .65f, SlowSpeedMultiplier = .8f, SlowDuration = 2, MaxPierce = 1 },
            new StructureDefinition { Kind = StructureKind.Blotter, Cost = 30, MaxHealth = 55, BuildSeconds = 1.2f, Range = 9,
                Damage = 0, ShotInterval = 2, Radius = 1.5f, MaxPierce = 1 },
            new StructureDefinition { Kind = StructureKind.TarPit, Cost = 30, MaxHealth = 55, BuildSeconds = .8f, Radius = .7f, SlowSpeedMultiplier = .65f, SlowDuration = .3f },
            new StructureDefinition { Kind = StructureKind.RepairPost, Cost = 80, MaxHealth = 70, BuildSeconds = 2, Range = 5, RepairPerSecond = 8 }
        };
        public List<MaterialDefinition> MaterialDefinitions = new List<MaterialDefinition>
        {
            new MaterialDefinition(),
            new MaterialDefinition { CostMultiplier = 1.8f, HealthMultiplier = 2.1f, DamageMultiplier = 1.25f, RefundFraction = 0.4f, BuildTimeMultiplier = 1.3f },
            new MaterialDefinition { CostMultiplier = 3.2f, HealthMultiplier = 4.2f, DamageMultiplier = 1.6f, RefundFraction = 0.2f, BuildTimeMultiplier = 1.75f }
        };
        public List<EnemyDefinition> EnemyDefinitions = new List<EnemyDefinition>
        {
            new EnemyDefinition { Kind = EnemyKind.Doodle, MaxHealth = 22, Speed = 2.1f, Damage = 6, AttackInterval = 1.1f, Reward = 1 },
            new EnemyDefinition { Kind = EnemyKind.Runner, MaxHealth = 14, Speed = 3.6f, Damage = 3, AttackInterval = 0.8f, Reward = 1 },
            new EnemyDefinition { Kind = EnemyKind.Gnawer, MaxHealth = 95, Speed = 1.25f, Damage = 18, AttackInterval = 0.9f, Reward = 3, StructureDamageMultiplier = 1.5f },
            new EnemyDefinition { Kind = EnemyKind.Spitter, MaxHealth = 35, Speed = 1.7f, Damage = 10, AttackInterval = 2.4f, Reward = 2 },
            new EnemyDefinition { Kind = EnemyKind.Brute, MaxHealth = 420, Speed = 1.0f, Damage = 22, AttackInterval = 1.2f, Reward = 4 },
            new EnemyDefinition { Kind = EnemyKind.Bulwark, MaxHealth = 260, Speed = 1.6f, Damage = 12, AttackInterval = 1.1f, Reward = 3 }
        };
        public List<WaveDefinition> Waves = new List<WaveDefinition>
        {
            new WaveDefinition { Count = 80, Sides = 1, Title = UserText.Message("copy.b57031491fd45312") },
            new WaveDefinition { Count = 120, Sides = 2, SpawnInterval = 0.12f, RunnerFraction = 0.22f, Title = UserText.Message("copy.15133dadc79c441f") },
            new WaveDefinition { Count = 180, Sides = 5, SpawnInterval = 0.10f, RunnerFraction = 0.25f, GnawerFraction = 0.08f, Title = UserText.Message("copy.72d13cd7a4c9d0b6") },
            new WaveDefinition { Count = 260, Sides = 10, SpawnInterval = 0.085f, RunnerFraction = 0.32f, GnawerFraction = 0.10f, Title = UserText.Message("copy.5f2dcf576cec2ddf") },
            new WaveDefinition { Count = 360, Sides = 3, SpawnInterval = 0.075f, RunnerFraction = 0.3f, GnawerFraction = 0.13f, Title = UserText.Message("copy.2f4e9f83bb8177ea") },
            new WaveDefinition { Count = 500, Sides = 12, SpawnInterval = 0.065f, RunnerFraction = 0.35f, GnawerFraction = 0.15f, Title = UserText.Message("copy.aadf0337b265d332") },
            new WaveDefinition { Count = 680, Sides = 15, SpawnInterval = 0.055f, RunnerFraction = 0.36f, GnawerFraction = 0.18f, Title = UserText.Message("copy.a917970b5bdd9f78") },
            new WaveDefinition { Count = 1000, Sides = 15, SpawnInterval = 0.045f, RunnerFraction = 0.38f, GnawerFraction = 0.20f, Title = UserText.Message("copy.6d8146012bcec7d3") }
        };

        public static GameBalance CreateDefault() => new GameBalance();

        /// <summary>Owned tuning data for a run or a trial. Later edits to the source cannot change its rules.</summary>
        public GameBalance CreateSnapshot()
        {
            // Copy scalar tuning together, including future scalar fields added by another partial.
            var copy = (GameBalance)MemberwiseClone();
            copy.Mechanics = Mechanics.CreateSnapshot();
            copy.Construction = Construction.CreateSnapshot();
            copy.Effects = Effects.CreateSnapshot();
            copy.Progression = Progression.CreateSnapshot();
            copy.Content = Content;
            copy.Battlefield = Battlefield?.CreateSnapshot();
            copy.Combat = Combat.CreateSnapshot();
            copy.Crowd = Crowd.CreateSnapshot();
            copy.Trials = Trials.CreateSnapshot();
            copy.WellPlacements = WellPlacements.ConvertAll(item => item.CreateSnapshot());
            copy.StructureDefinitions = StructureDefinitions.ConvertAll(definition => new StructureDefinition
            {
                Kind = definition.Kind, Cost = definition.Cost, MaxHealth = definition.MaxHealth,
                BuildSeconds = definition.BuildSeconds, Range = definition.Range, Damage = definition.Damage,
                ShotInterval = definition.ShotInterval, Radius = definition.Radius,
                FlightSeconds = definition.FlightSeconds, MaxPierce = definition.MaxPierce,
                ConeAngleDegrees = definition.ConeAngleDegrees, MaxTargets = definition.MaxTargets, JumpRange = definition.JumpRange,
                SlowSpeedMultiplier = definition.SlowSpeedMultiplier, SlowDuration = definition.SlowDuration, RepairPerSecond = definition.RepairPerSecond
            });
            copy.MaterialDefinitions = MaterialDefinitions.ConvertAll(definition => new MaterialDefinition
            {
                CostMultiplier = definition.CostMultiplier, HealthMultiplier = definition.HealthMultiplier,
                DamageMultiplier = definition.DamageMultiplier, RefundFraction = definition.RefundFraction,
                BuildTimeMultiplier = definition.BuildTimeMultiplier
            });
            copy.EnemyDefinitions = EnemyDefinitions.ConvertAll(definition => new EnemyDefinition
            {
                Kind = definition.Kind, MaxHealth = definition.MaxHealth, Speed = definition.Speed,
                Damage = definition.Damage, AttackInterval = definition.AttackInterval,
                StructureDamageMultiplier = definition.StructureDamageMultiplier, VisualScale = definition.VisualScale,
                Range = definition.Range, WindupSeconds = definition.WindupSeconds,
                ProjectileSeconds = definition.ProjectileSeconds, Reward = definition.Reward
            });
            copy.Waves = Waves.ConvertAll(definition => new WaveDefinition
            {
                Count = definition.Count, SpawnInterval = definition.SpawnInterval,
                InkGrant = definition.InkGrant,
                RunnerFraction = definition.RunnerFraction, GnawerFraction = definition.GnawerFraction,
                SpitterFraction = definition.SpitterFraction, BruteFraction = definition.BruteFraction, BulwarkFraction = definition.BulwarkFraction, OutlinedFraction = definition.OutlinedFraction,
                HatchedFraction = definition.HatchedFraction, Sides = definition.Sides, Title = definition.Title
            });
            return copy;
        }

        public StructureDefinition Structure(StructureKind kind)
        {
            foreach (var definition in StructureDefinitions) if (definition.Kind == kind) return definition;
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        public MaterialDefinition Material(MaterialTier tier) => MaterialDefinitions[(int)tier];
        public EnemyDefinition Enemy(EnemyKind kind)
        {
            foreach (var definition in EnemyDefinitions) if (definition.Kind == kind) return definition;
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    public sealed class WeaponMountState
    {
        public Point2 Aim;
        public int TargetId;
        public float Cooldown,AcquisitionRemaining,ActionRemaining;
    }

    public sealed class StructureState
    {
        public int Id;
        public Cell Cell;
        public StructureKind Kind;
        public string ContentId;
        internal ResolvedTowerAssembly Assembly;
        public MaterialTier Tier;
        public float Health, MaxHealth, BuildRemaining;
        public bool IsComplete, WaitingForClearance;
        public Point2 Aim;
        readonly WeaponMountState[] mounts={new WeaponMountState(),new WeaponMountState(),new WeaponMountState()};
        public WeaponMountState GetMountState(int mount)
        {if(mount<0||mount>=mounts.Length)throw new ArgumentOutOfRangeException(nameof(mount));return mounts[mount];}
        public Point2 PrimaryAim {get=>mounts[0].Aim;set=>mounts[0].Aim=value;}
        public Point2 SecondaryAim {get=>mounts[1].Aim;set=>mounts[1].Aim=value;}
        public int PrimaryTargetId {get=>mounts[0].TargetId;set=>mounts[0].TargetId=value;}
        public int SecondaryTargetId {get=>mounts[1].TargetId;set=>mounts[1].TargetId=value;}
        public float Cooldown {get=>mounts[0].Cooldown;set=>mounts[0].Cooldown=value;}
        public TargetPriority Priority;
        public TowerBlueprint Blueprint;
        public InkColor Color;
        internal float SecondaryCooldown {get=>mounts[1].Cooldown;set=>mounts[1].Cooldown=value;}
        // Acquisition and normalized reload belong to each mount; zero action remaining is one ready action.
        internal float PrimaryAcquisitionRemaining {get=>mounts[0].AcquisitionRemaining;set=>mounts[0].AcquisitionRemaining=value;}
        internal float PrimaryActionRemaining {get=>mounts[0].ActionRemaining;set=>mounts[0].ActionRemaining=value;}
        internal float SecondaryActionRemaining {get=>mounts[1].ActionRemaining;set=>mounts[1].ActionRemaining=value;}
        internal TowerColorBudget ColorBudget;
        internal int PaidInk;
        internal int FrontageUsed;
    }

    public sealed class EnemyState
    {
        public int Id;
        public EnemyKind Kind;
        public Point2 Position;
        public float Health, MaxHealth;
        public float VisualScale = 1;
        public DrawingTier DrawingTier;
        public float BlotRemaining, RedRemaining, BlueRemaining, GreenRemaining, WindupRemaining;
        internal float RedTickRemaining;
        internal StructureKind RedSourceWeapon;
        internal string RedSourceWeaponId;
        internal int RewardBudget = -1;
        internal TimedEffectState RedEffect, BlueEffect, GreenEffect;
        internal int ObjectiveIndex = -1;
        internal int RangedObjectiveIndex = -1;
        internal Point2 RangedTarget;
        internal float RangedCooldown;
        internal bool WindingUp;
        internal float AttackCooldown;
        internal float SpeedScale = 1;
        internal Point2 Destination;
        internal Point2 MovementDirection;
        internal int AvoidanceNeighborId;
        internal float AvoidanceSide;
        internal Point2 NavigationDirection;
        internal float NavigationCost;
        internal bool YieldingToNeighbor;
        internal bool CrowdHoldingPosition;
        internal bool HasDestination;
        internal bool VisualDeathPublished;
    }

    public readonly struct ShotEvent
    {
        public readonly Point2 From, To;
        public readonly StructureKind Kind;
        public readonly string WeaponId;
        public readonly float Radius;
        public readonly int SourceStructureId, MountSlotIndex;
        public readonly long ActionId;
        public readonly double SimulationSeconds;
        public ShotEvent(Point2 from, Point2 to, StructureKind kind, float radius)
            : this(from,to,kind,radius,LegacyContentIds.Weapon(kind)) { }
        public ShotEvent(Point2 from,Point2 to,StructureKind kind,float radius,string weaponId,
            int sourceStructureId=0,int mountSlotIndex=-1,long actionId=0,double simulationSeconds=0)
        {From=from;To=to;Kind=kind;Radius=radius;WeaponId=weaponId;SourceStructureId=sourceStructureId;MountSlotIndex=mountSlotIndex;ActionId=actionId;SimulationSeconds=simulationSeconds;}
    }

    public sealed class BuildQuote
    {
        public IReadOnlyList<Cell> Cells;
        public int Cost;
        public bool Valid;
        public string Reason;
    }
}
