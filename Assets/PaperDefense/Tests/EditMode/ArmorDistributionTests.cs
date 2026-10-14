using System;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class ArmorDistributionTests
    {
        [Test] public void DistributionOwnsItsBudgetAndRejectsFreeArmor()
        {
            var points = new[]{6,3,0,3}; var armor = new ArmorDistribution(points);
            points[0]=12;
            Assert.That(armor.Points[0],Is.EqualTo(6));
            Assert.That(armor.Remaining,Is.Zero);
            Assert.Throws<ArgumentException>(()=>new ArmorDistribution(new[]{12,1,0,0}));
            Assert.Throws<ArgumentException>(()=>new ArmorDistribution(new[]{-1,3,3,3}));
            Assert.Throws<ArgumentException>(()=>new ArmorDistribution(new[]{3,3,3}));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new ArmorDistribution(new[]{3,3,3,3},4));
        }
        [Test] public void DirectionsFollowFixedBodyFacingAndDiagonalTiesAreClockwise()
        {
            var armor = new ArmorDistribution(new[]{12,0,0,0},1);
            Assert.That(armor.Sector(new Point2(1,0)),Is.EqualTo(0));
            Assert.That(armor.Sector(new Point2(0,1)),Is.EqualTo(3));
            Assert.That(new ArmorDistribution(new[]{3,3,3,3}).Sector(new Point2(1,1)),Is.EqualTo(1));
            Assert.That(armor.Multiplier(default),Is.Zero);
        }
    }
}
