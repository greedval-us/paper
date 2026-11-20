using System.Linq;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Static album/workshop presentation. Recorded battle and trial poses remain explicit.</summary>
    public static class AssemblyPreviewPose
    {
        public const float Radians = -Mathf.PI / 4;
        static readonly MeshPen measuringPen = new MeshPen(null, 1024);

        public static AssemblyVisualPose For(RecipeSnapshot snapshot)
            => new AssemblyVisualPose(snapshot?.Recipe.Tower?.Mounts.Select(mount =>
                new AssemblyMountPose(mount.SlotIndex, Radians)));

        public static AssemblyVisualPose ForWeapon()
            => new AssemblyVisualPose(new[] { new AssemblyMountPose(0, Radians) });

        /// <summary>Fits the actual pen strokes at this pose; mechanical envelopes remain unchanged.</summary>
        public static Rect VisibleBounds(AssemblyVisualComposition composition)
        {
            if (composition == null) return default;
            // Presentation runs on Unity's main thread. Reuse scratch storage rather than
            // allocating a new mesh/vertex list for each card and workshop refresh.
            measuringPen.Clear();
            composition.Draw(measuringPen, Vector2.zero);
            return measuringPen.TryGetBounds(out var bounds) ? bounds : default;
        }
    }
}
