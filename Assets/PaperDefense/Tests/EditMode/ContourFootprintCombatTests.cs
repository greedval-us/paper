using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class ContourFootprintCombatTests
    {
        [Test] public void TransverseContourUsesDrawingAxesAndFixedArea()
        {
            var footprint=MortarFootprint.FromContour(FootprintContourTests.Box(1,.5f));
            var horizontal=FootprintGeometry.Resolve(footprint,2,default,new Point2(1,0));
            Assert.That(horizontal.Contains(default,new Point2(0,2)),Is.True);
            Assert.That(horizontal.Contains(default,new Point2(2,0)),Is.False);
            var vertical=FootprintGeometry.Resolve(footprint,2,default,new Point2(0,1));
            Assert.That(vertical.Contains(default,new Point2(2,0)),Is.True);
            Assert.That(vertical.Contains(default,new Point2(0,2)),Is.False);
        }
        [Test] public void SquareCornerHitsOutsideEquivalentCircleAndSurvivesRefit()
        {
            var balance=GameBalance.CreateDefault();var stats=balance.Structure(StructureKind.Mortar);
            stats.Radius=2;stats.Range=20;stats.Damage=5;stats.FlightSeconds=.5f;
            var bp=new TowerBlueprint("square","Square",ChassisKind.Standard,StructureKind.Mortar)
                .WithMortarFootprint(0,MortarFootprint.FromContour(FootprintContourTests.Box()));
            var tower=new StructureState{Id=1,Cell=new Cell(10,10),Kind=StructureKind.Mortar,Blueprint=bp,Tier=MaterialTier.Pencil,IsComplete=true,Health=100,PrimaryTargetId=1};
            var structures=new List<StructureState>{tower};
            var enemies=new List<EnemyState>{new EnemyState{Id=1,Position=new Point2(14.5f,10.5f),Health=100,MaxHealth=100},new EnemyState{Id=2,Position=new Point2(20,20),Health=100,MaxHealth=100}};
            var weapons=new WeaponSystem(balance);var events=new SimulationEventBuffer();var runStats=new RunStatistics();
            weapons.Fire(structures,enemies,new List<ShotEvent>(),1,1,.01f,1,runStats,events);
            Assert.That(weapons.Projectiles.Count,Is.EqualTo(1));
            var launched=weapons.Projectiles[0].ImpactFootprint;
            enemies[1].Position=new Point2(16.2f,12.2f);
            tower.Blueprint=bp.WithMortarFootprint(0,MortarFootprint.Circle);structures.Clear();
            weapons.Fire(structures,enemies,new List<ShotEvent>(),1,1,.5f,2,runStats,events);
            Assert.That(enemies[1].Health,Is.EqualTo(95));
            Assert.That(events.Last(e=>e.Kind==SimulationEventKind.ProjectileImpact).ImpactFootprint,Is.EqualTo(launched));
        }
        [Test] public void ContourRoundTripsAndCopiesRemainIndependent()
        {
            var shape=MortarFootprint.FromContour(FootprintContourTests.Box(1,.5f));
            var original=new TowerBlueprint("m","M",ChassisKind.Heavy,StructureKind.Mortar,StructureKind.Mortar).WithMortarFootprint(1,shape);
            var restored=new BlueprintData(original).ToBlueprint();
            Assert.That(restored.SecondFootprint.IsContour,Is.True);
            Assert.That(restored.SecondFootprint.SameShape(shape),Is.True);
            Assert.That(restored.FirstFootprint.IsCircle,Is.True);
            var copy=restored.WithName("Copy").AsVariant("copy","Copy").WithMortarFootprint(1,MortarFootprint.Circle);
            Assert.That(restored.SecondFootprint.SameShape(shape),Is.True);Assert.That(copy.SecondFootprint.IsCircle,Is.True);
        }
        [Test] public void SquareContourCannotBeAttachedToGun()
        {
            var shape=MortarFootprint.FromContour(FootprintContourTests.Box());
            Assert.Throws<ArgumentException>(()=>new TowerBlueprint("g","G",ChassisKind.Standard,StructureKind.Gun,firstFootprint:shape));
        }
        [Test] public void ConcaveNotchIsExcludedAndBoundaryIsIncluded()
        {
            var points=new[]{new Point2(-1,-1),new Point2(1,-1),new Point2(1,1),new Point2(.4f,1),new Point2(.4f,.3f),new Point2(-.4f,.3f),new Point2(-.4f,1),new Point2(-1,1)};
            var shape=MortarFootprint.FromContour(points);var geometry=FootprintGeometry.Resolve(shape,2,default,new Point2(0,1));
            float scale=2*shape.Normalized[2].Y;
            Assert.That(geometry.Contains(default,new Point2(0,.7f*scale)),Is.False);
            Assert.That(geometry.Contains(default,new Point2(.8f*scale,.7f*scale)),Is.True);
            Assert.That(geometry.Contains(default,geometry.OutlineOffset(2)),Is.True);
            Assert.That(geometry.BoundingRadius,Is.GreaterThan(geometry.ForwardRadius));
        }
        [Test] public void TransverseAndLongitudinalShapesTradeCoverageWithoutChangingCost()
        {
            var bp=new TowerBlueprint("probe","Probe",ChassisKind.Standard,StructureKind.Mortar);
            var along=MortarFootprint.FromContour(FootprintContourTests.Box(.6f,1.4f));
            var across=MortarFootprint.FromContour(FootprintContourTests.Box(1.4f,.6f));
            var line=MortarFootprintProbe.Compare(bp,0,along,across,GameBalance.CreateDefault(),FootprintFormation.Line);
            var column=MortarFootprintProbe.Compare(bp,0,along,across,GameBalance.CreateDefault(),FootprintFormation.Column);
            Assert.That(line.Second.HitCount,Is.GreaterThan(line.First.HitCount));
            Assert.That(column.First.HitCount,Is.GreaterThan(column.Second.HitCount));
            Assert.That(line.Second.Cost,Is.EqualTo(line.First.Cost));Assert.That(line.Second.DrawingQuality,Is.EqualTo(line.First.DrawingQuality));
        }
        [TestCase(20)] [TestCase(70)]
        public void ExactAspectLimitSurvivesRotatedFloatCoordinatesAndRuntimeValidation(float degrees)
        {
            double angle=degrees*Math.PI/180;
            var points=FootprintContourTests.Box(3,1).Select(p=>new Point2((float)(p.X*Math.Cos(angle)-p.Y*Math.Sin(angle)),(float)(p.X*Math.Sin(angle)+p.Y*Math.Cos(angle)))).ToArray();
            var shape=MortarFootprint.FromContour(points);
            var bp=new TowerBlueprint("limit","Limit",ChassisKind.Standard,StructureKind.Mortar).WithMortarFootprint(0,shape);
            var balance=GameBalance.CreateDefault();Assert.That(TowerCatalog.Validate(bp,balance,out string reason),Is.True,reason);
            Assert.That(MortarFootprintProbe.Compare(bp,0,shape,shape,balance,FootprintFormation.Group).Second.Completed,Is.True);
        }
    }
}
