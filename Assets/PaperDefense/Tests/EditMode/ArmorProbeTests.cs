using System;
using NUnit.Framework;
using PaperDefense.Core;
namespace PaperDefense.Tests
{
    public sealed class ArmorProbeTests
    {
        [Test] public void SurroundingProbeUsesEachWorldSideAndSameFiniteHealth()
        {
            var rules=GameBalance.CreateDefault();rules.Construction.ArmorDefense=.25f;rules.Construction.FoundationHealth=100;rules.Construction.ArmorHealthMultiplier=1;rules.Effects.MaximumDefense=.6f;
            var page=new TowerBlueprint("probe","Probe",ChassisKind.Standard,StructureKind.Gun,modules:TowerModules.Armor)
                .WithReinforcement(new ArmorDistribution(new[]{12,0,0,0},1));
            var report=ArmorProbe.Run(page,rules);
            Assert.That(report.Hits.Count,Is.EqualTo(4));Assert.That(report.InitialHealth,Is.EqualTo(100));
            Assert.That(report.Hits[0].Damage,Is.EqualTo(20));Assert.That(report.Hits[1].Damage,Is.EqualTo(8).Within(.001));
            Assert.That(report.Hits[3].RemainingHealth,Is.EqualTo(32).Within(.001));
            Assert.That(report.Cost,Is.EqualTo(TowerCatalog.Cost(page,rules,MaterialTier.Pencil)));
            Assert.That(ArmorProbe.Run(page,rules,1).Hits.Count,Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>ArmorProbe.Run(page,rules,4));
        }
    }
}
