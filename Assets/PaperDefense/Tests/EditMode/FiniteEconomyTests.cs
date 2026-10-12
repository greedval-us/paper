using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class FiniteEconomyTests
    {
        static GameBalance Finite()
        {
            var balance = GameBalance.CreateDefault();
            balance.Width = 20; balance.Height = 16;
            balance.StartingInk = 360;
            balance.PlanningSeconds = 10000; balance.IntermissionSeconds = .5f;
            balance.PassiveInkPerSecond = balance.EarlyLaunchInkPerSecond = 0;
            balance.MaxEarlyLaunchInk = 0;
            balance.Progression.RequireAssignedEnemyRewards = true;
            balance.Progression.SalvageSupplyMultiplier = 1;
            balance.Progression.SalvageWaveRewardMultiplier = 1.25f;
            balance.Waves.Clear();
            for (int i = 0; i < 8; i++) balance.Waves.Add(new WaveDefinition { Count = 0, InkGrant = 140 + 20 * i });
            return balance;
        }

        static void CompleteEmpty(GameSession session)
        { session.StartNextWave(); session.Tick(.01f); }

        [Test]
        public void WaitingDuringPlanningDoesNotGenerateInkOrGold()
        {
            var session = new GameSession(Finite(), starterDefenses: false);
            session.Tick(300);
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Planning));
            Assert.That(session.Ink, Is.EqualTo(360));
            Assert.That(session.GoldEarned, Is.Zero);
        }

        [Test]
        public void WaitingForAnUpgradeAfterItsCountdownDoesNotGenerateIncome()
        {
            var session = new GameSession(Finite(), starterDefenses: false);
            CompleteEmpty(session); CompleteEmpty(session);
            Assert.That(session.PendingUpgrade, Is.True);
            int ink = session.Ink, gold = session.GoldEarned;
            session.Tick(300);
            Assert.That(session.PendingUpgrade, Is.True);
            Assert.That(session.WaveIndex, Is.EqualTo(2));
            Assert.That(session.Ink, Is.EqualTo(ink));
            Assert.That(session.GoldEarned, Is.EqualTo(gold));
        }

        [Test]
        public void OneRemainingEnemyDoesNotGenerateIncome()
        {
            var balance = Finite(); balance.Enemy(EnemyKind.Doodle).Speed = 0;
            var session = new GameSession(balance, starterDefenses: false);
            session.StartNextWave();
            ((List<EnemyState>)session.Enemies).Add(new EnemyState { Id = 1, Kind = EnemyKind.Doodle,
                Health = 100, MaxHealth = 100, Position = new Point2(1.5f, 1.5f), RewardBudget = 0 });
            session.Tick(300);
            Assert.That(session.Phase, Is.EqualTo(RunPhase.Assault));
            Assert.That(session.Enemies.Count, Is.EqualTo(1));
            Assert.That(session.Ink, Is.EqualTo(360));
            Assert.That(session.GoldEarned, Is.Zero);
        }

        [Test]
        public void EachCompletedWavePaysItsOwnGrantAndGoldExactlyOnce()
        {
            var session = new GameSession(Finite(), starterDefenses: false);
            CompleteEmpty(session);
            Assert.That(session.Ink, Is.EqualTo(500));
            Assert.That(session.GoldEarned, Is.EqualTo(8));
            CompleteEmpty(session);
            Assert.That(session.Ink, Is.EqualTo(660));
            Assert.That(session.GoldEarned, Is.EqualTo(16));
            session.Tick(300);
            Assert.That(session.Ink, Is.EqualTo(660));
            Assert.That(session.GoldEarned, Is.EqualTo(16));
        }

        [Test]
        public void SalvageChangesFiniteWaveIncomeInsteadOfCreatingTimeIncome()
        {
            var session = new GameSession(Finite(), starterDefenses: false);
            CompleteEmpty(session); CompleteEmpty(session);
            int before = session.Ink;
            session.ChooseUpgrade(RunUpgrade.Salvage);
            Assert.That(session.Ink, Is.EqualTo(before + 60));
            CompleteEmpty(session);
            Assert.That(session.Ink, Is.EqualTo(before + 60 + 225));
        }

        [Test]
        public void OfficialSpawnFreezesItsRewardBeforeDefinitionChangesAndPaysOnce()
        {
            var balance = Finite();
            balance.Waves[0].Count = 2; balance.Waves[0].SpawnInterval = 1000;
            balance.Enemy(EnemyKind.Doodle).Reward = 7;
            var session = new GameSession(balance, starterDefenses: false);
            session.StartNextWave(); session.Tick(.01f);
            EnemyState enemy = session.Enemies[0];
            Assert.That(enemy.RewardBudget, Is.EqualTo(7));
            balance.Enemy(EnemyKind.Doodle).Reward = 99;
            int before = session.Ink;
            enemy.Health = 0;
            session.Tick(.01f);
            Assert.That(session.Ink, Is.EqualTo(before + 7));
            session.Tick(.01f);
            Assert.That(session.Ink, Is.EqualTo(before + 7));
            Assert.That(session.Kills, Is.EqualTo(1));
        }

        [TestCase(false, 3)]
        [TestCase(true, 0)]
        public void OnlyHistoricalFixturesCanReceiveUnassignedEnemyRewards(bool assignedRequired, int expected)
        {
            var balance = Finite(); balance.Progression.RequireAssignedEnemyRewards = assignedRequired;
            balance.Waves[0].Count = 2; balance.Waves[0].SpawnInterval = 1000;
            var session = new GameSession(balance, starterDefenses: false);
            session.StartNextWave(); session.Tick(.01f);
            ((List<EnemyState>)session.Enemies).Add(new EnemyState { Id = 99, Kind = EnemyKind.Gnawer,
                Health = 0, Position = new Point2(1.5f, 1.5f) });
            int before = session.Ink;
            session.Tick(.01f);
            Assert.That(session.Ink, Is.EqualTo(before + expected));
        }

        [Test]
        public void ARewardClaimIsConsumedOnceAndTransfersConserveTheOriginalBudget()
        {
            var parent = new EnemyState { RewardBudget = 7 };
            var child = new EnemyState();
            Assert.That(EnemyRewardBudget.Transfer(parent, child, 3), Is.True);
            Assert.That(parent.RewardBudget, Is.EqualTo(4));
            Assert.That(child.RewardBudget, Is.EqualTo(3));
            Assert.That(EnemyRewardBudget.Transfer(parent, new EnemyState(), 5), Is.False);
            Assert.That(EnemyRewardBudget.Transfer(parent, parent, 1), Is.False);
            Assert.That(EnemyRewardBudget.Transfer(parent, new EnemyState(), -1), Is.False);
            Assert.That(parent.RewardBudget + child.RewardBudget, Is.EqualTo(7));
            Assert.That(EnemyRewardBudget.Consume(parent, 99, true) + EnemyRewardBudget.Consume(child, 99, true), Is.EqualTo(7));
            Assert.That(EnemyRewardBudget.Consume(parent, 99, true) + EnemyRewardBudget.Consume(child, 99, true), Is.Zero);
            Assert.That(EnemyRewardBudget.Consume(new EnemyState(), 99, true), Is.Zero);
        }

        [Test]
        public void DamagedUpgradeAndEraseCannotReturnMoreThanThePaidInkOrRepairForFree()
        {
            var balance = Finite();
            balance.Structure(StructureKind.Gun).BuildSeconds = 0;
            var session = new GameSession(balance, starterDefenses: false);
            var cell = new Cell(6, 5);
            Assert.That(session.TryBuild(new[] { cell }, StructureKind.Gun, MaterialTier.Pencil, out _), Is.True);
            session.StructureAt(cell).Health *= .5f;
            Assert.That(session.TryUpgrade(cell, out _), Is.True);
            Assert.That(session.StructureAt(cell).Health / session.StructureAt(cell).MaxHealth, Is.EqualTo(.5f));
            Assert.That(session.TryUpgrade(cell, out _), Is.True);
            Assert.That(session.StructureAt(cell).Health / session.StructureAt(cell).MaxHealth, Is.EqualTo(.5f));
            Assert.That(session.TryErase(cell, out int refund), Is.True);
            Assert.That(refund, Is.EqualTo(12));
            Assert.That(session.Ink, Is.LessThan(360));
            Assert.That(session.TryErase(cell, out refund), Is.False);
            Assert.That(refund, Is.Zero);

            var free = new GameSession(balance, starterDefenses: true);
            Cell freeCell = free.Structures[0].Cell;
            Assert.That(free.TryErase(freeCell, out refund), Is.True);
            Assert.That(refund, Is.Zero);
            Assert.That(free.Ink, Is.EqualTo(360));
        }
    }
}
