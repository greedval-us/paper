using System;
using System.Collections.Generic;
using System.Linq;

namespace PaperDefense.Core
{
    /// <summary>Owns the chosen pages and numerical rules for one run.</summary>
    public sealed class RunLoadoutSnapshot
    {
        readonly GameBalance balance;
        public IReadOnlyList<AlbumEntry> Entries { get; }
        public IReadOnlyList<ResolvedEntry> Resolved { get; }
        public GameBalance Balance => balance.CreateSnapshot();

        public RunLoadoutSnapshot(IReadOnlyList<AlbumEntry> entries, GameBalance balance)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            if (entries.Count != ProfileLimits.LoadoutSlotCount)
                throw new ArgumentException("A run loadout requires exactly eight positions.", nameof(entries));

            this.balance = balance.CreateSnapshot();
            var owned = new AlbumEntry[ProfileLimits.LoadoutSlotCount];
            var resolved = new ResolvedEntry[ProfileLimits.LoadoutSlotCount];
            var identities = new HashSet<string>(StringComparer.Ordinal);
            bool canAttack = false;
            for (int slot = 0; slot < owned.Length; slot++)
            {
                var entry = entries[slot];
                if (entry == null) continue;
                if (!identities.Add(entry.Id)) throw new ArgumentException("A page cannot occupy multiple run loadout positions.", nameof(entries));
                // AlbumEntry and its recipe/legacy payloads are immutable owned value snapshots.
                owned[slot] = entry;
                resolved[slot] = Resolve(entry, slot, MaterialTier.Pencil);
                foreach (var weapon in resolved[slot].Weapons)
                    canAttack |= weapon.Damage > 0 && weapon.Attack != AttackKind.Cleanup && weapon.Family != AssemblyWeaponFamily.Blotter;
            }
            if (!canAttack) throw new ArgumentException("A run loadout requires an enemy-damaging weapon.", nameof(entries));
            Entries = Array.AsReadOnly(owned);
            Resolved = Array.AsReadOnly(resolved);
        }

        public ResolvedEntry ResolveSlot(int slot, MaterialTier material)
        {
            if (slot < 0 || slot >= ProfileLimits.LoadoutSlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!Enum.IsDefined(typeof(MaterialTier), material)) throw new ArgumentOutOfRangeException(nameof(material));
            var entry = Entries[slot];
            if (entry == null) return null;
            return material == MaterialTier.Pencil ? Resolved[slot] : Resolve(entry, slot, material);
        }

        ResolvedEntry Resolve(AlbumEntry entry, int slot, MaterialTier material)
        {
            var result = TowerCatalog.ResolveEntry(entry, balance, material);
            if (!result.Valid)
                throw new ArgumentException("Invalid run loadout page at slot " + slot + ": " +
                    string.Join(",", result.Diagnostics.Select(issue => issue.Code + ":" + issue.Path)));
            return result.Value;
        }
    }
}
