using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>A quiet squared sheet behind live assembly geometry, with no input or baked content.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    public sealed class NightNotebookPaperGraphic : UnityEngine.UI.MaskableGraphic
    {
        const int MaximumGridIntervals = 48;
        public override Texture mainTexture => Texture2D.whiteTexture;

        protected override void OnEnable()
        {
            raycastTarget = false;
            base.OnEnable();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper target)
        {
            target.Clear();
            var r = rectTransform.rect;
            if (r.width <= 0 || r.height <= 0 || !Finite(r.xMin) || !Finite(r.xMax) ||
                !Finite(r.yMin) || !Finite(r.yMax)) return;

            Fill(target, r, r, NightNotebookArt.Paper);
            if (r.height < 72 || r.width < 96)
            {
                // A single ground stroke anchors thumbnails without boxing in the silhouette.
                float lineWidth = r.width * .68f;
                Fill(target, r, new Rect(r.center.x - lineWidth * .5f, r.yMin + r.height * .08f, lineWidth, .9f),
                    MeshPen.Alpha(NightNotebookArt.Pencil, .18f));
                return;
            }

            bool draftingSheet = r.width >= 208 && r.height >= 136;
            float inset = draftingSheet ? 7 : 4;
            float margin = draftingSheet ? 20 : inset;
            float left = r.xMin + margin, right = r.xMax - inset;
            float bottom = r.yMin + inset, top = r.yMax - inset;
            // Large diagnostic sheets share the same bounded number of cells and ticks.
            float spacing = Mathf.Max(draftingSheet ? 26 : 32, Mathf.Max(r.width, r.height) / MaximumGridIntervals);
            Color fine = MeshPen.Alpha(NightNotebookArt.Grid, draftingSheet ? .065f : .082f);
            Color major = MeshPen.Alpha(NightNotebookArt.Grid, .14f);
            for (int column = 1; column <= MaximumGridIntervals; column++)
            {
                float x = left + column * spacing;
                if (x >= right) break;
                Fill(target, r, new Rect(x, bottom, .85f, top - bottom), column % 4 == 0 ? major : fine);
            }
            for (int row = 1; row <= MaximumGridIntervals; row++)
            {
                float y = bottom + row * spacing;
                if (y >= top) break;
                Fill(target, r, new Rect(left, y, right - left, .85f), row % 4 == 0 ? major : fine);
            }
            if (!draftingSheet) return;

            Color graphite = MeshPen.Alpha(NightNotebookArt.Pencil, .30f);
            Color brass = MeshPen.Alpha(NightNotebookArt.Accent, .48f);
            var gutter = new Rect(r.xMin, r.yMin, margin - 2, r.height);
            Fill(target, r, gutter, Color.Lerp(NightNotebookArt.Paper, Color.black, .21f));
            Fill(target, r, new Rect(left - 2, bottom, 1, top - bottom), graphite);
            Fill(target, r, new Rect(left, bottom, right - left, 1), graphite);
            for (int tick = 0; tick < MaximumGridIntervals * 2; tick++)
            {
                float x = left + tick * spacing * .5f;
                if (x > right - 1) break;
                float height = tick % 8 == 0 ? 7 : tick % 2 == 0 ? 4 : 2;
                Fill(target, r, new Rect(x, bottom, 1, height), tick % 8 == 0 ? brass : graphite);
            }
            for (int hole = 0; hole < MaximumGridIntervals / 2; hole++)
            {
                float y = top - spacing * (.5f + hole * 2);
                if (y < bottom + 3) break;
                Fill(target, r, new Rect(r.xMin + 7, y - 2.5f, 4, 4.5f), graphite);
                Fill(target, r, new Rect(r.xMin + 7, y - 1.5f, 4, 4), FieldMenuSkin.Background);
            }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static void Fill(UnityEngine.UI.VertexHelper target, Rect clip, Rect rect, Color color)
        {
            float left = Mathf.Max(clip.xMin, rect.xMin), right = Mathf.Min(clip.xMax, rect.xMax);
            float bottom = Mathf.Max(clip.yMin, rect.yMin), top = Mathf.Min(clip.yMax, rect.yMax);
            if (right <= left || top <= bottom) return;
            int first = target.currentVertCount;
            target.AddVert(new Vector2(left, bottom), color, Vector2.zero);
            target.AddVert(new Vector2(right, bottom), color, Vector2.zero);
            target.AddVert(new Vector2(right, top), color, Vector2.zero);
            target.AddVert(new Vector2(left, top), color, Vector2.zero);
            target.AddTriangle(first, first + 1, first + 2);
            target.AddTriangle(first, first + 2, first + 3);
        }
    }
}
