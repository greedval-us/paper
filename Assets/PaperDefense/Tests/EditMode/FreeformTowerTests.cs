using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class FreeformTowerTests
    {
        static DrawingStroke Box(float x, float y, float width, float height) => new DrawingStroke(new[] {
            new DrawingPoint(x,y), new DrawingPoint(x+width,y), new DrawingPoint(x+width,y+height),
            new DrawingPoint(x,y+height), new DrawingPoint(x,y) });
        static TowerBlueprint Tower(float body=.6f, float length=.6f, float width=.12f, bool duplicate=false, bool decoration=false)
        {
            var bodyStroke=Box(-body/2,-body/2,body,body);
            var parts=new List<DrawingPart> {
                new DrawingPart(DrawingBindings.Base, duplicate ? new[] {bodyStroke,bodyStroke} : new[]{bodyStroke}),
                new DrawingPart(DrawingBindings.Mount0,new[]{Box(-width/2,0,width,length)}) };
            if(decoration)parts.Add(new DrawingPart(DrawingBindings.Decoration,new[]{Box(-.9f,-.9f,1.8f,1.8f)}));
            return new TowerBlueprint("free", "Free tower", ChassisKind.Standard, StructureKind.Gun,
                drawing:new DrawingSnapshot(parts),origin:(DrawingOrigin)3);
        }
        [Test] public void LargerBodyBuysHealthAndCapacityAtHigherPrice()
        {
            var small=Tower(.4f);var large=Tower(.9f);var balance=GameBalance.CreateDefault();
            Assert.That(TowerCatalog.Health(large,balance,MaterialTier.Pencil),Is.GreaterThan(TowerCatalog.Health(small,balance,MaterialTier.Pencil)));
            Assert.That(TowerCatalog.Capacity(large,balance),Is.GreaterThan(TowerCatalog.Capacity(small,balance)));
            Assert.That(TowerCatalog.Cost(large,balance,MaterialTier.Pencil),Is.GreaterThan(TowerCatalog.Cost(small,balance,MaterialTier.Pencil)));
        }
        static TowerBlueprint WithBarrel(params DrawingStroke[] strokes)
        {
            var tower = Tower();
            return tower.WithFreeformDrawing(new DrawingSnapshot(new[] {
                tower.Drawing.GetPart(DrawingBindings.Base), new DrawingPart(DrawingBindings.Mount0, strokes) }));
        }
        [Test] public void RearExtensionCannotIncreaseRangeOrSpendWorkingInk()
        {
            var original = WithBarrel(Box(-.0625f, 0, .125f, .5f));
            var extended = WithBarrel(Box(-.0625f, -.5f, .125f, 1f));
            Assert.That(extended.FreeformDesign.GetBarrel(0).Length, Is.EqualTo(.5f));
            Assert.That(extended.FreeformDesign.GetBarrel(0).RangeMultiplier, Is.EqualTo(original.FreeformDesign.GetBarrel(0).RangeMultiplier));
            Assert.That(extended.FreeformDesign.InkUsed, Is.EqualTo(original.FreeformDesign.InkUsed));
        }
        [Test] public void RearCrossbarCannotIncreaseCaliberOrHitDamage()
        {
            var original = WithBarrel(Box(-.0625f, -.5f, .125f, 1f));
            var extended = WithBarrel(Box(-.0625f, -.5f, .125f, 1f),
                new DrawingStroke(new[] { new DrawingPoint(-.75f, -.5f), new DrawingPoint(.75f, -.5f) }));
            Assert.That(extended.FreeformDesign.GetBarrel(0).Width, Is.EqualTo(.125f));
            Assert.That(extended.FreeformDesign.GetBarrel(0).DamageMultiplier, Is.EqualTo(original.FreeformDesign.GetBarrel(0).DamageMultiplier));
        }
        [Test] public void WorkingWidthIncludesIntersectionOfSlantedStrokeWithMountPlane()
        {
            var tower = WithBarrel(new DrawingStroke(new[] { new DrawingPoint(-.75f, -.5f), new DrawingPoint(.25f, .5f) }));
            Assert.That(tower.FreeformDesign.GetBarrel(0).Width, Is.EqualTo(.5f));
            Assert.That(tower.FreeformDesign.GetBarrel(0).Length, Is.EqualTo(.5f));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void ConnectionBehindMountCannotJoinSeparateWorkingBarrels(bool singleStroke)
        {
            var left = new DrawingStroke(new[] { new DrawingPoint(-.5f, -.5f), new DrawingPoint(-.5f, .5f) });
            var right = new DrawingStroke(new[] { new DrawingPoint(.5f, -.5f), new DrawingPoint(.5f, .5f) });
            var separate = WithBarrel(left, right);
            var joined = singleStroke
                ? WithBarrel(new DrawingStroke(new[] { new DrawingPoint(-.5f, .5f), new DrawingPoint(-.5f, -.5f), new DrawingPoint(.5f, -.5f), new DrawingPoint(.5f, .5f) }))
                : WithBarrel(left, right, new DrawingStroke(new[] { new DrawingPoint(-.5f, -.5f), new DrawingPoint(.5f, -.5f) }));
            Assert.That(joined.FreeformDesign.GetBarrel(0).Width, Is.EqualTo(separate.FreeformDesign.GetBarrel(0).Width));
            Assert.That(joined.FreeformDesign.GetBarrel(0).MuzzleOffset.X, Is.EqualTo(-.5f));
        }
        [Test] public void HistoricalRulesSurviveCopiesAndSaveUntilExplicitDrawingApply()
        {
            var saved = new BlueprintData(WithBarrel(Box(-.0625f, -.5f, .125f, 1f)));
            saved.FreeformRuleVersion = 1;
            var historical = saved.ToBlueprint().WithName("Renamed").AsVariant("copy", "Copy").WithContent(GameBalance.CreateDefault().ResolveContent());
            Assert.That(historical.FreeformDesign.GetBarrel(0).Length, Is.EqualTo(1f));
            Assert.That(new BlueprintData(historical).FreeformRuleVersion, Is.EqualTo(1));
            var revised = historical.WithFreeformDrawing(historical.Drawing);
            Assert.That(revised.FreeformDesign.GetBarrel(0).Length, Is.EqualTo(.5f));
            Assert.That(new BlueprintData(revised).FreeformRuleVersion, Is.GreaterThan(1));
            Assert.That(historical.FreeformDesign.GetBarrel(0).Length, Is.EqualTo(1f));
        }
        [Test] public void LongerBarrelTradesTempoForRange()
        {
            var shortBarrel=Tower(length:.3f);var longBarrel=Tower(length:.9f);var balance=GameBalance.CreateDefault();
            Assert.That(TowerCatalog.WeaponRange(longBarrel,balance,StructureKind.Gun),Is.GreaterThan(TowerCatalog.WeaponRange(shortBarrel,balance,StructureKind.Gun)));
            Assert.That(TowerCatalog.ActionInterval(longBarrel,balance,StructureKind.Gun,1,1),Is.GreaterThan(TowerCatalog.ActionInterval(shortBarrel,balance,StructureKind.Gun,1,1)));
        }
        [Test] public void DuplicateStrokesAndDecorationCannotBuyStats()
        {
            var plain=Tower();var adorned=Tower(duplicate:true,decoration:true);var balance=GameBalance.CreateDefault();
            Assert.That(TowerCatalog.Health(adorned,balance,MaterialTier.Pencil),Is.EqualTo(TowerCatalog.Health(plain,balance,MaterialTier.Pencil)));
            Assert.That(TowerCatalog.Cost(adorned,balance,MaterialTier.Pencil),Is.EqualTo(TowerCatalog.Cost(plain,balance,MaterialTier.Pencil)));
        }
        [Test] public void EmptyFunctionalDrawingCannotBeBuilt()
        {
            var tower=new TowerBlueprint("empty","Empty",ChassisKind.Standard,StructureKind.Gun,origin:(DrawingOrigin)3);
            Assert.That(TowerCatalog.Validate(tower,out var reason),Is.False);
            Assert.That(reason,Is.Not.Empty);
        }
        [Test] public void FreeformRoundTripKeepsConstructionStats()
        {
            var original=Tower();var restored=new BlueprintData(original).ToBlueprint();var balance=GameBalance.CreateDefault();
            Assert.That(restored.Origin,Is.EqualTo((DrawingOrigin)3));
            Assert.That(restored.Drawing.GeometryHash,Is.EqualTo(original.Drawing.GeometryHash));
            Assert.That(TowerCatalog.WeaponRange(restored,balance,StructureKind.Gun),Is.EqualTo(TowerCatalog.WeaponRange(original,balance,StructureKind.Gun)));
        }
        [Test] public void OldDrawingAccuracyNoLongerReducesLiveFireRate()
        {
            var old=TowerBlueprint.NewDraft("old","Old drawing",ChassisKind.Standard,StructureKind.Gun);
            Assert.That(TowerCatalog.QualityMultiplier(old,GameBalance.CreateDefault()),Is.EqualTo(1));
        }
        [Test] public void WideBarrelChangesActualHitDamageAndPreviewTogether()
        {
            var balance=GameBalance.CreateDefault();var narrow=Tower(width:.06f);var wide=Tower(width:.4f);
            Assert.That(Hit(wide,balance),Is.GreaterThan(Hit(narrow,balance)));
            Assert.That(Hit(wide,balance),Is.EqualTo(TowerCatalog.WeaponDamage(wide,balance,wide.FirstWeaponId,MaterialTier.Pencil)).Within(.0001f));
        }
        static float Hit(TowerBlueprint blueprint,GameBalance balance)
        {
            var tower=new StructureState{Id=1,Cell=new Cell(10,10),Kind=StructureKind.Gun,Blueprint=blueprint,IsComplete=true,Health=100,Tier=MaterialTier.Pencil,PrimaryTargetId=1};
            var target=new EnemyState{Id=1,Kind=EnemyKind.Doodle,Position=new Point2(11.5f,10.5f),Health=1000,MaxHealth=1000};
            new WeaponSystem(balance).Fire(new List<StructureState>{tower},new List<EnemyState>{target},new List<ShotEvent>(),1,1,1f/30,1,new RunStatistics(),new SimulationEventBuffer());
            return 1000-target.Health;
        }
        [Test] public void EmptyDrawingCannotFireEvenWhenInjectedIntoCombat()
        {
            var tower=Tower().WithFreeformDrawing(DrawingSnapshot.Empty);
            Assert.That(Hit(tower,GameBalance.CreateDefault()),Is.Zero);
        }
        [Test] public void BodyWithoutBarrelIsAnIncompleteDraft()
        {
            var drawing=new DrawingSnapshot(new[]{new DrawingPart(DrawingBindings.Base,new[]{Box(-.3f,-.3f,.6f,.6f)})});
            var tower=Tower().WithFreeformDrawing(drawing);
            Assert.That(tower.FreeformDesign.IsComplete,Is.False);
            Assert.That(TowerCatalog.Validate(tower,out _),Is.False);
        }
        [Test] public void DisconnectedMarksDoNotEnlargeBody()
        {
            var tower=Tower();var body=new DrawingPart(DrawingBindings.Base,new[]{Box(-.3f,-.3f,.6f,.6f),Box(.8f,.8f,.1f,.1f)});
            var marked=tower.WithFreeformDrawing(new DrawingSnapshot(new[]{body,tower.Drawing.GetPart(DrawingBindings.Mount0)}));
            Assert.That(marked.FreeformDesign.BodyArea,Is.EqualTo(tower.FreeformDesign.BodyArea));
        }
        [Test] public void SampleSubdivisionAndRetracingDoNotChangeGeometry()
        {
            var a=new DrawingPoint(0,0);var b=new DrawingPoint(.8271484375f,.56640625f);var c=new DrawingPoint(-.5f,.5f);
            var sparse=new DrawingStroke(new[]{a,b,c,a});
            var dense=new DrawingStroke(new[]{a,new DrawingPoint(.206787109375f,.1416015625f),b,c,a});
            var tower=Tower();var barrel=tower.Drawing.GetPart(DrawingBindings.Mount0);
            var original=tower.WithFreeformDrawing(new DrawingSnapshot(new[]{new DrawingPart(DrawingBindings.Base,new[]{sparse}),barrel}));
            var divided=tower.WithFreeformDrawing(new DrawingSnapshot(new[]{new DrawingPart(DrawingBindings.Base,new[]{dense}),barrel}));
            var retraced=tower.WithFreeformDrawing(new DrawingSnapshot(new[]{new DrawingPart(DrawingBindings.Base,new[]{sparse,dense}),barrel}));
            Assert.That(divided.FreeformDesign.BodyArea,Is.EqualTo(original.FreeformDesign.BodyArea));
            Assert.That(retraced.FreeformDesign.BodyArea,Is.EqualTo(original.FreeformDesign.BodyArea));
        }
        [Test] public void MuzzleUsesTheMainBarrelTipAndIgnoresRemoteMarks()
        {
            var tower=Tower(length:.8f);var barrel=new DrawingPart(DrawingBindings.Mount0,new[]{Box(-.06f,0,.12f,.8f),Box(.8f,.9f,.1f,.05f)});
            var drawn=tower.WithFreeformDrawing(new DrawingSnapshot(new[]{tower.Drawing.GetPart(DrawingBindings.Base),barrel}));
            var muzzle=drawn.FreeformDesign.GetBarrel(0).MuzzleOffset;
            Assert.That(muzzle.X,Is.EqualTo(0).Within(.001f));Assert.That(muzzle.Y,Is.EqualTo(.8f).Within(.001f));
        }
        [Test] public void BackwardOnlyBarrelIsNotAForwardFiringDesign()
        {
            var tower=Tower();var drawing=new DrawingSnapshot(new[]{tower.Drawing.GetPart(DrawingBindings.Base),new DrawingPart(DrawingBindings.Mount0,new[]{Box(-.1f,-.8f,.2f,.6f)})});
            Assert.That(tower.WithFreeformDrawing(drawing).FreeformDesign.IsComplete,Is.False);
        }
        [Test] public void AssemblyAndNameCopiesKeepFreeformShapeAndOrigin()
        {
            var tower=Tower();var changed=tower.WithName("Changed").WithWeaponMount(0,LegacyContentIds.Weapon(StructureKind.Piercer));
            Assert.That(changed.IsFreeform,Is.True);
            Assert.That(changed.Drawing.GeometryHash,Is.EqualTo(tower.Drawing.GeometryHash));
            Assert.That(changed.FreeformDesign.IsComplete,Is.True);
            Assert.That(changed.AsNew("new","New").IsFreeform,Is.True);
        }
        [TestCase(0,typeof(ArgumentException))]
        [TestCase(3,typeof(NotSupportedException))]
        public void UnknownOrMissingGeometryRulesNeverSilentlyReinterpretSave(int version,Type error)
        {
            var saved=new BlueprintData(Tower());saved.FreeformRuleVersion=version;
            Assert.Throws(error,()=>saved.ToBlueprint());
        }
        [Test] public void FreeformCannotMasqueradeAsOldProfileVersion()
        { Assert.Throws<ArgumentException>(()=>new BlueprintData(Tower()).ToBlueprint(6)); }
        [Test] public void SavedLegacySixPageRemainsReadable()
        {
            var old=new TowerBlueprint("old","Old",ChassisKind.Standard,StructureKind.Gun);
            var restored=new BlueprintData(old).ToBlueprint(6);
            Assert.That(restored.Origin,Is.EqualTo(DrawingOrigin.LegacyStandard));
            Assert.That(restored.FreeformDesign,Is.Null);
        }
        [Test] public void ExtraBarrelsShareAProductionBudgetWithoutDynamicFeed()
        {
            var balance=GameBalance.CreateDefault();var single=Tower(body:.9f);
            var dual=single.WithWeaponMount(1,single.FirstWeaponId);
            Assert.That(TowerCatalog.FeedMultiplier(dual,balance),Is.LessThan(TowerCatalog.FeedMultiplier(single,balance)));
        }
    }
}
