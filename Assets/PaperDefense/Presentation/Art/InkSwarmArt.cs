using System;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Shared native battle artwork for the selected organic ink-creature direction.</summary>
    internal static class InkSwarmArt
    {
        internal const string Revision = "ink-swarm-v1";
        internal static NotebookVisualThemeAsset CreateTheme()
        {
            var theme = NotebookThemeDefaults.CreateTheme();
            theme.name = "Ink Swarm — dark notebook (runtime)";
            var data = theme.Data;
            data.Revision = Revision;
            data.NightInkOnDarkSurfaces = true;
            var palette = new Color[(int)NotebookInkChannel.BladeSteel + 1];
            Array.Copy(data.Palette, palette, data.Palette.Length);
            palette[(int)NotebookInkChannel.Ivory] = NightNotebookArt.Ink;
            palette[(int)NotebookInkChannel.Sketch] = NightNotebookArt.Pencil;
            palette[(int)NotebookInkChannel.Surface] = new Color32(8, 24, 20, 255);
            palette[(int)NotebookInkChannel.Amber] = NightNotebookArt.Accent;
            palette[(int)NotebookInkChannel.Danger] = new Color32(214, 135, 111, 255);
            palette[(int)NotebookInkChannel.Clean] = NightNotebookEffectPalette.Clean;
            palette[(int)NotebookInkChannel.NotebookPaper] = NightNotebookArt.Paper;
            palette[(int)NotebookInkChannel.Body] = NightNotebookArt.Body;
            palette[(int)NotebookInkChannel.GlassInk] = NightNotebookArt.GlassInk;
            palette[(int)NotebookInkChannel.Copper] = NightNotebookArt.Copper;
            palette[(int)NotebookInkChannel.BladeSteel] = NightNotebookArt.BladeSteel;
            data.Palette = palette;
            data.Clips.Clear();
            InkSwarmEnemyArt.PopulateClips(data);
            InkSwarmEffectArt.PopulateClips(data);
            theme.ReplaceData(data);
            return theme;
        }

        internal static NotebookArtShape Shape(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (id.StartsWith("enemy.", StringComparison.Ordinal)) return InkSwarmEnemyArt.Create(id);
            if (id.StartsWith("terrain.", StringComparison.Ordinal)) return InkSwarmSceneryArt.Create(id);
            if (id.StartsWith("fx.", StringComparison.Ordinal) || id == "object.blot") return InkSwarmEffectArt.Create(id);
            if (id.StartsWith("object.", StringComparison.Ordinal)) return InkSwarmSceneryArt.Create(id);
            return null;
        }
    }
}
