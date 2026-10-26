using System;
using System.Collections.Generic;
using System.Linq;

namespace PaperDefense.Core
{
    public enum AssemblyTrialScenario
    {
        Crowd = 0, Fast = 1, Armored = 2, Blots = 3, Repair = 4, Generator = 5, Single = 6, Line = 7
    }

    /// <summary>A measured, profile-free run. Null metrics do not apply to this assembly's role.</summary>
    public sealed class AssemblyTrialReport
    {
        public string EntryId { get; internal set; }
        public string EntryName { get; internal set; }
        public AssemblyTrialScenario Scenario { get; internal set; }
        public ResolvedEntry Resolved { get; internal set; }
        public TrialReport Recording { get; internal set; }
        public IReadOnlyList<TrialFrame> Frames => Recording.Frames;
        public int Budget { get; internal set; }
        public int Seed { get; internal set; }
        public int UnitCost { get; internal set; }
        public int Copies { get; internal set; }
        public int Cost { get; internal set; }
        public float DurationSeconds { get; internal set; }
        public float? Damage { get; internal set; }
        public float? IdleSeconds { get; internal set; }
        public float? RepairedHealth { get; internal set; }
        public float? PreventedMovement { get; internal set; }
        public int? Leaks { get; internal set; }
        public int? UnresolvedEnemies { get; internal set; }
        public int? BlotsCleared { get; internal set; }
        public int? UnresolvedBlots { get; internal set; }
        public int? Income { get; internal set; }
        public int? CompletedWaves { get; internal set; }
        public int? PaidWaves { get; internal set; }
    }

    /// <summary>Uses the production firing lane and production support sessions, without a profile store.</summary>
    public static class AssemblyTrialRunner
    {
        static readonly IReadOnlyList<AssemblyTrialScenario> towerScenarios = Array.AsReadOnly(new[] {
            AssemblyTrialScenario.Crowd, AssemblyTrialScenario.Fast, AssemblyTrialScenario.Armored,
            AssemblyTrialScenario.Blots, AssemblyTrialScenario.Single, AssemblyTrialScenario.Line });
        static readonly IReadOnlyList<AssemblyTrialScenario> supportScenarios = Array.AsReadOnly(new[] {
            AssemblyTrialScenario.Crowd, AssemblyTrialScenario.Fast, AssemblyTrialScenario.Armored,
            AssemblyTrialScenario.Single, AssemblyTrialScenario.Line });
        static readonly IReadOnlyList<AssemblyTrialScenario> repairScenarios = Array.AsReadOnly(new[] { AssemblyTrialScenario.Repair });
        static readonly IReadOnlyList<AssemblyTrialScenario> generatorScenarios = Array.AsReadOnly(new[] { AssemblyTrialScenario.Generator });

        public static IReadOnlyList<AssemblyTrialScenario> ScenariosFor(AlbumEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (entry.Kind != AlbumEntryKind.Structure) return towerScenarios;
            switch (entry.Snapshot.Recipe.Structure.Family)
            {
                case AssemblyStructureFamily.Repair: return repairScenarios;
                case AssemblyStructureFamily.Generator: return generatorScenarios;
                default: return supportScenarios;
            }
        }

        public static AssemblyTrialReport Run(AlbumEntry entry, GameBalance balance, AssemblyTrialScenario scenario,
            int budget = 400, int seed = 1337)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            BalanceValidation.Validate(balance);
            if (!Enum.IsDefined(typeof(AssemblyTrialScenario), scenario)) throw new ArgumentOutOfRangeException(nameof(scenario));
            if (budget < 0 || budget > TrialRules.MaximumBudget) throw new ArgumentOutOfRangeException(nameof(budget));
            if (!ScenariosFor(entry).Contains(scenario)) throw new ArgumentException("Scenario does not apply to this assembly.", nameof(scenario));
            var rules = balance.CreateSnapshot();
            var resolution = TowerCatalog.ResolveEntry(entry, rules);
            if (!resolution.Valid) throw new ArgumentException(string.Join(",", resolution.Diagnostics.Select(d => d.Code + ":" + d.Path)), nameof(entry));
            var report = new AssemblyTrialReport { EntryId = entry.Id, EntryName = entry.Name, Scenario = scenario,
                Resolved = resolution.Value, Budget = budget, Seed = seed, UnitCost = resolution.Value.Cost };
            if (entry.Kind == AlbumEntryKind.Structure) RunSupport(entry, rules, report);
            else RunTower(entry, rules, report);
            report.Copies = report.Recording.Copies; report.Cost = report.Recording.Cost;
            report.DurationSeconds = report.Recording.DurationSeconds;
            return report;
        }

