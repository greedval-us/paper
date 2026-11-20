using System.Collections.Generic;
using System.Linq;

namespace PaperDefense.Core
{
    public sealed partial class WorkshopProfile
    {
        // Reusable starters have their own stable identities, independent of the completed album pages.
        void EnsureStarterLibraryComponents(ComponentCatalog catalog)
        {
            if (catalog == null) return;
            AlbumEntry gun = null, piercer = null, mortar = null;
            foreach (var entry in catalog.Starters)
            {
                var tower = entry.Snapshot?.Recipe.Tower;
                if (tower == null) continue;
                foreach (var mount in tower.Mounts)
                {
                    if (mount.SlotIndex != 0) continue;
                    switch (mount.Weapon.Mechanism.Id)
                    {
                        case "core.gun": if (gun == null) gun = entry; break;
                        case "core.piercer": if (piercer == null) piercer = entry; break;
                        case "core.mortar": if (mortar == null) mortar = entry; break;
                    }
                }
            }
            if (gun != null)
            {
                var foundation = gun.Snapshot.Recipe.Tower.Foundation;
                AddStarterLibraryComponent("library.foundation.standard", "Стандартная основа",
                    new ComponentRecipeSnapshot(foundation, gun.Snapshot.BalanceRevision,
                        SelectedStarterDefinitions(gun.Snapshot, foundation.Parts)));
            }
            AddStarterWeaponComponent("library.weapon.gun", "Стандартный пулемёт", gun);
            AddStarterWeaponComponent("library.weapon.piercer", "Стандартное пробивное", piercer);
            AddStarterWeaponComponent("library.weapon.mortar", "Стандартная мортира", mortar);
        }

        void AddStarterWeaponComponent(string id, string name, AlbumEntry source)
        {
            if (source == null) return;
            var weapon = source.Snapshot.Recipe.Tower.Mounts.First(m => m.SlotIndex == 0).Weapon;
            AddStarterLibraryComponent(id, name, new ComponentRecipeSnapshot(weapon, source.Snapshot.BalanceRevision,
                SelectedStarterDefinitions(source.Snapshot, weapon.Parts)));
        }

        static IEnumerable<ComponentDefinition> SelectedStarterDefinitions(RecipeSnapshot source, IEnumerable<PartRef> parts)
        {
            var selected = new HashSet<(string, int)>(parts.Select(p => (p.Id, p.Revision)));
            return source.Definitions.Where(d => selected.Contains((d.Id, d.Revision)));
        }

        void AddStarterLibraryComponent(string id, string name, ComponentRecipeSnapshot snapshot)
        {
            // A saved standard may be renamed or revised by its owner. Never replace that record.
            if (!library.Any(entry => entry.Id == id)) library.Add(new ComponentLibraryEntry(id, name, 1, snapshot));
            foreach (var definition in snapshot.Definitions) unlocked.Add(definition.Id);
        }
    }
}
