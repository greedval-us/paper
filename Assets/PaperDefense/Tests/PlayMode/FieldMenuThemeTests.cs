#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    /// <summary>The menu skin must preserve the production navigation and input contract.</summary>
    public sealed class FieldMenuThemeTests
    {
        GameObject root;
        PlayerGameRoot game;
        PlayerShell shell;
        GameBalanceAsset balance;
        CompositeWorkshopPanel workshop;

        [UnitySetUp] public IEnumerator SetUp()
        {
            foreach (var previous in Object.FindObjectsByType<PlayerGameRoot>()) Object.Destroy(previous.gameObject);
            foreach (var previous in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(previous.gameObject);
            yield return null;
            var source = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            Assert.That(source, Is.Not.Null, "Use the production catalog with a disk-free profile.");
            balance = Object.Instantiate(source); balance.planningSeconds = 1000;
            root = new GameObject("Field menu theme fixture — memory only"); root.SetActive(false);
            game = root.AddComponent<PlayerGameRoot>(); game.Configure(balance, true); root.SetActive(true);
            game.Run.AutomaticSimulation = false;
            shell = root.GetComponentInChildren<PlayerShell>(); shell.enabled = false;
            workshop = root.GetComponentInChildren<WorkshopHud>(true).Composite;
            foreach (var responsive in root.GetComponentsInChildren<WorkshopResponsiveLayout>(true)) responsive.enabled = false;
            yield return null;
            Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            EventSystem.current?.SetSelectedGameObject(null);
            if (root != null) Object.Destroy(root);
            if (balance != null) Object.Destroy(balance);
            yield return null;
        }

        void Size(int width, int height)
        {
            shell.ApplyLayout(width, height);
            Canvas.ForceUpdateCanvases();
        }

        T Named<T>(string name) where T : Component
        {
            var component = root.GetComponentsInChildren<T>().SingleOrDefault(item => item.name == name);
            Assert.That(component, Is.Not.Null, "Missing active production control: " + name);
            return component;
        }

        void Click(string name)
        {
            var button = Named<Button>(name);
            Assert.That(button.IsInteractable(), Is.True, name + " must remain usable.");
            button.onClick.Invoke(); Canvas.ForceUpdateCanvases();
        }

        RectTransform AssertMenuTheme(PlayerPage page)
        {
            Assert.That(game.Page, Is.EqualTo(page));
            var scope = page == PlayerPage.Workshop ? workshop.Root : Named<RectTransform>("Page " + page);
            Assert.That(FieldMenuTheme.IsThemed(scope), Is.True, page + " needs a menu-only theme scope.");
            var theme = scope.GetComponentInParent<FieldMenuTheme>();
            Assert.That(theme, Is.Not.Null); theme.Refresh();
            var cards = scope.GetComponentsInChildren<PlayerCardGraphic>(true);
            var surfaces = scope.GetComponentsInChildren<WorkshopSurfaceGraphic>(true);
            Assert.That(cards.Length + surfaces.Length, Is.GreaterThan(0), "Check actual production surfaces.");
            foreach (var card in cards) Assert.That(card.FieldStyle, Is.True, page + "/" + card.name);
            foreach (var surface in surfaces) Assert.That(surface.FieldStyle, Is.True, page + "/" + surface.name);
            return scope;
        }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void ReapplyingHomeArtPreservesGeometryFocusNavigationAndPlayCallback(int width, int height)
        {
            Size(width, height);
            var scope = AssertMenuTheme(PlayerPage.Home);
            var play = Named<Button>("Open campaign"); int calls = 0;
            play.onClick.AddListener(() => calls++);
            EventSystem.current.SetSelectedGameObject(play.gameObject);
            Canvas.ForceUpdateCanvases();
            var contract = new InputContract(scope);
            var selected = EventSystem.current.currentSelectedGameObject;

            FieldMenuTheme.ApplyTo(scope); scope.GetComponent<FieldMenuTheme>().Refresh();
            FieldMenuTheme.ApplyTo(scope); scope.GetComponent<FieldMenuTheme>().Refresh();
            Canvas.ForceUpdateCanvases();

            contract.AssertUnchanged();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(selected));
            Assert.That(play.GetComponent<RectTransform>().rect.height * shell.Layout.Scale, Is.GreaterThanOrEqualTo(44));
            Assert.That(play.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(play.navigation.selectOnDown, Is.Not.Null);
            play.onClick.Invoke();
            Assert.That(calls, Is.EqualTo(1), "Reapplying art must preserve runtime listeners.");
            AssertMenuTheme(PlayerPage.Campaign);
        }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void ProductionMenuJourneyKeepsThemeAndControlsWithoutChangingTheRun(int width, int height)
        {
            Size(width, height);
            long tick = game.Run.Session.TickIndex;
            var loadout = game.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id).ToArray();
            AssertMenuTheme(PlayerPage.Home);
            Click("Open campaign"); Click("Open mission"); AssertMenuTheme(PlayerPage.Map);
            Click("Prepare mission"); AssertMenuTheme(PlayerPage.Preparation);
            Click("Open preparation album"); AssertMenuTheme(PlayerPage.Album);
            shell.Album.SelectEntry(game.Run.Workshop.Entries[0].Id);
            Click("Album edit"); AssertMenuTheme(PlayerPage.Workshop);
            workshop.Reflow(width, height); Canvas.ForceUpdateCanvases();
            var contract = new InputContract(workshop.Root);
            var selected = EventSystem.current.currentSelectedGameObject;
            FieldMenuTheme.ApplyTo(workshop.Root); workshop.Root.GetComponent<FieldMenuTheme>().Refresh();
            Canvas.ForceUpdateCanvases(); contract.AssertUnchanged();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(selected));
            Assert.That(Named<Button>("Composite return").IsInteractable(), Is.True);
            Click("Composite return"); AssertMenuTheme(PlayerPage.Album);
            Click("Album close"); AssertMenuTheme(PlayerPage.Preparation);
            Assert.That(Named<Button>("Start battle").IsInteractable(), Is.True);
            Click("Back"); AssertMenuTheme(PlayerPage.Map);
            Click("Back"); AssertMenuTheme(PlayerPage.Campaign);
            Click("Back"); AssertMenuTheme(PlayerPage.Home);
            Assert.That(game.Run.Session.TickIndex, Is.EqualTo(tick));
            Assert.That(game.Run.InWorkshop, Is.True);
            Assert.That(game.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id), Is.EqualTo(loadout));
            Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
        }

        [TestCase(800, 600)] [TestCase(1600, 900)]
        public void BattleControlsStayOutsideTheMenuSkinWhenPauseIsThemed(int width, int height)
        {
            Size(width, height); game.Navigate(PlayerPage.Preparation);
            Assert.That(game.StartBattle(), Is.True);
            var battle = Named<RectTransform>("Battle controls");
            AssertBattleOwnsItsTheme(battle);
            game.Pause(); AssertMenuTheme(PlayerPage.Pause);
            foreach (var theme in root.GetComponentsInChildren<FieldMenuTheme>()) theme.Refresh();
            AssertBattleOwnsItsTheme(battle);
            Click("Resume");
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle));
            Assert.That(Named<Button>("Battle pause").IsInteractable(), Is.True);
            AssertBattleOwnsItsTheme(battle);
        }

        static void AssertBattleOwnsItsTheme(RectTransform battle)
        {
            Assert.That(FieldMenuTheme.IsThemed(battle), Is.False, "Battle is a sibling of menu overlays.");
            Assert.That(battle.GetComponentsInChildren<FieldMenuTheme>(true), Is.Empty);
            Assert.That(battle.GetComponent<BattleHudTheme>(), Is.Not.Null);
            var cards = battle.GetComponentsInChildren<PlayerCardGraphic>(true);
            Assert.That(cards.Length, Is.GreaterThan(0));
            foreach (var card in cards) Assert.That(card.FieldStyle, Is.True, "Battle surface lost its own theme: " + card.name);
            foreach (var surface in battle.GetComponentsInChildren<WorkshopSurfaceGraphic>(true))
                Assert.That(surface.FieldStyle, Is.False, "Battle art changed: " + surface.name);
        }

        [Test] public void ApplyingToANarrowScopeCannotRestyleItsUnthemedSibling()
        {
            var probes = new GameObject("Theme boundary probes", typeof(RectTransform)); probes.transform.SetParent(root.transform, false);
            var scope = PlayerUi.Graphic<PlayerCardGraphic>(probes.transform, "Scoped sheet", new Rect(13, 17, 210, 150));
            var scopedSurface = PlayerUi.Graphic<WorkshopSurfaceGraphic>(scope.transform, "Scoped workshop control", new Rect(8, 12, 120, 44));
            var sibling = PlayerUi.Graphic<PlayerCardGraphic>(probes.transform, "Unthemed sibling", new Rect(250, 30, 210, 150));
            var siblingSurface = PlayerUi.Graphic<WorkshopSurfaceGraphic>(sibling.transform, "Unthemed workshop control", new Rect(4, 6, 120, 44));
            sibling.color = Color.magenta; siblingSurface.color = Color.cyan;
            var contract = new InputContract((RectTransform)probes.transform);

            FieldMenuTheme.ApplyTo(scope.rectTransform, false); scope.GetComponent<FieldMenuTheme>().Refresh();

            contract.AssertUnchanged();
            Assert.That(FieldMenuTheme.IsThemed(scopedSurface.transform), Is.True);
            Assert.That(scope.FieldStyle && scopedSurface.FieldStyle, Is.True);
            Assert.That(FieldMenuTheme.IsThemed(sibling.transform), Is.False);
            Assert.That(sibling.FieldStyle || siblingSurface.FieldStyle, Is.False);
            Assert.That(sibling.color, Is.EqualTo(Color.magenta));
            Assert.That(siblingSurface.color, Is.EqualTo(Color.cyan));
        }

        [Test] public void RebuildingCompactWorkshopKeepsTheBackgroundCrop()
        {
            Click("workshop"); workshop.Reflow(800, 600); Canvas.ForceUpdateCanvases();
            workshop.Root.GetComponent<FieldMenuTheme>().Refresh();
            var first = workshop.Root.GetComponentsInChildren<RawImage>().Single(image => image.name == "Field headquarters backdrop");
            Assert.That(first.texture, Is.Not.Null);
            var crop = first.uvRect;
            Assert.That(crop.width, Is.LessThan(1));
            workshop.Refresh(); Canvas.ForceUpdateCanvases();
            workshop.Root.GetComponent<FieldMenuTheme>().Refresh();
            var rebuilt = workshop.Root.GetComponentsInChildren<RawImage>().Single(image => image.name == "Field headquarters backdrop");
            Assert.That(rebuilt.uvRect, Is.EqualTo(crop));
        }

        [UnityTest] public IEnumerator PlainButtonForegroundsRecoverSemanticColorsThroughAvailabilityAndRefresh()
        {
            var scope = PlayerUi.Graphic<PlayerCardGraphic>(root.transform, "Plain availability probe", new Rect(0, 0, 320, 120));
            var plate = PlayerUi.Graphic<PlayerCardGraphic>(scope.transform, "Plain album choice", new Rect(8, 8, 300, 64));
            plate.Control = true;
            var button = plate.gameObject.AddComponent<Button>(); button.targetGraphic = plate;
            var label = new PlayerUi().Label(plate.transform, "Недоступный чертёж", new Rect(42, 4, 250, 56));
            var icon = PlayerUi.Graphic<PlayerIconGraphic>(plate.transform, "Plain choice icon", new Rect(8, 20, 24, 24));
            var workshopIcon = PlayerUi.Graphic<WorkshopIconGraphic>(plate.transform, "Plain workshop icon", new Rect(270, 20, 24, 24));
            label.color = NotebookPalette.Red; icon.color = PlayerUi.Muted; workshopIcon.color = NotebookPalette.Green;
            button.interactable = false;
            int clicks = 0; button.onClick.AddListener(() => clicks++);

            FieldMenuTheme.ApplyTo(scope.rectTransform, false);
            var theme = scope.GetComponent<FieldMenuTheme>();
            Assert.That(label.color, Is.EqualTo(FieldMenuSkin.Disabled));
            Assert.That(icon.color, Is.EqualTo(FieldMenuSkin.Disabled));
            Assert.That(workshopIcon.color, Is.EqualTo(FieldMenuSkin.Disabled));
            theme.Refresh();

            button.interactable = true;
            yield return null; // Exercise the production LateUpdate path without a custom Button caption hook.
            Assert.That(label.color, Is.EqualTo(FieldMenuSkin.TextColor(NotebookPalette.Red)));
            Assert.That(icon.color, Is.EqualTo(FieldMenuSkin.TextColor(PlayerUi.Muted)));
            Assert.That(workshopIcon.color, Is.EqualTo(FieldMenuSkin.TextColor(NotebookPalette.Green)));

            button.interactable = false; theme.Refresh();
            Assert.That(label.color, Is.EqualTo(FieldMenuSkin.Disabled));
            Assert.That(icon.color, Is.EqualTo(FieldMenuSkin.Disabled));
            Assert.That(workshopIcon.color, Is.EqualTo(FieldMenuSkin.Disabled));
            // A model update while unavailable replaces the semantic source, not the disabled tint.
            label.color = PlayerUi.Ink; icon.color = NotebookPalette.Red; workshopIcon.color = PlayerUi.Muted;
            yield return null;
            Assert.That(label.color, Is.EqualTo(FieldMenuSkin.Disabled));
            button.interactable = true; theme.Refresh();
            Assert.That(label.color, Is.EqualTo(FieldMenuSkin.Ink));
            Assert.That(icon.color, Is.EqualTo(FieldMenuSkin.TextColor(NotebookPalette.Red)));
            Assert.That(workshopIcon.color, Is.EqualTo(FieldMenuSkin.Muted));
            plate.FieldPrimary = true; theme.Refresh();
            Assert.That(label.color, Is.EqualTo(FieldMenuSkin.AccentInk));
            Assert.That(icon.color, Is.EqualTo(FieldMenuSkin.AccentInk));
            plate.FieldPrimary = false;
            yield return null;
            Assert.That(label.color, Is.EqualTo(FieldMenuSkin.Ink));
            Assert.That(icon.color, Is.EqualTo(FieldMenuSkin.TextColor(NotebookPalette.Red)));
            button.onClick.Invoke(); Assert.That(clicks, Is.EqualTo(1));
        }

        [Test] public void RunningTrialImmediatelyThemesTheRecreatedMeasurementLabels()
        {
            Size(1600, 900); Click("workshop");
            workshop.OpenEntry(game.Run.Workshop.Entries.First(entry => entry.Snapshot?.Recipe.Tower != null));
            workshop.Reflow(1600, 900);
            AssertMenuTheme(PlayerPage.Workshop);
            Click("Composite trial");
            var previousQuote = Named<Text>("Composite trial quote row 0 label");

            Click("Composite trial run");

            Assert.That(workshop.LastTrial, Is.Not.Null);
            Assert.That(Named<Text>("Composite trial quote row 0 label"), Is.Not.SameAs(previousQuote),
                "The run callback must recreate the nested report rows.");
            var labels = workshop.Root.GetComponentsInChildren<Text>()
                .Where(text => (text.name.StartsWith("Composite trial quote row ") ||
                    text.name.StartsWith("Composite trial measured row ")) && text.name.EndsWith(" label")).ToArray();
            Assert.That(labels.Length, Is.GreaterThan(0));
            foreach (var label in labels)
                Assert.That(label.color, Is.EqualTo(FieldMenuSkin.Ink), label.name + " must remain legible after rebuilding.");
        }

        /// <summary>Snapshot input and layout properties, allowing the skin to add decorative objects.</summary>
        sealed class InputContract
        {
            readonly List<Action> assertions = new List<Action>();
            public InputContract(RectTransform scope)
            {
                foreach (var rect in scope.GetComponentsInChildren<RectTransform>(true))
                {
                    var parent = rect.parent; var anchorMin = rect.anchorMin; var anchorMax = rect.anchorMax;
                    var pivot = rect.pivot; var position = rect.anchoredPosition3D; var size = rect.sizeDelta;
                    var offsetMin = rect.offsetMin; var offsetMax = rect.offsetMax;
                    var scale = rect.localScale; var rotation = rect.localRotation; var active = rect.gameObject.activeSelf;
                    assertions.Add(() => {
                        Assert.That(rect, Is.Not.Null, "An existing layout object was removed.");
                        Assert.That(rect.parent, Is.SameAs(parent), rect.name + " parent");
                        Assert.That(rect.anchorMin, Is.EqualTo(anchorMin), rect.name + " minimum anchor");
                        Assert.That(rect.anchorMax, Is.EqualTo(anchorMax), rect.name + " maximum anchor");
                        Assert.That(rect.pivot, Is.EqualTo(pivot), rect.name + " pivot");
                        Assert.That(rect.anchoredPosition3D, Is.EqualTo(position), rect.name + " position");
                        Assert.That(rect.sizeDelta, Is.EqualTo(size), rect.name + " size");
                        Assert.That(rect.offsetMin, Is.EqualTo(offsetMin), rect.name + " minimum offset");
                        Assert.That(rect.offsetMax, Is.EqualTo(offsetMax), rect.name + " maximum offset");
                        Assert.That(rect.localScale, Is.EqualTo(scale), rect.name + " scale");
                        Assert.That(rect.localRotation, Is.EqualTo(rotation), rect.name + " rotation");
                        Assert.That(rect.gameObject.activeSelf, Is.EqualTo(active), rect.name + " visibility");
                    });
                }
                foreach (var graphic in scope.GetComponentsInChildren<Graphic>(true))
                {
                    var raycast = graphic.raycastTarget; var padding = graphic.raycastPadding;
                    assertions.Add(() => {
                        Assert.That(graphic.raycastTarget, Is.EqualTo(raycast), graphic.name + " hit target");
                        Assert.That(graphic.raycastPadding, Is.EqualTo(padding), graphic.name + " hit padding");
                    });
                    foreach (string name in new[] { "Control", "Selected", "Focused", "Disabled", "Hovered", "Pressed", "IsControl", "Chosen" })
                    {
                        var field = graphic.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (field == null) continue;
                        object value = field.GetValue(graphic);
                        assertions.Add(() => Assert.That(field.GetValue(graphic), Is.EqualTo(value), graphic.name + "/" + field.Name));
                    }
                }
                foreach (var group in scope.GetComponentsInChildren<CanvasGroup>(true))
                {
                    bool interactable = group.interactable, blocks = group.blocksRaycasts, ignore = group.ignoreParentGroups;
                    assertions.Add(() => {
                        Assert.That(group.interactable, Is.EqualTo(interactable), group.name);
                        Assert.That(group.blocksRaycasts, Is.EqualTo(blocks), group.name);
                        Assert.That(group.ignoreParentGroups, Is.EqualTo(ignore), group.name);
                    });
                }
                foreach (var control in scope.GetComponentsInChildren<Selectable>(true))
                {
                    var navigation = control.navigation; var target = control.targetGraphic; bool interactable = control.interactable;
                    assertions.Add(() => {
                        Assert.That(control.navigation, Is.EqualTo(navigation), control.name + " keyboard navigation");
                        Assert.That(control.targetGraphic, Is.SameAs(target), control.name + " target graphic");
                        Assert.That(control.interactable, Is.EqualTo(interactable), control.name + " interactability");
                    });
                }
                foreach (var component in scope.GetComponentsInChildren<Component>(true))
                    foreach (var property in component.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (!property.CanRead || property.GetIndexParameters().Length != 0 || !typeof(UnityEventBase).IsAssignableFrom(property.PropertyType)) continue;
                        var callback = (UnityEventBase)property.GetValue(component);
                        if (callback == null) continue;
                        int count = callback.GetPersistentEventCount();
                        assertions.Add(() => {
                            Assert.That(property.GetValue(component), Is.SameAs(callback), component.name + "/" + property.Name);
                            Assert.That(callback.GetPersistentEventCount(), Is.EqualTo(count), component.name + "/" + property.Name);
                        });
                    }
            }
            public void AssertUnchanged() { foreach (var assertion in assertions) assertion(); }
        }
    }
}
#endif
