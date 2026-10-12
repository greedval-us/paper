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
    public static class BalanceWorkbookExport
    {
        [MenuItem("Paper Defense/Export mechanics and balance JSON")]
        public static void ExportFromMenu() => Debug.Log(Export());

        public static string Export()
        {
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
                configurations.Add(CreateConfiguration(assetPath, balance));
            }
            var sourcePaths = new List<string> {
                "Assets/PaperDefense/Application/GameBalanceAsset.cs",
                "Assets/PaperDefense/Editor/BalanceWorkbookExport.cs" };
            sourcePaths.AddRange(Directory.GetFiles(Path.Combine(root, "Assets/PaperDefense/Core"), "*.cs")
                .Select(path => Relative(root, path)));
            sourcePaths.AddRange(configurations.Select(item => item.AssetPath));
            var export = new ExportRecord {
                SchemaVersion = 1,
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

        static ConfigurationRecord CreateConfiguration(string assetPath, GameBalance balance)
        {
            var mechanics = MechanicsAvailability.Resolve(balance);
            var record = new ConfigurationRecord {
                Id = Path.GetFileNameWithoutExtension(assetPath), AssetPath = assetPath, Preset = balance.Preset.ToString(),
                Parameters = Flatten(balance),
                Construction = Flatten(balance.Construction), Effects = Flatten(balance.Effects), Progression = Flatten(balance.Progression),
                Structures = balance.StructureDefinitions.Select((item, index) => Row(item.Kind.ToString(), index, item)).ToArray(),
                Materials = balance.MaterialDefinitions.Select((item, index) => Row(((MaterialTier)index).ToString(), index, item)).ToArray(),
                Enemies = balance.EnemyDefinitions.Select((item, index) => Row(item.Kind.ToString(), index, item)).ToArray(),
                Waves = balance.Waves.Select((item, index) => new WaveRecord { Index = index + 1, Values = Flatten(item),
                    ResolvedInkGrant = item.InkGrant < 0 ? balance.WaveInkGrant : item.InkGrant }).ToArray(),
                Parts = PartCatalog.All.Select(item => new PartRecord { Id = item.Id, Name = item.Name, Description = item.Description,
                    InitiallyUnlocked = item.InitiallyUnlocked, IsCombatMount = item.IsCombatMount,
                    Mount = item.MountKind?.ToString() ?? "", Module = item.Module.ToString(), Chassis = item.Chassis?.ToString() ?? "",
                    UnlockGold = PartCatalog.GetUnlockGold(item, balance.Construction) }).ToArray(),
                Mechanics = new[] {
                    Capability("EnemyDrawingTiers", balance.Mechanics.EnemyDrawingTiers, mechanics.EnemyDrawingTiers),
                    Capability("CorpseBlots", balance.Mechanics.CorpseBlots, mechanics.CorpseBlots),
                    Capability("RangedEnemies", balance.Mechanics.RangedEnemies, mechanics.RangedEnemies),
                    Capability("InkWells", balance.Mechanics.InkWells, mechanics.InkWells),
                    Capability("ColorCartridges", balance.Mechanics.ColorCartridges, mechanics.ColorCartridges),
                    Capability("RunUpgrades", balance.Mechanics.RunUpgrades, mechanics.RunUpgrades),
                    Capability("DrawingQuality", balance.Mechanics.DrawingQuality, mechanics.DrawingQuality) },
                References = ReferenceRows(balance),
                Footprints = new[] { MortarFootprint.Circle.AspectRatio, MortarFootprint.MaximumAspectRatio }.Select(aspect => {
                    float radius = balance.Structure(StructureKind.Mortar).Radius;
                    var shape = FootprintGeometry.Resolve(new MortarFootprint(aspect), radius, new Point2(0, 0), new Point2(1, 0));
                    return new FootprintRecord { Radius = radius, AspectRatio = aspect, MaximumAspectRatio = MortarFootprint.MaximumAspectRatio,
                        ForwardRadius = shape.ForwardRadius, SideRadius = shape.SideRadius };
                }).ToArray()
            };
            record.Fingerprint = Hash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(record)));
            return record;
        }

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
                        TowerCatalog.FeedMultiplier(blueprint, balance), TowerCatalog.QualityMultiplier(blueprint, balance)) });
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
            foreach (FieldInfo field in fields) Visit(field.GetValue(value), path.Length == 0 ? field.Name : path + "." + field.Name, result);
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
        { public string Id, AssetPath, Preset, Fingerprint; public ParameterRecord[] Parameters, Construction, Effects, Progression;
            public ValueRecord[] Structures, Materials, Enemies; public WaveRecord[] Waves; public PartRecord[] Parts; public MechanicsRecord[] Mechanics; public ReferenceRecord[] References; public FootprintRecord[] Footprints; }
        [Serializable] public sealed class ParameterRecord { public string Path, Kind, Text, Unit; public double Number; public bool Boolean; }
        [Serializable] public sealed class ValueRecord { public string Id; public int Index; public ParameterRecord[] Values; }
        [Serializable] public sealed class WaveRecord { public int Index, ResolvedInkGrant; public ParameterRecord[] Values; }
        [Serializable] public sealed class MechanicsRecord { public string Id; public bool Configured, Effective; }
        [Serializable] public sealed class PartRecord
        { public string Id, Name, Description, Mount, Module, Chassis; public bool InitiallyUnlocked, IsCombatMount; public int UnlockGold; }
        [Serializable] public sealed class ReferenceRecord
        {
            public string Id, Name, Material;
            public int BaseCost, WeaponCost, AssemblyCredit, ModuleCost, Cost, ColorCost, Load, Capacity;
            public float MaterialCost, FoundationHealth, ArmorHealthMultiplier, MaterialHealth, WeaponDamage, MaterialDamage,
                ShotInterval, RefundFraction, MaximumRefundFraction, Health, Defense, BuildSeconds, DamagePerHit, ActionInterval;
        }
        [Serializable] public sealed class FootprintRecord { public float Radius, AspectRatio, MaximumAspectRatio, ForwardRadius, SideRadius; }
    }
}
