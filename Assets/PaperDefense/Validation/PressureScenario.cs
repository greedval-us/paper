using System;
using System.Collections.Generic;
using PaperDefense.Core;

namespace PaperDefense.Validation
{
    /// <summary>A public-command replay: a natural breach, construction under pressure, and 3000 live enemies.</summary>
    public sealed class PressureScenario
    {
        private static readonly Cell[] Directions =
        {
            new Cell(-1, 0), new Cell(1, 0), new Cell(0, -1), new Cell(0, 1),
            new Cell(-1, -1), new Cell(1, -1), new Cell(-1, 1), new Cell(1, 1)
        };
        private static readonly Cell[] ExtraBatteryOffsets =
        {
            new Cell(-5, -1), new Cell(-5, 1), new Cell(5, -1), new Cell(5, 1),
            new Cell(-1, -5), new Cell(1, -5), new Cell(-1, 5), new Cell(1, 5)
        };
        private readonly List<Cell> outerRing = new List<Cell>();
        private bool waveStarted, batteriesOrdered;
        private int highestObservedEnemyId;

        public GameSession Session { get; }
        public IRunReadModel ReadModel { get; }
        public PressureScenarioConfig Config { get; }
        public List<PressureScenarioCommand> Commands { get; } = new List<PressureScenarioCommand>();
        public PressureScenarioReport Report { get; } = new PressureScenarioReport();
        public bool Finished => Report.Completed;

        public PressureScenario(PressureScenarioConfig config = null, GameBalance configuredBalance = null)
        {
            Config = config ?? new PressureScenarioConfig();
            var balance = configuredBalance == null ? GameBalance.CreateDefault() : configuredBalance.CreateSnapshot();
            // This replay owns an open-field four-sided arena and a purchased closed wall ring.
            // Keep configured combat/art tuning, but do not inherit a production road map.
            balance.Battlefield = null;
            balance.Width = Config.Width; balance.Height = Config.Height;
            balance.StartingInk = Config.StartingInk; balance.BaseHealth = Config.BaseHealth;
            balance.PlanningSeconds = Config.PlanningSeconds;
            balance.SimulationStep = Config.StepSeconds;
            balance.MaxEnemiesPerCell = Config.MaxEnemiesPerCell;
            balance.PassiveInkPerSecond = 0; balance.EarlyLaunchInkPerSecond = 0; balance.WaveInkGrant = 0;
            balance.Enemy(EnemyKind.Doodle).Speed = Config.DoodleSpeed;
            balance.Enemy(EnemyKind.Runner).Speed = Config.RunnerSpeed;
            balance.Enemy(EnemyKind.Gnawer).Speed = Config.GnawerSpeed;
            balance.Waves = new List<WaveDefinition>
            {
                new WaveDefinition { Count = Config.EnemyCount, SpawnInterval = Config.SpawnInterval,
                    Sides = 15, RunnerFraction = Config.RunnerFraction, GnawerFraction = Config.GnawerFraction,
                    Title = UserText.Message("copy.b66dab71e0295f6c") }
            };
            Config.Balance = new PressureBalanceSnapshot(balance);
            Session = new GameSession(balance, Config.Seed, false);
            ReadModel = new RunReadModel(Session);
            Report.InnerBatteryFrontRadius = Config.InnerRadii[Config.InnerRadii.Length - 1] + 1;
            Report.MinimumEnemyRadiusAfterBreach = float.MaxValue;
            Report.ExpectedKills = Config.EnemyCount;
            BuildRing();
            Observe();
        }

        /// <summary>Every invocation advances one identical core step, regardless of rendering or speed.</summary>
        public void Step()
        {
            if (Finished) return;
            if (!waveStarted && Session.TickIndex >= Config.WarmupTicks)
            {
                Report.OuterWallCountAtLaunch = CountCompleteOuterWalls();
                Report.ClosedOuterRingAtLaunch = Report.OuterWallCountAtLaunch == outerRing.Count;
                Session.StartNextWave();
                Commands.Add(new PressureScenarioCommand
                {
                    Tick = Session.TickIndex, ElapsedSeconds = Session.Elapsed, Action = "StartWave",
                    Condition = "Planning construction has completed", Success = Session.Phase == RunPhase.Assault,
                    Phase = Session.Phase.ToString(), InkAfter = Session.Ink
                });
                waveStarted = true;
            }
            if (!batteriesOrdered && Report.FirstBreachTick > 0) DeployBatteries();
            Session.Tick(Config.StepSeconds);
            Observe();
            if (Session.Phase == RunPhase.Victory || Session.Phase == RunPhase.Defeat || Session.TickIndex >= Config.MaxTicks)
                Finish();
        }

