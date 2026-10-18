using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>One run's current wave eligibility. Retired waves cannot reopen, so no payout history accumulates.</summary>
    public sealed class GeneratorWaveLedger
    {
        readonly struct GeneratorAtStart
        {
            public StructureState Entity { get; }
            public int Income { get; }
            public GeneratorAtStart(StructureState entity, int income) { Entity = entity; Income = income; }
        }

        Guid boundRun;
        int latestWave;
        bool completed;
        Dictionary<int, GeneratorAtStart> eligible = new Dictionary<int, GeneratorAtStart>();

        public void BeginWave(Guid runId, int waveIndex, IReadOnlyList<StructureState> structures)
        {
            Validate(runId, waveIndex, structures);
            if ((boundRun != Guid.Empty && boundRun != runId) || waveIndex <= latestWave) return;

            var snapshot = new Dictionary<int, GeneratorAtStart>();
            foreach (var structure in structures)
            {
                if (!ActiveGenerator(structure) || snapshot.ContainsKey(structure.Id)) continue;
                snapshot.Add(structure.Id, new GeneratorAtStart(structure, structure.ResolvedEntry.Support.IncomePerWave));
            }
            boundRun = runId; latestWave = waveIndex; completed = false; eligible = snapshot;
        }

        public int CompleteWave(Guid runId, int waveIndex, IReadOnlyList<StructureState> structures)
        {
            Validate(runId, waveIndex, structures);
            if (runId != boundRun || waveIndex != latestWave || completed) return 0;

            long income = 0;
            var paid = new HashSet<int>();
            foreach (var structure in structures)
            {
                if (!ActiveGenerator(structure) || paid.Contains(structure.Id) ||
                    !eligible.TryGetValue(structure.Id, out var original) || !ReferenceEquals(original.Entity, structure)) continue;
                paid.Add(structure.Id);
                if (structure.GeneratorPaidWaves < int.MaxValue) structure.GeneratorPaidWaves++;
                income = Math.Min(int.MaxValue, income + original.Income);
            }
            completed = true; eligible.Clear();
            return (int)income;
        }

        static bool ActiveGenerator(StructureState structure)
            => structure != null && structure.Id > 0 && structure.IsComplete &&
                structure.BuildRemaining <= 0 && !float.IsInfinity(structure.BuildRemaining) &&
                structure.Health > 0 && !float.IsInfinity(structure.Health) &&
                structure.ResolvedEntry?.Kind == AlbumEntryKind.Structure &&
                structure.ResolvedEntry.Support?.Kind == AssemblyStructureFamily.Generator;

        static void Validate(Guid runId, int waveIndex, IReadOnlyList<StructureState> structures)
        {
            if (runId == Guid.Empty) throw new ArgumentException("A generator wave requires a run identity.", nameof(runId));
            if (waveIndex <= 0) throw new ArgumentOutOfRangeException(nameof(waveIndex));
            if (structures == null) throw new ArgumentNullException(nameof(structures));
        }
    }
}
