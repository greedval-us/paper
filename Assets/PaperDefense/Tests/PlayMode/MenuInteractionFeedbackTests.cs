using System;
using System.Collections;
using NUnit.Framework;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    /// <summary>Menu state changes must remain visible across the body of a control.</summary>
    public sealed class MenuInteractionFeedbackTests
    {
        GameObject root, ownedEventSystem;
        RectTransform scope;
        EventSystem events, previousEvents;
        bool previousEventSystemEnabled;
        float previousTimeScale;
        WorkshopUi workshopUi;

        [SetUp]
        public void SetUp()
        {
            previousTimeScale = Time.timeScale;
            previousEvents = EventSystem.current;
            if (previousEvents != null)
            {
                previousEventSystemEnabled = previousEvents.enabled;
                previousEvents.enabled = false;
            }
            ownedEventSystem = new GameObject("Isolated menu feedback events", typeof(EventSystem));
            events = ownedEventSystem.GetComponent<EventSystem>();
            root = new GameObject("Isolated menu feedback", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            scope = root.GetComponent<RectTransform>(); scope.sizeDelta = new Vector2(1600, 1000);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = previousTimeScale;
            if (events != null) events.SetSelectedGameObject(null);
            if (root != null) Object.DestroyImmediate(root);
            if (ownedEventSystem != null) Object.DestroyImmediate(ownedEventSystem);
            if (previousEvents != null) previousEvents.enabled = previousEventSystemEnabled;
        }

        [Test]
        public void HoverChangesTheFillOfANonPrimaryControl()
        {
            Color normal = ControlFill(selected: false, hovered: false);
            Color hovered = ControlFill(selected: false, hovered: true);

            Assert.That(ColorDifference(normal, hovered), Is.GreaterThan(.025f),
                "Hover must visibly change the control body, not just a narrow outline.");
        }

        [Test]
        public void PersistentSelectionHasADifferentFillFromNormalAndHover()
        {
            Color normal = ControlFill(selected: false, hovered: false);
            Color hovered = ControlFill(selected: false, hovered: true);
            Color selected = ControlFill(selected: true, hovered: false);

            Assert.That(ColorDifference(selected, normal), Is.GreaterThan(.025f),
                "A chosen card must remain identifiable after the pointer leaves.");
            Assert.That(ColorDifference(selected, hovered), Is.GreaterThan(.025f),
                "Pointer hover and persistent selection must have different body colors.");
        }

        [TestCase(false, false)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(true, true)]
        public void WarningAndSecondaryTextStayReadableAcrossChoiceAndHover(bool selected, bool hovered)
        {
            using (var vertices = new VertexHelper())
            {
                FieldMenuSkin.Draw(vertices, new Rect(0, 0, 240, 64), Color.white,
                    control: true, primary: false, selected: selected, focused: false,
                    disabled: false, hovered: hovered, pressed: false);
                // The first nine vertices are the plate gradient; test its brightest end,
                // where pale warning/secondary ink has the least available contrast.
                var brightest = Color.black;
                for (int i = 0; i < 9; i++)
                {
                    var vertex = new UIVertex(); vertices.PopulateUIVertex(ref vertex, i);
                    if (Luminance(vertex.color) > Luminance(brightest))
                        brightest = vertex.color;
                }
                Assert.That(Contrast(FieldMenuSkin.Muted, brightest), Is.GreaterThanOrEqualTo(4.5f));
                var warning = FieldMenuSkin.TextColor(NotebookPalette.Red);
                Assert.That(Contrast(warning, brightest), Is.GreaterThanOrEqualTo(4.5f),
                    "Hovering an unaffordable or invalid choice must keep its warning readable.");
            }
        }

        [TestCase(false, false)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(true, true)]
        public void PrimaryCaptionStaysReadableThroughHoverAndPress(bool hovered, bool pressed)
        {
            using (var vertices = new VertexHelper())
            {
                FieldMenuSkin.Draw(vertices, new Rect(0, 0, 240, 64), PlayerUi.Blue,
                    control: true, primary: true, selected: false, focused: false,
                    disabled: false, hovered: hovered, pressed: pressed);
                for (int i = 0; i < 9; i++)
                {
                    var vertex = new UIVertex(); vertices.PopulateUIVertex(ref vertex, i);
                    Assert.That(Contrast(FieldMenuSkin.AccentInk, vertex.color), Is.GreaterThanOrEqualTo(4.5f),
                        "The primary caption must stay legible while its brass plate is pressed.");
                }
            }
        }

        [UnityTest]
        public IEnumerator PointerFeedbackUsesRealtimeAndKeepsMouseSelectionSeparateFromKeyboardFocus()
        {
            Time.timeScale = 0;
            int clicks = 0;
            var controls = CreateControls(() => clicks++);
            ThemeControls();
            for (int index = 0; index < controls.Length; index++)
            {
                var button = controls[index];
                var feedback = Feedback(button);
                var contract = new ControlContract(button);
                var pointer = Pointer();

                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(feedback.HoverAmount, Is.GreaterThan(.9f), button.name + " hover while paused");
                Assert.That(feedback.FocusAmount, Is.LessThan(.05f), button.name + " pointer entry is not keyboard focus");

                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(feedback.PressAmount, Is.GreaterThan(.8f), button.name + " pointer press");
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(clicks, Is.EqualTo(index + 1), button.name + " one callback per click");
                Assert.That(feedback.PressAmount, Is.LessThan(.05f), button.name + " release");
                Assert.That(feedback.HoverAmount, Is.GreaterThan(.9f), button.name + " remains hovered after click");
                Assert.That(feedback.FocusAmount, Is.LessThan(.05f), button.name + " mouse selection is not keyboard focus");
                Assert.That(events.currentSelectedGameObject, Is.SameAs(button.gameObject));

                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(feedback.HoverAmount, Is.LessThan(.05f), button.name + " pointer exit clears hover after click");
                Assert.That(feedback.FocusAmount, Is.LessThan(.05f));
                Assert.That(events.currentSelectedGameObject, Is.SameAs(button.gameObject),
                    "Visual pointer exit must preserve the EventSystem selection.");
                contract.AssertUnchanged();
                events.SetSelectedGameObject(null);
            }
        }

        [UnityTest]
        public IEnumerator ThemeAttachmentAdoptsExistingKeyboardFocusWithoutConvertingPointerSelection()
        {
            Time.timeScale = 0;
            var fixtureScope = scope;
            for (int family = 0; family < 3; family++)
            {
                var menu = new GameObject("Late themed menu " + family, typeof(RectTransform));
                menu.transform.SetParent(fixtureScope, false);
                scope = menu.GetComponent<RectTransform>(); scope.sizeDelta = new Vector2(1600, 1000);
                var controls = CreateControls();
                var button = controls[family];
                events.SetSelectedGameObject(button.gameObject, new BaseEventData(events));

                ThemeControls();
                var feedback = Feedback(button);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(feedback.FocusAmount, Is.GreaterThan(.9f),
                    button.name + " must keep keyboard focus established before the theme attached.");

                ExecuteEvents.Execute(button.gameObject, Pointer(), ExecuteEvents.pointerDownHandler);
                ThemeControls();
                scope.GetComponent<FieldMenuTheme>().Refresh();
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(feedback.FocusAmount, Is.LessThan(.05f),
                    button.name + " theme refresh must preserve the switch to pointer selection.");
                Assert.That(feedback.PressAmount, Is.GreaterThan(.8f));
                Assert.That(events.currentSelectedGameObject, Is.SameAs(button.gameObject));
                events.SetSelectedGameObject(null);
            }
            scope = fixtureScope;
        }

        [UnityTest]
        public IEnumerator KeyboardSubmitFlashesTheControlAndKeepsItsFocus()
        {
            Time.timeScale = 0;
            int clicks = 0;
            var controls = CreateControls(() => clicks++);
            ThemeControls();
            for (int index = 0; index < controls.Length; index++)
            {
                var button = controls[index];
                var feedback = Feedback(button);
                events.SetSelectedGameObject(button.gameObject, new BaseEventData(events));
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(feedback.FocusAmount, Is.GreaterThan(.9f), button.name + " keyboard focus");
                Assert.That(feedback.HoverAmount, Is.LessThan(.05f));

                ExecuteEvents.Execute(button.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
                feedback.Refresh(true);
                Assert.That(clicks, Is.EqualTo(index + 1), button.name + " submit invokes the existing callback once");
                Assert.That(feedback.PressAmount, Is.GreaterThan(.8f), button.name + " submit has a visible press pulse");
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(feedback.PressAmount, Is.LessThan(.05f), button.name + " submit pulse releases while paused");
                Assert.That(feedback.FocusAmount, Is.GreaterThan(.9f), button.name + " submit keeps keyboard focus");
                Assert.That(events.currentSelectedGameObject, Is.SameAs(button.gameObject));
                events.SetSelectedGameObject(null);
            }
        }

        [UnityTest]
        public IEnumerator DisabledAndReenabledControlsDiscardStalePointerState()
        {
            Time.timeScale = 0;
            int clicks = 0;
            var controls = CreateControls(() => clicks++);
            ThemeControls();
            foreach (var button in controls)
            {
                var feedback = Feedback(button);
                var pointer = Pointer();
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(feedback.HoverAmount, Is.GreaterThan(.9f));
                Assert.That(feedback.PressAmount, Is.GreaterThan(.8f));

                button.interactable = false;
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                yield return new WaitForSecondsRealtime(.2f);
                AssertIdlePointer(feedback, button.name + " disabled");
                Assert.That(clicks, Is.Zero, "Disabled controls must not invoke their original callbacks.");

                button.interactable = true;
                yield return new WaitForSecondsRealtime(.2f);
                AssertIdlePointer(feedback, button.name + " reenabled without a fresh pointer event");
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(feedback.HoverAmount, Is.GreaterThan(.9f), "A new pointer entry must still work.");

                button.gameObject.SetActive(false);
                button.gameObject.SetActive(true);
                yield return new WaitForSecondsRealtime(.2f);
                AssertIdlePointer(feedback, button.name + " reactivated without a fresh pointer event");
                events.SetSelectedGameObject(null);
            }
        }

        [Test]
        public void ThemeRefreshReusesOneFeedbackComponentAndPreservesInputAndGeometry()
        {
            int clicks = 0;
            var controls = CreateControls(() => clicks++);
            var contracts = new ControlContract[controls.Length];
            for (int index = 0; index < controls.Length; index++) contracts[index] = new ControlContract(controls[index]);
            ThemeControls();
            var helpers = new UiInteractionFeedback[controls.Length];
            for (int index = 0; index < controls.Length; index++) helpers[index] = Feedback(controls[index]);

            FieldMenuTheme.ApplyTo(scope, false);
            scope.GetComponent<FieldMenuTheme>().Refresh();
            scope.GetComponent<FieldMenuTheme>().Refresh();

            for (int index = 0; index < controls.Length; index++)
            {
                var button = controls[index];
                Assert.That(UiInteractionFeedback.Ensure(button), Is.SameAs(helpers[index]), button.name);
                Assert.That(button.GetComponents<UiInteractionFeedback>().Length, Is.EqualTo(1), button.name);
                contracts[index].AssertUnchanged();
                ExecuteEvents.Execute(button.gameObject, Pointer(), ExecuteEvents.pointerClickHandler);
                Assert.That(clicks, Is.EqualTo(index + 1), "Refresh must preserve each existing callback exactly once.");
            }
        }

        [Test]
        public void LogicalChoiceRemainsIndependentOfHoverAndKeyboardFocus()
        {
            var controls = CreateControls();
            ThemeControls();
            foreach (var button in controls)
            {
                var feedback = Feedback(button);
                SetChoice(button, true);
                feedback.Refresh(true);
                Assert.That(feedback.SelectionAmount, Is.GreaterThan(.9f), button.name + " persistent choice");
                Assert.That(feedback.FocusAmount, Is.LessThan(.05f));
                Assert.That(feedback.HoverAmount, Is.LessThan(.05f));

                ExecuteEvents.Execute(button.gameObject, Pointer(), ExecuteEvents.pointerEnterHandler);
                feedback.Refresh(true);
                Assert.That(feedback.HoverAmount, Is.GreaterThan(.9f));
                Assert.That(feedback.SelectionAmount, Is.GreaterThan(.9f), "Hover must retain the logical choice.");
                ExecuteEvents.Execute(button.gameObject, Pointer(), ExecuteEvents.pointerExitHandler);
                events.SetSelectedGameObject(button.gameObject, new BaseEventData(events));
                feedback.Refresh(true);
                Assert.That(feedback.HoverAmount, Is.LessThan(.05f));
                Assert.That(feedback.FocusAmount, Is.GreaterThan(.9f));
                Assert.That(feedback.SelectionAmount, Is.GreaterThan(.9f), "Keyboard focus must retain the logical choice.");

                SetChoice(button, false);
                feedback.Refresh(true);
                Assert.That(feedback.SelectionAmount, Is.LessThan(.05f));
                Assert.That(feedback.FocusAmount, Is.GreaterThan(.9f), "Clearing choice must preserve keyboard focus.");
                events.SetSelectedGameObject(null);
            }
        }

        Button[] CreateControls(Action clicked = null)
        {
            var player = new PlayerUi().Button(scope, "Player menu action", new Rect(50, 60, 240, 64),
                PlayerIcon.Next, "Continue", clicked);
            workshopUi = new WorkshopUi();
            var workshop = workshopUi.Button(scope, "Workshop menu action", new Rect(50, 160, 240, 64),
                "Continue", clicked);
            var card = PlayerUi.Graphic<PlayerCardGraphic>(scope, "Album card action", new Rect(50, 260, 240, 100));
            card.Control = true; card.raycastTarget = true; card.color = Color.white;
            var album = card.gameObject.AddComponent<Button>(); album.targetGraphic = card;
            album.onClick.AddListener(() => clicked?.Invoke());
            var controls = new[] { player, workshop, album };
            for (int index = 0; index < controls.Length; index++)
                controls[index].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnDown = controls[(index + 1) % controls.Length],
                    selectOnUp = controls[(index + controls.Length - 1) % controls.Length] };
            return controls;
        }

        void ThemeControls() => FieldMenuTheme.ApplyTo(scope, false);
        PointerEventData Pointer() => new PointerEventData(events) { button = PointerEventData.InputButton.Left };

        static UiInteractionFeedback Feedback(Button button)
        {
            var feedback = button.GetComponent<UiInteractionFeedback>();
            Assert.That(feedback, Is.Not.Null, button.name + " must receive feedback through the menu theme.");
            return feedback;
        }

        void SetChoice(Button button, bool selected)
        {
            if (button.targetGraphic is PlayerCardGraphic card) { card.Selected = selected; card.SetVerticesDirty(); }
            else workshopUi.SetSelected(button, selected);
        }

        static void AssertIdlePointer(UiInteractionFeedback feedback, string message)
        {
            Assert.That(feedback.HoverAmount, Is.LessThan(.05f), message + " hover");
            Assert.That(feedback.PressAmount, Is.LessThan(.05f), message + " press");
        }

        sealed class ControlContract
        {
            readonly Button button;
            readonly RectTransform rect;
            readonly Transform parent;
            readonly Vector2 anchorMin, anchorMax, pivot, size, offsetMin, offsetMax;
            readonly Vector3 position, scale;
            readonly Quaternion rotation;
            readonly Navigation navigation;
            readonly Graphic target;
            readonly bool raycast;
            readonly Vector4 padding;
            readonly Button.ButtonClickedEvent callback;

            public ControlContract(Button button)
            {
                this.button = button; rect = button.GetComponent<RectTransform>(); parent = rect.parent;
                anchorMin = rect.anchorMin; anchorMax = rect.anchorMax; pivot = rect.pivot;
                size = rect.sizeDelta; position = rect.anchoredPosition3D;
                offsetMin = rect.offsetMin; offsetMax = rect.offsetMax;
                scale = rect.localScale; rotation = rect.localRotation;
                navigation = button.navigation; target = button.targetGraphic;
                raycast = target.raycastTarget; padding = target.raycastPadding; callback = button.onClick;
            }

            public void AssertUnchanged()
            {
                Assert.That(rect.parent, Is.SameAs(parent), button.name + " parent");
                Assert.That(rect.anchorMin, Is.EqualTo(anchorMin), button.name + " minimum anchor");
                Assert.That(rect.anchorMax, Is.EqualTo(anchorMax), button.name + " maximum anchor");
                Assert.That(rect.pivot, Is.EqualTo(pivot), button.name + " pivot");
                Assert.That(rect.sizeDelta, Is.EqualTo(size), button.name + " size");
                Assert.That(rect.anchoredPosition3D, Is.EqualTo(position), button.name + " position");
                Assert.That(rect.offsetMin, Is.EqualTo(offsetMin), button.name + " minimum offset");
                Assert.That(rect.offsetMax, Is.EqualTo(offsetMax), button.name + " maximum offset");
                Assert.That(rect.localScale, Is.EqualTo(scale), button.name + " scale");
                Assert.That(rect.localRotation, Is.EqualTo(rotation), button.name + " rotation");
                Assert.That(button.navigation, Is.EqualTo(navigation), button.name + " navigation");
                Assert.That(button.targetGraphic, Is.SameAs(target), button.name + " target graphic");
                Assert.That(target.raycastTarget, Is.EqualTo(raycast), button.name + " hit target");
                Assert.That(target.raycastPadding, Is.EqualTo(padding), button.name + " hit padding");
                Assert.That(button.onClick, Is.SameAs(callback), button.name + " callback");
            }
        }

        static Color ControlFill(bool selected, bool hovered)
        {
            using (var vertices = new VertexHelper())
            {
                FieldMenuSkin.Draw(vertices, new Rect(0, 0, 240, 64), Color.white,
                    control: true, primary: false, selected: selected, focused: false,
                    disabled: false, hovered: hovered, pressed: false);
                Assert.That(vertices.currentVertCount, Is.GreaterThan(0), "Sample the actual generated surface.");
                var center = new UIVertex();
                vertices.PopulateUIVertex(ref center, 0);
                Assert.That((Vector2)center.position, Is.EqualTo(new Vector2(120, 32)),
                    "Read the fill center so a decorative border cannot satisfy this regression.");
                return center.color;
            }
        }

        static float ColorDifference(Color left, Color right) => Mathf.Max(
            Mathf.Abs(left.r - right.r), Mathf.Abs(left.g - right.g), Mathf.Abs(left.b - right.b));

        static float Contrast(Color first, Color second)
        {
            float a = Luminance(first), b = Luminance(second);
            return (Mathf.Max(a, b) + .05f) / (Mathf.Min(a, b) + .05f);
        }

        static float Luminance(Color value)
        {
            float Linear(float channel) => channel <= .04045f ? channel / 12.92f : Mathf.Pow((channel + .055f) / 1.055f, 2.4f);
            return .2126f * Linear(value.r) + .7152f * Linear(value.g) + .0722f * Linear(value.b);
        }
    }
}
