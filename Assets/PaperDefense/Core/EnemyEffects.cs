using System;

namespace PaperDefense.Core
{
    public static class EnemyEffects
    {
        public static float DrawingHealthMultiplier(DrawingTier tier) =>
            tier == DrawingTier.Hatched ? 1.7f : tier == DrawingTier.Outlined ? 1.3f : 1;

        public static float Defense(EnemyState enemy, float blotReduction = .15f)
        {
            if (enemy == null) return 0;
            float defense = enemy.DrawingTier == DrawingTier.Hatched ? .15f :
                enemy.DrawingTier == DrawingTier.Outlined ? .08f : 0;
            if (enemy.BlotRemaining > 0) defense += Math.Max(0, blotReduction);
            if (enemy.GreenRemaining > 0) defense -= .2f;
            return Math.Max(0, Math.Min(.95f, defense));
        }

        public static float MovementMultiplier(EnemyState enemy, float blotSpeedBonus = .15f)
        {
            if (enemy == null) return 1;
            float multiplier = enemy.BlotRemaining > 0 ? 1 + Math.Max(0, blotSpeedBonus) : 1;
            return enemy.BlueRemaining > 0 ? Math.Min(multiplier, .65f) : multiplier;
        }

        public static float ApplyHit(EnemyState enemy, float raw, InkColor color, RunStatistics statistics,
            StructureKind weapon, float blotReduction = .15f)
        {
            if (enemy == null || enemy.Health <= 0 || raw < 0 || float.IsNaN(raw) || float.IsInfinity(raw)) return 0;
            switch (color)
            {
                case InkColor.Red:
                    if (enemy.RedRemaining <= 0) enemy.RedTickRemaining = .25f;
                    enemy.RedRemaining = 3;
                    enemy.RedSourceWeapon = weapon;
                    break;
                case InkColor.Blue: enemy.BlueRemaining = 3; break;
                case InkColor.Green: enemy.GreenRemaining = 3; break;
            }
            float actual = Math.Min(enemy.Health, raw * (1 - Defense(enemy, blotReduction)));
            enemy.Health = Math.Max(0, enemy.Health - actual);
            statistics?.RecordDamage(weapon, actual);
            return actual;
        }

        /// <summary>Each status has one refreshed timer. Red pulses do not apply further colors.</summary>
        public static void Advance(EnemyState enemy, float delta, RunStatistics statistics)
        {
            if (enemy == null || delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            float redActiveSeconds = Math.Min(delta, Math.Max(0, enemy.RedRemaining));
            enemy.BlotRemaining = Math.Max(0, enemy.BlotRemaining - delta);
            enemy.BlueRemaining = Math.Max(0, enemy.BlueRemaining - delta);
            enemy.GreenRemaining = Math.Max(0, enemy.GreenRemaining - delta);
            enemy.RedRemaining = Math.Max(0, enemy.RedRemaining - delta);
            if (redActiveSeconds > 0 && enemy.Health > 0)
            {
                if (enemy.RedTickRemaining <= 0) enemy.RedTickRemaining = .25f;
                enemy.RedTickRemaining -= redActiveSeconds;
                while (enemy.RedTickRemaining <= .000001f && enemy.Health > 0)
                {
                    float actual = Math.Min(1, enemy.Health);
                    enemy.Health = Math.Max(0, enemy.Health - actual);
                    statistics?.RecordDamage(enemy.RedSourceWeapon, actual);
                    enemy.RedTickRemaining += .25f;
                }
            }
            if (enemy.Health <= 0) enemy.RedRemaining = 0;
            if (enemy.RedRemaining <= 0) enemy.RedTickRemaining = 0;
        }
    }
}
