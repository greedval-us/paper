using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>A bounded catalogue viewport; content growth never moves neighbouring workshop controls.</summary>
    internal sealed class CatalogScrollList
    {
        public RectTransform Content { get; }
        public ScrollRect Scroll { get; }
        public CatalogScrollList(Transform parent, string name, Rect bounds, int count, int columns, float rowHeight, float gap)
        {
            var viewport = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(parent, false);
            var rect = viewport.GetComponent<RectTransform>(); Place(rect, bounds);
            var background = viewport.GetComponent<Image>(); background.color = new Color(1, 1, 1, .05f); background.raycastTarget = true;
            var body = new GameObject(name + " content", typeof(RectTransform)); body.transform.SetParent(rect, false);
            Content = body.GetComponent<RectTransform>();
            Content.anchorMin = new Vector2(0, 1); Content.anchorMax = new Vector2(1, 1); Content.pivot = new Vector2(0, 1);
            int rows = Mathf.CeilToInt(count / (float)Mathf.Max(1, columns));
            Content.sizeDelta = new Vector2(0, Mathf.Max(bounds.height, rows * (rowHeight + gap) - gap));
            Scroll = viewport.GetComponent<ScrollRect>(); Scroll.viewport = rect; Scroll.content = Content;
            Scroll.horizontal = false; Scroll.vertical = true; Scroll.movementType = ScrollRect.MovementType.Clamped;
            Scroll.scrollSensitivity = rowHeight; Scroll.inertia = false;
            if (Content.sizeDelta.y > bounds.height)
            {
                var track = new GameObject(name + " scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
                track.transform.SetParent(rect, false); var trackRect = track.GetComponent<RectTransform>();
                trackRect.anchorMin = new Vector2(1, 0); trackRect.anchorMax = new Vector2(1, 1); trackRect.pivot = new Vector2(1, 1);
                trackRect.anchoredPosition = Vector2.zero; trackRect.sizeDelta = new Vector2(6, 0); track.GetComponent<Image>().color = new Color(.2f, .35f, .55f, .1f);
                var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)); handle.transform.SetParent(trackRect, false);
                var handleRect = handle.GetComponent<RectTransform>(); handleRect.anchorMin = Vector2.zero; handleRect.anchorMax = Vector2.one; handleRect.sizeDelta = Vector2.zero;
                handle.GetComponent<Image>().color = new Color(.2f, .35f, .55f, .55f);
                var scrollbar = track.GetComponent<Scrollbar>(); scrollbar.handleRect = handleRect; scrollbar.targetGraphic = handle.GetComponent<Image>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
                Scroll.verticalScrollbar = scrollbar;
            }
        }
        static void Place(RectTransform rect, Rect box)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(box.x, -box.y); rect.sizeDelta = box.size; }
    }
}
