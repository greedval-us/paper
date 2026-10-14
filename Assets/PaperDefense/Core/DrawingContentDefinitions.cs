using System;
using System.Collections.Generic;
using System.Globalization;

namespace PaperDefense.Core
{
    [Serializable] public sealed class DrawingCoordinateInput
    {
        public float X, Y;
        public DrawingCoordinateInput() { }
        public DrawingCoordinateInput(float x, float y) { X = x; Y = y; }
    }
    [Serializable] public sealed class DrawingContourInput
    {
        public List<DrawingCoordinateInput> Points = new List<DrawingCoordinateInput>();
    }
    [Serializable] public sealed class DrawingTemplateDefinitionInput
    {
        public string Id, DisplayName;
        public int Revision = 1;
        public float PivotX, PivotY, Weight;
        public List<DrawingContourInput> Contours = new List<DrawingContourInput>();
        public DrawingTemplateDefinition CreateSnapshot() => new DrawingTemplateDefinition(this);
    }
    public sealed class DrawingTemplateDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int Revision { get; }
        public Point2 Pivot { get; }
        public float Weight { get; }
        public IReadOnlyList<DrawingStroke> Contours { get; }
        public string Signature { get; }
        public DrawingTemplateDefinition(DrawingTemplateDefinitionInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            DrawingBindings.Validate(input.Id, "template.id");
            string prefix = input.Id + " → ";
            if (input.Revision < 1) throw new ArgumentException(UserText.Message("copy.aa6937ca6782e758", prefix));
            if (string.IsNullOrWhiteSpace(input.DisplayName)) throw new ArgumentException(UserText.Message("copy.46a4431af97cf8de", prefix));
            Finite(input.PivotX, prefix + "pivot.x"); Finite(input.PivotY, prefix + "pivot.y");
            if (Math.Abs(input.PivotX) > 1 || Math.Abs(input.PivotY) > 1) throw new ArgumentException(UserText.Message("copy.8c2f9d0d1bd831eb", prefix));
            Finite(input.Weight, prefix + "weight");
            if (input.Weight < 0) throw new ArgumentException(UserText.Message("copy.b85b8c8f37cc6798", prefix));
            if (input.Contours == null || input.Contours.Count > DrawingSnapshot.MaxStrokes) throw new ArgumentException(UserText.Message("copy.d71dea7b8d796854", prefix));
            var contours = new DrawingStroke[input.Contours.Count]; int totalPoints = 0;
            for (int i = 0; i < contours.Length; i++)
            {
                var contour = input.Contours[i];
                if (contour?.Points == null) throw new ArgumentException(UserText.Message("copy.21ff2d36d74cb2c0", prefix, i));
                totalPoints += contour.Points.Count;
                if (totalPoints > DrawingSnapshot.MaxPoints) throw new ArgumentException(UserText.Message("copy.ea0c3a921cb40851", prefix));
                var points = new DrawingPoint[contour.Points.Count];
                for (int p = 0; p < points.Length; p++)
                {
                    var point = contour.Points[p];
                    if (point == null) throw new ArgumentException(UserText.Message("copy.687561e734e55ff2", prefix, i, p));
                    try { points[p] = new DrawingPoint(point.X, point.Y); }
                    catch (ArgumentException exception) { throw new ArgumentException(prefix + "contours[" + i + "].points[" + p + "]: " + exception.Message); }
                }
                contours[i] = new DrawingStroke(points);
            }
            if (input.Weight > 0 && contours.Length == 0) throw new ArgumentException(UserText.Message("copy.2b02241cc7c7d9ae", prefix));
            Id = input.Id; DisplayName = input.DisplayName; Revision = input.Revision;
            Pivot = new Point2(input.PivotX, input.PivotY); Weight = input.Weight; Contours = Array.AsReadOnly(contours);
            var geometry = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Base, Contours) });
            Signature = Id + ":" + Revision + ":" + geometry.GeometryHash + ":" +
                Weight.ToString("R", CultureInfo.InvariantCulture) + ":" + input.PivotX.ToString("R", CultureInfo.InvariantCulture) + ":" + input.PivotY.ToString("R", CultureInfo.InvariantCulture);
        }
        internal static void Finite(float value, string field)
        { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException(UserText.Message("copy.10dc91b2ab39522d", field)); }
    }

    [Serializable] public sealed class DrawingAssessmentSettingsInput
    {
        public int Revision = 2;
        public float Tolerance = .06f, CompletionThreshold = .95f, PrecisionThreshold = .95f;
        public DrawingAssessmentSettings CreateSnapshot() => new DrawingAssessmentSettings(this);
    }
    public sealed class DrawingAssessmentSettings
    {
        public int Revision { get; }
        public float Tolerance { get; }
        public float CompletionThreshold { get; }
        public float PrecisionThreshold { get; }
        public string Signature { get; }
        public DrawingAssessmentSettings(DrawingAssessmentSettingsInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Revision < 1) throw new ArgumentException(UserText.Message("copy.93aeae4e533c77aa"));
            DrawingTemplateDefinition.Finite(input.Tolerance, "drawingAssessment.tolerance");
            DrawingTemplateDefinition.Finite(input.CompletionThreshold, "drawingAssessment.completionThreshold");
            DrawingTemplateDefinition.Finite(input.PrecisionThreshold, "drawingAssessment.precisionThreshold");
            if (input.Tolerance <= 0 || input.Tolerance > 1) throw new ArgumentException(UserText.Message("copy.32d7fc69068d9490"));
            if (input.CompletionThreshold <= 0 || input.CompletionThreshold > 1) throw new ArgumentException(UserText.Message("copy.9f531e82b2c135cd"));
            if (input.PrecisionThreshold < 0 || input.PrecisionThreshold > 1) throw new ArgumentException(UserText.Message("copy.a3b04f0c4a05e8b1"));
            Revision = input.Revision; Tolerance = input.Tolerance;
            CompletionThreshold = input.CompletionThreshold; PrecisionThreshold = input.PrecisionThreshold;
            Signature = Revision + ":" + Tolerance.ToString("R", CultureInfo.InvariantCulture) + ":" + CompletionThreshold.ToString("R", CultureInfo.InvariantCulture) + ":" + PrecisionThreshold.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
