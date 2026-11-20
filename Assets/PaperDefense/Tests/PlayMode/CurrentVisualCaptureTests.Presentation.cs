#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed partial class CurrentVisualCaptureTests
    {
        [UnityTest, Explicit("Captures current pointer, keyboard and persistent-selection states using the production menu fixture.")]
        public IEnumerator CaptureMenuInteractionStates()
        {
            float previousTimeScale = Time.timeScale;
            try
            {
                captures.Clear(); Time.timeScale = 0;
                foreach (var size in Sizes)
                {
                    game.ReturnToHome(); shell.ApplyLayout(size.x, size.y);
                    var play = Find<Button>("Open campaign");
                    EventSystem.current.SetSelectedGameObject(null);
                    yield return new WaitForSecondsRealtime(.18f); CaptureInteraction("feedback-normal", size, play);
                    SendPointer(play, ExecuteEvents.pointerEnterHandler);
                    yield return new WaitForSecondsRealtime(.18f); CaptureInteraction("feedback-hover", size, play);
                    SendPointer(play, ExecuteEvents.pointerDownHandler);
                    yield return new WaitForSecondsRealtime(.09f); CaptureInteraction("feedback-pressed", size, play);
                    SendPointer(play, ExecuteEvents.pointerUpHandler); SendPointer(play, ExecuteEvents.pointerExitHandler);
                    EventSystem.current.SetSelectedGameObject(null);
                    EventSystem.current.SetSelectedGameObject(play.gameObject, new BaseEventData(EventSystem.current));
                    yield return new WaitForSecondsRealtime(.18f); CaptureInteraction("feedback-focus", size, play);
                    play.interactable = false;
                    yield return null; CaptureInteraction("feedback-disabled", size, play);
                    play.interactable = true; EventSystem.current.SetSelectedGameObject(null);

                    game.OpenAlbum(); shell.Album.SelectEntry("album.double");
                    var chosen = Find<Button>("Album choose album.double");
                    yield return new WaitForSecondsRealtime(.18f); CaptureInteraction("feedback-album-selected", size, chosen);
                    var other = Find<Button>("Album choose album.guard"); SendPointer(other, ExecuteEvents.pointerEnterHandler);
                    yield return new WaitForSecondsRealtime(.18f); CaptureInteraction("feedback-album-hover", size, other);
                    Assert.That(chosen.GetComponent<UiInteractionFeedback>().SelectionAmount, Is.EqualTo(1));
                    SendPointer(other, ExecuteEvents.pointerExitHandler);

                    game.EditAlbumEntry(null);
                    ((RectTransform)Workshop.Root.parent).sizeDelta = size; Workshop.Reflow(size.x, size.y);
                    var part = root.GetComponentsInChildren<Button>().First(button => button.name.StartsWith("Sequence inspect ", StringComparison.Ordinal));
                    yield return new WaitForSecondsRealtime(.18f); CaptureInteraction("feedback-workshop-selected", size, part);
                    SendPointer(part, ExecuteEvents.pointerEnterHandler);
                    yield return new WaitForSecondsRealtime(.18f); CaptureInteraction("feedback-workshop-hover", size, part);
                    SendPointer(part, ExecuteEvents.pointerExitHandler);
                    Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
                }
                WriteManifest("interaction-manifest.json");
            }
            finally { Time.timeScale = previousTimeScale; EventSystem.current?.SetSelectedGameObject(null); }
        }

        static void SendPointer<T>(Button button, ExecuteEvents.EventFunction<T> handler) where T : IEventSystemHandler
            => ExecuteEvents.Execute(button.gameObject,
                new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, handler);

        void CaptureInteraction(string name, Vector2Int size, Button button)
        {
            Capture(name, size);
            var feedback = button.GetComponent<UiInteractionFeedback>(); Assert.That(feedback, Is.Not.Null, button.name);
            File.WriteAllText(Path.Combine(Folder, name + "-" + size.x + "x" + size.y + "-interaction.json"),
                JsonUtility.ToJson(new InteractionEvidence {
                    control = button.name, hovered = feedback.HoverAmount, pressed = feedback.PressAmount,
                    focused = feedback.FocusAmount, selected = feedback.SelectionAmount,
                    interactable = button.IsInteractable(), timeScale = Time.timeScale
                }, true));
        }

        [UnityTest, Explicit("Captures current production page and modal entrances before, during and after their realtime transition.")]
        public IEnumerator CaptureMenuEntrances()
        {
            float previousTimeScale = Time.timeScale;
            try
            {
                captures.Clear(); Time.timeScale = 0;
                foreach (var size in Sizes)
                {
                    game.ReturnToHome(); shell.ApplyLayout(size.x, size.y);
                    game.Navigate(PlayerPage.Map);
                    var page = root.GetComponentsInChildren<UiMenuMotion>().Single(motion => motion.name == "Page Map");
                    yield return CaptureMotion("entrance-map", size, page);
                    var help = UiHelp.Show(page.transform, "Справка", "Подготовь оборону на карте. Выбери готовую башню в альбоме или собери свою в мастерской.");
                    try { yield return CaptureMotion("entrance-help", size, help.GetComponent<UiMenuMotion>(), window: true); }
                    finally { if (help != null) Object.DestroyImmediate(help); }
                    // Reflow intentionally settles presentation. Prepare the hidden
                    // workshop before the production entrance starts its native motion.
                    ((RectTransform)Workshop.Root.parent).sizeDelta = size; Workshop.Reflow(size.x, size.y);
                    game.EditAlbumEntry(null);
                    yield return CaptureMotion("entrance-workshop", size, Workshop.Root.GetComponent<UiMenuMotion>());
                    Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
                }
                WriteManifest("entrance-manifest.json");
            }
            finally { Time.timeScale = previousTimeScale; EventSystem.current?.SetSelectedGameObject(null); }
        }

        IEnumerator CaptureMotion(string name, Vector2Int size, UiMenuMotion motion, bool window = false)
        {
            Assert.That(motion, Is.Not.Null); Assert.That(motion.IsAnimating, Is.True, name);
            Assert.That(motion.Opacity, Is.GreaterThan(0).And.LessThan(1), "The start PNG must show an actual native entrance phase.");
            CaptureMotionFrame(name + "-start", size, motion);
            // PNG work can occupy more than the whole entrance duration. Consume that
            // display frame before restarting the same native animation; never label a
            // settled frame as intermediate or synthesize the CanvasGroup's opacity.
            yield return null;
            bool intermediate = false;
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                motion = window ? UiMenuMotion.PlayWindow(motion.gameObject) : UiMenuMotion.PlayScreen((RectTransform)motion.transform);
                float initial = motion.Opacity;
                yield return null;
                if (motion.IsAnimating && motion.Opacity > initial && motion.Opacity < 1)
                {
                    CaptureMotionFrame(name + "-moving", size, motion, nativeRestartAttempt: attempt);
                    intermediate = true; break;
                }
                yield return null;
            }
            Assert.That(intermediate, Is.True, "A moving capture requires an observed native frame with strictly intermediate opacity: " + name);
            yield return new WaitForSecondsRealtime(.23f);
            CaptureMotionFrame(name + "-settled", size, motion);
            Assert.That(motion.Opacity, Is.EqualTo(1)); Assert.That(motion.IsAnimating, Is.False);
        }

        void CaptureMotionFrame(string name, Vector2Int size, UiMenuMotion motion, int nativeRestartAttempt = 0)
        {
            float opacity = motion.Opacity; bool animating = motion.IsAnimating;
            if (nativeRestartAttempt > 0)
            {
                Assert.That(animating, Is.True);
                Assert.That(opacity, Is.GreaterThan(0).And.LessThan(1), "The PNG must depict an actual intermediate native transition.");
            }
            Capture(name, size, settleMotion: false);
            Assert.That(motion.Opacity, Is.EqualTo(opacity), "Capturing must preserve the sampled transition phase.");
            File.WriteAllText(Path.Combine(Folder, name + "-" + size.x + "x" + size.y + "-motion.json"),
                JsonUtility.ToJson(new MotionEvidence { scope = motion.name, opacity = opacity,
                    animating = animating, timeScale = Time.timeScale,
                    nativeRestartAttempt = nativeRestartAttempt,
                    selected = EventSystem.current?.currentSelectedGameObject?.name }, true));
        }

        [Serializable] sealed class InteractionEvidence
        {
            public string control;
            public float hovered, pressed, focused, selected, timeScale;
            public bool interactable;
        }
        [Serializable] sealed class MotionEvidence
        {
            public string scope, selected;
            public float opacity, timeScale;
            public bool animating;
            public int nativeRestartAttempt;
        }
    }
}
#endif
