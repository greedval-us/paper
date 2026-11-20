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
    /// <summary>Native production screens must use the standard art without losing frozen recipes or authored strokes.</summary>
    public sealed class StandardAssetScreenTests
    {
        GameObject root;
        PlayerGameRoot game;
        PlayerShell shell;
        GameBalanceAsset balance;

        [UnitySetUp] public IEnumerator SetUp()
        {
            foreach (var old in Object.FindObjectsByType<PlayerGameRoot>()) Object.Destroy(old.gameObject);
            foreach (var old in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(old.gameObject);
            yield return null;
            balance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/Game/PlayerBalance.asset"));
            root = new GameObject("Standard art screens — production balance, memory only"); root.SetActive(false);
            game = root.AddComponent<PlayerGameRoot>(); game.Configure(balance, true); root.SetActive(true);
            game.Run.AutomaticSimulation = false;
            shell = root.GetComponentInChildren<PlayerShell>(); shell.enabled = false;
            foreach (var responsive in root.GetComponentsInChildren<WorkshopResponsiveLayout>(true)) responsive.enabled = false;
            shell.ApplyLayout(1600, 900);
            Assert.That(game.Run.Workshop.SaveState, Is.EqualTo(ProfileSaveState.MemoryOnly));
            yield return null;
        }

        [UnityTest] public IEnumerator AlbumHealthAndCostIconsRenderVisibleNativeGeometry()
        {
            game.OpenAlbum(); shell.Album.SelectEntry(game.Run.Workshop.Entries.First().Id);
            yield return null; Flush();
            foreach (string metric in new[] { "Health", "Cost" })
            {
                var graphic = shell.Album.Root.GetComponentsInChildren<Graphic>(true)
                    .Single(item => item.name == "Album metric icon " + metric);
                Assert.That(graphic.enabled && graphic.gameObject.activeInHierarchy, Is.True,
                    metric + " must not depend on the removed sprite atlas.");
                Assert.That(graphic, Is.Not.TypeOf<Image>(), "A native symbol must provide the missing atlas icon.");
                Assert.That(graphic.canvasRenderer.GetMesh()?.vertexCount ?? 0, Is.GreaterThan(0));
                Assert.That(graphic.raycastTarget, Is.False);
            }
        }

        [UnityTest] public IEnumerator SharedModernPreviewRetainsEveryFrozenRecipeAndWeaponMount()
        {
            foreach (var entry in game.Run.Workshop.Entries.Where(entry => entry.Kind != AlbumEntryKind.Legacy))
            {
                var probe = Probe("Modern preview " + entry.Id);
                Draw(probe, entry, "Standard recipe preview");
                FieldMenuTheme.ApplyTo(probe, false);
                yield return null; Flush();
                var drawing = probe.GetComponentInChildren<WeaponAssemblyGraphic>();
                Assert.That(drawing, Is.Not.Null, entry.Id + " must use the same recipe composer as battle.");
                Assert.That(drawing.Recipe, Is.SameAs(entry.Snapshot), "Never substitute another standard recipe for a supported frozen assembly.");
                Assert.That(drawing.canvasRenderer.GetMesh()?.vertexCount ?? 0, Is.GreaterThan(0), entry.Id);
                Assert.That(probe.GetComponentsInChildren<NightNotebookPaperGraphic>(), Has.Length.EqualTo(1), entry.Id);
                Assert.That(probe.GetComponentsInChildren<Graphic>().All(item => !item.raycastTarget), Is.True);
                drawing.rectTransform.pivot = new Vector2(.3f, .7f);
                drawing.rectTransform.anchoredPosition3D = new Vector3(17, -23, 2);
                drawing.rectTransform.sizeDelta = new Vector2(280, 160);
                drawing.rectTransform.localRotation = Quaternion.Euler(0, 0, 7);
                drawing.rectTransform.localScale = new Vector3(.85f, .9f, 1);
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    probe.GetComponent<FieldMenuTheme>().Refresh(); Canvas.ForceUpdateCanvases();
                    var paper = probe.GetComponentInChildren<NightNotebookPaperGraphic>().rectTransform;
                    Assert.That(paper.pivot, Is.EqualTo(drawing.rectTransform.pivot));
                    Assert.That(paper.anchoredPosition3D, Is.EqualTo(drawing.rectTransform.anchoredPosition3D));
                    Assert.That(paper.sizeDelta, Is.EqualTo(drawing.rectTransform.sizeDelta));
                    Assert.That(Quaternion.Angle(paper.localRotation, drawing.rectTransform.localRotation), Is.LessThan(.001f));
                    Assert.That(paper.localScale, Is.EqualTo(drawing.rectTransform.localScale));
                    Assert.That(paper.GetComponent<LayoutElement>().ignoreLayout, Is.True);
                    Assert.That(probe.GetComponentsInChildren<NightNotebookPaperGraphic>(), Has.Length.EqualTo(1));
                }
                Assert.DoesNotThrow(() => WorkshopUi.ClearViews(probe),
                    "Retiring a linked preview must not remove another sibling while the production clear loop iterates.");
                Assert.That(probe.childCount, Is.Zero);
                Object.Destroy(probe.gameObject);
            }
        }

        [UnityTest] public IEnumerator HistoricalStockPreviewUsesStandardDrawingOnDarkSquaredPaper()
        {
            var entry = new WorkshopProfile().GetLoadoutEntries().First(item => item != null);
            Assert.That(entry.Kind, Is.EqualTo(AlbumEntryKind.Legacy));
            Assert.That(entry.LegacyBlueprint.HasPlayerDrawing, Is.False);
            var probe = Probe("Historical stock preview"); Draw(probe, entry, "Historical stock");
            FieldMenuTheme.ApplyTo(probe, false);
            yield return null; Flush();
            var drawing = probe.GetComponentInChildren<WeaponAssemblyGraphic>();
            Assert.That(drawing, Is.Not.Null, "Stock pages should use the standard native assembly drawing.");
            Assert.That(drawing.Blueprint, Is.SameAs(entry.LegacyBlueprint), "Keep the selected mounts and modules.");
            Assert.That(drawing.canvasRenderer.GetMesh()?.vertexCount ?? 0, Is.GreaterThan(0));
            Assert.That(probe.GetComponentInChildren<NightNotebookPaperGraphic>(), Is.Not.Null);
            Assert.That(probe.GetComponentsInChildren<BlueprintSketch>(), Is.Empty,
                "The historical authored renderer is reserved for player strokes.");
        }

        [UnityTest] public IEnumerator AuthoredPreviewKeepsOriginalStrokeDataAndProtectedReferencePaper()
        {
            var source = new WorkshopProfile().GetLoadoutEntries().First(item => item != null).LegacyBlueprint;
            var edit = new DrawingEditSession(source);
            Assert.That(edit.AddRectangle(DrawingBindings.Base, new DrawingPoint(-.35f, -.35f),
                new DrawingPoint(.35f, .35f), out var reason), Is.True, reason);
            var blueprint = source.WithDrawing(edit.Snapshot);
            var entry = new AlbumEntry("authored.standard-art-check", "Авторский рисунок", 1, blueprint);
            string before = blueprint.Drawing.GeometryHash;
            var probe = Probe("Authored preview"); Draw(probe, entry, "Preserved authored page");
            FieldMenuTheme.ApplyTo(probe, false);
            yield return null; Flush();
            var drawing = probe.GetComponentInChildren<BlueprintSketch>();
            Assert.That(drawing, Is.Not.Null, "Cleanup must preserve actual player-authored strokes.");
            var bound = typeof(BlueprintSketch).GetField("blueprint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(drawing);
            Assert.That(bound, Is.SameAs(blueprint));
            Assert.That(blueprint.Drawing.GeometryHash, Is.EqualTo(before));
            var papers = probe.GetComponentsInChildren<Image>();
            Assert.That(papers.Any(paper => BattleHudThemeEvidence.Contrast(NotebookPalette.Navy, paper.color) >= 4.5f), Is.True,
                "Historical dark ink needs a small protected reference sheet.");
            Assert.That(probe.GetComponentsInChildren<Graphic>().All(item => !item.raycastTarget), Is.True);
        }

        [UnityTest] public IEnumerator UnmappedSelectedHistoricalPartsUseAnExplicitlyLabelledFallback()
        {
            var pages = new[] {
                TowerBlueprint.NewStandardDraft("unknown.module", "Чужой модуль", "chassis.standard", "weapon.gun",
                    modules: new[] { new ModuleSelection("custom", "module.custom.unmapped") }),
                TowerBlueprint.NewStandardDraft("unknown.mount", "Чужое оружие", "chassis.standard", "weapon.custom.unmapped")
            };
            foreach (var page in pages)
            {
                var entry = new AlbumEntry(page.Id, page.Name, 1, page);
                var probe = Probe("Unsupported stock " + page.Id); Draw(probe, entry, page.Id);
                FieldMenuTheme.ApplyTo(probe, false);
                yield return null; Flush();
                Assert.That(probe.GetComponentsInChildren<WeaponAssemblyGraphic>(), Is.Empty,
                    "A partial composition must not pretend an unrendered selected part is present.");
                var caption = probe.GetComponentsInChildren<Text>().Single(item => item.name == page.Id + " schematic caption");
                Assert.That(caption.text, Is.Not.Empty, "Unsupported pages need a visible family/schematic label.");
                Assert.That(probe.GetComponentsInChildren<Graphic>().Any(item => !(item is Text) &&
                    item.canvasRenderer.GetMesh()?.vertexCount > 0), Is.True, "Fallbacks must render a native symbol.");
            }
        }

        [UnityTest] public IEnumerator ProductionMapUsesADarkNativeSheetAtCompactAndWideSizes()
        {
            foreach (var size in new[] { new Vector2Int(800, 600), new Vector2Int(1600, 900) })
            {
                shell.ApplyLayout(size.x, size.y); game.Navigate(PlayerPage.Map);
                yield return null; Flush();
                var map = root.GetComponentInChildren<NeutralMissionMapGraphic>();
                Assert.That(map, Is.Not.Null);
                var scope = map.transform.parent;
                Assert.That(scope.GetComponentsInChildren<NightNotebookPaperGraphic>(), Has.Length.EqualTo(1),
                    "The current light-ink map needs the standard dark squared sheet at " + size);
                Assert.That(scope.GetComponentsInChildren<Image>().Where(image => image.name.StartsWith("Field schematic backing", StringComparison.Ordinal)), Is.Empty);
                var surface = BattleHudThemeEvidence.Surface(map);
                Assert.That(surface.vertexCount, Is.GreaterThan(0));
                Assert.That(BattleHudThemeEvidence.Luminance(surface.dominantColor), Is.LessThan(.2f),
                    "A daylight field fill must not cover the dark minimap paper at " + size);
            }
        }

        [UnityTest] public IEnumerator UnstartedTrialHintStaysInsideThePrimaryLaneAndClearOfComparisonTitle()
        {
            foreach (var size in new[] { new Vector2Int(800, 600), new Vector2Int(1600, 900) })
            {
                shell.ApplyLayout(size.x, size.y); game.EditAlbumEntry(null);
                var workshop = root.GetComponentInChildren<WorkshopHud>(true).Composite;
                ((RectTransform)workshop.Root.parent).sizeDelta = size; workshop.Reflow(size.x, size.y);
                for (int step = 0; step < 3; step++) PressWorkshop(workshop, "Sequence next");
                PressWorkshop(workshop, "Composite trial"); yield return null; Flush();
                var hint = workshop.Root.GetComponentsInChildren<Text>().Single(item => item.name == "Composite trial field hint");
                var first = workshop.Root.GetComponentsInChildren<RectTransform>().Single(item => item.name == "Composite trial first viewport");
                var comparison = workshop.Root.GetComponentsInChildren<Text>().Single(item => item.name == "Composite trial second lane name");
                Assert.That(hint.transform.IsChildOf(first), Is.True, "The unstarted message belongs to the primary lane.");
                var inside = BattleHudThemeEvidence.Bounds(first, hint.rectTransform);
                Assert.That(first.rect.Contains(inside.min) && first.rect.Contains(inside.max), Is.True, "Hint must stay within the protected lane bounds at " + size);
                var hintBounds = BattleHudThemeEvidence.Bounds(workshop.Root, hint.rectTransform);
                var titleBounds = BattleHudThemeEvidence.Bounds(workshop.Root, comparison.rectTransform);
                Assert.That(hintBounds.Overlaps(titleBounds), Is.False, "Unstarted copy must not obscure the comparison heading at " + size);
                Assert.That(hint.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(hint.text.Length));
                PressWorkshop(workshop, "Composite trial header return");
                game.ReturnFromWorkshop();
            }
        }

        static void PressWorkshop(CompositeWorkshopPanel workshop, string name)
        {
            var button = workshop.Root.GetComponentsInChildren<Button>().Single(item => item.name == name);
            Assert.That(button.IsInteractable(), Is.True, name); button.onClick.Invoke(); Canvas.ForceUpdateCanvases();
        }

        RectTransform Probe(string name)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(shell.transform, false);
            var rect = (RectTransform)go.transform; PlayerUi.Place(rect, new Rect(24, 90, 400, 240)); return rect;
        }

        static void Draw(RectTransform parent, AlbumEntry entry, string identity)
        {
            var type = typeof(PlayerShell).Assembly.GetType("PaperDefense.Presentation.StandardAssemblyIllustration");
            Assert.That(type, Is.Not.Null, "All screens need one standard illustration policy.");
            var draw = type.GetMethod("Draw", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(Transform), typeof(AlbumEntry), typeof(Rect), typeof(string) }, null);
            Assert.That(draw, Is.Not.Null);
            draw.Invoke(null, new object[] { parent, entry, new Rect(0, 0, 400, 240), identity });
        }

        void Flush()
        {
            UiMenuMotion.FinishIn(root.transform);
            foreach (var theme in root.GetComponentsInChildren<FieldMenuTheme>(true)) theme.Refresh();
            foreach (var theme in root.GetComponentsInChildren<BattleHudTheme>(true)) theme.Refresh();
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            EventSystem.current?.SetSelectedGameObject(null);
            if (root != null) Object.Destroy(root);
            if (balance != null) Object.Destroy(balance);
            yield return null;
        }
    }
}
#endif
