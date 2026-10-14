using System;
using System.Collections.Generic;
using NUnit.Framework;
using PaperDefense.Core;

namespace PaperDefense.Tests
{
    public sealed class ArchivedDrawingFeedbackTests
    {
        static UserTextResolver Resolver()=>new UserTextResolver(new[]{
            new KeyValuePair<string,string>("copy.3cc592b5b09bbd9f","INCOMPLETE {0} {1}"),
            new KeyValuePair<string,string>("copy.5c24ada686c606b4","LOW {0}"),
            new KeyValuePair<string,string>("copy.e19aba16f8c4e47b","READABLE {0}"),
            new KeyValuePair<string,string>("drawing.feedback.archived","ARCHIVE {0} {1:0.##} {2:0.##} {3:0.##}"),
            new KeyValuePair<string,string>("copy.9e98735021c6b210","MOUNT ONE"),
            new KeyValuePair<string,string>("drawing.feedback.part","PART")
        });
        static BlueprintData Saved(string message,float precision=0.9f,int incomplete=0)
        {
            var stroke=new DrawingStroke(new[]{new DrawingPoint(-.2f,0),new DrawingPoint(.2f,.2f)});
            var page=new TowerBlueprint("archive","Archive",ChassisKind.Standard,StructureKind.Gun,drawing:new DrawingSnapshot(new[]{new DrawingPart(DrawingBindings.Mount0,new[]{stroke})}),origin:DrawingOrigin.PlayerDrawing);
            var saved=new BlueprintData(page);
            saved.Evaluation.Feedback=new[]{new DrawingFeedbackData{BindingId=DrawingBindings.Mount0,Quality=37,Coverage=.42f,Precision=precision,IncompleteContour=incomplete,Message=message}};
            return saved;
        }
        static ContentCatalogSnapshot Content(float threshold)
        {
            var input=LegacyContentCatalog.CreateInput(GameBalance.CreateDefault());input.Assessment.PrecisionThreshold=threshold;
            input.Templates.Add(new DrawingTemplateDefinitionInput{Id="template.archive",DisplayName="Changed resource name",Weight=25,
                Contours=new List<DrawingContourInput>{new DrawingContourInput{Points=new List<DrawingCoordinateInput>{new DrawingCoordinateInput(-.4f,0),new DrawingCoordinateInput(.4f,0)}}}});
            foreach(var part in input.Chassis)part.TemplateId="template.archive";foreach(var part in input.Weapons)part.TemplateId="template.archive";foreach(var part in input.Modules)part.TemplateId="template.archive";
            return input.CreateSnapshot();
        }

        [TestCase(.1f)] [TestCase(.99f)]
        public void KnownLowPrecisionMessageKeepsItsCategoryWhenCurrentThresholdChanges(float threshold)
        {
            const string original="Орудие: много линий в стороне от образца.";
            var saved=Saved(original);var restored=saved.ToBlueprint(5).WithContent(Content(threshold));
            var feedback=restored.Assessment.PartFeedback[0];
            Assert.That(Resolver().Resolve(feedback.Message),Is.EqualTo("LOW Орудие"));
            Assert.That(feedback.Precision,Is.EqualTo(.9f));Assert.That(feedback.Quality,Is.EqualTo(37));Assert.That(feedback.Coverage,Is.EqualTo(.42f));
            Assert.That(restored.Assessment.Quality,Is.EqualTo(saved.Evaluation.Quality));Assert.That(restored.Assessment.IsArchived,Is.True);
            Assert.That(restored.Drawing.GeometryHash,Is.EqualTo(saved.Evaluation.DrawingHash));Assert.That(restored.Assessment.ConstructionHash,Is.EqualTo(saved.Evaluation.ConstructionHash));
            var roundTrip=new BlueprintData(restored);var archiveField=typeof(DrawingFeedbackData).GetField("ArchivedMessage");Assert.That(archiveField,Is.Not.Null);
            Assert.That(archiveField.GetValue(roundTrip.Evaluation.Feedback[0]),Is.EqualTo(original));
            Assert.That(Resolver().Resolve(roundTrip.ToBlueprint(6).Assessment.PartFeedback[0].Message),Is.EqualTo("LOW Орудие"));
        }

        [TestCase("Орудие: контуры читаются уверенно.",0,"READABLE Орудие")]
        [TestCase("Орудие: контур 2 ещё не завершён.",2,"INCOMPLETE Орудие 2")]
        public void KnownArchivedCategoriesUseResourceMessages(string original,int incomplete,string expected)
        {
            var saved=Saved(original,.1f,incomplete);var feedback=saved.ToBlueprint(5).WithContent(Content(.99f)).Assessment.PartFeedback[0];
            Assert.That(Resolver().Resolve(feedback.Message),Is.EqualTo(expected));Assert.That(feedback.IncompleteContour,Is.EqualTo(incomplete));
        }

        [Test] public void UnknownArchivedMessageUsesNeutralResourceAndRetainsOriginalAndFacts()
        {
            const string original="Unknown old wording with a future category";
            var saved=Saved(original,.9f,4);var restored=saved.ToBlueprint(5);var feedback=restored.Assessment.PartFeedback[0];
            string display=Resolver().Resolve(feedback.Message);
            Assert.That(display,Does.StartWith("ARCHIVE MOUNT ONE"));Assert.That(display,Does.Not.Contain(original));
            Assert.That(feedback.IncompleteContour,Is.EqualTo(4));Assert.That(feedback.Quality,Is.EqualTo(37));Assert.That(feedback.Coverage,Is.EqualTo(.42f));Assert.That(feedback.Precision,Is.EqualTo(.9f));
            var again=new BlueprintData(restored).ToBlueprint(6);var serialized=new BlueprintData(again);
            var archiveField=typeof(DrawingFeedbackData).GetField("ArchivedMessage");Assert.That(archiveField,Is.Not.Null);
            Assert.That(archiveField.GetValue(serialized.Evaluation.Feedback[0]),Is.EqualTo(original));
            Assert.That(Resolver().Resolve(again.Assessment.PartFeedback[0].Message),Is.EqualTo(display));
        }

        [Test] public void AlreadySemanticArchivedMessageRemainsUnchanged()
        {
            string message=UserText.Message("copy.e19aba16f8c4e47b","Saved name");var saved=Saved(message);
            var restored=saved.ToBlueprint(6);Assert.That(restored.Assessment.PartFeedback[0].Message,Is.EqualTo(message));
        }
    }
}
