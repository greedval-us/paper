#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class WorkshopDrawingScreenTests
    {
        GameObject root, events;
        RunController run;
        WorkshopWizard wizard;
        Store store;
        GuidedDrawingPanel Panel => wizard.Drawing;

        void Open(bool threeMounts = false)
        {
            var balance = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(
                "Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            Assert.That(balance, Is.Not.Null, "Exercise the main game's catalog and workshop rules.");
            var prepared = balance.CreateWorkshopBalance();
            var profile = new WorkshopProfile(prepared.ResolveContent(), prepared);
            profile.GrantGold(1000);
            if (threeMounts)
                Assert.That(profile.Unlock("chassis.battery", out var reason, prepared), Is.True, reason);

            root = new GameObject("Isolated drawing screen", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            canvas.scaleFactor = Mathf.Min(Screen.width / 1600f, Screen.height / 1000f);
            events = new GameObject("Isolated drawing screen events", typeof(EventSystem));
            var host = new GameObject("Isolated drawing screen controller");
            host.transform.SetParent(root.transform, false); host.SetActive(false);
            run = host.AddComponent<RunController>(); run.balanceAsset = balance;
            store = new Store(profile); run.InitializeWorkshop(store);
            wizard = new WorkshopWizard(root.transform, run, null, _ => { }, () => { });
            if (threeMounts) Assert.That(wizard.SelectChassis("chassis.battery"), Is.True);
            Assert.That(wizard.Next(), Is.True);
            if (threeMounts)
            {
                Assert.That(wizard.SelectWeapon(1, "weapon.gun"), Is.True);
                Assert.That(wizard.SelectWeapon(2, "weapon.gun"), Is.True);
            }
            Assert.That(wizard.Next(), Is.True);
            Assert.That(wizard.Step, Is.EqualTo(2));
            Canvas.ForceUpdateCanvases();
        }

        [TearDown] public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (events != null) Object.DestroyImmediate(events);
        }

        Button Button(string name)
        {
            var button = root.GetComponentsInChildren<Button>(true)
                .SingleOrDefault(value => value.name == name && value.gameObject.activeInHierarchy);
            Assert.That(button, Is.Not.Null, "The drawing step must retain its action: " + name);
            return button;
        }

        void Click(string name)
        {
            var button = Button(name);
            Assert.That(button.IsInteractable(), Is.True, name);
            button.onClick.Invoke();
        }

        PointerEventData Pointer(Vector2 local) => new PointerEventData(events.GetComponent<EventSystem>())
        {
            pointerId = 17, button = PointerEventData.InputButton.Left,
            position = Panel.Canvas.LocalPointToScreen(local)
        };

        void DrawThroughRaycaster(Vector2 start, Vector2 end)
        {
            Canvas.ForceUpdateCanvases();
            var pointer = Pointer(start);
            var hits = new List<RaycastResult>();
            events.GetComponent<EventSystem>().RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, "A point on the visible paper must receive pointer input.");
            Assert.That(hits[0].gameObject, Is.SameAs(Panel.Canvas.gameObject),
                "Decorations and empty-state copy must not cover the drawing surface.");
            pointer.pointerPressRaycast = hits[0];
            var receiver = ExecuteEvents.GetEventHandler<IPointerDownHandler>(hits[0].gameObject);
            Assert.That(receiver, Is.SameAs(Panel.Canvas.gameObject));
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerDownHandler);
            Assert.That(Panel.Canvas.HasPendingGesture, Is.True);
            pointer.position = Pointer(end).position;
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(Panel.Canvas.HasPendingGesture, Is.False);
        }

        static void AssertPoint(DrawingPoint actual, Vector2 expected)
        {
            const float tolerance = 1f / DrawingPoint.Quantization;
            Assert.That(actual.X, Is.EqualTo(expected.x).Within(tolerance));
            Assert.That(actual.Y, Is.EqualTo(expected.y).Within(tolerance));
        }

        string AlbumState() => string.Join("|", run.Workshop.Album.Select(page => page.Id + ":" + page.Name)) +
            ";" + string.Join("|", run.Workshop.Loadout.Select(page => page?.Id));

        [UnityTest] public IEnumerator WidePaperReceivesRealPointerCoordinatesAndOneUndoWithoutSavingOrSpending()
            => ResizedPaperReceivesRealPointerCoordinatesAndOneUndoWithoutSavingOrSpending(1552, 674);

        [UnityTest] public IEnumerator CompactPaperReceivesRealPointerCoordinatesAndOneUndoWithoutSavingOrSpending()
            => ResizedPaperReceivesRealPointerCoordinatesAndOneUndoWithoutSavingOrSpending(1152, 624);

        IEnumerator ResizedPaperReceivesRealPointerCoordinatesAndOneUndoWithoutSavingOrSpending(int width, int height)
        {
            Open();
            int gold = run.Workshop.Gold, writes = store.Writes;
            string album = AlbumState();
            Panel.ReflowPage(width, height);
            Click("Guided drawing tool Rectangle");
            yield return null;
            var start = new Vector2(-.6f, -.6f); var end = new Vector2(.6f, .6f);
            DrawThroughRaycaster(start, end);
            var stroke = Panel.Session.Snapshot.GetPart(DrawingBindings.Base).Strokes.Single();
            Assert.That(stroke.Points.Count, Is.EqualTo(5));
            AssertPoint(stroke.Points[0], start);
            AssertPoint(stroke.Points[1], new Vector2(end.x, start.y));
            AssertPoint(stroke.Points[2], end);
            AssertPoint(stroke.Points[3], new Vector2(start.x, end.y));
            AssertPoint(stroke.Points[4], start);
            string accepted = Panel.Candidate.Drawing.GeometryHash;
            Assert.That(wizard.Draft.Drawing.GeometryHash, Is.EqualTo(accepted));
            Click("Guided drawing undo");
            Assert.That(Panel.Session.Snapshot.IsEmpty, Is.True, "The entire rectangle is one history command.");
            Assert.That(Panel.Session.CanUndo, Is.False);
            Click("Guided drawing redo");
            Assert.That(Panel.Candidate.Drawing.GeometryHash, Is.EqualTo(accepted));
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(store.Writes, Is.EqualTo(writes));
            Assert.That(AlbumState(), Is.EqualTo(album));
        }

        [Test] public void ResizingAnOpenStencilCatalogueKeepsItsInputScopeAndAcceptedHistory()
        {
            Open(); Panel.ReflowPage(1552, 674);
            Assert.That(Panel.Session.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.3f, -.3f),
                new DrawingPoint(.3f, .3f), out var reason), Is.True, reason);
            Panel.Canvas.FinishStroke();
            var session = Panel.Session;
            string accepted = Panel.Candidate.Drawing.GeometryHash;
            int gold = run.Workshop.Gold, writes = store.Writes;
            Click("Guided drawing more");
            Assert.That(Button("Guided drawing clear").IsInteractable(), Is.True);
            var opening = Button("Guided drawing stencils");
            events.GetComponent<EventSystem>().SetSelectedGameObject(opening.gameObject);
            Click("Guided drawing stencils");
            Panel.ReflowPage(1152, 624); Panel.Refresh(); Canvas.ForceUpdateCanvases();
            Assert.That(Panel.Session, Is.SameAs(session));
            Assert.That(Panel.Candidate.Drawing.GeometryHash, Is.EqualTo(accepted));
            Assert.That(Panel.Session.CanUndo, Is.True);
            Assert.That(Panel.Canvas.gameObject.activeSelf, Is.False, "Covered drawing must not process keyboard history.");
            Assert.That(Button("Guided drawing clear").IsInteractable(), Is.False);
            Assert.That(Button("Guided drawing tool Rectangle").IsInteractable(), Is.False);
            var catalogue = root.GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == "Guided drawing stencil catalogue");
            Assert.That(events.GetComponent<EventSystem>().currentSelectedGameObject.transform.IsChildOf(catalogue), Is.True);
            Click("Guided drawing close stencils");
            Assert.That(Panel.Canvas.gameObject.activeSelf, Is.True);
            Assert.That(Button("Guided drawing clear").IsInteractable(), Is.True);
            Assert.That(events.GetComponent<EventSystem>().currentSelectedGameObject, Is.SameAs(opening.gameObject));
            Assert.That(Panel.Session, Is.SameAs(session));
            Click("Guided drawing undo");
            Assert.That(Panel.Session.Snapshot.IsEmpty, Is.True);
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(store.Writes, Is.EqualTo(writes));
        }

        [UnityTest] public IEnumerator ThirdBarrelUsesItsOwnLayerAndProbeThenReachesReviewWithTheExactDrawing()
        {
            Open(threeMounts: true); Panel.ReflowPage(1152, 624);
            var session = Panel.Session;
            Assert.That(session.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.35f, -.35f),
                new DrawingPoint(.35f, .35f), out var bodyReason), Is.True, bodyReason);
            foreach (string binding in new[] { DrawingBindings.Mount0, DrawingBindings.Mount1 })
                Assert.That(session.AddRectangle(binding, new DrawingPoint(-.0625f, 0),
                    new DrawingPoint(.0625f, .5f), out var barrelReason), Is.True, barrelReason);
            Panel.Canvas.FinishStroke();
            var body = session.Snapshot.GetPart(DrawingBindings.Base).Strokes.Single().Points.ToArray();
            var first = session.Snapshot.GetPart(DrawingBindings.Mount0).Strokes.Single().Points.ToArray();
            var second = session.Snapshot.GetPart(DrawingBindings.Mount1).Strokes.Single().Points.ToArray();
            int gold = run.Workshop.Gold, writes = store.Writes;
            string album = AlbumState();
            Click("Guided drawing part " + DrawingBindings.Mount2);
            Click("Guided drawing tool Rectangle");
            yield return null;
            DrawThroughRaycaster(new Vector2(-.0625f, 0), new Vector2(.0625f, .65f));
            Assert.That(Panel.Canvas.SelectedBinding, Is.EqualTo(DrawingBindings.Mount2));
            Assert.That(session.Snapshot.GetPart(DrawingBindings.Mount2).Strokes.Count, Is.EqualTo(1));
            Assert.That(session.Snapshot.GetPart(DrawingBindings.Base).Strokes.Single().Points, Is.EqualTo(body));
            Assert.That(session.Snapshot.GetPart(DrawingBindings.Mount0).Strokes.Single().Points, Is.EqualTo(first));
            Assert.That(session.Snapshot.GetPart(DrawingBindings.Mount1).Strokes.Single().Points, Is.EqualTo(second));
            Assert.That(run.PreviewBlueprint(Panel.Candidate).Valid, Is.True);
            Panel.Probe.SetDistance(4); Click("Drawing probe fire");
            Assert.That(Panel.Probe.Mount, Is.EqualTo(2));
            var recording = Panel.Probe.Recording;
            Assert.That(recording, Is.Not.Null); Assert.That(recording.Completed, Is.True);
            Assert.That(recording.Hits, Is.GreaterThan(0));
            string accepted = Panel.Candidate.Drawing.GeometryHash;
            Assert.That(recording.Frames.First().Towers.Single().Blueprint.Drawing.GeometryHash, Is.EqualTo(accepted));
            var shots = recording.Frames.SelectMany(frame => frame.Shots).ToArray();
            Assert.That(shots, Is.Not.Empty);
            Assert.That(shots.All(shot => shot.MountSlotIndex == 2), Is.True,
                "The mini-probe must fire the selected third mount, not the first weapon.");
            Assert.That(wizard.Next(), Is.True, wizard.Status);
            Assert.That(wizard.Step, Is.EqualTo(3));
            Assert.That(wizard.Draft.Drawing.GeometryHash, Is.EqualTo(accepted));
            Assert.That(wizard.Draft.WeaponMounts.Select(mount => mount.SlotIndex), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(store.Writes, Is.EqualTo(writes));
            Assert.That(AlbumState(), Is.EqualTo(album));
        }

        [UnityTest] public IEnumerator MiddleButtonPanMovesTheRenderedPaperGridDuringTheGestureWithoutEditingTheDrawing()
        {
            Open(); Panel.ReflowPage(1152, 624);
            yield return null; Canvas.ForceUpdateCanvases();
            var paper = Panel.Root.GetComponentsInChildren<DrawingPaperGridGraphic>()
                .Single(graphic => graphic.name == "Drawing paper grid");
            var beforeMesh = paper.canvasRenderer.GetMesh();
            Assert.That(beforeMesh, Is.Not.Null);
            var beforeVertices = beforeMesh.vertices;
            Assert.That(beforeVertices, Is.Not.Empty, "The visible sheet must have a rendered grid before panning.");
            string drawing = Panel.Candidate.Drawing.GeometryHash;
            int gold = run.Workshop.Gold, writes = store.Writes;

            var pointer = Pointer(Vector2.zero);
            pointer.button = PointerEventData.InputButton.Middle;
            ExecuteEvents.Execute(Panel.Canvas.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            pointer.position += new Vector2(13, 7) * root.GetComponent<Canvas>().scaleFactor;
            ExecuteEvents.Execute(Panel.Canvas.gameObject, pointer, ExecuteEvents.dragHandler);
            Assert.That(Panel.Canvas.HasPendingGesture, Is.True);
            Assert.That(Panel.Canvas.Pan.sqrMagnitude, Is.GreaterThan(1));
            yield return null; Canvas.ForceUpdateCanvases();

            var afterMesh = paper.canvasRenderer.GetMesh();
            Assert.That(afterMesh, Is.Not.Null);
            Assert.That(afterMesh.vertexCount, Is.GreaterThan(0));
            Assert.That(afterMesh.vertices.SequenceEqual(beforeVertices), Is.False,
                "The full-paper grid must follow a middle-button drag while it is still in progress.");
            ExecuteEvents.Execute(Panel.Canvas.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(Panel.Canvas.HasPendingGesture, Is.False);
            Assert.That(Panel.Candidate.Drawing.GeometryHash, Is.EqualTo(drawing));
            Assert.That(Panel.Session.CanUndo, Is.False, "Moving the view must not add a drawing history command.");
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(store.Writes, Is.EqualTo(writes));
        }

        [Test] public void AddingASecondWeaponRebuildsPartTabsWithoutMovingThePlacedDrawingPage()
        {
            Open(); Panel.ReflowPage(1152, 624);
            var page = Panel.Root.GetComponent<RectTransform>();
            var position = new Vector2(24, -144);
            page.anchoredPosition = position;
            var parent = page.parent;
            var size = page.sizeDelta;
            Assert.That(Panel.Session.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.35f, -.35f),
                new DrawingPoint(.35f, .35f), out var bodyReason), Is.True, bodyReason);
            Assert.That(Panel.Session.AddRectangle(DrawingBindings.Mount0, new DrawingPoint(-.0625f, 0),
                new DrawingPoint(.0625f, .5f), out var barrelReason), Is.True, barrelReason);
            Panel.Canvas.FinishStroke();
            string accepted = Panel.Candidate.Drawing.GeometryHash;
            int gold = run.Workshop.Gold, writes = store.Writes;

            wizard.Back();
            Assert.That(wizard.Step, Is.EqualTo(1));
            Assert.That(wizard.SelectWeapon(1, "weapon.gun"), Is.True);
            Assert.That(wizard.Next(), Is.True);
            Assert.That(wizard.Step, Is.EqualTo(2));
            Assert.That(Panel.Root.GetComponent<RectTransform>(), Is.SameAs(page));
            Assert.That(page.parent, Is.SameAs(parent));
            Assert.That(page.anchoredPosition, Is.EqualTo(position),
                "Rebuilding the mounted-part tabs must preserve the page position assigned by the responsive shell.");
            Assert.That(page.sizeDelta, Is.EqualTo(size));
            Assert.That(Panel.Candidate.Drawing.GeometryHash, Is.EqualTo(accepted));
            Click("Guided drawing part " + DrawingBindings.Mount1);
            Assert.That(Panel.Canvas.SelectedBinding, Is.EqualTo(DrawingBindings.Mount1));
            Assert.That(Panel.Session.Snapshot.GetPart(DrawingBindings.Mount1).Strokes, Is.Empty);
            Assert.That(wizard.Next(), Is.False, "The newly installed second barrel still needs its own functional drawing.");
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(store.Writes, Is.EqualTo(writes));
        }

        [UnityTest] public IEnumerator DrawingHeaderKeepsCompletedStepNumbersVisibleAfterAnUnchangedWizardRefresh()
        {
            var balance = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(
                "Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            Assert.That(balance, Is.Not.Null);
            root = new GameObject("Isolated drawing header player"); root.SetActive(false);
            events = new GameObject("Isolated drawing header events", typeof(EventSystem));
            var game = root.AddComponent<PlayerGameRoot>(); game.Configure(balance, true);
            root.SetActive(true); game.Run.AutomaticSimulation = false;
            game.Navigate(PlayerPage.Workshop);
            var hud = root.GetComponentInChildren<WorkshopHud>(true);
            wizard = hud.Guided;
            // Drive the same refresh explicitly so the assertion is independent of the HUD's timer.
            hud.enabled = false;
            Assert.That(wizard.Next(), Is.True); Assert.That(wizard.Next(), Is.True);
            yield return null; yield return null;
            var responsive = root.GetComponentInChildren<WorkshopResponsiveLayout>(true);
            Assert.That(responsive, Is.Not.Null); responsive.Apply();
            yield return null; yield return null;
            var numbers = Enumerable.Range(0, 4).Select(index =>
                root.GetComponentsInChildren<Text>(true).Single(text => text.name == "Wizard progress " + index)).ToArray();
            foreach (var number in numbers)
                Assert.That(number.gameObject.activeInHierarchy && number.enabled, Is.True, number.name);

            int step = wizard.Step;
            string status = wizard.Status, drawing = wizard.Draft.Drawing.GeometryHash;
            wizard.Refresh();
            foreach(var number in numbers)
                Assert.That(number.gameObject.activeInHierarchy&&number.enabled,Is.True,
                    number.name+" must not disappear between a refresh and the next layout pass.");
            yield return null; yield return null;
            Assert.That(wizard.Step, Is.EqualTo(step));
            Assert.That(wizard.Status, Is.EqualTo(status));
            Assert.That(wizard.Draft.Drawing.GeometryHash, Is.EqualTo(drawing));
            foreach (var number in numbers)
                Assert.That(number.gameObject.activeInHierarchy && number.enabled, Is.True,
                    number.name + " must remain visible after the shell observes a routine refresh on the same drawing step.");
        }

        sealed class Store : IWorkshopProfileStore
        {
            readonly WorkshopProfile loaded;
            public int Writes;
            public Store(WorkshopProfile profile) { loaded = profile; }
            public ProfileLoadResult Load() => new ProfileLoadResult(loaded, ProfileLoadSource.Primary, "Isolated drawing profile", true);
            public ProfileSaveResult Save(WorkshopProfile profile) { Writes++; return new ProfileSaveResult(true, "Saved isolated drawing profile"); }
        }
    }
}
#endif
