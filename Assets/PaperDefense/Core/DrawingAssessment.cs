using System;
using System.Collections.Generic;
using System.Globalization;

namespace PaperDefense.Core
{
    public readonly struct DrawingPartFeedback
    {
        public readonly DrawingPartKey Part;
        public readonly float Quality, Coverage, Precision;
        public readonly int IncompleteContour;
        public readonly string Message;
        internal DrawingPartFeedback(DrawingPartKey part, float quality, float coverage, float precision, int incompleteContour, string message)
        { Part = part; Quality = quality; Coverage = coverage; Precision = precision; IncompleteContour = incompleteContour; Message = message; }
    }

    public sealed class DrawingAssessment
    {
        public int RuleVersion { get; }
        public string DrawingHash { get; }
        public string ConstructionHash { get; }
        public float Quality { get; }
        public IReadOnlyList<DrawingPartFeedback> PartFeedback { get; }
        internal DrawingAssessment(int ruleVersion, string drawingHash, string constructionHash, float quality,
            IReadOnlyList<DrawingPartFeedback> feedback)
        {
            RuleVersion = ruleVersion; DrawingHash = drawingHash; ConstructionHash = constructionHash; Quality = quality;
            var owned = new DrawingPartFeedback[feedback.Count];
            for (int i = 0; i < owned.Length; i++) owned[i] = feedback[i];
            PartFeedback = Array.AsReadOnly(owned);
        }
    }

    public static class DrawingEvaluator
    {
        public const int CurrentRuleVersion = 2, CacheCapacity = 256;
        public const float Tolerance = .06f;
        const int GridSize = 128;
        const float GridScale = GridSize / 2f;
        static readonly object cacheGate = new object();
        static readonly Dictionary<string, DrawingAssessment> cache = new Dictionary<string, DrawingAssessment>();
        static readonly Queue<string> cacheOrder = new Queue<string>();
        static readonly Cell[] toleranceOffsets = ToleranceOffsets();

        public static DrawingAssessment Evaluate(TowerBlueprint blueprint)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            string constructionHash = ConstructionHash(blueprint);
            string key = CurrentRuleVersion.ToString(CultureInfo.InvariantCulture) + ":" + (int)blueprint.Origin + ":" +
                blueprint.Drawing.GeometryHash + ":" + constructionHash;
            lock (cacheGate) if (cache.TryGetValue(key, out DrawingAssessment cachedInitial)) return cachedInitial;
            DrawingAssessment result = Assess(blueprint, constructionHash);
            lock (cacheGate)
            {
                if (cache.TryGetValue(key, out DrawingAssessment cachedExisting)) return cachedExisting;
                if (cache.Count >= CacheCapacity) cache.Remove(cacheOrder.Dequeue());
                cache.Add(key, result); cacheOrder.Enqueue(key);
            }
            return result;
        }

        /// <summary>The single efficiency formula; construction feed and external upgrades are applied separately.</summary>
        public static float FireMultiplier(float quality, float minimum = .85f, float maximum = 1)
        {
            if (float.IsNaN(quality) || float.IsInfinity(quality)) throw new ArgumentOutOfRangeException(nameof(quality));
            if (float.IsNaN(minimum) || float.IsInfinity(minimum) || minimum <= 0 || minimum > 1)
                throw new ArgumentOutOfRangeException(nameof(minimum));
            if (float.IsNaN(maximum) || float.IsInfinity(maximum) || maximum < minimum || maximum > 1)
                throw new ArgumentOutOfRangeException(nameof(maximum));
            return minimum + (maximum - minimum) * Math.Max(0, Math.Min(100, quality)) / 100;
        }

