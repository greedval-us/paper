using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PaperDefense.Tests
{
    public sealed class GuidedDrawingPanelTests
    {
        GameObject root, events;
        GuidedDrawingPanel panel;
        RunController run;
        Store store;
        int changes, probes;
        TowerBlueprint notified, probed;

        [SetUp] public void SetUp()
        {
            changes = probes = 0; notified = probed = null;
            root = new GameObject("Isolated guided drawing", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            events = new GameObject("Isolated guided drawing events", typeof(EventSystem));
            var host = new GameObject("Isolated guided drawing run"); host.transform.SetParent(root.transform); host.SetActive(false);
            run = host.AddComponent<RunController>(); store = new Store(); run.InitializeWorkshop(store, allowLegacyFixtureDefaults: true);
            var ui = new WorkshopUi(); ui.EnsureTooltip(root.transform);
            panel = new GuidedDrawingPanel(root.transform, ui, new DrawingEditorContext
            {
                Preview = p => run.PreviewBlueprint(p), Workshop = () => run.Workshop, Purchase = run.UnlockDrawingAid,
                Probe = (p, m, d, f) => { probes++; probed = p; return run.ProbeDrawing(p, m, d, f); }
            }, p => { changes++; notified = p; });
            panel.Open(Draft()); Canvas.ForceUpdateCanvases();
        }

        [TearDown] public void TearDown() { Object.DestroyImmediate(root); Object.DestroyImmediate(events); }

        TowerBlueprint Draft(StructureKind weapon = StructureKind.Gun)
            => TowerBlueprint.NewDraft("guided-drawing", "Guided drawing", ChassisKind.Standard, weapon).WithContent(run.Content).AsFreeform();
        TowerBlueprint Complete(TowerBlueprint blueprint = null)
        {
            blueprint = blueprint ?? Draft(); var session = new DrawingEditSession(blueprint);
            session.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.35f, -.35f), new DrawingPoint(.35f, .35f), out _);
            foreach (var mount in blueprint.WeaponMounts)
                session.AddRectangle(DrawingBindings.Mount(mount.SlotIndex), new DrawingPoint(-.0625f, 0), new DrawingPoint(.0625f, .5f), out _);
            return blueprint.WithFreeformDrawing(session.Snapshot);
        }
        UnityEngine.UI.Button Button(string name) => root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == name);
        UnityEngine.UI.Text Text(string name) => root.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(t => t.name == name);
        void Click(string name) => Button(name).onClick.Invoke();
        PointerEventData Pointer(float x, float y) => new PointerEventData(events.GetComponent<EventSystem>())
        { button = PointerEventData.InputButton.Left, position = panel.Canvas.LocalPointToScreen(new Vector2(x, y)) };
        void Gesture(float x0, float y0, float x1, float y1, bool release = true)
        {
            var pointer = Pointer(x0, y0); panel.Canvas.OnPointerDown(pointer);
            pointer.position = Pointer(x1, y1).position; panel.Canvas.OnDrag(pointer);
            if (release) panel.Canvas.OnPointerUp(pointer);
        }

        [Test] public void EmptyDrawingOffersAStartingShapeAndKeepsDetailsHidden()
        {
            Assert.That(panel.Root.activeSelf, Is.True);
            Assert.That(Text("Guided drawing empty title").gameObject.activeInHierarchy, Is.True);
            Assert.That(Text("Guided drawing detailed values").gameObject.activeInHierarchy, Is.False);
            Assert.That(Button("Guided drawing empty stencil").gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.TryFinish(out var candidate, out string reason), Is.False);
            Assert.That(candidate, Is.Null); Assert.That(reason, Is.Not.Empty);
            Assert.That(changes, Is.Zero);
            Click("Guided drawing details toggle");
            Assert.That(Text("Guided drawing detailed values").gameObject.activeInHierarchy, Is.True);
            Assert.That(Button("Drawing probe fire").gameObject.activeInHierarchy, Is.False);
        }

        [Test] public void HideCancelsOnlyPendingGestureAndReopenRetainsAcceptedHistory()
        {
            panel.Canvas.Tool = DrawingTool.Rectangle; Gesture(-.3f, -.3f, .3f, .3f);
            var session = panel.Session; string accepted = session.Snapshot.GeometryHash;
            Assert.That(changes, Is.EqualTo(1)); Assert.That(notified.Drawing.GeometryHash, Is.EqualTo(accepted));
            Gesture(-.1f, -.1f, .1f, .1f, false); Assert.That(panel.Canvas.HasPendingGesture, Is.True);
            panel.Hide(); Assert.That(panel.Canvas.HasPendingGesture, Is.False); Assert.That(panel.Root.activeSelf, Is.False);
            panel.Open(notified); Assert.That(panel.Session, Is.SameAs(session));
            Assert.That(panel.Session.Snapshot.GeometryHash, Is.EqualTo(accepted)); Assert.That(panel.Session.CanUndo, Is.True);
            Assert.That(changes, Is.EqualTo(1));
            panel.Canvas.Undo(); Assert.That(changes, Is.EqualTo(2)); Assert.That(notified.Drawing.IsEmpty, Is.True);
            panel.Canvas.Redo(); Assert.That(changes, Is.EqualTo(3)); Assert.That(notified.Drawing.GeometryHash, Is.EqualTo(accepted));
        }

        [Test] public void ViewingControlsAndRepeatedRefreshNeverPublishAChange()
        {
            panel.Open(Complete());
            panel.Canvas.ShowGrid = false; panel.Canvas.SnapToGrid = true; panel.Canvas.ShowWorkingGeometry = false;
            panel.Canvas.Zoom = 1.5f; panel.Canvas.Fit(); panel.Canvas.SelectedBinding = DrawingBindings.Mount0;
            panel.ShowDetails(true); panel.ShowDetails(false); panel.ShowStencils(true); panel.ShowStencils(false);
            panel.Refresh(); panel.Refresh();
            Assert.That(changes, Is.Zero); Assert.That(store.Writes, Is.Zero);
        }
        [Test] public void StencilCatalogueBlocksCoveredEditingControlsUntilClosed()
        {
            panel.Open(Complete());var clear=Button("Guided drawing clear");Assert.That(clear.IsInteractable(),Is.True);
            panel.ShowStencils(true);panel.Refresh();
            Assert.That(clear.IsInteractable(),Is.False);Assert.That(Button("Guided drawing height larger").IsInteractable(),Is.False);
            panel.ShowStencils(false);Assert.That(clear.IsInteractable(),Is.True);Assert.That(panel.Canvas.gameObject.activeSelf,Is.True);
        }

        [Test] public void FinishAcceptsAValidPendingBarrelAndNotifiesExactlyOnce()
        {
            panel.Canvas.Tool = DrawingTool.Rectangle; Gesture(-.35f, -.35f, .35f, .35f);
            panel.SelectNextPart(); Assert.That(panel.Canvas.SelectedBinding, Is.EqualTo(DrawingBindings.Mount0));
            Gesture(-.0625f, 0, .0625f, .5f, false);
            Assert.That(panel.Session.Snapshot.StrokeCount, Is.EqualTo(1));
            Assert.That(panel.TryFinish(out var candidate, out string reason), Is.True, GameText.Resolve(reason));
            Assert.That(candidate.FreeformDesign.IsUsable, Is.True); Assert.That(candidate.Drawing.StrokeCount, Is.EqualTo(2));
            Assert.That(panel.Canvas.HasPendingGesture, Is.False); Assert.That(changes, Is.EqualTo(2));
            Assert.That(store.Writes, Is.Zero); Assert.That(panel.Root.activeSelf, Is.True);
        }

        [Test] public void RejectedPendingShapeCannotFinishAnOtherwiseValidDrawing()
        {
            panel.Open(Complete()); string original = panel.Candidate.Drawing.GeometryHash;
            panel.Canvas.Tool = DrawingTool.Rectangle; Gesture(.2f, .1f, .2f, .3f, false);
            Assert.That(panel.TryFinish(out var candidate, out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty); Assert.That(candidate, Is.Null);
            Assert.That(panel.Candidate.Drawing.GeometryHash, Is.EqualTo(original)); Assert.That(changes, Is.Zero);
        }

        [Test] public void CompleteButOverBudgetDrawingCannotFinishOrFire()
        {
            panel.Session.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.9f, -.9f), new DrawingPoint(.9f, .9f), out _);
            panel.Session.AddRectangle(DrawingBindings.Mount0, new DrawingPoint(-.2f, 0), new DrawingPoint(.2f, .9f), out _);
            panel.Refresh(); Assert.That(panel.Candidate.FreeformDesign.IsComplete, Is.True);
            Assert.That(panel.TryFinish(out _, out string reason), Is.False); Assert.That(reason, Is.Not.Empty);
            Assert.That(Button("Drawing probe fire").interactable, Is.False);
            Click("Drawing probe fire"); Assert.That(probes, Is.Zero); Assert.That(store.Writes, Is.Zero);
        }

        [Test] public void EveryMountedBarrelHasItsOwnPartAndFreeStencil()
        {
            panel.Open(Draft().WithWeaponMount(1, LegacyContentIds.Weapon(StructureKind.Gun)).WithWeaponMount(2, LegacyContentIds.Weapon(StructureKind.Gun)));
            foreach (string binding in new[] { DrawingBindings.Base, DrawingBindings.Mount0, DrawingBindings.Mount1, DrawingBindings.Mount2 })
            {
                Click("Guided drawing part " + binding); panel.ShowStencils(true);
                Click("Guided insert stencil " + (binding == DrawingBindings.Base ? "drawing.body.basic" : "drawing.barrel.basic"));
                Assert.That(panel.Session.Snapshot.GetPart(binding).Strokes.Count, Is.GreaterThan(0));
                Assert.That(panel.Canvas.gameObject.activeSelf, Is.True);
            }
            Assert.That(changes, Is.EqualTo(4)); Assert.That(panel.Candidate.FreeformDesign.IsUsable, Is.True);
            Assert.That(run.Workshop.Gold, Is.EqualTo(100)); Assert.That(store.Writes, Is.Zero);
            panel.Canvas.Undo(); Assert.That(panel.Session.Snapshot.GetPart(DrawingBindings.Mount2).Strokes.Count, Is.Zero);
        }

        [Test] public void PaidStencilRequiresSuccessfulExplicitPurchaseBeforeInsertion()
        {
            panel.Canvas.SelectedBinding = DrawingBindings.Mount0; panel.ShowStencils(true);
            string before = panel.Session.Snapshot.GeometryHash;
            Assert.That(Button("Guided insert stencil drawing.sniper").interactable, Is.False);
            Click("Guided insert stencil drawing.sniper"); Assert.That(panel.Session.Snapshot.GeometryHash, Is.EqualTo(before));
            store.Fail = true; Click("Guided buy stencil drawing.sniper");
            Assert.That(run.Workshop.Gold, Is.EqualTo(100), "Opening confirmation must not spend gold.");
            Click("Workshop confirm action");
            Assert.That(run.Workshop.Gold, Is.EqualTo(100)); Assert.That(run.Workshop.IsDrawingUnlocked("drawing.sniper"), Is.False);
            Assert.That(changes, Is.Zero);
            string failure = Text("Guided drawing stencil gold").text;
            Assert.That(failure, Does.Contain("Disk unavailable"));
            panel.Refresh();
            Assert.That(Text("Guided drawing stencil gold").text, Is.EqualTo(failure), "Refreshing the draft must not hide a failed purchase behind the gold balance.");
            store.Fail = false; Click("Guided buy stencil drawing.sniper");
            Click("Workshop confirm action");
            Assert.That(run.Workshop.Gold, Is.EqualTo(68)); Assert.That(run.Workshop.IsDrawingUnlocked("drawing.sniper"), Is.True);
            Assert.That(panel.Session.Snapshot.GeometryHash, Is.EqualTo(before)); Assert.That(changes, Is.Zero);
            Assert.That(Button("Guided insert stencil drawing.sniper").interactable, Is.True);
            Click("Guided insert stencil drawing.sniper"); Assert.That(changes, Is.EqualTo(1));
            Assert.That(panel.Session.Snapshot.GeometryHash, Is.Not.EqualTo(before));
        }

        [Test] public void ProbeUsesDraftAndInvalidatesOnEditingButNotViewing()
        {
            panel.Open(Complete()); panel.Canvas.SelectedBinding = DrawingBindings.Mount0; panel.Probe.SetDistance(4);
            Click("Drawing probe fire"); var recording = panel.Probe.Recording;
            Assert.That(recording, Is.Not.Null); Assert.That(recording.Completed, Is.True);
            panel.Canvas.ShowGrid = false; panel.Canvas.Zoom *= 1.2f; panel.ShowDetails(true); panel.ShowDetails(false);
            Assert.That(panel.Probe.Recording, Is.SameAs(recording));
            Click("Guided drawing height larger"); Assert.That(panel.Probe.Recording, Is.Null);
            Click("Drawing probe fire"); Assert.That(probed.Drawing.GeometryHash, Is.EqualTo(panel.Candidate.Drawing.GeometryHash));
            int previousCalls = probes;
            Gesture(.1f, .1f, .2f, .2f, false); Assert.That(panel.Probe.Recording, Is.Null);
            Click("Drawing probe fire"); Assert.That(probes, Is.EqualTo(previousCalls));
            Assert.That(panel.Canvas.HasPendingGesture, Is.True); Assert.That(store.Writes, Is.Zero);
            panel.Hide(); Assert.That(panel.Probe.Recording, Is.Null);
        }

        [Test] public void ExternalFootprintChangeReplacesSessionEvenWhenGeometryIsUnchanged()
        {
            panel.Open(Complete(Draft(StructureKind.Mortar))); var previous = panel.Session;
            var changed = panel.Candidate.WithMortarFootprint(0, new MortarFootprint(2));
            panel.Open(changed);
            Assert.That(panel.Session, Is.Not.SameAs(previous));
            Assert.That(panel.Candidate.GetMortarFootprint(0).AspectRatio, Is.EqualTo(2));
            Assert.That(panel.Candidate.Drawing.GeometryHash, Is.EqualTo(changed.Drawing.GeometryHash));
            Assert.That(changes, Is.Zero);
        }

        [Test] public void DetailedValuesCompareOpeningAndCurrentGameplayPreview()
        {
            var opening = Complete(); panel.Open(opening); panel.Canvas.SelectedBinding = DrawingBindings.Mount0;
            Click("Guided drawing height larger"); panel.ShowDetails(true);
            var before = run.PreviewBlueprint(opening); var after = run.PreviewBlueprint(panel.Candidate);
            string expected = GameText.Resolve(DesignChangeFeedback.Mount(before.Weapons.Single(), after.Weapons.Single()));
            Assert.That(Text("Guided drawing detailed values").text, Does.Contain(expected));
        }

        [TestCase(1552, 674)]
        [TestCase(1152, 624)]
        public void ReflowKeepsTheWorkAreaSummaryAndInputToolsInsideTheirAvailablePage(int width, int height)
        {
            panel.ReflowPage(width, height); Canvas.ForceUpdateCanvases();
            var rects = panel.Root.GetComponentsInChildren<RectTransform>(true);
            var main = rects.Single(r => r.name == "Guided drawing work area");
            var summary = rects.Single(r => r.name == "Guided drawing summary");
            var catalogue = rects.Single(r => r.name == "Guided drawing stencil catalogue");
            var page = panel.Root.GetComponent<RectTransform>();
            var available = new Rect(page.rect.xMin, page.rect.yMax - height, width, height);
            foreach (var region in new[] { main, summary, catalogue })
            {
                var bounds = BoundsIn(page, region);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(available.xMin - 1), region.name);
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(available.xMax + 1), region.name);
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(available.yMin - 1), region.name);
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(available.yMax + 1), region.name);
            }
            AssertNoOverlap(BoundsIn(page, main), BoundsIn(page, summary), "The summary must not cover the drawing work area.");
            var viewport = rects.Single(r => r.name == "Guided drawing viewport");
            foreach (var tool in panel.Root.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .Where(button => button.name.StartsWith("Guided drawing tool ")))
                AssertNoOverlap(BoundsIn(page, viewport), BoundsIn(page, (RectTransform)tool.transform), tool.name);
            Assert.That(panel.Root.GetComponent<UnityEngine.UI.Graphic>().raycastTarget, Is.False);
        }

        static Rect BoundsIn(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            var points = corners.Select(point => parent.InverseTransformPoint(point)).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
                points.Max(point => point.x), points.Max(point => point.y));
        }

        static void AssertNoOverlap(Rect first, Rect second, string message)
        {
            float width = Mathf.Min(first.xMax, second.xMax) - Mathf.Max(first.xMin, second.xMin);
            float height = Mathf.Min(first.yMax, second.yMax) - Mathf.Max(first.yMin, second.yMin);
            Assert.That(width > 1 && height > 1, Is.False, message);
        }

        sealed class Store : IWorkshopProfileStore
        {
            public int Writes;
            public bool Fail;
            public ProfileLoadResult Load()
            { var profile = new WorkshopProfile(); profile.GrantGold(100); return new ProfileLoadResult(profile, ProfileLoadSource.Primary, "Isolated", true); }
            public ProfileSaveResult Save(WorkshopProfile value)
            { Writes++; return new ProfileSaveResult(!Fail, Fail ? "Disk unavailable" : ""); }
        }
    }
}
