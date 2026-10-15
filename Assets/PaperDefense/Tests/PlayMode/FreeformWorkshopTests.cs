using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class FreeformWorkshopTests
    {
        static TowerBlueprint Drawing()
        {
            var body=new DrawingStroke(new[]{new DrawingPoint(-.4f,-.35f),new DrawingPoint(.4f,-.35f),new DrawingPoint(.4f,.35f),new DrawingPoint(-.4f,.35f),new DrawingPoint(-.4f,-.35f)});
            var barrel=new DrawingStroke(new[]{new DrawingPoint(-.1f,0),new DrawingPoint(.1f,0),new DrawingPoint(.1f,.8f),new DrawingPoint(-.1f,.8f),new DrawingPoint(-.1f,0)});
            return TowerBlueprint.NewDraft("freeform.integration","Своя башня",ChassisKind.Standard,StructureKind.Gun)
                .WithFreeformDrawing(new DrawingSnapshot(new[]{new DrawingPart(DrawingBindings.Base,new[]{body}),new DrawingPart(DrawingBindings.Mount0,new[]{barrel})}));
        }
        [Test] public void JsonProfileRoundTripPreservesGeometryRulesLoadoutAndStatistics()
        {
            var profile=new WorkshopProfile();var blueprint=Drawing();
            Assert.That(profile.SaveBlueprint(blueprint,out var reason),Is.True,reason);
            Assert.That(profile.AssignSlot(0,blueprint.Id,out reason),Is.True,reason);
            string json=JsonUtility.ToJson(profile.ToData());
            var data=JsonUtility.FromJson<WorkshopProfileData>(json);
            Assert.That(data.Version,Is.EqualTo(7));
            var restored=WorkshopProfile.FromData(data).GetLoadoutBlueprints()[0];
            Assert.That(restored.IsFreeform,Is.True);
            Assert.That(restored.Drawing.GeometryHash,Is.EqualTo(blueprint.Drawing.GeometryHash));
            Assert.That(restored.FreeformDesign.BodyArea,Is.EqualTo(blueprint.FreeformDesign.BodyArea));
            var balance=GameBalance.CreateDefault();
            Assert.That(TowerCatalog.Cost(restored,balance,MaterialTier.Pencil),Is.EqualTo(TowerCatalog.Cost(blueprint,balance,MaterialTier.Pencil)));
            Assert.That(TowerCatalog.WeaponDamage(restored,balance,restored.FirstWeaponId,MaterialTier.Pencil),Is.EqualTo(TowerCatalog.WeaponDamage(blueprint,balance,blueprint.FirstWeaponId,MaterialTier.Pencil)));
        }
        [Test] public void WorkshopPreviewTrialAndDeployedTowerShareOneFreeformDesign()
        {
            var root=new GameObject("Isolated freeform application");root.SetActive(false);
            try
            {
                var run=root.AddComponent<RunController>();run.InitializeWorkshop(false,allowLegacyFixtureDefaults:true);
                var blueprint=Drawing();var preview=run.PreviewBlueprint(blueprint);
                Assert.That(preview.Valid,Is.True,preview.Reason);
                Assert.That(run.SaveBlueprint(blueprint,out var reason),Is.True,reason);
                Assert.That(run.AssignBlueprint(0,blueprint.Id,out reason),Is.True,reason);
                int gold=run.Workshop.Gold;long tick=run.Session.TickIndex;
                var trial=run.RunTrial(blueprint,TrialScenario.Crowd);
                Assert.That(trial.First.Frames[0].Towers[0].Blueprint.IsFreeform,Is.True);
                Assert.That(trial.First.Frames[0].Towers[0].MaxHealth,Is.EqualTo(preview.Health));

                Assert.That(run.Workshop.Gold,Is.EqualTo(gold));Assert.That(run.Session.TickIndex,Is.EqualTo(tick));
                Assert.That(run.StartRun(out reason),Is.True,reason);
                int ink=run.Session.Ink;
                Assert.That(run.BuildBlueprint(new[]{new Cell(30,20)},0,MaterialTier.Pencil,InkColor.None,out reason),Is.True,reason);
                Assert.That(ink-run.Session.Ink,Is.EqualTo(preview.Cost));
                Assert.That(run.Session.Structures[0].MaxHealth,Is.EqualTo(preview.Health));
                Assert.That(run.Session.Structures[0].Blueprint.IsFreeform,Is.True);
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
