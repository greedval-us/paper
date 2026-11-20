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
    public sealed class UiMenuMotionTests
    {
        GameObject root, ownedEvents;
        RectTransform canvas;
        EventSystem events, previousEvents;
        bool previousEventsEnabled;
        float previousTimeScale;

        [SetUp] public void SetUp()
        {
            previousTimeScale = Time.timeScale;
            previousEvents = EventSystem.current;
            if (previousEvents != null)
            {
                previousEventsEnabled = previousEvents.enabled;
                previousEvents.enabled = false;
            }
            ownedEvents = new GameObject("Isolated menu motion events", typeof(EventSystem));
            events = ownedEvents.GetComponent<EventSystem>();
            root = new GameObject("Isolated menu motion", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            canvas = root.GetComponent<RectTransform>(); canvas.sizeDelta = new Vector2(1600, 1000);
        }

        [TearDown] public void TearDown()
        {
            Time.timeScale = previousTimeScale;
            if (events != null) events.SetSelectedGameObject(null);
            if (root != null) Object.DestroyImmediate(root);
            if (ownedEvents != null) Object.DestroyImmediate(ownedEvents);
            if (previousEvents != null) previousEvents.enabled = previousEventsEnabled;
        }

        [UnityTest] public IEnumerator PageAndWindowReachOpaqueEndpointsMonotonicallyWhilePaused()
        {
            Time.timeScale = 0;
            var page = UiMenuMotion.PlayScreen(Panel(canvas, "Incoming page"));
            var window = UiMenuMotion.PlayWindow(Panel(canvas, "Incoming window").gameObject);
            Assert.That(page.IsAnimating, Is.True); Assert.That(window.IsAnimating, Is.True);
            Assert.That(page.Opacity, Is.GreaterThan(0).And.LessThan(1));
            Assert.That(window.Opacity, Is.GreaterThan(0).And.LessThan(page.Opacity));
            float pageAlpha = page.Opacity, windowAlpha = window.Opacity;
            double deadline = Time.realtimeSinceStartupAsDouble + 1;
            while ((page.IsAnimating || window.IsAnimating) && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
                Assert.That(page.Opacity, Is.GreaterThanOrEqualTo(pageAlpha).And.LessThanOrEqualTo(1));
                Assert.That(window.Opacity, Is.GreaterThanOrEqualTo(windowAlpha).And.LessThanOrEqualTo(1));
                pageAlpha = page.Opacity; windowAlpha = window.Opacity;
            }
            Assert.That(page.IsAnimating, Is.False); Assert.That(window.IsAnimating, Is.False);
            Assert.That(page.Opacity, Is.EqualTo(1)); Assert.That(window.Opacity, Is.EqualTo(1));
        }

        [Test] public void EntranceKeepsPointerSubmitFocusAndControlGeometrySynchronous()
        {
            int clicks = 0;
            var page = Panel(canvas, "Input page");
            var button = new PlayerUi().Button(page, "Immediate menu action", new Rect(30, 50, 240, 64),
                PlayerIcon.Next, "Continue", () => clicks++);
            var rect = button.GetComponent<RectTransform>();
            rect.localRotation = Quaternion.Euler(0, 0, 3);
            rect.localScale = new Vector3(.9f, 1.1f, 1);
            var geometry = new RectContract(rect);
            var callback = button.onClick; var navigation = button.navigation;
            var graphic = button.targetGraphic; var raycast = graphic.raycastTarget;
            events.SetSelectedGameObject(button.gameObject, new BaseEventData(events));

            var motion = UiMenuMotion.PlayScreen(page);
            var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);

            Assert.That(motion.IsAnimating, Is.True);
            Assert.That(clicks, Is.EqualTo(2), "Both native event handlers invoke the existing action before the entrance settles.");
            Assert.That(events.currentSelectedGameObject, Is.SameAs(button.gameObject));
            Assert.That(button.IsInteractable(), Is.True);
            Assert.That(button.onClick, Is.SameAs(callback)); Assert.That(button.navigation, Is.EqualTo(navigation));
            Assert.That(button.targetGraphic, Is.SameAs(graphic)); Assert.That(graphic.raycastTarget, Is.EqualTo(raycast));
            Assert.That(motion.OpacityGroup.interactable, Is.True); Assert.That(motion.OpacityGroup.blocksRaycasts, Is.True);
            geometry.AssertUnchanged();
            motion.Finish(); geometry.AssertUnchanged();
        }

        [UnityTest] public IEnumerator ModalPresentationGroupRemainsAuthoritativeDuringInnerPaperEntrance()
        {
            var modal = Panel(canvas, "Pause presentation");
            var semantic = modal.gameObject.AddComponent<CanvasGroup>();
            semantic.alpha = .37f; semantic.interactable = false; semantic.blocksRaycasts = false;
            semantic.ignoreParentGroups = true;
            var decoration = Panel(modal, "Existing paper decoration");
            var decorationGroup = decoration.gameObject.AddComponent<CanvasGroup>(); decorationGroup.alpha = .61f;
            var paper = Panel(modal, "Pause paper");
            var geometry = new RectContract(paper);

            var motion = UiMenuMotion.PlayWindow(modal.gameObject);
            Assert.That(motion, Is.Not.Null); Assert.That(motion.IsAnimating, Is.True);
            Assert.That(motion.OpacityGroup.gameObject, Is.SameAs(paper.gameObject));
            Assert.That(modal.GetComponents<CanvasGroup>().Length, Is.EqualTo(1));
            yield return null;
            Assert.That(semantic.alpha, Is.EqualTo(.37f)); Assert.That(semantic.interactable, Is.False);
            Assert.That(semantic.blocksRaycasts, Is.False); Assert.That(semantic.ignoreParentGroups, Is.True);
            Assert.That(decorationGroup.alpha, Is.EqualTo(.61f));
            motion.Finish();
            Assert.That(motion.Opacity, Is.EqualTo(1)); Assert.That(semantic.alpha, Is.EqualTo(.37f));
            geometry.AssertUnchanged();
        }

        [Test] public void AnExistingPageGroupIsPreservedWhenChoosingAnInnerVisualTarget()
        {
            var page = Panel(canvas, "Semantically hidden page");
            var semantic = page.gameObject.AddComponent<CanvasGroup>(); semantic.alpha = .42f;
            semantic.interactable = false; semantic.blocksRaycasts = false;
            var paper = Panel(page, "Page sheet");
            var motion = UiMenuMotion.PlayScreen(page);
            Assert.That(motion, Is.Not.Null);
            Assert.That(motion.OpacityGroup.gameObject, Is.SameAs(paper.gameObject));
            motion.Finish();
            Assert.That(semantic.alpha, Is.EqualTo(.42f));
            Assert.That(semantic.interactable, Is.False); Assert.That(semantic.blocksRaycasts, Is.False);
        }

        [TestCase(.42f, false, .42f)]
        [TestCase(0f, false, 0f)]
        [TestCase(.42f, true, .252f)]
        [TestCase(0f, true, 0f)]
        public void BackdropCannotBypassSemanticPageOpacityInsideOrOutsideTheOwnedVisual(
            float semanticAlpha, bool insidePaper, float inheritedAlpha)
        {
            var page = Panel(canvas, "Semantically faded map page");
            var semantic = page.gameObject.AddComponent<CanvasGroup>(); semantic.alpha = semanticAlpha;
            semantic.interactable = false; semantic.blocksRaycasts = false;
            var paper = Panel(page, "Page sheet");
            var map = Backdrop(insidePaper ? paper : page);

            var motion = UiMenuMotion.PlayScreen(page);
            Assert.That(motion.IsAnimating, Is.True);
            Assert.That(motion.OpacityGroup.gameObject, Is.SameAs(paper.gameObject));
            Canvas.ForceUpdateCanvases();
            Assert.That(map.canvasRenderer.GetInheritedAlpha(), Is.EqualTo(inheritedAlpha).Within(.001f),
                "The map must inherit semantic visibility, including a fully hidden page.");
            var exclusion = map.GetComponent<CanvasGroup>();
            Assert.That(exclusion == null || !exclusion.ignoreParentGroups, Is.True);
            Assert.That(semantic.alpha, Is.EqualTo(semanticAlpha));
            Assert.That(semantic.interactable, Is.False); Assert.That(semantic.blocksRaycasts, Is.False);

            motion.Finish(); Canvas.ForceUpdateCanvases();
            Assert.That(map.canvasRenderer.GetInheritedAlpha(), Is.EqualTo(semanticAlpha).Within(.001f));
        }

        [Test] public void AVisualWithNoSafeOpacityTargetLeavesItsExistingGroupUntouched()
        {
            var page = Panel(canvas, "Existing semantic group only");
            var semantic = page.gameObject.AddComponent<CanvasGroup>(); semantic.alpha = .46f;
            semantic.interactable = false;
            var motion = UiMenuMotion.PlayScreen(page);
            Assert.That(motion == null || !motion.IsAnimating, Is.True);
            Assert.That(semantic.alpha, Is.EqualTo(.46f)); Assert.That(semantic.interactable, Is.False);
            Assert.That(page.GetComponents<CanvasGroup>().Length, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator OnlyTheNonInteractiveMapRemainsOpaqueAndRefreshAdoptsARebuiltMap()
        {
            var page = Panel(canvas, "Map entrance page");
            var map = Backdrop(page);
            var motion = UiMenuMotion.PlayScreen(page);
            var exclusion = map.GetComponent<CanvasGroup>();
            Assert.That(exclusion, Is.Not.Null); Assert.That(exclusion.ignoreParentGroups, Is.True);
            Canvas.ForceUpdateCanvases();
            Assert.That(map.canvasRenderer.GetInheritedAlpha(), Is.EqualTo(1).Within(.001f));

            map.gameObject.SetActive(false);
            var replacement = Backdrop(page);
            motion.RefreshBackdrop();
            Assert.That(exclusion.ignoreParentGroups, Is.False, "The retired map must return to normal inheritance.");
            var replacementGroup = replacement.GetComponent<CanvasGroup>();
            Assert.That(replacementGroup, Is.Not.Null); Assert.That(replacementGroup.ignoreParentGroups, Is.True);
            Canvas.ForceUpdateCanvases();
            Assert.That(replacement.canvasRenderer.GetInheritedAlpha(), Is.EqualTo(1).Within(.001f));

            motion.enabled = false;
            Assert.That(motion.Opacity, Is.EqualTo(1)); Assert.That(motion.IsAnimating, Is.False);
            Assert.That(exclusion.ignoreParentGroups, Is.False); Assert.That(replacementGroup.ignoreParentGroups, Is.False);
            yield return null;
        }

        [Test] public void BackdropExclusionNeverTakesOverExistingGroupsOrInteractiveOrUnrelatedGraphics()
        {
            var page = Panel(canvas, "Backdrop authority page");
            var map = Backdrop(page);
            var semantic = map.gameObject.AddComponent<CanvasGroup>();
            semantic.alpha = .63f; semantic.ignoreParentGroups = false; semantic.interactable = false;
            var interactive = Backdrop(page); interactive.raycastTarget = true;
            var unrelated = Backdrop(page); unrelated.name = "Other map artwork";
            var image = Panel(page, "Field headquarters backdrop");

            var motion = UiMenuMotion.PlayScreen(page);
            Assert.That(semantic.alpha, Is.EqualTo(.63f)); Assert.That(semantic.ignoreParentGroups, Is.False);
            Assert.That(semantic.interactable, Is.False); Assert.That(map.GetComponents<CanvasGroup>().Length, Is.EqualTo(1));
            Assert.That(interactive.GetComponent<CanvasGroup>(), Is.Null);
            Assert.That(unrelated.GetComponent<CanvasGroup>(), Is.Null); Assert.That(image.GetComponent<CanvasGroup>(), Is.Null);
            motion.Finish();
            Assert.That(semantic.alpha, Is.EqualTo(.63f)); Assert.That(semantic.ignoreParentGroups, Is.False);
        }

        [Test] public void InstantFirstShowCreatesReusableBackdropOwnershipWithoutExcludingItsParent()
        {
            var page = Panel(canvas, "Instant page"); var map = Backdrop(page);
            var motion = UiMenuMotion.PlayScreen(page, false);
            var opacity = motion.OpacityGroup; var exclusion = map.GetComponent<CanvasGroup>();
            Assert.That(motion.IsAnimating, Is.False); Assert.That(motion.Opacity, Is.EqualTo(1));
            Assert.That(exclusion, Is.Not.Null); Assert.That(exclusion.ignoreParentGroups, Is.False);
            Assert.That(UiMenuMotion.PlayScreen(page), Is.SameAs(motion));
            Assert.That(motion.OpacityGroup, Is.SameAs(opacity)); Assert.That(map.GetComponent<CanvasGroup>(), Is.SameAs(exclusion));
            Assert.That(exclusion.ignoreParentGroups, Is.True);
            Assert.That(page.GetComponents<CanvasGroup>().Length, Is.EqualTo(1));
        }

        [Test] public void DisableAndReplayDiscardStaleProgressWithoutAddingGroupsOrMovingThePage()
        {
            var page = Panel(canvas, "Replayed page"); var geometry = new RectContract(page);
            var motion = UiMenuMotion.PlayScreen(page); var opacity = motion.OpacityGroup;
            page.gameObject.SetActive(false);
            Assert.That(motion.IsAnimating, Is.False); Assert.That(opacity.alpha, Is.EqualTo(1));
            page.gameObject.SetActive(true);
            Assert.That(motion.Opacity, Is.EqualTo(1), "Reactivation alone must not replay an old entrance.");
            Assert.That(UiMenuMotion.PlayScreen(page), Is.SameAs(motion)); Assert.That(motion.IsAnimating, Is.True);
            motion.enabled = false;
            Assert.That(motion.Opacity, Is.EqualTo(1));
            UiMenuMotion.PlayScreen(page);
            Assert.That(motion.IsAnimating, Is.False, "An explicitly disabled helper must stay at the endpoint.");
            motion.enabled = true; UiMenuMotion.PlayScreen(page);
            Assert.That(motion.IsAnimating, Is.True); Assert.That(motion.OpacityGroup, Is.SameAs(opacity));
            Assert.That(page.GetComponents<CanvasGroup>().Length, Is.EqualTo(1)); geometry.AssertUnchanged();
        }

        [Test] public void SuppressionIsNestedExceptionSafeAndDoesNotBlockActions()
        {
            var page = Panel(canvas, "Suppressed page");
            int clicks = 0;
            var button = new PlayerUi().Button(page, "Suppressed opening action", new Rect(20, 20, 240, 64),
                PlayerIcon.Next, "Continue", () => clicks++);
            IDisposable outer = UiMenuMotion.SuppressOpenings();
            try
            {
                using (UiMenuMotion.SuppressOpenings(false))
                    Assert.That(UiMenuMotion.PlayScreen(page).IsAnimating, Is.False);
                try
                {
                    using (UiMenuMotion.SuppressOpenings()) throw new InvalidOperationException("Expected test exception");
                }
                catch (InvalidOperationException) { }
                Assert.That(UiMenuMotion.PlayScreen(page).Opacity, Is.EqualTo(1));
                ExecuteEvents.Execute(button.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
                Assert.That(clicks, Is.EqualTo(1), "Suppressing visual openings must preserve synchronous input.");
            }
            finally { outer.Dispose(); outer.Dispose(); }
            Assert.That(UiMenuMotion.PlayScreen(page).IsAnimating, Is.True,
                "Disposed suppression scopes must not suppress subsequent user navigation.");
        }

        [Test] public void FinishInSettlesOnlyItsOwnDescendantsAndLeavesSemanticAlphaAlone()
        {
            var inside = Panel(canvas, "Reflow root");
            var page = UiMenuMotion.PlayScreen(Panel(inside, "Reflow page"));
            var window = UiMenuMotion.PlayWindow(Panel(inside, "Reflow window").gameObject);
            var outside = UiMenuMotion.PlayScreen(Panel(canvas, "Unrelated entrance"));
            var semantic = inside.gameObject.AddComponent<CanvasGroup>(); semantic.alpha = .58f;
            UiMenuMotion.FinishIn(inside);
            Assert.That(page.IsAnimating, Is.False); Assert.That(window.IsAnimating, Is.False);
            Assert.That(page.Opacity, Is.EqualTo(1)); Assert.That(window.Opacity, Is.EqualTo(1));
            Assert.That(outside.IsAnimating, Is.True); Assert.That(semantic.alpha, Is.EqualTo(.58f));
        }

        [UnityTest] public IEnumerator DestroyingOnlyTheHelperRestoresOpacityAndReleasesItsOwnedGroups()
        {
            var page = Panel(canvas, "Destroyed helper page"); var map = Backdrop(page);
            var motion = UiMenuMotion.PlayScreen(page);
            var opacity = motion.OpacityGroup; var exclusion = map.GetComponent<CanvasGroup>();
            Object.DestroyImmediate(motion);
            if (opacity != null) Assert.That(opacity.alpha, Is.EqualTo(1));
            if (exclusion != null) Assert.That(exclusion.ignoreParentGroups, Is.False);
            yield return null;
            Assert.That(opacity == null, Is.True); Assert.That(exclusion == null, Is.True);
            var replay = UiMenuMotion.PlayScreen(page);
            Assert.That(replay.IsAnimating, Is.True); Assert.That(page.GetComponents<CanvasGroup>().Length, Is.EqualTo(1));
        }

        static RectTransform Panel(Transform parent, string name)
        {
            var panel = PlayerUi.Graphic<Image>(parent, name, new Rect(40, 60, 640, 480));
            panel.color = Color.white; panel.raycastTarget = false;
            return panel.rectTransform;
        }

        static RawImage Backdrop(Transform parent)
        {
            var image = PlayerUi.Graphic<RawImage>(parent, "Field headquarters backdrop", new Rect(0, 0, 640, 480));
            image.color = Color.white; image.raycastTarget = false; return image;
        }

        sealed class RectContract
        {
            readonly RectTransform rect;
            readonly Transform parent;
            readonly Vector2 anchorMin, anchorMax, pivot, size, offsetMin, offsetMax;
            readonly Vector3 position, scale;
            readonly Quaternion rotation;
            public RectContract(RectTransform rect)
            {
                this.rect = rect; parent = rect.parent;
                anchorMin = rect.anchorMin; anchorMax = rect.anchorMax; pivot = rect.pivot;
                size = rect.sizeDelta; offsetMin = rect.offsetMin; offsetMax = rect.offsetMax;
                position = rect.anchoredPosition3D; scale = rect.localScale; rotation = rect.localRotation;
            }
            public void AssertUnchanged()
            {
                Assert.That(rect.parent, Is.SameAs(parent)); Assert.That(rect.anchorMin, Is.EqualTo(anchorMin));
                Assert.That(rect.anchorMax, Is.EqualTo(anchorMax)); Assert.That(rect.pivot, Is.EqualTo(pivot));
                Assert.That(rect.sizeDelta, Is.EqualTo(size)); Assert.That(rect.offsetMin, Is.EqualTo(offsetMin));
                Assert.That(rect.offsetMax, Is.EqualTo(offsetMax)); Assert.That(rect.anchoredPosition3D, Is.EqualTo(position));
                Assert.That(rect.localScale, Is.EqualTo(scale)); Assert.That(rect.localRotation, Is.EqualTo(rotation));
            }
        }
    }
}
