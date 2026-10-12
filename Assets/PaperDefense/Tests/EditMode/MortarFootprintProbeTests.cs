using System;
using NUnit.Framework;
using PaperDefense.Core;
namespace PaperDefense.Tests
{
    public sealed class MortarFootprintProbeTests
    {
        [TestCase(FootprintFormation.Group,9,3)]
        [TestCase(FootprintFormation.Column,6,9)]
        public void ProductionShotHasExpectedCoverageAndOwnedDeterministicFrames(FootprintFormation formation,int circle,int oval)
        {
            var balance=GameBalance.CreateDefault();
            var blueprint=TowerBlueprint.NewDraft("probe","Probe",ChassisKind.Heavy,StructureKind.Gun,StructureKind.Mortar,TowerModules.Tracking);
            var result=MortarFootprintProbe.Compare(blueprint,1,MortarFootprint.Circle,new MortarFootprint(3),balance,formation);
            var repeat=MortarFootprintProbe.Compare(blueprint,1,MortarFootprint.Circle,new MortarFootprint(3),balance,formation);
            Assert.That(result.First.Completed && result.Second.Completed,Is.True);
            Assert.That(result.First.HitCount,Is.EqualTo(circle)); Assert.That(result.Second.HitCount,Is.EqualTo(oval));
            Assert.That(result.First.MissedCount,Is.EqualTo(9-circle));
            Assert.That(result.First.Cost,Is.EqualTo(TowerCatalog.Cost(blueprint,balance,MaterialTier.Pencil)));
            Assert.That(result.Second.Cost,Is.EqualTo(result.First.Cost));
            Assert.That(result.Second.DrawingQuality,Is.EqualTo(result.First.DrawingQuality));
            Assert.That(result.First.DrawingQuality,Is.EqualTo(blueprint.Assessment.Quality));
            Assert.That(result.First.Frames.Count,Is.EqualTo(repeat.First.Frames.Count));
            for(int frame=0;frame<result.First.Frames.Count;frame++)
            {
                Assert.That(result.First.Frames[frame].Seconds,Is.EqualTo(repeat.First.Frames[frame].Seconds));
                for(int enemy=0;enemy<9;enemy++) Assert.That(result.First.Frames[frame].Enemies[enemy].Health,Is.EqualTo(repeat.First.Frames[frame].Enemies[enemy].Health));
            }
            for(int i=0;i<9;i++) Assert.That(result.First.Frames[0].Enemies[i].Position,Is.EqualTo(result.Second.Frames[0].Enemies[i].Position));
            Assert.That(result.First.Frames[0].Towers[0].Blueprint.Modules,Is.EqualTo(blueprint.Modules));
            Assert.That(result.First.Frames[0].Towers[0].Blueprint.Chassis,Is.EqualTo(blueprint.Chassis));
            Assert.That(result.First.Frames[0].Towers[0].Blueprint.Drawing,Is.SameAs(blueprint.Drawing));
            Assert.That(result.First.Frames[0].Towers[0].Blueprint.SecondWeapon,Is.Null);
            Assert.That(result.First.Frames,Is.Not.InstanceOf<TrialFrame[]>());
            int shells=0, impacts=0;
            foreach(var frame in result.Second.Frames) { shells=Math.Max(shells,frame.Projectiles.Count); impacts+=frame.Impacts.Count; }
            Assert.That(result.Second.Frames[result.Second.Frames.Count-1].Towers.Count,Is.Zero);
            Assert.That(result.Second.Frames[result.Second.Frames.Count-1].Seconds,Is.GreaterThan(result.Second.Frames[1].Seconds));
            Assert.That(shells,Is.EqualTo(1)); Assert.That(impacts,Is.EqualTo(1));
            Assert.That(result.Second.Frames[result.Second.Frames.Count-1].Impacts.Count,Is.EqualTo(1));
            blueprint=blueprint.WithMortarFootprint(1,MortarFootprint.Circle);
            balance.Structure(StructureKind.Mortar).Radius=99;
            Assert.That(result.Second.HitCount,Is.EqualTo(oval));
            Assert.That(result.Second.Frames[result.Second.Frames.Count-1].Impacts[0].ImpactFootprint.Value.ForwardRadius,Is.LessThan(99));
        }
        [Test] public void LongTrialRecordsBoundedOwnedRealMortarImpacts()
        {
            var report=TrialRunner.Run(new TowerBlueprint("mortar","Mortar",ChassisKind.Standard,StructureKind.Mortar),TrialScenario.Crowd);
            int impacts=0;
            foreach(var frame in report.Frames)
            {
                Assert.That(frame.Impacts.Count,Is.LessThanOrEqualTo(128));
                foreach(var impact in frame.Impacts) { impacts++; Assert.That(impact.Kind,Is.EqualTo(SimulationEventKind.ProjectileImpact)); Assert.That(impact.ImpactFootprint.HasValue,Is.True); }
                Assert.That(frame.Impacts,Is.Not.InstanceOf<SimulationEvent[]>());
            }
            Assert.That(impacts,Is.GreaterThan(0));
        }
        [Test] public void FlightCapReportsIncompleteAndDoesNotInventAnImpact()
        {
            var balance=GameBalance.CreateDefault(); balance.Structure(StructureKind.Mortar).FlightSeconds=100;
            var result=MortarFootprintProbe.Compare(new TowerBlueprint("probe","Probe",ChassisKind.Standard,StructureKind.Mortar),0,MortarFootprint.Circle,MortarFootprint.Circle,balance,FootprintFormation.Group).First;
            Assert.That(result.Completed,Is.False); Assert.That(result.Message,Is.Not.Empty);
            Assert.That(result.Frames.Count,Is.LessThanOrEqualTo(601)); Assert.That(result.HitCount,Is.Zero);
        }
    }
}



