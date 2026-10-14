using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class FootprintStudyTests
    {
        [Test] public void MatrixKeepsEqualConditionsAndRecordsActualImpacts()
        {
            var balance=GameBalance.CreateDefault();var source=new TowerBlueprint("study","Study",ChassisKind.Standard,StructureKind.Mortar);
            var report=FootprintStudy.Run(source,0,balance);
            Assert.That(report.Cases.Count,Is.EqualTo(9));Assert.That(report.Budget,Is.EqualTo(TowerCatalog.Cost(source,balance,MaterialTier.Pencil)));
            foreach(var row in report.Cases)
            {
                Assert.That(row.Samples.Count,Is.EqualTo(3));Assert.That(row.Completed,Is.True);
                foreach(var sample in row.Samples)
                {
                    Assert.That(sample.Launches,Is.EqualTo(1));Assert.That(sample.Recording.Cost,Is.EqualTo(report.Budget));
                    var first=sample.Recording.Frames[0];var last=sample.Recording.Frames.Last();
                    Assert.That(first.Towers[0].Blueprint.Drawing.GeometryHash,Is.EqualTo(source.Drawing.GeometryHash));
                    Assert.That(first.Towers[0].Tier,Is.EqualTo(MaterialTier.Pencil));
                    Assert.That(last.Impacts.Count,Is.EqualTo(1));Assert.That(last.Impacts[0].Kind,Is.EqualTo(SimulationEventKind.ProjectileImpact));
                    Assert.That(last.Enemies.Count(e=>e.Health<e.MaxHealth),Is.EqualTo(sample.Recording.HitCount));
                    Assert.That(last.Enemies.Sum(e=>e.MaxHealth-e.Health),Is.EqualTo(sample.Damage).Within(.001));
                    Assert.That(first.Enemies[0].Position,Is.Not.EqualTo(last.Enemies[0].Position));
                }
            }
        }
        [Test] public void SameTrajectoriesAndSeedlessRepeatsAreDeterministic()
        {
            var balance=GameBalance.CreateDefault();var source=new TowerBlueprint("study","Study",ChassisKind.Standard,StructureKind.Mortar);
            var first=FootprintStudy.Run(source,0,balance);var second=FootprintStudy.Run(source,0,balance);
            for(int i=0;i<9;i++)for(int sample=0;sample<3;sample++)
            {
                var a=first.Cases[i].Samples[sample].Recording;var b=second.Cases[i].Samples[sample].Recording;
                var control=first.Find(first.Cases[i].Scenario,FootprintStudyShape.Compact).Samples[sample].Recording;
                Assert.That(a.HitCount,Is.EqualTo(b.HitCount));Assert.That(a.Frames.Count,Is.EqualTo(b.Frames.Count));
                for(int f=0;f<a.Frames.Count;f++)for(int enemy=0;enemy<9;enemy++)
                {
                    Assert.That(a.Frames[f].Enemies[enemy].Position,Is.EqualTo(control.Frames[f].Enemies[enemy].Position));
                    Assert.That(a.Frames[f].Enemies[enemy].Health,Is.EqualTo(b.Frames[f].Enemies[enemy].Health));
                }
            }
        }
        [Test] public void TurningRouteActuallyChangesDirectionDuringFlight()
        {
            var source=new TowerBlueprint("study","Study",ChassisKind.Standard,StructureKind.Mortar);
            var row=FootprintStudy.Run(source,0,GameBalance.CreateDefault()).Find(FootprintStudyScenario.Turning,FootprintStudyShape.Compact);
            Assert.That(row.Samples.Select(s=>s.TurnSeconds).Distinct().Count(),Is.EqualTo(3));
            foreach(var sample in row.Samples)
            {
                var frames=sample.Recording.Frames;var a=frames[0].Enemies[0].Position;var b=frames[1].Enemies[0].Position;
                var c=frames[frames.Count-2].Enemies[0].Position;var d=frames.Last().Enemies[0].Position;
                Assert.That(b.X,Is.LessThan(a.X));Assert.That(b.Y,Is.EqualTo(a.Y));
                Assert.That(d.Y,Is.LessThan(c.Y));Assert.That(d.X,Is.EqualTo(c.X));
            }
        }
        [Test] public void ThreeShapesHaveEqualAreaAndDifferentOrientation()
        {
            foreach(FootprintStudyShape form in Enum.GetValues(typeof(FootprintStudyShape)))
            {
                var shape=FootprintStudy.CreateShape(form);var g=FootprintGeometry.Resolve(shape,2,default,new Point2(0,1));
                if(form==FootprintStudyShape.Compact){Assert.That(shape.IsCircle,Is.True);continue;}
                double area=0;for(int i=0;i<g.OutlineCount;i++){var a=g.OutlineOffset(i);var b=g.OutlineOffset(i+1);area+=(double)a.X*b.Y-(double)a.Y*b.X;}
                Assert.That(Math.Abs(area)/2,Is.EqualTo(4*Math.PI).Within(.00001));
                Assert.That(g.ForwardRadius>g.SideRadius,Is.EqualTo(form==FootprintStudyShape.Longitudinal));
            }
        }
        [Test] public void DifferentSituationsFavorDifferentFormsWithoutChangingWeapon()
        {
            var report=FootprintStudy.Run(new TowerBlueprint("study","Study",ChassisKind.Standard,StructureKind.Mortar),0,GameBalance.CreateDefault());
            var winners=new[]{FootprintStudyShape.Longitudinal,FootprintStudyShape.Transverse,FootprintStudyShape.Compact};
            for(int scenario=0;scenario<3;scenario++)
            {
                var winner=report.Find((FootprintStudyScenario)scenario,winners[scenario]);
                foreach(var row in report.Cases.Where(r=>r.Scenario==(FootprintStudyScenario)scenario&&r.Form!=winners[scenario]))Assert.That(winner.Hits,Is.GreaterThan(row.Hits));
            }
        }
        [Test] public void CapturedReportDoesNotChangeWhenSourceBalanceOrDraftChanges()
        {
            var balance=GameBalance.CreateDefault();var source=new TowerBlueprint("study","Study",ChassisKind.Heavy,StructureKind.Gun,StructureKind.Mortar,TowerModules.Tracking);
            var report=FootprintStudy.Run(source,1,balance);int hits=report.Cases[0].Hits;
            balance.Structure(StructureKind.Mortar).Radius=99;
            source=source.WithMortarFootprint(1,new MortarFootprint(3));
            Assert.That(report.Radius,Is.LessThan(99));Assert.That(report.Source.SecondFootprint.IsCircle,Is.True);Assert.That(report.Cases[0].Hits,Is.EqualTo(hits));
            Assert.That(report.Cases[0].Samples[0].Recording.Frames[0].Towers[0].Blueprint.FirstWeapon,Is.EqualTo(StructureKind.Gun));
        }
        [Test] public void LongFlightsAreBoundedAndIncompleteIsExplicit()
        {
            var balance=GameBalance.CreateDefault();balance.Structure(StructureKind.Mortar).FlightSeconds=30;
            var report=FootprintStudy.Run(new TowerBlueprint("study","Study",ChassisKind.Standard,StructureKind.Mortar),0,balance);
            Assert.That(report.Cases.All(row=>!row.Completed),Is.True);
            foreach(var row in report.Cases)foreach(var sample in row.Samples){Assert.That(sample.Recording.Frames.Count,Is.LessThanOrEqualTo(601));Assert.That(sample.Recording.Frames.Last().Impacts.Count,Is.Zero);}
        }
    }
}
