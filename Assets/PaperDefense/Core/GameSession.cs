using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public sealed class GameSession
    {
        public GameBalance Balance { get; }
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
        private readonly EnemyCrowdGrid crowd;
        private readonly int[] navigationBands;
        private readonly EnemyState[] frontline;
        private readonly float[] frontlineDistances;
        private readonly bool[] reservedFrontline;
        private readonly Random random;
        private int nextStructureId = 1, nextEnemyId = 1;
        private float spawnClock;
        private double supplyRemainder;
        private float fireMultiplier = 1, damageMultiplier = 1, healthMultiplier = 1, salvageBonus;
        private float supplyMultiplier = 1;
        private bool flowDirty = true;

        public GameSession(GameBalance balance, int seed = 1337, bool starterDefenses = true)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            if (balance.Width < 10 || balance.Height < 10) throw new ArgumentException("Поле должно быть не меньше 10 × 10 клеток.", nameof(balance));
            Balance = balance;
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
            weapons = new WeaponSystem(balance);
            random = new Random(seed);
            if (starterDefenses) AddStarterDefenses();
        }

        /// <summary>Quotes a deduplicated stroke. A single invalid cell rejects the entire order.</summary>
        public BuildQuote QuoteBuild(IReadOnlyList<Cell> cells, StructureKind kind, MaterialTier tier)
        {
            var unique = new List<Cell>();
            var quote = new BuildQuote { Cells = unique, Reason = "", Valid = false };
            if (!Enum.IsDefined(typeof(StructureKind), kind) || !Enum.IsDefined(typeof(MaterialTier), tier))
            { quote.Reason = "Неизвестный инструмент."; return quote; }
            if (Phase == RunPhase.Victory || Phase == RunPhase.Defeat)
            { quote.Reason = "Эта страница уже закончена."; return quote; }
            if (cells == null || cells.Count == 0) { quote.Reason = "Нарисуйте хотя бы одну клетку."; return quote; }
            var seen = new HashSet<Cell>();
            string invalid = "";
            int newSegments = 0;
            foreach (var cell in cells)
            {
                if (!seen.Add(cell)) continue;
                unique.Add(cell);
                if (invalid.Length != 0) continue;
                if (cell.X <= 0 || cell.Y <= 0 || cell.X >= Balance.Width - 1 || cell.Y >= Balance.Height - 1)
                    invalid = "Оставьте внешнее поле для появления врагов.";
                else if (Math.Abs(cell.X - BaseCell.X) <= 1 && Math.Abs(cell.Y - BaseCell.Y) <= 1)
                    invalid = "Центр тетради нужно оставить свободным.";
                else if (StructureAt(cell) != null)
                {
                    var existing = StructureAt(cell);
                    if ((kind != StructureKind.Wall && kind != StructureKind.Spikes) || existing.Kind != kind || existing.Tier != tier)
                        invalid = "Клетка уже занята несовместимым рисунком.";
                    continue;
                }
                newSegments++;
            }
            // Retain only stroke cells. Previewing never copies a large horde into a temporary set.
            if (invalid.Length == 0 && newSegments > 0)
                foreach (var enemy in enemies)
                    if (enemy.Health > 0 && seen.Contains(CellOf(enemy.Position)) && StructureAt(CellOf(enemy.Position)) == null)
                    { invalid = "Здесь находится враг."; break; }
            long cost = (long)BuildCost(kind, tier) * newSegments;
            quote.Cost = (int)Math.Min(int.MaxValue, cost);
            if (invalid.Length != 0) { quote.Reason = invalid; return quote; }
            if (cost > Ink) { quote.Reason = "Не хватает штрихов: нужно " + cost + "."; return quote; }
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
            float rate = Clamp(Balance.Material(structure.Tier).RefundFraction + salvageBonus, 0, 0.95f);
            refund = (int)Math.Floor(structure.PaidInk * fraction * rate + 0.00001f);
            Ink += refund;
            Statistics.RecordRefund(refund);
            RemoveStructure(structure);
            return true;
        }

        public bool TryUpgrade(Cell cell, out string reason)
        {
            reason = "";
            if (Phase == RunPhase.Victory || Phase == RunPhase.Defeat) { reason = "Эта страница уже закончена."; return false; }
            var structure = StructureAt(cell);
            if (structure == null) { reason = "Сначала выберите рисунок."; return false; }
            if (structure.Tier == MaterialTier.Marker) { reason = "Это уже самый прочный материал."; return false; }
            if (!structure.IsComplete) { reason = "Дождитесь завершения рисунка."; return false; }
            var nextTier = (MaterialTier)((int)structure.Tier + 1);
            int difference = Math.Max(0, BuildCost(structure.Kind, nextTier) - BuildCost(structure.Kind, structure.Tier));
            if (difference > Ink) { reason = "Не хватает штрихов: нужно " + difference + "."; return false; }
            Ink -= difference;
            structure.PaidInk += difference;
            Statistics.RecordUpgrade(difference);
            float fraction = Clamp(structure.Health / Math.Max(1, structure.MaxHealth), 0, 1);
            structure.Tier = nextTier;
            structure.MaxHealth = Balance.Structure(structure.Kind).MaxHealth * Balance.Material(nextTier).HealthMultiplier * healthMultiplier;
            structure.Health = structure.MaxHealth * fraction;
            structure.BuildRemaining = Math.Max(0, Balance.Structure(structure.Kind).BuildSeconds * Balance.Material(nextTier).BuildTimeMultiplier);
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

        public StructureState StructureAt(Cell cell) => Inside(cell) ? grid[cell.Y * Balance.Width + cell.X] : null;

        public void StartNextWave()
        {
            if ((Phase != RunPhase.Planning && Phase != RunPhase.Intermission) || PendingUpgrade) return;
            if (WaveIndex >= Balance.Waves.Count) { Phase = RunPhase.Victory; return; }
            Ink += Math.Min(Math.Max(0, Balance.MaxEarlyLaunchInk), (int)Math.Floor(Math.Max(0, Countdown) * Math.Max(0, Balance.EarlyLaunchInkPerSecond)));
            WaveIndex++;
            var wave = Balance.Waves[WaveIndex - 1];
            RemainingToSpawn = Math.Max(0, wave.Count);
            Countdown = 0;
            spawnClock = 0;
            Phase = RunPhase.Assault;
        }

        public void ChooseUpgrade(RunUpgrade upgrade)
        {
            if (!PendingUpgrade || !Enum.IsDefined(typeof(RunUpgrade), upgrade)) return;
            PendingUpgrade = false;
            Statistics.RecordUpgradeChoice(upgrade);
            switch (upgrade)
            {
                case RunUpgrade.RapidFire: fireMultiplier *= 1.22f; break;
                case RunUpgrade.DeepInk:
                    damageMultiplier *= 1.18f;
                    healthMultiplier *= 1.25f;
                    foreach (var structure in structures) { structure.Health *= 1.25f; structure.MaxHealth *= 1.25f; }
                    flowDirty = true;
                    StructureVisualVersion++;
                    break;
                case RunUpgrade.Salvage:
                    salvageBonus += 0.12f;
                    supplyMultiplier *= 1.25f;
                    Ink += 60;
                    break;
            }
        }

        /// <summary>Zero delta is a real pause. Large deltas are subdivided to prevent tunnelling.</summary>
        public void Tick(float delta)
        {
            shots.Clear();
            if (delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            if (Phase == RunPhase.Victory || Phase == RunPhase.Defeat) return;
            double remaining = delta;
            float maximumStep = Clamp(Balance.SimulationStep, 0.005f, 0.05f);
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
            crowd.Rebuild(enemies);
            supplyRemainder += Math.Max(0, Balance.PassiveInkPerSecond) * supplyMultiplier * delta;
            int supplied = (int)Math.Floor(supplyRemainder + 0.000001);
            Ink += supplied;
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
                structure.Cooldown = Math.Max(0, structure.Cooldown - delta);
                structure.FrontageUsed = 0;
                RefreshHealthBand(structure);
            }
            if (Phase == RunPhase.Planning || Phase == RunPhase.Intermission)
            {
                Countdown = Math.Max(0, Countdown - delta);
                if (Countdown <= 0 && !PendingUpgrade) StartNextWave();
            }
            if (Phase != RunPhase.Assault) return;
            SpawnEnemies(delta);
            if (flowDirty)
            {
                var profile = Balance.Enemy(EnemyKind.Doodle);
                long rebuildStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                flow.Rebuild(BaseCell, grid, profile.Speed, Math.Max(0.01f, profile.Damage) / Math.Max(0.05f, profile.AttackInterval));
                Statistics.RecordNavigation((System.Diagnostics.Stopwatch.GetTimestamp() - rebuildStarted) *
                    1000.0 / System.Diagnostics.Stopwatch.Frequency);
                flowDirty = false;
                FlowRebuildCount++;
                foreach (var enemy in enemies) enemy.HasDestination = false;
            }
            MoveEnemies(delta);
            if (BaseHealth <= 0) { BaseHealth = 0; Phase = RunPhase.Defeat; return; }
            weapons.Fire(structures, enemies, shots, fireMultiplier, damageMultiplier, delta, TickIndex, Statistics, EventBuffer);
            RemoveDeadEnemies();
            if (RemainingToSpawn == 0 && enemies.Count == 0 && !weapons.HasPendingProjectiles) CompleteWave();
        }

        private void SpawnEnemies(float delta)
        {
            var wave = Balance.Waves[WaveIndex - 1];
            spawnClock -= delta;
            float interval = Math.Max(0.005f, wave.SpawnInterval);
            while (RemainingToSpawn > 0 && spawnClock <= 0)
            {
                double roll = random.NextDouble();
                float gnawers = Clamp(wave.GnawerFraction, 0, 1);
                float runners = Clamp(wave.RunnerFraction, 0, 1 - gnawers);
                EnemyKind kind = roll < gnawers ? EnemyKind.Gnawer : roll < gnawers + runners ? EnemyKind.Runner : EnemyKind.Doodle;
                var definition = Balance.Enemy(kind);
                int sideCount = 0;
                for (int bit = 1; bit <= 8; bit <<= 1) if ((wave.Sides & bit) != 0) sideCount++;
                int sideIndex = random.Next(Math.Max(1, sideCount));
                int side = 1;
                if (sideCount > 0)
                {
                    for (int bit = 1; bit <= 8; bit <<= 1)
                        if ((wave.Sides & bit) != 0 && sideIndex-- == 0) { side = bit; break; }
                }
                int offset = side <= 2 ? random.Next(2, Balance.Height - 2) : random.Next(2, Balance.Width - 2);
                int edgeLength = side <= 2 ? Balance.Height - 4 : Balance.Width - 4;
                Cell spawnCell = default;
                bool admitted = false;
                for (int attempt = 0; attempt < edgeLength; attempt++)
                {
                    int edgeOffset = 2 + (offset - 2 + attempt) % edgeLength;
                    spawnCell = side == 1 ? new Cell(0, edgeOffset) : side == 2 ? new Cell(Balance.Width - 1, edgeOffset) :
                        side == 4 ? new Cell(edgeOffset, Balance.Height - 1) : new Cell(edgeOffset, 0);
                    if (crowd.CanEnter(spawnCell)) { admitted = true; break; }
                }
                if (!admitted) { spawnClock = 0; return; }
                spawnClock += interval;
                RemainingToSpawn--;
                Point2 position = crowd.Add(spawnCell);
                float lateScale = 1 + Math.Max(0, WaveIndex - 3) * 0.06f;
                enemies.Add(new EnemyState
                {
                    Id = nextEnemyId++, Kind = kind, Position = position,
                    Health = Math.Max(1, definition.MaxHealth * lateScale), MaxHealth = Math.Max(1, definition.MaxHealth * lateScale),
                    SpeedScale = 0.92f + (float)random.NextDouble() * 0.16f, AttackCooldown = 0
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
                if (enemy.HasDestination && DistanceSquared(enemy.Position, enemy.Destination) > 2.25f)
                    enemy.HasDestination = false;
                if (!enemy.HasDestination)
                {
                    enemy.Destination = Center(flow.Next(CellOf(enemy.Position)));
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
                if (IsBlocker(obstacle) && distance <= 1.3f * 1.3f && !OwnsFrontage(obstacle, enemy))
                    SelectFrontline(obstacle, enemy, distance);
            }
            foreach (var enemy in enemies)
            {
                if (enemy.Health <= 0) continue;
                var definition = Balance.Enemy(enemy.Kind);
                enemy.AttackCooldown = Math.Max(0, enemy.AttackCooldown - delta);
                if (DistanceSquared(enemy.Position, basePoint) <= 1.15f * 1.15f)
                {
                    if (enemy.AttackCooldown <= 0)
                    { BaseHealth -= Math.Max(0, definition.Damage); enemy.AttackCooldown = Math.Max(0.05f, definition.AttackInterval); }
                    continue;
                }
                Cell destination = CellOf(enemy.Destination);
                var obstacle = StructureAt(destination);
                if (IsBlocker(obstacle))
                {
                    float contactDistance = DistanceSquared(enemy.Position, Center(obstacle.Cell));
                    if (contactDistance <= 1.05f * 1.05f)
                    {
                        if (!OwnsFrontage(obstacle, enemy)) continue;
                        obstacle.FrontageUsed++;
                        if (enemy.AttackCooldown <= 0)
                        {
                            float siege = enemy.Kind == EnemyKind.Gnawer ? 1.5f : 1;
                            obstacle.Health = Math.Max(0, obstacle.Health - Math.Max(0, definition.Damage) * siege);
                            enemy.AttackCooldown = Math.Max(0.05f, definition.AttackInterval);
                            if (obstacle.Health <= 0) RemoveStructure(obstacle, true);
                            else RefreshHealthBand(obstacle);
                        }
                        if (obstacle.Health > 0) continue;
                    }
                }
                float dx = enemy.Destination.X - enemy.Position.X, dy = enemy.Destination.Y - enemy.Position.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                float movement = Math.Max(0, definition.Speed) * enemy.SpeedScale * delta;
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
            int start = (obstacle.Cell.Y * Balance.Width + obstacle.Cell.X) * 3;
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
                        CellOf(owner.Destination) == obstacle.Cell && distance <= 1.3f * 1.3f;
                    reservedFrontline[slot] = valid;
                    if (valid) frontlineDistances[slot] = distance;
                    else frontline[slot] = null;
                }
            }
        }

        private bool OwnsFrontage(StructureState obstacle, EnemyState enemy)
        {
            int start = (obstacle.Cell.Y * Balance.Width + obstacle.Cell.X) * 3;
            return frontline[start] == enemy || frontline[start + 1] == enemy || frontline[start + 2] == enemy;
        }

        private void RemoveDeadEnemies()
        {
            int write = 0;
            for (int read = 0; read < enemies.Count; read++)
            {
                var enemy = enemies[read];
                if (enemy.Health > 0) enemies[write++] = enemy;
                else { Kills++; Ink += Math.Max(0, Balance.Enemy(enemy.Kind).Reward); }
            }
            if (write < enemies.Count) enemies.RemoveRange(write, enemies.Count - write);
        }

        private void CompleteWave()
        {
            Ink += Math.Max(0, Balance.WaveInkGrant);
            if (WaveIndex >= Balance.Waves.Count) { Phase = RunPhase.Victory; Countdown = 0; return; }
            Phase = RunPhase.Intermission;
            Countdown = Math.Max(0, Balance.IntermissionSeconds);
            PendingUpgrade = WaveIndex == 2 || WaveIndex == 4 || WaveIndex == 6;
        }

        private void AddStarterDefenses()
        {
            int radius = Math.Min(5, Math.Min(Balance.Width / 2 - 2, Balance.Height / 2 - 2));
            for (int i = -2; i <= 2; i++)
            {
                AddStructure(new Cell(BaseCell.X - radius, BaseCell.Y + i), StructureKind.Wall, MaterialTier.Pencil, 0, true);
                AddStructure(new Cell(BaseCell.X + radius, BaseCell.Y + i), StructureKind.Wall, MaterialTier.Pencil, 0, true);
            }
            AddStructure(new Cell(BaseCell.X - 3, BaseCell.Y - 3), StructureKind.Gun, MaterialTier.Pencil, 0, true);
            AddStructure(new Cell(BaseCell.X - 3, BaseCell.Y + 3), StructureKind.Gun, MaterialTier.Pencil, 0, true);
            AddStructure(new Cell(BaseCell.X + 3, BaseCell.Y - 3), StructureKind.Piercer, MaterialTier.Pencil, 0, true);
            AddStructure(new Cell(BaseCell.X + 3, BaseCell.Y + 3), StructureKind.Gun, MaterialTier.Pencil, 0, true);
        }

        private void AddStructure(Cell cell, StructureKind kind, MaterialTier tier, int paid, bool complete)
        {
            var definition = Balance.Structure(kind);
            var material = Balance.Material(tier);
            float maxHealth = Math.Max(1, definition.MaxHealth * material.HealthMultiplier * healthMultiplier);
            var structure = new StructureState
            {
                Id = nextStructureId++, Cell = cell, Kind = kind, Tier = tier,
                Health = maxHealth, MaxHealth = maxHealth, PaidInk = paid,
                BuildRemaining = complete ? 0 : Math.Max(0, definition.BuildSeconds * material.BuildTimeMultiplier),
                IsComplete = complete || definition.BuildSeconds * material.BuildTimeMultiplier <= 0,
                Aim = Center(BaseCell), Priority = TargetPriority.Nearest
            };
            structures.Add(structure);
            grid[cell.Y * Balance.Width + cell.X] = structure;
            MarkStructureChanged(structure, structure.IsComplete);
        }

        private void RemoveStructure(StructureState structure, bool destroyed = false)
        {
            if (StructureAt(structure.Cell) != structure) return;
            if (destroyed)
            {
                Statistics.RecordLoss(structure.Cell, TickIndex, Elapsed, structure.Kind, structure.Tier, structure.PaidInk);
                EventBuffer.Publish(TickIndex, SimulationEventKind.StructureDestroyed, Center(structure.Cell), Center(structure.Cell),
                    structure.Kind, entityId: structure.Id);
            }
            grid[structure.Cell.Y * Balance.Width + structure.Cell.X] = null;
            structures.Remove(structure);
            int firstSlot = (structure.Cell.Y * Balance.Width + structure.Cell.X) * 3;
            for (int slot = firstSlot; slot < firstSlot + 3; slot++)
            { frontline[slot] = null; reservedFrontline[slot] = false; }
            navigationBands[structure.Cell.Y * Balance.Width + structure.Cell.X] = 0;
            MarkStructureChanged(structure, structure.IsComplete);
        }

        private static bool IsBlocker(StructureState structure) => structure != null && structure.IsComplete &&
            structure.Health > 0 && structure.Kind != StructureKind.Spikes;

        private void MarkStructureChanged(StructureState structure, bool topologyChanged)
        {
            StructureVisualVersion++;
            if (topologyChanged && structure.Kind != StructureKind.Spikes) { TopologyVersion++; flowDirty = true; }
            navigationBands[structure.Cell.Y * Balance.Width + structure.Cell.X] = IsBlocker(structure) ? WeightedFlowField.HealthBand(structure) : 0;
        }

        private void RefreshHealthBand(StructureState structure)
        {
            int index = structure.Cell.Y * Balance.Width + structure.Cell.X;
            int band = IsBlocker(structure) ? WeightedFlowField.HealthBand(structure) : 0;
            if (navigationBands[index] == band) return;
            navigationBands[index] = band;
            StructureVisualVersion++;
            if (structure.IsComplete && structure.Kind != StructureKind.Spikes) flowDirty = true;
        }

        private int BuildCost(StructureKind kind, MaterialTier tier) =>
            (int)Math.Ceiling(Math.Max(0, Balance.Structure(kind).Cost) * Math.Max(0, Balance.Material(tier).CostMultiplier));
        private bool Inside(Cell cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Balance.Width && cell.Y < Balance.Height;
        private static Cell CellOf(Point2 point) => new Cell((int)Math.Floor(point.X), (int)Math.Floor(point.Y));
        private static Point2 Center(Cell cell) => new Point2(cell.X + 0.5f, cell.Y + 0.5f);
        private static float DistanceSquared(Point2 a, Point2 b) { float dx = a.X - b.X, dy = a.Y - b.Y; return dx * dx + dy * dy; }
        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}
