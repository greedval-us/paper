using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public readonly struct PartRef
    {
        public string Id { get; }
        public int Revision { get; }
        public PartRef(string id, int revision)
        {
            if (string.IsNullOrWhiteSpace(id) || revision < 1) throw AssemblyCatalogException.At("assembly.part.identity", "part", id);
            Id = id; Revision = revision;
        }
    }
    public sealed class FoundationRecipe
    {
        public PartRef Frame { get; } public PartRef Hull { get; } public PartRef Armor { get; } public PartRef Feed { get; } public PartRef Turn { get; }
        public FoundationRecipe(PartRef frame, PartRef hull, PartRef armor, PartRef feed, PartRef turn)
        { Frame = frame; Hull = hull; Armor = armor; Feed = feed; Turn = turn; }
        public IEnumerable<PartRef> Parts { get { yield return Frame; yield return Hull; yield return Armor; yield return Feed; yield return Turn; } }
    }
    public sealed class WeaponRecipe
    {
        public PartRef Mechanism { get; } public PartRef Head { get; } public PartRef Sight { get; } public PartRef Magazine { get; } public PartRef Trigger { get; }
        public WeaponRecipe(PartRef mechanism, PartRef head, PartRef sight, PartRef magazine, PartRef trigger)
        { Mechanism = mechanism; Head = head; Sight = sight; Magazine = magazine; Trigger = trigger; }
        public IEnumerable<PartRef> Parts { get { yield return Mechanism; yield return Head; yield return Sight; yield return Magazine; yield return Trigger; } }
    }
    public sealed class WeaponMountRecipe
    {
        public int SlotIndex { get; }
        public WeaponRecipe Weapon { get; }
        public WeaponMountRecipe(int slotIndex, WeaponRecipe weapon)
        {
            if (slotIndex < 0 || slotIndex > 2) throw AssemblyCatalogException.At("assembly.recipe.mount_count", "mounts", slotIndex.ToString());
            SlotIndex = slotIndex; Weapon = weapon ?? throw new ArgumentNullException(nameof(weapon));
        }
    }
    public sealed class TowerRecipe
    {
        public FoundationRecipe Foundation { get; }
        public IReadOnlyList<WeaponMountRecipe> Mounts { get; }
        public TowerRecipe(FoundationRecipe foundation, IEnumerable<WeaponMountRecipe> mounts)
        {
            Foundation = foundation ?? throw new ArgumentNullException(nameof(foundation));
            var owned = new List<WeaponMountRecipe>(); var slots = new HashSet<int>();
            foreach (var mount in mounts ?? throw new ArgumentNullException(nameof(mounts)))
            {
                if (mount == null || !slots.Add(mount.SlotIndex)) throw AssemblyCatalogException.At("assembly.recipe.mount_duplicate", "mounts");
                owned.Add(new WeaponMountRecipe(mount.SlotIndex, mount.Weapon));
            }
            if (owned.Count == 0 || !slots.Contains(0)) throw AssemblyCatalogException.At("assembly.recipe.first_mount", "mounts");
            owned.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex)); Mounts = owned.AsReadOnly();
        }
    }
    public sealed class StructureRecipe
    {
        public AssemblyStructureFamily Family { get; }
        public PartRef Site { get; } public PartRef Function { get; } public PartRef Shell { get; } public PartRef Utility { get; }
        public StructureRecipe(AssemblyStructureFamily family, PartRef site, PartRef function, PartRef shell, PartRef utility)
        {
            if (family != AssemblyStructureFamily.Wall && family != AssemblyStructureFamily.Slow && family != AssemblyStructureFamily.Repair && family != AssemblyStructureFamily.Generator)
                throw AssemblyCatalogException.At("assembly.compatibility.unknown", "structure.family");
            Family = family; Site = site; Function = function; Shell = shell; Utility = utility;
        }
        public IEnumerable<PartRef> Parts { get { yield return Site; yield return Function; yield return Shell; yield return Utility; } }
    }
    public sealed class AssemblyRecipe
    {
        public TowerRecipe Tower { get; }
        public StructureRecipe Structure { get; }
        public AssemblyRecipe(TowerRecipe tower) { Tower = tower ?? throw new ArgumentNullException(nameof(tower)); }
        public AssemblyRecipe(StructureRecipe structure) { Structure = structure ?? throw new ArgumentNullException(nameof(structure)); }
        public IEnumerable<PartRef> Parts
        {
            get
            {
                if (Structure != null) { foreach (var p in Structure.Parts) yield return p; }
                else
                {
                    foreach (var p in Tower.Foundation.Parts) yield return p;
                    foreach (var m in Tower.Mounts) foreach (var p in m.Weapon.Parts) yield return p;
                }
            }
        }
    }

    [Serializable] public sealed class PartRefInput
    {
        public string Id; public int Revision = 1;
        public PartRef CreateSnapshot() => new PartRef(Id, Revision);
    }
    [Serializable] public sealed class FoundationRecipeInput
    {
        public PartRefInput Frame, Hull, Armor, Feed, Turn;
        public FoundationRecipe CreateSnapshot() => new FoundationRecipe(Frame.CreateSnapshot(), Hull.CreateSnapshot(), Armor.CreateSnapshot(), Feed.CreateSnapshot(), Turn.CreateSnapshot());
    }
    [Serializable] public sealed class WeaponRecipeInput
    {
        public PartRefInput Mechanism, Head, Sight, Magazine, Trigger;
        public WeaponRecipe CreateSnapshot() => new WeaponRecipe(Mechanism.CreateSnapshot(), Head.CreateSnapshot(), Sight.CreateSnapshot(), Magazine.CreateSnapshot(), Trigger.CreateSnapshot());
    }
    [Serializable] public sealed class WeaponMountRecipeInput
    {
        public int SlotIndex; public WeaponRecipeInput Weapon;
    }
    [Serializable] public sealed class StructureRecipeInput
    {
        public AssemblyStructureFamily Family;
        public PartRefInput Site, Function, Shell, Utility;
        public StructureRecipe CreateSnapshot() => new StructureRecipe(Family, Site.CreateSnapshot(), Function.CreateSnapshot(), Shell.CreateSnapshot(), Utility.CreateSnapshot());
    }
    [Serializable] public sealed class AssemblyStarterInput
    {
        public string Id, Name; public int Revision = 1;
        public AlbumEntryKind Kind;
        public FoundationRecipeInput Foundation;
        public WeaponMountRecipeInput[] Mounts = Array.Empty<WeaponMountRecipeInput>();
        public StructureRecipeInput Structure;
        public AssemblyRecipe CreateRecipe()
        {
            // Unity inline serialization materializes empty classes for null fields.
            // The tag chooses the payload; populated fields in the other variant are rejected.
            if (Kind == AlbumEntryKind.Structure)
            {
                if (Structure == null || HasPart(Foundation?.Frame, Foundation?.Hull, Foundation?.Armor, Foundation?.Feed, Foundation?.Turn) || (Mounts != null && Mounts.Length != 0))
                    throw AssemblyCatalogException.At("assembly.recipe.payload", Id);
                return new AssemblyRecipe(Structure.CreateSnapshot());
            }
            if (Kind != AlbumEntryKind.Tower || Foundation == null || (Structure != null && (Structure.Family != AssemblyStructureFamily.None || HasPart(Structure.Site, Structure.Function, Structure.Shell, Structure.Utility))))
                throw AssemblyCatalogException.At("assembly.recipe.payload", Id);
            var mounts = new List<WeaponMountRecipe>();
            foreach (var m in Mounts ?? throw new ArgumentNullException(nameof(Mounts))) mounts.Add(new WeaponMountRecipe(m.SlotIndex, m.Weapon.CreateSnapshot()));
            return new AssemblyRecipe(new TowerRecipe(Foundation.CreateSnapshot(), mounts));
        }
        static bool HasPart(params PartRefInput[] parts)
        {
            foreach (var part in parts) if (part != null && !string.IsNullOrEmpty(part.Id)) return true;
            return false;
        }
    }
}
