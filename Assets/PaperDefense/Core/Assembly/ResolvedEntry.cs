using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public sealed class AssemblyResolution
    {
        public bool Valid => Value != null;
        public ResolvedEntry Value { get; }
        public IReadOnlyList<AssemblyDiagnostic> Diagnostics { get; }
        AssemblyResolution(ResolvedEntry value, IEnumerable<AssemblyDiagnostic> diagnostics)
        { Value = value; Diagnostics = new List<AssemblyDiagnostic>(diagnostics).AsReadOnly(); }
        internal static AssemblyResolution Success(ResolvedEntry value) => new AssemblyResolution(value, Array.Empty<AssemblyDiagnostic>());
        internal static AssemblyResolution Failure(IEnumerable<AssemblyDiagnostic> diagnostics) => new AssemblyResolution(null, diagnostics);
    }

    public sealed class ResolvedSupport
    {
        public AssemblyStructureFamily Kind { get; }
        public float Radius { get; } public float RepairPerSecond { get; }
        public float SlowSpeedMultiplier { get; } public float SlowSeconds { get; }
        public int IncomePerWave { get; } public float RefundFraction { get; } public int MaximumInstances { get; }
        public IReadOnlyList<MaterialTier> AllowedMaterials { get; }
        internal ResolvedSupport(AssemblyStructureFamily kind, float radius, float repairPerSecond, float slow, float slowSeconds, int income, float refund)
        {
            Kind = kind; Radius = radius; RepairPerSecond = repairPerSecond; SlowSpeedMultiplier = slow; SlowSeconds = slowSeconds;
            IncomePerWave = income; RefundFraction = kind == AssemblyStructureFamily.Generator ? 0 : refund;
            MaximumInstances = kind == AssemblyStructureFamily.Generator ? 2 : 0;
            AllowedMaterials = Array.AsReadOnly(kind == AssemblyStructureFamily.Generator ? new[] { MaterialTier.Pencil } : new[] { MaterialTier.Pencil, MaterialTier.Ink, MaterialTier.Marker });
        }
        public int PaybackWaves(int cost)
        {
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            if (IncomePerWave == 0) return 0;
            return (int)(((long)cost + IncomePerWave - 1) / IncomePerWave);
        }
    }

    public sealed class ResolvedEntry
    {
        public AlbumEntryKind Kind { get; } public MaterialTier Material { get; }
        public RecipeSnapshot Snapshot { get; }
        public int Cost { get; } public float Health { get; } public int Load { get; } public int Capacity { get; }
        public float BuildSeconds { get; } public float Defense { get; } public float FrontDefense { get; }
        public float FrontDefenseDegrees => FrontDefense > 0 ? 90 : 0;
        public float EffectiveHealth => AssemblyValidation.Positive(Health / (1d - Defense), "entry.effectiveHealth");
        public float FeedCapacity { get; } public float FeedModifier { get; }
        public float FeedMultiplier => Weapons.Count == 0 ? 0 : FeedForActiveMounts(Weapons.Count);
        public float RefundFraction { get; }
        public IReadOnlyList<ResolvedWeapon> Weapons { get; }
        public ResolvedSupport Support { get; }
        public float Dps
        {
            get
            {
                double sum = 0; float feed = FeedMultiplier;
                foreach (var weapon in Weapons) sum += weapon.DpsAtFeed(feed);
                return AssemblyValidation.NonNegative(sum, "entry.dps");
            }
        }
        internal ResolvedEntry(AlbumEntryKind kind, MaterialTier material, RecipeSnapshot snapshot, int cost, float health,
            int load, int capacity, float buildSeconds, float defense, float frontDefense, float feedCapacity,
            float feedModifier, float refundFraction, IEnumerable<ResolvedWeapon> weapons, ResolvedSupport support = null)
        {
            Kind = kind; Material = material; Snapshot = snapshot; Cost = cost; Health = health; Load = load; Capacity = capacity;
            BuildSeconds = buildSeconds; Defense = defense; FrontDefense = frontDefense; FeedCapacity = feedCapacity;
            FeedModifier = feedModifier; RefundFraction = refundFraction;
            Weapons = new List<ResolvedWeapon>(weapons).AsReadOnly(); Support = support;
        }
        public float FeedForActiveMounts(int activeMounts)
        {
            if (activeMounts < 0 || activeMounts > Weapons.Count) throw new ArgumentOutOfRangeException(nameof(activeMounts));
            if (activeMounts == 0) return 0;
            // Legacy weapon snapshots already contain the exact effective cooldown from the old path.
            if (Kind == AlbumEntryKind.Legacy) return 1;
            return AssemblyValidation.Positive(Math.Min(1.25, Math.Min(FeedModifier, FeedCapacity * (double)FeedModifier / activeMounts)), "entry.feed");
        }
    }
}
