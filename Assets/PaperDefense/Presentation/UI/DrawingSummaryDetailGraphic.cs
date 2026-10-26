using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Paper folds, title ribbons and calibrated load segments for player panels and historical readouts.</summary>
    public sealed class DrawingSummaryDetailGraphic : NotebookAtlasGraphic
    {
        public enum Kind { FoldedPaper, Ribbon, InkAccent, InkHatching, LoadSegments, Clock }
        Kind shape;
        float fraction;
        readonly MeshPen pen = new MeshPen(null, 1536);
        public Kind Shape { get => shape; set { shape = value; SetVerticesDirty(); } }
        public float Fraction { get => fraction; set { fraction = Mathf.Clamp01(value); SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); pen.Clear(); Rect r = rectTransform.rect;
            if (r.width <= 0 || r.height <= 0) return;
            switch (shape)
            {
                case Kind.FoldedPaper: Paper(r); break;
                case Kind.Ribbon: Ribbon(r); break;
                case Kind.InkAccent:
                    pen.Line(new Vector2(r.xMin, r.center.y), new Vector2(r.xMax, r.center.y + .5f), 1.6f, MeshPen.Alpha(color, .75f));
                    pen.Line(new Vector2(r.xMin + 3, r.center.y - 1), new Vector2(r.xMax - 2, r.center.y - 1), .6f, MeshPen.Alpha(color, .35f));
                    break;
                case Kind.InkHatching:
                    float end = r.xMin + 2 + (r.width - 4) * fraction;
                    for (float x = r.xMin + 5; x < end - 7; x += 6)
                        pen.Line(new Vector2(x, r.yMin + 5), new Vector2(Mathf.Min(end - 3, x + 9), r.yMax - 5), .75f, new Color(1, 1, 1, .28f));
                    break;
                case Kind.LoadSegments: Segments(r); break;
                case Kind.Clock:
                    float radius = Mathf.Min(r.width, r.height) * .40f;
                    pen.Disc(r.center, radius, WorkshopUi.Paper, 28);
                    pen.Ring(r.center, radius, 1.6f, color, 32);
                    pen.Line(r.center, r.center + Vector2.up * radius * .65f, 1.8f, color);
                    pen.Line(r.center, r.center + Vector2.right * radius * .49f, 1.8f, color);
                    pen.Disc(r.center, 1.5f, color, 12);
                    break;
            }
            pen.CopyTo(vh, Vector2.zero, 1);
        }

        void Paper(Rect r)
        {
            const float margin = 2, fold = 25;
            Vector2[] contour =
            {
                new Vector2(r.xMin + margin + 5, r.yMax - margin), new Vector2(r.xMax - margin - 5, r.yMax - margin),
                new Vector2(r.xMax - margin, r.yMax - margin - 5), new Vector2(r.xMax - margin, r.yMin + fold),
                new Vector2(r.xMax - fold, r.yMin + margin), new Vector2(r.xMin + margin + 5, r.yMin + margin),
                new Vector2(r.xMin + margin, r.yMin + margin + 5), new Vector2(r.xMin + margin, r.yMax - margin - 5)
            };
            Polygon(contour, WorkshopUi.Paper, WorkshopUi.Ink, 1.25f);
            Color grid = new Color(.39f, .57f, .75f, .055f);
            for (float x = r.xMin + 20; x < r.xMax - fold; x += 22)
                pen.Line(new Vector2(x, r.yMin + 6), new Vector2(x, r.yMax - 6), .65f, grid);
            for (float y = r.yMin + 20; y < r.yMax - 6; y += 22)
                pen.Line(new Vector2(r.xMin + 6, y), new Vector2(r.xMax - fold, y), .65f, grid);
            Color fine = MeshPen.Alpha(WorkshopUi.Blue, .35f);
            pen.Line(new Vector2(r.xMin + 6, r.yMin + 12), new Vector2(r.xMin + 6, r.yMax - 9), .65f, fine);
            pen.Line(new Vector2(r.xMin + 11, r.yMin + 6), new Vector2(r.xMax - fold - 4, r.yMin + 6), .65f, fine);
            pen.Line(new Vector2(r.xMin + 10, r.yMax - 6), new Vector2(r.xMax - 10, r.yMax - 6), .65f, fine);
            Vector2 corner = new Vector2(r.xMax - fold, r.yMin + fold);
            Vector2 bottom = new Vector2(r.xMax - fold, r.yMin + margin);
            Vector2 right = new Vector2(r.xMax - margin, r.yMin + fold);
            pen.Triangle(corner, bottom, right, new Color32(218, 225, 226, 255));
            pen.Line(bottom, corner, 1, WorkshopUi.Ink); pen.Line(corner, right, 1, WorkshopUi.Ink);
            for (int i = 0; i < 3; i++)
                pen.Line(bottom + new Vector2(3 + i * 3, 5 + i * 4), right + new Vector2(-5 - i * 4, -3 - i * 3), .6f, fine);
        }

        void Ribbon(Rect r)
        {
            Vector2[] outline =
            {
                new Vector2(r.xMin + 8, r.yMax - 2), new Vector2(r.xMax - 8, r.yMax - 2),
                new Vector2(r.xMax - 2, r.yMax - 5), new Vector2(r.xMax - 9, r.center.y),
                new Vector2(r.xMax - 2, r.yMin + 4), new Vector2(r.xMax - 8, r.yMin + 2),
                new Vector2(r.xMin + 8, r.yMin + 2), new Vector2(r.xMin + 2, r.yMin + 4),
                new Vector2(r.xMin + 9, r.center.y), new Vector2(r.xMin + 2, r.yMax - 5)
            };
            Polygon(outline, WorkshopUi.Paper, WorkshopUi.Ink, 1.1f);
            pen.Line(new Vector2(r.xMin + 12, r.yMin + 5), new Vector2(r.xMax - 12, r.yMin + 5), .65f, MeshPen.Alpha(WorkshopUi.Blue, .32f));
        }

        void Segments(Rect r)
        {
            const int count = 7;
            float cell = (r.width - 2) / count;
            for (int i = 0; i < count; i++)
            {
                var a = new Vector2(r.xMin + 1 + cell * i, r.yMin + 1);
                float occupied = Mathf.Clamp01(fraction * count - i), w = cell - 1.5f;
                pen.Rect(a.x, a.y, w, r.height - 2, new Color32(228, 231, 233, 255));
                if (occupied > 0)
                {
                    pen.Rect(a.x, a.y, w * occupied, r.height - 2, color);
                    pen.Line(a + new Vector2(1, 2), a + new Vector2(Mathf.Max(1, w * occupied - 1), r.height - 4), .65f, new Color(1, 1, 1, .5f));
                }
                pen.Line(a, a + new Vector2(w, 0), .7f, MeshPen.Alpha(WorkshopUi.Ink, .55f));
                pen.Line(a, a + new Vector2(0, r.height - 2), .7f, MeshPen.Alpha(WorkshopUi.Ink, .55f));
                pen.Line(a + new Vector2(0, r.height - 2), a + new Vector2(w, r.height - 2), .7f, MeshPen.Alpha(WorkshopUi.Ink, .55f));
                pen.Line(a + new Vector2(w, 0), a + new Vector2(w, r.height - 2), .7f, MeshPen.Alpha(WorkshopUi.Ink, .55f));
            }
        }

        void Polygon(Vector2[] points, Color fill, Color ink, float weight)
        {
            Vector2 center = rectTransform.rect.center, previous = points[points.Length - 1];
            foreach (Vector2 next in points)
            { pen.Triangle(center, previous, next, fill); pen.Line(previous, next, weight, ink); previous = next; }
        }
    }
}
