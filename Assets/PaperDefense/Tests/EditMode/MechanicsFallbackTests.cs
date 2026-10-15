using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class MechanicsFallbackTests
    {
        [Test]
        public void DisabledWellsDoNotReserveObjectives()
        {
            var balance = GameBalancePresets.CreateTargetWorkshop();
            balance.Mechanics.InkWells = false;
            var run = new GameSession(balance, starterDefenses: false);
            Assert.That(run.Wells, Is.Empty);
        }

        [Test]
        public void DisabledUpgradesCannotBlockTheNextWave()
        {
            var balance = GameBalance.CreateDefault();
            balance.Mechanics.RunUpgrades = false;
            balance.Progression.UpgradeEveryWaves = 1;
            balance.Waves = new List<WaveDefinition> { new WaveDefinition { Count = 0 }, new WaveDefinition { Count = 0 } };
            var run = new GameSession(balance, starterDefenses: false);
            run.StartNextWave();
            run.Tick(1f / 30);
            Assert.That(run.PendingUpgrade, Is.False);
            run.StartNextWave();
            run.Tick(1f / 30);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Victory));
        }

    }
}
