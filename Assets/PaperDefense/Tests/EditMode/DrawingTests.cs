using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class DrawingTests
    {
        static TowerBlueprint Dual => new TowerBlueprint("dual", "Два ствола", ChassisKind.Standard,
            StructureKind.Gun, StructureKind.Gun);
        static DrawingStroke Line(float startX = -.1f, float startY = 0, float endX = .1f, float endY = .4f)
            => new DrawingStroke(new[] { new DrawingPoint(startX, startY), new DrawingPoint(endX, endY) });
        static DrawingPart Part(DrawingPartKey key) => new DrawingPart(key, new[] { Line() });
        static DrawingSnapshot TwoMounts => new DrawingSnapshot(new[] { Part(DrawingPartKey.Mount0), Part(DrawingPartKey.Mount1) });

        [Test]
        public void SnapshotOwnsEveryNestedCollectionAndDeepCopyKeepsTheAuthoredCoordinates()
        {
            var points = new[] { new DrawingPoint(.123f, -.137f), new DrawingPoint(.291f, .443f) };
            var stroke = new DrawingStroke(points);
            var strokes = new[] { stroke };
            var part = new DrawingPart(DrawingPartKey.Mount0, strokes);
            var parts = new[] { part };
            var drawing = new DrawingSnapshot(parts);
            DrawingSnapshot copy = drawing.DeepCopy();
            float originalX = drawing.GetPart(DrawingPartKey.Mount0).Strokes[0].Points[0].X;
            points[0] = new DrawingPoint(.9f, .9f);
            strokes[0] = Line();
            parts[0] = Part(DrawingPartKey.Decoration);

            Assert.That(copy, Is.Not.SameAs(drawing));
            Assert.That(copy.GetPart(DrawingPartKey.Mount0), Is.Not.SameAs(drawing.GetPart(DrawingPartKey.Mount0)));
            Assert.That(copy.GetPart(DrawingPartKey.Mount0).Strokes[0], Is.Not.SameAs(drawing.GetPart(DrawingPartKey.Mount0).Strokes[0]));
            Assert.That(copy.GetPart(DrawingPartKey.Mount0).Strokes[0].Points[0].X, Is.EqualTo(originalX));
            Assert.That(drawing.GetPart(DrawingPartKey.Mount0).Strokes[0].Points[0].X, Is.EqualTo(originalX));
            Assert.That(copy.GeometryHash, Is.EqualTo(drawing.GeometryHash));
            Assert.That(copy.PointCount, Is.EqualTo(2));
            Assert.Throws<NotSupportedException>(() => ((IList<DrawingPart>)drawing.Parts)[0] = part);
            Assert.Throws<NotSupportedException>(() => ((IList<DrawingStroke>)drawing.GetPart(DrawingPartKey.Mount0).Strokes)[0] = stroke);
            Assert.Throws<NotSupportedException>(() => ((IList<DrawingPoint>)drawing.GetPart(DrawingPartKey.Mount0).Strokes[0].Points)[0] = default);
        }

        [TestCase(float.NaN, 0)]
        [TestCase(0, float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity, 0)]
        [TestCase(1.0001f, 0)]
        [TestCase(0, -1.0001f)]
        public void InvalidOrOutOfBoundsPointsAreRejectedRatherThanClamped(float x, float y)
            => Assert.Throws<ArgumentOutOfRangeException>(() => new DrawingPoint(x, y));

        [Test]
        public void SnapshotUsesCanonicalPartOrderAndStableQuantizedCoordinates()
        {
            DrawingSnapshot first = new DrawingSnapshot(new[] { Part(DrawingPartKey.Mount1), Part(DrawingPartKey.Base) });
            DrawingSnapshot second = new DrawingSnapshot(new[] { Part(DrawingPartKey.Base), Part(DrawingPartKey.Mount1) });
            Assert.That(first.GeometryHash, Is.EqualTo(second.GeometryHash));
            Assert.That(first.FormatVersion, Is.EqualTo(DrawingSnapshot.CurrentFormatVersion));
            Assert.That(first.BindingVersion, Is.EqualTo(1));
            Assert.That(new DrawingPoint(.1000001f, 0).X, Is.EqualTo(new DrawingPoint(.1000002f, 0).X));
            Assert.That(DrawingSnapshot.Empty.IsEmpty, Is.True);
            Assert.That(DrawingSnapshot.Empty.PointCount, Is.Zero);
        }

        [Test]
        public void UnknownAndDuplicatePartsAndIncompleteStrokesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DrawingPart((DrawingPartKey)99, new[] { Line() }));
            Assert.Throws<ArgumentException>(() => new DrawingSnapshot(new[] { Part(DrawingPartKey.Base), Part(DrawingPartKey.Base) }));
            Assert.Throws<ArgumentException>(() => new DrawingStroke(new[] { new DrawingPoint(0, 0) }));
            Assert.Throws<ArgumentNullException>(() => new DrawingSnapshot(null));
        }

        [Test]
        public void RepeatedIdenticalPointsWithoutVisibleSegmentsRemainAnEmptyDraft()
        {
            var repeated = new DrawingStroke(new[] { new DrawingPoint(.2f, .3f), new DrawingPoint(.2f, .3f) });
            var drawing = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Base, new[] { repeated }) });
            Assert.That(drawing.PointCount, Is.EqualTo(2));
            Assert.That(drawing.IsEmpty, Is.True);
            var editor = new DrawingEditSession(Dual, drawing);
            Assert.That(editor.Clear(), Is.True, "Очистка убирает и невидимые записанные точки.");
            Assert.That(editor.Snapshot.PointCount, Is.Zero);
        }

        [Test]
        public void LimitsApplyToTheWholePageAcrossAllParts()
        {
            var maximum = new DrawingStroke[128];
            for (int i = 0; i < maximum.Length; i++) maximum[i] = Line();
            var valid = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Decoration, maximum) });
            Assert.That(valid.StrokeCount, Is.EqualTo(128));
            Assert.Throws<ArgumentException>(() => new DrawingSnapshot(new[]
            {
                new DrawingPart(DrawingPartKey.Decoration, maximum), Part(DrawingPartKey.Base)
            }));
            var points1024 = new DrawingPoint[1024];
            var points1025 = new DrawingPoint[1025];
            var full = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Base, new[]
            { new DrawingStroke(points1024), new DrawingStroke(points1024) }) });
            Assert.That(full.PointCount, Is.EqualTo(2048));
            Assert.Throws<ArgumentException>(() => new DrawingSnapshot(new[]
            {
                new DrawingPart(DrawingPartKey.Base, new[] { new DrawingStroke(points1024) }),
                new DrawingPart(DrawingPartKey.Decoration, new[] { new DrawingStroke(points1025) })
            }));
        }

        [Test]
        public void IdenticalGunsHaveDistinctBindingsAndForwardFacingLocalContours()
        {
            IReadOnlyList<DrawingPartTemplate> templates = DrawingTemplateCatalog.Create(Dual);
            DrawingPartTemplate first = null, second = null;
            foreach (var template in templates)
            {
                if (template.Key == DrawingPartKey.Mount0) first = template;
                if (template.Key == DrawingPartKey.Mount1) second = template;
            }
            Assert.That(first, Is.Not.Null); Assert.That(second, Is.Not.Null);
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.Pivot.X, Is.EqualTo(-.17f));
            Assert.That(second.Pivot.X, Is.EqualTo(.17f));
            float forward = 0;
            foreach (var contour in first.Contours) foreach (var point in contour.Points) forward = Math.Max(forward, point.Y);
            Assert.That(forward, Is.GreaterThanOrEqualTo(.44f));
            var single = new TowerBlueprint("single", "Один", ChassisKind.Standard, StructureKind.Gun);
            Assert.That(DrawingTemplateCatalog.Pivot(single, DrawingPartKey.Mount0).X, Is.Zero);
            Assert.That(DrawingTemplateCatalog.EnabledKeys(single), Has.No.Member(DrawingPartKey.Mount1));
        }

        [Test]
        public void EditorUndoRedoAndClearPreserveOtherPartsAndOldSnapshots()
        {
            var editor = new DrawingEditSession(Dual);
            Assert.That(editor.AddStroke(DrawingPartKey.Base, Line().Points, out string reason), Is.True, reason);
            DrawingSnapshot prior = editor.Snapshot;
            Assert.That(editor.AddStroke(DrawingPartKey.Mount0, Line().Points, out reason), Is.True, reason);
            Assert.That(editor.CanUndo, Is.True);
            Assert.That(editor.Undo(), Is.True);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Mount0).Strokes.Count, Is.Zero);
            Assert.That(editor.CanRedo, Is.True);
            Assert.That(editor.Redo(), Is.True);
            Assert.That(editor.ClearPart(DrawingPartKey.Base), Is.True);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Base).Strokes.Count, Is.Zero);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Mount0).Strokes.Count, Is.EqualTo(1));
            Assert.That(prior.GetPart(DrawingPartKey.Base).Strokes.Count, Is.EqualTo(1));
            Assert.That(prior.GetPart(DrawingPartKey.Mount0).Strokes.Count, Is.Zero);
            Assert.That(editor.Undo(), Is.True);
            Assert.That(editor.Snapshot.PointCount, Is.EqualTo(4));
        }

        [Test]
        public void EraserIntersectsSegmentsAndRemovesTouchedStrokesAsOneUndoCommand()
        {
            var editor = new DrawingEditSession(Dual);
            Assert.That(editor.AddStroke(DrawingPartKey.Mount0, Line(-.8f, 0, .8f, 0).Points, out _), Is.True);
            Assert.That(editor.AddStroke(DrawingPartKey.Mount0, Line(-.8f, .02f, .8f, .02f).Points, out _), Is.True);
            Assert.That(editor.AddStroke(DrawingPartKey.Mount1, Line().Points, out _), Is.True);
            Assert.That(editor.Erase(DrawingPartKey.Mount0, new DrawingPoint(0, 0), .03f), Is.True);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Mount0).Strokes.Count, Is.Zero);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Mount1).Strokes.Count, Is.EqualTo(1));
            Assert.That(editor.Undo(), Is.True);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Mount0).Strokes.Count, Is.EqualTo(2));
        }

        [Test]
        public void ReplacingOneWeaponClearsOnlyThatMountAndCanBeUndone()
        {
            var editor = new DrawingEditSession(Dual, TwoMounts);
            var replacement = new TowerBlueprint("dual", "Перо и пулемёт", ChassisKind.Standard,
                StructureKind.Piercer, StructureKind.Gun);
            Assert.That(editor.ReplaceBlueprint(replacement), Is.True);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Mount0).Strokes.Count, Is.Zero);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Mount1).Strokes.Count, Is.EqualTo(1));
            Assert.That(editor.Undo(), Is.True);
            Assert.That(editor.Blueprint.FirstWeapon, Is.EqualTo(StructureKind.Gun));
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Mount0).Strokes.Count, Is.EqualTo(1));
        }

        [Test]
        public void ChassisAndModuleReplacementClearOnlyTheirChangedParts()
        {
            var original = new TowerBlueprint("armored", "Броня", ChassisKind.Standard,
                StructureKind.Gun, modules: TowerModules.Armor);
            var editor = new DrawingEditSession(original, new DrawingSnapshot(new[]
            { Part(DrawingPartKey.Base), Part(DrawingPartKey.Mount0), Part(DrawingPartKey.Armor), Part(DrawingPartKey.Decoration) }));
            var replacement = new TowerBlueprint("armored", "Подача", ChassisKind.Heavy,
                StructureKind.Gun, modules: TowerModules.Feed);
            Assert.That(editor.ReplaceBlueprint(replacement), Is.True);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Base).Strokes.Count, Is.Zero);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Armor).Strokes.Count, Is.Zero);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Feed).Strokes.Count, Is.Zero);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Mount0).Strokes.Count, Is.EqualTo(1));
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Decoration).Strokes.Count, Is.EqualTo(1));
        }

        [Test]
        public void DrawingCannotCreateAdditionalMountsOrChangeConstructionLoad()
        {
            var single = new TowerBlueprint("single", "Один", ChassisKind.Standard, StructureKind.Gun);
            var editor = new DrawingEditSession(single);
            Assert.That(editor.AddStroke(DrawingPartKey.Mount1, Line().Points, out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(editor.CanUndo, Is.False);
            Assert.That(editor.AddStroke(DrawingPartKey.Decoration, Line(-1, -1, 1, 1).Points, out _), Is.True);
            Assert.That(TowerCatalog.Load(editor.Blueprint), Is.EqualTo(4));
            Assert.That(TowerCatalog.Cost(editor.Blueprint, GameBalance.CreateDefault(), MaterialTier.Pencil), Is.EqualTo(40));
            Assert.That(editor.Blueprint.SecondWeapon.HasValue, Is.False);
        }

        [Test]
        public void FailedLimitCommandLeavesTheDraftAndUndoHistoryUntouched()
        {
            var strokes = new DrawingStroke[128];
            for (int i = 0; i < strokes.Length; i++) strokes[i] = Line();
            var editor = new DrawingEditSession(Dual, new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Base, strokes) }));
            string hash = editor.Snapshot.GeometryHash;
            Assert.That(editor.AddStroke(DrawingPartKey.Mount0, Line().Points, out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(editor.Snapshot.GeometryHash, Is.EqualTo(hash));
            Assert.That(editor.CanUndo, Is.False);
        }
    }
}
