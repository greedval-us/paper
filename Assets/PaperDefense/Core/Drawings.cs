using System;
using System.Collections.Generic;
using System.Globalization;

namespace PaperDefense.Core
{
    // Values are persistent part identifiers. Never reorder or reuse them for another part.
    public enum DrawingPartKey { Base = 0, Mount0 = 1, Mount1 = 2, Armor = 3, Feed = 4, Tracking = 5, Decoration = 6, Rail = 7 }

    /// <summary>Persistent assembly slots, independent of the content selected in them.</summary>
    public static class DrawingBindings
    {
        public const string Base = "base", Mount0 = "mount.0", Mount1 = "mount.1", Decoration = "decoration";
        static readonly string[] legacy = { Base, Mount0, Mount1, "module.armor", "module.feed", "module.tracking", Decoration, "module.rail" };
        public static string For(DrawingPartKey key)
        {
            int index = (int)key;
            if (index < 0 || index >= legacy.Length) throw new ArgumentOutOfRangeException(nameof(key));
            return legacy[index];
        }
        public static bool TryLegacy(string binding, out DrawingPartKey key)
        {
            for (int i = 0; i < legacy.Length; i++) if (legacy[i] == binding) { key = (DrawingPartKey)i; return true; }
            key = (DrawingPartKey)(-1); return false;
        }
        public static string Module(string instanceId)
        {
            Validate(instanceId, nameof(instanceId));
            return "module." + instanceId;
        }
        public static void Validate(string value, string field = "bindingId")
        {
            if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Length > 160)
                throw new ArgumentException(field + ": требуется непустой устойчивый ID длиной до 160 знаков.");
            foreach (char c in value) if (char.IsControl(c)) throw new ArgumentException(field + ": управляющие знаки запрещены.");
        }
    }

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
        public string BindingId { get; }
        public IReadOnlyList<DrawingStroke> Strokes { get; }

        public DrawingPart(DrawingPartKey key, IReadOnlyList<DrawingStroke> strokes)
            : this(DrawingBindings.For(key), strokes) { }

        public DrawingPart(string bindingId, IReadOnlyList<DrawingStroke> strokes)
        {
            DrawingBindings.Validate(bindingId);
            if (strokes == null) throw new ArgumentNullException(nameof(strokes));
            if (strokes.Count > DrawingSnapshot.MaxStrokes)
                throw new ArgumentException("На странице помещается не более " + DrawingSnapshot.MaxStrokes + " штрихов.", nameof(strokes));
            BindingId = bindingId;
            DrawingBindings.TryLegacy(bindingId, out DrawingPartKey key); Key = key;
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
        public const int CurrentFormatVersion = 3, CurrentBindingVersion = 2;
        public const int LegacyFormatVersion = 2, LegacyBindingVersion = 1, LegacyPartCount = 8, MaximumParts = 128;
        public const int MaxStrokes = 128, MaxPoints = 2048;
        public static DrawingSnapshot Empty { get; } = new DrawingSnapshot(Array.Empty<DrawingPart>());
        public int FormatVersion { get; }
        public int BindingVersion { get; }
        public string GeometryHash { get; }
        public IReadOnlyList<DrawingPart> Parts { get; }
        public int StrokeCount { get; }
        public int PointCount { get; }
        public bool IsEmpty { get; }
        readonly Dictionary<string, DrawingPart> byBinding;

        public DrawingSnapshot(IReadOnlyList<DrawingPart> parts)
        {
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            if (parts.Count > MaximumParts) throw new ArgumentException("В рисунке слишком много привязок частей.", nameof(parts));
            var owned = new DrawingPart[LegacyPartCount];
            var extra = new SortedDictionary<string, DrawingPart>(StringComparer.Ordinal);
            int strokes = 0, points = 0;
            bool hasVisibleSegment = false;
            for (int i = 0; i < parts.Count; i++)
            {
                DrawingPart source = parts[i];
                if (source == null) throw new ArgumentException("Часть рисунка не может быть пустой ссылкой.", nameof(parts));
                if (DrawingBindings.TryLegacy(source.BindingId, out DrawingPartKey legacyKey))
                {
                    int key = (int)legacyKey;
                    if (owned[key] != null) throw new ArgumentException("Часть рисунка повторяется.", nameof(parts));
                    owned[key] = new DrawingPart(source.BindingId, source.Strokes);
                }
                else
                {
                    if (extra.ContainsKey(source.BindingId)) throw new ArgumentException("Привязка части рисунка повторяется.", nameof(parts));
                    extra.Add(source.BindingId, new DrawingPart(source.BindingId, source.Strokes));
                }
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
            var all = new List<DrawingPart>(owned); all.AddRange(extra.Values);
            if (all.Count > MaximumParts) throw new ArgumentException("В рисунке слишком много привязок частей.", nameof(parts));
            FormatVersion = extra.Count == 0 ? LegacyFormatVersion : CurrentFormatVersion;
            BindingVersion = extra.Count == 0 ? LegacyBindingVersion : CurrentBindingVersion;
            byBinding = new Dictionary<string, DrawingPart>(StringComparer.Ordinal);
            foreach (DrawingPart part in all) byBinding.Add(part.BindingId, part);
            StrokeCount = strokes; PointCount = points; IsEmpty = !hasVisibleSegment;
            Parts = all.AsReadOnly();
            GeometryHash = Hash(Parts, FormatVersion, BindingVersion).ToString("x16", CultureInfo.InvariantCulture);
        }

        public DrawingPart GetPart(DrawingPartKey key)
        {
            if (!Enum.IsDefined(typeof(DrawingPartKey), key)) throw new ArgumentOutOfRangeException(nameof(key));
            return Parts[(int)key];
        }

        public DrawingPart GetPart(string bindingId)
        {
            DrawingBindings.Validate(bindingId);
            return byBinding.TryGetValue(bindingId, out DrawingPart part) ? part : new DrawingPart(bindingId, Array.Empty<DrawingStroke>());
        }

        public DrawingSnapshot DeepCopy() => new DrawingSnapshot(Parts);

        static ulong Hash(IReadOnlyList<DrawingPart> parts, int format, int binding)
        {
            ulong hash = 14695981039346656037UL;
            Add(ref hash, format); Add(ref hash, binding);
            foreach (DrawingPart part in parts)
            {
                if (format == LegacyFormatVersion) Add(ref hash, (int)part.Key);
                else { foreach (char c in part.BindingId) Add(ref hash, c); Add(ref hash, 0); }
                Add(ref hash, part.Strokes.Count);
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
