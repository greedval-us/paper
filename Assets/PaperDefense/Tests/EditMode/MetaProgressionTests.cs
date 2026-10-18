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
        [TestCase(390, 4)] [TestCase(579, 4)]
        [TestCase(580, 5)] [TestCase(799, 5)]
        [TestCase(800, 6)] [TestCase(1049, 6)]
        [TestCase(1050, 7)] [TestCase(1329, 7)]
        [TestCase(1330, 8)] [TestCase(int.MaxValue, 8)]
        public void ExactXpBoundariesChooseTheHighestReachedLevel(int xp, int expectedLevel)
            => Assert.That(MetaProgression.LevelForXp(xp), Is.EqualTo(expectedLevel));

        [TestCase(-1)] [TestCase(int.MinValue)]
        public void NegativeXpIsRejected(int xp)
            => Assert.Throws<ArgumentOutOfRangeException>(() => MetaProgression.LevelForXp(xp));
    }
}
