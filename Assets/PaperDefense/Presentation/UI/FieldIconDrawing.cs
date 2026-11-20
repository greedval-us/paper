using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    // A 24-unit drawing grid shared by shell, workshop and HUD. The wash takes its hue
    // from the actual control ink, including disabled and dark-on-brass states.
    internal readonly struct FieldIconDrawing
    {
        static readonly MeshPen pen = new MeshPen(null, 768);
        readonly Color ink, wash;
        readonly float weight;
        readonly bool detail;

        FieldIconDrawing(Rect rect, Color tint, bool sketch)
        {
            float side = Mathf.Min(rect.width, rect.height);
            ink = tint;
            wash = new Color(tint.r, tint.g, tint.b, tint.a * .46f);
            weight = side < 22 ? 2.15f : 1.78f;
            detail = sketch && side >= 24;
        }

        public static void Draw(VertexHelper target, Rect rect, Color tint, WorkshopIcon icon)
        {
            target.Clear();
            if (icon == WorkshopIcon.None) return;
            pen.Clear();
            new FieldIconDrawing(rect, tint, true).Symbol(icon);
            pen.CopyTo(target, rect.center, Mathf.Min(rect.width, rect.height) / 24f);
        }

        public static void Draw(VertexHelper target, Rect rect, Color tint, PlayerIcon icon, bool sketch)
        {
            target.Clear();
            pen.Clear();
            new FieldIconDrawing(rect, tint, sketch).Symbol(icon);
            pen.CopyTo(target, rect.center, Mathf.Min(rect.width, rect.height) / 24f);
        }

        void Symbol(PlayerIcon icon)
        {
            switch (icon)
            {
                case PlayerIcon.Play: Symbol(WorkshopIcon.Play); break;
                case PlayerIcon.Back: Symbol(WorkshopIcon.Back); break;
                case PlayerIcon.Pause: Symbol(WorkshopIcon.Pause); break;
                case PlayerIcon.Book: Symbol(WorkshopIcon.Book); break;
                case PlayerIcon.Settings: Symbol(WorkshopIcon.Settings); break;
                case PlayerIcon.Help: Symbol(WorkshopIcon.Help); break;
                case PlayerIcon.Restart: Symbol(WorkshopIcon.Restart); break;
                case PlayerIcon.Check: Symbol(WorkshopIcon.Check); break;
                case PlayerIcon.Heart: Symbol(WorkshopIcon.Heart); break;
                case PlayerIcon.Drop: Symbol(WorkshopIcon.Ink); break;
                case PlayerIcon.Target: Symbol(WorkshopIcon.Target); break;
                case PlayerIcon.Save: Symbol(WorkshopIcon.Save); break;
                case PlayerIcon.Plus: Symbol(WorkshopIcon.Plus); break;
                case PlayerIcon.Trash: Symbol(WorkshopIcon.Trash); break;
                case PlayerIcon.Pencil: Symbol(WorkshopIcon.Pencil); break;
                case PlayerIcon.Next: Symbol(WorkshopIcon.Next); break;
                case PlayerIcon.Copy: Symbol(WorkshopIcon.Copy); break;
                case PlayerIcon.Shield: Symbol(WorkshopIcon.Shield); break;
                case PlayerIcon.Bolt: Symbol(WorkshopIcon.Damage); break;
                case PlayerIcon.Eraser: Symbol(WorkshopIcon.Eraser); break;
                case PlayerIcon.Close: Symbol(WorkshopIcon.Close); break;
                case PlayerIcon.ChevronDown: Symbol(WorkshopIcon.Chevron); break;
                case PlayerIcon.ChevronUp: Symbol(WorkshopIcon.ChevronUp); break;
                case PlayerIcon.Lock: Symbol(WorkshopIcon.Lock); break;
                case PlayerIcon.Fast:
                    Solid(2, 5, 12, 12, 2, 19);
                    Solid(12, 5, 22, 12, 12, 19);
                    break;
                case PlayerIcon.Home:
                    Solid(1, 11, 12, 2, 12, 6, 5, 12);
                    Solid(12, 2, 23, 11, 19, 12, 12, 6);
                    Wash(5, 12, 12, 6, 19, 12, 19, 21, 5, 21);
                    Path(5, 12, 5, 21, 19, 21, 19, 12);
                    Solid(10, 21, 10, 14, 14, 14, 14, 21);
                    break;
                case PlayerIcon.Map:
                    Wash(3, 6, 9, 3, 9, 18, 3, 21);
                    Wash(15, 6, 21, 3, 21, 18, 15, 21);
                    Path(3, 21, 3, 6, 9, 3, 15, 6, 21, 3, 21, 18, 15, 21, 9, 18, 3, 21);
                    Stroke(9, 3, 9, 18);
                    Stroke(15, 6, 15, 21);
                    break;
                case PlayerIcon.Exit:
                    Wash(3, 3, 10, 5, 10, 20, 3, 21);
                    Path(10, 21, 3, 21, 3, 3, 10, 3);
                    Stroke(9, 12, 18, 12);
                    Solid(15, 6, 22, 12, 15, 18);
                    break;
                case PlayerIcon.Flag:
                    Stroke(5, 22, 5, 3);
                    Wash(6, 3, 14, 4, 14, 12, 6, 11);
                    Path(5, 3, 14, 4, 14, 12, 5, 11);
                    Solid(14, 6, 22, 5, 19, 9, 22, 13, 14, 14);
                    break;
                case PlayerIcon.Trophy:
                    Wash(7, 4, 17, 4, 16, 11, 12, 15, 8, 11);
                    Path(7, 4, 17, 4, 16, 11, 12, 15, 8, 11, 7, 4);
                    Path(7, 6, 3, 6, 4, 10, 8, 12);
                    Path(17, 6, 21, 6, 20, 10, 16, 12);
                    Stroke(12, 15, 12, 19);
                    Solid(9, 18, 15, 18, 18, 21, 6, 21);
                    break;
                case PlayerIcon.Blotter:
                    Wash(6, 11, 18, 11, 21, 17, 3, 17);
                    Path(3, 17, 6, 11, 18, 11, 21, 17, 3, 17);
                    Path(9, 10, 9, 6, 10, 4, 14, 4, 15, 6, 15, 10);
                    Stroke(5, 20, 19, 20);
                    break;
                case PlayerIcon.Info:
                    Ring(12, 12, 9);
                    Dot(12, 7.5f, 1.4f);
                    Stroke(12, 11, 12, 17);
                    break;
            }
        }

        void Symbol(WorkshopIcon icon)
        {
            switch (icon)
            {
                case WorkshopIcon.Tower:
                    Wash(5, 11, 19, 11, 18, 21, 6, 21);
                    Path(6, 20, 6, 11, 4, 10, 4, 4, 8, 4, 8, 7, 10, 7, 10, 4, 14, 4, 14, 7, 16, 7, 16, 4, 20, 4, 20, 10, 18, 11, 18, 20);
                    Solid(10, 20, 10, 15, 12, 13, 14, 15, 14, 20);
                    Stroke(4, 21, 20, 21);
                    break;
                case WorkshopIcon.Weapon:
                    Wash(3, 10, 18, 4, 21, 9, 6, 15);
                    Path(3, 10, 18, 4, 21, 9, 6, 15, 3, 10);
                    Stroke(15, 6, 17, 10);
                    Ring(8, 17, 3);
                    Path(13, 13, 16, 18, 21, 18);
                    break;
                case WorkshopIcon.Pencil:
                    Wash(4, 14, 14, 4, 20, 10, 10, 20);
                    Path(3, 21, 4, 14, 16, 2, 22, 8, 10, 20, 3, 21);
                    Solid(4, 14, 8, 18, 3, 21);
                    Solid(14, 4, 16, 2, 22, 8, 20, 10);
                    if (detail) Stroke(8, 15, 16, 7, 1);
                    break;
                case WorkshopIcon.Book:
                    Wash(3, 5, 7, 4, 12, 7, 12, 21, 7, 18, 3, 18);
                    Path(12, 21, 7, 18, 3, 18, 3, 5, 7, 4, 12, 7, 17, 4, 21, 5, 21, 18, 17, 18, 12, 21, 12, 7);
                    Solid(15, 5, 18, 4, 18, 11, 16.5f, 9.5f, 15, 11);
                    break;
                case WorkshopIcon.Target:
                    Ring(12, 12, 6.6f);
                    Dot(12, 12, 2.2f);
                    Stroke(12, 2, 12, 6);
                    Stroke(12, 18, 12, 22);
                    Stroke(2, 12, 6, 12);
                    Stroke(18, 12, 22, 12);
                    break;
                case WorkshopIcon.Check:
                    Path(3.5f, 12.5f, 9, 18, 20.5f, 5.5f);
                    Stroke(9, 17, 20, 5, 2.8f);
                    break;
                case WorkshopIcon.Back:
                    Stroke(5, 12, 21, 12, 2.8f);
                    Path(10, 5, 3, 12, 10, 19);
                    break;
                case WorkshopIcon.Next:
                    Stroke(3, 12, 19, 12, 2.8f);
                    Path(14, 5, 21, 12, 14, 19);
                    break;
                case WorkshopIcon.Eraser:
                    Wash(9, 9, 15, 3, 21, 9, 15, 15);
                    Path(3, 15, 15, 3, 21, 9, 10, 20, 8, 20, 3, 15);
                    Stroke(8, 10, 15, 16);
                    Stroke(13, 21, 21, 21);
                    break;
                case WorkshopIcon.Line:
                    Stroke(5, 19, 19, 5);
                    Dot(5, 19, 2.4f);
                    Dot(19, 5, 2.4f);
                    break;
                case WorkshopIcon.Rectangle:
                    Wash(4, 7, 9, 7, 9, 19, 4, 19);
                    Path(3, 6, 21, 6, 21, 19, 3, 19, 3, 6);
                    break;
                case WorkshopIcon.Ellipse:
                    pen.Disc(Point(12, 12), 8.5f, wash, 24, .68f);
                    pen.Ring(Point(12, 12), 9, weight, ink, 28, .68f);
                    break;
                case WorkshopIcon.Move:
                    Stroke(4, 12, 20, 12);
                    Stroke(12, 4, 12, 20);
                    Solid(8, 6, 12, 2, 16, 6);
                    Solid(18, 8, 22, 12, 18, 16);
                    Solid(8, 18, 12, 22, 16, 18);
                    Solid(6, 8, 2, 12, 6, 16);
                    break;
                case WorkshopIcon.Undo:
                    Path(20, 19, 20, 14, 18, 10, 15, 8, 6, 8);
                    Solid(9, 2, 2, 8, 9, 14);
                    break;
                case WorkshopIcon.Redo:
                    Path(4, 19, 4, 14, 6, 10, 9, 8, 18, 8);
                    Solid(15, 2, 22, 8, 15, 14);
                    break;
                case WorkshopIcon.Grid:
                    Path(4, 4, 20, 4, 20, 20, 4, 20, 4, 4);
                    Stroke(12, 4, 12, 20);
                    Stroke(4, 12, 20, 12);
                    Wash(4, 4, 12, 4, 12, 12, 4, 12);
                    break;
                case WorkshopIcon.Magnet:
                    Path(5, 4, 5, 14, 6, 17, 9, 20, 15, 20, 18, 17, 19, 14, 19, 4);
                    Path(9, 4, 9, 14, 10, 16, 14, 16, 15, 14, 15, 4);
                    Solid(4, 3, 10, 3, 10, 9, 4, 9);
                    Solid(14, 3, 20, 3, 20, 9, 14, 9);
                    break;
                case WorkshopIcon.ZoomIn:
                case WorkshopIcon.ZoomOut:
                    Ring(10, 10, 6.8f);
                    Stroke(15, 15, 21, 21, 3);
                    Stroke(7, 10, 13, 10);
                    if (icon == WorkshopIcon.ZoomIn) Stroke(10, 7, 10, 13);
                    break;
                case WorkshopIcon.Fit:
                    Path(3, 8, 3, 3, 8, 3);
                    Path(16, 3, 21, 3, 21, 8);
                    Path(21, 16, 21, 21, 16, 21);
                    Path(8, 21, 3, 21, 3, 16);
                    Wash(7, 8, 17, 8, 17, 16, 7, 16);
                    break;
                case WorkshopIcon.Mirror:
                    Stroke(12, 3, 12, 21);
                    Solid(2, 18, 8, 7, 8, 18);
                    Path(22, 18, 16, 7, 16, 18, 22, 18);
                    break;
                case WorkshopIcon.Trash:
                    Wash(6, 8, 18, 8, 17, 21, 7, 21);
                    Path(6, 8, 7, 21, 17, 21, 18, 8);
                    Path(9, 5, 10, 3, 14, 3, 15, 5);
                    Stroke(4, 6, 20, 6, 2.4f);
                    Stroke(12, 11, 12, 17);
                    break;
                case WorkshopIcon.Chevron: Path(4, 8, 12, 16, 20, 8); break;
                case WorkshopIcon.ChevronUp: Path(4, 16, 12, 8, 20, 16); break;
                case WorkshopIcon.Damage:
                    Solid(16, 1.5f, 4, 14, 13, 14);
                    Solid(8.5f, 22.5f, 20.5f, 10, 11, 10);
                    break;
                case WorkshopIcon.FiringArc:
                    Arc(12, 17, 10, 205, 335);
                    Path(2.9f, 12.8f, 12, 17, 21.1f, 12.8f);
                    Stroke(12, 17, 12, 10, 1.3f);
                    break;
                case WorkshopIcon.Range:
                    Ring(12, 12, 6.8f);
                    Stroke(3, 12, 21, 12);
                    Solid(2, 12, 6, 8, 6, 16);
                    Solid(22, 12, 18, 8, 18, 16);
                    break;
                case WorkshopIcon.Cost:
                    pen.Disc(Point(12, 12), 8.8f, wash, 24);
                    Ring(12, 12, 9);
                    Solid(12, 6, 16, 12, 12, 18, 8, 12);
                    break;
                case WorkshopIcon.Ink:
                    Wash(12, 2, 18, 11, 19, 15, 17, 19, 13, 21, 9, 21, 6, 18, 5, 14, 6, 11);
                    Path(12, 2, 18, 11, 19, 15, 17, 19, 13, 21, 9, 21, 6, 18, 5, 14, 6, 11, 12, 2);
                    Path(8, 14, 9, 17, 11, 18);
                    break;
                case WorkshopIcon.Shield:
                case WorkshopIcon.Armor:
                    Wash(12, 3, 20, 6, 18, 15, 15, 19, 12, 21);
                    Path(12, 3, 20, 6, 18, 15, 15, 19, 12, 21, 9, 19, 6, 15, 4, 6, 12, 3);
                    if (icon == WorkshopIcon.Armor) { Stroke(8, 11, 16, 11); Stroke(12, 7, 12, 16); }
                    else Stroke(12, 5, 12, 18);
                    break;
                case WorkshopIcon.Plus:
                    Stroke(4, 12, 20, 12, 2.7f);
                    Stroke(12, 4, 12, 20, 2.7f);
                    break;
                case WorkshopIcon.Close:
                    Stroke(5, 5, 19, 19, 2.5f);
                    Stroke(5, 19, 19, 5, 2.5f);
                    break;
                case WorkshopIcon.Help:
                    Ring(12, 12, 9.5f);
                    Path(8, 8.5f, 9, 6.5f, 13, 6, 15.5f, 7.5f, 15.5f, 10, 12, 13, 12, 14);
                    Dot(12, 17.5f, 1.25f);
                    break;
                case WorkshopIcon.Save:
                    Wash(3, 3, 17, 3, 21, 7, 21, 20, 3, 20);
                    Path(3, 3, 17, 3, 21, 7, 21, 21, 3, 21, 3, 3);
                    Solid(7, 3, 15, 3, 15, 9, 7, 9);
                    Path(7, 20, 7, 14, 17, 14, 17, 20);
                    break;
                case WorkshopIcon.Settings:
                    Gear();
                    break;
                case WorkshopIcon.Copy:
                    Path(5, 16, 3, 16, 3, 3, 14, 3);
                    Wash(8, 7, 17, 7, 21, 11, 21, 21, 8, 21);
                    Path(8, 7, 17, 7, 21, 11, 21, 21, 8, 21, 8, 7);
                    Solid(16, 7, 21, 12, 16, 12);
                    break;
                case WorkshopIcon.Weight:
                    Ring(12, 6, 3);
                    Wash(7, 9, 17, 9, 21, 21, 3, 21);
                    Path(7, 9, 17, 9, 21, 21, 3, 21, 7, 9);
                    Stroke(9, 16, 15, 16);
                    break;
                case WorkshopIcon.Compare:
                    Stroke(3, 8, 18, 8);
                    Solid(15, 3, 22, 8, 15, 13);
                    Stroke(6, 16, 21, 16);
                    Solid(9, 11, 2, 16, 9, 21);
                    break;
                case WorkshopIcon.Pin:
                    Wash(7, 3, 17, 3, 15, 9, 19, 14, 5, 14, 9, 9);
                    Path(7, 3, 17, 3, 15, 9, 19, 14, 5, 14, 9, 9, 7, 3);
                    Solid(10.5f, 14, 13.5f, 14, 12, 22);
                    break;
                case WorkshopIcon.Clock:
                    Ring(12, 12, 9);
                    Wash(12, 4, 12, 12, 17.5f, 15.5f, 20, 12, 18, 7);
                    Path(12, 6, 12, 12, 16, 15);
                    break;
                case WorkshopIcon.Lock:
                    Path(7, 10, 7, 7, 8.5f, 4, 12, 3, 15.5f, 4, 17, 7, 17, 10);
                    Wash(4, 10, 20, 10, 20, 21, 4, 21);
                    Path(4, 10, 20, 10, 20, 21, 4, 21, 4, 10);
                    Stroke(12, 14, 12, 17, 2.6f);
                    break;
                case WorkshopIcon.Heart:
                    Wash(12, 20, 3, 11, 3, 7, 6, 4, 9, 4, 12, 7, 15, 4, 18, 4, 21, 7, 21, 11);
                    Path(12, 20, 3, 11, 3, 7, 6, 4, 9, 4, 12, 7, 15, 4, 18, 4, 21, 7, 21, 11, 12, 20);
                    if (detail) Stroke(6, 9, 7, 7, 1);
                    break;
                case WorkshopIcon.Reload:
                    Arc(12, 12, 8, -55, 245);
                    Solid(9, 2, 16, 5, 10, 9);
                    Path(12, 9, 12, 13, 15, 14);
                    break;
                case WorkshopIcon.Magazine:
                    Wash(5, 4, 19, 4, 18, 21, 7, 21);
                    Path(5, 4, 19, 4, 18, 21, 7, 21, 5, 4);
                    Stroke(6, 8, 18, 8, 2.6f);
                    Stroke(10, 12, 11, 17);
                    Stroke(15, 12, 15, 17);
                    break;
                case WorkshopIcon.Play:
                    Solid(6, 3, 21, 12, 6, 21);
                    break;
                case WorkshopIcon.Pause:
                    Solid(5, 4, 10, 4, 10, 20, 5, 20);
                    Solid(14, 4, 19, 4, 19, 20, 14, 20);
                    break;
                case WorkshopIcon.Restart:
                    Arc(12, 12, 8, -148, 148);
                    Solid(3, 3, 3, 11, 11, 10);
                    break;
            }
        }

        static Vector2 Point(float x, float y) => new Vector2(x - 12, 12 - y);

        void Stroke(float ax, float ay, float bx, float by, float thickness = 0)
        {
            pen.Line(Point(ax, ay), Point(bx, by), thickness > 0 ? thickness : weight, ink);
        }

        void Path(params float[] coordinates)
        {
            for (int i = 0; i < coordinates.Length; i += 2)
            {
                float x = coordinates[i], y = coordinates[i + 1];
                if (i > 0) Stroke(coordinates[i - 2], coordinates[i - 1], x, y);
                Dot(x, y, weight * .5f);
            }
        }

        void Dot(float x, float y, float radius) => pen.Disc(Point(x, y), radius, ink, 12);
        void Ring(float x, float y, float radius) => pen.Ring(Point(x, y), radius, weight, ink, 32);
        void Solid(params float[] xy) => Polygon(ink, xy);
        void Wash(params float[] xy) => Polygon(wash, xy);

        static void Polygon(Color color, float[] xy)
        {
            Vector2 first = Point(xy[0], xy[1]);
            for (int i = 4; i < xy.Length; i += 2)
                pen.Triangle(first, Point(xy[i - 2], xy[i - 1]), Point(xy[i], xy[i + 1]), color);
        }

        void Arc(float x, float y, float radius, float start, float end)
        {
            const int steps = 30;
            float angle = start * Mathf.Deg2Rad;
            Vector2 last = Point(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius);
            for (int i = 1; i <= steps; i++)
            {
                angle = Mathf.Lerp(start, end, i / (float)steps) * Mathf.Deg2Rad;
                Vector2 next = Point(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius);
                pen.Line(last, next, weight, ink);
                last = next;
            }
        }

        void Gear()
        {
            // Six broad teeth leave a clean open bearing even in a sixteen-pixel control.
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3;
                Vector2 radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 tangent = new Vector2(-radial.y, radial.x);
                pen.Quad(radial * 6 - tangent * 2, radial * 9.4f - tangent * 2,
                    radial * 9.4f + tangent * 2, radial * 6 + tangent * 2, ink);
            }
            pen.Ring(Vector2.zero, 5.7f, 3.6f, ink, 36);
        }
    }
}
