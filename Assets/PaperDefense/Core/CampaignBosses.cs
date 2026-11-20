namespace PaperDefense.Core
{
    internal static class CampaignBosses
    {
        internal static EnemyDefinition CreateStats(GameBalance balance, BossRank rank)
        {
            if (rank == BossRank.None) return null;
            bool siege = rank == BossRank.Midpoint;
            var stats = balance.Enemy(siege ? EnemyKind.Gnawer : EnemyKind.Spitter).CreateSnapshot();
            stats.Attack = siege ? EnemyAttackMode.Contact : EnemyAttackMode.Glob;
            // Keep the configured role's windup and flight time so its telegraph matches the attack.
            switch (balance.EnemyFamily, rank)
            {
                case (EnemyFamily.Ink, BossRank.Midpoint):       // Nib beetle: quick repeated bites.
                    Tune(stats, health: 7, speed: 1.20f, damage: 1.2f, interval: .75f, size: 2.0f);
                    break;
                case (EnemyFamily.Ink, BossRank.Finale):         // Ink manta: mobile, short-range volleys.
                    Tune(stats, health: 18, speed: 1.20f, damage: 1.5f, interval: .70f, size: 2.50f, range: .70f);
                    break;
                case (EnemyFamily.Undead, BossRank.Midpoint):    // Bone ram: heavy blows with a long recovery.
                    Tune(stats, health: 9, speed: 1.05f, damage: 2.4f, interval: 1.55f, size: 2.20f);
                    break;
                case (EnemyFamily.Undead, BossRank.Finale):      // Crypt king: long reach and a slow, heavy toll.
                    Tune(stats, health: 23, speed: .78f, damage: 2.7f, interval: 1.35f, size: 2.65f, range: 1.12f);
                    break;
                case (EnemyFamily.Clockwork, BossRank.Midpoint): // Press spider: sustained siege pressure.
                    Tune(stats, health: 11, speed: .85f, damage: 1.85f, interval: 1.05f, size: 2.30f);
                    break;
                case (EnemyFamily.Clockwork, BossRank.Finale):   // Archivist: fast card feed at medium range.
                    Tune(stats, health: 26, speed: .92f, damage: 1.4f, interval: .55f, size: 2.75f, range: .90f);
                    break;
            }
            return stats;
        }

        static void Tune(EnemyDefinition stats, float health, float speed, float damage, float interval, float size, float range = 1)
        {
            stats.MaxHealth *= health;
            stats.Speed *= speed;
            stats.Damage *= damage;
            stats.AttackInterval *= interval;
            stats.VisualScale *= size;
            stats.Range *= range;
        }
    }
}
