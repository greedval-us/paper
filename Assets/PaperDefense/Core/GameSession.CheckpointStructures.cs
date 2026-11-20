using System;

namespace PaperDefense.Core
{
    public sealed partial class GameSession
    {
        static StructureCheckpointData CaptureStructureCheckpoint(StructureState tower)
        {
            var data = new StructureCheckpointData {
                Id = tower.Id, X = tower.Cell.X, Y = tower.Cell.Y, Kind = tower.Kind, Tier = tower.Tier,
                Color = tower.Color, Priority = tower.Priority, ContentId = tower.ContentId,
                SourceAlbumEntryId = tower.SourceAlbumEntryId, PaidInk = tower.PaidInk, GeneratorPaidWaves = tower.GeneratorPaidWaves,
                Health = tower.Health, MaxHealth = tower.MaxHealth, BuildRemaining = tower.BuildRemaining,
                IsComplete = tower.IsComplete, WaitingForClearance = tower.WaitingForClearance,
                FacingDegrees = tower.FacingDegrees, AimX = tower.Aim.X, AimY = tower.Aim.Y,
                NextSupportFeedbackAt = tower.nextSupportFeedbackAt, NextSpikePulseAt = tower.NextSpikePulseAt,
                NextPrismPulseAt = tower.NextPrismPulseAt, NextPrismColor = tower.NextPrismColor,
                HasRecipe = tower.ResolvedEntry != null, HasBlueprint = tower.Blueprint != null,
                HasModernization = tower.ModernizationState != null, HasColorBudget = tower.ColorBudget != null,
                Recipe = tower.ResolvedEntry == null ? null : new RecipeSnapshotData(tower.ResolvedEntry.Snapshot),
                Blueprint = tower.Blueprint == null ? null : new BlueprintData(tower.Blueprint),
                ColorBudget = tower.ColorBudget?.CaptureCheckpoint(),
                Mounts = new MountCheckpointData[TowerBlueprint.MaximumWeaponMounts]
            };
            for (int slot = 0; slot < data.Mounts.Length; slot++)
            {
                var mount = tower.GetMountState(slot); var cycle = mount.Cycle;
                data.Mounts[slot] = new MountCheckpointData {
                    AimX = mount.Aim.X, AimY = mount.Aim.Y, TargetId = mount.TargetId, Cooldown = mount.Cooldown,
                    AcquisitionRemaining = mount.AcquisitionRemaining, ActionRemaining = mount.ActionRemaining,
                    CycleInitialized = cycle.Initialized, RoundsRemaining = cycle.RoundsRemaining,
                    IsReloading = cycle.IsReloading, RemainingWork = cycle.RemainingWork
                };
            }
            if (tower.ModernizationState != null)
            {
                var state = tower.ModernizationState; var ranks = state.Ranks; var pending = state.Pending;
                data.Modernization = new ModernizationCheckpointData {
                    Power = ranks.Power, Mechanics = ranks.Mechanics, Range = ranks.Range, Resilience = ranks.Resilience,
                    Color = ranks.Color, ColorRank = ranks.ColorRank, Revision = state.Revision,
                    HasPending = pending != null, PendingIsColor = pending?.IsColor ?? false,
                    PendingDirection = pending?.Direction ?? default, PendingColor = pending?.Color ?? InkColor.None,
                    Remaining = state.Remaining
                };
            }
            return data;
        }

