using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class ExtendedDrawingCatalogTests
    {
        static StructureKind Fan => (StructureKind)5;
        static StructureKind Ricochet => (StructureKind)6;
        static StructureKind Stapler => (StructureKind)7;
        static StructureKind Blotter => (StructureKind)8;
        static TowerBlueprint FanDraft => TowerBlueprint.NewDraft("fan-drawing", "Веер", ChassisKind.Standard, Fan);
        static TowerBlueprint RicochetDraft => TowerBlueprint.NewDraft("ricochet-drawing", "Рикошет", ChassisKind.Standard, Ricochet);
        static TowerBlueprint StaplerDraft => TowerBlueprint.NewDraft("stapler-drawing", "Скрепкострел", ChassisKind.Standard, Stapler);
        static TowerBlueprint BlotterDraft => TowerBlueprint.NewDraft("blotter-drawing", "Промокашка", ChassisKind.Standard, Blotter);

        static DrawingSnapshot Reference(TowerBlueprint blueprint)
        {
            var parts = new List<DrawingPart>();
            foreach (DrawingPartTemplate template in DrawingTemplateCatalog.Create(blueprint))
                parts.Add(new DrawingPart(template.Key, template.Contours));
            return new DrawingSnapshot(parts);
        }

        static float MaximumX(DrawingPart part)
        {
            float maximum = -1;
            foreach (DrawingStroke stroke in part.Strokes)
                foreach (DrawingPoint point in stroke.Points) maximum = Math.Max(maximum, point.X);
            return maximum;
        }

        static float MaximumY(DrawingPart part)
        {
            float maximum = -1;
            foreach (DrawingStroke stroke in part.Strokes)
                foreach (DrawingPoint point in stroke.Points) maximum = Math.Max(maximum, point.Y);
            return maximum;
        }

        static string PartHash(DrawingPart part) => new DrawingSnapshot(new[] { part }).GeometryHash;

        static bool HasForwardZigzag(DrawingPart part)
        {
            foreach (DrawingStroke stroke in part.Strokes)
            {
                float maximumY = -1;
                int previousDirection = 0, turns = 0;
                for (int i = 0; i < stroke.Points.Count; i++)
                {
                    maximumY = Math.Max(maximumY, stroke.Points[i].Y);
                    if (i == 0) continue;
                    float difference = stroke.Points[i].X - stroke.Points[i - 1].X;
                    int direction = difference > .001f ? 1 : difference < -.001f ? -1 : 0;
                    if (direction == 0) continue;
                    if (previousDirection != 0 && direction != previousDirection) turns++;
                    previousDirection = direction;
                }
                if (maximumY > .4f && turns >= 3) return true;
            }
            return false;
        }

        static bool HasForwardStaple(DrawingPart part)
        {
            foreach (DrawingStroke stroke in part.Strokes)
                for (int i = 1; i + 2 < stroke.Points.Count; i++)
                {
                    DrawingPoint before = stroke.Points[i - 1], first = stroke.Points[i];
                    DrawingPoint second = stroke.Points[i + 1], after = stroke.Points[i + 2];
                    if (first.Y > .4f && Math.Abs(second.Y - first.Y) < .001f && Math.Abs(second.X - first.X) > .2f &&
                        Math.Abs(before.X - first.X) < .001f && before.Y < first.Y - .05f &&
                        Math.Abs(after.X - second.X) < .001f && after.Y < second.Y - .05f) return true;
                }
            return false;
        }

        [Test]
        public void FanHasItsOwnWideForwardContourAndACompleteDrawingReachesFullQuality()
        {
            TowerBlueprint draft = FanDraft;
            Assert.That(draft.Assessment.Quality, Is.Zero, "An empty new page is still unfinished.");
            DrawingSnapshot reference = Reference(draft);
            DrawingPart mount = reference.GetPart(DrawingPartKey.Mount0);
            Assert.That(mount.Strokes.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(MaximumX(mount), Is.GreaterThan(.15f));
            Assert.That(MaximumY(mount), Is.GreaterThan(.3f));
            DrawingPart gun = Reference(TowerBlueprint.NewDraft("gun", "Пулемёт", ChassisKind.Standard,
                StructureKind.Gun)).GetPart(DrawingPartKey.Mount0);
            Assert.That(MaximumX(mount), Is.GreaterThan(MaximumX(gun)));
            Assert.That(draft.WithDrawing(reference).Assessment.Quality, Is.EqualTo(100));
            Assert.That(reference.FormatVersion, Is.EqualTo(DrawingSnapshot.CurrentFormatVersion));
            Assert.That(reference.BindingVersion, Is.EqualTo(1));
        }

        [Test]
        public void FanAndGunReferencesKeepSeparateLocalContoursAndEstablishedMountPivots()
        {
            var draft = TowerBlueprint.NewDraft("mixed", "Веер и пулемёт", ChassisKind.Standard, Fan, StructureKind.Gun);
            DrawingSnapshot reference = Reference(draft);
            Assert.That(MaximumX(reference.GetPart(DrawingPartKey.Mount0)), Is.GreaterThan(.15f));
            Assert.That(MaximumX(reference.GetPart(DrawingPartKey.Mount1)), Is.LessThan(.15f));
            Assert.That(DrawingTemplateCatalog.Pivot(draft, DrawingPartKey.Mount0).X, Is.EqualTo(-.17f));
            Assert.That(DrawingTemplateCatalog.Pivot(draft, DrawingPartKey.Mount1).X, Is.EqualTo(.17f));
            Assert.That(draft.WithDrawing(reference).Assessment.Quality, Is.EqualTo(100));
        }

        [Test]
        public void ReplacingOnlyOneGunWithAFanClearsOnlyThatMountAndCanBeUndone()
        {
            var original = TowerBlueprint.NewDraft("mixed-edit", "Два пулемёта", ChassisKind.Standard,
                StructureKind.Gun, StructureKind.Gun);
            DrawingSnapshot reference = Reference(original);
            var editor = new DrawingEditSession(original.WithDrawing(reference));
            DrawingStroke decoration = new DrawingStroke(new[] { new DrawingPoint(-.7f, -.7f), new DrawingPoint(.7f, .6f) });
            Assert.That(editor.AddStroke(DrawingPartKey.Decoration, decoration.Points, out _), Is.True);
            string before = editor.Snapshot.GeometryHash;
            var replacement = TowerBlueprint.NewDraft(original.Id, original.Name, original.Chassis, Fan, StructureKind.Gun);
            Assert.That(editor.ReplaceBlueprint(replacement), Is.True);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Mount0).Strokes, Is.Empty);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Mount1).Strokes.Count,
                Is.EqualTo(reference.GetPart(DrawingPartKey.Mount1).Strokes.Count));
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Base).Strokes.Count,
                Is.EqualTo(reference.GetPart(DrawingPartKey.Base).Strokes.Count));
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Decoration).Strokes.Count, Is.EqualTo(1));
            Assert.That(editor.Undo(), Is.True);
            Assert.That(editor.Snapshot.GeometryHash, Is.EqualTo(before));
        }

        [Test]
        public void ACustomFanDrawingKeepsItsActualCrookedStrokeInsteadOfTheReference()
        {
            var crooked = new DrawingStroke(new[] { new DrawingPoint(-.12f, .02f), new DrawingPoint(.03f, .19f),
                new DrawingPoint(.24f, .31f) });
            var drawing = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Mount0, new[] { crooked }) });
            TowerBlueprint page = FanDraft.WithDrawing(drawing);
            Assert.That(page.Drawing.GeometryHash, Is.EqualTo(drawing.GeometryHash));
            Assert.That(page.Drawing.GetPart(DrawingPartKey.Mount0).Strokes[0].Points[1], Is.EqualTo(crooked.Points[1]));
            Assert.That(page.Assessment.Quality, Is.GreaterThan(0).And.LessThan(100));
        }

        [Test]
        public void RicochetHasADistinctForwardZigzagAndACompleteDrawingReachesFullQuality()
        {
            TowerBlueprint draft = RicochetDraft;
            Assert.That(draft.Assessment.Quality, Is.Zero);
            DrawingSnapshot reference = Reference(draft);
            DrawingPart mount = reference.GetPart(DrawingPartKey.Mount0);
            Assert.That(HasForwardZigzag(mount), Is.True, "The pen outline should visibly identify its bouncing shot.");
            Assert.That(MaximumY(mount), Is.GreaterThan(.45f));
            Assert.That(MaximumX(mount), Is.GreaterThan(.1f).And.LessThan(.2f));
            Assert.That(PartHash(mount), Is.Not.EqualTo(PartHash(Reference(FanDraft).GetPart(DrawingPartKey.Mount0))));
            Assert.That(draft.WithDrawing(reference).Assessment.Quality, Is.EqualTo(100));
            Assert.That(reference.FormatVersion, Is.EqualTo(DrawingSnapshot.CurrentFormatVersion));
            Assert.That(reference.BindingVersion, Is.EqualTo(1));
        }

        [Test]
        public void APreviouslyAssessedPiercerDrawingIsNotACompleteRicochetDrawing()
        {
            var piercer = TowerBlueprint.NewDraft("piercer-quality-cache", "Пробой", ChassisKind.Standard, StructureKind.Piercer);
            DrawingSnapshot piercerReference = Reference(piercer);
            Assert.That(piercer.WithDrawing(piercerReference).Assessment.Quality, Is.EqualTo(100));
            TowerBlueprint ricochet = RicochetDraft.WithDrawing(piercerReference);
            Assert.That(ricochet.Assessment.Quality, Is.GreaterThan(0).And.LessThan(100));
            Assert.That(ricochet.Assessment.ConstructionHash,
                Is.Not.EqualTo(piercer.WithDrawing(piercerReference).Assessment.ConstructionHash));
        }

        [Test]
        public void FanAndRicochetUseDistinctLocalContoursAndEstablishedMountPivots()
        {
            var draft = TowerBlueprint.NewDraft("fan-ricochet", "Веер и рикошет", ChassisKind.Standard, Fan, Ricochet);
            DrawingSnapshot reference = Reference(draft);
            DrawingPart first = reference.GetPart(DrawingPartKey.Mount0);
            DrawingPart second = reference.GetPart(DrawingPartKey.Mount1);
            Assert.That(PartHash(first), Is.Not.EqualTo(PartHash(new DrawingPart(DrawingPartKey.Mount0, second.Strokes))));
            Assert.That(MaximumX(first), Is.GreaterThan(MaximumX(second)));
            Assert.That(HasForwardZigzag(second), Is.True);
            Assert.That(DrawingTemplateCatalog.Pivot(draft, DrawingPartKey.Mount0).X, Is.EqualTo(-.17f));
            Assert.That(DrawingTemplateCatalog.Pivot(draft, DrawingPartKey.Mount1).X, Is.EqualTo(.17f));
            Assert.That(draft.WithDrawing(reference).Assessment.Quality, Is.EqualTo(100));
        }

        [Test]
        public void ReplacingAFanWithRicochetClearsOnlyTheChangedMountAndUndoRestoresItsDrawing()
        {
            var original = TowerBlueprint.NewDraft("ricochet-edit", "Веер и пулемёт", ChassisKind.Standard, Fan, StructureKind.Gun);
            DrawingSnapshot reference = Reference(original);
            var editor = new DrawingEditSession(original.WithDrawing(reference));
            Assert.That(editor.AddStroke(DrawingPartKey.Decoration,
                new[] { new DrawingPoint(-.7f, -.6f), new DrawingPoint(.6f, -.7f) }, out _), Is.True);
            string before = editor.Snapshot.GeometryHash;
            string decoration = PartHash(editor.Snapshot.GetPart(DrawingPartKey.Decoration));
            var replacement = TowerBlueprint.NewDraft(original.Id, original.Name, original.Chassis, Ricochet, StructureKind.Gun);
            Assert.That(editor.ReplaceBlueprint(replacement), Is.True);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Mount0).Strokes, Is.Empty);
            Assert.That(PartHash(editor.Snapshot.GetPart(DrawingPartKey.Mount1)),
                Is.EqualTo(PartHash(reference.GetPart(DrawingPartKey.Mount1))));
            Assert.That(PartHash(editor.Snapshot.GetPart(DrawingPartKey.Base)),
                Is.EqualTo(PartHash(reference.GetPart(DrawingPartKey.Base))));
            Assert.That(PartHash(editor.Snapshot.GetPart(DrawingPartKey.Decoration)), Is.EqualTo(decoration));
            Assert.That(editor.Undo(), Is.True);
            Assert.That(editor.Blueprint.FirstWeapon, Is.EqualTo(Fan));
            Assert.That(editor.Snapshot.GeometryHash, Is.EqualTo(before));
        }

        [Test]
        public void StaplerHasADistinctForwardStapleAndACompleteDrawingReachesFullQuality()
        {
            TowerBlueprint draft = StaplerDraft;
            Assert.That(draft.Assessment.Quality, Is.Zero);
            DrawingSnapshot reference = Reference(draft);
            DrawingPart mount = reference.GetPart(DrawingPartKey.Mount0);
            Assert.That(HasForwardStaple(mount), Is.True, "The reference should show the staple's two legs and bridge.");
            Assert.That(MaximumY(mount), Is.GreaterThan(.4f));
            Assert.That(PartHash(mount), Is.Not.EqualTo(PartHash(Reference(FanDraft).GetPart(DrawingPartKey.Mount0))));
            Assert.That(draft.WithDrawing(reference).Assessment.Quality, Is.EqualTo(100));
            Assert.That(reference.FormatVersion, Is.EqualTo(DrawingSnapshot.CurrentFormatVersion));
            Assert.That(reference.BindingVersion, Is.EqualTo(1));
        }

        [Test]
        public void APreviouslyAssessedGunDrawingIsNotACompleteStaplerDrawing()
        {
            var gun = TowerBlueprint.NewDraft("gun-stapler-quality-cache", "Пулемёт", ChassisKind.Standard, StructureKind.Gun);
            DrawingSnapshot gunReference = Reference(gun);
            TowerBlueprint completeGun = gun.WithDrawing(gunReference);
            Assert.That(completeGun.Assessment.Quality, Is.EqualTo(100));
            TowerBlueprint stapler = StaplerDraft.WithDrawing(gunReference);
            Assert.That(stapler.Assessment.Quality, Is.GreaterThan(0).And.LessThan(100));
            Assert.That(stapler.Assessment.ConstructionHash, Is.Not.EqualTo(completeGun.Assessment.ConstructionHash));
        }

        [Test]
        public void MortarAndStaplerKeepTheirSeparateReferencesAndEstablishedMountPivots()
        {
            var draft = TowerBlueprint.NewDraft("mortar-stapler", "Мортира и скрепкострел", ChassisKind.Standard, StructureKind.Mortar, Stapler);
            DrawingSnapshot reference = Reference(draft);
            DrawingPart mortar = reference.GetPart(DrawingPartKey.Mount0), stapler = reference.GetPart(DrawingPartKey.Mount1);
            Assert.That(PartHash(mortar), Is.Not.EqualTo(PartHash(new DrawingPart(DrawingPartKey.Mount0, stapler.Strokes))));
            Assert.That(HasForwardStaple(stapler), Is.True);
            Assert.That(HasForwardStaple(mortar), Is.False);
            Assert.That(DrawingTemplateCatalog.Pivot(draft, DrawingPartKey.Mount0).X, Is.EqualTo(-.17f));
            Assert.That(DrawingTemplateCatalog.Pivot(draft, DrawingPartKey.Mount1).X, Is.EqualTo(.17f));
            Assert.That(draft.WithDrawing(reference).Assessment.Quality, Is.EqualTo(100));
        }

        [Test]
        public void ReplacingOnlyTheSecondGunWithStaplerRetainsTheFirstMountAndCanBeUndone()
        {
            var original = TowerBlueprint.NewDraft("stapler-edit", "Веер и пулемёт", ChassisKind.Standard, Fan, StructureKind.Gun);
            DrawingSnapshot reference = Reference(original);
            var editor = new DrawingEditSession(original.WithDrawing(reference));
            Assert.That(editor.AddStroke(DrawingPartKey.Decoration,
                new[] { new DrawingPoint(-.6f, -.5f), new DrawingPoint(.7f, -.6f) }, out _), Is.True);
            string before = editor.Snapshot.GeometryHash;
            string decoration = PartHash(editor.Snapshot.GetPart(DrawingPartKey.Decoration));
            var replacement = TowerBlueprint.NewDraft(original.Id, original.Name, original.Chassis, Fan, Stapler);
            Assert.That(editor.ReplaceBlueprint(replacement), Is.True);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Mount1).Strokes, Is.Empty);
            Assert.That(PartHash(editor.Snapshot.GetPart(DrawingPartKey.Mount0)),
                Is.EqualTo(PartHash(reference.GetPart(DrawingPartKey.Mount0))));
            Assert.That(PartHash(editor.Snapshot.GetPart(DrawingPartKey.Base)),
                Is.EqualTo(PartHash(reference.GetPart(DrawingPartKey.Base))));
            Assert.That(PartHash(editor.Snapshot.GetPart(DrawingPartKey.Decoration)), Is.EqualTo(decoration));
            Assert.That(editor.Undo(), Is.True);
            Assert.That(editor.Blueprint.SecondWeapon, Is.EqualTo(StructureKind.Gun));
            Assert.That(editor.Snapshot.GeometryHash, Is.EqualTo(before));
        }

        [Test]
        public void BlotterHasItsOwnWideAbsorbentHeadAndACompleteDrawingReachesFullQuality()
        {
            TowerBlueprint draft = BlotterDraft;
            Assert.That(draft.Assessment.Quality, Is.Zero);
            DrawingSnapshot reference = Reference(draft);
            DrawingPart mount = reference.GetPart(DrawingPartKey.Mount0);
            Assert.That(MaximumX(mount), Is.GreaterThan(.2f));
            Assert.That(MaximumY(mount), Is.GreaterThan(.35f));
            Assert.That(mount.Strokes.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(PartHash(mount), Is.Not.EqualTo(PartHash(Reference(FanDraft).GetPart(DrawingPartKey.Mount0))));
            Assert.That(draft.WithDrawing(reference).Assessment.Quality, Is.EqualTo(100));
            Assert.That(reference.FormatVersion, Is.EqualTo(DrawingSnapshot.CurrentFormatVersion));
            Assert.That(reference.BindingVersion, Is.EqualTo(1));
        }

        [Test]
        public void GunAndBlotterHaveIndependentLocalReferencesAndEstablishedMountPivots()
        {
            var draft = TowerBlueprint.NewDraft("gun-blotter", "Пулемёт и промокашка", ChassisKind.Standard, StructureKind.Gun, Blotter);
            DrawingSnapshot reference = Reference(draft);
            DrawingPart gun = reference.GetPart(DrawingPartKey.Mount0), blotter = reference.GetPart(DrawingPartKey.Mount1);
            Assert.That(MaximumX(gun), Is.LessThan(.15f));
            Assert.That(MaximumX(blotter), Is.GreaterThan(.2f));
            Assert.That(DrawingTemplateCatalog.Pivot(draft, DrawingPartKey.Mount0).X, Is.EqualTo(-.17f));
            Assert.That(DrawingTemplateCatalog.Pivot(draft, DrawingPartKey.Mount1).X, Is.EqualTo(.17f));
            Assert.That(draft.WithDrawing(reference).Assessment.Quality, Is.EqualTo(100));
        }

        [Test]
        public void CustomBlotterStrokesArePreservedWithoutInsertingTheReference()
        {
            var crooked = new DrawingStroke(new[] { new DrawingPoint(-.2f, .15f), new DrawingPoint(.05f, .21f),
                new DrawingPoint(.26f, .39f) });
            var drawing = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Mount0, new[] { crooked }) });
            TowerBlueprint page = BlotterDraft.WithDrawing(drawing);
            Assert.That(page.Drawing.GeometryHash, Is.EqualTo(drawing.GeometryHash));
            Assert.That(page.Drawing.GetPart(DrawingPartKey.Mount0).Strokes.Count, Is.EqualTo(1));
            Assert.That(page.Drawing.GetPart(DrawingPartKey.Mount0).Strokes[0].Points[1], Is.EqualTo(crooked.Points[1]));
            Assert.That(page.Assessment.Quality, Is.GreaterThan(0).And.LessThan(100));
        }

        [Test]
        public void ReplacingTheSecondGunWithBlotterClearsOnlyItsDrawingAndUndoRestoresIt()
        {
            var original = TowerBlueprint.NewDraft("blotter-edit", "Два пулемёта", ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun);
            DrawingSnapshot reference = Reference(original);
            var editor = new DrawingEditSession(original.WithDrawing(reference));
            string before = editor.Snapshot.GeometryHash;
            var replacement = TowerBlueprint.NewDraft(original.Id, original.Name, original.Chassis, StructureKind.Gun, Blotter);
            Assert.That(editor.ReplaceBlueprint(replacement), Is.True);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Mount1).Strokes, Is.Empty);
            Assert.That(PartHash(editor.Snapshot.GetPart(DrawingPartKey.Mount0)),
                Is.EqualTo(PartHash(reference.GetPart(DrawingPartKey.Mount0))));
            Assert.That(PartHash(editor.Snapshot.GetPart(DrawingPartKey.Base)),
                Is.EqualTo(PartHash(reference.GetPart(DrawingPartKey.Base))));
            Assert.That(editor.Undo(), Is.True);
            Assert.That(editor.Snapshot.GeometryHash, Is.EqualTo(before));
        }
    }
}
