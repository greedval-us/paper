using System;
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
    public sealed class AssemblyArtSilhouetteTests
    {
        static ComponentCatalog Catalog() => AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(
            "Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
        static PartRef P(string id) => new PartRef(id, 1);
        static Rect Bounds(AssemblyVisualFragment f)
        {
            var r = f.Region; var points = new[] { r.min, new Vector2(r.xMax, r.yMin), r.max, new Vector2(r.xMin, r.yMax) }.Select(f.Transform).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
        static byte Alpha(AssemblyVisualFragment f, Vector2 pixel, Color32[] pixels, int width)
        {
            Assert.That(f.Region.Contains(pixel) && f.Trim.Contains(pixel), Is.True, "Contact must lie on the rendered source region.");
            Assert.That(NotebookSpriteCatalog.TryGet(f.SpriteId, out var sprite), Is.True);
            int x = (int)sprite.rect.x + Mathf.FloorToInt((pixel.x - f.Trim.x) / f.Trim.width * sprite.rect.width);
            int y = (int)sprite.rect.y + Mathf.FloorToInt((pixel.y - f.Trim.y) / f.Trim.height * sprite.rect.height);
            return pixels[y * width + x].a;
        }
        static byte AlphaAtWorld(AssemblyVisualFragment f, Vector2 point, Color32[] pixels, int width)
        {
            var pixel = NotebookMountLayout.Rotate(point - f.Origin, -f.Radians) / f.Scale;
            if (!f.Region.Contains(pixel) || !f.Trim.Contains(pixel)) return 0;
            return Alpha(f, pixel, pixels, width);
        }

        [Test] public void SevenModernWeapons_RemainReadableAboveTheirLowStandardFoundation()
        {
            var catalog = Catalog();
            foreach (var core in catalog.Definitions.Where(p => p.Category == ComponentCategory.Mechanism))
            {
                var foundation = new FoundationRecipe(P("frame.standard"), P("hull.standard"), P("armor.none"), P("feed.standard"), P("turn.standard"));
                var weapon = new WeaponRecipe(P(core.Id), P("head." + core.Id.Substring(5)), P("sight.iron"), P("magazine.standard"), P("trigger.standard"));
                var recipe = new AssemblyRecipe(new TowerRecipe(foundation, new[] { new WeaponMountRecipe(0, weapon) }));
                var drawing = AssemblyVisualComposer.Compose(new RecipeSnapshot(recipe, catalog.BalanceRevision, catalog.Definitions), new AssemblyVisualPose());
                var body = drawing.Fragments.Where(f => f.PartId == "frame.standard" || f.PartId == "hull.standard").Select(Bounds).ToArray();
                var moving = drawing.Fragments.Where(f => f.PartId == weapon.Mechanism.Id || f.PartId == weapon.Head.Id).Select(Bounds).ToArray();
                float baseHeight = body.Max(r => r.yMax) - body.Min(r => r.yMin);
                float weaponHeight = moving.Max(r => r.yMax) - moving.Min(r => r.yMin);
                Assert.That(weaponHeight / baseHeight, Is.GreaterThanOrEqualTo(.65f), core.Id + ": actual moving ink must remain readable against the base; fixed support height does not count.");
            }
        }

        [TestCase("album.slow")]
        [TestCase("album.repair")]
        public void Supports_HaveOneDominantOpaqueSilhouette(string starterId)
        {
            var catalog = Catalog(); var starter = catalog.Starters.Single(e => e.Id == starterId);
            var drawing = AssemblyVisualComposer.Compose(starter.Snapshot, new AssemblyVisualPose());
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(atlas.LoadImage(File.ReadAllBytes(PaperDefense.Editor.NotebookSpriteImportTools.AtlasPath)), Is.True);
                var pixels = atlas.GetPixels32(); const int size = 512; var mask = new bool[size * size]; var box = drawing.Bounds;
                foreach (var f in drawing.Fragments)
                {
                    Assert.That(NotebookSpriteCatalog.TryGet(f.SpriteId, out var sprite), Is.True);
                    var bounds = Bounds(f); int left = Mathf.Max(0, Mathf.FloorToInt((bounds.xMin - box.xMin) / box.width * size));
                    int right = Mathf.Min(size - 1, Mathf.CeilToInt((bounds.xMax - box.xMin) / box.width * size));
                    int bottom = Mathf.Max(0, Mathf.FloorToInt((bounds.yMin - box.yMin) / box.height * size));
                    int top = Mathf.Min(size - 1, Mathf.CeilToInt((bounds.yMax - box.yMin) / box.height * size));
                    for (int y = bottom; y <= top; y++) for (int x = left; x <= right; x++)
                    {
                        var world = new Vector2(box.xMin + (x + .5f) / size * box.width, box.yMin + (y + .5f) / size * box.height);
                        var p = NotebookMountLayout.Rotate(world - f.Origin, -f.Radians) / f.Scale;
                        if (!f.Region.Contains(p) || !f.Trim.Contains(p)) continue;
                        int ax = (int)sprite.rect.x + Mathf.Clamp((int)((p.x - f.Trim.x) / f.Trim.width * sprite.rect.width), 0, (int)sprite.rect.width - 1);
                        int ay = (int)sprite.rect.y + Mathf.Clamp((int)((p.y - f.Trim.y) / f.Trim.height * sprite.rect.height), 0, (int)sprite.rect.height - 1);
                        if (pixels[ay * atlas.width + ax].a >= 32) mask[y * size + x] = true;
                    }
                }
                int total = mask.Count(p => p), largest = 0; var queue = new int[mask.Length];
                for (int start = 0; start < mask.Length; start++)
                {
                    if (!mask[start]) continue;
                    int head = 0, tail = 1; queue[0] = start; mask[start] = false;
                    while (head < tail)
                    {
                        int at = queue[head++], px = at % size, py = at / size;
                        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = px + dx, ny = py + dy;
                            if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                            int next = ny * size + nx; if (!mask[next]) continue;
                            mask[next] = false; queue[tail++] = next;
                        }
                    }
                    largest = Math.Max(largest, tail);
                }
                Assert.That(total, Is.GreaterThan(1000));
                Assert.That((float)largest / total, Is.GreaterThanOrEqualTo(.97f), starterId + ": opaque silhouette mass must not split into large isolated parts; this does not prove every joint.");
            }
            finally { UnityEngine.Object.DestroyImmediate(atlas); }
        }

        [Test] public void RepairRoofEaveAndFoot_UseTheirActualSharedPixelContacts()
        {
            var catalog = Catalog(); var drawing = AssemblyVisualComposer.Compose(catalog.Starters.Single(e => e.Id == "album.repair").Snapshot, new AssemblyVisualPose());
            var body = drawing.Fragments.First(f => f.PartId == "function.repair" && f.SpriteId == "part.full.function.repair");
            var roof = drawing.Fragments.Single(f => f.PartId == "shell.standard");
            var site = drawing.Fragments.Single(f => f.PartId == "site.repair");
            // Source landmarks: left front post under the eave, and its matching
            // roof edge on the two clean 627-pixel source canvases.
            Assert.That(Vector2.Distance(body.Transform(new Vector2(112, 340)), roof.Transform(new Vector2(89, 215))), Is.LessThan(.008f));
            Assert.That(Mathf.Abs(Bounds(body).yMin - Bounds(site).yMin), Is.LessThan(.008f), "The two drawn stone footprints seat at the same floor, rather than one floating above the other.");
        }

        [Test] public void ModernGunMechanism_UsesTheCleanSharedGearboxInsteadOfTheHistoricalMortarTube()
        {
            var gun = AssemblyVisualComposer.ComposeWeapon(new WeaponRecipe(P("core.gun"), P("head.gun"), P("sight.iron"), P("magazine.standard"), P("trigger.standard")));
            Assert.That(gun.Fragments.Where(f => f.PartId == "core.gun").Select(f => f.SpriteId),
                Is.EqualTo(new[] { "part.full.core.piercer" }), "The clean shared gearbox must leave the four barrels as the gun silhouette.");
        }

        [Test] public void ModernFourBarrelHead_DominatesItsMechanismRatherThanLookingLikeAMortar()
        {
            var gun = AssemblyVisualComposer.ComposeWeapon(new WeaponRecipe(P("core.gun"), P("head.gun"), P("sight.iron"), P("magazine.standard"), P("trigger.standard")));
            var core = gun.Fragments.Where(f => f.PartId == "core.gun").Select(Bounds).ToArray();
            var head = gun.Fragments.Where(f => f.PartId == "head.gun").Select(Bounds).ToArray();
            float coreHeight = core.Max(r => r.yMax) - core.Min(r => r.yMin);
            float headHeight = head.Max(r => r.yMax) - head.Min(r => r.yMin);
            Assert.That(headHeight / coreHeight, Is.GreaterThanOrEqualTo(1.5f));
        }

        [Test] public void ModernTurnHub_IsCompactAndSeatsItsActualFootOnTheAuthoredDisk()
        {
            var drawing = AssemblyVisualComposer.ComposeFoundation(new FoundationRecipe(P("frame.standard"), P("hull.standard"), P("armor.none"), P("feed.standard"), P("turn.standard")));
            Assert.That(drawing.Fragments.Any(f => f.SpriteId == "part.pilot.bearing"), Is.False, "The historical rear half-ring must not float above the modern empty mounts.");
            AssemblyVisualComposer.Fits.TryGetFrame("frame.standard", 1, out var frame);
            foreach (var socket in frame.Mounts)
            {
                var hub = drawing.Fragments.Single(f => f.PartId == "turn.standard" && f.SlotIndex == socket.SlotIndex);
                Assert.That(Bounds(hub).width, Is.LessThanOrEqualTo(.26f), "A turn hub must fit the real disk, rather than add an oversized empty dish.");
                // Actual bottom of the front bolt, rather than the surrounding plate.
                Assert.That(Vector2.Distance(hub.Transform(new Vector2(625.5f, 405.5f)), new Vector2(socket.Position.X, socket.Position.Y)), Is.LessThan(.000001f));
            }
        }

        [Test] public void ModernTurnHub_DoesNotCarryAClippedPatchOfTheBroadOldPlinth()
        {
            var drawing = AssemblyVisualComposer.ComposeFoundation(new FoundationRecipe(P("frame.standard"), P("hull.standard"), P("armor.none"), P("feed.standard"), P("turn.standard")));
            var hub = drawing.Fragments.First(f => f.PartId == "turn.standard");
            AssemblyVisualComposer.Fits.TryGetFrame("frame.standard", 1, out var frame);
            var seat = frame.Mounts.First(m => m.SlotIndex == hub.SlotIndex).Position;
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(atlas.LoadImage(File.ReadAllBytes(PaperDefense.Editor.NotebookSpriteImportTools.AtlasPath)), Is.True);
                var pixels = atlas.GetPixels32();
                foreach (float x in new[] { -.065f, .065f })
                foreach (float y in new[] { .002f, .005f })
                    Assert.That(AlphaAtWorld(hub, new Vector2(seat.X + x, seat.Y + y), pixels, atlas.width), Is.LessThan(8),
                        "Outside the narrow front bolt, the hub's lower corners must be transparent, not a clipped ivory floor rectangle.");
            }
            finally { UnityEngine.Object.DestroyImmediate(atlas); }
        }

        [Test] public void ModernReceiverFeet_SeatOnTheOpaqueFrontInnerLipRatherThanHoverOverTheOpening()
        {
            var catalog = Catalog();
            var feet = new[] { new Vector2(181, 9), new Vector2(181, 9), new Vector2(157, 40), new Vector2(163, 21), new Vector2(157, 27), new Vector2(157, 24), new Vector2(157, 33) };
            var families = new[] { "gun", "piercer", "mortar", "fan", "ricochet", "stapler", "blotter" };
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(atlas.LoadImage(File.ReadAllBytes(PaperDefense.Editor.NotebookSpriteImportTools.AtlasPath)), Is.True);
                var pixels = atlas.GetPixels32();
                for (int i = 0; i < families.Length; i++)
                {
                    var foundation = new FoundationRecipe(P("frame.standard"), P("hull.standard"), P("armor.none"), P("feed.standard"), P("turn.standard"));
                    var weapon = new WeaponRecipe(P("core." + families[i]), P("head." + families[i]), P("sight.iron"), P("magazine.standard"), P("trigger.standard"));
                    var snapshot = new RecipeSnapshot(new AssemblyRecipe(new TowerRecipe(foundation, new[] { new WeaponMountRecipe(0, weapon) })), catalog.BalanceRevision, catalog.Definitions);
                    var drawing = AssemblyVisualComposer.Compose(snapshot, new AssemblyVisualPose());
                    var receiver = drawing.Fragments.Single(f => f.PartId == "core." + families[i]);
                    var hub = drawing.Fragments.Single(f => f.PartId == "turn.standard");
                    // These are opaque source landmarks: the receiver's central
                    // lowest foot and the front inner edge of the open bearing.
                    var lip = new Vector2(625.5f, 643.5f);
                    Assert.That(Alpha(receiver, feet[i], pixels, atlas.width), Is.GreaterThanOrEqualTo(128));
                    Assert.That(Alpha(hub, lip, pixels, atlas.width), Is.GreaterThanOrEqualTo(128));
                    Assert.That(Alpha(hub, new Vector2(625.5f, 405.5f), pixels, atlas.width), Is.GreaterThanOrEqualTo(128));
                    Assert.That(Vector2.Distance(receiver.Transform(feet[i]), hub.Transform(lip)), Is.LessThan(.002f), families[i] + ": the central foot must reach the bearing's front lip.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(atlas); }
        }

        [Test] public void ReceiverMountedIronSight_StaysOnOpaqueReceiverInAimAndMaximumRecoil()
        {
            var catalog = Catalog(); var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(atlas.LoadImage(File.ReadAllBytes(PaperDefense.Editor.NotebookSpriteImportTools.AtlasPath)), Is.True);
                var pixels = atlas.GetPixels32();
                foreach (var family in new[] { "gun", "piercer", "mortar", "fan", "ricochet", "stapler", "blotter" })
                {
                    var foundation = new FoundationRecipe(P("frame.standard"), P("hull.standard"), P("armor.none"), P("feed.standard"), P("turn.standard"));
                    var weapon = new WeaponRecipe(P("core." + family), P("head." + family), P("sight.iron"), P("magazine.standard"), P("trigger.standard"));
                    var snapshot = new RecipeSnapshot(new AssemblyRecipe(new TowerRecipe(foundation, new[] { new WeaponMountRecipe(0, weapon) })), catalog.BalanceRevision, catalog.Definitions);
                    AssemblyVisualComposer.Fits.TryGetWeapon("core." + family, 1, out var fit);
                    foreach (var angle in new[] { 0f, Mathf.PI / 2, Mathf.PI })
                    {
                        var drawing = AssemblyVisualComposer.Compose(snapshot, new AssemblyVisualPose(new[] { new AssemblyMountPose(0, angle, -fit.RecoilLimit) }));
                        var sight = drawing.Fragments.Single(f => f.PartId == "sight.iron");
                        var receiver = drawing.Fragments.Single(f => f.PartId == "core." + family);
                        // Sample the centre of an opaque foot pixel, a few
                        // pixels inside its edge, to avoid angle-rounding seams.
                        // Measured lower central foot on AssemblyAccessories-v2.
                        var foot = new Vector2(257.5f, 15.5f);
                        Assert.That(Alpha(sight, foot, pixels, atlas.width), Is.GreaterThanOrEqualTo(128));
                        Assert.That(AlphaAtWorld(receiver, sight.Transform(foot), pixels, atlas.width), Is.GreaterThanOrEqualTo(128), family + ": the sight's real mounting foot must touch the receiver during recoil.");
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(atlas); }
        }

        [Test] public void BatteryFoundation_IsALowThreeDiskPlinthWithReadableMountedWeapons()
        {
            var drawing = AssemblyVisualComposer.ComposeFoundation(new FoundationRecipe(P("frame.battery"), P("hull.standard"), P("armor.none"), P("feed.standard"), P("turn.standard")));
            // Judge the actual opaque plinth, excluding its transparent native
            // crop padding. Physical keepouts still retain the whole crop;
            // EveryHull_PreservesTheActualFrameBaseRoofAndFront checks that
            // none of those contiguous source bands are discarded.
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(atlas.LoadImage(File.ReadAllBytes(PaperDefense.Editor.NotebookSpriteImportTools.AtlasPath)), Is.True);
                var pixels = atlas.GetPixels32(); float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
                foreach (var fragment in drawing.Fragments.Where(f => f.PartId == "frame.battery" || f.PartId == "hull.standard"))
                    for (int y = Mathf.CeilToInt(fragment.Region.yMin); y < fragment.Region.yMax; y++)
                    for (int x = Mathf.CeilToInt(fragment.Region.xMin); x < fragment.Region.xMax; x++)
                    {
                        var point = new Vector2(x + .5f, y + .5f);
                        if (!fragment.Region.Contains(point) || Alpha(fragment, point, pixels, atlas.width) < 128) continue;
                        bottom = Mathf.Min(bottom, fragment.Transform(point - Vector2.one * .5f).y);
                        top = Mathf.Max(top, fragment.Transform(point + Vector2.one * .5f).y);
                    }
                Assert.That(top - bottom, Is.GreaterThan(0));
                Assert.That(top - bottom, Is.LessThanOrEqualTo(.60f), "A battery must use a low three-pad plinth; a tall empty block makes all three heads unreadable in the shared fit.");
            }
            finally { UnityEngine.Object.DestroyImmediate(atlas); }
            Assert.That(drawing.Fragments.Count(f => f.PartId == "turn.standard"), Is.EqualTo(3));
        }

        [Test] public void SlowGrate_ClosesOnTheActualBowlRimCentreInTheSameSourcePlane()
        {
            var catalog = Catalog(); var drawing = AssemblyVisualComposer.Compose(catalog.Starters.Single(e => e.Id == "album.slow").Snapshot, new AssemblyVisualPose());
            var bowl = drawing.Fragments.Single(f => f.PartId == "function.slow");
            var grate = drawing.Fragments.Single(f => f.PartId == "shell.standard");
            Assert.That(Vector2.Distance(bowl.Transform(new Vector2(162, 139)), grate.Transform(new Vector2(154, 130))), Is.LessThan(.005f));
            Assert.That(grate.Scale / bowl.Scale, Is.EqualTo(1).Within(.02f));
        }
    }
}
