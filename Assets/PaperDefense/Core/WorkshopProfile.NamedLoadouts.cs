using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PaperDefense.Core
{
    public sealed partial class WorkshopProfile
    {
        readonly List<NamedLoadout> namedLoadouts = new List<NamedLoadout>();
        ReadOnlyCollection<NamedLoadout> namedLoadoutsView;
        public IReadOnlyList<NamedLoadout> NamedLoadouts => namedLoadoutsView ?? (namedLoadoutsView = namedLoadouts.AsReadOnly());

        public bool TrySaveNamedLoadout(string id, string name, out string code)
        {
            code = "";
            if (!NamedLoadout.ValidId(id) || namedLoadouts.Exists(item => item.Id == id))
            { code = "profile.loadout.identity"; return false; }
            if (!ValidateNamedLoadoutName(name, null, out var normalized, out code)) return false;
            if (namedLoadouts.Count >= ProfileLimits.MaximumNamedLoadouts) { code = "profile.loadout.full"; return false; }
            if (!CaptureNamedLoadout(id, normalized, loadout, out var captured, out code)) return false;
            namedLoadouts.Add(captured);
            return true;
        }

        public bool TryUpdateNamedLoadout(string id, out string code)
        {
            code = "";
            int index = namedLoadouts.FindIndex(item => item.Id == id);
            if (index < 0) { code = "profile.loadout.missing"; return false; }
            if (!CaptureNamedLoadout(id, namedLoadouts[index].Name, loadout, out var captured, out code)) return false;
            namedLoadouts[index] = captured;
            return true;
        }

        public bool TryRenameNamedLoadout(string id, string name, out string code)
        {
            code = "";
            int index = namedLoadouts.FindIndex(item => item.Id == id);
            if (index < 0) { code = "profile.loadout.missing"; return false; }
            if (!ValidateNamedLoadoutName(name, id, out var normalized, out code)) return false;
            var previous = namedLoadouts[index];
            namedLoadouts[index] = new NamedLoadout(previous.Id, normalized, previous.Slots);
            return true;
        }

        public bool TryDeleteNamedLoadout(string id, out string code)
        {
            code = "";
            int index = namedLoadouts.FindIndex(item => item.Id == id);
            if (index < 0) { code = "profile.loadout.missing"; return false; }
            namedLoadouts.RemoveAt(index);
            return true;
        }

        public bool TryApplyNamedLoadout(string id, out string code)
        {
            code = "";
            var selected = namedLoadouts.Find(item => item.Id == id);
            if (selected == null) { code = "profile.loadout.missing"; return false; }
            if (!ValidNamedLoadoutSlots(selected.Slots)) { code = "profile.loadout.slots"; return false; }
            // Validate every reference first, then publish the complete arrangement on this candidate.
            for (int i = 0; i < loadout.Length; i++) loadout[i] = selected.Slots[i];
            return true;
        }

        bool ValidateNamedLoadoutName(string name, string exceptId, out string normalized, out string code)
        {
            code = "";
            if (!NamedLoadout.TryNormalizeName(name, out normalized)) { code = "profile.loadout.name"; return false; }
            foreach (var item in namedLoadouts)
                if (item.Id != exceptId && string.Equals(item.Name, normalized, StringComparison.OrdinalIgnoreCase))
                { code = "profile.loadout.duplicate_name"; return false; }
            return true;
        }

        bool CaptureNamedLoadout(string id, string name, IReadOnlyList<string> slots, out NamedLoadout captured, out string code)
        {
            captured = null; code = "";
            if (!ValidNamedLoadoutSlots(slots)) { code = "profile.loadout.slots"; return false; }
            captured = new NamedLoadout(id, name, slots);
            return true;
        }

        bool ValidNamedLoadoutSlots(IReadOnlyList<string> slots)
        {
            if (slots == null || slots.Count != ProfileLimits.LoadoutSlotCount) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in slots)
                if (id != null && (!seen.Add(id) || !entries.Exists(entry => entry.Id == id))) return false;
            return true;
        }

        void ClearNamedLoadoutReferences(string entryId)
        {
            for (int i = 0; i < namedLoadouts.Count; i++)
            {
                var previous = namedLoadouts[i]; bool found = false;
                foreach (string id in previous.Slots) if (id == entryId) { found = true; break; }
                if (!found) continue;
                var slots = new string[ProfileLimits.LoadoutSlotCount];
                for (int slot = 0; slot < slots.Length; slot++)
                    slots[slot] = previous.Slots[slot] == entryId ? null : previous.Slots[slot];
                namedLoadouts[i] = new NamedLoadout(previous.Id, previous.Name, slots);
            }
        }

        void ReadNamedLoadoutData(WorkshopProfileData data)
        {
            if (data.Version < 10) return;
            if (data.NamedLoadouts == null || data.NamedLoadouts.Length > ProfileLimits.MaximumNamedLoadouts)
                throw new ArgumentException("Invalid named loadout collection.");
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var saved in data.NamedLoadouts)
            {
                if (saved == null) throw new ArgumentException("Missing named loadout.");
                var item = saved.CreateSnapshot();
                if (!identities.Add(item.Id) || !names.Add(item.Name) || !ValidNamedLoadoutSlots(item.Slots))
                    throw new ArgumentException("Invalid named loadout identity, name or album references.");
                namedLoadouts.Add(item);
            }
        }

        void WriteNamedLoadoutData(WorkshopProfileData data)
        {
            data.NamedLoadouts = new NamedLoadoutData[namedLoadouts.Count];
            for (int i = 0; i < namedLoadouts.Count; i++) data.NamedLoadouts[i] = new NamedLoadoutData(namedLoadouts[i]);
        }
        void CopyNamedLoadoutsTo(WorkshopProfile copy)
        {
            // NamedLoadout owns its array and is immutable, just like AlbumEntry.
            copy.namedLoadouts.Clear(); copy.namedLoadouts.AddRange(namedLoadouts);
        }
    }
}
