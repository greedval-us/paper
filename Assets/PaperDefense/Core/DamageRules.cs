using System;

namespace PaperDefense.Core
{
    /// <summary>The common defense formula for an ordinary hit.</summary>
    public static class DamageRules
    {
        public static float EffectiveDefense(float defense, float maximumDefense)
        {
            if (float.IsNaN(maximumDefense) || float.IsInfinity(maximumDefense) || maximumDefense < 0 || maximumDefense >= 1)
                throw new ArgumentOutOfRangeException(nameof(maximumDefense));
            if (float.IsNaN(defense) || float.IsInfinity(defense)) throw new ArgumentOutOfRangeException(nameof(defense));
            return Math.Max(0, Math.Min(maximumDefense, defense));
        }

        public static float Apply(float raw, float defense, float maximumDefense)
        {
            if (raw < 0 || float.IsNaN(raw) || float.IsInfinity(raw)) return 0;
            return raw * (1 - EffectiveDefense(defense, maximumDefense));
        }

        public static float StructureDamage(float raw, StructureState structure, GameBalance balance)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            float defense=0;
            if(structure?.Blueprint!=null)
            {
                if(structure.Assembly==null)structure.Assembly=TowerCatalog.Resolve(structure.Blueprint,balance);
                defense=structure.Assembly.Modify(ModifierStat.FoundationDefense,0);
            }
            return Apply(raw,defense,balance.Effects.MaximumDefense);
        }
    }
}
