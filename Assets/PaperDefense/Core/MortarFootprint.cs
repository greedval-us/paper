using System;
using System.Collections.Generic;
namespace PaperDefense.Core
{
    public sealed class MortarFootprint
    {
        public const float MaximumAspectRatio = 3;
        // File/numeric protection; the editable weapon definition supplies the playable limit.
        public const float StructuralMaximumAspectRatio = 64;
        public static MortarFootprint Circle { get; } = new MortarFootprint(1);
        public float AspectRatio { get; }
        public bool IsContour => Authored.Count > 0;
        public bool IsCircle => !IsContour && AspectRatio == 1;
        public bool FitsAspectLimit(float maximum) => AspectRatio <= maximum + (IsContour ? FootprintContour.AspectTolerance : 0);
        // X is screen-right (clockwise from forward), Y points from tower to target.
        public IReadOnlyList<Point2> Authored { get; } = Array.AsReadOnly(Array.Empty<Point2>());
        public IReadOnlyList<Point2> Normalized { get; } = Array.AsReadOnly(Array.Empty<Point2>());
        public MortarFootprint(float aspectRatio)
            : this(aspectRatio, MaximumAspectRatio) { }
        public MortarFootprint(float aspectRatio, float maximumAspectRatio)
        {
            if (float.IsNaN(maximumAspectRatio) || float.IsInfinity(maximumAspectRatio) || maximumAspectRatio < 1 || maximumAspectRatio > StructuralMaximumAspectRatio)
                throw new ArgumentOutOfRangeException(nameof(maximumAspectRatio));
            if (float.IsNaN(aspectRatio) || float.IsInfinity(aspectRatio) || aspectRatio < 1 || aspectRatio > maximumAspectRatio)
                throw new ArgumentOutOfRangeException(nameof(aspectRatio));
            AspectRatio = aspectRatio;
        }
        public static MortarFootprint CreateStored(float aspectRatio) => new MortarFootprint(aspectRatio, StructuralMaximumAspectRatio);
        MortarFootprint(FootprintContourAssessment assessment)
        { AspectRatio=assessment.AspectRatio;Authored=assessment.Authored;Normalized=assessment.Normalized; }
        public static MortarFootprint FromContour(IReadOnlyList<Point2> points, float maximumAspect = MaximumAspectRatio)
        {
            var result=FootprintContour.Analyze(points,maximumAspect:maximumAspect);
            if(!result.Valid)throw new ArgumentException("Invalid footprint contour: "+result.Issue,nameof(points));
            return new MortarFootprint(result);
        }
        public bool SameShape(MortarFootprint other)
        {
            if(other==null||IsContour!=other.IsContour||AspectRatio!=other.AspectRatio||Authored.Count!=other.Authored.Count)return false;
            for(int i=0;i<Authored.Count;i++)if(Authored[i].X!=other.Authored[i].X||Authored[i].Y!=other.Authored[i].Y)return false;
            return true;
        }
    }
}
