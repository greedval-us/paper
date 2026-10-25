using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Canvas view of the same resource primitives used in the batched world.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NotebookArtGraphic : NotebookAtlasGraphic
    {
        string shapeId, clipId;
        float seconds;
        Rect drawingBounds;
        bool hasBounds;
        AssemblyVisualComposition weaponPreview;
        MeshPen previewPen;
        public string ShapeId => shapeId;
        public string ClipId => clipId;
        public float Seconds => seconds;
        public bool RepeatPreview { get; set; }
        public void Bind(string shape, string clip = null)
        {
            shapeId = shape; clipId = clip; hasBounds = false; drawingBounds = default;
            weaponPreview = null;
            string family = null;
            if (clip == null)
                switch (shape)
                {
                    case "ui.weapon.gun": family = "gun"; break;
                    case "ui.weapon.piercer": family = "piercer"; break;
                    case "ui.weapon.mortar": family = "mortar"; break;
                    case "ui.weapon.fan": family = "fan"; break;
                    case "ui.weapon.ricochet": family = "ricochet"; break;
                    case "ui.weapon.stapler": family = "stapler"; break;
                    case "ui.weapon.blotter": family = "blotter"; break;
                }
            if (family != null)
            {
                var recipe = new WeaponRecipe(new PartRef("core." + family, 1), new PartRef("head." + family, 1),
                    new PartRef("sight.iron", 1), new PartRef("magazine.standard", 1), new PartRef("trigger.standard", 1));
                weaponPreview = AssemblyVisualComposer.ComposeWeapon(recipe, AssemblyPreviewPose.ForWeapon());
                drawingBounds = AssemblyPreviewPose.VisibleBounds(weaponPreview);
                hasBounds = drawingBounds.width > 0 && drawingBounds.height > 0;
                SetVerticesDirty();
                return;
            }
            var theme = NotebookVisualThemeAsset.Default;
            Measure(theme?.FindShape(shape), Vector2.zero, 0, 1);
            var animation = theme?.FindClip(clip);
            if (animation != null)
                foreach (var frame in animation.Frames)
                    Measure(theme.FindShape(frame.ShapeId), frame.Offset, frame.Rotation, frame.Scale);
            SetVerticesDirty();
        }
        void Measure(NotebookArtShape shape, Vector2 offset, float radians, float scale)
        {
            if (shape == null) return;
            if (NotebookSpriteCatalog.TryGetBounds(shape.Id, out var bounds))
            {
                Include(bounds.min); Include(new Vector2(bounds.xMin, bounds.yMax));
                Include(bounds.max); Include(new Vector2(bounds.xMax, bounds.yMin));
                return;
            }
            if (shape.Marks == null) return;
            foreach (var mark in shape.Marks)
            {
                foreach (var point in mark.Points)
                { Include(point - Vector2.one * mark.Width * .5f); Include(point + Vector2.one * mark.Width * .5f); }
                if (mark.Primitive == NotebookArtPrimitive.Circle || mark.Primitive == NotebookArtPrimitive.Ring)
                {
                    var radius = new Vector2(mark.Radius, mark.Radius * mark.Aspect) + Vector2.one * mark.Width * .5f;
                    Include(mark.Center - radius); Include(mark.Center + radius);
                    Include(mark.Center + new Vector2(radius.x, -radius.y)); Include(mark.Center + new Vector2(-radius.x, radius.y));
                }
            }
            void Include(Vector2 local)
            {
                float cosine = Mathf.Cos(radians), sine = Mathf.Sin(radians);
                Vector2 point = offset + new Vector2(local.x * cosine - local.y * sine, local.x * sine + local.y * cosine) * scale;
                drawingBounds = hasBounds ? Rect.MinMaxRect(Mathf.Min(drawingBounds.xMin, point.x), Mathf.Min(drawingBounds.yMin, point.y),
                    Mathf.Max(drawingBounds.xMax, point.x), Mathf.Max(drawingBounds.yMax, point.y)) : new Rect(point, Vector2.zero);
                hasBounds = true;
            }
        }
        public void SetTime(float value) { seconds = value; if (clipId != null) SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var theme = NotebookVisualThemeAsset.Default; if (theme == null || !hasBounds) return;
            float scale = .92f * Mathf.Min(rectTransform.rect.width / Mathf.Max(.001f, drawingBounds.width),
                rectTransform.rect.height / Mathf.Max(.001f, drawingBounds.height));
            if (weaponPreview != null)
            {
                if (previewPen == null) previewPen = new MeshPen(null, 32);
                previewPen.Clear();
                weaponPreview.Draw(previewPen, Vector2.zero, color.a);
                previewPen.CopyTo(vh, rectTransform.rect.center - drawingBounds.center * scale, scale);
                return;
            }
            var clip = theme.FindClip(clipId);
            float phase = RepeatPreview && clip != null && clip.Duration > 0 ? Mathf.Repeat(seconds, clip.Duration) : seconds;
            var frame = clip?.Sample(phase);
            var shape = theme.FindShape(frame?.ShapeId ?? shapeId);
            NotebookArtRenderer.DrawUI(vh, shape,
                rectTransform.rect.center - drawingBounds.center * scale + (frame?.Offset ?? Vector2.zero) * scale,
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
