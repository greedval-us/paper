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
        private readonly MechanicsAvailability mechanics;
        private readonly EffectsRules effects;
        private readonly float? qualityOverride;
        private readonly IBlotContext blotContext;
        private readonly EnemySpatialIndex spatial;
        private readonly List<EnemyState> targets = new List<EnemyState>(128);
        private readonly List<EnemyState> splashTargets = new List<EnemyState>(64);
        private readonly List<MortarProjectile> projectiles = new List<MortarProjectile>(32);
        private readonly HashSet<int> ricochetVictims = new HashSet<int>();
        private readonly EnemyDistanceComparer targetOrder = new EnemyDistanceComparer();
        private int nextProjectileId = 1;
        private double clockSeconds;

        internal IReadOnlyList<MortarProjectile> Projectiles => projectiles;
        internal bool HasPendingProjectiles => projectiles.Count != 0;

        internal WeaponSystem(GameBalance balance, float? qualityOverride = null, IBlotContext blotContext = null)
        {
            this.balance = balance;
            mechanics = MechanicsAvailability.Resolve(balance);
            effects = balance.Effects.ForEnemyDrawingTiers(mechanics.EnemyDrawingTiers);
            this.qualityOverride = qualityOverride;
            // An injected fixture may provide marks without campaign corpse generation.
            this.blotContext = balance.Mechanics.CorpseBlots ? blotContext : null;
            spatial = new EnemySpatialIndex(balance.Width, balance.Height);
        }

        internal void Fire(List<StructureState> structures, List<EnemyState> enemies, List<ShotEvent> shots,
            float fireMultiplier, float damageMultiplier, float delta, long tick,
            RunStatistics statistics, SimulationEventBuffer events, double? timeSeconds = null)
        {
            clockSeconds = timeSeconds ?? clockSeconds + delta;
            spatial.Rebuild(enemies);
            // Movement has already finished for this step. Resolve only shells launched on
            // earlier steps, so a newly launched shell retains its complete flight time.
            AdvanceProjectiles(delta, tick, statistics, events);
            foreach (var structure in structures)
            {
                if (!CanOperate(structure)) continue;
                Point2 origin = new Point2(structure.Cell.X + 0.5f, structure.Cell.Y + 0.5f);
                TowerBlueprint blueprint = structure.Blueprint;
                bool dynamic = balance.Construction.DynamicFeed;
                int activeMounts = 1;
                if (dynamic && blueprint != null)
                {
                    activeMounts = ConsumesProduction(structure, origin, blueprint.FirstWeapon, false, delta) ? 1 : 0;
                    if (blueprint.SecondWeapon.HasValue && ConsumesProduction(structure, origin, blueprint.SecondWeapon.Value, true, delta)) activeMounts++;
                }
                float feed = blueprint == null ? 1 : TowerCatalog.FeedMultiplier(blueprint, balance, activeMounts);
                float quality = QualityMultiplier(blueprint);
                ColorEffectPayload payload = mechanics.ColorCartridges ? ColorEffectPayload.Capture(structure.Color, effects) : default;
                TowerColorBudget colorBudget = null;
                if (balance.Effects.LimitTowerColor && payload.Color != InkColor.None)
                {
                    if (structure.ColorBudget == null)
                        structure.ColorBudget = new TowerColorBudget(balance.Effects.ColorWindowSeconds, balance.Effects.ColorTargetsPerWindow);
                    colorBudget = structure.ColorBudget;
                }
                FireMount(structure, origin, blueprint?.FirstWeapon ?? structure.Kind, false, shots,
                    fireMultiplier * feed, quality, dynamic, damageMultiplier, payload, colorBudget, delta, tick, statistics, events);
                if (blueprint != null && blueprint.SecondWeapon.HasValue)
                    FireMount(structure, origin, blueprint.SecondWeapon.Value, true, shots,
                        fireMultiplier * feed, quality, dynamic, damageMultiplier, payload, colorBudget, delta, tick, statistics, events);
            }
        }

        static bool CanOperate(StructureState structure)
            => structure.Health > 0 && structure.IsComplete && structure.BuildRemaining <= 0 && structure.Kind != StructureKind.Wall;

        float QualityMultiplier(TowerBlueprint blueprint)
        {
            if (!mechanics.DrawingQuality) return 1;
            float quality = qualityOverride ?? (blueprint == null || blueprint.Origin == DrawingOrigin.LegacyStandard ? 100 : blueprint.Assessment.Quality);
            if (!qualityOverride.HasValue && (blueprint == null || blueprint.Origin == DrawingOrigin.LegacyStandard)) return 1;
            return DrawingEvaluator.FireMultiplier(quality, balance.DrawingMinimumRate, balance.DrawingMaximumRate);
        }

        // Production is shared by unfinished reloads and acquired working mounts. A ready idle
        // mount releases its share. This preview never mutates tracking: each barrel revalidates
        // its actual target after the previous barrel has dealt damage or cleaned a blot.
        private bool ConsumesProduction(StructureState structure, Point2 origin, StructureKind kind, bool secondary, float delta)
        {
            if ((secondary ? structure.SecondaryActionRemaining : structure.PrimaryActionRemaining) > 0) return true;
            var definition = balance.Structure(kind);
            float range = MountRange(structure.Blueprint, definition);
            if(kind==StructureKind.Blotter) return blotContext!=null && blotContext.TryFindNearest(origin,range,out _);
            spatial.Query(origin, range, targets);
            EnemyState target = PickTarget(structure, origin, targets);
            if (target == null) return false;
            int previousId = secondary ? structure.SecondaryTargetId : structure.PrimaryTargetId;
            float remaining = previousId == target.Id
                ? (secondary ? structure.SecondaryAcquisitionRemaining : structure.PrimaryAcquisitionRemaining)
                : TowerCatalog.AcquisitionSeconds(structure.Blueprint, balance);
            return remaining <= delta + .000001f;
        }

        private void FireMount(StructureState structure, Point2 origin, StructureKind kind, bool secondary,
            List<ShotEvent> shots, float fireMultiplier, float qualityMultiplier, bool dynamic, float damageMultiplier,
            ColorEffectPayload payload, TowerColorBudget colorBudget, float delta, long tick,
            RunStatistics statistics, SimulationEventBuffer events)
        {
            var definition = balance.Structure(kind);
            float range = MountRange(structure.Blueprint, definition);
            float interval = TowerCatalog.ActionInterval(structure.Blueprint, balance, kind, fireMultiplier, qualityMultiplier);
            if (dynamic) AdvanceReload(structure, secondary, interval, delta);
            if(kind==StructureKind.Blotter)
            {
                FireBlotter(structure,origin,range,definition,secondary,shots,interval,dynamic,delta,tick,statistics,events);
                return;
            }
            spatial.Query(origin, range, targets);
            EnemyState target = PickTarget(structure, origin, targets);
            bool acquired = TrackTarget(structure, target, secondary, delta);
            if (secondary)
            {
                if (target != null) structure.SecondaryAim = target.Position;
            }
            else
            {
                if (target != null) structure.PrimaryAim = target.Position;
            }
            if (target == null) { statistics.RecordIdle(kind, delta); return; }
            if (!acquired) return;
            if(!TryBeginAction(structure,secondary,interval,dynamic)) return;
            float damage = Math.Max(0, definition.Damage * balance.Material(structure.Tier).DamageMultiplier * damageMultiplier);
            structure.Aim = target.Position;
            statistics.RecordLaunch(kind);
            switch (kind)
            {
                case StructureKind.Piercer:
                    FirePiercer(origin, target.Position, range, Math.Max(.2f, definition.Radius),
                        Math.Max(1, definition.MaxPierce), damage, payload, colorBudget, statistics);
                    break;
                case StructureKind.Mortar:
                    LaunchMortar(origin, target.Position, definition, damage, payload, colorBudget,
                        structure.Blueprint?.GetMortarFootprint(secondary ? 1 : 0) ?? MortarFootprint.Circle, tick, events);
                    return;
                case StructureKind.Fan:
                    FireFan(structure, origin, target.Position, definition, damage, payload, colorBudget, shots, tick, statistics, events);
                    return;
                case StructureKind.Ricochet:
                    FireRicochet(structure, origin, target, definition, damage, payload, colorBudget, shots, tick, statistics, events);
                    return;
                case StructureKind.Stapler:
                    ApplyDamage(target,damage,kind,payload,colorBudget,statistics);
                    EnemyEffects.ApplySlow(target,definition.SlowSpeedMultiplier,definition.SlowDuration);
                    break;
                case StructureKind.Spikes:
                    if (colorBudget != null) SortTargets(origin, targets);
                    foreach (var victim in targets) ApplyDamage(victim, damage, kind, payload, colorBudget, statistics);
                    break;
                default:
                    ApplyDamage(target, damage, kind, payload, colorBudget, statistics);
                    break;
            }
            Point2 shotEnd = kind == StructureKind.Piercer ? RayEnd(origin, structure.Aim, range) : structure.Aim;
            PublishShot(origin, shotEnd, kind, definition.Radius, structure.Id, shots, tick, events);
        }

        float MountRange(TowerBlueprint blueprint, StructureDefinition definition)
            => definition.Kind == StructureKind.Spikes ? Math.Max(.5f, definition.Radius) :
                Math.Max(0, TowerCatalog.WeaponRange(blueprint, balance, definition.Kind));

        // A visible immediate shot has both playback and live-event representations. Launch/impact stay separate.
        static void PublishShot(Point2 from, Point2 to, StructureKind kind, float radius, int structureId,
            List<ShotEvent> shots, long tick, SimulationEventBuffer events)
        {
            shots.Add(new ShotEvent(from, to, kind, radius));
            events.Publish(tick, SimulationEventKind.Shot, from, to, kind, radius, structureId);
        }

        private bool TrackTarget(StructureState structure, EnemyState target, bool secondary, float delta)
        {
            int previousId = secondary ? structure.SecondaryTargetId : structure.PrimaryTargetId;
            float remaining = secondary ? structure.SecondaryAcquisitionRemaining : structure.PrimaryAcquisitionRemaining;
            int id = target == null ? 0 : target.Id;
            if (target == null || structure.Blueprint == null) remaining = 0;
            else if (previousId != id) remaining = TowerCatalog.AcquisitionSeconds(structure.Blueprint, balance);
            remaining = Math.Max(0, remaining - delta);
            if (secondary) { structure.SecondaryTargetId = id; structure.SecondaryAcquisitionRemaining = remaining; }
            else { structure.PrimaryTargetId = id; structure.PrimaryAcquisitionRemaining = remaining; }
            return remaining <= .000001f;
        }

        private static void AdvanceReload(StructureState structure, bool secondary, float interval, float delta)
        {
            float remaining = Math.Max(0, (secondary ? structure.SecondaryActionRemaining : structure.PrimaryActionRemaining) - delta / interval);
            if (remaining <= .000001f) remaining = 0;
            if (secondary) { structure.SecondaryActionRemaining = remaining; structure.SecondaryCooldown = remaining * interval; }
            else { structure.PrimaryActionRemaining = remaining; structure.Cooldown = remaining * interval; }
        }

        // Preparation advances readiness without running target searches, attacks or cleanup.
        // Assault uses Fire instead, so a simulation step never advances a reload twice.
        internal void ReloadWithoutTargets(List<StructureState> structures, float fireMultiplier, float delta)
        {
            if (!balance.Construction.DynamicFeed) return;
            foreach (var structure in structures)
            {
                if (!CanOperate(structure)) continue;
                TowerBlueprint blueprint = structure.Blueprint;
                int pending = structure.PrimaryActionRemaining > 0 ? 1 : 0;
                if (blueprint != null && blueprint.SecondWeapon.HasValue && structure.SecondaryActionRemaining > 0) pending++;
                float production = fireMultiplier * (blueprint == null ? 1 : TowerCatalog.FeedMultiplier(blueprint, balance, pending));
                float quality = QualityMultiplier(blueprint);
                AdvanceReload(structure, false, TowerCatalog.ActionInterval(blueprint, balance, blueprint?.FirstWeapon ?? structure.Kind, production, quality), delta);
                if (blueprint != null && blueprint.SecondWeapon.HasValue)
                    AdvanceReload(structure, true, TowerCatalog.ActionInterval(blueprint, balance, blueprint.SecondWeapon.Value, production, quality), delta);
                structure.PrimaryTargetId = structure.SecondaryTargetId = 0;
                structure.PrimaryAcquisitionRemaining = structure.SecondaryAcquisitionRemaining = 0;
            }
        }

        private static bool TryBeginAction(StructureState structure, bool secondary, float cooldown, bool dynamic)
        {
            if(dynamic)
            {
                if((secondary?structure.SecondaryActionRemaining:structure.PrimaryActionRemaining)>0) return false;
                if(secondary) structure.SecondaryActionRemaining=1; else structure.PrimaryActionRemaining=1;
            }
            else if((secondary?structure.SecondaryCooldown:structure.Cooldown)>0) return false;
            if(secondary) structure.SecondaryCooldown=cooldown; else structure.Cooldown=cooldown;
            return true;
        }

        private void FireBlotter(StructureState structure,Point2 origin,float range,StructureDefinition definition,
            bool secondary,List<ShotEvent> shots,float interval,bool dynamic,float delta,
            long tick,RunStatistics statistics,SimulationEventBuffer events)
        {
            if(blotContext==null || !blotContext.TryFindNearest(origin,range,out Cell cell))
            {
                if(secondary) structure.SecondaryTargetId=0; else structure.PrimaryTargetId=0;
                statistics.RecordIdle(StructureKind.Blotter,delta); return;
            }
            Point2 target=new Point2(cell.X+.5f,cell.Y+.5f);
            int targetId=-(cell.Y*balance.Width+cell.X+1);
            if(secondary) {structure.SecondaryTargetId=targetId;structure.SecondaryAim=target;}
            else {structure.PrimaryTargetId=targetId;structure.PrimaryAim=target;}
            if(!TryBeginAction(structure,secondary,interval,dynamic)) return;
            float radius=Math.Max(0,definition.Radius);
            int cleared=blotContext.Clear(cell,radius);
            if(cleared<=0) return;
            structure.Aim=target;
            statistics.RecordBlotsCleared(cleared); statistics.RecordLaunch(StructureKind.Blotter);
            PublishShot(origin, target, StructureKind.Blotter, radius, structure.Id, shots, tick, events);
        }

        private void LaunchMortar(Point2 from, Point2 to, StructureDefinition definition, float damage,
            ColorEffectPayload payload, TowerColorBudget colorBudget, MortarFootprint footprint,
            long tick, SimulationEventBuffer events)
        {
            float duration = Math.Max(0, definition.FlightSeconds);
            var projectile = new MortarProjectile
            {
                Id = nextProjectileId++, From = from, To = to, Duration = duration,
                Remaining = duration, Radius = Math.Max(0, definition.Radius),
                Damage = damage, Weapon = StructureKind.Mortar, Color = payload.Color,
                EffectPayload = payload, ColorBudget = colorBudget,
                ImpactFootprint = FootprintGeometry.Resolve(footprint, definition.Radius, from, to)
            };
            projectiles.Add(projectile);
            events.Publish(tick, SimulationEventKind.ProjectileLaunched, from, to, projectile.Weapon,
                projectile.Radius, projectile.Id, duration, projectile.ImpactFootprint);
        }

        private void FireFan(StructureState structure, Point2 origin, Point2 aim, StructureDefinition definition,
            float damage, ColorEffectPayload payload, TowerColorBudget colorBudget, List<ShotEvent> shots,
            long tick, RunStatistics statistics, SimulationEventBuffer events)
        {
            float dx = aim.X - origin.X, dy = aim.Y - origin.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length <= .00001f) { dx = 1; dy = 0; }
            else { dx /= length; dy /= length; }
            float cosine = (float)Math.Cos(Math.Max(0, Math.Min(360, definition.ConeAngleDegrees)) * Math.PI / 360);
            SortTargets(origin, targets);
            int hit = 0, limit = Math.Max(0, definition.MaxTargets);
            foreach (EnemyState victim in targets)
            {
                if (hit >= limit) break;
                if (victim.Health <= 0) continue;
                float vx = victim.Position.X - origin.X, vy = victim.Position.Y - origin.Y;
                float distance = (float)Math.Sqrt(vx * vx + vy * vy);
                if (distance > .00001f && (vx * dx + vy * dy) / distance + .000001f < cosine) continue;
                ApplyDamage(victim, damage, StructureKind.Fan, payload, colorBudget, statistics);
                PublishShot(origin, victim.Position, StructureKind.Fan, 0, structure.Id, shots, tick, events);
                hit++;
            }
        }

        private void FireRicochet(StructureState structure, Point2 origin, EnemyState victim, StructureDefinition definition,
            float damage, ColorEffectPayload payload, TowerColorBudget colorBudget, List<ShotEvent> shots,
            long tick, RunStatistics statistics, SimulationEventBuffer events)
        {
            ricochetVictims.Clear();
            Point2 from=origin;
            int limit=Math.Max(0,definition.MaxTargets);
            float jumpRange=Math.Max(0,definition.JumpRange);
            for(int hit=0;victim!=null && hit<limit;hit++)
            {
                ricochetVictims.Add(victim.Id);
                Point2 impact=victim.Position;
                ApplyDamage(victim,damage,StructureKind.Ricochet,payload,colorBudget,statistics);
                PublishShot(from, impact, StructureKind.Ricochet, 0, structure.Id, shots, tick, events);
                if(hit+1>=limit) break;
                spatial.Query(impact,jumpRange,splashTargets);
                EnemyState next=null; float nearest=float.MaxValue;
                foreach(var candidate in splashTargets)
                {
                    if(candidate.Health<=0 || ricochetVictims.Contains(candidate.Id)) continue;
                    float distance=DistanceSquared(impact,candidate.Position);
                    if(next==null || distance<nearest || (distance==nearest && candidate.Id<next.Id))
                    {next=candidate;nearest=distance;}
                }
                from=impact; victim=next;
            }
        }

        private void AdvanceProjectiles(float delta, long tick, RunStatistics statistics, SimulationEventBuffer events)
        {
            int write = 0;
            for (int read = 0; read < projectiles.Count; read++)
            {
                var projectile = projectiles[read];
                projectile.Remaining = Math.Max(0, projectile.Remaining - delta);
                if (projectile.Remaining > 0.000001f) { projectiles[write++] = projectile; continue; }
                FootprintGeometry? geometry = projectile.ImpactFootprint;
                bool ellipse = geometry.HasValue && geometry.Value.ForwardRadius != geometry.Value.SideRadius;
                // Contains includes a small normalized boundary tolerance. Keep the broad phase
                // conservative under world-coordinate rounding, without changing circle queries.
                float queryRadius = ellipse
                    ? (float)Math.Min(float.MaxValue, (double)geometry.Value.BoundingRadius * 1.000001)
                    : projectile.Radius;
                spatial.Query(projectile.To, queryRadius, splashTargets);
                if (projectile.ColorBudget != null) SortTargets(projectile.To, splashTargets);
                foreach (var victim in splashTargets)
                {
                    if (ellipse && !geometry.Value.Contains(projectile.To, victim.Position)) continue;
                    ApplyDamage(victim, projectile.Damage, projectile.Weapon, projectile.EffectPayload, projectile.ColorBudget, statistics);
                }
                events.Publish(tick, SimulationEventKind.ProjectileImpact, projectile.From, projectile.To,
                    projectile.Weapon, projectile.Radius, projectile.Id, impactFootprint: projectile.ImpactFootprint);
            }
            if (write < projectiles.Count) projectiles.RemoveRange(write, projectiles.Count - write);
        }

        private void ApplyDamage(EnemyState victim, float damage, StructureKind weapon, ColorEffectPayload payload,
            TowerColorBudget colorBudget, RunStatistics statistics)
        {
            if (victim.Health <= 0 || damage <= 0) return;
            if (mechanics.EnemyDrawingTiers || mechanics.CorpseBlots || payload.Color != InkColor.None)
            {
                if (payload.Color != InkColor.None && colorBudget != null && !colorBudget.TryApply(victim.Id, clockSeconds)) payload = default;
                EnemyEffects.ApplyHit(victim, damage, payload, statistics, weapon, effects, mechanics.CorpseBlots ? balance.BlotReduction : 0); return;
            }
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
            float damage, ColorEffectPayload payload, TowerColorBudget colorBudget, RunStatistics statistics)
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
            SortTargets(origin, splashTargets);
            for (int i = 0; i < splashTargets.Count && i < maxPierce; i++)
                ApplyDamage(splashTargets[i], damage, StructureKind.Piercer, payload, colorBudget, statistics);
        }

        private void SortTargets(Point2 origin, List<EnemyState> list)
        { targetOrder.Origin = origin; list.Sort(targetOrder); }

        private sealed class EnemyDistanceComparer : IComparer<EnemyState>
        {
            internal Point2 Origin;
            public int Compare(EnemyState a, EnemyState b)
            {
                int result = DistanceSquared(Origin, a.Position).CompareTo(DistanceSquared(Origin, b.Position));
                return result != 0 ? result : a.Id.CompareTo(b.Id);
            }
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
