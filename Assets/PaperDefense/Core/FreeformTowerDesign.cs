using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>Versioned, immutable construction rules derived from functional strokes, never template accuracy.</summary>
    public sealed class FreeformTowerDesign
    {
        public const int RuleVersion = 2;
        readonly FreeformBarrel[] barrels = new FreeformBarrel[TowerBlueprint.MaximumWeaponMounts];
        public float BodyArea { get; }
        public IReadOnlyList<DrawingPoint> BodyOutline { get; }
        public float BodyGrowth { get; }
        public float HealthMultiplier { get; }
        public float CapacityMultiplier { get; }
        public float CostMultiplier { get; }
        public float BuildMultiplier { get; }
        public bool IsComplete { get; }
        public float InkUsed { get; }
        public float InkBudget { get; }
        public bool IsWithinBudget => InkUsed <= InkBudget;
        public bool IsUsable => IsComplete && IsWithinBudget;
        public string Message { get; }
        public FreeformBarrel GetBarrel(int slot)
        {
            if (slot < 0 || slot >= barrels.Length) throw new ArgumentOutOfRangeException(nameof(slot));
            return barrels[slot];
        }
        internal FreeformTowerDesign(TowerBlueprint blueprint, FreeformRules rules = null)
        {
            rules=rules??blueprint.Content?.FreeformRules??FreeformRules.Default;
            var geometry = FreeformGeometry.For(blueprint.Drawing, blueprint.FreeformRuleVersion);
            BodyArea = geometry.Part(DrawingBindings.Base).Area;
            BodyOutline = geometry.Part(DrawingBindings.Base).Outline ?? Array.Empty<DrawingPoint>();
            float size = Unit((float)Math.Sqrt(BodyArea / rules.BodyAreaReference));
            BodyGrowth = size;
            HealthMultiplier = rules.HealthBase + rules.HealthSize * size;
            CapacityMultiplier = rules.CapacityBase + rules.CapacitySize * size;
            bool complete = BodyArea >= rules.MinimumBodyArea;
            string message = complete ? "" : UserText.Message("freeform.missing_body");
            float barrelCost = 0;
            InkBudget=rules.BaseInkBudget+rules.ExtraMountInkBudget*(blueprint.WeaponMounts.Count-1);
            InkUsed=BodyArea*rules.BodyAreaInk;
            foreach (var mount in blueprint.WeaponMounts)
            {
                var shape = geometry.Part(DrawingBindings.Mount(mount.SlotIndex));
                var barrel = new FreeformBarrel(shape.Width, shape.Length, shape.MuzzleOffset,shape.Outline,rules);
                barrels[mount.SlotIndex] = barrel;
                barrelCost += rules.CostBarrelLength * Unit(barrel.Length / rules.BarrelLengthReference) + rules.CostBarrelWidth * Unit(barrel.Width / rules.BarrelWidthReference);
                InkUsed+=barrel.Length*rules.BarrelLengthInk+barrel.Width*rules.BarrelWidthInk;
                if (!barrel.IsComplete && complete)
                { complete = false; message = UserText.Message("freeform.missing_barrel", mount.SlotIndex + 1); }
            }
            CostMultiplier = rules.CostBase + rules.CostSize * size + barrelCost / blueprint.WeaponMounts.Count;
            BuildMultiplier = rules.BuildBase + rules.BuildSize * size;
            IsComplete = complete;
            Message = !complete ? message : !IsWithinBudget ? UserText.Message("freeform.over_budget",InkUsed,InkBudget) : UserText.Message("freeform.ready");
        }
        internal static float Unit(float value) => Math.Max(0, Math.Min(1, value));
    }

    public sealed class FreeformBarrel
    {
        public float Length { get; }
        public float Width { get; }
        public IReadOnlyList<DrawingPoint> WorkingOutline { get; }
        public float LengthGrowth { get; }
        public float WidthGrowth { get; }
        public float RangeMultiplier { get; }
        public float IntervalMultiplier { get; }
        public float DamageMultiplier { get; }
        public Point2 MuzzleOffset { get; }
        public bool IsComplete { get; }
        internal FreeformBarrel(float width, float length, Point2 muzzleOffset,IReadOnlyList<DrawingPoint> outline,FreeformRules rules)
        {
            Width = width; Length = length; MuzzleOffset = muzzleOffset;
            WorkingOutline = outline ?? Array.Empty<DrawingPoint>();
            IsComplete=length>=rules.MinimumBarrelLength&&muzzleOffset.Y>=rules.MinimumMuzzleForward;
            float reach = FreeformTowerDesign.Unit(length / rules.BarrelLengthReference), bore = FreeformTowerDesign.Unit(width / rules.BarrelWidthReference);
            LengthGrowth = reach; WidthGrowth = bore;
            RangeMultiplier = rules.RangeBase + rules.RangeLength * reach - rules.RangeWidthPenalty * bore;
            IntervalMultiplier = rules.IntervalBase + rules.IntervalLength * reach + rules.IntervalWidth * bore;
            DamageMultiplier = rules.DamageBase + rules.DamageWidth * bore;
        }
    }

}
