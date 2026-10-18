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
            => blueprint != null && !blueprint.HasPlayerDrawing;
        internal static bool UsesComposition(TowerBlueprint blueprint)
            => blueprint != null;

        public static bool TryGetBounds(TowerBlueprint blueprint, out Rect bounds)
        {
            uiPen.Clear();
            if (blueprint != null)
            {
                DrawStatic(uiPen, blueprint, Vector2.zero, MaterialTier.Ink, InkColor.None);
                DrawMounts(uiPen, blueprint, Vector2.zero, MaterialTier.Ink, InkColor.None, default, false, null, 0, 0);
            }
            bool found = uiPen.TryGetBounds(out bounds);
            if (found && NotebookMountLayout.Supports(blueprint))
            {
                // Reserve the whole rotation/recoil envelope once, so aiming never clips or pumps the UI scale.
                foreach (var selected in blueprint.WeaponMounts)
                {
                    var mount = NotebookMountLayout.Resolve(blueprint, selected.SlotIndex);
                    float radius = mount.MotionRadius(NotebookVisualThemeAsset.Default?.FindRecoil(mount.WeaponId)?.Distance ?? 0);
                    bounds = Union(bounds, new Rect(mount.RotationCenter - Vector2.one * radius, Vector2.one * radius * 2));
                }
                if (blueprint.Reinforcement != null) bounds = Union(bounds, new Rect(-.65f, -.65f, 1.3f, 1.3f));
            }
            return found;
        }
        static Rect Union(Rect left, Rect right) => Rect.MinMaxRect(Mathf.Min(left.xMin, right.xMin),
            Mathf.Min(left.yMin, right.yMin), Mathf.Max(left.xMax, right.xMax), Mathf.Max(left.yMax, right.yMax));
        static bool UsesStandardLayout(TowerBlueprint blueprint, NotebookVisualThemeAsset theme)
        {
            if (!NotebookMountLayout.Supports(blueprint)) return false;
            foreach (var selected in blueprint.WeaponMounts)
            {
                var shape = theme?.FindShape(selected.WeaponId);
                if (shape != null && NotebookArtRenderer.IsNativeUtility(shape)) return false;
            }
            return true;
        }

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
                DoodleGlyphs.DrawResource(pen, id, pivot, opacity: opacity, theme: theme);
            }
        }

        internal static void DrawConstruction(MeshPen pen, StructureView structure, Vector2 origin, float fraction,
            double seconds = 0, NotebookAnimationState animation = null, NotebookVisualThemeAsset theme = null)
        {
            var blueprint = structure.Blueprint;
            if (!blueprint.HasPlayerDrawing)
            {
                float opacity = Mathf.Lerp(.2f, 1, Mathf.Clamp01(fraction));
                DrawStatic(pen, blueprint, origin, structure.Tier, structure.Color, opacity, theme);
                DrawMounts(pen, blueprint, origin, structure.Tier, structure.Color, structure, true,
                    animation, structure.Id, seconds, opacity, theme);
                return;
            }
            var geometry = AuthoredGeometryCache.For(blueprint.Drawing);
            foreach (var template in DrawingTemplateCatalog.Create(blueprint))
            {
                Vector2 pivot = origin + new Vector2(template.Pivot.X, template.Pivot.Y);
                pen.AppendRevealed(geometry.GetPart(template.BindingId), pivot, 0,
                    DoodleGlyphs.TierColor(structure.Tier), fraction);
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
            if (UsesStandardLayout(blueprint, theme))
            {
                // Draw order is independent of slot identity; a missing neighbour never renumbers a socket.
                for (int layer = 0; layer < 4; layer++)
                for (int order = 0; order < 3; order++)
                    foreach (var mount in blueprint.WeaponMounts)
                    {
                        var resolved = NotebookMountLayout.Resolve(blueprint, mount.SlotIndex);
                        if (resolved.DrawOrder != order) continue;
                        Point2 target = overrideAim != null ? overrideAim(mount.SlotIndex)
                            : aimed ? structure.GetMountAim(mount.SlotIndex) : default;
                        resolved.Draw(pen, origin, resolved.AimAngle(target, origin),
                            animation?.Recoil(structureId, mount.SlotIndex, seconds, theme) ?? 0,
                            opacity, NotebookArtRenderer.RasterTint(theme), layer);
                    }
                return;
            }
            foreach (var mount in blueprint.WeaponMounts)
            {
                Point2 local = DrawingTemplateCatalog.Pivot(blueprint, DrawingBindings.Mount(mount.SlotIndex));
                Vector2 pivot = origin + new Vector2(local.X, local.Y);
                Point2 aim = overrideAim != null ? overrideAim(mount.SlotIndex) : aimed ? structure.GetMountAim(mount.SlotIndex) : default;
                Vector2 forward = Direction(aim, pivot);
                float angle = Mathf.Atan2(forward.y, forward.x) - Mathf.PI * .5f;
                // A quiet mounting ring separates the rotating weapon from the authored body.
                if (aimed && pen.WorldUnitsPerPixel > 0)
                    pen.Ring(pivot, .065f, .018f, MeshPen.Alpha(NotebookPalette.Navy, opacity * .65f), 12);
                pivot += forward * (animation?.Recoil(structureId, mount.SlotIndex, seconds, theme) ?? 0);
                if (authored != null) pen.Append(authored.GetPart(DrawingBindings.Mount(mount.SlotIndex)), pivot, angle, tint);
                else DoodleGlyphs.DrawResource(pen, mount.WeaponId, pivot, radians: angle, opacity: opacity, theme: theme);
            }
        }

        internal static Vector2 Muzzle(StructureView structure, int slot, Point2 aim, double seconds,
            NotebookAnimationState animation, NotebookVisualThemeAsset theme)
        {
            Vector2 origin = new Vector2(structure.Cell.X + .5f, structure.Cell.Y + .5f);
            if (UsesStandardLayout(structure.Blueprint, theme))
            {
                var mount = NotebookMountLayout.Resolve(structure.Blueprint, slot);
                // Effect endpoints may be individual fan rays or an extended piercer ray.
                // Their origin follows the rendered mount pose, not the direction of that segment.
                return origin + mount.Transform(mount.Muzzle, mount.AimAngle(structure.GetMountAim(slot), origin),
                    animation?.Recoil(structure.Id, slot, seconds, theme) ?? 0);
            }
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
