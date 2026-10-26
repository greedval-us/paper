using System.Linq;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Static album/workshop presentation. Recorded battle and trial poses remain explicit.</summary>
    public static class AssemblyPreviewPose
    {
        public const float Radians = -Mathf.PI / 4;

        public static AssemblyVisualPose For(RecipeSnapshot snapshot)
            => new AssemblyVisualPose(snapshot?.Recipe.Tower?.Mounts.Select(mount =>
                new AssemblyMountPose(mount.SlotIndex, Radians)));

        public static AssemblyVisualPose ForWeapon()
            => new AssemblyVisualPose(new[] { new AssemblyMountPose(0, Radians) });

        /// <summary>Fits what is drawn at this pose, without unused swept motion space.
        /// Every fragment keeps its source scale, pivot, region and shared transform.</summary>
        public static Rect VisibleBounds(AssemblyVisualComposition composition)
        {
            if (composition == null) return default;
            bool any = false; Vector2 min = Vector2.zero, max = Vector2.zero;
            foreach (var fragment in composition.Fragments)
            {
                var area = Rect.MinMaxRect(Mathf.Max(fragment.Trim.xMin, fragment.Region.xMin), Mathf.Max(fragment.Trim.yMin, fragment.Region.yMin),
                    Mathf.Min(fragment.Trim.xMax, fragment.Region.xMax), Mathf.Min(fragment.Trim.yMax, fragment.Region.yMax));
                if (area.width <= 0 || area.height <= 0) continue;
                Include(fragment.Transform(area.min)); Include(fragment.Transform(new Vector2(area.xMax, area.yMin)));
                Include(fragment.Transform(area.max)); Include(fragment.Transform(new Vector2(area.xMin, area.yMax)));
            }
            return any ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : default;

            void Include(Vector2 point)
            {
                if (!any) { min = max = point; any = true; }
                else { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            }
        }
    }
}
