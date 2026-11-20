#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class ResponsiveMenuUiTests
    {
        static readonly string LongName = new string('Ш', 48);
        AssemblyCommandFixture fixture;
        GameObject canvas, player;

        [SetUp] public void SetUp()
        {
            fixture = new AssemblyCommandFixture(configure: data => {
                var snapshot = data.Entries[0].CreateSnapshot().Snapshot;
                data.Entries = Enumerable.Range(0, 8).Select(index => new AlbumEntryData(
                    new AlbumEntry("responsive.long." + index, LongName, 1, snapshot))).ToArray();
                data.Album = Array.Empty<BlueprintData>();
                data.Loadout = Enumerable.Range(0, 8).Select(index => "responsive.long." + index).ToArray();
            });
        }

        [TearDown] public void TearDown()
        {
            EventSystem.current?.SetSelectedGameObject(null);
            if (canvas != null) Object.DestroyImmediate(canvas);
            if (player != null) Object.DestroyImmediate(player);
            fixture?.Dispose();
        }

        AlbumPanel Album(int width, int height)
        {
            canvas = new GameObject("Responsive menu album", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
            var panel = new AlbumPanel(canvas.transform, fixture.Run, TextResourcesAsset.Default, _ => { }, () => { });
            panel.Reflow(width, height); panel.SelectEntry("responsive.long.0");
            Canvas.ForceUpdateCanvases();
            return panel;
        }

        PlayerShell Player(int width, int height)
        {
            player = new GameObject("Responsive menu player"); player.SetActive(false);
            var game = player.AddComponent<PlayerGameRoot>(); game.Configure(fixture.BalanceAsset, true);
            player.SetActive(true); game.enabled = false; game.Run.AutomaticSimulation = false;
            var shell = player.GetComponentInChildren<PlayerShell>(); shell.enabled = false;
            shell.ApplyLayout(width, height); Canvas.ForceUpdateCanvases();
            return shell;
        }

        [TestCase(800, 600)] [TestCase(1024, 768)] [TestCase(1150, 720)]
        [TestCase(1280, 720)] [TestCase(1920, 1080)] [TestCase(2560, 1080)]
        public void LongAlbumNamesKeepReadableSummariesAndTheirFullIdentity(int width, int height)
        {
            var panel = Album(width, height);
            for (int index = 0; index < 8; index++)
            {
                var card = panel.Root.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Album choose responsive.long." + index);
                var title = card.GetComponentsInChildren<Text>().Single(text => text.name == "Album card name");
                AssertReadable(title, 1);
                AssertFullOrEllipsized(title.text, LongName);
                Assert.That(card.GetComponent<WorkshopTooltip>().Hint, Does.Contain(LongName), "A shortened card still exposes the saved name.");
                var slot = panel.Root.GetComponentsInChildren<Button>().Single(button => button.name == "Album assign " + index);
                var caption = slot.GetComponentInChildren<Text>();
                AssertReadable(caption, 1);
                Assert.That(caption.text, Does.StartWith((index + 1) + "."), "The slot number remains visible.");
                Assert.That(slot.GetComponent<WorkshopTooltip>().Hint, Does.Contain(LongName), "A shortened assignment retains its full name in the hint.");
            }
            var selected = panel.Root.GetComponentsInChildren<Text>().Single(text => text.name == "Album selected name");
            Assert.That(selected.text, Is.EqualTo(LongName), "The selected entry has space for its complete saved name.");
            AssertReadable(selected, 1);
            var health = panel.Root.GetComponentsInChildren<Text>().Single(text => text.name == "Album metric label Health");
            Assert.That(Top(health.rectTransform), Is.GreaterThanOrEqualTo(Bottom(selected.rectTransform) + 2), "A wrapped name must push statistics down.");
            var details = panel.Root.GetComponentsInChildren<Button>().Single(button => button.name == "Album details");
            details.onClick.Invoke();
            Assert.That(string.Join(" ", panel.Root.GetComponentsInChildren<Text>().Select(text => text.text)), Does.Contain(LongName));
            Assert.That(fixture.Store.SaveCalls, Is.Zero, "Presentation must not rename or save entries.");
        }

        [TestCase(800, 600)] [TestCase(1024, 768)]
        public void CompactMapHeaderKeepsGuidanceReadableAndFullInstructionsAccessible(int width, int height)
        {
            var shell = Player(width, height);
            var game = player.GetComponent<PlayerGameRoot>(); game.Navigate(PlayerPage.Map);
            shell.ApplyLayout(width, height); Canvas.ForceUpdateCanvases();
            var header = player.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Notebook header");
            foreach (var text in header.GetComponentsInChildren<Text>().Where(text => text.transform.parent == header))
                AssertReadable(text, shell.Layout.Scale);
            Assert.That(header.GetComponentsInChildren<Text>().Any(text => text.name == "Player progress"), Is.True);
            header.GetComponentsInChildren<Button>().Single(button => button.name == "Screen help").onClick.Invoke();
            var help = player.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Context help");
            Assert.That(string.Join(" ", help.GetComponentsInChildren<Text>().Select(text => text.text)),
                Does.Contain("В подготовке выбери до восьми башен и сооружений").And.Contain("свободные места допустимы"));
        }

        [Test] public void HomeKeepsItsActionsWithoutRepeatingTheNavigationSequence()
        {
            Player(800, 600);
            foreach (string name in new[] { "Open campaign", "Open album", "workshop", "settings", "help", "exit" })
                Assert.That(player.GetComponentsInChildren<Button>().Single(button => button.name == name).IsInteractable(), Is.True, name);
            Assert.That(player.GetComponentsInChildren<Text>().Any(text => text.text == "Карта → Боевой набор → Защита"), Is.False);
        }

        static float Top(RectTransform rect) => -rect.anchoredPosition.y;
        static float Bottom(RectTransform rect) => Top(rect) + rect.rect.height;

        static void AssertFullOrEllipsized(string displayed, string full)
        {
            Assert.That(displayed, Is.Not.Empty);
            if (displayed != full)
            {
                Assert.That(displayed, Does.EndWith("…"), "A concise name announces the omission.");
                Assert.That(full, Does.StartWith(displayed.Substring(0, displayed.Length - 1)));
            }
        }

        static void AssertReadable(Text label, float scale)
        {
            var settings = label.GetGenerationSettings(label.rectTransform.rect.size * scale);
            settings.scaleFactor = 1;
            settings.fontSize = Mathf.RoundToInt(label.fontSize * scale);
            settings.resizeTextMinSize = Mathf.CeilToInt(label.resizeTextMinSize * scale);
            settings.resizeTextMaxSize = Mathf.Max(settings.resizeTextMinSize, Mathf.RoundToInt(label.resizeTextMaxSize * scale));
            settings.generateOutOfBounds = false;
            using (var generated = new TextGenerator())
            {
                generated.Populate(label.text, settings);
                Assert.That(generated.characterCountVisible, Is.GreaterThanOrEqualTo(label.text.Length), label.name + ": " + label.text);
                int size = label.resizeTextForBestFit ? generated.fontSizeUsedForBestFit : settings.fontSize;
                Assert.That(size, Is.GreaterThanOrEqualTo(15), label.name + " must keep a readable physical font.");
            }
        }
    }
}
#endif
