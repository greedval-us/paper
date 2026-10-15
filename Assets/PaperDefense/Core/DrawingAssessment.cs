using System;
using System.Collections.Generic;
using System.Globalization;

namespace PaperDefense.Core
{
    public readonly struct DrawingPartFeedback
    {
        public readonly DrawingPartKey Part;
        public readonly string BindingId;
        public readonly float Quality, Coverage, Precision;
        public readonly int IncompleteContour;
        public readonly string Message, ArchivedMessage;
        internal DrawingPartFeedback(DrawingPartKey part, float quality, float coverage, float precision, int incompleteContour, string message)
            : this(DrawingBindings.For(part), quality, coverage, precision, incompleteContour, message) { }
        public DrawingPartFeedback(string bindingId, float quality, float coverage, float precision, int incompleteContour, string message, string archivedMessage = null)
        {
            DrawingBindings.Validate(bindingId); DrawingTemplateDefinition.Finite(quality, bindingId + ".quality");
            DrawingTemplateDefinition.Finite(coverage, bindingId + ".coverage"); DrawingTemplateDefinition.Finite(precision, bindingId + ".precision");
            if (quality < 0 || quality > 100 || coverage < 0 || coverage > 1 || precision < 0 || precision > 1 || incompleteContour < 0)
                throw new ArgumentException(UserText.Message("copy.0e0173ae1aa49e7e", bindingId));
            BindingId = bindingId; DrawingBindings.TryLegacy(bindingId, out DrawingPartKey key); Part = key;
            Quality = quality; Coverage = coverage; Precision = precision; IncompleteContour = incompleteContour; Message = message ?? "";
            ArchivedMessage = archivedMessage ?? "";
        }
    }

