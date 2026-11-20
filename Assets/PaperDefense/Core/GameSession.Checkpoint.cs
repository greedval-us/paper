using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public sealed partial class GameSession
    {
        const int MaximumCheckpointCells = 65536, MaximumCheckpointStructures = 8192;
        const double MaximumCheckpointSeconds = 604800;

        public bool TryCaptureIntermissionCheckpoint(out GameSessionCheckpointData data, out string reason)
        {
            data = null;
            reason = "Сохранение доступно между волнами после завершения боя на поле.";
            if (Phase != RunPhase.Intermission || CompletedWaves != WaveIndex || WaveIndex <= 0 ||
                WaveIndex >= rules.Waves.Count || RemainingToSpawn != 0 || enemies.Count != 0 ||
                weapons.HasPendingProjectiles || EnemyProjectiles.Count != 0) return false;
            try
            {
                var captured = new GameSessionCheckpointData {
                    RunId = RunId.ToString("N"), Phase = Phase, WaveIndex = WaveIndex, CompletedWaves = CompletedWaves,
                    Ink = Ink, Kills = Kills, GoldEarned = GoldEarned, BaseHealth = BaseHealth,
                    NextStructureId = nextStructureId, NextEnemyId = nextEnemyId, TickIndex = TickIndex,
                    Countdown = Countdown, Elapsed = Elapsed, PendingUpgrade = PendingUpgrade,
                    TopologyVersion = TopologyVersion, StructureVisualVersion = StructureVisualVersion,
                    FlowRebuildCount = FlowRebuildCount, FlowDirty = flowDirty,
                    RandomSeed = random.Seed, RandomDrawCount = random.DrawCount,
                    EventSequence = EventBuffer.LatestSequence, DroppedEvents = EventBuffer.DroppedEventCount,
                    NextActionId = weapons.CheckpointActionId, SupplyRemainder = supplyRemainder,
                    FireMultiplier = fireMultiplier, DamageMultiplier = damageMultiplier, HealthMultiplier = healthMultiplier,
                    SalvageBonus = salvageBonus, SupplyMultiplier = supplyMultiplier, WaveRewardMultiplier = waveRewardMultiplier,
                    ColoredInk = (int[])coloredInk.Clone(), ClaimedInkFlasks = claimedInkFlasks,
                    BlotterCooldownRemaining = BlotterCooldownRemaining,
                    Structures = new StructureCheckpointData[structures.Count], Wells = new WellCheckpointData[wells.Count],
                    Blots = new BlotCheckpointData[Blots.Count], Statistics = Statistics.CaptureCheckpoint()
                };
                ValidateCheckpointHeader(rules, captured);
                for (int i = 0; i < structures.Count; i++) captured.Structures[i] = CaptureStructureCheckpoint(structures[i]);
                for (int i = 0; i < wells.Count; i++)
                {
                    var well = wells[i]; captured.Wells[i] = new WellCheckpointData {
                        Id = well.Id, X = well.Cell.X, Y = well.Cell.Y, Color = well.Color, Health = well.Health,
                        MaxHealth = well.MaxHealth, Active = well.Active, Threatened = well.Threatened, EligibleWave = well.EligibleWave
                    };
                }
                for (int i = 0; i < Blots.Count; i++) captured.Blots[i] = new BlotCheckpointData {
                    X = Blots[i].Cell.X, Y = Blots[i].Cell.Y, Remaining = Blots[i].Remaining
                };
                data = captured; reason = ""; return true;
            }
            catch (Exception error) when (CheckpointFailure(error))
            { reason = "Не удалось подготовить сохранение боя: состояние выходит за поддерживаемые границы."; return false; }
        }

        public static bool TryRestoreIntermissionCheckpoint(GameBalance balance, GameSessionCheckpointData data,
            out GameSession session, out string reason)
        {
            session = null; reason = "Сохранённое состояние боя повреждено или несовместимо.";
            try
            {
                ValidateCheckpointHeader(balance, data);
                // The candidate is private until every nested record and relationship is valid.
                var restored = new GameSession(balance, data.RandomSeed, false);
                restored.RestoreCheckpointState(data);
                session = restored; reason = ""; return true;
            }
            catch (Exception error) when (CheckpointFailure(error)) { return false; }
        }

        static bool CheckpointFailure(Exception error) => error is ArgumentException || error is InvalidOperationException ||
            error is NotSupportedException || error is OverflowException || error is FormatException ||
            error is KeyNotFoundException || error is NullReferenceException || error is IndexOutOfRangeException;
        static void CheckpointRequire(bool valid)
        { if (!valid) throw new ArgumentException("Invalid intermission checkpoint."); }
        static bool CheckpointNumber(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static bool CheckpointNonNegative(double value) => CheckpointNumber(value) && value >= 0;
        static bool CheckpointPositive(double value) => CheckpointNumber(value) && value > 0;
        static bool CheckpointTime(double value) => CheckpointNonNegative(value) && value <= MaximumCheckpointSeconds;

        static void ValidateCheckpointHeader(GameBalance balance, GameSessionCheckpointData data)
        {
            CheckpointRequire(balance != null && data != null && data.Version == 1);
            CheckpointRequire(balance.Width >= 10 && balance.Height >= 10 &&
                (long)balance.Width * balance.Height <= MaximumCheckpointCells);
            BalanceValidation.Validate(balance);
            CheckpointRequire(Guid.TryParseExact(data.RunId, "N", out var id) && id != Guid.Empty);
            CheckpointRequire(data.Phase == RunPhase.Intermission && data.WaveIndex > 0 &&
                data.WaveIndex < balance.Waves.Count && data.CompletedWaves == data.WaveIndex);
            CheckpointRequire(data.Ink >= 0 && data.Kills >= 0 && data.GoldEarned >= 0 &&
                data.NextStructureId > 0 && data.NextStructureId < int.MaxValue && data.NextEnemyId > 0 && data.NextEnemyId < int.MaxValue);
            CheckpointRequire(CheckpointPositive(data.BaseHealth) && data.BaseHealth <= balance.BaseHealth &&
                CheckpointTime(data.Countdown) && data.Countdown <= balance.IntermissionSeconds && CheckpointTime(data.Elapsed));
            CheckpointRequire(data.TickIndex > 0 && data.TickIndex <= 1000000000000 &&
                data.RandomDrawCount >= 0 && data.RandomDrawCount <= CheckpointRandom.MaximumReplayDraws);
            CheckpointRequire(data.TopologyVersion >= 0 && data.StructureVisualVersion >= 0 && data.FlowRebuildCount >= 0 &&
                data.EventSequence >= 0 && data.EventSequence < long.MaxValue && data.DroppedEvents >= 0 && data.DroppedEvents <= data.EventSequence &&
                data.NextActionId >= 0 && data.NextActionId < long.MaxValue);
            CheckpointRequire(CheckpointNumber(data.SupplyRemainder) && data.SupplyRemainder >= -.0000011 && data.SupplyRemainder < 1.000001 &&
                CheckpointPositive(data.FireMultiplier) && CheckpointPositive(data.DamageMultiplier) && CheckpointPositive(data.HealthMultiplier) &&
                CheckpointNonNegative(data.SalvageBonus) && CheckpointPositive(data.SupplyMultiplier) && CheckpointPositive(data.WaveRewardMultiplier));
            CheckpointRequire(data.ColoredInk != null && data.ColoredInk.Length == 4 && data.ColoredInk[0] == 0);
            foreach (int amount in data.ColoredInk) CheckpointRequire(amount >= 0);
            CheckpointRequire(data.ClaimedInkFlasks >= 0 && data.ClaimedInkFlasks <= data.CompletedWaves &&
                CheckpointTime(data.BlotterCooldownRemaining) && data.BlotterCooldownRemaining <= balance.BlotterCooldown);
            var mechanics = MechanicsAvailability.Resolve(balance);
            CheckpointRequire(mechanics.WaveInkFlasks || data.ClaimedInkFlasks == 0);
            CheckpointRequire(!data.PendingUpgrade || mechanics.RunUpgrades && balance.Progression.UpgradeEveryWaves > 0 &&
                data.WaveIndex <= balance.Progression.LastUpgradeWave && data.WaveIndex % balance.Progression.UpgradeEveryWaves == 0);
            CheckpointRequire(data.Structures != null && data.Structures.Length <= MaximumCheckpointStructures &&
                data.Structures.Length <= (long)balance.Width * balance.Height && data.Wells != null &&
                data.Wells.Length == (mechanics.InkWells ? balance.WellPlacements.Count : 0) && data.Blots != null &&
                data.Blots.Length <= (long)balance.Width * balance.Height && (mechanics.CorpseBlots || data.Blots.Length == 0));
            CheckpointRequire(data.Statistics != null);
        }

        void RestoreCheckpointState(GameSessionCheckpointData data)
        {
            RunId = Guid.ParseExact(data.RunId, "N"); Phase = data.Phase; WaveIndex = data.WaveIndex;
            CompletedWaves = data.CompletedWaves; Ink = data.Ink; Kills = data.Kills; GoldEarned = data.GoldEarned;
            BaseHealth = data.BaseHealth; TickIndex = data.TickIndex; Countdown = data.Countdown; Elapsed = data.Elapsed;
            PendingUpgrade = data.PendingUpgrade; nextStructureId = data.NextStructureId; nextEnemyId = data.NextEnemyId;
            supplyRemainder = data.SupplyRemainder; fireMultiplier = data.FireMultiplier; damageMultiplier = data.DamageMultiplier;
            healthMultiplier = data.HealthMultiplier; salvageBonus = data.SalvageBonus; supplyMultiplier = data.SupplyMultiplier;
            waveRewardMultiplier = data.WaveRewardMultiplier; claimedInkFlasks = data.ClaimedInkFlasks;
            Array.Copy(data.ColoredInk, coloredInk, coloredInk.Length); BlotterCooldownRemaining = data.BlotterCooldownRemaining;
            Statistics.RestoreCheckpoint(data.Statistics);
            var ids = new HashSet<int>();
            foreach (var saved in data.Structures)
            {
                CheckpointRequire(saved != null && ids.Add(saved.Id));
                var structure = RestoreStructureCheckpoint(saved);
                CheckpointRequire(grid[structure.Cell.Y * rules.Width + structure.Cell.X] == null);
                structures.Add(structure); grid[structure.Cell.Y * rules.Width + structure.Cell.X] = structure;
                navigationBands[structure.Cell.Y * rules.Width + structure.Cell.X] = IsBlocker(structure) ? WeightedFlowField.HealthBand(structure) : 0;
            }
            for (int i = 0; i < wells.Count; i++)
            {
                var saved = data.Wells[i]; var well = wells[i];
                CheckpointRequire(saved != null && saved.Id == i && saved.X == well.Cell.X && saved.Y == well.Cell.Y && saved.Color == well.Color &&
                    saved.MaxHealth == well.MaxHealth && CheckpointNonNegative(saved.Health) && saved.Health <= saved.MaxHealth &&
                    (!saved.Active || saved.Health > 0) && saved.EligibleWave >= 0);
                well.Health = saved.Health; well.Active = saved.Active; well.Threatened = saved.Threatened; well.EligibleWave = saved.EligibleWave;
            }
            var blotCells = new HashSet<Cell>();
            foreach (var saved in data.Blots)
            {
                CheckpointRequire(saved != null && CheckpointPositive(saved.Remaining) && saved.Remaining <= MaximumCheckpointSeconds);
                var cell = new Cell(saved.X, saved.Y); CheckpointRequire(Inside(cell) && blotCells.Add(cell));
                GetCorpseBlots().Add(cell, saved.Remaining);
            }
            // A completed wave has no live generator eligibility. Seal its watermark without
            // paying any structure; the next BeginWave will capture the restored live entities.
            generatorLedger.BeginWave(RunId, WaveIndex, Array.Empty<StructureState>());
            generatorLedger.CompleteWave(RunId, WaveIndex, Array.Empty<StructureState>());
            flowDirty = data.FlowDirty;
            if (!flowDirty) RebuildNavigation();
            TopologyVersion = data.TopologyVersion; StructureVisualVersion = data.StructureVisualVersion; FlowRebuildCount = data.FlowRebuildCount;
            random.RestoreDrawCount(data.RandomDrawCount); weapons.RestoreCheckpointClock(Elapsed, data.NextActionId);
            // Corpse reconstruction may have produced private candidate events. No saved or
            // reconstruction events become visible to audio/rendering after publication.
            EventBuffer.RestoreCheckpointSequence(data.EventSequence, data.DroppedEvents);
        }
    }
}
