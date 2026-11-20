#if UNITY_EDITOR
using System.Collections.Generic;
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
    public sealed class NightNotebookConnectionTests
    {
        static PartRef P(string id) => new PartRef(id, 1);
        static WeaponRecipe Gun() => new WeaponRecipe(P("core.gun"), P("head.gun"), P("sight.iron"), P("magazine.standard"), P("trigger.standard"));
        static FoundationRecipe Foundation(string frame) => new FoundationRecipe(P(frame), P("hull.standard"), P("armor.none"), P("feed.standard"), P("turn.standard"));
        static RecipeSnapshot Tower(string frameId)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>("Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
            Assert.That(AssemblyVisualComposer.Fits.TryGetFrame(frameId, 1, out var frame), Is.True);
            var recipe = new AssemblyRecipe(new TowerRecipe(Foundation(frameId), frame.Mounts.Select(socket => new WeaponMountRecipe(socket.SlotIndex, Gun()))));
            Assert.That(AssemblyFitRules.Validate(recipe, AssemblyVisualComposer.Fits), Is.Empty);
            return new RecipeSnapshot(recipe, catalog.BalanceRevision, catalog.Definitions);
        }
        static IEnumerable<Vector2> Corners(AssemblyVisualFragment f)
        {
            yield return f.Transform(f.Region.min); yield return f.Transform(new Vector2(f.Region.xMax, f.Region.yMin));
            yield return f.Transform(f.Region.max); yield return f.Transform(new Vector2(f.Region.xMin, f.Region.yMax));
        }
        static Rect Union(IEnumerable<AssemblyVisualFragment> fragments)
        {
            var p = fragments.SelectMany(Corners).ToArray();
            return Rect.MinMaxRect(p.Min(v => v.x), p.Min(v => v.y), p.Max(v => v.x), p.Max(v => v.y));
        }
        static float HullTop(AssemblyVisualComposition c) => c.Fragments.Where(f => f.SlotIndex < 0 && f.Layer == (int)ComponentCategory.Hull).SelectMany(Corners).Max(v => v.y);
        static void AssertRect(Rect a, Rect b)
        {
            Assert.That(a.xMin, Is.EqualTo(b.xMin).Within(.00001f)); Assert.That(a.xMax, Is.EqualTo(b.xMax).Within(.00001f));
            Assert.That(a.yMin, Is.EqualTo(b.yMin).Within(.00001f)); Assert.That(a.yMax, Is.EqualTo(b.yMax).Within(.00001f));
        }
        [TestCase("frame.precision")] [TestCase("frame.standard")] [TestCase("frame.heavy")] [TestCase("frame.battery")]
        public void OccupiedSupportsJoinTheHullWithoutChangingPartsLayersOrAggregateBounds(string frame)
        {
            var snapshot = Tower(frame); var c = AssemblyVisualComposer.Compose(snapshot, new AssemblyVisualPose());
            var turns = c.Fragments.Where(f => f.Layer == 5).ToArray();
            Assert.That(turns.Length, Is.EqualTo(snapshot.Recipe.Tower.Mounts.Count));
            foreach (var turn in turns)
            {
                Assert.That(turn.PartId, Is.EqualTo("turn.standard")); Assert.That(turn.Radians, Is.Zero);
                AssertRect(turn.Trim, new Rect(-.065f, -.065f, .13f, .13f));
                Assert.That(turn.Region.width, Is.EqualTo(.13f).Within(.00001f));
                Assert.That(turn.Origin.y + turn.Region.yMin, Is.EqualTo(HullTop(c)).Within(.00001f));
                Assert.That(turn.Region.yMax, Is.EqualTo(.065f).Within(.00001f));
            }
            AssertRect(c.Bounds, Union(c.Fragments.Where(f => f.Layer != 5)));
            int weaponParts = Gun().Parts.Count(p => !p.Id.EndsWith(".none", System.StringComparison.Ordinal));
            Assert.That(c.Fragments.Count, Is.EqualTo(c.Fragments.Count(f => f.SlotIndex < 0) + turns.Length * (weaponParts + 1)));
        }
        [Test] public void OutboardSocketsMeetTheRibbedDeckBesideTheRaisedCenterHousing()
        {
            var foundation = new FoundationRecipe(P("frame.heavy"), P("hull.ribbed"), P("armor.none"),
                P("feed.standard"), P("turn.standard"));
            var drawing = AssemblyVisualComposer.ComposeFoundation(foundation);
            var deck = drawing.Fragments.Where(f => f.PartId == "hull.ribbed").OrderByDescending(f => f.Region.width).First();
            float contact = deck.Region.yMin + deck.Region.height * NightNotebookArt.RibbedDeckTop;
            var supports = drawing.Fragments.Where(f => f.Layer == 5).ToArray();
            Assert.That(supports.Length, Is.EqualTo(2));
            foreach (var support in supports)
                Assert.That(support.Origin.y + support.Region.yMin, Is.EqualTo(contact).Within(.00001f));
        }

        [Test] public void VacantFoundationSupportsStayInsideTheOriginalAggregateEnvelopeAndStandaloneHubsRemainCompact()
        {
            var c = AssemblyVisualComposer.ComposeFoundation(Foundation("frame.battery"));
            foreach (var turn in c.Fragments.Where(f => f.Layer == 5))
                Assert.That(turn.Origin.y + turn.Region.yMin, Is.EqualTo(HullTop(c)).Within(.00001f));
            var original = c.Fragments.Select(f => f.Layer == 5 ? new AssemblyVisualFragment(f.PartId, f.SpriteId, f.SlotIndex, f.Layer, f.Trim, f.Trim, f.Origin, f.Scale, f.Radians) : f);
            AssertRect(c.Bounds, Union(original)); Assert.That(c.Anchors, Is.Empty);
            var compact = new Rect(-.065f, -.065f, .13f, .13f);
            AssertRect(AssemblyVisualComposer.ComposePart(P("turn.standard")).Fragments.Single().Region, compact);
            AssertRect(AssemblyVisualComposer.ComposeWeapon(Gun()).Fragments.Single(f => f.Layer == 5).Region, compact);
        }
        [Test] public void SupportsStayFixedWhileAimRecoilAndMuzzlesKeepTheirMechanicalCoordinates()
        {
            var snapshot = Tower("frame.battery");
            Assert.That(AssemblyVisualComposer.Fits.TryGetWeapon("core.gun", 1, out var weapon), Is.True);
            Assert.That(AssemblyVisualComposer.Fits.TryGetFrame("frame.battery", 1, out var frame), Is.True);
            var motions = new[] { new AssemblyMountPose(0, weapon.MinRadians, -weapon.RecoilLimit), new AssemblyMountPose(1, weapon.MaxRadians, -weapon.RecoilLimit * .5f), new AssemblyMountPose(2, -.75f) };
            var rest = AssemblyVisualComposer.Compose(snapshot, new AssemblyVisualPose());
            var aimed = AssemblyVisualComposer.Compose(snapshot, new AssemblyVisualPose(motions));
            Assert.That(aimed.Fragments.Select(f => (f.PartId, f.SlotIndex, f.Layer)), Is.EqualTo(rest.Fragments.Select(f => (f.PartId, f.SlotIndex, f.Layer))));
            foreach (var turn in aimed.Fragments.Where(f => f.Layer == 5))
            {
                var before = rest.Fragments.Single(f => f.Layer == 5 && f.SlotIndex == turn.SlotIndex);
                AssertRect(turn.Region, before.Region); Assert.That(turn.Origin, Is.EqualTo(before.Origin)); Assert.That(turn.Radians, Is.Zero);
            }
            foreach (var anchor in aimed.Anchors)
            {
                var socket = frame.Mounts.Single(s => s.SlotIndex == anchor.SlotIndex); var motion = motions.Single(m => m.SlotIndex == anchor.SlotIndex);
                var attach = new Vector2(socket.Position.X, socket.Position.Y); var pivot = attach + new Vector2(weapon.Pivot.X, weapon.Pivot.Y);
                var muzzle = pivot + NotebookMountLayout.Rotate(new Vector2(0, .30f + motion.Recoil), motion.Radians);
                Assert.That(Vector2.Distance(anchor.Attach, attach), Is.LessThan(.00001f));
                Assert.That(Vector2.Distance(anchor.RotationCenter, pivot), Is.LessThan(.00001f)); Assert.That(Vector2.Distance(anchor.Muzzle, muzzle), Is.LessThan(.00001f));
            }
            AssertRect(aimed.Bounds, Union(aimed.Fragments.Where(f => f.Layer != 5)));
        }
        [TestCase("frame.precision")] [TestCase("frame.standard")]
        [TestCase("frame.heavy")] [TestCase("frame.battery")]
        public void TurnMeshPreservesCompactHubAndSupportWithinCertifiedBand(string frame)
        {
            var c = AssemblyVisualComposer.Compose(Tower(frame), new AssemblyVisualPose()); var turn = c.Fragments.First(f => f.Layer == 5);
            var pen = new MeshPen(null); turn.Draw(pen, Vector2.zero, 1, Color.white);
            using (var mesh = new VertexHelper())
            {
                pen.CopyTo(mesh, Vector2.zero, 1); var vertices = new UIVertex[mesh.currentVertCount];
                for (int i = 0; i < vertices.Length; i++) mesh.PopulateUIVertex(ref vertices[i], i);
                Assert.That(vertices, Is.Not.Empty); var local = vertices.Select(v => (Vector2)v.position - turn.Origin).ToArray();
                Assert.That(local.Min(v => v.y), Is.LessThanOrEqualTo(HullTop(c) - turn.Origin.y + .012f));
                var stem = local.Where(v => v.y < -.08f).ToArray(); Assert.That(stem, Is.Not.Empty);
                Assert.That(stem.Any(v => v.x < -.004f), Is.True); Assert.That(stem.Any(v => v.x > .004f), Is.True);
                var hub = local.Where(v => Mathf.Abs(v.y) <= .065f).ToArray();
                Assert.That(hub.Any(v => v.x < -.04f), Is.True); Assert.That(hub.Any(v => v.x > .04f), Is.True); Assert.That(hub.Any(v => v.y > .025f), Is.True);
                // v4 widens the load-bearing stem inside the original .13 U band.
                // The hub retains its exact compact drawing rather than stretching
                // down to the hull; the final certified-region checks bound the foot.
                var compact = AssemblyVisualComposer.ComposePart(P("turn.standard")).Fragments.Single();
                var compactPen = new MeshPen(null); compact.Draw(compactPen, Vector2.zero, 1, Color.white);
                using (var compactMesh = new VertexHelper())
                {
                    compactPen.CopyTo(compactMesh, Vector2.zero, 1);
                    var compactVertices = new UIVertex[compactMesh.currentVertCount];
                    for (int i = 0; i < compactVertices.Length; i++) compactMesh.PopulateUIVertex(ref compactVertices[i], i);
                    bool unchangedHub = false;
                    for (int start = 0; start + compactVertices.Length <= local.Length && !unchangedHub; start++)
                        unchangedHub = compactVertices.Select((v, i) => Vector2.Distance(v.position, local[start + i]) <= .00001f).All(equal => equal);
                    Assert.That(unchangedHub, Is.True, "The hub must retain its exact compact geometry at the physical origin.");
                }
                Assert.That(vertices.Any(v => v.color.r == 241 && v.color.g == 231 && v.color.b == 203), Is.True);
                foreach (var v in local)
                { Assert.That(v.x, Is.InRange(turn.Region.xMin - .00001f, turn.Region.xMax + .00001f)); Assert.That(v.y, Is.InRange(turn.Region.yMin - .00001f, turn.Region.yMax + .00001f)); }
            }
        }
    }
}
#endif
