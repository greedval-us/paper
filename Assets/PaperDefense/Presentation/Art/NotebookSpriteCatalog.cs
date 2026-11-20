using System.Runtime.CompilerServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("PaperDefense.Editor.Tests")]

namespace PaperDefense.Presentation
{
    /// <summary>Cached bounds and drawing entry points for semantic native geometry.</summary>
    public static class NotebookSpriteCatalog
    {
        static readonly Dictionary<string, Rect> boundsById = new Dictionary<string, Rect>();
        public static Texture2D Texture => Texture2D.whiteTexture;
        public static Vector2 WhiteUV => new Vector2(.5f, .5f);
        public static bool TryGetBounds(string id, out Rect bounds)
        {
            if (id != null && boundsById.TryGetValue(id, out bounds)) return true;
            var pen = new MeshPen(null, 128);
            if (!TryDraw(pen, id, Vector2.zero)) { bounds = default; return false; }
            if (!pen.TryGetBounds(out bounds)) return false;
            boundsById[id] = bounds;
            return true;
        }
        internal static bool TryDraw(MeshPen pen, string id, Vector2 pivot, float scale = 1,
            float radians = 0, float opacity = 1, Vector2? localScale = null, Color? tint = null)
            => NotebookArtRenderer.Draw(pen, NotebookVisualThemeAsset.Default.FindShape(id), pivot, scale,
                radians, tint, opacity, localScale);
        public static bool TryDrawUI(VertexHelper target, string id, Vector2 pivot, float scale = 1,
            float radians = 0, float opacity = 1, Vector2? localScale = null, Color? tint = null)
            => NotebookArtRenderer.DrawUI(target, NotebookVisualThemeAsset.Default.FindShape(id), pivot, scale,
                radians, tint, opacity, localScale);
        public static void ResetCache() => boundsById.Clear();
    }
}
