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
        }

        Button Button(string name) => root.GetComponentsInChildren<Button>(true).Single(value => value.name == name);

        [Test] public void HistoricalRemovedWeaponResolvesAgainstCurrentCatalogWithoutLosingRetainedBaseLines()
        {
            Initialize();
            var data=HistoricalProfileTestData.Create(new WorkshopProfile(),7);
            var line=new DrawingStroke(new[]{new DrawingPoint(-.1f,0),new DrawingPoint(.1f,.4f)});
            var missing=new TowerBlueprint(data.Album[0].Id,"Removed weapon page","chassis.standard","weapon.removed",
                drawing:new DrawingSnapshot(new[]{new DrawingPart(DrawingBindings.Base,new[]{line})}),origin:DrawingOrigin.PlayerDrawing);
            data.Album[0]=new BlueprintData(missing);
            run.InitializeWorkshop(new MemoryStore(WorkshopProfile.FromData(data)));
            var restored=run.Workshop.Album.Single(page=>page.Id==missing.Id);
            Assert.That(restored.FirstWeaponId,Is.EqualTo("weapon.removed"));
            string retained=restored.Drawing.GeometryHash;
            var repaired=restored.WithContent(run.Content).WithWeaponMount(0,"weapon.sample.19");
            Assert.That(repaired.Content,Is.SameAs(run.Content));
            Assert.That(repaired.Drawing.GetPart(DrawingBindings.Base).Strokes.Count,Is.EqualTo(1));
            Assert.That(restored.Drawing.GeometryHash,Is.EqualTo(retained),"Resolving current content must not mutate the historical page.");
            Assert.That(DrawingTemplateCatalog.Create(repaired).Count,Is.GreaterThanOrEqualTo(2));
            var evaluated=new TowerBlueprint(repaired.Id,repaired.Name,repaired.ChassisId,repaired.FirstWeaponId,
                drawing:repaired.Drawing,origin:DrawingOrigin.PlayerDrawing,content:run.Content);
            Assert.That(repaired.Assessment.Quality,Is.EqualTo(evaluated.Assessment.Quality));
            Assert.That(repaired.Assessment.Quality,Is.GreaterThan(0));
        }

        [TestCase(false)] [TestCase(true)]
        public void HistoricalEmptyAndPartiallyAssignedStartupPagesKeepConstructionInputSafe(bool assignOne)
        {
            var database=ScriptableObject.CreateInstance<ContentDatabaseAsset>();owned.Add(database);
            var source=AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/PaperDefense/Configuration/Content/NotebookContent.asset");
            Assert.That(source,Is.Not.Null);database.catalog=source.catalog.CreateCopy();
            var starter=database.catalog.StartingPages[0];database.catalog.StartingPages.Clear();
            if(assignOne){starter.Slot=2;database.catalog.StartingPages.Add(starter);}
            var preset=ScriptableObject.CreateInstance<BalancePresetAsset>();owned.Add(preset);
            preset.database=database;preset.rules=GlobalBalanceSettings.From(GameBalance.CreateDefault());
            Assert.DoesNotThrow(()=>Initialize(preset));
            var input=root.AddComponent<DrawingInput>();input.Initialize(run,null,null);
            var notebook=root.AddComponent<NotebookHud>();Assert.DoesNotThrow(()=>notebook.Initialize(run,input));
            for(int slot=0;slot<ProfileLimits.LoadoutSlotCount;slot++)
                Assert.That(Button("Tool "+slot).interactable,Is.EqualTo(assignOne&&slot==2));
            Assert.DoesNotThrow(()=>Button("Tool 0").onClick.Invoke());
            Assert.That(input.BlueprintSlot,Is.EqualTo(-1));Assert.That(input.Message,Does.Contain("Назначь чертёж"));
            var session=run.Session;bool started=run.StartRun(out _);
            Assert.That(started,Is.EqualTo(assignOne));Assert.That(run.InWorkshop,Is.EqualTo(!assignOne));
            if(assignOne){Assert.That(run.Session,Is.Not.SameAs(session));run.OpenWorkshop();}
            else Assert.That(run.Session,Is.SameAs(session));
            Assert.That(run.Workshop.Album.Count,Is.EqualTo(assignOne?1:0));
            var empty=new WorkshopProfile(database.CreateSnapshot());
            if(assignOne)Assert.That(empty.GetLoadoutBlueprints()[2],Is.Not.Null);
            Assert.DoesNotThrow(()=>run.InitializeWorkshop(new MemoryStore(empty)));
            Assert.That(input.BlueprintSlot,Is.EqualTo(-1));Assert.That(input.Tool,Is.EqualTo(StructureKind.Wall));
            Assert.That(Button("Tool 0").interactable,Is.False);
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
