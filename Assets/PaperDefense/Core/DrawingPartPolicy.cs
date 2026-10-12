using System;

namespace PaperDefense.Core
{
    /// <summary>Assembly bindings shared by drawing commands, saved pages and reference templates.</summary>
    public static class DrawingPartPolicy
    {
        public static bool IsEnabled(TowerBlueprint blueprint, DrawingPartKey key)
        {
            if (blueprint == null) return false;
            switch (key)
            {
                case DrawingPartKey.Base: case DrawingPartKey.Mount0: case DrawingPartKey.Decoration: return true;
                case DrawingPartKey.Mount1: return blueprint.SecondWeapon.HasValue;
                default:
                    TowerModules module = ModuleFor(key);
                    return module != TowerModules.None && (blueprint.Modules & module) != 0;
            }
        }

        public static bool ShouldRetain(DrawingPartKey key, TowerBlueprint before, ChassisKind afterChassis,
            StructureKind afterFirstWeapon, StructureKind? afterSecondWeapon, TowerModules afterModules)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            switch (key)
            {
                case DrawingPartKey.Base: return before.Chassis == afterChassis;
                case DrawingPartKey.Mount0: return before.FirstWeapon == afterFirstWeapon;
                case DrawingPartKey.Mount1: return afterSecondWeapon.HasValue && before.SecondWeapon == afterSecondWeapon;
                case DrawingPartKey.Decoration: return true;
                default:
                    TowerModules module = ModuleFor(key);
                    return module != TowerModules.None && (before.Modules & afterModules & module) != 0;
            }
        }

        static TowerModules ModuleFor(DrawingPartKey key)
        {
            switch (key)
            {
                case DrawingPartKey.Armor: return TowerModules.Armor;
                case DrawingPartKey.Feed: return TowerModules.Feed;
                case DrawingPartKey.Tracking: return TowerModules.Tracking;
                case DrawingPartKey.Rail: return TowerModules.Rail;
                default: return TowerModules.None;
            }
        }
    }
}
