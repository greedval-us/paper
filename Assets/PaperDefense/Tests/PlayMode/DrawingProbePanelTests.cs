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
    public sealed class DrawingProbePanelTests
    {
        GameObject root,events;DrawingEditorPanel editor;RunController run;Store store;
        int applied,calls;TowerBlueprint sent;
        [SetUp] public void SetUp()
        {
            applied=calls=0;sent=null;
            root=new GameObject("Isolated inline probe",typeof(RectTransform),typeof(Canvas));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            events=new GameObject("Isolated probe events",typeof(EventSystem));
            var host=new GameObject("Isolated probe controller");host.transform.SetParent(root.transform);host.SetActive(false);
            run=host.AddComponent<RunController>();store=new Store();run.InitializeWorkshop(store,allowLegacyFixtureDefaults:true);
            editor=new DrawingEditorPanel(root.transform,Resources.Load<Font>("NotebookHand"),_=>applied++,context:new DrawingEditorContext
            {Preview=p=>run.PreviewBlueprint(p),Workshop=()=>run.Workshop,Probe=(p,m,d,f)=>{calls++;sent=p;return run.ProbeDrawing(p,m,d,f);}});
            Open();
        }
        [TearDown] public void TearDown(){Object.DestroyImmediate(root);Object.DestroyImmediate(events);}
        void Open(bool heavy=false)
        {
            var source=new TowerBlueprint("inline","Inline",heavy?ChassisKind.Heavy:ChassisKind.Standard,StructureKind.Gun,heavy?(StructureKind?)StructureKind.Piercer:null).WithContent(run.Content).AsFreeform();
            var session=new DrawingEditSession(source,source.Drawing);
            session.AddRectangle(DrawingBindings.Base,new DrawingPoint(-.35f,-.35f),new DrawingPoint(.35f,.35f),out _);
            session.AddRectangle(DrawingBindings.Mount0,new DrawingPoint(-.0625f,0),new DrawingPoint(.0625f,.5f),out _);
            if(heavy)session.AddRectangle(DrawingBindings.Mount1,new DrawingPoint(-.0625f,0),new DrawingPoint(.0625f,.5f),out _);
            editor.Open(source.WithFreeformDrawing(session.Snapshot));editor.Canvas.SelectedBinding=DrawingBindings.Mount0;editor.ShowProbe(true);editor.Probe.SetDistance(4);Canvas.ForceUpdateCanvases();
        }
        Button Button(string name)=>root.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        void Click(string name)=>Button(name).onClick.Invoke();
        void Fire()=>Click("Drawing probe fire");
        [Test] public void RunsEditedDraftWithoutApplyingSavingOrSpending()
        {
            string initial=editor.Session.Snapshot.GeometryHash;int gold=run.Workshop.Gold,writes=store.Writes;
            var album=run.Workshop.Album.ToArray();
            Click("Drawing height larger");Fire();
            Assert.That(calls,Is.EqualTo(1));Assert.That(editor.Probe.Recording.Completed,Is.True);
            Assert.That(sent.Drawing.GeometryHash,Is.EqualTo(editor.Session.Snapshot.GeometryHash).And.Not.EqualTo(initial));
            Assert.That(applied,Is.Zero);Assert.That(store.Writes,Is.EqualTo(writes));Assert.That(run.Workshop.Gold,Is.EqualTo(gold));
            CollectionAssert.AreEqual(album,run.Workshop.Album);
            Assert.That(editor.IsOpen,Is.True);
        }
        [Test] public void EditingUndoAndConditionChangesDiscardOldResultButViewingDoesNot()
        {
            Fire();var recording=editor.Probe.Recording;Assert.That(recording,Is.Not.Null);
            editor.Canvas.ShowGrid=false;editor.Canvas.Zoom*=1.2f;Assert.That(editor.Probe.Recording,Is.SameAs(recording));
            Click("Drawing height larger");Assert.That(editor.Probe.Recording,Is.Null);Assert.That(editor.Probe.Distance,Is.EqualTo(4));
            Fire();editor.Canvas.Undo();Assert.That(editor.Probe.Recording,Is.Null);
            Fire();editor.Probe.SetDistance(5);Assert.That(editor.Probe.Recording,Is.Null);
            Fire();editor.Probe.SetFormation(DrawingProbeFormation.Column);Assert.That(editor.Probe.Recording,Is.Null);
        }
        [Test] public void PendingGestureClearsRecordingAndCannotFireOrApply()
        {
            Fire();string hash=editor.Session.Snapshot.GeometryHash;
            var pointer=new PointerEventData(events.GetComponent<EventSystem>()){button=PointerEventData.InputButton.Left,position=editor.Canvas.LocalPointToScreen(new Vector2(.1f,.1f))};
            editor.Canvas.OnPointerDown(pointer);Assert.That(editor.Probe.Recording,Is.Null);Fire();
            Assert.That(calls,Is.EqualTo(1));Assert.That(editor.Canvas.HasPendingGesture,Is.True);
            Assert.That(editor.Session.Snapshot.GeometryHash,Is.EqualTo(hash));Assert.That(applied,Is.Zero);
            editor.Canvas.CancelGesture();Fire();Assert.That(calls,Is.EqualTo(2));
        }
        [Test] public void InvalidGeometryDisablesProbeAndCannotReplayOldResult()
        {
            Fire();editor.Session.ClearPart(DrawingBindings.Base);editor.Canvas.FinishStroke();
            Assert.That(Button("Drawing probe fire").interactable,Is.False);Assert.That(editor.Probe.Recording,Is.Null);
            Fire();Assert.That(calls,Is.EqualTo(1));
            editor.Canvas.Undo();Fire();Assert.That(calls,Is.EqualTo(2));
        }
        [Test] public void MountSwitchAndCloseResetRecordingWhileTabsKeepIt()
        {
            Open(true);Fire();var recording=editor.Probe.Recording;editor.ShowProbe(false);editor.ShowProbe(true);
            Assert.That(editor.Probe.Recording,Is.SameAs(recording));
            editor.Canvas.SelectedBinding=DrawingBindings.Mount1;Assert.That(editor.Probe.Recording,Is.Null);Assert.That(editor.Probe.Mount,Is.EqualTo(1));
            Fire();Assert.That(editor.Probe.Recording.Frames.SelectMany(f=>f.Shots).All(s=>s.MountSlotIndex==1),Is.True);
            editor.Canvas.SelectedBinding=DrawingBindings.Base;Assert.That(editor.Probe.Mount,Is.EqualTo(1));
            editor.Close(false);Assert.That(editor.Probe.Recording,Is.Null);Open();Assert.That(editor.Probe.Recording,Is.Null);Assert.That(applied,Is.Zero);
        }
        sealed class Store:IWorkshopProfileStore
        {
            public int Writes;
            public ProfileLoadResult Load()=>new ProfileLoadResult(new WorkshopProfile(),ProfileLoadSource.Primary,"Isolated",true);
            public ProfileSaveResult Save(WorkshopProfile value){Writes++;return new ProfileSaveResult(true,"");}
        }
    }
}
