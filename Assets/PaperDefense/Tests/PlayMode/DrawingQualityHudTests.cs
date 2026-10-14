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
            run = root.AddComponent<RunController>(); run.InitializeWorkshop(false, allowLegacyFixtureDefaults: true);
            hud = root.AddComponent<WorkshopHud>(); hud.Initialize(run);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);
        void Click(string name) => root.GetComponentsInChildren<Button>(true).Single(b => b.name == name).onClick.Invoke();
        [Test] public void FreeformEditorPreviewAndNormalTrialUseTheSameConstructionWithoutGrading()
        {
            Click("New blueprint"); Click("Second Gun"); Click("Open drawing editor");
            var session = hud.DrawingEditor.Session;
            Assert.That(session.AddStroke(DrawingBindings.Base, new[] {
                new DrawingPoint(-.4f, -.4f), new DrawingPoint(.4f, -.4f),
                new DrawingPoint(.4f, .4f), new DrawingPoint(-.4f, .4f), new DrawingPoint(-.4f, -.4f) }, out _), Is.True);
            for (int slot = 0; slot < 2; slot++)
                Assert.That(session.AddStroke(DrawingBindings.Mount(slot), new[] {
                    new DrawingPoint(-.08f, 0), new DrawingPoint(-.08f, .6f),
                    new DrawingPoint(.08f, .6f), new DrawingPoint(.08f, 0) }, out _), Is.True);
            Click("Apply drawing");
            Assert.That(hud.DrawingEditor.IsOpen, Is.False);
            Assert.That(hud.Draft.IsFreeform, Is.True);
            Assert.That(hud.Draft.FreeformDesign.IsComplete, Is.True);
            var preview = run.PreviewBlueprint(hud.Draft);
            Assert.That(preview.Valid, Is.True, preview.Reason);
            Assert.That(preview.QualityMultiplier, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<Button>(true).Any(b => b.name == "Drawing quality details" || b.name == "Compare drawing quality"), Is.False);
            Assert.That(root.GetComponentsInChildren<Button>(true).Any(b => b.name == "Trial quality mode"), Is.False);
            long before = run.Session.TickIndex;
            int gold = run.Workshop.Gold, ink = run.Session.Ink;
            var trial = hud.RunDrawingTrial();
            Assert.That(trial, Is.Not.Null);
            Assert.That(trial.First.QualityMultiplier, Is.EqualTo(1));
            Assert.That(trial.First.Cost, Is.EqualTo(preview.Cost * trial.First.Copies));
            Assert.That(run.Session.TickIndex, Is.EqualTo(before));
            Assert.That(run.Workshop.Gold, Is.EqualTo(gold));
            Assert.That(run.Session.Ink, Is.EqualTo(ink));
        }
    }
    internal static class FreeformUiTestDrawing
    {
        public static void Complete(DrawingEditSession session, float bodyHalfSize = .4f)
        {
            var design = session.Blueprint.WithFreeformDrawing(session.Snapshot).FreeformDesign;
            if (design.BodyArea < .06f)
                Assert.That(session.AddStroke(DrawingBindings.Base, new[] {
                    new DrawingPoint(-bodyHalfSize, -bodyHalfSize), new DrawingPoint(bodyHalfSize, -bodyHalfSize),
                    new DrawingPoint(bodyHalfSize, bodyHalfSize), new DrawingPoint(-bodyHalfSize, bodyHalfSize), new DrawingPoint(-bodyHalfSize, -bodyHalfSize) }, out _), Is.True);
            foreach (var mount in session.Blueprint.WeaponMounts)
            {
                if (design.GetBarrel(mount.SlotIndex)?.IsComplete == true) continue;
                Assert.That(session.AddStroke(DrawingBindings.Mount(mount.SlotIndex), new[] {
                    new DrawingPoint(0, 0), new DrawingPoint(0, .6f) }, out _), Is.True);
            }
            var completed=session.Blueprint.WithFreeformDrawing(session.Snapshot).FreeformDesign;
            Assert.That(completed.IsUsable,Is.True,"Shared UI fixture must remain complete and within the real construction ink budget: "+completed.InkUsed+"/"+completed.InkBudget);
        }
    }

}
