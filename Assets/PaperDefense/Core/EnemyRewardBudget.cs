namespace PaperDefense.Core
{
    internal static class EnemyRewardBudget
    {
        internal static int Consume(EnemyState enemy, int fallback, bool requireAssigned)
        {
            if (enemy == null) return 0;
            int reward = enemy.RewardBudget >= 0 ? enemy.RewardBudget : requireAssigned ? 0 : System.Math.Max(0, fallback);
            enemy.RewardBudget = 0;
            return reward;
        }

        internal static bool Transfer(EnemyState parent, EnemyState child, int amount)
        {
            if (parent == null || child == null || ReferenceEquals(parent, child) || amount < 0 ||
                parent.RewardBudget < amount || child.RewardBudget > 0) return false;
            parent.RewardBudget -= amount;
            child.RewardBudget = amount;
            return true;
        }
    }
}
