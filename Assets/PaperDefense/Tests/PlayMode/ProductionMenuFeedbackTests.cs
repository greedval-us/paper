#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class ProductionMenuFeedbackTests
    {
        GameObject root;
        GameBalanceAsset balance;
        PlayerGameRoot game;
        PlayerShell shell;
        CompositeWorkshopPanel workshop;
        float timeScale;

        [UnitySetUp] public IEnumerator SetUp()
        {
            timeScale = Time.timeScale;
            foreach (var previous in Object.FindObjectsByType<PlayerGameRoot>()) Object.Destroy(previous.gameObject);
            foreach (var previous in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(previous.gameObject);
            yield return null;
            balance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/Game/PlayerBalance.asset"));
            root = new GameObject("Production menu feedback fixture — memory only"); root.SetActive(false);
            game = root.AddComponent<PlayerGameRoot>(); game.Configure(balance, true); root.SetActive(true);
            game.Run.AutomaticSimulation = false;
            shell = root.GetComponentInChildren<PlayerShell>(); shell.enabled = false;
            workshop = root.GetComponentInChildren<WorkshopHud>(true).Composite;
            foreach (var responsive in root.GetComponentsInChildren<WorkshopResponsiveLayout>(true)) responsive.enabled = false;
            yield return null;
            Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
        }

        Button Named(string name) => root.GetComponentsInChildren<Button>().Single(button => button.name == name);
        static PointerEventData Pointer() => new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        static void Enter(Button button) => ExecuteEvents.Execute(button.gameObject, Pointer(), ExecuteEvents.pointerEnterHandler);
        static void Exit(Button button) => ExecuteEvents.Execute(button.gameObject, Pointer(), ExecuteEvents.pointerExitHandler);

        [UnityTest] public IEnumerator OpeningAndRebuildingProductionMenusRetainsVisibleKeyboardFocus()
        {
            game.ReturnToHome();
            yield return new WaitForSecondsRealtime(.18f); AssertVisibleFocus();
            game.OpenAlbum();
            EventSystem.current.SetSelectedGameObject(Named("Album choose album.guard").gameObject);
            yield return new WaitForSecondsRealtime(.18f); AssertVisibleFocus();
            shell.Album.SelectEntry("album.double");
            yield return new WaitForSecondsRealtime(.18f); AssertVisibleFocus();
            game.EditAlbumEntry(null);
            yield return new WaitForSecondsRealtime(.18f); AssertVisibleFocus();
            workshop.Reflow(800, 600);
            yield return new WaitForSecondsRealtime(.18f); AssertVisibleFocus();
            Named("Composite route structure").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.18f); AssertVisibleFocus();
        }

        static void AssertVisibleFocus()
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(selected, Is.Not.Null);
            var feedback = selected.GetComponent<UiInteractionFeedback>();
            Assert.That(feedback, Is.Not.Null, selected.name);
            Assert.That(feedback.FocusAmount, Is.GreaterThan(.9f), selected.name + " must show its restored keyboard focus without another move.");
        }

        [UnityTest] public IEnumerator HelpAndResultBuildersRetainTheirInitiallySelectedFocus()
        {
            var page = Named("Open campaign").GetComponentInParent<FieldMenuTheme>().transform;
            var help = UiHelp.Show(page, "Справка", "Проверка доступной с клавиатуры подсказки.");
            yield return new WaitForSecondsRealtime(.18f); AssertVisibleFocus();
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Close context help"));
            Object.Destroy(help); yield return null;
            game.Navigate(PlayerPage.Result);
            yield return new WaitForSecondsRealtime(.18f); AssertVisibleFocus();
        }

        [UnityTest] public IEnumerator ExistingReducedMotionSettingSnapsFeedbackWithoutMovingTheControl()
        {
            game.SetReducedMotion(true);
            var button = Named("Open campaign"); var feedback = button.GetComponent<UiInteractionFeedback>();
            Assert.That(feedback, Is.Not.Null);
            var rect = button.GetComponent<RectTransform>(); var position = rect.anchoredPosition; var size = rect.sizeDelta;
            Enter(button);
            Assert.That(feedback.HoverAmount, Is.EqualTo(1), "Reduced motion must apply the endpoint immediately.");
            ExecuteEvents.Execute(button.gameObject, Pointer(), ExecuteEvents.pointerDownHandler);
            Assert.That(feedback.PressAmount, Is.EqualTo(1));
            ExecuteEvents.Execute(button.gameObject, Pointer(), ExecuteEvents.pointerUpHandler); Exit(button);
            Assert.That(feedback.HoverAmount, Is.Zero); Assert.That(feedback.PressAmount, Is.Zero);
            Assert.That(rect.anchoredPosition, Is.EqualTo(position)); Assert.That(rect.sizeDelta, Is.EqualTo(size));
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = timeScale; EventSystem.current?.SetSelectedGameObject(null);
            if (root != null) Object.Destroy(root); if (balance != null) Object.Destroy(balance);
            yield return null;
        }
    }
}
#endif
