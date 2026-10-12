using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PaperDefense.Core;

namespace PaperDefense.Presentation
{
    /// <summary>Resolved reference strokes shared by world, album thumbnails and trial playback.</summary>
    internal static class ReferenceDrawingCache
    {
        static readonly ConditionalWeakTable<TowerBlueprint, DrawingSnapshot> drawings = new ConditionalWeakTable<TowerBlueprint, DrawingSnapshot>();
        sealed class VisualChoice { internal readonly bool Template; internal VisualChoice(bool value) { Template=value; } }
        static readonly ConditionalWeakTable<TowerBlueprint, VisualChoice> choices = new ConditionalWeakTable<TowerBlueprint, VisualChoice>();
        static readonly ConditionalWeakTable<TowerBlueprint, VisualChoice>.CreateValueCallback createChoice = blueprint => new VisualChoice(ComputeTemplateVisual(blueprint));
        public static DrawingSnapshot For(TowerBlueprint blueprint) => drawings.GetValue(blueprint, Build);
        static DrawingSnapshot Build(TowerBlueprint blueprint)
        {
            var parts = new List<DrawingPart>();
            foreach (var template in DrawingTemplateCatalog.Create(blueprint)) parts.Add(new DrawingPart(template.BindingId, template.Contours));
            return new DrawingSnapshot(parts);
        }
        public static bool UsesTemplateVisual(TowerBlueprint blueprint)
            => blueprint != null && choices.GetValue(blueprint,createChoice).Template;
        static bool ComputeTemplateVisual(TowerBlueprint blueprint)
        {
            if (blueprint != null && blueprint.WeaponMounts.Count > 2) return true;
            if (blueprint?.Content == null || blueprint.Content.IsLegacyAdapter) return false;
            if (!blueprint.Content.TryGetChassis(blueprint.ChassisId, out var chassis) || !blueprint.Content.TryGetWeapon(blueprint.FirstWeaponId, out var first)) return false;
            if (chassis.VisualStyle == DrawingVisualStyle.Template || first.VisualStyle == DrawingVisualStyle.Template ||
                LegacyContentIds.Chassis(blueprint.ChassisId) == (ChassisKind)(-1) || LegacyContentIds.Weapon(blueprint.FirstWeaponId) == (StructureKind)(-1)) return true;
            foreach (var mount in blueprint.WeaponMounts)
                if (blueprint.Content.TryGetWeapon(mount.WeaponId, out var weapon) &&
                    (weapon.VisualStyle == DrawingVisualStyle.Template || LegacyContentIds.Weapon(mount.WeaponId) == (StructureKind)(-1))) return true;
            foreach (var selected in blueprint.ModuleSelections)
                if (blueprint.Content.TryGetModule(selected.DefinitionId, out var module) &&
                    (module.VisualStyle == DrawingVisualStyle.Template || LegacyContentIds.Module(selected.DefinitionId) == TowerModules.None)) return true;
            return false;
        }
        public static DrawingSnapshot VisibleDrawing(TowerBlueprint blueprint)
            => blueprint.Origin == DrawingOrigin.PlayerDrawing ? blueprint.Drawing : For(blueprint);
    }
}