        static void RunTower(AlbumEntry entry, GameBalance rules, AssemblyTrialReport report)
        {
            TrialScenario legacyScenario = LegacyScenario(report.Scenario);
            if (report.Scenario == AssemblyTrialScenario.Single || report.Scenario == AssemblyTrialScenario.Line)
                report.Recording = TrialRunner.RunAssemblyFixture(entry, rules, legacyScenario, report.Budget,
                    report.Seed, TargetFixture(rules, report.Scenario), 0);
            else report.Recording = TrialRunner.Run(entry, rules, legacyScenario, report.Budget, report.Seed);
            bool cleanerOnly = report.Resolved.Weapons.Count > 0 && report.Resolved.Weapons.All(w => w.Family == AssemblyWeaponFamily.Blotter);
            report.Damage = cleanerOnly ? (float?)null : report.Recording.Damage;
            report.IdleSeconds = report.Recording.IdleSeconds;
            if (!cleanerOnly) { report.Leaks = report.Recording.Leaks; report.UnresolvedEnemies = report.Recording.UnresolvedEnemies; }
            if (report.Scenario == AssemblyTrialScenario.Blots)
            { report.BlotsCleared = report.Recording.BlotsCleared; report.UnresolvedBlots = report.Recording.UnresolvedBlots; }
        }

        static TrialScenario LegacyScenario(AssemblyTrialScenario scenario)
            => scenario == AssemblyTrialScenario.Fast ? TrialScenario.Fast : scenario == AssemblyTrialScenario.Armored ? TrialScenario.Armored :
               scenario == AssemblyTrialScenario.Blots ? TrialScenario.Blots : TrialScenario.Crowd;

        static TrialScenarioDefinition TargetFixture(GameBalance rules, AssemblyTrialScenario scenario)
        {
            var fixture = rules.Trials.For(LegacyScenario(scenario)).CreateSnapshot();
            if (scenario == AssemblyTrialScenario.Single) { fixture.Count = 1; fixture.SpawnInterval = .1f; }
            else if (scenario == AssemblyTrialScenario.Line) { fixture.Count = 24; fixture.SpawnInterval = .5f; }
            return fixture;
        }

        static GameBalance SupportRules(GameBalance rules, int budget, int fixtureCost)
        {
            var owned = rules.CreateSnapshot();
            owned.Battlefield = null; owned.Crowd.Enabled = false; owned.EnableInkWells = false;
            owned.Width = Math.Max(64, owned.Width); owned.Height = Math.Max(40, owned.Height);
            owned.BaseHealth = 100000000;
            owned.StartingInk = (int)Math.Min(int.MaxValue, (long)budget + fixtureCost);
            owned.PlanningSeconds = owned.IntermissionSeconds = 1000000;
            owned.PassiveInkPerSecond = owned.EarlyLaunchInkPerSecond = 0; owned.MaxEarlyLaunchInk = 0;
            owned.WaveInkGrant = owned.WaveGold = owned.VictoryGold = 0;
            owned.Progression.UpgradeEveryWaves = 0;
            owned.Waves.Clear();
            for (int i = 0; i < 8; i++) owned.Waves.Add(new WaveDefinition { Count = 0, InkGrant = 0 });
            return owned;
        }

