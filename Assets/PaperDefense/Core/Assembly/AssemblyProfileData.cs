using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    [Serializable] public sealed class AssemblyRecipeData
    {
        public AlbumEntryKind Kind;
        public FoundationRecipeInput Foundation;
        public WeaponMountRecipeInput[] Mounts;
        public StructureRecipeInput Structure;
        public AssemblyRecipeData() { }
        public AssemblyRecipeData(AssemblyRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            Kind = recipe.Tower != null ? AlbumEntryKind.Tower : AlbumEntryKind.Structure;
            if (recipe.Tower != null)
            {
                Foundation = AssemblyProfileDataCopy.Foundation(recipe.Tower.Foundation);
                Mounts = new WeaponMountRecipeInput[recipe.Tower.Mounts.Count];
                for (int i = 0; i < Mounts.Length; i++) Mounts[i] = new WeaponMountRecipeInput
                    { SlotIndex = recipe.Tower.Mounts[i].SlotIndex, Weapon = AssemblyProfileDataCopy.Weapon(recipe.Tower.Mounts[i].Weapon) };
            }
            else
            {
                var structure = recipe.Structure;
                Structure = new StructureRecipeInput { Family = structure.Family, Site = AssemblyProfileDataCopy.Part(structure.Site),
                    Function = AssemblyProfileDataCopy.Part(structure.Function), Shell = AssemblyProfileDataCopy.Part(structure.Shell), Utility = AssemblyProfileDataCopy.Part(structure.Utility) };
                Mounts = Array.Empty<WeaponMountRecipeInput>();
            }
        }
        public AssemblyRecipe CreateSnapshot()
        {
            AssemblyProfileDataCopy.GuardFuture(this);
            AssemblyProfileDataCopy.EntryKind(Kind);
            if (Kind == AlbumEntryKind.Tower)
            {
                if (AssemblyProfileDataCopy.HasStructure(Structure)) throw new ArgumentException("Unexpected structure recipe payload.");
                var foundation = AssemblyProfileDataCopy.ReadFoundation(Foundation);
                if (Mounts == null || Mounts.Length < 1 || Mounts.Length > TowerBlueprint.MaximumWeaponMounts)
                    throw new ArgumentException("Missing or invalid weapon mounts.");
                var mounts = new List<WeaponMountRecipe>();
                foreach (var mount in Mounts)
                {
                    if (mount == null) throw new ArgumentException("Missing weapon mount.");
                    mounts.Add(new WeaponMountRecipe(mount.SlotIndex, AssemblyProfileDataCopy.ReadWeapon(mount.Weapon)));
                }
                return new AssemblyRecipe(new TowerRecipe(foundation, mounts));
            }
            if (Kind != AlbumEntryKind.Structure || Structure == null || AssemblyProfileDataCopy.HasFoundation(Foundation) || (Mounts != null && Mounts.Length != 0))
                throw new ArgumentException("Invalid assembly recipe payload.");
            if (!Enum.IsDefined(typeof(AssemblyStructureFamily), Structure.Family) || Structure.Family == AssemblyStructureFamily.All)
                throw new NotSupportedException("Unknown structure family.");
            return new AssemblyRecipe(new StructureRecipe(Structure.Family,
                AssemblyProfileDataCopy.ReadPart(Structure.Site), AssemblyProfileDataCopy.ReadPart(Structure.Function),
                AssemblyProfileDataCopy.ReadPart(Structure.Shell), AssemblyProfileDataCopy.ReadPart(Structure.Utility)));
        }
    }

    [Serializable] public sealed class RecipeSnapshotData
    {
        public int Version = 1;
        public string BalanceRevision;
        public AssemblyRecipeData Recipe;
        public ComponentDefinitionInput[] Definitions;
        public RecipeSnapshotData() { }
        public RecipeSnapshotData(RecipeSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            BalanceRevision = snapshot.BalanceRevision; Recipe = new AssemblyRecipeData(snapshot.Recipe);
            Definitions = AssemblyProfileDataCopy.Definitions(snapshot.Definitions);
        }
        public RecipeSnapshot CreateSnapshot()
        {
            AssemblyProfileDataCopy.GuardFuture(this);
            AssemblyProfileDataCopy.Version(Version);
            if (Recipe == null) throw new ArgumentException("Missing saved recipe.");
            var recipe = Recipe.CreateSnapshot(); var definitions = AssemblyProfileDataCopy.ReadDefinitions(Definitions);
            AssemblyProfileDataCopy.RequireDefinitions(recipe.Parts, definitions);
            return new RecipeSnapshot(recipe, BalanceRevision, definitions);
        }
    }

    [Serializable] public sealed class ComponentLibraryReferenceData
    {
        public string Id;
        public int Revision = 1;
        public ComponentLibraryKind Kind;
        public int SlotIndex = -1;
        public ComponentLibraryReferenceData() { }
        public ComponentLibraryReferenceData(ComponentLibraryReference reference)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            Id = reference.Id; Revision = reference.Revision; Kind = reference.Kind; SlotIndex = reference.SlotIndex;
        }
        public ComponentLibraryReference CreateSnapshot()
        {
            AssemblyProfileDataCopy.ComponentKind(Kind);
            return new ComponentLibraryReference(Id, Revision, Kind, SlotIndex);
        }
    }

    [Serializable] public sealed class AlbumEntryData
    {
        public int Version = 1;
        public string Id, Name;
        public int Revision = 1;
        public AlbumEntryKind Kind;
        public RecipeSnapshotData Snapshot;
        public BlueprintData LegacyBlueprint;
        public ComponentLibraryReferenceData[] LibraryReferences;
        public AlbumEntryData() { }
        public AlbumEntryData(AlbumEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            Id = entry.Id; Name = entry.Name; Revision = entry.Revision; Kind = entry.Kind;
            if (entry.Kind == AlbumEntryKind.Legacy) LegacyBlueprint = new BlueprintData(entry.LegacyBlueprint);
            else Snapshot = new RecipeSnapshotData(entry.Snapshot);
            LibraryReferences = new ComponentLibraryReferenceData[entry.LibraryReferences.Count];
            for (int i = 0; i < LibraryReferences.Length; i++) LibraryReferences[i] = new ComponentLibraryReferenceData(entry.LibraryReferences[i]);
        }
        public AlbumEntry CreateSnapshot()
        {
            AssemblyProfileDataCopy.GuardFuture(this);
            AssemblyProfileDataCopy.Version(Version); AssemblyProfileDataCopy.EntryKind(Kind);
            var references = new List<ComponentLibraryReference>();
            foreach (var reference in LibraryReferences ?? Array.Empty<ComponentLibraryReferenceData>())
            {
                if (reference == null) throw new ArgumentException("Missing library reference.");
                references.Add(reference.CreateSnapshot());
            }
            if (Kind == AlbumEntryKind.Legacy)
            {
                if (LegacyBlueprint == null || AssemblyProfileDataCopy.HasSnapshot(Snapshot) || references.Count != 0)
                    throw new ArgumentException("Invalid legacy entry payload.");
                var blueprint = LegacyBlueprint.ToBlueprint(7);
                if (blueprint.Id != Id || blueprint.Name != Name) throw new ArgumentException("Legacy entry identity does not match its blueprint.");
                if (!TowerCatalog.ValidateParts(blueprint, out var reason) || (blueprint.HasPlayerDrawing && blueprint.Drawing.IsEmpty))
                    throw new ArgumentException("Invalid saved legacy blueprint: " + reason);
                return new AlbumEntry(Id, Name, Revision, blueprint);
            }
            if (Snapshot == null || AssemblyProfileDataCopy.HasBlueprint(LegacyBlueprint))
                throw new ArgumentException("Invalid assembly entry payload.");
            var snapshot = Snapshot.CreateSnapshot();
            if ((snapshot.Recipe.Tower != null ? AlbumEntryKind.Tower : AlbumEntryKind.Structure) != Kind)
                throw new ArgumentException("Entry kind does not match its recipe.");
            return new AlbumEntry(Id, Name, Revision, snapshot, references);
        }
    }

    [Serializable] public sealed class ComponentRecipeSnapshotData
    {
        public int Version = 1;
        public ComponentLibraryKind Kind;
        public FoundationRecipeInput Foundation;
        public WeaponRecipeInput Weapon;
        public string BalanceRevision;
        public ComponentDefinitionInput[] Definitions;
        public ComponentRecipeSnapshotData() { }
        public ComponentRecipeSnapshotData(ComponentRecipeSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            Kind = snapshot.Kind; BalanceRevision = snapshot.BalanceRevision;
            if (snapshot.Foundation != null) Foundation = AssemblyProfileDataCopy.Foundation(snapshot.Foundation);
            else Weapon = AssemblyProfileDataCopy.Weapon(snapshot.Weapon);
            Definitions = AssemblyProfileDataCopy.Definitions(snapshot.Definitions);
        }
        public ComponentRecipeSnapshot CreateSnapshot()
        {
            AssemblyProfileDataCopy.GuardFuture(this);
            AssemblyProfileDataCopy.Version(Version); AssemblyProfileDataCopy.ComponentKind(Kind);
            var definitions = AssemblyProfileDataCopy.ReadDefinitions(Definitions);
            if (Kind == ComponentLibraryKind.Foundation)
            {
                if (AssemblyProfileDataCopy.HasWeapon(Weapon)) throw new ArgumentException("Unexpected weapon component payload.");
                var foundation = AssemblyProfileDataCopy.ReadFoundation(Foundation);
                AssemblyProfileDataCopy.RequireDefinitions(foundation.Parts, definitions);
                return new ComponentRecipeSnapshot(foundation, BalanceRevision, definitions);
            }
            if (AssemblyProfileDataCopy.HasFoundation(Foundation)) throw new ArgumentException("Unexpected foundation component payload.");
            var weapon = AssemblyProfileDataCopy.ReadWeapon(Weapon);
            AssemblyProfileDataCopy.RequireDefinitions(weapon.Parts, definitions);
            return new ComponentRecipeSnapshot(weapon, BalanceRevision, definitions);
        }
    }

    [Serializable] public sealed class ComponentLibraryEntryData
    {
        public int Version = 1;
        public string Id, Name;
        public int Revision = 1;
        public ComponentLibraryKind Kind;
        public ComponentRecipeSnapshotData Snapshot;
        public ComponentLibraryEntryData() { }
        public ComponentLibraryEntryData(ComponentLibraryEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            Id = entry.Id; Name = entry.Name; Revision = entry.Revision; Kind = entry.Kind;
            Snapshot = new ComponentRecipeSnapshotData(entry.Snapshot);
        }
        public ComponentLibraryEntry CreateSnapshot()
        {
            AssemblyProfileDataCopy.GuardFuture(this);
            AssemblyProfileDataCopy.Version(Version); AssemblyProfileDataCopy.ComponentKind(Kind);
            if (Snapshot == null) throw new ArgumentException("Missing component library snapshot.");
            var snapshot = Snapshot.CreateSnapshot();
            if (snapshot.Kind != Kind) throw new ArgumentException("Component library kind does not match its snapshot.");
            return new ComponentLibraryEntry(Id, Name, Revision, snapshot);
        }
    }

    [Serializable] public sealed class RunRewardReceiptData
    {
        public int Version = 1;
        public string RunId;
        public int Waves;
        public bool VictoryBonus;
    }
    [Serializable] public sealed class OperationReceiptData
    {
        public int Version = 1;
        public string OperationId, Type, ResultId;
    }

    internal static class AssemblyProfileDataCopy
    {
        // Scan all present variants before malformed payload checks. A newer nested schema
        // protects the source even when it appears in an inactive or otherwise damaged union.
        static void FutureVersion(int version, int supported)
        { if (version > supported) throw new NotSupportedException("Newer assembly profile metadata."); }
        internal static void GuardFuture(WorkshopProfileData data)
        {
            if (data == null) return;
            FutureVersion(data.Version, WorkshopProfile.CurrentVersion);
            // This pass deliberately ignores missing fields, invalid amounts and malformed
            // siblings. Every collection must be scanned before structural reading can fail.
            foreach (var entry in data.Entries ?? Array.Empty<AlbumEntryData>()) GuardFuture(entry);
            foreach (var entry in data.Library ?? Array.Empty<ComponentLibraryEntryData>()) GuardFuture(entry);
            foreach (var receipt in data.Rewards ?? Array.Empty<RunRewardReceiptData>())
                if (receipt != null) FutureVersion(receipt.Version, 1);
            foreach (var receipt in data.Operations ?? Array.Empty<OperationReceiptData>())
                if (receipt != null) FutureVersion(receipt.Version, 1);
            foreach (var blueprint in data.Album ?? Array.Empty<BlueprintData>()) GuardFuture(blueprint);
        }
        internal static void GuardFuture(AlbumEntryData data)
        {
            if (data == null) return;
            FutureVersion(data.Version, 1); EntryKind(data.Kind);
            GuardFuture(data.Snapshot); GuardFuture(data.LegacyBlueprint);
            foreach (var reference in data.LibraryReferences ?? Array.Empty<ComponentLibraryReferenceData>())
                if (reference != null) ComponentKind(reference.Kind);
        }
        internal static void GuardFuture(RecipeSnapshotData data)
        {
            if (data == null) return;
            FutureVersion(data.Version, 1); GuardFuture(data.Recipe); GuardFutureDefinitions(data.Definitions);
            GuardUnknownParts(RecipeParts(data.Recipe), data.Definitions);
        }
        internal static void GuardFuture(AssemblyRecipeData data)
        {
            if (data == null) return;
            EntryKind(data.Kind);
            if (data.Structure != null && data.Structure.Family != AssemblyStructureFamily.None &&
                data.Structure.Family != AssemblyStructureFamily.Wall && data.Structure.Family != AssemblyStructureFamily.Slow &&
                data.Structure.Family != AssemblyStructureFamily.Repair && data.Structure.Family != AssemblyStructureFamily.Generator)
                throw new NotSupportedException("Unknown structure family.");
        }
        internal static void GuardFuture(ComponentLibraryEntryData data)
        {
            if (data == null) return;
            FutureVersion(data.Version, 1); ComponentKind(data.Kind); GuardFuture(data.Snapshot);
        }
        internal static void GuardFuture(ComponentRecipeSnapshotData data)
        {
            if (data == null) return;
            FutureVersion(data.Version, 1); ComponentKind(data.Kind); GuardFutureDefinitions(data.Definitions);
            GuardUnknownParts(data.Kind == ComponentLibraryKind.Foundation ? FoundationParts(data.Foundation) : WeaponParts(data.Weapon), data.Definitions);
        }
        static IEnumerable<PartRefInput> RecipeParts(AssemblyRecipeData recipe)
        {
            if (recipe == null) yield break;
            if (recipe.Kind == AlbumEntryKind.Tower)
            {
                foreach (var part in FoundationParts(recipe.Foundation)) yield return part;
                foreach (var mount in recipe.Mounts ?? Array.Empty<WeaponMountRecipeInput>())
                    if (mount != null) foreach (var part in WeaponParts(mount.Weapon)) yield return part;
            }
            else if (recipe.Kind == AlbumEntryKind.Structure && recipe.Structure != null)
            {
                yield return recipe.Structure.Site; yield return recipe.Structure.Function;
                yield return recipe.Structure.Shell; yield return recipe.Structure.Utility;
            }
        }
        static IEnumerable<PartRefInput> FoundationParts(FoundationRecipeInput recipe)
        {
            if (recipe == null) yield break;
            yield return recipe.Frame; yield return recipe.Hull; yield return recipe.Armor; yield return recipe.Feed; yield return recipe.Turn;
        }
        static IEnumerable<PartRefInput> WeaponParts(WeaponRecipeInput recipe)
        {
            if (recipe == null) yield break;
            yield return recipe.Mechanism; yield return recipe.Head; yield return recipe.Sight; yield return recipe.Magazine; yield return recipe.Trigger;
        }
        static void GuardUnknownParts(IEnumerable<PartRefInput> parts, ComponentDefinitionInput[] definitions)
        {
            // Missing definitions are malformed metadata. Present, valid references whose
            // identity is absent from an embedded catalog are unsupported saved content.
            if (definitions == null) return;
            var known = new HashSet<(string, int)>();
            foreach (var definition in definitions)
                if (definition != null && !string.IsNullOrWhiteSpace(definition.Id) && definition.Revision > 0)
                    known.Add((definition.Id, definition.Revision));
            foreach (var part in parts)
                if (part != null && !string.IsNullOrWhiteSpace(part.Id) && part.Revision > 0 && !known.Contains((part.Id, part.Revision)))
                    throw new NotSupportedException("Unknown saved component ID or revision: " + part.Id + "@" + part.Revision);
        }
        static void GuardFuture(BlueprintData data)
        {
            if (data == null) return;
            FutureVersion(data.ContentMetadataVersion, 1); FutureVersion(data.EvaluationMetadataVersion, 1);
            FutureVersion(data.DrawingMetadataVersion, 1); FutureVersion(data.FootprintMetadataVersion, 2);
            FutureVersion(data.WeaponMountMetadataVersion, 2); FutureVersion(data.FreeformRuleVersion, FreeformTowerDesign.RuleVersion);
            if (data.Reinforcement != null) FutureVersion(data.Reinforcement.Version, 1);
            // Zero/-1 occur in empty Unity inline objects; -1 also represents historical
            // numeric fields for stable content IDs without an old enum counterpart.
            if (((int)data.Origin != 0 && (int)data.Origin != -1 && !Enum.IsDefined(typeof(DrawingOrigin), data.Origin)) ||
                ((int)data.Chassis != -1 && !Enum.IsDefined(typeof(ChassisKind), data.Chassis)) ||
                ((int)data.FirstWeapon != -1 && !Enum.IsDefined(typeof(StructureKind), data.FirstWeapon)) ||
                ((int)data.SecondWeapon != -1 && !Enum.IsDefined(typeof(StructureKind), data.SecondWeapon)) ||
                (data.Modules & ~(TowerModules.Armor | TowerModules.Feed | TowerModules.Tracking | TowerModules.Rail)) != 0)
                throw new NotSupportedException("Unknown historical blueprint enum.");
            if (data.Drawing != null)
            {
                FutureVersion(data.Drawing.FormatVersion, DrawingSnapshot.CurrentFormatVersion);
                FutureVersion(data.Drawing.BindingVersion, DrawingSnapshot.CurrentBindingVersion);
                foreach (var part in data.Drawing.Parts ?? Array.Empty<DrawingPartData>())
                    if (part != null && (int)part.Key != -1 && !Enum.IsDefined(typeof(DrawingPartKey), part.Key))
                        throw new NotSupportedException("Unknown historical drawing part.");
            }
            foreach (var mount in data.WeaponMounts ?? Array.Empty<WeaponMountSelectionData>())
                if (mount != null) { FutureVersion(mount.FootprintContourVersion, 1); FutureVersion(mount.VolleyVersion, 1); }
            // Evaluation.RuleVersion identifies an archived algorithm, not a DTO schema.
        }
        static void GuardFutureDefinitions(ComponentDefinitionInput[] inputs)
        {
            foreach (var source in inputs ?? Array.Empty<ComponentDefinitionInput>())
            {
                if (source == null) continue;
                if (!Enum.IsDefined(typeof(ComponentCategory), source.Category) || (source.WeaponFamilies & ~AssemblyWeaponFamily.All) != 0 ||
                    (source.StructureFamilies & ~AssemblyStructureFamily.All) != 0) throw new NotSupportedException("Unknown embedded component category or family.");
                foreach (var modifier in source.Modifiers ?? Array.Empty<ComponentModifierInput>())
                    if (modifier != null && (!Enum.IsDefined(typeof(AssemblyStat), modifier.Stat) || !Enum.IsDefined(typeof(ModifierOperation), modifier.Operation)))
                        throw new NotSupportedException("Unknown embedded component modifier.");
            }
        }
        internal static void Version(int version)
        {
            if (version > 1) throw new NotSupportedException("Newer assembly profile metadata.");
            if (version != 1) throw new ArgumentException("Missing assembly profile metadata version.");
        }
        internal static void EntryKind(AlbumEntryKind kind)
        { if (!Enum.IsDefined(typeof(AlbumEntryKind), kind)) throw new NotSupportedException("Unknown album entry kind."); }
        internal static void ComponentKind(ComponentLibraryKind kind)
        { if (!Enum.IsDefined(typeof(ComponentLibraryKind), kind)) throw new NotSupportedException("Unknown component library kind."); }
        internal static PartRefInput Part(PartRef part) => new PartRefInput { Id = part.Id, Revision = part.Revision };
        internal static PartRef ReadPart(PartRefInput part)
        { if (part == null) throw new ArgumentException("Missing recipe part."); return new PartRef(part.Id, part.Revision); }
        internal static FoundationRecipeInput Foundation(FoundationRecipe recipe) => new FoundationRecipeInput
        { Frame = Part(recipe.Frame), Hull = Part(recipe.Hull), Armor = Part(recipe.Armor), Feed = Part(recipe.Feed), Turn = Part(recipe.Turn) };
        internal static WeaponRecipeInput Weapon(WeaponRecipe recipe) => new WeaponRecipeInput
        { Mechanism = Part(recipe.Mechanism), Head = Part(recipe.Head), Sight = Part(recipe.Sight), Magazine = Part(recipe.Magazine), Trigger = Part(recipe.Trigger) };
        internal static FoundationRecipe ReadFoundation(FoundationRecipeInput recipe)
        {
            if (recipe == null) throw new ArgumentException("Missing foundation recipe.");
            return new FoundationRecipe(ReadPart(recipe.Frame), ReadPart(recipe.Hull), ReadPart(recipe.Armor), ReadPart(recipe.Feed), ReadPart(recipe.Turn));
        }
        internal static WeaponRecipe ReadWeapon(WeaponRecipeInput recipe)
        {
            if (recipe == null) throw new ArgumentException("Missing weapon recipe.");
            return new WeaponRecipe(ReadPart(recipe.Mechanism), ReadPart(recipe.Head), ReadPart(recipe.Sight), ReadPart(recipe.Magazine), ReadPart(recipe.Trigger));
        }
        // Unity may materialize empty inline variant objects on a JSON round trip.
        static bool HasPart(params PartRefInput[] parts)
        { foreach (var part in parts) if (part != null && (!string.IsNullOrEmpty(part.Id) || part.Revision < 0 || part.Revision > 1)) return true; return false; }
        internal static bool HasFoundation(FoundationRecipeInput recipe)
            => recipe != null && HasPart(recipe.Frame, recipe.Hull, recipe.Armor, recipe.Feed, recipe.Turn);
        internal static bool HasWeapon(WeaponRecipeInput recipe)
            => recipe != null && HasPart(recipe.Mechanism, recipe.Head, recipe.Sight, recipe.Magazine, recipe.Trigger);
        internal static bool HasStructure(StructureRecipeInput recipe)
            => recipe != null && (recipe.Family != AssemblyStructureFamily.None || HasPart(recipe.Site, recipe.Function, recipe.Shell, recipe.Utility));
        internal static bool HasSnapshot(RecipeSnapshotData snapshot)
            => snapshot != null && ((snapshot.Version != 0 && snapshot.Version != 1) || !string.IsNullOrEmpty(snapshot.BalanceRevision) || (snapshot.Definitions != null && snapshot.Definitions.Length != 0) ||
                (snapshot.Recipe != null && (snapshot.Recipe.Kind != AlbumEntryKind.Tower || HasFoundation(snapshot.Recipe.Foundation) || HasStructure(snapshot.Recipe.Structure) || (snapshot.Recipe.Mounts != null && snapshot.Recipe.Mounts.Length != 0))));
        internal static bool HasBlueprint(BlueprintData blueprint)
            => blueprint != null && (!string.IsNullOrEmpty(blueprint.Id) || !string.IsNullOrEmpty(blueprint.Name) || !string.IsNullOrEmpty(blueprint.ChassisId) ||
                !string.IsNullOrEmpty(blueprint.FirstWeaponId) || !string.IsNullOrEmpty(blueprint.SecondWeaponId) || blueprint.ContentMetadataVersion != 0 ||
                blueprint.DrawingMetadataVersion != 0 || blueprint.EvaluationMetadataVersion != 0 || blueprint.WeaponMountMetadataVersion != 0 ||
                blueprint.FootprintMetadataVersion != 0 || blueprint.FreeformRuleVersion != 0 || (blueprint.ModuleSelections != null && blueprint.ModuleSelections.Length != 0) ||
                (blueprint.WeaponMounts != null && blueprint.WeaponMounts.Length != 0) || blueprint.Chassis != 0 || blueprint.FirstWeapon != 0 ||
                blueprint.SecondWeapon != 0 || blueprint.HasSecondWeapon || blueprint.Modules != 0 || blueprint.FirstFootprintAspect != 0 || blueprint.SecondFootprintAspect != 0 ||
                ((int)blueprint.Origin != 0 && (int)blueprint.Origin != -1) ||
                (blueprint.Drawing != null && (blueprint.Drawing.FormatVersion != 0 || blueprint.Drawing.BindingVersion != 0 || (blueprint.Drawing.Parts != null && blueprint.Drawing.Parts.Length != 0))) ||
                (blueprint.Evaluation != null && (blueprint.Evaluation.RuleVersion != 0 || !string.IsNullOrEmpty(blueprint.Evaluation.DrawingHash) ||
                    !string.IsNullOrEmpty(blueprint.Evaluation.ReferenceSignature) || !string.IsNullOrEmpty(blueprint.Evaluation.ConstructionHash) ||
                    blueprint.Evaluation.Quality != 0 || (blueprint.Evaluation.Feedback != null && blueprint.Evaluation.Feedback.Length != 0))) ||
                (blueprint.Reinforcement != null && (blueprint.Reinforcement.Version != 0 || blueprint.Reinforcement.FacingQuarterTurns != 0 ||
                    (blueprint.Reinforcement.Points != null && blueprint.Reinforcement.Points.Length != 0))));
        internal static ComponentDefinitionInput[] Definitions(IReadOnlyList<ComponentDefinition> definitions)
        {
            var result = new ComponentDefinitionInput[definitions.Count];
            for (int i = 0; i < result.Length; i++)
            {
                var source = definitions[i];
                var dto = new ComponentDefinitionInput { Id = source.Id, Name = source.Name, Revision = source.Revision, Category = source.Category,
                    RequiredLevel = source.RequiredLevel, UnlockGold = source.UnlockGold, WeaponFamilies = source.WeaponFamilies, StructureFamilies = source.StructureFamilies,
                    Modifiers = new ComponentModifierInput[source.Modifiers.Count], RequiredParts = new PartRefInput[source.RequiredParts.Count] };
                for (int j = 0; j < dto.Modifiers.Length; j++) dto.Modifiers[j] = new ComponentModifierInput
                    { Stat = source.Modifiers[j].Stat, Operation = source.Modifiers[j].Operation, Value = source.Modifiers[j].Value };
                for (int j = 0; j < dto.RequiredParts.Length; j++) dto.RequiredParts[j] = Part(source.RequiredParts[j]);
                result[i] = dto;
            }
            return result;
        }
        internal static ComponentDefinition[] ReadDefinitions(ComponentDefinitionInput[] inputs)
        {
            GuardFutureDefinitions(inputs);
            if (inputs == null) throw new ArgumentException("Missing embedded component definitions.");
            var result = new ComponentDefinition[inputs.Length];
            for (int i = 0; i < result.Length; i++)
            {
                var source = inputs[i];
                if (source == null || source.Modifiers == null || source.RequiredParts == null) throw new ArgumentException("Missing embedded component fields.");
                if (!Enum.IsDefined(typeof(ComponentCategory), source.Category) || (source.WeaponFamilies & ~AssemblyWeaponFamily.All) != 0 ||
                    (source.StructureFamilies & ~AssemblyStructureFamily.All) != 0) throw new NotSupportedException("Unknown embedded component category or family.");
                foreach (var modifier in source.Modifiers)
                {
                    if (modifier == null) throw new ArgumentException("Missing embedded modifier.");
                    if (!Enum.IsDefined(typeof(AssemblyStat), modifier.Stat) || !Enum.IsDefined(typeof(ModifierOperation), modifier.Operation))
                        throw new NotSupportedException("Unknown embedded component modifier.");
                }
                foreach (var required in source.RequiredParts) ReadPart(required);
                result[i] = source.CreateSnapshot();
            }
            return result;
        }
        internal static void RequireDefinitions(IEnumerable<PartRef> parts, ComponentDefinition[] definitions)
        {
            var identities = new HashSet<(string, int)>();
            foreach (var definition in definitions) identities.Add((definition.Id, definition.Revision));
            foreach (var part in parts)
                if (!identities.Contains((part.Id, part.Revision))) throw new NotSupportedException("Unknown saved component ID or revision: " + part.Id + "@" + part.Revision);
        }
    }
}
