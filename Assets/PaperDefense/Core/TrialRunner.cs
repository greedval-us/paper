using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public enum TrialScenario { Crowd, Fast, Armored, Blots }

    /// <summary>A bounded firing-lane fixture using production targeting, projectiles and effects. No profile or rewards.</summary>
    public static class TrialRunner
    {
        public static TrialComparison CompareQuality(TowerBlueprint blueprint, GameBalance balance, TrialScenario scenario,
            int budget = 400, int seed = 1337, bool counterfire = false, InkColor color = InkColor.None)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            var rules = balance.CreateSnapshot();
            return new TrialComparison
            {
                First = RunFixture(blueprint, rules, scenario, budget, seed, counterfire, color),
                Second = RunFixture(blueprint, rules, scenario, budget, seed, counterfire, color, 100)
            };
        }
        public static TrialComparison Compare(TowerBlueprint first, TowerBlueprint second, TrialScenario scenario,
            int budget = 400, int seed = 1337, bool counterfire = false, InkColor color = InkColor.None)
            => Compare(first, second, StandardFixtureBalance(), scenario, budget, seed, counterfire, color);

        public static TrialReport Run(TowerBlueprint blueprint, TrialScenario scenario,
            int budget = 400, int seed = 1337, bool counterfire = false, InkColor color = InkColor.None)
            => Run(blueprint, StandardFixtureBalance(), scenario, budget, seed, counterfire, color);

        /// <summary>Compare both lanes against one owned snapshot of the current battle tuning.</summary>
        public static TrialComparison Compare(TowerBlueprint first, TowerBlueprint second, GameBalance balance,
            TrialScenario scenario, int budget = 400, int seed = 1337, bool counterfire = false, InkColor color = InkColor.None)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            GameBalance rules = balance.CreateSnapshot();
            return new TrialComparison { First = RunFixture(first, rules, scenario, budget, seed, counterfire, color),
                Second = RunFixture(second, rules, scenario, budget, seed, counterfire, color) };
        }

        /// <summary>Use current weapon, material and effect rules without changing the caller's balance.</summary>
        public static TrialReport Run(TowerBlueprint blueprint, GameBalance balance, TrialScenario scenario,
            int budget = 400, int seed = 1337, bool counterfire = false, InkColor color = InkColor.None)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            return RunFixture(blueprint, balance.CreateSnapshot(), scenario, budget, seed, counterfire, color);
        }

        // Historical overloads are explicitly the standard fixture, including its original tier effects.
        static GameBalance StandardFixtureBalance()
        {
            var balance = GameBalance.CreateDefault();
            balance.EnableEnemyMechanics = true;
            return balance;
        }

        static TrialReport RunFixture(TowerBlueprint blueprint, GameBalance balance, TrialScenario scenario,
            int budget, int seed, bool counterfire, InkColor color, float? qualityOverride = null)
        {
            if (!TowerCatalog.Validate(blueprint, balance, out string reason)) throw new ArgumentException(reason, nameof(blueprint));
            if (!Enum.IsDefined(typeof(TrialScenario), scenario)) throw new ArgumentOutOfRangeException(nameof(scenario));
            if (!Enum.IsDefined(typeof(InkColor), color)) throw new ArgumentOutOfRangeException(nameof(color));
            if (budget < 0 || budget > 20000) throw new ArgumentOutOfRangeException(nameof(budget));
            int unitCost = Math.Max(1, TowerCatalog.Cost(blueprint, balance, MaterialTier.Pencil));
            int copies = budget / unitCost;
            var towers = new List<StructureState>(copies);
            for (int i = 0; i < copies; i++)
            {
                float health = TowerCatalog.Health(blueprint, balance, MaterialTier.Pencil);
                // Keep the small-budget battery near the lane and assign every extra copy a unique cell.
                Cell cell = i < 48 ? new Cell(32 + i % 4 * 2, 10 + i / 4 * 2) : new Cell(1 + (i - 48) % 60, 1 + (i - 48) / 60);
                towers.Add(new StructureState { Id = i + 1, Cell = cell,
                    Kind = blueprint.FirstWeapon, Blueprint = blueprint, Color = color, IsComplete = true,
                    Health = health, MaxHealth = health, Tier = MaterialTier.Pencil });
            }
            var report = new TrialReport { BlueprintId = blueprint.Id, Scenario = scenario, Budget = budget,
                Seed = seed, Counterfire = counterfire, Color = color, Copies = copies, Cost = copies * unitCost,
                DrawingQuality = qualityOverride ?? (blueprint.Origin == DrawingOrigin.LegacyStandard ? 100 : blueprint.Assessment.Quality),
                QualityMultiplier = qualityOverride.HasValue ? TowerCatalog.QualityMultiplier(qualityOverride.Value, balance) : TowerCatalog.QualityMultiplier(blueprint, balance) };
            var random = new Random(seed);
            TrialBlotContext blotContext = scenario == TrialScenario.Blots ? new TrialBlotContext(random) : null;
            if (blotContext != null)
                report.Scope = "60-second finite seeded blot fixture; complete pencil towers at fixed grid positions. " +
                    "48 fixed marks persist until cleaned and never respawn. No enemies, counterfire, construction delay, walls, gold or permanent rewards.";
            var weapons = new WeaponSystem(balance, qualityOverride, blotContext); var stats = new RunStatistics();
            var events = new SimulationEventBuffer(32); var shots = new List<ShotEvent>();
            var enemies = new List<EnemyState>(256);
            var frames = new List<TrialFrame>(181);
            var frameShots = new List<ShotEvent>();
            frames.Add(new TrialFrame(0, enemies, towers, weapons.Projectiles, frameShots, blotContext?.Marks));
            int count = scenario == TrialScenario.Blots ? 0 : scenario == TrialScenario.Crowd ? 240 : scenario == TrialScenario.Fast ? 160 : 80;
            float interval = scenario == TrialScenario.Crowd ? .1f : scenario == TrialScenario.Fast ? .15f : .3f;
            float speed = scenario == TrialScenario.Fast ? 5 : scenario == TrialScenario.Armored ? 1.5f : 2.3f;
            float healthPerEnemy = scenario == TrialScenario.Armored ? 140 : scenario == TrialScenario.Fast ? 14 : 22;
            const float delta = 1f / 30;
            float spawnClock = 0; int spawned = 0;
            for (int tick = 0; tick < 1800; tick++)
            {
                report.DurationSeconds = (tick + 1) * delta;
                spawnClock -= delta;
                while (spawned < count && spawnClock <= 0)
                {
                    enemies.Add(new EnemyState { Id = ++spawned, Kind = scenario == TrialScenario.Fast ? EnemyKind.Runner :
                        scenario == TrialScenario.Armored ? EnemyKind.Gnawer : EnemyKind.Doodle,
                        Position = new Point2(10, 10.5f + (float)random.NextDouble() * 8), Health = healthPerEnemy, MaxHealth = healthPerEnemy,
                        DrawingTier = scenario == TrialScenario.Armored ? DrawingTier.Hatched : DrawingTier.Sketch });
                    spawnClock += interval;
                }
                foreach (StructureState tower in towers)
                    if (!balance.Construction.DynamicFeed)
                    { tower.Cooldown = Math.Max(0, tower.Cooldown - delta); tower.SecondaryCooldown = Math.Max(0, tower.SecondaryCooldown - delta); }
                foreach (EnemyState enemy in enemies)
                {
                    EnemyEffects.Advance(enemy, delta, stats, balance.Effects, balance.BlotReduction);
                    if (enemy.Health <= 0) continue;
                    enemy.Position = new Point2(enemy.Position.X + speed * EnemyEffects.MovementMultiplier(enemy, balance.Effects, balance.BlotSpeedBonus) * delta, enemy.Position.Y);
                    enemy.AttackCooldown = Math.Max(0, enemy.AttackCooldown - delta);
                    if (counterfire && enemy.AttackCooldown <= 0)
                    {
                        StructureState nearest = null; float nearestSquared = 12.5f * 12.5f;
                        foreach (StructureState tower in towers)
                        {
                            if (tower.Health <= 0) continue;
                            float dx = tower.Cell.X + .5f - enemy.Position.X, dy = tower.Cell.Y + .5f - enemy.Position.Y;
                            float distance = dx * dx + dy * dy;
                            if (distance < nearestSquared) { nearest = tower; nearestSquared = distance; }
                        }
                        if (nearest != null)
                        {
                            nearest.Health = Math.Max(0, nearest.Health - DamageRules.StructureDamage(10, nearest, balance));
                            enemy.AttackCooldown = 2.4f;
                        }
                    }
                }
                shots.Clear();
                weapons.Fire(towers, enemies, shots, 1, 1, delta, tick + 1, stats, events, report.DurationSeconds);
                // Visual trails are bounded independently from damage and the live shot list.
                for (int i = 0; i < shots.Count && frameShots.Count < 1024; i++) frameShots.Add(shots[i]);
                int write = 0;
                for (int read = 0; read < enemies.Count; read++)
                {
                    EnemyState enemy = enemies[read];
                    if (enemy.Health <= 0) continue;
                    if (enemy.Position.X >= 48) { report.Leaks++; continue; }
                    enemies[write++] = enemy;
                }
                if (write < enemies.Count) enemies.RemoveRange(write, enemies.Count - write);
                bool finished = spawned == count && enemies.Count == 0 && !weapons.HasPendingProjectiles &&
                    (blotContext == null || blotContext.Marks.Count == 0);
                if ((tick + 1) % 10 == 0 || finished || tick == 1799)
                {
                    frames.Add(new TrialFrame(report.DurationSeconds, enemies, towers, weapons.Projectiles, frameShots, blotContext?.Marks));
                    frameShots.Clear();
                }
                if (finished) break;
            }
            // A duration cap is explicit: surviving lane enemies count as unresolved, not kills.
            report.UnresolvedEnemies = count - spawned + enemies.Count;
            report.BlotsCleared = stats.BlotsCleared;
            report.UnresolvedBlots = blotContext?.Marks.Count ?? 0;
            foreach (StructureState tower in towers) if (tower.Health > 0) report.SurvivingTowers++;
            foreach (StructureKind kind in Enum.GetValues(typeof(StructureKind)))
            { report.Damage += stats.Damage(kind); report.IdleSeconds += stats.IdleSeconds(kind); }
            report.Frames = frames.AsReadOnly();
            return report;
        }

        sealed class TrialBlotContext : IBlotContext
        {
            const int MarkCount = 48;
            readonly List<BlotState> marks = new List<BlotState>(MarkCount);
            internal IReadOnlyList<BlotState> Marks => marks;

            internal TrialBlotContext(Random random)
            {
                Cell nearFirstTower = new Cell(33, 10);
                marks.Add(new BlotState { Cell = nearFirstTower, Remaining = 60 });
                var candidates = new List<Cell>(280);
                for (int y = 8; y < 28; y++)
                    for (int x = 29; x < 43; x++)
                    {
                        Cell cell = new Cell(x, y);
                        if (cell != nearFirstTower) candidates.Add(cell);
                    }
                // Sampling without replacement gives a bounded, identical page for every battery size.
                for (int i = 1; i < MarkCount; i++)
                {
                    int chosen = random.Next(candidates.Count);
                    marks.Add(new BlotState { Cell = candidates[chosen], Remaining = 60 });
                    candidates[chosen] = candidates[candidates.Count - 1];
                    candidates.RemoveAt(candidates.Count - 1);
                }
            }

            public bool TryFindNearest(Point2 origin, float range, out Cell cell)
                => BlotQueries.TryFindNearest(marks, origin, range, out cell);

            public int Clear(Cell center, float radius)
            {
                int count = BlotQueries.Clear(marks, center, radius);
                int write = 0;
                for (int read = 0; read < marks.Count; read++)
                    if (marks[read].Remaining > 0) marks[write++] = marks[read];
                if (write < marks.Count) marks.RemoveRange(write, marks.Count - write);
                return count;
            }
        }
    }

    [Serializable] public sealed class TrialComparison
    {
        public TrialReport First, Second;
    }
    [Serializable] public sealed class TrialReport
    {
        public float DrawingQuality { get; internal set; }
        public float QualityMultiplier { get; internal set; } = 1;
        public string BlueprintId;
        public string Scope = "60-second headless lane; complete pencil towers at fixed grid positions; identical seeded spawn stream. Counterfire is a direct 10-damage shot every 2.4 seconds within 12.5 cells. No construction delay, walls, blots, gold or permanent rewards.";
        public TrialScenario Scenario;
        public InkColor Color;
        public int Budget, Seed, Cost, Copies, Leaks, SurvivingTowers, UnresolvedEnemies;
        public int BlotsCleared, UnresolvedBlots;
        public bool Counterfire;
        public float Damage, IdleSeconds, DurationSeconds;
        public IReadOnlyList<TrialFrame> Frames { get; internal set; } = Array.AsReadOnly(Array.Empty<TrialFrame>());
    }

    /// <summary>Owned value snapshots for bounded training playback, independent of later simulation steps.</summary>
    public sealed class TrialFrame
    {
        public float Seconds { get; }
        public IReadOnlyList<EnemyView> Enemies { get; }
        public IReadOnlyList<StructureView> Towers { get; }
        public IReadOnlyList<ProjectileView> Projectiles { get; }
        public IReadOnlyList<ShotEvent> Shots { get; }
        public IReadOnlyList<BlotView> Blots { get; }

        internal TrialFrame(float seconds, IReadOnlyList<EnemyState> enemies, IReadOnlyList<StructureState> towers,
            IReadOnlyList<MortarProjectile> projectiles, IReadOnlyList<ShotEvent> shots, IReadOnlyList<BlotState> blots = null)
        {
            Seconds = seconds;
            var enemyValues = new EnemyView[enemies.Count];
            for (int i = 0; i < enemyValues.Length; i++) enemyValues[i] = new EnemyView(enemies[i]);
            var towerValues = new StructureView[towers.Count];
            for (int i = 0; i < towerValues.Length; i++) towerValues[i] = new StructureView(towers[i]);
            var projectileValues = new ProjectileView[projectiles.Count];
            for (int i = 0; i < projectileValues.Length; i++) projectileValues[i] = new ProjectileView(projectiles[i]);
            var shotValues = new ShotEvent[shots.Count];
            for (int i = 0; i < shotValues.Length; i++) shotValues[i] = shots[i];
            var blotValues = new BlotView[blots?.Count ?? 0];
            for (int i = 0; i < blotValues.Length; i++) blotValues[i] = new BlotView(blots[i]);
            Enemies = Array.AsReadOnly(enemyValues);
            Towers = Array.AsReadOnly(towerValues);
            Projectiles = Array.AsReadOnly(projectileValues);
            Shots = Array.AsReadOnly(shotValues);
            Blots = Array.AsReadOnly(blotValues);
        }
    }
}
