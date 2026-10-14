using System;

namespace PaperDefense.Core
{
    /// <summary>Launch-time equal-area footprint, oriented in world coordinates.</summary>
    public readonly struct FootprintGeometry
    {
        public float ForwardRadius { get; }
        public float SideRadius { get; }
        public Point2 Forward { get; }
        public float BoundingRadius { get; }
        readonly MortarFootprint contour;
        readonly float radius;
        public bool IsContour => contour != null;
        public bool IsCircle => !IsContour && ForwardRadius == SideRadius;
        public int OutlineCount => IsContour ? contour.Normalized.Count : 64;

        private FootprintGeometry(float forwardRadius, float sideRadius, Point2 forward)
        { ForwardRadius = forwardRadius; SideRadius = sideRadius; Forward = forward; BoundingRadius=forwardRadius;contour=null;radius=0; }

        private FootprintGeometry(MortarFootprint footprint,float radius,Point2 forward)
        {
            contour=footprint;this.radius=radius;Forward=forward;
            double along=0,across=0,bounds=0;
            foreach(var p in footprint.Normalized){along=Math.Max(along,Math.Abs(p.Y));across=Math.Max(across,Math.Abs(p.X));bounds=Math.Max(bounds,Math.Sqrt((double)p.X*p.X+(double)p.Y*p.Y));}
            ForwardRadius=(float)(along*radius);SideRadius=(float)(across*radius);BoundingRadius=(float)(bounds*radius);
            if(!Finite(BoundingRadius))throw new ArgumentOutOfRangeException(nameof(radius));
        }

        /// <summary>Same launch-time perimeter for telegraphs, impacts and the workbench.</summary>
        public Point2 OutlineOffset(int index)
        {
            if(IsContour)
            {
                var p=contour.Normalized[index%OutlineCount];
                return new Point2(radius*(Forward.X*p.Y+Forward.Y*p.X),radius*(Forward.Y*p.Y-Forward.X*p.X));
            }
            double a=(index%OutlineCount)*Math.PI*2/OutlineCount;
            return new Point2((float)(Forward.X*Math.Cos(a)*ForwardRadius-Forward.Y*Math.Sin(a)*SideRadius),
                (float)(Forward.Y*Math.Cos(a)*ForwardRadius+Forward.X*Math.Sin(a)*SideRadius));
        }

        public static FootprintGeometry Resolve(MortarFootprint footprint, float radius, Point2 from, Point2 to)
        {
            if (footprint == null) throw new ArgumentNullException(nameof(footprint));
            if (!Finite(radius) || radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            if (!Finite(from.X) || !Finite(from.Y)) throw new ArgumentOutOfRangeException(nameof(from));
            if (!Finite(to.X) || !Finite(to.Y)) throw new ArgumentOutOfRangeException(nameof(to));
            // Double intermediates keep finite extreme coordinates from overflowing the direction.
            double dx = (double)to.X - from.X, dy = (double)to.Y - from.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            Point2 forward = length == 0 ? new Point2(1, 0) : new Point2((float)(dx / length), (float)(dy / length));
            if(footprint.IsContour)return new FootprintGeometry(footprint,radius,forward);
            double scale = Math.Sqrt(footprint.AspectRatio);
            float along = (float)(radius * scale), across = (float)(radius / scale);
            if (!Finite(along) || !Finite(across)) throw new ArgumentOutOfRangeException(nameof(radius));
            return new FootprintGeometry(along, across, forward);
        }

        public bool Contains(Point2 center, Point2 point)
        {
            if (!Finite(center.X) || !Finite(center.Y) || !Finite(point.X) || !Finite(point.Y)) return false;
            if(IsContour)
            {
                if(radius==0)return center.X==point.X&&center.Y==point.Y;
                double dx=(double)point.X-center.X,dy=(double)point.Y-center.Y;
                return FootprintContour.Contains(contour.Normalized,new Point2((float)((dx*Forward.Y-dy*Forward.X)/radius),(float)((dx*Forward.X+dy*Forward.Y)/radius)));
            }
            // Preserve the old circle's exact squared-distance comparison, including radius zero.
            if (ForwardRadius == SideRadius)
            {
                float dx = point.X - center.X, dy = point.Y - center.Y;
                return dx * dx + dy * dy <= ForwardRadius * ForwardRadius;
            }
            double x = (double)point.X - center.X, y = (double)point.Y - center.Y;
            double along = (x * Forward.X + y * Forward.Y) / ForwardRadius;
            double across = (-x * Forward.Y + y * Forward.X) / SideRadius;
            return along * along + across * across <= 1.000001;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
