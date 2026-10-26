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
            var entry = structure?.ResolvedEntry;
            if (entry != null && entry.Kind != AlbumEntryKind.Legacy)
                return Apply(raw, entry.Defense, .6f);
            float defense=0;
            if(structure?.Blueprint!=null)
            {
                if(structure.Assembly==null)structure.Assembly=TowerCatalog.Resolve(structure.Blueprint,balance);
                defense=structure.Assembly.Modify(ModifierStat.FoundationDefense,0);
            }
            return Apply(raw,defense,balance.Effects.MaximumDefense);
        }
        public static float StructureDamage(float raw,StructureState structure,GameBalance balance,Point2 source)
        {
            var entry = structure?.ResolvedEntry;
            if (entry != null && entry.Kind != AlbumEntryKind.Legacy)
            {
                if (balance == null) throw new ArgumentNullException(nameof(balance));
                float resolvedDefense = entry.Defense;
                if (entry.FrontDefense > resolvedDefense && InsideFoundationFront(structure, source))
                    resolvedDefense = entry.FrontDefense;
                return Apply(raw, resolvedDefense, .6f);
            }
            if(structure?.Blueprint?.Reinforcement==null)return StructureDamage(raw,structure,balance);
            if(balance==null)throw new ArgumentNullException(nameof(balance));
            if(structure.Assembly==null)structure.Assembly=TowerCatalog.Resolve(structure.Blueprint,balance);
            float defense=structure.Assembly.Modify(ModifierStat.FoundationDefense,0);
            var offset=new Point2(source.X-structure.Cell.X-.5f,source.Y-structure.Cell.Y-.5f);
            defense*=structure.Blueprint.Reinforcement.Multiplier(offset);
            return Apply(raw,defense,balance.Effects.MaximumDefense);
        }

        static bool InsideFoundationFront(StructureState structure, Point2 source)
        {
            double centerX = structure.Cell.X + .5, centerY = structure.Cell.Y + .5;
            double aimX = structure.Aim.X - centerX, aimY = structure.Aim.Y - centerY;
            double sourceX = source.X - centerX, sourceY = source.Y - centerY;
            double aimLengthSquared = aimX * aimX + aimY * aimY;
            double sourceLengthSquared = sourceX * sourceX + sourceY * sourceY;
            if (aimLengthSquared == 0 || sourceLengthSquared == 0) return false;
            double cosine = (aimX * sourceX + aimY * sourceY) / Math.Sqrt(aimLengthSquared * sourceLengthSquared);
            return cosine + .0000001 >= Math.Sqrt(.5);
        }
    }
}
