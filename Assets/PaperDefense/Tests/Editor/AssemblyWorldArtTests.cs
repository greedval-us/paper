using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AssemblyWorldArtTests
    {
        static AssemblyVisualComposition Compose(StructureView structure)
            => DoodleGlyphs.ComposeAssembly(structure);

        static GameSession Session(RecipeSnapshot snapshot, float heading = 90)
        {
            var balance = GameBalance.CreateDefault(); balance.StartingInk = 10000;
            var session = new GameSession(balance, starterDefenses: false);
            var resolution = AssemblyResolver.Resolve(snapshot, balance, MaterialTier.Pencil);
            Assert.That(resolution.Valid, Is.True);
            Assert.That(session.TryBuildEntry(new[] { new Cell(12, 12) }, resolution.Value, MaterialTier.Pencil,
                InkColor.None, out string reason, facingDegrees: heading), Is.True, reason);
            return session;
        }

        [Test] public void EveryStarterWorldVisualUsesItsFrozenRecipeParts()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>("Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
            foreach (var entry in catalog.Starters)
            {
                var session = Session(entry.Snapshot);
                var view = new RunReadModel(session).Structures[0];
                var actual = Compose(view);
                var expected = AssemblyVisualComposer.Compose(view.ResolvedEntry.Snapshot, null);
                Assert.That(actual.Fragments.Select(f => (f.PartId, f.SpriteId, f.Layer)),
                    Is.EqualTo(expected.Fragments.Select(f => (f.PartId, f.SpriteId, f.Layer))), entry.Id);
                Assert.That(actual.Bounds, Is.EqualTo(expected.Bounds), entry.Id);
                Assert.That(actual.Anchors.Count, Is.EqualTo(entry.Kind == AlbumEntryKind.Tower ? entry.Snapshot.Recipe.Tower.Mounts.Count : 0), entry.Id);
                if (entry.Kind == AlbumEntryKind.Tower)
                    foreach (var mount in entry.Snapshot.Recipe.Tower.Mounts)
                    {
                        var core = mount.Weapon.Mechanism;
                        var family = entry.Snapshot.Definitions.Single(d => d.Id == core.Id && d.Revision == core.Revision).WeaponFamilies;
                        var support = actual.Fragments.Single(f => f.SlotIndex == mount.SlotIndex && f.Layer == 5);
                        Assert.That(support.WeaponFamily, Is.EqualTo(family), entry.Id);
                    }
            }
        }

        [TestCase(0f)][TestCase(90f)][TestCase(180f)][TestCase(270f)]
        public void IdleWorldWeaponsShowThePlayersHeadingOnEveryMount(float heading)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>("Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
            foreach (var entry in catalog.Starters.Where(value => value.Kind == AlbumEntryKind.Tower))
            {
                var session = Session(entry.Snapshot, heading);
                var view = new RunReadModel(session).Structures[0];
                var actual = Compose(view);
                var rest = AssemblyVisualComposer.Compose(entry.Snapshot, null);
                var rotation = Quaternion.Euler(0, 0, heading - 90);
                foreach (var f in actual.Fragments.Where(f => f.SlotIndex < 0))
                {
                    var original = rest.Fragments.First(b => b.PartId == f.PartId && b.Region == f.Region);
                    Assert.That(Mathf.DeltaAngle(f.Radians * Mathf.Rad2Deg, original.Radians * Mathf.Rad2Deg + heading - 90),
                        Is.EqualTo(0).Within(.001f), f.PartId);
                }
                foreach (var anchor in actual.Anchors)
                {
                    var original = rest.Anchors.Single(a => a.SlotIndex == anchor.SlotIndex);
                    Assert.That(Vector2.Distance(anchor.RotationCenter, rotation * original.RotationCenter), Is.LessThan(.00001f), entry.Id);
                    Assert.That(Vector2.Distance(anchor.Muzzle, rotation * original.Muzzle), Is.LessThan(.00001f), entry.Id);
                }
            }
        }

        [Test] public void ModernBarrelOriginUsesTheSameUniformViewTransformAroundItsLogicalCell()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>("Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
            var session = Session(catalog.Starters.Single(entry => entry.Id == "album.double").Snapshot);
            var state = session.StructureAt(new Cell(12, 12));
            state.PrimaryAim = new Point2(30, 12.5f); state.SecondaryAim = new Point2(0, 12.5f);
            var tower = new RunReadModel(session).Structures[0];
            var native = Compose(tower);
            Vector2 origin = new Vector2(tower.Cell.X + .5f, tower.Cell.Y + .5f);
            foreach (var anchor in native.Anchors)
            {
                var actual = DoodleGlyphs.SegmentOrigin(tower, anchor.SlotIndex, tower.GetMountAim(anchor.SlotIndex), origin, 0, null, null);
                var expected = NotebookAssemblyView.Point(origin, anchor.Muzzle, tower.ResolvedEntry.Snapshot);
                Assert.That(Vector2.Distance(actual, expected), Is.LessThan(.00001f),
                    "The live muzzle must follow the same whole-tree view transform as every visible fragment.");
                Vector2 continuation = origin + new Vector2(1, 2);
                Assert.That(DoodleGlyphs.SegmentOrigin(tower, anchor.SlotIndex, default, continuation, 0, null, null), Is.EqualTo(continuation));
            }
            var recordedAim = tower.GetMountAim(0);
            var originalMuzzle = DoodleGlyphs.SegmentOrigin(tower, 0, recordedAim, origin, 0, null, null);
            state.PrimaryAim = new Point2(12, 24);
            var turned = new RunReadModel(session).Structures[0];
            Assert.That(DoodleGlyphs.SegmentOrigin(turned, 0, recordedAim, origin, 0, null, null), Is.EqualTo(originalMuzzle));
            Assert.That(state.Cell, Is.EqualTo(new Cell(12, 12)), "Presentation magnification must preserve logical placement.");
        }

        [Test] public void IndependentLiveAimKeepsTheSameFixedFoundationAndMovesEachMuzzle()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>("Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
            var entry = catalog.Starters.Single(e => e.Id == "album.double");
            var session = Session(entry.Snapshot);
            var neutral = Compose(new RunReadModel(session).Structures[0]);
            var state = session.StructureAt(new Cell(12, 12));
            state.PrimaryAim = new Point2(30, 12.5f); state.SecondaryAim = new Point2(0, 12.5f);
            var aimed = Compose(new RunReadModel(session).Structures[0]);
            Assert.That(aimed.Fragments.Where(f => f.SlotIndex < 0).Select(f => (f.SpriteId, f.Origin, f.Radians)),
                Is.EqualTo(neutral.Fragments.Where(f => f.SlotIndex < 0).Select(f => (f.SpriteId, f.Origin, f.Radians))));
            for (int slot = 0; slot < 2; slot++)
            {
                var anchor = aimed.Anchors.Single(a => a.SlotIndex == slot);
                Assert.That(anchor.Attach, Is.EqualTo(neutral.Anchors.Single(a => a.SlotIndex == slot).Attach));
                Assert.That(slot == 0 ? anchor.Muzzle.x > anchor.RotationCenter.x : anchor.Muzzle.x < anchor.RotationCenter.x, Is.True);
                Assert.That(Vector2.Distance(anchor.Muzzle, neutral.Anchors.Single(a => a.SlotIndex == slot).Muzzle), Is.GreaterThan(.05f));
            }
        }
    }
}
