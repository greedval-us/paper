using System;
using NUnit.Framework;
using PaperDefense.Core;
namespace PaperDefense.Tests
{
    public sealed class WorkshopAssemblyStageTests
    {
        [Test] public void PartStageAllowsBlankDrawingButFinalValidationRequiresGeometry()
        {
            var balance=GameBalance.CreateDefault();var bp=new TowerBlueprint("stage","Stage",ChassisKind.Standard,StructureKind.Gun).AsFreeform();
            Assert.That(TowerCatalog.ValidateAssembly(bp,balance,out var reason),Is.True,reason);
            Assert.That(TowerCatalog.Validate(bp,balance,out _),Is.False);
        }
        [Test] public void PartStageStillRejectsTooManyMounts()
        {
            var balance=GameBalance.CreateDefault();var bp=new TowerBlueprint("stage","Stage","chassis.standard",new[]{new WeaponMountSelection(0,"weapon.gun"),new WeaponMountSelection(1,"weapon.gun"),new WeaponMountSelection(2,"weapon.gun")}).AsFreeform();
            Assert.That(TowerCatalog.ValidateAssembly(bp,balance,out _),Is.False);
        }

        [Test] public void ExplicitStandardDraftAndAssemblyKeepReferenceStatsAndValidationWithoutDrawing()
        {
            var balance=GameBalance.CreateDefault();var content=balance.ResolveContent();
            var draft=TowerBlueprint.NewStandardDraft("standard-stage","Standard stage","chassis.standard","weapon.gun",content:content);
            AssertStandardStats(draft,new TowerBlueprint("standard-reference","Reference","chassis.standard","weapon.gun",content:content),balance);

            var mounts=new[]{new WeaponMountSelection(0,"weapon.piercer"),new WeaponMountSelection(1,"weapon.gun")};
            var modules=new[]{new ModuleSelection("stage-feed","module.feed")};
            var changed=draft.WithStandardAssembly("chassis.heavy",mounts,modules);
            var reference=new TowerBlueprint("changed-reference","Reference","chassis.heavy",mounts,modules,content:content);
            AssertStandardStats(changed,reference,balance);
            Assert.That(changed.Id,Is.EqualTo(draft.Id));
            Assert.That(TowerCatalog.Validate(changed,balance,out var reason),Is.True,reason);

            var overloaded=changed.WithStandardAssembly("chassis.standard",mounts,modules);
            var overloadedReference=new TowerBlueprint("overloaded-reference","Reference","chassis.standard",mounts,modules,content:content);
            AssertStandardStats(overloaded,overloadedReference,balance);
            Assert.That(TowerCatalog.Validate(overloaded,balance,out _),Is.False,"The explicit standard path must still reject excess load.");
        }

