#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    /// <summary>Production battle controls must retain their input contract while becoming readable on the night field.</summary>
    public sealed class BattleHudThemeTests : BattleHudThemeFixture
    {
        static readonly Vector2Int[] Sizes = {
            new Vector2Int(800, 600), new Vector2Int(1024, 768), new Vector2Int(1280, 720),
            new Vector2Int(1600, 900), new Vector2Int(1920, 1080)
        };

        [UnityTest] public IEnumerator BattlePanelsRenderDarkSurfacesAtEverySupportedSize()
        {
            StartBattle();
            foreach (var size in Sizes)
            {
                Size(size); yield return null;
                Click("Battle context toggle"); Hud.Refresh(); yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(Theme, Is.Not.Null, "The battle needs its own scoped theme: " + size);
                Assert.That(Battle.GetComponentsInChildren<FieldMenuTheme>(true), Is.Empty,
                    "The battle theme must remain independent from menu overlays.");
                var header = Named<RectTransform>("Battle header");
                var castleChip = BattleHudThemeEvidence.Bounds(header, Named<RectTransform>("Battle live castle chip"));
                var waveRibbon = BattleHudThemeEvidence.Bounds(header, Named<RectTransform>("Battle wave ribbon"));
                Assert.That(castleChip.Overlaps(waveRibbon), Is.False, "The wave ribbon must not cover castle health at " + size);
                foreach (string action in new[] { "Album battle erase", "Album battle blotter", "Tactical pause" })
                {
                    var button = Named<Button>(action);
                    var glyph = button.GetComponentInChildren<PlayerIconGraphic>();
                    Assert.That(glyph, Is.Not.Null, action + " needs a visible semantic glyph.");
                    Assert.That(glyph.canvasRenderer.GetMesh()?.vertexCount ?? 0, Is.GreaterThan(0));
                    AssertContained(((RectTransform)button.transform).rect, BattleHudThemeEvidence.Bounds((RectTransform)button.transform, glyph.rectTransform), action);
                    Assert.That(button.GetComponent<PlayerTooltip>().Message, Is.Not.Empty);
                    EventSystem.current.SetSelectedGameObject(button.gameObject);
                    Canvas.ForceUpdateCanvases();
                    var caption = Named<Text>("Battle tool hint");
                    Assert.That(caption.gameObject.activeInHierarchy, Is.True);
                    Assert.That(caption.text, Is.EqualTo(button.GetComponent<PlayerTooltip>().Message));
                    Assert.That(caption.preferredHeight, Is.LessThanOrEqualTo(caption.rectTransform.rect.height + 1), "Action hint height at " + size);
                    Assert.That(caption.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(caption.text.Length), "Action hint must remain complete at " + size);
                    EventSystem.current.SetSelectedGameObject(null);
                }
                foreach (string name in new[] { "Battle header", "Construction dock", "Build tool 0", "Battle context" })
                {
                    var card = Named<PlayerCardGraphic>(name);
                    Assert.That(card.FieldStyle, Is.True, name + " at " + size);
                    var surface = BattleHudThemeEvidence.Surface(card);
                    Assert.That(surface.vertexCount, Is.GreaterThan(0), name + " must have a rendered mesh.");
                    Assert.That(surface.whiteAreaFraction, Is.LessThan(.08f), name + " still renders a large white background at " + size);
                    Assert.That(BattleHudThemeEvidence.Luminance(surface.dominantColor), Is.LessThan(.20f), name + " needs a dark fill.");
                }
                var firstCard = Named<Button>("Build tool 0");
                EventSystem.current.SetSelectedGameObject(firstCard.gameObject);
                Canvas.ForceUpdateCanvases();
                var nameLabel = Named<Text>("Battle tool hint");
                Assert.That(nameLabel.gameObject.activeInHierarchy, Is.True);
                Assert.That(nameLabel.text, Does.Contain(GameText.Resolve(game.Run.RunLoadout.Entries[0].Name)));
                var priceLabel = Named<Text>("Album battle price 0");
                var cardFill = BattleHudThemeEvidence.Surface(Named<PlayerCardGraphic>("Build tool 0")).dominantColor;
                Assert.That(BattleHudThemeEvidence.Contrast(nameLabel.color, BattleHudThemeEvidence.Surface(Named<PlayerCardGraphic>("Battle tool hint plate")).dominantColor), Is.GreaterThanOrEqualTo(4.5f), "Card name contrast at " + size);
                Assert.That(BattleHudThemeEvidence.Contrast(priceLabel.color, cardFill), Is.GreaterThanOrEqualTo(4.5f), "Card price contrast at " + size);
                Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
            }
        }

        [UnityTest] public IEnumerator ToolHintRemainsProtectedAndReadableAfterRefreshAndTextGrowth()
        {
            StartBattle();
            foreach (var size in Sizes)
            {
                Size(size); yield return null;
                foreach (string value in new[] { ShortHint, LongHint })
                {
                    Hud.ShowHint(value); Hud.Refresh(); yield return null;
                    Canvas.ForceUpdateCanvases();
                    AssertProtectedHint(size, value == ShortHint);
                    Hud.Refresh(); yield return null;
                    Canvas.ForceUpdateCanvases();
                    AssertProtectedHint(size, value == ShortHint);
                }
            }
        }

        void AssertProtectedHint(Vector2Int size, bool fullShortHint)
        {
            var hint = Named<Text>("Battle tool hint");
            var plate = Named<PlayerCardGraphic>("Battle tool hint plate");
            Assert.That(hint.transform.IsChildOf(plate.transform), Is.True, "The hint needs a local protected surface.");
            Assert.That(plate.FieldStyle, Is.True);
            Assert.That(plate.raycastTarget, Is.False, "The hint strip must not consume battlefield input.");
            Assert.That(hint.raycastTarget, Is.False);
            Assert.That(plate.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
            Assert.That(hint.color.a, Is.GreaterThanOrEqualTo(.95f));
            var fill = BattleHudThemeEvidence.Surface(plate).dominantColor;
            Assert.That(BattleHudThemeEvidence.Contrast(hint.color, fill), Is.GreaterThanOrEqualTo(4.5f), "Hint/plate contrast at " + size);
            Assert.That(BattleHudThemeEvidence.Contrast(hint.color, NightNotebookArt.Paper), Is.GreaterThanOrEqualTo(4.5f), "Hint/night-field contrast at " + size);
            Assert.That(hint.text, Is.Not.Empty);
            if (fullShortHint) Assert.That(hint.text, Is.EqualTo(ShortHint), "The normal tool/material caption must remain complete.");
            else Assert.That(hint.text, Is.EqualTo(LongHint), "Expandable hints must retain the complete rejection reason.");
            Assert.That(hint.preferredHeight, Is.LessThanOrEqualTo(hint.rectTransform.rect.height + 1), "Hint lines must fit at " + size);
            Assert.That(hint.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(hint.text.Length), "The displayed hint must not be silently clipped at " + size);
            AssertContained(plate.rectTransform.rect, BattleHudThemeEvidence.Bounds(plate.rectTransform, hint.rectTransform), "Hint inside its plate at " + size);
            var bounds = BattleHudThemeEvidence.Bounds(Battle, plate.rectTransform);
            AssertContained(Battle.rect, bounds, "Hint plate inside canvas at " + size);
            Assert.That(plate.rectTransform.rect.width, Is.LessThanOrEqualTo(Mathf.Min(shell.Layout.Width - 32, 720) + 1));
            Assert.That(plate.rectTransform.rect.height, Is.InRange(28f, shell.Layout.Field.height - 48));
            Assert.That(plate.rectTransform.rect.height, Is.GreaterThanOrEqualTo(hint.preferredHeight + 7), "The surface must grow with multiline hints.");
            var dock = BattleHudThemeEvidence.Bounds(Battle, Named<RectTransform>("Construction dock"));
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(dock.yMax - 1), "The protected strip must stay above the dock.");
        }

        [UnityTest] public IEnumerator InkStocksKeepTheirColorsThroughHoverFocusAndRefresh()
        {
            StartBattle(); Size(new Vector2Int(1600, 900)); yield return null;
            foreach (int index in new[] { 1, 2, 3 })
            {
                Hud.Refresh(); yield return null; AssertInkRoles();
                var button = InkStock((InkColor)index);
                var pointer = new PointerEventData(EventSystem.current);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                Hud.Refresh(); yield return null; AssertInkRoles();
                EventSystem.current.SetSelectedGameObject(button.gameObject);
                Hud.Refresh(); yield return null; AssertInkRoles();
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                EventSystem.current.SetSelectedGameObject(null);
                Hud.Refresh(); yield return null; AssertInkRoles();
            }
        }

        Selectable InkStock(InkColor color) => Named<Selectable>("Ink stock " + color);

        void AssertInkRoles()
        {
            Color red = InkStock(InkColor.Red).GetComponentInChildren<PlayerIconGraphic>().color;
            Color blue = InkStock(InkColor.Blue).GetComponentInChildren<PlayerIconGraphic>().color;
            Color green = InkStock(InkColor.Green).GetComponentInChildren<PlayerIconGraphic>().color;
            Assert.That(red.r, Is.GreaterThan(red.g * 1.15f), "Red ink lost its red role.");
            Assert.That(red.r, Is.GreaterThan(red.b * 1.15f));
            Assert.That(blue.b, Is.GreaterThan(blue.r * 1.15f), "Blue ink must not become brass or ivory.");
            Assert.That(blue.b, Is.GreaterThan(blue.g * 1.08f));
            Assert.That(green.g, Is.GreaterThan(green.r * 1.08f), "Green ink lost its green role.");
            Assert.That(green.g, Is.GreaterThan(green.b * 1.04f));
        }

        [UnityTest] public IEnumerator SelectedBaseToolAndDisabledWaveLaunchSurviveTheTheme()
        {
            StartBattle(); Size(new Vector2Int(1600, 900)); yield return null;
            Click("Build tool 2"); Hud.Refresh(); yield return null;
            Assert.That(game.Drawing.BlueprintSlot, Is.EqualTo(2));
            Assert.That(game.Drawing.Tier, Is.EqualTo(MaterialTier.Pencil));
            Assert.That(Named<PlayerCardGraphic>("Build tool 2").Selected, Is.True);
            Assert.That(Named<PlayerCardGraphic>("Build tool 0").Selected, Is.False);
            var launch = Named<Button>("Launch wave"); Assert.That(launch.IsInteractable(), Is.True);
            Click("Launch wave"); Hud.Refresh(); yield return null;
            Assert.That(game.Run.Session.Phase, Is.EqualTo(RunPhase.Assault));
            Assert.That(launch.interactable, Is.False);
            Assert.That(launch.IsInteractable(), Is.False);
            Assert.That(launch.GetComponent<PlayerCardGraphic>().Disabled, Is.True);
            Assert.That(Named<PlayerCardGraphic>("Build tool 2").Selected, Is.True);
            Assert.That(BattleHudThemeEvidence.Surface(launch.targetGraphic).whiteAreaFraction, Is.LessThan(.08f));
        }


        [UnityTest] public IEnumerator LiveWarningsAndInkStocksSurviveDisableAndSourceChanges()
        {
            StartBattle(); Size(new Vector2Int(1600, 900)); yield return null;
            var theme = (BattleHudTheme)Theme;
            var tool = Named<Button>("Build tool 0");
            var price = Named<Text>("Album battle price 0");
            var inkButton = InkStock(InkColor.Blue);
            var glyph = inkButton.GetComponentInChildren<PlayerIconGraphic>();

            // The controller publishes raw semantic colors; the presentation must keep later writes live.
            price.color = NotebookPalette.Red; glyph.color = NotebookPalette.Blue;
            theme.SyncColors();
            AssertRed(price.color); AssertBlue(glyph.color);
            Color warning = price.color;
            tool.interactable = false; inkButton.interactable = false; theme.SyncColors();
            Assert.That(Vector4.Distance(price.color, warning), Is.GreaterThan(.1f), "A disabled action needs a distinct visual state.");
            glyph.color = NotebookPalette.Green; price.color = NotebookPalette.Red;
            theme.SyncColors();
            tool.interactable = true; inkButton.interactable = true; theme.SyncColors();
            AssertRed(price.color); AssertGreen(glyph.color);

            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(inkButton.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            EventSystem.current.SetSelectedGameObject(inkButton.gameObject);
            // Hover/focus refreshes the stock: its model-owned blue color
            // supersedes the temporary source write used above to test theme updates.
            yield return null; theme.SyncColors(); AssertBlue(glyph.color);
            ExecuteEvents.Execute(inkButton.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            EventSystem.current.SetSelectedGameObject(null);
            price.color = PlayerUi.Ink; theme.SyncColors();
            Canvas.ForceUpdateCanvases();
            var fill = BattleHudThemeEvidence.Surface(tool.targetGraphic).dominantColor;
            Assert.That(BattleHudThemeEvidence.Contrast(price.color, fill), Is.GreaterThanOrEqualTo(4.5f), "Normal affordability text must recover after the warning.");
        }

        [UnityTest] public IEnumerator SavedAuthoredReferenceSheetKeepsDarkDrawingsReadableInTheCurrentTheme()
        {
            StartBattle(); Size(new Vector2Int(1600, 900)); yield return null;
            var preview = PlayerUi.Graphic<PlayerCardGraphic>(Battle, "Legacy inspector preview probe", new Rect(20, 120, 296, 112));
            preview.color = FieldMenuSkin.Panel;
            var legacy = new WorkshopProfile().GetLoadoutEntries().First(entry => entry != null);
            Assert.That(legacy.Kind, Is.EqualTo(AlbumEntryKind.Legacy));
            var edit = new DrawingEditSession(legacy.LegacyBlueprint);
            Assert.That(edit.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.3f, -.3f),
                new DrawingPoint(.3f, .3f), out var reason), Is.True, reason);
            var authored = legacy.LegacyBlueprint.WithDrawing(edit.Snapshot);
            string geometryBefore = authored.Drawing.GeometryHash;
            legacy = new AlbumEntry("authored.battle-reference", "Сохранённый рисунок", 1, authored);
            var illustrate = typeof(PlayerBattleHud).GetMethod("AlbumIllustration", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(illustrate, Is.Not.Null);
            illustrate.Invoke(Hud, new object[] { preview.transform, legacy, new Rect(0, 0, 296, 112), "Battle inspected" });
            var paper = Named<Image>("Battle inspected certified assembly authored reference sheet");
            Assert.That(paper.raycastTarget, Is.False);
            for (int refresh = 0; refresh < 3; refresh++)
            {
                RefreshTheme(); yield return null; Canvas.ForceUpdateCanvases();
                var surface = BattleHudThemeEvidence.Surface(paper);
                Assert.That(surface.vertexCount, Is.GreaterThan(0));
                Assert.That(BattleHudThemeEvidence.Contrast(NotebookPalette.Navy, surface.dominantColor), Is.GreaterThanOrEqualTo(4.5f),
                    "The local reference paper must protect historical dark ink even after repeated theme registration.");
                var drawing = preview.GetComponentInChildren<BlueprintSketch>();
                Assert.That(drawing, Is.Not.Null);
                Assert.That(drawing.canvasRenderer.GetMesh()?.vertexCount ?? 0, Is.GreaterThan(0));
                Assert.That(authored.Drawing.GeometryHash, Is.EqualTo(geometryBefore));
            }
        }

        static void AssertRed(Color value)
        { Assert.That(value.r, Is.GreaterThan(value.g * 1.15f)); Assert.That(value.r, Is.GreaterThan(value.b * 1.15f)); }
        static void AssertBlue(Color value)
        { Assert.That(value.b, Is.GreaterThan(value.r * 1.15f)); Assert.That(value.b, Is.GreaterThan(value.g * 1.08f)); }
        static void AssertGreen(Color value)
        { Assert.That(value.g, Is.GreaterThan(value.r * 1.08f)); Assert.That(value.g, Is.GreaterThan(value.b * 1.04f)); }

        static void AssertContained(Rect outer, Rect inner, string message)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - 1), message);
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + 1), message);
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - 1), message);
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + 1), message);
        }
    }

    /// <summary>Shared disk-free production setup for the regression and opt-in evidence fixture.</summary>
    public abstract class BattleHudThemeFixture
    {
        protected const string ShortHint = "Дозор · базовая сборка";
        protected const string LongHint = "Нельзя построить здесь: клетка уже занята. Выберите свободное место для Дозора и проверьте, что путь к замку остаётся открытым.";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        protected GameObject root;
        protected PlayerGameRoot game;
        protected PlayerShell shell;
        protected GameBalanceAsset balance;
        protected PlayerBattleHud Hud => (PlayerBattleHud)typeof(PlayerShell).GetField("battle", Private).GetValue(shell);
        protected RectTransform Battle => (RectTransform)typeof(PlayerShell).GetField("battleRoot", Private).GetValue(shell);
        protected Component Theme => Battle.GetComponents<Component>().SingleOrDefault(component => component.GetType().Name == "BattleHudTheme");

        [UnitySetUp] public IEnumerator SetUp()
        {
            foreach (var previous in Object.FindObjectsByType<PlayerGameRoot>()) Object.Destroy(previous.gameObject);
            foreach (var previous in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(previous.gameObject);
            yield return null;
            var source = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            Assert.That(source, Is.Not.Null, "The battle fixture uses the production balance without changing it.");
            balance = Object.Instantiate(source);
            root = new GameObject("Battle HUD theme fixture — production balance, memory only"); root.SetActive(false);
            game = root.AddComponent<PlayerGameRoot>(); game.Configure(balance, true); root.SetActive(true);
            game.Run.AutomaticSimulation = false;
            shell = root.GetComponentInChildren<PlayerShell>(); shell.enabled = false;
            foreach (var responsive in root.GetComponentsInChildren<WorkshopResponsiveLayout>(true)) responsive.enabled = false;
            Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
            yield return null;
        }

        protected void StartBattle()
        { Assert.That(game.StartBattle(), Is.True, game.Status); Hud.Refresh(); Canvas.ForceUpdateCanvases(); }
        protected void Size(Vector2Int size)
        { shell.ApplyLayout(size.x, size.y); Hud.Refresh(); Canvas.ForceUpdateCanvases(); }
        protected T Named<T>(string name) where T : Component
        {
            var component = Battle.GetComponentsInChildren<T>(true).SingleOrDefault(item => item.name == name);
            Assert.That(component, Is.Not.Null, "Missing production battle control: " + name);
            return component;
        }
        protected void Click(string name)
        {
            var button = Named<Button>(name);
            Assert.That(button.IsInteractable(), Is.True, name + " must remain usable.");
            button.onClick.Invoke(); Hud.Refresh(); Canvas.ForceUpdateCanvases();
        }
        protected void RefreshTheme()
        {
            Assert.That(Theme, Is.Not.Null, "The production battle root needs BattleHudTheme.");
            var refresh = Theme.GetType().GetMethod("Refresh", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(refresh, Is.Not.Null, "Capture needs the same public theme refresh used by the battle.");
            refresh.Invoke(Theme, null);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            EventSystem.current?.SetSelectedGameObject(null);
            if (root != null) Object.Destroy(root);
            if (balance != null) Object.Destroy(balance);
            yield return null;
        }
    }

    internal static class BattleHudThemeEvidence
    {
        [Serializable] internal sealed class SurfaceSample
        {
            public int vertexCount;
            public float whiteAreaFraction;
            public Color dominantColor;
        }
        internal static SurfaceSample Surface(Graphic graphic)
        {
            var mesh = graphic.canvasRenderer.GetMesh();
            if (mesh == null || mesh.vertexCount == 0) return new SurfaceSample();
            var vertices = mesh.vertices; var colors = mesh.colors32; var triangles = mesh.triangles;
            float largest = 0, white = 0;
            Color dominant = Color.clear;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                float area = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).magnitude * .5f;
                Color tint = ((Color)colors[a] + (Color)colors[b] + (Color)colors[c]) / 3;
                if (area > largest && tint.a > .5f) { largest = area; dominant = tint; }
                if (tint.a > .7f && tint.r > .80f && tint.g > .80f && tint.b > .80f) white += area;
            }
            float plateArea = graphic.rectTransform.rect.width * graphic.rectTransform.rect.height;
            return new SurfaceSample { vertexCount = mesh.vertexCount, dominantColor = dominant,
                whiteAreaFraction = plateArea > 0 ? white / plateArea : 0 };
        }
        internal static Rect Bounds(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]), max = min;
            for (int i = 1; i < corners.Length; i++)
            { Vector2 point = parent.InverseTransformPoint(corners[i]); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return new Rect(min, max - min);
        }
        internal static float Luminance(Color color)
            => .2126f * Linear(color.r) + .7152f * Linear(color.g) + .0722f * Linear(color.b);
        internal static float Contrast(Color first, Color second)
        { float a = Luminance(first), b = Luminance(second); return (Mathf.Max(a, b) + .05f) / (Mathf.Min(a, b) + .05f); }
        static float Linear(float channel) => channel <= .04045f ? channel / 12.92f : Mathf.Pow((channel + .055f) / 1.055f, 2.4f);
    }
}
#endif
