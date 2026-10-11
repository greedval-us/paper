using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>
    /// Target selection and weapon geometry live here, independently of wave and construction rules.
    /// Add new weapon effects in this system; all queries share its reusable spatial buckets.
    /// </summary>
    internal sealed class WeaponSystem
    {
        private readonly GameBalance balance;
        private readonly EnemySpatialIndex spatial;
        private readonly List<EnemyState> targets = new List<EnemyState>(128);
        private readonly List<EnemyState> splashTargets = new List<EnemyState>(64);
        private readonly List<MortarProjectile> projectiles = new List<MortarProjectile>(32);
        private int nextProjectileId = 1;

        internal IReadOnlyList<MortarProjectile> Projectiles => projectiles;
        internal bool HasPendingProjectiles => projectiles.Count != 0;

        internal WeaponSystem(GameBalance balance)
        { this.balance = balance; spatial = new EnemySpatialIndex(balance.Width, balance.Height); }

        internal void Fire(List<StructureState> structures, List<EnemyState> enemies, List<ShotEvent> shots,
            float fireMultiplier, float damageMultiplier, float delta, long tick,
            RunStatistics statistics, SimulationEventBuffer events)
        {
            spatial.Rebuild(enemies);
            // Movement has already finished for this step. Resolve only shells launched on
            // earlier steps, so a newly launched shell retains its complete flight time.
            AdvanceProjectiles(delta, tick, statistics, events);
            foreach (var structure in structures)
            {
                if (structure.Health <= 0 || !structure.IsComplete || structure.BuildRemaining > 0 || structure.Kind == StructureKind.Wall) continue;
                var definition = balance.Structure(structure.Kind);
                Point2 origin = new Point2(structure.Cell.X + 0.5f, structure.Cell.Y + 0.5f);
                float range = structure.Kind == StructureKind.Spikes ? Math.Max(0.5f, definition.Radius) : Math.Max(0, definition.Range);
                spatial.Query(origin, range, targets);
                EnemyState target = PickTarget(structure, origin, targets);
                // Idle means no live target in range, including time spent cooling down.
                if (target == null) { statistics.RecordIdle(structure.Kind, delta); continue; }
                if (structure.Cooldown > 0) continue;
                float damage = Math.Max(0, definition.Damage * balance.Material(structure.Tier).DamageMultiplier * damageMultiplier);
                structure.Cooldown = Math.Max(0.05f, definition.ShotInterval / Math.Max(0.01f, fireMultiplier));
                structure.Aim = target.Position;
                statistics.RecordLaunch(structure.Kind);
                switch (structure.Kind)
                {
                    case StructureKind.Piercer:
                        FirePiercer(origin, target.Position, range, Math.Max(0.2f, definition.Radius),
                            Math.Max(1, definition.MaxPierce), damage, statistics);
                        break;
                    case StructureKind.Mortar:
                        LaunchMortar(origin, target.Position, definition, damage, tick, events);
                        continue;
                    case StructureKind.Spikes:
                        foreach (var victim in targets) ApplyDamage(victim, damage, structure.Kind, statistics);
                        break;
                    default:
                        ApplyDamage(target, damage, structure.Kind, statistics);
                        break;
                }
                Point2 shotEnd = structure.Kind == StructureKind.Piercer ? RayEnd(origin, structure.Aim, range) : structure.Aim;
                shots.Add(new ShotEvent(origin, shotEnd, structure.Kind, definition.Radius));
                events.Publish(tick, SimulationEventKind.Shot, origin, shotEnd, structure.Kind,
                    definition.Radius, structure.Id);
            }
        }

        private void LaunchMortar(Point2 from, Point2 to, StructureDefinition definition, float damage,
            long tick, SimulationEventBuffer events)
        {
            float duration = Math.Max(0, definition.FlightSeconds);
            var projectile = new MortarProjectile
            {
                Id = nextProjectileId++, From = from, To = to, Duration = duration,
                Remaining = duration, Radius = Math.Max(0, definition.Radius),
                Damage = damage, Weapon = StructureKind.Mortar
            };
            projectiles.Add(projectile);
            events.Publish(tick, SimulationEventKind.ProjectileLaunched, from, to, projectile.Weapon,
                projectile.Radius, projectile.Id, duration);
        }

        private void AdvanceProjectiles(float delta, long tick, RunStatistics statistics, SimulationEventBuffer events)
        {
            int write = 0;
            for (int read = 0; read < projectiles.Count; read++)
            {
                var projectile = projectiles[read];
                projectile.Remaining = Math.Max(0, projectile.Remaining - delta);
                if (projectile.Remaining > 0.000001f) { projectiles[write++] = projectile; continue; }
                spatial.Query(projectile.To, projectile.Radius, splashTargets);
                foreach (var victim in splashTargets)
                    ApplyDamage(victim, projectile.Damage, projectile.Weapon, statistics);
                events.Publish(tick, SimulationEventKind.ProjectileImpact, projectile.From, projectile.To,
                    projectile.Weapon, projectile.Radius, projectile.Id);
            }
            if (write < projectiles.Count) projectiles.RemoveRange(write, projectiles.Count - write);
        }

        private static void ApplyDamage(EnemyState victim, float damage, StructureKind weapon, RunStatistics statistics)
        {
            if (victim.Health <= 0 || damage <= 0) return;
            float actualDamage = Math.Min(victim.Health, damage);
            victim.Health -= actualDamage;
            statistics.RecordDamage(weapon, actualDamage);
        }

        private static EnemyState PickTarget(StructureState structure, Point2 origin, List<EnemyState> candidates)
        {
            EnemyState best = null;
            float bestDistance = float.MaxValue;
            foreach (var candidate in candidates)
            {
                if (candidate.Health <= 0) continue;
                float distance = DistanceSquared(origin, candidate.Position);
                bool preferred = best == null;
                if (best != null)
                {
                    if (structure.Priority == TargetPriority.Siege && (candidate.Kind == EnemyKind.Gnawer) != (best.Kind == EnemyKind.Gnawer))
                        preferred = candidate.Kind == EnemyKind.Gnawer;
                    else if (structure.Priority == TargetPriority.Strongest && candidate.Health != best.Health)
                        preferred = candidate.Health > best.Health;
                    else preferred = distance < bestDistance || (distance == bestDistance && candidate.Id < best.Id);
                }
                if (preferred) { best = candidate; bestDistance = distance; }
            }
            return best;
        }

        private void FirePiercer(Point2 origin, Point2 target, float range, float radius, int maxPierce,
            float damage, RunStatistics statistics)
        {
            float dx = target.X - origin.X, dy = target.Y - origin.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length <= 0.00001f) return;
            dx /= length; dy /= length;
            splashTargets.Clear();
            foreach (var enemy in targets)
            {
                if (enemy.Health <= 0) continue;
                float ex = enemy.Position.X - origin.X, ey = enemy.Position.Y - origin.Y;
                float along = ex * dx + ey * dy;
                float across = Math.Abs(ex * dy - ey * dx);
                if (along >= 0 && along <= range && across <= radius) splashTargets.Add(enemy);
            }
            splashTargets.Sort((a, b) =>
            {
                int comparison = DistanceSquared(origin, a.Position).CompareTo(DistanceSquared(origin, b.Position));
                return comparison != 0 ? comparison : a.Id.CompareTo(b.Id);
            });
            for (int i = 0; i < splashTargets.Count && i < maxPierce; i++)
                ApplyDamage(splashTargets[i], damage, StructureKind.Piercer, statistics);
        }

        private static float DistanceSquared(Point2 a, Point2 b)
        { float dx = a.X - b.X, dy = a.Y - b.Y; return dx * dx + dy * dy; }

        private static Point2 RayEnd(Point2 from, Point2 to, float range)
        {
            float dx = to.X - from.X, dy = to.Y - from.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            return length <= 0.00001f ? to : new Point2(from.X + dx / length * range, from.Y + dy / length * range);
        }
    }
}