        static DrawingAssessment Assess(TowerBlueprint blueprint, string constructionHash)
        {
            if (blueprint.Origin == DrawingOrigin.LegacyStandard)
                return new DrawingAssessment(CurrentRuleVersion, blueprint.Drawing.GeometryHash, constructionHash, 100,
                    Array.Empty<DrawingPartFeedback>());
            var feedback = new List<DrawingPartFeedback>();
            float weightedQuality = 0, totalWeight = 0;
            IReadOnlyList<DrawingPartTemplate> templates;
            try { templates = DrawingTemplateCatalog.Create(blueprint); }
            catch (ArgumentException)
            {
                return new DrawingAssessment(CurrentRuleVersion, blueprint.Drawing.GeometryHash, constructionHash, 0, feedback);
            }
            foreach (DrawingPartTemplate template in templates)
            {
                float weight = Weight(template.Key);
                if (weight == 0) continue;
                bool[] reference = Raster(template.Contours);
                bool[] authored = Raster(blueprint.Drawing.GetPart(template.Key).Strokes);
                bool[] nearReference = Dilate(reference), nearAuthored = Dilate(authored);
                float coverage = FractionMatched(reference, nearAuthored);
                float precision = FractionMatched(authored, nearReference);
                float quality = coverage + precision > 0 ? 200 * coverage * precision / (coverage + precision) : 0;
                int unfinished = 0;
                for (int contour = 0; contour < template.Contours.Count; contour++)
                    if (FractionMatched(Raster(new[] { template.Contours[contour] }), nearAuthored) < .95f)
                    { unfinished = contour + 1; break; }
                string name = PartName(template.Key);
                string message = unfinished > 0 ? name + ": контур " + unfinished + " ещё не завершён." :
                    precision < .95f ? name + ": много линий в стороне от образца." : name + ": контуры читаются уверенно.";
                feedback.Add(new DrawingPartFeedback(template.Key, quality, coverage, precision, unfinished, message));
                weightedQuality += quality * weight; totalWeight += weight;
            }
            float overall = totalWeight > 0 ? weightedQuality / totalWeight : 0;
            return new DrawingAssessment(CurrentRuleVersion, blueprint.Drawing.GeometryHash, constructionHash,
                Math.Max(0, Math.Min(100, overall)), feedback);
        }

        static float Weight(DrawingPartKey key)
        {
            switch (key)
            {
                case DrawingPartKey.Base: case DrawingPartKey.Mount0: case DrawingPartKey.Mount1: return 25;
                case DrawingPartKey.Armor: case DrawingPartKey.Feed: case DrawingPartKey.Tracking: case DrawingPartKey.Rail: return 5;
                default: return 0;
            }
        }

        static string PartName(DrawingPartKey key)
        {
            switch (key)
            {
                case DrawingPartKey.Base: return "Основание";
                case DrawingPartKey.Mount0: return "Орудие 1";
                case DrawingPartKey.Mount1: return "Орудие 2";
                case DrawingPartKey.Armor: return "Бронепластины";
                case DrawingPartKey.Feed: return "Подача";
                case DrawingPartKey.Tracking: return "Наведение";
                case DrawingPartKey.Rail: return "Направляющая";
                default: return "Декор";
            }
        }

        // A union of touched cells counts geometry once: point density, stroke order and retracing carry no weight.
        static bool[] Raster(IReadOnlyList<DrawingStroke> strokes)
        {
            var cells = new bool[GridSize * GridSize];
            foreach (DrawingStroke stroke in strokes)
                for (int i = 1; i < stroke.Points.Count; i++) RasterSegment(cells, stroke.Points[i - 1], stroke.Points[i]);
            return cells;
        }

