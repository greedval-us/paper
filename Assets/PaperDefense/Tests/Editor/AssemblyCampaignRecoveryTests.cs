using System.Linq;
using NUnit.Framework;
using PaperDefense.Validation;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AssemblyCampaignRecoveryTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void RepairCampaign_WithSeed2026_ReachesAnOrdinaryTerminalOutcome(int generators)
        {
            AssemblyBalanceScenario.LoadRuntime(out var balance, out var catalog);
            string before = JsonUtility.ToJson(balance);
            var row = AssemblyBalanceScenario.RunCampaign(balance, catalog, 2026, generators, true);
            Assert.That(row.Outcome, Is.EqualTo("Victory").Or.EqualTo("Defeat"),
                "A fixed battery must use ordinary commands to escape its repaired-blocker stalemate.");
            Assert.That(JsonUtility.ToJson(balance), Is.EqualTo(before), "The policy must not alter live balance inputs.");
            Assert.That(row.StartingInk, Is.EqualTo(640));
            Assert.That(row.StartingBaseHealth, Is.EqualTo(250));
            Assert.That(row.Cost, Is.EqualTo(row.Placements.Sum(placement => placement.Cost)));
            Assert.That(row.Copies, Is.EqualTo(row.Placements.Length));
            Assert.That(row.Placements.Count(placement => placement.EntryId == "album.repair"), Is.EqualTo(1));
            Assert.That(row.RecoveryActions, Has.Length.EqualTo(1));
            var recovery = row.RecoveryActions[0];
            Assert.That(recovery.Command, Is.EqualTo("Erase"));
            Assert.That(recovery.EntryId, Is.EqualTo("album.repair"));
            Assert.That(recovery.NoKillSeconds, Is.GreaterThanOrEqualTo(30));
            Assert.That(recovery.LiveEnemyIds, Has.Length.EqualTo(1));
            Assert.That(recovery.Refund, Is.GreaterThan(0).And.LessThanOrEqualTo(65));
            Assert.That(recovery.InkAfter, Is.EqualTo(recovery.InkBefore + recovery.Refund));
            Assert.That(row.RefundedInk, Is.EqualTo(row.RecoveryActions.Sum(action => action.Refund)));
            Assert.That(row.SurvivingStructures,
                Is.EqualTo(row.Copies - row.DestroyedStructures - row.RecoveryActions.Length));
            Assert.That(row.BaseDamageTaken, Is.EqualTo(row.StartingBaseHealth - row.BaseHealth).Within(.001f));
        }

        [Test]
        public void RecoveryCampaign_ReplaysTheSamePaidCommandsAndOutcome()
        {
            AssemblyBalanceScenario.LoadRuntime(out var balance, out var catalog);
            var first = AssemblyBalanceScenario.RunCampaign(balance, catalog, 2026, 0, true);
            var second = AssemblyBalanceScenario.RunCampaign(balance, catalog, 2026, 0, true);
            Assert.That(JsonUtility.ToJson(second), Is.EqualTo(JsonUtility.ToJson(first)));
        }

        [Test]
        public void HistoricalPolicy_StillReproducesTheCapturedRepairStalemate()
        {
            AssemblyBalanceScenario.LoadRuntime(out var balance, out var catalog);
            var row = AssemblyBalanceScenario.RunCampaign(balance, catalog, 2026, 0, true,
                recoverStalemates: false);
            Assert.That(row.Outcome, Is.EqualTo("Timeout"));
            Assert.That(row.CompletedWaves, Is.EqualTo(2));
            Assert.That(row.SpawnedEnemies - row.Kills, Is.EqualTo(1));
            Assert.That(row.BaseHealth, Is.EqualTo(240));
            Assert.That(row.RecoveryActions, Is.Empty);
            Assert.That(row.RefundedInk, Is.Zero);
        }
    }
}
