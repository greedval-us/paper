using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class ExpandedCatalogHudTests
    {
        [TestCase("Fan", "конус")]
        [TestCase("Ricochet", "цепочка")]
        [TestCase("Stapler", "замедл")]
        [TestCase("Blotter", "клякс")]
        public void NewMountCanBeChosenAndItsPurposeIsExplained(string kind, string explanation)
        {
            var root = new GameObject("Isolated expanded catalog"); root.SetActive(false);
            try
            {
                var run = root.AddComponent<RunController>(); run.InitializeWorkshop(false, allowLegacyFixtureDefaults: true);
                var hud = root.AddComponent<WorkshopHud>(); hud.Initialize(run);
                var button = root.GetComponentsInChildren<Button>(true).SingleOrDefault(b => b.name == "First " + kind);
                Assert.That(button, Is.Not.Null, "Every implemented mount must be selectable in the workshop.");
                button.onClick.Invoke();
                Assert.That(hud.Draft.FirstWeapon.ToString(), Is.EqualTo(kind));
                Assert.That(hud.Status.ToLowerInvariant(), Does.Contain(explanation));
                Assert.That(hud.Draft.Origin, Is.EqualTo(DrawingOrigin.PlayerDrawing));
                Assert.That(hud.Draft.Drawing.IsEmpty, Is.True, "A new assembly must await its own hand drawing.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
