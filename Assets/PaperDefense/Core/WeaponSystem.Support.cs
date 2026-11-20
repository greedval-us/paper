using System.Collections.Generic;

namespace PaperDefense.Core
{
    internal sealed partial class WeaponSystem
    {
        readonly List<StructureState> readySpikeTraps = new List<StructureState>();

        void PrepareFieldDefense(StructureState source, float damageMultiplier, RunStatistics statistics)
        {
            if (!source.IsComplete || source.BuildRemaining > 0 || source.Health <= 0) return;
            var support = source.ResolvedEntry.Support;
            if (support.Kind == AssemblyStructureFamily.Prism && clockSeconds + .00001 >= source.NextPrismPulseAt)
                FirePrism(source, support, damageMultiplier, statistics);
            if (support.Kind == AssemblyStructureFamily.Spikes && clockSeconds + .00001 >= source.NextSpikePulseAt)
                readySpikeTraps.Add(source);
            if (support.Kind != AssemblyStructureFamily.Beacon || support.ArmorReduction <= 0) return;
            float blotArmor = mechanics.CorpseBlots ? balance.BlotReduction : 0;
            var origin = new Point2(source.Cell.X + .5f, source.Cell.Y + .5f);
            var mark = new ColorEffectPayload(InkColor.Green, ResolvedSupport.BeaconLingerSeconds, support.ArmorReduction, 0, false);
            spatial.Query(origin, support.Radius, targets);
            foreach (var enemy in targets)
            {
                if (EnemyEffects.ArmorBeforeReduction(enemy, effects, blotArmor) <= 0) continue;
                float previous = enemy.GreenEffect?.Active.Strength ?? 0;
                EnemyEffects.ApplyArmorReduction(enemy, mark, source.Kind, source.ContentId);
                if (enemy.GreenEffect.Active.Strength != mark.Strength) continue;
                statistics?.RecordArmorMark(enemy);
                if (mark.Strength > previous && source.CanPublishSupportFeedback(clockSeconds))
                    visualEvents?.Publish(visualTick, SimulationEventKind.BeaconMarked, origin, enemy.Position, source.Kind,
                        radius: support.Radius, entityId: enemy.Id, weaponId: source.ContentId, sourceStructureId: source.Id,
                        simulationSeconds: clockSeconds, magnitude: mark.Strength, colorEffect: mark);
            }
        }

        void FirePrism(StructureState source, ResolvedSupport support, float damageMultiplier, RunStatistics statistics)
        {
            var origin = new Point2(source.Cell.X + .5f, source.Cell.Y + .5f);
            spatial.Query(origin, support.Radius, targets);
            if (targets.Count == 0) return;

            var color = source.NextPrismColor;
            const float duration = ResolvedSupport.PrismEffectSeconds;
            const float burnTick = .5f;
            ColorEffectPayload pulse;
            switch (color)
            {
                case InkColor.Red:
                    pulse = new ColorEffectPayload(color, duration, support.BurnDamagePerSecond * damageMultiplier * burnTick, burnTick, true);
                    source.NextPrismColor = InkColor.Blue;
                    break;
                case InkColor.Blue:
                    pulse = ColorEffectPayload.Slow(support.SlowSpeedMultiplier, duration);
                    source.NextPrismColor = InkColor.Green;
                    break;
                default:
                    pulse = new ColorEffectPayload(color, duration, support.ArmorReduction, 0, false);
                    source.NextPrismColor = InkColor.Red;
                    break;
            }
            source.NextPrismPulseAt = clockSeconds + ResolvedSupport.PrismIntervalSeconds;
            statistics?.RecordLaunch(source.ContentId);
            visualEvents?.Publish(visualTick, SimulationEventKind.PrismPulsed, origin, origin, source.Kind,
                radius: support.Radius, weaponId: source.ContentId, sourceStructureId: source.Id,
                simulationSeconds: clockSeconds, colorEffect: pulse);
            float blotArmor = mechanics.CorpseBlots ? balance.BlotReduction : 0;
            foreach (var enemy in targets)
            {
                if (color == InkColor.Green && EnemyEffects.ArmorBeforeReduction(enemy, effects, blotArmor) <= 0) continue;
                EnemyEffects.ApplyStatus(enemy, pulse, source.Kind, source.ContentId);
                if (color == InkColor.Blue && pulse.Strength < 1 && enemy.BlueEffect.Active.Strength == pulse.Strength)
                    statistics?.RecordPrismSlow(enemy);
                if (color == InkColor.Green && enemy.GreenEffect.Active.Strength == pulse.Strength)
                    statistics?.RecordArmorMark(enemy);
            }
        }

        void FireSpikeTrap(StructureState source, float damageMultiplier, RunStatistics statistics)
        {
            var support = source.ResolvedEntry.Support;
            var origin = new Point2(source.Cell.X + .5f, source.Cell.Y + .5f);
            spatial.Query(origin, support.Radius, targets);
            if (targets.Count == 0) return;
            source.NextSpikePulseAt = clockSeconds + ResolvedSupport.SpikeIntervalSeconds;
            statistics?.RecordLaunch(source.ContentId);
            visualEvents?.Publish(visualTick, SimulationEventKind.SpikesTriggered, origin, origin, source.Kind,
                radius: support.Radius, weaponId: source.ContentId, sourceStructureId: source.Id, simulationSeconds: clockSeconds);
            float blotArmor = mechanics.CorpseBlots ? balance.BlotReduction : 0;
            foreach (var enemy in targets)
                EnemyEffects.ApplyHit(enemy, support.SpikeDamage * damageMultiplier, default, statistics, source.Kind, effects,
                    blotArmor, source.ContentId, visualEvents, visualTick, clockSeconds, source.Id);
        }
    }
}
