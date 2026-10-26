using System;
using NUnit.Framework;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class RunRewardReceiptTests
    {
        static WorkshopProfile Profile(int gold = 0, int xp = 0)
        {
            var data = new WorkshopProfile().ToData(); data.Gold = gold; data.TotalXp = xp;
            return WorkshopProfile.FromData(data);
        }

        static WorkshopProfile RoundTrip(WorkshopProfile profile)
            => WorkshopProfile.FromData(JsonUtility.FromJson<WorkshopProfileData>(JsonUtility.ToJson(profile.ToData())));

        [Test] public void RepeatedVictory_GrantsOnce()
        {
            var profile = Profile(); var run = Guid.NewGuid();
            Assert.That(profile.TryApplyRunReward(run, 8, true, out var code), Is.True, code);
            Assert.That(profile.TryApplyRunReward(run, 8, true, out code), Is.True, code);
            Assert.That(profile.Gold, Is.EqualTo(94)); Assert.That(profile.TotalXp, Is.EqualTo(120));
            Assert.That(profile.ToData().Rewards.Length, Is.EqualTo(1));
            Assert.That(profile.FindRunReward(run).Waves, Is.EqualTo(8));
            Assert.That(profile.FindRunReward(run).VictoryBonus, Is.True);
        }

        [Test] public void FiveWavesWithoutVictory_GrantsOnlyCompletedWaves()
        {
            var profile = Profile(); var run = Guid.NewGuid();
            Assert.That(profile.TryApplyRunReward(run, 5, false, out var code), Is.True, code);
            Assert.That(profile.Gold, Is.EqualTo(40)); Assert.That(profile.TotalXp, Is.EqualTo(50));
            Assert.That(profile.FindRunReward(run).RunId, Is.EqualTo(run));
            Assert.That(profile.FindRunReward(run).Waves, Is.EqualTo(5));
            Assert.That(profile.FindRunReward(run).VictoryBonus, Is.False);
        }

        [Test] public void CumulativeProgress_GrantsOnlyNewWavesAndThenVictoryBonus()
        {
            var profile = Profile(); var run = Guid.NewGuid();
            Assert.That(profile.TryApplyRunReward(run, 1, false, out var code), Is.True, code);
            Assert.That(profile.Gold, Is.EqualTo(8)); Assert.That(profile.TotalXp, Is.EqualTo(10));
            Assert.That(profile.TryApplyRunReward(run, 5, false, out code), Is.True, code);
            Assert.That(profile.Gold, Is.EqualTo(40)); Assert.That(profile.TotalXp, Is.EqualTo(50));
            Assert.That(profile.TryApplyRunReward(run, 8, false, out code), Is.True, code);
            Assert.That(profile.Gold, Is.EqualTo(64)); Assert.That(profile.TotalXp, Is.EqualTo(80));
            Assert.That(profile.TryApplyRunReward(run, 8, true, out code), Is.True, code);
            Assert.That(profile.Gold, Is.EqualTo(94)); Assert.That(profile.TotalXp, Is.EqualTo(120));
            Assert.That(profile.ToData().Rewards.Length, Is.EqualTo(1));
        }

        [TestCase(0)] [TestCase(2)] [TestCase(5)]
        public void OlderOrRepeatedProgress_IsAnExactNoOp(int waves)
        {
            var profile = Profile(); var run = Guid.NewGuid();
            Assert.That(profile.TryApplyRunReward(run, 5, false, out var code), Is.True, code);
            string before = JsonUtility.ToJson(profile.ToData());
            Assert.That(profile.TryApplyRunReward(run, waves, false, out code), Is.True, code);
            Assert.That(JsonUtility.ToJson(profile.ToData()), Is.EqualTo(before));
        }

        [Test] public void OldNonVictoryProgress_CannotRemoveAnAlreadyPaidVictory()
        {
            var profile = Profile(); var run = Guid.NewGuid();
            Assert.That(profile.TryApplyRunReward(run, 8, true, out var code), Is.True, code);
            string before = JsonUtility.ToJson(profile.ToData());
            Assert.That(profile.TryApplyRunReward(run, 5, false, out code), Is.True, code);
            Assert.That(profile.TryApplyRunReward(run, 8, false, out code), Is.True, code);
            Assert.That(JsonUtility.ToJson(profile.ToData()), Is.EqualTo(before));
        }

        [Test] public void NoCompletedWaves_DoesNotCreateAReceipt()
        {
            var profile = Profile(19, 23); string before = JsonUtility.ToJson(profile.ToData());
            var run = Guid.NewGuid();
            Assert.That(profile.TryApplyRunReward(run, 0, false, out var code), Is.True, code);
            Assert.That(profile.FindRunReward(run), Is.Null);
            Assert.That(JsonUtility.ToJson(profile.ToData()), Is.EqualTo(before));
        }

        [Test] public void SeparateRunIdentities_EarnSeparateRewards()
        {
            var profile = Profile(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
            Assert.That(profile.TryApplyRunReward(first, 5, false, out var code), Is.True, code);
            Assert.That(profile.TryApplyRunReward(second, 8, true, out code), Is.True, code);
            Assert.That(profile.Gold, Is.EqualTo(134)); Assert.That(profile.TotalXp, Is.EqualTo(170));
            Assert.That(profile.ToData().Rewards.Length, Is.EqualTo(2));
            Assert.That(profile.FindRunReward(first).Waves, Is.EqualTo(5));
            Assert.That(profile.FindRunReward(second).VictoryBonus, Is.True);
        }

        [TestCase(-1)] [TestCase(9)] [TestCase(int.MinValue)] [TestCase(int.MaxValue)]
        public void InvalidWaveTotals_LeaveTheWholeProfileUnchanged(int waves)
        {
            var profile = Profile(7, 11); var run = Guid.NewGuid();
            Assert.That(profile.TryApplyRunReward(run, 5, false, out var code), Is.True, code);
            string before = JsonUtility.ToJson(profile.ToData());
            Assert.That(profile.TryApplyRunReward(run, waves, false, out code), Is.False);
            Assert.That(code, Is.EqualTo("profile.reward.waves"));
            Assert.That(JsonUtility.ToJson(profile.ToData()), Is.EqualTo(before));
        }

        [TestCase(0)] [TestCase(5)] [TestCase(7)]
        public void VictoryBeforeEightWaves_IsRejectedAtomically(int waves)
        {
            var profile = Profile(7, 11); var run = Guid.NewGuid();
            Assert.That(profile.TryApplyRunReward(run, 5, false, out var code), Is.True, code);
            string before = JsonUtility.ToJson(profile.ToData());
            Assert.That(profile.TryApplyRunReward(run, waves, true, out code), Is.False);
            Assert.That(code, Is.EqualTo("profile.reward.victory"));
            Assert.That(JsonUtility.ToJson(profile.ToData()), Is.EqualTo(before));
        }

        [Test] public void EmptyRunIdentity_IsRejectedAtomically()
        {
            var profile = Profile(7, 11); string before = JsonUtility.ToJson(profile.ToData());
            Assert.That(profile.TryApplyRunReward(Guid.Empty, 8, true, out var code), Is.False);
            Assert.That(code, Is.EqualTo("profile.reward.run"));
            Assert.That(profile.FindRunReward(Guid.Empty), Is.Null);
            Assert.That(JsonUtility.ToJson(profile.ToData()), Is.EqualTo(before));
        }

        [Test] public void ReceiptSnapshot_RemainsDetachedWhenProgressAdvances()
        {
            var profile = Profile(); var run = Guid.NewGuid();
            Assert.That(profile.FindRunReward(run), Is.Null);
            Assert.That(profile.TryApplyRunReward(run, 5, false, out var code), Is.True, code);
            RunRewardReceipt previous = profile.FindRunReward(run);
            var exported = profile.ToData(); exported.Rewards[0].Waves = 0; exported.Rewards[0].RunId = Guid.NewGuid().ToString("N");
            Assert.That(profile.FindRunReward(run).Waves, Is.EqualTo(5));
            Assert.That(profile.TryApplyRunReward(run, 8, true, out code), Is.True, code);
            Assert.That(previous.Waves, Is.EqualTo(5)); Assert.That(previous.VictoryBonus, Is.False);
            Assert.That(profile.FindRunReward(run), Is.Not.SameAs(previous));
            Assert.That(profile.FindRunReward(run).Waves, Is.EqualTo(8));
        }

        [Test] public void WorkingCopy_AdvancesReceiptsAndBalancesIndependently()
        {
            var original = Profile(); var run = Guid.NewGuid();
            Assert.That(original.TryApplyRunReward(run, 5, false, out var code), Is.True, code);
            var copy = original.CreateWorkingCopy();
            Assert.That(copy.TryApplyRunReward(run, 8, true, out code), Is.True, code);
            Assert.That(original.Gold, Is.EqualTo(40)); Assert.That(original.TotalXp, Is.EqualTo(50));
            Assert.That(original.FindRunReward(run).Waves, Is.EqualTo(5));
            Assert.That(copy.Gold, Is.EqualTo(94)); Assert.That(copy.TotalXp, Is.EqualTo(120));
            Assert.That(original.TryApplyRunReward(run, 6, false, out code), Is.True, code);
            Assert.That(original.Gold, Is.EqualTo(48)); Assert.That(original.TotalXp, Is.EqualTo(60));
            Assert.That(copy.FindRunReward(run).Waves, Is.EqualTo(8));
            Assert.That(copy.Gold, Is.EqualTo(94)); Assert.That(copy.TotalXp, Is.EqualTo(120));
        }

        [Test] public void JsonReload_PreservesReceiptsAndPaysOnlyTheRemainingDelta()
        {
            var profile = Profile(); var run = Guid.NewGuid();
            Assert.That(profile.TryApplyRunReward(run, 5, false, out var code), Is.True, code);
            var restored = RoundTrip(profile);
            Assert.That(restored.TryApplyRunReward(run, 5, false, out code), Is.True, code);
            Assert.That(restored.Gold, Is.EqualTo(40)); Assert.That(restored.TotalXp, Is.EqualTo(50));
            Assert.That(restored.TryApplyRunReward(run, 8, true, out code), Is.True, code);
            restored = RoundTrip(restored);
            Assert.That(restored.TryApplyRunReward(run, 8, true, out code), Is.True, code);
            Assert.That(restored.Gold, Is.EqualTo(94)); Assert.That(restored.TotalXp, Is.EqualTo(120));
            Assert.That(restored.ToData().Rewards[0].RunId, Is.EqualTo(run.ToString("N")));
            Assert.That(restored.ToData().Rewards.Length, Is.EqualTo(1));
        }

        [Test] public void UppercaseSavedGuid_IsTheSameRunWhenAdvancingProgress()
        {
            var run = Guid.ParseExact("0123456789abcdef0123456789abcdef", "N");
            var data = Profile(40, 50).ToData();
            data.Rewards = new[] { new RunRewardReceiptData { RunId = run.ToString("N").ToUpperInvariant(), Waves = 5 } };
            var profile = WorkshopProfile.FromData(data);
            Assert.That(profile.FindRunReward(run).Waves, Is.EqualTo(5));
            Assert.That(profile.TryApplyRunReward(run, 8, true, out var code), Is.True, code);
            Assert.That(profile.Gold, Is.EqualTo(94)); Assert.That(profile.TotalXp, Is.EqualTo(120));
            Assert.That(profile.ToData().Rewards.Length, Is.EqualTo(1));
            Assert.That(profile.ToData().Rewards[0].RunId, Is.EqualTo(run.ToString("N")));
        }

        [Test] public void SaturatedBalances_StillRecordProgressWithoutWrappingOrPayingAgain()
        {
            var profile = Profile(int.MaxValue - 3, int.MaxValue - 4); var run = Guid.NewGuid();
            Assert.That(profile.TryApplyRunReward(run, 5, false, out var code), Is.True, code);
            Assert.That(profile.Gold, Is.EqualTo(int.MaxValue)); Assert.That(profile.TotalXp, Is.EqualTo(int.MaxValue));
            Assert.That(profile.FindRunReward(run).Waves, Is.EqualTo(5));
            Assert.That(profile.TryApplyRunReward(run, 8, true, out code), Is.True, code);
            string before = JsonUtility.ToJson(profile.ToData());
            Assert.That(profile.TryApplyRunReward(run, 8, true, out code), Is.True, code);
            Assert.That(profile.Gold, Is.EqualTo(int.MaxValue)); Assert.That(profile.TotalXp, Is.EqualTo(int.MaxValue));
            Assert.That(profile.FindRunReward(run).VictoryBonus, Is.True);
            Assert.That(JsonUtility.ToJson(profile.ToData()), Is.EqualTo(before));
        }

        [Test] public void FutureRewardMetadata_RemainsUnsupported()
        {
            var profile = Profile(); var run = Guid.NewGuid();
            Assert.That(profile.TryApplyRunReward(run, 5, false, out var code), Is.True, code);
            var data = profile.ToData(); data.Rewards[0].Version = 999;
            Assert.Throws<NotSupportedException>(() => WorkshopProfile.FromData(data));
            Assert.That(profile.Gold, Is.EqualTo(40)); Assert.That(profile.FindRunReward(run).Waves, Is.EqualTo(5));
        }
    }
}
