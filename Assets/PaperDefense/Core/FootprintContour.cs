using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public enum FootprintContourIssue { None, PointCount, Nonfinite, Open, Intersection, Area, AimOutside, Elongated, TooFar, Narrow }

    public sealed class FootprintContourAssessment
    {
        public FootprintContourIssue Issue { get; }
        public bool Valid => Issue == FootprintContourIssue.None;
        public IReadOnlyList<Point2> Authored { get; }
        public IReadOnlyList<Point2> Normalized { get; }
        public float AspectRatio { get; }
        public int FirstEdge { get; }
        public int SecondEdge { get; }
        public bool ClosedGap { get; }
        internal FootprintContourAssessment(FootprintContourIssue issue, Point2[] authored, Point2[] normalized,
            float aspect = 1, int firstEdge = -1, int secondEdge = -1, bool closedGap = false)
        { Issue = issue; Authored = Array.AsReadOnly(authored); Normalized = Array.AsReadOnly(normalized);
            AspectRatio = aspect; FirstEdge = firstEdge; SecondEdge = secondEdge; ClosedGap = closedGap; }
    }

    public static class FootprintContour
    {
        public const int MaximumPoints = 192;
        public const float MinimumWidth = .35f;
        public const float MaximumReach = 3;
        internal const float AspectTolerance = .00001f;
        public static FootprintContourAssessment Analyze(IReadOnlyList<Point2> points, bool closed = true,
            float maximumAspect = MortarFootprint.MaximumAspectRatio)
        {
            if (!Finite(maximumAspect) || maximumAspect < 1 || maximumAspect > MortarFootprint.StructuralMaximumAspectRatio)
                throw new ArgumentOutOfRangeException(nameof(maximumAspect));
            if (points == null || points.Count < 3 || points.Count > MaximumPoints)
                return Result(FootprintContourIssue.PointCount, Array.Empty<Point2>());
            var list = new List<Point2>();
            double extent = 0;
            foreach (var p in points)
            {
                if (!Finite(p.X) || !Finite(p.Y)) return Result(FootprintContourIssue.Nonfinite, Array.Empty<Point2>());
                extent = Math.Max(extent, Math.Max(Math.Abs((double)p.X), Math.Abs((double)p.Y)));
                if (list.Count == 0 || !Same(list[list.Count - 1], p)) list.Add(p);
            }
            if (list.Count > 1 && Same(list[0], list[list.Count - 1])) { list.RemoveAt(list.Count - 1); closed = true; }
            var authored = list.ToArray();
            if (list.Count < 3 || extent == 0) return Result(FootprintContourIssue.Area, authored);
            // Dimensionless intermediates make validation independent of pen scale and avoid overflow.
            var unit = new Point2[list.Count];
            for (int i = 0; i < unit.Length; i++) unit[i] = new Point2((float)(list[i].X / extent), (float)(list[i].Y / extent));
            bool gap = !closed;
            if (gap)
            {
                double minX = 1, maxX = -1, minY = 1, maxY = -1;
                foreach (var p in unit) { minX=Math.Min(minX,p.X);maxX=Math.Max(maxX,p.X);minY=Math.Min(minY,p.Y);maxY=Math.Max(maxY,p.Y); }
                double diagonal = Math.Sqrt((maxX-minX)*(maxX-minX)+(maxY-minY)*(maxY-minY));
                if (Distance(unit[0], unit[unit.Length-1]) > diagonal * .12)
                    return Result(FootprintContourIssue.Open, authored, first: unit.Length-1);
            }
            for (int i=0;i<unit.Length;i++)
            {
                var a=unit[i];var b=unit[(i+1)%unit.Length];var c=unit[(i+2)%unit.Length];
                if (Math.Abs(Cross(a,b,c)) < 1e-10 && ((double)b.X-a.X)*(c.X-b.X)+((double)b.Y-a.Y)*(c.Y-b.Y) < 0)
                    return Result(FootprintContourIssue.Intersection,authored,first:i,second:(i+1)%unit.Length);
                for(int j=i+2;j<unit.Length;j++)
                {
                    if(i==0&&j==unit.Length-1)continue;
                    if(Intersects(a,b,unit[j],unit[(j+1)%unit.Length]))
                        return Result(FootprintContourIssue.Intersection,authored,first:i,second:j);
                }
            }
            double twiceArea=0;
            for(int i=0;i<unit.Length;i++)twiceArea+=(double)unit[i].X*unit[(i+1)%unit.Length].Y-(double)unit[i].Y*unit[(i+1)%unit.Length].X;
            if(Math.Abs(twiceArea)<1e-10)return Result(FootprintContourIssue.Area,authored);
            if(!Contains(unit,default,false))return Result(FootprintContourIssue.AimOutside,authored);
            double scale=Math.Sqrt(2*Math.PI/Math.Abs(twiceArea));
            var normalized=new Point2[unit.Length];
            for(int i=0;i<unit.Length;i++)normalized[i]=new Point2((float)(unit[i].X*scale),(float)(unit[i].Y*scale));
            float aspect=Aspect(normalized);
            if(aspect>maximumAspect+AspectTolerance)return Result(FootprintContourIssue.Elongated,authored,normalized,aspect);
            for(int i=0;i<normalized.Length;i++)
                if(Distance(default,normalized[i])>MaximumReach+1e-5)
                    return Result(FootprintContourIssue.TooFar,authored,normalized,aspect,i);
            if(NarrowEdges(normalized,out int first,out int second))return Result(FootprintContourIssue.Narrow,authored,normalized,aspect,first,second);
            return new FootprintContourAssessment(FootprintContourIssue.None,authored,normalized,aspect,closedGap:gap);
        }

        static FootprintContourAssessment Result(FootprintContourIssue issue,Point2[] authored,Point2[] normalized=null,float aspect=1,int first=-1,int second=-1)
            => new FootprintContourAssessment(issue,authored,normalized??Array.Empty<Point2>(),aspect,first,second);
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        static bool Same(Point2 a,Point2 b)=>a.X==b.X&&a.Y==b.Y;
        static double Cross(Point2 a,Point2 b,Point2 c)=>((double)b.X-a.X)*((double)c.Y-a.Y)-((double)b.Y-a.Y)*((double)c.X-a.X);
        static double Distance(Point2 a,Point2 b)=>Math.Sqrt(((double)a.X-b.X)*(a.X-b.X)+((double)a.Y-b.Y)*(a.Y-b.Y));
        static double Projection(Point2 p,Point2 a,Point2 b)
        {
            double x=(double)b.X-a.X,y=(double)b.Y-a.Y,den=x*x+y*y;
            return den==0?0:Math.Max(0,Math.Min(1,(((double)p.X-a.X)*x+((double)p.Y-a.Y)*y)/den));
        }
        static Point2 Lerp(Point2 a,Point2 b,double t)=>new Point2((float)(a.X+((double)b.X-a.X)*t),(float)(a.Y+((double)b.Y-a.Y)*t));
        static bool OnSegment(Point2 p,Point2 a,Point2 b,double tolerance=1e-7)=>Distance(p,Lerp(a,b,Projection(p,a,b)))<=tolerance;
        static bool Intersects(Point2 a,Point2 b,Point2 c,Point2 d)
        {
            double abC=Cross(a,b,c),abD=Cross(a,b,d),cdA=Cross(c,d,a),cdB=Cross(c,d,b);
            if(((abC>0&&abD<0)||(abC<0&&abD>0))&&((cdA>0&&cdB<0)||(cdA<0&&cdB>0)))return true;
            return OnSegment(a,c,d)||OnSegment(b,c,d)||OnSegment(c,a,b)||OnSegment(d,a,b);
        }
        internal static bool Contains(IReadOnlyList<Point2> points,Point2 p,bool boundary=true)
        {
            bool inside=false;
            for(int i=0,j=points.Count-1;i<points.Count;j=i++)
            {
                var a=points[j];var b=points[i];
                if(OnSegment(p,a,b,1e-6))return boundary;
                if((a.Y>p.Y)!=(b.Y>p.Y)&&p.X<((double)b.X-a.X)*(p.Y-a.Y)/((double)b.Y-a.Y)+a.X)inside=!inside;
            }
            return inside;
        }
        static float Aspect(Point2[] points)
        {
            var sorted=new List<Point2>(points);sorted.Sort((a,b)=>a.X==b.X?a.Y.CompareTo(b.Y):a.X.CompareTo(b.X));
            var hull=new List<Point2>();
            foreach(var p in sorted){while(hull.Count>=2&&Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
            int lower=hull.Count;
            for(int i=sorted.Count-2;i>=0;i--){var p=sorted[i];while(hull.Count>lower&&Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
            hull.RemoveAt(hull.Count-1);
            double area=double.MaxValue,aspect=1;
            for(int i=0;i<hull.Count;i++)
            {
                var a=hull[i];var b=hull[(i+1)%hull.Count];double length=Distance(a,b),x=(b.X-a.X)/length,y=(b.Y-a.Y)/length;
                double minU=double.MaxValue,maxU=double.MinValue,minV=double.MaxValue,maxV=double.MinValue;
                foreach(var p in hull){double u=p.X*x+p.Y*y,v=-p.X*y+p.Y*x;minU=Math.Min(minU,u);maxU=Math.Max(maxU,u);minV=Math.Min(minV,v);maxV=Math.Max(maxV,v);}
                double width=maxU-minU,height=maxV-minV,candidate=width*height,ratio=Math.Max(width,height)/Math.Min(width,height);
                if(candidate<area-1e-6||(Math.Abs(candidate-area)<=1e-6&&ratio<aspect)){area=candidate;aspect=ratio;}
            }
            return (float)aspect;
        }
        static bool NarrowEdges(Point2[] points,out int first,out int second)
        {
            first=second=-1;
            var lengths=new double[points.Length];var starts=new double[points.Length];double perimeter=0;
            for(int i=0;i<points.Length;i++){starts[i]=perimeter;lengths[i]=Distance(points[i],points[(i+1)%points.Length]);perimeter+=lengths[i];}
            for(int i=0;i<points.Length;i++)
            {
                int steps=Math.Max(1,(int)Math.Ceiling(lengths[i]/(MinimumWidth*.25)));
                for(int k=0;k<=steps;k++)
                {
                    double t=(double)k/steps,along=starts[i]+lengths[i]*t;var p=Lerp(points[i],points[(i+1)%points.Length],t);
                    for(int j=0;j<points.Length;j++)
                    {
                        if(i==j)continue;
                        double u=Projection(p,points[j],points[(j+1)%points.Length]),arc=Math.Abs(along-(starts[j]+lengths[j]*u));
                        if(Math.Min(arc,perimeter-arc)<MinimumWidth*2)continue;
                        var q=Lerp(points[j],points[(j+1)%points.Length],u);
                        if(Distance(p,q)<MinimumWidth-1e-5&&Contains(points,Lerp(p,q,.5),false)){first=i;second=j;return true;}
                    }
                }
            }
            return false;
        }
    }
}