        private void BuildRing()
        {
            Cell center = Session.BaseCell;
            int r = Config.OuterRadius;
            for (int x = -r; x <= r; x++)
            { outerRing.Add(new Cell(center.X + x, center.Y - r)); outerRing.Add(new Cell(center.X + x, center.Y + r)); }
            for (int y = -r + 1; y < r; y++)
            { outerRing.Add(new Cell(center.X - r, center.Y + y)); outerRing.Add(new Cell(center.X + r, center.Y + y)); }
            IssueBuild(outerRing, StructureKind.Wall, "Complete closed outer ring before assault");
        }

        private void DeployBatteries()
        {
            batteriesOrdered = true;
            foreach (int radius in Config.InnerRadii)
                foreach (Cell direction in Directions)
                    IssueBattery(new Cell(Session.BaseCell.X + radius * direction.X, Session.BaseCell.Y + radius * direction.Y));
            foreach (Cell offset in ExtraBatteryOffsets)
                IssueBattery(new Cell(Session.BaseCell.X + offset.X, Session.BaseCell.Y + offset.Y));
        }

        private void IssueBattery(Cell cell)
        {
            StructureKind kind = Report.CombatWeaponsBuilt % 4 == 3 ? StructureKind.Piercer : StructureKind.Mortar;
            if (IssueBuild(new[] { cell }, kind, "First outer wall was destroyed by enemy attacks"))
                Report.CombatWeaponsBuilt++;
        }

        private bool IssueBuild(IReadOnlyList<Cell> cells, StructureKind kind, string condition)
        {
            int inkBefore = Session.Ink;
            RunPhase phase = Session.Phase;
            bool success = Session.TryBuild(cells, kind, MaterialTier.Marker, out string reason);
            var command = new PressureScenarioCommand
            {
                Tick = Session.TickIndex, ElapsedSeconds = Session.Elapsed, Action = "Build",
                Condition = condition, Kind = kind, Tier = MaterialTier.Marker, Phase = phase.ToString(),
                Success = success, Reason = reason, Cost = inkBefore - Session.Ink, InkAfter = Session.Ink,
                Cells = new PressureCell[cells.Count]
            };
            for (int i = 0; i < cells.Count; i++) command.Cells[i] = new PressureCell(cells[i]);
            Commands.Add(command);
            if (phase == RunPhase.Assault && success) Report.CombatBuildCommands++;
            if (!success) Report.FailedCommands++;
            return success;
        }

