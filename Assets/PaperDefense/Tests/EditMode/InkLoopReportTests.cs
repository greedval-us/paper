using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Validation;

namespace PaperDefense.Tests
{
    public sealed class InkLoopReportTests
    {
        static InkLoopScenario SuppliedScenario(GameBalance balance, IReadOnlyList<TowerBlueprint> pages)
        {
            var constructor=typeof(InkLoopScenario).GetConstructor(new[]{typeof(GameBalance),typeof(int),typeof(IReadOnlyList<TowerBlueprint>)});
            Assert.That(constructor,Is.Not.Null,"Validation campaigns must accept an optional four-page loadout.");
            return (InkLoopScenario)constructor.Invoke(new object[]{balance,1337,pages});
        }
        static GameBalance SuppliedBalance()
        {
            var balance=GameBalance.CreateDefault();balance.Preset=GameBalancePreset.Target;
            balance.Width=40;balance.Height=32;balance.EnableInkWells=false;balance.StartingInk=2000;
            balance.PassiveInkPerSecond=balance.EarlyLaunchInkPerSecond=0;
            balance.Waves=new List<WaveDefinition>{new WaveDefinition{Count=0}};
            balance.Content=LegacyContentCatalog.Create(balance);
            return balance;
        }
        [Test] public void SuppliedCampaignLoadoutOwnsItsListAndBuildsPaidPagesAgainstSessionContent()
        {
            var balance=SuppliedBalance();var pages=new List<TowerBlueprint>();
            var expected=new HashSet<string>();
            foreach(var page in new WorkshopProfile().GetLoadoutBlueprints().Take(ProfileLimits.HistoricalLoadoutSlotCount))
            {var copy=page.AsVariant("supplied."+pages.Count,"Supplied "+pages.Count);pages.Add(copy);expected.Add(copy.Id);}
            var scenario=SuppliedScenario(balance,pages);
            pages[1]=new TowerBlueprint("mutated","Mutated",ChassisKind.Standard,StructureKind.Gun);
            scenario.Step();int towers=0;
            foreach(var structure in scenario.Session.Structures)
            {
                if(structure.Blueprint==null)continue;towers++;
                Assert.That(expected.Contains(structure.Blueprint.Id),Is.True);
                Assert.That(structure.Blueprint.Content,Is.SameAs(scenario.Session.Balance.Content));
                Assert.That(structure.PaidInk,Is.EqualTo(TowerCatalog.Cost(structure.Blueprint,balance,MaterialTier.Pencil)));
            }
            Assert.That(towers,Is.GreaterThan(0));Assert.That(scenario.Session.Ink,Is.LessThan(balance.StartingInk));
            Assert.That(scenario.Report.FailedCommands,Is.Zero);
        }
        [TestCase("count")] [TestCase("null")] [TestCase("invalid")]
        public void SuppliedCampaignRejectsIncompleteOrInvalidLoadout(string defect)
        {
            var balance=SuppliedBalance();var pages=new List<TowerBlueprint>(new WorkshopProfile().GetLoadoutBlueprints().Take(ProfileLimits.HistoricalLoadoutSlotCount));
            if(defect=="count")pages.RemoveAt(3);
            if(defect=="null")pages[1]=null;
            if(defect=="invalid")pages[1]=new TowerBlueprint("invalid","Invalid",ChassisKind.Standard,StructureKind.Gun).WithWeaponMount(2,"weapon.gun");
            var error=Assert.Throws<System.Reflection.TargetInvocationException>(()=>SuppliedScenario(balance,pages));
            Assert.That(error.InnerException,Is.InstanceOf<System.ArgumentException>());
        }
        [Test] public void ExplicitNullCampaignLoadoutKeepsDefaultReportAndCommandsUnchanged()
        {
            var balance=SuppliedBalance();var original=new InkLoopScenario(balance);
            var explicitNull=SuppliedScenario(balance,null);
            original.Step();explicitNull.Step();
            Assert.That(UnityEngine.JsonUtility.ToJson(explicitNull.Report),Is.EqualTo(UnityEngine.JsonUtility.ToJson(original.Report)));
        }

