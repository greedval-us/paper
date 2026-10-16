using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Canvas view of the same resource primitives used in the batched world.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NotebookArtGraphic : MaskableGraphic
    {
        public override Texture mainTexture => NotebookSpriteCatalog.Texture;
        string shapeId, clipId;
        float seconds, extent = .6f;
        public string ShapeId => shapeId;
        public string ClipId => clipId;
        public float Seconds => seconds;
        public bool RepeatPreview { get; set; }
        public void Bind(string shape, string clip = null)
        {
            shapeId = shape; clipId = clip; extent = .12f;
            var theme = NotebookVisualThemeAsset.Default;
            Measure(theme?.FindShape(shape));
            var animation = theme?.FindClip(clip);
            if (animation != null)
                foreach (var frame in animation.Frames) Measure(theme.FindShape(frame.ShapeId));
            SetVerticesDirty();
        }
        void Measure(NotebookArtShape shape)
        {
            if (shape == null) return;
            if (NotebookSpriteCatalog.TryGetBounds(shape.Id, out var bounds))
                extent = Mathf.Max(extent, Mathf.Abs(bounds.xMin), Mathf.Abs(bounds.xMax), Mathf.Abs(bounds.yMin), Mathf.Abs(bounds.yMax));
            if (shape.Marks == null) return;
            foreach (var mark in shape.Marks)
            {
                foreach (var point in mark.Points) extent = Mathf.Max(extent, Mathf.Abs(point.x), Mathf.Abs(point.y));
                extent = Mathf.Max(extent, Mathf.Abs(mark.Center.x) + mark.Radius,
                    Mathf.Abs(mark.Center.y) + mark.Radius * mark.Aspect);
            }
        }
        public void SetTime(float value) { seconds = value; if (clipId != null) SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var theme = NotebookVisualThemeAsset.Default; if (theme == null) return;
            var clip = theme.FindClip(clipId);
            float phase = RepeatPreview && clip != null && clip.Duration > 0 ? Mathf.Repeat(seconds, clip.Duration) : seconds;
            var frame = clip?.Sample(phase);
            var shape = theme.FindShape(frame?.ShapeId ?? shapeId);
            float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .43f / extent;
            NotebookArtRenderer.DrawUI(vh, shape,
                rectTransform.rect.center + (frame?.Offset ?? Vector2.zero) * scale,
                scale * (frame?.Scale ?? 1), frame?.Rotation ?? 0, opacity: (frame?.Alpha ?? 1) * color.a);
        }
        public static void Attach(Button button, string partId)
        {
            if (button == null) return;
            var bounds = button.GetComponent<RectTransform>().rect;
            float size = Mathf.Min(36, bounds.height - 8);
            var go = new GameObject("Part illustration", typeof(RectTransform), typeof(CanvasRenderer), typeof(NotebookArtGraphic));
            go.transform.SetParent(button.transform, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
            rect.pivot = new Vector2(0, .5f); rect.anchoredPosition = new Vector2(6, 0); rect.sizeDelta = new Vector2(size, size);
            var graphic = go.GetComponent<NotebookArtGraphic>(); graphic.raycastTarget = false; graphic.Bind("ui." + partId);
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                var labelRect = label.rectTransform;
                labelRect.anchoredPosition = new Vector2(size + 12, labelRect.anchoredPosition.y);
                labelRect.sizeDelta = new Vector2(bounds.width - size - 18, labelRect.sizeDelta.y);
            }
        }
    }
}
