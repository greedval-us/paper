#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class AssemblyRewardTests
    {
        [Test] public void RepeatedVictory_GrantsOnce()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var runId = Guid.NewGuid();
                Assert.That(fixture.Run.RunRewards.Apply(runId, 8, true).Success, Is.True);
                Assert.That(fixture.Run.RunRewards.Apply(runId, 8, true).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(94));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(120));
                Assert.That(fixture.SavedData.Rewards.Count(receipt => receipt.RunId == runId.ToString("N")), Is.EqualTo(1));
                Assert.That(fixture.Run.HasPendingRunRewards, Is.False);
            }
        }

        [Test] public void CumulativeWaveTotalsPayOnlyThePreviouslyUnpaidDifference()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var runId = Guid.NewGuid();
                Assert.That(fixture.Run.RunRewards.Apply(runId, 1, false).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(8));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(10));
                Assert.That(fixture.Run.RunRewards.Apply(runId, 5, false).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(40));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(50));
                int saves = fixture.Store.SaveCalls;
                Assert.That(fixture.Run.RunRewards.Apply(runId, 2, false).Success, Is.True);
                Assert.That(fixture.Run.RunRewards.Apply(runId, 5, false).Success, Is.True);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(saves));
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(40));
                Assert.That(fixture.Run.RunRewards.Apply(runId, 8, false).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(64));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(80));
                Assert.That(fixture.Run.RunRewards.Apply(runId, 8, true).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(94));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(120));
            }
        }

        [Test] public void ReloadedReceiptPreventsARepeatedResultFromWritingOrPayingAgain()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var runId = Guid.NewGuid();
                Assert.That(fixture.Run.RunRewards.Apply(runId, 8, true).Success, Is.True);
                fixture.Restart();
                int saves = fixture.Store.SaveCalls;
                Assert.That(fixture.Run.RunRewards.Apply(runId, 8, true).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(94));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(120));
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(saves));
                Assert.That(fixture.SavedData.Rewards.Length, Is.EqualTo(1));
            }
        }

        [Test] public void DifferentRunIdsReceiveIndependentDurableReceipts()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Assert.That(fixture.Run.RunRewards.Apply(Guid.NewGuid(), 8, true).Success, Is.True);
                Assert.That(fixture.Run.RunRewards.Apply(Guid.NewGuid(), 3, false).Success, Is.True);
                fixture.Restart();
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(118));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(150));
                Assert.That(fixture.SavedData.Rewards.Length, Is.EqualTo(2));
            }
        }

        [Test] public void ZeroCompletedWavesAndAnEmptyRetryDoNotWriteOrPublish()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                int published = 0; fixture.Run.WorkshopChanged += () => published++;
                byte[] before = fixture.SavedBytes;
                Assert.That(fixture.Run.RunRewards.Apply(Guid.NewGuid(), 0, false).Success, Is.True);
                Assert.That(fixture.Run.RunRewards.RetryPending().Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.Zero);
                Assert.That(fixture.Run.Workshop.TotalXp, Is.Zero);
                Assert.That(fixture.SavedBytes, Is.EqualTo(before));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
                Assert.That(published, Is.Zero);
                Assert.That(fixture.Run.HasPendingRunRewards, Is.False);
            }
        }

        [TestCase(true, 1, false)]
        [TestCase(false, -1, false)]
        [TestCase(false, 9, false)]
        [TestCase(false, 7, true)]
        [TestCase(false, 0, true)]
        public void InvalidRewardInputRejectsWithoutChangingProfileOrDisk(bool emptyId, int waves, bool victory)
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                string before = fixture.State(); byte[] disk = fixture.SavedBytes;
                var result = fixture.Run.RunRewards.Apply(emptyId ? Guid.Empty : Guid.NewGuid(), waves, victory);
                Assert.That(result.Success, Is.False);
                Assert.That(result.Code, Is.Not.Empty);
                Assert.That(fixture.State(), Is.EqualTo(before));
                Assert.That(fixture.SavedBytes, Is.EqualTo(disk));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
                Assert.That(fixture.Run.HasPendingRunRewards, Is.False);
            }
        }

        [Test] public void RewardsCanCommitDuringBattleWithoutOpeningWorkshopEditing()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Assert.That(fixture.Run.StartRun(out var reason), Is.True, reason);
                Assert.That(fixture.Run.RunRewards.Apply(Guid.NewGuid(), 1, false).Success, Is.True);
                Assert.That(fixture.Run.InWorkshop, Is.False);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(8));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(10));
                Assert.That(fixture.SavedData.Rewards.Length, Is.EqualTo(1));
            }
        }

        [Test] public void RefusedRewardKeepsThePublishedProfileAndDiskAndExposesPendingWork()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                string before = fixture.State(); byte[] disk = fixture.SavedBytes;
                var runId = Guid.NewGuid(); int published = 0;
                fixture.Run.WorkshopChanged += () => published++;
                fixture.Store.RefuseWrites = true;
                Assert.That(fixture.Run.RunRewards.Apply(runId, 5, false).Success, Is.False);
                Assert.That(fixture.State(), Is.EqualTo(before));
                Assert.That(fixture.SavedBytes, Is.EqualTo(disk));
                Assert.That(fixture.SavedData.Rewards, Is.Empty);
                Assert.That(published, Is.Zero);
                Assert.That(fixture.Run.RunRewards.HasPending, Is.True);
                Assert.That(fixture.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.WriteFailed));
                Assert.That(fixture.Run.Workshop.HasUnsavedChanges, Is.True);
                fixture.Store.RefuseWrites = false;
                Assert.That(fixture.Run.RunRewards.RetryPending().Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(40));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(50));
                Assert.That(fixture.SavedData.Rewards.Single().Waves, Is.EqualTo(5));
                Assert.That(published, Is.EqualTo(1));
                Assert.That(fixture.Run.HasPendingRunRewards, Is.False);
                Assert.That(fixture.Run.Workshop.HasUnsavedChanges, Is.False);
            }
        }

        [Test] public void RepeatedWriteFailuresMergeTheHighestCumulativeRewardForOneRun()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var runId = Guid.NewGuid(); fixture.Store.RefuseWrites = true;
                Assert.That(fixture.Run.RunRewards.Apply(runId, 2, false).Success, Is.False);
                Assert.That(fixture.Run.RunRewards.Apply(runId, 8, true).Success, Is.False);
                Assert.That(fixture.Run.RunRewards.Apply(runId, 3, false).Success, Is.False);
                Assert.That(fixture.Run.Workshop.Gold, Is.Zero);
                fixture.Store.RefuseWrites = false;
                Assert.That(fixture.Run.RunRewards.RetryPending().Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(94));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(120));
                Assert.That(fixture.SavedData.Rewards.Single().Waves, Is.EqualTo(8));
                Assert.That(fixture.SavedData.Rewards.Single().VictoryBonus, Is.True);
            }
        }

        [Test] public void FailedRewardThenPurchaseThenRetryPreservesBothTransactions()
        {
            using (var fixture = new AssemblyCommandFixture(gold: 94, xp: 390))
            {
                var runId = Guid.NewGuid(); fixture.Store.RefuseWrites = true;
                Assert.That(fixture.Run.RunRewards.Apply(runId, 3, false).Success, Is.False);
                fixture.Store.RefuseWrites = false;
                Assert.That(fixture.Commands.PurchasePart("head.long", Guid.NewGuid()).Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(14));
                Assert.That(fixture.Run.Workshop.IsUnlocked("head.long"), Is.True);
                Assert.That(fixture.Run.HasPendingRunRewards, Is.True);
                Assert.That(fixture.Run.Workshop.HasUnsavedChanges, Is.True);
                Assert.That(fixture.Run.RunRewards.RetryPending().Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(38));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(420));
                Assert.That(fixture.Run.Workshop.IsUnlocked("head.long"), Is.True);
                fixture.Restart();
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(38));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(420));
                Assert.That(fixture.Run.Workshop.IsUnlocked("head.long"), Is.True);
                Assert.That(fixture.SavedData.Operations.Length, Is.EqualTo(1));
                Assert.That(fixture.SavedData.Rewards.Single().Waves, Is.EqualTo(3));
            }
        }

        [Test] public void RetryCommitsOutstandingRunsTogetherWithoutDroppingEitherReceipt()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                fixture.Store.RefuseWrites = true;
                Assert.That(fixture.Run.RunRewards.Apply(Guid.NewGuid(), 2, false).Success, Is.False);
                Assert.That(fixture.Run.RunRewards.Apply(Guid.NewGuid(), 3, false).Success, Is.False);
                int published = 0; fixture.Run.WorkshopChanged += () => published++;
                fixture.Store.RefuseWrites = false;
                Assert.That(fixture.Run.RunRewards.RetryPending().Success, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(40));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(50));
                Assert.That(fixture.SavedData.Rewards.Length, Is.EqualTo(2));
                Assert.That(published, Is.EqualTo(1));
                int saves = fixture.Store.SaveCalls;
                Assert.That(fixture.Run.RunRewards.RetryPending().Success, Is.True);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(saves));
            }
        }

        [Test] public void ReturningToWorkshopKeepsAFailedRewardAvailableForRetry()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                Assert.That(fixture.Run.StartRun(out var reason), Is.True, reason);
                fixture.Store.RefuseWrites = true;
                Assert.That(fixture.Run.RunRewards.Apply(Guid.NewGuid(), 5, false).Success, Is.False);
                fixture.Run.OpenWorkshop();
                Assert.That(fixture.Run.InWorkshop, Is.True);
                Assert.That(fixture.Run.HasPendingRunRewards, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.Zero);
                fixture.Store.RefuseWrites = false;
                Assert.That(fixture.Run.RetryPendingRewards(out reason), Is.True, reason);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(40));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(50));
            }
        }

        [Test] public void RetrySaveAlsoFlushesPendingRewardsInsteadOfSavingOnlyTheOldBalance()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                fixture.Store.RefuseWrites = true;
                Assert.That(fixture.Run.RunRewards.Apply(Guid.NewGuid(), 1, false).Success, Is.False);
                fixture.Store.RefuseWrites = false;
                Assert.That(fixture.Run.RetrySave(out var reason), Is.True, reason);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(8));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(10));
                Assert.That(fixture.SavedData.Rewards.Single().Waves, Is.EqualTo(1));
                Assert.That(fixture.Run.HasPendingRunRewards, Is.False);
            }
        }

        [Test] public void AFailedPendingRewardPreventsReplacingTheCurrentProfileOrStore()
        {
            using (var fixture = new AssemblyCommandFixture(gold: 23, xp: 17))
            {
                fixture.Store.RefuseWrites = true;
                Assert.That(fixture.Run.RunRewards.Apply(Guid.NewGuid(), 1, false).Success, Is.False);
                var workshop = fixture.Run.Workshop; byte[] disk = fixture.SavedBytes;
                Assert.Throws<InvalidOperationException>(() => fixture.Run.InitializeWorkshop(false));
                Assert.That(fixture.Run.Workshop, Is.SameAs(workshop));
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(23));
                Assert.That(fixture.Run.HasPendingRunRewards, Is.True);
                Assert.That(fixture.SavedBytes, Is.EqualTo(disk));
                fixture.Store.RefuseWrites = false;
                Assert.That(fixture.Run.RetryPendingRewards(out var reason), Is.True, reason);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(31));
                Assert.That(fixture.SavedData.Gold, Is.EqualTo(31));
            }
        }

        [Test] public void SubscriberFailureAfterCommitDoesNotKeepOrDuplicateThePaidIntent()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var runId = Guid.NewGuid();
                var failure = new ArgumentException("Injected reward observer failure");
                Action subscriber = () => throw failure;
                fixture.Run.WorkshopChanged += subscriber;
                try { Assert.That(Assert.Throws<ArgumentException>(() => fixture.Run.RunRewards.Apply(runId, 8, true)), Is.SameAs(failure)); }
                finally { fixture.Run.WorkshopChanged -= subscriber; }
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(94));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(120));
                Assert.That(fixture.Run.HasPendingRunRewards, Is.False);
                Assert.That(fixture.Run.Workshop.HasUnsavedChanges, Is.False);
                Assert.That(fixture.SavedData.Rewards.Length, Is.EqualTo(1));
                int saves = fixture.Store.SaveCalls;
                Assert.That(fixture.Run.RunRewards.Apply(runId, 8, true).Success, Is.True);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(saves));
            }
        }

        [Test] public void ProtectedProfilesRejectRewardsWithoutPublishingOrWritingAndKeepTheIntent()
        {
            using (var fixture = new AssemblyCommandFixture(gold: 23, xp: 17))
            {
                fixture.Run.InitializeWorkshop(new ProtectedStore(fixture.Store));
                byte[] disk = fixture.SavedBytes; int saves = fixture.Store.SaveCalls;
                Assert.That(fixture.Run.RunRewards.Apply(Guid.NewGuid(), 1, false).Success, Is.False);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(23));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(17));
                Assert.That(fixture.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.RecoveryRequired));
                Assert.That(fixture.Run.HasPendingRunRewards, Is.True);
                Assert.That(fixture.Run.Workshop.HasUnsavedChanges, Is.True);
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(saves));
                Assert.That(fixture.SavedBytes, Is.EqualTo(disk));
            }
        }

        [Test] public void ADirectlyConstructedServiceRegistersItsPendingRewardsWithTheController()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var service = new RunRewardService(fixture.Run);
                fixture.Store.RefuseWrites = true;
                Assert.That(service.Apply(Guid.NewGuid(), 1, false).Success, Is.False);
                Assert.That(fixture.Run.HasPendingRunRewards, Is.True);
                Assert.That(fixture.Run.RunRewards, Is.SameAs(service));
                fixture.Store.RefuseWrites = false;
                Assert.That(fixture.Run.RetrySave(out var reason), Is.True, reason);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(8));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(10));
                Assert.That(service.HasPending, Is.False);
                Assert.That(fixture.SavedData.Rewards.Single().Waves, Is.EqualTo(1));
            }
        }

        [Test] public void ASecondServiceCannotCreateAnIndependentPendingQueueForTheSameProfile()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var current = fixture.Run.RunRewards;
                Assert.Throws<InvalidOperationException>(() => new RunRewardService(fixture.Run));
                Assert.That(fixture.Run.RunRewards, Is.SameAs(current));
            }
        }

        [Test] public void DirectlyConstructedServicePendingRewardsPreventDiscardingTheProfile()
        {
            using (var fixture = new AssemblyCommandFixture(gold: 23, xp: 17))
            {
                var service = new RunRewardService(fixture.Run);
                fixture.Store.RefuseWrites = true;
                Assert.That(service.Apply(Guid.NewGuid(), 1, false).Success, Is.False);
                var workshop = fixture.Run.Workshop;
                Assert.Throws<InvalidOperationException>(() => fixture.Run.InitializeWorkshop(false));
                Assert.That(fixture.Run.Workshop, Is.SameAs(workshop));
                Assert.That(service.HasPending, Is.True);
                fixture.Store.RefuseWrites = false;
                Assert.That(fixture.Run.RetryPendingRewards(out var reason), Is.True, reason);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(31));
            }
        }

        [Test] public void AServiceCachedBeforeProfileReplacementCannotClearCurrentPendingStatus()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var stale = fixture.Run.RunRewards;
                fixture.Run.InitializeWorkshop(fixture.Store);
                var current = fixture.Run.RunRewards;
                fixture.Store.RefuseWrites = true;
                Assert.That(current.Apply(Guid.NewGuid(), 1, false).Success, Is.False);
                Assert.That(stale.RetryPending().Success, Is.False);
                Assert.That(fixture.Run.RunRewards, Is.SameAs(current));
                Assert.That(fixture.Run.HasPendingRunRewards, Is.True);
                Assert.That(fixture.Run.Workshop.HasUnsavedChanges, Is.True);
                Assert.That(fixture.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.WriteFailed));
                fixture.Store.RefuseWrites = false;
                Assert.That(fixture.Run.RetrySave(out var reason), Is.True, reason);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(8));
            }
        }

        [Test] public void AServiceCachedBeforeProfileReplacementCannotPayIntoTheNewProfile()
        {
            using (var fixture = new AssemblyCommandFixture(gold: 23, xp: 17))
            {
                var stale = fixture.Run.RunRewards;
                fixture.Run.InitializeWorkshop(fixture.Store);
                string before = fixture.State(); byte[] disk = fixture.SavedBytes;
                int saves = fixture.Store.SaveCalls;
                Assert.That(stale.Apply(Guid.NewGuid(), 8, true).Success, Is.False);
                Assert.That(fixture.State(), Is.EqualTo(before));
                Assert.That(fixture.SavedBytes, Is.EqualTo(disk));
                Assert.That(fixture.Store.SaveCalls, Is.EqualTo(saves));
                Assert.That(fixture.Run.HasPendingRunRewards, Is.False);
            }
        }

        [Test] public void RewardQueuedByACommitObserverMustFinishBeforeProfileReplacement()
        {
            using (var fixture = new AssemblyCommandFixture(gold: 23, xp: 17))
            {
                var runId = Guid.NewGuid();
                fixture.Store.RefuseWrites = true;
                Assert.That(fixture.Run.RunRewards.Apply(runId, 1, false).Success, Is.False);
                fixture.Store.RefuseWrites = false;
                bool newerRewardRejected = false;
                Action subscriber = null;
                subscriber = () => {
                    fixture.Run.WorkshopChanged -= subscriber;
                    fixture.Store.RefuseWrites = true;
                    newerRewardRejected = !fixture.Run.RunRewards.Apply(runId, 2, false).Success;
                };
                fixture.Run.WorkshopChanged += subscriber;
                try { Assert.Throws<InvalidOperationException>(() => fixture.Run.InitializeWorkshop(false)); }
                finally { fixture.Run.WorkshopChanged -= subscriber; }
                Assert.That(newerRewardRejected, Is.True);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(31));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(27));
                Assert.That(fixture.Run.HasPendingRunRewards, Is.True);
                Assert.That(fixture.Run.Workshop.HasUnsavedChanges, Is.True);
                fixture.Store.RefuseWrites = false;
                Assert.That(fixture.Run.RetryPendingRewards(out var reason), Is.True, reason);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(39));
                Assert.That(fixture.Run.Workshop.TotalXp, Is.EqualTo(37));
                Assert.That(fixture.SavedData.Rewards.Single().Waves, Is.EqualTo(2));
            }
        }

        sealed class ProtectedStore : IWorkshopProfileStore
        {
            readonly IWorkshopProfileStore source;
            internal ProtectedStore(IWorkshopProfileStore source) { this.source = source; }
            public ProfileLoadResult Load()
            {
                var loaded = source.Load();
                return new ProfileLoadResult(loaded.Profile, ProfileLoadSource.RecoveryRequired, "Injected protected profile", false);
            }
            public ProfileSaveResult Save(WorkshopProfile profile) => source.Save(profile);
        }
    }
}
#endif
