using System;
using System.Collections.Generic;
using System.Linq;

namespace PaperDefense.Core
{
    [Serializable]
    public sealed class RunStatisticsCheckpointData
    {
        public int Version = 1;
        public int NavigationRebuilds;
        public double NavigationTotalMilliseconds, NavigationMaxMilliseconds;
        public float TotalDamage, TotalIdleSeconds, TotalRepairedHealth, BaseDamageTaken, UnattributedBaseDamage;
        public int TotalGeneratorIncome, TotalEnemiesMarked, TotalEnemiesSlowedByPrism, TotalSpawnedEnemies;
        public int BaseHits, UnattributedBaseHits, BuildInvestment, UpgradeInvestment, RefundedInk, LostInvestment;
        public int PeakAliveEnemies, TotalStructuresBuilt, DestroyedStructures, BlotsCleared;
        public bool DefeatTelemetryTruncated;
        public int[] SpawnedByKind, SpawnedByDrawingTier;
        public RunUpgrade[] Upgrades;
        public StatisticsWeaponCheckpointData[] Weapons;
        public StatisticsBreachCheckpointData[] Breaches;
        public StatisticsWaveCheckpointData[] Waves;
        public StatisticsApproachCheckpointData[] Approaches;
        public StatisticsPositionCheckpointData[] Positions;
    }

    [Serializable] public sealed class StatisticsWeaponCheckpointData
    {
        public string Id;
        public float Damage, Idle;
        public int Launches;
        public int[] Built;
    }
    [Serializable] public sealed class StatisticsBreachCheckpointData
    {
        public int X, Y, Investment;
        public long Tick;
        public float Elapsed;
        public StructureKind Kind;
        public MaterialTier Tier;
        public string ContentId;
    }
    [Serializable] public sealed class StatisticsWaveCheckpointData
    {
        public int WaveIndex, Destroyed, Lost;
        public float Damage;
    }
    [Serializable] public sealed class StatisticsApproachCheckpointData
    {
        public int X, Y, Hits;
        public float Damage;
    }
    [Serializable] public sealed class StatisticsPositionCheckpointData
    {
        public int Id, X, Y, Launches;
        public string ContentId;
        public float ActiveSeconds;
        public bool Destroyed, Removed;
    }

    public sealed partial class RunStatistics
    {
        const int MaximumCheckpointWeaponMetrics = 2048;

        internal RunStatisticsCheckpointData CaptureCheckpoint()
        {
            return new RunStatisticsCheckpointData {
                NavigationRebuilds = NavigationRebuilds, NavigationTotalMilliseconds = NavigationTotalMilliseconds,
                NavigationMaxMilliseconds = NavigationMaxMilliseconds,
                TotalDamage = TotalDamage, TotalIdleSeconds = TotalIdleSeconds, TotalRepairedHealth = TotalRepairedHealth,
                TotalGeneratorIncome = TotalGeneratorIncome, TotalEnemiesMarked = TotalEnemiesMarked,
                TotalEnemiesSlowedByPrism = TotalEnemiesSlowedByPrism, TotalSpawnedEnemies = TotalSpawnedEnemies,
                BaseHits = BaseHits, BaseDamageTaken = BaseDamageTaken,
                UnattributedBaseDamage = UnattributedBaseDamage, UnattributedBaseHits = UnattributedBaseHits,
                BuildInvestment = BuildInvestment, UpgradeInvestment = UpgradeInvestment, RefundedInk = RefundedInk,
                LostInvestment = LostInvestment, PeakAliveEnemies = PeakAliveEnemies,
                TotalStructuresBuilt = TotalStructuresBuilt, DestroyedStructures = DestroyedStructures, BlotsCleared = BlotsCleared,
                DefeatTelemetryTruncated = DefeatTelemetryTruncated,
                SpawnedByKind = (int[])spawnedByKind.Clone(), SpawnedByDrawingTier = (int[])spawnedByDrawingTier.Clone(),
                Upgrades = upgrades.ToArray(),
                Weapons = weaponIds.Select(id => {
                    var value = metrics[id];
                    return new StatisticsWeaponCheckpointData { Id = id, Damage = value.Damage, Idle = value.Idle,
                        Launches = value.Launches, Built = new[] { BuiltCount(id, MaterialTier.Pencil),
                            BuiltCount(id, MaterialTier.Ink), BuiltCount(id, MaterialTier.Marker) } };
                }).ToArray(),
                Breaches = breaches.Select(value => new StatisticsBreachCheckpointData { X = value.Cell.X, Y = value.Cell.Y,
                    Tick = value.Tick, Elapsed = value.Elapsed, Kind = value.Kind, Tier = value.Tier,
                    Investment = value.Investment, ContentId = value.ContentId }).ToArray(),
                Waves = waveLosses.OrderBy(pair => pair.Key).Select(pair => new StatisticsWaveCheckpointData {
                    WaveIndex = pair.Key, Damage = pair.Value.Damage, Destroyed = pair.Value.Destroyed, Lost = pair.Value.Lost }).ToArray(),
                Approaches = approachLosses.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X)
                    .Select(pair => new StatisticsApproachCheckpointData { X = pair.Key.X, Y = pair.Key.Y,
                        Damage = pair.Value.Damage, Hits = pair.Value.Hits }).ToArray(),
                Positions = positionActivities.OrderBy(pair => pair.Key).Select(pair => new StatisticsPositionCheckpointData {
                    Id = pair.Key, X = pair.Value.Cell.X, Y = pair.Value.Cell.Y, ContentId = pair.Value.ContentId,
                    ActiveSeconds = pair.Value.ActiveSeconds, Launches = pair.Value.Launches,
                    Destroyed = pair.Value.Destroyed, Removed = pair.Value.Removed }).ToArray()
            };
        }

