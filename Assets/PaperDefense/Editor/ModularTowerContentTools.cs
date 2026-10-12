using System;
using System.Collections.Generic;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Editor
{
    /// <summary>Explicit, repeatable migration of the active data preset. Historical numbers are retained.</summary>
    public static class ModularTowerContentTools
    {
        const string ContentPath = "Assets/PaperDefense/Configuration/Content/NotebookContent.asset";
        const string TargetPath = "Assets/PaperDefense/Configuration/Content/Target.asset";

        [MenuItem("Paper Defense/Content/Apply modular tower preset")]
        public static void Apply()
        {
            var source = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(ContentPath);
            var target = AssetDatabase.LoadAssetAtPath<BalancePresetAsset>(TargetPath);
            if (source == null || target == null) throw new InvalidOperationException("Modular tower content sources missing.");
            var catalog = source.catalog.CreateCopy();
            var overrides = JsonUtility.FromJson<ContentOverrides>(JsonUtility.ToJson(target.overrides));
            var text = TextResourcesAsset.Default;
            string Copy(string key) => text.Resolve(UserText.Message(key));

            // All four existing module combinations remain legal in Historical, subject to load.
            foreach (var chassis in catalog.Chassis)
            {
                if (chassis.Id != "chassis.standard" && chassis.Id != "chassis.heavy") continue;
                chassis.UpgradeSlotCount = 3;
                chassis.ArmorSlotCount = 1;
                var active = Clone(chassis);
                active.UpgradeSlotCount = 2;
                active.FeedCapacity = 1.6f;
                Replace(overrides.Chassis, active, item => item.Id);
            }
            foreach (var module in catalog.Modules)
                module.SlotKind = module.Id == "module.armor" ? ModuleSlotKind.Armor : ModuleSlotKind.Upgrade;
            foreach (var module in overrides.Modules)
                module.SlotKind = module.Id == "module.armor" ? ModuleSlotKind.Armor : ModuleSlotKind.Upgrade;

            AddBase(catalog, overrides, "precision", 1, 3, 16, 20, 1f, .25f, 20, Copy);
            AddBase(catalog, overrides, "battery", 3, 1, 22, 34, 1.8f, .65f, 21, Copy);
            SetBand(catalog, overrides, "weapon.gun", 0, 8);
            SetBand(catalog, overrides, "weapon.piercer", 0, 12);
            SetBand(catalog, overrides, "weapon.mortar", 6, 18);

            // Validate independent candidates before publishing either asset's changed data.
            using (var check = new Candidate(source, target, catalog, overrides)) check.Preset.CreateBalance();
            source.catalog = catalog;
            target.overrides = overrides;
            EditorUtility.SetDirty(source); EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(source); AssetDatabase.SaveAssetIfDirty(target);
        }

        static void AddBase(ContentCatalogInput catalog, ContentOverrides overrides, string suffix, int mounts,
            int upgrades, int capacity, int price, float feed, float build, int order, Func<string,string> text)
        {
            string id = "chassis." + suffix, template = "template." + id;
            var chassis = new ChassisDefinitionInput
            {
                Id = id, Name = text("content." + id + ".name"),
                Description = text("content." + id + ".description"),
                ButtonLabel = text("content." + id + ".button"), UnlockLabel = text("content." + id + ".button"),
                Enabled = false, InitiallyUnlocked = false, UnlockGold = 90, Order = order,
                TemplateId = template, VisualStyle = DrawingVisualStyle.Template,
                Capacity = capacity, BaseCost = price, MountCount = mounts,
                UpgradeSlotCount = upgrades, ArmorSlotCount = 1, FeedCapacity = feed,
                BaseHealth = 55, HealthMultiplier = 1, BuildSeconds = build, SecondaryBuildSeconds = .4f,
                PrimaryMountPivotY = .04f, PrimaryDualMountPivotX = -.17f, PrimaryDualMountPivotY = .04f,
                SecondaryMountPivotX = .17f, SecondaryMountPivotY = .04f
            };
            Replace(catalog.Chassis, chassis, item => item.Id);
            var active = Clone(chassis); active.Enabled = true;
            if (mounts == 3)
            {
                active.MountPivots.Add(new MountPivotInput { SlotIndex = 0, X = -.24f, Y = .04f });
                active.MountPivots.Add(new MountPivotInput { SlotIndex = 1, X = 0, Y = .04f });
                active.MountPivots.Add(new MountPivotInput { SlotIndex = 2, X = .24f, Y = .04f });
            }
            Replace(overrides.Chassis, active, item => item.Id);
            var shape = new DrawingTemplateDefinitionInput
            {
                Id = template, DisplayName = chassis.Name, Revision = 1, Weight = 25,
                Contours = new List<DrawingContourInput>()
            };
            shape.Contours.Add(Contour(-.43f,-.32f, .43f,-.32f, .49f,.06f, .28f,.36f, -.28f,.36f, -.49f,.06f, -.43f,-.32f));
            if (mounts == 1)
            {
                shape.Contours.Add(Contour(-.22f,-.22f, -.22f,.24f, .22f,.24f, .22f,-.22f));
                shape.Contours.Add(Contour(-.33f,-.1f, .33f,-.1f));
            }
            else
            {
                shape.Contours.Add(Contour(-.4f,.02f, .4f,.02f));
                shape.Contours.Add(Contour(-.28f,-.25f, -.28f,.25f));
                shape.Contours.Add(Contour(0,-.25f, 0,.25f));
                shape.Contours.Add(Contour(.28f,-.25f, .28f,.25f));
            }
            Replace(catalog.Templates, shape, item => item.Id);
        }

        static DrawingContourInput Contour(params float[] coordinates)
        {
            var result = new DrawingContourInput();
            for (int i = 0; i < coordinates.Length; i += 2)
                result.Points.Add(new DrawingCoordinateInput(coordinates[i], coordinates[i+1]));
            return result;
        }

        static void SetBand(ContentCatalogInput catalog, ContentOverrides overrides, string id, float minimum, float maximum)
        {
            var weapon = Clone(overrides.Weapons.FirstOrDefault(item => item.Id == id) ?? catalog.Weapons.Single(item => item.Id == id));
            weapon.MinimumRange = minimum; weapon.Stats.Range = maximum;
            Replace(overrides.Weapons, weapon, item => item.Id);
        }
        static T Clone<T>(T source) => JsonUtility.FromJson<T>(JsonUtility.ToJson(source));
        static void Replace<T>(List<T> list, T item, Func<T,string> id)
        {
            int index = list.FindIndex(value => id(value) == id(item));
            if (index < 0) list.Add(item); else list[index] = item;
        }

        sealed class Candidate : IDisposable
        {
            readonly ContentDatabaseAsset content;
            internal readonly BalancePresetAsset Preset;
            internal Candidate(ContentDatabaseAsset source, BalancePresetAsset preset, ContentCatalogInput catalog, ContentOverrides overrides)
            {
                content = ScriptableObject.CreateInstance<ContentDatabaseAsset>(); content.catalog = catalog;
                Preset = ScriptableObject.CreateInstance<BalancePresetAsset>();
                Preset.database = content; Preset.rules = Clone(preset.rules); Preset.overrides = overrides;
            }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(Preset); UnityEngine.Object.DestroyImmediate(content); }
        }
    }
}
