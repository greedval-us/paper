using System;
using System.Collections.Generic;
using System.Globalization;

namespace PaperDefense.Core
{
    // Values are persistent part identifiers. Never reorder or reuse them for another part.
    public enum DrawingPartKey { Base = 0, Mount0 = 1, Mount1 = 2, Armor = 3, Feed = 4, Tracking = 5, Decoration = 6, Rail = 7 }

    /// <summary>Part-local coordinates; +Y is the weapon's forward axis. Fixed precision keeps saves and hashes stable.</summary>
    public readonly struct DrawingPoint : IEquatable<DrawingPoint>
    {
        public const int Quantization = 4096;
        public readonly float X, Y;

        public DrawingPoint(float x, float y)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || x < -1 || x > 1)
                throw new ArgumentOutOfRangeException(nameof(x), "Координаты рисунка должны быть конечными и находиться между −1 и 1.");
            if (float.IsNaN(y) || float.IsInfinity(y) || y < -1 || y > 1)
                throw new ArgumentOutOfRangeException(nameof(y), "Координаты рисунка должны быть конечными и находиться между −1 и 1.");
            X = (float)Math.Round(x * Quantization) / Quantization;
            Y = (float)Math.Round(y * Quantization) / Quantization;
        }

        public bool Equals(DrawingPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object other) => other is DrawingPoint point && Equals(point);
        public override int GetHashCode() => unchecked(((int)(X * Quantization) * 397) ^ (int)(Y * Quantization));
    }

    /// <summary>Immutable authored polyline. Its width is visual and never changes collision or range.</summary>
    public sealed class DrawingStroke
    {
        public const float Width = .018f;
        public IReadOnlyList<DrawingPoint> Points { get; }

        public DrawingStroke(IReadOnlyList<DrawingPoint> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count < 2 || points.Count > DrawingSnapshot.MaxPoints)
                throw new ArgumentException("Штрих должен содержать от 2 до " + DrawingSnapshot.MaxPoints + " точек.", nameof(points));
            var owned = new DrawingPoint[points.Count];
            for (int i = 0; i < owned.Length; i++) owned[i] = new DrawingPoint(points[i].X, points[i].Y);
            Points = Array.AsReadOnly(owned);
        }
    }

    public sealed class DrawingPart
    {
        public DrawingPartKey Key { get; }
        public IReadOnlyList<DrawingStroke> Strokes { get; }

        public DrawingPart(DrawingPartKey key, IReadOnlyList<DrawingStroke> strokes)
        {
            if (!Enum.IsDefined(typeof(DrawingPartKey), key)) throw new ArgumentOutOfRangeException(nameof(key));
            if (strokes == null) throw new ArgumentNullException(nameof(strokes));
            if (strokes.Count > DrawingSnapshot.MaxStrokes)
                throw new ArgumentException("На странице помещается не более " + DrawingSnapshot.MaxStrokes + " штрихов.", nameof(strokes));
            Key = key;
            var owned = new DrawingStroke[strokes.Count];
            for (int i = 0; i < owned.Length; i++)
            {
                if (strokes[i] == null) throw new ArgumentException("Штрих не может быть пустой ссылкой.", nameof(strokes));
                owned[i] = new DrawingStroke(strokes[i].Points);
            }
            Strokes = Array.AsReadOnly(owned);
        }
    }

    /// <summary>Canonical owned source data. Thumbnails, meshes and assessments are derived from this snapshot.</summary>
    public sealed class DrawingSnapshot
    {
        public const int CurrentFormatVersion = 2, CurrentBindingVersion = 1;
        public const int MaxStrokes = 128, MaxPoints = 2048;
        public static DrawingSnapshot Empty { get; } = new DrawingSnapshot(Array.Empty<DrawingPart>());
        public int FormatVersion => CurrentFormatVersion;
        public int BindingVersion => CurrentBindingVersion;
        public string GeometryHash { get; }
        public IReadOnlyList<DrawingPart> Parts { get; }
        public int StrokeCount { get; }
        public int PointCount { get; }
        public bool IsEmpty { get; }

        public DrawingSnapshot(IReadOnlyList<DrawingPart> parts)
        {
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            const int partCount = 8;
            if (parts.Count > partCount) throw new ArgumentException("В рисунке не более восьми частей.", nameof(parts));
            var owned = new DrawingPart[partCount];
            int strokes = 0, points = 0;
            bool hasVisibleSegment = false;
            for (int i = 0; i < parts.Count; i++)
            {
                DrawingPart source = parts[i];
                if (source == null) throw new ArgumentException("Часть рисунка не может быть пустой ссылкой.", nameof(parts));
                int key = (int)source.Key;
                if (key < 0 || key >= partCount) throw new ArgumentException("Неизвестная часть рисунка.", nameof(parts));
                if (owned[key] != null) throw new ArgumentException("Часть рисунка повторяется.", nameof(parts));
                owned[key] = new DrawingPart(source.Key, source.Strokes);
                strokes += source.Strokes.Count;
                foreach (DrawingStroke stroke in source.Strokes)
                {
                    points += stroke.Points.Count;
                    for (int point = 1; point < stroke.Points.Count; point++)
                        if (!stroke.Points[point].Equals(stroke.Points[point - 1])) hasVisibleSegment = true;
                }
                if (strokes > MaxStrokes || points > MaxPoints)
                    throw new ArgumentException("На странице не более " + MaxStrokes + " штрихов и " + MaxPoints + " точек.", nameof(parts));
            }
            for (int i = 0; i < owned.Length; i++)
                if (owned[i] == null) owned[i] = new DrawingPart((DrawingPartKey)i, Array.Empty<DrawingStroke>());
            StrokeCount = strokes; PointCount = points; IsEmpty = !hasVisibleSegment;
            Parts = Array.AsReadOnly(owned);
            GeometryHash = Hash(owned).ToString("x16", CultureInfo.InvariantCulture);
        }

        public DrawingPart GetPart(DrawingPartKey key)
        {
            if (!Enum.IsDefined(typeof(DrawingPartKey), key)) throw new ArgumentOutOfRangeException(nameof(key));
            return Parts[(int)key];
        }

        public DrawingSnapshot DeepCopy() => new DrawingSnapshot(Parts);

        static ulong Hash(IReadOnlyList<DrawingPart> parts)
        {
            ulong hash = 14695981039346656037UL;
            Add(ref hash, CurrentFormatVersion); Add(ref hash, CurrentBindingVersion);
            foreach (DrawingPart part in parts)
            {
                Add(ref hash, (int)part.Key); Add(ref hash, part.Strokes.Count);
                foreach (DrawingStroke stroke in part.Strokes)
                {
                    Add(ref hash, stroke.Points.Count);
                    foreach (DrawingPoint point in stroke.Points)
                    { Add(ref hash, (int)(point.X * DrawingPoint.Quantization)); Add(ref hash, (int)(point.Y * DrawingPoint.Quantization)); }
                }
            }
            return hash;
        }

        static void Add(ref ulong hash, int value)
        {
            unchecked
            {
                for (int shift = 0; shift < 32; shift += 8)
                { hash ^= (byte)(value >> shift); hash *= 1099511628211UL; }
            }
        }
    }
}
