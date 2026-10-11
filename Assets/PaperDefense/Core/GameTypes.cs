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

    public enum MaterialTier { Pencil, Ink, Marker }
    public enum StructureKind { Wall, Gun, Piercer, Mortar, Spikes }
    public enum EnemyKind { Doodle, Runner, Gnawer, Spitter }
    public enum DrawingTier { Sketch, Outlined, Hatched }
    public enum InkColor { None, Red, Blue, Green }
    public enum RunPhase { Planning, Assault, Intermission, Victory, Defeat }
    public enum TargetPriority { Nearest, Strongest, Siege }
    public enum RunUpgrade { RapidFire, DeepInk, Salvage }

    public sealed class StructureDefinition
    {
        public StructureKind Kind;
        public int Cost;
        public float MaxHealth, BuildSeconds, Range, Damage, ShotInterval, Radius, FlightSeconds = .8f;
        public int MaxPierce;
    }

    public sealed class MaterialDefinition
    {
        public float CostMultiplier = 1, HealthMultiplier = 1, DamageMultiplier = 1;
        public float RefundFraction = 0.8f, BuildTimeMultiplier = 1;
    }

    public sealed class EnemyDefinition
    {
        public EnemyKind Kind;
        public float MaxHealth, Speed, Damage, AttackInterval;
        public float Range = 12.5f, WindupSeconds = .8f, ProjectileSeconds = .65f;
        public int Reward;
    }

    public sealed class WaveDefinition
    {
        public int Count = 80;
        public float SpawnInterval = 0.14f, RunnerFraction, GnawerFraction, SpitterFraction, OutlinedFraction, HatchedFraction;
        /// <summary>Spawn edges: west 1, east 2, north 4, south 8.</summary>
        public int Sides = 1;
        public string Title = "Первые каракули";
    }

    /// <summary>Mutable tuning data. Configure before constructing a session.</summary>
    public sealed class GameBalance
    {
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
            new StructureDefinition { Kind = StructureKind.Spikes, Cost = 8, MaxHealth = 35, BuildSeconds = 0.5f, Range = 0.7f, Damage = 9, ShotInterval = 0.65f, Radius = 0.65f }
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
            new EnemyDefinition { Kind = EnemyKind.Gnawer, MaxHealth = 95, Speed = 1.25f, Damage = 18, AttackInterval = 0.9f, Reward = 3 },
            new EnemyDefinition { Kind = EnemyKind.Spitter, MaxHealth = 35, Speed = 1.7f, Damage = 10, AttackInterval = 2.4f, Reward = 2 }
        };
        public List<WaveDefinition> Waves = new List<WaveDefinition>
        {
            new WaveDefinition { Count = 80, Sides = 1, Title = "Каракули с запада" },
            new WaveDefinition { Count = 120, Sides = 2, SpawnInterval = 0.12f, RunnerFraction = 0.22f, Title = "Бегуны с востока" },
            new WaveDefinition { Count = 180, Sides = 5, SpawnInterval = 0.10f, RunnerFraction = 0.25f, GnawerFraction = 0.08f, Title = "Первые грызуны · запад / север" },
            new WaveDefinition { Count = 260, Sides = 10, SpawnInterval = 0.085f, RunnerFraction = 0.32f, GnawerFraction = 0.10f, Title = "Два фронта · восток / юг" },
            new WaveDefinition { Count = 360, Sides = 3, SpawnInterval = 0.075f, RunnerFraction = 0.3f, GnawerFraction = 0.13f, Title = "Чернильный прилив · запад / восток" },
            new WaveDefinition { Count = 500, Sides = 12, SpawnInterval = 0.065f, RunnerFraction = 0.35f, GnawerFraction = 0.15f, Title = "Штурм полей · север / юг" },
            new WaveDefinition { Count = 680, Sides = 15, SpawnInterval = 0.055f, RunnerFraction = 0.36f, GnawerFraction = 0.18f, Title = "Со всех сторон" },
            new WaveDefinition { Count = 1000, Sides = 15, SpawnInterval = 0.045f, RunnerFraction = 0.38f, GnawerFraction = 0.20f, Title = "Финальная осада · 1000 каракуль" }
        };

        public static GameBalance CreateDefault() => new GameBalance();
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

    public sealed class StructureState
    {
        public int Id;
        public Cell Cell;
        public StructureKind Kind;
        public MaterialTier Tier;
        public float Health, MaxHealth, BuildRemaining, Cooldown;
        public bool IsComplete, WaitingForClearance;
        public Point2 Aim;
        public TargetPriority Priority;
        public TowerBlueprint Blueprint;
        public InkColor Color;
        internal float SecondaryCooldown, AcquisitionRemaining;
        internal int TargetId;
        internal int PaidInk;
        internal int FrontageUsed;
    }

    public sealed class EnemyState
    {
        public int Id;
        public EnemyKind Kind;
        public Point2 Position;
        public float Health, MaxHealth;
        public DrawingTier DrawingTier;
        public float BlotRemaining, RedRemaining, BlueRemaining, GreenRemaining, WindupRemaining;
        internal float RedTickRemaining;
        internal StructureKind RedSourceWeapon;
        internal int ObjectiveIndex = -1;
        internal int RangedObjectiveIndex = -1;
        internal Point2 RangedTarget;
        internal float RangedCooldown;
        internal bool WindingUp;
        internal float AttackCooldown;
        internal float SpeedScale = 1;
        internal Point2 Destination;
        internal bool HasDestination;
    }

    public readonly struct ShotEvent
    {
        public readonly Point2 From, To;
        public readonly StructureKind Kind;
        public readonly float Radius;
        public ShotEvent(Point2 from, Point2 to, StructureKind kind, float radius)
        { From = from; To = to; Kind = kind; Radius = radius; }
    }

    public sealed class BuildQuote
    {
        public IReadOnlyList<Cell> Cells;
        public int Cost;
        public bool Valid;
        public string Reason;
    }
}
