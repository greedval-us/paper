using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEditor;

namespace PaperDefense.Editor
{
    public static partial class BalanceWorkbookExport
    {
        public const string PlayerPath = "Assets/PaperDefense/Configuration/Game/PlayerBalance.asset";
        public const string AssemblyExportPath = "docs/balance/assembly-runtime.json";

        // Expected JSON supplies recipe identities and input-field names only. All numbers come from live assets/resolvers.
        public static JObject CreatePlayerAssembly()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(PlayerPath);
            var balance = asset.CreateWorkshopBalance();
            var catalog = asset.dataPreset.database.assemblyCatalog.CreateSnapshot();
            var result = CreatePlayerAssembly(JObject.Parse(File.ReadAllText(AlbumBalanceImport.SourcePath)), balance, catalog);
            result["source"] = new JObject {
                ["balance"] = PlayerPath, ["rules"] = AssetDatabase.GetAssetPath(asset.dataPreset),
                ["content"] = AssetDatabase.GetAssetPath(asset.dataPreset.database),
                ["assemblyCatalog"] = AssetDatabase.GetAssetPath(asset.dataPreset.database.assemblyCatalog)
            };
            return result;
        }

        public static JObject CreatePlayerAssembly(JObject design, GameBalance balance, ComponentCatalog catalog)
        {
            BalanceValidation.Validate(balance);
            var definitions = new JArray();
            // Enumerate the runtime set, including unexpected IDs. A missing or additional definition cannot disappear in export.
            foreach (var part in catalog.Definitions)
            {
                var numeric = new JObject();
                foreach (var field in AlbumBalanceImport.NumericFields)
                {
                    var stat = field.stat;
                    if (field.key == "targets" && part.Category == ComponentCategory.Mechanism && part.WeaponFamilies == AssemblyWeaponFamily.Mortar)
                        stat = AssemblyStat.EvaluationTargets;
                    var modifier = part.Modifiers.SingleOrDefault(m => m.Stat == stat && m.Operation == field.operation);
                    if (modifier != null) numeric[field.key] = modifier.Value;
                }
                definitions.Add(new JObject { ["id"] = part.Id, ["revision"] = part.Revision,
                    ["level"] = part.RequiredLevel, ["gold"] = part.UnlockGold, ["numeric"] = numeric });
            }
            var weapons = new JArray(); var towers = new JArray(); var buildings = new JArray();
            foreach (var row in design["weapons"])
            {
                var weapon = Weapon(row);
                var recipe = new AssemblyRecipe(new TowerRecipe(StandardFoundation(), new[] { new WeaponMountRecipe(0, weapon) }));
                var resolved = Resolve(recipe, catalog, balance); var w = resolved.Weapons[0];
                var parts = weapon.Parts.Select(p => Get(catalog, p)).ToArray();
                int targets = w.Family == AssemblyWeaponFamily.Mortar ? (int)parts[0].Get(AssemblyStat.EvaluationTargets, ModifierOperation.Set) : w.TargetLimit;
                weapons.Add(new JObject { ["id"] = row["id"].Value<string>(), ["resolved"] = new JObject {
                    ["damage"] = w.Damage, ["interval"] = w.Interval, ["reload"] = w.ReloadSeconds,
                    ["clip"] = w.MagazineSize, ["cycle"] = w.CycleSeconds, ["dps"] = w.NominalDps,
                    ["range"] = w.Range, ["minRange"] = w.MinimumRange,
                    ["cost"] = AssemblyResolver.WeaponCost(weapon, catalog, balance, MaterialTier.Pencil),
                    ["load"] = parts.Sum(p => p.Get(AssemblyStat.Load, ModifierOperation.Add)),
                    ["level"] = parts.Max(p => p.RequiredLevel), ["acquisition"] = w.AcquisitionSeconds,
                    ["radius"] = w.Radius, ["targets"] = targets } });
            }
            foreach (var row in design["towers"])
            {
                var foundation = new FoundationRecipe(Ref(row,"frame"),Ref(row,"hull"),Ref(row,"armor"),Ref(row,"feed"),Ref(row,"turn"));
                var weapon = Weapon(design["weapons"][(int)row["weapon"]]);
                var recipe = new AssemblyRecipe(new TowerRecipe(foundation, Enumerable.Range(0,(int)row["count"]).Select(i => new WeaponMountRecipe(i,weapon))));
                var resolved = ResolveNamed((string)row["id"],recipe,catalog,balance); var numbers = EntryNumbers(resolved, catalog);
                numbers["frontDefense"] = resolved.FrontDefense; numbers["feed"] = resolved.FeedMultiplier;
                numbers["dps"] = resolved.Dps; numbers["range"] = resolved.Weapons.Max(w => w.Range);
                numbers["acquisition"] = resolved.Weapons.Max(w => w.AcquisitionSeconds);
                towers.Add(new JObject { ["id"] = (string)row["id"], ["resolved"] = numbers, ["parts"] = References(resolved.Snapshot.Recipe) });
            }
            foreach (var row in design["buildings"])
            {
                var family = (AssemblyStructureFamily)Enum.Parse(typeof(AssemblyStructureFamily), (string)row["family"], true);
                var recipe = new AssemblyRecipe(new StructureRecipe(family, Ref(row,"site"),Ref(row,"function"),Ref(row,"shell"),Ref(row,"utility")));
                var resolved = ResolveNamed((string)row["id"],recipe,catalog,balance); var numbers = EntryNumbers(resolved,catalog);
                numbers["range"] = resolved.Support.Radius;
                numbers["rate"] = family == AssemblyStructureFamily.Generator ? resolved.Support.IncomePerWave : resolved.Support.RepairPerSecond;
                numbers["slow"] = resolved.Support.SlowSpeedMultiplier; numbers["build"] = resolved.BuildSeconds;
                buildings.Add(new JObject { ["id"] = (string)row["id"], ["resolved"] = numbers, ["parts"] = References(resolved.Snapshot.Recipe) });
            }
            var generator = Resolve(catalog.Starters.Single(e => e.Id == "album.generator").Snapshot.Recipe,catalog,balance).Support;
            var rewards = RunRewardTotals.Calculate(1,false); var victory = RunRewardTotals.Calculate(8,true);
            var profile = WorkshopProfile.CreateNew(catalog);
            var levels = new JArray(); int xp = 0;
            for (int level = 1; level <= 8; level++)
            {
                var progress = MetaProgression.GetLevelProgress(xp);
                levels.Add(new JObject { ["level"] = progress.Level, ["xp"] = progress.LevelStartXp });
                if (progress.NextLevelXp.HasValue) xp = progress.NextLevelXp.Value;
            }
            return new JObject {
                ["schemaVersion"] = 1, ["balanceRevision"] = catalog.BalanceRevision,
                ["definitions"] = definitions, ["weapons"] = weapons, ["towers"] = towers, ["buildings"] = buildings,
                ["starters"] = new JArray(catalog.Starters.Select(s => s.Id)),
                ["rules"] = new JObject {
                    ["slots"] = ProfileLimits.LoadoutSlotCount, ["maxAlbumPages"] = ProfileLimits.MaximumAlbumPages,
                    ["initialGold"] = profile.Gold, ["startingInk"] = balance.StartingInk, ["waves"] = balance.Waves.Count,
                    ["waveGold"] = rewards.WaveGold, ["victoryGold"] = victory.VictoryGold,
                    ["xpPerWave"] = rewards.WaveXp, ["xpVictory"] = victory.VictoryXp,
                    ["maxFeed"] = AssemblyBalanceLimits.MaximumFeed, ["acquisition"] = balance.Construction.AcquisitionSeconds,
                    ["maxDefense"] = AssemblyBalanceLimits.MaximumDefense,
                    ["maxGenerators"] = generator.MaximumInstances, ["generatorRefund"] = generator.RefundFraction,
                    ["generatorMaterial"] = generator.AllowedMaterials.Single().ToString().ToLowerInvariant(),
                    ["towerRefund"] = balance.Material(MaterialTier.Pencil).RefundFraction,
                    ["slowFloor"] = AssemblyBalanceLimits.MinimumSlow, ["geometryPower"] = false
                },
                ["levels"] = levels
            };
        }