        internal void RestoreCheckpoint(RunStatisticsCheckpointData data)
        {
            // Validate every record before replacing any published aggregate. Never replay Record* methods.
            ValidateCheckpoint(data);
            NavigationRebuilds = data.NavigationRebuilds; NavigationTotalMilliseconds = data.NavigationTotalMilliseconds;
            NavigationMaxMilliseconds = data.NavigationMaxMilliseconds;
            TotalDamage = data.TotalDamage; TotalIdleSeconds = data.TotalIdleSeconds; TotalRepairedHealth = data.TotalRepairedHealth;
            TotalGeneratorIncome = data.TotalGeneratorIncome; TotalEnemiesMarked = data.TotalEnemiesMarked;
            TotalEnemiesSlowedByPrism = data.TotalEnemiesSlowedByPrism; TotalSpawnedEnemies = data.TotalSpawnedEnemies;
            BaseHits = data.BaseHits; BaseDamageTaken = data.BaseDamageTaken;
            UnattributedBaseDamage = data.UnattributedBaseDamage; UnattributedBaseHits = data.UnattributedBaseHits;
            BuildInvestment = data.BuildInvestment; UpgradeInvestment = data.UpgradeInvestment; RefundedInk = data.RefundedInk;
            LostInvestment = data.LostInvestment; PeakAliveEnemies = data.PeakAliveEnemies;
            TotalStructuresBuilt = data.TotalStructuresBuilt; DestroyedStructures = data.DestroyedStructures; BlotsCleared = data.BlotsCleared;
            DefeatTelemetryTruncated = data.DefeatTelemetryTruncated;
            Array.Copy(data.SpawnedByKind, spawnedByKind, spawnedByKind.Length);
            Array.Copy(data.SpawnedByDrawingTier, spawnedByDrawingTier, spawnedByDrawingTier.Length);
            upgrades.Clear(); upgrades.AddRange(data.Upgrades);
            metrics.Clear(); weaponIds.Clear();
            foreach (var saved in data.Weapons)
            {
                var value = new WeaponMetrics { Damage = saved.Damage, Idle = saved.Idle, Launches = saved.Launches };
                for (int tier = 0; tier < 3; tier++) value.Built[(MaterialTier)tier] = saved.Built[tier];
                metrics.Add(saved.Id, value); weaponIds.Add(saved.Id);
            }
            breaches.Clear();
            foreach (var saved in data.Breaches)
                breaches.Add(new BreachRecord(new Cell(saved.X, saved.Y), saved.Tick, saved.Elapsed,
                    saved.Kind, saved.Tier, saved.Investment, saved.ContentId));
            waveLosses.Clear();
            foreach (var saved in data.Waves)
                waveLosses.Add(saved.WaveIndex, new WaveLoss { Damage = saved.Damage, Destroyed = saved.Destroyed, Lost = saved.Lost });
            approachLosses.Clear();
            foreach (var saved in data.Approaches)
                approachLosses.Add(new Cell(saved.X, saved.Y), new ApproachLoss { Damage = saved.Damage, Hits = saved.Hits });
            positionActivities.Clear();
            foreach (var saved in data.Positions)
                positionActivities.Add(saved.Id, new PositionActivity { Id = saved.Id, Cell = new Cell(saved.X, saved.Y),
                    ContentId = saved.ContentId, ActiveSeconds = saved.ActiveSeconds, Launches = saved.Launches,
                    Destroyed = saved.Destroyed, Removed = saved.Removed });
        }