    /// <summary>A saved assessment result, tied to authored geometry and selected slots, never to mutable assets.</summary>
    public sealed class DrawingEvaluationStamp
    {
        public int RuleVersion { get; }
        public string ReferenceSignature { get; }
        public string DrawingHash { get; }
        public string ConstructionHash { get; }
        public float Quality { get; }
        public IReadOnlyList<DrawingPartFeedback> PartFeedback { get; }
        public DrawingEvaluationStamp(int ruleVersion, string referenceSignature, string drawingHash, string constructionHash,
            float quality, IReadOnlyList<DrawingPartFeedback> feedback = null)
        {
            if (ruleVersion < 1 || string.IsNullOrEmpty(referenceSignature) || string.IsNullOrEmpty(drawingHash) || string.IsNullOrEmpty(constructionHash))
                throw new ArgumentException(UserText.Message("copy.a32aa2295733c329"));
            DrawingTemplateDefinition.Finite(quality, "drawingEvaluation.quality");
            if (quality < 0 || quality > 100) throw new ArgumentException(UserText.Message("copy.8e80e85c46e36428"));
            if (feedback != null && feedback.Count > DrawingSnapshot.MaximumParts) throw new ArgumentException(UserText.Message("copy.8ba450993b9dedb5"));
            var owned = new DrawingPartFeedback[feedback?.Count ?? 0]; var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < owned.Length; i++)
            {
                var part = feedback[i];
                if (part.BindingId == null || !seen.Add(part.BindingId)) throw new ArgumentException(UserText.Message("copy.04ebb6ffcd5e4c59"));
                owned[i] = new DrawingPartFeedback(part.BindingId, part.Quality, part.Coverage, part.Precision, part.IncompleteContour, part.Message, part.ArchivedMessage);
            }
            RuleVersion = ruleVersion; ReferenceSignature = referenceSignature; DrawingHash = drawingHash;
            ConstructionHash = constructionHash; Quality = quality; PartFeedback = Array.AsReadOnly(owned);
        }
    }

    public sealed class DrawingAssessment
    {
        public int RuleVersion { get; }
        public string DrawingHash { get; }
        public string ConstructionHash { get; }
        public float Quality { get; }
        public string ReferenceSignature { get; }
        public bool IsArchived { get; }
        public IReadOnlyList<DrawingPartFeedback> PartFeedback { get; }
        internal DrawingAssessment(int ruleVersion, string drawingHash, string constructionHash, float quality,
            IReadOnlyList<DrawingPartFeedback> feedback, string referenceSignature = "legacy.rules.v2", bool isArchived = false)
        {
            RuleVersion = ruleVersion; DrawingHash = drawingHash; ConstructionHash = constructionHash; Quality = quality;
            ReferenceSignature = referenceSignature; IsArchived = isArchived;
            var owned = new DrawingPartFeedback[feedback.Count];
            for (int i = 0; i < owned.Length; i++) owned[i] = feedback[i];
            PartFeedback = Array.AsReadOnly(owned);
        }
        public DrawingEvaluationStamp ToStamp() => new DrawingEvaluationStamp(RuleVersion, ReferenceSignature, DrawingHash, ConstructionHash, Quality, PartFeedback);
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
        static readonly DrawingAssessmentSettings legacySettings = new DrawingAssessmentSettingsInput().CreateSnapshot();

        public static DrawingAssessment Evaluate(TowerBlueprint blueprint, DrawingEvaluationStamp stamp = null)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            string constructionHash = ConstructionHash(blueprint);
            // Historical stamps stay readable, but free construction never invokes template matching.
            if (blueprint.IsFreeform)
                return new DrawingAssessment(blueprint.FreeformRuleVersion, blueprint.Drawing.GeometryHash, constructionHash,
                    100, Array.Empty<DrawingPartFeedback>(), "freeform.v" + blueprint.FreeformRuleVersion);
            DrawingAssessmentSettings settings = blueprint.Content?.Assessment ?? legacySettings;
            IReadOnlyList<DrawingPartTemplate> templates;
            try { templates = DrawingTemplateCatalog.Create(blueprint); }
            catch (ArgumentException)
            {
                if (stamp != null) return RestoreStamp(blueprint, constructionHash, stamp, "unavailable");
                return new DrawingAssessment(settings.Revision, blueprint.Drawing.GeometryHash, constructionHash,
                    blueprint.Origin == DrawingOrigin.LegacyStandard ? 100 : 0, Array.Empty<DrawingPartFeedback>());
            }
            string referenceSignature = ReferenceSignature(settings, templates);
            string key = referenceSignature + ":" + (int)blueprint.Origin + ":" + blueprint.Drawing.GeometryHash + ":" + constructionHash;
            if (stamp != null)
            {
                DrawingAssessment restored = RestoreStamp(blueprint, constructionHash, stamp, referenceSignature);
                lock (cacheGate)
                    if (cache.TryGetValue(key, out DrawingAssessment cachedSaved) && !restored.IsArchived &&
                        cachedSaved.Quality == stamp.Quality && cachedSaved.RuleVersion == stamp.RuleVersion &&
                        SameFeedback(cachedSaved.PartFeedback, stamp.PartFeedback))
                        return cachedSaved;
                return restored;
            }
            lock (cacheGate) if (cache.TryGetValue(key, out DrawingAssessment cachedInitial)) return cachedInitial;
            DrawingAssessment result = Assess(blueprint, constructionHash, settings, templates, referenceSignature);
            lock (cacheGate)
            {
                if (cache.TryGetValue(key, out DrawingAssessment cachedExisting)) return cachedExisting;
                if (cache.Count >= CacheCapacity) cache.Remove(cacheOrder.Dequeue());
                cache.Add(key, result); cacheOrder.Enqueue(key);
            }
            return result;
        }

        static bool SameFeedback(IReadOnlyList<DrawingPartFeedback> first, IReadOnlyList<DrawingPartFeedback> second)
        {
            if (first.Count != second.Count) return false;
            for (int i = 0; i < first.Count; i++)
            {
                var a = first[i]; var b = second[i];
                if (a.BindingId != b.BindingId || a.Quality != b.Quality || a.Coverage != b.Coverage ||
                    a.Precision != b.Precision || a.IncompleteContour != b.IncompleteContour ||
                    a.Message != b.Message || a.ArchivedMessage != b.ArchivedMessage) return false;
            }
            return true;
        }

        static DrawingAssessment RestoreStamp(TowerBlueprint blueprint, string constructionHash, DrawingEvaluationStamp stamp, string referenceSignature)
        {
            if (stamp.DrawingHash != blueprint.Drawing.GeometryHash || stamp.ConstructionHash != constructionHash)
                throw new ArgumentException(UserText.Message("copy.9783048127d5ed7e"));
            return new DrawingAssessment(stamp.RuleVersion, stamp.DrawingHash, stamp.ConstructionHash, stamp.Quality,
                stamp.PartFeedback, stamp.ReferenceSignature, stamp.ReferenceSignature != referenceSignature);
        }

        static DrawingAssessment Assess(TowerBlueprint blueprint, string constructionHash, DrawingAssessmentSettings settings,
            IReadOnlyList<DrawingPartTemplate> templates, string referenceSignature)
        {
            if (blueprint.Origin == DrawingOrigin.LegacyStandard)
                return new DrawingAssessment(settings.Revision, blueprint.Drawing.GeometryHash, constructionHash, 100,
                    Array.Empty<DrawingPartFeedback>(), referenceSignature);
            var feedback = new List<DrawingPartFeedback>();
            float weightedQuality = 0, totalWeight = 0;
            Cell[] toleranceOffsets = ToleranceOffsets(settings.Tolerance);
            foreach (DrawingPartTemplate template in templates)
            {
                float weight = template.Weight;
                if (weight == 0) continue;
                bool[] reference = Raster(template.Contours);
                bool[] authored = Raster(blueprint.Drawing.GetPart(template.BindingId).Strokes);
                bool[] nearReference = Dilate(reference, toleranceOffsets), nearAuthored = Dilate(authored, toleranceOffsets);
                float coverage = FractionMatched(reference, nearAuthored);
                float precision = FractionMatched(authored, nearReference);
                float quality = coverage + precision > 0 ? 200 * coverage * precision / (coverage + precision) : 0;
                int unfinished = 0;
                for (int contour = 0; contour < template.Contours.Count; contour++)
                    if (FractionMatched(Raster(new[] { template.Contours[contour] }), nearAuthored) < settings.CompletionThreshold)
                    { unfinished = contour + 1; break; }
                string name = template.DisplayName;
                string message = unfinished > 0 ? UserText.Message("copy.3cc592b5b09bbd9f", name, unfinished) :
                    precision < settings.PrecisionThreshold ? UserText.Message("copy.5c24ada686c606b4", name) : UserText.Message("copy.e19aba16f8c4e47b", name);
                feedback.Add(new DrawingPartFeedback(template.BindingId, quality, coverage, precision, unfinished, message));
                weightedQuality += quality * weight; totalWeight += weight;
            }
            float overall = totalWeight > 0 ? weightedQuality / totalWeight : 0;
            return new DrawingAssessment(settings.Revision, blueprint.Drawing.GeometryHash, constructionHash,
                Math.Max(0, Math.Min(100, overall)), feedback, referenceSignature);
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
                case DrawingPartKey.Base: return UserText.Message("copy.0369683b41e6de45");
                case DrawingPartKey.Mount0: return UserText.Message("copy.fc2e45f6e3ac7703");
                case DrawingPartKey.Mount1: return UserText.Message("copy.c0e8f8688c74f19e");
                case DrawingPartKey.Armor: return UserText.Message("copy.e41e849ff6126cff");
                case DrawingPartKey.Feed: return UserText.Message("copy.d4a738612fc5e701");
                case DrawingPartKey.Tracking: return UserText.Message("copy.41e4c92b11a67cdf");
                case DrawingPartKey.Rail: return UserText.Message("copy.215d555ed53c9640");
                default: return UserText.Message("copy.a26f1865b599b9c4");
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

        static Cell[] ToleranceOffsets(float tolerance)
        {
            var offsets = new List<Cell>();
            float radius = tolerance * GridScale; int bound = (int)Math.Ceiling(radius);
            for (int y = -bound; y <= bound; y++) for (int x = -bound; x <= bound; x++)
                if (x * x + y * y <= radius * radius) offsets.Add(new Cell(x, y));
            return offsets.ToArray();
        }
        static bool[] Dilate(bool[] source, Cell[] toleranceOffsets)
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
            bool legacy = blueprint.GetWeaponId(2)==null && Enum.IsDefined(typeof(ChassisKind), blueprint.Chassis) && Enum.IsDefined(typeof(StructureKind), blueprint.FirstWeapon) &&
                (blueprint.SecondWeaponId == null || (blueprint.SecondWeapon.HasValue && Enum.IsDefined(typeof(StructureKind), blueprint.SecondWeapon.Value)));
            foreach (var selected in blueprint.ModuleSelections)
                legacy &= DrawingBindings.TryLegacy(DrawingBindings.Module(selected.InstanceId), out _) && selected.DefinitionId == "module." + selected.InstanceId;
            if (legacy)
            {
                AddHash(ref hash, DrawingSnapshot.LegacyBindingVersion); AddHash(ref hash, (int)blueprint.Chassis);
                AddHash(ref hash, (int)blueprint.FirstWeapon); AddHash(ref hash, blueprint.SecondWeapon.HasValue ? (int)blueprint.SecondWeapon.Value : -1);
                AddHash(ref hash, (int)blueprint.Modules);
            }
            else
            {
                AddHash(ref hash, DrawingSnapshot.CurrentBindingVersion); AddHash(ref hash, blueprint.ChassisId);
                AddHash(ref hash, blueprint.FirstWeaponId); AddHash(ref hash, blueprint.SecondWeaponId ?? "");
                if(blueprint.GetWeaponId(2)!=null){AddHash(ref hash,DrawingBindings.Mount2);AddHash(ref hash,blueprint.GetWeaponId(2));}
                var modules = new List<ModuleSelection>(blueprint.ModuleSelections);
                modules.Sort((a, b) => string.CompareOrdinal(a.InstanceId, b.InstanceId));
                foreach (var selected in modules) { AddHash(ref hash, selected.InstanceId); AddHash(ref hash, selected.DefinitionId); }
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
        static string ReferenceSignature(DrawingAssessmentSettings settings, IReadOnlyList<DrawingPartTemplate> templates)
        {
            ulong hash = 14695981039346656037UL; AddHash(ref hash, settings.Signature);
            foreach (var template in templates)
            {
                AddHash(ref hash, template.BindingId); AddHash(ref hash, template.ReferenceSignature);
                AddHash(ref hash, template.Pivot.X.ToString("R", CultureInfo.InvariantCulture)); AddHash(ref hash, template.Pivot.Y.ToString("R", CultureInfo.InvariantCulture));
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
        static void AddHash(ref ulong hash, string value) { foreach (char c in value) AddHash(ref hash, c); AddHash(ref hash, 0); }
        static void AddHash(ref ulong hash, int value)
        {
            unchecked
            {
                for (int shift = 0; shift < 32; shift += 8) { hash ^= (byte)(value >> shift); hash *= 1099511628211UL; }
            }
        }
    }
}
