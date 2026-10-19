using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed class PlayerPaperGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var pen = new MeshPen(null, 4096); var r = rectTransform.rect;
            if (r.width <= 0 || r.height <= 0) return;
            Color grid = PlayerUi.Grid;
            for (float x = r.xMin; x < r.xMax; x += 28) pen.Line(new Vector2(x, r.yMin), new Vector2(x, r.yMax), .7f, grid);
            for (float y = r.yMin; y < r.yMax; y += 28) pen.Line(new Vector2(r.xMin, y), new Vector2(r.xMax, y), .7f, grid);
            pen.CopyTo(vh, Vector2.zero, 1);
        }
    }

    /// <summary>Notebook decoration is geometry around the live page, never a baked UI screenshot.</summary>
    public sealed class NotebookBindingGraphic : MaskableGraphic
    {
        public override Texture mainTexture => NotebookSpriteCatalog.Texture;
        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear(); var pen = new MeshPen(null, 4096); Rect r = rectTransform.rect;
            for (float y = r.yMin + 24; y < r.yMax; y += 52)
            {
                pen.Box(new Vector2(r.xMin + 15, y), new Vector2(23, 12), new Color32(94, 91, 78, 255), PlayerUi.Ink, 1.5f, 17);
                pen.Line(new Vector2(r.xMin + 2, y + 3), new Vector2(r.xMin + 24, y + 3), 4, new Color32(61, 70, 75, 255));
                pen.Line(new Vector2(r.xMin + 2, y + 4), new Vector2(r.xMin + 24, y + 4), 1, new Color32(183, 184, 171, 255));
            }
            for (int side = 0; side < 2; side++)
            for (int tree = 0; tree < 5; tree++)
            {
                float x = side == 0 ? r.xMin + 40 + tree % 2 * 28 : r.xMax - 30 - tree % 2 * 25;
                float y = r.yMin + 70 + tree * (r.height - 150) / 5;
                NotebookSpriteCatalog.TryDraw(pen, "terrain.pine", new Vector2(x, y), 64 + tree % 2 * 22, opacity: .72f);
            }
            pen.CopyTo(target, Vector2.zero, 1);
        }
    }

    public sealed class NotebookOpenBookGraphic : MaskableGraphic
    {
        public override Texture mainTexture => NotebookSpriteCatalog.Texture;
        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear(); var pen = new MeshPen(null, 2048); Rect r = rectTransform.rect;
            Vector2 P(float x, float y) => new Vector2(r.xMin + r.width * x, r.yMin + r.height * y);
            var paper = new Color32(252, 247, 231, 255);
            pen.Quad(P(.045f, .06f), P(.5f, .03f), P(.96f, .09f), P(.93f, .92f), PlayerUi.Ink);
            pen.Quad(P(.05f, .09f), P(.5f, .06f), P(.5f, .92f), P(.08f, .89f), paper);
            pen.Quad(P(.5f, .06f), P(.95f, .12f), P(.92f, .95f), P(.5f, .92f), paper);
            for (int row = 0; row < 8; row++)
            {
                float y = .16f + row * .09f;
                pen.Line(P(.075f, y), P(.485f, y - .015f), .65f, PlayerUi.Grid);
                pen.Line(P(.515f, y - .015f), P(.935f, y + .03f), .65f, PlayerUi.Grid);
            }
            for (int column = 0; column < 12; column++)
            {
                float x = .10f + column * .07f;
                if (x > .47f && x < .53f) continue;
                pen.Line(P(x, .13f), P(x, .9f), .65f, PlayerUi.Grid);
            }
            pen.Line(P(.08f, .89f), P(.50f, .92f), 1.8f, PlayerUi.Ink);
            pen.Line(P(.50f, .92f), P(.92f, .95f), 1.8f, PlayerUi.Ink);
            pen.Line(P(.08f, .89f), P(.05f, .09f), 1.8f, PlayerUi.Ink);
            pen.Line(P(.92f, .95f), P(.95f, .12f), 1.8f, PlayerUi.Ink);
            pen.Line(P(.05f, .09f), P(.50f, .06f), 1.8f, PlayerUi.Ink);
            pen.Line(P(.50f, .06f), P(.95f, .12f), 1.8f, PlayerUi.Ink);
            pen.Line(P(.50f, .06f), P(.50f, .92f), 2.2f, PlayerUi.Ink);
            for (int ring = 0; ring < 10; ring++)
            {
                float y = .13f + ring * .075f;
                pen.Line(P(.484f, y), P(.517f, y + .018f), 3.5f, PlayerUi.Ink);
                pen.Line(P(.484f, y + .008f), P(.517f, y + .026f), 1.1f, paper);
            }
            pen.CopyTo(target, Vector2.zero, 1);
        }
    }

    public sealed class PlayerCardGraphic : MaskableGraphic
    {
        public float Radius = 4;
        public bool NotebookStyle;
        public bool Selected;
        public bool Focused;
        public bool Control;
        public bool Disabled;
        public bool Hovered;
        public bool Pressed;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Rect r = rectTransform.rect;
            if (NotebookStyle && color.a >= .9f && r.width >= 8 && r.height >= 8)
            { NotebookPaperSurface.Draw(vh, r, color, Control, Selected, Focused, Disabled, Hovered, Pressed); return; }
            float radius = Mathf.Min(Radius, r.width * .5f, r.height * .5f);
            vh.AddVert(r.center, color, Vector2.zero);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 center = new Vector2(corner == 0 || corner == 3 ? r.xMax - radius : r.xMin + radius,
                    corner < 2 ? r.yMax - radius : r.yMin + radius);
                for (int i = 0; i <= 8; i++)
                {
                    float angle = (corner * 90 + i * 90f / 8) * Mathf.Deg2Rad;
                    vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                }
            }
            for (int i = 1; i <= 36; i++) vh.AddTriangle(0, i, i == 36 ? 1 : i + 1);
            if (Selected)
            {
                var pen = new MeshPen(null, 64);
                var a = new Vector2(r.xMin + 3, r.yMin + 3); var b = new Vector2(r.xMax - 3, r.yMax - 3);
                pen.Line(a, new Vector2(b.x, a.y), 2, PlayerUi.Ink);
                pen.Line(new Vector2(b.x, a.y), b, 2, PlayerUi.Ink);
                pen.Line(b, new Vector2(a.x, b.y), 2, PlayerUi.Ink);
                pen.Line(new Vector2(a.x, b.y), a, 2, PlayerUi.Ink);
                pen.CopyTo(vh, Vector2.zero, 1);
            }
            if (Focused)
            {
                var pen = new MeshPen(null, 64);
                Color focusInk = color == PlayerUi.Blue ? PlayerUi.Card : PlayerUi.Blue;
                float inset = Selected ? 7 : 3;
                var a = new Vector2(r.xMin + inset, r.yMin + inset);
                var b = new Vector2(r.xMax - inset, r.yMax - inset);
                foreach (var corner in new[] { a, new Vector2(a.x, b.y), b, new Vector2(b.x, a.y) })
                {
                    pen.Line(corner, corner + new Vector2(corner.x == a.x ? 10 : -10, 0), 2, focusInk);
                    pen.Line(corner, corner + new Vector2(0, corner.y == a.y ? 10 : -10), 2, focusInk);
                }
                pen.CopyTo(vh, Vector2.zero, 1);
            }
        }
    }
}
