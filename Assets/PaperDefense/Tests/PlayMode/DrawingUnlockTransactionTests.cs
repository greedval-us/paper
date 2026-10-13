using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using Object=UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class DrawingUnlockTransactionTests
    {
        GameObject root;
        RunController Controller(IWorkshopProfileStore store)
        {
            root=new GameObject("Isolated drawing purchases");root.SetActive(false);
            var run=root.AddComponent<RunController>();run.InitializeWorkshop(store,allowLegacyFixtureDefaults:true);return run;
        }
        [TearDown] public void Cleanup(){if(root!=null)Object.DestroyImmediate(root);}
        [Test] public void FailedPurchasePublishesNothingAndSuccessfulRetrySurvivesReload()
        {
            var profile=new WorkshopProfile();profile.GrantGold(200);var store=new Store(profile){Fail=true};var run=Controller(store);
            int events=0;run.WorkshopChanged+=()=>events++;
            Assert.That(run.UnlockDrawingAid("drawing.sniper",out var reason),Is.False);Assert.That(reason,Is.Not.Empty);
            Assert.That(run.Workshop.Gold,Is.EqualTo(200));Assert.That(run.Workshop.IsDrawingUnlocked("drawing.sniper"),Is.False);Assert.That(events,Is.Zero);
            store.Fail=false;Assert.That(run.UnlockDrawingAid("drawing.sniper",out reason),Is.True,reason);
            Assert.That(run.Workshop.Gold,Is.EqualTo(168));Assert.That(events,Is.EqualTo(1));
            Assert.That(run.UnlockDrawingAid("drawing.sniper",out reason),Is.True,reason);Assert.That(run.Workshop.Gold,Is.EqualTo(168));Assert.That(events,Is.EqualTo(1));
            Object.DestroyImmediate(root);root=null;
            var reloaded=Controller(store);Assert.That(reloaded.Workshop.Gold,Is.EqualTo(168));Assert.That(reloaded.Workshop.IsDrawingUnlocked("drawing.sniper"),Is.True);
            Assert.That(reloaded.UnlockDrawingAid("drawing.missing",out _),Is.False);
        }
        [Test] public void OverBudgetPreviewCannotSaveTrialOrBuild()
        {
            var run=Controller(new Store(new WorkshopProfile()));
            var session=new DrawingEditSession(new TowerBlueprint("too.large","Large",ChassisKind.Standard,StructureKind.Gun).AsFreeform());
            Assert.That(session.AddRectangle(DrawingBindings.Base,new DrawingPoint(-.9f,-.9f),new DrawingPoint(.9f,.9f),out _),Is.True);
            Assert.That(session.AddRectangle(DrawingBindings.Mount0,new DrawingPoint(-.3f,0),new DrawingPoint(.3f,.9f),out _),Is.True);
            var tower=session.Blueprint.WithFreeformDrawing(session.Snapshot);var preview=run.PreviewBlueprint(tower);
            Assert.That(preview.FreeformDesign.IsComplete,Is.True);Assert.That(preview.FreeformDesign.IsWithinBudget,Is.False);Assert.That(preview.Valid,Is.False);
            Assert.That(preview.Weapons.Count,Is.EqualTo(1));Assert.That(preview.Weapons[0].Damage,Is.GreaterThan(0));
            Assert.That(preview.Weapons[0].FireRate,Is.EqualTo(preview.FireRate));
            Assert.That(run.SaveBlueprint(tower,out _),Is.False);Assert.That(run.RunTrial(tower,TrialScenario.Crowd),Is.Null);
            var balance=GameBalance.CreateDefault();balance.StartingInk=2000;
            var game=new GameSession(balance,starterDefenses:false);
            Assert.That(game.TryBuildBlueprint(new[]{new Cell(30,20)},tower,MaterialTier.Pencil,InkColor.None,out _),Is.False);
        }
        sealed class Store:IWorkshopProfileStore
        {
            WorkshopProfileData saved;public bool Fail;
            public Store(WorkshopProfile initial){saved=initial.ToData();}
            public ProfileLoadResult Load()=>new ProfileLoadResult(WorkshopProfile.FromData(saved),ProfileLoadSource.Primary,"Isolated store",true);
            public ProfileSaveResult Save(WorkshopProfile candidate)
            {if(Fail)return new ProfileSaveResult(false,"Injected failure");saved=JsonUtility.FromJson<WorkshopProfileData>(JsonUtility.ToJson(candidate.ToData()));return new ProfileSaveResult(true,"");}
        }
    }
}
