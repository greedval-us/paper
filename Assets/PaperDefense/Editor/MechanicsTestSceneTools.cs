using System;
using System.Collections.Generic;
using System.IO;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PaperDefense.Editor
{
    public static class MechanicsTestSceneTools
    {
        public const string SceneRoot = "Assets/PaperDefense/Scenes/Tests";
        public const string DataRoot = "Assets/PaperDefense/Configuration/Tests";
        public static readonly string[] SceneNames = {
            "01_Assembly", "02_Drawing", "03_Weapons/Gun", "03_Weapons/Piercer", "03_Weapons/Mortar",
            "03_Weapons/Fan", "03_Weapons/Ricochet", "03_Weapons/Stapler", "03_Weapons/Blotter",
            "04_Modules/Armor", "04_Modules/Feed", "04_Modules/Tracking", "04_Modules/Rail",
            "05_EnemyDrawingTiers", "06_RangedEnemies", "07_CorpseBlots", "08_InkWells",
            "09_ColorCartridges", "10_ConstructionNavigation", "11_WavesEconomyUpgrades",
            "12_ProfilePersistence", "13_Integration", "14_Pressure3000"
        };
        public static readonly string[] ScenarioIds = {
            "assembly", "drawing", "gun", "piercer", "mortar", "fan", "ricochet", "stapler", "blotter",
            "armor", "feed", "tracking", "rail", "enemy_tiers", "ranged", "blots", "wells", "color",
            "construction", "economy", "profile", "integration", "pressure"
        };

        // This technical menu selector remains stable; visible scene instructions use editor.* resource keys.
        [MenuItem("Paper Defense/Validation/Generate missing mechanics scenes")]
        public static void GenerateMissing()
        {
            var source = AssetDatabase.LoadAssetAtPath<BalancePresetAsset>("Assets/PaperDefense/Configuration/Content/Target.asset");
            var text = AssetDatabase.LoadAssetAtPath<TextResourcesAsset>("Assets/PaperDefense/Resources/Text/ru.asset");
            if (source == null || text == null) throw new InvalidOperationException("mechanics.generator: source preset/text resource missing.");
            source.CreateBalance(); text.CreateResolver();
            Scene active = SceneManager.GetActiveScene();
            EnsureFolder(SceneRoot); EnsureFolder(DataRoot);
            try
            {
                for (int i = 0; i < SceneNames.Length; i++)
                {
                    string scenePath = SceneRoot + "/" + SceneNames[i] + ".unity";
                    EnsureFolder(Path.GetDirectoryName(scenePath).Replace('\\', '/'));
                    string folder = DataRoot + "/" + ScenarioIds[i]; EnsureFolder(folder);
                    var scenario = AssetDatabase.LoadAssetAtPath<MechanicsTestScenarioAsset>(folder + "/Scenario.asset");
                    if (scenario == null) scenario = CreateScenario((MechanicsTestFocus)i, source, text, folder);
                    if (File.Exists(scenePath)) continue; // Existing authored scenes/assets are never overwritten.
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    try
                    {
                        var root = new GameObject("Mechanics Test - " + ScenarioIds[i], typeof(MechanicsTestScene));
                        SceneManager.MoveGameObjectToScene(root, scene);
                        root.GetComponent<MechanicsTestScene>().scenario = scenario;
                        if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new IOException("mechanics.generator: scene save failed.");
                    }
                    finally { EditorSceneManager.CloseScene(scene, true); }
                }
                AssetDatabase.SaveAssets();
                Debug.Log(text.CreateResolver().Resolve(UserText.Message("editor.mechanics.generated", SceneNames.Length)));
            }
            finally { if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); }
        }

        static MechanicsTestScenarioAsset CreateScenario(MechanicsTestFocus focus, BalancePresetAsset source,
            TextResourcesAsset text, string folder)
        {
            var database = ScriptableObject.CreateInstance<ContentDatabaseAsset>();
            database.catalog = source.database.catalog.CreateCopy();
            Apply(database.catalog, source.overrides);
            foreach (var chassis in database.catalog.Chassis) { chassis.Enabled = true; chassis.InitiallyUnlocked = true; }
            foreach (var weapon in database.catalog.Weapons) { weapon.Enabled = true; weapon.InitiallyUnlocked = true; }
            foreach (var module in database.catalog.Modules) { module.Enabled = true; module.InitiallyUnlocked = true; }
            database.catalog.StartingPages.Clear();
            for (int slot = 0; slot < 4; slot++) database.catalog.StartingPages.Add(new StartingBlueprintDefinitionInput {
                Id = "fixture.start." + slot, Name = text.CreateResolver().Resolve(UserText.Message("test.blueprint_name", slot + 1)),
                ChassisId = "chassis.standard", FirstWeaponId = "weapon.gun", Slot = slot
            });
            AssetDatabase.CreateAsset(database, folder + "/Catalog.asset");
            var preset = ScriptableObject.CreateInstance<BalancePresetAsset>();
            preset.database = database; preset.rules = GlobalBalanceSettings.From(source.CreateBalance());
            var rules = preset.rules;
            rules.Width = 48; rules.Height = 32; rules.StartingInk = 600;
            rules.PlanningSeconds = 3600; rules.IntermissionSeconds = 3600;
            rules.PassiveInkPerSecond = 0; rules.EarlyLaunchInkPerSecond = 0; rules.MaxEarlyLaunchInk = 0;
            rules.Mechanics = None(); rules.EnableEnemyMechanics = false; rules.EnableInkWells = false;
            rules.Waves = new List<WaveDefinition> { new WaveDefinition {
                Count = 12, SpawnInterval = .3f, Sides = 1, Title = text.CreateResolver().Resolve(UserText.Message("test.wave_name")), InkGrant = 0
            } };
            if (focus == MechanicsTestFocus.Economy)
            {
                rules.Waves = new List<WaveDefinition> {
                    new WaveDefinition { Count = 3, SpawnInterval = .2f, Sides = 1, Title = text.CreateResolver().Resolve(UserText.Message("test.wave_name")), InkGrant = 20 },
                    new WaveDefinition { Count = 5, SpawnInterval = .2f, Sides = 1, Title = text.CreateResolver().Resolve(UserText.Message("test.wave_name")), InkGrant = 20 }
                };
                rules.Progression.UpgradeEveryWaves = 1; rules.Progression.LastUpgradeWave = 1;
                foreach (var enemy in rules.EnemyDefinitions) { enemy.MaxHealth = 12; enemy.Speed = 3; }
            }
            var scenario = ScriptableObject.CreateInstance<MechanicsTestScenarioAsset>();
            scenario.scenarioId = ScenarioIds[(int)focus]; scenario.focus = focus;
            scenario.titleKey = "test.scene." + scenario.scenarioId + ".title";
            scenario.instructionsKey = "test.scene." + scenario.scenarioId + ".instructions";
            scenario.preset = preset; scenario.textResources = text; scenario.mechanics = None();
            scenario.workshopMode = focus == MechanicsTestFocus.Assembly || focus == MechanicsTestFocus.Drawing ||
                focus == MechanicsTestFocus.Profile || focus == MechanicsTestFocus.Integration;
            Configure(scenario);
            rules.Mechanics = scenario.mechanics.CreateSnapshot();
            rules.EnableEnemyMechanics = scenario.mechanics.EnemyDrawingTiers || scenario.mechanics.CorpseBlots || scenario.mechanics.RangedEnemies;
            rules.EnableInkWells = scenario.mechanics.InkWells;
            rules.StartingInk = scenario.startingInk;
            if (focus == MechanicsTestFocus.Integration || focus == MechanicsTestFocus.Pressure)
            { preset.rules = GlobalBalanceSettings.From(source.CreateBalance()); preset.rules.Mechanics = scenario.mechanics.CreateSnapshot(); }
            scenario.observationKeys = new[] { "test.observe." + scenario.scenarioId };
            AssetDatabase.CreateAsset(preset, folder + "/Preset.asset");
            AssetDatabase.CreateAsset(scenario, folder + "/Scenario.asset");
            scenario.CreateBalance();
            return scenario;
        }

        static void Configure(MechanicsTestScenarioAsset s)
        {
            var f = s.focus;
            if (f >= MechanicsTestFocus.Gun && f <= MechanicsTestFocus.Blotter)
            {
                string id = "weapon." + s.scenarioId;
                s.placements.Add(Placement(id));
                if (f == MechanicsTestFocus.Blotter)
                {
                    s.mechanics.CorpseBlots = true;
                    s.targets.Add(Target(40.5f, 25.5f, 100000));
                    s.blots.Add(Blot(24, 16)); s.blots.Add(Blot(25, 16)); s.blots.Add(Blot(23, 17));
                }
                else if (f == MechanicsTestFocus.Mortar)
                {
                    s.targets.Add(Target(21.5f, 16.5f));
                    s.targets.Add(Target(30.5f, 16.5f)); s.targets.Add(Target(31.5f, 17));
                }
                else
                {
                    s.targets.Add(Target(23.5f, 16.5f));
                    s.targets.Add(Target(f == MechanicsTestFocus.Piercer ? 25.5f : 24.5f, 16.5f));
                    s.targets.Add(Target(f == MechanicsTestFocus.Piercer ? 27.5f : 23.5f,
                        f == MechanicsTestFocus.Piercer ? 16.5f : 17.5f));
                    if (f == MechanicsTestFocus.Stapler) s.targets[0].stationary = false;
                }
            }
            switch (f)
            {
                case MechanicsTestFocus.Assembly: break;
                case MechanicsTestFocus.Drawing:
                    s.mechanics.DrawingQuality = true;
                    s.placements.Add(Placement("weapon.gun", "chassis.battery", new[] { "weapon.gun", "weapon.piercer", "weapon.mortar" }));
                    break;
                case MechanicsTestFocus.Armor:
                case MechanicsTestFocus.Tracking:
                case MechanicsTestFocus.Rail:
                    var plain = Placement("weapon.gun"); plain.cell = new Vector2Int(18, 13); s.placements.Add(plain);
                    var modified = Placement("weapon.gun"); modified.cell = new Vector2Int(18, 19);
                    modified.moduleIds = new[] { "module." + s.scenarioId }; s.placements.Add(modified);
                    if (f == MechanicsTestFocus.Armor)
                    {
                        s.mechanics.RangedEnemies = true;
                        s.targets.Add(Target(12.5f, 10.5f, 1000, true, EnemyKind.Spitter));
                        s.targets.Add(Target(12.5f, 22.5f, 1000, true, EnemyKind.Spitter));
                    }
                    else if (f == MechanicsTestFocus.Rail)
                    { s.targets.Add(Target(28, 13.5f)); s.targets.Add(Target(28, 19.5f)); }
                    else
                    {
                        s.targets.Add(Target(23.5f, 13.5f, 8)); s.targets.Add(Target(24.5f, 13.5f, 8));
                        s.targets.Add(Target(23.5f, 19.5f, 8)); s.targets.Add(Target(24.5f, 19.5f, 8));
                    }
                    break;
                case MechanicsTestFocus.Feed:
                    var feed = Placement("weapon.gun", "chassis.battery", new[] { "weapon.gun", "weapon.gun", "weapon.gun" });
                    feed.moduleIds = new[] { "module.feed" }; s.placements.Add(feed);
                    s.targets.Add(Target(23.5f, 15.5f, 100000)); s.targets.Add(Target(23.5f, 17.5f, 100000));
                    break;
                case MechanicsTestFocus.EnemyTiers:
                    s.mechanics.EnemyDrawingTiers = true;
                    for (int i = 0; i < 3; i++)
                    {
                        var tierGun = Placement("weapon.gun"); tierGun.cell = new Vector2Int(18, 8 + i * 8); s.placements.Add(tierGun);
                        var target = Target(23.5f, 8.5f + i * 8); target.drawingTier = (DrawingTier)i; s.targets.Add(target);
                    }
                    break;
                case MechanicsTestFocus.Ranged:
                    s.mechanics.RangedEnemies = true; s.placements.Add(Placement("weapon.gun"));
                    s.targets.Add(Target(10.5f, 16.5f, 1000, true, EnemyKind.Spitter));
                    break;
                case MechanicsTestFocus.Blots:
                    s.mechanics.CorpseBlots = true; s.placements.Add(Placement("weapon.gun"));
                    s.targets.Add(Target(23.5f, 16.5f, 8)); s.targets.Add(Target(23.5f, 17.5f, 500, false));
                    s.blots.Add(Blot(24, 16)); s.blots.Add(Blot(25, 16));
                    break;
                case MechanicsTestFocus.Wells: s.mechanics.InkWells = true; s.placements.Add(Placement("weapon.gun")); break;
                case MechanicsTestFocus.Color:
                    s.mechanics.ColorCartridges = true;
                    s.placements.Add(Placement("weapon.gun", "chassis.battery", new[] { "weapon.gun", "weapon.gun", "weapon.gun" }));
                    for (int i = 0; i < 5; i++) s.targets.Add(Target(23.5f, 14.5f + i, 1000));
                    break;
                case MechanicsTestFocus.Construction: s.placements.Add(Placement("weapon.gun")); break;
                case MechanicsTestFocus.Economy:
                    s.mechanics.RunUpgrades = true;
                    var economy = Placement("weapon.gun"); economy.cell = new Vector2Int(18, 16); s.placements.Add(economy);
                    break;
                case MechanicsTestFocus.Profile: break;
                case MechanicsTestFocus.Integration: s.mechanics = new MechanicsConfiguration(); s.startingInk = 720; break;
                case MechanicsTestFocus.Pressure: s.mechanics = None(); s.startingInk = 20000; s.startPaused = false; break;
            }
        }
        static MechanicsPlacement Placement(string weapon, string chassis = "chassis.standard", string[] weapons = null)
            => new MechanicsPlacement { chassisId = chassis, weaponIds = weapons ?? new[] { weapon } };
        static MechanicsTarget Target(float x, float y, float hp = 500, bool stationary = true, EnemyKind kind = EnemyKind.Doodle)
            => new MechanicsTarget { position = new Vector2(x, y), health = hp, stationary = stationary, kind = kind };
        static MechanicsBlot Blot(int x, int y) => new MechanicsBlot { cell = new Vector2Int(x, y) };
        static MechanicsConfiguration None() => new MechanicsConfiguration {
            EnemyDrawingTiers = false, CorpseBlots = false, RangedEnemies = false, InkWells = false,
            ColorCartridges = false, RunUpgrades = false, DrawingQuality = false
        };
        static void Apply(ContentCatalogInput catalog, ContentOverrides overrides)
        {
            var owned = new ContentCatalogInput { Chassis = overrides.Chassis, Weapons = overrides.Weapons,
                Modules = overrides.Modules, Effects = overrides.Effects, Templates = overrides.Templates }.CreateCopy();
            Replace(catalog.Chassis, owned.Chassis, x => x.Id); Replace(catalog.Weapons, owned.Weapons, x => x.Id);
            Replace(catalog.Modules, owned.Modules, x => x.Id); Replace(catalog.Effects, owned.Effects, x => x.Id);
            Replace(catalog.Templates, owned.Templates, x => x.Id);
        }
        static void Replace<T>(List<T> target, List<T> overrides, Func<T, string> id)
        { foreach (var item in overrides) { int index = target.FindIndex(x => id(x) == id(item)); if (index < 0) throw new ArgumentException("mechanics: unknown override."); target[index] = item; } }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
