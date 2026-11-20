using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Shared rigid rotation for native assembly parts and their attachment coordinates.</summary>
    public static class NotebookMountLayout
    {
        internal static float BodyRadians(float heading) => Mathf.DeltaAngle(90, heading) * Mathf.Deg2Rad;
        internal static float BodyRadians(StructureView tower) => tower.HasDirectionalWeapon ? BodyRadians(tower.FacingDegrees) : 0;

        internal static bool HasDirectionalMount(StructureKind kind, ResolvedEntry entry, TowerBlueprint blueprint, ContentCatalogSnapshot content)
        {
            if (blueprint != null)
            {
                foreach (var mount in blueprint.WeaponMounts)
                    if (content.GetWeapon(mount.WeaponId).Attack != AttackKind.Radial) return true;
                return false;
            }
            if (entry == null) return !ConstructionToolRules.IsTool(kind);
            foreach (var weapon in entry.Weapons)
                if (weapon.Attack != AttackKind.Radial) return true;
            return false;
        }

        internal static Vector2 Rotate(Vector2 point, float radians)
        { float c = Mathf.Cos(radians), s = Mathf.Sin(radians); return new Vector2(c * point.x - s * point.y, s * point.x + c * point.y); }
    }
}
