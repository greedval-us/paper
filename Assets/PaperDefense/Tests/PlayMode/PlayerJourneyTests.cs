using System.Collections;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class PlayerJourneyTests
    {
        GameObject root;
        PlayerGameRoot game;
        GameBalanceAsset balance;

        [UnitySetUp] public IEnumerator SetUp()
        {
            foreach (var previous in Object.FindObjectsByType<PlayerGameRoot>()) Object.Destroy(previous.gameObject);
            foreach (var previous in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(previous.gameObject);
            yield return null;
            root = new GameObject("Player journey fixture"); root.SetActive(false);
            var camera = new GameObject("Fixture camera", typeof(Camera)); camera.tag = "MainCamera"; camera.transform.SetParent(root.transform);
            balance = ScriptableObject.CreateInstance<GameBalanceAsset>(); balance.ResetToDefaults(); balance.planningSeconds = 1000;
            game = root.AddComponent<PlayerGameRoot>(); game.Configure(balance, true);
            root.SetActive(true); yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        { Object.Destroy(root); Object.Destroy(balance); yield return null; }

        Button Find(string name)
        {
            foreach (var button in root.GetComponentsInChildren<Button>())
                if (button.name == name) return button;
            Assert.Fail("Missing active control: " + name); return null;
        }

        [UnityTest] public IEnumerator FreshLaunchShowsMenuWithoutAdvancingOrWritingProfile()
        {
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
            Assert.That(game.Run.InWorkshop, Is.True);
            Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
            Assert.That(game.Drawing.enabled, Is.False);
            long tick = game.Run.Session.TickIndex; yield return null;
            Assert.That(game.Run.Session.TickIndex, Is.EqualTo(tick));
            Assert.That(root.GetComponentsInChildren<NotebookHud>(true), Is.Empty);
            Assert.That(Find("Open campaign").GetComponentInChildren<PlayerIconGraphic>(), Is.Not.Null);
            Assert.That(Find("Open campaign").GetComponentsInChildren<Text>(), Is.Empty);
        }

        [UnityTest] public IEnumerator IconNavigationReachesRealBattleWithFrozenLoadout()
        {
            Find("Open campaign").onClick.Invoke();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Map));
            Find("Prepare mission").onClick.Invoke();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Find("Starting material 1").onClick.Invoke();
            Find("Start battle").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle));
            Assert.That(game.Run.InWorkshop, Is.False);
            Assert.That(game.Run.BuildLoadout.Count, Is.EqualTo(4));
            Assert.That(game.Drawing.Tier, Is.EqualTo(MaterialTier.Ink));
            Assert.That(game.Drawing.enabled, Is.True);
            Assert.That(game.Drawing.HandleSessionHotkeys, Is.False);
        }

        [UnityTest] public IEnumerator PauseBlocksConstructionAndSimulationUntilResume()
        {
            Assert.That(game.StartBattle(), Is.True); yield return null;
            Find("Battle pause").onClick.Invoke();
            long tick = game.Run.Session.TickIndex;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Pause));
            Assert.That(game.Drawing.enabled, Is.False);
            Assert.That(game.Run.Paused, Is.True);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(game.Run.Session.TickIndex, Is.EqualTo(tick));
            Find("Resume").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle));
            Assert.That(game.Run.Paused, Is.False);
            Assert.That(game.Drawing.enabled, Is.True);
        }

        [UnityTest] public IEnumerator SettingsKeepBattlePausedAndReturnToPause()
        {
            game.StartBattle(); game.Pause();
            Find("Pause settings").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Settings));
            Assert.That(game.Run.Paused, Is.True); Assert.That(game.Drawing.enabled, Is.False);
            bool ranges = game.World.ShowRanges;
            Find("Toggle ranges").onClick.Invoke();
            Assert.That(game.World.ShowRanges, Is.EqualTo(!ranges));
            Find("Close settings").onClick.Invoke();
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Pause));
        }

        [UnityTest] public IEnumerator PauseBlocksKeyboardSubmitOnPreviouslyFocusedBattleControl()
        {
            game.StartBattle(); yield return null;
            var speed = Find("Battle speed");
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(speed.gameObject);
            game.Pause();
            int previousSpeed = game.Run.Speed;
            Assert.That(speed.IsInteractable(), Is.False);
            UnityEngine.EventSystems.ExecuteEvents.Execute(speed.gameObject,
                new UnityEngine.EventSystems.BaseEventData(UnityEngine.EventSystems.EventSystem.current),
                UnityEngine.EventSystems.ExecuteEvents.submitHandler);
            Assert.That(game.Run.Speed, Is.EqualTo(previousSpeed));
            game.Resume(); Assert.That(speed.IsInteractable(), Is.True);
        }

        [UnityTest] public IEnumerator LeavingBattleKeepsAlbumAndReturnsToSafePreparation()
        {
            string blueprint = game.Run.Workshop.Loadout[0].Id;
            int gold = game.Run.Workshop.Gold;
            game.StartBattle(); game.Pause(); Find("Leave battle").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Map));
            Assert.That(game.Run.InWorkshop, Is.True); Assert.That(game.Drawing.enabled, Is.False);
            Assert.That(game.Run.Workshop.Loadout[0].Id, Is.EqualTo(blueprint));
            Assert.That(game.Run.Workshop.Gold, Is.EqualTo(gold));
        }

        [UnityTest] public IEnumerator ExistingWorkshopReturnsToPreparationBeforeStartingRun()
        {
            Find("workshop").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Workshop));
            Assert.That(Find("Start run"), Is.Not.Null);
            Find("Start run").onClick.Invoke(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
            Assert.That(game.Run.InWorkshop, Is.True);
            Assert.That(root.GetComponentInChildren<WorkshopHud>(true).PresentationVisible, Is.False);
        }

        [UnityTest] public IEnumerator UpgradeChoicesDisplayAmountsFromTheActiveRules()
        {
            Assert.That(game.StartBattle(), Is.True);
            game.Navigate(PlayerPage.Upgrades); yield return null;
            var rules = game.Run.Session.Balance.Progression;
            var labels = new System.Collections.Generic.List<string>();
            foreach (var label in root.GetComponentsInChildren<Text>()) labels.Add(label.text);
            Assert.That(labels, Does.Contain("Темп стрельбы +" + Mathf.RoundToInt((rules.RapidFireMultiplier - 1) * 100) + "%"));
            Assert.That(labels, Does.Contain("Сразу +" + rules.SalvageImmediateInk + " штрихов\nНаграда волн +" +
                Mathf.RoundToInt((rules.SalvageWaveRewardMultiplier - 1) * 100) + "%\nВозврат +" + Mathf.RoundToInt(rules.SalvageRefundBonus * 100) + " п.п."));
            Assert.That(Find("Choose upgrade 2"), Is.Not.Null);
        }
    }
}
