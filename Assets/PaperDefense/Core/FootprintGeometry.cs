using System;

namespace PaperDefense.Core
{
    /// <summary>Launch-time equal-area footprint, oriented in world coordinates.</summary>
    public readonly struct FootprintGeometry
    {
        public float ForwardRadius { get; }
        public float SideRadius { get; }
        public Point2 Forward { get; }
        public float BoundingRadius => ForwardRadius;

        private FootprintGeometry(float forwardRadius, float sideRadius, Point2 forward)
        { ForwardRadius = forwardRadius; SideRadius = sideRadius; Forward = forward; }

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
            double scale = Math.Sqrt(footprint.AspectRatio);
            float along = (float)(radius * scale), across = (float)(radius / scale);
            if (!Finite(along) || !Finite(across)) throw new ArgumentOutOfRangeException(nameof(radius));
            return new FootprintGeometry(along, across, forward);
        }

        public bool Contains(Point2 center, Point2 point)
        {
            if (!Finite(center.X) || !Finite(center.Y) || !Finite(point.X) || !Finite(point.Y)) return false;
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
