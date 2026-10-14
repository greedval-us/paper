using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class FootprintContourTests
    {
        public static Point2[] Box(float x = 1, float y = 1) => new[] { new Point2(-x,-y), new Point2(x,-y), new Point2(x,y), new Point2(-x,y) };
        static double Area(System.Collections.Generic.IReadOnlyList<Point2> points)
        { double sum=0; for(int i=0;i<points.Count;i++){var a=points[i];var b=points[(i+1)%points.Count];sum+=(double)a.X*b.Y-(double)a.Y*b.X;} return Math.Abs(sum)*.5; }
        [Test] public void ScaleAndWindingDoNotChangeWorkingArea()
        {
            foreach(float scale in new[]{.0001f,1f,10000f})
            {
                var points=Box(.6f*scale,1.2f*scale); var result=FootprintContour.Analyze(points);
                Assert.That(result.Valid,Is.True,result.Issue.ToString());
                Assert.That(Area(result.Normalized),Is.EqualTo(Math.PI).Within(.00001));
                Assert.That(result.AspectRatio,Is.EqualTo(2).Within(.001));
                Assert.That(Area(FootprintContour.Analyze(points.Reverse().ToArray()).Normalized),Is.EqualTo(Math.PI).Within(.00001));
                points[0]=default; Assert.That(result.Authored[0].X,Is.EqualTo(-.6f*scale));
            }
        }
        [Test] public void AcceptsConcavityWithoutReplacingItByHull()
        {
            var points=new[]{new Point2(-1,-1),new Point2(1,-1),new Point2(1,1),new Point2(.4f,1),new Point2(.4f,.3f),new Point2(-.4f,.3f),new Point2(-.4f,1),new Point2(-1,1)};
            var result=FootprintContour.Analyze(points);
            Assert.That(result.Valid,Is.True,result.Issue.ToString()); Assert.That(result.Normalized.Count,Is.EqualTo(8));
        }
        [Test] public void ReportsCrossingEdgesAndMissingAim()
        {
            var cross=FootprintContour.Analyze(new[]{new Point2(-1,-1),new Point2(1,1),new Point2(-1,1),new Point2(1,-1)});
            Assert.That(cross.Issue,Is.EqualTo(FootprintContourIssue.Intersection)); Assert.That(cross.FirstEdge,Is.GreaterThanOrEqualTo(0)); Assert.That(cross.SecondEdge,Is.GreaterThanOrEqualTo(0));
            Assert.That(FootprintContour.Analyze(Box().Select(p=>new Point2(p.X+4,p.Y)).ToArray()).Issue,Is.EqualTo(FootprintContourIssue.AimOutside));
        }
        [Test] public void NearEndpointsCloseButLargeGapDoesNot()
        {
            Assert.That(FootprintContour.Analyze(Box(),false).Issue,Is.EqualTo(FootprintContourIssue.Open));
            var result=FootprintContour.Analyze(Box().Concat(new[]{new Point2(-1,-.98f)}).ToArray(),false);
            Assert.That(result.Valid,Is.True,result.Issue.ToString()); Assert.That(result.ClosedGap,Is.True);
        }
        [Test] public void NarrowBridgeIsFlaggedEvenWithSquareBounds()
        {
            var p=new[]{new Point2(-1,-1),new Point2(-.1f,-1),new Point2(-.1f,-.05f),new Point2(.1f,-.05f),new Point2(.1f,-1),new Point2(1,-1),new Point2(1,1),new Point2(.1f,1),new Point2(.1f,.05f),new Point2(-.1f,.05f),new Point2(-.1f,1),new Point2(-1,1)};
            var result=FootprintContour.Analyze(p);
            Assert.That(result.Issue,Is.EqualTo(FootprintContourIssue.Narrow)); Assert.That(result.FirstEdge,Is.GreaterThanOrEqualTo(0));
        }
        [Test] public void ElongationCannotBeBypassedByRotation()
        {
            var p=Box(.1f,1).Select(v=>new Point2((v.X-v.Y)*.7071f,(v.X+v.Y)*.7071f)).ToArray();
            Assert.That(FootprintContour.Analyze(p).Issue,Is.EqualTo(FootprintContourIssue.Elongated));
        }
        [Test] public void NonfiniteAndExcessiveInputAreRejected()
        {
            Assert.That(FootprintContour.Analyze(new[]{default(Point2),new Point2(float.NaN,0),new Point2(1,1)}).Issue,Is.EqualTo(FootprintContourIssue.Nonfinite));
            Assert.That(FootprintContour.Analyze(Enumerable.Repeat(default(Point2),1000).ToArray()).Issue,Is.EqualTo(FootprintContourIssue.PointCount));
        }
    }
}
