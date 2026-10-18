using System;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class RunRewardTotalsTests
    {
        [TestCase(0, false, 0, 0)] [TestCase(5, false, 40, 50)]
        [TestCase(8, false, 64, 80)] [TestCase(8, true, 94, 120)]
        public void ResultTotalsUseOnlyCompletedWavesAndTheActualVictoryBonus(int waves, bool victory, int gold, int xp)
        {
            var total = RunRewardTotals.Calculate(waves, victory);
            Assert.That(total.Gold, Is.EqualTo(gold)); Assert.That(total.TotalXp, Is.EqualTo(xp));
            Assert.That(total.WaveGold + total.VictoryGold, Is.EqualTo(gold));
            Assert.That(total.WaveXp + total.VictoryXp, Is.EqualTo(xp));
        }
        [TestCase(-1, false)] [TestCase(9, false)] [TestCase(5, true)]
        public void InvalidPartialVictoriesCannotBeDisplayedAsEarnedRewards(int waves, bool victory)
            => Assert.Throws<ArgumentException>(() => RunRewardTotals.Calculate(waves, victory));

        [Test] public void TheReceiptAndProfilePublishTheSameCumulativeRewardWithoutAnotherPayment()
        {
            var profile = new WorkshopProfile(); var run = Guid.NewGuid();
            Assert.That(profile.TryApplyRunReward(run, 5, false, out var code), Is.True, code);
            var partial = profile.FindRunReward(run);
            Assert.That(partial.Gold, Is.EqualTo(40)); Assert.That(partial.TotalXp, Is.EqualTo(50));
            Assert.That(profile.TryApplyRunReward(run, 8, true, out code), Is.True, code);
            var receipt = profile.FindRunReward(run);
            Assert.That(receipt.Gold, Is.EqualTo(94)); Assert.That(receipt.TotalXp, Is.EqualTo(120));
            Assert.That(partial.Gold, Is.EqualTo(40));
            int gold = profile.Gold, xp = profile.TotalXp;
            Assert.That(profile.TryApplyRunReward(run, 8, true, out code), Is.True, code);
            Assert.That(profile.Gold, Is.EqualTo(gold)); Assert.That(profile.TotalXp, Is.EqualTo(xp));
        }
    }
}
