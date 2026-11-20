using System;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class MetaProgressionTests
    {
        [TestCase(0, 1)] [TestCase(99, 1)]
        [TestCase(100, 2)] [TestCase(229, 2)]
        [TestCase(230, 3)] [TestCase(389, 3)]
        [TestCase(390, 4)] [TestCase(539, 4)]
        [TestCase(540, 5)] [TestCase(719, 5)]
        [TestCase(720, 6)] [TestCase(889, 6)]
        [TestCase(890, 7)] [TestCase(1049, 7)]
        [TestCase(1050, 8)] [TestCase(int.MaxValue, 8)]
        public void ExactXpBoundariesChooseTheHighestReachedLevel(int xp, int expectedLevel)
            => Assert.That(MetaProgression.LevelForXp(xp), Is.EqualTo(expectedLevel));

        [TestCase(-1)] [TestCase(int.MinValue)]
        public void NegativeXpIsRejected(int xp)
            => Assert.Throws<ArgumentOutOfRangeException>(() => MetaProgression.LevelForXp(xp));

        [TestCase(99, 1, 0, 100, .99f)]
        [TestCase(100, 2, 100, 230, 0f)]
        [TestCase(510, 4, 390, 540, 120f / 150f)]
        [TestCase(1049, 7, 890, 1050, 159f / 160f)]
        public void LevelProgressUsesTheCurrentAuthoritativeInterval(int xp, int level, int start, int next, float fraction)
        {
            var progress = MetaProgression.GetLevelProgress(xp);
            Assert.That(progress.TotalXp, Is.EqualTo(xp));
            Assert.That(progress.Level, Is.EqualTo(level));
            Assert.That(progress.LevelStartXp, Is.EqualTo(start));
            Assert.That(progress.NextLevelXp, Is.EqualTo(next));
            Assert.That(progress.Normalized, Is.EqualTo(fraction).Within(.00001f));
            Assert.That(progress.IsMaximum, Is.False);
        }

        [TestCase(1050)] [TestCase(int.MaxValue)]
        public void MaximumLevelKeepsAllXpWithoutInventingAnotherThreshold(int xp)
        {
            var progress = MetaProgression.GetLevelProgress(xp);
            Assert.That(progress.Level, Is.EqualTo(8));
            Assert.That(progress.TotalXp, Is.EqualTo(xp));
            Assert.That(progress.LevelStartXp, Is.EqualTo(1050));
            Assert.That(progress.NextLevelXp, Is.Null);
            Assert.That(progress.IsMaximum, Is.True);
            Assert.That(progress.Normalized, Is.EqualTo(1));
        }

        [Test] public void LevelProgressRejectsNegativeXp()
            => Assert.Throws<ArgumentOutOfRangeException>(() => MetaProgression.GetLevelProgress(-1));
    }
}