        private void Observe()
        {
            Report.Tick = Session.TickIndex; Report.ElapsedSeconds = Session.Elapsed;
            Report.Phase = Session.Phase.ToString(); Report.BaseHealth = Session.BaseHealth;
            Report.Ink = Session.Ink; Report.Kills = Session.Kills; Report.Alive = Session.Enemies.Count;
            Report.RemainingToSpawn = Session.RemainingToSpawn;
            if (Session.Statistics.PeakAliveEnemies > Report.PeakAliveEnemies)
            { Report.PeakAliveEnemies = Session.Statistics.PeakAliveEnemies; Report.PeakTick = Session.TickIndex; }
            if (Report.FirstBreachTick == 0 && Session.Statistics.DestroyedStructures > 0)
                foreach (BreachRecord breach in Session.Statistics.Breaches)
                    if (breach.Kind == StructureKind.Wall && Radius(breach.Cell) == Config.OuterRadius)
                    {
                        Report.FirstBreachTick = breach.Tick; Report.FirstBreachSeconds = breach.Elapsed;
                        Report.FirstBreachCell = new PressureCell(breach.Cell);
                        Report.AliveAtFirstBreach = Session.Enemies.Count;
                        Report.OuterWallCountAtFirstBreach = CountCompleteOuterWalls();
                        break;
                    }
            float cx = Session.BaseCell.X + .5f, cy = Session.BaseCell.Y + .5f;
            foreach (EnemyState enemy in Session.Enemies)
            {
                if (enemy.Id > highestObservedEnemyId)
                {
                    highestObservedEnemyId = enemy.Id;
                    if (enemy.Position.X < 1) Report.WestSpawns++;
                    else if (enemy.Position.X >= Config.Width - 1) Report.EastSpawns++;
                    else if (enemy.Position.Y >= Config.Height - 1) Report.NorthSpawns++;
                    else if (enemy.Position.Y < 1) Report.SouthSpawns++;
                }
                if (Report.FirstBreachTick == 0) continue;
                float radius = Math.Max(Math.Abs(enemy.Position.X - cx), Math.Abs(enemy.Position.Y - cy));
                Report.MinimumEnemyRadiusAfterBreach = Math.Min(Report.MinimumEnemyRadiusAfterBreach, radius);
                if (Report.FirstInnerFrontTick == 0 && radius <= Report.InnerBatteryFrontRadius)
                    Report.FirstInnerFrontTick = Session.TickIndex;
            }
            Report.DestroyedStructures = Session.Statistics.DestroyedStructures;
            Report.MortarLaunches = Session.Statistics.Launches(StructureKind.Mortar);
            Report.PiercerLaunches = Session.Statistics.Launches(StructureKind.Piercer);
            Report.MortarDamage = Session.Statistics.Damage(StructureKind.Mortar);
            Report.PiercerDamage = Session.Statistics.Damage(StructureKind.Piercer);
        }

        private void Finish()
        {
            Report.Completed = true;
            Report.TimedOut = Session.Phase == RunPhase.Assault || Session.Phase == RunPhase.Planning;
            Report.Passed = Session.Phase == RunPhase.Victory && Session.BaseHealth > 0 &&
                Session.Kills == Config.EnemyCount && Report.PeakAliveEnemies == Config.EnemyCount &&
                Report.ClosedOuterRingAtLaunch && Report.FirstBreachTick > 0 && Report.FirstInnerFrontTick > 0 &&
                Report.CombatWeaponsBuilt == Config.InnerRadii.Length * 8 + ExtraBatteryOffsets.Length &&
                Report.FailedCommands == 0 && Report.MortarDamage > 0 && Report.PiercerDamage > 0 &&
                Report.WestSpawns > 0 && Report.EastSpawns > 0 && Report.NorthSpawns > 0 && Report.SouthSpawns > 0;
            Report.Fingerprint = Fingerprint();
        }

        private int Radius(Cell cell) => Math.Max(Math.Abs(cell.X - Session.BaseCell.X), Math.Abs(cell.Y - Session.BaseCell.Y));
        private int CountCompleteOuterWalls()
        {
            int count = 0;
            foreach (Cell cell in outerRing)
            { var structure = Session.StructureAt(cell); if (structure != null && structure.IsComplete && structure.Health > 0) count++; }
            return count;
        }

