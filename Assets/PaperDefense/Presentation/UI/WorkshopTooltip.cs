using UnityEngine;
using UnityEngine.EventSystems;

namespace PaperDefense.Presentation
{
    /// <summary>Event-only trigger; a single overlay performs layout for the currently visible hint.</summary>
    public sealed class WorkshopTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, IPointerDownHandler, ICancelHandler
    {
        WorkshopUi owner;
        UnityEngine.UI.Graphic targetGraphic;
        UnityEngine.UI.Selectable selectable;
        UnityEngine.UI.Text caption;
        string hint;
        public string Hint => caption != null ? caption.text : hint;
        internal RectTransform Target { get; private set; }
        internal bool Visible => isActiveAndEnabled && Target != null &&
            (selectable == null || selectable.isActiveAndEnabled) &&
            (targetGraphic == null || targetGraphic.isActiveAndEnabled && !targetGraphic.canvasRenderer.cull);

        internal void Bind(WorkshopUi ui, string hint)
        {
            if (owner != null) owner.HideTooltip(this);
            owner = ui;
            this.hint = hint;
            caption = null;
            Target = transform as RectTransform;
            targetGraphic = GetComponent<UnityEngine.UI.Graphic>();
            selectable = GetComponent<UnityEngine.UI.Selectable>();
            if (targetGraphic != null && !string.IsNullOrWhiteSpace(hint)) targetGraphic.raycastTarget = true;
        }

        internal void FollowCaption(UnityEngine.UI.Text text) => caption = text;

        public void OnPointerEnter(PointerEventData eventData) { if (Visible) owner?.ShowTooltip(this); }
        public void OnSelect(BaseEventData eventData) { if (Visible) owner?.ShowTooltip(this); }
        public void OnPointerExit(PointerEventData eventData) => owner?.HideTooltip(this);
        public void OnDeselect(BaseEventData eventData) => owner?.HideTooltip(this);
        public void OnPointerDown(PointerEventData eventData) => owner?.HideTooltip(this);
        public void OnCancel(BaseEventData eventData) => owner?.HideTooltip(this);
        void OnDisable() => owner?.HideTooltip(this);
        void OnDestroy() => owner?.HideTooltip(this);
    }

    internal sealed class WorkshopTooltipOverlay : MonoBehaviour
    {
        const float Margin = 12, Gap = 10, PaddingX = 14, PaddingY = 11;
        readonly Vector3[] corners = new Vector3[4];
        RectTransform panel;
        UnityEngine.UI.Text label;
        public WorkshopTooltip Source { get; private set; }

        internal void Initialize(UnityEngine.UI.Text text)
        {
            panel = (RectTransform)transform;
            label = text;
        }

        internal void Show(WorkshopTooltip source)
        {
            if (!source.Visible) return;
            Source = source;
            label.text = source.Hint;
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            Position();
        }

        internal void Hide()
        {
            Source = null;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        void OnDisable() => Source = null;

        void LateUpdate()
        {
            if (Source == null || !Source.Visible) { Hide(); return; }
            if (label.text != Source.Hint) label.text = Source.Hint;
            Position();
        }

        void Position()
        {
            if (Source == null || !(transform.parent is RectTransform root)) { Hide(); return; }
            float width = root.rect.width, height = root.rect.height;
            if (width <= Margin * 2 || height <= Margin * 2) { Hide(); return; }
            Source.Target.GetWorldCorners(corners);
            float left = float.PositiveInfinity, top = float.PositiveInfinity;
            float right = float.NegativeInfinity, bottom = float.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 point = root.InverseTransformPoint(corners[i]);
                float x = point.x - root.rect.xMin, y = root.rect.yMax - point.y;
                left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                top = Mathf.Min(top, y); bottom = Mathf.Max(bottom, y);
            }
            Rect target = Rect.MinMaxRect(left, top, right, bottom);
            Rect safe = new Rect(Margin, Margin, width - Margin * 2, height - Margin * 2);
            if (!target.Overlaps(safe)) { Hide(); return; }
            float wantedWidth = Mathf.Clamp(label.preferredWidth + PaddingX * 2, Mathf.Min(180, safe.width), Mathf.Min(400, safe.width));
            Vector2 size = Measure(wantedWidth, safe.height);
            float centeredX = Mathf.Clamp(target.center.x - size.x * .5f, safe.xMin, safe.xMax - size.x);
            float centeredY = Mathf.Clamp(target.center.y - size.y * .5f, safe.yMin, safe.yMax - size.y);
            Rect placement;
            if (bottom + Gap + size.y <= safe.yMax)
                placement = new Rect(centeredX, bottom + Gap, size.x, size.y);
            else if (top - Gap - size.y >= safe.yMin)
                placement = new Rect(centeredX, top - Gap - size.y, size.x, size.y);
            else if (right + Gap + size.x <= safe.xMax)
                placement = new Rect(right + Gap, centeredY, size.x, size.y);
            else if (left - Gap - size.x >= safe.xMin)
                placement = new Rect(left - Gap - size.x, centeredY, size.x, size.y);
            else
            {
                // Very long localized hints use the largest free side and still never cover their source.
                Rect available = new Rect(safe.xMin, Mathf.Max(safe.yMin, bottom + Gap), safe.width,
                    Mathf.Max(0, safe.yMax - bottom - Gap));
                PickLarger(ref available, new Rect(safe.xMin, safe.yMin, safe.width, Mathf.Max(0, top - Gap - safe.yMin)));
                PickLarger(ref available, new Rect(Mathf.Max(safe.xMin, right + Gap), safe.yMin,
                    Mathf.Max(0, safe.xMax - right - Gap), safe.height));
                PickLarger(ref available, new Rect(safe.xMin, safe.yMin, Mathf.Max(0, left - Gap - safe.xMin), safe.height));
                if (available.width < PaddingX * 2 + 24 || available.height < PaddingY * 2 + 20) { Hide(); return; }
                size = Measure(Mathf.Min(wantedWidth, available.width), available.height);
                placement = new Rect(Mathf.Clamp(target.center.x - size.x * .5f, available.xMin, available.xMax - size.x),
                    Mathf.Clamp(target.center.y - size.y * .5f, available.yMin, available.yMax - size.y), size.x, size.y);
            }
            WorkshopUi.Place(panel, placement);
            WorkshopUi.Place(label.rectTransform, new Rect(PaddingX, PaddingY,
                Mathf.Max(0, placement.width - PaddingX * 2), Mathf.Max(0, placement.height - PaddingY * 2)));
        }

        Vector2 Measure(float width, float maximumHeight)
        {
            float contentWidth = Mathf.Max(1, width - PaddingX * 2);
            var settings = label.GetGenerationSettings(new Vector2(contentWidth, 0));
            float textHeight = label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
            return new Vector2(width, Mathf.Min(maximumHeight, Mathf.Max(22, Mathf.Ceil(textHeight)) + PaddingY * 2));
        }

        static void PickLarger(ref Rect selected, Rect candidate)
        {
            if (candidate.width * candidate.height > selected.width * selected.height) selected = candidate;
        }
    }
}
