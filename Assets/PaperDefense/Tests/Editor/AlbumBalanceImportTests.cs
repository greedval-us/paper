using System;
using System.IO;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Editor;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AlbumBalanceImportTests
    {
        const string Target = "Assets/PaperDefense/Tests/Editor/TemporaryAssemblyCatalog.asset";
        static string Source => File.ReadAllText("docs/balance/album-balance-v1.json");

        [TearDown] public void Cleanup() { AssetDatabase.DeleteAsset(Target); }

        [TestCase("ru-RU")]
        [TestCase("en-US")]
        public void ImportNumbers_IsIndependentOfEditorCulture(string culture)
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var catalog = AlbumBalanceImport.Import(Source, Target).CreateSnapshot();
                catalog.TryGet("frame.standard", 1, out var frame);
                Assert.That(frame.Get(AssemblyStat.Feed, ModifierOperation.Set), Is.EqualTo(1.6f));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test] public void ValidSource_ImportsAndReimportsWithoutChangingGuidOrData()
        {
            Assert.That(AlbumBalanceImport.Validate(Source), Is.Empty);
            var preview = AlbumBalanceImport.Preview(Source, Target);
            Assert.That(preview.Added.Count, Is.EqualTo(54));
            var imported = AlbumBalanceImport.Import(Source, Target);
            string guid = AssetDatabase.AssetPathToGUID(Target);
            string before = File.ReadAllText(Target);
            Assert.That(imported.CreateSnapshot().Starters.Count, Is.EqualTo(8));
            Assert.That(AlbumBalanceImport.Preview(Source, Target).Changed, Is.Empty);
            AlbumBalanceImport.Import(Source, Target);
            Assert.That(AssetDatabase.AssetPathToGUID(Target), Is.EqualTo(guid));
            Assert.That(File.ReadAllText(Target), Is.EqualTo(before));
            var changed = JObject.Parse(Source);
            changed["foundations"][0]["cost"] = 17;
            Assert.That(AlbumBalanceImport.Preview(changed.ToString(), Target).Changed, Does.Contain("frame.standard@1"));
        }

        [TestCase("duplicate", "assembly.component.duplicate")]
        [TestCase("category", "assembly.category.missing")]
        [TestCase("gold", "assembly.unlock.negative")]
        [TestCase("nonfinite", "assembly.number.nonfinite")]
        [TestCase("compatibility", "assembly.compatibility.unknown")]
        [TestCase("part", "assembly.part.missing")]
        [TestCase("wrongCategory", "assembly.part.category")]
        [TestCase("weaponIndex", "assembly.recipe.weapon_index")]
        [TestCase("mountCount", "assembly.recipe.mount_count")]
        [TestCase("version", "assembly.balance.version")]
        [TestCase("versionObject", "assembly.balance.version")]
        [TestCase("versionArray", "assembly.balance.version")]
        [TestCase("fractionalGold", "assembly.number.range")]
        [TestCase("array", "assembly.source.array")]
        [TestCase("multiplier", "assembly.number.range")]
        public void InvalidImport_ReportsStableCodeAndPreservesExistingAsset(string mutation, string code)
        {
            AlbumBalanceImport.Import(Source, Target);
            string before = File.ReadAllText(Target), guid = AssetDatabase.AssetPathToGUID(Target);
            var json = JObject.Parse(Source);
            switch (mutation)
            {
                case "duplicate": ((JArray)json["foundations"]).Add(json["foundations"][0].DeepClone()); break;
                case "category": ((JArray)json["foundations"]).Remove(json["foundations"].First(x => (string)x["id"] == "hull.standard"));
                    ((JArray)json["foundations"]).Remove(json["foundations"].First(x => (string)x["id"] == "hull.light"));
                    ((JArray)json["foundations"]).Remove(json["foundations"].First(x => (string)x["id"] == "hull.reinforced")); break;
                case "gold": json["foundations"][0]["gold"] = -1; break;
                case "nonfinite": json["weaponParts"][0]["damageMult"] = double.NaN; break;
                case "compatibility": json["weaponParts"][0]["compatible"] = "core.unknown"; break;
                case "part": json["towers"][0]["frame"] = "frame.unknown"; break;
                case "wrongCategory": json["towers"][0]["frame"] = "hull.standard"; break;
                case "weaponIndex": json["towers"][0]["weapon"] = 100; break;
                case "mountCount": json["towers"][0]["count"] = 4; break;
                case "version": json["version"] = "album-future"; break;
                case "versionObject": json["version"] = new JObject(); break;
                case "versionArray": json["version"] = new JArray(); break;
                case "fractionalGold": json["foundations"][0]["gold"] = 1.0000000001; break;
                case "array": ((JObject)json).Remove("buildingParts"); break;
                case "multiplier": json["weaponParts"][0]["damageMult"] = -1; break;
            }
            var diagnostics = AlbumBalanceImport.Validate(json.ToString());
            Assert.That(diagnostics.Select(x => x.Code), Does.Contain(code));
            Assert.That(diagnostics.All(x => !string.IsNullOrWhiteSpace(x.Path)), Is.True);
            Assert.Throws<AssemblyCatalogException>(() => AlbumBalanceImport.Import(json.ToString(), Target));
            Assert.That(File.ReadAllText(Target), Is.EqualTo(before));
            Assert.That(AssetDatabase.AssetPathToGUID(Target), Is.EqualTo(guid));
            Assert.That(AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(Target).CreateSnapshot().Definitions.Count, Is.EqualTo(54));
        }

        [Test] public void SignedDiscountsAndTypedFamilies_ArePreserved()
        {
            var catalog = AlbumBalanceImport.Import(Source, Target).CreateSnapshot();
            Assert.That(catalog.TryGet("head.short", 1, out var head), Is.True);
            Assert.That(head.Get(AssemblyStat.Cost, ModifierOperation.Add), Is.EqualTo(-4));
            Assert.That(head.WeaponFamilies, Is.EqualTo(AssemblyWeaponFamily.Gun | AssemblyWeaponFamily.Piercer));
            Assert.That(catalog.TryGet("trigger.burst", 1, out var trigger), Is.True);
            Assert.That(trigger.RequiredParts.Single().Id, Is.EqualTo("magazine.standard"));
            Assert.That(catalog.TryGet("core.mortar", 1, out var mortar), Is.True);
            Assert.That(mortar.Get(AssemblyStat.TargetLimit, ModifierOperation.Set), Is.Zero);
            Assert.That(mortar.Get(AssemblyStat.EvaluationTargets, ModifierOperation.Set), Is.EqualTo(8));
        }

        [Test] public void MalformedJson_ReturnsDiagnosticAndDoesNotCreateAsset()
        {
            Assert.That(AlbumBalanceImport.Validate("{broken").Select(x => x.Code), Does.Contain("assembly.source.json"));
            Assert.Throws<AssemblyCatalogException>(() => AlbumBalanceImport.Import("{broken", Target));
            Assert.That(File.Exists(Target), Is.False);
        }
    }
}