        StructureState RestoreStructureCheckpoint(StructureCheckpointData data)
        {
            CheckpointRequire(data.Id > 0 && data.Id < nextStructureId && data.X > 0 && data.Y > 0 &&
                data.X < rules.Width - 1 && data.Y < rules.Height - 1 &&
                (Math.Abs(data.X - BaseCell.X) > 1 || Math.Abs(data.Y - BaseCell.Y) > 1));
            CheckpointRequire(Enum.IsDefined(typeof(MaterialTier), data.Tier) && Enum.IsDefined(typeof(InkColor), data.Color) &&
                Enum.IsDefined(typeof(TargetPriority), data.Priority) && data.PaidInk >= 0 && data.GeneratorPaidWaves >= 0 &&
                data.GeneratorPaidWaves <= CompletedWaves && !string.IsNullOrEmpty(data.ContentId) && data.ContentId.Length <= 128 &&
                (string.IsNullOrEmpty(data.SourceAlbumEntryId) || data.SourceAlbumEntryId.Length <= ProfileLimits.MaximumBlueprintIdLength));
            CheckpointRequire(CheckpointPositive(data.Health) && CheckpointPositive(data.MaxHealth) && data.Health <= data.MaxHealth &&
                CheckpointTime(data.BuildRemaining) && (!data.IsComplete || data.BuildRemaining == 0) &&
                (!data.WaitingForClearance || !data.IsComplete && data.BuildRemaining == 0));
            CheckpointRequire(CheckpointNumber(data.FacingDegrees) && data.FacingDegrees >= 0 && data.FacingDegrees < 360 &&
                CheckpointNumber(data.AimX) && CheckpointNumber(data.AimY) &&
                CheckpointNonNegative(data.NextSupportFeedbackAt) && CheckpointNonNegative(data.NextSpikePulseAt) &&
                CheckpointNonNegative(data.NextPrismPulseAt) &&
                (data.NextPrismColor == InkColor.Red || data.NextPrismColor == InkColor.Blue || data.NextPrismColor == InkColor.Green));
            CheckpointRequire(data.Mounts != null && data.Mounts.Length == TowerBlueprint.MaximumWeaponMounts &&
                !(data.HasRecipe && data.HasBlueprint) && (Mechanics.ColorCartridges || data.Color == InkColor.None));
            CheckpointRequire((data.HasRecipe || !AssemblyProfileDataCopy.HasSnapshot(data.Recipe)) &&
                (data.HasBlueprint || !AssemblyProfileDataCopy.HasBlueprint(data.Blueprint)) &&
                (data.HasModernization || EmptyModernizationCheckpoint(data.Modernization)) &&
                (data.HasColorBudget || EmptyColorBudgetCheckpoint(data.ColorBudget)));

            StructureState tower;
            if (data.HasRecipe)
            {
                CheckpointRequire(data.Recipe != null && data.Recipe.Definitions != null && data.Recipe.Definitions.Length <= 64);
                var snapshot = data.Recipe.CreateSnapshot();
                var result = AssemblyResolver.Resolve(snapshot, rules, data.Tier);
                CheckpointRequire(result.Valid);
                tower = new StructureState(result.Value);
                CheckpointRequire(tower.Kind == data.Kind && tower.ContentId == data.ContentId);
            }
            else
            {
                tower = new StructureState { Kind = data.Kind, Tier = data.Tier, ContentId = data.ContentId };
                if (data.HasBlueprint)
                {
                    CheckpointRequire(data.Blueprint != null);
                    var blueprint = data.Blueprint.ToBlueprint().WithContent(rules.Content);
                    CheckpointRequire(TowerCatalog.Validate(blueprint, rules, out _));
                    tower.Blueprint = blueprint; tower.Assembly = TowerCatalog.Resolve(blueprint, rules.Content);
                    CheckpointRequire(blueprint.FirstWeapon == data.Kind && tower.Assembly.First.Id == data.ContentId);
                    if (Mechanics.FieldModernization) tower.LegacyFieldEntry = tower.Assembly.CreateLegacySnapshot(rules, data.Tier);
                }
                else
                {
                    CheckpointRequire(Enum.IsDefined(typeof(StructureKind), data.Kind));
                    var definition = rules.Structure(data.Kind);
                    CheckpointRequire(data.ContentId == LegacyContentIds.Weapon(data.Kind));
                    if (Mechanics.FieldModernization && !ConstructionToolRules.IsTool(data.Kind) &&
                        weapons.TryGetDirectWeapon(data.Kind, out var weapon))
                        tower.LegacyFieldEntry = ResolvedTowerAssembly.CaptureDirectTower(weapon, definition, rules, data.Tier);
                }
            }
            tower.Id = data.Id; tower.Cell = new Cell(data.X, data.Y);
            tower.SourceAlbumEntryId = string.IsNullOrEmpty(data.SourceAlbumEntryId) ? null : data.SourceAlbumEntryId;
            CheckpointRequire(!ReservedObjective(tower.Cell) && (Battlefield == null ||
                Battlefield.CanPlace(tower.Cell, data.HasBlueprint ? StructureKind.Gun : data.Kind)));
            tower.Color = data.Color; tower.Priority = data.Priority; tower.PaidInk = data.PaidInk;
            tower.GeneratorPaidWaves = data.GeneratorPaidWaves;
            if (tower.HasDirectionalWeapon) tower.InitializePlacementFacing(data.FacingDegrees);
            else CheckpointRequire(data.FacingDegrees == 0);
            if (data.HasModernization) RestoreModernizationCheckpoint(tower, data.Modernization);
            CheckpointRequire(!Mechanics.FieldModernization || tower.ModernizationEntry == null || data.Color == InkColor.None || data.HasModernization);
            float expectedHealth = tower.ModernizationEntry != null ? tower.ModernizationEntry.Health * healthMultiplier :
                tower.ResolvedEntry != null ? tower.ResolvedEntry.Health * healthMultiplier : StructureHealth(tower, data.Tier);
            CheckpointRequire(CheckpointPositive(expectedHealth) && Math.Abs((double)data.MaxHealth - expectedHealth) <=
                Math.Max(.0001, expectedHealth * .00001));
            tower.Health = data.Health; tower.MaxHealth = data.MaxHealth; tower.BuildRemaining = data.BuildRemaining;
            tower.IsComplete = data.IsComplete; tower.WaitingForClearance = data.WaitingForClearance;
            tower.Aim = new Point2(data.AimX, data.AimY); tower.nextSupportFeedbackAt = data.NextSupportFeedbackAt;
            tower.NextSpikePulseAt = data.NextSpikePulseAt; tower.NextPrismPulseAt = data.NextPrismPulseAt; tower.NextPrismColor = data.NextPrismColor;
            for (int slot = 0; slot < data.Mounts.Length; slot++) RestoreMountCheckpoint(tower, slot, data.Mounts[slot]);
            if (data.HasColorBudget)
            {
                CheckpointRequire(data.ColorBudget != null);
                float seconds = Mechanics.FieldModernization ? 1 : rules.Effects.ColorWindowSeconds;
                int limit = Mechanics.FieldModernization ? 3 + Math.Max(0, tower.Modernization.ColorRank - 1) : rules.Effects.ColorTargetsPerWindow;
                CheckpointRequire(data.ColorBudget.WindowSeconds == seconds && data.ColorBudget.TargetLimit == limit);
                tower.ColorBudget = TowerColorBudget.RestoreCheckpoint(data.ColorBudget, nextEnemyId);
            }
            return tower;
        }

