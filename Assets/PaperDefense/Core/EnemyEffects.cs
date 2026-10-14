using System;

namespace PaperDefense.Core
{
    public static class EnemyEffects
    {
        static readonly EffectsRules Historical = new EffectsRules();

        public static float ApplyHit(EnemyState enemy, float raw, ColorEffectPayload payload, RunStatistics statistics,
            StructureKind weapon, EffectsRules defenseRules, float blotReduction = .15f,string weaponId=null,
            SimulationEventBuffer events=null,long tick=0,double simulationSeconds=0,int sourceStructureId=0,int mountSlotIndex=-1,long actionId=0)
        {
            if (defenseRules == null) throw new ArgumentNullException(nameof(defenseRules));
            if (enemy == null || enemy.Health <= 0 || raw < 0 || float.IsNaN(raw) || float.IsInfinity(raw)) return 0;
            switch (payload.Color)
            {
                case InkColor.Red:
                    if (enemy.RedEffect == null) enemy.RedEffect = new TimedEffectState(InkColor.Red);
                    enemy.RedEffect.TryApply(payload, weapon,weaponId);
                    SyncRed(enemy);
                    break;
                case InkColor.Blue:
                    if (enemy.BlueEffect == null) enemy.BlueEffect = new TimedEffectState(InkColor.Blue);
                    enemy.BlueEffect.TryApply(payload, weapon,weaponId);
                    enemy.BlueRemaining = enemy.BlueEffect.Remaining;
                    break;
                case InkColor.Green:
                    if (enemy.GreenEffect == null) enemy.GreenEffect = new TimedEffectState(InkColor.Green);
                    enemy.GreenEffect.TryApply(payload, weapon,weaponId);
                    enemy.GreenRemaining = enemy.GreenEffect.Remaining;
                    break;
            }
            float actual = Math.Min(enemy.Health, DamageRules.Apply(raw, Defense(enemy, defenseRules, blotReduction), defenseRules.MaximumDefense));
            enemy.Health = Math.Max(0, enemy.Health - actual);
            statistics?.RecordDamage(weaponId??LegacyContentIds.Weapon(weapon), actual);
            PublishDamage(enemy,enemy.Health+actual,events,tick,simulationSeconds,weapon,weaponId,sourceStructureId,mountSlotIndex,actionId);
            return actual;
        }

        public static void ApplySlow(EnemyState enemy, float speedMultiplier, float duration)
        {
            ColorEffectPayload payload = ColorEffectPayload.Slow(speedMultiplier, duration);
            TimedEffectState.Validate(payload);
            if (enemy == null || enemy.Health <= 0) return;
            if (enemy.BlueEffect == null) enemy.BlueEffect = new TimedEffectState(InkColor.Blue);
            enemy.BlueEffect.TryApply(payload, StructureKind.Gun);
            enemy.BlueRemaining = enemy.BlueEffect.Remaining;
        }

        public static float DrawingHealthMultiplier(DrawingTier tier) => DrawingHealthMultiplier(tier, Historical);
        public static float DrawingHealthMultiplier(DrawingTier tier, EffectsRules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            return tier == DrawingTier.Hatched ? rules.HatchedHealthMultiplier :
                tier == DrawingTier.Outlined ? rules.OutlinedHealthMultiplier : 1;
        }

        public static float Defense(EnemyState enemy, float blotReduction = .15f)
            => Defense(enemy, Historical, blotReduction);
        public static float Defense(EnemyState enemy, EffectsRules rules, float blotReduction = .15f)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (enemy == null) return 0;
            float defense = enemy.DrawingTier == DrawingTier.Hatched ? rules.HatchedDefense :
                enemy.DrawingTier == DrawingTier.Outlined ? rules.OutlinedDefense : 0;
            if (enemy.BlotRemaining > 0) defense += Math.Max(0, blotReduction);
            if (enemy.GreenEffect != null)
            { if (enemy.GreenEffect.HasActive) defense -= enemy.GreenEffect.Active.Strength; }
            else if (enemy.GreenRemaining > 0) defense -= rules.GreenDefenseReduction;
            return DamageRules.EffectiveDefense(defense, rules.MaximumDefense);
        }

