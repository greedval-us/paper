using UnityEngine;

namespace PaperDefense.Presentation
{
    public enum WorkshopIcon
    {
        None, Tower, Weapon, Pencil, Target, Book, Check, Next, Back, Eraser, Line,
        Rectangle, Ellipse, Move, Undo, Redo, Grid, Magnet, ZoomIn, ZoomOut, Fit,
        Mirror, Trash, Chevron, Damage, Range, Cost, Ink, Shield, Plus, Close,
        Help, Save, Settings, Copy
    }

    /// <summary>Small, consistent line symbols. Geometry is independent of fonts and obeys uGUI masks.</summary>
    public sealed class WorkshopIconGraphic : UnityEngine.UI.MaskableGraphic
    {
        [SerializeField] WorkshopIcon icon;
        readonly MeshPen pen = new MeshPen(null, 640);
        const float Weight = 1.7f;
        public WorkshopIcon Icon
        {
            get => icon;
            set { if (icon == value) return; icon = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            pen.Clear();
            if (icon == WorkshopIcon.None) return;
            switch (icon)
            {
                case WorkshopIcon.Tower:
                    Path(5, 21, 5, 10, 4, 10, 4, 4, 8, 4, 8, 7, 10, 7, 10, 4, 14, 4, 14, 7, 16, 7, 16, 4, 20, 4, 20, 10, 19, 10, 19, 21, 5, 21);
                    Path(10, 21, 10, 15, 14, 15, 14, 21);
                    break;
                case WorkshopIcon.Weapon:
                    Path(4, 8, 19, 5, 21, 10, 6, 13, 4, 8);
                    Stroke(16, 6, 18, 11);
                    Path(10, 13, 13, 18, 20, 18);
                    Circle(8, 18, 3);
                    break;
                case WorkshopIcon.Pencil:
                    Path(4, 20, 5, 14, 16, 3, 21, 8, 10, 19, 4, 20);
                    Stroke(14, 5, 19, 10);
                    Stroke(5, 14, 10, 19);
                    Stroke(8, 16, 16, 8);
                    break;
                case WorkshopIcon.Target:
                    Circle(12, 12, 8);
                    Circle(12, 12, 4);
                    Dot(12, 12, 1.3f);
                    Stroke(12, 2, 12, 5);
                    Stroke(19, 12, 22, 12);
                    break;
                case WorkshopIcon.Book:
                    Path(12, 6, 8, 4, 3, 4, 3, 19, 8, 19, 12, 21, 16, 19, 21, 19, 21, 4, 16, 4, 12, 6, 12, 21);
                    Stroke(6, 8, 9, 9);
                    Stroke(15, 9, 18, 8);
                    break;
                case WorkshopIcon.Check:
                    Path(4, 12, 9, 17, 20, 6);
                    break;
                case WorkshopIcon.Next:
                    Stroke(4, 12, 20, 12);
                    Path(14, 6, 20, 12, 14, 18);
                    break;
                case WorkshopIcon.Back:
                    Stroke(4, 12, 20, 12);
                    Path(10, 6, 4, 12, 10, 18);
                    break;
                case WorkshopIcon.Eraser:
                    Path(3, 14, 14, 3, 21, 10, 10, 21, 3, 14);
                    Stroke(8, 9, 15, 16);
                    Stroke(10, 21, 21, 21);
                    break;
                case WorkshopIcon.Line:
                    Stroke(5, 19, 19, 5);
                    Circle(5, 19, 2);
                    Circle(19, 5, 2);
                    break;
                case WorkshopIcon.Rectangle:
                    Box(4, 5, 16, 14);
                    break;
                case WorkshopIcon.Ellipse:
                    Ellipse(12, 12, 9, 6.5f);
                    break;
                case WorkshopIcon.Move:
                    Stroke(12, 3, 12, 21);
                    Stroke(3, 12, 21, 12);
                    Path(9, 6, 12, 3, 15, 6);
                    Path(9, 18, 12, 21, 15, 18);
                    Path(6, 9, 3, 12, 6, 15);
                    Path(18, 9, 21, 12, 18, 15);
                    break;
                case WorkshopIcon.Undo:
                    CurveArrow(false);
                    break;
                case WorkshopIcon.Redo:
                    CurveArrow(true);
                    break;
                case WorkshopIcon.Grid:
                    Box(4, 4, 16, 16);
                    Stroke(9.3f, 4, 9.3f, 20);
                    Stroke(14.7f, 4, 14.7f, 20);
                    Stroke(4, 9.3f, 20, 9.3f);
                    Stroke(4, 14.7f, 20, 14.7f);
                    break;
                case WorkshopIcon.Magnet:
                    Path(4, 4, 8, 4, 8, 13);
                    Arc(12, 13, 4, 0, 180);
                    Path(16, 13, 16, 4, 20, 4, 20, 13);
                    Arc(12, 13, 8, 0, 180);
                    Stroke(4, 13, 4, 4);
                    Stroke(4, 8, 8, 8);
                    Stroke(16, 8, 20, 8);
                    break;
                case WorkshopIcon.ZoomIn:
                case WorkshopIcon.ZoomOut:
                    Circle(10, 10, 6.5f);
                    Stroke(15, 15, 21, 21);
                    Stroke(7, 10, 13, 10);
                    if (icon == WorkshopIcon.ZoomIn) Stroke(10, 7, 10, 13);
                    break;
                case WorkshopIcon.Fit:
                    Path(3, 9, 3, 3, 9, 3);
                    Path(15, 3, 21, 3, 21, 9);
                    Path(21, 15, 21, 21, 15, 21);
                    Path(9, 21, 3, 21, 3, 15);
                    Box(8, 8, 8, 8);
                    break;
                case WorkshopIcon.Mirror:
                    for (int y = 3; y < 22; y += 5) Stroke(12, y, 12, y + 2);
                    Path(3, 18, 8, 7, 8, 18, 3, 18);
                    Path(21, 18, 16, 7, 16, 18, 21, 18);
                    break;
                case WorkshopIcon.Trash:
                    Stroke(4, 7, 20, 7);
                    Path(6, 7, 7, 21, 17, 21, 18, 7);
                    Path(9, 7, 9, 3, 15, 3, 15, 7);
                    Stroke(10, 11, 10, 17);
                    Stroke(14, 11, 14, 17);
                    break;
                case WorkshopIcon.Chevron:
                    Path(6, 9, 12, 15, 18, 9);
                    break;
                case WorkshopIcon.Damage:
                    Path(14, 2, 5, 13, 11, 13, 9, 22, 20, 10, 13, 10, 14, 2);
                    break;
                case WorkshopIcon.Range:
                    Circle(12, 12, 7);
                    Stroke(2, 12, 22, 12);
                    Path(5, 9, 2, 12, 5, 15);
                    Path(19, 9, 22, 12, 19, 15);
                    Dot(12, 12, 1.3f);
                    break;
                case WorkshopIcon.Cost:
                    Circle(12, 12, 8);
                    Circle(12, 12, 5.4f);
                    Path(12, 8, 15, 12, 12, 16, 9, 12, 12, 8);
                    break;
                case WorkshopIcon.Ink:
                    Path(12, 3, 7, 10, 5, 14, 5.5f, 17, 8, 20, 12, 21, 16, 20, 18.5f, 17, 19, 14, 17, 10, 12, 3);
                    Path(8, 14, 8.5f, 16.5f, 11, 18);
                    break;
                case WorkshopIcon.Shield:
                    Path(12, 3, 20, 6, 19, 14, 16, 18, 12, 21, 8, 18, 5, 14, 4, 6, 12, 3);
                    Path(8, 12, 11, 15, 16, 9);
                    break;
                case WorkshopIcon.Plus:
                    Stroke(5, 12, 19, 12);
                    Stroke(12, 5, 12, 19);
                    break;
                case WorkshopIcon.Close:
                    Stroke(6, 6, 18, 18);
                    Stroke(6, 18, 18, 6);
                    break;
                case WorkshopIcon.Help:
                    Circle(12, 12, 9);
                    Path(9, 8.5f, 10, 6.8f, 12.5f, 6.5f, 14.5f, 7.5f, 15, 9.5f, 14, 11, 12, 12.5f, 12, 14);
                    Dot(12, 17.5f, 1);
                    break;
                case WorkshopIcon.Save:
                    Path(4, 3, 17, 3, 21, 7, 21, 21, 3, 21, 3, 3, 4, 3);
                    Path(7, 3, 7, 9, 16, 9, 16, 3);
                    Path(7, 21, 7, 14, 17, 14, 17, 21);
                    Stroke(13, 5, 13, 7);
                    break;
                case WorkshopIcon.Settings:
                    Gear();
                    Circle(12, 12, 3.2f);
                    break;
                case WorkshopIcon.Copy:
                    Box(8, 7, 12, 14);
                    Path(5, 17, 3, 17, 3, 3, 15, 3, 15, 4);
                    break;
            }
            Rect rect = rectTransform.rect;
            pen.CopyTo(vh, rect.center, Mathf.Max(0, Mathf.Min(rect.width, rect.height)) / 24f);
        }

        static Vector2 Point(float x, float y) => new Vector2(x - 12, 12 - y);

        void Stroke(float x1, float y1, float x2, float y2)
        {
            Vector2 from = Point(x1, y1), to = Point(x2, y2);
            pen.Line(from, to, Weight, color);
            pen.Disc(from, Weight * .5f, color, 8);
            pen.Disc(to, Weight * .5f, color, 8);
        }

        void Path(params float[] points)
        {
            for (int i = 2; i + 1 < points.Length; i += 2) Stroke(points[i - 2], points[i - 1], points[i], points[i + 1]);
        }

        void Dot(float x, float y, float radius) => pen.Disc(Point(x, y), radius, color, 16);

        void Box(float x, float y, float width, float height)
            => Path(x, y, x + width, y, x + width, y + height, x, y + height, x, y);

        void Circle(float x, float y, float radius) => Ellipse(x, y, radius, radius);

        void Ellipse(float x, float y, float rx, float ry)
        {
            const int segments = 32;
            Vector2 previous = Point(x + rx, y);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vector2 next = Point(x + Mathf.Cos(angle) * rx, y + Mathf.Sin(angle) * ry);
                pen.Line(previous, next, Weight, color);
                pen.Disc(next, Weight * .5f, color, 6);
                previous = next;
            }
        }

