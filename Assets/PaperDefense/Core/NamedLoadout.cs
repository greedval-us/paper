using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>A named arrangement of album identities, not copies of the album's recipes.</summary>
    public sealed class NamedLoadout
    {
        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<string> Slots { get; }

        public NamedLoadout(string id, string name, IReadOnlyList<string> slots)
        {
            if (!ValidId(id)) throw new ArgumentException("profile.loadout.identity", nameof(id));
            if (!TryNormalizeName(name, out var normalized)) throw new ArgumentException("profile.loadout.name", nameof(name));
            if (slots == null || slots.Count != ProfileLimits.LoadoutSlotCount)
                throw new ArgumentException("profile.loadout.slots", nameof(slots));
            var owned = new string[ProfileLimits.LoadoutSlotCount];
            var identities = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < owned.Length; i++)
            {
                // JsonUtility serializes an optional null string as an empty string.
                string entry = string.IsNullOrEmpty(slots[i]) ? null : slots[i];
                if (entry != null)
                {
                    if (entry.Length > ProfileLimits.MaximumBlueprintIdLength || !identities.Add(entry))
                        throw new ArgumentException("profile.loadout.slots", nameof(slots));
                    ContentValidation.Id(entry, "namedLoadout.slots[" + i + "]");
                }
                owned[i] = entry;
            }
            Id = id; Name = normalized; Slots = Array.AsReadOnly(owned);
        }

        internal static bool ValidId(string id) => Guid.TryParseExact(id, "N", out var value) && value != Guid.Empty &&
            string.Equals(id, value.ToString("N"), StringComparison.Ordinal);

        internal static bool TryNormalizeName(string name, out string normalized)
        {
            normalized = null;
            if (name == null) return false;
            foreach (char character in name)
                if (char.IsControl(character) || character == '<' || character == '>') return false;
            normalized = name.Trim();
            return normalized.Length > 0 && normalized.Length <= ProfileLimits.MaximumLoadoutNameLength;
        }
    }

    [Serializable]
    public sealed class NamedLoadoutData
    {
        public int Version = 1;
        public string Id, Name;
        public string[] Slots;
        public NamedLoadoutData() { }
        public NamedLoadoutData(NamedLoadout source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Id = source.Id; Name = source.Name;
            Slots = new string[ProfileLimits.LoadoutSlotCount];
            for (int i = 0; i < Slots.Length; i++) Slots[i] = source.Slots[i];
        }
        public NamedLoadout CreateSnapshot()
        {
            AssemblyProfileDataCopy.Version(Version);
            return new NamedLoadout(Id, Name, Slots);
        }
    }
}
