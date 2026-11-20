#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class SequentialStructureWorkshopUiTests
    {
        AssemblyCommandFixture fixture;
        GameObject events;
        CompositeWorkshopPanel panel;

        [SetUp] public void SetUp()
        {
            events = new GameObject("Structure workshop events", typeof(EventSystem));
            OpenWorkshop(500, 390);
        }
        void OpenWorkshop(int gold, int xp)
        {
            fixture?.Dispose();
            fixture = new AssemblyCommandFixture(gold: gold, xp: xp);
            var hud = fixture.Root.AddComponent<WorkshopHud>(); hud.Initialize(fixture.Run); panel = hud.Composite;
            Click("Composite route structure");
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(events); fixture.Dispose(); }
        Button Find(string name) => panel.Root.GetComponentsInChildren<Button>(true).Single(item => item.name == name);
        Text Label(string name) => panel.Root.GetComponentsInChildren<Text>(true).Single(item => item.name == name);
        InputField NameField() => panel.Root.GetComponentsInChildren<InputField>(true).Single(item => item.name == "Structure name");
        void Click(string name)
        {
            var button = Find(name); Assert.That(button.IsInteractable(), Is.True, name + ": " + panel.Status);
            button.onClick.Invoke(); Canvas.ForceUpdateCanvases();
        }

        [Test] public void StructureEntranceOrdersFamilyCompositionReviewAndStartsWithFourOrdinaryParts()
        {
            Assert.That(panel.Root.GetComponentsInChildren<Button>(true).Count(item => item.name.StartsWith("Structure step ")), Is.EqualTo(3));
            Assert.That(Find("Structure step 2").interactable, Is.False, "A new structure reaches review through composition.");
            foreach (var family in new[] { "Wall", "Slow", "Repair", "Generator", "Spikes", "Beacon", "Prism" }) Assert.That(Find("Structure family " + family), Is.Not.Null);
            Click("Structure next");
            Assert.That(panel.DraftSession.ActiveComponent, Is.EqualTo(WorkshopComponentKind.Structure));
            foreach (var category in new[] { "Site", "Function", "Shell", "Utility" }) Assert.That(Find("Structure category " + category), Is.Not.Null);
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Shell.Id, Is.EqualTo("shell.standard"));
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Utility.Id, Is.EqualTo("utility.none"));
        }

        [TestCase(AssemblyStructureFamily.Wall, 200f, 0f, 0)]
        [TestCase(AssemblyStructureFamily.Slow, 55f, 0f, 0)]
        [TestCase(AssemblyStructureFamily.Repair, 70f, 6f, 0)]
        [TestCase(AssemblyStructureFamily.Generator, 80f, 0f, 18)]
        [TestCase(AssemblyStructureFamily.Spikes, 45f, 0f, 0)]
        [TestCase(AssemblyStructureFamily.Beacon, 65f, 0f, 0)]
        [TestCase(AssemblyStructureFamily.Prism, 72f, 0f, 0)]
        public void EveryFamilyCanBeReviewedSavedAndAssignedWithItsProductionSupportStats(AssemblyStructureFamily family, float health, float repair, int income)
        {
            var offer = fixture.Run.Workshop.GetStructureOffer(family);
            Click("Structure family " + family);
            if (!offer.Owned) Click("Structure purchase family");
            Click("Structure next"); Click("Structure next");
            Assert.That(panel.DraftSession.IsComponentOpen, Is.False);
            Assert.That(panel.DraftSession.Resolved.Health, Is.EqualTo(health));
            Assert.That(panel.DraftSession.Resolved.Support.Kind, Is.EqualTo(family));
            Assert.That(panel.DraftSession.Resolved.Support.RepairPerSecond, Is.EqualTo(repair));
            Assert.That(panel.DraftSession.Resolved.Support.IncomePerWave, Is.EqualTo(income));
            if (family == AssemblyStructureFamily.Repair) Assert.That(Label("Structure totals").text, Does.Contain("HP/с"));
            if (family == AssemblyStructureFamily.Generator) Assert.That(Label("Structure totals").text, Does.Contain("штр./волну"));
            NameField().text = "Моё сооружение " + family;
            Assert.That(Find("Structure assign").interactable, Is.False, "Only a saved recipe can enter the battle set.");
            Click("Structure save"); Click("Structure slot 7"); Click("Structure assign");
            Assert.That(fixture.Run.Workshop.LoadoutEntries[7].Id, Is.EqualTo(panel.DraftSession.Draft.Id));
            Assert.That(fixture.Run.Workshop.LoadoutEntries[7].Name, Is.EqualTo("Моё сооружение " + family));
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(500 - offer.GoldCost));
        }

        [Test] public void NewPlayerCanInspectThePrismAndSeeBothMissingRequirementsAtSmallSize()
        {
            OpenWorkshop(40, 0);
            panel.Reflow(800, 600);
            Click("Structure family Prism");
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Family, Is.EqualTo(AssemblyStructureFamily.Wall));
            Assert.That(Label("Structure offer purpose").text, Does.Contain("поджигает").And.Contain("замедляет").And.Contain("ослабляет броню"));
            Assert.That(Label("Structure offer terms").text, Does.Contain("уровень 3").And.Contain("80 зол."));
            var purchase = Find("Structure purchase family");
            Assert.That(purchase.IsInteractable(), Is.False);
            AssertInside((RectTransform)purchase.transform);
            Assert.That(Find("Structure step 1").IsInteractable(), Is.False);
            Click("Structure family Slow");
            Assert.That(Find("Structure next").IsInteractable(), Is.True);
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(40));
        }

        [Test] public void RefusedPrismPurchaseCanBeRetriedBeforeBuildingAndAssigningTheNewPage()
        {
            var previous = fixture.Run.Workshop.LoadoutEntries[7].Id;
            Click("Structure family Prism");
            fixture.Store.RefuseWrites = true;
            Click("Structure purchase family");
            Assert.That(panel.Status, Does.Contain("Запись не удалась"));
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(500));
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Family, Is.EqualTo(AssemblyStructureFamily.Wall));
            fixture.Store.RefuseWrites = false;
            Click("Structure purchase family");
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(380));
            Assert.That(fixture.Run.Workshop.LoadoutEntries[7].Id, Is.EqualTo(previous));
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Family, Is.EqualTo(AssemblyStructureFamily.Prism));
            Click("Structure next"); Click("Structure next"); Click("Structure save"); Click("Structure slot 7"); Click("Structure assign");
            Assert.That(fixture.Run.Workshop.LoadoutEntries[7].Snapshot.Recipe.Structure.Family, Is.EqualTo(AssemblyStructureFamily.Prism));
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(380));
        }

        [Test] public void InspectingALightShellShowsItsConsequencesBeforePurchaseAndInstallation()
        {
            Click("Structure next"); Click("Structure category Shell"); Click("Structure inspect shell.light");
            Assert.That(panel.DraftSession.Resolved.Health, Is.EqualTo(200));
            Assert.That(Label("Structure candidate totals").text, Does.Contain("200 → 150 (-50)"));
            Click("Structure candidate details");
            Assert.That(Label("Context help text").text, Does.Contain("Плюс:").And.Contain("Минус:").And.Contain("После установки:"));
            Click("Close context help");
            Click("Structure install shell.light");
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(465));
            Assert.That(panel.DraftSession.Resolved.Health, Is.EqualTo(150));
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Shell.Id, Is.EqualTo("shell.light"));
            Assert.That(Find("Structure install shell.light").interactable, Is.False);
            Click("Structure inspect shell.standard"); Click("Structure install shell.standard");
            Click("Structure inspect shell.light"); Click("Structure install shell.light");
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(465), "A permanent unlock is not charged twice.");
        }

        [Test] public void PurchaseWriteFailureLeavesTheOrdinaryShellAndGoldUnchanged()
        {
            Click("Structure next"); Click("Structure category Shell"); Click("Structure inspect shell.light");
            fixture.Store.RefuseWrites = true; Click("Structure install shell.light");
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Shell.Id, Is.EqualTo("shell.standard"));
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(500));
            Assert.That(panel.DraftSession.LastResult.Code, Is.EqualTo("profile.write_failed"));
            fixture.Store.RefuseWrites = false; Click("Structure install shell.light");
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Shell.Id, Is.EqualTo("shell.light"));
        }

        [Test] public void SelectingTheCurrentFamilyKeepsItsEditedRecipeAndPurchasedParts()
        {
            Click("Structure next"); Click("Structure category Shell"); Click("Structure inspect shell.light"); Click("Structure install shell.light");
            NameField().text = "Лёгкая баррикада";
            var before = panel.DraftSession.PreviewRecipe;
            Click("Structure back"); Click("Structure family Wall");
            Assert.That(panel.DraftSession.PreviewRecipe, Is.SameAs(before));
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Shell.Id, Is.EqualTo("shell.light"));
            Assert.That(panel.DraftSession.Name, Is.EqualTo("Лёгкая баррикада"));
            Assert.That(fixture.Run.Workshop.IsUnlocked("shell.light"), Is.True);
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(465));
            Assert.That(panel.Root.Find("Workshop confirmation overlay"), Is.Null);
        }

        [TestCase(800, 600)] [TestCase(1024, 768)]
        public void ChangingFamilyRequiresAnExplicitDecisionBeforeReplacingEditedParts(int width, int height)
        {
            panel.Reflow(width, height);
            Click("Structure next"); Click("Structure category Shell"); Click("Structure inspect shell.light"); Click("Structure install shell.light");
            NameField().text = "Моё сооружение"; Click("Structure back");
            Click("Structure family Repair");
            var confirmation = panel.Root.Find("Workshop confirmation overlay/Workshop confirmation") as RectTransform;
            Assert.That(confirmation, Is.Not.Null); AssertInside(confirmation);
            AssertInside(Find("Workshop cancel confirmation").transform as RectTransform); AssertInside(Find("Workshop confirm action").transform as RectTransform);
            Click("Workshop cancel confirmation");
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Family, Is.EqualTo(AssemblyStructureFamily.Wall));
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Shell.Id, Is.EqualTo("shell.light"));
            Assert.That(panel.DraftSession.Name, Is.EqualTo("Моё сооружение"));
            Click("Structure family Repair"); Click("Workshop confirm action");
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Family, Is.EqualTo(AssemblyStructureFamily.Repair));
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Shell.Id, Is.EqualTo("shell.standard"));
            Assert.That(panel.DraftSession.Name, Is.EqualTo("Моё сооружение"));
            Assert.That(fixture.Run.Workshop.IsUnlocked("shell.light"), Is.True);
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(465));
            Assert.That(fixture.Store.SaveCalls, Is.EqualTo(1), "Family replacement remains local; the permanent purchase is the only durable command.");
        }

        [Test] public void APartAboveTheCurrentLevelCanBeInspectedButCannotBePurchased()
        {
            Click("Structure family Generator"); Click("Structure next"); Click("Structure category Utility"); Click("Structure inspect utility.economy");
            Assert.That(Find("Structure install utility.economy").interactable, Is.False);
            Assert.That(Label("Structure conditions").text, Does.Contain("8").And.Contain("180"));
            Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(500));
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Utility.Id, Is.EqualTo("utility.none"));
        }

        [Test] public void AnEmptyNameBlocksReviewUntilTheDraftHasAValidName()
        {
            Click("Structure next"); NameField().text = "";
            Assert.That(Find("Structure next").interactable, Is.False);
            NameField().text = "Моя баррикада"; Click("Structure next");
            Assert.That(Find("Structure save").interactable, Is.True);
        }

        [Test] public void FailedSaveKeepsTheNameAndRecipeEditableAndDoesNotAssignAnUnsavedPage()
        {
            Click("Structure next"); Click("Structure category Shell"); Click("Structure inspect shell.light"); Click("Structure install shell.light");
            Click("Structure next"); NameField().text = "Несохранённая баррикада";
            fixture.Store.RefuseWrites = true; Click("Structure save");
            Assert.That(panel.Step, Is.EqualTo(2)); Assert.That(NameField().text, Is.EqualTo("Несохранённая баррикада"));
            Assert.That(Find("Structure assign").interactable, Is.False);
            Assert.That(fixture.Run.Workshop.Entries.Any(entry => entry.Id == panel.DraftSession.Draft.Id), Is.False);
            Click("Structure back");
            Assert.That(panel.DraftSession.ActiveComponent, Is.EqualTo(WorkshopComponentKind.Structure));
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Shell.Id, Is.EqualTo("shell.light"));
            NameField().text = "Исправленная баррикада"; fixture.Store.RefuseWrites = false;
            Click("Structure next"); Click("Structure save");
            Assert.That(fixture.Run.Workshop.Entries.Single(entry => entry.Id == panel.DraftSession.Draft.Id).Name, Is.EqualTo("Исправленная баррикада"));
        }

        [Test] public void SwitchingBetweenTowerAndStructurePreservesEachEditableDraftAndStage()
        {
            Click("Structure family Repair"); Click("Structure next"); Click("Structure category Shell");
            Click("Structure inspect shell.light"); Click("Structure install shell.light"); NameField().text = "Полевая мастерская";
            Click("Composite route tower");
            Assert.That(panel.DraftSession.ActiveComponent, Is.EqualTo(WorkshopComponentKind.Foundation));
            Click("Sequence next"); Assert.That(panel.Step, Is.EqualTo(1));
            Click("Composite route structure");
            Assert.That(panel.Step, Is.EqualTo(1)); Assert.That(NameField().text, Is.EqualTo("Полевая мастерская"));
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Family, Is.EqualTo(AssemblyStructureFamily.Repair));
            Assert.That(panel.DraftSession.PreviewRecipe.Structure.Shell.Id, Is.EqualTo("shell.light"));
            Assert.That(panel.DraftSession.ActiveCategory, Is.EqualTo(ComponentCategory.Shell));
            Click("Composite route tower"); Assert.That(panel.Step, Is.EqualTo(1));
            Assert.That(panel.DraftSession.ActiveComponent, Is.EqualTo(WorkshopComponentKind.Weapon));
        }

        [Test] public void FailedAssignmentKeepsThePreviousBattleSlotAndCanRetryTheSavedStructure()
        {
            Click("Structure next"); Click("Structure next"); NameField().text = "Запасная баррикада"; Click("Structure save"); Click("Structure slot 7");
            string previous = fixture.Run.Workshop.LoadoutEntries[7].Id;
            fixture.Store.RefuseWrites = true; Click("Structure assign");
            Assert.That(fixture.Run.Workshop.LoadoutEntries[7].Id, Is.EqualTo(previous));
            Assert.That(NameField().text, Is.EqualTo("Запасная баррикада"));
            Assert.That(panel.Status, Is.Not.Empty);
            fixture.Store.RefuseWrites = false; Click("Structure assign");
            Assert.That(fixture.Run.Workshop.LoadoutEntries[7].Id, Is.EqualTo(panel.DraftSession.Draft.Id));
        }

        [TestCase(800, 600)] [TestCase(1024, 768)]
        public void CompactStructureEditorAndReviewKeepPrimaryActionsInsideTheWorkshop(int width, int height)
        {
            panel.Reflow(width, height); Click("Structure next"); Click("Structure category Shell"); Click("Structure inspect shell.light");
            AssertInside(Find("Structure next").transform as RectTransform); AssertInside(Find("Structure install shell.light").transform as RectTransform);
            Click("Structure next");
            AssertInside(NameField().transform as RectTransform); AssertInside(Find("Structure save").transform as RectTransform); AssertInside(Find("Structure assign").transform as RectTransform);
        }
        void AssertInside(RectTransform control)
        {
            Assert.That(control.rect.width, Is.GreaterThan(0)); Assert.That(control.rect.height, Is.GreaterThan(0));
            var corners = new Vector3[4]; control.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var local = panel.Root.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(panel.Root.rect.xMin - .1f, panel.Root.rect.xMax + .1f));
                Assert.That(local.y, Is.InRange(panel.Root.rect.yMin - .1f, panel.Root.rect.yMax + .1f));
            }
        }
    }
}
#endif
