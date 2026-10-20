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
    public sealed class ComponentPurchaseUiTests
    {
        GameObject canvas;
        ComponentAssemblyPanel Panel(AssemblyCommandFixture fixture, WorkshopDraftSession draft)
        {
            if (canvas == null) canvas = new GameObject("Isolated component catalogue", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(1024, 768);
            var panel = new ComponentAssemblyPanel(canvas.transform, fixture.Run, draft, TextResourcesAsset.Default, () => { });
            panel.Reflow(976, 538); panel.Show(); return panel;
        }
        static WorkshopDraftSession Weapon(AssemblyCommandFixture fixture)
        { var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(fixture.Gun()); draft.OpenWeapon(0); draft.SelectCategory(ComponentCategory.Head); return draft; }
        Button Button(string name) => canvas.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
        [TearDown] public void TearDown() { if (canvas != null) Object.DestroyImmediate(canvas); }

        [Test] public void FoundationInspectionShowsOnlyTheFoundationQuoteAndCapabilitiesWithoutChangingItsTower()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390))
            {
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(fixture.Gun()); draft.OpenFoundation();
                draft.SelectCategory(ComponentCategory.Hull);
                var beforeRecipe = draft.PreviewRecipe; string before = fixture.State();
                var panel = Panel(fixture, draft);
                var label = canvas.GetComponentsInChildren<Text>(true).Single(text => text.name == "Component resolved statistics");
                Assert.That(label.text, Does.Contain("Цена основы: 16"), "The foundation drawing must quote its own sixteen strokes, not the forty-stroke armed tower.");
                Assert.That(label.text, Does.Contain("Крепления: 2"));
                Assert.That(label.text, Does.Contain("Подача: 1,6"));
                Assert.That(label.text, Does.Contain("Прочность: 55"));
                Assert.That(label.text, Does.Contain("Вместимость: 10"));
                Assert.That(label.text, Does.Not.Contain("урон").And.Not.Contain("DPS"));
                Assert.That(draft.PreviewRecipe, Is.SameAs(beforeRecipe));
                Assert.That(fixture.State(), Is.EqualTo(before));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void WeaponDrawingShowsSixResolvedWeaponCharacteristicsAndItsOwnPriceWithoutEditing()
        {
            using(var fixture=new AssemblyCommandFixture(94,390))
            {
                var draft=Weapon(fixture); draft.SelectCategory(ComponentCategory.Mechanism);
                var beforeRecipe=draft.PreviewRecipe; string before=fixture.State();
                Panel(fixture,draft);
                var weapon=draft.Resolved.Weapons.Single();
                var statistics=canvas.GetComponentsInChildren<Text>().Single(text=>text.name=="Component resolved statistics");
                string Number(float value)=>value.ToString("0.###",System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
                Assert.That(statistics.isActiveAndEnabled,Is.True);
                Assert.That(statistics.text,Does.Contain("Урон: "+Number(weapon.Damage)));
                Assert.That(statistics.text,Does.Contain("Дальность: "+Number(weapon.Range)));
                Assert.That(statistics.text,Does.Contain("Обойма: "+weapon.MagazineSize));
                Assert.That(statistics.text,Does.Contain("Перезарядка: "+Number(weapon.ReloadSeconds)+" с"));
                Assert.That(statistics.text,Does.Contain("Нагрузка башни: "+draft.Resolved.Load));
                Assert.That(statistics.text,Does.Contain("Цена оружия: 24 штр."),"The weapon drawing quotes its own selected five parts, excluding the sixteen-stroke foundation.");
                Assert.That(statistics.text.TrimEnd().Split('\n').Length,Is.EqualTo(6));
                Assert.That(draft.PreviewRecipe,Is.SameAs(beforeRecipe));
                Assert.That(fixture.State(),Is.EqualTo(before)); Assert.That(fixture.Store.SaveCalls,Is.Zero);
            }
        }

        [Test] public void NativeSubmitInstallsThePartWithoutDestroyingTheExecutingButton()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390))
            {
                Assert.That(fixture.Commands.PurchasePart("head.long", Guid.NewGuid()).Success, Is.True);
                var draft = Weapon(fixture); var panel = Panel(fixture, draft); panel.Catalog.InspectPart("head.long");
                var events = new GameObject("Isolated catalogue submit events", typeof(EventSystem));
                try
                {
                    var button = Button("Component select part");
                    EventSystem.current.SetSelectedGameObject(button.gameObject);
                    ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    Assert.That(draft.PreviewRecipe.Tower.Mounts.Single().Weapon.Head.Id, Is.EqualTo("head.long"));
                    UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
                }
                finally { Object.DestroyImmediate(events); }
            }
        }

        [Test] public void LockedPartShowsConditionsAndCannotBeInstalled()
        {
            using (var fixture = new AssemblyCommandFixture(94, 0))
            {
                var draft = Weapon(fixture); var before = draft.PreviewRecipe;
                var panel = Panel(fixture, draft); panel.Catalog.SetShowUnavailable(true); panel.Catalog.InspectPart("head.long");
                Assert.That(Button("Component select part").interactable, Is.False);
                Assert.That(Button("Component purchase part").interactable, Is.False);
                string text = string.Join("|", canvas.GetComponentsInChildren<Text>(true).Select(t => t.text));
                Assert.That(text, Does.Contain("4")); Assert.That(text, Does.Contain("80"));
                Assert.That(draft.PreviewRecipe, Is.SameAs(before));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void InspectingLockedLongBarrelComparesResolverNumbersWithoutChangingTheDraft()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390))
            {
                var draft = Weapon(fixture); var beforeRecipe = draft.PreviewRecipe; string before = fixture.State();
                var original = draft.Resolved;
                var tower = beforeRecipe.Tower; var weapon = tower.Mounts[0].Weapon;
                var candidateRecipe = new AssemblyRecipe(new TowerRecipe(tower.Foundation, new[] {
                    new WeaponMountRecipe(0, new WeaponRecipe(weapon.Mechanism, new PartRef("head.long", 1), weapon.Sight, weapon.Magazine, weapon.Trigger)) }));
                var candidate = AssemblyResolver.Resolve(candidateRecipe, fixture.Catalog, fixture.Balance, MaterialTier.Pencil).Value;
                var panel = Panel(fixture, draft); panel.Catalog.SetShowUnavailable(true); panel.Catalog.InspectPart("head.long");
                var label = canvas.GetComponentsInChildren<Text>(true).SingleOrDefault(text => text.name == "Component comparison");
                Assert.That(label, Is.Not.Null, "Inspecting a replacement must show current → candidate values from the shared resolver.");
                string Number(float value) => value.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
                Assert.That(label.text, Does.Contain(Number(original.Weapons[0].Damage) + " → " + Number(candidate.Weapons[0].Damage)));
                Assert.That(label.text, Does.Contain(Number(original.Weapons[0].Range) + " → " + Number(candidate.Weapons[0].Range)));
                Assert.That(label.text, Does.Contain(original.Cost + " → " + candidate.Cost));
                Assert.That(label.text, Does.Contain(original.Load + " → " + candidate.Load));
                Assert.That(draft.PreviewRecipe, Is.SameAs(beforeRecipe));
                Assert.That(fixture.State(), Is.EqualTo(before));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
                Assert.That(Button("Component select part").interactable, Is.False);
            }
        }

        [Test] public void InspectingMortarMechanismUsesTheInstallCompatiblePartsWithoutEditingTheGunDraft()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390))
            {
                var gun = fixture.Gun();
                var mortar = fixture.Catalog.Starters.First(entry => entry.Kind == AlbumEntryKind.Tower && entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.mortar");
                // Use a foundation that can carry the mortar, so this test isolates inspection
                // from the separate overload gate already exercised by the workshop tests.
                var source = new AlbumEntry("inspection.mechanism", "Gun fixture", 1, new RecipeSnapshot(new AssemblyRecipe(new TowerRecipe(
                    mortar.Snapshot.Recipe.Tower.Foundation, gun.Snapshot.Recipe.Tower.Mounts)), fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions));
                var install = new WorkshopDraftSession(fixture.Run); install.Begin(source); install.OpenWeapon(0);
                Assert.That(install.SelectPart("core.mortar"), Is.True);
                var expectedRecipe = install.PreviewRecipe;
                Assert.That(expectedRecipe.Tower.Mounts[0].Weapon.Head.Id, Is.EqualTo("head.mortar"));
                var expected = AssemblyResolver.Resolve(expectedRecipe, fixture.Catalog, fixture.Balance, MaterialTier.Pencil);
                Assert.That(expected.Valid, Is.True);
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(source); draft.OpenWeapon(0);
                var beforeRecipe = draft.PreviewRecipe; string before = fixture.State();
                var panel = Panel(fixture, draft); panel.Catalog.SetShowUnavailable(true); panel.Catalog.InspectPart("core.mortar");
                var preview = canvas.GetComponentInChildren<AssemblyMechanicPreview>();
                Assert.That(preview, Is.Not.Null, "Inspecting the mortar must show its own ready diagram, rather than the previously equipped gun.");
                Assert.That(preview.Weapon.Family, Is.EqualTo(AssemblyWeaponFamily.Mortar));
                Assert.That(preview.Weapon.Radius, Is.EqualTo(expected.Value.Weapons[0].Radius));
                var property = typeof(AssemblyMechanicPreview).GetProperty("Recipe");
                Assert.That(property, Is.Not.Null, "The displayed composition needs the same compatible part recipe as installation.");
                var displayed = (RecipeSnapshot)property.GetValue(preview);
                Assert.That(displayed.Recipe.Tower.Mounts[0].Weapon.Parts.Select(part => part.Id),
                    Is.EqualTo(expectedRecipe.Tower.Mounts[0].Weapon.Parts.Select(part => part.Id)));
                var comparison = canvas.GetComponentsInChildren<Text>().Single(text => text.name == "Component comparison");
                Assert.That(comparison.text, Does.Contain("Радиус"));
                Assert.That(draft.PreviewRecipe, Is.SameAs(beforeRecipe));
                Assert.That(draft.PreviewRecipe.Tower.Mounts[0].Weapon.Mechanism.Id, Is.EqualTo("core.gun"));
                Assert.That(fixture.State(), Is.EqualTo(before));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void MagazineShowsCapacityAndCycleTimingsFromTheResolver()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var draft = Weapon(fixture); draft.SelectCategory(ComponentCategory.Magazine);
                Panel(fixture, draft);
                var weapon = draft.Resolved.Weapons.Single();
                string expected = TextResourcesAsset.Default.Resolve(UserText.Message("assembly.component.magazine_stats",
                    weapon.MagazineSize, weapon.Interval, weapon.ReloadSeconds));
                Assert.That(canvas.GetComponentsInChildren<Text>(true).Any(t => t.text == expected), Is.True);
            }
        }

        [TestCase("core.gun", "head.gun", "magazine.drum")]
        [TestCase("core.gun", "head.gun", "magazine.belt")]
        [TestCase("core.stapler", "head.stapler", "magazine.drum")]
        [TestCase("core.mortar", "head.mortar", "magazine.standard")]
        [TestCase("core.fan", "head.fan", "magazine.standard")]
        public void InspectedMagazineShowsVisibleResolvedCapacityIntervalAndReloadWithoutEditing(string mechanism, string head, string inspectedMagazine)
        {
            using (var fixture = new AssemblyCommandFixture(94, 390))
            {
                var starter = mechanism == "core.mortar" ? fixture.Catalog.Starters.First(entry => entry.Kind == AlbumEntryKind.Tower &&
                    entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == mechanism) : fixture.Gun();
                var originalWeapon = starter.Snapshot.Recipe.Tower.Mounts[0].Weapon;
                var weapon = new WeaponRecipe(new PartRef(mechanism, 1), new PartRef(head, 1), originalWeapon.Sight, new PartRef("magazine.standard", 1), originalWeapon.Trigger);
                var recipe = new AssemblyRecipe(new TowerRecipe(starter.Snapshot.Recipe.Tower.Foundation, new[] { new WeaponMountRecipe(0, weapon) }));
                var source = new AlbumEntry("inspection.magazine", "Magazine fixture", 1, new RecipeSnapshot(recipe, fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions));
                var draft = new WorkshopDraftSession(fixture.Run); draft.Begin(source); draft.OpenWeapon(0); draft.SelectCategory(ComponentCategory.Magazine);
                var beforeRecipe = draft.PreviewRecipe; string before = fixture.State();
                var current = draft.Resolved.Weapons[0];
                var candidateRecipe = new AssemblyRecipe(new TowerRecipe(recipe.Tower.Foundation, new[] {
                    new WeaponMountRecipe(0, new WeaponRecipe(weapon.Mechanism, weapon.Head, weapon.Sight, new PartRef(inspectedMagazine, 1), weapon.Trigger)) }));
                var candidate = AssemblyResolver.Resolve(candidateRecipe, fixture.Catalog, fixture.Balance, MaterialTier.Pencil).Value.Weapons[0];
                var panel = Panel(fixture, draft); panel.Catalog.SetShowUnavailable(true); panel.Catalog.InspectPart(inspectedMagazine); Canvas.ForceUpdateCanvases();
                var cycle = canvas.GetComponentsInChildren<Text>().SingleOrDefault(text => text.name == "Component magazine cycle");
                Assert.That(cycle, Is.Not.Null, "The defining magazine statistics must have their own visible region, including mortar and fan views.");
                Assert.That(cycle.isActiveAndEnabled && cycle.rectTransform.rect.height >= 60, Is.True);
                var modifiers=canvas.GetComponentsInChildren<Text>(true).Single(text=>text.name=="Component inspected modifiers");
                if(modifiers.isActiveAndEnabled && !string.IsNullOrWhiteSpace(modifiers.text) && modifiers.rectTransform.rect.height>0)
                {
                    Rect ScreenRect(RectTransform rect)
                    {
                        var corners=new Vector3[4]; rect.GetWorldCorners(corners);
                        return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
                    }
                    Assert.That(ScreenRect(modifiers.rectTransform).Overlaps(ScreenRect(cycle.rectTransform)),Is.False,
                        "Magazine multipliers cannot obscure capacity or cycle timings.");
                }
                string Number(float value) => value.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
                if (inspectedMagazine == "magazine.standard")
                {
                    Assert.That(cycle.text, Does.Contain(current.MagazineSize.ToString()));
                    Assert.That(cycle.text, Does.Contain(Number(current.Interval)));
                    Assert.That(cycle.text, Does.Contain(Number(current.ReloadSeconds)));
                }
                else
                {
                    Assert.That(cycle.text, Does.Contain(current.MagazineSize + " → " + candidate.MagazineSize));
                    Assert.That(cycle.text, Does.Contain(Number(current.Interval) + " → " + Number(candidate.Interval)));
                    Assert.That(cycle.text, Does.Contain(Number(current.ReloadSeconds) + " → " + Number(candidate.ReloadSeconds)));
                }
                Assert.That(draft.PreviewRecipe, Is.SameAs(beforeRecipe));
                Assert.That(fixture.State(), Is.EqualTo(before));
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void CatalogueOpensWithTheFirstOwnedPartFullyInsideItsViewport()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var panel = Panel(fixture, Weapon(fixture)); panel.Catalog.SetShowUnavailable(true); Canvas.ForceUpdateCanvases();
                var first = Button("Component inspect head.gun").GetComponent<RectTransform>();
                var viewport = (RectTransform)first.parent.parent;
                var corners = new Vector3[4]; first.GetWorldCorners(corners);
                float top = viewport.InverseTransformPoint(corners[1]).y;
                float bottom = viewport.InverseTransformPoint(corners[0]).y;
                Assert.That(top, Is.LessThanOrEqualTo(viewport.rect.yMax + .1f));
                Assert.That(bottom, Is.GreaterThanOrEqualTo(viewport.rect.yMin - .1f));
            }
        }

        [Test] public void ReflowKeepsTheEquippedFanFullyInsideItsCatalogueViewport()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390, data =>
                data.Unlocked = data.Unlocked.Concat(new[] { "core.fan", "head.fan" }).Distinct().ToArray()))
            {
                var starter = fixture.Gun(); var old = starter.Snapshot.Recipe.Tower.Mounts[0].Weapon;
                var weapon = new WeaponRecipe(new PartRef("core.fan", 1), new PartRef("head.fan", 1), old.Sight, old.Magazine, old.Trigger);
                var recipe = new AssemblyRecipe(new TowerRecipe(starter.Snapshot.Recipe.Tower.Foundation, new[] { new WeaponMountRecipe(0, weapon) }));
                var draft = new WorkshopDraftSession(fixture.Run);
                draft.Begin(new AlbumEntry("inspection.fan", "Fan fixture", 1, new RecipeSnapshot(recipe, fixture.Catalog.BalanceRevision, fixture.Catalog.Definitions)));
                draft.OpenWeapon(0);
                var panel = Panel(fixture, draft); panel.Reflow(1232, 488); Canvas.ForceUpdateCanvases();
                var selected = Button("Component inspect core.fan").GetComponent<RectTransform>();
                var viewport = (RectTransform)selected.parent.parent;
                var corners = new Vector3[4]; selected.GetWorldCorners(corners);
                Assert.That(viewport.InverseTransformPoint(corners[1]).y, Is.LessThanOrEqualTo(viewport.rect.yMax + .1f));
                Assert.That(viewport.InverseTransformPoint(corners[0]).y, Is.GreaterThanOrEqualTo(viewport.rect.yMin - .1f),
                    "Reflow must reveal the selected part again after rebuilding the catalogue's final geometry.");
                panel.Catalog.SetShowUnavailable(true); Canvas.ForceUpdateCanvases();
                selected = Button("Component inspect core.fan").GetComponent<RectTransform>();
                viewport = (RectTransform)selected.parent.parent; selected.GetWorldCorners(corners);
                Assert.That(viewport.InverseTransformPoint(corners[1]).y, Is.LessThanOrEqualTo(viewport.rect.yMax + .1f));
                Assert.That(viewport.InverseTransformPoint(corners[0]).y, Is.GreaterThanOrEqualTo(viewport.rect.yMin - .1f),
                    "Showing unavailable variants must keep the equipped fan visible in the rebuilt catalogue.");
                Assert.That(fixture.Store.SaveCalls, Is.Zero);
            }
        }

        [Test] public void PurchaseRequiresConfirmationAndNeverInstallsThePart()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390))
            {
                var draft = Weapon(fixture); var panel = Panel(fixture, draft);
                panel.Catalog.SetShowUnavailable(true); panel.Catalog.InspectPart("head.long");
                Button("Component purchase part").onClick.Invoke();
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(94));
                Assert.That(draft.PreviewRecipe.Tower.Mounts.Single().Weapon.Head.Id, Is.EqualTo("head.gun"));
                Button("Component purchase confirm").onClick.Invoke();
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(14));
                Assert.That(fixture.Run.Workshop.IsUnlocked("head.long"), Is.True);
                Assert.That(draft.PreviewRecipe.Tower.Mounts.Single().Weapon.Head.Id, Is.EqualTo("head.gun"));
                Assert.That(Button("Component select part").interactable, Is.True);
                Button("Component select part").onClick.Invoke();
                Assert.That(draft.PreviewRecipe.Tower.Mounts.Single().Weapon.Head.Id, Is.EqualTo("head.long"));
                Assert.That(draft.Draft.Snapshot.Recipe.Tower.Mounts.Single().Weapon.Head.Id, Is.EqualTo("head.gun"));
            }
        }

        [Test] public void PurchasedRightIsVisibleInAnotherCompatibleCatalogue()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390))
            {
                var first = Weapon(fixture); var panel = Panel(fixture, first);
                panel.Catalog.SetShowUnavailable(true); panel.Catalog.InspectPart("head.long");
                Button("Component purchase part").onClick.Invoke(); Button("Component purchase confirm").onClick.Invoke();
                panel.Hide(); Object.DestroyImmediate(panel.Root.gameObject);
                var second = Weapon(fixture); var reopened = Panel(fixture, second); reopened.Catalog.InspectPart("head.long");
                Assert.That(Button("Component select part").interactable, Is.True);
                Assert.That(Button("Component purchase part").gameObject.activeSelf, Is.False);
                Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(14));
                Assert.That(second.PreviewRecipe.Tower.Mounts.Single().Weapon.Head.Id, Is.EqualTo("head.gun"));
            }
        }

        [Test] public void UnavailableToggleKeepsCompatibilityFiltering()
        {
            using (var fixture = new AssemblyCommandFixture())
            {
                var panel = Panel(fixture, Weapon(fixture));
                Assert.That(canvas.transform.Find("Component assembly/Component catalog/Component catalogue list/Component catalogue list content/Component inspect head.long"), Is.Null);
                panel.Catalog.SetShowUnavailable(true);
                Assert.That(canvas.GetComponentsInChildren<Button>(true).Any(b => b.name == "Component inspect head.long"), Is.True);
                Assert.That(canvas.GetComponentsInChildren<Button>(true).Any(b => b.name == "Component inspect head.mortar"), Is.False);
            }
        }

        [Test] public void BurstAndDrumOfferAnExplicitStockMagazineAction()
        {
            using (var fixture = new AssemblyCommandFixture(1000, 700))
            {
                Assert.That(fixture.Commands.PurchasePart("magazine.drum", Guid.NewGuid()).Success, Is.True);
                Assert.That(fixture.Commands.PurchasePart("trigger.burst", Guid.NewGuid()).Success, Is.True);
                var draft = Weapon(fixture); draft.SelectPart("magazine.drum"); draft.SelectPart("trigger.burst");
                var panel = Panel(fixture, draft);
                Assert.That(draft.CanSaveComponent, Is.False);
                Assert.That(draft.PreviewRecipe.Tower.Mounts.Single().Weapon.Magazine.Id, Is.EqualTo("magazine.drum"));
                Button("Component apply suggested part").onClick.Invoke();
                Assert.That(draft.PreviewRecipe.Tower.Mounts.Single().Weapon.Magazine.Id, Is.EqualTo("magazine.standard"));
                Assert.That(draft.PreviewRecipe.Tower.Mounts.Single().Weapon.Trigger.Id, Is.EqualTo("trigger.burst"));
                Assert.That(draft.Resolved, Is.Not.Null);
                Assert.That(draft.Diagnostics.Any(d => d.Code == "assembly.part.required"), Is.False);
                Assert.That(draft.CanSaveComponent, Is.True, "The full catalog certifies the burst trigger with its required stock magazine.");
                Assert.That(draft.Diagnostics.Any(d => d.Code.Contains("fit")), Is.False);
            }
        }

        [Test] public void RefusedPurchaseKeepsCatalogueAndNestedDraftOpen()
        {
            using (var fixture = new AssemblyCommandFixture(94, 390))
            {
                var draft = Weapon(fixture); var panel = Panel(fixture, draft); var before = draft.PreviewRecipe;
                panel.Catalog.SetShowUnavailable(true); panel.Catalog.InspectPart("head.long"); fixture.Store.RefuseWrites = true;
                Button("Component purchase part").onClick.Invoke(); Button("Component purchase confirm").onClick.Invoke();
                Assert.That(panel.Root.gameObject.activeSelf, Is.True); Assert.That(draft.IsComponentOpen, Is.True);
                Assert.That(draft.PreviewRecipe, Is.SameAs(before)); Assert.That(fixture.Run.Workshop.Gold, Is.EqualTo(94));
                Assert.That(canvas.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("Injected assembly transaction write refusal")), Is.True);
            }
        }
    }
}
#endif