        public static float MovementMultiplier(EnemyState enemy, float blotSpeedBonus = .15f)
            => MovementMultiplier(enemy, Historical, blotSpeedBonus);
        public static float MovementMultiplier(EnemyState enemy, EffectsRules rules, float blotSpeedBonus = .15f)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (enemy == null) return 1;
            float multiplier = enemy.BlotRemaining > 0 ? 1 + Math.Max(0, blotSpeedBonus) : 1;
            if (enemy.BlueEffect != null)
                return enemy.BlueEffect.HasActive ? Math.Min(multiplier, enemy.BlueEffect.Active.Strength) : multiplier;
            return enemy.BlueRemaining > 0 ? Math.Min(multiplier, rules.BlueSpeedMultiplier) : multiplier;
        }

        public static float ApplyHit(EnemyState enemy, float raw, InkColor color, RunStatistics statistics,
            StructureKind weapon, float blotReduction = .15f)
            => ApplyHit(enemy, raw, color, statistics, weapon, Historical, blotReduction);
        public static float ApplyHit(EnemyState enemy, float raw, InkColor color, RunStatistics statistics,
            StructureKind weapon, EffectsRules rules, float blotReduction = .15f)
            => ApplyHit(enemy, raw, ColorEffectPayload.Capture(color, rules), statistics, weapon, rules, blotReduction);

        /// <summary>Different strengths keep independent timers; only the strongest acts.</summary>
        public static void Advance(EnemyState enemy, float delta, RunStatistics statistics)
            => Advance(enemy, delta, statistics, Historical);
        public static void Advance(EnemyState enemy, float delta, RunStatistics statistics, EffectsRules rules,
            float blotReduction = .15f,SimulationEventBuffer events=null,long tick=0,double simulationSeconds=0)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (enemy == null || delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            if (enemy.BlueEffect != null)
            {
                enemy.BlueEffect.Advance(delta, enemy, rules, statistics, blotReduction);
                enemy.BlueRemaining = enemy.BlueEffect.Remaining;
            }
            else enemy.BlueRemaining = Math.Max(0, enemy.BlueRemaining - delta);

            // Defense is piecewise constant. Resolve red pulses before expiring the
            // defense that protected their interval, then continue with the next one.
            // This is bounded by the stored defense strengths, not by frame duration.
            double remaining = delta;
            while (remaining > .0000001)
            {
                float step = (float)remaining;
                if (enemy.GreenRemaining > 0) step = Math.Min(step, enemy.GreenRemaining);
                if (enemy.BlotRemaining > 0) step = Math.Min(step, enemy.BlotRemaining);
                AdvanceRed(enemy, step, statistics, rules, blotReduction, events, tick, simulationSeconds);
                AdvanceDefense(enemy, step, statistics, rules, blotReduction);
                remaining -= step;
            }
        }

        static void AdvanceDefense(EnemyState enemy, float delta, RunStatistics statistics, EffectsRules rules, float blotReduction)
        {
            enemy.BlotRemaining = RemainingAfter(enemy.BlotRemaining, delta);
            if (enemy.GreenEffect != null)
            {
                enemy.GreenEffect.Advance(delta, enemy, rules, statistics, blotReduction);
                enemy.GreenRemaining = enemy.GreenEffect.Remaining;
            }
            else enemy.GreenRemaining = RemainingAfter(enemy.GreenRemaining, delta);
        }

        static float RemainingAfter(float remaining, float delta)
            => remaining - delta <= .000001f ? 0 : remaining - delta;

        static void AdvanceRed(EnemyState enemy, float delta, RunStatistics statistics, EffectsRules rules,
            float blotReduction, SimulationEventBuffer events, long tick, double simulationSeconds)
        {
            float previousHealth = enemy.Health;
            float redActiveSeconds = Math.Min(delta, Math.Max(0, enemy.RedRemaining));
            if (enemy.RedEffect != null)
            {
                var source = enemy.RedEffect.Advance(delta, enemy, rules, statistics, blotReduction);
                SyncRed(enemy);
                PublishDamage(enemy,previousHealth,events,tick,simulationSeconds,source.Weapon,source.WeaponId);
                return;
            }
            // Directly initialized historical fixtures still use their explicit scalar timers.
            enemy.RedRemaining = RemainingAfter(enemy.RedRemaining, delta);
            if (redActiveSeconds > 0 && enemy.Health > 0)
            {
                if (enemy.RedTickRemaining <= 0) enemy.RedTickRemaining = rules.RedTickSeconds;
                enemy.RedTickRemaining -= redActiveSeconds;
                while (enemy.RedTickRemaining <= .000001f && enemy.Health > 0)
                {
                    float raw = rules.RedDamagePerTick;
                    if (!rules.RedIgnoresDefense) raw = DamageRules.Apply(raw, Defense(enemy, rules, blotReduction), rules.MaximumDefense);
                    float actual = Math.Min(raw, enemy.Health);
                    enemy.Health = Math.Max(0, enemy.Health - actual);
                    statistics?.RecordDamage(enemy.RedSourceWeaponId??LegacyContentIds.Weapon(enemy.RedSourceWeapon), actual);
                    enemy.RedTickRemaining += rules.RedTickSeconds;
                }
            }
            if (enemy.Health <= 0) enemy.RedRemaining = 0;
            if (enemy.RedRemaining <= 0) enemy.RedTickRemaining = 0;
            PublishDamage(enemy,previousHealth,events,tick,simulationSeconds,enemy.RedSourceWeapon,enemy.RedSourceWeaponId);
        }

        internal static void PublishDamage(EnemyState enemy,float previousHealth,SimulationEventBuffer events,long tick,double simulationSeconds,
            StructureKind weapon=default,string weaponId=null,int sourceStructureId=0,int mountSlotIndex=-1,long actionId=0)
        {
            if(events==null||enemy==null||enemy.Health>=previousHealth)return;
            events.Publish(tick,SimulationEventKind.EnemyHit,enemy.Position,enemy.Position,weapon,entityId:enemy.Id,weaponId:weaponId,
                sourceStructureId:sourceStructureId,mountSlotIndex:mountSlotIndex,actionId:actionId,simulationSeconds:simulationSeconds,
                enemyKind:enemy.Kind,enemyDrawingTier:enemy.DrawingTier,enemyVisualScale:enemy.VisualScale);
            PublishDeath(enemy,events,tick,simulationSeconds,weapon,weaponId,sourceStructureId,mountSlotIndex,actionId);
        }

        internal static void PublishDeath(EnemyState enemy,SimulationEventBuffer events,long tick,double simulationSeconds,
            StructureKind weapon=default,string weaponId=null,int sourceStructureId=0,int mountSlotIndex=-1,long actionId=0)
        {
            if(events==null||enemy==null||enemy.Health>0||enemy.VisualDeathPublished)return;
            enemy.VisualDeathPublished=true;
            events.Publish(tick,SimulationEventKind.EnemyDied,enemy.Position,enemy.Position,weapon,entityId:enemy.Id,weaponId:weaponId,
                sourceStructureId:sourceStructureId,mountSlotIndex:mountSlotIndex,actionId:actionId,simulationSeconds:simulationSeconds,
                enemyKind:enemy.Kind,enemyDrawingTier:enemy.DrawingTier,enemyVisualScale:enemy.VisualScale);
        }

        static void SyncRed(EnemyState enemy)
        {
            enemy.RedRemaining = enemy.RedEffect.Remaining;
            enemy.RedTickRemaining = enemy.RedEffect.TickRemaining;
            enemy.RedSourceWeapon = enemy.RedEffect.Weapon;
            enemy.RedSourceWeaponId = enemy.RedEffect.WeaponId;
        }
    }
}
