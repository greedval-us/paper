using System.Collections;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace PaperDefense.Tests
{
    public sealed class DrawingRangeStateTests
    {
        GameObject root;
        RunController controller;
        NotebookWorldView view;
        DrawingInput drawing;
        Keyboard keyboard;
        InputSettings originalSettings, fixtureSettings;

        [UnitySetUp] public IEnumerator CreateIsolatedInput()
        {
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
            camera.orthographic = true;
            camera.orthographicSize = controller.Session.Balance.Height * .5f;
            camera.transform.position = new Vector3(controller.Session.Balance.Width * .5f, controller.Session.Balance.Height * .5f, -10);
            drawing = root.AddComponent<DrawingInput>();
            drawing.Initialize(controller, view, camera);
            keyboard = InputSystem.AddDevice<Keyboard>("RangeStateTestKeyboard");
            keyboard.MakeCurrent();
            root.SetActive(true);
            yield return null;
            yield return null;
        }

        [UnityTearDown] public IEnumerator DisposeIsolatedInput()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (root != null) Object.Destroy(root);
            if (fixtureSettings != null)
            {
                Assert.That(originalSettings != null, Is.True, "Fixture input settings must retain the original instance.");
                InputSystem.settings = originalSettings;
                if (fixtureSettings != null) Object.Destroy(fixtureSettings);
            }
            yield return null;
        }

        [Test] public void InputRangeStateFollowsTheRendererChangedByStandControls()
        {
            view.ShowRanges = true;
            Assert.That(drawing.ShowRanges, Is.True, "The stand button changes the renderer directly; input must expose that actual state.");
            view.ShowRanges = false;
            Assert.That(drawing.ShowRanges, Is.False);
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
    }
}
