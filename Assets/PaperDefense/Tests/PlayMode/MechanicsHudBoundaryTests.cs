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
        public void AlbumBattleDisablesUnavailableActionsAndHidesAbsentInkWells()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                fixture.Preset.rules.Mechanics.CorpseBlots = false;
                fixture.Preset.rules.Mechanics.ColorCartridges = false;
                fixture.Preset.rules.Mechanics.InkWells = false;
                var owner = new GameObject("Released mechanics HUD"); owner.SetActive(false);
                try
                {
                    var game = owner.AddComponent<PlayerGameRoot>(); game.Configure(fixture.BalanceAsset, true);
                    owner.SetActive(true); game.Run.AutomaticSimulation = false;
                    Assert.That(game.StartBattle(), Is.True);
                    Canvas.ForceUpdateCanvases();
                    Button Action(string name) => owner.GetComponentsInChildren<Button>().Single(button => button.name == name);
                    Assert.That(Action("Album battle blotter").IsInteractable(), Is.False);
                    Assert.That(owner.GetComponentsInChildren<Button>().Any(button => button.name == "Album battle paint" ||
                        button.name.StartsWith("Material ") || button.name.StartsWith("Ink ") && !button.name.StartsWith("Ink well ")), Is.False,
                        "Production construction has no prebuild material or color controls.");
                    Assert.That(owner.GetComponentsInChildren<Button>(true)
                        .Where(button => button.name.StartsWith("Ink well "))
                        .All(button => !button.gameObject.activeInHierarchy), Is.True);
                    Assert.That(Action("Album battle erase").IsInteractable(), Is.True);
                    Assert.That(Action("Build tool 0").IsInteractable(), Is.True);
                    Assert.That(owner.GetComponentsInChildren<NotebookHud>(true), Is.Empty);
                }
                finally { Object.DestroyImmediate(owner); }
            }
        }

    }
}
#endif