        /// <summary>Same-version state signature; elapsed profiling measurements are deliberately excluded.</summary>
        public string Fingerprint()
        {
            var hash = new StateHash();
            hash.Add(Session.TickIndex); hash.Add((int)Session.Phase); hash.Add(Session.Elapsed);
            hash.Add(Session.Ink); hash.Add(Session.Kills); hash.Add(Session.BaseHealth); hash.Add(Session.RemainingToSpawn);
            hash.Add(Session.TopologyVersion); hash.Add(Session.StructureVisualVersion); hash.Add(Session.FlowRebuildCount);
            foreach (StructureState structure in Session.Structures)
            {
                hash.Add(structure.Id); hash.Add(structure.Cell.X); hash.Add(structure.Cell.Y);
                hash.Add((int)structure.Kind); hash.Add((int)structure.Tier); hash.Add(structure.Health);
                hash.Add(structure.MaxHealth); hash.Add(structure.BuildRemaining); hash.Add(structure.Cooldown);
                hash.Add(structure.IsComplete ? 1 : 0); hash.Add(structure.WaitingForClearance ? 1 : 0);
                hash.Add(structure.Aim.X); hash.Add(structure.Aim.Y); hash.Add((int)structure.Priority);
            }
            foreach (EnemyState enemy in Session.Enemies)
            { hash.Add(enemy.Id); hash.Add((int)enemy.Kind); hash.Add(enemy.Health); hash.Add(enemy.Position.X); hash.Add(enemy.Position.Y); }
            foreach (MortarProjectile shell in Session.Projectiles)
            { hash.Add(shell.Id); hash.Add(shell.To.X); hash.Add(shell.To.Y); hash.Add(shell.Remaining); hash.Add(shell.Damage); }
            foreach (StructureKind kind in Enum.GetValues(typeof(StructureKind)))
            { hash.Add(Session.Statistics.Damage(kind)); hash.Add(Session.Statistics.IdleSeconds(kind)); hash.Add(Session.Statistics.Launches(kind)); }
            foreach (PressureScenarioCommand command in Commands)
            { hash.Add(command.Tick); hash.Add(command.Cost); hash.Add(command.Success ? 1 : 0); hash.Add(command.InkAfter); }
            return hash.Value.ToString("x16");
        }

        private sealed class StateHash
        {
            public ulong Value = 14695981039346656037UL;
            public void Add(long value) { unchecked { for (int i = 0; i < 8; i++) { Value ^= (byte)value; Value *= 1099511628211UL; value >>= 8; } } }
            public void Add(float value) => Add(BitConverter.ToInt32(BitConverter.GetBytes(value), 0));
        }
    }

    [Serializable] public sealed class PressureScenarioConfig
    {
        public int Seed = 1337, Width = 96, Height = 64, StartingInk = 20000, EnemyCount = 3000;
        public int MaxEnemiesPerCell = 6, OuterRadius = 16, WarmupTicks = 150, MaxTicks = 18000;
        public float StepSeconds = 1f / 30f, PlanningSeconds = 1000, BaseHealth = 250;
        public float SpawnInterval = .005f, RunnerFraction = .2f, GnawerFraction = .2f;
        public float DoodleSpeed = 1, RunnerSpeed = 1.6f, GnawerSpeed = .8f;
        public int[] InnerRadii = { 3, 5, 7, 9, 11 };
        public string Rules = "Default finite health, damage, reload, construction and material rules. Scenario tuning sets map, starting budget, enemy mix/speeds and spawn interval; passive supply, early-launch income and wave grants are zero. No direct entity mutation. At tick 0 buy a closed Marker wall ring. After 150 fixed steps launch. After the first naturally destroyed outer wall, buy 48 inner Marker weapons: three mortars then one piercer, repeated. No further commands or injected bonuses.";
        public PressureBalanceSnapshot Balance;
    }

    [Serializable] public struct PressureCell
    {
        public int X, Y;
        public PressureCell(Cell cell) { X = cell.X; Y = cell.Y; }
    }
    [Serializable] public sealed class PressureScenarioCommand
    {
        public long Tick;
        public float ElapsedSeconds;
        public string Action, Condition, Phase, Reason;
        public StructureKind Kind;
        public MaterialTier Tier;
        public PressureCell[] Cells = new PressureCell[0];
        public bool Success;
        public int Cost, InkAfter;
    }
    [Serializable] public sealed class PressureScenarioReport
    {
        public bool Completed, Passed, TimedOut, ClosedOuterRingAtLaunch;
        public string Phase, Fingerprint;
        public long Tick, PeakTick, FirstBreachTick, FirstInnerFrontTick;
        public float ElapsedSeconds, BaseHealth, FirstBreachSeconds, MinimumEnemyRadiusAfterBreach;
        public float InnerBatteryFrontRadius, MortarDamage, PiercerDamage;
        public PressureCell FirstBreachCell;
        public int Ink, Kills, Alive, RemainingToSpawn, ExpectedKills, PeakAliveEnemies, AliveAtFirstBreach;
        public int OuterWallCountAtLaunch, OuterWallCountAtFirstBreach, CombatWeaponsBuilt, CombatBuildCommands;
        public int FailedCommands, DestroyedStructures, MortarLaunches, PiercerLaunches;
        public int WestSpawns, EastSpawns, NorthSpawns, SouthSpawns;
    }

