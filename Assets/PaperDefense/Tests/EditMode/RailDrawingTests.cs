using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class RailDrawingTests
    {
        static TowerModules Rail => (TowerModules)8;
        static TowerBlueprint Draft(TowerModules modules = TowerModules.None)
            => TowerBlueprint.NewDraft("rail-page", "Направляющая", ChassisKind.Standard, StructureKind.Gun, modules: modules);
        static DrawingStroke Mark => new DrawingStroke(new[] { new DrawingPoint(-.127f, -.319f), new DrawingPoint(.233f, .471f) });
        static string PartHash(DrawingPart part) => new DrawingSnapshot(new[] { part }).GeometryHash;

        static DrawingSnapshot Reference(TowerBlueprint blueprint)
        {
            var parts = new List<DrawingPart>();
            foreach (DrawingPartTemplate template in DrawingTemplateCatalog.Create(blueprint))
                parts.Add(new DrawingPart(template.Key, template.Contours));
            return new DrawingSnapshot(parts);
        }

        [Test]
        public void CurrentSnapshotsAppendAnOwnedRailPartWithoutMovingDecorationOrExistingCoordinates()
        {
            Assert.That(DrawingSnapshot.Empty.FormatVersion, Is.EqualTo(2));
            Assert.That(DrawingSnapshot.Empty.BindingVersion, Is.EqualTo(1));
            Assert.That(DrawingSnapshot.Empty.Parts.Count, Is.EqualTo(8));
            Assert.That((int)DrawingPartKey.Decoration, Is.EqualTo(6));
            Assert.That((int)DrawingPartKey.Rail, Is.EqualTo(7));
            DrawingStroke source = Mark;
            var snapshot = new DrawingSnapshot(new[]
            {
                new DrawingPart(DrawingPartKey.Rail, new[] { source }),
                new DrawingPart(DrawingPartKey.Decoration, new[] { source }),
                new DrawingPart(DrawingPartKey.Base, new[] { source })
            });
            DrawingSnapshot copy = snapshot.DeepCopy();
            Assert.That(copy.Parts[6].Key, Is.EqualTo(DrawingPartKey.Decoration));
            Assert.That(copy.Parts[7].Key, Is.EqualTo(DrawingPartKey.Rail));
            Assert.That(copy.GetPart(DrawingPartKey.Rail).Strokes[0], Is.Not.SameAs(snapshot.GetPart(DrawingPartKey.Rail).Strokes[0]));
            foreach (DrawingPartKey key in new[] { DrawingPartKey.Base, DrawingPartKey.Decoration, DrawingPartKey.Rail })
                Assert.That(copy.GetPart(key).Strokes[0].Points[0], Is.EqualTo(source.Points[0]));
            Assert.That(copy.GeometryHash, Is.EqualTo(snapshot.GeometryHash));
        }

        [Test]
        public void RailPointsAndStrokesConsumeTheExistingWholePageLimits()
        {
            var maximum = new DrawingStroke[DrawingSnapshot.MaxStrokes];
            for (int i = 0; i < maximum.Length; i++) maximum[i] = Mark;
            var full = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Rail, maximum) });
            Assert.That(full.StrokeCount, Is.EqualTo(DrawingSnapshot.MaxStrokes));
            Assert.Throws<ArgumentException>(() => new DrawingSnapshot(new[]
            {
                full.GetPart(DrawingPartKey.Rail), new DrawingPart(DrawingPartKey.Base, new[] { Mark })
            }));
            Assert.Throws<ArgumentException>(() => new DrawingSnapshot(new[]
            {
                new DrawingPart(DrawingPartKey.Rail, new[] { new DrawingStroke(new DrawingPoint[1025]) }),
                new DrawingPart(DrawingPartKey.Base, new[] { new DrawingStroke(new DrawingPoint[1024]) })
            }));
        }

        [Test]
        public void RailReferenceIsEnabledOnlyForItsModuleAndHasItsOwnStableLocalPivot()
        {
            TowerBlueprint plain = Draft(), rail = Draft(Rail);
            Assert.That(DrawingTemplateCatalog.IsEnabled(plain, DrawingPartKey.Rail), Is.False);
            Assert.That(DrawingTemplateCatalog.IsEnabled(rail, DrawingPartKey.Rail), Is.True);
            Assert.That(DrawingTemplateCatalog.IsEnabled(rail, (DrawingPartKey)99), Is.False);
            Assert.That(DrawingTemplateCatalog.EnabledKeys(plain), Has.No.Member(DrawingPartKey.Rail));
            Assert.That(DrawingTemplateCatalog.EnabledKeys(rail), Has.Member(DrawingPartKey.Rail));
            DrawingPartTemplate railTemplate = null;
            foreach (DrawingPartTemplate template in DrawingTemplateCatalog.Create(rail))
                if (template.Key == DrawingPartKey.Rail) railTemplate = template;
            Assert.That(railTemplate, Is.Not.Null);
            Assert.That(railTemplate.Contours.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(railTemplate.Pivot.X, Is.Zero); Assert.That(railTemplate.Pivot.Y, Is.Zero);
            var heavyDual = TowerBlueprint.NewDraft("rail-dual", "Два", ChassisKind.Heavy, StructureKind.Gun, StructureKind.Gun, Rail);
            Point2 other = DrawingTemplateCatalog.Pivot(heavyDual, DrawingPartKey.Rail);
            Assert.That(other.X, Is.Zero); Assert.That(other.Y, Is.Zero);
            foreach (DrawingPartKey key in new[] { DrawingPartKey.Base, DrawingPartKey.Mount0, DrawingPartKey.Mount1,
                DrawingPartKey.Armor, DrawingPartKey.Feed, DrawingPartKey.Tracking, DrawingPartKey.Decoration })
            {
                Point2 before = DrawingTemplateCatalog.Pivot(plain, key), after = DrawingTemplateCatalog.Pivot(rail, key);
                Assert.That(after.X, Is.EqualTo(before.X), key.ToString());
                Assert.That(after.Y, Is.EqualTo(before.Y), key.ToString());
            }
        }

        [Test]
        public void RailHasFiveFixedWeightAndMissingContourFeedbackInAssessmentVersionTwo()
        {
            TowerBlueprint rail = Draft(Rail);
            DrawingSnapshot reference = Reference(rail);
            TowerBlueprint complete = rail.WithDrawing(reference);
            Assert.That(complete.Assessment.RuleVersion, Is.EqualTo(2));
            Assert.That(complete.Assessment.Quality, Is.EqualTo(100));
            var missing = new DrawingSnapshot(new[] { reference.GetPart(DrawingPartKey.Base), reference.GetPart(DrawingPartKey.Mount0) });
            DrawingAssessment assessment = rail.WithDrawing(missing).Assessment;
            Assert.That(assessment.Quality, Is.EqualTo(5000f / 55).Within(.0001f));
            bool found = false;
            foreach (DrawingPartFeedback feedback in assessment.PartFeedback)
                if (feedback.Part == DrawingPartKey.Rail)
                {
                    found = true;
                    Assert.That(feedback.Quality, Is.Zero);
                    Assert.That(feedback.IncompleteContour, Is.EqualTo(1));
                    Assert.That(feedback.Message, Does.Contain("Направляющая").And.Contain("контур"));
                }
            Assert.That(found, Is.True);
            Assert.That(rail.Assessment.Quality, Is.Zero);
        }

        [Test]
        public void DormantRailStrokesStayLosslessButDoNotAffectQualityOrAcceptEditorInput()
        {
            TowerBlueprint plain = Draft();
            var parts = new List<DrawingPart>(Reference(plain).Parts);
            parts[(int)DrawingPartKey.Rail] = new DrawingPart(DrawingPartKey.Rail, new[] { Mark });
            DrawingSnapshot drawing = new DrawingSnapshot(parts);
            TowerBlueprint page = plain.WithDrawing(drawing);
            Assert.That(page.Assessment.Quality, Is.EqualTo(100));
            Assert.That(page.Drawing.GetPart(DrawingPartKey.Rail).Strokes.Count, Is.EqualTo(1));
            foreach (DrawingPartFeedback feedback in page.Assessment.PartFeedback)
                Assert.That(feedback.Part, Is.Not.EqualTo(DrawingPartKey.Rail));
            var editor = new DrawingEditSession(page);
            string before = editor.Snapshot.GeometryHash;
            Assert.That(editor.AddStroke(DrawingPartKey.Rail, Mark.Points, out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(editor.Snapshot.GeometryHash, Is.EqualTo(before));
        }

        [Test]
        public void InstallingRailChangesAssessmentIdentityWhileNamesReuseTheImmutableCachedResult()
        {
            DrawingSnapshot reference = Reference(Draft());
            TowerBlueprint plain = Draft().WithDrawing(reference), rail = Draft(Rail).WithDrawing(reference);
            Assert.That(plain.Assessment.Quality, Is.EqualTo(100));
            Assert.That(rail.Assessment.Quality, Is.EqualTo(5000f / 55).Within(.0001f));
            Assert.That(rail.Assessment.ConstructionHash, Is.Not.EqualTo(plain.Assessment.ConstructionHash));
            Assert.That(rail.WithName("Другой заголовок").Assessment, Is.SameAs(rail.Assessment));
            Assert.Throws<NotSupportedException>(() => ((IList<DrawingPartFeedback>)rail.Assessment.PartFeedback)[0] = default);
        }

        [Test]
        public void RemovingRailClearsOnlyItsPartAndUndoRedoRestoreTheAssemblyAndDrawingTogether()
        {
            TowerBlueprint original = Draft(Rail | TowerModules.Armor);
            DrawingSnapshot reference = Reference(original);
            var editor = new DrawingEditSession(original.WithDrawing(reference));
            Assert.That(editor.AddStroke(DrawingPartKey.Decoration, Mark.Points, out _), Is.True);
            string before = editor.Snapshot.GeometryHash;
            DrawingSnapshot prior = editor.Snapshot;
            Assert.That(editor.ReplaceBlueprint(Draft(TowerModules.Armor)), Is.True);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Rail).Strokes, Is.Empty);
            foreach (DrawingPartKey key in new[] { DrawingPartKey.Base, DrawingPartKey.Mount0, DrawingPartKey.Armor, DrawingPartKey.Decoration })
                Assert.That(PartHash(editor.Snapshot.GetPart(key)), Is.EqualTo(PartHash(prior.GetPart(key))), key.ToString());
            Assert.That(prior.GetPart(DrawingPartKey.Rail).Strokes, Is.Not.Empty);
            Assert.That(editor.Undo(), Is.True);
            Assert.That(editor.Blueprint.Modules, Is.EqualTo(original.Modules));
            Assert.That(editor.Snapshot.GeometryHash, Is.EqualTo(before));
            Assert.That(editor.Redo(), Is.True);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Rail).Strokes, Is.Empty);
        }

        [Test]
        public void WeaponAndChassisChangesPreserveInstalledRailAndItsOwnEraseCanBeUndone()
        {
            TowerBlueprint original = Draft(Rail);
            DrawingSnapshot reference = Reference(original);
            var editor = new DrawingEditSession(original.WithDrawing(reference));
            string railHash = PartHash(reference.GetPart(DrawingPartKey.Rail));
            var replacement = TowerBlueprint.NewDraft(original.Id, original.Name, ChassisKind.Heavy, StructureKind.Piercer, modules: Rail);
            Assert.That(editor.ReplaceBlueprint(replacement), Is.True);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Base).Strokes, Is.Empty);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Mount0).Strokes, Is.Empty);
            Assert.That(PartHash(editor.Snapshot.GetPart(DrawingPartKey.Rail)), Is.EqualTo(railHash));
            Assert.That(editor.AddStroke(DrawingPartKey.Rail, Mark.Points, out _), Is.True);
            string beforeClear = editor.Snapshot.GeometryHash;
            Assert.That(editor.Erase(DrawingPartKey.Rail, Mark.Points[1], .01f), Is.True);
            Assert.That(PartHash(editor.Snapshot.GetPart(DrawingPartKey.Rail)), Is.EqualTo(railHash));
            Assert.That(editor.Undo(), Is.True);
            Assert.That(editor.Snapshot.GeometryHash, Is.EqualTo(beforeClear));
            Assert.That(editor.ClearPart(DrawingPartKey.Rail), Is.True);
            Assert.That(editor.Snapshot.GetPart(DrawingPartKey.Rail).Strokes, Is.Empty);
            Assert.That(editor.Undo(), Is.True);
            Assert.That(editor.Snapshot.GeometryHash, Is.EqualTo(beforeClear));
        }
    }
}
