using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class ConstructionBoundaryTests
    {
        [Test] public void ContinuousGestureInterpolatesAndDeduplicatesItsReturnPath()
        {
            var gesture = new ConstructionGesture();
            gesture.Begin(new Cell(4, 4), true);
            gesture.Move(new Cell(7, 7));
            gesture.Move(new Cell(4, 4));
            CollectionAssert.AreEqual(new[] { new Cell(4, 4), new Cell(5, 5), new Cell(6, 6), new Cell(7, 7) }, gesture.Cells);
            Assert.Throws<System.NotSupportedException>(() => ((IList<Cell>)gesture.Cells).Clear());
        }

        [Test] public void SinglePlacementTracksOnlyTheLatestCellAndCannotResumeAfterCancel()
        {
            var gesture = new ConstructionGesture();
            gesture.Begin(new Cell(4, 4), false);
            gesture.Move(new Cell(8, 6));
            CollectionAssert.AreEqual(new[] { new Cell(8, 6) }, gesture.Cells);
            gesture.Cancel();
            gesture.Move(new Cell(9, 7));
            Assert.That(gesture.Active, Is.False);
            Assert.That(gesture.Cells, Is.Empty);
            gesture.Begin(new Cell(9, 7), false);
            CollectionAssert.AreEqual(new[] { new Cell(9, 7) }, gesture.Cells);
        }

        [Test] public void QuotesAndCommandsApplyTheSameWorkshopAndLoadoutRestrictions()
        {
            var root = new GameObject("Construction application boundary"); root.SetActive(false);
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.ResetToDefaults();
                var run = root.AddComponent<RunController>(); run.balanceAsset = asset;
                var cells = new[] { new Cell(10, 10) };
                Assert.That(run.QuoteBuild(cells, StructureKind.Wall, MaterialTier.Pencil).Valid, Is.False);
                run.InitializeWorkshop(false);
                var quote = run.QuoteBuild(cells, StructureKind.Wall, MaterialTier.Pencil);
                Assert.That(run.Build(cells, StructureKind.Wall, MaterialTier.Pencil, out var reason), Is.False);
                Assert.That(quote.Valid, Is.False); Assert.That(quote.Reason, Is.EqualTo(reason));
                quote = run.QuoteBlueprint(cells, 0, MaterialTier.Pencil, InkColor.None);
                Assert.That(run.BuildBlueprint(cells, 0, MaterialTier.Pencil, InkColor.None, out reason), Is.False);
                Assert.That(quote.Valid, Is.False); Assert.That(quote.Reason, Is.EqualTo(reason));

                Assert.That(run.StartRun(out reason), Is.True, reason);
                quote = run.QuoteBuild(cells, StructureKind.Gun, MaterialTier.Pencil);
                Assert.That(run.Build(cells, StructureKind.Gun, MaterialTier.Pencil, out reason), Is.False);
                Assert.That(quote.Valid, Is.False); Assert.That(quote.Reason, Is.EqualTo(reason));
                quote = run.QuoteBlueprint(cells, 0, MaterialTier.Pencil, InkColor.None);
                Assert.That(quote.Valid, Is.True, quote.Reason);
                var blueprint = run.BuildLoadout[0];
                int ink = run.Session.Ink;
                Assert.That(run.BuildBlueprint(cells, 0, MaterialTier.Pencil, InkColor.None, out reason), Is.True, reason);
                var built = run.Session.StructureAt(cells[0]).Value.Blueprint;
                Assert.That(built.Id, Is.EqualTo(blueprint.Id));
                Assert.That(built.Drawing.GeometryHash, Is.EqualTo(blueprint.Drawing.GeometryHash));
                Assert.That(ink - run.Session.Ink, Is.EqualTo(quote.Cost));
                Assert.That(built.Content, Is.SameAs(blueprint.Content));
                Assert.Throws<System.NotSupportedException>(() => ((IList<TowerBlueprint>)run.BuildLoadout).Clear());
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(asset); }
        }
    }
}
