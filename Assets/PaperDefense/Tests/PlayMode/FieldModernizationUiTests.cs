#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class FieldModernizationUiTests
    {
        GameObject root;
        PlayerGameRoot game;
        GameBalanceAsset balance;
        BalancePresetAsset preset;

        [UnitySetUp] public IEnumerator SetUp()
        {
            foreach (var old in Object.FindObjectsByType<PlayerGameRoot>()) Object.Destroy(old.gameObject);
            foreach (var old in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(old.gameObject);
            yield return null;
            root = new GameObject("Field modernization UI fixture"); root.SetActive(false);
            new GameObject("Fixture camera", typeof(Camera)).transform.SetParent(root.transform);
            var source = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/Game/PlayerBalance.asset");
            balance = Object.Instantiate(source); preset = Object.Instantiate(source.dataPreset);
            balance.dataPreset = preset; preset.rules.PlanningSeconds = 1000;
            game = root.AddComponent<PlayerGameRoot>(); game.Configure(balance, true);
            root.SetActive(true); yield return null;
            Assert.That(game.StartBattle(), Is.True);
            game.Run.AutomaticSimulation = false;
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        { Object.Destroy(root); Object.Destroy(balance); Object.Destroy(preset); yield return null; }

        Button Find(string name) => root.GetComponentsInChildren<Button>().Single(button => button.name == name);

        void AssertFieldConfirmation()
        {
            Canvas.ForceUpdateCanvases();
            var card = root.GetComponentsInChildren<PlayerCardGraphic>().Single(graphic => graphic.name == "Confirmation");
            Assert.That(FieldMenuTheme.IsThemed(card.transform), Is.True, "Battle confirmations need their own dark field theme scope.");
            Assert.That(card.FieldStyle, Is.True);
            Assert.That(BattleHudThemeEvidence.Luminance(BattleHudThemeEvidence.Surface(card).dominantColor), Is.LessThan(.2f));
            foreach (string name in new[] { "Cancel confirmation", "Confirm action" })
            {
                var button = Find(name); var surface = (PlayerCardGraphic)button.targetGraphic;
                Assert.That(surface.FieldStyle, Is.True);
                Assert.That(BattleHudThemeEvidence.Contrast(button.GetComponentInChildren<Text>().color,
                    BattleHudThemeEvidence.Surface(surface).dominantColor), Is.GreaterThanOrEqualTo(4.5f));
            }
            var selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.transform.IsChildOf(card.transform), Is.True, "Keyboard focus must stay inside the confirmation.");
        }

        StructureView BuildTower(int slot = 0)
        {
            for (int y = 2; y < game.Run.Session.Balance.Height - 2; y++)
            for (int x = 2; x < game.Run.Session.Balance.Width - 2; x++)
            {
                var cells = new[] { new Cell(x, y) };
                if (!game.Run.QuoteEntry(cells, slot, MaterialTier.Pencil, InkColor.None).Valid) continue;
                Assert.That(game.Run.BuildEntry(cells, slot, MaterialTier.Pencil, InkColor.None, out _), Is.True);
                for (int step = 0; step < 160; step++) game.Run.StepSimulation();
                return game.Run.Session.StructureAt(cells[0]).Value;
            }
            Assert.Fail("No legal tower position in first mission."); return default;
        }

        void Inspect(StructureView tower, int width, int height)
        {
            game.Drawing.Inspect(tower.Cell);
            root.GetComponentInChildren<PlayerShell>().ApplyLayout(width, height);
            Canvas.ForceUpdateCanvases();
        }

        [Test] public void InspectorShowsTheInstalledWeaponSectorOnTacticalPause()
        {
            var tower = BuildTower();
            game.Run.TogglePause();
            Inspect(tower, 800, 600);
            Assert.That(root.GetComponentsInChildren<Button>(true).Any(button => button.name == "Turn tower"), Is.False);
            Assert.That(root.GetComponentsInChildren<Text>().Any(label => label.text.Contains("Сектор") && label.text.Contains("°")), Is.True);
            Assert.That(game.Drawing.IsPlacingTower, Is.False);
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle));
            Assert.That(game.Run.TacticalPaused, Is.True);
            Assert.That(game.Run.Session.StructureAt(tower.Cell).Value.FacingDegrees, Is.EqualTo(tower.FacingDegrees));
            Assert.That(game.Drawing.InspectedCell, Is.EqualTo(tower.Cell));
        }

        void AssertUpgradeSelectionDoesNotChangeTheField(StructureView tower, System.Action select)
        {
            int ink = game.Run.Session.Ink, count = game.Run.Session.Structures.Count;
            long tick = game.Run.Session.TickIndex;
            var before = game.Run.Session.StructureAt(tower.Cell).Value;
            var coloredInk = new[] { InkColor.Red, InkColor.Blue, InkColor.Green }
                .Select(color => game.Run.Session.ColoredInk(color)).ToArray();
            select();
            Assert.That(game.Drawing.Mode, Is.EqualTo(DrawingMode.Build), "Opening upgrades must safely leave destructive tools.");
            Assert.That(game.Drawing.Message, Does.Contain("ЛКМ по башне").And.Contain("Space"));
            Assert.That(game.Run.Session.Ink, Is.EqualTo(ink));
            Assert.That(game.Run.Session.Structures.Count, Is.EqualTo(count));
            Assert.That(game.Run.Session.TickIndex, Is.EqualTo(tick));
            Assert.That(new[] { InkColor.Red, InkColor.Blue, InkColor.Green }
                .Select(color => game.Run.Session.ColoredInk(color)), Is.EqualTo(coloredInk));
            var after = game.Run.Session.StructureAt(tower.Cell);
            Assert.That(after.HasValue, Is.True, "Choosing upgrades must never erase the selected tower.");
            Assert.That(after.Value.Id, Is.EqualTo(before.Id));
            Assert.That(after.Value.Health, Is.EqualTo(before.Health));
            Assert.That(after.Value.ResolvedEntry, Is.SameAs(before.ResolvedEntry));
            Assert.That(after.Value.Modernization.Revision, Is.EqualTo(before.Modernization.Revision));
            Assert.That(after.Value.Modernization.IsWorking, Is.False);
            Assert.That(after.Value.Color, Is.EqualTo(before.Color));
        }

        static void AssertVisibleUpgradeText(Text text, string context)
        {
            Assert.That(text.gameObject.activeInHierarchy, Is.True, context);
            Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(14), context);
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), context + ": " + text.text);
            Assert.That(text.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(text.text.Length),
                context + " must render every character: " + text.text);
        }

        void StartWithEmptyLastSlot()
        {
            game.ReturnToMap();
            Assert.That(game.Run.AssemblyCommands.AssignSlot(ProfileLimits.LoadoutSlotCount - 1, null, System.Guid.NewGuid()).Success, Is.True);
            Assert.That(game.StartBattle(), Is.True);
            game.Run.AutomaticSimulation = false;
            Assert.That(game.Run.RunLoadout.Entries.Last(), Is.Null);
        }

        [UnityTest] public IEnumerator HistoricalMachineGunAndFreeformPageBuyUpgradesFromTheirFieldBadges()
        {
            game.ReturnToMap();
            var machineGun = new WorkshopProfile().GetLoadoutBlueprints().Single(page => page?.Id == "starter.gun");
            var freeform = FreeformWorkshopTests.Drawing();
            var historical = new[] { machineGun, freeform };
            for (int slot = 0; slot < historical.Length; slot++)
            {
                var page = historical[slot];
                Assert.That(game.Run.SaveBlueprint(page, out var reason), Is.True, reason);
                Assert.That(game.Run.AssignBlueprint(slot, page.Id, out reason), Is.True, reason);
            }
            Assert.That(game.StartBattle(), Is.True);
            game.Run.AutomaticSimulation = false;
            Assert.That(game.Run.RunLoadout.Entries.Any(page => page?.Kind == AlbumEntryKind.Tower), Is.True,
                "Historical pages share this loadout with current assembly pages.");
            yield return null;

            for (int slot = 0; slot < historical.Length; slot++)
            {
                var built = BuildTower(slot);
                var original = historical[slot];
                Assert.That(built.IsComplete, Is.True);
                Assert.That(built.SourceAlbumEntryId, Is.EqualTo(original.Id));
                Assert.That(built.Blueprint.Origin, Is.EqualTo(original.Origin));
                var center = new Vector2(built.Cell.X + .5f, built.Cell.Y + .5f);
                var aim = new Point2(center.x + 3, center.y + 2);
                var muzzle = NotebookTowerComposition.Muzzle(built, 0, aim, 0, null, NotebookVisualThemeAsset.Default);
                game.Drawing.Focus(built.Cell);
                yield return null;
                Find("Battle upgrades").onClick.Invoke();
                var overlay = root.GetComponentInChildren<TowerDevelopmentOverlay>();
                overlay.RefreshNow();
                var badge = overlay.GetComponentsInChildren<Button>(true).Single(button => button.name == "Tower development " + built.Id);
                Assert.That(badge.gameObject.activeInHierarchy, Is.True, original.Id);
                badge.onClick.Invoke();
                Assert.That(game.Drawing.InspectedCell, Is.EqualTo(built.Cell));
                yield return null;
                var quote = game.Run.QuoteModernization(built.Id, ModernizationDirection.Power);
                Assert.That(quote.Valid, Is.True, quote.Reason);
                Assert.That(quote.After.Weapons[0].Damage, Is.GreaterThan(quote.Before.Weapons[0].Damage));
                var buy = Find("Modernize Power");
                Assert.That(buy.IsInteractable(), Is.True, original.Id);
                int ink = game.Run.Session.Ink;
                buy.onClick.Invoke();
                Assert.That(game.Run.Session.Ink, Is.EqualTo(ink - quote.Cost));
                Assert.That(game.Run.Session.StructureAt(built.Cell).Value.Modernization.IsWorking, Is.True);
                for (int step = 0; step < 180 && game.Run.Session.StructureAt(built.Cell).Value.Modernization.IsWorking; step++)
                    game.Run.StepSimulation();
                var upgraded = game.Run.Session.StructureAt(built.Cell).Value;
                Assert.That(upgraded.Modernization.Rank(ModernizationDirection.Power), Is.EqualTo(1));
                Assert.That(upgraded.ModernizationEntry.Weapons[0].Damage, Is.EqualTo(quote.After.Weapons[0].Damage));
                Assert.That(upgraded.FacingDegrees, Is.EqualTo(built.FacingDegrees));
                Assert.That(upgraded.Blueprint.Drawing.GeometryHash, Is.EqualTo(original.Drawing.GeometryHash));
                Assert.That(DoodleGlyphs.SegmentOrigin(upgraded, 0, aim, center, 0, null, NotebookVisualThemeAsset.Default),
                    Is.EqualTo(muzzle), "Shots must still leave the original barrel.");
                overlay.RefreshNow();
                Assert.That(badge.GetComponent<PlayerTooltip>().Message, Does.Contain("1/6"));
            }
        }

        [UnityTest] public IEnumerator GeneratorAndWallOfferTheirOwnActionsInsteadOfWeaponUpgrades()
        {
            int Slot(AssemblyStructureFamily kind) => Enumerable.Range(0, game.Run.RunLoadout.Entries.Count)
                .Single(index => game.Run.RunLoadout.Resolved[index]?.Support?.Kind == kind);
            var generator = BuildTower(Slot(AssemblyStructureFamily.Generator));
            Inspect(generator, 800, 600);
            yield return null;
            Assert.That(root.GetComponentsInChildren<Text>().Any(label => label.text.Contains("Доход") && label.text.Contains("волн")), Is.True);
            Assert.That(root.GetComponentsInChildren<Button>().Any(button => button.name == "Modernize Power"), Is.False);
            Assert.That(root.GetComponentsInChildren<Button>().Any(button => button.name == "Fortify support" && button.IsInteractable()), Is.False);
            var wall = BuildTower(Slot(AssemblyStructureFamily.Wall));
            Inspect(wall, 800, 600);
            yield return null;
            Assert.That(root.GetComponentsInChildren<Button>().Any(button => button.name == "Modernize Power"), Is.False);
            var fortify = Find("Fortify support");
            Assert.That(fortify.IsInteractable(), Is.True);
            int ink = game.Run.Session.Ink;
            fortify.onClick.Invoke();
            Assert.That(game.Run.Session.Ink, Is.LessThan(ink));
            Assert.That(game.Run.Session.StructureAt(wall.Cell).Value.Tier, Is.EqualTo(MaterialTier.Ink));
        }

        [UnityTest] public IEnumerator LegacyWallFortificationUsesItsOwnPriceWithEmptyAlbumSlots()
        {
            StartWithEmptyLastSlot(); yield return null;
            var session = (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Run);
            Cell? position = null;
            for (int y = 2; y < session.Balance.Height - 2 && !position.HasValue; y++)
            for (int x = 2; x < session.Balance.Width - 2 && !position.HasValue; x++)
            {
                var candidate = new Cell(x, y);
                if (session.QuoteBuild(new[] { candidate }, StructureKind.Wall, MaterialTier.Pencil).Valid) position = candidate;
            }
            Assert.That(position.HasValue, Is.True);
            // Seed a historical support in the real session. Modern album controller commands
            // deliberately reject direct tools; this regression exercises inspecting an existing one.
            Assert.That(session.TryBuild(new[] { position.Value }, StructureKind.Wall, MaterialTier.Pencil, out var reason), Is.True, reason);
            for (int step = 0; step < 160; step++) game.Run.StepSimulation();
            var wall = game.Run.Session.StructureAt(position.Value).Value;
            Assert.That(wall.IsComplete, Is.True);
            Assert.That(wall.SourceAlbumEntryId, Is.Null);
            Assert.That(wall.ResolvedEntry, Is.Null);
            int price = TowerCatalog.ToolCost(session.Balance, StructureKind.Wall, MaterialTier.Ink)
                - TowerCatalog.ToolCost(session.Balance, StructureKind.Wall, MaterialTier.Pencil);
            Assert.That(price, Is.GreaterThan(0));
            Inspect(wall, 800, 600); yield return null;
            var fortify = Find("Fortify support");
            Assert.That(fortify.IsInteractable(), Is.True);
            Assert.That(fortify.GetComponentInChildren<Text>().text, Does.Contain(price + " штр."));
            int ink = game.Run.Session.Ink;
            fortify.onClick.Invoke();
            var upgraded = game.Run.Session.StructureAt(wall.Cell).Value;
            Assert.That(game.Run.Session.Ink, Is.EqualTo(ink - price));
            Assert.That(upgraded.Id, Is.EqualTo(wall.Id));
            Assert.That(upgraded.Tier, Is.EqualTo(MaterialTier.Ink));
            Assert.That(upgraded.MaxHealth, Is.GreaterThan(wall.MaxHealth));
            yield return null;
        }

        [UnityTest] public IEnumerator EmptySelectedSlotStillAllowsPointerInspectionAndPaidUpgrade()
        {
            using (var nativeInput = new NativeInputIsolation())
            {
                var originalSettings = InputSystem.settings;
                if (originalSettings == null)
                {
                    originalSettings = ScriptableObject.CreateInstance<InputSettings>();
                    originalSettings.hideFlags = HideFlags.HideAndDontSave;
                    InputSystem.settings = originalSettings;
                }
                var fixtureSettings = Object.Instantiate(originalSettings);
                fixtureSettings.hideFlags = HideFlags.HideAndDontSave;
                fixtureSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                fixtureSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                var originalFlags = originalSettings != null ? originalSettings.hideFlags : HideFlags.None;
                Mouse mouse = null;
                try
                {
                    if (originalSettings != null && originalFlags == HideFlags.HideAndDontSave) originalSettings.hideFlags = HideFlags.None;
                    InputSystem.settings = fixtureSettings;
                    if (originalSettings != null) originalSettings.hideFlags = originalFlags;
                    mouse = InputSystem.AddDevice<Mouse>("FieldModernizationInspectionMouse"); mouse.MakeCurrent();
                    StartWithEmptyLastSlot(); yield return null;
                    var tower = BuildTower();
                    game.Drawing.SelectSlot(ProfileLimits.LoadoutSlotCount - 1);
                    Assert.That(game.Drawing.BlueprintSlot, Is.EqualTo(ProfileLimits.LoadoutSlotCount - 1));
                    game.Drawing.SelectMode(DrawingMode.Erase);
                    AssertUpgradeSelectionDoesNotChangeTheField(tower, () => Find("Battle upgrades").onClick.Invoke());
                    game.Drawing.Focus(tower.Cell);
                    yield return null; yield return null;
                    var camera = root.GetComponentInChildren<PaperDefenseBootstrap>().worldCamera;
                    Vector2 screen = camera.WorldToScreenPoint(new Vector3(tower.Cell.X + .5f, tower.Cell.Y + .5f, 0));
                    Assert.That(camera.pixelRect.Contains(screen), Is.True);
                    int ink = game.Run.Session.Ink;
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left));
                    yield return null; yield return null;
                    Assert.That(mouse.leftButton.isPressed, Is.True, "The real input system must deliver the fixture press.");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                    yield return null; yield return null;
                    Assert.That(game.Drawing.InspectedCell, Is.EqualTo(tower.Cell), "An empty construction slot must not suppress selecting an existing tower.");
                    Assert.That(game.Run.Session.Ink, Is.EqualTo(ink), "Inspection must not build, erase or pay for anything.");
                    var shell = root.GetComponentInChildren<PlayerShell>();
                    ((PlayerBattleHud)typeof(PlayerShell).GetField("battle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell)).Refresh();
                    Canvas.ForceUpdateCanvases();
                    var quote = game.Run.QuoteModernization(tower.Id, ModernizationDirection.Power);
                    Assert.That(quote.Valid, Is.True, quote.Reason);
                    var buy = Find("Modernize Power");
                    Assert.That(buy.IsInteractable(), Is.True);
                    buy.onClick.Invoke();
                    Assert.That(game.Run.Session.Ink, Is.EqualTo(ink - quote.Cost));
                    Assert.That(game.Run.Session.StructureAt(tower.Cell).Value.Modernization.IsWorking, Is.True);
                }
                finally
                {
                    if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                    if (originalSettings != null)
                    {
                        originalSettings.hideFlags = HideFlags.None;
                        InputSystem.settings = originalSettings;
                        originalSettings.hideFlags = originalFlags;
                    }
                    Object.Destroy(fixtureSettings);
                }
            }
        }

        [UnityTest] public IEnumerator VisibleUpgradeActionSafelyLeavesEraseAndBlotterWithoutSpending()
        {
            var tower = BuildTower();
            Inspect(tower, 800, 600);
            foreach (var mode in new[] { DrawingMode.Erase, DrawingMode.Blotter })
            {
                Cell? selection = mode == DrawingMode.Blotter ? tower.Cell : (Cell?)null;
                game.Drawing.Inspect(selection);
                game.Drawing.SelectMode(mode);
                Assert.That(game.Drawing.Mode, Is.EqualTo(mode));
                var action = Find("Battle upgrades");
                Assert.That(action.IsInteractable(), Is.True);
                Assert.That(action.GetComponentsInChildren<Text>().Any(text => text.text.Contains("Улучшить") && text.text.Contains("U")), Is.True);
                AssertUpgradeSelectionDoesNotChangeTheField(tower, () => action.onClick.Invoke());
                Assert.That(game.Drawing.InspectedCell, Is.EqualTo(selection), "Opening upgrades must preserve the current selection.");
                yield return null;
                if (selection.HasValue) Assert.That(Find("Modernize Power").gameObject.activeInHierarchy, Is.True);
            }
        }

        [UnityTest] public IEnumerator UpgradeAndPaintModeRequestsSafelyReturnToBuildingWithoutSpending()
        {
            var tower = BuildTower();
            foreach (var mode in new[] { DrawingMode.Erase, DrawingMode.Blotter })
            foreach (var requested in new[] { DrawingMode.Upgrade, DrawingMode.Paint })
            {
                game.Drawing.SelectMode(mode);
                Assert.That(game.Drawing.Mode, Is.EqualTo(mode));
                int changes = 0;
                System.Action changed = () => changes++;
                game.Drawing.SelectionChanged += changed;
                try { AssertUpgradeSelectionDoesNotChangeTheField(tower, () => game.Drawing.SelectMode(requested)); }
                finally { game.Drawing.SelectionChanged -= changed; }
                Assert.That(changes, Is.EqualTo(1), "The U/C route must refresh the selected tool presentation.");
                yield return null;
            }
        }

        [UnityTest] public IEnumerator TacticalPauseKeepsBattleCommandsAndMenuRestoresIt()
        {
            game.Run.TogglePause();
            yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle), "Tactical pause keeps the field visible.");
            Assert.That(game.CanBuild, Is.True, "Space pause must allow field commands.");
            long tick = game.Run.Session.TickIndex;
            Assert.That(game.Run.AdvanceFrame(1, game.Run.Paused, 2), Is.Zero);
            game.Pause(); yield return null;
            Assert.That(game.CanBuild, Is.False);
            game.Resume(); yield return null;
            Assert.That(game.Page, Is.EqualTo(PlayerPage.Battle));
            Assert.That(game.Run.Paused, Is.True, "Leaving menu must preserve tactical pause.");
            Assert.That(game.CanBuild, Is.True);
            Assert.That(game.Run.Session.TickIndex, Is.EqualTo(tick));
        }

        [UnityTest] public IEnumerator ConfirmationStopsTimeAndControllerCommandsAndPreservesTacticalPause()
        {
            game.Run.TogglePause();
            var shell = root.GetComponentInChildren<PlayerShell>();
            shell.Confirm("Выбор цвета", "Цвет закрепится за этой башней.", () => { });
            yield return null;
            AssertFieldConfirmation();
            Assert.That(game.CanBuild, Is.False);
            int ink = game.Run.Session.Ink;
            Assert.That(game.Run.BuildEntry(new[] { new Cell(12, 12) }, 0, MaterialTier.Pencil, InkColor.None, out _), Is.False,
                "A modal also suspends controller commands reached through stale UI callbacks.");
            Assert.That(game.Run.Session.Ink, Is.EqualTo(ink));
            shell.CancelConfirmation(); yield return null;
            Assert.That(game.Run.Paused, Is.True);
            Assert.That(game.CanBuild, Is.True);
        }

        [UnityTest] public IEnumerator CompactInspectorHasReachableDirectionsAtEveryRequiredResolution()
        {
            var tower = BuildTower();
            var shell = root.GetComponentInChildren<PlayerShell>();
            bool shellWasEnabled = shell.enabled;
            shell.enabled = false;
            try
            {
                foreach (var size in new[] { new Vector2Int(800, 600), new Vector2Int(1280, 720), new Vector2Int(1600, 900) })
                {
                    Inspect(tower, size.x, size.y);
                    Assert.That(shell.Layout.Dock.height, Is.LessThanOrEqualTo(124));
                    var upgradeAction = Find("Battle upgrades");
                    Assert.That(upgradeAction.IsInteractable(), Is.True);
                    Canvas.ForceUpdateCanvases(); yield return null;
                    foreach (var caption in upgradeAction.GetComponentsInChildren<Text>())
                        AssertVisibleUpgradeText(caption, "Visible upgrade invitation at " + size);
                    var powerRow = (RectTransform)Find("Modernize Power").transform.parent;
                    Assert.That(powerRow.rect.height, Is.LessThan(80), "A valid single-stat row must not reserve a redundant reason band.");
                    Assert.That(powerRow.GetComponentsInChildren<Text>().Any(t => t.text == "Купить следующий ранг"), Is.False);
                    Assert.That(root.GetComponentsInChildren<Text>().Single(t => t.name == "Modernization limits").text,
                        Does.Contain("0/6").And.Contain("Тех. I"));
                    Assert.That(root.GetComponentsInChildren<Text>().Single(t => t.name == "Modernization limits").GetComponent<PlayerTooltip>().Message,
                        Does.Contain("улучшения 0/6").And.Contain("технология I").And.Contain("Только одно обычное направление"));
                    var keys = new[] { "Power", "Mechanics", "Range", "Resilience", "color Red", "color Blue", "color Green" };
                    var names = new[] { "Мощность", "Механика", "Дальность", "Стойкость", "Красный", "Синий", "Зелёный" };
                    for (int index = 0; index < keys.Length; index++)
                    {
                        string direction = keys[index];
                        var button = Find("Modernize " + direction);
                        var row = button.transform.parent;
                        var focus = button.IsInteractable() ? button : row.GetComponent<Button>();
                        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(focus.gameObject);
                        Canvas.ForceUpdateCanvases(); yield return null;
                        Canvas.ForceUpdateCanvases();
                        var title = row.GetComponentsInChildren<Text>().Single(text => text.text.StartsWith(names[index] + "\n"));
                        Assert.That(title.text, Does.Contain("0 → I"), direction + " needs both a visible name and next rank.");
                        AssertVisibleUpgradeText(title, direction + " title at " + size);
                        var price = button.GetComponentsInChildren<Text>().Single(text => text.text.Contains(index < 4 ? "штр." : "черн."));
                        var quote = index < 4 ? game.Run.QuoteModernization(tower.Id, (ModernizationDirection)index)
                            : game.Run.QuoteColorModernization(tower.Id, (InkColor)(index - 3));
                        Assert.That(price.text, Does.Contain(quote.Cost.ToString()).And.Contain("с"), direction + " needs its real cost, unit and work time.");
                        AssertVisibleUpgradeText(price, direction + " price at " + size);
                        var viewport = button.GetComponentInParent<ScrollRect>().viewport;
                        var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
                        foreach (var corner in corners)
                        {
                            var p = viewport.InverseTransformPoint(corner);
                            Assert.That(p.x, Is.InRange(viewport.rect.xMin - 1, viewport.rect.xMax + 1), direction);
                            Assert.That(p.y, Is.InRange(viewport.rect.yMin - 1, viewport.rect.yMax + 1), direction);
                        }
                        foreach (var copy in row.GetComponentsInChildren<Text>())
                            Assert.That(copy.preferredHeight, Is.LessThanOrEqualTo(copy.rectTransform.rect.height + 1),
                                direction + " preview text must fit at " + size + ": " + copy.text);
                    }
                    Assert.That(root.GetComponentsInChildren<Button>().Any(b => b.name == "Album battle paint" || b.name == "Material 1"), Is.False);
                    Assert.That(Find("Modernize color Blue").transform.parent.GetComponentsInChildren<Text>()
                        .Any(t => t.text.Contains("Скорость ×1 →") && t.text.Contains("целей/с 0 → 3")), Is.True,
                        "An uncolored tower starts without slow or colored targets; blue preview must not claim zero enemy speed.");
                }
                yield return null;
            }
            finally { shell.enabled = shellWasEnabled; }
        }

        [UnityTest] public IEnumerator OrdinaryPurchasePaysWhilePausedChangesOnlySelectedCopyAndFinishesAfterResume()
        {
            var first = BuildTower(); var second = BuildTower();
            Inspect(first, 800, 600); game.Run.TogglePause();
            int ink = game.Run.Session.Ink;
            Find("Modernize Power").onClick.Invoke();
            var working = game.Run.Session.StructureAt(first.Cell).Value;
            Assert.That(working.Modernization.IsWorking, Is.True);
            Assert.That(game.Run.Session.Ink, Is.LessThan(ink));
            Assert.That(game.Run.Session.StructureAt(second.Cell).Value.Modernization.StepsUsed, Is.Zero);
            float remaining = working.Modernization.WorkRemaining;
            game.Run.AdvanceFrame(1, false, 2);
            Assert.That(game.Run.Session.StructureAt(first.Cell).Value.Modernization.WorkRemaining, Is.EqualTo(remaining));
            game.Run.TogglePause();
            for (int step = 0; step < 140; step++) game.Run.AdvanceFrame(FixedStepClock.Step, false, 1);
            Assert.That(game.Run.Session.StructureAt(first.Cell).Value.Modernization.Rank(ModernizationDirection.Power), Is.EqualTo(1));
            Assert.That(game.Run.Session.StructureAt(second.Cell).Value.Modernization.StepsUsed, Is.Zero);
            yield return null;
        }

        [UnityTest] public IEnumerator FirstColorRequiresConfirmationCapturesIdentityAndCancelNeverPays()
        {
            var tower = BuildTower(); var other = BuildTower();
            // Isolated interaction fixture only; campaign balance evidence never uses injected resources.
            var session = (GameSession)typeof(RunController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.Run);
            var inks = (int[])typeof(GameSession).GetField("coloredInk", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
            inks[(int)InkColor.Red] = 100;
            Inspect(tower, 800, 600); game.Run.TogglePause();
            Find("Modernize color Red").onClick.Invoke();
            Assert.That(root.GetComponentInChildren<PlayerShell>().HasConfirmation, Is.True);
            AssertFieldConfirmation();
            Assert.That(game.Run.Session.StructureAt(tower.Cell).Value.Color, Is.EqualTo(InkColor.None));
            Find("Cancel confirmation").onClick.Invoke();
            Assert.That(inks[(int)InkColor.Red], Is.EqualTo(100)); Assert.That(game.Run.Paused, Is.True);
            Find("Modernize color Red").onClick.Invoke();
            game.Drawing.Inspect(other.Cell);
            Find("Confirm action").onClick.Invoke();
            var accepted = game.Run.Session.StructureAt(tower.Cell).Value;
            Assert.That(accepted.Color, Is.EqualTo(InkColor.Red)); Assert.That(accepted.Modernization.IsWorking, Is.True);
            Assert.That(game.Run.Session.StructureAt(other.Cell).Value.Color, Is.EqualTo(InkColor.None),
                "Changing selection during confirmation must not redirect its captured quote.");
            Assert.That(inks[(int)InkColor.Red], Is.LessThan(100)); Assert.That(game.Run.Paused, Is.True);
            Inspect(accepted, 800, 600);
            Assert.That(root.GetComponentsInChildren<Button>().Any(b => b.name == "Modernize color Blue" || b.name == "Modernize color Green"), Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator PreparationAndHelpDescribeBaseConstructionAndPaidFieldDevelopment()
        {
            game.ReturnToMap(); game.Navigate(PlayerPage.Preparation); yield return null;
            Assert.That(root.GetComponentsInChildren<Button>().Any(b => b.name.StartsWith("Starting material ")), Is.False);
            Assert.That(root.GetComponentsInChildren<Text>().Single(t => t.name == "Preparation material description").text,
                Does.Contain("Базовое строительство"));
            game.OpenSettings(true); yield return null;
            string controls = root.GetComponentsInChildren<Text>().Single(t => t.name == "Help controls text").text;
            Assert.That(controls, Does.Contain("тактическая пауза"));
            Assert.That(controls, Does.Contain("Меню и подтверждения блокируют приказы"));
            Assert.That(controls, Does.Not.Contain("Q —"));
        }
    }
}
#endif
