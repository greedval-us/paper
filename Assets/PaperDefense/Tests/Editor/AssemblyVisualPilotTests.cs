using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class AssemblyVisualPilotTests
    {
        public static RecipeSnapshot Pilot(bool armor = false)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>("Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
            var recipe = catalog.Starters.Single(e => e.Id == "album.double").Snapshot.Recipe;
            if (armor)
            {
                var f = recipe.Tower.Foundation;
                recipe = new AssemblyRecipe(new TowerRecipe(new FoundationRecipe(f.Frame, f.Hull, new PartRef("armor.plate", 1), f.Feed, f.Turn), recipe.Tower.Mounts));
            }
            return new RecipeSnapshot(recipe, catalog.BalanceRevision, catalog.Definitions);
        }

        [Test] public void FreeDoubleAndArmoredPilot_PassNumericalAndPhysicalValidation()
        {
            foreach (bool armor in new[] { false, true })
            {
                var snapshot = Pilot(armor);
                var catalog = new ComponentCatalog(snapshot.BalanceRevision, snapshot.Definitions);
                Assert.That(AssemblyResolver.Resolve(snapshot.Recipe, catalog, GameBalance.CreateDefault(), MaterialTier.Pencil, AssemblyVisualComposer.Fits).Valid, Is.True);
                Assert.That(AssemblyFitRules.Validate(snapshot.Recipe, AssemblyVisualComposer.Fits), Is.Empty);
                var result = AssemblyVisualComposer.Compose(snapshot, new AssemblyVisualPose());
                Assert.That(result.Anchors.Select(a => a.SlotIndex), Is.EqualTo(new[] { 0, 1 }));
                foreach (string id in new[] { "frame.standard", "hull.standard", "feed.standard", "turn.standard", "core.gun", "head.gun", "sight.iron", "magazine.standard", "trigger.standard" })
                    Assert.That(result.Fragments.Any(f => f.PartId == id), Is.True, id);
                Assert.That(result.Fragments.Count(f => f.PartId == "hull.standard"), Is.EqualTo(2), "Separate front and back hull layers");
                Assert.That(result.Fragments.Any(f => f.PartId == "armor.plate"), Is.EqualTo(armor));
                Assert.That(result.Fragments.Select(f => f.Layer), Is.Ordered);
            }
        }

        [Test] public void CertifiedBoundaryPoses_UseTheSameOccupiedCornersAsCore()
        {
            AssemblyVisualComposer.Fits.TryGetWeapon("core.gun", 1, out var fit);
            float radius = AssemblyFitRules.SweptRadius(fit);
            var source = JsonUtility.FromJson<AssemblyVisualComposer.Manifest>(System.IO.File.ReadAllText(PaperDefense.Editor.NotebookSpriteImportTools.AssemblyPath));
            var split = source.pilots.Single();
            foreach (float a in new[] { -Mathf.PI, -Mathf.PI / 2, 0, Mathf.PI / 2, Mathf.PI })
            foreach (float b in new[] { -Mathf.PI, -Mathf.PI / 2, 0, Mathf.PI / 2, Mathf.PI })
            foreach (float recoil in new[] { 0, -.085f })
            {
                var composition = AssemblyVisualComposer.Compose(Pilot(true), new AssemblyVisualPose(new[] { new AssemblyMountPose(0, a, recoil), new AssemblyMountPose(1, b, recoil) }));
                foreach (var f in composition.Fragments.Where(f => f.SlotIndex >= 0))
                {
                    if (split.fragments.Single(s => s.spriteId == f.SpriteId).group == 0) continue;
                    var pivot = composition.Anchors.Single(p => p.SlotIndex == f.SlotIndex).RotationCenter;
                    foreach (var p in new[] { f.Trim.min, new Vector2(f.Trim.xMax, f.Trim.yMin), f.Trim.max, new Vector2(f.Trim.xMin, f.Trim.yMax) })
                        Assert.That(Vector2.Distance(f.Transform(p), pivot), Is.LessThanOrEqualTo(radius + .000001f), f.SpriteId);
                }
            }
        }

        [Test] public void RecipeAndExistingAlbumAdapter_ProduceIdenticalGeometry()
        {
            var snapshot = Pilot();
            var blueprint = new TowerBlueprint("same-pilot", "Same pilot", ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun);
            using (var recipe = new VertexHelper())
            using (var album = new VertexHelper())
            {
                NotebookTowerComposition.DrawRecipeUI(recipe, snapshot, new AssemblyVisualPose(), Vector2.zero, 1);
                NotebookTowerComposition.DrawUI(album, blueprint, Vector2.zero, 1);
                Assert.That(recipe.currentVertCount, Is.EqualTo(album.currentVertCount));
                var left = new UIVertex(); var right = new UIVertex();
                for (int i = 0; i < recipe.currentVertCount; i++)
                {
                    recipe.PopulateUIVertex(ref left, i); album.PopulateUIVertex(ref right, i);
                    Assert.That(Vector3.Distance(left.position, right.position), Is.LessThan(.000001f));
                    Assert.That(Vector4.Distance(left.uv1, right.uv1), Is.LessThan(.000001f));
                }
            }
        }

        [Test] public void IndependentAnglesAndRecoil_AreKeyedByStableSlot()
        {
            var rest = AssemblyVisualComposer.Compose(Pilot(), new AssemblyVisualPose());
            var pose = new AssemblyVisualPose(new[] { new AssemblyMountPose(1, -Mathf.PI / 2, -.085f), new AssemblyMountPose(0, Mathf.PI / 2, 0) });
            var moved = AssemblyVisualComposer.Compose(Pilot(), pose);
            for (int slot = 0; slot < 2; slot++)
            {
                var a = rest.Anchors.Single(p => p.SlotIndex == slot);
                var b = moved.Anchors.Single(p => p.SlotIndex == slot);
                float angle = slot == 0 ? Mathf.PI / 2 : -Mathf.PI / 2;
                var direction = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
                float length = Vector2.Distance(a.Muzzle, a.RotationCenter) + (slot == 1 ? -.085f : 0);
                Assert.That(Vector2.Distance(b.Muzzle, a.RotationCenter + direction * length), Is.LessThan(.00001f));
                Assert.That(b.Attach, Is.EqualTo(a.Attach));
            }
        }

        [Test] public void UnknownVisualVersionSocketAndUncoveredPart_AreRejected()
        {
            Assert.Throws<FormatException>(() => AssemblyVisualComposer.Compose(Pilot(), new AssemblyVisualPose(visualRevision: "unknown-revision")));
            string source = System.IO.File.ReadAllText(PaperDefense.Editor.NotebookSpriteImportTools.AssemblyPath);
            Assert.Throws<FormatException>(() => AssemblyVisualComposer.BuildRuntimeJson(source.Replace("gun.standard", "unknown.socket"), null));
            var recipe = Pilot().Recipe;
            var f = recipe.Tower.Foundation;
            var unknown = new AssemblyRecipe(new TowerRecipe(new FoundationRecipe(new PartRef("frame.standard", 99), f.Hull, f.Armor, f.Feed, f.Turn), recipe.Tower.Mounts));
            Assert.That(AssemblyFitRules.Validate(unknown, AssemblyVisualComposer.Fits).Count, Is.GreaterThan(0));
        }

        [Test] public void ImportRejectsSpriteBoundsWhichDivergeFromCertifiedGeometry()
        {
            string source = System.IO.File.ReadAllText(PaperDefense.Editor.NotebookSpriteImportTools.AssemblyPath);
            var bounds = new System.Collections.Generic.Dictionary<string, Rect> { { "chassis.standard", new Rect(-.34468f, -.28f, .8f, .62f) } };
            Assert.Throws<FormatException>(() => AssemblyVisualComposer.BuildRuntimeJson(source, null, bounds));
        }

        [Test] public void ImportRejectsArmorMovedIntoTheCertifiedSweep()
        {
            string source = System.IO.File.ReadAllText(PaperDefense.Editor.NotebookSpriteImportTools.AssemblyPath);
            var manifest = JsonUtility.FromJson<AssemblyVisualComposer.Manifest>(source);
            manifest.foundationParts.Single(p => p.partId == "armor.plate").scale *= 10;
            var error = Assert.Throws<AssemblyCatalogException>(() => AssemblyVisualComposer.BuildRuntimeJson(JsonUtility.ToJson(manifest), null));
            Assert.That(error.Diagnostics.Select(d => d.Code), Does.Contain("assembly.clearance"));
        }

        [Test] public void MixedLegacyTower_RetainsEveryDeclaredGunLayer()
        {
            var blueprint = new TowerBlueprint("mixed", "Mixed", ChassisKind.Standard, StructureKind.Gun, StructureKind.Piercer);
            var mesh = new Mesh();
            try
            {
                var pen = new MeshPen(mesh);
                NotebookTowerComposition.DrawMounts(pen, blueprint, Vector2.zero, MaterialTier.Pencil, InkColor.None, default, false, null, 0, 0);
                pen.Upload();
                Assert.That(mesh.vertexCount, Is.EqualTo(36), "Eight pilot fragments and the unchanged whole piercer sprite");
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test] public void TrimAndPacking_PreserveAttachAndMuzzleWithinHalfSourcePixel()
        {
            var composition = AssemblyVisualComposer.Compose(Pilot(), new AssemblyVisualPose());
            foreach (var anchor in composition.Anchors)
            {
                var mount = NotebookMountLayout.Resolve(new TowerBlueprint("fit", "Fit", ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun), anchor.SlotIndex);
                Assert.That(Vector2.Distance(mount.Socket, anchor.Attach) / mount.Scale, Is.LessThan(.5f));
                Assert.That(Vector2.Distance(mount.Transform(mount.Muzzle), anchor.Muzzle) / mount.Scale, Is.LessThan(.5f));
                var barrel = composition.Fragments.Single(f => f.SlotIndex == anchor.SlotIndex && f.SpriteId == "part.gun.barrels");
                Assert.That(Vector2.Distance(barrel.Transform(new Vector2(444, 460)), anchor.Muzzle) / barrel.Scale, Is.LessThan(.5f));
                Assert.That(NotebookSpriteCatalog.TryGet(barrel.SpriteId, out var sprite), Is.True);
                Assert.That(sprite.rect.width, Is.GreaterThan(0));
                foreach (var trim in new[] { barrel.Trim, new Rect(20, 30, 480, 490) })
                {
                    var mesh = new Mesh();
                    try
                    {
                        var pen = new MeshPen(mesh);
                        NotebookSpriteCatalog.TryDrawFrame(pen, barrel.SpriteId, trim, trim, barrel.Origin, barrel.Scale, barrel.Radians, 1, Color.white);
                        pen.Upload();
                        var v = mesh.vertices;
                        Vector2 renderedMuzzle = (Vector2)v[0] + (Vector2)(v[1] - v[0]) * ((444 - trim.x) / trim.width)
                            + (Vector2)(v[3] - v[0]) * ((460 - trim.y) / trim.height);
                        Assert.That(Vector2.Distance(renderedMuzzle, anchor.Muzzle) / barrel.Scale, Is.LessThan(.5f));
                        var uv = new System.Collections.Generic.List<Vector2>(); mesh.GetUVs(1, uv);
                        Assert.That(uv[0].x, Is.EqualTo(sprite.rect.xMin / sprite.texture.width).Within(.000001f));
                        Assert.That(uv[2].y, Is.EqualTo(sprite.rect.yMax / sprite.texture.height).Within(.000001f));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(mesh); }
                }
            }
        }
    }
}
