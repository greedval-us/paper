using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    internal sealed partial class WeaponAttackResolver
    {
        readonly List<MortarProjectile> projectiles = new List<MortarProjectile>(32);
        int nextProjectileId = 1;
        internal IReadOnlyList<MortarProjectile> Projectiles => projectiles;

        void LaunchMortar(Point2 from, Point2 to, MortarFootprint footprint, bool compositeCycle, in Hit hit)
        {
            var definition = hit.Weapon.Numeric;
            float duration = Math.Max(0, definition.FlightSeconds);
            var projectile = new MortarProjectile {
                Id = nextProjectileId++, From = from, To = to, Duration = duration, Remaining = duration,
                Radius = Math.Max(0, definition.Radius), Damage = hit.Damage,
                Weapon = hit.Kind, WeaponId = hit.Weapon.Id, Definition = hit.Weapon, CompositeCycle = compositeCycle,
                Color = hit.Payload.Color, EffectPayload = hit.Payload, ColorBudget = hit.ColorBudget,
                SourceStructureId = hit.SourceId, MountSlotIndex = hit.Mount, ActionId = hit.ActionId,
                ImpactFootprint = FootprintGeometry.Resolve(footprint, definition.Radius, from, to)
            };
            projectiles.Add(projectile);
            hit.Events.Publish(hit.Tick, SimulationEventKind.ProjectileLaunched, from, to, hit.Kind,
                projectile.Radius, projectile.Id, duration, projectile.ImpactFootprint, hit.Weapon.Id,
                hit.SourceId, hit.Mount, hit.ActionId, hit.Time, attack: hit.Weapon.Attack, colorEffect: hit.Payload);
        }

        internal void AdvanceLegacyProjectiles(float delta, long tick, double time, RunStatistics statistics, SimulationEventBuffer events)
        {
            int write = 0;
            for (int read = 0; read < projectiles.Count; read++)
            {
                var projectile = projectiles[read];
                if (projectile.CompositeCycle) { projectiles[write++] = projectile; continue; }
                projectile.Remaining = Math.Max(0, projectile.Remaining - delta);
                if (projectile.Remaining > .000001f) { projectiles[write++] = projectile; continue; }
                Impact(projectile, tick, time, statistics, events);
            }
            if (write < projectiles.Count) projectiles.RemoveRange(write, projectiles.Count - write);
        }

        internal bool ResolveNextCompositeImpact(long tick, double time, RunStatistics statistics, SimulationEventBuffer events)
        {
            for (int i = 0; i < projectiles.Count; i++)
            {
                var projectile = projectiles[i];
                if (!projectile.CompositeCycle || projectile.Remaining > .000001f) continue;
                Impact(projectile, tick, time, statistics, events);
                projectiles.RemoveAt(i);
                return true;
            }
            return false;
        }

        internal float NextCompositeImpact(float step)
        {
            foreach (var projectile in projectiles)
                if (projectile.CompositeCycle) step = Math.Min(step, projectile.Remaining);
            return step;
        }

        internal void AdvanceCompositeProjectiles(float step)
        {
            foreach (var projectile in projectiles)
                if (projectile.CompositeCycle) projectile.Remaining = Math.Max(0, projectile.Remaining - step);
        }

        void Impact(MortarProjectile projectile, long tick, double time, RunStatistics statistics, SimulationEventBuffer events)
        {
            var hit = new Hit {
                Weapon = projectile.Definition, Damage = projectile.Damage, Payload = projectile.EffectPayload,
                ColorBudget = projectile.ColorBudget, SourceId = projectile.SourceStructureId,
                Mount = projectile.MountSlotIndex, ActionId = projectile.ActionId,
                Tick = tick, Time = time, Statistics = statistics, Events = events
            };
            var geometry = projectile.ImpactFootprint;
            bool shaped = geometry.HasValue && !geometry.Value.IsCircle;
            // Keep the broad phase conservative at the footprint's floating-point boundary.
            float queryRadius = shaped
                ? (float)Math.Min(float.MaxValue, (double)geometry.Value.BoundingRadius * 1.000001)
                : projectile.Radius;
            spatial.Query(projectile.To, queryRadius, splashTargets);
            if (hit.ColorBudget != null) SortTargets(projectile.To, splashTargets);
            foreach (var victim in splashTargets)
            {
                if (shaped && !geometry.Value.Contains(projectile.To, victim.Position)) continue;
                ApplyDamage(victim, hit);
            }
            events.Publish(tick, SimulationEventKind.ProjectileImpact, projectile.From, projectile.To,
                hit.Kind, projectile.Radius, projectile.Id, impactFootprint: geometry, weaponId: hit.Weapon.Id,
                sourceStructureId: hit.SourceId, mountSlotIndex: hit.Mount, actionId: hit.ActionId,
                simulationSeconds: time, attack: AttackKind.AreaProjectile, colorEffect: hit.Payload);
        }
    }
}
