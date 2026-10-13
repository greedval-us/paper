using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Tests
{
    public sealed class DataDrivenWorkshopUiTests
    {
        readonly List<Object> owned = new List<Object>();
        GameObject root;
        RunController run;
        WorkshopHud hud;

        [TearDown] public void TearDown()
        { for (int i = owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(owned[i]); owned.Clear(); }

        void Initialize()
        {
            var database = ScriptableObject.CreateInstance<ContentDatabaseAsset>(); owned.Add(database);
            var input = LegacyContentCatalog.CreateInput(GameBalance.CreateDefault());
            foreach (var item in input.Chassis) item.TemplateId = "template.ui.sample";
            foreach (var item in input.Weapons) item.TemplateId = "template.ui.sample";
            foreach (var item in input.Modules) item.TemplateId = "template.ui.sample";
            input.Templates.Add(new DrawingTemplateDefinitionInput { Id = "template.ui.sample", DisplayName = "Проверочный образец", Weight = 25,
                Contours = new List<DrawingContourInput> { new DrawingContourInput { Points = new List<DrawingCoordinateInput> { new DrawingCoordinateInput(-.1f, 0), new DrawingCoordinateInput(.1f, .4f) } } } });
            for (int i = 0; i < 20; i++) input.Weapons.Add(new WeaponDefinitionInput
            {
                Id = "weapon.sample." + i, Name = "Вариант " + i, ButtonLabel = "ВАРИАНТ " + i, Description = "Одиночный проверочный вариант", TemplateId = "template.ui.sample", InitiallyUnlocked = true, Order = 100 + i,
                Load = 1, Stats = new StructureDefinition { Cost = 31, MaxHealth = 55, BuildSeconds = 0, Range = 9, Damage = 7, ShotInterval = .8f }
            });
            input.Chassis.Add(new ChassisDefinitionInput { Id = "chassis.sample", Name = "Проверочное основание", ButtonLabel = "ПРОВЕРКА", InitiallyUnlocked = true, TemplateId = "template.ui.sample", Capacity = 20, BaseCost = 18, BaseHealth = 70 });
            input.Modules.Add(new ModuleDefinitionInput { Id = "module.sample", Name = "Проверочный модуль", ButtonLabel = "МОДУЛЬ", InitiallyUnlocked = true, Repeatable = true, TemplateId = "template.ui.sample" });
            input.Modules.Single(value => value.Id == LegacyContentIds.Module(TowerModules.Armor)).Repeatable = true;
            database.catalog = input;
            var preset = ScriptableObject.CreateInstance<BalancePresetAsset>(); owned.Add(preset); preset.database = database; preset.rules = GlobalBalanceSettings.From(GameBalance.CreateDefault());
            preset.rules.Trials.CounterfireDamage = 17; preset.rules.Trials.CounterfireInterval = 3.5f; preset.rules.Trials.CounterfireRange = 8.5f;
            Initialize(preset);
        }

        void Initialize(BalancePresetAsset preset, WorkshopProfile profile = null)
        {
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>(); owned.Add(asset); asset.dataPreset = preset;
            root = new GameObject("Isolated data driven workshop"); owned.Add(root); root.SetActive(false);
            run = root.AddComponent<RunController>(); run.balanceAsset = asset;
            if (profile == null) run.InitializeWorkshop(false); else run.InitializeWorkshop(new MemoryStore(profile));
            hud = root.AddComponent<WorkshopHud>(); hud.Initialize(run);
        }

        Button Button(string name) => root.GetComponentsInChildren<Button>(true).Single(value => value.name == name);

        [Test] public void TwentiethWeaponAndThirdChassisAppearAndSelectTheirStableIdsInScrollableCatalogues()
        {
            Initialize();
            Button("First weapon.sample.19").onClick.Invoke();
            Button("Chassis chassis.sample").onClick.Invoke();
            Assert.That(hud.Draft.FirstWeaponId, Is.EqualTo("weapon.sample.19"));
            Assert.That(hud.Draft.ChassisId, Is.EqualTo("chassis.sample"));
            Assert.That(run.PreviewBlueprint(hud.Draft).Valid, Is.True);
            Assert.That(root.GetComponentsInChildren<Button>(true).Count(value => value.name.StartsWith("First weapon.sample.")), Is.EqualTo(20));
            var weapons = root.GetComponentsInChildren<ScrollRect>(true).Single(value => value.name == "First weapon catalogue");
            var chassis = root.GetComponentsInChildren<ScrollRect>(true).Single(value => value.name == "Chassis catalogue");
            Assert.That(weapons.content.sizeDelta.y, Is.GreaterThan(weapons.viewport.sizeDelta.y));
            Assert.That(chassis.content.sizeDelta.y, Is.GreaterThan(chassis.viewport.sizeDelta.y));
            Assert.That(weapons.verticalScrollbar, Is.Not.Null, "Overflow has an explicit reachable scrollbar.");
        }

        [Test] public void RepeatedNewEquipmentCanBeDrawnIndependentlyWithoutNewDrawingEnumValues()
        {
            Initialize(); Button("Chassis chassis.sample").onClick.Invoke();
            Button("Module module.sample").onClick.Invoke(); Button("Module module.sample").onClick.Invoke();
            Assert.That(hud.Draft.ModuleSelections.Count, Is.EqualTo(2));
            hud.DrawingEditor.Open(hud.Draft);
            var first = hud.Draft.ModuleSelections[0]; var second = hud.Draft.ModuleSelections[1];
            string firstBinding = DrawingBindings.Module(first.InstanceId), secondBinding = DrawingBindings.Module(second.InstanceId);
            Assert.That(root.GetComponentsInChildren<Button>(true).Any(b => b.name == "Draw part " + firstBinding), Is.False, "Equipment is not a functional drawing role.");
            Assert.That(hud.DrawingEditor.Session.AddStroke(firstBinding, new[] { new DrawingPoint(0, 0), new DrawingPoint(.1f, .3f) }, out _), Is.True);
            Assert.That(hud.DrawingEditor.Session.Snapshot.GetPart(secondBinding).Strokes, Is.Empty);
            FreeformUiTestDrawing.Complete(hud.DrawingEditor.Session); Button("Apply drawing").onClick.Invoke(); Button("Save blueprint as new").onClick.Invoke();
            Assert.That(run.Workshop.Album.Last().Drawing.GetPart(firstBinding).Strokes.Count, Is.EqualTo(1));
            Assert.That(run.Workshop.Album.Last().ModuleSelections.Select(value => value.InstanceId), Is.EquivalentTo(new[] { first.InstanceId, second.InstanceId }));
        }

        [Test] public void LegacyEquipmentMadeRepeatableByDataPreservesItsFirstBindingAndCreatesAnotherInstance()
        {
            Initialize(); Button("Chassis chassis.sample").onClick.Invoke();
            Button("Module Armor").onClick.Invoke(); Button("Module Armor").onClick.Invoke();
            Assert.That(hud.Draft.ModuleSelections.Count, Is.EqualTo(2));
            Assert.That(hud.Draft.ModuleSelections[0].InstanceId, Is.EqualTo("armor"));
            Assert.That(hud.Draft.ModuleSelections[1].InstanceId, Is.Not.EqualTo("armor"));
            Assert.That(hud.Draft.ModuleSelections.All(value => value.DefinitionId == LegacyContentIds.Module(TowerModules.Armor)), Is.True);
        }

        [Test] public void TrialDescriptionUsesPreparedDataRatherThanDuplicatedCombatNumbers()
        {
            Initialize(); Button("Open trial").onClick.Invoke();
            string text = string.Join("\n", root.GetComponentsInChildren<Text>(true).Select(value => value.text));
            Assert.That(text, Does.Contain("17 урона").And.Contain(3.5f.ToString("0.#") + " с").And.Contain(8.5f.ToString("0.#")));
        }

        [Test] public void PersistedAcceptanceManifestProvidesReachableCardsPricesUnlocksAndGenericSelections()
        {
            var manifest = AssetDatabase.LoadAssetAtPath<ContentAcceptanceManifestAsset>("Assets/PaperDefense/Content/Acceptance/ContentAcceptanceManifest.asset");
            if (manifest == null) Assert.Ignore("The separate data-only acceptance stage has not created its manifest yet.");
            Assert.That(manifest.preset, Is.Not.Null);
            var content = manifest.preset.CreateBalance().ResolveContent();
            var profile = new WorkshopProfile(content); profile.GrantGold(manifest.startingGold);
            Initialize(manifest.preset, profile); root.SetActive(true); Canvas.ForceUpdateCanvases();
            var baseline = content.GetChassis(manifest.baselineChassisId);
            root.GetComponentsInChildren<Button>(true).Single(value => value.name.StartsWith("Chassis ") &&
                value.GetComponentInChildren<Text>().text.StartsWith(baseline.ButtonLabel + " / ")).onClick.Invoke();
            var existing = content.GetWeapon(manifest.existingWeaponId);
            var existingCard = root.GetComponentsInChildren<Button>(true).Single(value => value.name.StartsWith("First ") &&
                value.GetComponentInChildren<Text>().text == existing.ButtonLabel);
            Reach(existingCard); existingCard.onClick.Invoke();
            int changedPrice = baseline.BaseCost + Mathf.Max(0, manifest.expectedExistingWeaponCost - existing.AssemblyCredit);
            Assert.That(run.PreviewBlueprint(hud.Draft).Cost, Is.EqualTo(changedPrice));
            Assert.That(string.Join("\n", root.GetComponentsInChildren<Text>(true).Select(value => value.text)), Does.Contain("Цена: " + changedPrice + " штр."));
            var weapon = content.GetWeapon(manifest.newWeaponId);
            var module = content.GetModule(manifest.newModuleId);
            var chassis = content.GetChassis(manifest.newChassisId);
            foreach (var part in new WorkshopPartDefinition[] { weapon, module, chassis })
            {
                if (part.InitiallyUnlocked) continue;
                var unlock = Button("Unlock " + part.Id);
                Reach(unlock);
                Assert.That(unlock.GetComponentInChildren<Text>().text, Does.Contain(part.UnlockLabel).And.Contain(part.UnlockGold + " золота"));
                int before = run.Workshop.Gold; unlock.onClick.Invoke();
                Assert.That(run.Workshop.IsUnlocked(part.Id), Is.True);
                Assert.That(before - run.Workshop.Gold, Is.EqualTo(part.UnlockGold));
            }
            Assert.That(weapon.UnlockGold, Is.EqualTo(manifest.expectedWeaponUnlockGold));
            var first = Button("First " + manifest.newWeaponId); Reach(first);
            Assert.That(first.GetComponentInChildren<Text>().text, Is.EqualTo(weapon.ButtonLabel)); first.onClick.Invoke();
            Assert.That(hud.Draft.FirstWeaponId, Is.EqualTo(manifest.newWeaponId));
            Assert.That(hud.Status, Is.EqualTo(weapon.Description));
            Assert.That(run.PreviewBlueprint(hud.Draft).Cost, Is.EqualTo(manifest.expectedAssemblyCost));
            Assert.That(string.Join("\n", root.GetComponentsInChildren<Text>(true).Select(value => value.text)), Does.Contain("Цена: " + manifest.expectedAssemblyCost + " штр."));
            var equipment = Button("Module " + manifest.newModuleId); Reach(equipment); equipment.onClick.Invoke();
            Assert.That(hud.Draft.ModuleSelections.Any(value => value.DefinitionId == manifest.newModuleId), Is.True);
            Assert.That(hud.Status, Is.EqualTo(module.Description));
            var foundation = Button("Chassis " + manifest.newChassisId); Reach(foundation); foundation.onClick.Invoke();
            Assert.That(foundation.GetComponentInChildren<Text>().text, Does.Contain(chassis.ButtonLabel).And.Contain(chassis.Capacity.ToString()));
            Assert.That(hud.Draft.ChassisId, Is.EqualTo(manifest.newChassisId));
            Assert.That(hud.Status, Is.EqualTo(chassis.Description));
        }

        [Test] public void ReplacingRemovedWeaponWithCatalogEntryRestoresItsDrawingEditor()
        {
            Initialize();
            var data=new WorkshopProfile().ToData();
            var line=new DrawingStroke(new[]{new DrawingPoint(-.1f,0),new DrawingPoint(.1f,.4f)});
            var missing=new TowerBlueprint(data.Album[0].Id,"Removed weapon page","chassis.standard","weapon.removed",
                drawing:new DrawingSnapshot(new[]{new DrawingPart(DrawingBindings.Base,new[]{line})}),origin:DrawingOrigin.PlayerDrawing);
            data.Album[0]=new BlueprintData(missing);
            run.InitializeWorkshop(new MemoryStore(WorkshopProfile.FromData(data)));
            Button("Album drawing 0").onClick.Invoke();
            Assert.That(hud.Draft.FirstWeaponId,Is.EqualTo("weapon.removed"));
            Button("First weapon.sample.19").onClick.Invoke();
            Assert.That(hud.Draft.Content,Is.SameAs(run.Content));
            Assert.That(DrawingTemplateCatalog.Create(hud.Draft).Count,Is.GreaterThanOrEqualTo(2));
            var evaluated=new TowerBlueprint(hud.Draft.Id,hud.Draft.Name,hud.Draft.ChassisId,hud.Draft.FirstWeaponId,
                drawing:hud.Draft.Drawing,origin:DrawingOrigin.PlayerDrawing,content:run.Content);
            Assert.That(hud.Draft.Assessment.Quality,Is.EqualTo(evaluated.Assessment.Quality));
            Assert.That(hud.Draft.Assessment.Quality,Is.GreaterThan(0),"Retained base lines use the current template when repairing a removed weapon.");
            hud.DrawingEditor.Open(hud.Draft);
            Assert.That(hud.DrawingEditor.Session.AddStroke(DrawingBindings.Mount0,
                new[]{new DrawingPoint(-.1f,0),new DrawingPoint(.1f,.4f)},out var reason),Is.True,reason);
            FreeformUiTestDrawing.Complete(hud.DrawingEditor.Session); Button("Apply drawing").onClick.Invoke();
            Assert.That(hud.Draft.Drawing.GetPart(DrawingBindings.Mount0).Strokes.Count,Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void EmptyAndPartiallyAssignedStartupPagesRenderAndCanBeFilledInWorkshop(bool assignOne)
        {
            var database = ScriptableObject.CreateInstance<ContentDatabaseAsset>(); owned.Add(database);
            var source = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/PaperDefense/Configuration/Content/NotebookContent.asset");
            Assert.That(source, Is.Not.Null);
            database.catalog = source.catalog.CreateCopy();
            var starter = database.catalog.StartingPages[0];
            database.catalog.StartingPages.Clear();
            if (assignOne) { starter.Slot = 2; database.catalog.StartingPages.Add(starter); }
            var preset = ScriptableObject.CreateInstance<BalancePresetAsset>(); owned.Add(preset);
            preset.database = database; preset.rules = GlobalBalanceSettings.From(GameBalance.CreateDefault());
            Assert.DoesNotThrow(() => Initialize(preset));
            var input = root.AddComponent<DrawingInput>(); input.Initialize(run, null, null);
            var notebook = root.AddComponent<NotebookHud>();
            Assert.DoesNotThrow(() => notebook.Initialize(run, input));
            Assert.That(Button("Assign slot 0").GetComponentInChildren<Text>().text, Does.Contain("пусто"));
            for (int slot = 0; slot < ProfileLimits.LoadoutSlotCount; slot++)
                Assert.That(Button("Tool " + slot).interactable, Is.EqualTo(assignOne && slot == 2));
            Assert.DoesNotThrow(() => Button("Tool 0").onClick.Invoke());
            Assert.That(input.BlueprintSlot, Is.EqualTo(-1));
            Assert.That(input.Message, Does.Contain("Назначь чертёж"));
            var workshopSession = run.Session;
            Button("Start run").onClick.Invoke();
            Assert.That(run.InWorkshop, Is.True);
            Assert.That(run.Session, Is.SameAs(workshopSession));
            Assert.That(run.Workshop.Album.Count, Is.EqualTo(assignOne ? 1 : 0));
            Assert.That(hud.Status, Does.Contain("Выберите чертёж"));

            Button("New blueprint").onClick.Invoke();
            Button("Open drawing editor").onClick.Invoke();
            Assert.That(hud.DrawingEditor.Session.AddStroke(DrawingBindings.Mount0,
                new[] { new DrawingPoint(-.1f, 0), new DrawingPoint(.1f, .3f) }, out var reason), Is.True, reason);
            FreeformUiTestDrawing.Complete(hud.DrawingEditor.Session); Button("Apply drawing").onClick.Invoke();
            Button("Save blueprint").onClick.Invoke();
            Assert.That(run.Workshop.Album.Count, Is.EqualTo(assignOne ? 2 : 1), hud.Status);
            Button("Assign slot 0").onClick.Invoke();
            input.SelectSlot(0);
            Assert.That(run.Workshop.Loadout[0].Id, Is.EqualTo(hud.Draft.Id));
            Assert.That(Button("Tool 0").interactable, Is.True);
            Assert.That(input.BlueprintSlot, Is.EqualTo(0));

            var empty = new WorkshopProfile(database.CreateSnapshot());
            if (assignOne) Assert.That(empty.GetLoadoutBlueprints()[2], Is.Not.Null);
            Assert.DoesNotThrow(() => run.InitializeWorkshop(new MemoryStore(empty)));
            Assert.That(input.BlueprintSlot, Is.EqualTo(-1));
            Assert.That(input.Tool, Is.EqualTo(StructureKind.Wall));
            Assert.That(Button("Tool 0").interactable, Is.False);
        }

        static void Reach(Button button)
        {
            var scroll = button.GetComponentInParent<ScrollRect>(); Assert.That(scroll, Is.Not.Null);
            var target = button.GetComponent<RectTransform>();
            float overflow = scroll.content.rect.height - scroll.viewport.rect.height;
            if (overflow > 0)
            {
                Assert.That(scroll.verticalScrollbar, Is.Not.Null);
                float distance = -target.anchoredPosition.y + target.rect.height * .5f - scroll.viewport.rect.height * .5f;
                scroll.verticalNormalizedPosition = 1 - Mathf.Clamp01(distance / overflow);
                Canvas.ForceUpdateCanvases();
            }
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, target);
            Assert.That(bounds.center.y, Is.InRange(scroll.viewport.rect.yMin, scroll.viewport.rect.yMax), "The catalogue card is reachable inside its viewport.");
        }

        sealed class MemoryStore : IWorkshopProfileStore
        {
            WorkshopProfile profile;
            public MemoryStore(WorkshopProfile profile) { this.profile = profile; }
            public ProfileLoadResult Load() => new ProfileLoadResult(profile, ProfileLoadSource.Primary, "Isolated acceptance UI profile", true);
            public ProfileSaveResult Save(WorkshopProfile candidate) { profile = candidate; return new ProfileSaveResult(true, ""); }
        }
    }
}
