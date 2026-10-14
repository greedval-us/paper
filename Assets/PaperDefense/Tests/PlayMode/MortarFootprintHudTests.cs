using System;
using System.IO;
using System.Linq;
using PaperDefense.Application;
using UnityEngine.UI;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
namespace PaperDefense.Tests
{
    public sealed class MortarFootprintHudTests
    {
        GameObject root;
        MortarFootprintPanel panel;
        TowerBlueprint draft;
        [SetUp] public void Setup()
        {
            root = new GameObject("Isolated footprint HUD", typeof(RectTransform), typeof(Canvas));
            draft = TowerBlueprint.NewDraft("probe", "Probe", ChassisKind.Heavy, StructureKind.Mortar, StructureKind.Mortar);
            panel = new MortarFootprintPanel(root.transform, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), value => draft = value,
                (value,mount,a,b,formation) => MortarFootprintProbe.Compare(value,mount,a,b,GameBalance.CreateDefault(),formation), value =>
                { TowerCatalog.Validate(value,GameBalance.CreateDefault(),out string reason); return reason; });
        }
        [TearDown] public void Cleanup() => UnityEngine.Object.DestroyImmediate(root);
        [Test] public void TransversePresetTrialsBesideDrawingAndUndoRestoresCircle()
        {
            panel.Open(draft,0);
            var button=root.GetComponentsInChildren<Button>(true).SingleOrDefault(b=>b.name=="Footprint transverse");
            Assert.That(button,Is.Not.Null,"The free contour editor must offer a transverse starting shape.");
            button.onClick.Invoke();
            Assert.That(draft.FirstFootprint.IsContour,Is.True);
            Assert.That(draft.SecondFootprint.IsCircle,Is.True);
            Assert.That(panel.Recording,Is.Not.Null);
            Assert.That(panel.Recording.Second.Completed,Is.True);
            Assert.That(panel.Baseline.IsCircle,Is.True);
            var transverse=draft.FirstFootprint;
            panel.UseEllipseTool();Assert.That(draft.FirstFootprint.IsContour,Is.False);
            panel.Undo();Assert.That(draft.FirstFootprint.SameShape(transverse),Is.True);
            Assert.That(panel.Canvas.gameObject.activeSelf,Is.True);
            panel.UseFreehandTool();
            root.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Footprint undo").onClick.Invoke();
            Assert.That(draft.FirstFootprint.IsCircle,Is.True);
        }
        void Drag(float y, MortarFootprintPanel selected = null)
        {
            var activePanel = selected ?? panel;
            activePanel.UseEllipseTool();
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left, position = activePanel.Axis.LocalPointToScreen(activePanel.Axis.Endpoint) };
            activePanel.Axis.OnPointerDown(pointer);
            pointer.position = activePanel.Axis.LocalPointToScreen(new Vector2(0,y)); activePanel.Axis.OnDrag(pointer);
            activePanel.Axis.OnPointerUp(pointer);
        }
        void DrawContour(params Point2[] points)
        {
            Canvas.ForceUpdateCanvases();
            var pointer=new PointerEventData(null){button=PointerEventData.InputButton.Left,pointerId=11,position=panel.Canvas.LocalPointToScreen(points[0])};
            panel.Canvas.OnPointerDown(pointer);Assert.That(panel.IsDragging,Is.True);Assert.That(panel.Recording,Is.Null);
            for(int i=1;i<points.Length;i++){pointer.position=panel.Canvas.LocalPointToScreen(points[i]);panel.Canvas.OnDrag(pointer);}
            panel.Canvas.OnPointerUp(pointer);
        }
        [Test] public void MouseContourAppliesNormalizedShapeAndInvalidStrokeClearsTrialWithoutChangingDraft()
        {
            panel.Open(draft,0);var drawing=draft.Drawing;float quality=draft.Assessment.Quality;
            DrawContour(new Point2(-1,-.5f),new Point2(1,-.5f),new Point2(1,.5f),new Point2(-1,.5f),new Point2(-1,-.5f));
            Assert.That(draft.FirstFootprint.IsContour,Is.True);Assert.That(panel.Recording,Is.Not.Null);
            Assert.That(panel.Working.DisplayedPoints.Count,Is.EqualTo(4));Assert.That(draft.Drawing,Is.SameAs(drawing));Assert.That(draft.Assessment.Quality,Is.EqualTo(quality));
            var accepted=draft.FirstFootprint;
            DrawContour(new Point2(-1,-1),new Point2(1,1),new Point2(-1,1),new Point2(1,-1),new Point2(-1,-1));
            Assert.That(panel.Assessment.Issue,Is.EqualTo(FootprintContourIssue.Intersection));Assert.That(panel.Recording,Is.Null);
            Assert.That(draft.FirstFootprint,Is.SameAs(accepted));
            root.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Footprint repeat").onClick.Invoke();
            Assert.That(panel.Recording,Is.Null,"Invalid contour must never replay the last valid shape.");
            Assert.That(root.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Footprint shape feedback").text,Does.Contain("пересекаются"));
            panel.Undo();Assert.That(draft.FirstFootprint,Is.SameAs(accepted));Assert.That(panel.Recording,Is.Not.Null);
        }
        [Test] public void OpenContourCanBeExplicitlyClosedAndMountSwitchCancelsPendingStroke()
        {
            panel.Open(draft,0);
            DrawContour(new Point2(-1,-1),new Point2(1,-1),new Point2(1,1),new Point2(-1,1));
            Assert.That(panel.Assessment.Issue,Is.EqualTo(FootprintContourIssue.Open));Assert.That(draft.FirstFootprint.IsCircle,Is.True);
            root.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Footprint close contour").onClick.Invoke();
            Assert.That(draft.FirstFootprint.IsContour,Is.True);Assert.That(panel.Recording,Is.Not.Null);
            var pointer=new PointerEventData(null){button=PointerEventData.InputButton.Left,position=panel.Canvas.LocalPointToScreen(new Point2(-1,-1))};
            panel.Canvas.OnPointerDown(pointer);Assert.That(panel.Canvas.HasPendingGesture,Is.True);
            panel.Open(draft,1);panel.Canvas.OnPointerUp(pointer);
            Assert.That(panel.Canvas.HasPendingGesture,Is.False);Assert.That(draft.SecondFootprint.IsCircle,Is.True);Assert.That(panel.Recording,Is.Not.Null);
        }
        [Test] public void EscOrLeavingCanvasCancelsOnlyPendingLine()
        {
            panel.Open(draft,0);var accepted=draft.FirstFootprint;
            var pointer=new PointerEventData(null){button=PointerEventData.InputButton.Left,position=panel.Canvas.LocalPointToScreen(new Point2(-1,-1))};
            panel.Canvas.OnPointerDown(pointer);panel.Canvas.CancelGesture();panel.Canvas.OnPointerUp(pointer);
            Assert.That(draft.FirstFootprint,Is.SameAs(accepted));Assert.That(panel.IsDragging,Is.False);
            panel.Canvas.OnPointerDown(pointer);pointer.position=panel.Canvas.LocalPointToScreen(new Point2(50,50));panel.Canvas.OnDrag(pointer);
            Assert.That(panel.Canvas.HasPendingGesture,Is.False);Assert.That(draft.FirstFootprint,Is.SameAs(accepted));
            panel.Close();Assert.That(panel.Recording,Is.Null);
        }
        [TestCase(2e38f)] [TestCase(float.MaxValue)] public void LargeFiniteImportedContourRemainsVisibleAndEditable(float coordinate)
        {
            var shape=MortarFootprint.FromContour(new[]{new Point2(-coordinate,-coordinate),new Point2(coordinate,-coordinate),new Point2(coordinate,coordinate),new Point2(-coordinate,coordinate)});
            panel.Open(draft.WithMortarFootprint(0,shape),0);Canvas.ForceUpdateCanvases();
            var center=panel.Canvas.LocalPointToScreen(default);
            Assert.That(Vector2.Distance(center,panel.Canvas.LocalPointToScreen(new Point2(coordinate,0))),Is.GreaterThan(100));
            DrawContour(new Point2(-coordinate*.5f,-coordinate*.5f),new Point2(coordinate*.5f,-coordinate*.5f),new Point2(coordinate*.5f,coordinate*.5f),new Point2(-coordinate*.5f,coordinate*.5f),new Point2(-coordinate*.5f,-coordinate*.5f));
            Assert.That(panel.Assessment.Valid,Is.True);Assert.That(panel.Recording,Is.Not.Null);
        }
        [Test] public void WorkshopControlsPreserveAuthoredArtThroughVariantAndTemporaryStore()
        {
            string folder = Path.Combine(Path.GetTempPath(), "PaperFootprintUI-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); string path = Path.Combine(folder,"profile.json");
            var host = new GameObject("Isolated footprint workshop"); host.SetActive(false);
            try
            {
                var store = new WorkshopProfileStore(path); var initial = new WorkshopProfile(); initial.GrantGold(1000);
                Assert.That(initial.Unlock("chassis.heavy",out var unlockReason),Is.True,unlockReason); Assert.That(store.Save(initial).Success,Is.True);
                var run = host.AddComponent<RunController>(); run.InitializeWorkshop(store, allowLegacyFixtureDefaults: true);
                var hud = host.AddComponent<WorkshopHud>(); hud.Initialize(run);
                Action<string> click = name => host.GetComponentsInChildren<Button>(true).Single(b => b.name == name).onClick.Invoke();
                click("New blueprint"); click("Chassis Heavy"); click("First Mortar"); click("Second Mortar"); click("Open drawing editor");
                Assert.That(hud.DrawingEditor.Session.AddStroke(DrawingPartKey.Mount0,new[] { new DrawingPoint(0,0),new DrawingPoint(0,.4f) },out _), Is.True);
                FreeformUiTestDrawing.Complete(hud.DrawingEditor.Session); click("Apply drawing"); click("Save blueprint"); var source = hud.Draft;
                Assert.That(run.Workshop.Album.Any(p=>p.Id==source.Id),Is.True,hud.Status);
                Assert.That(source.Drawing.IsEmpty, Is.False);
                click("Mortar footprint 0"); Assert.That(hud.MortarFootprintEditor.IsOpen, Is.True);
                Drag(10000,hud.MortarFootprintEditor); hud.MortarFootprintEditor.Close();
                click("Mortar footprint 1"); Drag(10000,hud.MortarFootprintEditor); hud.MortarFootprintEditor.Close();
                click("Save blueprint as new"); var variant = hud.Draft;
                Assert.That(variant.Id, Is.Not.EqualTo(source.Id));
                Assert.That(variant.Drawing.GeometryHash, Is.EqualTo(source.Drawing.GeometryHash));
                Assert.That(variant.Origin, Is.EqualTo(source.Origin));
                Assert.That(variant.Assessment.Quality, Is.EqualTo(source.Assessment.Quality));
                var stored = new WorkshopProfileStore(path).Load().Profile.Album.Single(p => p.Id == variant.Id);
                Assert.That(stored.FirstFootprint.AspectRatio, Is.EqualTo(3)); Assert.That(stored.SecondFootprint.AspectRatio, Is.EqualTo(3));
                Assert.That(stored.Drawing.GeometryHash, Is.EqualTo(source.Drawing.GeometryHash));
                Assert.That(stored.Origin, Is.EqualTo(source.Origin)); Assert.That(stored.Assessment.Quality, Is.EqualTo(source.Assessment.Quality));
                click("Mortar footprint 1"); click("First Gun"); Assert.That(hud.MortarFootprintEditor.IsOpen, Is.False);
                Assert.That(run.Workshop.Album.Single(p => p.Id == source.Id).FirstFootprint.AspectRatio, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                Assert.That(Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase), Is.True);
                Directory.Delete(folder,true);
            }
        }
        [TestCase(10)]
        [TestCase(8)]
        public void OverloadedDraftExplainsUnavailableProbeClearsRecordingAndRecovers(int capacity)
        {
            var host = new GameObject("Isolated invalid footprint workshop");
            host.SetActive(false);
            var balanceAsset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            balanceAsset.construction.StandardCapacity = capacity;
            // Keep the armored starter valid while testing the unchanged load of one versus two bare mortars.
            balanceAsset.construction.ArmorLoad = 0;
            try
            {
                var run = host.AddComponent<RunController>();
                run.balanceAsset = balanceAsset;
                run.InitializeWorkshop(false);
                var hud = host.AddComponent<WorkshopHud>();
                hud.Initialize(run);
                Action<string> click = name => host.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == name).onClick.Invoke();
                click("New blueprint");
                click("First Mortar");
                click("Open drawing editor");
                FreeformUiTestDrawing.Complete(hud.DrawingEditor.Session, .224f);
                hud.DrawingEditor.Close(true);
                click("Mortar footprint 0");
                Assert.That(hud.MortarFootprintEditor.Recording, Is.Not.Null);
                hud.MortarFootprintEditor.Close();

                click("Second Mortar");
                click("Open drawing editor");
                FreeformUiTestDrawing.Complete(hud.DrawingEditor.Session, .224f);
                hud.DrawingEditor.Close(true);
                var invalidPreview = run.PreviewBlueprint(hud.Draft);
                Assert.That(invalidPreview.Valid, Is.False);
                string reason = TextResourcesAsset.Default.Resolve(invalidPreview.Reason);
                Assert.That(reason, Does.Contain("нагрузки"));
                Assert.That(invalidPreview.Capacity, Is.EqualTo(capacity));
                Assert.DoesNotThrow(() => click("Mortar footprint 0"));
                Assert.That(hud.MortarFootprintEditor.IsOpen, Is.True);
                Assert.That(hud.MortarFootprintEditor.Recording, Is.Null);
                Assert.That(host.GetComponentsInChildren<Text>(true).Any(text =>
                    text.gameObject.activeSelf && text.transform.parent.gameObject.activeSelf && text.text.Contains(reason)), Is.True);
                Assert.DoesNotThrow(() => click("Footprint repeat"));
                Assert.DoesNotThrow(() => click("Footprint column"));
                Assert.DoesNotThrow(() => click("Footprint circle"));
                Assert.That(hud.MortarFootprintEditor.Recording, Is.Null);

                hud.MortarFootprintEditor.Close();
                click("Second None");
                click("Mortar footprint 0");
                Assert.That(hud.MortarFootprintEditor.Recording, Is.Not.Null);
                Assert.That(hud.MortarFootprintEditor.Recording.First.Completed, Is.True);
                var feedback = host.GetComponentsInChildren<Text>(true)
                    .Single(text => text.name == "Footprint probe feedback");
                Assert.That(feedback.text, Does.Contain("поражено"));
                Assert.That(feedback.text, Does.Contain("вне отпечатка"));
                Assert.That(feedback.text, Does.Contain("цена"));
                Assert.That(feedback.text, Does.Not.Contain("качество"));
                Assert.That(feedback.text.IndexOf('\uFFFD'), Is.EqualTo(-1),
                    "Visible feedback must contain no replacement code points.");
                Assert.That(hud.MortarFootprintEditor.PlaybackSeconds, Is.LessThan(.1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(balanceAsset);
            }
        }
        [Test] public void BlankNameDraftExplainsUnavailableProbeAndRecoversAfterRename()
        {
            var host = new GameObject("Isolated unnamed footprint workshop");
            host.SetActive(false);
            try
            {
                var run = host.AddComponent<RunController>();
                run.InitializeWorkshop(false, allowLegacyFixtureDefaults: true);
                var hud = host.AddComponent<WorkshopHud>();
                hud.Initialize(run);
                Action<string> click = name => host.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == name).onClick.Invoke();
                click("New blueprint");
                click("First Mortar");
                click("Open drawing editor");
                FreeformUiTestDrawing.Complete(hud.DrawingEditor.Session);
                hud.DrawingEditor.Close(true);
                var input = host.GetComponentsInChildren<InputField>(true).Single(field => field.name == "Blueprint name");
                input.SetTextWithoutNotify("");
                input.onEndEdit.Invoke("");
                string reason = TextResourcesAsset.Default.Resolve(run.PreviewBlueprint(hud.Draft).Reason);
                Assert.That(reason, Is.Not.Empty);
                Assert.DoesNotThrow(() => click("Mortar footprint 0"));
                Assert.That(hud.MortarFootprintEditor.Recording, Is.Null);
                Assert.That(host.GetComponentsInChildren<Text>(true).Single(text => text.name == "Footprint probe feedback").text, Does.Contain(reason));
                hud.MortarFootprintEditor.Close();
                input.SetTextWithoutNotify("Restored name");
                input.onEndEdit.Invoke("Restored name");
                click("Mortar footprint 0");
                Assert.That(hud.MortarFootprintEditor.Recording, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
        [Test] public void LegacyVariantKeepsOriginAndPeriodicHudRefreshDoesNotCancelDrag()
        {
            var host = new GameObject("Isolated legacy footprint workshop"); host.SetActive(false);
            try
            {
                var run=host.AddComponent<RunController>();run.InitializeWorkshop(false, allowLegacyFixtureDefaults: true);
                var hud=host.AddComponent<WorkshopHud>();hud.Initialize(run);
                var source=hud.Draft;
                host.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Save blueprint as new").onClick.Invoke();
                Assert.That(hud.Draft.Origin, Is.EqualTo(source.Origin));
                Assert.That(hud.Draft.Id, Is.Not.EqualTo(source.Id));
                var mortar=run.Workshop.Loadout[3]; hud.MortarFootprintEditor.Open(mortar,0);
                hud.MortarFootprintEditor.UseEllipseTool();
                var axis=hud.MortarFootprintEditor.Axis;
                var pointer=new PointerEventData(null) {button=PointerEventData.InputButton.Left,position=axis.LocalPointToScreen(axis.Endpoint)};
                axis.OnPointerDown(pointer);
                typeof(WorkshopHud).GetMethod("Refresh",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(hud,null);
                Assert.That(hud.MortarFootprintEditor.IsDragging,Is.True);
                Assert.That(hud.MortarFootprintEditor.Recording,Is.Null);
                axis.OnPointerUp(pointer);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }        [Test] public void PointerClampsBothMortarsIndependentlyAndKeepsBaselineFrozen()
        {
            panel.Open(draft,0); Assert.That(panel.IsOpen, Is.True); Assert.That(panel.Axis.Endpoint.y, Is.GreaterThan(panel.Axis.rectTransform.rect.center.y));
            Drag(10000); Assert.That(draft.FirstFootprint.AspectRatio, Is.EqualTo(3)); Assert.That(draft.SecondFootprint.AspectRatio, Is.EqualTo(1));
            Assert.That(panel.Baseline.AspectRatio, Is.EqualTo(1)); panel.PinBaseline(); Assert.That(panel.Baseline.AspectRatio, Is.EqualTo(3));
            panel.Open(draft,1); Drag(10000); Assert.That(draft.SecondFootprint.AspectRatio, Is.EqualTo(3));
            Drag(-10000); Assert.That(draft.SecondFootprint.AspectRatio, Is.EqualTo(1));
        }
        [Test] public void DragStopsOldRecordingThenReleaseStartsFreshRecordingAndCloseCancels()
        {
            panel.Open(draft,0); var old = panel.Recording; Assert.That(old, Is.Not.Null);
            panel.UseEllipseTool();
            Canvas.ForceUpdateCanvases(); var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left, position = panel.Axis.LocalPointToScreen(panel.Axis.Endpoint) };
            panel.Axis.OnPointerDown(pointer); Assert.That(panel.IsDragging, Is.True); Assert.That(panel.Recording, Is.Null);
            pointer.position = panel.Axis.LocalPointToScreen(new Vector2(0,200)); panel.Axis.OnDrag(pointer);
            Assert.That(draft.FirstFootprint.AspectRatio, Is.EqualTo(1), "Draft commits only when gesture ends.");
            panel.Axis.OnPointerUp(pointer); Assert.That(panel.Recording, Is.Not.Null.And.Not.SameAs(old)); Assert.That(panel.PlaybackSeconds, Is.LessThan(.1f));
            pointer.position = panel.Axis.LocalPointToScreen(panel.Axis.Endpoint);
            panel.Axis.OnPointerDown(pointer); Assert.That(panel.IsDragging, Is.True); panel.Close(); panel.Axis.OnPointerUp(pointer); Assert.That(panel.IsOpen, Is.False);
            panel.Open(draft,0); Assert.That(panel.Axis.AspectRatio, Is.EqualTo(draft.FirstFootprint.AspectRatio));
        }
    }
}










