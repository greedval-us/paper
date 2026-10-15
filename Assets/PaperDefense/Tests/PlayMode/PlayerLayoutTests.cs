using NUnit.Framework;
using PaperDefense.Presentation;

namespace PaperDefense.Tests
{
    public sealed class PlayerLayoutTests
    {
        [TestCase(1600,1000)] [TestCase(1280,720)] [TestCase(1024,768)]
        [TestCase(1920,1080)] [TestCase(2560,1080)]
        public void WorldViewportLeavesSpaceForHeaderWellsAndTools(int width,int height)
        {
            foreach(float scale in new[]{1f,1.25f,1.5f})
            {
                var layout=new PlayerLayout(width,height,scale);
                var field=layout.NormalizedField;
                Assert.That(field.xMin,Is.GreaterThanOrEqualTo(0));
                Assert.That(field.yMin,Is.GreaterThanOrEqualTo(0));
                Assert.That(field.xMax,Is.LessThanOrEqualTo(1));
                Assert.That(field.yMax,Is.LessThanOrEqualTo(1));
                Assert.That(layout.Field.xMin,Is.GreaterThanOrEqualTo(8));
                Assert.That(layout.Field.yMin,Is.GreaterThanOrEqualTo(60));
                Assert.That(layout.Field.height,Is.GreaterThan(0));
            }
        }

        [TestCase(1280,720)] [TestCase(1920,1080)]
        public void NormalBattleAllocatesAtLeastSixtyFivePercentToWorld(int width,int height)
        {
            var field = new PlayerLayout(width,height).NormalizedField;
            Assert.That(field.width * field.height, Is.InRange(.65f, .76f));
        }
    }
}
