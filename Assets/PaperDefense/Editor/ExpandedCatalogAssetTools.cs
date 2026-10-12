using System;
using System.Collections.Generic;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Editor
{
    public static class ExpandedCatalogAssetTools
    {
        static readonly StructureKind[] ExpandedKinds =
            { StructureKind.Fan, StructureKind.Ricochet, StructureKind.Stapler, StructureKind.Blotter };
        static readonly string[] AssetPaths =
            { "Assets/PaperDefense/Configuration/NotebookBalance.asset", "Assets/PaperDefense/Configuration/TargetNotebookBalance.asset" };

        public static int EnsureExpandedCatalog(GameBalanceAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            var existing = asset.weapons ?? Array.Empty<GameBalanceAsset.WeaponSettings>();
            var weapons = new List<GameBalanceAsset.WeaponSettings>(existing);
            var present = new HashSet<StructureKind>();
            foreach (var weapon in existing) if (weapon != null) present.Add(weapon.kind);
            var defaults = GameBalance.CreateDefault();
            int added = 0;
            foreach (var kind in ExpandedKinds)
            {
                if (present.Contains(kind)) continue;
                var definition = defaults.Structure(kind);
                weapons.Add(new GameBalanceAsset.WeaponSettings
                {
                    kind = kind, cost = definition.Cost, health = definition.MaxHealth,
                    buildSeconds = definition.BuildSeconds, range = definition.Range, damage = definition.Damage,
                    shotInterval = definition.ShotInterval, radius = definition.Radius, flightSeconds = definition.FlightSeconds,
                    pierce = definition.MaxPierce, coneAngleDegrees = definition.ConeAngleDegrees,
                    maxTargets = definition.MaxTargets, jumpRange = definition.JumpRange,
                    slowSpeedMultiplier = definition.SlowSpeedMultiplier, slowDuration = definition.SlowDuration
                });
                added++;
            }
            if (added > 0) asset.weapons = weapons.ToArray();
            if (asset.construction == null) asset.construction = new TowerConstructionRules();
            return added;
        }

        public static string EnsureExpandedCatalogAssets()
        {
            // Load both before changing either, so a missing asset cannot leave a partial update.
            var assets = new GameBalanceAsset[AssetPaths.Length];
            for (int i = 0; i < assets.Length; i++)
            {
                assets[i] = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(AssetPaths[i]);
                if (assets[i] == null) throw new InvalidOperationException("Balance asset not found: " + AssetPaths[i]);
            }
            var report = new ExpansionReport { assets = new AssetExpansion[assets.Length] };
            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                var original = asset.weapons ?? Array.Empty<GameBalanceAsset.WeaponSettings>();
                var serialized = new string[original.Length];
                for (int j = 0; j < original.Length; j++) serialized[j] = JsonUtility.ToJson(original[j]);
                int added = EnsureExpandedCatalog(asset);
                bool preserved = true;
                for (int j = 0; j < original.Length; j++)
                    preserved &= ReferenceEquals(original[j], asset.weapons[j]) && serialized[j] == JsonUtility.ToJson(asset.weapons[j]);
                if (!preserved) throw new InvalidOperationException("Existing weapon settings changed: " + AssetPaths[i]);
                // Saving the loaded object also serializes newly introduced construction fields.
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                report.assets[i] = new AssetExpansion
                {
                    path = AssetPaths[i], weaponsBefore = original.Length, weaponsAfter = asset.weapons.Length,
                    added = added, existingWeaponsPreserved = preserved, targetPreset = asset.useTargetWorkshopPreset
                };
            }
            return JsonUtility.ToJson(report);
        }

        [Serializable] sealed class ExpansionReport { public AssetExpansion[] assets; }
        [Serializable] sealed class AssetExpansion
        {
            public string path;
            public int weaponsBefore, weaponsAfter, added;
            public bool existingWeaponsPreserved, targetPreset;
        }
    }
}
