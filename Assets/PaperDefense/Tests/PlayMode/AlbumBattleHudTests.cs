#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class AlbumBattleHudTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject owner;
        PlayerGameRoot game;
        PlayerShell shell;
        GameBalanceAsset balance;
        PlayerBattleHud Hud => (PlayerBattleHud)typeof(PlayerShell).GetField("battle", Private).GetValue(shell);
        GameSession Session => (GameSession)typeof(RunController).GetField("session", Private).GetValue(game.Run);
        Button Button(string name) => owner.GetComponentsInChildren<Button>().Single(value => value.name == name);
        Text Copy(string name) => owner.GetComponentsInChildren<Text>().Single(value => value.name == name);
        RectTransform Rect(string name) => owner.GetComponentsInChildren<RectTransform>().Single(value => value.name == name);

        [UnitySetUp] public IEnumerator SetUp()
        {
            foreach (var previous in Object.FindObjectsByType<PlayerGameRoot>()) Object.Destroy(previous.gameObject);
            foreach (var previous in Object.FindObjectsByType<PaperDefenseBootstrap>()) Object.Destroy(previous.gameObject);
            yield return null;
            balance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameBalanceAsset>("Assets/PaperDefense/Configuration/Game/PlayerBalance.asset"));
            balance.planningSeconds = 1000;
            owner = new GameObject("Isolated album battle HUD"); owner.SetActive(false);
            var camera = new GameObject("Album HUD camera", typeof(Camera)); camera.transform.SetParent(owner.transform); camera.tag = "MainCamera";
            game = owner.AddComponent<PlayerGameRoot>(); game.Configure(balance, true);
            owner.SetActive(true); yield return null;
            game.Run.AutomaticSimulation = false;
            shell = owner.GetComponentInChildren<PlayerShell>(); shell.enabled = false;
        }

        [UnityTearDown] public IEnumerator TearDown()
        { Object.Destroy(owner); Object.Destroy(balance); yield return null; }

        void StartBattle()
        { Assert.That(game.StartBattle(), Is.True); Canvas.ForceUpdateCanvases(); Hud.Refresh(); }

        Cell Build(int slot)
        {
            for (int y = 2; y < game.Run.Session.Balance.Height - 2; y++)
            for (int x = 2; x < game.Run.Session.Balance.Width - 2; x++)
            {
                var cell = new Cell(x, y);
                if (!game.Run.QuoteEntry(new[] { cell }, slot, MaterialTier.Pencil, InkColor.None).Valid) continue;
                Assert.That(game.Run.BuildEntry(new[] { cell }, slot, MaterialTier.Pencil, InkColor.None, out var reason), Is.True, reason);
                return cell;
            }
            Assert.Fail("An affordable free cell is required for slot " + slot); return default;
        }

        [UnityTest] public IEnumerator EightCardsRemainVisibleWithoutHorizontalScrollAtEveryRequiredSize()
        {
            StartBattle();
            foreach (var size in new[] { new Vector2Int(1024, 768), new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
            {
                shell.ApplyLayout(size.x, size.y); Canvas.ForceUpdateCanvases(); Hud.Refresh();
                var dock = Rect("Construction dock");
                var cards = owner.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Build tool ", StringComparison.Ordinal)).ToArray();
                Assert.That(cards.Length, Is.EqualTo(8), size.ToString());
                Assert.That(dock.GetComponentsInChildren<ScrollRect>().Any(scroll => scroll.horizontal), Is.False);
                var occupied = new List<UnityEngine.Rect>();
                for (int slot = 0; slot < 8; slot++)
                {
                    var rect = (RectTransform)Button("Build tool " + slot).transform;
                    var bounds = Bounds(dock, rect);
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(dock.rect.xMin - 1));
                    Assert.That(bounds.xMax, Is.LessThanOrEqualTo(dock.rect.xMax + 1));
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(dock.rect.yMin - 1));
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(dock.rect.yMax + 1));
                    foreach (var previous in occupied) Assert.That(bounds.Overlaps(previous), Is.False);
                    occupied.Add(bounds);
                }
                foreach (string action in new[] { "Album battle erase", "Material 0", "Material 1", "Material 2", "Ink 0", "Ink 1", "Ink 2", "Ink 3" })
                    Assert.That(Button(action).gameObject.activeInHierarchy, Is.True);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator EveryCardUsesTheFrozenNameAndSelectsItsOwnSlot()
        {
            StartBattle();
            for (int slot = 0; slot < 8; slot++)
            {
                Assert.That(Copy("Album battle name " + slot).text, Is.EqualTo(GameText.Resolve(game.Run.RunLoadout.Entries[slot].Name)));
                Button("Build tool " + slot).onClick.Invoke(); Hud.Refresh();
                Assert.That(game.Drawing.BlueprintSlot, Is.EqualTo(slot));
                Assert.That(game.Drawing.Mode, Is.EqualTo(DrawingMode.Build));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator EmptyPositionsStayDisabledAndDoNotShiftTheFollowingCards()
        {
            Assert.That(game.Run.AssemblyCommands.AssignSlot(2, null, Guid.NewGuid()).Success, Is.True);
            string following = game.Run.Workshop.LoadoutEntries[3].Id; StartBattle();
            Assert.That(Button("Build tool 2").interactable, Is.False);
            Assert.That(Copy("Album battle price 2").text, Is.EqualTo("—"));
            Button("Build tool 3").onClick.Invoke(); Hud.Refresh();
            Assert.That(game.Drawing.BlueprintSlot, Is.EqualTo(3));
            Assert.That(game.Run.RunLoadout.Entries[3].Id, Is.EqualTo(following));
            yield return null;
        }

        [UnityTest] public IEnumerator MaterialPricesComeFromEntryQuotesAndGeneratorSelectionUsesPencilOnly()
        {
            StartBattle();
            foreach (var tier in new[] { MaterialTier.Pencil, MaterialTier.Ink, MaterialTier.Marker })
            {
                Button("Material " + (int)tier).onClick.Invoke(); Hud.Refresh();
                for (int slot = 0; slot < 8; slot++)
                {
                    bool generator = game.Run.RunLoadout.Resolved[slot].Support?.Kind == AssemblyStructureFamily.Generator;
                    if (generator)
                    {
                        Assert.That(Button("Build tool " + slot).interactable, Is.True);
                        Assert.That(Copy("Album battle price " + slot).text,
                            Does.Contain(game.Run.RunLoadout.ResolveSlot(slot, MaterialTier.Pencil).Cost.ToString()));
                        Assert.That(Copy("Album battle price " + slot).text, Does.Contain("карандаш").IgnoreCase);
                        continue;
                    }
                    var expected = game.Run.RunLoadout.ResolveSlot(slot, tier);
                    Assert.That(Copy("Album battle price " + slot).text, Does.Contain(expected.Cost.ToString()));
                }
            }
            int generatorSlot = Enumerable.Range(0, 8).Single(index => game.Run.RunLoadout.Resolved[index].Support?.Kind == AssemblyStructureFamily.Generator);
            Button("Build tool " + generatorSlot).onClick.Invoke(); Hud.Refresh();
            Assert.That(game.Drawing.BlueprintSlot, Is.EqualTo(generatorSlot));
            Assert.That(game.Drawing.Tier, Is.EqualTo(MaterialTier.Pencil));
            yield return null;
        }

        [UnityTest] public IEnumerator WeaponInspectorUsesResolvedMagazineAndTheLiveMountCycle()
        {
            StartBattle(); var cell = Build(0);
            var entity = ((List<StructureState>)typeof(GameSession).GetField("structures", Private).GetValue(Session)).Single(value => value.Cell == cell);
            entity.IsComplete = true; entity.BuildRemaining = 0;
            var cycle = (WeaponCycleState)Activator.CreateInstance(typeof(WeaponCycleState), BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { 2, true, .5d }, null);
            typeof(WeaponMountState).GetProperty("Cycle").SetValue(entity.GetMountState(0), cycle);
            game.Drawing.Inspect(cell); Hud.Refresh();
            var weapon = game.Run.Session.StructureAt(cell).Value.ResolvedEntry.Weapons[0];
            Assert.That(Copy("Album battle structure details").text, Does.Contain("2 / " + weapon.MagazineSize));
            Assert.That(Copy("Album battle structure details").text, Does.Contain("Смена обоймы"));
            yield return null;
        }

        [UnityTest] public IEnumerator GeneratorInspectorShowsIncomeFullWaveRulePencilAndZeroRefund()
        {
            StartBattle(); int slot = Enumerable.Range(0, 8).Single(index => game.Run.RunLoadout.Resolved[index].Support?.Kind == AssemblyStructureFamily.Generator);
            var cell = Build(slot); game.Drawing.Inspect(cell); Hud.Refresh();
            var support = game.Run.Session.StructureAt(cell).Value.ResolvedEntry.Support;
            string text = Copy("Album battle structure details").text;
            Assert.That(text, Does.Contain(support.IncomePerWave.ToString()));
            Assert.That(text, Does.Contain("полн").IgnoreCase);
            Assert.That(text, Does.Contain("карандаш").IgnoreCase);
            Assert.That(text, Does.Contain("Возврат: 0"));
            yield return null;
        }

        [UnityTest] public IEnumerator RepairInspectorExplainsOneTargetAndResolvedHealthPerSecond()
        {
            StartBattle(); int slot = Enumerable.Range(0, 8).Single(index => game.Run.RunLoadout.Resolved[index].Support?.Kind == AssemblyStructureFamily.Repair);
            var cell = Build(slot); game.Drawing.Inspect(cell); Hud.Refresh();
            float amount = game.Run.Session.StructureAt(cell).Value.ResolvedEntry.Support.RepairPerSecond;
            string text = Copy("Album battle structure details").text;
            Assert.That(text, Does.Contain("одна цель").IgnoreCase);
            Assert.That(text, Does.Contain(amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)));
            Assert.That(text, Does.Contain("HP/с"));
            yield return null;
        }

        [UnityTest] public IEnumerator IdenticalRecipesKeepTheirOwnFrozenNameAndPreviewAfterMaterialUpgrade()
        {
            var source = game.Run.Workshop.LoadoutEntries[0];
            var first = new AlbumEntry("draft.hud.first", "Северный дозор", 1, source.Snapshot);
            var second = new AlbumEntry("draft.hud.second", "Южный дозор", 1, source.Snapshot);
            var firstSave = game.Run.AssemblyCommands.SaveEntryAsVariant(first, Guid.NewGuid());
            var secondSave = game.Run.AssemblyCommands.SaveEntryAsVariant(second, Guid.NewGuid());
            Assert.That(firstSave.Success, Is.True, firstSave.Code);
            Assert.That(secondSave.Success, Is.True, secondSave.Code);
            Assert.That(game.Run.AssemblyCommands.AssignSlot(0, firstSave.ResultId, Guid.NewGuid()).Success, Is.True);
            Assert.That(game.Run.AssemblyCommands.AssignSlot(1, secondSave.ResultId, Guid.NewGuid()).Success, Is.True);
            StartBattle(); var cells = new[] { Build(0), Build(1) };
            var entities = (List<StructureState>)typeof(GameSession).GetField("structures", Private).GetValue(Session);
            foreach (var entity in entities) { entity.IsComplete = true; entity.BuildRemaining = 0; }
            for (int slot = 0; slot < cells.Length; slot++)
            {
                foreach (bool upgrade in new[] { false, true })
                {
                    if (upgrade) Assert.That(game.Run.Upgrade(cells[slot], out var reason), Is.True, reason);
                    game.Drawing.Inspect(cells[slot]); Hud.Refresh(); Canvas.ForceUpdateCanvases(); yield return null;
                    Assert.That(Copy("Album battle structure name").text, Is.EqualTo(game.Run.RunLoadout.Entries[slot].Name),
                        "The selected page identity must survive recipe resolution and material upgrades.");
                    var preview = Rect("Album battle inspected preview").GetComponentInChildren<WeaponAssemblyGraphic>();
                    Assert.That(preview, Is.Not.Null, "The selected frozen page must supply its certified preview.");
                    var mesh = preview.GetComponent<CanvasRenderer>().GetMesh();
                    Assert.That(mesh, Is.Not.Null); Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                }
            }
        }

        [UnityTest] public IEnumerator CardNamesAndGeneratorPencilPriceHaveRoomForTheirRenderedLinesAtEveryRequiredSize()
        {
            string longName = new string('Ш', 48);
            var longPage = new AlbumEntry("draft.hud.long", longName, 1, game.Run.Workshop.LoadoutEntries[0].Snapshot);
            var saved = game.Run.AssemblyCommands.SaveEntryAsVariant(longPage, Guid.NewGuid());
            Assert.That(saved.Success, Is.True, saved.Code);
            Assert.That(game.Run.AssemblyCommands.AssignSlot(0, saved.ResultId, Guid.NewGuid()).Success, Is.True);
            StartBattle();
            foreach (var size in new[] { new Vector2Int(1024, 768), new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
            {
                shell.ApplyLayout(size.x, size.y); Hud.Refresh(); Canvas.ForceUpdateCanvases();
                for (int slot = 0; slot < 8; slot++)
                {
                    var name = Copy("Album battle name " + slot); var price = Copy("Album battle price " + slot);
                    Assert.That(name.preferredHeight, Is.LessThanOrEqualTo(name.rectTransform.rect.height + 1),
                        "Name " + slot + " at " + size + ": " + name.text);
                    Assert.That(price.preferredHeight, Is.LessThanOrEqualTo(price.rectTransform.rect.height + 1),
                        "Price " + slot + " at " + size + ": " + price.text);
                    Assert.That(name.cachedTextGenerator.lineCount, Is.GreaterThan(0));
                    Assert.That(price.cachedTextGenerator.lineCount, Is.GreaterThan(0));
                    Assert.That(Bounds((RectTransform)Button("Build tool " + slot).transform, name.rectTransform)
                        .Overlaps(Bounds((RectTransform)Button("Build tool " + slot).transform, price.rectTransform)), Is.False);
                }
                int generator = Enumerable.Range(0, 8).Single(slot => game.Run.RunLoadout.Resolved[slot].Support?.Kind == AssemblyStructureFamily.Generator);
                Assert.That(Copy("Album battle price " + generator).cachedTextGenerator.lineCount, Is.EqualTo(2));
                Assert.That(Button("Build tool 0").GetComponent<PlayerTooltip>().Message, Does.Contain(longName),
                    "The full frozen name must remain available to keyboard and pointer users.");
                for (int tier = 0; tier < 3; tier++)
                {
                    var label = Button("Material " + tier).GetComponentInChildren<Text>();
                    Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + 1),
                        "Material " + tier + " must remain a complete single-line caption at " + size);
                    Assert.That(label.cachedTextGenerator.lineCount, Is.EqualTo(1));
                }
            }
            yield return null;
        }

        static UnityEngine.Rect Bounds(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            var minimum = (Vector2)parent.InverseTransformPoint(corners[0]);
            var maximum = (Vector2)parent.InverseTransformPoint(corners[2]);
            return new UnityEngine.Rect(minimum, maximum - minimum);
        }
    }
}
#endif