        static void RasterSegment(bool[] cells, DrawingPoint from, DrawingPoint to)
        {
            if (from.Equals(to)) return;
            double fromX = (from.X + 1) * GridScale, fromY = (from.Y + 1) * GridScale;
            double toX = (to.X + 1) * GridScale, toY = (to.Y + 1) * GridScale;
            double dx = toX - fromX, dy = toY - fromY;
            int x = GridCell(fromX), y = GridCell(fromY), endX = GridCell(toX), endY = GridCell(toY);
            int stepX = Math.Sign(dx), stepY = Math.Sign(dy);
            double deltaX = stepX == 0 ? double.PositiveInfinity : 1 / Math.Abs(dx);
            double deltaY = stepY == 0 ? double.PositiveInfinity : 1 / Math.Abs(dy);
            double nextX = stepX == 0 ? double.PositiveInfinity :
                (stepX > 0 ? x + 1 - fromX : fromX - x) / Math.Abs(dx);
            double nextY = stepY == 0 ? double.PositiveInfinity :
                (stepY > 0 ? y + 1 - fromY : fromY - y) / Math.Abs(dy);
            bool verticalBoundary = stepX == 0 && fromX == Math.Floor(fromX);
            bool horizontalBoundary = stepY == 0 && fromY == Math.Floor(fromY);
            MarkEndpoint(cells, fromX, fromY); MarkEndpoint(cells, toX, toY);
            // Supercover traversal includes both side cells at a corner. Reversing or subdividing a segment
            // therefore keeps the same geometric union, including exact grid-edge strokes.
            for (int traversed = 0; traversed <= GridSize * 2 + 4; traversed++)
            {
                Mark(cells, x, y);
                if (verticalBoundary) Mark(cells, (int)fromX - 1, y);
                if (horizontalBoundary) Mark(cells, x, (int)fromY - 1);
                if (x == endX && y == endY) break;
                if (Math.Abs(nextX - nextY) < 1e-12)
                {
                    Mark(cells, x + stepX, y); Mark(cells, x, y + stepY);
                    x += stepX; y += stepY; nextX += deltaX; nextY += deltaY;
                }
                else if (nextX < nextY) { x += stepX; nextX += deltaX; }
                else { y += stepY; nextY += deltaY; }
            }
        }

        static int GridCell(double value) => Math.Max(0, Math.Min(GridSize - 1, (int)Math.Floor(value)));
        static void MarkEndpoint(bool[] cells, double x, double y)
        {
            int cellX = (int)Math.Floor(x), cellY = (int)Math.Floor(y);
            Mark(cells, cellX, cellY);
            if (x == cellX) Mark(cells, cellX - 1, cellY);
            if (y == cellY) Mark(cells, cellX, cellY - 1);
            if (x == cellX && y == cellY) Mark(cells, cellX - 1, cellY - 1);
        }
        static void Mark(bool[] cells, int x, int y)
        { if (x >= 0 && y >= 0 && x < GridSize && y < GridSize) cells[y * GridSize + x] = true; }

        static Cell[] ToleranceOffsets()
        {
            var offsets = new List<Cell>();
            float radius = Tolerance * GridScale; int bound = (int)Math.Ceiling(radius);
            for (int y = -bound; y <= bound; y++) for (int x = -bound; x <= bound; x++)
                if (x * x + y * y <= radius * radius) offsets.Add(new Cell(x, y));
            return offsets.ToArray();
        }
        static bool[] Dilate(bool[] source)
        {
            var near = new bool[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (!source[i]) continue;
                int x = i % GridSize, y = i / GridSize;
                foreach (Cell offset in toleranceOffsets) Mark(near, x + offset.X, y + offset.Y);
            }
            return near;
        }
        static float FractionMatched(bool[] source, bool[] near)
        {
            int count = 0, matched = 0;
            for (int i = 0; i < source.Length; i++) if (source[i]) { count++; if (near[i]) matched++; }
            return count > 0 ? (float)matched / count : 0;
        }

        static string ConstructionHash(TowerBlueprint blueprint)
        {
            ulong hash = 14695981039346656037UL;
            AddHash(ref hash, DrawingSnapshot.CurrentBindingVersion); AddHash(ref hash, (int)blueprint.Chassis);
            AddHash(ref hash, (int)blueprint.FirstWeapon); AddHash(ref hash, blueprint.SecondWeapon.HasValue ? (int)blueprint.SecondWeapon.Value : -1);
            AddHash(ref hash, (int)blueprint.Modules);
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
        static void AddHash(ref ulong hash, int value)
        {
            unchecked
            {
                for (int shift = 0; shift < 32; shift += 8) { hash ^= (byte)(value >> shift); hash *= 1099511628211UL; }
            }
        }
    }
}
