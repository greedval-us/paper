using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    internal sealed partial class WeaponSystem
    {
        readonly Dictionary<ResolvedWeapon, WeaponDefinition> compositeWeapons = new Dictionary<ResolvedWeapon, WeaponDefinition>();

        // Snapshot values have already received material and part modifiers. This adapter only
        // supplies the existing hit geometry and effect code with its immutable numeric input.
        WeaponDefinition CompositeDefinition(ResolvedWeapon weapon)
        {
            if (compositeWeapons.TryGetValue(weapon, out var result)) return result;
            var kind = StructureState.CombatKind(weapon.Family);
            var input = new WeaponDefinitionInput
            {
                Id = LegacyContentIds.Weapon(kind), Name = weapon.SourceId, LegacyKind = kind,
                Attack = weapon.Attack, MinimumRange = weapon.MinimumRange, FiringArcDegrees = weapon.FiringArcDegrees,
                Stats = new StructureDefinition
                {
                    Kind = kind, MaxHealth = 1, Range = weapon.Range, Damage = weapon.Damage,
                    ShotInterval = weapon.Interval, Radius = weapon.Radius, FlightSeconds = weapon.FlightSeconds,
                    MaxPierce = weapon.TargetLimit, MaxTargets = weapon.TargetLimit,
                    ConeAngleDegrees = weapon.ConeDegrees, JumpRange = weapon.JumpRange,
                    SlowSpeedMultiplier = weapon.SlowSpeedMultiplier, SlowDuration = weapon.SlowSeconds
                }
            };
            Dictionary<string, EffectDefinition> effectDefinitions = null;
            if (weapon.SlowSeconds > 0 && weapon.SlowSpeedMultiplier < 1)
            {
                const string id = "assembly.slow";
                input.EffectIds.Add(id);
                effectDefinitions = new Dictionary<string, EffectDefinition>
                {
                    { id, new EffectDefinition(new EffectDefinitionInput
                        { Id = id, Kind = EffectKind.Slow, Strength = Math.Max(.4f, weapon.SlowSpeedMultiplier), Duration = weapon.SlowSeconds }) }
                };
            }
            result = new WeaponDefinition(input, effectDefinitions);
            compositeWeapons.Add(weapon, result);
            return result;
        }

        readonly List<StructureState> isolatedComposite = new List<StructureState>(1);
        readonly List<CompositeMount> compositeMounts = new List<CompositeMount>();
        readonly Dictionary<StructureState, int> activeCompositeMounts = new Dictionary<StructureState, int>();

        struct CompositeMount
        {
            internal StructureState Structure;
            internal ResolvedWeapon Weapon;
            internal WeaponDefinition Definition;
            internal Point2 Origin;
            internal ColorEffectPayload Payload;
            internal TowerColorBudget ColorBudget;
            internal EnemyState Target;
            internal Cell Blot;
            internal bool HasTarget;
            internal WeaponMountState State => Structure.GetMountState(Weapon.SlotIndex);
        }

        bool CompositeTarget(StructureState structure, Point2 origin, ResolvedWeapon weapon, out EnemyState target, out Cell blot)
        {
            target = null; blot = default;
            var sector = new FiringSector(structure, weapon.FiringArcDegrees);
            if (weapon.Attack == AttackKind.Cleanup)
                return TryFindBlot(origin, weapon.MinimumRange, weapon.Range, sector, out blot);
            spatial.Query(origin, weapon.Range, targets);
            FilterRange(origin, weapon.MinimumRange, weapon.Range, sector, targets);
            target = PickTarget(structure, origin, targets);
            return target != null;
        }

        // Interleave every composite mount and shell on one timeline. Only the pure cycle
        // rules change ammunition/progress; hit geometry and impacts remain the shared operations.
        void FireComposites(List<StructureState> structures, List<ShotEvent> shots, float fireMultiplier, float damageMultiplier,
            float delta, long tick, RunStatistics statistics, SimulationEventBuffer events, int isolatedMount = -1)
        {
            if (delta <= 0) return;
            compositeMounts.Clear();
            readySpikeTraps.Clear();
            bool found = isolatedMount < 0;
            foreach (var structure in structures)
            {
                if (structure.ResolvedEntry?.Support != null)
                {
                    PrepareFieldDefense(structure, damageMultiplier, statistics);
                    continue;
                }
                if (structure.ResolvedEntry == null || !CanOperate(structure)) continue;
                ColorEffectPayload payload = mechanics.ColorCartridges ? ColorPayload(structure) : default;
                var budget = ColorBudget(structure,payload);
                foreach (var weapon in structure.ResolvedEntry.Weapons)
                {
                    if (isolatedMount >= 0 && weapon.SlotIndex != isolatedMount) continue;
                    found = true;
                    compositeMounts.Add(new CompositeMount
                    {
                        Structure = structure, Weapon = weapon, Definition = CompositeDefinition(weapon),
                        Origin = new Point2(structure.Cell.X + .5f, structure.Cell.Y + .5f),
                        Payload = payload, ColorBudget = budget
                    });
                }
            }
            if (!found) throw new ArgumentOutOfRangeException(nameof(isolatedMount));
            // Every beacon has now marked its targets, regardless of build order.
            foreach (var trap in readySpikeTraps) FireSpikeTrap(trap, damageMultiplier, statistics);

            double end = clockSeconds, start = end - delta, elapsed = 0;
            float remaining = delta;
            while (true)
            {
                RefreshCompositeTargets();
                // Impacts at this instant precede target selection for a ready action.
                if (attacks.ResolveNextCompositeImpact(tick, start + elapsed, statistics, events))
                    continue;
                int ready = -1;
                for (int i = 0; i < compositeMounts.Count; i++)
                {
                    var mount = compositeMounts[i];
                    float feed = CompositeFeed(mount, fireMultiplier);
                    if (mount.HasTarget && mount.State.AcquisitionRemaining <= 0 &&
                        mount.State.Cycle.RemainingWork == 0 && feed > 0) { ready = i; break; }
                }
                if (ready >= 0)
                {
                    clockSeconds = start + elapsed;
                    EmitCompositeShot(compositeMounts[ready], CompositeFeed(compositeMounts[ready], fireMultiplier),
                        damageMultiplier, shots, tick, statistics, events);
                    continue;
                }
                if (remaining <= 0) break;

                float step = remaining;
                foreach (var mount in compositeMounts)
                {
                    if (mount.State.AcquisitionRemaining > 0) step = Math.Min(step, mount.State.AcquisitionRemaining);
                    step = Math.Min(step, CompositeBoundary(mount.State, mount.Weapon, CompositeFeed(mount, fireMultiplier)));
                }
                step = attacks.NextCompositeImpact(step);
                foreach (var mount in compositeMounts)
                {
                    var state = mount.State;
                    float feed = CompositeFeed(mount, fireMultiplier);
                    state.Cycle = WeaponCycleRules.Step(state.Cycle, mount.Weapon, step, false, feed).State;
                    state.AcquisitionRemaining = Math.Max(0, state.AcquisitionRemaining - step);
                    if (!mount.HasTarget) statistics.RecordIdle(mount.Definition.Id, step);
                    SyncCompositeReadiness(state, mount.Weapon, feed);
                }
                attacks.AdvanceCompositeProjectiles(step);
                remaining = Math.Max(0, remaining - step); elapsed += step;
            }
            foreach (var mount in compositeMounts)
                SyncCompositeReadiness(mount.State, mount.Weapon, CompositeFeed(mount, fireMultiplier));
            clockSeconds = end;
        }

        void RefreshCompositeTargets()
        {
            activeCompositeMounts.Clear();
            for (int i = 0; i < compositeMounts.Count; i++)
            {
                var mount = compositeMounts[i]; var state = mount.State;
                mount.HasTarget = CompositeTarget(mount.Structure, mount.Origin, mount.Weapon, out mount.Target, out mount.Blot);
                int id = !mount.HasTarget ? 0 : mount.Target != null ? mount.Target.Id : -(mount.Blot.Y * balance.Width + mount.Blot.X + 1);
                if (id != state.TargetId) state.AcquisitionRemaining = id == 0 ? 0 : mount.Weapon.AcquisitionSeconds;
                state.TargetId = id;
                state.Aim = !mount.HasTarget ? mount.Structure.RestAim :
                    mount.Target != null ? mount.Target.Position : new Point2(mount.Blot.X + .5f, mount.Blot.Y + .5f);
                if (!activeCompositeMounts.ContainsKey(mount.Structure)) activeCompositeMounts.Add(mount.Structure, 0);
                if (state.Cycle.RemainingWork > 0 || mount.HasTarget && state.AcquisitionRemaining <= 0)
                    activeCompositeMounts[mount.Structure]++;
                compositeMounts[i] = mount;
            }
        }

        float CompositeFeed(CompositeMount mount, float fireMultiplier)
            => mount.Structure.ResolvedEntry.FeedForActiveMounts(activeCompositeMounts[mount.Structure]) * fireMultiplier;

        static float CompositeBoundary(WeaponMountState state, ResolvedWeapon weapon, float feed)
        {
            if (feed <= 0 || state.Cycle.RemainingWork == 0) return float.PositiveInfinity;
            double duration = state.Cycle.IsReloading ? weapon.ReloadSeconds / (double)feed
                : Math.Max(weapon.MinimumActionInterval, weapon.Interval / (double)feed);
            return (float)Math.Max(float.Epsilon, Math.Min(float.MaxValue, state.Cycle.RemainingWork * duration));
        }

        void EmitCompositeShot(CompositeMount mount, float feed, float damageMultiplier, List<ShotEvent> shots,
            long tick, RunStatistics statistics, SimulationEventBuffer events)
        {
            var structure = mount.Structure; var weapon = mount.Weapon; var state = mount.State;
            // Refreshing all mounts overwrote the shared query buffer. The chosen mount must
            // restore its candidates before the attack resolver consumes them.
            CompositeTarget(structure, mount.Origin, weapon, out var target, out var blot);
            Point2 aim = state.Aim;
            int cleared = weapon.Attack == AttackKind.Cleanup ? blotContext.Clear(blot, weapon.Radius) : 0;
            var cycle = WeaponCycleRules.Step(state.Cycle, weapon, float.Epsilon, true, feed);
            state.Cycle = cycle.StatesAfterShots[0];
            if (weapon.Attack == AttackKind.Cleanup && cleared <= 0) return;
            float damage = TowerCatalog.FiniteStat(Math.Max(0, weapon.Damage * damageMultiplier), "damage");
            var shot = new WeaponAttack {
                Source = structure, Mount = weapon.SlotIndex, Weapon = mount.Definition, Origin = mount.Origin,
                Aim = aim, Target = target, Range = weapon.Range, Damage = damage,
                Payload = weapon.Attack == AttackKind.Cleanup ? default : mount.Payload,
                ColorBudget = mount.ColorBudget, ClearedBlots = cleared,
                PierceLimit = weapon.TargetLimit, CompositeTimeline = true
            };
            attacks.Fire(shot, shots, tick, clockSeconds, statistics, events);
        }

        void ReloadComposite(StructureState structure, float fireMultiplier, float delta)
        {
            var entry = structure.ResolvedEntry;
            float remaining = delta;
            while (remaining > 0)
            {
                int pending = 0;
                foreach (var weapon in entry.Weapons)
                    if (structure.GetMountState(weapon.SlotIndex).Cycle.RemainingWork > 0) pending++;
                float feed = entry.FeedForActiveMounts(pending) * fireMultiplier;
                float step = remaining;
                foreach (var weapon in entry.Weapons)
                    step = Math.Min(step, CompositeBoundary(structure.GetMountState(weapon.SlotIndex), weapon, feed));
                foreach (var weapon in entry.Weapons)
                {
                    var state = structure.GetMountState(weapon.SlotIndex);
                    state.Cycle = WeaponCycleRules.Step(state.Cycle, weapon, step, false, feed).State;
                    state.TargetId = 0; state.AcquisitionRemaining = 0;
                    SyncCompositeReadiness(state, weapon, feed);
                }
                remaining = Math.Max(0, remaining - step);
            }
        }

        static void SyncCompositeReadiness(WeaponMountState state, ResolvedWeapon weapon, float feed)
        {
            state.ActionRemaining = (float)state.Cycle.RemainingWork;
            state.Cooldown = feed > 0 ? (float)(state.Cycle.RemainingWork *
                (state.Cycle.IsReloading ? weapon.ReloadAtFeed(feed) : weapon.IntervalAtFeed(feed))) : 0;
        }
    }
}
