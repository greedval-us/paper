using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace PaperDefense.Tests
{
    public sealed class DrawingWorkbenchTests
    {
        GameObject root, events, controllerRoot;
        DrawingEditorPanel editor;
        Font runtimeFont;
        int applied;

        [SetUp] public void SetUp()
        {
            root = new GameObject("Isolated drawing workbench", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            events = new GameObject("Isolated workbench events", typeof(EventSystem));
            runtimeFont = Resources.Load<Font>("NotebookHand");
            Assert.That(runtimeFont, Is.Not.Null, "Workbench checks must use the shipped runtime font.");
            editor = new DrawingEditorPanel(root.transform, runtimeFont, _ => applied++);
            editor.Open(TowerBlueprint.NewDraft("workbench", "Workbench", ChassisKind.Standard, StructureKind.Gun));
            Canvas.ForceUpdateCanvases();
        }

        [TearDown] public void Cleanup() { Object.DestroyImmediate(root); Object.DestroyImmediate(events); if (controllerRoot != null) Object.DestroyImmediate(controllerRoot); }

        UnityEngine.UI.Button Button(string name) => root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == name && b.gameObject.activeInHierarchy);
        UnityEngine.UI.Text Text(string name) => root.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(t => t.name == name && t.gameObject.activeInHierarchy);

        PointerEventData Pointer(float x, float y) => new PointerEventData(events.GetComponent<EventSystem>())
        {
            button = PointerEventData.InputButton.Left,
            position = editor.Canvas.LocalPointToScreen(new Vector2(x, y))
        };

        [Test] public void PointerReleaseIncludesItsFinalPointAndHasOneUndoStep()
        {
            var pointer = Pointer(-.2f, -.2f);
            editor.Canvas.OnPointerDown(pointer);
            pointer.position = Pointer(.1f, .1f).position;
            editor.Canvas.OnDrag(pointer);
            pointer.position = Pointer(.3f, .3f).position;
            editor.Canvas.OnPointerUp(pointer);
            var points = editor.Session.Snapshot.GetPart(DrawingBindings.Base).Strokes.Single().Points;
            Assert.That(points.Last().X, Is.EqualTo(.3f).Within(.002f));
            Assert.That(points.Last().Y, Is.EqualTo(.3f).Within(.002f));
            Assert.That(editor.Session.Undo(), Is.True);
            Assert.That(editor.Session.Snapshot.StrokeCount, Is.Zero);
            Assert.That(editor.Session.CanUndo, Is.False);
        }

        [Test] public void StrayDragWithoutPointerDownDoesNotCreateGeometry()
        {
            editor.Canvas.OnDrag(Pointer(-.2f, -.2f));
            editor.Canvas.OnDrag(Pointer(.2f, .2f));
            editor.Canvas.OnPointerUp(Pointer(.3f, .3f));
            Assert.That(editor.Session.Snapshot.StrokeCount, Is.Zero);
            Assert.That(editor.Session.CanUndo, Is.False);
        }
        [Test] public void WorkingGeometryReadoutFollowsEditsAndUndoWithoutSpendingExtraInk()
        {
            editor.Session.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.25f, -.25f), new DrawingPoint(.25f, .25f), out _);
            editor.Session.AddRectangle(DrawingBindings.Mount0, new DrawingPoint(-.0625f, -.5f), new DrawingPoint(.0625f, .5f), out _);
            editor.Canvas.SelectedBinding = DrawingBindings.Mount0;
            editor.Canvas.FinishStroke();
            string before = Text("Drawing working geometry").text;
            Assert.That(before, Does.Contain("0,50"));
            editor.Session.ScalePart(DrawingBindings.Mount0, 1, 1.5f, out _);
            editor.Canvas.FinishStroke();
            Assert.That(Text("Drawing working geometry").text, Does.Contain("0,75"));
            editor.Canvas.Undo();
            Assert.That(Text("Drawing working geometry").text, Is.EqualTo(before));
            editor.Canvas.SelectedBinding = DrawingBindings.Base;
            Assert.That(Text("Drawing working geometry").text, Does.Contain("0,25"));
            Assert.That(applied, Is.Zero);
        }
        [Test] public void HistoricalEditorExplainsUpgradeAndCancelKeepsOriginalRules()
        {
            editor.Session.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.25f, -.25f), new DrawingPoint(.25f, .25f), out _);
            editor.Session.AddRectangle(DrawingBindings.Mount0, new DrawingPoint(-.0625f, -.5f), new DrawingPoint(.0625f, .5f), out _);
            var data = new BlueprintData(editor.Session.Blueprint.WithFreeformDrawing(editor.Session.Snapshot)); data.FreeformRuleVersion = 1;
            var old = data.ToBlueprint(); editor.Close(false); editor.Open(old);
            Assert.That(Text("Drawing introduction").text, Does.Contain("Применить"));
            Assert.That(old.FreeformDesign.GetBarrel(0).Length, Is.EqualTo(1));
            editor.Close(false);
            Assert.That(applied, Is.Zero);
            Assert.That(new BlueprintData(old).FreeformRuleVersion, Is.EqualTo(1));
        }
        [Test] public void CapacityFeedbackUsesRoundedPreviewCapacity()
        {
            var run = Connected(new Store(new WorkshopProfile()));
            editor.Session.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.25f, -.25f), new DrawingPoint(.25f, .25f), out _);
            editor.Session.AddRectangle(DrawingBindings.Mount0, new DrawingPoint(-.0625f, 0), new DrawingPoint(.0625f, .5f), out _);
            editor.Canvas.FinishStroke();
            var before = run.PreviewBlueprint(editor.Session.Blueprint.WithFreeformDrawing(editor.Session.Snapshot));
            editor.Session.ScalePart(DrawingBindings.Base, 1.001f, 1.001f, out _);
            editor.Canvas.FinishStroke();
            var after = run.PreviewBlueprint(editor.Session.Blueprint.WithFreeformDrawing(editor.Session.Snapshot));
            Assert.That(after.Capacity, Is.EqualTo(before.Capacity));
            Assert.That(Text("Drawing goal guidance").text, Does.Contain("вместимость без изменений"));
        }

        [Test] public void DisableDiscardsPendingGestureBeforeCanvasIsReenabled()
        {
            editor.Canvas.OnPointerDown(Pointer(-.2f, -.2f));
            editor.Canvas.OnDrag(Pointer(.2f, .2f));
            editor.Canvas.gameObject.SetActive(false);
            editor.Canvas.gameObject.SetActive(true);
            editor.Canvas.OnPointerUp(Pointer(.3f, .3f));
            Assert.That(editor.Session.Snapshot.StrokeCount, Is.Zero);
        }

        [Test] public void FocusLossDiscardsPendingGestureAndReopenNeverReplaysIt()
        {
            editor.Canvas.OnPointerDown(Pointer(-.2f, -.2f)); editor.Canvas.OnDrag(Pointer(.2f, .2f));
            editor.Canvas.SendMessage("OnApplicationFocus", false);
            editor.Canvas.SendMessage("OnApplicationFocus", true);
            editor.Canvas.OnPointerUp(Pointer(.3f, .3f));
            Assert.That(editor.Session.Snapshot.StrokeCount, Is.Zero);
            editor.Canvas.OnPointerDown(Pointer(-.2f, -.2f)); editor.Canvas.OnDrag(Pointer(.2f, .2f));
            var blueprint = editor.Session.Blueprint;
            editor.Close(false); editor.Open(blueprint);
            editor.Canvas.OnPointerUp(Pointer(.3f, .3f));
            Assert.That(editor.Session.Snapshot.StrokeCount, Is.Zero);
            Assert.That(editor.Session.CanUndo, Is.False);
        }

        [UnityTest] public IEnumerator KeyboardHistorySkipsTextFieldsAndEscapeCancelsGestureBeforeClosing()
        {
            var original = InputSystem.settings;
            var settings = Object.Instantiate(original); settings.hideFlags = HideFlags.HideAndDontSave;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var flags = original.hideFlags;
            if (flags == HideFlags.HideAndDontSave) original.hideFlags = HideFlags.None;
            try { InputSystem.settings = settings; } finally { original.hideFlags = flags; }
            var keyboard = InputSystem.AddDevice<Keyboard>("DrawingWorkbenchKeyboard"); keyboard.MakeCurrent();
            try
            {
                yield return null;
                editor.Session.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.2f, -.2f), new DrawingPoint(.2f, .2f), out _);
                yield return Keys(keyboard, Key.LeftCtrl, Key.Z); Assert.That(editor.Session.Snapshot.StrokeCount, Is.Zero);
                yield return Keys(keyboard, Key.LeftCtrl, Key.LeftShift, Key.Z); Assert.That(editor.Session.Snapshot.StrokeCount, Is.EqualTo(1));
                yield return Keys(keyboard, Key.LeftCtrl, Key.Z); Assert.That(editor.Session.Snapshot.StrokeCount, Is.Zero);
                yield return Keys(keyboard, Key.LeftCtrl, Key.Y); Assert.That(editor.Session.Snapshot.StrokeCount, Is.EqualTo(1));
                var field = new GameObject("Focused input", typeof(RectTransform), typeof(UnityEngine.UI.InputField)); field.transform.SetParent(root.transform, false);
                events.GetComponent<EventSystem>().SetSelectedGameObject(field);
                yield return Keys(keyboard, Key.LeftCtrl, Key.Z);
                Assert.That(editor.Session.Snapshot.StrokeCount, Is.EqualTo(1), "Typing must not trigger drawing history.");
                events.GetComponent<EventSystem>().SetSelectedGameObject(null);
                editor.Canvas.OnPointerDown(Pointer(-.2f, -.2f)); editor.Canvas.OnDrag(Pointer(.2f, .2f));
                yield return Keys(keyboard, Key.Escape);
                Assert.That(editor.Canvas.HasPendingGesture, Is.False); Assert.That(editor.IsOpen, Is.True);
                Assert.That(editor.Session.Snapshot.StrokeCount, Is.EqualTo(1));
                yield return Keys(keyboard, Key.Escape); Assert.That(editor.IsOpen, Is.False);
            }
            finally
            {
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings = original; Object.DestroyImmediate(settings);
            }
        }
        static IEnumerator Keys(Keyboard keyboard, params Key[] keys)
        {
            keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        }

        [Test] public void CompleteOverBudgetDrawingCannotApplyThroughDirectClose()
        {
            editor.Session.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.9f, -.9f), new DrawingPoint(.9f, .9f), out _);
            editor.Session.AddRectangle(DrawingBindings.Mount0, new DrawingPoint(-.2f, 0), new DrawingPoint(.2f, .9f), out _);
            Assert.That(editor.Session.Blueprint.WithFreeformDrawing(editor.Session.Snapshot).FreeformDesign.IsComplete, Is.True);
            editor.Close(true);
            Assert.That(editor.IsOpen, Is.True);
            Assert.That(applied, Is.Zero);
            Assert.That(Button("Apply drawing").interactable, Is.False);
            Assert.That(Text("Drawing ink usage").color.r, Is.GreaterThan(Text("Drawing ink usage").color.b));
        }

        [Test] public void FinishStrokeWithoutGestureRefreshesExternalSessionEditsWithoutAddingHistory()
        {
            Assert.That(Button("Apply drawing").interactable, Is.False);
            Assert.That(editor.Session.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.3f, -.3f), new DrawingPoint(.3f, .3f), out _), Is.True);
            Assert.That(editor.Session.AddRectangle(DrawingBindings.Mount0, new DrawingPoint(-.08f, 0), new DrawingPoint(.08f, .6f), out _), Is.True);
            string complete = editor.Session.Snapshot.GeometryHash;
            int changes = 0, edits = 0;
            editor.Canvas.Changed += () => changes++; editor.Canvas.Edited += () => edits++;
            Assert.That(editor.Canvas.HasPendingGesture, Is.False);
            editor.Canvas.FinishStroke();
            Assert.That(Button("Apply drawing").interactable, Is.True);
            Assert.That(changes, Is.EqualTo(1)); Assert.That(edits, Is.Zero);
            editor.Canvas.FinishStroke();
            Assert.That(changes, Is.EqualTo(2)); Assert.That(edits, Is.Zero);
            Assert.That(editor.Session.Snapshot.GeometryHash, Is.EqualTo(complete));
            Assert.That(editor.Session.Undo(), Is.True); Assert.That(editor.Session.Undo(), Is.True);
            Assert.That(editor.Session.Snapshot.StrokeCount, Is.Zero);
            Assert.That(editor.Session.CanUndo, Is.False, "Refresh notifications must not add any history commands.");
        }

        [TestCase(DrawingTool.Line, 2)]
        [TestCase(DrawingTool.Rectangle, 5)]
        [TestCase(DrawingTool.Ellipse, 33)]
        public void ShapeHasGhostBeforeReleaseThenOneAtomicCommand(DrawingTool tool, int count)
        {
            editor.Canvas.Tool = tool;
            var pointer = Pointer(-.2f, -.3f); editor.Canvas.OnPointerDown(pointer);
            pointer.position = Pointer(.2f, .3f).position; editor.Canvas.OnDrag(pointer);
            Assert.That(editor.Canvas.HasPendingGesture, Is.True);
            Assert.That(editor.Session.Snapshot.StrokeCount, Is.Zero);
            Assert.That(editor.Session.CanUndo, Is.False);
            pointer.position = Pointer(.4f, .5f).position; editor.Canvas.OnPointerUp(pointer);
            var points = editor.Session.Snapshot.GetPart(DrawingBindings.Base).Strokes.Single().Points;
            Assert.That(points.Count, Is.EqualTo(count));
            Assert.That(points.Max(p => p.X), Is.EqualTo(.4f).Within(.002f));
            Assert.That(points.Max(p => p.Y), Is.EqualTo(.5f).Within(.002f));
            Assert.That(editor.Session.Undo(), Is.True);
            Assert.That(editor.Session.CanUndo, Is.False);
        }

        [Test] public void SweptWholeStrokeEraserCommitsOnceAndUndoesEveryTouchedStroke()
        {
            editor.Session.AddLine(DrawingBindings.Base, new DrawingPoint(-.3f, -.2f), new DrawingPoint(-.3f, .2f), out _);
            editor.Session.AddLine(DrawingBindings.Base, new DrawingPoint(.3f, -.2f), new DrawingPoint(.3f, .2f), out _);
            string original = editor.Session.Snapshot.GeometryHash;
            editor.Canvas.Tool = DrawingTool.Eraser;
            var pointer = Pointer(-.6f, 0); editor.Canvas.OnPointerDown(pointer);
            pointer.position = Pointer(.6f, 0).position; editor.Canvas.OnDrag(pointer);
            Assert.That(editor.Session.Snapshot.GeometryHash, Is.EqualTo(original));
            editor.Canvas.OnPointerUp(pointer);
            Assert.That(editor.Session.Snapshot.StrokeCount, Is.Zero);
            Assert.That(editor.Session.Undo(), Is.True);
            Assert.That(editor.Session.Snapshot.GeometryHash, Is.EqualTo(original));
        }

        [TestCase("tool")]
        [TestCase("part")]
        [TestCase("zoom")]
        [TestCase("cancel")]
        public void ContextChangesDiscardPendingGestureWithoutJoiningItToAnotherPart(string change)
        {
            editor.Canvas.OnPointerDown(Pointer(-.2f, -.2f)); editor.Canvas.OnDrag(Pointer(.2f, .2f));
            switch (change)
            {
                case "tool": editor.Canvas.Tool = DrawingTool.Eraser; break;
                case "part": editor.Canvas.SelectedBinding = DrawingBindings.Mount0; break;
                case "zoom": editor.Canvas.Zoom = 1.4f; break;
                default: editor.Canvas.CancelGesture(); break;
            }
            editor.Canvas.OnPointerUp(Pointer(.3f, .3f));
            Assert.That(editor.Session.Snapshot.StrokeCount, Is.Zero);
            Assert.That(editor.Session.CanUndo, Is.False);
        }

        [Test] public void MovePreviewCommitsOnceAndOutOfBoundsMoveRejectsWithoutClipping()
        {
            editor.Session.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.2f, -.2f), new DrawingPoint(.2f, .2f), out _);
            string original = editor.Session.Snapshot.GeometryHash;
            editor.Canvas.Tool = DrawingTool.Move;
            var pointer = Pointer(0, 0); editor.Canvas.OnPointerDown(pointer);
            pointer.position = Pointer(.5f, .2f).position; editor.Canvas.OnDrag(pointer);
            Assert.That(editor.Session.Snapshot.GeometryHash, Is.EqualTo(original));
            editor.Canvas.OnPointerUp(pointer);
            string moved = editor.Session.Snapshot.GeometryHash;
            Assert.That(moved, Is.Not.EqualTo(original));
            pointer = Pointer(0, 0); editor.Canvas.OnPointerDown(pointer);
            pointer.position = Pointer(.5f, 0).position; editor.Canvas.OnPointerUp(pointer);
            Assert.That(editor.Session.Snapshot.GeometryHash, Is.EqualTo(moved));
            Assert.That(Text("Drawing rejection").text, Is.Not.Empty);
            Assert.That(editor.Session.Undo(), Is.True);
            Assert.That(editor.Session.Snapshot.GeometryHash, Is.EqualTo(original));
        }

        [Test] public void PanZoomAndFitOnlyChangeTheViewAndSnapChangesTheCommittedEndpoint()
        {
            var pointer = Pointer(0, 0); pointer.button = PointerEventData.InputButton.Middle;
            editor.Canvas.OnPointerDown(pointer); pointer.position = Pointer(.2f, .2f).position;
            editor.Canvas.OnDrag(pointer); editor.Canvas.OnPointerUp(pointer);
            Assert.That(editor.Canvas.Pan.sqrMagnitude, Is.GreaterThan(0));
            editor.Canvas.Zoom = 1.5f; editor.Canvas.Fit();
            Assert.That(editor.Canvas.Pan, Is.EqualTo(Vector2.zero));
            Assert.That(editor.Canvas.Zoom, Is.EqualTo(1));
            Assert.That(editor.Session.CanUndo, Is.False);
            editor.Canvas.SnapToGrid = true; editor.Canvas.Tool = DrawingTool.Line;
            editor.Canvas.OnPointerDown(Pointer(.02f, .02f)); editor.Canvas.OnPointerUp(Pointer(.13f, .23f));
            var end = editor.Session.Snapshot.GetPart(DrawingBindings.Base).Strokes.Single().Points.Last();
            Assert.That(end.X, Is.EqualTo(DrawingCanvasGraphic.GridSpacing).Within(.002f));
            Assert.That(end.Y, Is.EqualTo(2 * DrawingCanvasGraphic.GridSpacing).Within(.002f));
        }

        [Test] public void FreeStencilsGiveAnEmptyCanvasABeginnerPathAndRemainUndoable()
        {
            Assert.That(Text("Drawing gesture help").text, Does.Contain("трафарета"));
            Button("Insert stencil drawing.body.basic").onClick.Invoke();
            editor.Canvas.SelectedBinding = DrawingBindings.Mount0;
            Button("Insert stencil drawing.barrel.basic").onClick.Invoke();
            Assert.That(Button("Apply drawing").interactable, Is.True);
            Assert.That(editor.Session.Blueprint.WithFreeformDrawing(editor.Session.Snapshot).FreeformDesign.IsUsable, Is.True);
            editor.Canvas.Undo(); Assert.That(Button("Apply drawing").interactable, Is.False);
            editor.Close(false); Assert.That(applied, Is.Zero);
        }

        [Test] public void FailedPurchasePreservesGoldAndGeometryAndRetryDoesNotInsertUntilRequested()
        {
            var profile = new WorkshopProfile(); profile.GrantGold(100); var store = new Store(profile) { Fail = true };
            var run = Connected(store); editor.Canvas.SelectedBinding = DrawingBindings.Mount0;
            var originalReadModel = run.Workshop;
            string before = editor.Session.Snapshot.GeometryHash;
            Button("Buy stencil drawing.sniper").onClick.Invoke();
            Assert.That(run.Workshop.Gold, Is.EqualTo(100)); Assert.That(run.Workshop.IsDrawingUnlocked("drawing.sniper"), Is.False);
            Assert.That(run.Workshop, Is.SameAs(originalReadModel));
            Assert.That(editor.Session.Snapshot.GeometryHash, Is.EqualTo(before));
            Assert.That(Text("Drawing rejection").text, Is.Not.Empty);
            store.Fail = false; Button("Buy stencil drawing.sniper").onClick.Invoke();
            Assert.That(run.Workshop.Gold, Is.EqualTo(68)); Assert.That(run.Workshop.IsDrawingUnlocked("drawing.sniper"), Is.True);
            Assert.That(run.Workshop, Is.Not.SameAs(originalReadModel), "Ownership cache must follow the published read model.");
            editor.Canvas.Zoom = 1.1f; editor.Canvas.ShowGrid = false;
            editor.Canvas.SelectedBinding = DrawingBindings.Base; editor.Canvas.SelectedBinding = DrawingBindings.Mount0;
            Assert.That(Button("Buy stencil drawing.sniper").interactable, Is.False);
            Assert.That(editor.Session.Snapshot.GeometryHash, Is.EqualTo(before));
            Assert.That(Button("Insert stencil drawing.sniper").interactable, Is.True);
            Button("Insert stencil drawing.sniper").onClick.Invoke();
            Assert.That(editor.Session.Snapshot.GeometryHash, Is.Not.EqualTo(before));
            Assert.That(run.Workshop.Gold, Is.EqualTo(68));
            editor.Canvas.Undo(); Assert.That(editor.Session.Snapshot.GeometryHash, Is.EqualTo(before));
            editor.Close(false); Assert.That(applied, Is.Zero); Assert.That(run.Workshop.IsDrawingUnlocked("drawing.sniper"), Is.True);
        }

        [Test] public void PaidStencilsAreDisabledWithoutAnApplicationProfile()
        {
            editor.Canvas.SelectedBinding = DrawingBindings.Mount0;
            Assert.That(Button("Buy stencil drawing.sniper").interactable, Is.False);
            Assert.That(Button("Insert stencil drawing.sniper").interactable, Is.False);
            Button("Insert stencil drawing.sniper").onClick.Invoke();
            Assert.That(editor.Session.Snapshot.StrokeCount, Is.Zero);
            Assert.That(Text("Drawing stencil gold").text, Does.Contain("профиля"));
        }

        [Test] public void SelectedMountDisplaysResolvedStatsAndRefreshesImmediatelyAfterResizing()
        {
            var run = Connected(new Store(new WorkshopProfile()));
            Button("Insert stencil drawing.body.basic").onClick.Invoke(); editor.Canvas.SelectedBinding = DrawingBindings.Mount0;
            Button("Insert stencil drawing.barrel.basic").onClick.Invoke();
            var candidate = editor.Session.Blueprint.WithFreeformDrawing(editor.Session.Snapshot);
            var stats = run.PreviewBlueprint(candidate).Weapons.Single();
            var expected = TextResourcesAsset.Default.Resolve(UserText.Message("drawing.workbench.mount_stats", 1, stats.Damage, stats.Range, stats.FireRate, stats.Interval));
            Assert.That(Text("Drawing selected mount stats").text, Does.StartWith(expected));
            string old = Text("Drawing selected mount stats").text;
            Button("Drawing height larger").onClick.Invoke();
            Assert.That(Text("Drawing selected mount stats").text, Is.Not.EqualTo(old));
        }

        [Test] public void VisibleWorkbenchRegionsDoNotOverlapAndStayInsideThePanel()
        {
            string[] names = { "Drawing viewport", "Drawing working geometry", "Drawing parts", "Drawing live stats", "Drawing selected mount stats", "Drawing goal guidance", "Drawing stencils", "Drawing rejection", "Apply drawing", "Cancel drawing" };
            var regions = root.GetComponentsInChildren<RectTransform>().Where(r => names.Contains(r.name)).ToArray();
            Assert.That(regions.Length, Is.EqualTo(names.Length));
            var board = root.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Authored drawing editor");
            for (int i = 0; i < regions.Length; i++)
            {
                var a = Bounds(regions[i], board);
                Assert.That(a.xMin, Is.GreaterThanOrEqualTo(board.rect.xMin)); Assert.That(a.xMax, Is.LessThanOrEqualTo(board.rect.xMax));
                Assert.That(a.yMin, Is.GreaterThanOrEqualTo(board.rect.yMin)); Assert.That(a.yMax, Is.LessThanOrEqualTo(board.rect.yMax));
                for (int j = i + 1; j < regions.Length; j++) Assert.That(a.Overlaps(Bounds(regions[j], board)), Is.False, regions[i].name + " overlaps " + regions[j].name);
            }
        }
        [TestCase(.6f)]
        [TestCase(.8f)]
        public void ShippedFontFitsAllVisibleCopyIncludingThreeMountFeedAndStencilDescriptions(float scale)
        {
            var run = Connected(new Store(new WorkshopProfile()));
            root.GetComponent<Canvas>().scaleFactor = scale;
            var blueprint = editor.Session.Blueprint.WithWeaponMount(1, LegacyContentIds.Weapon(StructureKind.Gun)).WithWeaponMount(2, LegacyContentIds.Weapon(StructureKind.Gun));
            editor.Open(blueprint);
            AssertVisibleTextFits();
            Button("Insert stencil drawing.body.basic").onClick.Invoke();
            foreach (var binding in new[] { DrawingBindings.Mount0, DrawingBindings.Mount1, DrawingBindings.Mount2 })
            {
                editor.Canvas.SelectedBinding = binding; Button("Insert stencil drawing.barrel.basic").onClick.Invoke();
                AssertVisibleTextFits();
            }
            var stats = run.PreviewBlueprint(editor.Session.Blueprint.WithFreeformDrawing(editor.Session.Snapshot));
            Assert.That(stats.FeedMultiplier, Is.LessThan(1), "Exercise the extra shared-feed line.");
            foreach (var binding in new[] { DrawingBindings.Base, DrawingBindings.Mount0, DrawingBindings.Decoration })
            { editor.Canvas.SelectedBinding = binding; AssertVisibleTextFits(); }
            editor.Session.ClearPart(DrawingBindings.Base);
            editor.Session.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.5f, -.5f), new DrawingPoint(.5f, .5f), out _);
            editor.Canvas.SelectedBinding = DrawingBindings.Base; editor.Canvas.FinishStroke(); AssertVisibleTextFits();
            editor.Session.ClearPart(DrawingBindings.Mount0);
            editor.Session.AddRectangle(DrawingBindings.Mount0, new DrawingPoint(-.4f, 0), new DrawingPoint(.4f, 1), out _);
            editor.Canvas.SelectedBinding = DrawingBindings.Mount0; editor.Canvas.FinishStroke(); AssertVisibleTextFits();
        }
        void AssertVisibleTextFits()
        {
            Canvas.ForceUpdateCanvases();
            var failures = new List<string>();
            foreach (var label in root.GetComponentsInChildren<UnityEngine.UI.Text>())
            {
                if (string.IsNullOrEmpty(label.text)) continue;
                if (label.font != runtimeFont) failures.Add(label.name + ": wrong font");
                if (label.preferredHeight > label.rectTransform.rect.height + .1f)
                    failures.Add(label.name + ": needs " + label.preferredHeight + ", allocated " + label.rectTransform.rect.height + "; " + label.text);
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }
        [Test] public void GridAndThumbnailRemainAtLeastOnePhysicalPixelWideAtStandScale()
        {
            var canvas = root.GetComponent<Canvas>(); canvas.scaleFactor = .6f; Canvas.ForceUpdateCanvases();
            using (var mesh = new UnityEngine.UI.VertexHelper())
            {
                Populate(editor.Canvas, mesh);
                // Every grid line is a four-vertex quad; inspect both orientations across the full sheet.
                for (int line = 0; line < 42; line++)
                {
                    var a = Vertex(mesh, line * 4); var b = Vertex(mesh, line * 4 + 3);
                    Assert.That(Vector3.Distance(a, b) * canvas.scaleFactor, Is.GreaterThanOrEqualTo(1.24f));
                }
                mesh.Clear(); Populate(root.GetComponentsInChildren<DrawingStencilGraphic>().First(), mesh);
                Assert.That(Vector3.Distance(Vertex(mesh, 0), Vertex(mesh, 3)) * canvas.scaleFactor, Is.GreaterThanOrEqualTo(1.24f));
            }
        }
        static void Populate(UnityEngine.UI.Graphic graphic, UnityEngine.UI.VertexHelper mesh)
            => graphic.GetType().GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(UnityEngine.UI.VertexHelper) }, null).Invoke(graphic, new object[] { mesh });
        static Vector3 Vertex(UnityEngine.UI.VertexHelper mesh, int index)
        { var vertex = new UIVertex(); mesh.PopulateUIVertex(ref vertex, index); return vertex.position; }
        static Rect Bounds(RectTransform element, RectTransform parent)
        {
            var corners = new Vector3[4]; element.GetWorldCorners(corners);
            var min = parent.InverseTransformPoint(corners[0]); var max = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        RunController Connected(Store store)
        {
            editor.Close(false); controllerRoot = new GameObject("Isolated stencil controller"); controllerRoot.SetActive(false);
            var run = controllerRoot.AddComponent<RunController>(); run.InitializeWorkshop(store, allowLegacyFixtureDefaults: true);
            editor = new DrawingEditorPanel(root.transform, runtimeFont, _ => applied++, context:
                new DrawingEditorContext { Preview = bp => run.PreviewBlueprint(bp), Workshop = () => run.Workshop, Purchase = run.UnlockDrawingAid });
            editor.Open(TowerBlueprint.NewDraft("connected", "Connected", ChassisKind.Standard, StructureKind.Gun).WithContent(run.Content));
            Canvas.ForceUpdateCanvases(); return run;
        }
        sealed class Store : IWorkshopProfileStore
        {
            WorkshopProfileData saved; public bool Fail;
            public Store(WorkshopProfile profile) { saved = profile.ToData(); }
            public ProfileLoadResult Load() => new ProfileLoadResult(WorkshopProfile.FromData(saved), ProfileLoadSource.Primary, "Isolated", true);
            public ProfileSaveResult Save(WorkshopProfile candidate)
            { if (Fail) return new ProfileSaveResult(false, "Injected failure"); saved = JsonUtility.FromJson<WorkshopProfileData>(JsonUtility.ToJson(candidate.ToData())); return new ProfileSaveResult(true, ""); }
        }
    }
}