        static bool EmptyModernizationCheckpoint(ModernizationCheckpointData data) => data == null ||
            data.Power == 0 && data.Mechanics == 0 && data.Range == 0 && data.Resilience == 0 && data.ColorRank == 0 &&
            data.Revision == 0 && data.Color == InkColor.None && !data.HasPending && !data.PendingIsColor &&
            data.PendingDirection == default && data.PendingColor == InkColor.None && data.Remaining == 0;
        static bool EmptyColorBudgetCheckpoint(ColorBudgetCheckpointData data) => data == null ||
            data.WindowSeconds == 0 && data.TargetLimit == 0 && !data.HasWindow && data.NextWindow == 0 &&
            (data.Affected == null || data.Affected.Length == 0);

        void RestoreMountCheckpoint(StructureState tower, int slot, MountCheckpointData data)
        {
            CheckpointRequire(data != null && CheckpointNumber(data.AimX) && CheckpointNumber(data.AimY) &&
                data.TargetId >= -rules.Width * rules.Height && data.TargetId < nextEnemyId && CheckpointTime(data.Cooldown) &&
                CheckpointTime(data.AcquisitionRemaining) && CheckpointTime(data.ActionRemaining) &&
                CheckpointNonNegative(data.RemainingWork) && data.RemainingWork <= 1.0000001 && data.RoundsRemaining >= 0);
            if (data.TargetId < 0)
            {
                bool cleanup = false;
                if (tower.ResolvedEntry != null)
                {
                    foreach (var weapon in tower.ResolvedEntry.Weapons)
                        if (weapon.SlotIndex == slot && weapon.Attack == AttackKind.Cleanup) cleanup = true;
                }
                else if (tower.Assembly != null) cleanup = tower.Assembly.Weapons[slot]?.Attack == AttackKind.Cleanup;
                else if (slot == 0 && weapons.TryGetDirectWeapon(tower.Kind, out var direct)) cleanup = direct.Attack == AttackKind.Cleanup;
                CheckpointRequire(cleanup);
            }
            if (data.CycleInitialized)
            {
                ResolvedWeapon weapon = null;
                if (tower.ResolvedEntry != null)
                    foreach (var candidate in tower.ResolvedEntry.Weapons) if (candidate.SlotIndex == slot) weapon = candidate;
                CheckpointRequire(weapon != null && data.RoundsRemaining <= weapon.MagazineSize &&
                    (data.IsReloading ? data.RoundsRemaining == 0 && data.RemainingWork > 0 : data.RoundsRemaining > 0));
            }
            else CheckpointRequire(data.RoundsRemaining == 0 && !data.IsReloading && data.RemainingWork == 0);
            var mount = tower.GetMountState(slot);
            mount.Aim = new Point2(data.AimX, data.AimY); mount.TargetId = data.TargetId; mount.Cooldown = data.Cooldown;
            mount.AcquisitionRemaining = data.AcquisitionRemaining; mount.ActionRemaining = data.ActionRemaining;
            mount.Cycle = data.CycleInitialized ? new WeaponCycleState(data.RoundsRemaining, data.IsReloading, data.RemainingWork) : new WeaponCycleState();
        }

