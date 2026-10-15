using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class DrawingAssessmentTests
    {
        static TowerBlueprint Assembly => TowerBlueprint.NewDraft("quality", "Рисунок", ChassisKind.Standard, StructureKind.Gun);
        static TowerBlueprint Authored(DrawingSnapshot drawing, TowerBlueprint assembly = null)
        {
            assembly = assembly ?? Assembly;
            return new TowerBlueprint(assembly.Id, assembly.Name, assembly.Chassis, assembly.FirstWeapon,
                assembly.SecondWeapon, assembly.Modules, drawing, DrawingOrigin.PlayerDrawing);
        }

        static DrawingSnapshot Reference(TowerBlueprint assembly)
        {
            var parts = new List<DrawingPart>();
            foreach (DrawingPartTemplate template in DrawingTemplateCatalog.Create(assembly))
                parts.Add(new DrawingPart(template.Key, template.Contours));
            return new DrawingSnapshot(parts);
        }

        static DrawingSnapshot Transform(DrawingSnapshot source, Func<DrawingPoint, int, DrawingPoint> transform)
        {
            var parts = new List<DrawingPart>(); int pointIndex = 0;
            foreach (DrawingPart part in source.Parts)
            {
                var strokes = new List<DrawingStroke>();
                foreach (DrawingStroke stroke in part.Strokes)
                {
                    var points = new DrawingPoint[stroke.Points.Count];
                    for (int i = 0; i < points.Length; i++) points[i] = transform(stroke.Points[i], pointIndex++);
                    strokes.Add(new DrawingStroke(points));
                }
                parts.Add(new DrawingPart(part.Key, strokes));
            }
            return new DrawingSnapshot(parts);
        }

        static DrawingSnapshot Reverse(DrawingSnapshot source)
        {
            var parts = new List<DrawingPart>();
            for (int partIndex = source.Parts.Count - 1; partIndex >= 0; partIndex--)
            {
                DrawingPart part = source.Parts[partIndex]; var strokes = new List<DrawingStroke>();
                for (int strokeIndex = part.Strokes.Count - 1; strokeIndex >= 0; strokeIndex--)
                {
                    DrawingStroke stroke = part.Strokes[strokeIndex]; var points = new DrawingPoint[stroke.Points.Count];
                    for (int i = 0; i < points.Length; i++) points[i] = stroke.Points[points.Length - i - 1];
                    strokes.Add(new DrawingStroke(points));
                }
                parts.Add(new DrawingPart(part.Key, strokes));
            }
            return new DrawingSnapshot(parts);
        }

        [Test]
        public void CompleteReferenceAndReasonableHandTremorBothReachFullQuality()
        {
            DrawingSnapshot reference = Reference(Assembly);
            DrawingSnapshot shaken = Transform(reference, (point, index) => new DrawingPoint(
                point.X + (float)Math.Sin(index * 1.7) * .02f, point.Y + (float)Math.Cos(index * 2.3) * .02f));
            Assert.That(Authored(reference).Assessment.Quality, Is.EqualTo(100));
            Assert.That(Authored(shaken).Assessment.Quality, Is.EqualTo(100));
        }

        [Test]
        public void StrokeOrderDirectionAndDuplicateTracingDoNotChangeQuality()
        {
            DrawingSnapshot reference = Reference(Assembly);
            var partial = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Mount0,
                new[] { reference.GetPart(DrawingPartKey.Mount0).Strokes[0] }) });
            var duplicates = new List<DrawingPart>();
            foreach (DrawingPart part in partial.Parts)
            {
                var strokes = new List<DrawingStroke>();
                for (int repeat = 0; repeat < 5; repeat++) foreach (DrawingStroke stroke in part.Strokes) strokes.Add(stroke);
                duplicates.Add(new DrawingPart(part.Key, strokes));
            }
            float quality = Authored(partial).Assessment.Quality;
            Assert.That(quality, Is.GreaterThan(0).And.LessThan(100));
            Assert.That(Authored(Reverse(partial)).Assessment.Quality, Is.EqualTo(quality));
            Assert.That(Authored(new DrawingSnapshot(duplicates)).Assessment.Quality, Is.EqualTo(quality));
        }

        [Test]
        public void SubdividingStraightSegmentsDoesNotRewardInputEventDensity()
        {
            DrawingSnapshot reference = Reference(Assembly);
            var parts = new List<DrawingPart>();
            foreach (DrawingPart part in reference.Parts)
            {
                var strokes = new List<DrawingStroke>();
                foreach (DrawingStroke stroke in part.Strokes)
                {
                    var points = new List<DrawingPoint> { stroke.Points[0] };
                    for (int i = 1; i < stroke.Points.Count; i++)
                        for (int step = 1; step <= 7; step++)
                        {
                            DrawingPoint from = stroke.Points[i - 1], to = stroke.Points[i]; float t = step / 7f;
                            points.Add(new DrawingPoint(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t));
                        }
                    strokes.Add(new DrawingStroke(points));
                }
                parts.Add(new DrawingPart(part.Key, strokes));
            }
            Assert.That(Authored(new DrawingSnapshot(parts)).Assessment.Quality,
                Is.EqualTo(Authored(reference).Assessment.Quality));
        }

        [Test]
        public void FillingEveryFunctionalPartHasLowPrecisionInsteadOfAnAutomaticPerfectScore()
        {
            var strokes = new DrawingStroke[64];
            for (int i = 0; i < strokes.Length; i++)
            {
                float y = -1 + i * 2f / (strokes.Length - 1);
                strokes[i] = new DrawingStroke(new[] { new DrawingPoint(-1, y), new DrawingPoint(1, y) });
            }
            var fill = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Base, strokes),
                new DrawingPart(DrawingPartKey.Mount0, strokes) });
            Assert.That(Authored(fill).Assessment.Quality, Is.LessThan(50));
        }

        [Test]
        public void SmallProportionDifferencesAreToleratedWhileLargeScaleChangesRemainMeaningful()
        {
            DrawingSnapshot reference = Reference(Assembly);
            DrawingSnapshot close = Transform(reference, (point, index) => new DrawingPoint(point.X * 1.03f, point.Y * 1.03f));
            DrawingSnapshot enlarged = Transform(reference, (point, index) => new DrawingPoint(point.X * 1.8f, point.Y * 1.8f));
            Assert.That(Authored(close).Assessment.Quality, Is.EqualTo(100));
            Assert.That(Authored(enlarged).Assessment.Quality, Is.LessThan(80));
        }

        [Test]
        public void DecorationDoesNotBoostOrPenalizeTheFunctionalAssessment()
        {
            DrawingSnapshot reference = Reference(Assembly);
            var parts = new List<DrawingPart>(reference.Parts);
            parts[(int)DrawingPartKey.Decoration] = new DrawingPart(DrawingPartKey.Decoration, new[]
            { new DrawingStroke(new[] { new DrawingPoint(-1, -1), new DrawingPoint(1, 1), new DrawingPoint(-1, 1) }) });
            Assert.That(Authored(new DrawingSnapshot(parts)).Assessment.Quality, Is.EqualTo(100));
            var decorationOnly = new DrawingSnapshot(new[] { parts[(int)DrawingPartKey.Decoration] });
            Assert.That(decorationOnly.IsEmpty, Is.False);
            Assert.That(Authored(decorationOnly).Assessment.Quality, Is.Zero);
        }

        [Test]
        public void PartWeightsAreFixedAndMissingMountFeedbackNamesTheUnfinishedContour()
        {
            var dual = TowerBlueprint.NewDraft("dual", "Два", ChassisKind.Standard, StructureKind.Gun, StructureKind.Gun);
            DrawingSnapshot reference = Reference(dual);
            var incomplete = new DrawingSnapshot(new[] { reference.GetPart(DrawingPartKey.Base), reference.GetPart(DrawingPartKey.Mount0) });
            DrawingAssessment assessment = Authored(incomplete, dual).Assessment;
            Assert.That(assessment.Quality, Is.EqualTo(200f / 3).Within(.0001f));
            bool missing = false;
            foreach (DrawingPartFeedback feedback in assessment.PartFeedback)
                if (feedback.Part == DrawingPartKey.Mount1)
                {
                    missing = true;
                    Assert.That(feedback.Quality, Is.Zero);
                    Assert.That(feedback.IncompleteContour, Is.EqualTo(1));
                    Assert.That(TestText.Resolve(feedback.Message), Does.Contain("2").And.Contain("контур"));
                }
            Assert.That(missing, Is.True);
        }

        [Test]
        public void AnUndrawnModuleHasItsFixedSmallWeightRegardlessOfWeaponContourDensity()
        {
            var armored = TowerBlueprint.NewDraft("armored", "Броня", ChassisKind.Standard,
                StructureKind.Gun, modules: TowerModules.Armor);
            DrawingSnapshot reference = Reference(armored);
            var incomplete = new DrawingSnapshot(new[] { reference.GetPart(DrawingPartKey.Base), reference.GetPart(DrawingPartKey.Mount0) });
            Assert.That(Authored(incomplete, armored).Assessment.Quality, Is.EqualTo(5000f / 55).Within(.0001f));
        }

        [Test]
        public void LegacyPagesKeepFullEfficiencyAndNewEmptyDraftsHaveZeroQuality()
        {
            var legacy = new TowerBlueprint("old", "Старый", ChassisKind.Standard, StructureKind.Gun);
            Assert.That(legacy.Assessment.Quality, Is.EqualTo(100));
            Assert.That(Assembly.Origin, Is.EqualTo(DrawingOrigin.PlayerDrawing));
            Assert.That(Assembly.Assessment.Quality, Is.Zero);
        }

        [Test]
        public void AssessmentIsImmutableCachedAndVersionedAgainstDrawingAndConstruction()
        {
            TowerBlueprint first = Authored(Reference(Assembly));
            DrawingAssessment assessment = first.Assessment;
            Assert.That(DrawingEvaluator.Evaluate(first), Is.SameAs(assessment));
            Assert.That(first.WithName("Новое имя").Assessment, Is.SameAs(assessment));
            Assert.That(assessment.RuleVersion, Is.EqualTo(DrawingEvaluator.CurrentRuleVersion));
            Assert.That(assessment.DrawingHash, Is.EqualTo(first.Drawing.GeometryHash));
            Assert.That(assessment.ConstructionHash, Is.Not.Empty);
            var changed = first.WithAssembly(ChassisKind.Standard, StructureKind.Piercer, null, TowerModules.None);
            Assert.That(changed.Assessment.ConstructionHash, Is.Not.EqualTo(assessment.ConstructionHash));
            Assert.That(changed.Assessment.Quality, Is.LessThan(assessment.Quality));
            Assert.Throws<NotSupportedException>(() => ((IList<DrawingPartFeedback>)assessment.PartFeedback)[0] = default);
            foreach (var property in typeof(DrawingAssessment).GetProperties())
                Assert.That(property.GetSetMethod(true), Is.Null, property.Name);
        }

        [Test]
        public void AssessmentCacheEvictsOldEntriesWhileBlueprintsKeepTheirImmutableResult()
        {
            TowerBlueprint first = Authored(Reference(Assembly));
            DrawingAssessment retained = first.Assessment;
            for (int i = 0; i <= DrawingEvaluator.CacheCapacity; i++)
            {
                float x = -.9f + i * .005f;
                var stroke = new DrawingStroke(new[] { new DrawingPoint(x, -.8f), new DrawingPoint(x, .8f) });
                Authored(new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Decoration, new[] { stroke }) }));
            }
            Assert.That(DrawingEvaluator.Evaluate(first), Is.Not.SameAs(retained));
            Assert.That(first.Assessment, Is.SameAs(retained));
            Assert.That(first.Assessment.Quality, Is.EqualTo(100));
        }

    }
}
