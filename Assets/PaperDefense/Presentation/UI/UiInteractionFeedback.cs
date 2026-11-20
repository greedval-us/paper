using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Independent input feedback for field menu buttons; only their mesh changes.</summary>
    [DisallowMultipleComponent]
    public sealed class UiInteractionFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler, IMoveHandler
    {
        Selectable control;
        Graphic surface;
        PlayerCardGraphic card;
        WorkshopSurfaceGraphic workshop;
        PlayerGameRoot player;
        bool pointerOver, pointerHeld, keyboardFocus;
        float submitRemaining, lastSelection;
        float hoverTarget, pressTarget, focusTarget;

        public float HoverAmount { get; private set; }
        public float PressAmount { get; private set; }
        public float FocusAmount { get; private set; }
        // Semantic choice must be visible in the same rebuild that changes its model flag.
        public float SelectionAmount => Available && (card != null && card.Selected || workshop != null && workshop.Chosen) ? 1 : 0;
        internal bool Available { get; private set; }

        public static UiInteractionFeedback Ensure(Selectable control)
        {
            if (!(control is Button) || !Supports(control.targetGraphic)) return null;
            var feedback = control.GetComponent<UiInteractionFeedback>();
            bool newlyAttached = feedback == null;
            if (feedback == null) feedback = control.gameObject.AddComponent<UiInteractionFeedback>();
            feedback.Cache(control);
            // Modal/result builders may establish programmatic focus before applying their theme.
            // Reapplying an existing helper must preserve the pointer/keyboard distinction.
            if (newlyAttached && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == control.gameObject)
                feedback.keyboardFocus = true;
            feedback.Refresh();
            return feedback;
        }

        static bool Supports(Graphic graphic) => graphic is PlayerCardGraphic card && card.FieldStyle ||
            graphic is WorkshopSurfaceGraphic workshop && workshop.FieldStyle;

        void Cache(Selectable value)
        {
            if (value == null) value = GetComponent<Selectable>();
            Graphic next = value != null ? value.targetGraphic : null;
            if (surface != next)
            {
                Detach();
                ClearInput();
            }
            control = value;
            surface = next;
            card = next as PlayerCardGraphic;
            workshop = next as WorkshopSurfaceGraphic;
            player = value != null ? value.GetComponentInParent<PlayerGameRoot>(true) : null;
            if (!(value is Button) || !Supports(next)) return;
            if (card != null) card.InteractionFeedback = this;
            if (workshop != null) workshop.InteractionFeedback = this;
            // A plain Button otherwise tints the finished mesh a second time. This is scoped to
            // supported field surfaces, and leaves semantic Graphic.color and every callback intact.
            value.transition = Selectable.Transition.None;
            next.CrossFadeColor(Color.white, 0, true, true);
        }

        void OnEnable()
        {
            Cache(control);
            ClearInput();
            Refresh(true);
        }

        void OnDisable()
        {
            ClearInput();
            Available = false;
            hoverTarget = pressTarget = focusTarget = 0;
            SetAmounts(0, 0, 0);
            lastSelection = 0;
            Dirty();
        }

        void OnDestroy() => Detach();

        void Detach()
        {
            if (card != null && card.InteractionFeedback == this) card.InteractionFeedback = null;
            if (workshop != null && workshop.InteractionFeedback == this) workshop.InteractionFeedback = null;
        }

        bool CanInteract() => isActiveAndEnabled && control is Button && control.IsActive() &&
            control.IsInteractable() && surface != null && surface.isActiveAndEnabled && Supports(surface);

        public void Refresh(bool instant = false)
        {
            bool available = CanInteract();
            bool availabilityChanged = available != Available;
            Available = available;
            if (!available)
            {
                ClearInput();
                hoverTarget = pressTarget = focusTarget = 0;
                SetAmounts(0, 0, 0);
            }
            else
            {
                hoverTarget = pointerOver ? 1 : 0;
                pressTarget = pointerHeld && pointerOver || submitRemaining > 0 ? 1 : 0;
                focusTarget = keyboardFocus ? 1 : 0;
                if (instant || player != null && player.ReducedMotion)
                    SetAmounts(hoverTarget, pressTarget, focusTarget);
            }
            float selection = SelectionAmount;
            if (availabilityChanged || selection != lastSelection) Dirty();
            lastSelection = selection;
        }

        void Update()
        {
            float delta = Time.unscaledDeltaTime;
            submitRemaining = Mathf.Max(0, submitRemaining - delta);
            Refresh();
            if (!Available || player != null && player.ReducedMotion) return;
            SetAmounts(Step(HoverAmount, hoverTarget, delta, .10f, .14f),
                Step(PressAmount, pressTarget, delta, .05f, .10f),
                Step(FocusAmount, focusTarget, delta, .10f, .14f));
        }

        static float Step(float value, float target, float delta, float enter, float exit)
            => Mathf.MoveTowards(value, target, delta / (target > value ? enter : exit));

        void SetAmounts(float hover, float press, float focus)
        {
            if (HoverAmount == hover && PressAmount == press && FocusAmount == focus) return;
            HoverAmount = hover;
            PressAmount = press;
            FocusAmount = focus;
            Dirty();
        }

        void ClearInput() { pointerOver = pointerHeld = keyboardFocus = false; submitRemaining = 0; }
        void Dirty() { if (surface != null) surface.SetVerticesDirty(); }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanInteract()) return;
            pointerOver = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData) { pointerOver = false; Refresh(); }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanInteract() || eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            pointerOver = pointerHeld = true;
            keyboardFocus = false;
            Refresh();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            pointerHeld = false;
            Refresh();
        }

        public void OnSelect(BaseEventData eventData)
        {
            keyboardFocus = CanInteract() && !(eventData is PointerEventData);
            Refresh();
        }

        public void OnDeselect(BaseEventData eventData) { keyboardFocus = false; Refresh(); }

        public void OnMove(AxisEventData eventData)
        {
            if (!CanInteract() || EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject) return;
            keyboardFocus = true;
            Refresh();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!CanInteract()) return;
            keyboardFocus = true;
            submitRemaining = .10f;
            Refresh();
        }
    }
}
