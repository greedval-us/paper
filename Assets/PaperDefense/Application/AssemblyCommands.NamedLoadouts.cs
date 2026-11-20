using System;
using System.Collections.Generic;
using System.Linq;
using PaperDefense.Core;

namespace PaperDefense.Application
{
    public sealed partial class AssemblyCommands
    {
        public AssemblyCommandResult SaveNamedLoadout(string name, Guid operationId)
            => Execute("SaveNamedLoadout", operationId, candidate => {
                var valid = ValidateLoadoutSlots(candidate, candidate.Loadout);
                if (!valid.Success) return valid;
                string id = operationId.ToString("N");
                return candidate.TrySaveNamedLoadout(id, name, out var code)
                    ? AssemblyCommandResult.Ok(id) : AssemblyCommandResult.Fail(code);
            });

        public AssemblyCommandResult UpdateNamedLoadout(string id, Guid operationId)
            => Execute("UpdateNamedLoadout", operationId, candidate => {
                var valid = ValidateLoadoutSlots(candidate, candidate.Loadout);
                if (!valid.Success) return valid;
                return candidate.TryUpdateNamedLoadout(id, out var code)
                    ? AssemblyCommandResult.Ok(id) : AssemblyCommandResult.Fail(code);
            });

        public AssemblyCommandResult RenameNamedLoadout(string id, string name, Guid operationId)
            => Execute("RenameNamedLoadout", operationId, candidate =>
                candidate.TryRenameNamedLoadout(id, name, out var code)
                    ? AssemblyCommandResult.Ok(id) : AssemblyCommandResult.Fail(code));

        public AssemblyCommandResult DeleteNamedLoadout(string id, Guid operationId)
            => Execute("DeleteNamedLoadout", operationId, candidate =>
                candidate.TryDeleteNamedLoadout(id, out var code)
                    ? AssemblyCommandResult.Ok(id) : AssemblyCommandResult.Fail(code));

        public AssemblyCommandResult ApplyNamedLoadout(string id, Guid operationId)
            => Execute("ApplyNamedLoadout", operationId, candidate => {
                var valid = ValidateNamedLoadout(candidate, id);
                if (!valid.Success) return valid;
                // All eight references change together in one candidate and one durable commit.
                return candidate.TryApplyNamedLoadout(id, out var code)
                    ? AssemblyCommandResult.Ok(id) : AssemblyCommandResult.Fail(code);
            });

        /// <summary>Read-only apply check. Starting a battle retains its separate attacking-tower gate.</summary>
        public AssemblyCommandResult CheckNamedLoadout(string id)
        {
            if (!owner.CreateAssemblyCandidate(out var candidate, out var reason))
                return AssemblyCommandResult.Fail("profile.edit.unavailable", "profile", reason);
            try { return ValidateNamedLoadout(candidate, id); }
            catch (AssemblyCatalogException error)
            { return new AssemblyCommandResult(false, error.Diagnostics.FirstOrDefault()?.Code ?? "assembly.invalid", diagnostics: error.Diagnostics); }
            catch (ArgumentException error) { return AssemblyCommandResult.Fail("assembly.invalid", "entry", error.Message); }
            catch (NotSupportedException error) { return AssemblyCommandResult.Fail("assembly.unsupported", "entry", error.Message); }
        }

        AssemblyCommandResult ValidateNamedLoadout(WorkshopProfile candidate, string id)
        {
            var saved = candidate.NamedLoadouts.FirstOrDefault(value => value.Id == id);
            if (saved == null) return AssemblyCommandResult.Fail("profile.loadout.missing");
            var valid = ValidateLoadoutSlots(candidate, saved.Slots);
            return valid.Success ? AssemblyCommandResult.Ok(id) : valid;
        }

        AssemblyCommandResult ValidateLoadoutSlots(WorkshopProfile candidate, IReadOnlyList<string> slots)
        {
            if (slots == null || slots.Count != ProfileLimits.LoadoutSlotCount)
                return AssemblyCommandResult.Fail("profile.loadout.slots");
            var used = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < slots.Count; i++)
            {
                string id = slots[i];
                if (id == null) continue;
                if (!used.Add(id)) return AssemblyCommandResult.Fail("profile.loadout.slots");
                var entry = candidate.Entries.FirstOrDefault(value => value.Id == id);
                var valid = ValidateOwned(candidate, entry);
                if (!valid.Success) return valid;
            }
            return AssemblyCommandResult.Ok(null);
        }
    }
}