        static void ValidateCheckpoint(RunStatisticsCheckpointData data)
        {
            if (data == null) throw new ArgumentException("Missing checkpoint statistics.");
            if (data.Version > 1) throw new NotSupportedException("Future checkpoint statistics version.");
            RequireCheckpoint(data.Version == 1);
            CheckpointNumbers(data.NavigationRebuilds, data.NavigationTotalMilliseconds, data.NavigationMaxMilliseconds,
                data.TotalDamage, data.TotalIdleSeconds, data.TotalRepairedHealth, data.TotalGeneratorIncome,
                data.TotalEnemiesMarked, data.TotalEnemiesSlowedByPrism, data.TotalSpawnedEnemies,
                data.BaseHits, data.BaseDamageTaken, data.UnattributedBaseDamage, data.UnattributedBaseHits,
                data.BuildInvestment, data.UpgradeInvestment, data.RefundedInk, data.LostInvestment,
                data.PeakAliveEnemies, data.TotalStructuresBuilt, data.DestroyedStructures, data.BlotsCleared);
            CheckpointCounts(data.SpawnedByKind, kindSlots); CheckpointCounts(data.SpawnedByDrawingTier, tierSlots);
            RequireCheckpoint(data.Upgrades != null && data.Upgrades.Length <= 16);
            foreach (var value in data.Upgrades) RequireCheckpoint(Enum.IsDefined(typeof(RunUpgrade), value));
            RequireCheckpoint(data.Weapons != null && data.Weapons.Length <= MaximumCheckpointWeaponMetrics);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in data.Weapons)
            {
                RequireCheckpoint(value != null && value.Id != null && value.Id.Length <= 512 && ids.Add(value.Id));
                CheckpointNumbers(value.Damage, value.Idle, value.Launches); CheckpointCounts(value.Built, 3);
            }
            RequireCheckpoint(data.Breaches != null && data.Breaches.Length <= 32);
            foreach (var value in data.Breaches)
            {
                RequireCheckpoint(value != null && Enum.IsDefined(typeof(StructureKind), value.Kind) &&
                    Enum.IsDefined(typeof(MaterialTier), value.Tier) && (value.ContentId == null || value.ContentId.Length <= 512));
                CheckpointNumbers(value.Tick, value.Elapsed, value.Investment);
            }
            RequireCheckpoint(data.Waves != null && data.Waves.Length <= MaximumWaveLossRecords);
            var waveIds = new HashSet<int>();
            foreach (var value in data.Waves)
            {
                RequireCheckpoint(value != null && value.WaveIndex > 0 && waveIds.Add(value.WaveIndex));
                CheckpointNumbers(value.Damage, value.Destroyed, value.Lost);
            }
            RequireCheckpoint(data.Approaches != null && data.Approaches.Length <= MaximumApproachLossRecords);
            var cells = new HashSet<Cell>();
            foreach (var value in data.Approaches)
            {
                RequireCheckpoint(value != null && cells.Add(new Cell(value.X, value.Y)));
                CheckpointNumbers(value.Damage, value.Hits);
            }
            RequireCheckpoint(data.Positions != null && data.Positions.Length <= MaximumPositionRecords);
            var structureIds = new HashSet<int>();
            foreach (var value in data.Positions)
            {
                RequireCheckpoint(value != null && value.Id > 0 && structureIds.Add(value.Id) &&
                    (value.ContentId == null || value.ContentId.Length <= 512) && (!value.Destroyed || value.Removed));
                CheckpointNumbers(value.ActiveSeconds, value.Launches);
            }
        }

        static void CheckpointCounts(int[] values, int length)
        {
            RequireCheckpoint(values != null && values.Length == length);
            foreach (int value in values) RequireCheckpoint(value >= 0);
        }
        static void CheckpointNumbers(params double[] values)
        {
            foreach (double value in values) RequireCheckpoint(value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value));
        }
        static void RequireCheckpoint(bool valid)
        { if (!valid) throw new ArgumentException("Invalid checkpoint statistics."); }
    }
}
