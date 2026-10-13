using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class WorkshopReadabilityTests
    {
        GameObject root;

        [SetUp] public void SetUp()
        {
            root = new GameObject("Isolated workshop text readability"); root.SetActive(false);
            var run = root.AddComponent<RunController>(); run.InitializeWorkshop(false, allowLegacyFixtureDefaults: true);
            var hud = root.AddComponent<WorkshopHud>(); hud.Initialize(run);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [TestCase("Save blueprint as new")]
        [TestCase("Open drawing editor")]
        public void CompactActionCaptionRendersCompletelyInsideItsButton(string name)
        {
            AssertFits(Button(name).GetComponentInChildren<Text>(true));
        }

        [Test] public void AlbumCardsRenderCompositionHealthAndFireRateBelowTheirNames()
        {
            Button("New blueprint").onClick.Invoke();
            Button("Second Gun").onClick.Invoke();
            Button("Open drawing editor").onClick.Invoke();
            var editor = root.GetComponent<WorkshopHud>().DrawingEditor;
            Assert.That(editor.Session.AddStroke(DrawingPartKey.Mount0,
                new[] { new DrawingPoint(0, 0), new DrawingPoint(0, .4f) }, out _), Is.True);
            Assert.That(editor.Session.AddStroke(DrawingPartKey.Mount1,
                new[] { new DrawingPoint(.1f, 0), new DrawingPoint(.1f, .4f) }, out _), Is.True);
            FreeformUiTestDrawing.Complete(editor.Session); Button("Apply drawing").onClick.Invoke();
            Button("Save blueprint").onClick.Invoke();
            var cards = root.GetComponentsInChildren<Button>(true)
                .Where(b => b.name.StartsWith("Album drawing ") && b.gameObject.activeSelf).ToArray();
            Assert.That(cards.Length, Is.EqualTo(5), "Both standard pages and the saved author drawing must be checked");
            Assert.That(cards.Select(b => b.GetComponentInChildren<Text>(true).text), Has.None.Contains("/100"));
            foreach (var card in cards)
            {
                var label = card.GetComponentInChildren<Text>(true);
                Assert.That(label.text, Does.Contain("\n"), card.name);
                Assert.That(label.text.Split('\n')[1], Does.Contain("HP"), card.name);
                Assert.That(label.text.Split('\n')[1], Does.Contain("/с"), card.name);
                AssertFits(label);
            }
        }

        [Test] public void LockedPartButtonsRenderTheirGoldPriceBelowThePartName()
        {
            var unlocks = root.GetComponentsInChildren<Button>(true)
                .Where(b => b.name.StartsWith("Unlock ") && b.interactable).ToArray();
            Assert.That(unlocks, Is.Not.Empty);
            foreach (var button in unlocks)
            {
                var label = button.GetComponentInChildren<Text>(true);
                Assert.That(label.text, Does.Contain("\n"), button.name);
                Assert.That(label.text.Split('\n')[1], Does.Contain("золота"), button.name);
                AssertFits(label);
            }
        }

        Button Button(string name) => root.GetComponentsInChildren<Button>(true).Single(b => b.name == name);

        static void AssertFits(Text label)
        {
            string context = label.transform.parent.name + ": " + label.text.Replace('\n', '|');
            var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            using (var clipped = new TextGenerator())
            using (var complete = new TextGenerator())
            {
                clipped.Populate(label.text, settings);
                settings.verticalOverflow = VerticalWrapMode.Overflow;
                complete.Populate(label.text, settings);
                Assert.That(complete.vertexCount, Is.GreaterThan(4), context + " must contain visible glyphs");
                Assert.That(clipped.vertexCount, Is.EqualTo(complete.vertexCount),
                    context + " loses glyphs when confined to its actual rectangle");
            }
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f),
                context + " needs enough height for every text line without drawing over adjacent controls");
        }
    }
}
