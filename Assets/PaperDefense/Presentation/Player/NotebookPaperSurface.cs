using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Stable ink strokes and cut paper edges. Decoration never changes layout or hit areas.</summary>
    public static class NotebookPaperSurface
    {
        public static void Draw(VertexHelper target, Rect rect, Color color)
            => Draw(target, rect, color, false, false, false, false, false, false);

        internal static void Draw(VertexHelper target, Rect rect, Color color, bool control,
            bool selected, bool focused, bool disabled, bool hovered, bool pressed)
        {
            if (color.a <= 0 || rect.width < 8 || rect.height < 8) return;
            var pen = new MeshPen(null, 4096);
            var r = Inset(rect, 2.5f);
            float cut = Mathf.Min(control ? 9 : 13, r.width * .08f, r.height * .22f);
            Vector2[] edge = Edge(r, cut);
            bool light = color.grayscale > .6f;
            if (r.height > 80)
            {
                Vector2 offset = new Vector2(1.1f, -1.5f);
                for (int i = 0; i < edge.Length; i++)
                    pen.Triangle(r.center + offset, edge[i] + offset, edge[(i + 1) % edge.Length] + offset,
                        new Color(0.13f, .20f, .34f, color.a * .10f));
            }
            for (int i = 0; i < edge.Length; i++) pen.Triangle(r.center, edge[i], edge[(i + 1) % edge.Length], color);

            if (!light && control)
            {
                Color hatch = PlayerUi.Card; hatch.a = color.a * (disabled ? .08f : .25f);
                Hatch(pen, Inset(r, 6), hatch);
            }
            else if (r.height > 150 && r.width > 250 && light)
            {
                Rect sheet = Inset(r, 12);
                Color ruling = PlayerUi.Grid; ruling.a *= color.a * .65f;
                for (float x = sheet.xMin + 16; x < sheet.xMax; x += 20)
                    pen.Line(new Vector2(x, sheet.yMin), new Vector2(x, sheet.yMax), .65f, ruling);
                for (float y = sheet.yMin + 16; y < sheet.yMax; y += 20)
                    pen.Line(new Vector2(sheet.xMin, y), new Vector2(sheet.xMax, y), .65f, ruling);
            }

            Color ink = PlayerUi.Ink; ink.a = color.a * (disabled ? .48f : .94f);
            Color innerInk = light ? ink : PlayerUi.Card; innerInk.a = color.a * (disabled ? .25f : .48f);
            float stroke = selected || hovered || pressed ? 2.5f : 2;
            Outline(pen, edge, stroke, ink, disabled);
            Outline(pen, Edge(Inset(r, 2.7f), Mathf.Max(2, cut - 1.5f)), 1.05f, innerInk, disabled);

            if (selected)
            {
                Color mark = light ? PlayerUi.Blue : PlayerUi.Card; mark.a = color.a;
                Stroke(pen, new Vector2(r.xMin + 10, r.yMin + 7), new Vector2(r.xMax - 10, r.yMin + 7), 2.6f, mark, 8);
            }
            if (pressed && !disabled)
            {
                Color mark = light ? PlayerUi.Blue : PlayerUi.Card; mark.a = color.a * .8f;
                Stroke(pen, new Vector2(r.xMin + 9, r.yMax - 7), new Vector2(r.xMax - 9, r.yMax - 7), 2, mark, 11);
            }
            if (focused && !disabled) Focus(pen, Inset(r, selected ? 9 : 6), light ? PlayerUi.Blue : PlayerUi.Card);
            pen.CopyTo(target, Vector2.zero, 1);
        }

        static Rect Inset(Rect r, float amount)
            => new Rect(r.xMin + amount, r.yMin + amount, Mathf.Max(0, r.width - amount * 2), Mathf.Max(0, r.height - amount * 2));

        static Vector2[] Edge(Rect r, float cut)
        {
            cut = Mathf.Min(cut, r.width * .2f, r.height * .2f);
            const int segments = 5;
            var points = new Vector2[4 * (segments + 1)];
            for (int corner = 0; corner < 4; corner++)
            {
                var center = new Vector2(corner == 0 || corner == 3 ? r.xMin + cut : r.xMax - cut,
                    corner < 2 ? r.yMin + cut : r.yMax - cut);
                for (int step = 0; step <= segments; step++)
                {
                    float angle = (180 + corner * 90 + step * 90f / segments) * Mathf.Deg2Rad;
                    points[corner * (segments + 1) + step] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * cut;
                }
            }
            return points;
        }

        static void Outline(MeshPen pen, Vector2[] edge, float width, Color color, bool dashed)
        {
            for (int i = 0; i < edge.Length; i++)
            {
                Vector2 from = edge[i], to = edge[(i + 1) % edge.Length];
                if (!dashed) { Stroke(pen, from, to, width, color, i); continue; }
                Vector2 delta = to - from;
                float length = delta.magnitude;
                for (float d = 0; d < length; d += 10)
                    pen.Line(from + delta * (d / length), from + delta * (Mathf.Min(d + 6, length) / length), width, color);
            }
        }

        static void Hatch(MeshPen pen, Rect r, Color color)
        {
            if (r.width <= 0 || r.height <= 0) return;
            for (float offset = -r.height; offset < r.width; offset += 3.8f + MeshPen.Hash(Mathf.RoundToInt(offset * 13)) * 2.2f)
            {
                float startX = Mathf.Max(0, offset), endX = Mathf.Min(r.width, offset + r.height);
                Vector2 from = new Vector2(r.xMin + startX, r.yMin + startX - offset);
                Vector2 to = new Vector2(r.xMin + endX, r.yMin + endX - offset);
                Stroke(pen, from, to, .7f + MeshPen.Hash(Mathf.RoundToInt(offset * 7)) * .55f, color, Mathf.RoundToInt(offset));
            }
        }

        static void Focus(MeshPen pen, Rect r, Color color)
        {
            float length = Mathf.Min(14, r.width * .2f, r.height * .35f);
            foreach (var corner in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax),
                new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin) })
            {
                pen.Line(corner, corner + new Vector2(corner.x == r.xMin ? length : -length, 0), 2.5f, color);
                pen.Line(corner, corner + new Vector2(0, corner.y == r.yMin ? length : -length), 2.5f, color);
            }
        }

        static void Stroke(MeshPen pen, Vector2 from, Vector2 to, float width, Color color, int seed)
        {
            Vector2 delta = to - from;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized;
            int segments = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 22));
            Vector2 previous = from;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector2 next = Vector2.Lerp(from, to, t) + normal * (Mathf.Sin(i * 2.37f + seed) * .25f * Mathf.Sin(t * Mathf.PI));
                pen.Line(previous, next, width, color); previous = next;
            }
        }
    }
}
