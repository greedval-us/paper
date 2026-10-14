using System;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class WellPlacementDefinitionTests
    {
        [TestCase(128,96,64,48)]
        [TestCase(129,97,64,48)]
        [TestCase(int.MaxValue,int.MaxValue,1073741823,1073741823)]
        public void EquivalentRatiosUseWideIntermediatesAndKeepIntegerFloor(int width,int height,int x,int y)
        {
            var small=new WellPlacementDefinition{Color=InkColor.Red,XNumerator=1,XDenominator=2,YNumerator=1,YDenominator=2};
            var large=new WellPlacementDefinition{Color=InkColor.Red,XNumerator=1000000000,XDenominator=2000000000,YNumerator=1000000000,YDenominator=2000000000};
            Assert.That(large.CreateSnapshot().Resolve(width,height),Is.EqualTo(new Cell(x,y)));
            Assert.That(large.Resolve(width,height),Is.EqualTo(small.Resolve(width,height)));
        }

        [TestCase(0,96)]
        [TestCase(128,0)]
        [TestCase(-1,96)]
        public void InvalidDimensionsCannotResolveAWell(int width,int height)
        {
            var placement=new WellPlacementDefinition{Color=InkColor.Blue,XNumerator=1,XDenominator=2,YNumerator=1,YDenominator=2};
            Assert.That(()=>placement.Resolve(width,height),Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
