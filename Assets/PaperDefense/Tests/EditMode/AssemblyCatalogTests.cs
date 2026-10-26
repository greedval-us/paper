using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AssemblyCatalogTests
    {
        const string Path = "Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset";

        [Test]
        public void ImportV1_HasCompleteFreeStarterSet()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(Path).CreateSnapshot();
            Assert.That(catalog.BalanceRevision, Is.EqualTo("album-v1.0"));
            Assert.That(catalog.Definitions.Count, Is.EqualTo(54));
            Assert.That(catalog.Starters.Count, Is.EqualTo(8));
            Assert.That(catalog.Definitions.Count(x => x.Category <= ComponentCategory.Turn), Is.EqualTo(14));
            Assert.That(catalog.Definitions.Count(x => x.Category == ComponentCategory.Mechanism), Is.EqualTo(7));
            Assert.That(catalog.Definitions.Count(x => x.Category >= ComponentCategory.Head && x.Category <= ComponentCategory.Trigger), Is.EqualTo(18));
            Assert.That(catalog.Definitions.Count(x => x.Category >= ComponentCategory.Site), Is.EqualTo(15));
            Assert.That(catalog.Starters.Select(x => x.Id), Is.EqualTo(new[] {
                "album.guard", "album.double", "album.piercer", "album.mortar",
                "album.wall", "album.slow", "album.repair", "album.generator" }));
            foreach (var entry in catalog.Starters)
                foreach (var part in entry.Snapshot.Recipe.Parts)
                {
                    Assert.That(catalog.TryGet(part.Id, part.Revision, out var definition), Is.True, part.Id);
                    Assert.That(definition.UnlockGold, Is.Zero, part.Id);
                }
            Assert.That(catalog.TryGet("core.gun", 2, out _), Is.False);
            Assert.That(catalog.TryGet("missing", 1, out _), Is.False);
        }

        [Test]
        public void PublishedSnapshot_OwnsNestedDefinitionsAndRecipeArrays()
        {
            var source = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(Path);
            var candidate = UnityEngine.Object.Instantiate(source);
            try
            {
                var snapshot = candidate.CreateSnapshot();
                var firstId = candidate.catalog.Definitions[0].Id;
                candidate.catalog.Definitions[0].Id = "changed";
                candidate.catalog.Definitions[0].Modifiers[0].Value = 999;
                candidate.catalog.Starters[1].Mounts[0].Weapon.Mechanism.Id = "changed";
                candidate.catalog.Starters = Array.Empty<AssemblyStarterInput>();
                Assert.That(snapshot.TryGet(firstId, 1, out var frame), Is.True);
                Assert.That(frame.Get(AssemblyStat.Cost, ModifierOperation.Add), Is.EqualTo(16));
                Assert.That(snapshot.Starters.Count, Is.EqualTo(8));
                Assert.That(snapshot.Starters[1].Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id, Is.EqualTo("core.gun"));
                Assert.That(snapshot.Definitions, Is.Not.InstanceOf<ComponentDefinition[]>());
            }
            finally { UnityEngine.Object.DestroyImmediate(candidate); }
        }

        [Test]
        public void RecipeSnapshot_CopiesDefinitionArrayAndKeepsExactRevisions()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(Path).CreateSnapshot();
            var starter = catalog.Starters[0];
            var definitions = starter.Snapshot.Definitions.ToArray();
            var snapshot = new RecipeSnapshot(starter.Snapshot.Recipe, catalog.BalanceRevision, definitions);
            definitions[0] = null;
            Assert.That(snapshot.Definitions[0], Is.Not.Null);
            Assert.That(snapshot.Definitions.All(x => x.Revision == 1), Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void StarterInput_RejectsPopulatedAlternatePayload(bool tower)
        {
            var source = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(Path);
            var candidate = UnityEngine.Object.Instantiate(source);
            try
            {
                var entry = candidate.catalog.Starters[tower ? 0 : 4];
                if (tower)
                    entry.Structure = new StructureRecipeInput { Function = new PartRefInput { Id = "function.wall" } };
                else
                    entry.Foundation = new FoundationRecipeInput { Hull = new PartRefInput { Id = "hull.standard" } };
                var error = Assert.Throws<AssemblyCatalogException>(() => entry.CreateRecipe());
                Assert.That(error.Diagnostics.Select(x => x.Code), Does.Contain("assembly.recipe.payload"));
            }
            finally { UnityEngine.Object.DestroyImmediate(candidate); }
        }
    }
}
