using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Notebook binding and drafting marks in the gutter outside the camera viewport.</summary>
    public sealed class BattlePageFrameGraphic : MaskableGraphic
    {
        Rect field;
        readonly MeshPen pen = new MeshPen(null, 768);

        public void SetField(Rect bounds)
        {
            field = bounds;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear();
            if (field.width <= 0 || field.height <= 0) return;
            Rect page = rectTransform.rect;
            float left = page.xMin + field.xMin, right = page.xMin + field.xMax;
            float top = page.yMax - field.yMin, bottom = page.yMax - field.yMax;
            pen.Clear();
            Color binding = FieldMenuSkin.Background;
            pen.Rect(page.xMin, top, page.width, page.yMax - top, binding);
            pen.Rect(page.xMin, page.yMin, page.width, bottom - page.yMin, binding);
            pen.Rect(page.xMin, bottom, left - page.xMin, field.height, binding);
            pen.Rect(right, bottom, page.xMax - right, field.height, binding);

            Color edge = Color.Lerp(binding, FieldMenuSkin.Border, .52f);
            pen.Rect(left - 1, bottom - 1, field.width + 2, 1, edge);
            pen.Rect(left - 1, top, field.width + 2, 1, edge);
            pen.Rect(left - 1, bottom, 1, field.height, edge);
            pen.Rect(right, bottom, 1, field.height, edge);

            Color fine = Color.Lerp(binding, FieldMenuSkin.Muted, .27f);
            Color major = Color.Lerp(binding, FieldMenuSkin.Accent, .62f);
            for (int i = 0; i * 24 < field.width; i++)
            {
                float x = left + i * 24;
                bool full = i % 4 == 0;
                float depth = full ? 5 : 2;
                pen.Rect(x, top + 2, 1, depth, full ? major : fine);
                pen.Rect(x, bottom - 2 - depth, 1, depth, full ? major : fine);
            }
            for (int i = 0; i * 24 < field.height; i++)
            {
                float y = top - i * 24;
                bool full = i % 4 == 0;
                float depth = full ? 5 : 2;
                pen.Rect(left - 2 - depth, y, depth, 1, full ? major : fine);
                pen.Rect(right + 2, y, depth, 1, full ? major : fine);
            }
            pen.CopyTo(target, Vector2.zero, 1);
        }
    }
}