        [Test] public void ExplicitStandardAssemblyRejectsHistoricalDrawingWithoutChangingHistoricalFactoriesOrEdits()
        {
            var stroke=new DrawingStroke(new[]{new DrawingPoint(-.3f,0),new DrawingPoint(.3f,0),new DrawingPoint(.3f,.5f),new DrawingPoint(-.3f,0)});
            var drawing=new DrawingSnapshot(new[]{new DrawingPart(DrawingPartKey.Base,new[]{stroke}),new DrawingPart(DrawingPartKey.Mount0,new[]{stroke})});
            var modules=new[]{new ModuleSelection("historical-feed","module.feed")};
            foreach(var origin in new[]{DrawingOrigin.PlayerDrawing,DrawingOrigin.Freeform})
            {
                var historical=new TowerBlueprint("historical-stage","Historical stage","chassis.standard","weapon.gun",drawing:drawing,origin:origin,freeformRuleVersion:1);
                string hash=historical.Drawing.GeometryHash;int version=historical.FreeformRuleVersion;
                Assert.Throws<InvalidOperationException>(()=>historical.WithStandardAssembly(historical.ChassisId,historical.WeaponMounts,modules));
                Assert.That(historical.Origin,Is.EqualTo(origin));Assert.That(historical.Drawing,Is.SameAs(drawing));
                Assert.That(historical.Drawing.GeometryHash,Is.EqualTo(hash));Assert.That(historical.FreeformRuleVersion,Is.EqualTo(version));
                var edited=historical.WithAssembly(historical.ChassisId,historical.WeaponMounts,modules);
                Assert.That(edited.Origin,Is.EqualTo(origin),"The existing drawing editor API retains its historical origin.");
                Assert.That(edited.Drawing.GeometryHash,Is.EqualTo(hash));Assert.That(edited.FreeformRuleVersion,Is.EqualTo(version));
                Assert.That(historical.ModuleSelections,Is.Empty,"Historical edits produce copies.");
            }
            var oldDraft=TowerBlueprint.NewDraft("old-stage","Old stage","chassis.standard","weapon.gun");
            Assert.That(oldDraft.Origin,Is.EqualTo(DrawingOrigin.PlayerDrawing));Assert.That(oldDraft.Drawing.IsEmpty,Is.True);
            var standard=new TowerBlueprint("old-standard","Old standard","chassis.standard","weapon.gun");
            Assert.That(standard.WithAssembly(standard.ChassisId,standard.WeaponMounts,modules).Origin,Is.EqualTo(DrawingOrigin.PlayerDrawing),
                "Only the explicit standard API changes the construction policy.");
        }

        static void AssertStandardStats(TowerBlueprint actual,TowerBlueprint reference,GameBalance balance)
        {
            Assert.That(actual.Origin,Is.EqualTo(DrawingOrigin.LegacyStandard));
            Assert.That(actual.Drawing.IsEmpty,Is.True);Assert.That(actual.FreeformDesign,Is.Null);
            Assert.That(TowerCatalog.ValidateAssembly(actual,balance,out var assemblyReason),
                Is.EqualTo(TowerCatalog.ValidateAssembly(reference,balance,out var expectedAssemblyReason)));
            Assert.That(assemblyReason,Is.EqualTo(expectedAssemblyReason));
            Assert.That(TowerCatalog.Validate(actual,balance,out var reason),Is.EqualTo(TowerCatalog.Validate(reference,balance,out var expectedReason)));
            Assert.That(reason,Is.EqualTo(expectedReason));
            Assert.That(TowerCatalog.Load(actual,balance),Is.EqualTo(TowerCatalog.Load(reference,balance)));
            Assert.That(TowerCatalog.Capacity(actual,balance),Is.EqualTo(TowerCatalog.Capacity(reference,balance)));
            Assert.That(TowerCatalog.Health(actual,balance,MaterialTier.Pencil),Is.EqualTo(TowerCatalog.Health(reference,balance,MaterialTier.Pencil)));
            Assert.That(TowerCatalog.Cost(actual,balance,MaterialTier.Pencil),Is.EqualTo(TowerCatalog.Cost(reference,balance,MaterialTier.Pencil)));
            Assert.That(TowerCatalog.BuildSeconds(actual,balance,MaterialTier.Pencil),Is.EqualTo(TowerCatalog.BuildSeconds(reference,balance,MaterialTier.Pencil)));
            float feed=TowerCatalog.FeedMultiplier(actual,balance),expectedFeed=TowerCatalog.FeedMultiplier(reference,balance);
            Assert.That(feed,Is.EqualTo(expectedFeed));
            foreach(var mount in actual.WeaponMounts)
            {
                Assert.That(TowerCatalog.WeaponRange(actual,balance,mount.WeaponId,mount.SlotIndex),
                    Is.EqualTo(TowerCatalog.WeaponRange(reference,balance,mount.WeaponId,mount.SlotIndex)));
                Assert.That(TowerCatalog.ActionInterval(actual,balance,mount.WeaponId,feed,mount.SlotIndex),
                    Is.EqualTo(TowerCatalog.ActionInterval(reference,balance,mount.WeaponId,expectedFeed,mount.SlotIndex)));
            }
        }
    }
}
