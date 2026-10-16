using System.Collections;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class DrawingRangeStateTests
    {
        GameObject root;
        RunController controller;
        NotebookWorldView view;
        DrawingInput drawing;
        Keyboard keyboard;
        Mouse mouse;
        Camera worldCamera;
        InputSettings originalSettings, fixtureSettings;
        NativeInputIsolation nativeInput;

        [UnitySetUp] public IEnumerator CreateIsolatedInput()
        {
            nativeInput = new NativeInputIsolation();
            originalSettings = InputSystem.settings;
            if (originalSettings == null)
            {
                originalSettings = ScriptableObject.CreateInstance<InputSettings>();
                originalSettings.hideFlags = HideFlags.HideAndDontSave;
                InputSystem.settings = originalSettings;
            }
            fixtureSettings = Object.Instantiate(originalSettings);
            fixtureSettings.hideFlags = HideFlags.HideAndDontSave;
            fixtureSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            fixtureSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            HideFlags originalFlags = originalSettings.hideFlags;
            try
            {
                if (originalFlags == HideFlags.HideAndDontSave) originalSettings.hideFlags = HideFlags.None;
                InputSystem.settings = fixtureSettings;
            }
            finally { originalSettings.hideFlags = originalFlags; }

            root = new GameObject("Range state input fixture");
            root.SetActive(false);
            controller = root.AddComponent<RunController>();
            controller.AutomaticSimulation = false;
            controller.Initialize(allowLegacyFixtureDefaults: true);
            controller.TogglePause();
            view = root.AddComponent<NotebookWorldView>();
            view.Initialize(controller.Session);
            var cameraObject = new GameObject("Range input camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            worldCamera = camera;
            camera.orthographic = true;
            camera.orthographicSize = controller.Session.Balance.Height * .5f;
            camera.transform.position = new Vector3(controller.Session.Balance.Width * .5f, controller.Session.Balance.Height * .5f, -10);
            drawing = root.AddComponent<DrawingInput>();
            drawing.Initialize(controller, view, camera);
            keyboard = InputSystem.AddDevice<Keyboard>("RangeStateTestKeyboard");
            keyboard.MakeCurrent();
            mouse = InputSystem.AddDevice<Mouse>("ConstructionTestMouse");
            mouse.MakeCurrent();
            root.SetActive(true);
            yield return null;
            yield return null;
        }

        [UnityTearDown] public IEnumerator DisposeIsolatedInput()
        {
            try
            {
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (root != null) Object.Destroy(root);
                if (fixtureSettings != null)
                {
                    Assert.That(originalSettings != null, Is.True, "Fixture input settings must retain the original instance.");
                    InputSystem.settings = originalSettings;
                    if (fixtureSettings != null) Object.Destroy(fixtureSettings);
                }
            }
            finally { nativeInput?.Dispose(); nativeInput = null; }
            yield return null;
        }

        [Test] public void InputRangeStateFollowsTheRendererChangedByStandControls()
        {
            view.ShowRanges = true;
            Assert.That(drawing.ShowRanges, Is.True, "The stand button changes the renderer directly; input must expose that actual state.");
            view.ShowRanges = false;
            Assert.That(drawing.ShowRanges, Is.False);
        }

        [UnityTest] public IEnumerator ExistingWallCanBeInspectedByClickAndExtendedByDrag()
        {
            drawing.SelectTool(StructureKind.Wall);
            var start = new Cell(35, 20); var end = new Cell(38, 20);
            Assert.That(controller.Build(new[] { start }, StructureKind.Wall, MaterialTier.Pencil, out var reason), Is.True, reason);
            int ink = controller.Session.Ink;
            yield return Pointer(ScreenCell(start.X, start.Y), true);
            yield return Pointer(ScreenCell(start.X, start.Y), false);
            Assert.That(drawing.InspectedCell, Is.EqualTo(start));
            Assert.That(controller.Session.Ink, Is.EqualTo(ink));
            int extensionCost = controller.QuoteBuild(new[] { start, new Cell(36, 20), new Cell(37, 20), end }, StructureKind.Wall, MaterialTier.Pencil).Cost;
            yield return Pointer(ScreenCell(start.X, start.Y), true);
            yield return Pointer(ScreenCell(end.X, end.Y), true);
            yield return Pointer(ScreenCell(end.X, end.Y), false);
            Assert.That(controller.Session.StructureAt(end).HasValue, Is.True);
            Assert.That(drawing.InspectedCell.HasValue, Is.False);
            Assert.That(ink - controller.Session.Ink, Is.EqualTo(extensionCost));
        }

        [UnityTest] public IEnumerator TabTogglesTheCurrentRendererStateInOrdinaryInput()
        {
            drawing.ConstructionOnly = false;
            yield return AssertTabTogglesAfterStandControl();
        }

        [UnityTest] public IEnumerator TabTogglesTheCurrentRendererStateInConstructionInput()
        {
            drawing.ConstructionOnly = true;
            yield return AssertTabTogglesAfterStandControl();
        }

        IEnumerator AssertTabTogglesAfterStandControl()
        {
            view.ShowRanges = true;
            yield return PressTab();
            Assert.That(view.ShowRanges, Is.False, "The first Tab must hide ranges enabled by the stand's button.");
            Assert.That(drawing.ShowRanges, Is.False);

            yield return PressTab();
            Assert.That(view.ShowRanges, Is.True, "The next Tab must show ranges again.");
            Assert.That(drawing.ShowRanges, Is.True);

            view.ShowRanges = false;
            yield return PressTab();
            Assert.That(view.ShowRanges, Is.True, "Tab must also follow a range state disabled directly by the stand.");
            Assert.That(drawing.ShowRanges, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        IEnumerator PressTab()
        {
            Assert.That(keyboard.enabled, Is.True);
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
            yield return null;
            yield return null;
            Assert.That(Keyboard.current, Is.SameAs(keyboard), "The fixture must own the keyboard while its queued Tab is delivered.");
            Assert.That(keyboard.tabKey.isPressed, Is.True, "The real input system must receive the queued Tab press.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        Vector2 ScreenCell(int x, int y) => worldCamera.WorldToScreenPoint(new Vector3(x + .5f, y + .5f, 0));

        IEnumerator Pointer(Vector2 position, bool held)
        {
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left, held));
            yield return null;
            yield return null;
        }

        void BuildWall(int x)
        {
            Assert.That(controller.Build(new[] { new Cell(x, 20) }, StructureKind.Wall, MaterialTier.Pencil, out var reason), Is.True, reason);
        }

        [UnityTest] public IEnumerator ReleasePositionCompletesTheConstructionLine()
        {
            drawing.SelectTool(StructureKind.Wall);
            yield return Pointer(ScreenCell(35, 20), true);
            yield return Pointer(ScreenCell(39, 20), false);
            for (int x = 35; x <= 39; x++)
                Assert.That(controller.Session.StructureAt(new Cell(x, 20)).HasValue, Is.True, "Missing release segment " + x);
        }

        [UnityTest] public IEnumerator FastEraserDragRemovesEveryCrossedCell()
        {
            for (int x = 35; x <= 39; x++) BuildWall(x);
            drawing.SelectMode(DrawingMode.Erase);
            yield return Pointer(ScreenCell(35, 20), true);
            yield return Pointer(ScreenCell(39, 20), false);
            for (int x = 35; x <= 39; x++)
                Assert.That(controller.Session.StructureAt(new Cell(x, 20)).HasValue, Is.False, "Skipped eraser cell " + x);
        }

        [UnityTest] public IEnumerator EraserCannotStartFromAnOutsidePress()
        {
            BuildWall(35);
            drawing.SelectMode(DrawingMode.Erase);
            yield return Pointer(new Vector2(-100, -100), true);
            yield return Pointer(ScreenCell(35, 20), true);
            yield return Pointer(ScreenCell(35, 20), false);
            Assert.That(controller.Session.StructureAt(new Cell(35, 20)).HasValue, Is.True);
        }

        [UnityTest] public IEnumerator LeavingTheMapCancelsConstructionUntilANewPress()
        {
            int ink = controller.Session.Ink;
            drawing.SelectTool(StructureKind.Wall);
            yield return Pointer(ScreenCell(35, 20), true);
            yield return Pointer(new Vector2(-100, -100), true);
            yield return Pointer(ScreenCell(39, 20), true);
            yield return Pointer(ScreenCell(39, 20), false);
            Assert.That(controller.Session.Ink, Is.EqualTo(ink));
            Assert.That(controller.Session.StructureAt(new Cell(35, 20)).HasValue, Is.False);
        }

        [UnityTest] public IEnumerator CancelStopsHeldEraserUntilANewPress()
        {
            BuildWall(35); BuildWall(39);
            drawing.SelectMode(DrawingMode.Erase);
            yield return Pointer(ScreenCell(35, 20), true);
            drawing.Cancel();
            yield return Pointer(ScreenCell(39, 20), true);
            yield return Pointer(ScreenCell(39, 20), false);
            Assert.That(controller.Session.StructureAt(new Cell(39, 20)).HasValue, Is.True);
        }

        [UnityTest] public IEnumerator UiOwnedPressCannotTurnIntoAnEraserGesture()
        {
            BuildWall(35);
            drawing.SelectMode(DrawingMode.Erase);
            if (EventSystem.current == null)
            {
                var events = new GameObject("Input test event system", typeof(EventSystem));
                events.transform.SetParent(root.transform, false);
            }
            var overlay = new GameObject("Blocking input test UI", typeof(Canvas), typeof(GraphicRaycaster));
            overlay.transform.SetParent(root.transform, false);
            overlay.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var panel = new GameObject("Blocking panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(overlay.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            yield return Pointer(ScreenCell(35, 20), true);
            Assert.That(drawing.HasHover, Is.False, "The UI must own the initial press.");
            overlay.SetActive(false);
            yield return Pointer(ScreenCell(35, 20), true);
            yield return Pointer(ScreenCell(35, 20), false);
            Assert.That(controller.Session.StructureAt(new Cell(35, 20)).HasValue, Is.True);
        }

        [UnityTest] public IEnumerator DisablingInputDiscardsAnUnpaidConstructionGesture()
        {
            drawing.SelectTool(StructureKind.Wall);
            int ink = controller.Session.Ink;
            yield return Pointer(ScreenCell(35, 20), true);
            drawing.enabled = false;
            drawing.enabled = true;
            yield return Pointer(ScreenCell(39, 20), false);
            Assert.That(controller.Session.Ink, Is.EqualTo(ink));
            Assert.That(controller.Session.StructureAt(new Cell(35, 20)).HasValue, Is.False);
        }

        [UnityTest] public IEnumerator OutsidePressAndMapMoveInOneFrameCannotStartErasing()
        {
            BuildWall(35);
            drawing.SelectMode(DrawingMode.Erase);
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(-100, -100) }.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = ScreenCell(35, 20) }.WithButton(MouseButton.Left));
            yield return null;
            yield return null;
            yield return Pointer(ScreenCell(35, 20), false);
            Assert.That(controller.Session.StructureAt(new Cell(35, 20)).HasValue, Is.True);
        }

        [UnityTest] public IEnumerator PressAndMoveInOneFrameKeepTheFirstConstructionCell()
        {
            drawing.SelectTool(StructureKind.Wall);
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = ScreenCell(35, 20) }.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = ScreenCell(39, 20) }.WithButton(MouseButton.Left));
            yield return null;
            yield return null;
            yield return Pointer(ScreenCell(39, 20), false);
            for (int x = 35; x <= 39; x++)
                Assert.That(controller.Session.StructureAt(new Cell(x, 20)).HasValue, Is.True, "Lost press position " + x);
        }

        [UnityTest] public IEnumerator FocusLossCancelsConstructionUntilANewPress()
        {
            drawing.SelectTool(StructureKind.Wall);
            int ink = controller.Session.Ink;
            yield return Pointer(ScreenCell(35, 20), true);
            drawing.SendMessage("OnApplicationFocus", false);
            drawing.SendMessage("OnApplicationFocus", true);
            yield return Pointer(ScreenCell(39, 20), false);
            Assert.That(controller.Session.Ink, Is.EqualTo(ink));
        }

        [UnityTest] public IEnumerator PriorityHotkeyUsesTheCurrentPointerCell()
        {
            var first = new Cell(35, 20); var second = new Cell(39, 20);
            Assert.That(controller.Build(new[] { first, second }, StructureKind.Gun, MaterialTier.Pencil, out var reason), Is.True, reason);
            var initial = controller.Session.StructureAt(first).Value.Priority;
            yield return Pointer(ScreenCell(first.X, first.Y), false);
            keyboard.MakeCurrent(); mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = ScreenCell(second.X, second.Y) });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.P));
            yield return null;
            yield return null;
            Assert.That(controller.Session.StructureAt(first).Value.Priority, Is.EqualTo(initial));
            Assert.That(controller.Session.StructureAt(second).Value.Priority, Is.Not.EqualTo(initial));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        [Test] public void PrecisePointerSettingIsRestoredOnlyAfterTheLastInputDisables()
        {
            Assert.That(fixtureSettings.disableRedundantEventsMerging, Is.True);
            var second = root.AddComponent<DrawingInput>();
            second.Initialize(controller, view, worldCamera);
            drawing.enabled = false;
            Assert.That(fixtureSettings.disableRedundantEventsMerging, Is.True);
            second.enabled = false;
            Assert.That(fixtureSettings.disableRedundantEventsMerging, Is.EqualTo(originalSettings.disableRedundantEventsMerging));
            drawing.enabled = true;
            Assert.That(fixtureSettings.disableRedundantEventsMerging, Is.True);
        }
    }
}
