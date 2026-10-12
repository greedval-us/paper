using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Validation;

namespace PaperDefense.Tests
{
    public sealed class InkLoopReportTests
    {
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
