using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Draws the exact measured envelope supplied by Core; never interprets author strokes.</summary>
    internal static class WorkingGeometryOverlay
    {
        internal static void Draw(VertexHelper mesh, FreeformTowerDesign design, string binding, Vector2 center, float scale, float pixel)
        {
            if (design == null) return;
            bool barrel = DrawingBindings.TryMount(binding, out int slot);
            if (!barrel && binding != DrawingBindings.Base) return;
            var weapon = barrel ? design.GetBarrel(slot) : null;
            var outline = barrel ? weapon?.WorkingOutline : design.BodyOutline;
            if (outline == null || outline.Count < 2) return;
            var ink = new Color(.12f, .48f, .54f, .65f);
            float minX = 1, maxX = -1, minY = 1, maxY = -1;
            foreach (var point in outline)
            { minX = Mathf.Min(minX, point.X); maxX = Mathf.Max(maxX, point.X); minY = Mathf.Min(minY, point.Y); maxY = Mathf.Max(maxY, point.Y); }
            int start = mesh.currentVertCount;
            foreach (var point in outline) mesh.AddVert(center + new Vector2(point.X, point.Y) * scale, new Color(.12f, .48f, .54f, .075f), Vector2.zero);
            for (int i = 1; i + 1 < outline.Count; i++) mesh.AddTriangle(start, start + i, start + i + 1);
            // Horizontal hatching differentiates functional geometry without relying on hue.
            for (float y = minY + .04f; y < maxY; y += .08f)
            {
                float left = 1, right = -1;
                for (int i = 1; i < outline.Count; i++)
                {
                    var a = outline[i - 1]; var b = outline[i];
                    if ((a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y))
                    {
                        float x = a.X + (b.X - a.X) * (y - a.Y) / (b.Y - a.Y);
                        left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                    }
                }
                if (right > left) BlueprintSketch.Line(mesh, center + new Vector2(left, y) * scale, center + new Vector2(right, y) * scale, pixel, new Color(ink.r, ink.g, ink.b, .28f));
            }
            for (int i = 1; i < outline.Count; i++)
                Dashed(mesh, center + new Vector2(outline[i - 1].X, outline[i - 1].Y) * scale, center + new Vector2(outline[i].X, outline[i].Y) * scale, pixel, ink);
            if (!barrel) return;
            // Rulers follow the measured bounding dimensions, including open or slanted strokes.
            float rulerX = Mathf.Min(.94f, maxX + .07f), rulerY = Mathf.Max(.06f, minY + .04f);
            Dimension(mesh, center + new Vector2(rulerX, minY) * scale, center + new Vector2(rulerX, maxY) * scale, pixel, ink);
            Dimension(mesh, center + new Vector2(minX, rulerY) * scale, center + new Vector2(maxX, rulerY) * scale, pixel, ink);
            Dashed(mesh, center + Vector2.left * scale, center + Vector2.right * scale, pixel, ink);
            var muzzle = center + new Vector2(weapon.MuzzleOffset.X, weapon.MuzzleOffset.Y) * scale;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8, b = (i + 1) * Mathf.PI / 8;
                BlueprintSketch.Line(mesh, muzzle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 7, muzzle + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 7, pixel * 1.5f, ink);
            }
        }
        static void Dimension(VertexHelper mesh, Vector2 a, Vector2 b, float pixel, Color ink)
        {
            Dashed(mesh, a, b, pixel, ink);
            var direction = (b - a).normalized;
            var tick = new Vector2(-direction.y, direction.x) * 5;
            BlueprintSketch.Line(mesh, a - tick, a + tick, pixel, ink);
            BlueprintSketch.Line(mesh, b - tick, b + tick, pixel, ink);
        }
        static void Dashed(VertexHelper mesh, Vector2 a, Vector2 b, float pixel, Color ink)
        {
            float length = Vector2.Distance(a, b);
            if (length < .001f) return;
            var direction = (b - a) / length;
            for (float offset = 0; offset < length; offset += 10 * pixel)
                BlueprintSketch.Line(mesh, a + direction * offset, a + direction * Mathf.Min(length, offset + 6 * pixel), pixel, ink);
        }
    }
}
