using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class DataDrawingBindingTests
    {
        static ContentCatalogInput Input()
        {
            var input = LegacyContentCatalog.CreateInput(GameBalance.CreateDefault());
            foreach (var item in input.Chassis) item.TemplateId = "template.test.outline";
            foreach (var item in input.Weapons) item.TemplateId = "template.test.outline";
            foreach (var item in input.Modules) item.TemplateId = "template.test.outline";
            input.Templates.Add(new DrawingTemplateDefinitionInput
            {
                Id = "template.test.outline", DisplayName = "Проверочный контур", Weight = 25,
                Contours = new List<DrawingContourInput> { new DrawingContourInput { Points = new List<DrawingCoordinateInput>
                    { new DrawingCoordinateInput(-.1f, 0), new DrawingCoordinateInput(.1f, .3f) } } }
            });
            input.Modules.Add(new ModuleDefinitionInput { Id = "module.test.range", Name = "Проверочное оборудование", TemplateId = "template.test.outline", Repeatable = true });
            return input;
        }
        static TowerBlueprint Blueprint(ContentCatalogSnapshot content, params ModuleSelection[] modules)
            => new TowerBlueprint("test.page", "Проверочная страница", "chassis.standard", "weapon.gun", "weapon.gun", modules,
                origin: DrawingOrigin.PlayerDrawing, content: content);
        static DrawingSnapshot Reference(TowerBlueprint blueprint)
        {
            var parts = new List<DrawingPart>();
            foreach (var template in DrawingTemplateCatalog.Create(blueprint)) parts.Add(new DrawingPart(template.BindingId, template.Contours));
            return new DrawingSnapshot(parts);
        }
        static readonly DrawingStroke mark = new DrawingStroke(new[] { new DrawingPoint(0, 0), new DrawingPoint(.1f, .2f) });

        [Test] public void RepeatedEquipmentUsesIndependentStableDrawingSlotsThroughUndoAndReassembly()
        {
            var content = Input().CreateSnapshot();
            var blueprint = Blueprint(content, new ModuleSelection("instance.a", "module.test.range"), new ModuleSelection("instance.b", "module.test.range"));
            var editor = new DrawingEditSession(blueprint);
            Assert.That(editor.AddStroke("module.instance.a", mark.Points, out _), Is.True);
            Assert.That(editor.AddStroke("module.instance.b", mark.Points, out _), Is.True);
            Assert.That(editor.Snapshot.FormatVersion, Is.EqualTo(3));
            Assert.That(editor.Snapshot.BindingVersion, Is.EqualTo(2));
            Assert.That(editor.ClearPart("module.instance.a"), Is.True);
            Assert.That(editor.Snapshot.GetPart("module.instance.b").Strokes.Count, Is.EqualTo(1));
            Assert.That(editor.Undo(), Is.True);
            Assert.That(editor.Snapshot.GetPart("module.instance.a").Strokes.Count, Is.EqualTo(1));
            var authored = blueprint.WithDrawing(editor.Snapshot);
            var changed = authored.WithAssembly(blueprint.ChassisId, blueprint.FirstWeaponId, blueprint.SecondWeaponId,
                new[] { new ModuleSelection("instance.b", "module.test.range") });
            Assert.That(changed.Drawing.GetPart("module.instance.a").Strokes, Is.Empty);
            Assert.That(changed.Drawing.GetPart("module.instance.b").Strokes.Count, Is.EqualTo(1));
            Assert.That(authored.Drawing.GetPart("module.instance.a").Strokes.Count, Is.EqualTo(1));
        }

        [Test] public void IdenticalWeaponsKeepSeparateLinesAndOnlyChangedMountIsCleared()
        {
            var content = Input().CreateSnapshot(); var blueprint = Blueprint(content);
            var drawing = new DrawingSnapshot(new[] { new DrawingPart(DrawingBindings.Mount0, new[] { mark }), new DrawingPart(DrawingBindings.Mount1, new[] { mark }) });
            var authored = blueprint.WithDrawing(drawing);
            var changed = authored.WithAssembly(blueprint.ChassisId, "weapon.piercer", blueprint.SecondWeaponId, blueprint.ModuleSelections);
            Assert.That(changed.Drawing.GetPart(DrawingBindings.Mount0).Strokes, Is.Empty);
            Assert.That(changed.Drawing.GetPart(DrawingBindings.Mount1).Strokes.Count, Is.EqualTo(1));
            Assert.That(changed.AsVariant("copy", "Копия").Drawing.GeometryHash, Is.EqualTo(changed.Drawing.GeometryHash));
        }

        [Test] public void SavedQualitySurvivesTemplateChangesUntilTheAuthorEditsTheDrawing()
        {
            var input = Input(); var firstContent = input.CreateSnapshot();
            var page = Blueprint(firstContent); page = page.WithDrawing(Reference(page));
            Assert.That(page.Assessment.Quality, Is.EqualTo(100));
            input.Templates[0].Revision++;
            input.Templates[0].Contours[0].Points = new List<DrawingCoordinateInput> { new DrawingCoordinateInput(-.8f, -.7f), new DrawingCoordinateInput(-.7f, -.4f) };
            input.Assessment.Revision++;
            var updated = page.WithContent(input.CreateSnapshot());
            Assert.That(updated.Assessment.Quality, Is.EqualTo(100));
            Assert.That(updated.Assessment.IsArchived, Is.True);
            Assert.That(updated.WithName("Новый заголовок").Assessment.Quality, Is.EqualTo(100));
            Assert.That(updated.WithDrawing(updated.Drawing.DeepCopy()).Assessment.Quality, Is.EqualTo(100), "Applying an unchanged drawing does not re-evaluate old work.");
            var editor = new DrawingEditSession(updated);
            Assert.That(editor.AddStroke(DrawingBindings.Mount0, mark.Points, out _), Is.True);
            var edited = updated.WithDrawing(editor.Snapshot);
            Assert.That(edited.Assessment.IsArchived, Is.False);
            Assert.That(edited.Assessment.Quality, Is.LessThan(100));
            Assert.That(page.Assessment.Quality, Is.EqualTo(100), "The already prepared page remains frozen.");
        }

        [Test] public void TemplateSnapshotOwnsItsGeometryAndAssessmentSettings()
        {
            var input = Input(); var snapshot = input.CreateSnapshot(); var page = Blueprint(snapshot);
            string signature = snapshot.GetTemplate("template.test.outline").Signature;
            input.Templates[0].Contours[0].Points[0].X = .8f; input.Assessment.Tolerance = .8f;
            Assert.That(snapshot.GetTemplate("template.test.outline").Signature, Is.EqualTo(signature));
            Assert.That(snapshot.Assessment.Tolerance, Is.EqualTo(.06f));
            Assert.That(DrawingTemplateCatalog.Create(page), Is.SameAs(DrawingTemplateCatalog.Create(page)), "Bindings and references resolve once per immutable page.");
        }

        [Test] public void AssessmentStampCannotBeAttachedToAnotherDrawing()
        {
            var content = Input().CreateSnapshot(); var page = Blueprint(content);
            var changed = new DrawingSnapshot(new[] { new DrawingPart(DrawingPartKey.Base, new[] { mark }) });
            Assert.Throws<ArgumentException>(() => new TowerBlueprint(page.Id, page.Name, page.ChassisId, page.FirstWeaponId, page.SecondWeaponId,
                page.ModuleSelections, changed, page.Origin, content: content, evaluationStamp: page.EvaluationStamp));
        }

        [Test] public void InvalidContourReportsItsRecordAndField()
        {
            var input = Input(); input.Templates[0].Contours[0].Points[1].Y = float.NaN;
            var error = Assert.Throws<ArgumentException>(() => input.CreateSnapshot());
            Assert.That(error.Message, Does.Contain("template.test.outline").And.Contain("contours[0].points[1]"));
        }
    }
}
