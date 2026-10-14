using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class SessionPreparationTests
    {
        [Test] public void PreparationOwnsTargetsAndRetainsExactStationaryPositionAndSeed()
        {
            var balance = GameBalance.CreateDefault(); balance.EnableInkWells = false;
            var targets = new List<PreparedEnemy> { new PreparedEnemy(new Point2(4.25f, 6.75f), 90) };
            var first = GameSession.CreatePrepared(balance, 7331, targets);
            var second = GameSession.CreatePrepared(balance, 7331, targets);
            targets.Clear(); balance.StartingInk = 9999;
            for (int i = 0; i < 30; i++) { first.Tick(1f / 30); second.Tick(1f / 30); }
            Assert.That(first.Enemies.Count, Is.EqualTo(1));
            Assert.That(first.Enemies[0].Position.X, Is.EqualTo(4.25f));
            Assert.That(first.Enemies[0].Position.Y, Is.EqualTo(6.75f));
            Assert.That(first.Ink, Is.EqualTo(second.Ink));
            Assert.That(first.TickIndex, Is.EqualTo(second.TickIndex));
            Assert.That(first.RemainingToSpawn, Is.Zero);
        }
        [TestCase(float.NaN, 4, 10)]
        [TestCase(4, float.PositiveInfinity, 10)]
        [TestCase(4, 4, -1)]
        [TestCase(-1, 4, 10)]
        public void PreparationRejectsInvalidTargets(float x, float y, float health)
            => Assert.Throws<ArgumentException>(() => GameSession.CreatePrepared(GameBalance.CreateDefault(), 1,
                new[] { new PreparedEnemy(new Point2(x, y), health) }));
        [Test] public void PreparationRejectsColorAndBlotsWhenTheirMechanicIsDisabled()
        {
            var balance = GameBalance.CreateDefault(); balance.Mechanics.ColorCartridges = false;
            Assert.Throws<ArgumentException>(() => GameSession.CreatePrepared(balance, 1, null, redInk: 1));
            balance.Mechanics.CorpseBlots = false;
            Assert.Throws<ArgumentException>(() => GameSession.CreatePrepared(balance, 1, null,
                new[] { new PreparedBlot(new Cell(4, 4), 10) }));
        }
        [Test] public void ColorStockAndBlotsBelongOnlyToThePreparedSession()
        {
            var balance = GameBalance.CreateDefault();
            balance.EnableEnemyMechanics = true;
            balance.Mechanics.CorpseBlots = true;
            var session = GameSession.CreatePrepared(balance, 1, null, new[] { new PreparedBlot(new Cell(4, 4), 10) }, 10, 20, 30);
            Assert.That(session.ColoredInk(InkColor.Red), Is.EqualTo(10));
            Assert.That(session.ColoredInk(InkColor.Blue), Is.EqualTo(20));
            Assert.That(session.ColoredInk(InkColor.Green), Is.EqualTo(30));
            Assert.That(session.Blots.Count, Is.EqualTo(1));
            Assert.That(new GameSession(balance, starterDefenses: false).ColoredInk(InkColor.Red), Is.Zero);
        }
    }
}
