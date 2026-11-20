using PaperDefense.Core;

namespace PaperDefense.Presentation
{
    internal static class EnemyFamilyNames
    {
        // In EnemyKind order. A family changes the name and silhouette of each combat role.
        static readonly string[] Ink = { "Каракули", "Бегуны", "Грызуны", "Плевуны", "Громилы", "Щитоносцы" };
        static readonly string[] Undead = { "Костяки", "Костегоны", "Костоломы", "Звонари", "Склепоносцы", "Стражи склепа" };
        static readonly string[] Clockwork = { "Заводники", "Пружинники", "Дыроколы", "Метатели", "Маховики", "Панцирники" };

        internal static string BossName(EnemyFamily family, BossRank boss)
        {
            if (boss == BossRank.None) return "";
            bool finale = boss == BossRank.Finale;
            return family switch
            {
                EnemyFamily.Undead => finale ? "Король склепа" : "Костяной таран",
                EnemyFamily.Clockwork => finale ? "Архивариус" : "Пресс-паук",
                _ => finale ? "Кляксокрыл" : "Перокус"
            };
        }

        internal static string Title(EnemyFamily family) => family switch
        {
            EnemyFamily.Undead => "Нежить",
            EnemyFamily.Clockwork => "Механизмы",
            _ => "Чернильная орда"
        };

        internal static string Plural(EnemyFamily family, EnemyKind kind)
        {
            var names = family switch
            {
                EnemyFamily.Undead => Undead,
                EnemyFamily.Clockwork => Clockwork,
                _ => Ink
            };
            return names[(int)kind];
        }
    }
}
