using System;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>One binding, anchor and layer contract for world, album and trial composition.</summary>
    public static class NotebookTowerComposition
    {
        static readonly MeshPen uiPen = new MeshPen(null, 4096);
        public static bool UsesResourceArt(TowerBlueprint blueprint)
            => blueprint != null && !blueprint.HasPlayerDrawing && blueprint.Content != null
                && !blueprint.Content.IsLegacyAdapter && ReferenceDrawingCache.UsesTemplateVisual(blueprint)
                && NotebookVisualThemeAsset.Default != null;
        internal static bool UsesComposition(TowerBlueprint blueprint)
            => blueprint != null && (blueprint.HasPlayerDrawing || UsesResourceArt(blueprint));

        public static void DrawUI(VertexHelper target, TowerBlueprint blueprint, Vector2 center, float scale,
            MaterialTier tier = MaterialTier.Ink, InkColor ink = InkColor.None)
        {
            if (blueprint == null) return;
            uiPen.Clear();
            DrawStatic(uiPen, blueprint, Vector2.zero, tier, ink);
            DrawMounts(uiPen, blueprint, Vector2.zero, tier, ink, default, false, null, 0, 0);
            uiPen.CopyTo(target, center, scale);
        }

        /// <summary>Animated gallery composition. Aim targets use blueprint-local coordinates before UI scaling.</summary>
        public static void DrawPreviewUI(VertexHelper target, TowerBlueprint blueprint, Vector2 center, float scale,
            NotebookAnimationState animation, int structureId, double seconds, Func<int, Point2> aim = null,
            MaterialTier tier = MaterialTier.Ink, InkColor ink = InkColor.None, NotebookVisualThemeAsset theme = null)
        {
            if (blueprint == null) return;
            uiPen.Clear();
            DrawStatic(uiPen, blueprint, Vector2.zero, tier, ink, theme: theme);
            DrawMounts(uiPen, blueprint, Vector2.zero, tier, ink, default, false, animation, structureId, seconds,
                theme: theme, overrideAim: aim);
            uiPen.CopyTo(target, center, scale);
        }

        internal static void DrawStatic(MeshPen pen, TowerBlueprint blueprint, Vector2 at, MaterialTier tier,
            InkColor ink, float opacity = 1, NotebookVisualThemeAsset theme = null)
        {
            Color tint = MeshPen.Alpha(DoodleGlyphs.TierColor(tier), opacity);
            bool authored = blueprint.HasPlayerDrawing;
            var geometry = authored ? AuthoredGeometryCache.For(blueprint.Drawing) : null;
            theme = theme ?? NotebookVisualThemeAsset.Default;
            foreach (var template in DrawingTemplateCatalog.Create(blueprint))
            {
                if (template.Role == DrawingPartRole.Mount) continue;
                Vector2 pivot = at + new Vector2(template.Pivot.X, template.Pivot.Y);
                if (authored) { pen.Append(geometry.GetPart(template.BindingId), pivot, 0, tint); continue; }
                string id = template.Role == DrawingPartRole.Base ? blueprint.ChassisId : null;
                if (template.Role == DrawingPartRole.Module)
                    foreach (var module in blueprint.ModuleSelections)
                        if (DrawingBindings.Module(module.InstanceId) == template.BindingId) { id = module.DefinitionId; break; }
                if (id == null) continue;
                if (!NotebookArtRenderer.Draw(pen, theme?.FindShape(id), pivot, strokeTint: MeshPen.Alpha(tint, 1), opacity: opacity, theme: theme))
                    pen.Append(AuthoredGeometryCache.For(ReferenceDrawingCache.For(blueprint)).GetPart(template.BindingId), pivot, 0, tint);
            }
        }

        internal static void DrawMounts(MeshPen pen, TowerBlueprint blueprint, Vector2 origin, MaterialTier tier,
            InkColor ink, StructureView structure, bool aimed, NotebookAnimationState animation, int structureId,
            double seconds, float opacity = 1, NotebookVisualThemeAsset theme = null, Func<int, Point2> overrideAim = null)
        {
            theme = theme ?? NotebookVisualThemeAsset.Default;
            Color tint = MeshPen.Alpha(blueprint.HasPlayerDrawing || ink == InkColor.None
                ? DoodleGlyphs.TierColor(tier) : DoodleGlyphs.InkTone(ink), opacity);
            var authored = blueprint.HasPlayerDrawing ? AuthoredGeometryCache.For(blueprint.Drawing) : null;
            foreach (var mount in blueprint.WeaponMounts)
            {
                Point2 local = DrawingTemplateCatalog.Pivot(blueprint, DrawingBindings.Mount(mount.SlotIndex));
                Vector2 pivot = origin + new Vector2(local.X, local.Y);
                Point2 aim = overrideAim != null ? overrideAim(mount.SlotIndex) : aimed ? structure.GetMountAim(mount.SlotIndex) : default;
                Vector2 forward = Direction(aim, pivot);
                float angle = Mathf.Atan2(forward.y, forward.x) - Mathf.PI * .5f;
                pivot += forward * (animation?.Recoil(structureId, mount.SlotIndex, seconds, theme) ?? 0);
                if (authored != null) pen.Append(authored.GetPart(DrawingBindings.Mount(mount.SlotIndex)), pivot, angle, tint);
                else if (!NotebookArtRenderer.Draw(pen, theme?.FindShape(mount.WeaponId), pivot, radians: angle, strokeTint: MeshPen.Alpha(tint, 1), opacity: opacity, theme: theme))
                    pen.Append(AuthoredGeometryCache.For(ReferenceDrawingCache.For(blueprint)).GetPart(DrawingBindings.Mount(mount.SlotIndex)), pivot, angle, tint);
            }
        }

        internal static Vector2 Muzzle(StructureView structure, int slot, Point2 aim, double seconds,
            NotebookAnimationState animation, NotebookVisualThemeAsset theme)
        {
            Vector2 origin = new Vector2(structure.Cell.X + .5f, structure.Cell.Y + .5f);
            Point2 point = structure.Blueprint == null ? new Point2(0, .04f)
                : DrawingTemplateCatalog.Pivot(structure.Blueprint, DrawingBindings.Mount(slot));
            Vector2 pivot = origin + new Vector2(point.X, point.Y);
            Vector2 forward = Direction(aim, pivot), side = new Vector2(forward.y, -forward.x);
            string weapon = structure.Blueprint?.GetWeaponId(slot) ?? structure.ContentId;
            var recoil = theme?.FindRecoil(weapon);
            Vector2 muzzle = recoil == null ? new Vector2(0, .5f) : recoil.Muzzle;
            if (structure.Blueprint?.IsFreeform == true)
            {
                var barrel = structure.Blueprint.FreeformDesign.GetBarrel(slot);
                if (barrel != null) muzzle = new Vector2(barrel.MuzzleOffset.X, barrel.MuzzleOffset.Y);
            }
            return pivot + side * muzzle.x + forward * (muzzle.y + (animation?.Recoil(structure.Id, slot, seconds, theme) ?? 0));
        }
        internal static Vector2 SegmentOrigin(StructureView structure, int slot, Point2 aim, Vector2 recordedFrom,
            double seconds, NotebookAnimationState animation, NotebookVisualThemeAsset theme)
        {
            Vector2 source = new Vector2(structure.Cell.X + .5f, structure.Cell.Y + .5f);
            return (recordedFrom - source).sqrMagnitude <= .00000001f
                ? Muzzle(structure, slot, aim, seconds, animation, theme) : recordedFrom;
        }
        static Vector2 Direction(Point2 aim, Vector2 pivot)
        {
            if (aim.X == 0 && aim.Y == 0) return Vector2.up;
            Vector2 direction = new Vector2(aim.X, aim.Y) - pivot;
            return direction.sqrMagnitude < .00001f ? Vector2.up : direction.normalized;
        }
    }
}
