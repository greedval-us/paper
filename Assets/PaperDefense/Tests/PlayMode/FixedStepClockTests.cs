using NUnit.Framework;
using PaperDefense.Application;

namespace PaperDefense.Tests
{
    public sealed class FixedStepClockTests
    {
        [Test]
        public void SlowFrameDiscardsExcessAndNeverLeavesUnboundedDebt()
        {
            var clock = new FixedStepClock();
            Assert.That(clock.AdvanceFrame(2, false, 1), Is.EqualTo(6));
            Assert.That(clock.DiscardedSeconds, Is.GreaterThan(1.7));
            Assert.That(clock.AccumulatedSeconds, Is.LessThan(FixedStepClock.Step));
            Assert.That(clock.AdvanceFrame(0, false, 1), Is.Zero);
        }

        [Test]
        public void DoubleSpeedUsesTwiceAsManyUnchangedSteps()
        {
            var normal = new FixedStepClock();
            var fast = new FixedStepClock();
            int normalTicks = 0, fastTicks = 0;
            for (int frame = 0; frame < 60; frame++)
            {
                normalTicks += normal.AdvanceFrame(1f / 60, false, 1);
                fastTicks += fast.AdvanceFrame(1f / 60, false, 2);
            }
            Assert.That(normalTicks, Is.EqualTo(30));
            Assert.That(fastTicks, Is.EqualTo(60));
            Assert.That(fast.DiscardedSeconds, Is.Zero);
        }

        [Test]
        public void PauseDropsFractionalDebtAndAccumulatesNothing()
        {
            var clock = new FixedStepClock();
            Assert.That(clock.AdvanceFrame(FixedStepClock.Step * .75f, false, 1), Is.Zero);
            Assert.That(clock.AdvanceFrame(20, true, 2), Is.Zero);
            Assert.That(clock.AdvanceFrame(FixedStepClock.Step * .5f, false, 1), Is.Zero);
            Assert.That(clock.AdvanceFrame(FixedStepClock.Step * .5f, false, 1), Is.EqualTo(1));
        }
    }
}
