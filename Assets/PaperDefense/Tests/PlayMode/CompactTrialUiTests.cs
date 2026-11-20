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
    public sealed class CompactTrialUiTests
    {
        AssemblyCommandFixture fixture;
        GameObject canvas, events;
        CompositeWorkshopPanel panel;

        [SetUp] public void SetUp()
        {
            fixture = new AssemblyCommandFixture(94, 390, data => {
                var generator = data.Entries.Select(value => value.CreateSnapshot()).Single(value => value.Id == "album.generator");
                data.Entries = data.Entries.Concat(new[] { new AlbumEntryData(new AlbumEntry(
                    "trial.generator.variant", "Запасной генератор", 1, generator.Snapshot)) }).ToArray();
            });
            canvas = new GameObject("Compact trial canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            events = new GameObject("Compact trial events", typeof(EventSystem));
            panel = new CompositeWorkshopPanel(canvas.transform, fixture.Run, null, null, sequentialFlow: true);
            panel.Reflow(800, 600);
        }
        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(canvas); Object.DestroyImmediate(events); fixture.Dispose();
        }
        T Find<T>(string name) where T : Component => canvas.GetComponentsInChildren<T>().Single(value => value.name == name);
        void Click(string name)
        {
            var button = Find<Button>(name); Assert.That(button.IsInteractable(), Is.True, name);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Canvas.ForceUpdateCanvases();
        }
        void Open(AlbumEntry entry) { panel.OpenEntry(entry); Click("Composite trial"); }
        AlbumEntry Entry(string id) => fixture.Run.Workshop.Entries.Single(value => value.Id == id);
        static string Number(float value) => value.ToString("0.##", TextResourcesAsset.Default.Culture);
        static string Delta(float value) => value.ToString("+0.##;−0.##;0", TextResourcesAsset.Default.Culture);
        static void AssertInside(RectTransform container, RectTransform child)
        {
            var bounds = BattleHudThemeEvidence.Bounds(container, child);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(container.rect.xMin - 1), child.name);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(container.rect.yMin - 1), child.name);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(container.rect.xMax + 1), child.name);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(container.rect.yMax + 1), child.name);
        }

        Text Cell(string metric, string column, bool quote = false)
        {
            string prefix = "Composite trial " + (quote ? "quote" : "measured");
            var row = Find<RectTransform>(prefix + " metric " + metric);
            string start = prefix + (column == "A" ? " row " : column == "B" ? " comparison row " : " delta row ");
            return row.GetComponentsInChildren<Text>().Single(value => value.name.StartsWith(start) && value.name.EndsWith(" value"));
        }
        void AssertMetric(string metric, float? first, float? second, bool quote = false)
        {
            Assert.That(Cell(metric, "A", quote).text, Is.EqualTo(first.HasValue ? Number(first.Value) : "—"), metric + " A");
            Assert.That(Cell(metric, "B", quote).text, Is.EqualTo(second.HasValue ? Number(second.Value) : "—"), metric + " B");
            Assert.That(Cell(metric, "Delta", quote).text,
                Is.EqualTo(first.HasValue && second.HasValue ? Delta(second.Value - first.Value) : "—"), metric + " B − A");
        }
        void AssertSameConditions()
        {
            var first = panel.LastAssemblyTrial; var second = panel.LastAssemblyTrialComparison;
            Assert.That(second, Is.Not.Null); Assert.That(first.EntryId, Is.Not.EqualTo(second.EntryId));
            Assert.That(second.Scenario, Is.EqualTo(first.Scenario)); Assert.That(second.Budget, Is.EqualTo(first.Budget));
            Assert.That(second.Seed, Is.EqualTo(first.Seed));
            Assert.That(first.Cost, Is.LessThanOrEqualTo(first.Budget)); Assert.That(second.Cost, Is.LessThanOrEqualTo(second.Budget));
        }

        [Test] public void RealComparisonKeepsColumnIdentitiesAndKeyboardActionsFixedWhileItsReportScrolls()
        {
            string state = fixture.State(); byte[] saved = fixture.SavedBytes;
            Open(fixture.Gun()); Click("Composite trial run"); AssertSameConditions();
            var timedFirst = panel.LastAssemblyTrial; var timedSecond = panel.LastAssemblyTrialComparison;
            Assert.That(timedFirst.Scenario, Is.EqualTo(AssemblyTrialScenario.Single));
            Assert.That(Mathf.Abs(timedSecond.DurationSeconds - timedFirst.DurationSeconds), Is.GreaterThan(.0001f),
                "The real Single reports must exercise a nonzero duration difference.");
            AssertMetric("DurationSeconds", timedFirst.DurationSeconds, timedSecond.DurationSeconds);
            Assert.That(Cell("DurationSeconds", "Delta").color, Is.EqualTo(FieldMenuSkin.Muted),
                "Even a nonzero duration difference does not identify a better assembly.");
            Click("Composite trial scenario Blots"); Click("Composite trial run");
            AssertSameConditions();
            var first = panel.LastAssemblyTrial; var second = panel.LastAssemblyTrialComparison;
            Assert.That(second.UnitCost, Is.Not.EqualTo(first.UnitCost));
            Assert.That(second.Copies, Is.Not.EqualTo(first.Copies));
            Assert.That(second.Cost, Is.Not.EqualTo(first.Cost), "The quote fixture must exercise signed differences rather than only zero.");
            AssertMetric("UnitCost", first.UnitCost, second.UnitCost, quote: true);
            AssertMetric("Copies", first.Copies, second.Copies, quote: true);
            AssertMetric("Cost", first.Cost, second.Cost, quote: true);
            AssertMetric("Damage", first.Damage, second.Damage);
            AssertMetric("Leaks", first.Leaks, second.Leaks);
            AssertMetric("UnresolvedEnemies", first.UnresolvedEnemies, second.UnresolvedEnemies);
            AssertMetric("BlotsCleared", first.BlotsCleared, second.BlotsCleared);
            Assert.That(first.BlotsCleared, Is.EqualTo(0)); Assert.That(Cell("BlotsCleared", "A").text, Is.EqualTo("0"));
            AssertMetric("UnresolvedBlots", first.UnresolvedBlots, second.UnresolvedBlots);
            AssertMetric("DurationSeconds", first.DurationSeconds, second.DurationSeconds);
            Assert.That(Cell("DurationSeconds", "Delta").color, Is.EqualTo(FieldMenuSkin.Muted), "Duration alone is not an improvement score.");
            Assert.That(Find<Text>("Composite trial delta legend").text, Is.EqualTo("Δ = B − A"));
            var headers = new[] { Find<Text>("Composite trial entry name"), Find<Text>("Composite trial comparison entry name") };
            string firstName = GameText.Resolve(TextResourcesAsset.Default, first.EntryName);
            string secondName = GameText.Resolve(TextResourcesAsset.Default, second.EntryName);
            Assert.That(headers[0].text, Is.EqualTo("A · " + firstName));
            Assert.That(headers[1].text, Is.EqualTo("B · " + secondName));
            Assert.That(headers[0].GetComponent<WorkshopTooltip>().Hint, Does.StartWith("Полоса A: " + firstName + "\n"));
            Assert.That(headers[1].GetComponent<WorkshopTooltip>().Hint, Does.StartWith("Полоса B: " + secondName + "\n"));
            var headerBounds = headers.Select(value => BattleHudThemeEvidence.Bounds(panel.Root, value.rectTransform)).ToArray();
            var names = new[] { "Composite trial run", "Composite trial pause", "Composite trial restart", "Composite trial compare", "Composite trial header return" };
            var actions = names.Select(Find<Button>).ToArray();
            var actionBounds = actions.Select(value => BattleHudThemeEvidence.Bounds(panel.Root, (RectTransform)value.transform)).ToArray();
            var scroll = Find<ScrollRect>("Composite trial report scroll");
            var sheet = Find<RectTransform>("Composite trial sheet");
            foreach (var header in headers)
            {
                Assert.That(header.GetComponentInParent<ScrollRect>(), Is.Null, header.name);
                AssertInside(sheet, header.rectTransform);
            }
            foreach (float position in new[] { 1f, 0f })
            {
                scroll.verticalNormalizedPosition = position; Canvas.ForceUpdateCanvases();
                for (int i = 0; i < headers.Length; i++) Assert.That(BattleHudThemeEvidence.Bounds(panel.Root, headers[i].rectTransform), Is.EqualTo(headerBounds[i]));
                for (int i = 0; i < actions.Length; i++)
                {
                    Assert.That(actions[i].GetComponentInParent<ScrollRect>(), Is.Null, actions[i].name);
                    AssertInside(sheet, (RectTransform)actions[i].transform);
                    Assert.That(BattleHudThemeEvidence.Bounds(panel.Root, (RectTransform)actions[i].transform), Is.EqualTo(actionBounds[i]));
                    Assert.That(BattleHudThemeEvidence.Bounds(panel.Root, scroll.viewport).Overlaps(actionBounds[i]), Is.False, actions[i].name);
                }
            }
            EventSystem.current.SetSelectedGameObject(null);
            var pause = Find<Button>("Composite trial pause"); var hint = pause.GetComponent<WorkshopTooltip>();
            Assert.That(hint, Is.Not.Null); Assert.That(hint.Hint, Is.Not.Empty);
            EventSystem.current.SetSelectedGameObject(pause.gameObject); Canvas.ForceUpdateCanvases();
            Assert.That(Find<Text>("Hint").text, Is.EqualTo(hint.Hint));
            Assert.That(panel.LastAssemblyTrial, Is.SameAs(first)); Assert.That(panel.LastAssemblyTrialComparison, Is.SameAs(second));
            Assert.That(fixture.State(), Is.EqualTo(state)); Assert.That(fixture.SavedBytes, Is.EqualTo(saved));
            Assert.That(fixture.Store.SaveCalls, Is.Zero);
        }

        [Test] public void DifferentSupportRolesShowInapplicableCellsInsteadOfInventingZeroOrAnEffectDelta()
        {
            string state = fixture.State(); byte[] saved = fixture.SavedBytes;
            Open(Entry("album.wall")); Click("Composite trial run"); AssertSameConditions();
            var first = panel.LastAssemblyTrial; var second = panel.LastAssemblyTrialComparison;
            Assert.That(first.Resolved.Support.Kind, Is.EqualTo(AssemblyStructureFamily.Wall));
            Assert.That(second.Resolved.Support.Kind, Is.EqualTo(AssemblyStructureFamily.Slow));
            Assert.That(first.PreventedMovement, Is.Null); Assert.That(second.PreventedMovement, Is.Not.Null);
            AssertMetric("PreventedMovement", first.PreventedMovement, second.PreventedMovement);
            Assert.That(Cell("PreventedMovement", "A").text, Is.EqualTo("—"));
            Assert.That(Cell("PreventedMovement", "Delta").text, Is.EqualTo("—"));
            AssertMetric("DurationSeconds", first.DurationSeconds, second.DurationSeconds);
            Assert.That(fixture.State(), Is.EqualTo(state)); Assert.That(fixture.SavedBytes, Is.EqualTo(saved));
            Assert.That(fixture.Store.SaveCalls, Is.Zero);
        }

        [Test] public void GeneratorComparisonUsesTwoRealCopiesAndLocalWaveIncomeWithoutRewardsOrProfileWrites()
        {
            string state = fixture.State(); byte[] saved = fixture.SavedBytes;
            long tick = fixture.Run.Session.TickIndex; int ink = fixture.Run.Session.Ink, loads = fixture.Store.LoadCalls;
            Open(Entry("album.generator")); Click("Composite trial run"); AssertSameConditions();
            var first = panel.LastAssemblyTrial; var second = panel.LastAssemblyTrialComparison;
            Assert.That(first.Budget, Is.EqualTo(400)); Assert.That(first.Seed, Is.EqualTo(1337));
            Assert.That(first.Copies, Is.EqualTo(2)); Assert.That(second.Copies, Is.EqualTo(2));
            AssertMetric("UnitCost", first.UnitCost, second.UnitCost, quote: true);
            AssertMetric("Copies", first.Copies, second.Copies, quote: true);
            AssertMetric("Cost", first.Cost, second.Cost, quote: true);
            AssertMetric("Income", first.Income, second.Income);
            AssertMetric("CompletedWaves", first.CompletedWaves, second.CompletedWaves);
            AssertMetric("PaidWaves", first.PaidWaves, second.PaidWaves);
            Assert.That(first.CompletedWaves, Is.EqualTo(8)); Assert.That(first.PaidWaves, Is.EqualTo(8));
            Assert.That(first.Damage, Is.Null); Assert.That(second.Damage, Is.Null);
            Assert.That(fixture.Run.Session.TickIndex, Is.EqualTo(tick)); Assert.That(fixture.Run.Session.Ink, Is.EqualTo(ink));
            Assert.That(fixture.State(), Is.EqualTo(state)); Assert.That(fixture.SavedBytes, Is.EqualTo(saved));
            Assert.That(fixture.Store.SaveCalls, Is.Zero); Assert.That(fixture.Store.LoadCalls, Is.EqualTo(loads));
        }
    }
}
#endif
