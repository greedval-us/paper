using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Editor
{
    /// <summary>Read-only runtime export for the reproducible mechanics/balance workbook.</summary>
    public static partial class BalanceWorkbookExport
    {
        [MenuItem("Paper Defense/Export mechanics and balance JSON")]
        public static void ExportFromMenu() => Debug.Log(Export());

        public static string Export()
        {
            ExportPlayerAssembly();
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            var configurations = new List<ConfigurationRecord>();
            foreach (string assetPath in new[] {
                "Assets/PaperDefense/Configuration/NotebookBalance.asset",
                "Assets/PaperDefense/Configuration/TargetNotebookBalance.asset" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(assetPath);
                if (asset == null) throw new FileNotFoundException("Balance asset not found", assetPath);
                var balance = asset.CreateWorkshopBalance();
                BalanceValidation.Validate(balance);
                configurations.Add(CreateConfiguration(assetPath, asset, balance));
            }
            var sourcePaths = new List<string> {
                "Assets/PaperDefense/Application/GameBalanceAsset.cs",
                "Assets/PaperDefense/Editor/BalanceWorkbookExport.cs",
                "Tools/BalanceWorkbook/build.mjs" };
            foreach (string directory in new[] { "Core", "Application" })
                sourcePaths.AddRange(Directory.GetFiles(Path.Combine(root, "Assets/PaperDefense/" + directory), "*.cs").Select(path => Relative(root, path)));
            sourcePaths.AddRange(configurations.Select(item => item.AssetPath));
            sourcePaths.AddRange(AssetDatabase.GetDependencies(configurations.Select(item => item.AssetPath).ToArray(), true)
                .Where(path => path.StartsWith("Assets/PaperDefense/", StringComparison.Ordinal) && File.Exists(Path.Combine(root, path))));
            var export = new ExportRecord {
                SchemaVersion = 2,
                ExportedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                UnityVersion = UnityEngine.Application.unityVersion,
                Mode = "GameBalanceAsset.CreateWorkshopBalance()",
                Scope = "Effective configured runtime rules. Read-only export. No profiles or live runs are modified.",
                Configurations = configurations.ToArray(),
                Sources = sourcePaths.Distinct().OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => new SourceRecord { Path = path, Sha256 = Hash(File.ReadAllBytes(Path.Combine(root, path))) }).ToArray()
            };
            string pathOut = Path.Combine(root, "docs/balance/balance-data.json");
            Directory.CreateDirectory(Path.GetDirectoryName(pathOut));
            File.WriteAllText(pathOut, JsonUtility.ToJson(export, true) + Environment.NewLine, new UTF8Encoding(false));
            return pathOut;
        }

        static ConfigurationRecord CreateConfiguration(string assetPath, GameBalanceAsset asset, GameBalance balance)
        {
            var mechanics = MechanicsAvailability.Resolve(balance);
            var content = balance.ResolveContent();
            var record = new ConfigurationRecord {
                Id = Path.GetFileNameWithoutExtension(assetPath), AssetPath = assetPath, Preset = balance.Preset.ToString(),
                CatalogAssetPath = asset.dataPreset != null ? AssetDatabase.GetAssetPath(asset.dataPreset.database) : "",
                PresetAssetPath = asset.dataPreset != null ? AssetDatabase.GetAssetPath(asset.dataPreset) : "",
                Parameters = Flatten(balance),
                Construction = Flatten(balance.Construction), Effects = Flatten(balance.Effects), Progression = Flatten(balance.Progression),
                Structures = balance.StructureDefinitions.Select((item, index) => Row(item.Kind.ToString(), index, item)).ToArray(),
                Materials = balance.MaterialDefinitions.Select((item, index) => Row(((MaterialTier)index).ToString(), index, item)).ToArray(),
                Enemies = balance.EnemyDefinitions.Select((item, index) => Row(item.Kind.ToString(), index, item)).ToArray(),
                Waves = balance.Waves.Select((item, index) => new WaveRecord { Index = index + 1, Values = Flatten(item),
                    ResolvedInkGrant = item.InkGrant < 0 ? balance.WaveInkGrant : item.InkGrant }).ToArray(),
                Parts = content.AllParts.Select(item => new PartRecord { Id = item.Id, Name = item.Name, Description = item.Description,
                    Category = item.Category.ToString(), ButtonLabel = item.ButtonLabel, UnlockLabel = item.UnlockLabel, TemplateId = item.TemplateId,
                    Enabled = item.Enabled, Order = item.Order, VisualStyle = item.VisualStyle.ToString(),
                    InitiallyUnlocked = item.InitiallyUnlocked, IsCombatMount = item.IsCombatMount,
                    Mount = item.MountKind?.ToString() ?? "", Module = item.Module.ToString(), Chassis = item.Chassis?.ToString() ?? "",
                    UnlockGold = item.UnlockGold }).ToArray(),
                Catalog = CatalogRows(content, asset.dataPreset, balance),
                Mechanics = new[] {
                    Capability("EnemyDrawingTiers", balance.Mechanics.EnemyDrawingTiers, mechanics.EnemyDrawingTiers),
                    Capability("CorpseBlots", balance.Mechanics.CorpseBlots, mechanics.CorpseBlots),
                    Capability("RangedEnemies", balance.Mechanics.RangedEnemies, mechanics.RangedEnemies),
                    Capability("InkWells", balance.Mechanics.InkWells, mechanics.InkWells),
                    Capability("ColorCartridges", balance.Mechanics.ColorCartridges, mechanics.ColorCartridges),
                    Capability("RunUpgrades", balance.Mechanics.RunUpgrades, mechanics.RunUpgrades) },
                References = ReferenceRows(balance),
                Footprints = content.Weapons.Where(weapon => weapon.SupportsFootprint).SelectMany(weapon => new[] { 1f, weapon.MaximumFootprintAspectRatio }.Distinct().Select(aspect => {
                    float radius = weapon.Stats.Radius;
                    var shape = FootprintGeometry.Resolve(new MortarFootprint(aspect, weapon.MaximumFootprintAspectRatio), radius, new Point2(0, 0), new Point2(1, 0));
                    return new FootprintRecord { WeaponId = weapon.Id, Name = weapon.Name, Radius = radius, AspectRatio = aspect, MaximumAspectRatio = weapon.MaximumFootprintAspectRatio,
                        ForwardRadius = shape.ForwardRadius, SideRadius = shape.SideRadius };
                })).ToArray()
            };
            record.Fingerprint = Hash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(record)));
            return record;
        }

        // These rows read the resolved, immutable definitions. Overrides are already applied by CreateWorkshopBalance.
        static CatalogRecord[] CatalogRows(ContentCatalogSnapshot content, BalancePresetAsset preset, GameBalance balance)
        {
            var rows = new List<CatalogRecord>();
            foreach (var item in content.Chassis)
            {
                var values = new List<ParameterRecord>();
                Add(values, "Capacity", item.Capacity); Add(values, "BaseCost", item.BaseCost); Add(values, "MountCount", item.MountCount);
                Add(values, "UpgradeSlotCount", item.UpgradeSlotCount); Add(values, "ArmorSlotCount", item.ArmorSlotCount); Add(values, "FeedCapacity", item.FeedCapacity);
                Add(values, "BaseHealth", item.BaseHealth); Add(values, "HealthMultiplier", item.HealthMultiplier); Add(values, "BuildSeconds", item.BuildSeconds); Add(values, "SecondaryBuildSeconds", item.SecondaryBuildSeconds);
                Add(values, "PrimaryMountPivot.X", item.PrimaryMountPivot.X); Add(values, "PrimaryMountPivot.Y", item.PrimaryMountPivot.Y);
                Add(values, "PrimaryDualMountPivot.X", item.PrimaryDualMountPivot.X); Add(values, "PrimaryDualMountPivot.Y", item.PrimaryDualMountPivot.Y);
                Add(values, "SecondaryMountPivot.X", item.SecondaryMountPivot.X); Add(values, "SecondaryMountPivot.Y", item.SecondaryMountPivot.Y);
                for (int i = 0; i < item.MountCount; i++)
                {
                    Add(values, "MountPivots[" + i + "].X", item.MountPivots[i].X); Add(values, "MountPivots[" + i + "].Y", item.MountPivots[i].Y);
                    var pivot = item.GetMountPivot(i, item.MountCount);
                    Add(values, "FullMountPivots[" + i + "].X", pivot.X); Add(values, "FullMountPivots[" + i + "].Y", pivot.Y);
                }
                rows.Add(CatalogRow(item.Id, item.Name, "Chassis", values, preset, preset?.overrides?.Chassis.Any(value => value.Id == item.Id) == true));
            }
            foreach (var item in content.Weapons)
            {
                var values = new List<ParameterRecord>(); var stats = item.Stats;
                Add(values, "Attack", item.Attack); Add(values, "Load", item.Load); Add(values, "AssemblyCredit", item.AssemblyCredit);
                Add(values, "MinimumLineRadius", item.MinimumLineRadius); Add(values, "MinimumRadialRange", item.MinimumRadialRange); Add(values, "MaximumFootprintAspectRatio", item.MaximumFootprintAspectRatio);
                Add(values, "MinimumRange", item.MinimumRange); Add(values, "WeaponRange", TowerCatalog.WeaponRange(null, balance, item.Id));
                Add(values, "Stats.Cost", stats.Cost); Add(values, "Stats.MaxHealth", stats.MaxHealth); Add(values, "Stats.BuildSeconds", stats.BuildSeconds);
                Add(values, "Stats.Range", stats.Range); Add(values, "Stats.Damage", stats.Damage); Add(values, "Stats.ShotInterval", stats.ShotInterval);
                Add(values, "Stats.Radius", stats.Radius); Add(values, "Stats.FlightSeconds", stats.FlightSeconds); Add(values, "Stats.MaxPierce", stats.MaxPierce);
                Add(values, "Stats.ConeAngleDegrees", stats.ConeAngleDegrees); Add(values, "Stats.MaxTargets", stats.MaxTargets); Add(values, "Stats.JumpRange", stats.JumpRange);
                Add(values, "Stats.SlowSpeedMultiplier", stats.SlowSpeedMultiplier); Add(values, "Stats.SlowDuration", stats.SlowDuration);
                for (int i = 0; i < item.EffectIds.Count; i++) Add(values, "EffectIds[" + i + "]", item.EffectIds[i]);
                rows.Add(CatalogRow(item.Id, item.Name, "Weapon", values, preset, preset?.overrides?.Weapons.Any(value => value.Id == item.Id) == true));
            }
            foreach (var item in content.Modules)
            {
                var values = new List<ParameterRecord>();
                Add(values, "Load", item.Load); Add(values, "Cost", item.Cost); Add(values, "BuildSeconds", item.BuildSeconds); Add(values, "Repeatable", item.Repeatable); Add(values, "ExclusiveGroup", item.ExclusiveGroup); Add(values, "SlotKind", item.SlotKind);
                for (int i = 0; i < item.AllowedChassisIds.Count; i++) Add(values, "AllowedChassisIds[" + i + "]", item.AllowedChassisIds[i]);
                for (int i = 0; i < item.AllowedAttacks.Count; i++) Add(values, "AllowedAttacks[" + i + "]", item.AllowedAttacks[i]);
                for (int i = 0; i < item.Modifiers.Count; i++)
                { var modifier = item.Modifiers[i]; string path = "Modifiers[" + i + "]."; Add(values, path + "Stat", modifier.Stat); Add(values, path + "Operation", modifier.Operation); Add(values, path + "Scope", modifier.Scope); Add(values, path + "MountIndex", modifier.MountIndex); Add(values, path + "Value", modifier.Value); }
                rows.Add(CatalogRow(item.Id, item.Name, "Module", values, preset, preset?.overrides?.Modules.Any(value => value.Id == item.Id) == true));
            }
            foreach (var item in content.Effects)
            {
                var values = new List<ParameterRecord>(); Add(values, "Kind", item.Kind); Add(values, "Duration", item.Duration); Add(values, "Strength", item.Strength); Add(values, "TickSeconds", item.TickSeconds); Add(values, "IgnoresDefense", item.IgnoresDefense);
                rows.Add(CatalogRow(item.Id, item.Id, "Effect", values, preset, preset?.overrides?.Effects.Any(value => value.Id == item.Id) == true));
            }
            foreach (var item in content.Templates)
            {
                var values = new List<ParameterRecord>(); Add(values, "Revision", item.Revision); Add(values, "Weight", item.Weight); Add(values, "Pivot.X", item.Pivot.X); Add(values, "Pivot.Y", item.Pivot.Y); Add(values, "Signature", item.Signature); Add(values, "ContourCount", item.Contours.Count);
                for (int stroke = 0; stroke < item.Contours.Count; stroke++)
                    for (int point = 0; point < item.Contours[stroke].Points.Count; point++)
                    { var coordinate = item.Contours[stroke].Points[point]; string path = "Contours[" + stroke + "][" + point + "]."; Add(values, path + "X", coordinate.X); Add(values, path + "Y", coordinate.Y); }
                rows.Add(CatalogRow(item.Id, item.DisplayName, "Template", values, preset, preset?.overrides?.Templates.Any(value => value.Id == item.Id) == true));
            }
            foreach (var page in content.StartupBlueprints)
            {
                var values = new List<ParameterRecord>();
                Add(values, "ChassisId", page.ChassisId); Add(values, "FirstWeaponId", page.FirstWeaponId);
                if (page.SecondWeaponId != null) Add(values, "SecondWeaponId", page.SecondWeaponId);
                for (int i = 0; i < page.WeaponMounts.Count; i++)
                {
                    var mount = page.WeaponMounts[i]; string path = "WeaponMounts[" + i + "].";
                    Add(values, path + "SlotIndex", mount.SlotIndex); Add(values, path + "WeaponId", mount.WeaponId); Add(values, path + "Footprint.AspectRatio", mount.Footprint.AspectRatio);
                }
                for (int i = 0; i < page.ModuleSelections.Count; i++)
                { Add(values, "Modules[" + i + "].InstanceId", page.ModuleSelections[i].InstanceId); Add(values, "Modules[" + i + "].DefinitionId", page.ModuleSelections[i].DefinitionId); }
                for (int i = 0; i < content.StartupSlotIds.Count; i++) if (content.StartupSlotIds[i] == page.Id) Add(values, "Slot", i);
                rows.Add(CatalogRow(page.Id, page.Name, "StartingPage", values, preset, false));
            }
            var assessment = new List<ParameterRecord>();
            Add(assessment, "Revision", content.Assessment.Revision); Add(assessment, "Tolerance", content.Assessment.Tolerance); Add(assessment, "CompletionThreshold", content.Assessment.CompletionThreshold); Add(assessment, "PrecisionThreshold", content.Assessment.PrecisionThreshold);
            Add(assessment, "RedEffectId", content.RedEffectId); Add(assessment, "BlueEffectId", content.BlueEffectId); Add(assessment, "GreenEffectId", content.GreenEffectId);
            rows.Add(CatalogRow("catalog.rules", "Правила оценки и цветные эффекты", "Catalog", assessment, preset, false));
            return rows.ToArray();
        }
        static CatalogRecord CatalogRow(string id, string name, string category, List<ParameterRecord> values, BalancePresetAsset preset, bool isOverride)
            => new CatalogRecord { Id = id, Name = name, Category = category, IsOverride = isOverride, Values = values.ToArray(),
                SourceAssetPath = preset == null ? "Legacy adapter" : AssetDatabase.GetAssetPath(isOverride ? (UnityEngine.Object)preset : preset.database) };
        static void Add(List<ParameterRecord> values, string path, object value) => Visit(value, path, values);

        static ReferenceRecord[] ReferenceRows(GameBalance balance)
        {
            var rows = new List<ReferenceRecord>();
            foreach (MaterialTier material in Enum.GetValues(typeof(MaterialTier)))
            {
                var blueprint = new TowerBlueprint("export-gun-armor", "Пулемёт с бронёй", ChassisKind.Standard,
                    StructureKind.Gun, modules: TowerModules.Armor);
                var definition = balance.Structure(StructureKind.Gun);
                var mat = balance.Material(material); var construction = balance.Construction;
                rows.Add(new ReferenceRecord {
                    Id = "gun-armor-" + material, Name = blueprint.Name, Material = material.ToString(),
                    BaseCost = construction.StandardBaseCost, WeaponCost = definition.Cost,
                    AssemblyCredit = construction.WeaponAssemblyCredit, ModuleCost = construction.ArmorCost,
                    MaterialCost = mat.CostMultiplier, FoundationHealth = construction.FoundationHealth,
                    ArmorHealthMultiplier = construction.ArmorHealthMultiplier, MaterialHealth = mat.HealthMultiplier,
                    WeaponDamage = definition.Damage, MaterialDamage = mat.DamageMultiplier,
                    ShotInterval = definition.ShotInterval, RefundFraction = mat.RefundFraction,
                    MaximumRefundFraction = balance.Progression.MaximumRefundFraction,
                    Cost = TowerCatalog.Cost(blueprint, balance, material), Health = TowerCatalog.Health(blueprint, balance, material),
                    Defense = DamageRules.EffectiveDefense(TowerCatalog.Defense(blueprint, balance), balance.Effects.MaximumDefense),
                    BuildSeconds = TowerCatalog.BuildSeconds(blueprint, balance, material),
                    ColorCost = TowerCatalog.ColorCost(blueprint, balance),
                    Load = TowerCatalog.Load(blueprint, balance), Capacity = TowerCatalog.Capacity(blueprint, balance),
                    DamagePerHit = definition.Damage * mat.DamageMultiplier,
                    ActionInterval = TowerCatalog.ActionInterval(blueprint, balance, definition.Kind,
                        TowerCatalog.FeedMultiplier(blueprint, balance)) });
            }
            return rows.ToArray();
        }

        static MechanicsRecord Capability(string id, bool configured, bool effective)
            => new MechanicsRecord { Id = id, Configured = configured, Effective = effective };
        static ValueRecord Row(string id, int index, object value)
            => new ValueRecord { Id = id, Index = index, Values = Flatten(value) };

        // Reflection covers future fields automatically; unsupported fields fail the export instead of disappearing.
        static ParameterRecord[] Flatten(object value)
        {
            var result = new List<ParameterRecord>();
            Visit(value, "", result);
            return result.ToArray();
        }
        static void Visit(object value, string path, List<ParameterRecord> result)
        {
            if (value == null) throw new InvalidOperationException("Missing export value: " + path);
            Type type = value.GetType();
            if (type.IsEnum) { result.Add(new ParameterRecord { Path = path, Kind = "enum", Text = value.ToString(), Number = Convert.ToInt64(value), Unit = "enum" }); return; }
            if (value is bool flag) { result.Add(new ParameterRecord { Path = path, Kind = "bool", Boolean = flag, Unit = "bool" }); return; }
            if (value is string text) { result.Add(new ParameterRecord { Path = path, Kind = "string", Text = text, Unit = "текст" }); return; }
            if (type.IsPrimitive || value is decimal)
            {
                double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(number) || double.IsInfinity(number)) throw new InvalidOperationException("Nonfinite export value: " + path);
                result.Add(new ParameterRecord { Path = path, Kind = "number", Number = number, Unit = Unit(path) }); return;
            }
            if (value is IList list)
            { for (int i = 0; i < list.Count; i++) Visit(list[i], path + "[" + i + "]", result); return; }
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
            if (fields.Length == 0) throw new InvalidOperationException("Unsupported export type: " + type.FullName);
            foreach (FieldInfo field in fields)
            {
                // The immutable content graph has an explicit stable-ID export above, separate from legacy balance fields.
                if (type == typeof(GameBalance) && field.Name == nameof(GameBalance.Content)) continue;
                Visit(field.GetValue(value), path.Length == 0 ? field.Name : path + "." + field.Name, result);
            }
        }

        static string Unit(string path)
        {
            string name = path.Substring(path.LastIndexOf('.') + 1);
            if (name == "WellInkPerWave") return "цветные чернила/волну";
            if (name == "ColorMinimum") return "цветные чернила";
            if (name == "ColorCostDivisor") return "делитель";
            if (name == "RedDamagePerTick") return "HP/тик";
            if (name == "LegacyHealthGrowthPerWave") return "доля HP/волну";
            if (name == "LegacyHealthGrowthStartsAfterWave" || name == "UpgradeEveryWaves" || name == "LastUpgradeWave") return "волны";
            if (name == "ColorTargetsPerWindow" || name == "MaxPierce" || name == "MaxTargets") return "цели";
            if (name == "Speed") return "клетки/с";
            if (name.EndsWith("PerSecond", StringComparison.Ordinal)) return "штрихи/с";
            if (name.Contains("Seconds") || name.Contains("Interval") || name.EndsWith("Duration", StringComparison.Ordinal) || name.EndsWith("Cooldown", StringComparison.Ordinal) || name == "SimulationStep" || name == "BlotLifetime")
                return name.EndsWith("Multiplier", StringComparison.Ordinal) ? "множитель" : "с";
            if (name.EndsWith("Multiplier", StringComparison.Ordinal) || name.EndsWith("ScaleMinimum", StringComparison.Ordinal) || name.EndsWith("ScaleVariation", StringComparison.Ordinal) || name == "DynamicFeedCapacity") return "множитель";
            if (name.Contains("Fraction") || name.Contains("Defense") || name == "BlotReduction" || name == "BlotSpeedBonus" || name == "SalvageRefundBonus") return "доля";
            if (name.Contains("Health")) return "HP";
            if (name.Contains("Damage")) return "HP/попадание";
            if (name.Contains("Gold")) return "золото";
            if (name.Contains("Ink") || name.Contains("Cost") || name == "Reward" || name == "WeaponAssemblyCredit") return "штрихи";
            if (name == "Width" || name == "Height" || name.Contains("Range") || name.Contains("Radius")) return "клетки";
            if (name.Contains("Degrees")) return "градусы";
            if (name.EndsWith("Load", StringComparison.Ordinal) || name == "StandardCapacity" || name == "HeavyCapacity") return "нагрузка";
            if (name == "Sides") return "маска 1/2/4/8";
            if (name.Contains("Rate")) return "множитель";
            return "ед.";
        }
        static string Relative(string root, string path) => path.Substring(root.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/');
        static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }

        [Serializable] public sealed class ExportRecord
        { public int SchemaVersion; public string ExportedAtUtc, UnityVersion, Mode, Scope; public ConfigurationRecord[] Configurations; public SourceRecord[] Sources; }
        [Serializable] public sealed class SourceRecord { public string Path, Sha256; }
        [Serializable] public sealed class ConfigurationRecord
        { public string Id, AssetPath, Preset, Fingerprint, CatalogAssetPath, PresetAssetPath; public ParameterRecord[] Parameters, Construction, Effects, Progression;
            public ValueRecord[] Structures, Materials, Enemies; public WaveRecord[] Waves; public PartRecord[] Parts; public CatalogRecord[] Catalog; public MechanicsRecord[] Mechanics; public ReferenceRecord[] References; public FootprintRecord[] Footprints; }
        [Serializable] public sealed class ParameterRecord { public string Path, Kind, Text, Unit; public double Number; public bool Boolean; }
        [Serializable] public sealed class ValueRecord { public string Id; public int Index; public ParameterRecord[] Values; }
        [Serializable] public sealed class WaveRecord { public int Index, ResolvedInkGrant; public ParameterRecord[] Values; }
        [Serializable] public sealed class MechanicsRecord { public string Id; public bool Configured, Effective; }
        [Serializable] public sealed class PartRecord
        { public string Id, Name, Description, Mount, Module, Chassis, Category, ButtonLabel, UnlockLabel, TemplateId, VisualStyle; public bool InitiallyUnlocked, IsCombatMount, Enabled; public int UnlockGold, Order; }
        [Serializable] public sealed class CatalogRecord { public string Id, Name, Category, SourceAssetPath; public bool IsOverride; public ParameterRecord[] Values; }
        [Serializable] public sealed class ReferenceRecord
        {
            public string Id, Name, Material;
            public int BaseCost, WeaponCost, AssemblyCredit, ModuleCost, Cost, ColorCost, Load, Capacity;
            public float MaterialCost, FoundationHealth, ArmorHealthMultiplier, MaterialHealth, WeaponDamage, MaterialDamage,
                ShotInterval, RefundFraction, MaximumRefundFraction, Health, Defense, BuildSeconds, DamagePerHit, ActionInterval;
        }
        [Serializable] public sealed class FootprintRecord { public string WeaponId, Name; public float Radius, AspectRatio, MaximumAspectRatio, ForwardRadius, SideRadius; }
    }
}
