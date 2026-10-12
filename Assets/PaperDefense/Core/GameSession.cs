using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public sealed partial class GameSession
    {
        private readonly GameBalance rules;
        internal GameBalance OwnedBalance => rules;
        public GameBalance Balance => rules.CreateSnapshot();
        public MechanicsAvailability Mechanics { get; }
        public IReadOnlyList<StructureState> Structures => structures;
        public IReadOnlyList<EnemyState> Enemies => enemies;
        public IReadOnlyList<ShotEvent> Shots => shots;
        public IReadOnlyList<MortarProjectile> Projectiles => weapons.Projectiles;
        public RunStatistics Statistics { get; } = new RunStatistics();
        public SimulationEventBuffer EventBuffer { get; } = new SimulationEventBuffer();
        public long TickIndex { get; private set; }
        public int TopologyVersion { get; private set; }
        public int StructureVisualVersion { get; private set; }
        public int FlowRebuildCount { get; private set; }
        public Cell BaseCell { get; }
        public int Ink { get; private set; }
        public float BaseHealth { get; private set; }
        public int Kills { get; private set; }
        public int WaveIndex { get; private set; }
        public int RemainingToSpawn { get; private set; }
        public RunPhase Phase { get; private set; }
        public float Countdown { get; private set; }
        public float Elapsed { get; private set; }
        public bool PendingUpgrade { get; private set; }
        private readonly List<StructureState> structures = new List<StructureState>();
        private readonly List<EnemyState> enemies = new List<EnemyState>();
        private readonly List<ShotEvent> shots = new List<ShotEvent>();
        private readonly StructureState[] grid;
        private readonly WeightedFlowField flow;
        private readonly WeaponSystem weapons;
        private readonly EffectsRules enemyEffects;
        private readonly EnemyCrowdGrid crowd;
        private readonly int[] navigationBands;
        private readonly EnemyState[] frontline;
        private readonly float[] frontlineDistances;
        private readonly bool[] reservedFrontline;
        private readonly Random random;
        private readonly bool legacyEnemyHealthGrowth;
        private int nextStructureId = 1, nextEnemyId = 1;
        private float spawnClock;
        private double supplyRemainder;
        private float fireMultiplier = 1, damageMultiplier = 1, healthMultiplier = 1, salvageBonus;
        private float supplyMultiplier = 1;
        private float waveRewardMultiplier = 1;
        private bool flowDirty = true;

        public GameSession(GameBalance balance, int seed = 1337, bool starterDefenses = true)
        {
            BalanceValidation.Validate(balance);
            balance=balance.CreateSnapshot();
            balance.Content=balance.ResolveContent();
            rules = balance;
            Mechanics = MechanicsAvailability.Resolve(balance);
            legacyEnemyHealthGrowth = !balance.EnableEnemyMechanics;
            enemyEffects = balance.Effects.ForEnemyDrawingTiers(Mechanics.EnemyDrawingTiers);
            BaseCell = new Cell(balance.Width / 2, balance.Height / 2);
            BaseHealth = Math.Max(1, balance.BaseHealth);
            Ink = Math.Max(0, balance.StartingInk);
            Countdown = Math.Max(0, balance.PlanningSeconds);
            Phase = RunPhase.Planning;
            grid = new StructureState[balance.Width * balance.Height];
            flow = new WeightedFlowField(balance.Width, balance.Height);
            crowd = new EnemyCrowdGrid(balance.Width, balance.Height, balance.MaxEnemiesPerCell);
            navigationBands = new int[grid.Length];
            frontline = new EnemyState[grid.Length * 3];
            frontlineDistances = new float[grid.Length * 3];
            reservedFrontline = new bool[grid.Length * 3];
            weapons = new WeaponSystem(balance, null, CreateBlotContext());
            random = new Random(seed);
            InitializeObjectives();
            if (starterDefenses) AddStarterDefenses();
        }

        /// <summary>Quotes a deduplicated stroke. A single invalid cell rejects the entire order.</summary>
        public BuildQuote QuoteBuild(IReadOnlyList<Cell> cells, StructureKind kind, MaterialTier tier)
            => QuoteBuild(cells, kind, tier, null);

        // Placement stays shared; an assembly supplies its complete price before affordability is checked.
        private BuildQuote QuoteBuild(IReadOnlyList<Cell> cells, StructureKind kind, MaterialTier tier, int? unitPrice)
        {
            var unique = new List<Cell>();
            var quote = new BuildQuote { Cells = unique, Reason = "", Valid = false };
            if ((!unitPrice.HasValue && !Enum.IsDefined(typeof(StructureKind), kind)) || !Enum.IsDefined(typeof(MaterialTier), tier))
            { quote.Reason = UserText.Message("copy.cef60cd86a8db8c7"); return quote; }
            if (Phase == RunPhase.Victory || Phase == RunPhase.Defeat)
            { quote.Reason = UserText.Message("copy.850e91bbbd5c94d8"); return quote; }
            if (cells == null || cells.Count == 0) { quote.Reason = UserText.Message("copy.2bda845eb8de3b76"); return quote; }
            var seen = new HashSet<Cell>();
            string invalid = "";
            int newSegments = 0;
            foreach (var cell in cells)
            {
                if (!seen.Add(cell)) continue;
                unique.Add(cell);
                if (invalid.Length != 0) continue;
                if (cell.X <= 0 || cell.Y <= 0 || cell.X >= rules.Width - 1 || cell.Y >= rules.Height - 1)
                    invalid = UserText.Message("copy.4938873b1b576f02");
                else if (Math.Abs(cell.X - BaseCell.X) <= 1 && Math.Abs(cell.Y - BaseCell.Y) <= 1)
                    invalid = UserText.Message("copy.de6ff12a6096d83f");
                else if (ReservedObjective(cell)) invalid = UserText.Message("copy.3d5bb5922e409c4c");
                else if (StructureAt(cell) != null)
                {
                    var existing = StructureAt(cell);
                    if ((kind != StructureKind.Wall && kind != StructureKind.Spikes) || existing.Kind != kind || existing.Tier != tier)
                        invalid = UserText.Message("copy.d85abdff9eafbb67");
                    continue;
                }
                newSegments++;
            }
            // Retain only stroke cells. Previewing never copies a large horde into a temporary set.
            if (invalid.Length == 0 && newSegments > 0)
                foreach (var enemy in enemies)
                    if (enemy.Health > 0 && seen.Contains(CellOf(enemy.Position)) && StructureAt(CellOf(enemy.Position)) == null)
                    { invalid = UserText.Message("copy.20f4659591475b68"); break; }
            long cost = (long)(unitPrice ?? BuildCost(kind, tier)) * newSegments;
            quote.Cost = (int)Math.Min(int.MaxValue, cost);
            if (invalid.Length != 0) { quote.Reason = invalid; return quote; }
            if (cost > Ink) { quote.Reason = UserText.Message("copy.d952db5ec0c442f9", cost); return quote; }
            quote.Valid = true;
            return quote;
        }

        public bool TryBuild(IReadOnlyList<Cell> cells, StructureKind kind, MaterialTier tier, out string reason)
        {
            var quote = QuoteBuild(cells, kind, tier);
            reason = quote.Reason;
            if (!quote.Valid) return false;
            Ink -= quote.Cost;
            int unitCost = BuildCost(kind, tier);
            int built = 0;
            foreach (var cell in quote.Cells)
                if (StructureAt(cell) == null) { AddStructure(cell, kind, tier, unitCost, false); built++; }
            Statistics.RecordBuild(quote.Cost, built, kind, tier);
            return true;
        }

        public bool TryErase(Cell cell, out int refund)
        {
            refund = 0;
            if (Phase == RunPhase.Victory || Phase == RunPhase.Defeat) return false;
            var structure = StructureAt(cell);
            if (structure == null) return false;
            float fraction = Clamp(structure.Health / Math.Max(1, structure.MaxHealth), 0, 1);
            float rate = Clamp(rules.Material(structure.Tier).RefundFraction + salvageBonus, 0, rules.Progression.MaximumRefundFraction);
            refund = (int)Math.Floor(structure.PaidInk * fraction * rate + 0.00001f);
            CreditInk(refund);
            Statistics.RecordRefund(refund);
            RemoveStructure(structure);
            return true;
        }

        public bool TryUpgrade(Cell cell, out string reason)
        {
            reason = "";
            if (Phase == RunPhase.Victory || Phase == RunPhase.Defeat) { reason = UserText.Message("copy.850e91bbbd5c94d8"); return false; }
            var structure = StructureAt(cell);
            if (structure == null) { reason = UserText.Message("copy.e60f6b7240c54994"); return false; }
            if (structure.Tier == MaterialTier.Marker) { reason = UserText.Message("copy.f7a6e14c3b97d071"); return false; }
            if (!structure.IsComplete) { reason = UserText.Message("copy.0fbbc427bf3169b9"); return false; }
            var nextTier = (MaterialTier)((int)structure.Tier + 1);
            int difference = Math.Max(0, StructureBuildCost(structure, nextTier) - StructureBuildCost(structure, structure.Tier));
            if (difference > Ink) { reason = UserText.Message("copy.d952db5ec0c442f9", difference); return false; }
            Ink -= difference;
            structure.PaidInk += difference;
            Statistics.RecordUpgrade(difference);
            float fraction = Clamp(structure.Health / Math.Max(1, structure.MaxHealth), 0, 1);
            structure.Tier = nextTier;
            structure.MaxHealth = StructureHealth(structure, nextTier);
            structure.Health = structure.MaxHealth * fraction;
            structure.BuildRemaining = Math.Max(0, StructureBuildSeconds(structure, nextTier));
            structure.IsComplete = structure.BuildRemaining <= 0;
            structure.WaitingForClearance = false;
            MarkStructureChanged(structure, true);
            return true;
        }

        public bool SetPriority(Cell cell, TargetPriority priority)
        {
            var structure = StructureAt(cell);
            if (structure == null || !Enum.IsDefined(typeof(TargetPriority), priority)) return false;
            structure.Priority = priority;
            StructureVisualVersion++;
            return true;
        }

        public StructureState StructureAt(Cell cell) => Inside(cell) ? grid[cell.Y * rules.Width + cell.X] : null;

        public void StartNextWave()
        {
            if ((Phase != RunPhase.Planning && Phase != RunPhase.Intermission) || PendingUpgrade) return;
            if (WaveIndex >= rules.Waves.Count) { Phase = RunPhase.Victory; return; }
            CreditInk(Math.Min(Math.Max(0, rules.MaxEarlyLaunchInk), (int)Math.Floor(Math.Max(0, Countdown) * Math.Max(0, rules.EarlyLaunchInkPerSecond))));
            WaveIndex++;
            var wave = rules.Waves[WaveIndex - 1];
            RemainingToSpawn = Math.Max(0, wave.Count);
            Countdown = 0;
            spawnClock = 0;
            Phase = RunPhase.Assault;
        }

        /// <summary>Zero delta is a real pause. Large deltas are subdivided to prevent tunnelling.</summary>
        public void Tick(float delta)
        {
            shots.Clear();
            if (delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            if (Phase == RunPhase.Victory || Phase == RunPhase.Defeat) return;
            double remaining = delta;
            float maximumStep = Clamp(rules.SimulationStep, 0.005f, 0.05f);
            while (remaining > 0.0000001 && Phase != RunPhase.Victory && Phase != RunPhase.Defeat)
            {
                float step = (float)Math.Min(remaining, maximumStep);
                Step(step);
                remaining -= step;
            }
        }

        private void Step(float delta)
        {
            TickIndex++;
            Elapsed += delta;
            // Status lifetimes are shared by independent sources, including the stapler.
            TickEnemyStatuses(delta);
            TickCorpseBlots(delta);
            AdvanceEnemyGlobs(delta);
            crowd.Rebuild(enemies);
            supplyRemainder += Math.Max(0, rules.PassiveInkPerSecond) * supplyMultiplier * delta;
            int supplied = (int)Math.Floor(supplyRemainder + 0.000001);
            CreditInk(supplied);
            supplyRemainder -= supplied;
            foreach (var structure in structures)
            {
                if (!structure.IsComplete)
                {
                    structure.BuildRemaining = Math.Max(0, structure.BuildRemaining - delta);
                    if (structure.BuildRemaining <= 0)
                    {
                        bool waiting = structure.Kind != StructureKind.Spikes && crowd.Count(structure.Cell) > 0;
                        if (structure.WaitingForClearance != waiting) StructureVisualVersion++;
                        structure.WaitingForClearance = waiting;
                        if (!waiting)
                        {
                            structure.IsComplete = true;
                            MarkStructureChanged(structure, true);
                        }
                    }
                }
                if (!rules.Construction.DynamicFeed)
                {
                    for(int mount=0;mount<TowerBlueprint.MaximumWeaponMounts;mount++)
                    {var state=structure.GetMountState(mount);state.Cooldown=Math.Max(0,state.Cooldown-delta);}
                }
                structure.FrontageUsed = 0;
                RefreshHealthBand(structure);
            }
            if (Phase == RunPhase.Planning || Phase == RunPhase.Intermission)
            {
                Countdown = Math.Max(0, Countdown - delta);
                if (Countdown <= 0 && !PendingUpgrade) StartNextWave();
            }
            if (Phase != RunPhase.Assault)
            {
                weapons.ReloadWithoutTargets(structures, fireMultiplier, delta);
                return;
            }
            SpawnEnemies(delta);
            if (flowDirty)
            {
                var profile = rules.Enemy(EnemyKind.Doodle);
                long rebuildStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                flow.Rebuild(BaseCell, grid, profile.Speed, Math.Max(rules.Combat.MinimumPathDamage, profile.Damage) / Math.Max(rules.Combat.MinimumActionInterval, profile.AttackInterval));
                RebuildWellFlows(profile);
                Statistics.RecordNavigation((System.Diagnostics.Stopwatch.GetTimestamp() - rebuildStarted) *
                    1000.0 / System.Diagnostics.Stopwatch.Frequency);
                flowDirty = false;
                FlowRebuildCount++;
                foreach (var enemy in enemies) enemy.HasDestination = false;
            }
            MoveEnemies(delta);
            if (BaseHealth <= 0) { BaseHealth = 0; Phase = RunPhase.Defeat; return; }
            weapons.Fire(structures, enemies, shots, fireMultiplier, damageMultiplier, delta, TickIndex, Statistics, EventBuffer, Elapsed);
            RemoveDeadEnemies();
            if (RemainingToSpawn == 0 && enemies.Count == 0 && !weapons.HasPendingProjectiles && EnemyProjectiles.Count == 0) CompleteWave();
        }

        private void SpawnEnemies(float delta)
        {
            var wave = rules.Waves[WaveIndex - 1];
            spawnClock -= delta;
            float interval = Math.Max(rules.Combat.MinimumSpawnInterval, wave.SpawnInterval);
            while (RemainingToSpawn > 0 && spawnClock <= 0)
            {
                double roll = random.NextDouble();
                float gnawers = Clamp(wave.GnawerFraction, 0, 1);
                float runners = Clamp(wave.RunnerFraction, 0, 1 - gnawers);
                float spitters = Mechanics.RangedEnemies ? Clamp(wave.SpitterFraction, 0, 1 - gnawers - runners) : 0;
                EnemyKind kind = roll < gnawers ? EnemyKind.Gnawer : roll < gnawers + runners ? EnemyKind.Runner :
                    roll < gnawers + runners + spitters ? EnemyKind.Spitter : EnemyKind.Doodle;
                var definition = rules.Enemy(kind);
                int sideCount = 0;
                for (int bit = 1; bit <= 8; bit <<= 1) if ((wave.Sides & bit) != 0) sideCount++;
                int sideIndex = random.Next(Math.Max(1, sideCount));
                int side = 1;
                if (sideCount > 0)
                {
                    for (int bit = 1; bit <= 8; bit <<= 1)
                        if ((wave.Sides & bit) != 0 && sideIndex-- == 0) { side = bit; break; }
                }
                int offset = side <= 2 ? random.Next(2, rules.Height - 2) : random.Next(2, rules.Width - 2);
                int edgeLength = side <= 2 ? rules.Height - 4 : rules.Width - 4;
                Cell spawnCell = default;
                bool admitted = false;
                for (int attempt = 0; attempt < edgeLength; attempt++)
                {
                    int edgeOffset = 2 + (offset - 2 + attempt) % edgeLength;
                    spawnCell = side == 1 ? new Cell(0, edgeOffset) : side == 2 ? new Cell(rules.Width - 1, edgeOffset) :
                        side == 4 ? new Cell(edgeOffset, rules.Height - 1) : new Cell(edgeOffset, 0);
                    if (crowd.CanEnter(spawnCell)) { admitted = true; break; }
                }
                if (!admitted) { spawnClock = 0; return; }
                spawnClock += interval;
                RemainingToSpawn--;
                Point2 position = crowd.Add(spawnCell);
                DrawingTier drawing = DrawingTier.Sketch;
                if (Mechanics.EnemyDrawingTiers)
                {
                    double tierRoll = random.NextDouble();
                    drawing = tierRoll < Clamp(wave.HatchedFraction,0,1) ? DrawingTier.Hatched :
                        tierRoll < Clamp(wave.HatchedFraction + wave.OutlinedFraction,0,1) ? DrawingTier.Outlined : DrawingTier.Sketch;
                }
                float lateScale = legacyEnemyHealthGrowth ?
                    1 + Math.Max(0, WaveIndex - rules.Progression.LegacyHealthGrowthStartsAfterWave) * rules.Progression.LegacyHealthGrowthPerWave :
                    EnemyEffects.DrawingHealthMultiplier(drawing, rules.Effects);
                enemies.Add(new EnemyState
                {
                    Id = nextEnemyId++, Kind = kind, Position = position,
                    Health = Math.Max(1, definition.MaxHealth * lateScale), MaxHealth = Math.Max(1, definition.MaxHealth * lateScale),
                    SpeedScale = rules.Progression.EnemySpeedScaleMinimum +
                        (float)random.NextDouble() * rules.Progression.EnemySpeedScaleVariation, AttackCooldown = 0,
                    DrawingTier = drawing, ObjectiveIndex = ChooseObjective(), RewardBudget = Math.Max(0, definition.Reward)
                });
                Statistics.ObserveEnemies(enemies.Count);
            }
        }

        private void MoveEnemies(float delta)
        {
            Point2 basePoint = Center(BaseCell);
            // Destinations are refreshed before validating persistent frontage ownership.
            foreach (var enemy in enemies)
            {
                if (enemy.Health <= 0) continue;
                if (enemy.HasDestination && DistanceSquared(enemy.Position, enemy.Destination) > rules.Combat.DestinationRepathDistanceSquared)
                    enemy.HasDestination = false;
                if (!enemy.HasDestination)
                {
                    enemy.Destination = Center(EnemyFlow(enemy).Next(CellOf(enemy.Position)));
                    enemy.HasDestination = true;
                }
            }
            RetainFrontlineOwners();
            // Fill vacancies with the nearest contenders. A closer newcomer cannot rotate
            // through a living owner's position while that owner is cooling down.
            foreach (var enemy in enemies)
            {
                if (enemy.Health <= 0) continue;
                var obstacle = StructureAt(CellOf(enemy.Destination));
                float distance = obstacle == null ? float.MaxValue : DistanceSquared(enemy.Position, Center(obstacle.Cell));
                if (IsBlocker(obstacle) && distance <= rules.Combat.FrontageContactDistance * rules.Combat.FrontageContactDistance && !OwnsFrontage(obstacle, enemy))
                    SelectFrontline(obstacle, enemy, distance);
            }
            foreach (var enemy in enemies)
            {
                if (enemy.Health <= 0) continue;
                var definition = rules.Enemy(enemy.Kind);
                enemy.AttackCooldown = Math.Max(0, enemy.AttackCooldown - delta);
                if (TryRangedAttack(enemy, delta)) continue;
                if (DistanceSquared(enemy.Position, TargetPoint(enemy)) <= rules.Combat.ObjectiveContactDistance * rules.Combat.ObjectiveContactDistance)
                {
                    if (enemy.AttackCooldown <= 0)
                    { DamageObjective(enemy.ObjectiveIndex, Math.Max(0, definition.Damage)); enemy.AttackCooldown = Math.Max(rules.Combat.MinimumActionInterval, definition.AttackInterval); }
                    continue;
                }
                Cell destination = CellOf(enemy.Destination);
                var obstacle = StructureAt(destination);
                if (IsBlocker(obstacle))
                {
                    float contactDistance = DistanceSquared(enemy.Position, Center(obstacle.Cell));
                    if (contactDistance <= rules.Combat.StructureContactDistance * rules.Combat.StructureContactDistance)
                    {
                        if (!OwnsFrontage(obstacle, enemy)) continue;
                        obstacle.FrontageUsed++;
                        if (enemy.AttackCooldown <= 0)
                        {
                            float siege = Math.Max(0, definition.StructureDamageMultiplier);
                            float damage = DamageRules.StructureDamage(Math.Max(0, definition.Damage) * siege, obstacle, rules);
                            obstacle.Health = Math.Max(0, obstacle.Health - damage);
                            enemy.AttackCooldown = Math.Max(rules.Combat.MinimumActionInterval, definition.AttackInterval);
                            if (obstacle.Health <= 0) RemoveStructure(obstacle, true);
                            else RefreshHealthBand(obstacle);
                        }
                        if (obstacle.Health > 0) continue;
                    }
                }
                float dx = enemy.Destination.X - enemy.Position.X, dy = enemy.Destination.Y - enemy.Position.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                float movement = Math.Max(0, definition.Speed) * enemy.SpeedScale *
                    EnemyEffects.MovementMultiplier(enemy, enemyEffects, Mechanics.CorpseBlots ? rules.BlotSpeedBonus : 0) * delta;
                bool reached = distance <= movement + 0.00001f;
                var nextPosition = reached ? enemy.Destination : distance > 0.00001f ?
                    new Point2(enemy.Position.X + dx / distance * movement, enemy.Position.Y + dy / distance * movement) : enemy.Position;
                if (IsBlocker(StructureAt(CellOf(nextPosition)))) continue;
                if (crowd.TryMove(enemy.Position, nextPosition))
                {
                    enemy.Position = nextPosition;
                    if (reached) enemy.HasDestination = false;
                }
            }
        }

        private void SelectFrontline(StructureState obstacle, EnemyState enemy, float distance)
        {
            int start = (obstacle.Cell.Y * rules.Width + obstacle.Cell.X) * 3;
            int available = -1;
            for (int slot = start; slot < start + 3; slot++)
            {
                if (reservedFrontline[slot]) continue;
                var current = frontline[slot];
                if (current == null) { available = slot; break; }
                if (available < 0 || frontlineDistances[slot] > frontlineDistances[available] ||
                    (frontlineDistances[slot] == frontlineDistances[available] && current.Id > frontline[available].Id))
                    available = slot;
            }
            if (available < 0) return;
            var displaced = frontline[available];
            if (displaced != null && (frontlineDistances[available] < distance ||
                (frontlineDistances[available] == distance && displaced.Id < enemy.Id))) return;
            frontline[available] = enemy; frontlineDistances[available] = distance;
        }

        private void RetainFrontlineOwners()
        {
            for (int index = 0; index < grid.Length; index++)
            {
                var obstacle = grid[index];
                for (int slot = index * 3; slot < index * 3 + 3; slot++)
                {
                    var owner = frontline[slot];
                    float distance = owner == null || obstacle == null ? float.MaxValue :
                        DistanceSquared(owner.Position, Center(obstacle.Cell));
                    bool valid = IsBlocker(obstacle) && owner != null && owner.Health > 0 && owner.HasDestination &&
                        CellOf(owner.Destination) == obstacle.Cell && distance <= rules.Combat.FrontageContactDistance * rules.Combat.FrontageContactDistance;
                    reservedFrontline[slot] = valid;
                    if (valid) frontlineDistances[slot] = distance;
                    else frontline[slot] = null;
                }
            }
        }

        private bool OwnsFrontage(StructureState obstacle, EnemyState enemy)
        {
            int start = (obstacle.Cell.Y * rules.Width + obstacle.Cell.X) * 3;
            return frontline[start] == enemy || frontline[start + 1] == enemy || frontline[start + 2] == enemy;
        }

        private void RemoveDeadEnemies()
        {
            int write = 0;
            for (int read = 0; read < enemies.Count; read++)
            {
                var enemy = enemies[read];
                if (enemy.Health > 0) enemies[write++] = enemy;
                else
                {
                    OnEnemyDeath(enemy);
                    Kills++;
                    CreditInk(EnemyRewardBudget.Consume(enemy, rules.Enemy(enemy.Kind).Reward, rules.Progression.RequireAssignedEnemyRewards));
                }
            }
            if (write < enemies.Count) enemies.RemoveRange(write, enemies.Count - write);
        }

        private void AddStarterDefenses()
        {
            int radius = Math.Min(5, Math.Min(rules.Width / 2 - 2, rules.Height / 2 - 2));
            for (int i = -2; i <= 2; i++)
            {
                AddStructure(new Cell(BaseCell.X - radius, BaseCell.Y + i), StructureKind.Wall, MaterialTier.Pencil, 0, true);
                AddStructure(new Cell(BaseCell.X + radius, BaseCell.Y + i), StructureKind.Wall, MaterialTier.Pencil, 0, true);
            }
            AddLegacyStarter(new Cell(BaseCell.X - 3, BaseCell.Y - 3), StructureKind.Gun);
            AddLegacyStarter(new Cell(BaseCell.X - 3, BaseCell.Y + 3), StructureKind.Gun);
            AddLegacyStarter(new Cell(BaseCell.X + 3, BaseCell.Y - 3), StructureKind.Piercer);
            AddLegacyStarter(new Cell(BaseCell.X + 3, BaseCell.Y + 3), StructureKind.Gun);
        }

        // Convenience layout for legacy scenes; removed catalog records have no replacement.
        private void AddLegacyStarter(Cell cell,StructureKind kind)
        {if(rules.Content.TryGetWeapon(LegacyContentIds.Weapon(kind),out var weapon)&&weapon.Enabled)AddStructure(cell,kind,MaterialTier.Pencil,0,true,weapon.Numeric,weapon.Id);}

        private void AddStructure(Cell cell, StructureKind kind, MaterialTier tier, int paid, bool complete,StructureDefinition configuredDefinition=null,string contentId=null)
        {
            var definition = configuredDefinition ?? rules.Structure(kind);
            var material = rules.Material(tier);
            float maxHealth = Math.Max(1, definition.MaxHealth * material.HealthMultiplier * healthMultiplier);
            var structure = new StructureState
            {
                Id = nextStructureId++, Cell = cell, Kind = kind, Tier = tier, ContentId=contentId??LegacyContentIds.Weapon(kind),
                Health = maxHealth, MaxHealth = maxHealth, PaidInk = paid,
                BuildRemaining = complete ? 0 : Math.Max(0, definition.BuildSeconds * material.BuildTimeMultiplier),
                IsComplete = complete || definition.BuildSeconds * material.BuildTimeMultiplier <= 0,
                Aim = Center(BaseCell), Priority = TargetPriority.Nearest
            };
            structures.Add(structure);
            grid[cell.Y * rules.Width + cell.X] = structure;
            MarkStructureChanged(structure, structure.IsComplete);
        }

        private void RemoveStructure(StructureState structure, bool destroyed = false)
        {
            if (StructureAt(structure.Cell) != structure) return;
            if (destroyed)
            {
                Statistics.RecordLoss(structure.Cell, TickIndex, Elapsed, structure.Kind, structure.Tier, structure.PaidInk,structure.ContentId);
                EventBuffer.Publish(TickIndex, SimulationEventKind.StructureDestroyed, Center(structure.Cell), Center(structure.Cell),
                    structure.Kind, entityId: structure.Id,weaponId:structure.ContentId);
            }
            grid[structure.Cell.Y * rules.Width + structure.Cell.X] = null;
            structures.Remove(structure);
            int firstSlot = (structure.Cell.Y * rules.Width + structure.Cell.X) * 3;
            for (int slot = firstSlot; slot < firstSlot + 3; slot++)
            { frontline[slot] = null; reservedFrontline[slot] = false; }
            navigationBands[structure.Cell.Y * rules.Width + structure.Cell.X] = 0;
            MarkStructureChanged(structure, structure.IsComplete);
        }

        private static bool IsBlocker(StructureState structure) => structure != null && structure.IsComplete &&
            structure.Health > 0 && structure.Kind != StructureKind.Spikes;

        private void MarkStructureChanged(StructureState structure, bool topologyChanged)
        {
            StructureVisualVersion++;
            if (topologyChanged && structure.Kind != StructureKind.Spikes) { TopologyVersion++; flowDirty = true; }
            navigationBands[structure.Cell.Y * rules.Width + structure.Cell.X] = IsBlocker(structure) ? WeightedFlowField.HealthBand(structure) : 0;
        }

        private void RefreshHealthBand(StructureState structure)
        {
            int index = structure.Cell.Y * rules.Width + structure.Cell.X;
            int band = IsBlocker(structure) ? WeightedFlowField.HealthBand(structure) : 0;
            if (navigationBands[index] == band) return;
            navigationBands[index] = band;
            StructureVisualVersion++;
            if (structure.IsComplete && structure.Kind != StructureKind.Spikes) flowDirty = true;
        }

        private int BuildCost(StructureKind kind, MaterialTier tier) => TowerCatalog.ToolCost(rules, kind, tier);
        private void CreditInk(int amount) => Ink = (int)Math.Min(int.MaxValue, (long)Ink + Math.Max(0, amount));
        private bool Inside(Cell cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < rules.Width && cell.Y < rules.Height;
        private static Cell CellOf(Point2 point) => new Cell((int)Math.Floor(point.X), (int)Math.Floor(point.Y));
        private static Point2 Center(Cell cell) => new Point2(cell.X + 0.5f, cell.Y + 0.5f);
        private static float DistanceSquared(Point2 a, Point2 b) { float dx = a.X - b.X, dy = a.Y - b.Y; return dx * dx + dy * dy; }
        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}