        static void RunSupport(AlbumEntry entry, GameBalance balance, AssemblyTrialReport report)
        {
            bool repair = report.Scenario == AssemblyTrialScenario.Repair;
            bool generator = report.Scenario == AssemblyTrialScenario.Generator;
            int fixtureCost = repair ? TowerCatalog.ToolCost(balance, StructureKind.Wall, MaterialTier.Pencil) : 0;
            var rules = SupportRules(balance, report.Budget, fixtureCost);
            int count = report.UnitCost <= 0 ? 0 : report.Budget / report.UnitCost;
            if (generator) count = Math.Min(count, report.Resolved.Support.MaximumInstances);
            if (!generator && !repair)
            {
                int perCell = Math.Max(1, Math.Min(4, rules.MaxEnemiesPerCell));
                int targetCount = TargetFixture(rules, report.Scenario).Count;
                int columns = (targetCount + perCell * (rules.Height - 4) - 1) / (perCell * (rules.Height - 4));
                rules.Width = Math.Max(rules.Width, columns + 16);
            }
            var targets = generator || repair ? Array.Empty<PreparedEnemy>() : SupportTargets(rules, report.Scenario, report.Seed);
            if (!generator && !repair)
            {
                var fixture = TargetFixture(rules, report.Scenario);
                rules.Enemy(fixture.EnemyKind).Speed = fixture.Speed;
            }
            var session = targets.Length == 0 ? new GameSession(rules, report.Seed, false) : GameSession.CreatePrepared(rules, report.Seed, targets);
            GameSession reference = report.Resolved.Support.Kind == AssemblyStructureFamily.Slow ?
                GameSession.CreatePrepared(rules, report.Seed, targets) : null;
            StructureState target = null;
            if (repair)
            {
                if (!session.TryBuild(new[] { new Cell(10, 10) }, StructureKind.Wall, MaterialTier.Pencil, out string reason))
                    throw new InvalidOperationException(reason);
                target = session.StructureAt(new Cell(10, 10)); Ready(target);
                // The independent target is a fixture, not one of the purchased support copies.
                target.MaxHealth = 100000; target.Health = 20;
            }
            var candidates = SupportCells(rules, repair ? new Cell(10, 10) : new Cell(12, 10));
            int built = 0;
            foreach (var cell in candidates)
            {
                if (built >= count) break;
                if (!session.TryBuildEntry(new[] { cell }, report.Resolved, MaterialTier.Pencil, InkColor.None,
                    out _, entry.Id)) continue;
                Ready(session.StructureAt(cell)); built++;
            }
            var recording = new TrialReport { BlueprintId = entry.Id, Scenario = LegacyScenario(report.Scenario),
                Budget = report.Budget, Seed = report.Seed, Copies = built, Cost = built * report.UnitCost,
                Color = InkColor.None, DrawingQuality = 100 };
            report.Recording = recording;
            var frames = new List<TrialFrame>();
            var frameEvents = new List<SimulationEvent>(); long eventCursor = 0;
            frames.Add(Frame(session, 0, frameEvents));
            float initialHealth = target?.Health ?? 0;
            float prevented = 0; int initialInk = session.Ink;
            int ticks = generator ? 8 : rules.Trials.MaximumTicks;
            for (int tick = 0; tick < ticks; tick++)
            {
                var before = reference == null ? null : Positions(session);
                var referenceBefore = reference == null ? null : Positions(reference);
                if (generator) session.StartNextWave();
                session.Tick(rules.Trials.StepSeconds); reference?.Tick(rules.Trials.StepSeconds);
                recording.DurationSeconds = (tick + 1) * rules.Trials.StepSeconds;
                if (reference != null) prevented += Math.Max(0, Movement(reference, referenceBefore) - Movement(session, before));
                foreach (var value in session.EventBuffer)
                    if (value.Sequence > eventCursor)
                    { if (frameEvents.Count < TrialRunner.MaximumFrameEvents) frameEvents.Add(value); eventCursor = value.Sequence; }
                if (generator || (tick + 1) % TrialRules.RecordingStrideTicks == 0 || tick == ticks - 1)
                { frames.Add(Frame(session, recording.DurationSeconds, frameEvents)); frameEvents.Clear(); }
            }
            recording.Frames = frames.AsReadOnly();
            recording.SurvivingTowers = session.Structures.Count(s => s.SourceAlbumEntryId == entry.Id && s.Health > 0);
            if (repair) report.RepairedHealth = target.Health - initialHealth;
            if (reference != null) report.PreventedMovement = prevented;
            if (generator)
            {
                report.CompletedWaves = session.CompletedWaves;
                report.Income = session.Ink - initialInk;
                report.PaidWaves = session.Structures.Where(s => s.SourceAlbumEntryId == entry.Id)
                    .Select(s => s.GeneratorPaidWaves).DefaultIfEmpty(0).Min();
            }
        }