        static GameBalance IndexedCampaignBalance(bool wells=false)
        {
            var balance=SuppliedBalance();balance.EnableInkWells=wells;balance.WellInkPerWave=100;
            if(wells)balance.Waves.Add(new WaveDefinition{Count=0});
            var input=LegacyContentCatalog.CreateInput(balance);
            input.Templates.Add(new DrawingTemplateDefinitionInput{Id="template.campaign",DisplayName="Campaign",Weight=0});
            foreach(var chassis in input.Chassis){chassis.TemplateId="template.campaign";chassis.BuildSeconds=chassis.SecondaryBuildSeconds=0;}
            foreach(var weapon in input.Weapons){weapon.TemplateId="template.campaign";weapon.Stats.BuildSeconds=0;}
            foreach(var module in input.Modules){module.TemplateId="template.campaign";module.BuildSeconds=0;}
            var heavy=input.Chassis.Find(c=>c.Id=="chassis.heavy");heavy.MountCount=3;heavy.Capacity=100;
            var mortar=input.Weapons.Find(w=>w.Id=="weapon.mortar");mortar.MinimumRange=6;mortar.Stats.Range=18;
            var gun=input.Weapons.Find(w=>w.Id=="weapon.gun");gun.MinimumRange=6;gun.Stats.Range=8;
            input.Modules.Find(m=>m.Id=="module.rail").Modifiers=new List<StatModifierInput>{
                new StatModifierInput{Stat=ModifierStat.WeaponRange,Operation=ModifierOperation.Multiply,Scope=ModifierScope.Mount,MountIndex=2,Value=1.25f}};
            balance.Content=input.CreateSnapshot();return balance;
        }
        static bool Covered(InkLoopScenario scenario,Point2 target)
            =>(bool)typeof(InkLoopScenario).GetMethod("CoveredByTargetTower",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(scenario,new object[]{target});
        [Test] public void CampaignCoverageUsesThirdMountScopedRangeAndItsMaximumBoundary()
        {
            var balance=IndexedCampaignBalance();var scenario=new InkLoopScenario(balance);
            var page=new TowerBlueprint("coverage","Coverage","chassis.heavy",new[]{new WeaponMountSelection(0,"weapon.gun"),new WeaponMountSelection(2,"weapon.mortar")},
                new[]{new ModuleSelection("rail","module.rail")},content:balance.Content);
            Assert.That(scenario.Session.TryBuildBlueprint(new[]{new Cell(10,10)},page,MaterialTier.Pencil,InkColor.None,out string reason),Is.True,reason);
            Assert.That(Covered(scenario,new Point2(33,10.5f)),Is.True,"The third mount reaches its scoped maximum of22.5 cells.");
            Assert.That(Covered(scenario,new Point2(33.01f,10.5f)),Is.False);
        }
        [Test] public void CampaignCoverageRejectsMortarDeadzoneAndIncludesItsMinimumBoundary()
        {
            var balance=IndexedCampaignBalance();var scenario=new InkLoopScenario(balance);
            var page=new TowerBlueprint("deadzone","Deadzone","chassis.heavy","weapon.mortar",content:balance.Content);
            Assert.That(scenario.Session.TryBuildBlueprint(new[]{new Cell(10,10)},page,MaterialTier.Pencil,InkColor.None,out string reason),Is.True,reason);
            Assert.That(Covered(scenario,new Point2(16.49f,10.5f)),Is.False);
            Assert.That(Covered(scenario,new Point2(16.5f,10.5f)),Is.True);
        }
        [TestCase(5f,false)] [TestCase(7f,true)]
        public void CampaignPaintFindsEligibleThirdMountAfterMortarAndRespectsItsDeadzone(float distance,bool paint)
        {
            var balance=IndexedCampaignBalance(true);var scenario=new InkLoopScenario(balance);
            scenario.Session.StartNextWave();scenario.Session.Tick(1f/30);
            var page=new TowerBlueprint("paint","Paint","chassis.heavy",new[]{new WeaponMountSelection(0,"weapon.mortar"),new WeaponMountSelection(2,"weapon.gun")},content:balance.Content);
            var cell=new Cell(10,10);
            Assert.That(scenario.Session.TryBuildBlueprint(new[]{cell},page,MaterialTier.Pencil,InkColor.None,out string reason),Is.True,reason);
            ((List<EnemyState>)scenario.Session.Enemies).Add(new EnemyState{Id=1,Health=100,MaxHealth=100,Position=new Point2(10.5f+distance,10.5f)});
            typeof(InkLoopScenario).GetMethod("TargetPaintAndBlotter",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(scenario,null);
            Assert.That(scenario.Session.StructureAt(cell).Color,Is.EqualTo(paint?InkColor.Blue:InkColor.None));
        }

        [Test]
        public void TargetCampaignEarnsBlueInkEarlyEnoughToSlowLaterEnemies()
        {
            var balance = GameBalancePresets.CreateTargetWorkshop();
            for(int i=0;i<balance.Waves.Count;i++)
            {
                if(i>=2)balance.Waves[i].SpitterFraction=System.Math.Min(.16f,.04f*(i-1));
                if(i>=1)balance.Waves[i].OutlinedFraction=System.Math.Min(.35f,.08f*i);
                if(i>=4)balance.Waves[i].HatchedFraction=System.Math.Min(.2f,.05f*(i-3));
            }
            var scenario = new InkLoopScenario(balance);
            while (!scenario.Finished) scenario.Step();

            Assert.That(scenario.Report.CampaignPassed, Is.True);
            Assert.That(scenario.Report.MechanicsObserved, Is.True,
                "The campaign must recapture a well, earn a whole-wave payout and slow a real later enemy.");
            Assert.That(scenario.Report.ColoredInkSpent, Is.GreaterThan(0));
            Assert.That(scenario.Report.PeakSlowedEnemies, Is.GreaterThan(0));
            Assert.That(scenario.Report.FailedCommands, Is.Zero);
        }

        [Test]
        public void TargetCampaignClearsTheFirstSpitterWaveWithinFiniteEarnedInk()
        {
            var balance = GameBalancePresets.CreateTargetWorkshop();
            balance.Waves = balance.Waves.GetRange(0, 3);
            balance.Waves[1].OutlinedFraction = .08f;
            balance.Waves[2].OutlinedFraction = .16f;
            balance.Waves[2].SpitterFraction = .04f;
            var scenario = new InkLoopScenario(balance);
            while (!scenario.Finished && scenario.Session.TickIndex < 10000) scenario.Step();

            Assert.That(scenario.Session.Phase, Is.EqualTo(RunPhase.Victory),
                "A surviving ranged attacker must be reachable without waiting-income or injected resources.");
            Assert.That(scenario.Session.Kills, Is.EqualTo(380));
            Assert.That(scenario.Report.PursuitTowersBuilt, Is.GreaterThan(0));
            Assert.That(scenario.Report.FailedCommands, Is.Zero);
            Assert.That(balance.PassiveInkPerSecond, Is.Zero);
            Assert.That(balance.EarlyLaunchInkPerSecond, Is.Zero);
            Assert.That(scenario.Session.Ink, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void ReportDistinguishesExplicitZeroAndCustomGrantsFromTheHistoricalFallback()
        {
            var balance = GameBalance.CreateDefault();
            balance.Preset = GameBalancePreset.Target;
            balance.Width = 40; balance.Height = 24; balance.EnableInkWells = false;
            balance.StartingInk = 0; balance.PlanningSeconds = 1000;
            balance.PassiveInkPerSecond = balance.EarlyLaunchInkPerSecond = 0;
            balance.MaxEarlyLaunchInk = 0; balance.WaveInkGrant = 75;
            balance.Waves = new List<WaveDefinition>
            {
                new WaveDefinition { Count = 0, InkGrant = -1 },
                new WaveDefinition { Count = 0, InkGrant = 321 },
                new WaveDefinition { Count = 0, InkGrant = 0 }
            };
            var scenario = new InkLoopScenario(balance);
            Assert.That(scenario.Report.Preset, Is.EqualTo("Target"));
            Assert.That(scenario.Report.WaveInkGrant, Is.EqualTo(75));
            int[] configured = { -1, 321, 0 }, resolved = { 75, 321, 0 };
            for (int i = 0; i < configured.Length; i++)
            {
                Assert.That(scenario.Report.WaveTuning[i].ConfiguredInkGrant, Is.EqualTo(configured[i]));
                Assert.That(scenario.Report.WaveTuning[i].InkGrant, Is.EqualTo(resolved[i]));
                scenario.Step();
            }
            Assert.That(scenario.Report.Waves.Count, Is.EqualTo(3));
            for (int i = 0; i < configured.Length; i++)
            {
                Assert.That(scenario.Report.Waves[i].ConfiguredInkGrant, Is.EqualTo(configured[i]));
                Assert.That(scenario.Report.Waves[i].InkGrant, Is.EqualTo(resolved[i]));
                Assert.That(balance.Waves[i].InkGrant, Is.EqualTo(configured[i]));
            }
        }
    }
}
