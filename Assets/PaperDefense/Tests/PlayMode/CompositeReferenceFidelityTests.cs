#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class CompositeReferenceFidelityTests
    {
        AssemblyCommandFixture fixture;
        GameObject canvas;
        CompositeWorkshopPanel panel;
        [SetUp] public void SetUp()
        {
            fixture = new AssemblyCommandFixture(500, 390);
            canvas = new GameObject("Reference composition", typeof(RectTransform), typeof(Canvas));
            panel = new CompositeWorkshopPanel(canvas.transform, fixture.Run, null, () => { });
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(canvas); fixture.Dispose(); }
        Text Label(string name) => panel.Root.GetComponentsInChildren<Text>().Single(label => label.name == name);
        Button Button(string name) => panel.Root.GetComponentsInChildren<Button>().Single(button => button.name == name);
        void Click(string name) { Button(name).onClick.Invoke(); Canvas.ForceUpdateCanvases(); }

        [TestCase(1024, 768)][TestCase(1600, 900)]
        public void Foundation_HasFourActualFoundationCharacteristicsAndSelectedCard(float width, float height)
        {
            panel.Reflow(width, height);
            var recipe = panel.DraftSession.PreviewRecipe.Tower.Foundation;
            int cost = AssemblyResolver.FoundationCost(recipe, fixture.Catalog, fixture.Balance, MaterialTier.Pencil);
            Assert.That(Label("Composite foundation mount value").text, Is.EqualTo("2"));
            Assert.That(Label("Composite foundation capacity value").text, Is.EqualTo(panel.DraftSession.Resolved.Capacity.ToString()));
            Assert.That(Label("Composite foundation health value").text, Does.Contain(panel.DraftSession.Resolved.Health.ToString("0.#")));
            Assert.That(Label("Composite foundation cost value").text, Does.Contain(cost.ToString()));
            Assert.That(cost, Is.LessThan(panel.DraftSession.Resolved.Cost), "Foundation price excludes the weapon.");
            var selected = panel.Root.GetComponentsInChildren<FoundationDetailGraphic>().Where(mark => mark.Shape == FoundationDetailGraphic.Kind.SelectedCorner);
            Assert.That(selected.Count(), Is.EqualTo(1));
            Assert.That(selected.Single().transform.parent.name, Does.Contain(recipe.Frame.Id));
        }

        [TestCase(1024, 768)][TestCase(1600, 900)]
        public void Weapon_CardsShowResolvedIncrementalPricesAndEachCategoryOpensItsOwnSocket(float width, float height)
        {
            panel.Reflow(width, height); Click("Composite next");
            var prices = panel.Root.GetComponentsInChildren<Text>().Where(label => label.name == "Composite weapon price").ToArray();
            Assert.That(prices.Length, Is.EqualTo(7));
            Assert.That(prices.All(price => price.text.Contains("штр")), Is.True);
            Assert.That(prices.Single(price => price.transform.parent.name == "Composite weapon core.gun").text, Does.Contain("24"));
            foreach (var category in new[] { ComponentCategory.Mechanism, ComponentCategory.Head, ComponentCategory.Sight, ComponentCategory.Magazine, ComponentCategory.Trigger })
            {
                Click("Composite weapon category " + category);
                Assert.That(panel.DraftSession.IsComponentOpen, Is.True);
                Assert.That(panel.DraftSession.ActiveCategory, Is.EqualTo(category));
                panel.DraftSession.CancelComponent(); panel.Refresh();
            }
            Assert.That(fixture.Store.SaveCalls, Is.Zero, "Opening category controls does not publish a draft.");
        }

        [TestCase(1024, 768)][TestCase(1600, 900)]
        public void Review_HasSeparateActualChecksAndCompactNumericalRows(float width, float height)
        {
            panel.Reflow(width, height); Click("Composite next"); Click("Composite next");
            foreach (var name in new[] { "Composite compatibility check", "Composite ownership check", "Composite load cost" })
            {
                var row = Label(name);
                Assert.That(row.text, Is.Not.Empty);
                Assert.That(row.color, Is.EqualTo(WorkshopUi.Ink));
            }
            Assert.That(Label("Composite review cost value").text, Does.Contain(panel.DraftSession.Resolved.Cost.ToString()));
            Assert.That(Label("Composite review health value").text, Does.Contain(panel.DraftSession.Resolved.Health.ToString("0.#")));
            Assert.That(Label("Composite review dps value").text, Does.Contain(panel.DraftSession.Resolved.Dps.ToString("0.##")));
            Assert.That(panel.Root.GetComponentsInChildren<WorkshopIconGraphic>().Count(icon => icon.Icon == WorkshopIcon.Check), Is.GreaterThanOrEqualTo(3));
            Assert.That(Button("Composite trial").IsInteractable(), Is.EqualTo(panel.DraftSession.CanTrial));
        }
    }
}
#endif
