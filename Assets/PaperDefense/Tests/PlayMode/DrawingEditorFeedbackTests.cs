using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class DrawingEditorFeedbackTests
    {
        GameObject root, events;
        DrawingEditorPanel editor;
        string rejection;

        [SetUp] public void SetUp()
        {
            root = new GameObject("Isolated drawing feedback", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            events = new GameObject("Isolated feedback events", typeof(EventSystem));
            editor = new DrawingEditorPanel(root.transform, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), _ => { });
            editor.Canvas.Rejected += reason => rejection = reason;
        }

        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(events);
        }

        void Open(int strokes)
        {
            var line = new DrawingStroke(new[] { new DrawingPoint(-.2f, 0), new DrawingPoint(.2f, 0) });
            var drawing = new DrawingSnapshot(new[]
            {
                new DrawingPart(DrawingPartKey.Base, Enumerable.Repeat(line, strokes).ToArray())
            });
            editor.Open(TowerBlueprint.NewDraft("feedback", "Feedback", ChassisKind.Standard, StructureKind.Gun).WithDrawing(drawing));
            Canvas.ForceUpdateCanvases();
        }

        void Stroke(DrawingPartKey part)
        {
            editor.Canvas.SelectedPart = part;
            var pointer = Pointer(Vector2.zero);
            editor.Canvas.OnPointerDown(pointer);
            pointer.position = editor.Canvas.LocalPointToScreen(new Vector2(.1f, .1f));
            editor.Canvas.OnDrag(pointer);
            editor.Canvas.OnPointerUp(pointer);
        }

        PointerEventData Pointer(Vector2 point) => new PointerEventData(events.GetComponent<EventSystem>())
        {
            button = PointerEventData.InputButton.Left,
            position = editor.Canvas.LocalPointToScreen(point)
        };

        void Click(string name) => root.GetComponentsInChildren<Button>(true).Single(button => button.name == name).onClick.Invoke();
        bool HasText(string value)
        {
            string expected = TextResourcesAsset.Default.Resolve(value);
            return root.GetComponentsInChildren<Text>(true).Any(label => label.gameObject.activeInHierarchy && label.text.Contains(expected));
        }
        void AssertRejected()
        {
            Assert.That(rejection, Is.Not.Null.And.Not.Empty, "The real pointer stroke must be rejected.");
            Assert.That(HasText(rejection), Is.True, "A refresh must preserve the rejected-stroke explanation.");
            Assert.That(HasText("Качество "), Is.True, "Quality feedback remains available beside the error.");
        }

        [Test] public void RejectedStrokeSurvivesRefreshControlsAndNoOpEdits()
        {
            Open(128);
            string unchanged = editor.Session.Snapshot.GeometryHash;
            Stroke(DrawingPartKey.Base);
            AssertRejected();
            foreach (string control in new[] { "Draw part Mount0", "Drawing pencil", "Drawing eraser", "Drawing zoom in",
                "Drawing zoom out", "Drawing undo", "Drawing redo", "Clear drawing part" })
            {
                Click(control);
                AssertRejected();
            }
            var pointer = Pointer(new Vector2(.35f, .35f));
            editor.Canvas.OnPointerDown(pointer);
            editor.Canvas.OnPointerUp(pointer);
            editor.Canvas.FinishStroke();
            AssertRejected();
            Assert.That(editor.Session.Snapshot.GeometryHash, Is.EqualTo(unchanged));
        }

        [Test] public void SuccessfulStrokeClearsEarlierRejection()
        {
            Open(0);
            Stroke(DrawingPartKey.Mount1);
            AssertRejected();
            Stroke(DrawingPartKey.Base);
            Assert.That(editor.Session.Snapshot.StrokeCount, Is.EqualTo(1));
            Assert.That(HasText(rejection), Is.False);
            Assert.That(HasText("Качество "), Is.True);
        }

        [Test] public void SuccessfulUndoClearsEarlierRejection()
        {
            Open(127);
            Stroke(DrawingPartKey.Base);
            Stroke(DrawingPartKey.Base);
            AssertRejected();
            Click("Drawing undo");
            Assert.That(editor.Session.Snapshot.StrokeCount, Is.EqualTo(127));
            Assert.That(HasText(rejection), Is.False);
        }

        [Test] public void SuccessfulRedoClearsEarlierRejection()
        {
            Open(127);
            Stroke(DrawingPartKey.Base);
            Click("Drawing undo");
            Stroke(DrawingPartKey.Mount1);
            AssertRejected();
            Click("Drawing redo");
            Assert.That(editor.Session.Snapshot.StrokeCount, Is.EqualTo(128));
            Assert.That(HasText(rejection), Is.False);
        }

        [Test] public void SuccessfulEraseClearsEarlierRejection()
        {
            Open(128);
            Stroke(DrawingPartKey.Base);
            AssertRejected();
            Click("Drawing eraser");
            var pointer = Pointer(Vector2.zero);
            editor.Canvas.OnPointerDown(pointer);
            editor.Canvas.OnPointerUp(pointer);
            Assert.That(editor.Session.Snapshot.StrokeCount, Is.Zero);
            Assert.That(HasText(rejection), Is.False);
        }

        [Test] public void SuccessfulClearPartClearsEarlierRejection()
        {
            Open(128);
            Stroke(DrawingPartKey.Base);
            AssertRejected();
            Click("Clear drawing part");
            Assert.That(editor.Session.Snapshot.StrokeCount, Is.Zero);
            Assert.That(HasText(rejection), Is.False);
        }

        [Test] public void OpeningAnotherSessionClearsEarlierRejection()
        {
            Open(128);
            Stroke(DrawingPartKey.Base);
            AssertRejected();
            Open(0);
            Assert.That(HasText(rejection), Is.False);
            Assert.That(HasText("Качество "), Is.True);
        }
    }
}
