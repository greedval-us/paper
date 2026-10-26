using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Editor
{
    public sealed class AssemblyImportPreview
    {
        public IReadOnlyList<string> Added { get; internal set; }
        public IReadOnlyList<string> Changed { get; internal set; }
        public IReadOnlyList<string> Removed { get; internal set; }
        public IReadOnlyList<string> StarterChanges { get; internal set; }
    }

    /// <summary>Explicit album-v1 translation. Localized labels are consumed here only.</summary>
    public static class AlbumBalanceImport
    {
        public const string SourcePath = "docs/balance/album-balance-v1.json";
        public const string CatalogPath = "Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset";
        static readonly Dictionary<string, ComponentCategory> Categories = new Dictionary<string, ComponentCategory>(StringComparer.Ordinal)
        {
            { "Рамка", ComponentCategory.Frame }, { "Корпус", ComponentCategory.Hull }, { "Броня", ComponentCategory.Armor },
            { "Подача", ComponentCategory.Feed }, { "Поворот", ComponentCategory.Turn }, { "Ствол / головка", ComponentCategory.Head },
            { "Прицел", ComponentCategory.Sight }, { "Магазин", ComponentCategory.Magazine }, { "Спуск", ComponentCategory.Trigger },
            { "Основание", ComponentCategory.Site }, { "Функция", ComponentCategory.Function }, { "Оболочка", ComponentCategory.Shell }, { "Оснащение", ComponentCategory.Utility }
        };
        static readonly Dictionary<string, AssemblyWeaponFamily> Weapons = new Dictionary<string, AssemblyWeaponFamily>(StringComparer.Ordinal)
        {
            { "core.gun", AssemblyWeaponFamily.Gun }, { "core.piercer", AssemblyWeaponFamily.Piercer }, { "core.mortar", AssemblyWeaponFamily.Mortar },
            { "core.fan", AssemblyWeaponFamily.Fan }, { "core.ricochet", AssemblyWeaponFamily.Ricochet }, { "core.stapler", AssemblyWeaponFamily.Stapler }, { "core.blotter", AssemblyWeaponFamily.Blotter }
        };
        static readonly Dictionary<string, AssemblyStructureFamily> Structures = new Dictionary<string, AssemblyStructureFamily>(StringComparer.Ordinal)
        {
            { "wall", AssemblyStructureFamily.Wall }, { "slow", AssemblyStructureFamily.Slow },
            { "repair", AssemblyStructureFamily.Repair }, { "generator", AssemblyStructureFamily.Generator }
        };
        internal static readonly (string key, AssemblyStat stat, ModifierOperation operation)[] NumericFields =
        {
            ("cost", AssemblyStat.Cost, ModifierOperation.Add), ("load", AssemblyStat.Load, ModifierOperation.Add), ("build", AssemblyStat.BuildSeconds, ModifierOperation.Add),
            ("hp", AssemblyStat.Health, ModifierOperation.Set), ("hpMult", AssemblyStat.Health, ModifierOperation.Multiply),
            ("capacity", AssemblyStat.Capacity, ModifierOperation.Set), ("capacityAdd", AssemblyStat.Capacity, ModifierOperation.Add),
            ("mounts", AssemblyStat.MountCount, ModifierOperation.Set), ("feed", AssemblyStat.Feed, ModifierOperation.Set), ("feedMult", AssemblyStat.Feed, ModifierOperation.Multiply),
            ("defense", AssemblyStat.Defense, ModifierOperation.Add), ("frontDefense", AssemblyStat.FrontDefense, ModifierOperation.Add),
            ("acquisitionMult", AssemblyStat.AcquisitionSeconds, ModifierOperation.Multiply), ("damage", AssemblyStat.Damage, ModifierOperation.Set),
            ("damageMult", AssemblyStat.Damage, ModifierOperation.Multiply), ("interval", AssemblyStat.Interval, ModifierOperation.Set), ("intervalMult", AssemblyStat.Interval, ModifierOperation.Multiply),
            ("range", AssemblyStat.Range, ModifierOperation.Set), ("rangeMult", AssemblyStat.Range, ModifierOperation.Multiply), ("minRange", AssemblyStat.MinimumRange, ModifierOperation.Set),
            ("targets", AssemblyStat.TargetLimit, ModifierOperation.Set), ("radius", AssemblyStat.Radius, ModifierOperation.Set), ("radiusMult", AssemblyStat.Radius, ModifierOperation.Multiply),
            ("clip", AssemblyStat.MagazineSize, ModifierOperation.Set), ("clipMult", AssemblyStat.MagazineSize, ModifierOperation.Multiply), ("clipOverride", AssemblyStat.MagazineOverride, ModifierOperation.Set),
            ("reload", AssemblyStat.ReloadSeconds, ModifierOperation.Set), ("reloadMult", AssemblyStat.ReloadSeconds, ModifierOperation.Multiply),
            ("slow", AssemblyStat.Slow, ModifierOperation.Set), ("slowSeconds", AssemblyStat.SlowSeconds, ModifierOperation.Set),
            ("flight", AssemblyStat.FlightSeconds, ModifierOperation.Set), ("jump", AssemblyStat.JumpRange, ModifierOperation.Set),
            ("rate", AssemblyStat.SupportRate, ModifierOperation.Set), ("rateMult", AssemblyStat.SupportRate, ModifierOperation.Multiply)
        };

        public static IReadOnlyList<AssemblyDiagnostic> Validate(string json)
        {
            try { Parse(json); return Array.Empty<AssemblyDiagnostic>(); }
            catch (AssemblyCatalogException ex) { return ex.Diagnostics; }
            catch (JsonException) { return new[] { new AssemblyDiagnostic("assembly.source.json", "source") }; }
        }

        public static AssemblyImportPreview Preview(string json, string assetPath)
        {
            var next = ReadValidated(json); var existing = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(CheckedPath(assetPath));
            var previous = existing != null ? existing.catalog.Definitions : Array.Empty<ComponentDefinitionInput>();
            string Key(ComponentDefinitionInput d) => d.Id + "@" + d.Revision;
            var old = previous.ToDictionary(Key, JsonUtility.ToJson, StringComparer.Ordinal);
            var fresh = next.Definitions.ToDictionary(Key, JsonUtility.ToJson, StringComparer.Ordinal);
            var oldStarters = existing != null ? existing.catalog.Starters.ToDictionary(s => s.Id, JsonUtility.ToJson, StringComparer.Ordinal) : new Dictionary<string, string>();
            return new AssemblyImportPreview
            {
                Added = fresh.Keys.Where(k => !old.ContainsKey(k)).ToList().AsReadOnly(),
                Changed = fresh.Keys.Where(k => old.TryGetValue(k, out var value) && value != fresh[k]).ToList().AsReadOnly(),
                Removed = old.Keys.Where(k => !fresh.ContainsKey(k)).ToList().AsReadOnly(),
                StarterChanges = next.Starters.Where(s => !oldStarters.TryGetValue(s.Id, out var value) || value != JsonUtility.ToJson(s)).Select(s => s.Id)
                    .Concat(oldStarters.Keys.Where(id => !next.Starters.Any(s => s.Id == id))).ToList().AsReadOnly()
            };
        }

        public static AssemblyCatalogAsset Import(string json, string assetPath)
        {
            var data = ReadValidated(json); // No asset mutation before the entire graph passes.
            string path = CheckedPath(assetPath);
            var existingObject = AssetDatabase.LoadMainAssetAtPath(path);
            if (existingObject != null && !(existingObject is AssemblyCatalogAsset))
                throw AssemblyCatalogException.At("assembly.asset.type", path);
            var existing = existingObject as AssemblyCatalogAsset;
            if (existing != null && JsonUtility.ToJson(existing.catalog) == JsonUtility.ToJson(data)) return existing;
            if (existing == null)
            {
                var created = ScriptableObject.CreateInstance<AssemblyCatalogAsset>(); created.catalog = data;
                try { AssetDatabase.CreateAsset(created, path); AssetDatabase.SaveAssetIfDirty(created); return created; }
                catch { if (!AssetDatabase.Contains(created)) UnityEngine.Object.DestroyImmediate(created); throw; }
            }
            existing.catalog = data;
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }

        [MenuItem("Paper Defense/Content/Preview assembly balance import")]
        static void PreviewMenu()
        {
            var p = Preview(File.ReadAllText(SourcePath), CatalogPath);
            Debug.Log("Assembly import: added " + string.Join(", ", p.Added) + "; changed " + string.Join(", ", p.Changed) + "; removed " + string.Join(", ", p.Removed) + "; starters " + string.Join(", ", p.StarterChanges));
        }
        [MenuItem("Paper Defense/Content/Import assembly balance")]
        static void ImportMenu() => Import(File.ReadAllText(SourcePath), CatalogPath);

        static string CheckedPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.Ordinal) || path.Contains("..") || path.Contains("\\") || !AssetDatabase.IsValidFolder(Path.GetDirectoryName(path)?.Replace('\\', '/')))
                throw AssemblyCatalogException.At("assembly.asset.path", path ?? "assetPath");
            return path;
        }
        public static AssemblyCatalogInput ReadValidated(string json)
        {
            try { return Parse(json); }
            catch (JsonException) { throw AssemblyCatalogException.At("assembly.source.json", "source"); }
        }
        static AssemblyCatalogInput Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw AssemblyCatalogException.At("assembly.source.json", "source");
            var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (root["version"]?.Type != JTokenType.String || (string)root["version"] != "album-v1.0") throw AssemblyCatalogException.At("assembly.balance.version", "version");
            var definitions = new List<ComponentDefinitionInput>();
            string[] groups = { "foundations", "cores", "weaponParts", "buildingParts" };
            foreach (var group in groups) foreach (var row in RequiredArray(root, group)) definitions.Add(ReadDefinition(row, group));
            foreach (ComponentCategory category in Enum.GetValues(typeof(ComponentCategory)))
                if (!definitions.Any(d => d.Category == category)) throw AssemblyCatalogException.At("assembly.category.missing", "definitions", category.ToString());
            var initial = new AssemblyCatalogInput { BalanceRevision = "album-v1.0", Definitions = definitions.ToArray() };
            var catalog = initial.CreateSnapshot(); // Duplicate exact revisions rejected before recipe lookup.
            int[] counts = { 14, 7, 18, 15 };
            for (int i = 0; i < groups.Length; i++) if (RequiredArray(root, groups[i]).Count != counts[i]) throw AssemblyCatalogException.At("assembly.source.count", groups[i], counts[i].ToString());
            var weapons = RequiredArray(root, "weapons").Select(ReadWeapon).ToArray();
            var entries = new List<AssemblyStarterInput>(); var entryIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in RequiredArray(root, "towers"))
            {
                int index = Integer(t, "weapon"); int count = Integer(t, "count");
                if (index < 0 || index >= weapons.Length) throw AssemblyCatalogException.At("assembly.recipe.weapon_index", t.Path + ".weapon");
                if (count < 1 || count > 3) throw AssemblyCatalogException.At("assembly.recipe.mount_count", t.Path + ".count");
                var entry = new AssemblyStarterInput
                {
                    Id = Text(t, "id"), Name = Text(t, "name"), Kind = AlbumEntryKind.Tower,
                    Foundation = new FoundationRecipeInput { Frame = Ref(t, "frame"), Hull = Ref(t, "hull"), Armor = Ref(t, "armor"), Feed = Ref(t, "feed"), Turn = Ref(t, "turn") },
                    Mounts = Enumerable.Range(0, count).Select(slot => new WeaponMountRecipeInput { SlotIndex = slot, Weapon = weapons[index] }).ToArray()
                };
                CheckEntry(t, entry, catalog, entryIds, entries);
            }
            foreach (var b in RequiredArray(root, "buildings"))
            {
                if (!Structures.TryGetValue(Text(b, "family"), out var family)) throw AssemblyCatalogException.At("assembly.compatibility.unknown", b.Path + ".family");
                var entry = new AssemblyStarterInput
                {
                    Id = Text(b, "id"), Name = Text(b, "name"), Kind = AlbumEntryKind.Structure,
                    Structure = new StructureRecipeInput { Family = family, Site = Ref(b, "site"), Function = Ref(b, "function"), Shell = Ref(b, "shell"), Utility = Ref(b, "utility") }
                };
                CheckEntry(b, entry, catalog, entryIds, entries);
            }
            if (entries.Count != 8) throw AssemblyCatalogException.At("assembly.starter.count", "starters", "8");
            initial.Starters = entries.ToArray(); initial.CreateSnapshot(); return initial;
        }
        static void CheckEntry(JToken row, AssemblyStarterInput entry, ComponentCatalog catalog, HashSet<string> ids, List<AssemblyStarterInput> starters)
        {
            if (!ids.Add(entry.Id)) throw AssemblyCatalogException.At("assembly.entry.duplicate", row.Path + ".id");
            var issues = catalog.ValidateRecipe(entry.CreateRecipe());
            if (issues.Count != 0) throw new AssemblyCatalogException(issues);
            if (row["starter"]?.Type != JTokenType.Boolean) throw AssemblyCatalogException.At("assembly.source.field", row.Path + ".starter");
            if ((bool)row["starter"]) starters.Add(entry);
        }
        static WeaponRecipeInput ReadWeapon(JToken row) => new WeaponRecipeInput
        { Mechanism = Ref(row, "core"), Head = Ref(row, "head"), Sight = Ref(row, "sight"), Magazine = Ref(row, "magazine"), Trigger = Ref(row, "trigger") };

        static ComponentDefinitionInput ReadDefinition(JToken row, string group)
        {
            string id = Text(row, "id");
            ComponentCategory category;
            if (group == "cores") category = ComponentCategory.Mechanism;
            else if (!Categories.TryGetValue(Text(row, "slot"), out category)) throw AssemblyCatalogException.At("assembly.category.unknown", row.Path + ".slot");
            bool validGroup = group == "foundations" ? category <= ComponentCategory.Turn : group == "cores" ? category == ComponentCategory.Mechanism : group == "weaponParts" ? category >= ComponentCategory.Head && category <= ComponentCategory.Trigger : category >= ComponentCategory.Site;
            if (!validGroup) throw AssemblyCatalogException.At("assembly.category.unknown", row.Path + ".slot");
            var d = new ComponentDefinitionInput { Id = id, Name = Text(row, "name"), Revision = 1, Category = category, RequiredLevel = Integer(row, "level"), UnlockGold = Integer(row, "gold") };
            if (d.UnlockGold < 0) throw AssemblyCatalogException.At("assembly.unlock.negative", row.Path + ".gold");
            if (group == "cores")
            {
                if (!Weapons.TryGetValue(id, out var family)) throw AssemblyCatalogException.At("assembly.compatibility.unknown", row.Path + ".id");
                d.WeaponFamilies = family;
            }
            else
            {
                string compatible = Text(row, "compatible");
                foreach (string token in compatible.Split(','))
                {
                    if (token == "all")
                    {
                        if (group == "buildingParts") d.StructureFamilies |= AssemblyStructureFamily.All; else d.WeaponFamilies |= AssemblyWeaponFamily.All;
                    }
                    else if (group == "weaponParts" && Weapons.TryGetValue(token, out var weapon)) d.WeaponFamilies |= weapon;
                    else if (group == "buildingParts" && Structures.TryGetValue(token, out var structure)) d.StructureFamilies |= structure;
                    else throw AssemblyCatalogException.At("assembly.compatibility.unknown", row.Path + ".compatible", token);
                }
            }
            string required = group == "foundations" ? "cost,load,build,hpMult,capacityAdd,feedMult,defense,frontDefense,acquisitionMult" : group == "cores" ? "cost,load,damage,interval,range,minRange,targets,radius,build,clip,reload,slow,slowSeconds,flight,jump" : group == "weaponParts" ? "cost,load,damageMult,intervalMult,rangeMult,clipMult,clipOverride,reloadMult,acquisitionMult,radiusMult" : "cost,load,capacity,hp,hpMult,range,rangeMult,rate,rateMult,slow,defense,build";
            if (category == ComponentCategory.Frame) required += ",hp,capacity,mounts,feed";
            foreach (var key in required.Split(',')) if (row[key] == null) throw AssemblyCatalogException.At("assembly.source.field", row.Path + "." + key);
            var modifiers = new List<ComponentModifierInput>();
            foreach (var field in NumericFields)
            {
                if (row[field.key] == null) continue;
                float value = Number(row, field.key);
                bool signedDelta = field.key == "capacityAdd" || ((field.key == "cost" || field.key == "load" || field.key == "build") && (category == ComponentCategory.Hull || category == ComponentCategory.Head || category == ComponentCategory.Shell));
                bool integral = field.key == "cost" || field.key == "load" || field.key == "capacity" || field.key == "capacityAdd" || field.key == "mounts" || field.key == "clip" || field.key == "clipMult" || field.key == "clipOverride" || field.key == "targets";
                if ((!signedDelta && value < 0) || (field.operation == ModifierOperation.Multiply && value <= 0) || (integral && value != Math.Truncate(value)) || ((field.key == "clip" || field.key == "interval" || field.key == "reload") && value <= 0) || ((field.key == "slow" || field.key == "defense" || field.key == "frontDefense") && value > 1))
                    throw AssemblyCatalogException.At("assembly.number.range", row.Path + "." + field.key);
                modifiers.Add(new ComponentModifierInput { Stat = field.stat, Operation = field.operation, Value = value });
            }
            if (d.WeaponFamilies == AssemblyWeaponFamily.Mortar && category == ComponentCategory.Mechanism)
            {
                var target = modifiers.Single(m => m.Stat == AssemblyStat.TargetLimit);
                modifiers.Add(new ComponentModifierInput { Stat = AssemblyStat.EvaluationTargets, Operation = ModifierOperation.Set, Value = target.Value });
                target.Value = 0; // Zero denotes no AoE cap, not the eight-target balance scenario.
            }
            if (d.WeaponFamilies == AssemblyWeaponFamily.Fan && category == ComponentCategory.Mechanism)
                modifiers.Add(new ComponentModifierInput { Stat = AssemblyStat.ConeDegrees, Operation = ModifierOperation.Set, Value = 90 });
            if (id == "trigger.burst") d.RequiredParts = new[] { new PartRefInput { Id = "magazine.standard", Revision = 1 } };
            d.Modifiers = modifiers.ToArray(); return d;
        }
        static JArray RequiredArray(JObject root, string key) => root[key] as JArray ?? throw AssemblyCatalogException.At("assembly.source.array", key);
        static string Text(JToken row, string key)
        {
            if (!(row is JObject) || row[key]?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)row[key])) throw AssemblyCatalogException.At("assembly.source.field", row.Path + "." + key);
            return (string)row[key];
        }
        static PartRefInput Ref(JToken row, string key) => new PartRefInput { Id = Text(row, key), Revision = 1 };
        static int Integer(JToken row, string key)
        {
            if (!(row is JObject) || (row[key]?.Type != JTokenType.Integer && row[key]?.Type != JTokenType.Float))
                throw AssemblyCatalogException.At("assembly.source.field", row.Path + "." + key);
            double value = (double)row[key];
            if (double.IsNaN(value) || double.IsInfinity(value)) throw AssemblyCatalogException.At("assembly.number.nonfinite", row.Path + "." + key);
            if (value != Math.Truncate(value) || value > int.MaxValue || value < int.MinValue) throw AssemblyCatalogException.At("assembly.number.range", row.Path + "." + key);
            return (int)value;
        }
        static float Number(JToken row, string key)
        {
            if (!(row is JObject)) throw AssemblyCatalogException.At("assembly.source.field", row.Path);
            var token = row[key];
            if (token?.Type == JTokenType.String && ((string)token == "NaN" || ((string)token).Contains("Infinity"))) throw AssemblyCatalogException.At("assembly.number.nonfinite", row.Path + "." + key);
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)) throw AssemblyCatalogException.At("assembly.source.field", row.Path + "." + key);
            float value;
            if (!float.TryParse(((JValue)token).ToString(CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out value) || float.IsNaN(value) || float.IsInfinity(value)) throw AssemblyCatalogException.At("assembly.number.nonfinite", row.Path + "." + key);
            return value;
        }
    }
}
