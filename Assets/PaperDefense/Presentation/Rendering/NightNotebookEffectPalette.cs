using UnityEngine;
using PaperDefense.Core;

namespace PaperDefense.Presentation
{
    /// <summary>Only the built-in effects opt into bright ink on an explicitly dark drawing surface.</summary>
    internal static class NightNotebookEffectPalette
    {
        internal static readonly Color Clean = new Color32(135, 199, 150, 255);

        static bool UsesNightInk(NotebookVisualThemeAsset theme, bool darkSurface)
            => darkSurface && (theme == null || ReferenceEquals(theme, NotebookVisualThemeAsset.Default) || theme.Data.NightInkOnDarkSurfaces);

        internal static Color Tone(Color light, Color dark, NotebookVisualThemeAsset theme, bool darkSurface)
            => UsesNightInk(theme, darkSurface) ? dark : light;

        internal static Color ColorInk(InkColor color, NotebookVisualThemeAsset theme = null, bool darkSurface = true)
        {
            switch (color)
            {
                case InkColor.Red: return Tone(new Color32(164, 49, 43, 255), new Color32(247, 142, 127, 255), theme, darkSurface);
                case InkColor.Blue: return Tone(NotebookPalette.Blue, new Color32(127, 195, 242, 255), theme, darkSurface);
                case InkColor.Green: return Tone(new Color32(30, 92, 64, 255), new Color32(145, 216, 156, 255), theme, darkSurface);
                default: return Tone(NotebookPalette.Marker, NightNotebookArt.Accent, theme, darkSurface);
            }
        }

        internal static bool DrawResource(MeshPen pen, string id, Vector2 at,
            NotebookVisualThemeAsset theme, bool darkSurface, float scale = 1, float radians = 0,
            float opacity = 1, Vector2? localScale = null, InkColor inkColor = InkColor.None)
        {
            if (inkColor != InkColor.None)
            {
                theme = theme ?? NotebookVisualThemeAsset.Default;
                var coloredShape = theme.FindShape(id);
                if (coloredShape != null)
                    return NotebookArtRenderer.Draw(pen, coloredShape, at, scale, radians, opacity: opacity,
                        localScale: localScale, theme: theme, effectTint: ColorInk(inkColor, theme, darkSurface));
            }
            if (!UsesNightInk(theme, darkSurface))
                return DoodleGlyphs.DrawResource(pen, id, at, scale, radians, opacity, localScale, theme);
            theme = theme ?? NotebookVisualThemeAsset.Default;
            var shape = theme.FindShape(id);
            if (shape == null) return DoodleGlyphs.DrawResource(pen, id, at, scale, radians, opacity, localScale, theme);
            // Tint stroke channels at draw time; palette entries and cached shape marks stay immutable.
            Color ink = id != null && id.Contains("spitter") ? NightNotebookArt.Accent : NightNotebookArt.Ink;
            return NotebookArtRenderer.Draw(pen, shape, at, scale, radians, ink, opacity, localScale, theme);
        }
    }
}
