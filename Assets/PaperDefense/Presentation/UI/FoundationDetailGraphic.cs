using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Small blue-pen details for the foundation page; all dimensions are local UI pixels.</summary>
    public sealed class FoundationDetailGraphic : MaskableGraphic
    {
        public enum Kind { Frame, ProgressCircle, MountPips, Divider, SelectedCorner, GoldStack, Arrow }

        [SerializeField] Kind shape;
        [SerializeField] bool selected, focused, hovered;
        [SerializeField] int count = 1, total = 3;
        [SerializeField] float strokeWidth = 1.2f, radius = 13;
        [SerializeField] Color fill = Color.clear;
        readonly MeshPen pen = new MeshPen(null, 2048);

        public Kind Shape { get => shape; set { if (shape == value) return; shape = value; SetVerticesDirty(); } }
        public bool Selected { get => selected; set { if (selected == value) return; selected = value; SetVerticesDirty(); } }
        public bool Focused { get => focused; set { if (focused == value) return; focused = value; SetVerticesDirty(); } }
        public bool Hovered { get => hovered; set { if (hovered == value) return; hovered = value; SetVerticesDirty(); } }
        public int Count { get => count; set { if (count == value) return; count = value; SetVerticesDirty(); } }
        public int Total { get => total; set { if (total == value) return; total = value; SetVerticesDirty(); } }
        public float StrokeWidth { get => strokeWidth; set { if (strokeWidth == value) return; strokeWidth = value; SetVerticesDirty(); } }
        public float Radius { get => radius; set { if (radius == value) return; radius = value; SetVerticesDirty(); } }
        public Color Fill { get => fill; set { if (fill == value) return; fill = value; SetVerticesDirty(); } }
        public override Texture mainTexture => NotebookSpriteCatalog.Texture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); pen.Clear();
            Rect bounds = rectTransform.rect;
            if (bounds.width <= 0 || bounds.height <= 0) return;
            switch (shape)
            {
                case Kind.Frame: Frame(bounds); break;
                case Kind.ProgressCircle: Progress(bounds); break;
                case Kind.MountPips: Pips(bounds); break;
                case Kind.Divider:
                    if (bounds.width >= bounds.height)
                        pen.Line(new Vector2(bounds.xMin + 1, bounds.center.y), new Vector2(bounds.xMax - 1, bounds.center.y), strokeWidth, color);
                    else pen.Line(new Vector2(bounds.center.x, bounds.yMin + 1), new Vector2(bounds.center.x, bounds.yMax - 1), strokeWidth, color);
                    break;
                case Kind.SelectedCorner: Corner(bounds); break;
                case Kind.GoldStack: Gold(bounds); break;
                case Kind.Arrow: Arrow(bounds); break;
            }
            pen.CopyTo(vh, Vector2.zero, 1);
        }

        void Frame(Rect bounds)
        {
            Color ink = selected ? WorkshopUi.Blue : color;
            float weight = focused ? 2.3f : selected || hovered ? Mathf.Max(1.8f, strokeWidth) : strokeWidth;
            Rounded(Inset(bounds, 1.5f), radius, fill, ink, weight);
            if (fill.a > .9f && fill.grayscale < .6f)
            {
                var inside = Inset(bounds, 7);
                for (float x = inside.xMin; x < inside.xMax - 16; x += 7)
                    pen.Line(new Vector2(x, inside.yMin), new Vector2(x + 16, inside.yMax), .6f, Alpha(WorkshopUi.Paper, .14f));
            }
            Rounded(Inset(bounds, 4.2f), Mathf.Max(0, radius - 2.7f), Color.clear, Alpha(ink, .34f), .65f);
            if (focused)
            {
                var inner = Inset(bounds, 9);
                Color focus = fill.a > .9f && fill.grayscale < .6f ? WorkshopUi.Paper : WorkshopUi.Blue;
                foreach (var corner in new[] { new Vector2(inner.xMin, inner.yMin), new Vector2(inner.xMin, inner.yMax), new Vector2(inner.xMax, inner.yMin), new Vector2(inner.xMax, inner.yMax) })
                {
                    pen.Line(corner, corner + new Vector2(corner.x == inner.xMin ? 12 : -12, 0), 2, focus);
                    pen.Line(corner, corner + new Vector2(0, corner.y == inner.yMin ? 12 : -12), 2, focus);
                }
            }
        }

        void Rounded(Rect bounds, float cornerRadius, Color paper, Color ink, float weight)
        {
            float r = Mathf.Clamp(cornerRadius, 0, Mathf.Min(bounds.width, bounds.height) * .5f);
            const int segments = 8, pointCount = (segments + 1) * 4;
            Vector2 previous = RoundedPoint(bounds, r, pointCount - 1);
            for (int i = 0; i < pointCount; i++)
            {
                Vector2 next = RoundedPoint(bounds, r, i);
                if (paper.a > 0) pen.Triangle(bounds.center, previous, next, paper);
                pen.Line(previous, next, weight, ink);
                previous = next;
            }
        }

        static Vector2 RoundedPoint(Rect bounds, float r, int index)
        {
            const int segments = 8;
            int corner = index / (segments + 1);
            float angle = (corner * 90 + index % (segments + 1) * 90f / segments) * Mathf.Deg2Rad;
            float x = corner == 0 || corner == 3 ? bounds.xMax - r : bounds.xMin + r;
            float y = corner < 2 ? bounds.yMax - r : bounds.yMin + r;
            return new Vector2(x + Mathf.Cos(angle) * r, y + Mathf.Sin(angle) * r);
        }

        void Progress(Rect bounds)
        {
            float r = Mathf.Max(0, Mathf.Min(bounds.width, bounds.height) * .5f - 2);
            Color ink = selected ? WorkshopUi.Blue : color;
            pen.Disc(bounds.center, r, selected ? WorkshopUi.Blue : WorkshopUi.Paper, 48);
            pen.Ring(bounds.center, r, selected ? 2.1f : strokeWidth, ink, 48);
            if (focused || hovered) pen.Ring(bounds.center, r - 1, focused ? 2.8f : 1.8f, WorkshopUi.Blue, 48);
            pen.Ring(bounds.center + new Vector2(.35f, -.3f), r - 3.1f, selected ? 1.0f : .65f,
                selected ? Alpha(WorkshopUi.Paper, .9f) : Alpha(ink, .23f), 48);
        }

        void Pips(Rect bounds)
        {
            int slots = Mathf.Clamp(total, 1, 8), filled = Mathf.Clamp(count, 0, slots);
            float diameter = Mathf.Min(bounds.height - 3, (bounds.width - (slots - 1) * 5) / slots);
            float r = Mathf.Max(1, diameter * .5f - .5f), gap = diameter + 5;
            float first = bounds.center.x - (slots - 1) * gap * .5f;
            for (int i = 0; i < slots; i++)
            {
                var center = new Vector2(first + i * gap, bounds.center.y);
                pen.Disc(center, r, i < filled ? color : WorkshopUi.Paper, 28);
                pen.Ring(center, r, strokeWidth, color, 32);
                if (i < filled)
                    pen.Line(center + new Vector2(-r * .5f, r * .3f), center + new Vector2(-r * .15f, r * .65f), .8f, Alpha(WorkshopUi.Paper, .7f));
            }
        }

        void Corner(Rect bounds)
        {
            float size = Mathf.Min(bounds.width, bounds.height);
            var a = new Vector2(bounds.xMax - size, bounds.yMax);
            var b = new Vector2(bounds.xMax, bounds.yMax);
            var c = new Vector2(bounds.xMax, bounds.yMax - size);
            var d = new Vector2(bounds.xMax - size * .52f, bounds.yMax - size);
            var e = new Vector2(bounds.xMax - size, bounds.yMax - size * .52f);
            pen.Triangle(a, b, c, WorkshopUi.Blue);
            pen.Triangle(a, c, d, WorkshopUi.Blue);
            pen.Triangle(a, d, e, WorkshopUi.Blue);
            pen.Line(a + new Vector2(3, -3), b + new Vector2(-3, -3), .7f, Alpha(WorkshopUi.Paper, .5f));
            var p = new Vector2(bounds.xMax - size * .75f, bounds.yMax - size * .48f);
            var q = new Vector2(bounds.xMax - size * .51f, bounds.yMax - size * .72f);
            var r = new Vector2(bounds.xMax - size * .18f, bounds.yMax - size * .27f);
            pen.Line(p, q, size * .12f, WorkshopUi.Paper);
            pen.Line(q, r, size * .12f, WorkshopUi.Paper);
        }

        void Gold(Rect bounds)
        {
            float scale = Mathf.Min(bounds.width, bounds.height) / 30;
            Vector2 center = bounds.center;
            Coin(center + new Vector2(-8, -7) * scale, 5.8f * scale);
            Coin(center + new Vector2(2, -8) * scale, 5.8f * scale);
            Coin(center + new Vector2(9, -5) * scale, 5.6f * scale);
            Coin(center + new Vector2(-4, 0) * scale, 5.7f * scale);
            Coin(center + new Vector2(5, 1) * scale, 5.6f * scale);
            Coin(center + new Vector2(0, 8) * scale, 5.7f * scale);
        }

        void Coin(Vector2 center, float r)
        {
            Color edge = new Color32(111, 83, 33, 255), gold = new Color32(223, 178, 69, 255);
            pen.Disc(center + new Vector2(0, -r * .28f), r, edge, 20, .72f);
            pen.Disc(center, r, gold, 24, .78f);
            pen.Ring(center, r, Mathf.Max(.8f, r * .15f), edge, 28, .78f);
            pen.Disc(center + new Vector2(-r * .18f, r * .21f), r * .58f, new Color32(249, 219, 133, 255), 20, .72f);
            pen.Line(center + new Vector2(-r * .43f, r * .34f), center + new Vector2(-r * .13f, r * .51f), Mathf.Max(.65f, r * .10f), WorkshopUi.Paper);
        }

        void Arrow(Rect bounds)
        {
            Vector2 start = new Vector2(bounds.xMin + bounds.width * .12f, bounds.yMax - bounds.height * .08f);
            Vector2 end = new Vector2(bounds.xMax - bounds.width * .16f, bounds.yMin + bounds.height * .12f);
            Vector2 a = start + new Vector2(bounds.width * .52f, -bounds.height * .12f);
            Vector2 b = end + new Vector2(-bounds.width * .10f, bounds.height * .48f);
            Vector2 previous = start;
            for (int i = 1; i <= 20; i++)
            {
                float t = i / 20f, s = 1 - t;
                Vector2 next = s * s * s * start + 3 * s * s * t * a + 3 * s * t * t * b + t * t * t * end;
                pen.Line(previous, next, strokeWidth, color); previous = next;
            }
            Vector2 direction = (end - b).normalized, side = new Vector2(-direction.y, direction.x);
            float tip = Mathf.Clamp(Mathf.Min(bounds.width, bounds.height) * .19f, 5, 12);
            pen.Line(end - direction * tip + side * tip * .43f, end, strokeWidth, color);
            pen.Line(end - direction * tip - side * tip * .43f, end, strokeWidth, color);
        }

        static Rect Inset(Rect rect, float amount) => new Rect(rect.x + amount, rect.y + amount,
            Mathf.Max(0, rect.width - amount * 2), Mathf.Max(0, rect.height - amount * 2));
        static Color Alpha(Color value, float multiplier) { value.a *= multiplier; return value; }
    }
}
