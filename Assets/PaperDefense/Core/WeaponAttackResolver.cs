using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    // Prepared by either mount cycle after it has acquired a target and committed the shot.
    internal struct WeaponAttack
    {
        internal StructureState Source;
        internal int Mount;
        internal WeaponDefinition Weapon;
        internal Point2 Origin, Aim;
        internal EnemyState Target;
        internal float Range, Damage;
        internal ColorEffectPayload Payload;
        internal TowerColorBudget ColorBudget;
        internal MortarFootprint Footprint;
        internal FanVolley Volley;
        internal int PierceLimit, ClearedBlots;
        internal bool CompositeTimeline;
    }

    /// <summary>Resolves weapon geometry and damage for a single simulation. Cadence and acquisition belong to the caller.</summary>
    internal sealed partial class WeaponAttackResolver
    {
        readonly EnemySpatialIndex spatial;
        readonly List<EnemyState> targets;
        readonly List<EnemyState> splashTargets = new List<EnemyState>(64);
        readonly HashSet<int> ricochetVictims = new HashSet<int>();
        readonly EnemyDistanceComparer targetOrder = new EnemyDistanceComparer();
        readonly MechanicsAvailability mechanics;
        readonly EffectsRules effects;
        readonly float blotReduction;
        long nextActionId;

        // All damage and feedback for one action share its identity, including delayed impacts.
        struct Hit
        {
            internal WeaponDefinition Weapon;
            internal float Damage;
            internal ColorEffectPayload Payload;
            internal TowerColorBudget ColorBudget;
            internal int SourceId, Mount;
            internal long ActionId, Tick;
            internal double Time;
            internal RunStatistics Statistics;
            internal SimulationEventBuffer Events;
            internal StructureKind Kind => Weapon.LegacyKind ?? (StructureKind)(-1);
        }

        internal WeaponAttackResolver(EnemySpatialIndex spatial, List<EnemyState> acquiredTargets,
            MechanicsAvailability mechanics, EffectsRules effects, float blotReduction)
        {
            this.spatial = spatial;
            targets = acquiredTargets;
            this.mechanics = mechanics;
            this.effects = effects;
            this.blotReduction = mechanics.CorpseBlots ? blotReduction : 0;
        }

        internal void Fire(in WeaponAttack shot, List<ShotEvent> shots, long tick, double time,
            RunStatistics statistics, SimulationEventBuffer events)
        {
            var weapon = shot.Weapon;
            var stats = weapon.Numeric;
            var hit = new Hit {
                Weapon = weapon, Damage = shot.Damage, Payload = shot.Payload, ColorBudget = shot.ColorBudget,
                SourceId = shot.Source.Id, Mount = shot.Mount, ActionId = ++nextActionId,
                Tick = tick, Time = time, Statistics = statistics, Events = events
            };
            events.Publish(tick, SimulationEventKind.WeaponActionStarted, shot.Origin, shot.Aim, hit.Kind,
                entityId: hit.SourceId, weaponId: weapon.Id, sourceStructureId: hit.SourceId,
                mountSlotIndex: hit.Mount, actionId: hit.ActionId, simulationSeconds: time,
                attack: weapon.Attack, colorEffect: hit.Payload);
            statistics.RecordLaunch(weapon.Id);
            if (weapon.Attack != AttackKind.Cleanup && shot.Damage > 0 && shot.Source.ResolvedEntry?.Support == null &&
                (shot.Source.ResolvedEntry != null || shot.Source.Blueprint != null || !ConstructionToolRules.IsTool(shot.Source.Kind)))
                statistics.RecordPositionLaunch(shot.Source.Id, shot.Source.Cell, shot.Source.ContentId);

            switch (weapon.Attack)
            {
                case AttackKind.Single:
                    ApplyDamage(shot.Target, hit);
                    PublishShot(shot.Origin, shot.Aim, stats.Radius, hit, shots);
                    break;
                case AttackKind.Line:
                    FirePiercer(shot, hit);
                    PublishShot(shot.Origin, RayEnd(shot.Origin, shot.Aim, shot.Range), stats.Radius, hit, shots);
                    break;
                case AttackKind.AreaProjectile:
                    LaunchMortar(shot.Origin, shot.Aim, shot.Footprint ?? MortarFootprint.Circle, shot.CompositeTimeline, hit);
                    break;
                case AttackKind.Cone:
                    if (shot.Volley == null) FireFan(shot, hit, shots);
                    else FireVolley(shot, shot.Volley, hit, shots);
                    break;
                case AttackKind.Ricochet:
                    FireRicochet(shot, hit, shots);
                    break;
                case AttackKind.Radial:
                    if (hit.ColorBudget != null) SortTargets(shot.Origin, targets);
                    foreach (var victim in targets) ApplyDamage(victim, hit);
                    PublishShot(shot.Origin, shot.Aim, stats.Radius, hit, shots);
                    break;
                case AttackKind.Cleanup:
                    statistics.RecordBlotsCleared(shot.ClearedBlots);
                    PublishShot(shot.Origin, shot.Aim, stats.Radius, hit, shots);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(weapon.Attack), weapon.Attack, "Unknown weapon attack geometry.");
            }
        }

        static void PublishShot(Point2 from, Point2 to, float radius, in Hit hit, List<ShotEvent> shots)
        {
            shots.Add(new ShotEvent(from, to, hit.Kind, radius, hit.Weapon.Id, hit.SourceId,
                hit.Mount, hit.ActionId, hit.Time, hit.Payload));
            hit.Events.Publish(hit.Tick, SimulationEventKind.Shot, from, to, hit.Kind, radius, hit.SourceId,
                weaponId: hit.Weapon.Id, sourceStructureId: hit.SourceId, mountSlotIndex: hit.Mount,
                actionId: hit.ActionId, simulationSeconds: hit.Time, attack: hit.Weapon.Attack, colorEffect: hit.Payload);
        }

        void ApplyDamage(EnemyState victim, in Hit hit)
        {
            if (victim.Health <= 0 || hit.Damage < 0) return;
            if (hit.Damage > 0)
            {
                var payload = hit.Payload;
                if (mechanics.EnemyDrawingTiers || mechanics.CorpseBlots || payload.Color != InkColor.None)
                {
                    if (payload.Color != InkColor.None && hit.ColorBudget != null && !hit.ColorBudget.TryApply(victim.Id, hit.Time))
                        payload = default;
                    EnemyEffects.ApplyHit(victim, hit.Damage, payload, hit.Statistics, hit.Kind, effects, blotReduction,
                        hit.Weapon.Id, hit.Events, hit.Tick, hit.Time, hit.SourceId, hit.Mount, hit.ActionId, hit.Weapon.Attack);
                }
                else
                {
                    float damage = Math.Min(victim.Health, hit.Damage);
                    victim.Health -= damage;
                    hit.Statistics.RecordDamage(hit.Weapon.Id, damage);
                    EnemyEffects.PublishDamage(victim, victim.Health + damage, hit.Events, hit.Tick, hit.Time,
                        hit.Kind, hit.Weapon.Id, hit.SourceId, hit.Mount, hit.ActionId);
                }
            }
            // Intrinsic effects such as the stapler's slow also work on a zero-damage attack.
            if (hit.Weapon.Effects == null) return;
            foreach (var effect in hit.Weapon.Effects)
            {
                if (effect.Kind == EffectKind.Slow) EnemyEffects.ApplySlow(victim, effect.Strength, effect.Duration);
                else EnemyEffects.ApplyHit(victim, 0, ColorEffectPayload.Capture(effect), hit.Statistics,
                    hit.Kind, effects, blotReduction, hit.Weapon.Id);
            }
        }

        void FirePiercer(in WeaponAttack shot, in Hit hit)
        {
            float dx = shot.Aim.X - shot.Origin.X, dy = shot.Aim.Y - shot.Origin.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length <= .00001f) return;
            dx /= length;
            dy /= length;
            float radius = Math.Max(shot.Weapon.MinimumLineRadius, shot.Weapon.Numeric.Radius);
            splashTargets.Clear();
            foreach (var enemy in targets)
            {
                if (enemy.Health <= 0) continue;
                float x = enemy.Position.X - shot.Origin.X, y = enemy.Position.Y - shot.Origin.Y;
                float along = x * dx + y * dy;
                if (along >= 0 && along <= shot.Range && Math.Abs(x * dy - y * dx) <= radius)
                    splashTargets.Add(enemy);
            }
            SortTargets(shot.Origin, splashTargets);
            for (int i = 0; i < splashTargets.Count && i < shot.PierceLimit; i++)
                ApplyDamage(splashTargets[i], hit);
        }

        void FireVolley(in WeaponAttack shot, FanVolley volley, in Hit hit, List<ShotEvent> shots)
        {
            var weapon = shot.Weapon;
            volley.Validate(weapon);
            var counts = volley.Allocate(weapon.VolleyPellets);
            var victims = new EnemyState[counts.Length];
            var ends = new Point2[counts.Length];
            var sector = new FiringSector(shot.Source, weapon.FiringArcDegrees);
            // Resolve every guide against the same targets before applying any damage.
            for (int guide = 0; guide < counts.Length; guide++)
            {
                var direction = volley.Direction(guide, shot.Origin, shot.Aim);
                double best = shot.Range;
                if (!sector.Contains(direction.X, direction.Y))
                {
                    double cross = shot.Source.Forward.X * direction.Y - shot.Source.Forward.Y * direction.X;
                    double edge = (shot.Source.FacingDegrees + Math.Sign(cross) * weapon.FiringArcDegrees * .5) * Math.PI / 180;
                    direction = new Point2((float)Math.Cos(edge), (float)Math.Sin(edge));
                }
                foreach (var enemy in targets)
                {
                    if (enemy.Health <= 0) continue;
                    double x = (double)enemy.Position.X - shot.Origin.X, y = (double)enemy.Position.Y - shot.Origin.Y;
                    double along = x * direction.X + y * direction.Y, side = x * direction.Y - y * direction.X;
                    double radius = weapon.VolleyHitRadius, inside = radius * radius - side * side;
                    if (inside < 0 || along < 0) continue;
                    double distance = Math.Max(0, along - Math.Sqrt(inside));
                    if (distance > best || distance == best && victims[guide] != null && enemy.Id >= victims[guide].Id) continue;
                    best = distance;
                    victims[guide] = enemy;
                }
                ends[guide] = new Point2(shot.Origin.X + direction.X * (float)best, shot.Origin.Y + direction.Y * (float)best);
            }
            for (int guide = 0; guide < counts.Length; guide++)
                for (int pellet = 0; pellet < counts[guide]; pellet++)
                {
                    if (victims[guide] != null) ApplyDamage(victims[guide], hit);
                    PublishShot(shot.Origin, ends[guide], weapon.VolleyHitRadius, hit, shots);
                }
        }

        void FireFan(in WeaponAttack shot, in Hit hit, List<ShotEvent> shots)
        {
            var stats = shot.Weapon.Numeric;
            float dx = shot.Aim.X - shot.Origin.X, dy = shot.Aim.Y - shot.Origin.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length <= .00001f) { dx = 1; dy = 0; }
            else { dx /= length; dy /= length; }
            float cosine = (float)Math.Cos(Math.Max(0, Math.Min(360, stats.ConeAngleDegrees)) * Math.PI / 360);
            SortTargets(shot.Origin, targets);
            int hits = 0, limit = Math.Max(0, stats.MaxTargets);
            foreach (var victim in targets)
            {
                if (hits >= limit) break;
                if (victim.Health <= 0) continue;
                float vx = victim.Position.X - shot.Origin.X, vy = victim.Position.Y - shot.Origin.Y;
                float distance = (float)Math.Sqrt(vx * vx + vy * vy);
                if (distance > .00001f && (vx * dx + vy * dy) / distance + .000001f < cosine) continue;
                ApplyDamage(victim, hit);
                PublishShot(shot.Origin, victim.Position, 0, hit, shots);
                hits++;
            }
        }

        void FireRicochet(in WeaponAttack shot, in Hit hit, List<ShotEvent> shots)
        {
            var stats = shot.Weapon.Numeric;
            int limit = Math.Max(0, stats.MaxTargets);
            float jumpRange = Math.Max(0, stats.JumpRange);
            var from = shot.Origin;
            var victim = shot.Target;
            ricochetVictims.Clear();
            for (int i = 0; victim != null && i < limit; i++)
            {
                ricochetVictims.Add(victim.Id);
                Point2 impact = victim.Position;
                ApplyDamage(victim, hit);
                PublishShot(from, impact, 0, hit, shots);
                if (i + 1 >= limit) break;
                spatial.Query(impact, jumpRange, splashTargets);
                EnemyState next = null;
                float nearest = float.MaxValue;
                foreach (var candidate in splashTargets)
                {
                    if (candidate.Health <= 0 || ricochetVictims.Contains(candidate.Id)) continue;
                    float distance = DistanceSquared(impact, candidate.Position);
                    if (next == null || distance < nearest || distance == nearest && candidate.Id < next.Id)
                    {
                        next = candidate;
                        nearest = distance;
                    }
                }
                from = impact;
                victim = next;
            }
        }

        void SortTargets(Point2 origin, List<EnemyState> list)
        {
            targetOrder.Origin = origin;
            list.Sort(targetOrder);
        }

        sealed class EnemyDistanceComparer : IComparer<EnemyState>
        {
            internal Point2 Origin;
            public int Compare(EnemyState a, EnemyState b)
            {
                int result = DistanceSquared(Origin, a.Position).CompareTo(DistanceSquared(Origin, b.Position));
                return result != 0 ? result : a.Id.CompareTo(b.Id);
            }
        }

        static float DistanceSquared(Point2 a, Point2 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        static Point2 RayEnd(Point2 from, Point2 to, float range)
        {
            float dx = to.X - from.X, dy = to.Y - from.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            return length <= .00001f ? to : new Point2(from.X + dx / length * range, from.Y + dy / length * range);
        }
    }
}
