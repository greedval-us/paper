using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AssemblyFoundationVariantArtTests
    {
        Color32[] pixels;
        int atlasWidth;
        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static PartRef P(string id) => new PartRef(id, 1);
        static FoundationRecipe Foundation(string frame, string hull = "hull.standard", string feed = "feed.standard")
            => new FoundationRecipe(P(frame), P(hull), P("armor.none"), P(feed), P("turn.standard"));
        static WeaponRecipe Weapon(string sight = "sight.iron", string trigger = "trigger.standard")
            => new WeaponRecipe(P("core.gun"), P("head.gun"), P(sight), P("magazine.standard"), P(trigger));

        [OneTimeSetUp] public void ReadNativeAtlasOnce()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(PaperDefense.Editor.NotebookSpriteImportTools.AtlasPath)), Is.True);
                pixels = texture.GetPixels32(); atlasWidth = texture.width;
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        [OneTimeTearDown] public void ReleaseAtlas() { pixels = null; sprites.Clear(); }
        byte Alpha(AssemblyVisualFragment fragment, Vector2 pixel)
        {
            if (!fragment.Region.Contains(pixel) || !fragment.Trim.Contains(pixel)) return 0;
            if (!sprites.TryGetValue(fragment.SpriteId, out var sprite))
            {
                Assert.That(NotebookSpriteCatalog.TryGet(fragment.SpriteId, out sprite), Is.True);
                sprites.Add(fragment.SpriteId, sprite);
            }
            int x = (int)sprite.rect.x + Mathf.FloorToInt((pixel.x - fragment.Trim.x) / fragment.Trim.width * sprite.rect.width);
            int y = (int)sprite.rect.y + Mathf.FloorToInt((pixel.y - fragment.Trim.y) / fragment.Trim.height * sprite.rect.height);
            return pixels[y * atlasWidth + x].a;
        }
        byte WorldAlpha(AssemblyVisualFragment fragment, Vector2 world)
            => Alpha(fragment, NotebookMountLayout.Rotate(world - fragment.Origin, -fragment.Radians) / fragment.Scale);
        static Rect Bounds(AssemblyVisualFragment fragment)
        {
            var r = fragment.Region;
            var points = new[] { r.min, new Vector2(r.xMax, r.yMin), r.max, new Vector2(r.xMin, r.yMax) }.Select(fragment.Transform).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
        (float fraction, float height) Visible(AssemblyVisualComposition drawing, AssemblyVisualFragment selected)
        {
            var order = drawing.Fragments.ToArray();
            int selectedIndex = Array.FindIndex(order, f => f.Equals(selected));
            Assert.That(selectedIndex, Is.GreaterThanOrEqualTo(0));
            var above = order.Skip(selectedIndex + 1).ToArray();
            int total = 0, visible = 0; float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            var region = selected.Region;
            // Pixel centres avoid floating-point ambiguity on inverse transforms.
            for (int y = Mathf.CeilToInt(region.yMin); y < region.yMax; y += 2)
            for (int x = Mathf.CeilToInt(region.xMin); x < region.xMax; x += 2)
            {
                var point = new Vector2(x + .5f, y + .5f);
                if (Alpha(selected, point) < 128) continue;
                total++; var world = selected.Transform(point);
                if (above.Any(f => WorldAlpha(f, world) >= 128)) continue;
                visible++; bottom = Mathf.Min(bottom, world.y); top = Mathf.Max(top, world.y);
            }
            Assert.That(total, Is.GreaterThan(100), selected.PartId + ": the selected part must contain real opaque pixels.");
            return ((float)visible / total, visible == 0 ? 0 : top - bottom);
        }

        [TestCase("frame.standard", "hull.standard")]
        [TestCase("frame.standard", "hull.light")]
        [TestCase("frame.standard", "hull.reinforced")]
        [TestCase("frame.heavy", "hull.standard")]
        [TestCase("frame.heavy", "hull.light")]
        [TestCase("frame.heavy", "hull.reinforced")]
        [TestCase("frame.precision", "hull.standard")]
        [TestCase("frame.precision", "hull.light")]
        [TestCase("frame.precision", "hull.reinforced")]
        [TestCase("frame.battery", "hull.standard")]
        [TestCase("frame.battery", "hull.light")]
        [TestCase("frame.battery", "hull.reinforced")]
        public void EveryHull_PreservesTheActualFrameBaseRoofAndFrontUnderItsSelectedOwner(string frameId, string hullId)
        {
            var drawing = AssemblyVisualComposer.ComposeFoundation(Foundation(frameId, hullId));
            var basis = drawing.Fragments.Single(f => f.PartId == frameId);
            var sameSource = drawing.Fragments.Where(f => f.SpriteId == basis.SpriteId).OrderBy(f => f.Region.yMin).ToArray();
            Assert.That(sameSource.Length, Is.EqualTo(3), "The original base, roof and front must survive every hull choice.");
            Assert.That(sameSource.Count(f => f.PartId == frameId), Is.EqualTo(1));
            Assert.That(sameSource.Count(f => f.PartId == hullId), Is.EqualTo(2), "The roof and foreground belong to the selected hull.");
            if (hullId != "hull.standard") Assert.That(drawing.Fragments.Any(f => f.PartId == "hull.standard"), Is.False, "An unselected hull must not appear as a phantom recipe part.");
            float next = basis.Trim.yMin;
            foreach (var band in sameSource)
            {
                Assert.That(band.Region.xMin, Is.EqualTo(basis.Trim.xMin).Within(.00001f));
                Assert.That(band.Region.width, Is.EqualTo(basis.Trim.width).Within(.00001f));
                Assert.That(band.Region.yMin, Is.EqualTo(next).Within(.0001f), "Source bands must meet without losing real frame pixels.");
                next = band.Region.yMax;
            }
            Assert.That(next, Is.EqualTo(basis.Trim.yMax).Within(.0001f));
            var front = sameSource.Single(f => f.Layer == 14); var bounds = Bounds(front);
            Assert.That(AssemblyVisualComposer.Fits.TryGetFrame(frameId, 1, out var fit), Is.True);
            Assert.That(fit.Keepouts.Any(k => k.PartId == hullId && Mathf.Abs(k.X - bounds.xMin) < .00001f && Mathf.Abs(k.Y - bounds.yMin) < .00001f
                && Mathf.Abs(k.Width - bounds.width) < .00001f && Mathf.Abs(k.Height - bounds.height) < .00001f), Is.True,
                "Core must certify the same selected-hull foreground that was actually drawn.");
        }

        [TestCase("frame.standard", "feed.standard")]
        [TestCase("frame.heavy", "feed.standard")]
        [TestCase("frame.precision", "feed.standard")]
        [TestCase("frame.battery", "feed.standard")]
        [TestCase("frame.standard", "feed.boost")]
        [TestCase("frame.heavy", "feed.boost")]
        [TestCase("frame.precision", "feed.boost")]
        [TestCase("frame.battery", "feed.boost")]
        public void Feeds_AttachToAnOpaqueFacadeAndRemainVisibleAfterForegroundLayers(string frameId, string feedId)
        {
            var drawing = AssemblyVisualComposer.ComposeFoundation(Foundation(frameId, feed: feedId));
            var feed = drawing.Fragments.Single(f => f.PartId == feedId);
            // Real paper/metal on the feed receiver, rather than a transparent
            // source-canvas centre or the silhouette's bounding rectangle.
            var joint = feedId == "feed.standard" ? new Vector2(200.5f, 250.5f) : new Vector2(162.5f, 142.5f);
            Assert.That(Alpha(feed, joint), Is.GreaterThanOrEqualTo(128));
            var world = feed.Transform(joint);
            Assert.That(drawing.Fragments.Where(f => f.PartId == frameId || f.PartId == "hull.standard").Any(f => WorldAlpha(f, world) >= 128),
                Is.True, frameId + ": feed's opaque receiver must meet the real facade, rather than float beside its lower ellipse.");
            Assert.That(Visible(drawing, feed).fraction, Is.GreaterThanOrEqualTo(.75f), "A facade-mounted feed must remain visible above the foreground wall.");
        }

        [TestCase("sight.scope")]
        [TestCase("sight.tracking")]
        public void Optics_ExposeTheirRealShapeAboveTheMagazineInsteadOfOnlyHavingAHiddenBinding(string sightId)
        {
            var drawing = AssemblyVisualComposer.ComposeWeapon(Weapon(sightId));
            var sight = drawing.Fragments.Single(f => f.PartId == sightId);
            var visible = Visible(drawing, sight);
            Assert.That(visible.fraction, Is.GreaterThanOrEqualTo(.50f), "At least half of the actual optical device must survive higher magazine/trigger layers.");
            Assert.That(visible.height, Is.GreaterThanOrEqualTo(.04f), "The exposed optical silhouette must remain readable in the complete weapon.");
            var receiver = drawing.Fragments.Single(f => f.PartId == "core.gun");
            var foot = sightId == "sight.scope" ? new Vector2(165.5f, 20.5f) : new Vector2(140.5f, 25.5f);
            Assert.That(Alpha(sight, foot), Is.GreaterThanOrEqualTo(128));
            Assert.That(WorldAlpha(receiver, sight.Transform(foot)), Is.GreaterThanOrEqualTo(128), "The enlarged optic's actual foot must still meet the receiver.");
        }

        [Test] public void BurstTrigger_HasADistinguishableVisibleSilhouetteInTheCompleteWeapon()
        {
            var stock = AssemblyVisualComposer.ComposeWeapon(Weapon());
            var burst = AssemblyVisualComposer.ComposeWeapon(Weapon(trigger: "trigger.burst"));
            var standardHeight = Visible(stock, stock.Fragments.Single(f => f.PartId == "trigger.standard")).height;
            var controller = burst.Fragments.Single(f => f.PartId == "trigger.burst");
            var burstShape = Visible(burst, controller);
            var foot = new Vector2(130.5f, 210.5f);
            Assert.That(Alpha(controller, foot), Is.GreaterThanOrEqualTo(128));
            Assert.That(WorldAlpha(burst.Fragments.Single(f => f.PartId == "core.gun"), controller.Transform(foot)), Is.GreaterThanOrEqualTo(128),
                "The visible burst controller must still attach to real opaque receiver pixels.");
            Assert.That(burstShape.fraction, Is.GreaterThanOrEqualTo(.75f));
            Assert.That(burstShape.height, Is.GreaterThanOrEqualTo(.045f), "The burst controller must read as a real alternate trigger, rather than a tiny pattern change on the same receiver.");
            Assert.That(standardHeight, Is.GreaterThan(0));
            Assert.That(burstShape.height / standardHeight, Is.GreaterThanOrEqualTo(1.5f));
        }
    }
}
