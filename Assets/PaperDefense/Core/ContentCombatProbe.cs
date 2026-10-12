using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>Finite validation lane that exercises the production attack dispatcher against configured stationary targets.</summary>
    public static class ContentCombatProbe
    {
        public const int MaximumTargets = 128, MaximumTicks = 100000;
        public static ContentCombatProbeResult Run(TowerBlueprint blueprint, GameBalance source, Cell towerCell,
            IReadOnlyList<Point2> targetOffsets, float targetHealth, float stepSeconds, int maximumTicks,
            bool stopAfterFirstAction = true, InkColor color = InkColor.None)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (targetOffsets == null || targetOffsets.Count == 0 || targetOffsets.Count > MaximumTargets) throw new ArgumentException("probe.targets: нужен конечный список до " + MaximumTargets + " целей");
            if (maximumTicks < 1 || maximumTicks > MaximumTicks || stepSeconds <= 0 || float.IsNaN(stepSeconds) || float.IsInfinity(stepSeconds)) throw new ArgumentException("probe.duration: неверные условия");
            if (targetHealth <= 0 || float.IsNaN(targetHealth) || float.IsInfinity(targetHealth)) throw new ArgumentException("probe.targetHealth: требуется положительное здоровье");
            var balance = source.CreateSnapshot(); balance.Content = source.ResolveContent();
            blueprint = blueprint.WithContent(balance.Content);
            if (!TowerCatalog.Validate(blueprint, balance, out var reason)) throw new ArgumentException(reason);
            float towerHealth = TowerCatalog.Health(blueprint, balance, MaterialTier.Pencil);
            var tower = new StructureState { Id = 1, Cell = towerCell, Kind = blueprint.FirstWeapon, ContentId = blueprint.FirstWeaponId,
                Blueprint = blueprint, Tier = MaterialTier.Pencil, IsComplete = true, Health = towerHealth, MaxHealth = towerHealth, Color = color };
            var towers = new List<StructureState> { tower }; var empty = new List<StructureState>();
            var enemies = new List<EnemyState>(); var origin = new Point2(towerCell.X + .5f, towerCell.Y + .5f);
            for (int i = 0; i < targetOffsets.Count; i++) enemies.Add(new EnemyState { Id = i + 1, Kind = EnemyKind.Doodle,
                Position = new Point2(origin.X + targetOffsets[i].X, origin.Y + targetOffsets[i].Y), Health = targetHealth, MaxHealth = targetHealth });
            var weapon = new WeaponSystem(balance); var stats = new RunStatistics(); var events = new SimulationEventBuffer(); var shots = new List<ShotEvent>();
            bool action = false; int ticks = 0, primaryActions = 0, secondaryActions = 0;
            for (; ticks < maximumTicks; ticks++)
            {
                tower.Cooldown = Math.Max(0, tower.Cooldown - stepSeconds);
                tower.SecondaryCooldown = Math.Max(0, tower.SecondaryCooldown - stepSeconds);
                float primaryBefore = tower.Cooldown, secondaryBefore = tower.SecondaryCooldown;
                weapon.Fire(stopAfterFirstAction && action ? empty : towers, enemies, shots, 1, 1, stepSeconds, ticks, stats, events);
                if (tower.Cooldown > primaryBefore + .000001f) primaryActions++;
                if (tower.SecondaryCooldown > secondaryBefore + .000001f) secondaryActions++;
                action |= stats.Launches(blueprint.FirstWeaponId) > 0;
                if (stopAfterFirstAction && action && !weapon.HasPendingProjectiles) { ticks++; break; }
            }
            var result = new ContentCombatProbeResult { Ticks = ticks, Launches = stats.Launches(blueprint.FirstWeaponId), Damage = stats.Damage(blueprint.FirstWeaponId),
                PrimaryActions = primaryActions, SecondaryActions = secondaryActions,
                TargetDamage = new float[enemies.Count], BlueRemaining = new float[enemies.Count], RedRemaining = new float[enemies.Count], GreenRemaining = new float[enemies.Count] };
            for (int i = 0; i < enemies.Count; i++) { result.TargetDamage[i] = targetHealth - enemies[i].Health;
                result.BlueRemaining[i] = enemies[i].BlueRemaining; result.RedRemaining[i] = enemies[i].RedRemaining; result.GreenRemaining[i] = enemies[i].GreenRemaining; }
            return result;
        }
    }
    [Serializable] public sealed class ContentCombatProbeResult
    {
        public int Ticks, Launches, PrimaryActions, SecondaryActions;
        public float Damage;
        public float[] TargetDamage, BlueRemaining, RedRemaining, GreenRemaining;
    }
}
