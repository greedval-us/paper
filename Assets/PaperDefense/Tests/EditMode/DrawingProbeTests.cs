using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
namespace PaperDefense.Tests
{
    public sealed class DrawingProbeTests
    {
        [Test] public void FiresOnlyTheChosenMountOnceAndKeepsWholeTowerCost()
        {
            var balance=GameBalance.CreateDefault();
            var page=new TowerBlueprint("probe","Probe",ChassisKind.Heavy,StructureKind.Gun,StructureKind.Piercer);
            var report=DrawingProbe.Run(page,1,4,DrawingProbeFormation.Column,balance);
            Assert.That(report.Completed,Is.True);Assert.That(report.Launches,Is.EqualTo(1));Assert.That(report.Hits,Is.GreaterThan(1));
            Assert.That(report.Cost,Is.EqualTo(TowerCatalog.Cost(page,balance,MaterialTier.Pencil)));
            Assert.That(report.Frames.SelectMany(f=>f.Shots).All(s=>s.MountSlotIndex==1),Is.True);
            Assert.That(report.Frames.SelectMany(f=>f.Shots).Select(s=>s.ActionId).Distinct().Count(),Is.EqualTo(1));
        }
        [Test] public void FixedTargetsStayOutsideShortBarrelAndInsideLongerBarrel()
        {
            var balance=GameBalance.CreateDefault();var original=new TowerBlueprint("length","Length",ChassisKind.Standard,StructureKind.Gun).AsFreeform();
            var edit=new DrawingEditSession(original,original.Drawing);
            edit.AddRectangle(DrawingBindings.Base,new DrawingPoint(-.25f,-.25f),new DrawingPoint(.25f,.25f),out _);
            edit.AddRectangle(DrawingBindings.Mount0,new DrawingPoint(-.0625f,0),new DrawingPoint(.0625f,.3f),out _);
            var shortPage=original.WithFreeformDrawing(edit.Snapshot);edit.ScalePart(DrawingBindings.Mount0,1,2,out _);var longPage=original.WithFreeformDrawing(edit.Snapshot);
            float between=(TowerCatalog.WeaponRange(shortPage,balance,"weapon.gun",0)+TowerCatalog.WeaponRange(longPage,balance,"weapon.gun",0))/2;
            var a=DrawingProbe.Run(shortPage,0,between,DrawingProbeFormation.Single,balance);var b=DrawingProbe.Run(longPage,0,between,DrawingProbeFormation.Single,balance);
            Assert.That(a.Launches,Is.Zero);Assert.That(a.Completed,Is.True);Assert.That(b.Hits,Is.EqualTo(1));
            Assert.That(a.Frames[0].Enemies[0].Position,Is.EqualTo(b.Frames[0].Enemies[0].Position));
            Assert.That(b.Damage,Is.EqualTo(TowerCatalog.WeaponDamage(longPage,balance,"weapon.gun",MaterialTier.Pencil,0)).Within(.001));
        }
        [Test] public void MortarWaitsForOneRealImpactAndDoesNotLaunchAgain()
        {
            var report=DrawingProbe.Run(new TowerBlueprint("mortar","Mortar",ChassisKind.Standard,StructureKind.Mortar),0,4,DrawingProbeFormation.Front,GameBalance.CreateDefault());
            Assert.That(report.Completed,Is.True);Assert.That(report.Launches,Is.EqualTo(1));Assert.That(report.Hits,Is.GreaterThan(0));
            Assert.That(report.Frames.Any(f=>f.Projectiles.Count>0),Is.True);Assert.That(report.Frames.Last().Projectiles,Is.Empty);
            Assert.That(report.Frames.Last().Impacts.Count,Is.EqualTo(1));
        }
        [Test] public void FanSideGuidesCanMissWithoutInventingDamage()
        {
            var page=new TowerBlueprint("fan","Fan",ChassisKind.Standard,StructureKind.Fan).WithFanVolley(0,new FanVolley(new[]{FanGuide.FromAngle(-30),FanGuide.FromAngle(30)}));
            var report=DrawingProbe.Run(page,0,4,DrawingProbeFormation.Single,GameBalance.CreateDefault());
            Assert.That(report.Completed,Is.True);Assert.That(report.Launches,Is.EqualTo(1));Assert.That(report.Hits,Is.Zero);Assert.That(report.Damage,Is.Zero);
            Assert.That(report.Frames.Last().Shots.Count,Is.EqualTo(6));
        }
        [Test] public void RejectsBadDistanceMountFormationAndCleanupWeapon()
        {
            var balance=GameBalance.CreateDefault();var page=new TowerBlueprint("gun","Gun",ChassisKind.Standard,StructureKind.Gun);
            foreach(float distance in new[]{0f,float.NaN,float.PositiveInfinity,31f})Assert.Throws<ArgumentOutOfRangeException>(()=>DrawingProbe.Run(page,0,distance,DrawingProbeFormation.Single,balance));
            Assert.Throws<ArgumentOutOfRangeException>(()=>DrawingProbe.Run(page,2,4,DrawingProbeFormation.Single,balance));
            Assert.Throws<ArgumentOutOfRangeException>(()=>DrawingProbe.Run(page,0,4,(DrawingProbeFormation)99,balance));
            Assert.Throws<ArgumentException>(()=>DrawingProbe.Run(new TowerBlueprint("cleanup","Cleanup",ChassisKind.Standard,StructureKind.Blotter),0,4,DrawingProbeFormation.Single,balance));
        }
    }
}
