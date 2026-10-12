using System;
namespace PaperDefense.Core
{
    public sealed class MortarFootprint
    {
        public const float MaximumAspectRatio = 3;
        // File/numeric protection; the editable weapon definition supplies the playable limit.
        public const float StructuralMaximumAspectRatio = 64;
        public static MortarFootprint Circle { get; } = new MortarFootprint(1);
        public float AspectRatio { get; }
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
    }
}
