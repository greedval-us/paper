using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AssemblyProfileDataTests
    {
        static T RoundTrip<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        static RecipeSnapshot TowerSnapshot() => AssemblyResolverFixture.Catalog().Starters[0].Snapshot;
        static ComponentDefinition[] Used(FoundationRecipe recipe)
        {
            var catalog = AssemblyResolverFixture.Catalog();
            return recipe.Parts.Select(p => { catalog.TryGet(p.Id, p.Revision, out var d); return d; }).ToArray();
        }
        static ComponentDefinition[] Used(WeaponRecipe recipe)
        {
            var catalog = AssemblyResolverFixture.Catalog();
            return recipe.Parts.Select(p => { catalog.TryGet(p.Id, p.Revision, out var d); return d; }).ToArray();
        }

        [Test] public void TowerEntry_RoundTripsOwnedRecipeAndDefinitions()
        {
            var original = new AlbumEntry("saved.tower", "Saved tower", 3, TowerSnapshot());
            var data = RoundTrip(new AlbumEntryData(original));
            var restored = data.CreateSnapshot();
            Assert.That(restored.Id, Is.EqualTo(original.Id));
            Assert.That(restored.Name, Is.EqualTo(original.Name));
            Assert.That(restored.Revision, Is.EqualTo(3));
            Assert.That(restored.Kind, Is.EqualTo(AlbumEntryKind.Tower));
            var resolution = AssemblyResolver.Resolve(restored.Snapshot, GameBalance.CreateDefault(), MaterialTier.Pencil);
            Assert.That(resolution.Valid, Is.True);
            Assert.That(resolution.Value.Cost, Is.EqualTo(40));
            Assert.That(resolution.Value.Weapons[0].Damage, Is.EqualTo(12));
        }

        [Test] public void StructureEntry_RoundTripsWithoutTowerVariantPayload()
        {
            var starter = AssemblyResolverFixture.Catalog().Starters[7];
            var restored = RoundTrip(new AlbumEntryData(starter)).CreateSnapshot();
            Assert.That(restored.Kind, Is.EqualTo(AlbumEntryKind.Structure));
            Assert.That(restored.Snapshot.Recipe.Tower, Is.Null);
            Assert.That(restored.Snapshot.Recipe.Structure.Family, Is.EqualTo(AssemblyStructureFamily.Generator));
            Assert.That(AssemblyResolver.Resolve(restored.Snapshot, GameBalance.CreateDefault(), MaterialTier.Pencil).Value.Support.IncomePerWave,
                Is.EqualTo(12));
        }

        [Test] public void LegacyEntry_RoundTripsHistoricalBlueprintWithoutConversion()
        {
            var blueprint = new TowerBlueprint("saved.legacy", "Historical", ChassisKind.Standard, StructureKind.Mortar,
                modules: TowerModules.Armor, firstFootprint: new MortarFootprint(2));
            var restored = RoundTrip(new AlbumEntryData(new AlbumEntry(blueprint.Id, blueprint.Name, 1, blueprint))).CreateSnapshot();
            Assert.That(restored.Kind, Is.EqualTo(AlbumEntryKind.Legacy));
            Assert.That(restored.Snapshot, Is.Null);
            Assert.That(JsonUtility.ToJson(new BlueprintData(restored.LegacyBlueprint)), Is.EqualTo(JsonUtility.ToJson(new BlueprintData(blueprint))));
        }

        [Test] public void JsonInlineEmptyOppositeVariants_AreAcceptedByTheirTags()
        {
            var dto = RoundTrip(new AlbumEntryData(new AlbumEntry("saved.tower", "Tower", 1, TowerSnapshot())));
            dto.LegacyBlueprint = new BlueprintData();
            dto.Snapshot.Recipe.Structure = new StructureRecipeInput
            {
                Site = new PartRefInput(), Function = new PartRefInput(), Shell = new PartRefInput(), Utility = new PartRefInput()
            };
            Assert.That(dto.CreateSnapshot().Kind, Is.EqualTo(AlbumEntryKind.Tower));
        }

        [Test] public void PopulatedOppositeRecipeVariant_IsRejected()
        {
            var dto = new RecipeSnapshotData(TowerSnapshot());
            dto.Recipe.Structure = new StructureRecipeInput { Family = AssemblyStructureFamily.Wall };
            Assert.Catch<ArgumentException>(() => dto.CreateSnapshot());
        }

        [Test] public void EntryKindMustMatchItsSnapshotKind()
        {
            var dto = new AlbumEntryData(new AlbumEntry("saved.tower", "Tower", 1, TowerSnapshot()));
            dto.Kind = AlbumEntryKind.Structure;
            Assert.Catch<ArgumentException>(() => dto.CreateSnapshot());
        }

        [Test] public void EmbeddedRevisionTwo_IsSelfContainedAndDoesNotNeedCurrentCatalog()
        {
            var input = AssemblyResolverFixture.Input();
            var definition = input.Definitions.Single(d => d.Id == "core.gun");
            definition.Revision = 2;
            var recipe = AssemblyResolverFixture.Tower(new WeaponRecipe(AssemblyResolverFixture.Part("core.gun", 2),
                AssemblyResolverFixture.Part("head.gun"), AssemblyResolverFixture.Part("sight.iron"),
                AssemblyResolverFixture.Part("magazine.standard"), AssemblyResolverFixture.Part("trigger.standard")));
            var catalog = AssemblyResolverFixture.Modified(input);
            var resolution = AssemblyResolver.Resolve(recipe, catalog, GameBalance.CreateDefault(), MaterialTier.Pencil);
            Assert.That(resolution.Valid, Is.True);
            var restored = RoundTrip(new RecipeSnapshotData(resolution.Value.Snapshot)).CreateSnapshot();
            Assert.That(restored.Recipe.Tower.Mounts[0].Weapon.Mechanism.Revision, Is.EqualTo(2));
            Assert.That(AssemblyResolver.Resolve(restored, GameBalance.CreateDefault(), MaterialTier.Pencil).Value.Cost, Is.EqualTo(40));
        }

        [Test] public void MissingEmbeddedReferencedRevision_ProtectsSource()
        {
            var dto = new RecipeSnapshotData(TowerSnapshot());
            dto.Recipe.Mounts[0].Weapon.Mechanism.Revision = 99;
            Assert.Throws<NotSupportedException>(() => dto.CreateSnapshot());
        }

        [TestCase("entry")] [TestCase("snapshot")] [TestCase("library")] [TestCase("component")]
        public void FutureDtoVersion_ProtectsSource(string kind)
        {
            var foundation = AssemblyResolverFixture.Foundation();
            var component = new ComponentRecipeSnapshot(foundation, "album-v1.0", Used(foundation));
            if (kind == "entry")
            {
                var dto = new AlbumEntryData(new AlbumEntry("entry", "Entry", 1, TowerSnapshot())) { Version = 2 };
                Assert.Throws<NotSupportedException>(() => dto.CreateSnapshot());
            }
            else if (kind == "snapshot")
            {
                var dto = new RecipeSnapshotData(TowerSnapshot()) { Version = 2 };
                Assert.Throws<NotSupportedException>(() => dto.CreateSnapshot());
            }
            else if (kind == "library")
            {
                var dto = new ComponentLibraryEntryData(new ComponentLibraryEntry("library", "Library", 1, component)) { Version = 2 };
                Assert.Throws<NotSupportedException>(() => dto.CreateSnapshot());
            }
            else
            {
                var dto = new ComponentRecipeSnapshotData(component) { Version = 2 };
                Assert.Throws<NotSupportedException>(() => dto.CreateSnapshot());
            }
        }

        [TestCase(true)] [TestCase(false)]
        public void ComponentLibrary_RoundTripsExactlyOneFivePartRecipe(bool foundationKind)
        {
            var foundation = AssemblyResolverFixture.Foundation(); var weapon = AssemblyResolverFixture.Weapon();
            var snapshot = foundationKind ? new ComponentRecipeSnapshot(foundation, "album-v1.0", Used(foundation))
                : new ComponentRecipeSnapshot(weapon, "album-v1.0", Used(weapon));
            var original = new ComponentLibraryEntry("library.component", "Saved component", 2, snapshot);
            var dto = RoundTrip(new ComponentLibraryEntryData(original));
            var restored = dto.CreateSnapshot();
            Assert.That(restored.Id, Is.EqualTo(original.Id));
            Assert.That(restored.Revision, Is.EqualTo(2));
            Assert.That(restored.Kind, Is.EqualTo(foundationKind ? ComponentLibraryKind.Foundation : ComponentLibraryKind.Weapon));
            Assert.That(restored.Snapshot.Definitions.Count, Is.EqualTo(5));
            Assert.That(restored.Snapshot.Foundation == null, Is.EqualTo(!foundationKind));
            Assert.That(restored.Snapshot.Weapon == null, Is.EqualTo(foundationKind));
        }

        [Test] public void ComponentSnapshot_OwnsCopiedDefinitionsAndDtoOwnsMutableInputs()
        {
            var foundation = AssemblyResolverFixture.Foundation(); var definitions = Used(foundation);
            var original = new ComponentRecipeSnapshot(foundation, "album-v1.0", definitions);
            definitions[0] = null;
            Assert.That(original.Definitions[0], Is.Not.Null);
            var dto = new ComponentRecipeSnapshotData(original);
            var restored = dto.CreateSnapshot();
            dto.Definitions[0].Modifiers[0].Value = 999;
            dto.Foundation.Frame.Id = "changed";
            Assert.That(restored.Foundation.Frame.Id, Is.EqualTo("frame.standard"));
            Assert.That(restored.Definitions[0].Modifiers[0].Value, Is.Not.EqualTo(999));
            Assert.That(original.Definitions[0].Modifiers[0].Value, Is.Not.EqualTo(999));
        }

        [Test] public void ComponentSnapshot_RejectsWrongCategoryAndMissingDependency()
        {
            var weapon = AssemblyResolverFixture.Weapon(); var data = AssemblyResolverFixture.Input();
            data.Definitions.Single(d => d.Id == "trigger.standard").RequiredParts = new[] { new PartRefInput { Id = "magazine.drum", Revision = 1 } };
            var changed = AssemblyResolverFixture.Modified(data);
            var selected = weapon.Parts.Select(p => { changed.TryGet(p.Id, p.Revision, out var d); return d; }).ToArray();
            Assert.Catch<ArgumentException>(() => new ComponentRecipeSnapshot(weapon, "album-v1.0", selected));
            var foundation = AssemblyResolverFixture.Foundation(); var wrong = Used(foundation);
            wrong[0] = new ComponentDefinition("frame.standard", 1, "Wrong", ComponentCategory.Hull, 1, 0,
                AssemblyWeaponFamily.All, AssemblyStructureFamily.None, Array.Empty<ComponentModifier>());
            Assert.Catch<ArgumentException>(() => new ComponentRecipeSnapshot(foundation, "album-v1.0", wrong));
        }

        [Test] public void LibraryReferenceMetadata_DoesNotReplaceOwnedSnapshot()
        {
            var dto = new AlbumEntryData(new AlbumEntry("entry", "Entry", 1, TowerSnapshot()));
            dto.LibraryReferences = new[] { new ComponentLibraryReferenceData
                { Id = "deleted.library", Revision = 1, Kind = ComponentLibraryKind.Weapon, SlotIndex = 0 } };
            var roundTrip = RoundTrip(dto);
            Assert.That(roundTrip.LibraryReferences[0].Id, Is.EqualTo("deleted.library"));
            var restored = roundTrip.CreateSnapshot();
            Assert.That(restored.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id, Is.EqualTo("core.gun"));
            Assert.That(restored.LibraryReferences[0].Id, Is.EqualTo("deleted.library"));
            Assert.That(new AlbumEntryData(restored).LibraryReferences[0].Id, Is.EqualTo("deleted.library"));
        }

        [TestCase("category")] [TestCase("modifier")]
        public void UnknownEmbeddedEnums_ProtectSource(string field)
        {
            var dto = new RecipeSnapshotData(TowerSnapshot());
            if (field == "category") dto.Definitions[0].Category = (ComponentCategory)99;
            else dto.Definitions[0].Modifiers[0].Stat = (AssemblyStat)99;
            Assert.Throws<NotSupportedException>(() => dto.CreateSnapshot());
        }

        [Test] public void NonemptyInactiveLegacyPayload_IsRejected()
        {
            var dto = new AlbumEntryData(new AlbumEntry("entry", "Entry", 1, TowerSnapshot()));
            dto.LegacyBlueprint = new BlueprintData { Modules = TowerModules.Armor };
            Assert.Catch<ArgumentException>(() => dto.CreateSnapshot());
        }

        [TestCase("content")] [TestCase("evaluation")] [TestCase("drawing")] [TestCase("footprint")]
        [TestCase("mounts")] [TestCase("reinforcement")] [TestCase("freeform")] [TestCase("origin")]
        [TestCase("drawingFormat")] [TestCase("drawingBinding")] [TestCase("contour")] [TestCase("volley")]
        [TestCase("chassisEnum")] [TestCase("weaponEnum")] [TestCase("moduleEnum")]
        public void InactiveLegacyFutureMetadata_ProtectsSourceBeforeUnionValidation(string field)
        {
            var dto = new AlbumEntryData(new AlbumEntry("entry", "Entry", 1, TowerSnapshot()));
            dto.LegacyBlueprint = new BlueprintData();
            switch (field)
            {
                case "content": dto.LegacyBlueprint.ContentMetadataVersion = 999; break;
                case "evaluation": dto.LegacyBlueprint.EvaluationMetadataVersion = 999; break;
                case "drawing": dto.LegacyBlueprint.DrawingMetadataVersion = 999; break;
                case "footprint": dto.LegacyBlueprint.FootprintMetadataVersion = 999; break;
                case "mounts": dto.LegacyBlueprint.WeaponMountMetadataVersion = 999; break;
                case "reinforcement": dto.LegacyBlueprint.Reinforcement = new ArmorDistributionData { Version = 999 }; break;
                case "freeform": dto.LegacyBlueprint.FreeformRuleVersion = 999; break;
                case "origin": dto.LegacyBlueprint.Origin = (DrawingOrigin)999; break;
                case "drawingFormat": dto.LegacyBlueprint.Drawing = new DrawingData { FormatVersion = 999 }; break;
                case "drawingBinding": dto.LegacyBlueprint.Drawing = new DrawingData { BindingVersion = 999 }; break;
                case "contour": dto.LegacyBlueprint.WeaponMounts = new[] { new WeaponMountSelectionData { FootprintContourVersion = 999 } }; break;
                case "volley": dto.LegacyBlueprint.WeaponMounts = new[] { new WeaponMountSelectionData { VolleyVersion = 999 } }; break;
                case "chassisEnum": dto.LegacyBlueprint.Chassis = (ChassisKind)999; break;
                case "weaponEnum": dto.LegacyBlueprint.FirstWeapon = (StructureKind)999; break;
                case "moduleEnum": dto.LegacyBlueprint.Modules = (TowerModules)(1 << 28); break;
            }
            Assert.Throws<NotSupportedException>(() => RoundTrip(dto).CreateSnapshot());
        }

        [TestCase("kind")] [TestCase("category")] [TestCase("modifier")]
        [TestCase("operation")] [TestCase("structureFamily")]
        public void InactiveSnapshotFutureMetadata_ProtectsSourceBeforeUnionValidation(string field)
        {
            var blueprint = new TowerBlueprint("legacy", "Legacy", ChassisKind.Standard, StructureKind.Gun);
            var dto = new AlbumEntryData(new AlbumEntry(blueprint.Id, blueprint.Name, 1, blueprint));
            dto.Snapshot = field == "kind" || field == "structureFamily"
                ? new RecipeSnapshotData { Recipe = new AssemblyRecipeData() } : new RecipeSnapshotData(TowerSnapshot());
            switch (field)
            {
                case "kind": dto.Snapshot.Recipe.Kind = (AlbumEntryKind)999; break;
                case "category": dto.Snapshot.Definitions[0].Category = (ComponentCategory)999; break;
                case "modifier": dto.Snapshot.Definitions[0].Modifiers[0].Stat = (AssemblyStat)999; break;
                case "operation": dto.Snapshot.Definitions[0].Modifiers[0].Operation = (ModifierOperation)999; break;
                case "structureFamily": dto.Snapshot.Recipe.Structure = new StructureRecipeInput { Family = (AssemblyStructureFamily)999 }; break;
            }
            Assert.Throws<NotSupportedException>(() => RoundTrip(dto).CreateSnapshot());
        }

        [Test] public void LegacyEntry_RejectsPopulatedLibraryReferences()
        {
            var blueprint = new TowerBlueprint("legacy", "Legacy", ChassisKind.Standard, StructureKind.Gun);
            var dto = new AlbumEntryData(new AlbumEntry(blueprint.Id, blueprint.Name, 1, blueprint))
            {
                LibraryReferences = new[] { new ComponentLibraryReferenceData
                    { Id = "saved.component", Revision = 1, Kind = ComponentLibraryKind.Weapon, SlotIndex = 0 } }
            };
            Assert.Catch<ArgumentException>(() => RoundTrip(dto).CreateSnapshot());
        }

        [TestCase("entries")] [TestCase("library")] [TestCase("rewards")]
        [TestCase("operations")] [TestCase("legacyAlbum")]
        public void ProfilePreflight_LaterFutureDataWinsOverEarlierMalformedEntry(string collection)
        {
            var catalog = AssemblyResolverFixture.Catalog();
            var source = WorkshopProfile.CreateNew(catalog).ToData();
            source.Gold = -1;
            source.Entries[0] = new AlbumEntryData { Version = 0 };
            AddLateFuture(source, collection);
            var result = ProfileMigrationV8.Migrate(RoundTrip(source), catalog);
            Assert.That(result.Success, Is.False);
            Assert.That(result.IsUnsupported, Is.True, "A damaged earlier field must not make a later future schema writable through backup recovery.");
        }

        [TestCase("entries")] [TestCase("library")] [TestCase("rewards")]
        [TestCase("operations")] [TestCase("legacyAlbum")]
        public void ProfilePreflight_LaterFutureSiblingWinsOverEarlierMalformedSibling(string collection)
        {
            var catalog = AssemblyResolverFixture.Catalog();
            var source = WorkshopProfile.CreateNew(catalog).ToData();
            AddLateFuture(source, collection);
            switch (collection)
            {
                case "entries": source.Entries[0] = new AlbumEntryData { Version = 0 }; break;
                case "library": source.Library[0] = new ComponentLibraryEntryData { Version = 0 }; break;
                case "rewards": source.Rewards[0] = new RunRewardReceiptData { Version = 0 }; break;
                case "operations": source.Operations[0] = new OperationReceiptData { Version = 0 }; break;
                case "legacyAlbum": source.Album[0] = new BlueprintData(); break;
            }
            var result = ProfileMigrationV8.Migrate(RoundTrip(source), catalog);
            Assert.That(result.Success, Is.False);
            Assert.That(result.IsUnsupported, Is.True, "Collection order must not hide a future schema behind a malformed sibling.");
        }

        [Test] public void ProfilePreflight_MalformedOnlyDataRemainsRecoverable()
        {
            var source = WorkshopProfile.CreateNew(AssemblyResolverFixture.Catalog()).ToData();
            source.Gold = -1;
            source.Entries = new[] { new AlbumEntryData { Version = 0 }, null };
            source.Library = new[] { new ComponentLibraryEntryData { Version = 0 }, null };
            source.Rewards = new[] { new RunRewardReceiptData { Version = 0 }, null };
            source.Operations = new[] { new OperationReceiptData { Version = 0 }, null };
            source.Album = new BlueprintData[] { null, new BlueprintData() };
            var result = ProfileMigrationV8.Migrate(RoundTrip(source), AssemblyResolverFixture.Catalog());
            Assert.That(result.Success, Is.False);
            Assert.That(result.IsUnsupported, Is.False, "Malformed supported data should retain ordinary compatible-backup recovery.");
        }

        [TestCase("id")] [TestCase("revision")]
        public void ProfilePreflight_LaterUnknownEntryPartWinsOverEarlierMalformedEntry(string identity)
        {
            var catalog = AssemblyResolverFixture.Catalog();
            var source = WorkshopProfile.CreateNew(catalog).ToData();
            source.Entries[0] = new AlbumEntryData { Version = 0 };
            var frame = source.Entries[1].Snapshot.Recipe.Foundation.Frame;
            if (identity == "id") frame.Id = "frame.unknown.saved";
            else frame.Revision = 999;
            var result = ProfileMigrationV8.Migrate(RoundTrip(source), catalog);
            Assert.That(result.Success, Is.False);
            Assert.That(result.IsUnsupported, Is.True, "An unknown embedded identity must protect the entire source despite an earlier malformed page.");
        }

        [TestCase(true, "id")] [TestCase(true, "revision")]
        [TestCase(false, "id")] [TestCase(false, "revision")]
        public void ProfilePreflight_LaterUnknownLibraryPartWinsOverEarlierMalformedSibling(bool foundationKind, string identity)
        {
            var catalog = AssemblyResolverFixture.Catalog();
            var foundation = AssemblyResolverFixture.Foundation(); var weapon = AssemblyResolverFixture.Weapon();
            var snapshot = foundationKind ? new ComponentRecipeSnapshot(foundation, "album-v1.0", Used(foundation))
                : new ComponentRecipeSnapshot(weapon, "album-v1.0", Used(weapon));
            var saved = new ComponentLibraryEntryData(new ComponentLibraryEntry("library.saved", "Saved component", 1, snapshot));
            var part = foundationKind ? saved.Snapshot.Foundation.Frame : saved.Snapshot.Weapon.Mechanism;
            if (identity == "id") part.Id = "component.unknown.saved";
            else part.Revision = 999;
            var source = WorkshopProfile.CreateNew(catalog).ToData();
            source.Library = new[] { new ComponentLibraryEntryData { Version = 0 }, saved };
            var result = ProfileMigrationV8.Migrate(RoundTrip(source), catalog);
            Assert.That(result.Success, Is.False);
            Assert.That(result.IsUnsupported, Is.True, "A malformed library sibling must not hide a later unknown embedded identity.");
        }

        [TestCase("emptyId")] [TestCase("zeroRevision")] [TestCase("nullDefinitions")]
        public void ProfilePreflight_MalformedPartReferencesRemainRecoverable(string malformed)
        {
            var catalog = AssemblyResolverFixture.Catalog();
            var source = WorkshopProfile.CreateNew(catalog).ToData();
            var snapshot = source.Entries[1].Snapshot;
            if (malformed == "emptyId") snapshot.Recipe.Foundation.Frame.Id = "";
            else if (malformed == "zeroRevision") snapshot.Recipe.Foundation.Frame.Revision = 0;
            else snapshot.Definitions = null;
            Assert.DoesNotThrow(() => AssemblyProfileDataCopy.GuardFuture(source), "Preflight must defer malformed fields to structural reading.");
            var result = ProfileMigrationV8.Migrate(source, catalog);
            Assert.That(result.Success, Is.False);
            Assert.That(result.IsUnsupported, Is.False, "Missing fields are corruption, not evidence of an unknown embedded identity.");
        }

        static void AddLateFuture(WorkshopProfileData source, string collection)
        {
            switch (collection)
            {
                case "entries": source.Entries[1].Version = 999; break;
                case "library": source.Library = new[] { new ComponentLibraryEntryData(), new ComponentLibraryEntryData { Version = 999 } }; break;
                case "rewards": source.Rewards = new[] { new RunRewardReceiptData(), new RunRewardReceiptData { Version = 999 } }; break;
                case "operations": source.Operations = new[] { new OperationReceiptData(), new OperationReceiptData { Version = 999 } }; break;
                case "legacyAlbum": source.Album[1].ContentMetadataVersion = 999; break;
            }
        }

        [Test] public void MissingNestedDtoMetadata_IsMalformed()
        {
            var dto = new RecipeSnapshotData(TowerSnapshot()) { Version = 0 };
            Assert.Catch<ArgumentException>(() => dto.CreateSnapshot());
            dto.Version = 1; dto.Definitions[0].Modifiers[0] = null;
            Assert.Catch<ArgumentException>(() => dto.CreateSnapshot());
        }

        [Test] public void LibraryComponent_RejectsPopulatedOppositeVariant()
        {
            var foundation = AssemblyResolverFixture.Foundation();
            var dto = new ComponentRecipeSnapshotData(new ComponentRecipeSnapshot(foundation, "album-v1.0", Used(foundation)));
            dto.Weapon = new WeaponRecipeInput { Mechanism = new PartRefInput { Id = "core.gun", Revision = 1 } };
            Assert.Catch<ArgumentException>(() => dto.CreateSnapshot());
        }

        [Test] public void LegacyEntry_PreservesHistoricalFreeformRuleVersionAndGeometry()
        {
            var stroke = new DrawingStroke(new[] { new DrawingPoint(-.3f, -.3f), new DrawingPoint(.3f, -.3f),
                new DrawingPoint(.3f, .3f), new DrawingPoint(-.3f, .3f), new DrawingPoint(-.3f, -.3f) });
            var drawing = new DrawingSnapshot(new[] { new DrawingPart(DrawingBindings.Base, new[] { stroke }),
                new DrawingPart(DrawingBindings.Mount0, new[] { stroke }) });
            var blueprint = new TowerBlueprint("legacy.freeform", "Historical freeform", ChassisKind.Standard, StructureKind.Gun,
                drawing: drawing, origin: DrawingOrigin.Freeform, freeformRuleVersion: 1);
            var restored = RoundTrip(new AlbumEntryData(new AlbumEntry(blueprint.Id, blueprint.Name, 1, blueprint))).CreateSnapshot();
            Assert.That(restored.LegacyBlueprint.FreeformRuleVersion, Is.EqualTo(1));
            Assert.That(restored.LegacyBlueprint.Drawing.GeometryHash, Is.EqualTo(drawing.GeometryHash));
        }

        [Test] public void ReceiptDtos_RoundTripGuidStringsAndProgress()
        {
            string runId = Guid.NewGuid().ToString("N"), operationId = Guid.NewGuid().ToString("N");
            var reward = RoundTrip(new RunRewardReceiptData { RunId = runId, Waves = 8, VictoryBonus = true });
            var operation = RoundTrip(new OperationReceiptData { OperationId = operationId, Type = "save", ResultId = "entry.saved" });
            Assert.That(reward.RunId, Is.EqualTo(runId)); Assert.That(reward.Waves, Is.EqualTo(8)); Assert.That(reward.VictoryBonus, Is.True);
            Assert.That(operation.OperationId, Is.EqualTo(operationId)); Assert.That(operation.Type, Is.EqualTo("save"));
            Assert.That(operation.ResultId, Is.EqualTo("entry.saved"));
        }
    }
}
