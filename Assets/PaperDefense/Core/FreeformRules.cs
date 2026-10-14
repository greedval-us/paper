using System;

namespace PaperDefense.Core
{
    /// <summary>Authoring coefficients; geometry interpretation remains separately versioned by FreeformTowerDesign.RuleVersion.</summary>
    [Serializable] public sealed class FreeformRulesInput
    {
        public float BodyAreaReference = .8f;
        public float MinimumBodyArea = .06f;
        public float BarrelLengthReference = .9f;
        public float BarrelWidthReference = .5f;
        public float MinimumBarrelLength = .12f;
        public float MinimumMuzzleForward = .1f;
        public float HealthBase = .6f;
        public float HealthSize = 1.2f;
        public float CapacityBase = .6f;
        public float CapacitySize = .8f;
        public float CostBase = .75f;
        public float CostSize = .45f;
        public float CostBarrelLength = .12f;
        public float CostBarrelWidth = .15f;
        public float BuildBase = .8f;
        public float BuildSize = .4f;
        public float RangeBase = .65f;
        public float RangeLength = .75f;
        public float RangeWidthPenalty = .2f;
        public float IntervalBase = .75f;
        public float IntervalLength = .55f;
        public float IntervalWidth = .35f;
        public float DamageBase = .75f;
        public float DamageWidth = .75f;
        public float BaseInkBudget = 65;
        public float ExtraMountInkBudget = 35;
        public float BodyAreaInk = 50;
        public float BarrelLengthInk = 25;
        public float BarrelWidthInk = 35;
        public FreeformRules CreateSnapshot() => new FreeformRules(this);
        public FreeformRulesInput CreateCopy() => (FreeformRulesInput)MemberwiseClone();
    }
    public sealed class FreeformRules
    {
        public static FreeformRules Default { get; } = new FreeformRulesInput().CreateSnapshot();
        public float BodyAreaReference { get; }
        public float MinimumBodyArea { get; }
        public float BarrelLengthReference { get; }
        public float BarrelWidthReference { get; }
        public float MinimumBarrelLength { get; }
        public float MinimumMuzzleForward { get; }
        public float HealthBase { get; }
        public float HealthSize { get; }
        public float CapacityBase { get; }
        public float CapacitySize { get; }
        public float CostBase { get; }
        public float CostSize { get; }
        public float CostBarrelLength { get; }
        public float CostBarrelWidth { get; }
        public float BuildBase { get; }
        public float BuildSize { get; }
        public float RangeBase { get; }
        public float RangeLength { get; }
        public float RangeWidthPenalty { get; }
        public float IntervalBase { get; }
        public float IntervalLength { get; }
        public float IntervalWidth { get; }
        public float DamageBase { get; }
        public float DamageWidth { get; }
        public float BaseInkBudget { get; }
        public float ExtraMountInkBudget { get; }
        public float BodyAreaInk { get; }
        public float BarrelLengthInk { get; }
        public float BarrelWidthInk { get; }
        internal FreeformRules(FreeformRulesInput input)
        {
            if(input==null)throw new ArgumentNullException(nameof(input));
            ContentValidation.Positive(input.BodyAreaReference,"freeform.BodyAreaReference"); BodyAreaReference=input.BodyAreaReference;
            ContentValidation.Positive(input.MinimumBodyArea,"freeform.MinimumBodyArea"); MinimumBodyArea=input.MinimumBodyArea;
            ContentValidation.Positive(input.BarrelLengthReference,"freeform.BarrelLengthReference"); BarrelLengthReference=input.BarrelLengthReference;
            ContentValidation.Positive(input.BarrelWidthReference,"freeform.BarrelWidthReference"); BarrelWidthReference=input.BarrelWidthReference;
            ContentValidation.Positive(input.MinimumBarrelLength,"freeform.MinimumBarrelLength"); MinimumBarrelLength=input.MinimumBarrelLength;
            ContentValidation.Positive(input.MinimumMuzzleForward,"freeform.MinimumMuzzleForward"); MinimumMuzzleForward=input.MinimumMuzzleForward;
            ContentValidation.Positive(input.HealthBase,"freeform.HealthBase"); HealthBase=input.HealthBase;
            ContentValidation.NonNegative(input.HealthSize,"freeform.HealthSize"); HealthSize=input.HealthSize;
            ContentValidation.Positive(input.CapacityBase,"freeform.CapacityBase"); CapacityBase=input.CapacityBase;
            ContentValidation.NonNegative(input.CapacitySize,"freeform.CapacitySize"); CapacitySize=input.CapacitySize;
            ContentValidation.Positive(input.CostBase,"freeform.CostBase"); CostBase=input.CostBase;
            ContentValidation.NonNegative(input.CostSize,"freeform.CostSize"); CostSize=input.CostSize;
            ContentValidation.NonNegative(input.CostBarrelLength,"freeform.CostBarrelLength"); CostBarrelLength=input.CostBarrelLength;
            ContentValidation.NonNegative(input.CostBarrelWidth,"freeform.CostBarrelWidth"); CostBarrelWidth=input.CostBarrelWidth;
            ContentValidation.Positive(input.BuildBase,"freeform.BuildBase"); BuildBase=input.BuildBase;
            ContentValidation.NonNegative(input.BuildSize,"freeform.BuildSize"); BuildSize=input.BuildSize;
            ContentValidation.Positive(input.RangeBase,"freeform.RangeBase"); RangeBase=input.RangeBase;
            ContentValidation.NonNegative(input.RangeLength,"freeform.RangeLength"); RangeLength=input.RangeLength;
            ContentValidation.NonNegative(input.RangeWidthPenalty,"freeform.RangeWidthPenalty"); RangeWidthPenalty=input.RangeWidthPenalty;
            ContentValidation.Positive(input.IntervalBase,"freeform.IntervalBase"); IntervalBase=input.IntervalBase;
            ContentValidation.NonNegative(input.IntervalLength,"freeform.IntervalLength"); IntervalLength=input.IntervalLength;
            ContentValidation.NonNegative(input.IntervalWidth,"freeform.IntervalWidth"); IntervalWidth=input.IntervalWidth;
            ContentValidation.Positive(input.DamageBase,"freeform.DamageBase"); DamageBase=input.DamageBase;
            ContentValidation.NonNegative(input.DamageWidth,"freeform.DamageWidth"); DamageWidth=input.DamageWidth;
            ContentValidation.Positive(input.BaseInkBudget,"freeform.BaseInkBudget"); BaseInkBudget=input.BaseInkBudget;
            ContentValidation.NonNegative(input.ExtraMountInkBudget,"freeform.ExtraMountInkBudget"); ExtraMountInkBudget=input.ExtraMountInkBudget;
            ContentValidation.NonNegative(input.BodyAreaInk,"freeform.BodyAreaInk"); BodyAreaInk=input.BodyAreaInk;
            ContentValidation.NonNegative(input.BarrelLengthInk,"freeform.BarrelLengthInk"); BarrelLengthInk=input.BarrelLengthInk;
            ContentValidation.NonNegative(input.BarrelWidthInk,"freeform.BarrelWidthInk"); BarrelWidthInk=input.BarrelWidthInk;
            if(RangeBase<=RangeWidthPenalty)throw new ArgumentException("freeform: range width penalty must leave positive range");
            if(MinimumBodyArea>4 || MinimumBarrelLength>2 || MinimumMuzzleForward>1)throw new ArgumentException("freeform: minimum geometry exceeds drawing bounds");
            // Protect arithmetic as well as individual scalars from invalid authoring.
            ContentValidation.Finite(BaseInkBudget+2*ExtraMountInkBudget,"freeform.totalBudget");
            ContentValidation.Finite(4*BodyAreaInk+6*BarrelLengthInk+6*BarrelWidthInk,"freeform.totalInk");
            ContentValidation.Finite(HealthBase+HealthSize+CapacityBase+CapacitySize+CostBase+CostSize+CostBarrelLength+CostBarrelWidth+BuildBase+BuildSize+RangeBase+RangeLength+IntervalBase+IntervalLength+IntervalWidth+DamageBase+DamageWidth,"freeform.totalCoefficients");
        }
    }
}