        void RestoreModernizationCheckpoint(StructureState tower, ModernizationCheckpointData data)
        {
            CheckpointRequire(Mechanics.FieldModernization && tower.ModernizationEntry != null && data != null &&
                data.Revision > 0 && data.Revision < int.MaxValue && CheckpointTime(data.Remaining));
            var ranks = new ModernizationRanks(data.Power, data.Mechanics, data.Range, data.Resilience, data.Color, data.ColorRank);
            int cap = Mechanics.MaximumModernizationRank;
            CheckpointRequire(data.Power <= cap && data.Mechanics <= cap && data.Range <= cap && data.Resilience <= cap && data.ColorRank <= cap &&
                tower.Color == ranks.Color && (data.Color == InkColor.None || Mechanics.ColorCartridges));
            var baseline = tower.ModernizationEntry;
            var effective = ModernizationRules.Resolve(baseline, ranks);
            if (tower.LegacyFieldEntry != null) tower.LegacyFieldEntry = effective; else tower.ResolvedEntry = effective;
            var state = new ModernizationState(baseline) { Ranks = ranks, Revision = data.Revision, Remaining = data.Remaining };
            tower.ModernizationState = state;
            if (!data.HasPending) { CheckpointRequire(data.Remaining == 0); return; }
            CheckpointRequire(Enum.IsDefined(typeof(ModernizationDirection), data.PendingDirection));
            int current; ModernizationRanks result; int cost;
            if (data.PendingIsColor)
            {
                CheckpointRequire(data.PendingColor != InkColor.None && Enum.IsDefined(typeof(InkColor), data.PendingColor) &&
                    data.PendingColor == ranks.Color && Mechanics.ColorCartridges);
                current = ranks.ColorRank; result = ranks.WithColor(data.PendingColor, current + 1);
                cost = ModernizationRules.ColorCost(baseline.Cost, current + 1);
            }
            else
            {
                CheckpointRequire(data.PendingColor == InkColor.None);
                current = ranks.Rank(data.PendingDirection); result = ranks.Next(data.PendingDirection);
                cost = ModernizationRules.OrdinaryCost(baseline.Cost, data.PendingDirection, current + 1);
            }
            CheckpointRequire(current + 1 <= cap && data.Remaining > 0 && data.Remaining <= ModernizationRules.WorkSeconds(current + 1));
            var beforeColor = ModernizationRules.ResolveColor(ColorEffectPayload.Capture(ranks.Color, rules.Content), ranks.ColorRank);
            var afterColor = data.PendingIsColor ? ModernizationRules.ResolveColor(ColorEffectPayload.Capture(data.PendingColor, rules.Content), current + 1) : beforeColor;
            state.Pending = new ModernizationQuote(RunId, tower.Id, data.Revision - 1, data.PendingDirection, data.PendingColor,
                data.PendingIsColor, current, current + 1, cost, ModernizationRules.WorkSeconds(current + 1), effective,
                data.PendingIsColor ? effective : ModernizationRules.Resolve(baseline, result), beforeColor, afterColor, result, "");
        }
    }
}