        void Arc(float x, float y, float radius, float fromDegrees, float toDegrees)
        {
            int segments = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(toDegrees - fromDegrees) / 12));
            float first = fromDegrees * Mathf.Deg2Rad;
            Vector2 previous = new Vector2(x + Mathf.Cos(first) * radius, y + Mathf.Sin(first) * radius);
            for (int i = 1; i <= segments; i++)
            {
                float angle = Mathf.Lerp(fromDegrees, toDegrees, i / (float)segments) * Mathf.Deg2Rad;
                Vector2 next = new Vector2(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius);
                Stroke(previous.x, previous.y, next.x, next.y);
                previous = next;
            }
        }

        void CurveArrow(bool mirror)
        {
            float[] points = { 18, 19, 20, 15, 19, 10, 16, 7, 11, 6, 7, 8, 4, 11 };
            for (int i = 2; i < points.Length; i += 2)
                Stroke(mirror ? 24 - points[i - 2] : points[i - 2], points[i - 1], mirror ? 24 - points[i] : points[i], points[i + 1]);
            if (mirror) Path(20, 5, 20, 11, 14, 11);
            else Path(4, 5, 4, 11, 10, 11);
        }

        void Gear()
        {
            const int count = 32;
            Vector2 previous = new Vector2(12 + 8.5f, 12);
            for (int i = 1; i <= count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                float radius = i % 4 == 0 || i % 4 == 1 ? 8.5f : 6.8f;
                Vector2 next = new Vector2(12 + Mathf.Cos(angle) * radius, 12 + Mathf.Sin(angle) * radius);
                Stroke(previous.x, previous.y, next.x, next.y);
                previous = next;
            }
        }
    }
}
