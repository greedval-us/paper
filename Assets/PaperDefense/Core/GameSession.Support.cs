using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public static class ConstructionToolRules
    {
        public static bool IsTool(StructureKind kind)=>kind==StructureKind.Wall||kind==StructureKind.Spikes||kind==StructureKind.TarPit||kind==StructureKind.RepairPost||kind==StructureKind.Generator||kind==StructureKind.Beacon||kind==StructureKind.Prism;
        public static bool IsTrap(StructureKind kind)=>kind==StructureKind.Spikes||kind==StructureKind.TarPit;
    }
    public sealed partial class GameSession
    {
        readonly List<StructureState> activeRepairPosts=new List<StructureState>();
        readonly List<StructureState> activeTarPits=new List<StructureState>();
        void ApplySupport(float delta)
        {
            activeRepairPosts.Clear();activeTarPits.Clear();
            foreach(var structure in structures)
            {
                if(!structure.IsComplete||structure.Health<=0)continue;
                if(structure.ResolvedEntry!=null)continue;
                if(structure.Kind==StructureKind.RepairPost)activeRepairPosts.Add(structure);
                else if(structure.Kind==StructureKind.TarPit)activeTarPits.Add(structure);
            }
            var modernRates=StructureSupportRules.RepairContributions(structures);
            if(activeRepairPosts.Count>0||modernRates.Count>0)foreach(var target in structures)
            {
                if(!target.IsComplete||target.Health<=0||target.Kind==StructureKind.RepairPost||target.Health>=target.MaxHealth)continue;
                modernRates.TryGetValue(target,out var contribution);
                float rate = contribution.Rate;
                StructureState repairingSource = contribution.Source;
                foreach(var post in activeRepairPosts)
                {
                    var definition=rules.Structure(post.Kind);
                    if(DistanceSquared(Center(post.Cell),Center(target.Cell))<=definition.Range*definition.Range &&
                        (definition.RepairPerSecond > rate || definition.RepairPerSecond == rate &&
                            (repairingSource == null || post.Id < repairingSource.Id)))
                    { rate = definition.RepairPerSecond; repairingSource = post; }
                }
                if(rate<=0)continue;
                float beforeRepair = target.Health;
                target.Health=Math.Min(target.MaxHealth,target.Health+rate*delta);
                Statistics.RecordRepair(target.Health - beforeRepair);
                float restored = target.Health - beforeRepair;
                if (restored > 0 && repairingSource != null && repairingSource.CanPublishSupportFeedback(Elapsed))
                    EventBuffer.Publish(TickIndex, SimulationEventKind.RepairApplied, Center(repairingSource.Cell), Center(target.Cell),
                        repairingSource.Kind, radius: repairingSource.ResolvedEntry?.Support?.Radius ?? rules.Structure(repairingSource.Kind).Range,
                        entityId: target.Id, weaponId: repairingSource.ContentId,
                        sourceStructureId: repairingSource.Id, simulationSeconds: Elapsed, magnitude: restored);
                RefreshHealthBand(target);
            }
            foreach(var trap in activeTarPits)
            {
                var definition=rules.Structure(trap.Kind);
                float radius=Math.Max(.5f,definition.Radius);
                foreach(var enemy in enemies)
                    if(enemy.Health>0&&DistanceSquared(Center(trap.Cell),enemy.Position)<=radius*radius)
                        ApplyFieldSlow(trap, enemy, definition.SlowSpeedMultiplier, Math.Max(delta, definition.SlowDuration), radius);
            }
            foreach(var source in structures)
                if(source.ResolvedEntry?.Support?.Kind==AssemblyStructureFamily.Slow)
                    foreach(var enemy in enemies)
                        if(enemy.Health>0&&StructureSupportRules.TryGetSlow(source,enemy.Position,out var effect))
                            ApplyFieldSlow(source, enemy, effect.Strength, effect.Duration, source.ResolvedEntry.Support.Radius);
        }

        void ApplyFieldSlow(StructureState source, EnemyState enemy, float strength, float duration, float radius)
        {
            float before = EnemyEffects.MovementMultiplier(enemy, enemyEffects, Mechanics.CorpseBlots ? rules.BlotSpeedBonus : 0);
            EnemyEffects.ApplySlow(enemy, strength, duration);
            float after = EnemyEffects.MovementMultiplier(enemy, enemyEffects, Mechanics.CorpseBlots ? rules.BlotSpeedBonus : 0);
            if (after < before && source.CanPublishSupportFeedback(Elapsed))
                EventBuffer.Publish(TickIndex, SimulationEventKind.SlowApplied, Center(source.Cell), enemy.Position, source.Kind,
                    radius: radius, entityId: enemy.Id, weaponId: source.ContentId, sourceStructureId: source.Id,
                    simulationSeconds: Elapsed, magnitude: after);
        }

        void PublishStructureHit(StructureState target, Point2 from, float previousHealth)
        {
            float damage = Math.Max(0, previousHealth - target.Health);
            if (damage <= 0) return;
            EventBuffer.Publish(TickIndex, SimulationEventKind.StructureHit, from, Center(target.Cell), target.Kind,
                entityId: target.Id, weaponId: target.ContentId, simulationSeconds: Elapsed, magnitude: damage);
        }
    }
}
