using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PaperDefense.Core
{
    /// <summary>Calculates modern support effects without publishing changes to simulation state.</summary>
    public static class StructureSupportRules
    {
        public static IReadOnlyDictionary<StructureState, float> RepairRates(IReadOnlyList<StructureState> structures)
        {
            if (structures == null) throw new ArgumentNullException(nameof(structures));
            var rates = new Dictionary<StructureState, float>();
            foreach (var source in structures)
            {
                var support = ActiveSupport(source, AssemblyStructureFamily.Repair);
                if (support == null || support.RepairPerSecond <= 0) continue;

                StructureState nearest = null;
                double nearestDistance = support.Radius * (double)support.Radius;
                foreach (var target in structures)
                {
                    if (!Active(target) || ReferenceEquals(target, source) || target.Id == source.Id ||
                        !(target.Health < target.MaxHealth) || target.Kind == StructureKind.RepairPost ||
                        target.ResolvedEntry?.Support?.Kind == AssemblyStructureFamily.Repair) continue;
                    double dx = (double)source.Cell.X - target.Cell.X;
                    double dy = (double)source.Cell.Y - target.Cell.Y;
                    double distance = dx * dx + dy * dy;
                    if (distance > nearestDistance) continue;
                    if (nearest != null && distance == nearestDistance && target.Id >= nearest.Id) continue;
                    nearest = target;
                    nearestDistance = distance;
                }

                if (nearest == null) continue;
                rates.TryGetValue(nearest, out float current);
                rates[nearest] = Math.Max(current, support.RepairPerSecond);
            }
            return new ReadOnlyDictionary<StructureState, float>(rates);
        }

        public static float RepairedHealth(StructureState target, float rate, float delta)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (!NonNegative(rate)) throw new ArgumentOutOfRangeException(nameof(rate));
            if (!NonNegative(delta)) throw new ArgumentOutOfRangeException(nameof(delta));
            return (float)Math.Min(target.MaxHealth, target.Health + rate * (double)delta);
        }

        public static bool TryGetSlow(StructureState source, Point2 position, out ColorEffectPayload effect)
        {
            effect = default;
            var support = ActiveSupport(source, AssemblyStructureFamily.Slow);
            if (support == null) return false;
            double dx = source.Cell.X + .5d - position.X;
            double dy = source.Cell.Y + .5d - position.Y;
            if (!(dx * dx + dy * dy <= support.Radius * (double)support.Radius)) return false;
            effect = ColorEffectPayload.Slow(Math.Max(.4f, support.SlowSpeedMultiplier), support.SlowSeconds);
            return true;
        }

        static ResolvedSupport ActiveSupport(StructureState source, AssemblyStructureFamily family)
        {
            if (!Active(source) || source.ResolvedEntry?.Kind != AlbumEntryKind.Structure) return null;
            var support = source.ResolvedEntry.Support;
            return support?.Kind == family ? support : null;
        }

        static bool Active(StructureState structure)
            => structure != null && structure.IsComplete && structure.BuildRemaining <= 0 && structure.Health > 0;

        static bool NonNegative(float value) => value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
