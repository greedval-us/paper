using System;
using System.Linq;
using System.Reflection;
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
        {
            var method = typeof(NotebookWorldView).Assembly.GetType("PaperDefense.Presentation.DoodleGlyphs")
                .GetMethod("ComposeAssembly", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "World and trial require the same frozen recipe composition as the album.");
            return (AssemblyVisualComposition)method.Invoke(null, new object[] { structure, 0d, null, null });
        }

        static GameSession Session(RecipeSnapshot snapshot)
        {
            var balance = GameBalance.CreateDefault(); balance.StartingInk = 10000;
            var session = new GameSession(balance, starterDefenses: false);
            var resolution = AssemblyResolver.Resolve(snapshot, balance, MaterialTier.Pencil);
            Assert.That(resolution.Valid, Is.True);
            Assert.That(session.TryBuildEntry(new[] { new Cell(12, 12) }, resolution.Value, MaterialTier.Pencil,
                InkColor.None, out string reason), Is.True, reason);
            for (int slot = 0; slot < 3; slot++) session.StructureAt(new Cell(12, 12)).GetMountState(slot).Aim = default;
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
                var expected = AssemblyVisualComposer.Compose(view.ResolvedEntry.Snapshot, AssemblyPreviewPose.For(view.ResolvedEntry.Snapshot));
                Assert.That(actual.Fragments.Select(f => (f.PartId, f.SpriteId, f.Layer)),
                    Is.EqualTo(expected.Fragments.Select(f => (f.PartId, f.SpriteId, f.Layer))), entry.Id);
                Assert.That(actual.Bounds, Is.EqualTo(expected.Bounds), entry.Id);
                Assert.That(actual.Anchors.Count, Is.EqualTo(entry.Kind == AlbumEntryKind.Tower ? entry.Snapshot.Recipe.Tower.Mounts.Count : 0), entry.Id);
            }
        }

        [Test] public void IdleWorldWeaponsUseTheSameForwardRightDisplayPoseUntilTheyAcquireARealAim()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>("Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset").CreateSnapshot();
            foreach (var entry in catalog.Starters.Where(value => value.Kind == AlbumEntryKind.Tower))
            {
                var session = Session(entry.Snapshot);
                var view = new RunReadModel(session).Structures[0];
                var actual = Compose(view);
                var expected = AssemblyVisualComposer.Compose(entry.Snapshot, AssemblyPreviewPose.For(entry.Snapshot));
                Assert.That(actual.Fragments.Select(fragment => (fragment.Origin, fragment.Radians)),
                    Is.EqualTo(expected.Fragments.Select(fragment => (fragment.Origin, fragment.Radians))), entry.Id);
                Assert.That(actual.Anchors.Select(anchor => anchor.Muzzle), Is.EqualTo(expected.Anchors.Select(anchor => anchor.Muzzle)), entry.Id);
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
                Assert.That(Vector2.Distance(actual, origin + anchor.Muzzle * 2), Is.LessThan(.00001f),
                    "The live muzzle must follow the same whole-tree view transform as every visible fragment.");
                Vector2 continuation = origin + new Vector2(1, 2);
                Assert.That(DoodleGlyphs.SegmentOrigin(tower, anchor.SlotIndex, default, continuation, 0, null, null), Is.EqualTo(continuation));
            }
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
