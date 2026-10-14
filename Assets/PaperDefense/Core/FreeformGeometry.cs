using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PaperDefense.Core
{
    /// <summary>Exact vector envelopes of connected strokes. Retracing and collinear subdivision never add mass.
    /// Small gaps are welded without changing the points; remote components do not inflate the main shape.</summary>
    internal sealed class FreeformGeometry
    {
        const double WeldDistance = .0625, WeldSquared = WeldDistance * WeldDistance;
        static readonly ConditionalWeakTable<DrawingSnapshot, FreeformGeometry> cache = new ConditionalWeakTable<DrawingSnapshot, FreeformGeometry>();
        static readonly ConditionalWeakTable<DrawingSnapshot, FreeformGeometry> legacyCache = new ConditionalWeakTable<DrawingSnapshot, FreeformGeometry>();
        readonly Dictionary<string, Shape> parts = new Dictionary<string, Shape>(StringComparer.Ordinal);
        internal static FreeformGeometry For(DrawingSnapshot drawing, int version = FreeformTowerDesign.RuleVersion) => version == 1
            ? legacyCache.GetValue(drawing, value => new FreeformGeometry(value, false))
            : cache.GetValue(drawing, value => new FreeformGeometry(value, true));
        internal Shape Part(string binding) => parts.TryGetValue(binding, out var shape) ? shape : default;
        FreeformGeometry(DrawingSnapshot drawing, bool forwardOnly)
        {
            foreach (var part in drawing.Parts)
                if (part.BindingId == DrawingBindings.Base || DrawingBindings.TryMount(part.BindingId, out _))
                    parts[part.BindingId] = Measure(part, forwardOnly && part.BindingId != DrawingBindings.Base);
        }
        internal readonly struct Shape
        {
            internal readonly float Area, Width, Length;
            internal readonly Point2 MuzzleOffset;
            internal readonly IReadOnlyList<DrawingPoint> Outline;
            internal Shape(float area, float width, float length, Point2 muzzleOffset, IReadOnlyList<DrawingPoint> outline)
            { Area = area; Width = width; Length = length; MuzzleOffset = muzzleOffset; Outline = outline; }
        }
        static Shape Measure(DrawingPart part, bool forwardOnly)
        {
            var strokes = forwardOnly ? ForwardFragments(part.Strokes) : part.Strokes;
            int count = strokes.Count;
            var roots = new int[count];
            for (int i = 0; i < count; i++) roots[i] = i;
            for (int i = 0; i < count; i++) for (int j = 0; j < i; j++)
                if (Root(roots,i) != Root(roots,j) && Touches(strokes[i], strokes[j]))
                    roots[Root(roots,i)] = Root(roots,j);
            var groups = new Dictionary<int, HashSet<DrawingPoint>>();
            for (int i = 0; i < count; i++)
            {
                int root = Root(roots,i);
                if (!groups.TryGetValue(root,out var points)) groups[root] = points = new HashSet<DrawingPoint>();
                foreach (var point in strokes[i].Points) points.Add(point);
            }
            Shape best = default;
            bool body = part.BindingId == DrawingBindings.Base;
            foreach (var points in groups.Values)
            {
                var candidate = Envelope(points);
                float score = body ? candidate.Area : candidate.Length, previous = body ? best.Area : best.Length;
                if (score > previous || (score == previous && (candidate.Area > best.Area ||
                    (candidate.Area == best.Area && (candidate.Width > best.Width ||
                    (candidate.Width == best.Width && (candidate.MuzzleOffset.Y > best.MuzzleOffset.Y ||
                    (candidate.MuzzleOffset.Y == best.MuzzleOffset.Y && candidate.MuzzleOffset.X < best.MuzzleOffset.X)))))))) best = candidate;
            }
            return best;
        }
        // Clip before connectivity: a rear bridge, even within one stroke, cannot join working regions.
        static IReadOnlyList<DrawingStroke> ForwardFragments(IReadOnlyList<DrawingStroke> strokes)
        {
            var result = new List<DrawingStroke>();
            foreach (var stroke in strokes)
            {
                var fragment = new List<DrawingPoint>();
                for (int i = 1; i < stroke.Points.Count; i++)
                {
                    var a = stroke.Points[i - 1]; var b = stroke.Points[i];
                    if (a.Y < 0 && b.Y < 0) continue;
                    var from = a.Y < 0 ? Intersection(a, b) : a;
                    var to = b.Y < 0 ? Intersection(a, b) : b;
                    if (fragment.Count == 0) fragment.Add(from);
                    if (!fragment[fragment.Count - 1].Equals(to)) fragment.Add(to);
                    if (b.Y < 0)
                    {
                        if (fragment.Count >= 2) result.Add(new DrawingStroke(fragment));
                        fragment.Clear();
                    }
                }
                if (fragment.Count >= 2) result.Add(new DrawingStroke(fragment));
            }
            return result;
        }
        static DrawingPoint Intersection(DrawingPoint a, DrawingPoint b)
            => new DrawingPoint((float)(a.X + (double)(b.X - a.X) * -a.Y / (b.Y - a.Y)), 0);
        static int Root(int[] roots,int index)
        { while (roots[index] != index) { roots[index] = roots[roots[index]]; index = roots[index]; } return index; }
        static bool Touches(DrawingStroke first, DrawingStroke second)
        {
            for (int i=1;i<first.Points.Count;i++) for (int j=1;j<second.Points.Count;j++)
            {
                var a=first.Points[i-1];var b=first.Points[i];var c=second.Points[j-1];var d=second.Points[j];
                if (Math.Min(a.X,b.X)>Math.Max(c.X,d.X)+WeldDistance || Math.Min(c.X,d.X)>Math.Max(a.X,b.X)+WeldDistance ||
                    Math.Min(a.Y,b.Y)>Math.Max(c.Y,d.Y)+WeldDistance || Math.Min(c.Y,d.Y)>Math.Max(a.Y,b.Y)+WeldDistance) continue;
                // A proper intersection, or endpoint proximity (also covers collinear/degenerate segments).
                if (Cross(a,b,c)*Cross(a,b,d)<0 && Cross(c,d,a)*Cross(c,d,b)<0) return true;
                if (Distance(a,c,d)<=WeldSquared || Distance(b,c,d)<=WeldSquared ||
                    Distance(c,a,b)<=WeldSquared || Distance(d,a,b)<=WeldSquared) return true;
            }
            return false;
        }
        static double Distance(DrawingPoint point, DrawingPoint a, DrawingPoint b)
        {
            double dx=b.X-a.X,dy=b.Y-a.Y, length=dx*dx+dy*dy;
            double t=length==0?0:Math.Max(0,Math.Min(1,((point.X-a.X)*dx+(point.Y-a.Y)*dy)/length));
            double x=point.X-a.X-t*dx,y=point.Y-a.Y-t*dy;return x*x+y*y;
        }
        static Shape Envelope(HashSet<DrawingPoint> unique)
        {
            if (unique.Count<2) return default;
            var points=new List<DrawingPoint>(unique);
            points.Sort((a,b)=>a.X==b.X?a.Y.CompareTo(b.Y):a.X.CompareTo(b.X));
            float minX=1,maxX=-1,minY=1,maxY=-1;
            foreach(var point in points){minX=Math.Min(minX,point.X);maxX=Math.Max(maxX,point.X);minY=Math.Min(minY,point.Y);maxY=Math.Max(maxY,point.Y);}
            var hull=new List<DrawingPoint>();
            foreach(var point in points){while(hull.Count>=2&&Cross(hull[hull.Count-2],hull[hull.Count-1],point)<=0)hull.RemoveAt(hull.Count-1);hull.Add(point);}
            int lower=hull.Count;
            for(int i=points.Count-2;i>=0;i--){var point=points[i];while(hull.Count>lower&&Cross(hull[hull.Count-2],hull[hull.Count-1],point)<=0)hull.RemoveAt(hull.Count-1);hull.Add(point);}
            double twiceArea=0;
            for(int i=1;i<hull.Count;i++)twiceArea+=(double)hull[i-1].X*hull[i].Y-(double)hull[i].X*hull[i-1].Y;
            float tipLeft=1,tipRight=-1;
            foreach(var point in hull)if(point.Y==maxY){tipLeft=Math.Min(tipLeft,point.X);tipRight=Math.Max(tipRight,point.X);}
            return new Shape((float)(Math.Abs(twiceArea)*.5),maxX-minX,maxY-minY,new Point2((tipLeft+tipRight)*.5f,maxY),hull.AsReadOnly());
        }
        static double Cross(DrawingPoint a,DrawingPoint b,DrawingPoint c) => (double)(b.X-a.X)*(c.Y-a.Y)-(double)(b.Y-a.Y)*(c.X-a.X);
    }
}
