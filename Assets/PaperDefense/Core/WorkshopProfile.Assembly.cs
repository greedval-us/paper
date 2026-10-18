using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>Candidate mutations. Application validates assemblies and publishes only after persistence succeeds.</summary>
    public sealed partial class WorkshopProfile
    {
        public bool TryPurchaseAssemblyPart(ComponentDefinition part, out string code)
        {
            code = "";
            if (part == null) { code = "profile.part.missing"; return false; }
            if (unlocked.Contains(part.Id)) return true;
            if (MetaProgression.LevelForXp(TotalXp) < part.RequiredLevel) { code = "profile.part.level"; return false; }
            if (Gold < part.UnlockGold) { code = "profile.part.gold"; return false; }
            Gold -= part.UnlockGold;
            unlocked.Add(part.Id);
            return true;
        }

        public bool TrySaveAssemblyEntry(AlbumEntry input, bool asVariant, Guid operationId, out AlbumEntry saved, out string code)
        {
            saved = null; code = "";
            if (operationId == Guid.Empty) { code = "profile.operation.invalid"; return false; }
            if (input == null) { code = "profile.entry.missing"; return false; }
            if (input.Kind == AlbumEntryKind.Legacy) { code = "profile.entry.legacy_readonly"; return false; }
            string targetId = asVariant ? operationId.ToString("N") : input.Id;
            if (!ValidAssemblyEntryId(targetId)) { code = "profile.entry.identity"; return false; }
            int index = entries.FindIndex(e => e.Id == targetId);
            if (asVariant && index >= 0) { code = "profile.entry.conflict"; return false; }
            int revision = 1;
            if (index >= 0)
            {
                var previous = entries[index];
                if (previous.Kind == AlbumEntryKind.Legacy) { code = "profile.entry.legacy_readonly"; return false; }
                if (input.Revision != previous.Revision || previous.Revision == int.MaxValue) { code = "profile.entry.revision"; return false; }
                revision = previous.Revision + 1;
            }
            else if (entries.Count >= ProfileLimits.MaximumAlbumPages) { code = "profile.entry.full"; return false; }
            var next = new AlbumEntry(targetId, input.Name, revision, input.Snapshot, input.LibraryReferences);
            var nextEntries = new List<AlbumEntry>(entries);
            if (index < 0) nextEntries.Add(next); else nextEntries[index] = next;
            var projection = CreateAssemblyProjection(nextEntries);
            entries.Clear(); entries.AddRange(nextEntries);
            album.Clear(); album.AddRange(projection);
            saved = next;
            return true;
        }

        static bool ValidAssemblyEntryId(string id)
        {
            if (id == null || id.Length > ProfileLimits.MaximumBlueprintIdLength) return false;
            try { ContentValidation.Id(id, "entry.id"); return true; }
            catch (ArgumentException) { return false; }
        }

        static List<TowerBlueprint> CreateAssemblyProjection(IEnumerable<AlbumEntry> source)
        {
            var result = new List<TowerBlueprint>();
            foreach (var entry in source)
            {
                var blueprint = entry.Kind == AlbumEntryKind.Legacy ? entry.LegacyBlueprint : CompatibilityBlueprint(entry);
                if (blueprint != null) result.Add(blueprint);
            }
            return result;
        }

        public bool TrySaveLibraryComponent(ComponentLibraryEntry input, bool asVariant, Guid operationId, out ComponentLibraryEntry saved, out string code)
        {
            saved = null; code = "";
            if (operationId == Guid.Empty) { code = "profile.operation.invalid"; return false; }
            if (input == null) { code = "profile.library.missing"; return false; }
            string targetId = asVariant ? operationId.ToString("N") : input.Id;
            int index = library.FindIndex(e => e.Id == targetId);
            if (asVariant && index >= 0) { code = "profile.library.conflict"; return false; }
            int revision = 1;
            if (index >= 0)
            {
                var previous = library[index];
                if (input.Revision != previous.Revision || previous.Revision == int.MaxValue) { code = "profile.library.revision"; return false; }
                revision = previous.Revision + 1;
            }
            var next = new ComponentLibraryEntry(targetId, input.Name, revision, input.Snapshot);
            if (index < 0) library.Add(next); else library[index] = next;
            saved = next;
            return true;
        }

        public bool TryDeleteAssemblyEntry(string id, out string code)
        {
            code = ""; int index = entries.FindIndex(e => e.Id == id);
            if (index < 0) { code = "profile.entry.missing"; return false; }
            entries.RemoveAt(index);
            album.RemoveAll(blueprint => blueprint.Id == id);
            for (int slot = 0; slot < loadout.Length; slot++) if (loadout[slot] == id) loadout[slot] = null;
            return true;
        }

        public bool TryDeleteLibraryComponent(string id, out string code)
        {
            code = ""; int index = library.FindIndex(e => e.Id == id);
            if (index < 0) { code = "profile.library.missing"; return false; }
            library.RemoveAt(index);
            return true;
        }

        public bool TryAssignAssemblySlot(int slot, string id, out string code)
        {
            code = "";
            if (slot < 0 || slot >= loadout.Length) { code = "profile.slot.invalid"; return false; }
            if (id == null) { loadout[slot] = null; return true; }
            if (!entries.Exists(entry => entry.Id == id)) { code = "profile.entry.missing"; return false; }
            for (int previous = 0; previous < loadout.Length; previous++) if (loadout[previous] == id) loadout[previous] = null;
            loadout[slot] = id;
            return true;
        }

        public OperationReceiptData FindOperation(Guid id)
        {
            if (id == Guid.Empty) return null;
            foreach (var receipt in operations)
                if (Guid.TryParseExact(receipt.OperationId, "N", out var savedId) && savedId == id) return Copy(receipt);
            return null;
        }

        public void RecordOperation(Guid id, string type, string resultId)
        {
            if (id == Guid.Empty) throw new ArgumentException("An operation requires a nonempty GUID.", nameof(id));
            if (string.IsNullOrWhiteSpace(type) || type.Length > 64) throw new ArgumentException("Invalid operation type.", nameof(type));
            if (resultId != null) ContentValidation.Id(resultId, "operation.resultId");
            if (FindOperation(id) != null) throw new ArgumentException("An operation receipt already exists.", nameof(id));
            operations.Add(new OperationReceiptData { OperationId = id.ToString("N"), Type = type, ResultId = resultId });
        }
    }
}
