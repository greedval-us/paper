using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
namespace PaperDefense.Tests
{
    public sealed class WorkshopWizardTests
    {
        GameObject root,events;RunController run;WorkshopWizard wizard;Store store;
        [SetUp] public void SetUp()
        {
            root=new GameObject("Isolated guided workshop",typeof(RectTransform),typeof(Canvas));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            root.GetComponent<RectTransform>().sizeDelta=new Vector2(1600,1000);
            events=new GameObject("Isolated guided events",typeof(EventSystem));
            var host=new GameObject("Isolated guided controller");host.transform.SetParent(root.transform);host.SetActive(false);
            run=host.AddComponent<RunController>();store=new Store();run.InitializeWorkshop(store,allowLegacyFixtureDefaults:true);
            wizard=new WorkshopWizard(root.transform,run,null,_=>{},()=>{});Canvas.ForceUpdateCanvases();
        }
        [TearDown] public void TearDown(){Object.DestroyImmediate(root);Object.DestroyImmediate(events);}
        void Draw()
        {
            var edit=wizard.Drawing.Session;
            edit.AddRectangle(DrawingBindings.Base,new DrawingPoint(-.35f,-.35f),new DrawingPoint(.35f,.35f),out _);
            foreach(var mount in wizard.Draft.WeaponMounts)edit.AddRectangle(DrawingBindings.Mount(mount.SlotIndex),new DrawingPoint(-.0625f,0),new DrawingPoint(.0625f,.6f),out _);
            wizard.Drawing.Canvas.FinishStroke();
        }
        UnityEngine.UI.Button Button(string name)=>root.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name==name&&b.gameObject.activeInHierarchy);
        [Test] public void NewTowerCannotSkipDrawingOrSaveAnIncompletePage()
        {
            int count=run.Workshop.Album.Count,writes=store.Writes;
            Assert.That(wizard.Step,Is.Zero);Assert.That(wizard.Draft.IsFreeform,Is.True);Assert.That(wizard.GoToStep(3),Is.False);
            Assert.That(wizard.Next(),Is.True);Assert.That(wizard.Next(),Is.True);Assert.That(wizard.Next(),Is.False);
            Assert.That(wizard.Step,Is.EqualTo(2));Assert.That(wizard.Save(),Is.False);
            Assert.That(run.Workshop.Album.Count,Is.EqualTo(count));Assert.That(store.Writes,Is.EqualTo(writes));
        }
        [Test] public void CompleteSequenceSavesExactlyTheCurrentDrawingWithoutSpendingGold()
        {
            wizard.Next();wizard.Next();Draw();string hash=wizard.Drawing.Candidate.Drawing.GeometryHash;int gold=run.Workshop.Gold,writes=store.Writes;
            Assert.That(wizard.Next(),Is.True,wizard.Status);Assert.That(wizard.Step,Is.EqualTo(3));Assert.That(wizard.Save(),Is.True,wizard.Status);
            Assert.That(store.Writes,Is.EqualTo(writes+1));Assert.That(run.Workshop.Gold,Is.EqualTo(gold));
            Assert.That(run.Workshop.Album.Single(p=>p.Id==wizard.Draft.Id).Drawing.GeometryHash,Is.EqualTo(hash));
        }
        [Test] public void FailedSaveKeepsDraftAndReviewStepForRetry()
        {
            wizard.Next();wizard.Next();Draw();wizard.Next();string id=wizard.Draft.Id,hash=wizard.Draft.Drawing.GeometryHash;
            int count=run.Workshop.Album.Count;store.Fail=true;
            Assert.That(wizard.Save(),Is.False);Assert.That(wizard.Step,Is.EqualTo(3));Assert.That(wizard.Draft.Id,Is.EqualTo(id));
            Assert.That(wizard.Draft.Drawing.GeometryHash,Is.EqualTo(hash));Assert.That(run.Workshop.Album.Count,Is.EqualTo(count));
            store.Fail=false;Assert.That(wizard.Save(),Is.True);
        }
        [Test] public void BackAndAlbumPreserveAcceptedStrokesAndDiscardOnlyPendingGesture()
        {
            wizard.Next();wizard.Next();Draw();string hash=wizard.Drawing.Candidate.Drawing.GeometryHash;
            var pointer=new PointerEventData(events.GetComponent<EventSystem>()){button=PointerEventData.InputButton.Left,position=wizard.Drawing.Canvas.LocalPointToScreen(new Vector2(.1f,.1f))};
            wizard.Drawing.Canvas.OnPointerDown(pointer);wizard.ShowAlbum(true);Assert.That(wizard.AlbumOpen,Is.True);
            wizard.ShowAlbum(false);Assert.That(wizard.Drawing.Canvas.HasPendingGesture,Is.False);
            Assert.That(wizard.Drawing.Candidate.Drawing.GeometryHash,Is.EqualTo(hash));
            wizard.Back();wizard.Next();Assert.That(wizard.Drawing.Candidate.Drawing.GeometryHash,Is.EqualTo(hash));
        }
        [Test] public void ClosedDetailsAreNotVisibleUntilRequested()
        {
            var details=root.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(t=>t.name=="Wizard detailed values");
            Assert.That(details.gameObject.activeInHierarchy,Is.False);Button("Wizard details").onClick.Invoke();Assert.That(details.gameObject.activeInHierarchy,Is.True);
        }
        [Test] public void ExistingMortarVolleyAndArmorEditorsRemainReachable()
        {
            var bp=new TowerBlueprint("all-shapes","Shapes",ChassisKind.Heavy,StructureKind.Mortar,StructureKind.Fan,TowerModules.Armor).WithContent(run.Content);
            wizard.Load(bp);
            Button("Wizard geometry 0").onClick.Invoke();Assert.That(wizard.Mortar.IsOpen,Is.True);wizard.Mortar.Close();
            Button("Wizard geometry 1").onClick.Invoke();Assert.That(wizard.Volley.IsOpen,Is.True);wizard.Volley.Close();
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.Not.Null,"Legacy editor clears its selection before hiding; the modal must restore a usable control.");
            Button("Wizard geometry 2").onClick.Invoke();Assert.That(wizard.Armor.IsOpen,Is.True);
            wizard.Armor.Close();Assert.That(EventSystem.current.currentSelectedGameObject,Is.Not.Null);
        }
        [Test] public void SwitchingFoundationRequiresAssemblyRecheck()
        {
            wizard.Load(new TowerBlueprint("three","Three","chassis.heavy",new[]{new WeaponMountSelection(0,"weapon.gun"),new WeaponMountSelection(1,"weapon.gun"),new WeaponMountSelection(2,"weapon.gun")},content:run.Content));
            wizard.GoToStep(0);
            Assert.That(wizard.SelectChassis("chassis.standard"),Is.True);wizard.Next();Assert.That(wizard.Next(),Is.False);
            Button("Wizard mount 2").onClick.Invoke();Button("Wizard remove mount").onClick.Invoke();Assert.That(wizard.Next(),Is.True,wizard.Status);
        }
        [Test] public void EmptyNameCannotStrandTheDraftBeforeItsNameField()
        {
            wizard.Next();wizard.Next();Draw();wizard.Next();
            var field=root.GetComponentsInChildren<UnityEngine.UI.InputField>().Single();field.text=" ";field.onEndEdit.Invoke(field.text);
            Assert.That(string.IsNullOrWhiteSpace(wizard.Draft.Name),Is.False);
            wizard.Back();Assert.That(wizard.Next(),Is.True,wizard.Status);Assert.That(wizard.Step,Is.EqualTo(3));
        }
        [Test] public void AuthoredPreviewsShowTheCurrentDrawingInsteadOfTheReferenceTemplate()
        {
            wizard.Next();wizard.Next();Draw();wizard.Next();
            foreach(var sketch in root.GetComponentsInChildren<BlueprintSketch>())Assert.That(sketch.ShowReference,Is.False,sketch.name);
            wizard.Save();wizard.ShowAlbum(true);
            foreach(var sketch in root.GetComponentsInChildren<BlueprintSketch>().Where(s=>s.name=="Album thumbnail"))Assert.That(sketch.ShowReference,Is.False);
        }
        [Test] public void AlbumBlocksBackgroundKeyboardActionsAndRestoresTheirState()
        {
            var next=Button("Wizard next");events.GetComponent<EventSystem>().SetSelectedGameObject(next.gameObject);
            wizard.ShowAlbum(true);wizard.Refresh();
            Assert.That(next.IsInteractable(),Is.False);Assert.That(Button("Wizard step 0").IsInteractable(),Is.False);
            Assert.That(events.GetComponent<EventSystem>().currentSelectedGameObject.transform.IsChildOf(root.transform.Find("Wizard album overlay")),Is.True);
            wizard.ShowAlbum(false);Assert.That(next.IsInteractable(),Is.True);Assert.That(Button("Wizard step 3").IsInteractable(),Is.False);
            Assert.That(events.GetComponent<EventSystem>().currentSelectedGameObject,Is.SameAs(next.gameObject));
        }
        [Test] public void RebuildingAdditionsAndLeavingWorkshopRestoresBackgroundInput()
        {
            wizard.Next();Button("Wizard additions").onClick.Invoke();
            Assert.That(Button("Wizard next").IsInteractable(),Is.False);
            Button("Close additions").onClick.Invoke();Assert.That(Button("Wizard next").IsInteractable(),Is.True);
            Button("Wizard additions").onClick.Invoke();wizard.Suspend();wizard.Resume();
            Assert.That(root.transform.Find("Wizard additions overlay"),Is.Null);Assert.That(Button("Wizard next").IsInteractable(),Is.True);
        }
        sealed class Store:IWorkshopProfileStore
        {
            public bool Fail;public int Writes;
            public ProfileLoadResult Load(){var profile=new WorkshopProfile();profile.GrantGold(10000);return new ProfileLoadResult(profile,ProfileLoadSource.Primary,"Isolated",true);}
            public ProfileSaveResult Save(WorkshopProfile value){Writes++;return new ProfileSaveResult(!Fail,Fail?"Injected write failure":"");}
        }
    }
}
