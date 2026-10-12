using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
namespace PaperDefense.Tests
{
    public sealed class MortarFootprintProbeIntegrationTests
    {
        [Test] public void WorkshopProbeLeavesProfileLoadoutCurrencyAndSessionUntouched()
        {
            var root=new GameObject("Isolated footprint probe"); root.SetActive(false);
            try
            {
                var controller=root.AddComponent<RunController>(); controller.InitializeWorkshop(false, allowLegacyFixtureDefaults: true);
                int gold=controller.Workshop.Gold,ink=controller.Session.Ink,album=controller.Workshop.Album.Count;
                long tick=controller.Session.TickIndex;
                var selected=controller.Workshop.Loadout[3];
                var result=controller.CompareMortarFootprints(selected,0,MortarFootprint.Circle,new MortarFootprint(3),FootprintFormation.Group);
                Assert.That(result.First.Completed,Is.True);
                Assert.That(controller.Workshop.Gold,Is.EqualTo(gold)); Assert.That(controller.Session.Ink,Is.EqualTo(ink));
                Assert.That(controller.Session.TickIndex,Is.EqualTo(tick)); Assert.That(controller.Workshop.Album.Count,Is.EqualTo(album));
                Assert.That(controller.Workshop.Loadout[3],Is.SameAs(selected));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
