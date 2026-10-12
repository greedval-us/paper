using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class DrawingQualityHudTests
    {
        GameObject root;
        RunController run;
        WorkshopHud hud;
        [SetUp] public void SetUp()
        {
            root = new GameObject("Isolated quality HUD"); root.SetActive(false);
            run = root.AddComponent<RunController>(); run.InitializeWorkshop(false);
            hud = root.AddComponent<WorkshopHud>(); hud.Initialize(run);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);
        void Click(string name) => root.GetComponentsInChildren<Button>(true).Single(b => b.name == name).onClick.Invoke();
        [Test] public void CardEditorAndQualityTrialDescribeTheSameCachedAssessment()
        {
            Click("New blueprint"); Click("Second Gun"); Click("Open drawing editor");
            Assert.That(hud.DrawingEditor.Session.AddStroke(DrawingPartKey.Mount1,
                new[] { new DrawingPoint(0, 0), new DrawingPoint(.08f, .4f) }, out _), Is.True);
            Click("Apply drawing");
            var assessment = hud.Draft.Assessment;
            Assert.That(assessment.Quality, Is.LessThan(100));
            Click("Drawing quality details");
            var details = root.GetComponentsInChildren<Text>(true).Single(t => t.name == "Drawing quality explanation");
            Assert.That(details.text, Does.Contain("Основание"));
            Assert.That(details.text, Does.Contain("Ствол 1").IgnoreCase);
            Assert.That(details.text, Does.Contain("контур"));
            Click("Close quality details");
            long before = run.Session.TickIndex;
            int gold = run.Workshop.Gold, ink = run.Session.Ink;
            Click("Open trial"); Click("Trial quality mode"); Click("Run trial");
            Assert.That(root.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("100% качества")), Is.True);
            Assert.That(run.Session.TickIndex, Is.EqualTo(before));
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(run.Session.Ink, Is.EqualTo(ink));
            Assert.That(hud.Draft.Assessment, Is.SameAs(assessment));
        }
    }
}
