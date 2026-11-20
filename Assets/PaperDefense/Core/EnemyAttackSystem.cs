using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    // Zero preserves the attack of enemy definitions saved before attacks were configurable.
    public enum EnemyAttackMode { Default = 0, Contact = 1, Glob = 2 }

    internal interface IEnemyAttackWorld
    {
        long TickIndex { get; }
        float Elapsed { get; }
        SimulationEventBuffer EventBuffer { get; }
        Point2 ObjectivePosition(EnemyState enemy);
        bool TraceBlocker(Point2 from, Point2 to, out StructureState blocker, out Point2 impact);
        void HitObjective(int index, float damage, Point2 source);
        void HitStructure(StructureState blocker, Point2 source, float rawDamage);
    }

    /// <summary>Decides when enemies hold to attack and advances their in-flight attacks.</summary>
    internal sealed class EnemyAttackSystem
    {
        readonly CombatTuningRules combat;
        readonly bool rangedEnabled;
        readonly int projectileLimit;
        readonly IEnemyAttackWorld world;
        readonly List<EnemyGlob> projectiles = new List<EnemyGlob>(64);
        int nextProjectileId = 1;

        internal IReadOnlyList<EnemyGlob> Projectiles => projectiles;

        internal EnemyAttackSystem(GameBalance rules, bool rangedEnabled, IEnemyAttackWorld world)
        {
            combat = rules.Combat;
            projectileLimit = rules.Width * rules.Height * Math.Max(1, rules.MaxEnemiesPerCell);
            this.rangedEnabled = rangedEnabled;
            this.world = world;
        }

        internal static bool UsesGlob(EnemyDefinition definition, bool rangedEnabled)
        {
            if (!rangedEnabled) return false;
            if (definition.Attack == EnemyAttackMode.Default) return definition.Kind == EnemyKind.Spitter;
            return definition.Attack == EnemyAttackMode.Glob;
        }

        // True reserves this step for an attack, including its windup or cooldown in range.
        internal bool TryEngage(EnemyState enemy, EnemyDefinition definition, float delta)
        {
            enemy.AttackCooldown = Math.Max(0, enemy.AttackCooldown - delta);
            if (UsesGlob(definition, rangedEnabled) && TryGlob(enemy, definition, delta))
                return true;

            var objective = world.ObjectivePosition(enemy);
            float range = combat.ObjectiveContactDistance;
            if (DistanceSquared(enemy.Position, objective) > range * range) return false;
            if (enemy.AttackCooldown <= 0)
            {
                PublishAttack(enemy, objective);
                world.HitObjective(enemy.ObjectiveIndex, Math.Max(0, definition.Damage * enemy.ObjectiveDamageScale), enemy.Position);
                enemy.AttackCooldown = Math.Max(combat.MinimumActionInterval, definition.AttackInterval);
            }
            return true;
        }

        internal void StrikeBlocker(EnemyState enemy, EnemyDefinition definition, StructureState blocker)
        {
            if (enemy.AttackCooldown > 0) return;
            float rawDamage = Math.Max(0, definition.Damage) * Math.Max(0, definition.StructureDamageMultiplier);
            PublishAttack(enemy, Center(blocker.Cell));
            world.HitStructure(blocker, enemy.Position, rawDamage);
            enemy.AttackCooldown = Math.Max(combat.MinimumActionInterval, definition.AttackInterval);
        }

        bool TryGlob(EnemyState enemy, EnemyDefinition definition, float delta)
        {
            enemy.RangedCooldown = Math.Max(0, enemy.RangedCooldown - delta);
            if (enemy.WindingUp)
            {
                enemy.WindupRemaining = Math.Max(0, enemy.WindupRemaining - delta);
                if (enemy.WindupRemaining > .000001f) return true;
                if (projectiles.Count >= projectileLimit) return true;
                float duration = Math.Max(0, definition.ProjectileSeconds);
                var glob = new EnemyGlob {
                    Id = nextProjectileId++, From = enemy.Position, To = enemy.RangedTarget,
                    Remaining = duration, Duration = duration, Damage = Math.Max(0, definition.Damage),
                    ObjectiveDamageScale = enemy.ObjectiveDamageScale, ObjectiveIndex = enemy.RangedObjectiveIndex,
                    SourceEnemyKind = enemy.Kind, SourceEnemyDrawingTier = enemy.DrawingTier
                };
                projectiles.Add(glob);
                PublishAttack(enemy, glob.To);
                world.EventBuffer.Publish(world.TickIndex, SimulationEventKind.EnemyGlobLaunched, glob.From, glob.To, default,
                    entityId: glob.Id, travelSeconds: duration, simulationSeconds: world.Elapsed, enemyKind: enemy.Kind,
                    enemyDrawingTier: enemy.DrawingTier, enemyVisualScale: enemy.VisualScale);
                enemy.WindingUp = false;
                enemy.RangedCooldown = Math.Max(combat.MinimumActionInterval, definition.AttackInterval);
                return true;
            }

            var target = world.ObjectivePosition(enemy);
            int objective = enemy.ObjectiveIndex;
            if (world.TraceBlocker(enemy.Position, target, out var blocker, out _))
            {
                target = Center(blocker.Cell);
                objective = int.MinValue;
            }
            float range = Math.Max(combat.MinimumRangedDistance, definition.Range);
            if (DistanceSquared(enemy.Position, target) > range * range) return false;
            if (enemy.RangedCooldown <= 0)
            {
                enemy.RangedTarget = target;
                enemy.RangedObjectiveIndex = objective;
                enemy.WindingUp = true;
                enemy.WindupRemaining = Math.Max(0, definition.WindupSeconds);
            }
            return true;
        }

        internal void AdvanceProjectiles(float delta)
        {
            int write = 0;
            for (int read = 0; read < projectiles.Count; read++)
            {
                var glob = projectiles[read];
                float previous = glob.Duration <= .000001f ? 0 : 1 - glob.Remaining / glob.Duration;
                glob.Remaining = Math.Max(0, glob.Remaining - delta);
                float current = glob.Duration <= .000001f ? 1 : 1 - glob.Remaining / glob.Duration;
                var from = Point2.Lerp(glob.From, glob.To, previous);
                var to = Point2.Lerp(glob.From, glob.To, current);
                if (world.TraceBlocker(from, to, out var blocker, out var impact))
                {
                    PublishImpact(glob, impact);
                    world.HitStructure(blocker, glob.From, glob.Damage);
                    continue;
                }
                if (glob.Remaining <= .000001f)
                {
                    PublishImpact(glob, glob.To);
                    if (!glob.TargetsStructure) world.HitObjective(glob.ObjectiveIndex, glob.Damage * glob.ObjectiveDamageScale, glob.From);
                    continue;
                }
                projectiles[write++] = glob;
            }
            if (write < projectiles.Count) projectiles.RemoveRange(write, projectiles.Count - write);
        }

        void PublishAttack(EnemyState enemy, Point2 target)
            => world.EventBuffer.Publish(world.TickIndex, SimulationEventKind.EnemyAttack, enemy.Position, target,
                default, entityId: enemy.Id, simulationSeconds: world.Elapsed, enemyKind: enemy.Kind,
                enemyDrawingTier: enemy.DrawingTier, enemyVisualScale: enemy.VisualScale);

        void PublishImpact(EnemyGlob glob, Point2 impact)
            => world.EventBuffer.Publish(world.TickIndex, SimulationEventKind.EnemyGlobImpact, glob.From, impact,
                default, entityId: glob.Id, simulationSeconds: world.Elapsed, enemyKind: glob.SourceEnemyKind,
                enemyDrawingTier: glob.SourceEnemyDrawingTier);

        static Point2 Center(Cell cell) => new Point2(cell.X + .5f, cell.Y + .5f);

        static float DistanceSquared(Point2 from, Point2 to)
        {
            float dx = from.X - to.X, dy = from.Y - to.Y;
            return dx * dx + dy * dy;
        }
    }
}
