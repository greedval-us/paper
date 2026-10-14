using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public enum TrialScenario { Crowd = 0, Fast = 1, Armored = 2, Blots = 3 }

    /// <summary>A bounded firing-lane fixture using production targeting, projectiles and effects. No profile or rewards.</summary>
    public static class TrialRunner
    {
        public const int MaximumFrameEvents = 512;
        public static TrialComparison CompareQuality(TowerBlueprint blueprint, GameBalance balance, TrialScenario scenario,
            int budget = 400, int seed = 1337, bool counterfire = false, InkColor color = InkColor.None)
        {
            BalanceValidation.Validate(balance);
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
            BalanceValidation.Validate(balance);
            GameBalance rules = balance.CreateSnapshot();
            return new TrialComparison { First = RunFixture(first, rules, scenario, budget, seed, counterfire, color),
                Second = RunFixture(second, rules, scenario, budget, seed, counterfire, color) };
        }

        /// <summary>Use current weapon, material and effect rules without changing the caller's balance.</summary>
        public static TrialReport Run(TowerBlueprint blueprint, GameBalance balance, TrialScenario scenario,
            int budget = 400, int seed = 1337, bool counterfire = false, InkColor color = InkColor.None)
        {
            BalanceValidation.Validate(balance);
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
            if (budget < 0 || budget > TrialRules.MaximumBudget) throw new ArgumentOutOfRangeException(nameof(budget));
            var mechanics = MechanicsAvailability.Resolve(balance);
            var effects = balance.Effects.ForEnemyDrawingTiers(mechanics.EnemyDrawingTiers);
            if (!mechanics.ColorCartridges) color = InkColor.None;
            var trial = balance.Trials;
            if (balance.Content != null) blueprint = blueprint.WithContent(balance.Content);
            int unitCost = Math.Max(1, TowerCatalog.Cost(blueprint, balance, MaterialTier.Pencil));
            int copies = budget / unitCost;
            var towers = new List<StructureState>(copies);
            for (int i = 0; i < copies; i++)
            {
                float health = TowerCatalog.Health(blueprint, balance, MaterialTier.Pencil);
                // Keep the small-budget battery near the lane and assign every extra copy a unique cell.
                Cell cell = i < trial.CloseTowerCount ? new Cell(trial.CloseTowerX + i % trial.CloseTowerColumns * trial.CloseTowerSpacing, trial.CloseTowerY + i / trial.CloseTowerColumns * trial.CloseTowerSpacing) : new Cell(trial.OverflowTowerX + (i - trial.CloseTowerCount) % trial.OverflowTowerColumns, trial.OverflowTowerY + (i - trial.CloseTowerCount) / trial.OverflowTowerColumns);
                towers.Add(new StructureState { Id = i + 1, Cell = cell,
                    Kind = blueprint.FirstWeapon, Blueprint = blueprint, Color = color, IsComplete = true,
                    Health = health, MaxHealth = health, Tier = MaterialTier.Pencil });
            }
            var report = new TrialReport { BlueprintId = blueprint.Id, Scenario = scenario, Budget = budget,
                Seed = seed, Counterfire = counterfire, Color = color, Copies = copies, Cost = copies * unitCost,
                DrawingQuality = qualityOverride ?? (blueprint.Origin == DrawingOrigin.LegacyStandard ? 100 : blueprint.Assessment.Quality),
                QualityMultiplier = qualityOverride.HasValue ? TowerCatalog.QualityMultiplier(qualityOverride.Value, balance) : TowerCatalog.QualityMultiplier(blueprint, balance) };
            report.Scope = UserText.Message("trial.scope.lane", trial.MaximumTicks * trial.StepSeconds,
                trial.CounterfireDamage, trial.CounterfireInterval, trial.CounterfireRange);
            var random = new Random(seed);
            TrialBlotContext blotContext = scenario == TrialScenario.Blots && balance.Mechanics.CorpseBlots ? new TrialBlotContext(random, trial) : null;
            if (scenario == TrialScenario.Blots && blotContext == null)
                report.Scope = UserText.Message("trial.scope.blots_disabled");
            if (blotContext != null)
                report.Scope = UserText.Message("trial.scope.blots", trial.MaximumTicks * trial.StepSeconds, trial.BlotCount);
            var weapons = new WeaponSystem(balance, qualityOverride, blotContext); var stats = new RunStatistics();
            var events = new SimulationEventBuffer(MaximumFrameEvents); var shots = new List<ShotEvent>();
            if(blotContext!=null)blotContext.Events=events;
            var enemies = new List<EnemyState>(256);
            var frames = new List<TrialFrame>(trial.MaximumTicks / TrialRules.RecordingStrideTicks + 1);
            var frameShots = new List<ShotEvent>();
            var frameImpacts = new List<SimulationEvent>();
            var frameEvents = new List<SimulationEvent>();
            long eventCursor=0,frameDroppedEvents=0;
            frames.Add(new TrialFrame(0, enemies, towers, weapons.Projectiles, frameShots, blotContext?.Marks));
            TrialScenarioDefinition fixture = trial.For(scenario);
            int count = fixture.Count;
            float delta = trial.StepSeconds;
            float spawnClock = 0; int spawned = 0;
            for (int tick = 0; tick < trial.MaximumTicks; tick++)
            {
                report.DurationSeconds = (tick + 1) * delta;
                if(blotContext!=null){blotContext.Tick=tick+1;blotContext.Seconds=report.DurationSeconds;}
                spawnClock -= delta;
                while (spawned < count && spawnClock <= 0)
                {
                    enemies.Add(new EnemyState { Id = ++spawned, Kind = fixture.EnemyKind, VisualScale=balance.Enemy(fixture.EnemyKind).VisualScale,
                        Position = new Point2(trial.SpawnX, trial.SpawnY + (float)random.NextDouble() * trial.SpawnYVariation), Health = fixture.Health, MaxHealth = fixture.Health,
                        DrawingTier = mechanics.EnemyDrawingTiers ? fixture.DrawingTier : DrawingTier.Sketch });
                    spawnClock += fixture.SpawnInterval;
                }
                foreach (StructureState tower in towers)
                    if (!balance.Construction.DynamicFeed)
                    { for(int mount=0;mount<TowerBlueprint.MaximumWeaponMounts;mount++){var state=tower.GetMountState(mount);state.Cooldown=Math.Max(0,state.Cooldown-delta);} }
                foreach (EnemyState enemy in enemies)
                {
                    EnemyEffects.Advance(enemy, delta, stats, effects, mechanics.CorpseBlots ? balance.BlotReduction : 0,events,tick+1,report.DurationSeconds);
                    if (enemy.Health <= 0) continue;
                    enemy.Position = new Point2(enemy.Position.X + fixture.Speed * EnemyEffects.MovementMultiplier(enemy, effects, mechanics.CorpseBlots ? balance.BlotSpeedBonus : 0) * delta, enemy.Position.Y);
                    enemy.AttackCooldown = Math.Max(0, enemy.AttackCooldown - delta);
                    if (counterfire && enemy.AttackCooldown <= 0)
                    {
                        StructureState nearest = null; float nearestSquared = trial.CounterfireRange * trial.CounterfireRange;
                        foreach (StructureState tower in towers)
                        {
                            if (tower.Health <= 0) continue;
                            float dx = tower.Cell.X + .5f - enemy.Position.X, dy = tower.Cell.Y + .5f - enemy.Position.Y;
                            float distance = dx * dx + dy * dy;
                            if (distance < nearestSquared) { nearest = tower; nearestSquared = distance; }
                        }
                        if (nearest != null)
                        {
                            events.Publish(tick+1,SimulationEventKind.EnemyAttack,enemy.Position,new Point2(nearest.Cell.X+.5f,nearest.Cell.Y+.5f),default,
                                entityId:enemy.Id,simulationSeconds:report.DurationSeconds,enemyKind:enemy.Kind,enemyDrawingTier:enemy.DrawingTier,enemyVisualScale:enemy.VisualScale);
                            nearest.Health = Math.Max(0, nearest.Health - DamageRules.StructureDamage(trial.CounterfireDamage, nearest, balance, enemy.Position));
                            if(nearest.Health<=0)events.Publish(tick+1,SimulationEventKind.StructureDestroyed,
                                new Point2(nearest.Cell.X+.5f,nearest.Cell.Y+.5f),new Point2(nearest.Cell.X+.5f,nearest.Cell.Y+.5f),nearest.Kind,
                                entityId:nearest.Id,weaponId:nearest.ContentId,simulationSeconds:report.DurationSeconds);
                            enemy.AttackCooldown = trial.CounterfireInterval;
                        }
                    }
                }
                shots.Clear();
                weapons.Fire(towers, enemies, shots, 1, 1, delta, tick + 1, stats, events, report.DurationSeconds);
                // Visual trails are bounded independently from damage and the live shot list.
                for (int i = 0; i < shots.Count && frameShots.Count < TrialRules.MaximumFrameShots; i++) frameShots.Add(shots[i]);
                int write = 0;
                for (int read = 0; read < enemies.Count; read++)
                {
                    EnemyState enemy = enemies[read];
                    if (enemy.Health <= 0) {EnemyEffects.PublishDeath(enemy,events,tick+1,report.DurationSeconds);continue;}
                    if (enemy.Position.X >= trial.ExitX) { report.Leaks++; continue; }
                    enemies[write++] = enemy;
                }
                if (write < enemies.Count) enemies.RemoveRange(write, enemies.Count - write);
                if(events.Count>0&&events[0].Sequence>eventCursor+1)frameDroppedEvents+=events[0].Sequence-eventCursor-1;
                foreach(var value in events)
                {
                    if(value.Sequence<=eventCursor)continue;
                    if(frameEvents.Count<MaximumFrameEvents)frameEvents.Add(value);else frameDroppedEvents++;
                    if(value.Kind==SimulationEventKind.ProjectileImpact&&frameImpacts.Count<128)frameImpacts.Add(value);
                    eventCursor=value.Sequence;
                }
                bool finished = spawned == count && enemies.Count == 0 && !weapons.HasPendingProjectiles &&
                    (blotContext == null || blotContext.Marks.Count == 0);
                if ((tick + 1) % TrialRules.RecordingStrideTicks == 0 || finished || tick == trial.MaximumTicks - 1)
                {
                    frames.Add(new TrialFrame(report.DurationSeconds, enemies, towers, weapons.Projectiles, frameShots, blotContext?.Marks, frameImpacts,frameEvents,frameDroppedEvents));
                    frameShots.Clear();
                    frameImpacts.Clear();
                    frameEvents.Clear();frameDroppedEvents=0;
                }
                if (finished) break;
            }
            // A duration cap is explicit: surviving lane enemies count as unresolved, not kills.
            report.UnresolvedEnemies = count - spawned + enemies.Count;
            report.BlotsCleared = stats.BlotsCleared;
            report.UnresolvedBlots = blotContext?.Marks.Count ?? 0;
            foreach (StructureState tower in towers) if (tower.Health > 0) report.SurvivingTowers++;
            report.Damage = stats.TotalDamage; report.IdleSeconds = stats.TotalIdleSeconds;
            report.Frames = frames.AsReadOnly();
            return report;
        }

        sealed class TrialBlotContext : IBlotRangeContext
        {
            readonly List<BlotState> marks = new List<BlotState>();
            internal IReadOnlyList<BlotState> Marks => marks;
            internal SimulationEventBuffer Events;
            internal long Tick;
            internal double Seconds;

            internal TrialBlotContext(Random random, TrialRules trial)
            {
                if (trial.BlotCount == 0) return;
                Cell nearFirstTower = new Cell(trial.FirstBlotX, trial.FirstBlotY);
                marks.Add(new BlotState { Cell = nearFirstTower, Remaining = trial.BlotLifetime });
                var candidates = new List<Cell>(280);
                for (int y = trial.BlotMinY; y < trial.BlotMaxY; y++)
                    for (int x = trial.BlotMinX; x < trial.BlotMaxX; x++)
                    {
                        Cell cell = new Cell(x, y);
                        if (cell != nearFirstTower) candidates.Add(cell);
                    }
                // Sampling without replacement gives a bounded, identical page for every battery size.
                for (int i = 1; i < trial.BlotCount; i++)
                {
                    int chosen = random.Next(candidates.Count);
                    marks.Add(new BlotState { Cell = candidates[chosen], Remaining = trial.BlotLifetime });
                    candidates[chosen] = candidates[candidates.Count - 1];
                    candidates.RemoveAt(candidates.Count - 1);
                }
            }

            public bool TryFindNearest(Point2 origin, float range, out Cell cell)
                => BlotQueries.TryFindNearest(marks, origin, range, out cell);
            public bool TryFindNearest(Point2 origin,float minimumRange,float maximumRange,out Cell cell)
                =>BlotQueries.TryFindNearest(marks,origin,minimumRange,maximumRange,out cell);

            public int Clear(Cell center, float radius)
            {
                int count = BlotQueries.Clear(marks, center, radius,OnCleared);
                int write = 0;
                for (int read = 0; read < marks.Count; read++)
                    if (marks[read].Remaining > 0) marks[write++] = marks[read];
                if (write < marks.Count) marks.RemoveRange(write, marks.Count - write);
                return count;
            }

            void OnCleared(Cell cell)=>Events?.Publish(Tick,SimulationEventKind.BlotCleared,
                new Point2(cell.X+.5f,cell.Y+.5f),new Point2(cell.X+.5f,cell.Y+.5f),default,simulationSeconds:Seconds);
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
        public string Scope;
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
        public IReadOnlyList<SimulationEvent> Impacts { get; }
        /// <summary>Events since the previous snapshot, each retaining its own simulation timestamp.</summary>
        public IReadOnlyList<SimulationEvent> Events { get; }
        public long DroppedEventCount { get; }

        internal TrialFrame(float seconds, IReadOnlyList<EnemyState> enemies, IReadOnlyList<StructureState> towers,
            IReadOnlyList<MortarProjectile> projectiles, IReadOnlyList<ShotEvent> shots, IReadOnlyList<BlotState> blots = null, IReadOnlyList<SimulationEvent> impacts = null,
            IReadOnlyList<SimulationEvent> events=null,long droppedEventCount=0)
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
            var impactValues = new SimulationEvent[impacts?.Count ?? 0];
            for (int i = 0; i < impactValues.Length; i++) impactValues[i] = impacts[i];
            Impacts = Array.AsReadOnly(impactValues);
            var eventValues=new SimulationEvent[events?.Count??0];
            for(int i=0;i<eventValues.Length;i++)eventValues[i]=events[i];
            Events=Array.AsReadOnly(eventValues);DroppedEventCount=droppedEventCount;
        }
    }
}
