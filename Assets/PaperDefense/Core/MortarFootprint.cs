using System;
namespace PaperDefense.Core
{
    public sealed class MortarFootprint
    {
        public const float MaximumAspectRatio = 3;
        public static MortarFootprint Circle { get; } = new MortarFootprint(1);
        public float AspectRatio { get; }
        public MortarFootprint(float aspectRatio)
        {
            if (float.IsNaN(aspectRatio) || float.IsInfinity(aspectRatio) || aspectRatio < 1 || aspectRatio > MaximumAspectRatio)
                throw new ArgumentOutOfRangeException(nameof(aspectRatio));
            AspectRatio = aspectRatio;
        }
    }
}
