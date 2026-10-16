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
            float cut = Mathf.Min(control ? 7 : 10, r.width * .08f, r.height * .14f);
            Vector2[] edge = Edge(r, cut);
            bool light = color.grayscale > .6f;
            for (int i = 0; i < edge.Length; i++) pen.Triangle(r.center, edge[i], edge[(i + 1) % edge.Length], color);

            if (!light && control)
            {
                Color hatch = PlayerUi.Card; hatch.a = color.a * (disabled ? .08f : .19f);
                Hatch(pen, Inset(r, 6), hatch);
            }
            else if (r.height > 150 && r.width > 250 && light)
            {
                Rect sheet = Inset(r, 12);
                Color ruling = PlayerUi.Grid; ruling.a *= color.a * .65f;
                for (float x = sheet.xMin + 16; x < sheet.xMax; x += 28)
                    pen.Line(new Vector2(x, sheet.yMin), new Vector2(x, sheet.yMax), .65f, ruling);
                for (float y = sheet.yMin + 16; y < sheet.yMax; y += 28)
                    pen.Line(new Vector2(sheet.xMin, y), new Vector2(sheet.xMax, y), .65f, ruling);
            }

            Color ink = PlayerUi.Ink; ink.a = color.a * (disabled ? .48f : .94f);
            Color innerInk = light ? ink : PlayerUi.Card; innerInk.a = color.a * (disabled ? .25f : .72f);
            float stroke = selected || hovered || pressed ? 2.1f : 1.45f;
            Outline(pen, edge, stroke, ink, disabled);
            Outline(pen, Edge(Inset(r, 2.7f), Mathf.Max(2, cut - 1.5f)), .8f, innerInk, disabled);

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
            return new[] {
                new Vector2(r.xMin + cut, r.yMin), new Vector2(r.xMax - cut, r.yMin + .4f),
                new Vector2(r.xMax, r.yMin + cut), new Vector2(r.xMax - .4f, r.yMax - cut),
                new Vector2(r.xMax - cut, r.yMax), new Vector2(r.xMin + cut, r.yMax - .4f),
                new Vector2(r.xMin, r.yMax - cut), new Vector2(r.xMin + .4f, r.yMin + cut)
            };
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
            for (float offset = -r.height; offset < r.width; offset += 5)
            {
                float startX = Mathf.Max(0, offset), endX = Mathf.Min(r.width, offset + r.height);
                Vector2 from = new Vector2(r.xMin + startX, r.yMin + startX - offset);
                Vector2 to = new Vector2(r.xMin + endX, r.yMin + endX - offset);
                Stroke(pen, from, to, .85f, color, Mathf.RoundToInt(offset));
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
                Vector2 next = Vector2.Lerp(from, to, t) + normal * (Mathf.Sin(i * 2.37f + seed) * .35f * Mathf.Sin(t * Mathf.PI));
                pen.Line(previous, next, width, color); previous = next;
            }
        }
    }
}
