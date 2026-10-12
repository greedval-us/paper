using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class ExpandedTrialHudTests
    {
        [Test] public void BlotTrainingIsExplicitAndShowsClearedAndUnresolvedMarks()
        {
            var root = new GameObject("Isolated blot training UI"); root.SetActive(false);
            try
            {
                var run = root.AddComponent<RunController>(); run.InitializeWorkshop(false);
                var hud = root.AddComponent<WorkshopHud>(); hud.Initialize(run);
                var buttons = root.GetComponentsInChildren<Button>(true);
                buttons.Single(b => b.name == "Open trial").onClick.Invoke();
                var blots = buttons.SingleOrDefault(b => b.name == "Trial 3");
                Assert.That(blots, Is.Not.Null, "Support needs an explicitly selectable blot fixture.");
                blots.onClick.Invoke();
                buttons.Single(b => b.name == "Run trial").onClick.Invoke();
                string text = string.Join("\n", root.GetComponentsInChildren<Text>(true).Select(t => t.text)).ToLowerInvariant();
                Assert.That(text, Does.Contain("заданные кляксы"));
                Assert.That(text, Does.Contain("очищено клякс"));
                Assert.That(text, Does.Contain("не очищены"));
                Assert.That(run.Session.TickIndex, Is.Zero);
                Assert.That(run.Workshop.Gold, Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
