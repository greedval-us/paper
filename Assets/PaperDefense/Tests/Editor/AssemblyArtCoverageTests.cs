using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AssemblyArtCoverageTests
    {
        [Serializable] sealed class VisualFragment
        {
            public string spriteId, socketId, motion;
            public int layer;
            public Vector2 canvas, attach;
            public NotebookMountLayout.PixelRect trim;
        }
        [Serializable] sealed class VisualPart
        {
            public string partId, assetRevision, neutral;
            public int revision;
            public VisualFragment[] fragments;
        }
        [Serializable] sealed class VisualManifest
        {
            public string revision;
            public VisualPart[] visualParts;
        }
        static ComponentCatalog Catalog() => AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(
            "Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
        static VisualManifest Manifest() => JsonUtility.FromJson<VisualManifest>(
            Resources.Load<TextAsset>("Art/NotebookMounts").text);

        [Test] public void AllDefinitions_HaveVisualMapping()
        {
            var catalog = Catalog(); var visuals = Manifest().visualParts ?? Array.Empty<VisualPart>();
            Assert.That(catalog.Definitions.Count, Is.EqualTo(54), "Production catalog, not a reduced fixture.");
            var missingDefinitionIds = catalog.Definitions.Where(d => !visuals.Any(v =>
                v.partId == d.Id && v.revision == d.Revision &&
                (v.fragments != null && v.fragments.Length > 0 || !string.IsNullOrWhiteSpace(v.neutral))))
                .Select(d => d.Id).ToArray();
            Assert.That(missingDefinitionIds, Is.Empty);
            Assert.That(visuals.Select(v => v.partId + "@" + v.revision).Distinct().Count(), Is.EqualTo(54));
            foreach (var definition in catalog.Definitions)
                Assert.That(AssemblyVisualComposer.Fits.TryGetPart(definition.Id, definition.Revision, out var part), Is.True, definition.Id);
        }

        [Test] public void Bindings_HaveSpritesStableSocketsAndKnownRevisions()
        {
            var manifest = Manifest();
            Assert.That(manifest.visualParts, Is.Not.Null);
            Assert.That(manifest.visualParts, Has.Length.EqualTo(54));
            foreach (var part in manifest.visualParts)
            {
                Assert.That(part.assetRevision, Is.EqualTo(manifest.revision), part.partId);
                if (!string.IsNullOrEmpty(part.neutral))
                {
                    Assert.That(part.partId, Is.EqualTo("armor.none").Or.EqualTo("utility.none"));
                    Assert.That(part.fragments, Is.Empty);
                    continue;
                }
                Assert.That(part.fragments, Is.Not.Empty, part.partId);
                Assert.That(part.fragments.Select(f => f.socketId).Distinct().Count(), Is.EqualTo(part.fragments.Length), part.partId);
                foreach (var fragment in part.fragments)
                {
                    Assert.That(NotebookSpriteCatalog.TryGet(fragment.spriteId, out var sprite), Is.True, fragment.spriteId);
                    Assert.That(sprite.rect.width, Is.GreaterThan(0));
                    Assert.That(fragment.socketId, Is.Not.Null.And.Not.Empty);
                    Assert.That(fragment.canvas.x, Is.GreaterThan(0)); Assert.That(fragment.canvas.y, Is.GreaterThan(0));
                    Assert.That(fragment.trim, Is.Not.Null);
                    Assert.That(fragment.trim.width, Is.GreaterThan(0)); Assert.That(fragment.trim.height, Is.GreaterThan(0));
                    Assert.That(fragment.attach.x, Is.InRange(0, fragment.canvas.x));
                    Assert.That(fragment.attach.y, Is.InRange(0, fragment.canvas.y));
                    Assert.That(fragment.motion, Is.EqualTo("fixed").Or.EqualTo("rotating").Or.EqualTo("recoiling"));
                }
            }
        }

        [Test] public void EveryStarter_ComposesOrderedPartsAndRealMuzzles()
        {
            var catalog = Catalog();
            foreach (var entry in catalog.Starters)
            {
                Assert.That(AssemblyFitRules.Validate(entry.Snapshot.Recipe, AssemblyVisualComposer.Fits), Is.Empty, entry.Id);
                var result = AssemblyVisualComposer.Compose(entry.Snapshot, new AssemblyVisualPose());
                Assert.That(result.Fragments, Is.Not.Empty, entry.Id);
                Assert.That(result.Bounds.width, Is.GreaterThan(0)); Assert.That(result.Bounds.height, Is.GreaterThan(0));
                Assert.That(result.Fragments.Select(f => f.Layer), Is.Ordered, entry.Id);
                if (entry.Snapshot.Recipe.Tower == null) Assert.That(result.Anchors, Is.Empty);
                else
                {
                    Assert.That(result.Anchors.Count, Is.EqualTo(entry.Snapshot.Recipe.Tower.Mounts.Count));
                    foreach (var anchor in result.Anchors)
                        Assert.That(Vector2.Distance(anchor.Muzzle, anchor.RotationCenter), Is.GreaterThan(.01f));
                }
                var repeat = AssemblyVisualComposer.Compose(entry.Snapshot, new AssemblyVisualPose());
                Assert.That(repeat.Fragments.Select(f => (f.PartId, f.SpriteId, f.SlotIndex, f.Layer, f.Origin, f.Scale)),
                    Is.EqualTo(result.Fragments.Select(f => (f.PartId, f.SpriteId, f.SlotIndex, f.Layer, f.Origin, f.Scale))));
            }
        }

        [Test] public void FourFramesAndSevenFamilies_CertifyIndependentBoundaryPosesIncludingThreeBarrels()
        {
            var catalog = Catalog();
            foreach (var frame in catalog.Definitions.Where(d => d.Category == ComponentCategory.Frame))
            foreach (var core in catalog.Definitions.Where(d => d.Category == ComponentCategory.Mechanism))
            {
                AssemblyVisualComposer.Fits.TryGetFrame(frame.Id, frame.Revision, out var fitFrame);
                Assert.That(fitFrame, Is.Not.Null, frame.Id);
                var foundation = new FoundationRecipe(new PartRef(frame.Id, frame.Revision), Ref("hull.standard"), Ref("armor.none"), Ref("feed.standard"), Ref("turn.standard"));
                var weapon = new WeaponRecipe(Ref(core.Id), Ref("head." + core.Id.Substring(5)), Ref("sight.iron"), Ref("magazine.standard"), Ref("trigger.standard"));
                var recipe = new AssemblyRecipe(new TowerRecipe(foundation, fitFrame.Mounts.Select(m => new WeaponMountRecipe(m.SlotIndex, weapon))));
                Assert.That(AssemblyFitRules.Validate(recipe, AssemblyVisualComposer.Fits), Is.Empty, frame.Id + "/" + core.Id);
                var snapshot = new RecipeSnapshot(recipe, catalog.BalanceRevision, catalog.Definitions);
                AssemblyVisualComposer.Fits.TryGetWeapon(core.Id, core.Revision, out var fitWeapon);
                foreach (float angle in new[] { fitWeapon.MinRadians, 0, fitWeapon.MaxRadians })
                foreach (float recoil in new[] { 0, -fitWeapon.RecoilLimit })
                {
                    var composition = AssemblyVisualComposer.Compose(snapshot, new AssemblyVisualPose(fitFrame.Mounts.Select(m =>
                        new AssemblyMountPose(m.SlotIndex, m.SlotIndex % 2 == 0 ? angle : -angle, recoil))));
                    Assert.That(composition.Anchors.Count, Is.EqualTo(fitFrame.Mounts.Count));
                    Assert.That(composition.Bounds.Contains(composition.Anchors[0].Muzzle), Is.True);
                    Assert.That(composition.Fragments.All(f => f.Scale > 0), Is.True);
                }
            }
        }

        [Test] public void PartInspection_UsesRealPartsAndLeavesOnlyNeutralOptionsEmpty()
        {
            foreach (var definition in Catalog().Definitions)
            {
                var reference = new PartRef(definition.Id, definition.Revision);
                Assert.That(AssemblyVisualComposer.HasVisualMapping(reference), Is.True, definition.Id);
                var composition = AssemblyVisualComposer.ComposePart(reference);
                if (definition.Id == "armor.none" || definition.Id == "utility.none") Assert.That(composition.Fragments, Is.Empty);
                else
                {
                    Assert.That(composition.Fragments, Is.Not.Empty, definition.Id);
                    Assert.That(composition.Fragments.All(f => f.PartId == definition.Id), Is.True);
                    Assert.That(composition.Bounds.width, Is.GreaterThan(0)); Assert.That(composition.Bounds.height, Is.GreaterThan(0));
                }
            }
            Assert.Throws<ArgumentException>(() => AssemblyVisualComposer.ComposePart(Ref("head.gun", 99)));
        }

        [Test] public void FamilyCards_ShowCompleteWeaponsAndFoundationCardsKeepTheirEmptyMounts()
        {
            var catalog = Catalog();
            foreach (var core in catalog.Definitions.Where(d => d.Category == ComponentCategory.Mechanism))
            {
                var recipe = new WeaponRecipe(Ref(core.Id), Ref("head." + core.Id.Substring(5)), Ref("sight.iron"), Ref("magazine.standard"), Ref("trigger.standard"));
                var composition = AssemblyVisualComposer.ComposeWeapon(recipe);
                foreach (var reference in recipe.Parts) Assert.That(composition.Fragments.Any(f => f.PartId == reference.Id), Is.True, reference.Id);
                Assert.That(composition.Fragments.Any(f => f.PartId.StartsWith("frame.", StringComparison.Ordinal)), Is.False);
                Assert.That(composition.Anchors.Single().RotationCenter, Is.EqualTo(Vector2.zero));
            }
            foreach (var frame in catalog.Definitions.Where(d => d.Category == ComponentCategory.Frame))
            {
                var foundation = new FoundationRecipe(Ref(frame.Id), Ref("hull.standard"), Ref("armor.none"), Ref("feed.standard"), Ref("turn.standard"));
                var composition = AssemblyVisualComposer.ComposeFoundation(foundation);
                AssemblyVisualComposer.Fits.TryGetFrame(frame.Id, frame.Revision, out var fit);
                Assert.That(composition.Fragments.Where(f => f.PartId == "turn.standard").Select(f => f.SlotIndex).Distinct().Count(), Is.EqualTo(fit.Mounts.Count));
                Assert.That(composition.Fragments.Any(f => f.PartId.StartsWith("core.", StringComparison.Ordinal)), Is.False);
                Assert.That(composition.Anchors, Is.Empty);
            }
        }

        [Test] public void FrameSpecificSolidRegions_MatchTheActualRenderedBaseAndFrontWall()
        {
            foreach (var frame in Catalog().Definitions.Where(d => d.Category == ComponentCategory.Frame))
            {
                var foundation = new FoundationRecipe(Ref(frame.Id), Ref("hull.standard"), Ref("armor.none"), Ref("feed.standard"), Ref("turn.standard"));
                var drawing = AssemblyVisualComposer.ComposeFoundation(foundation);
                AssemblyVisualComposer.Fits.TryGetFrame(frame.Id, frame.Revision, out var fit);
                var artwork = drawing.Fragments.Where(f => f.PartId == frame.Id || f.PartId == "hull.standard" && f.Layer == 14).ToArray();
                var solids = fit.Keepouts.Where(k => k.PartId == frame.Id || k.PartId == "hull.standard").ToArray();
                Assert.That(solids.Length, Is.EqualTo(artwork.Length), frame.Id);
                foreach (var fragment in artwork)
                {
                    var r = fragment.Region;
                    var points = new[] { r.min, new Vector2(r.xMax, r.yMin), r.max, new Vector2(r.xMin, r.yMax) }.Select(fragment.Transform).ToArray();
                    float x = points.Min(p => p.x), y = points.Min(p => p.y), width = points.Max(p => p.x) - x, height = points.Max(p => p.y) - y;
                    Assert.That(solids.Any(k => k.PartId == fragment.PartId && Mathf.Abs(k.X - x) < .000001f && Mathf.Abs(k.Y - y) < .000001f
                        && Mathf.Abs(k.Width - width) < .000001f && Mathf.Abs(k.Height - height) < .000001f), Is.True,
                        frame.Id + "/" + fragment.PartId + " must not inherit another frame's wall rectangle.");
                }
            }
        }
        static PartRef Ref(string id, int revision = 1) => new PartRef(id, revision);

        [Test] public void Reimport_PreservesGuidsSourceTransformsAndUnchangedPixels()
        {
            string[] paths = { PaperDefense.Editor.NotebookSpriteImportTools.AtlasPath,
                PaperDefense.Editor.NotebookSpriteImportTools.CatalogPath, PaperDefense.Editor.NotebookSpriteImportTools.MountsPath };
            var guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            string source = File.ReadAllText(PaperDefense.Editor.NotebookSpriteImportTools.AssemblyPath);
            string runtime = File.ReadAllText(paths[2]); var atlas = File.ReadAllBytes(paths[0]);
            PaperDefense.Editor.NotebookSpriteImportTools.Rebuild();
            Assert.That(paths.Select(AssetDatabase.AssetPathToGUID), Is.EqualTo(guids));
            Assert.That(File.ReadAllText(PaperDefense.Editor.NotebookSpriteImportTools.AssemblyPath), Is.EqualTo(source));
            Assert.That(File.ReadAllText(paths[2]).Replace("\r\n", "\n"), Is.EqualTo(runtime.Replace("\r\n", "\n")));
            Assert.That(File.ReadAllBytes(paths[0]), Is.EqualTo(atlas));
        }
    }
}
