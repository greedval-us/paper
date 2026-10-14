using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Stable ink strokes and cut paper edges. Decoration never changes layout or hit areas.</summary>
    public static class NotebookPaperSurface
    {
        public static void Draw(VertexHelper target, Rect rect, Color color)
        {
            if (color.a <= 0 || rect.width < 8 || rect.height < 8) return;
            var pen = new MeshPen(null, 2048);
            var r = new Rect(rect.x + 3, rect.y + 3, rect.width - 6, rect.height - 6);
            float cut = Mathf.Min(12, r.width * .12f, r.height * .12f);
            Vector2[] edge = {
                new Vector2(r.xMin + cut, r.yMin), new Vector2(r.xMax - cut * .5f, r.yMin + 1),
                new Vector2(r.xMax, r.yMin + cut), new Vector2(r.xMax - 1, r.yMax - cut),
                new Vector2(r.xMax - cut, r.yMax), new Vector2(r.xMin + cut * .3f, r.yMax - 1),
                new Vector2(r.xMin, r.yMax - cut * .6f), new Vector2(r.xMin + 1, r.yMin + cut)
            };
            bool light = color.grayscale > .6f;
            Color fill = light ? new Color(color.r * .985f, color.g * .965f, color.b * .91f, color.a) : color;
            Color ink = light ? new Color(.12f, .20f, .33f, color.a * .62f) : new Color(.96f, .93f, .82f, color.a * .6f);
            Color ghost = ink; ghost.a *= .25f;
            for (int i = 0; i < edge.Length; i++) pen.Triangle(r.center, edge[i], edge[(i + 1) % edge.Length], fill);
            for (int i = 0; i < edge.Length; i++)
            {
                Stroke(pen, edge[i], edge[(i + 1) % edge.Length], 1.15f, ink, i);
                Stroke(pen, Vector2.Lerp(edge[i], r.center, .012f), Vector2.Lerp(edge[(i + 1) % edge.Length], r.center, .012f), .65f, ghost, i + 4);
            }
            if (r.height > 150 && r.width > 250 && light)
            {
                Color ruling = new Color(.28f, .43f, .59f, color.a * .065f);
                for (float y = r.yMin + 30; y < r.yMax - 14; y += 30)
                    pen.Line(new Vector2(r.xMin + 9, y), new Vector2(r.xMax - 9, y), .7f, ruling);
            }
            if (r.width > 100 && r.height > 65)
                for (int i = 0; i < 6; i++)
                    pen.Line(new Vector2(r.xMax - 12 - i * 3, r.yMin + 7), new Vector2(r.xMax - 6 - i * 3, r.yMin + 14), .8f, ghost);
            pen.CopyTo(target, Vector2.zero, 1);
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
                Vector2 next = Vector2.Lerp(from, to, t) + normal * (Mathf.Sin(i * 2.37f + seed) * .65f * Mathf.Sin(t * Mathf.PI));
                pen.Line(previous, next, width, color); previous = next;
            }
        }
    }
}
