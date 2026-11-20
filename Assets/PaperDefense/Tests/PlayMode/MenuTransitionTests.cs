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
    /// <summary>Entrances decorate real menus without postponing navigation, modality or input.</summary>
    public sealed class MenuTransitionTests
    {
        GameObject root;
        GameBalanceAsset balance;
        PlayerGameRoot game;
        PlayerShell shell;
        CompositeWorkshopPanel workshop;
        float previousTimeScale;
        static readonly Vector2Int[] Sizes = { new Vector2Int(800, 600), new Vector2Int(1600, 900) };

        [UnitySetUp] public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            foreach (var previous in Object.FindObjectsByType<PlayerGameRoot>()) Object.Destroy(previous.gameObject);
            foreach (var previous in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(previous.gameObject);
            yield return null;
            var source = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            Assert.That(source, Is.Not.Null);
            balance = Object.Instantiate(source); balance.planningSeconds = 1000;
            root = new GameObject("Menu transition fixture — memory only"); root.SetActive(false);
            game = root.AddComponent<PlayerGameRoot>(); game.Configure(balance, true); root.SetActive(true);
            game.Run.AutomaticSimulation = false;
            shell = root.GetComponentInChildren<PlayerShell>(); shell.enabled = false;
            workshop = root.GetComponentInChildren<WorkshopHud>(true).Composite;
            foreach (var responsive in root.GetComponentsInChildren<WorkshopResponsiveLayout>(true)) responsive.enabled = false;
            shell.ApplyLayout(800, 600);
            yield return null;
            Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale;
            EventSystem.current?.SetSelectedGameObject(null);
            if (root != null) Object.Destroy(root);
            if (balance != null) Object.Destroy(balance);
            yield return null;
        }

        [Test] public void ReducedMotionStopsBakedEnemyPosesButKeepsTheirDeathFade()
        {
            string id = NotebookAnimationState.EnemyId(PaperDefense.Core.EnemyKind.Runner,
                PaperDefense.Core.DrawingTier.Sketch, NotebookEnemyMotion.Walk);
            var normal = (game.World.VisualTheme ?? NotebookVisualThemeAsset.Default).FindClip(id);
            Assert.That(normal.Frames.Select(frame => frame.ShapeId).Distinct().Count(), Is.GreaterThan(1));
            game.SetReducedMotion(true);
            var reduced = game.World.VisualTheme.FindClip(id);
            Assert.That(reduced.Frames.Select(frame => frame.ShapeId).Distinct().Count(), Is.EqualTo(1),
                "Reduced motion must also freeze authored limbs and jaws, not only frame transforms.");
            var death = game.World.VisualTheme.FindClip(NotebookAnimationState.EnemyId(PaperDefense.Core.EnemyKind.Runner,
                PaperDefense.Core.DrawingTier.Sketch, NotebookEnemyMotion.Death));
            Assert.That(death.Frames.Select(frame => frame.Alpha).Distinct().Count(), Is.GreaterThan(1));
            game.SetReducedMotion(false);
            Assert.That((game.World.VisualTheme ?? NotebookVisualThemeAsset.Default).FindClip(id), Is.SameAs(normal));
        }

        T Named<T>(string name) where T : Component
        {
            var item = root.GetComponentsInChildren<T>().SingleOrDefault(candidate => candidate.name == name);
            Assert.That(item, Is.Not.Null, "Missing active production control: " + name);
            return item;
        }

        void Click(string name)
        {
            var button = Named<Button>(name);
            Assert.That(button.IsInteractable(), Is.True, name + " must work during its entrance.");
            button.onClick.Invoke();
        }

        static UiMenuMotion MotionOn(GameObject target)
        {
            var motion = target.GetComponent<UiMenuMotion>() ?? target.GetComponentInChildren<UiMenuMotion>(true);
            Assert.That(motion, Is.Not.Null, target.name + " must have an incoming UiMenuMotion.");
            return motion;
        }

        static void AssertEntering(UiMenuMotion motion)
        {
            Assert.That(motion.Opacity, Is.InRange(0f, .999f));
            Assert.That(motion.IsAnimating, Is.True);
        }

        static void AssertSettled(UiMenuMotion motion)
        {
            Assert.That(motion.Opacity, Is.EqualTo(1).Within(.0001f));
            Assert.That(motion.IsAnimating, Is.False);
        }

        void HomeAt(Vector2Int size)
        {
            game.ReturnToHome(); shell.ApplyLayout(size.x, size.y); Canvas.ForceUpdateCanvases();
        }

        static PointerEventData Pointer() => new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };

        [Test] public void HomeToMapBeginsAnEntranceWithNavigationAndFocusAlreadyUsable()
        {
            foreach (var size in Sizes)
            {
                HomeAt(size); Click("Open campaign"); Click("Open mission");

                Assert.That(game.Page, Is.EqualTo(PlayerPage.Map));
                var page = Named<RectTransform>("Page Map");
                var motion = MotionOn(page.gameObject); AssertEntering(motion);
                Assert.That(page.localScale, Is.EqualTo(Vector3.one));
                Assert.That(page.localRotation, Is.EqualTo(Quaternion.identity));
                var prepare = Named<Button>("Prepare mission");
                Assert.That(prepare.IsInteractable(), Is.True);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(prepare.gameObject));
                var feedback = prepare.GetComponent<UiInteractionFeedback>();
                Assert.That(feedback, Is.Not.Null);
                ExecuteEvents.Execute(prepare.gameObject, Pointer(), ExecuteEvents.pointerEnterHandler);
                feedback.Refresh(true);
                Assert.That(feedback.HoverAmount, Is.EqualTo(1), "Existing pointer feedback must remain available while fading.");
                AssertEntering(motion);
                ExecuteEvents.Execute(prepare.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation), "Keyboard input must not wait for the fade.");
            }
        }

        [Test] public void NativeHelpWindowBeginsAnEntranceWithImmediateScopedFocusAndClose()
        {
            foreach (var size in Sizes)
            {
                HomeAt(size);
                var play = Named<Button>("Open campaign");
                EventSystem.current.SetSelectedGameObject(play.gameObject);
                var help = UiHelp.Show(Named<RectTransform>("Page Home"), "Справка", "Проверка открытия настоящего окна.");

                AssertEntering(MotionOn(help));
                var close = Named<Button>("Close context help");
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(close.gameObject));
                Assert.That(close.IsInteractable(), Is.True);
                Assert.That(play.IsInteractable(), Is.False, "The modal's covered siblings are excluded immediately.");
                Click("Close context help");
                Assert.That(help.activeInHierarchy, Is.False, "Closing is synchronous even during the entrance.");
                Assert.That(play.IsInteractable(), Is.True);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(play.gameObject));
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
            }
        }

        [Test] public void RapidNativeNavigationRetiresEveryOutgoingHierarchyInTheSameFrame()
        {
            foreach (var size in Sizes)
            {
                HomeAt(size);
                var home = Named<RectTransform>("Page Home");
                Click("Open campaign"); Click("Open mission"); var map = Named<RectTransform>("Page Map");
                Click("Prepare mission"); var preparation = Named<RectTransform>("Page Preparation");
                Click("Open preparation album"); var album = Named<RectTransform>("Page Album");
                Click("Album close");

                Assert.That(game.Page, Is.EqualTo(PlayerPage.Preparation));
                foreach (var outgoing in new[] { home, map, preparation, album })
                    Assert.That(outgoing.gameObject.activeInHierarchy, Is.False, outgoing.name + " must retire before another input.");
                Assert.That(root.GetComponentsInChildren<RectTransform>().Count(rect => rect.name.StartsWith("Page ")), Is.EqualTo(1));
                var active = root.GetComponentsInChildren<Button>();
                Assert.That(active.Count(button => button.name == "Start battle"), Is.EqualTo(1));
                foreach (string retired in new[] { "Open campaign", "Prepare mission", "Album close" })
                    Assert.That(active.Any(button => button.name == retired), Is.False, "Outgoing duplicate: " + retired);
                Assert.That(Named<Button>("Start battle").IsInteractable(), Is.True);
                AssertEntering(MotionOn(Named<RectTransform>("Page Preparation").gameObject));
            }
        }

        [UnityTest] public IEnumerator PageEntranceFinishesWhileTimeIsPausedAndTheMapBackdropStaysOpaque()
        {
            Time.timeScale = 0;
            foreach (var size in Sizes)
            {
                HomeAt(size); Click("Open campaign"); Click("Open mission");
                var page = Named<RectTransform>("Page Map");
                var motion = MotionOn(page.gameObject); AssertEntering(motion);
                var prepare = Named<Button>("Prepare mission").GetComponent<RectTransform>();
                var position = prepare.anchoredPosition; var extent = prepare.sizeDelta;
                var backdrop = Named<RawImage>("Field headquarters backdrop");
                Canvas.ForceUpdateCanvases();
                Assert.That(backdrop.raycastTarget, Is.False);
                Assert.That(backdrop.color.a, Is.EqualTo(1));
                Assert.That(backdrop.canvasRenderer.GetInheritedAlpha(), Is.EqualTo(1).Within(.001f),
                    "The field map must cover the light paper while incoming controls fade.");

                yield return new WaitForSecondsRealtime(.25f);

                AssertSettled(motion);
                Assert.That(prepare.anchoredPosition, Is.EqualTo(position));
                Assert.That(prepare.sizeDelta, Is.EqualTo(extent));
                Assert.That(page.localScale, Is.EqualTo(Vector3.one));
                Assert.That(page.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(game.Page, Is.EqualTo(PlayerPage.Map));
                Assert.That(Named<Button>("Prepare mission").IsInteractable(), Is.True);
                var backdropGroup = backdrop.GetComponent<CanvasGroup>();
                Assert.That(backdropGroup == null || !backdropGroup.ignoreParentGroups, Is.True,
                    "Backdrop exclusion is temporary and must not outlive the entrance.");
            }
        }

        [Test] public void ReducedMotionSettlesExistingPageAndModalAndSkipsFollowingEntrances()
        {
            Click("Open campaign"); Click("Open mission");
            var mapMotion = MotionOn(Named<RectTransform>("Page Map").gameObject); AssertEntering(mapMotion);
            var help = UiHelp.Show(Named<RectTransform>("Page Map"), "Справка", "Проверка уменьшения движения.");
            var helpMotion = MotionOn(help); AssertEntering(helpMotion);

            game.SetReducedMotion(true);

            AssertSettled(mapMotion); AssertSettled(helpMotion);
            Assert.That(Named<Button>("Close context help").IsInteractable(), Is.True);
            Click("Close context help"); Click("Prepare mission");
            AssertSettled(MotionOn(Named<RectTransform>("Page Preparation").gameObject));
            help = UiHelp.Show(Named<RectTransform>("Page Preparation"), "Справка", "Без движения.");
            AssertSettled(MotionOn(help)); Click("Close context help");
            game.SetReducedMotion(false); Click("Back");
            AssertEntering(MotionOn(Named<RectTransform>("Page Map").gameObject));
        }

        [Test] public void SamePageRefreshAndResponsiveLayoutSnapWithoutReplayingTheEntrance()
        {
            Click("Open campaign"); Click("Open mission"); AssertEntering(MotionOn(Named<RectTransform>("Page Map").gameObject));

            shell.Show(PlayerPage.Map);

            AssertSettled(MotionOn(Named<RectTransform>("Page Map").gameObject));
            foreach (var size in Sizes)
            {
                shell.ApplyLayout(size.x, size.y); Canvas.ForceUpdateCanvases();
                var page = Named<RectTransform>("Page Map");
                AssertSettled(MotionOn(page.gameObject));
                Assert.That(page.rect.size, Is.EqualTo(new Vector2(shell.Layout.Width, shell.Layout.Height)));
                var prepare = Named<Button>("Prepare mission");
                Assert.That(prepare.GetComponent<RectTransform>().rect.height * shell.Layout.Scale, Is.GreaterThanOrEqualTo(44));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(prepare.gameObject));
                Assert.That(prepare.IsInteractable(), Is.True);
            }
        }

        [Test] public void WorkshopEntranceSurvivesDataRefreshAndSettlesOnResponsiveReflow()
        {
            long tick = game.Run.Session.TickIndex;
            var loadout = game.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id).ToArray();
            game.EditAlbumEntry(null);
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Workshop));
            var motion = MotionOn(workshop.Root.gameObject); AssertEntering(motion);
            var draft = workshop.DraftSession.Draft;
            float opacity = motion.Opacity;

            workshop.RefreshStatus(); workshop.Refresh();

            Assert.That(MotionOn(workshop.Root.gameObject), Is.SameAs(motion));
            Assert.That(motion.Opacity, Is.GreaterThanOrEqualTo(opacity), "Rebuilt content cannot restart the entrance.");
            Assert.That(workshop.DraftSession.Draft, Is.SameAs(draft));
            foreach (var size in Sizes)
            {
                ((RectTransform)workshop.Root.parent).sizeDelta = new Vector2(size.x, size.y);
                workshop.Reflow(size.x, size.y); Canvas.ForceUpdateCanvases();
                AssertSettled(MotionOn(workshop.Root.gameObject));
                Assert.That(workshop.Root.rect.size, Is.EqualTo(new Vector2(size.x, size.y)));
                Assert.That(Named<Button>("Composite return").IsInteractable(), Is.True);
                Assert.That(workshop.Root.GetComponentsInChildren<RawImage>().Count(image => image.name == "Field headquarters backdrop"), Is.EqualTo(1));
            }
            Assert.That(game.Run.Session.TickIndex, Is.EqualTo(tick));
            Assert.That(game.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id), Is.EqualTo(loadout));
            Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
            Click("Composite return");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Home));
            Assert.That(workshop.Root.gameObject.activeInHierarchy, Is.False);
        }

        [Test] public void PauseAndConfirmationKeepSemanticAlphaAndCompleteNativeCancelResumeAndConfirmImmediately()
        {
            Click("Open campaign"); Click("Open mission"); Click("Prepare mission"); Click("Start battle"); Click("Battle pause");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Pause));
            Assert.That(game.Run.Paused, Is.True);
            var presentation = Named<RectTransform>("Pause presentation");
            var semantic = presentation.GetComponent<CanvasGroup>();
            Assert.That(semantic, Is.Not.Null); Assert.That(semantic.alpha, Is.EqualTo(1));
            var pauseMotion = MotionOn(presentation.gameObject); AssertEntering(pauseMotion);
            Assert.That(pauseMotion.OpacityGroup, Is.Not.SameAs(semantic),
                "Pause already owns the authoritative visibility group.");
            Assert.That(presentation.GetComponents<CanvasGroup>().Length, Is.EqualTo(1));

            Click("Leave battle");

            var confirmation = Named<RectTransform>("Confirmation shield");
            AssertEntering(MotionOn(confirmation.gameObject));
            Assert.That(semantic.alpha, Is.Zero, "The covered pause must hide in the confirmation callback.");
            Assert.That(Named<Button>("Resume").IsInteractable(), Is.False);
            Assert.That(Named<Button>("Cancel confirmation").IsInteractable(), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(Named<Button>("Cancel confirmation").gameObject));
            Assert.That(game.CanBuild, Is.False);
            Click("Close confirmation");
            Assert.That(confirmation.gameObject.activeInHierarchy, Is.False);
            Assert.That(semantic.alpha, Is.EqualTo(1), "Cancel restores the semantic group without a fade delay.");
            Assert.That(Named<Button>("Resume").IsInteractable(), Is.True);
            Click("Resume");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle)); Assert.That(game.Run.Paused, Is.False);
            Assert.That(presentation.gameObject.activeInHierarchy, Is.False);
            Assert.That(Named<Button>("Battle pause").IsInteractable(), Is.True);
            Click("Battle pause"); Click("Leave battle"); Click("Confirm action");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Map)); Assert.That(game.Run.InWorkshop, Is.True);
            Assert.That(shell.HasConfirmation, Is.False);
            Assert.That(Named<Button>("Prepare mission").IsInteractable(), Is.True);
        }
    }
}
#endif