    [Serializable] public sealed class PressureBalanceSnapshot
    {
        public int Width, Height, StartingInk, MaxEnemiesPerCell, MaxEarlyLaunchInk, WaveInkGrant;
        public float BaseHealth, PlanningSeconds, IntermissionSeconds, SimulationStep, PassiveInkPerSecond, EarlyLaunchInkPerSecond;
        public List<PressureStructureTuning> Structures = new List<PressureStructureTuning>();
        public List<PressureEnemyTuning> Enemies = new List<PressureEnemyTuning>();
        public List<PressureMaterialTuning> Materials = new List<PressureMaterialTuning>();
        public int WaveCount, SpawnSides;
        public float SpawnInterval, RunnerFraction, GnawerFraction;
        public PressureBalanceSnapshot(GameBalance balance)
        {
            Width = balance.Width; Height = balance.Height; StartingInk = balance.StartingInk; BaseHealth = balance.BaseHealth;
            MaxEnemiesPerCell = balance.MaxEnemiesPerCell; PlanningSeconds = balance.PlanningSeconds;
            IntermissionSeconds = balance.IntermissionSeconds; SimulationStep = balance.SimulationStep;
            PassiveInkPerSecond = balance.PassiveInkPerSecond; EarlyLaunchInkPerSecond = balance.EarlyLaunchInkPerSecond;
            MaxEarlyLaunchInk = balance.MaxEarlyLaunchInk; WaveInkGrant = balance.WaveInkGrant;
            foreach (StructureDefinition d in balance.StructureDefinitions) Structures.Add(new PressureStructureTuning
            { Kind = d.Kind, Cost = d.Cost, MaxHealth = d.MaxHealth, BuildSeconds = d.BuildSeconds, Range = d.Range,
                Damage = d.Damage, ShotInterval = d.ShotInterval, Radius = d.Radius, FlightSeconds = d.FlightSeconds, MaxPierce = d.MaxPierce });
            foreach (EnemyDefinition d in balance.EnemyDefinitions) Enemies.Add(new PressureEnemyTuning
            { Kind = d.Kind, MaxHealth = d.MaxHealth, Speed = d.Speed, Damage = d.Damage, AttackInterval = d.AttackInterval, Reward = d.Reward });
            for (int i = 0; i < balance.MaterialDefinitions.Count; i++)
            {
                MaterialDefinition d = balance.MaterialDefinitions[i];
                Materials.Add(new PressureMaterialTuning { Tier = (MaterialTier)i, CostMultiplier = d.CostMultiplier,
                    HealthMultiplier = d.HealthMultiplier, DamageMultiplier = d.DamageMultiplier, RefundFraction = d.RefundFraction,
                    BuildTimeMultiplier = d.BuildTimeMultiplier });
            }
            WaveDefinition wave = balance.Waves[0];
            WaveCount = wave.Count; SpawnSides = wave.Sides; SpawnInterval = wave.SpawnInterval;
            RunnerFraction = wave.RunnerFraction; GnawerFraction = wave.GnawerFraction;
        }
    }
    [Serializable] public sealed class PressureStructureTuning
    {
        public StructureKind Kind;
        public int Cost, MaxPierce;
        public float MaxHealth, BuildSeconds, Range, Damage, ShotInterval, Radius, FlightSeconds;
    }
    [Serializable] public sealed class PressureEnemyTuning
    {
        public EnemyKind Kind;
        public int Reward;
        public float MaxHealth, Speed, Damage, AttackInterval;
    }
    [Serializable] public sealed class PressureMaterialTuning
    {
        public MaterialTier Tier;
        public float CostMultiplier, HealthMultiplier, DamageMultiplier, RefundFraction, BuildTimeMultiplier;
    }
}
