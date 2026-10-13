using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class DrawingWorkshopRulesTests
    {
        static DrawingStroke Box(float left,float bottom,float right,float top) => new DrawingStroke(new[]{new DrawingPoint(left,bottom),new DrawingPoint(right,bottom),new DrawingPoint(right,top),new DrawingPoint(left,top),new DrawingPoint(left,bottom)});
        static TowerBlueprint Tower(float halfBody=.4f,float width=.2f,float length=.6f) => new TowerBlueprint("ink.test","Ink test",ChassisKind.Standard,StructureKind.Gun,drawing:new DrawingSnapshot(new[]{new DrawingPart(DrawingBindings.Base,new[]{Box(-halfBody,-halfBody,halfBody,halfBody)}),new DrawingPart(DrawingBindings.Mount0,new[]{Box(-width/2,0,width/2,length)})}),origin:DrawingOrigin.Freeform);
        [Test] public void FullCanvasFunctionalGeometryCannotBeSavedOrBuilt()
        {
            var tower=Tower(.98f,1.9f,.98f);
            Assert.That(tower.FreeformDesign.IsComplete,Is.True);
            Assert.That(TowerCatalog.Validate(tower,out var reason),Is.False,"Complete geometry must still obey construction ink allowance");
            Assert.That(reason,Is.Not.Empty);
            Assert.That(new WorkshopProfile().SaveBlueprint(tower,out _),Is.False);
        }
        static ContentCatalogInput Input()
        {
            var input=LegacyContentCatalog.CreateInput(GameBalance.CreateDefault());
            input.Templates.Add(new DrawingTemplateDefinitionInput{Id="test.drawing",DisplayName="Drawing",Weight=0});
            foreach(var part in input.Chassis)part.TemplateId="test.drawing";
            foreach(var part in input.Weapons)part.TemplateId="test.drawing";
            foreach(var part in input.Modules)part.TemplateId="test.drawing";
            return input;
        }
        [Test] public void BudgetUsesGeometryAndIgnoresDecorationAndRetracing()
        {
            var tower=Tower(.5f,.25f,.5f);var design=tower.FreeformDesign;
            Assert.That(design.InkUsed,Is.EqualTo(71.25f).Within(.01f));
            var duplicated=tower.WithFreeformDrawing(new DrawingSnapshot(new[]{new DrawingPart(DrawingBindings.Base,new[]{tower.Drawing.GetPart(DrawingBindings.Base).Strokes[0],tower.Drawing.GetPart(DrawingBindings.Base).Strokes[0]}),tower.Drawing.GetPart(DrawingBindings.Mount0),new DrawingPart(DrawingBindings.Decoration,new[]{Box(-1,-1,1,1)})}));
            Assert.That(duplicated.FreeformDesign.InkUsed,Is.EqualTo(design.InkUsed));
            Assert.That(Tower(.75f,.25f,.5f).FreeformDesign.InkUsed,Is.GreaterThan(design.InkUsed));
            Assert.That(Tower(.5f,.5f,.5f).FreeformDesign.InkUsed,Is.GreaterThan(design.InkUsed));
            Assert.That(Tower(.5f,.25f,.75f).FreeformDesign.InkUsed,Is.GreaterThan(design.InkUsed));
            Assert.That(tower.WithWeaponMount(1,tower.FirstWeaponId).FreeformDesign.InkBudget,Is.EqualTo(100));
            Assert.That(tower.WithWeaponMount(1,tower.FirstWeaponId).WithWeaponMount(2,tower.FirstWeaponId).FreeformDesign.InkBudget,Is.EqualTo(135));
        }
        [Test] public void ConfigSnapshotsAndCopiesOwnRulesAndStencilCoordinates()
        {
            var input=Input();var snapshot=input.CreateSnapshot();var copy=input.CreateCopy();
            input.FreeformRules.BaseInkBudget=1;input.DrawingUnlocks[0].Contours[0].Points[0].X=.9f;
            Assert.That(Tower().WithContent(snapshot).FreeformDesign.IsWithinBudget,Is.True);
            Assert.That(copy.FreeformRules.BaseInkBudget,Is.EqualTo(65));
            Assert.That(snapshot.DrawingUnlocks[0].Strokes[0].Points[0].X,Is.LessThan(0));
            Assert.That(copy.DrawingUnlocks[0].Contours[0].Points[0].X,Is.LessThan(0));
        }
        [Test] public void SaturatingEveryBonusExceedsBudgetButEveryStarterStencilFits()
        {
            Assert.That(Tower(.45f,.5f,.9f).FreeformDesign.IsWithinBudget,Is.False);
            var content=Input().CreateSnapshot();var tower=Tower().WithContent(content);
            foreach(var definition in content.DrawingUnlocks)
            {
                if(definition.Role!=DrawingStencilRole.Barrel)continue;
                var session=new DrawingEditSession(tower);Assert.That(session.ReplaceFromStencil(DrawingBindings.Base,content.DrawingUnlocks[0],out _),Is.True);
                Assert.That(session.ReplaceFromStencil(DrawingBindings.Mount0,definition,out _),Is.True);
                Assert.That(tower.WithFreeformDrawing(session.Snapshot).FreeformDesign.IsUsable,Is.True,definition.Id);
            }
        }
        [Test] public void BonusSaturationDoesNotStopFullGeometryConsumingInk()
        {
            var saturated=Tower(.5f,.5f,.9f);var larger=Tower(.6f,.7f,1);
            Assert.That(larger.FreeformDesign.HealthMultiplier,Is.EqualTo(saturated.FreeformDesign.HealthMultiplier));
            Assert.That(larger.FreeformDesign.GetBarrel(0).DamageMultiplier,Is.EqualTo(saturated.FreeformDesign.GetBarrel(0).DamageMultiplier));
            Assert.That(larger.FreeformDesign.InkUsed,Is.GreaterThan(saturated.FreeformDesign.InkUsed));
        }
        [Test] public void InvalidConfigCannotCreateSnapshot()
        {
            var input=Input();input.FreeformRules.BarrelWidthReference=float.NaN;Assert.Throws<ArgumentException>(()=>input.CreateSnapshot());
            input=Input();input.FreeformRules.BodyAreaReference=0;Assert.Throws<ArgumentException>(()=>input.CreateSnapshot());
            input=Input();input.FreeformRules.BaseInkBudget=-1;Assert.Throws<ArgumentException>(()=>input.CreateSnapshot());
            input=Input();input.DrawingUnlocks[0].GoldCost=-1;Assert.Throws<ArgumentException>(()=>input.CreateSnapshot());
            input=Input();input.DrawingUnlocks[0].Contours[0].Points[0].X=2;Assert.Throws<ArgumentException>(()=>input.CreateSnapshot());
            input=Input();input.DrawingUnlocks[1].Id=input.DrawingUnlocks[0].Id;Assert.Throws<ArgumentException>(()=>input.CreateSnapshot());
        }
        [Test] public void LowerCurrentAllowanceBlocksUseWithoutDiscardingHistoricalPage()
        {
            var profile=new WorkshopProfile();var tower=Tower();Assert.That(profile.SaveBlueprint(tower,out _),Is.True);
            var input=Input();input.FreeformRules.BaseInkBudget=1;var content=input.CreateSnapshot();
            var restored=WorkshopProfile.FromData(profile.ToData());restored.ConfigureContent(content);
            Assert.That(restored.Album.Count,Is.EqualTo(profile.Album.Count));
            var rebound=tower.WithContent(content);
            Assert.That(rebound.FreeformDesign.IsComplete,Is.True);Assert.That(rebound.FreeformDesign.IsUsable,Is.False);
            Assert.That(restored.ValidateOwned(rebound,out _),Is.False);
        }
        [Test] public void CustomMinimumGeometrySurvivesReloadAndRemainsEditableUnderStricterRules()
        {
            var input=Input();input.FreeformRules.MinimumBodyArea=.001f;input.FreeformRules.MinimumBarrelLength=.01f;input.FreeformRules.MinimumMuzzleForward=.01f;
            var content=input.CreateSnapshot();var profile=new WorkshopProfile(content);var tower=Tower(.05f,.04f,.05f).WithContent(content);
            Assert.That(profile.SaveBlueprint(tower,out var reason),Is.True,reason);
            var restored=WorkshopProfile.FromData(profile.ToData());TowerBlueprint saved=null;
            foreach(var page in restored.Album)if(page.Id==tower.Id)saved=page;
            Assert.That(saved,Is.Not.Null);Assert.That(saved.Drawing.GeometryHash,Is.EqualTo(tower.Drawing.GeometryHash));
            Assert.That(TowerCatalog.Validate(saved,out _),Is.False,"Default minimum geometry is stricter than the authoring catalog");
            Assert.That(TowerCatalog.Validate(saved.WithContent(content),out _),Is.True);
            var edit=new DrawingEditSession(saved);Assert.That(edit.ScalePart(DrawingBindings.Base,4,4,out _),Is.True);
        }
        [Test] public void ExplicitBalanceContentControlsAllResolvedStatsAndBudget()
        {
            var tower=Tower();var balance=GameBalance.CreateDefault();float health=TowerCatalog.Health(tower,balance,MaterialTier.Pencil);
            var input=Input();input.FreeformRules.HealthBase=4;input.FreeformRules.DamageBase=3;input.FreeformRules.BaseInkBudget=1;balance.Content=input.CreateSnapshot();
            Assert.That(TowerCatalog.Health(tower,balance,MaterialTier.Pencil),Is.GreaterThan(health));
            Assert.That(TowerCatalog.WeaponDamage(tower,balance,tower.FirstWeaponId,MaterialTier.Pencil),Is.GreaterThan(balance.Structure(StructureKind.Gun).Damage*3));
            Assert.That(TowerCatalog.Validate(tower,balance,out _),Is.False);
        }
        [Test] public void DrawingPurchasesAreIdempotentPersistentAndSeparateFromEquipment()
        {
            var content=Input().CreateSnapshot();var profile=new WorkshopProfile(content);string id="drawing.sniper";
            Assert.That(profile.UnlockDrawingAid(id,out _),Is.False);Assert.That(profile.IsDrawingUnlocked(id),Is.False);
            profile.GrantGold(1000);Assert.That(profile.UnlockDrawingAid(id,out _),Is.True);int gold=profile.Gold;
            Assert.That(profile.UnlockDrawingAid(id,out _),Is.True);Assert.That(profile.Gold,Is.EqualTo(gold));
            Assert.That(profile.UnlockDrawingAid("drawing.unknown",out _),Is.False);
            foreach(var part in profile.Parts)Assert.That(part.Id,Is.Not.EqualTo(id));
            var restored=WorkshopProfile.FromData(profile.ToData());restored.ConfigureContent(content);Assert.That(restored.IsDrawingUnlocked(id),Is.True);
            Assert.That(restored.IsDrawingUnlocked("drawing.body.basic"),Is.True);
        }
        [Test] public void InitialDrawingGrantIsPermanentAcrossReloadAndCatalogChange()
        {
            var input=Input();input.DrawingUnlocks.Find(item=>item.Id=="drawing.sniper").InitiallyUnlocked=true;
            var content=input.CreateSnapshot();var profile=new WorkshopProfile(content);
            Assert.That(profile.IsDrawingUnlocked("drawing.sniper"),Is.True);
            Assert.That(profile.UnlockDrawingAid("drawing.sniper",out _),Is.True);Assert.That(profile.Gold,Is.Zero);
            var saved=profile.ToData();Assert.That(saved.Unlocked,Does.Contain("drawing.sniper"));
            input.DrawingUnlocks.Find(item=>item.Id=="drawing.sniper").InitiallyUnlocked=false;
            var restored=WorkshopProfile.FromData(saved);restored.ConfigureContent(input.CreateSnapshot());
            Assert.That(restored.IsDrawingUnlocked("drawing.sniper"),Is.True);Assert.That(restored.UnlockDrawingAid("drawing.sniper",out _),Is.True);Assert.That(restored.Gold,Is.Zero);
        }
        [Test] public void NewInitialDrawingGrantMarksContentPreparationChangedExactlyOnce()
        {
            var input=Input();var profile=new WorkshopProfile(input.CreateSnapshot());
            input.DrawingUnlocks.Find(item=>item.Id=="drawing.rapid").InitiallyUnlocked=true;var updated=input.CreateSnapshot();
            Assert.That(profile.ConfigureContent(updated),Is.True);
            Assert.That(profile.ToData().Unlocked,Does.Contain("drawing.rapid"));
            Assert.That(profile.ConfigureContent(updated),Is.False);
        }
        [TestCase("health")][TestCase("damage")][TestCase("range")][TestCase("interval")][TestCase("build")]
        public void FiniteCoefficientsCannotDeployNonFiniteResolvedStatistics(string statistic)
        {
            var input=Input();var balance=GameBalance.CreateDefault();var tower=Tower();
            switch(statistic)
            {
                case "health":input.FreeformRules.HealthBase=1e38f;break;
                case "damage":input.FreeformRules.DamageBase=1e38f;break;
                case "range":input.FreeformRules.RangeBase=1e38f;break;
                case "interval":input.FreeformRules.IntervalBase=1e38f;break;
                case "build":input.FreeformRules.BuildBase=1e38f;input.Chassis.Find(item=>item.Id==tower.ChassisId).BuildSeconds=10;break;
            }
            balance.Content=input.CreateSnapshot();
            Assert.That(TowerCatalog.Validate(tower,balance,out var reason),Is.False,"Final "+statistic+" arithmetic must remain finite, including low production and material multipliers");
            Assert.That(reason,Is.Not.Empty);
        }
        [Test] public void GeneratedShapesTransformsAndStampAreAtomicAndUndoable()
        {
            var session=new DrawingEditSession(Tower());string original=session.Snapshot.GeometryHash;
            Assert.That(session.AddRectangle(DrawingBindings.Decoration,new DrawingPoint(-.2f,-.2f),new DrawingPoint(.2f,.2f),out _),Is.True);
            string rectangle=session.Snapshot.GeometryHash;Assert.That(session.Undo(),Is.True);Assert.That(session.Snapshot.GeometryHash,Is.EqualTo(original));Assert.That(session.Redo(),Is.True);
            Assert.That(session.TranslatePart(DrawingBindings.Decoration,.1f,0,out _),Is.True);
            Assert.That(session.ScalePart(DrawingBindings.Decoration,2,1,out _),Is.True);
            Assert.That(session.MirrorPartHorizontal(DrawingBindings.Decoration,out _),Is.True);
            string mirrored=session.Snapshot.GeometryHash;Assert.That(session.TranslatePart(DrawingBindings.Decoration,2,0,out var reason),Is.False);Assert.That(reason,Is.Not.Empty);Assert.That(session.Snapshot.GeometryHash,Is.EqualTo(mirrored));
            Assert.That(session.TranslatePart(DrawingBindings.Decoration,0,0,out _),Is.False);
            Assert.That(session.Undo(),Is.True);Assert.That(session.Undo(),Is.True);Assert.That(session.Undo(),Is.True);Assert.That(session.Snapshot.GeometryHash,Is.EqualTo(rectangle));
            var stencil=Input().CreateSnapshot().DrawingUnlocks[0];Assert.That(session.ReplaceFromStencil(DrawingBindings.Base,stencil,out _),Is.True);
            Assert.That(session.Snapshot.GetPart(DrawingBindings.Decoration).Strokes.Count,Is.EqualTo(1));
            Assert.That(session.ReplaceFromStencil(DrawingBindings.Mount0,stencil,out _),Is.False);
            Assert.That(session.Undo(),Is.True);Assert.That(session.Snapshot.GeometryHash,Is.EqualTo(rectangle));
        }
        [Test] public void LineEllipseAndSweptEraserRespectOneOperationHistory()
        {
            var session=new DrawingEditSession(Tower());
            Assert.That(session.AddLine(DrawingBindings.Decoration,new DrawingPoint(0,-.7f),new DrawingPoint(0,.7f),out _),Is.True);
            Assert.That(session.AddEllipse(DrawingBindings.Decoration,new DrawingPoint(-.3f,-.3f),new DrawingPoint(.3f,.3f),out _),Is.True);string before=session.Snapshot.GeometryHash;
            Assert.That(session.ErasePath(DrawingBindings.Decoration,new[]{new DrawingPoint(-.8f,0),new DrawingPoint(.8f,0)},.02f,out _),Is.True);
            Assert.That(session.Snapshot.GetPart(DrawingBindings.Decoration).Strokes,Is.Empty);
            Assert.That(session.Undo(),Is.True);Assert.That(session.Snapshot.GeometryHash,Is.EqualTo(before));
            Assert.That(session.ScalePart(DrawingBindings.Decoration,float.NaN,1,out _),Is.False);
        }
    }
}