        static TrialFrame Frame(GameSession session, float seconds, IReadOnlyList<SimulationEvent> events)
            => new TrialFrame(seconds, session.Enemies, session.Structures, session.Projectiles, session.Shots,
                session.Blots, events: events);

        static void Ready(StructureState structure)
        { structure.IsComplete = true; structure.BuildRemaining = 0; structure.WaitingForClearance = false; }

        static List<Cell> SupportCells(GameBalance rules, Cell center)
        {
            var cells = new List<Cell>();
            for (int y = 1; y < rules.Height - 1; y++)
                for (int x = 1; x < rules.Width - 1; x++) cells.Add(new Cell(x, y));
            cells.Sort((a, b) => {
                long da = (long)(a.X - center.X) * (a.X - center.X) + (long)(a.Y - center.Y) * (a.Y - center.Y);
                long db = (long)(b.X - center.X) * (b.X - center.X) + (long)(b.Y - center.Y) * (b.Y - center.Y);
                int difference = da.CompareTo(db); return difference != 0 ? difference : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
            });
            return cells;
        }

        static PreparedEnemy[] SupportTargets(GameBalance rules, AssemblyTrialScenario scenario, int seed)
        {
            var fixture = TargetFixture(rules, scenario);
            var result = new PreparedEnemy[fixture.Count];
            var random = new Random(seed);
            int perCell = Math.Max(1, Math.Min(4, rules.MaxEnemiesPerCell));
            int rows = rules.Height - 4;
            int columns = (fixture.Count + perCell * rows - 1) / (perCell * rows);
            int firstX = Math.Max(8, columns + 1);
            for (int i = 0; i < result.Length; i++)
            {
                // Admission honors the supplied battle limit, including the production limit of three.
                int cellIndex = i / perCell;
                float x = firstX + .25f - cellIndex / rows;
                float y = scenario == AssemblyTrialScenario.Line || scenario == AssemblyTrialScenario.Single ?
                    10.25f + cellIndex + i % perCell * .2f : 2.25f + cellIndex % rows + i % perCell * .2f;
                if (scenario == AssemblyTrialScenario.Fast) x -= (float)random.NextDouble() * .1f;
                result[i] = new PreparedEnemy(new Point2(x, y), fixture.Health, fixture.EnemyKind,
                    rules.EnableEnemyMechanics && rules.Mechanics.EnemyDrawingTiers ? fixture.DrawingTier : DrawingTier.Sketch, false);
            }
            return result;
        }

        static Dictionary<int, Point2> Positions(GameSession session)
        {
            var result = new Dictionary<int, Point2>();
            foreach (var enemy in session.Enemies) result.Add(enemy.Id, enemy.Position);
            return result;
        }
        static float Movement(GameSession session, Dictionary<int, Point2> before)
        {
            double total = 0;
            foreach (var enemy in session.Enemies)
                if (before.TryGetValue(enemy.Id, out var previous))
                { double dx = enemy.Position.X - previous.X, dy = enemy.Position.Y - previous.Y; total += Math.Sqrt(dx * dx + dy * dy); }
            return (float)total;
        }
    }
}