        public static string ExportPlayerAssembly()
        {
            var result = CreatePlayerAssembly();
            Directory.CreateDirectory("docs/balance");
            File.WriteAllText(AssemblyExportPath,result.ToString()+Environment.NewLine,new UTF8Encoding(false));
            return AssemblyExportPath;
        }
        public static IReadOnlyList<string> NumericDifferences(JObject design, JObject runtime)
        {
            var differences = new List<string>();
            if ((string)runtime["balanceRevision"] != (string)design["version"]) differences.Add("balanceRevision");
            var expectedDefinitions = new[] { "foundations","cores","weaponParts","buildingParts" }.SelectMany(g => design[g]).ToArray();
            var expectedIds = expectedDefinitions.Select(d => (string)d["id"]).OrderBy(id => id,StringComparer.Ordinal);
            if (!expectedIds.SequenceEqual(runtime["definitions"].Select(d => (string)d["id"]).OrderBy(id => id,StringComparer.Ordinal))) differences.Add("definitions.ids");
            foreach (var definition in runtime["definitions"])
                if (definition["revision"]?.Type != JTokenType.Integer || (int)definition["revision"] != 1) differences.Add((string)definition["id"]+".revision");
            var starters = new[] { "towers","buildings" }.SelectMany(g => design[g]).Where(r => (bool)r["starter"]).Select(r => (string)r["id"]);
            if (!starters.SequenceEqual(runtime["starters"].Values<string>())) differences.Add("starters");
            void Compare(JToken expected, JToken actual, string path)
            {
                if (expected.Type == JTokenType.Integer || expected.Type == JTokenType.Float)
                {
                    double number = actual != null && (actual.Type == JTokenType.Float || actual.Type == JTokenType.Integer) ? (double)actual : double.NaN;
                    double value = (double)expected;
                    if (double.IsNaN(number) || double.IsInfinity(number) || Math.Abs(value-number) > 1e-5+Math.Abs(value)*1e-6) differences.Add(path);
                }
                else if (!JToken.DeepEquals(expected,actual)) differences.Add(path);
            }
            foreach (var property in ((JObject)design["rules"]).Properties()) Compare(property.Value,runtime["rules"]?[property.Name],"rules."+property.Name);
            if (runtime["levels"]?.Count() != design["levels"].Count()) differences.Add("levels.count");
            foreach (var level in design["levels"].Select((row,index) => new {row,index}))
                foreach (string key in new[] { "level","xp" }) Compare(level.row[key],runtime["levels"]?.ElementAtOrDefault(level.index)?[key],"levels."+level.index+"."+key);
            foreach (var starter in AlbumBalanceImport.ReadValidated(design.ToString()).Starters)
            {
                var actual = runtime[starter.Kind == AlbumEntryKind.Tower ? "towers" : "buildings"].FirstOrDefault(r => (string)r["id"] == starter.Id);
                if (!JToken.DeepEquals(References(starter.CreateRecipe()),actual?["parts"])) differences.Add(starter.Id+".parts");
            }
            foreach (string group in new[] { "foundations","cores","weaponParts","buildingParts","weapons","towers","buildings" })
            {
                bool definition = group != "weapons" && group != "towers" && group != "buildings";
                var actual = (JArray)runtime[definition ? "definitions" : group];
                if (!definition && !design[group].Select(r => (string)r["id"]).OrderBy(id => id,StringComparer.Ordinal).SequenceEqual(actual.Select(r => (string)r["id"]).OrderBy(id => id,StringComparer.Ordinal))) differences.Add(group+".ids");
                foreach (var row in design[group])
                {
                    string id = (string)row["id"]; var matches = actual.Where(r => (string)r["id"] == id).ToArray();
                    if (matches.Length != 1) { differences.Add(group+"."+id); continue; }
                    var numbers = definition ? row : row["resolved"];
                    foreach (var p in ((JObject)numbers).Properties().Where(p => p.Value.Type == JTokenType.Integer || p.Value.Type == JTokenType.Float))
                        Compare(p.Value,definition && (p.Name == "level" || p.Name == "gold") ? matches[0][p.Name] : matches[0][definition ? "numeric" : "resolved"]?[p.Name],id+"."+p.Name);
                }
            }
            return differences.AsReadOnly();
        }
        static JObject EntryNumbers(ResolvedEntry e, ComponentCatalog catalog) => new JObject {
            ["cost"] = e.Cost, ["hp"] = e.Health, ["load"] = e.Load, ["capacity"] = e.Capacity, ["defense"] = e.Defense,
            ["level"] = e.Snapshot.Definitions.Max(p => p.RequiredLevel) };
        static ComponentDefinition Get(ComponentCatalog c, PartRef p)
        { if (!c.TryGet(p.Id,p.Revision,out var part)) throw new InvalidOperationException(p.Id); return part; }
        static ResolvedEntry Resolve(AssemblyRecipe recipe, ComponentCatalog catalog, GameBalance balance)
        { var result = AssemblyResolver.Resolve(recipe,catalog,balance,MaterialTier.Pencil); if (!result.Valid) throw new AssemblyCatalogException(result.Diagnostics); return result.Value; }
        static ResolvedEntry ResolveNamed(string id, AssemblyRecipe fallback, ComponentCatalog catalog, GameBalance balance)
        {
            var starter = catalog.Starters.FirstOrDefault(e => e.Id == id);
            if (starter == null) return Resolve(fallback,catalog,balance);
            var result = AssemblyResolver.ResolveEntry(starter,balance,MaterialTier.Pencil);
            if (!result.Valid) throw new AssemblyCatalogException(result.Diagnostics);
            return result.Value;
        }
        static JArray References(AssemblyRecipe recipe) => new JArray(recipe.Parts.Select(p => new JObject { ["id"] = p.Id, ["revision"] = p.Revision }));
        static PartRef Ref(JToken row,string key) => new PartRef((string)row[key],1);
        static WeaponRecipe Weapon(JToken row) => new WeaponRecipe(Ref(row,"core"),Ref(row,"head"),Ref(row,"sight"),Ref(row,"magazine"),Ref(row,"trigger"));
        static FoundationRecipe StandardFoundation() => new FoundationRecipe(new PartRef("frame.standard",1),new PartRef("hull.standard",1),new PartRef("armor.none",1),new PartRef("feed.standard",1),new PartRef("turn.standard",1));
    }
}
