using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;

namespace PaperDefense.Tests
{
    public sealed class DesignChangeFeedbackTests
    {
        [Test] public void ScalingTheAuthoredContourDoesNotChangeTheComparedWorkingSize()
        {
            var shape=new[]{new Point2(-.5f,-1),new Point2(.5f,-1),new Point2(.5f,1),new Point2(-.5f,1)};
            var scaled=shape.Select(p=>new Point2(p.X*1.5f,p.Y*1.5f)).ToArray();
            string first=TextResourcesAsset.Default.Resolve(DesignChangeFeedback.Footprint(MortarFootprint.Circle,MortarFootprint.FromContour(shape),2.4f));
            string second=TextResourcesAsset.Default.Resolve(DesignChangeFeedback.Footprint(MortarFootprint.Circle,MortarFootprint.FromContour(scaled),2.4f));
            Assert.That(second,Is.EqualTo(first));Assert.That(first,Does.Contain("18,1"));
        }
        [Test] public void VolleyComparisonReportsDistributionWithoutInventingNewPellets()
        {
            var definition=GameBalance.CreateDefault().ResolveContent().GetWeapon("weapon.fan");
            var a=new FanVolley(new[]{FanGuide.FromAngle(-30),FanGuide.FromAngle(30)});
            var b=new FanVolley(new[]{FanGuide.FromAngle(-30),FanGuide.FromAngle(0),FanGuide.FromAngle(30)});
            string message=TextResourcesAsset.Default.Resolve(DesignChangeFeedback.Volley(a,b,definition));
            Assert.That(message,Does.Contain("3+3 → 2+2+2"));Assert.That(message,Does.Contain("всего 6 снарядов"));
            Assert.That(TextResourcesAsset.Default.Resolve(DesignChangeFeedback.Volley(null,b,definition)),Does.Contain("Новый режим"));
            var bent=new FanVolley(new[]{new FanGuide(new[]{new Point2(0,0),new Point2(.7f,.5f),new Point2(0,2)})});
            Assert.That(TextResourcesAsset.Default.Resolve(DesignChangeFeedback.Volley(new FanVolley(new[]{FanGuide.FromAngle(0)}),bent,definition)),Does.Contain("без изменений"));
        }
        [Test] public void HistoricalArmorComparisonStillAppliesTheDefenseCap()
        {
            string capped=TextResourcesAsset.Default.Resolve(DesignChangeFeedback.Armor(
                new ArmorDistribution(new[]{8,0,0,0}),new ArmorDistribution(new[]{12,0,0,0}),.25f,.6f));
            Assert.That(capped,Does.Contain("60% → 60%"));
            Assert.That(capped,Does.Contain("без изменений"));
        }
        [Test] public void HistoricalVolleyWithReducedPelletBudgetKeepsItsIncompatibleBaseline()
        {
            var rules=GameBalance.CreateDefault();
            var input=LegacyContentCatalog.CreateInput(rules);
            input.Weapons.Single(w=>w.Id=="weapon.fan").VolleyPellets=2;
            foreach(var item in input.Chassis)item.TemplateId="template.change";
            foreach(var item in input.Weapons)item.TemplateId="template.change";
            foreach(var item in input.Modules)item.TemplateId="template.change";
            input.Templates.Add(new DrawingTemplateDefinitionInput{Id="template.change",DisplayName="Sample",Weight=25,
                Contours=new List<DrawingContourInput>{new DrawingContourInput{Points=new List<DrawingCoordinateInput>{
                    new DrawingCoordinateInput(-.1f,0),new DrawingCoordinateInput(.1f,.4f)}}}});
            var content=input.CreateSnapshot();
            var saved=new TowerBlueprint("old-volley","Old",ChassisKind.Standard,StructureKind.Fan)
                .WithFanVolley(0,new FanVolley(new[]{FanGuide.FromAngle(-30),FanGuide.FromAngle(0),FanGuide.FromAngle(30)}))
                .WithContent(content);
            var repaired=saved.WithFanVolley(0,new FanVolley(new[]{FanGuide.FromAngle(-30),FanGuide.FromAngle(30)}));
            var weapon=content.GetWeapon("weapon.fan");
            string Compare(TowerBlueprint before,TowerBlueprint after)=>TextResourcesAsset.Default.Resolve(
                DesignChangeFeedback.Volley(before.GetFanVolley(0),after.GetFanVolley(0),weapon));
            Assert.That(Compare(saved,saved),Does.Contain("недоступно"));
            Assert.That(Compare(saved,repaired),Does.Contain("недоступно"),
                "An incompatible historical baseline must not silently become the repaired recipe.");
            Assert.That(saved.GetFanVolley(0).Guides.Count,Is.EqualTo(3));
            Assert.That(repaired.GetFanVolley(0).Guides.Count,Is.EqualTo(2));
            Assert.That(Compare(repaired,repaired),Does.Contain("1+1 → 1+1"));
        }
    }
}
