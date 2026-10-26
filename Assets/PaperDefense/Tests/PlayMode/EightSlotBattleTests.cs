#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class EightSlotBattleTests
    {
        AssemblyCommandFixture fixture;
        GameObject root;
        DrawingInput drawing;
        NotebookWorldView view;
        Camera camera;
        Keyboard keyboard;
        Mouse mouse;
        NativeInputIsolation nativeInput;
        InputSettings originalSettings, fixtureSettings;
        RunController Run => fixture.Run;

        [UnitySetUp] public IEnumerator SetUp()
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
            var flags = originalSettings.hideFlags;
            try
            {
                if (flags == HideFlags.HideAndDontSave) originalSettings.hideFlags = HideFlags.None;
                InputSystem.settings = fixtureSettings;
            }
            finally { originalSettings.hideFlags = flags; }
            foreach (var owner in Object.FindObjectsByType<PlayerGameRoot>()) Object.Destroy(owner.gameObject);
            foreach (var owner in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(owner.gameObject);
            yield return null;
            fixture = new AssemblyCommandFixture(); root = fixture.Root;
            Assert.That(Run.StartRun(out var reason), Is.True, reason);
            Run.AutomaticSimulation = false;
            view = root.AddComponent<NotebookWorldView>(); view.Initialize(Run.Session);
            var cameraObject = new GameObject("Eight slot battle camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false); camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = Run.Session.Balance.Height * .5f;
            camera.transform.position = new Vector3(Run.Session.Balance.Width * .5f, Run.Session.Balance.Height * .5f, -10);
            drawing = root.AddComponent<DrawingInput>(); drawing.Initialize(Run, view, camera);
            keyboard = InputSystem.AddDevice<Keyboard>("EightSlotBattleKeyboard"); keyboard.MakeCurrent();
            mouse = InputSystem.AddDevice<Mouse>("EightSlotBattleMouse"); mouse.MakeCurrent();
            if (EventSystem.current == null)
                new GameObject("Eight slot event system", typeof(EventSystem)).transform.SetParent(root.transform, false);
            root.SetActive(true); EventSystem.current.SetSelectedGameObject(null);
            yield return null; yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            try
            {
                EventSystem.current?.SetSelectedGameObject(null);
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                fixture?.Dispose(); fixture = null;
                if (fixtureSettings != null)
                {
                    Assert.That(originalSettings != null, Is.True);
                    InputSystem.settings = originalSettings;
                    if (fixtureSettings != null) Object.Destroy(fixtureSettings);
                }
            }
            finally { nativeInput?.Dispose(); nativeInput = null; }
            yield return null;
        }

        [UnityTest] public IEnumerator DigitEight_SelectsSlotSeven()
        {
            yield return Keys(Key.Digit8);
            Assert.That(drawing.BlueprintSlot, Is.EqualTo(7));
            Assert.That(drawing.Tool, Is.EqualTo(StructureKind.Generator));
        }

        [UnityTest] public IEnumerator EveryDigitSelectsItsOriginalFrozenPositionWithoutCallingLegacyTools()
        {
            var digits = new[] { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8 };
            for (int slot = 0; slot < digits.Length; slot++)
            {
                yield return Keys(digits[slot]);
                Assert.That(drawing.BlueprintSlot, Is.EqualTo(slot), "Digit " + (slot + 1));
                Assert.That(drawing.Mode, Is.EqualTo(DrawingMode.Build));
                Assert.That(drawing.Tool, Is.EqualTo(new StructureState(Run.RunLoadout.ResolveSlot(slot, MaterialTier.Pencil)).Kind));
            }
        }

        [UnityTest] public IEnumerator AllEightSelectedPagesUseTheSameQuotePreviewAndActualPayment()
        {
            for (int slot = 0; slot < 8; slot++)
            {
                drawing.SelectSlot(slot); var cell = ClearCell(slot); int ink = Run.Session.Ink;
                var quote = Run.QuoteEntry(new[] { cell }, slot, drawing.Tier, drawing.Color);
                yield return Pointer(cell, false);
                Assert.That(drawing.BlueprintSlot, Is.EqualTo(slot));
                Assert.That(drawing.HasHover, Is.True);
                Assert.That(drawing.PreviewCost, Is.EqualTo(quote.Cost), "Preview slot " + slot);
                Assert.That(drawing.PreviewReason, Is.Empty);
                yield return Pointer(cell, true); yield return Pointer(cell, false);
                var built = Run.Session.StructureAt(cell);
                Assert.That(built.HasValue, Is.True, "Slot " + slot + ": " + drawing.Message);
                Assert.That(built.Value.ResolvedEntry, Is.SameAs(Run.RunLoadout.ResolveSlot(slot, MaterialTier.Pencil)));
                Assert.That(ink - Run.Session.Ink, Is.EqualTo(quote.Cost), "Payment slot " + slot);
            }
        }

        [UnityTest] public IEnumerator AnEmptySelectedSlotCannotBuildThePreviouslySelectedPageOrARawTool()
        {
            Run.OpenWorkshop();
            Assert.That(Run.AssemblyCommands.AssignSlot(7, null, Guid.NewGuid()).Success, Is.True);
            Assert.That(Run.StartRun(out var reason), Is.True, reason); view.Initialize(Run.Session);
            drawing.SelectSlot(0); yield return Keys(Key.Digit8);
            Assert.That(drawing.BlueprintSlot, Is.EqualTo(7));
            var cell = ClearCell(0); int ink = Run.Session.Ink, structures = Run.Session.Structures.Count;
            yield return Pointer(cell, false);
            Assert.That(drawing.PreviewCost, Is.Zero); Assert.That(drawing.PreviewReason, Is.Not.Empty);
            yield return Pointer(cell, true); yield return Pointer(cell, false);
            Assert.That(Run.Session.Ink, Is.EqualTo(ink)); Assert.That(Run.Session.Structures.Count, Is.EqualTo(structures));
            Assert.That(Run.Session.StructureAt(cell), Is.Null);
        }

        [UnityTest] public IEnumerator MaterialColorAndEraseHaveSeparateActionsAndDigitsKeepTheirChoices()
        {
            drawing.SelectSlot(0); yield return Keys(Key.Q);
            Assert.That(drawing.Tier, Is.EqualTo(MaterialTier.Ink));
            drawing.SelectColor(InkColor.Red);
            Assert.That(drawing.Color, Is.EqualTo(InkColor.Red));
            yield return Keys(Key.E); Assert.That(drawing.Mode, Is.EqualTo(DrawingMode.Erase));
            yield return Keys(Key.Digit3);
            Assert.That(drawing.BlueprintSlot, Is.EqualTo(2)); Assert.That(drawing.Mode, Is.EqualTo(DrawingMode.Build));
            Assert.That(drawing.Tier, Is.EqualTo(MaterialTier.Ink)); Assert.That(drawing.Color, Is.EqualTo(InkColor.Red));
            yield return Keys(Key.C); Assert.That(drawing.Mode, Is.EqualTo(DrawingMode.Paint));
            Assert.That(drawing.BlueprintSlot, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator FocusedNameFieldBlocksBattleHotkeysAndMapConstruction()
        {
            var field = new GameObject("Battle name field", typeof(RectTransform), typeof(InputField));
            field.transform.SetParent(root.transform, false);
            yield return AssertFocusedUiBlocks(field);
        }

        [UnityTest] public IEnumerator GeneratorSelectionAndMaterialShortcutsStayPencilOnly()
        {
            drawing.SelectSlot(0); drawing.SelectTier(MaterialTier.Ink);
            yield return Keys(Key.Digit8);
            Assert.That(drawing.BlueprintSlot, Is.EqualTo(7)); Assert.That(drawing.Tier, Is.EqualTo(MaterialTier.Pencil));
            yield return Keys(Key.Q); Assert.That(drawing.Tier, Is.EqualTo(MaterialTier.Pencil));
            drawing.SelectTier(MaterialTier.Marker); Assert.That(drawing.Tier, Is.EqualTo(MaterialTier.Pencil));
            var cell = ClearCell(7); yield return Pointer(cell, false);
            Assert.That(drawing.PreviewReason, Is.Empty);
            Assert.That(drawing.PreviewCost, Is.EqualTo(Run.RunLoadout.ResolveSlot(7, MaterialTier.Pencil).Cost));
        }

        [UnityTest] public IEnumerator ASelectedChildOfANameFieldStillBlocksBattleHotkeysAndConstruction()
        {
            var field = new GameObject("Battle name parent", typeof(RectTransform), typeof(InputField));
            field.transform.SetParent(root.transform, false);
            var child = new GameObject("Selected name text", typeof(RectTransform)); child.transform.SetParent(field.transform, false);
            yield return AssertFocusedUiBlocks(child);
        }

        [UnityTest] public IEnumerator AHiddenNameFieldCannotKeepTheReturnedBattleInputBlocked()
        {
            var field = new GameObject("Hidden previous workshop field", typeof(RectTransform), typeof(InputField));
            field.transform.SetParent(root.transform, false);
            EventSystem.current.SetSelectedGameObject(field);
            field.SetActive(false);
            yield return Keys(Key.Digit8);
            Assert.That(drawing.BlueprintSlot, Is.EqualTo(7));
            var cell = ClearCell(7); yield return Pointer(cell, false);
            Assert.That(drawing.HasHover, Is.True); Assert.That(drawing.PreviewReason, Is.Empty);
            field.SetActive(true); drawing.SelectSlot(0);
            field.GetComponent<InputField>().enabled = false;
            EventSystem.current.SetSelectedGameObject(field);
            yield return Keys(Key.Digit8);
            Assert.That(drawing.BlueprintSlot, Is.EqualTo(7));
            yield return Pointer(cell, false);
            Assert.That(drawing.HasHover, Is.True); Assert.That(drawing.PreviewReason, Is.Empty);
        }

        [UnityTest] public IEnumerator FocusedTmpNameFieldBlocksBattleHotkeysAndMapConstruction()
        {
            var type = Type.GetType("TMPro.TMP_InputField, Unity.TextMeshPro", throwOnError: true);
            var field = new GameObject("Battle TMP name field", typeof(RectTransform), type);
            field.transform.SetParent(root.transform, false);
            yield return AssertFocusedUiBlocks(field);
        }

        IEnumerator AssertFocusedUiBlocks(GameObject selected)
        {
            drawing.SelectSlot(0); var cell = ClearCell(0); int ink = Run.Session.Ink, structures = Run.Session.Structures.Count;
            int slot = drawing.BlueprintSlot; var tool = drawing.Tool; bool paused = Run.Paused;
            EventSystem.current.SetSelectedGameObject(selected);
            yield return Keys(Key.Digit8, Key.E, Key.Q, Key.Space, Key.N, Key.F);
            Assert.That(drawing.BlueprintSlot, Is.EqualTo(slot)); Assert.That(drawing.Tool, Is.EqualTo(tool));
            Assert.That(drawing.Mode, Is.EqualTo(DrawingMode.Build)); Assert.That(drawing.Tier, Is.EqualTo(MaterialTier.Pencil));
            Assert.That(Run.Paused, Is.EqualTo(paused)); Assert.That(Run.Session.Phase, Is.EqualTo(RunPhase.Planning));
            yield return Pointer(cell, true); yield return Pointer(cell, false);
            Assert.That(drawing.HasHover, Is.False);
            Assert.That(Run.Session.Ink, Is.EqualTo(ink)); Assert.That(Run.Session.Structures.Count, Is.EqualTo(structures));
        }

        [UnityTest] public IEnumerator AnOpenModalBlocksHotkeysAndPointerEvenAwayFromTheModalGraphics()
        {
            var sheet = new GameObject("Battle modal", typeof(RectTransform)); sheet.transform.SetParent(root.transform, false);
            WorkshopModal.Open(sheet, () => sheet.SetActive(false));
            yield return AssertFocusedUiBlocks(null);
        }

        [UnityTest] public IEnumerator ClosingAModalDoesNotReplayAPressStartedBehindIt()
        {
            drawing.SelectSlot(0); var cell = ClearCell(0); int ink = Run.Session.Ink;
            var sheet = new GameObject("Battle held press modal", typeof(RectTransform)); sheet.transform.SetParent(root.transform, false);
            WorkshopModal.Open(sheet, () => sheet.SetActive(false));
            yield return Pointer(cell, true); sheet.SetActive(false);
            yield return Pointer(cell, true); yield return Pointer(cell, false);
            Assert.That(Run.Session.Ink, Is.EqualTo(ink)); Assert.That(Run.Session.StructureAt(cell), Is.Null);
        }

        [UnityTest] public IEnumerator FrozenMaterialCostAndRangeSurviveAuthoringChangesUntilTheGestureIsPaid()
        {
            var frozen = Run.RunLoadout; var expected = frozen.ResolveSlot(0, MaterialTier.Ink);
            fixture.Preset.rules.MaterialDefinitions[(int)MaterialTier.Ink].CostMultiplier = 100;
            fixture.AssemblyAsset.catalog.Definitions.Single(part => part.Id == "core.gun").Modifiers
                .Single(modifier => modifier.Stat == AssemblyStat.Range).Value = 200;
            drawing.SelectSlot(0); drawing.SelectTier(MaterialTier.Ink); var cell = ClearCell(0, MaterialTier.Ink);
            int ink = Run.Session.Ink; yield return Pointer(cell, false);
            Assert.That(drawing.PreviewCost, Is.EqualTo(expected.Cost));
            var range = (float)typeof(NotebookWorldView).GetField("previewRange", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            Assert.That(range, Is.EqualTo(expected.Weapons.Max(weapon => weapon.Range)));
            yield return Pointer(cell, true); yield return Pointer(cell, false);
            Assert.That(Run.RunLoadout, Is.SameAs(frozen));
            Assert.That(Run.Session.StructureAt(cell).Value.ResolvedEntry.Cost, Is.EqualTo(expected.Cost));
            Assert.That(ink - Run.Session.Ink, Is.EqualTo(expected.Cost));
        }

        [UnityTest] public IEnumerator LegacyFixtureKeepsTheOriginalFourSlotsAndFiveToEightConstructionTools()
        {
            var legacyRoot = new GameObject("Explicit legacy input route"); legacyRoot.SetActive(false);
            try
            {
                drawing.enabled = false;
                var controller = legacyRoot.AddComponent<RunController>(); controller.AutomaticSimulation = false;
                controller.InitializeWorkshop(persistent: false, allowLegacyFixtureDefaults: true);
                Assert.That(controller.StartRun(out var reason), Is.True, reason);
                var world = legacyRoot.AddComponent<NotebookWorldView>(); world.Initialize(controller.Session);
                var input = legacyRoot.AddComponent<DrawingInput>(); input.Initialize(controller, world, camera);
                legacyRoot.SetActive(true); yield return Keys(Key.Digit8);
                Assert.That(controller.RunLoadout, Is.Not.Null); Assert.That(input.BlueprintSlot, Is.EqualTo(-1));
                Assert.That(input.Tool, Is.EqualTo(StructureKind.RepairPost));
                yield return Keys(Key.Digit1); Assert.That(input.BlueprintSlot, Is.Zero);
            }
            finally { Object.DestroyImmediate(legacyRoot); }
        }

        Cell ClearCell(int slot, MaterialTier material = MaterialTier.Pencil)
        {
            for (int y = 2; y < Run.Session.Balance.Height - 2; y++)
            for (int x = 2; x < Run.Session.Balance.Width - 2; x++)
            {
                var cell = new Cell(x, y);
                if (Run.QuoteEntry(new[] { cell }, slot, material, InkColor.None).Valid &&
                    camera.pixelRect.Contains(camera.WorldToScreenPoint(new Vector3(x + .5f, y + .5f)))) return cell;
            }
            Assert.Fail("The isolated board must contain an affordable visible cell for slot " + slot); return default;
        }
        IEnumerator Keys(params Key[] keys)
        {
            keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return null; yield return null;
            Assert.That(Keyboard.current, Is.SameAs(keyboard)); Assert.That(keyboard.enabled, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        }
        IEnumerator Pointer(Cell cell, bool held)
        {
            var position = (Vector2)camera.WorldToScreenPoint(new Vector3(cell.X + .5f, cell.Y + .5f));
            mouse.MakeCurrent(); InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left, held));
            yield return null; yield return null;
            Assert.That(Mouse.current, Is.SameAs(mouse)); Assert.That(mouse.enabled, Is.True);
        }
    }
}
#endif
