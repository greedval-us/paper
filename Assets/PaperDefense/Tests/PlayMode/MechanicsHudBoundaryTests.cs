using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class MechanicsHudBoundaryTests
    {
        GameObject root;
        GameBalanceAsset asset;
        RunController run;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Isolated mechanics HUD"); root.SetActive(false);
            asset = ScriptableObject.CreateInstance<GameBalanceAsset>(); asset.ResetToDefaults();
            run = root.AddComponent<RunController>(); run.balanceAsset = asset;
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(root); Object.DestroyImmediate(asset); }

        Button Button(string name) => root.GetComponentsInChildren<Button>(true).Single(button => button.name == name);
        static string TextField(object owner, string name) => ((Text)owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner)).text;

        [TestCase(DrawingMode.Paint)]
        [TestCase(DrawingMode.Blotter)]
        public void NewRunResetsUnavailableSelectionsAndKeyboardCommandsCannotRestoreThem(DrawingMode oldMode)
        {
            run.InitializeWorkshop(false);
            var input = root.AddComponent<DrawingInput>(); input.Initialize(run, null, null);
            input.SelectColor(InkColor.Red); input.SelectMode(oldMode);
            Assert.That(input.Color, Is.EqualTo(InkColor.Red)); Assert.That(input.Mode, Is.EqualTo(oldMode));

            asset.mechanics.ColorCartridges = asset.mechanics.CorpseBlots = false;
            run.OpenWorkshop();
            Assert.That(input.Color, Is.EqualTo(InkColor.None)); Assert.That(input.Mode, Is.EqualTo(DrawingMode.Build));
            input.SelectColor(InkColor.Green); input.SelectMode(DrawingMode.Paint); input.SelectMode(DrawingMode.Blotter);
            Assert.That(input.Color, Is.EqualTo(InkColor.None)); Assert.That(input.Mode, Is.EqualTo(DrawingMode.Build));
            input.SelectMode(DrawingMode.Erase);
            Assert.That(input.Mode, Is.EqualTo(DrawingMode.Erase), "Required construction tools remain selectable.");
        }

        [Test]
        public void NotebookHidesUnavailableActionsAndForecastsOnlyAvailableThreats()
        {
            asset.mechanics.CorpseBlots = asset.mechanics.ColorCartridges = asset.mechanics.InkWells = false;
            asset.mechanics.RangedEnemies = asset.mechanics.EnemyDrawingTiers = false;
            asset.waves[0].spitters = asset.waves[0].hatched = 1;
            run.InitializeWorkshop(false);
            var input = root.AddComponent<DrawingInput>(); input.Initialize(run, null, null);
            var hud = root.AddComponent<NotebookHud>(); hud.Initialize(run, input);

            Assert.That(Button("Tool " + (ProfileLimits.LoadoutSlotCount + 4)).gameObject.activeSelf, Is.False);
            Assert.That(Button("Tool " + (ProfileLimits.LoadoutSlotCount + 5)).gameObject.activeSelf, Is.False);
            for (int i = 0; i < 4; i++) Assert.That(Button("Ink color " + i).gameObject.activeSelf, Is.False);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(Button("Activate well " + i).gameObject.activeSelf, Is.False);
                Assert.That(Button("Focus well " + i).gameObject.activeSelf, Is.False);
            }
            Assert.That(Button("Tool " + ProfileLimits.LoadoutSlotCount).gameObject.activeSelf, Is.True);
            Assert.That(Button("Focus base").gameObject.activeSelf, Is.True);
            Assert.That(TextField(hud, "forecast"), Does.Not.Contain("Плевуны").And.Not.Contain("штриховка"));
            Assert.That(TextField(hud, "navigationHint"), Does.Not.Contain("окраску"));
        }

    }
}
