using System;
using System.Collections.Generic;

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

        public static bool IsEnabled(TowerBlueprint blueprint, string bindingId)
        {
            if (blueprint == null || bindingId == null) return false;
            if (bindingId == DrawingBindings.Base || bindingId == DrawingBindings.Mount0 || bindingId == DrawingBindings.Decoration) return true;
            if (bindingId == DrawingBindings.Mount1) return blueprint.SecondWeaponId != null;
            foreach (var module in blueprint.ModuleSelections)
                if (DrawingBindings.Module(module.InstanceId) == bindingId) return true;
            return false;
        }

        public static bool ShouldRetain(string bindingId, TowerBlueprint before, string afterChassis,
            string afterFirstWeapon, string afterSecondWeapon, IEnumerable<ModuleSelection> afterModules)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (bindingId == DrawingBindings.Base) return before.ChassisId == afterChassis;
            if (bindingId == DrawingBindings.Mount0) return before.FirstWeaponId == afterFirstWeapon;
            if (bindingId == DrawingBindings.Mount1) return afterSecondWeapon != null && before.SecondWeaponId == afterSecondWeapon;
            if (bindingId == DrawingBindings.Decoration) return true;
            foreach (var old in before.ModuleSelections)
                if (DrawingBindings.Module(old.InstanceId) == bindingId && afterModules != null)
                    foreach (var next in afterModules)
                        if (next.InstanceId == old.InstanceId && next.DefinitionId == old.DefinitionId) return true;
            return false;
        }

        public static bool ShouldRetain(string bindingId, TowerBlueprint before, TowerBlueprint after)
        {
            if (after == null) throw new ArgumentNullException(nameof(after));
            return ShouldRetain(bindingId, before, after.ChassisId, after.FirstWeaponId, after.SecondWeaponId, after.ModuleSelections);
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
