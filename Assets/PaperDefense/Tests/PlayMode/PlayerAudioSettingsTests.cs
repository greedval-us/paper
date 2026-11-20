#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class PlayerAudioSettingsTests
    {
        internal static Type SettingsType
        {
            get
            {
                var type = typeof(PlayerGameRoot).Assembly.GetType("PaperDefense.Presentation.PlayerAudioSettings");
                Assert.That(type, Is.Not.Null, "Audio preferences need a presentation model independent of the campaign profile.");
                return type;
            }
        }

        internal static object MemorySettings() => SettingsType.GetMethod("Load").Invoke(null, new object[] { true });
        internal static T Read<T>(object settings, string name) => (T)SettingsType.GetProperty(name).GetValue(settings);
        internal static void Call(object settings, string name, object value) => SettingsType.GetMethod(name).Invoke(settings, new[] { value });

        [Test] public void MemoryOnlyAudioStartsWithAudibleModerateDefaults()
        {
            object settings = MemorySettings();
            Assert.That(Read<float>(settings, "SfxVolume"), Is.EqualTo(.65f).Within(.0001f));
            Assert.That(Read<bool>(settings, "Muted"), Is.False);
            Assert.That(Read<float>(settings, "EffectiveVolume"), Is.EqualTo(.65f).Within(.0001f));
        }

        [Test] public void VolumeClampsFiniteInputAndRejectsNonFiniteValues()
        {
            object settings = MemorySettings();
            Call(settings, "SetVolume", 4f);
            Assert.That(Read<float>(settings, "SfxVolume"), Is.EqualTo(1f));
            Call(settings, "SetVolume", -2f);
            Assert.That(Read<float>(settings, "SfxVolume"), Is.Zero);
            Call(settings, "SetVolume", .4f);
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Call(settings, "SetVolume", invalid);
            Assert.That(Read<float>(settings, "SfxVolume"), Is.EqualTo(.4f));
        }

        [Test] public void MutePreservesVolumeAndOnlyActualChangesNotify()
        {
            object settings = MemorySettings(); int changes = 0;
            Action changed = () => changes++;
            SettingsType.GetEvent("Changed").AddEventHandler(settings, changed);
            Call(settings, "SetVolume", .4f);
            Call(settings, "SetVolume", .4f);
            Call(settings, "SetMuted", true);
            Call(settings, "SetMuted", true);
            Assert.That(Read<float>(settings, "EffectiveVolume"), Is.Zero);
            Assert.That(Read<float>(settings, "SfxVolume"), Is.EqualTo(.4f));
            Call(settings, "SetMuted", false);
            Assert.That(Read<float>(settings, "EffectiveVolume"), Is.EqualTo(.4f));
            Assert.That(changes, Is.EqualTo(3));
        }

        [Test] public void MemoryOnlyInstancesNeverSharePreferenceChanges()
        {
            object first = MemorySettings();
            Call(first, "SetVolume", .12f); Call(first, "SetMuted", true);
            object second = MemorySettings();
            Assert.That(Read<float>(second, "SfxVolume"), Is.EqualTo(.65f).Within(.0001f));
            Assert.That(Read<bool>(second, "Muted"), Is.False);
        }

        [Test] public void InjectedStoreLoadsWithoutWritingAndPersistsOnlyActualChanges()
        {
            var store = new PreferenceStore { Volume = .28f, Mute = 1 };
            var settings = new PlayerAudioSettings(store);
            Assert.That(settings.SfxVolume, Is.EqualTo(.28f)); Assert.That(settings.Muted, Is.True);
            Assert.That(store.Writes, Is.Zero); Assert.That(store.Saves, Is.Zero);
            settings.SetMuted(false); settings.SetVolume(.72f);
            settings.SetMuted(false); settings.SetVolume(.72f);
            Assert.That(store.Writes, Is.EqualTo(2)); Assert.That(store.Saves, Is.EqualTo(2));
            Assert.That(store.Keys.Count, Is.EqualTo(2), "Audio uses exactly two preferences separate from the campaign profile.");
            var reloaded = new PlayerAudioSettings(store);
            Assert.That(reloaded.SfxVolume, Is.EqualTo(.72f)); Assert.That(reloaded.Muted, Is.False);
        }

        [TestCase(float.NaN, .65f)] [TestCase(float.PositiveInfinity, .65f)]
        [TestCase(float.NegativeInfinity, .65f)] [TestCase(2f, 1f)] [TestCase(-1f, 0f)]
        public void InvalidStoredVolumeIsSanitizedWithoutRewritingPreferences(float stored, float expected)
        {
            var store = new PreferenceStore { Volume = stored, Mute = 9 };
            var settings = new PlayerAudioSettings(store);
            Assert.That(settings.SfxVolume, Is.EqualTo(expected)); Assert.That(settings.Muted, Is.False);
            Assert.That(store.Writes, Is.Zero); Assert.That(store.Saves, Is.Zero);
        }

        [Test] public void MemoryOnlyModeNeverReadsOrWritesEvenAnInjectedStore()
        {
            var store = new PreferenceStore { Volume = .1f, Mute = 1 };
            var settings = new PlayerAudioSettings(store, memoryOnly: true);
            Assert.That(settings.SfxVolume, Is.EqualTo(.65f)); Assert.That(settings.Muted, Is.False);
            settings.SetVolume(.5f); settings.SetMuted(true);
            Assert.That(store.Reads, Is.Zero); Assert.That(store.Writes, Is.Zero); Assert.That(store.Saves, Is.Zero);
        }

        sealed class PreferenceStore : IPlayerAudioPreferenceStore
        {
            public float Volume = .65f;
            public int Mute, Reads, Writes, Saves;
            public readonly HashSet<string> Keys = new HashSet<string>();
            public float GetFloat(string key, float fallback) { Reads++; return Volume; }
            public int GetInt(string key, int fallback) { Reads++; return Mute; }
            public void SetFloat(string key, float value) { Writes++; Keys.Add(key); Volume = value; }
            public void SetInt(string key, int value) { Writes++; Keys.Add(key); Mute = value; }
            public void Save() { Saves++; }
        }
    }

    public sealed class PlayerAudioSettingsUiTests : BattleHudThemeFixture
    {
        T Control<T>(string name) where T : Component
        {
            var control = root.GetComponentsInChildren<T>().SingleOrDefault(value => value.name == name);
            Assert.That(control, Is.Not.Null, "Missing audio settings control: " + name);
            return control;
        }

        object AudioSettings => typeof(PlayerGameRoot).GetProperty("AudioSettings").GetValue(game);

        [UnityTest] public IEnumerator AudioDialogAppliesVolumeAndMuteImmediatelyThenRestoresFocus()
        {
            game.OpenSettings(); yield return null;
            var open = Control<Button>("Settings audio");
            EventSystem.current.SetSelectedGameObject(open.gameObject);
            open.onClick.Invoke(); Canvas.ForceUpdateCanvases();
            var slider = Control<Slider>("Audio settings volume");
            Assert.That(slider.minValue, Is.Zero); Assert.That(slider.maxValue, Is.EqualTo(100));
            slider.value = 37;
            Assert.That(PlayerAudioSettingsTests.Read<float>(AudioSettings, "SfxVolume"), Is.EqualTo(.37f).Within(.0001f));
            Control<Button>("Audio settings mute").onClick.Invoke();
            Assert.That(PlayerAudioSettingsTests.Read<float>(AudioSettings, "EffectiveVolume"), Is.Zero);
            Control<Button>("Audio settings close").onClick.Invoke();
            yield return null;
            Assert.That(root.GetComponentsInChildren<Slider>().Any(value => value.name == "Audio settings volume"), Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(open.gameObject));
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Settings));
        }

        [UnityTest] public IEnumerator OpenAudioDialogSurvivesResizeAndDoesNotChangePauseOrPendingDisplayChoice()
        {
            StartBattle(); game.OpenSettings(); yield return null;
            Assert.That(game.Run.Paused, Is.True);
            game.DisplaySettings.SelectRelative(1);
            var selected = game.DisplaySettings.SelectedResolution;
            Control<Button>("Settings audio").onClick.Invoke();
            Control<Slider>("Audio settings volume").value = 22;
            foreach (var size in new[] { new Vector2Int(800, 600), new Vector2Int(1600, 900) })
            {
                shell.ApplyLayout(size.x, size.y); Canvas.ForceUpdateCanvases(); yield return null;
                Assert.That(Control<Slider>("Audio settings volume").value, Is.EqualTo(22).Within(.01f));
                var slider = Control<Slider>("Audio settings volume");
                Assert.That(slider.handleRect.rect.height, Is.InRange(24, slider.GetComponent<RectTransform>().rect.height),
                    "The slider thumb must fit inside its touch target rather than overlap the caption.");
                Assert.That(Control<Button>("Audio settings close").IsInteractable(), Is.True);
                Assert.That(game.DisplaySettings.SelectedResolution, Is.EqualTo(selected));
                Assert.That(game.Run.Paused, Is.True);
                foreach (var label in Control<RectTransform>("Audio settings sheet").GetComponentsInChildren<Text>())
                {
                    var generation = label.GetGenerationSettings(label.rectTransform.rect.size);
                    generation.generateOutOfBounds = false;
                    using (var generated = new TextGenerator())
                    {
                        generated.Populate(label.text, generation);
                        Assert.That(generated.characterCountVisible, Is.GreaterThanOrEqualTo(label.text.Length),
                            "Audio caption must fit at " + size + ": " + label.text);
                    }
                }
            }
            Control<Button>("Audio settings close").onClick.Invoke();
            Assert.That(game.Run.Paused, Is.True);
        }

        [UnityTest] public IEnumerator BackClosesOnlyTheAudioDialogAndLeavingSettingsDoesNotReopenIt()
        {
            game.OpenSettings(); yield return null;
            Control<Button>("Settings audio").onClick.Invoke();
            game.Back(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Settings));
            Assert.That(root.GetComponentsInChildren<Slider>().Any(value => value.name == "Audio settings volume"), Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Control<Button>("Settings audio").gameObject));
            Control<Button>("Settings audio").onClick.Invoke();
            game.CloseSettings(); yield return null;
            game.OpenSettings(); yield return null;
            Assert.That(root.GetComponentsInChildren<Slider>().Any(value => value.name == "Audio settings volume"), Is.False);
        }
    }
}
#endif
