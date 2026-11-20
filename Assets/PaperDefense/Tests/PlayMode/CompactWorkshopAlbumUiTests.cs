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
    public sealed class CompactWorkshopAlbumUiTests
    {
        GameObject canvas, events;

        AlbumPanel Create(AssemblyCommandFixture fixture)
        {
            events = new GameObject("Compact album keyboard", typeof(EventSystem));
            canvas = new GameObject("Compact album", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 600);
            var panel = new AlbumPanel(canvas.transform, fixture.Run, TextResourcesAsset.Default, _ => { }, () => { });
            panel.Reflow(800, 600); Canvas.ForceUpdateCanvases();
            return panel;
        }

        T Find<T>(string name) where T : Component => canvas.GetComponentsInChildren<T>().Single(item => item.name == name);
        void Submit(string name)
        {
            var button = Find<Button>(name);
            Assert.That(button.IsInteractable(), Is.True, name);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Canvas.ForceUpdateCanvases();
        }
        static Rect Bounds(RectTransform target, RectTransform relative)
        {
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            var points = corners.Select(relative.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
                points.Max(point => point.x), points.Max(point => point.y));
        }
        static void AssertInside(RectTransform target, RectTransform container)
        {
            var bounds = Bounds(target, container);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(container.rect.xMin - 1), target.name);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(container.rect.yMin - 1), target.name);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(container.rect.xMax + 1), target.name);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(container.rect.yMax + 1), target.name);
        }
        void AssertComparisonSummaryAndActionsVisible()
        {
            Canvas.ForceUpdateCanvases();
            var inspector = Find<RectTransform>("Album inspector");
            var actions = new[] { "Album details", "Album edit", "Album variant", "Album delete" }
                .Select(name => (RectTransform)Find<Button>(name).transform).ToArray();
            foreach (var action in actions)
            {
                AssertInside(action, inspector);
                Assert.That(action.GetComponentInParent<ScrollRect>(), Is.Null, "Actions remain available while the comparison scrolls.");
            }
            foreach (string stat in new[] { "Health", "Load", "Cost" })
            {
                foreach (string prefix in new[] { "Album metric value ", "Album metric delta " })
                {
                    var amount = Find<Text>(prefix + stat).rectTransform;
                    AssertInside(amount, inspector);
                    var scroll = amount.GetComponentInParent<ScrollRect>();
                    if (scroll != null) AssertInside(amount, scroll.viewport);
                    foreach (var action in actions)
                        Assert.That(Bounds(amount, inspector).Overlaps(Bounds(action, inspector)), Is.False, prefix + stat + " overlaps " + action.name);
                }
            }
            var direction = Find<Text>("Album comparison direction");
            Assert.That(direction.text, Is.EqualTo("Общие: образец → выбранная"));
            AssertInside(direction.rectTransform, inspector);
            var directionScroll = direction.GetComponentInParent<ScrollRect>();
            if (directionScroll != null) AssertInside(direction.rectTransform, directionScroll.viewport);
        }
        [TearDown] public void TearDown()
        {
            if (canvas != null) Object.DestroyImmediate(canvas);
            if (events != null) Object.DestroyImmediate(events);
        }

        [Test] public void KeyboardPinsAnExplicitReferenceAcrossSelectionFilteringAndReflowWithoutWriting()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390))
            {
                string state = fixture.State(); byte[] saved = fixture.SavedBytes;
                var panel = Create(fixture); panel.SelectEntry("album.guard"); Canvas.ForceUpdateCanvases();
                Assert.That(panel.ReferenceId, Is.Null, "Selection alone must not pick a comparison reference.");
                var compare = Find<Button>("Album compare");
                var hint = compare.GetComponent<WorkshopTooltip>();
                Assert.That(hint, Is.Not.Null); Assert.That(hint.Hint, Is.Not.Empty);
                EventSystem.current.SetSelectedGameObject(compare.gameObject); Canvas.ForceUpdateCanvases();
                Assert.That(Find<Text>("Hint").text, Is.EqualTo(hint.Hint), "The icon action explains itself on keyboard focus.");
                EventSystem.current.SetSelectedGameObject(null);
                Assert.That(canvas.GetComponentsInChildren<Text>().Any(item => item.name == "Hint"), Is.False, "Leaving focus dismisses the transient hint.");
                Submit("Album compare");
                Assert.That(panel.ReferenceId, Is.EqualTo("album.guard"));
                panel.SetFilter(AlbumFilter.Structures); panel.SelectEntry("album.repair");
                panel.Reflow(1600, 900); panel.Reflow(800, 600);
                Assert.That(panel.SelectedId, Is.EqualTo("album.repair"));
                Assert.That(panel.ReferenceId, Is.EqualTo("album.guard"), "A hidden reference survives filter and resize.");
                AssertComparisonSummaryAndActionsVisible();
                Submit("Album compare clear"); Assert.That(panel.ReferenceId, Is.Null);
                Assert.That(fixture.State(), Is.EqualTo(state)); Assert.That(fixture.SavedBytes, Is.EqualTo(saved));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void ComparisonUsesFrozenPageDefinitionsAndKeepsExactAmountsReadableAt800()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390, data => {
                var original = data.Entries.Select(item => item.CreateSnapshot()).Single(item => item.Id == "album.guard");
                string hullId = original.Snapshot.Recipe.Tower.Foundation.Hull.Id;
                string feedId = original.Snapshot.Recipe.Tower.Foundation.Feed.Id;
                var definitions = original.Snapshot.Definitions.Select(part => {
                    if (part.Id != hullId && part.Id != feedId) return part;
                    var stat = part.Id == hullId ? AssemblyStat.Health : AssemblyStat.Feed;
                    var operation = part.Id == hullId ? ModifierOperation.Add : ModifierOperation.Multiply;
                    float value = part.Id == hullId ? part.Get(stat, operation) + 37 : .5f;
                    return new ComponentDefinition(part.Id, part.Revision, part.Name, part.Category, part.RequiredLevel, part.UnlockGold,
                        part.WeaponFamilies, part.StructureFamilies,
                        part.Modifiers.Where(modifier => !(modifier.Stat == stat && modifier.Operation == operation))
                            .Concat(new[] { new ComponentModifier(stat, operation, value) }), part.RequiredParts);
                });
                var page = new AlbumEntry("frozen.guard", "Сохранённая усиленная страница", 1,
                    new RecipeSnapshot(original.Snapshot.Recipe, original.Snapshot.BalanceRevision, definitions));
                data.Entries = data.Entries.Concat(new[] { new AlbumEntryData(page) }).ToArray();
            }))
            {
                var stock = fixture.Run.Workshop.Entries.Single(item => item.Id == "album.guard");
                var frozen = fixture.Run.Workshop.Entries.Single(item => item.Id == "frozen.guard");
                var before = fixture.Run.PreviewAlbumEntry(stock); var after = fixture.Run.PreviewAlbumEntry(frozen);
                Assert.That(before.Valid && after.Valid, Is.True);
                Assert.That(after.Value.Health, Is.Not.EqualTo(before.Value.Health), "Fixture must distinguish frozen and live definitions.");
                string state = fixture.State(); byte[] saved = fixture.SavedBytes;
                var panel = Create(fixture); panel.SelectEntry(stock.Id); Submit("Album compare"); panel.SelectEntry(frozen.Id);
                Canvas.ForceUpdateCanvases();
                string Number(double value) => value.ToString("0.###", TextResourcesAsset.Default.Culture);
                Assert.That(Find<Text>("Album metric value Health").text, Is.EqualTo(Number(before.Value.Health) + " → " + Number(after.Value.Health)));
                Assert.That(Find<Text>("Album metric delta Health").text, Is.EqualTo("Δ +" + Number(after.Value.Health - before.Value.Health)));
                Assert.That(Find<Text>("Album metric value Cost").text, Is.EqualTo(Number(before.Value.Cost) + " → " + Number(after.Value.Cost)));
                Assert.That(Find<Text>("Album metric value Load").text, Is.EqualTo(before.Value.Load + "/" + before.Value.Capacity + " → " + after.Value.Load + "/" + after.Value.Capacity));
                Assert.That(after.Value.FeedMultiplier, Is.Not.EqualTo(before.Value.FeedMultiplier));
                var oldWeapon = before.Value.Weapons.Single(); var weapon = after.Value.Weapons.Single();
                Assert.That(Find<Text>("Album metric value Weapon 0 Interval").text,
                    Is.EqualTo(Number(oldWeapon.IntervalAtFeed(before.Value.FeedMultiplier)) + " → " + Number(weapon.IntervalAtFeed(after.Value.FeedMultiplier))));
                Assert.That(Find<Text>("Album metric value Weapon 0 Reload").text,
                    Is.EqualTo(Number(oldWeapon.ReloadAtFeed(before.Value.FeedMultiplier)) + " → " + Number(weapon.ReloadAtFeed(after.Value.FeedMultiplier))));
                foreach (string stat in new[] { "Health", "Load", "Cost" })
                {
                    var amount = Find<Text>("Album metric value " + stat);
                    Assert.That(amount.text, Is.Not.Empty);
                    Assert.That(amount.preferredWidth, Is.LessThanOrEqualTo(amount.rectTransform.rect.width + 1), stat + " must retain its exact amount.");
                }
                Assert.That(fixture.State(), Is.EqualTo(state)); Assert.That(fixture.SavedBytes, Is.EqualTo(saved));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void DifferentRolesOnlyCompareCommonStatsWithoutInventingWeaponOrSupportDeltas()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                string state = fixture.State(); byte[] saved = fixture.SavedBytes;
                var panel = Create(fixture); panel.SelectEntry("album.guard"); Submit("Album compare");
                panel.SelectEntry("album.mortar"); AssertCommonComparisonOnly(weaponCounts: true);
                var guard = fixture.Run.PreviewAlbumEntry(fixture.Run.Workshop.Entries.Single(item => item.Id == "album.guard")).Value;
                var mortar = fixture.Run.PreviewAlbumEntry(fixture.Run.Workshop.Entries.Single(item => item.Id == "album.mortar")).Value;
                Assert.That(Find<Text>("Album metric value WeaponCount").text, Is.EqualTo(guard.Weapons.Count + " → " + mortar.Weapons.Count));
                string countDelta = (mortar.Weapons.Count - guard.Weapons.Count).ToString("+0;−0;0", TextResourcesAsset.Default.Culture);
                Assert.That(Find<Text>("Album metric delta WeaponCount").text, Is.EqualTo("Δ " + countDelta));
                Assert.That(Find<Text>("Album metric delta WeaponCount").color, Is.EqualTo(FieldMenuSkin.Muted),
                    "The number of weapons does not imply superiority across different weapon families.");
                panel.SelectEntry("album.repair"); AssertCommonComparisonOnly();
                Submit("Album compare"); panel.SelectEntry("album.generator"); AssertCommonComparisonOnly();
                Assert.That(panel.ReferenceId, Is.EqualTo("album.repair"));
                Assert.That(fixture.State(), Is.EqualTo(state)); Assert.That(fixture.SavedBytes, Is.EqualTo(saved));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        void AssertCommonComparisonOnly(bool weaponCounts = false)
        {
            Assert.That(Find<Text>("Album comparison status").text, Is.Not.Empty);
            var deltas = canvas.GetComponentsInChildren<Text>().Where(item => item.name.StartsWith("Album metric delta "))
                .Select(item => item.name.Substring("Album metric delta ".Length)).ToArray();
            Assert.That(deltas, Does.Contain("Health").And.Contain("Load").And.Contain("Cost"));
            Assert.That(deltas.All(stat => stat == "Health" || stat == "Load" || stat == "Cost" || stat == "BuildSeconds" || stat == "Defense" || stat == "FrontDefense" || weaponCounts && stat == "WeaponCount"), Is.True,
                "Different attack/support roles have no comparable effect or theoretical DPS delta.");
        }
    }
}
#endif
