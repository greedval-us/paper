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
        UnityEngine.UI.Button Button(string name)=>root.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name==name&&b.gameObject.activeInHierarchy);
        string ProfileState()=>run.Workshop.Gold+";"+
            string.Join("|",run.Workshop.Album.Select(page=>JsonUtility.ToJson(new BlueprintData(page))))+";"+
            string.Join("|",run.Workshop.Loadout.Select(page=>page?.Id))+";"+
            string.Join("|",run.Workshop.Parts.Where(part=>run.Workshop.IsUnlocked(part.Id)).Select(part=>part.Id));
        void Review()
        {
            Assert.That(wizard.Next(),Is.True,wizard.Status);Assert.That(wizard.Next(),Is.True,wizard.Status);
            Assert.That(wizard.Step,Is.EqualTo(2));Assert.That(root.transform.Find("Wizard review"),Is.Not.Null);
        }
        void AssertStandard(TowerBlueprint page)
        {
            Assert.That(page.Origin,Is.EqualTo(DrawingOrigin.LegacyStandard));
            Assert.That(page.Drawing.IsEmpty,Is.True);Assert.That(page.FreeformDesign,Is.Null);
        }
        [Test] public void NewStandardTowerHasThreeStepsAndReachesReviewWithoutDrawing()
        {
            int count=run.Workshop.Album.Count,writes=store.Writes;
            Assert.That(wizard.Step,Is.Zero);AssertStandard(wizard.Draft);
            Assert.That(root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(b=>b.name.StartsWith("Wizard step ")).Select(b=>b.name),
                Is.EquivalentTo(new[]{"Wizard step 0","Wizard step 1","Wizard step 2"}));
            Assert.That(wizard.GoToStep(2),Is.False);Assert.That(wizard.GoToStep(3),Is.False);Assert.That(wizard.Save(),Is.False);
            Review();AssertStandard(wizard.Draft);
            Assert.That(wizard.Drawing==null||!wizard.Drawing.Root.activeInHierarchy,Is.True,"The normal path must never expose the old canvas.");
            Assert.That(run.Workshop.Album.Count,Is.EqualTo(count));Assert.That(store.Writes,Is.EqualTo(writes));
        }
        [Test] public void CompleteSequenceSavesStandardAssemblyWithoutStrokesOrSpendingGold()
        {
            Review();string id=wizard.Draft.Id;int gold=run.Workshop.Gold,writes=store.Writes;
            Assert.That(wizard.Save(),Is.True,wizard.Status);
            Assert.That(store.Writes,Is.EqualTo(writes+1));Assert.That(run.Workshop.Gold,Is.EqualTo(gold));
            AssertStandard(run.Workshop.Album.Single(p=>p.Id==id));
        }
        [Test] public void FailedSaveKeepsDraftAndReviewStepForRetry()
        {
            Review();string draft=JsonUtility.ToJson(new BlueprintData(wizard.Draft));
            string profile=ProfileState();int writes=store.Writes;store.Fail=true;
            Assert.That(wizard.Save(),Is.False);Assert.That(wizard.Step,Is.EqualTo(2));
            Assert.That(JsonUtility.ToJson(new BlueprintData(wizard.Draft)),Is.EqualTo(draft));
            Assert.That(ProfileState(),Is.EqualTo(profile));Assert.That(store.Writes,Is.EqualTo(writes+1));
            store.Fail=false;Assert.That(wizard.Save(),Is.True,wizard.Status);AssertStandard(wizard.Draft);
        }
        [Test] public void FailedVariantSavePreservesTheOriginalPageAndCanBeRetried()
        {
            Review();Assert.That(wizard.Save(),Is.True,wizard.Status);
            var original=wizard.Draft;string profile=ProfileState();int count=run.Workshop.Album.Count;
            store.Fail=true;Assert.That(wizard.SaveVariant(),Is.False);
            Assert.That(wizard.Draft,Is.SameAs(original));Assert.That(ProfileState(),Is.EqualTo(profile));
            Assert.That(wizard.Step,Is.EqualTo(2));AssertStandard(wizard.Draft);
            store.Fail=false;Assert.That(wizard.SaveVariant(),Is.True,wizard.Status);
            Assert.That(wizard.Draft.Id,Is.Not.EqualTo(original.Id));Assert.That(run.Workshop.Album.Count,Is.EqualTo(count+1));
            Assert.That(JsonUtility.ToJson(new BlueprintData(run.Workshop.Album.Single(p=>p.Id==original.Id))),
                Is.EqualTo(JsonUtility.ToJson(new BlueprintData(original))));
        }
        [Test] public void BackAndAlbumPreserveTheStandardAssembly()
        {
            Assert.That(wizard.Next(),Is.True);Assert.That(wizard.SelectWeapon(1,"weapon.gun"),Is.True);
            Assert.That(wizard.Next(),Is.True,wizard.Status);string before=JsonUtility.ToJson(new BlueprintData(wizard.Draft));
            wizard.ShowAlbum(true);Assert.That(wizard.AlbumOpen,Is.True);wizard.ShowAlbum(false);
            Assert.That(wizard.Step,Is.EqualTo(2));wizard.Back();Assert.That(wizard.Step,Is.EqualTo(1));
            Assert.That(wizard.Next(),Is.True,wizard.Status);
            Assert.That(JsonUtility.ToJson(new BlueprintData(wizard.Draft)),Is.EqualTo(before));AssertStandard(wizard.Draft);
            Assert.That(wizard.Drawing==null||!wizard.Drawing.Root.activeInHierarchy,Is.True);
        }
        [Test] public void ClosedDetailsAreNotVisibleUntilRequested()
        {
            Review();var details=root.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(t=>t.name=="Wizard detailed values");
            Assert.That(details.gameObject.activeInHierarchy,Is.False);Button("Wizard details").onClick.Invoke();Assert.That(details.gameObject.activeInHierarchy,Is.True);
        }
        [Test] public void StandardReviewDoesNotOfferFreeformMortarVolleyOrArmorEditors()
        {
            wizard.Load(new TowerBlueprint("all-shapes","Shapes",ChassisKind.Heavy,StructureKind.Mortar,StructureKind.Fan,TowerModules.Armor).WithContent(run.Content));
            Assert.That(wizard.Step,Is.EqualTo(2));
            Assert.That(root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Any(button=>button.gameObject.activeInHierarchy&&button.name.StartsWith("Wizard geometry ")),Is.False);
            Assert.That(wizard.Mortar==null||!wizard.Mortar.IsOpen,Is.True);
            Assert.That(wizard.Volley==null||!wizard.Volley.IsOpen,Is.True);
            Assert.That(wizard.Armor==null||!wizard.Armor.IsOpen,Is.True);
        }
        [Test] public void SwitchingFoundationRequiresAssemblyRecheck()
        {
            wizard.Load(new TowerBlueprint("three","Three","chassis.heavy",new[]{new WeaponMountSelection(0,"weapon.gun"),new WeaponMountSelection(1,"weapon.gun"),new WeaponMountSelection(2,"weapon.gun")},content:run.Content));
            Assert.That(wizard.GoToStep(0),Is.True);
            Assert.That(wizard.SelectChassis("chassis.standard"),Is.True);AssertStandard(wizard.Draft);
            Assert.That(wizard.Next(),Is.True);Assert.That(wizard.Next(),Is.False);
            Button("Wizard mount 2").onClick.Invoke();Button("Wizard remove mount").onClick.Invoke();
            Assert.That(wizard.Next(),Is.True,wizard.Status);Assert.That(wizard.Step,Is.EqualTo(2));AssertStandard(wizard.Draft);
        }
        [Test] public void EmptyNameCannotStrandTheDraftBeforeItsNameField()
        {
            Review();var field=root.GetComponentsInChildren<UnityEngine.UI.InputField>().Single();field.text=" ";field.onEndEdit.Invoke(field.text);
            Assert.That(string.IsNullOrWhiteSpace(wizard.Draft.Name),Is.False);
            wizard.Back();Assert.That(wizard.Next(),Is.True,wizard.Status);Assert.That(wizard.Step,Is.EqualTo(2));AssertStandard(wizard.Draft);
        }
        [Test] public void StandardPreviewsUseTheSameAtlasCompositionAsReviewWithoutCreatingStrokes()
        {
            Review();
            foreach(string preview in new[]{"Wizard assembled thumbnail","Wizard tower preview"})
                Assert.That(root.GetComponentsInChildren<BlueprintSketch>(true).Single(sketch=>sketch.name==preview).ShowReference,Is.False,preview);
            Assert.That(wizard.Save(),Is.True,wizard.Status);wizard.ShowAlbum(true);AssertStandard(wizard.Draft);
            AssertStandard(run.Workshop.Album.Single(page=>page.Id==wizard.Draft.Id));
        }
        [Test] public void AlbumBlocksBackgroundKeyboardActionsAndRestoresTheirState()
        {
            var next=Button("Wizard next");events.GetComponent<EventSystem>().SetSelectedGameObject(next.gameObject);
            wizard.ShowAlbum(true);wizard.Refresh();
            Assert.That(next.IsInteractable(),Is.False);Assert.That(Button("Wizard step 0").IsInteractable(),Is.False);
            Assert.That(events.GetComponent<EventSystem>().currentSelectedGameObject.transform.IsChildOf(root.transform.Find("Wizard album overlay")),Is.True);
            wizard.ShowAlbum(false);Assert.That(next.IsInteractable(),Is.True);Assert.That(Button("Wizard step 2").IsInteractable(),Is.False);
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
