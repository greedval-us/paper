using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Editor;
using PaperDefense.Validation;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Tests
{
    public sealed class AssemblyBalanceAcceptanceTests
    {
        [Test] public void RuntimeExport_MatchesDesignData()
        {
            var design = JObject.Parse(File.ReadAllText(AlbumBalanceImport.SourcePath));
            var runtime = BalanceWorkbookExport.CreatePlayerAssembly();
            Assert.That(BalanceWorkbookExport.NumericDifferences(design,runtime), Is.Empty);
            Assert.That(runtime["definitions"].Count(), Is.EqualTo(54));
            Assert.That(runtime["starters"].Count(), Is.EqualTo(8));
            Assert.That((string)runtime["source"]["balance"], Is.EqualTo(BalanceWorkbookExport.PlayerPath));
            Assert.That((string)runtime["source"]["rules"], Does.EndWith("PlayerRules.asset"));
            Assert.That((string)runtime["source"]["content"], Does.EndWith("PlayerContent.asset"));
            Assert.That((string)runtime["source"]["assemblyCatalog"], Is.EqualTo(AlbumBalanceImport.CatalogPath));
            var starters = runtime["starters"].Values<string>().ToHashSet();
            Assert.That(runtime["towers"].Concat(runtime["buildings"]).Where(r => starters.Contains((string)r["id"]))
                .Sum(r => (int)r["resolved"]["cost"]), Is.EqualTo(426));
        }

        [Test] public void CloneMutation_ReportsBothComponentAndResolvedDamageWithoutTouchingAssets()
        {
            string source = File.ReadAllText(AlbumBalanceImport.SourcePath);
            string assetBefore = File.ReadAllText(AlbumBalanceImport.CatalogPath);
            var input = AlbumBalanceImport.ReadValidated(source);
            input.Definitions.Single(d => d.Id == "core.gun").Modifiers.Single(m => m.Stat == AssemblyStat.Damage).Value += 1;
            var balance = AssetDatabase.LoadAssetAtPath<GameBalanceAsset>(BalanceWorkbookExport.PlayerPath).CreateWorkshopBalance();
            var design = JObject.Parse(source);
            var runtime = BalanceWorkbookExport.CreatePlayerAssembly(design,balance,input.CreateSnapshot());
            var differences = BalanceWorkbookExport.NumericDifferences(design,runtime);
            Assert.That(differences, Does.Contain("core.gun.damage"));
            Assert.That(differences, Does.Contain("weaponRecipe.Пулемёт.damage"));
            Assert.That(differences, Does.Contain("album.guard.dps"));
            Assert.That(File.ReadAllText(AlbumBalanceImport.SourcePath), Is.EqualTo(source));
            Assert.That(File.ReadAllText(AlbumBalanceImport.CatalogPath), Is.EqualTo(assetBefore));
            Assert.That(BalanceWorkbookExport.NumericDifferences(design,BalanceWorkbookExport.CreatePlayerAssembly()), Is.Empty);
        }

        [Test] public void Export_IsDeterministicAndRejectsNonfiniteOrIdentityDrift()
        {
            var first = BalanceWorkbookExport.CreatePlayerAssembly();
            Assert.That(BalanceWorkbookExport.CreatePlayerAssembly().ToString(), Is.EqualTo(first.ToString()));
            var design = JObject.Parse(File.ReadAllText(AlbumBalanceImport.SourcePath));
            first["definitions"][0]["numeric"]["cost"] = double.NaN;
            first["definitions"][1]["revision"] = 2;
            first["starters"][0] = "missing";
            var differences = BalanceWorkbookExport.NumericDifferences(design,first);
            Assert.That(differences, Does.Contain("frame.standard.cost"));
            Assert.That(differences.Any(d => d.EndsWith(".revision")), Is.True);
            Assert.That(differences, Does.Contain("starters"));
        }

        [TestCase(1337,200,TrialScenario.Fast)]
        [TestCase(2026,400,TrialScenario.Crowd)]
        [TestCase(4242,800,TrialScenario.Armored)]
        public void SameInputs_RepeatMeasuredRowsWithoutMutatingRules(int seed,int budget,TrialScenario scenario)
        {
            AssemblyBalanceScenario.LoadRuntime(out var balance,out var catalog);
            string before = JsonUtility.ToJson(balance);
            var first = AssemblyBalanceScenario.Run(balance,catalog,seed,budget,scenario);
            var second = AssemblyBalanceScenario.Run(balance,catalog,seed,budget,scenario);
            Assert.That(first.Select(JsonUtility.ToJson), Is.EqualTo(second.Select(JsonUtility.ToJson)));
            Assert.That(JsonUtility.ToJson(balance), Is.EqualTo(before));
            foreach (var row in first)
            {
                Assert.That(row.Cost, Is.InRange(0,budget));
                Assert.That(row.Cost, Is.EqualTo(row.Copies*row.UnitCost));
                Assert.That(row.Placements.Sum(p => p.Cost), Is.EqualTo(row.Cost));
                Assert.That(row.Damage, Is.GreaterThanOrEqualTo(0));
                Assert.That(row.RepairedHealth, Is.GreaterThanOrEqualTo(0));
                if (row.IncomeApplicable) Assert.That(row.Copies, Is.LessThanOrEqualTo(2));
            }
        }

        [Test] public void Campaign_RepeatsActualEightWavesAndAccountsForPaidSupport()
        {
            AssemblyBalanceScenario.LoadRuntime(out var balance,out var catalog);
            var first = AssemblyBalanceScenario.RunCampaign(balance,catalog,1337,2,true);
            var second = AssemblyBalanceScenario.RunCampaign(balance,catalog,1337,2,true);
            Assert.That(JsonUtility.ToJson(first), Is.EqualTo(JsonUtility.ToJson(second)));
            Assert.That(first.Cost, Is.EqualTo(first.Placements.Sum(p => p.Cost)));
            Assert.That(first.StartingInk, Is.EqualTo(balance.StartingInk));
            Assert.That(first.StartingBaseHealth, Is.EqualTo(balance.BaseHealth));
            Assert.That(first.CompletedWaves, Is.InRange(0,8));
            Assert.That(first.SpawnedByKind.Sum(), Is.EqualTo(first.SpawnedEnemies));
            Assert.That(first.SpawnedByDrawingTier.Sum(), Is.EqualTo(first.SpawnedEnemies));
            Assert.That(first.BaseDamageTaken, Is.EqualTo(first.StartingBaseHealth-first.BaseHealth).Within(.001f));
            Assert.That(first.Income, Is.LessThanOrEqualTo(2*8*12));
            Assert.That(first.NetResource, Is.EqualTo(first.Income-first.Placements.Where(p => p.EntryId == "album.generator").Sum(p => p.Cost)));
            Assert.That(first.LeaksApplicable, Is.False);
        }

        [Test] public void Catalog_CoversSevenCombatFamiliesAndMixedControlAndCleaning()
        {
            AssemblyBalanceScenario.LoadRuntime(out var balance,out var catalog);
            var entries = AssemblyBalanceScenario.TowerEntries(catalog);
            var resolutions = entries.Select(e => TowerCatalog.ResolveEntry(e,balance)).ToArray();
            Assert.That(resolutions.All(r => r.Valid), Is.True);
            Assert.That(resolutions.SelectMany(r => r.Value.Weapons).Select(w => w.Family).Distinct().Count(), Is.EqualTo(7));
            Assert.That(entries.Count(e => e.Snapshot.Recipe.Tower.Mounts.Count > 1), Is.GreaterThanOrEqualTo(4));
        }

        [Test] public void StarterRecipeAndProgressionDrift_CannotHideBehindUnchangedIds()
        {
            var design = JObject.Parse(File.ReadAllText(AlbumBalanceImport.SourcePath));
            var input = AlbumBalanceImport.ReadValidated(design.ToString());
            input.Starters.Single(e => e.Id == "album.guard").Mounts[0].Weapon = input.Starters.Single(e => e.Id == "album.piercer").Mounts[0].Weapon;
            AssemblyBalanceScenario.LoadRuntime(out var balance,out _);
            var runtime = BalanceWorkbookExport.CreatePlayerAssembly(design,balance,input.CreateSnapshot());
            Assert.That(BalanceWorkbookExport.NumericDifferences(design,runtime), Does.Contain("album.guard.dps"));
            runtime = BalanceWorkbookExport.CreatePlayerAssembly(); runtime["levels"][1]["xp"] = 999;
            Assert.That(BalanceWorkbookExport.NumericDifferences(design,runtime), Does.Contain("levels.1.xp"));
        }

        [Test] public void Dominance_ConsidersCleaningUtilityAcrossAllComparableScenarios()
        {
            AssemblyBalanceRow Row(string id,string scenario,float damage,int cleaned) => new AssemblyBalanceRow {
                Fixture = "FiringLane", Role = "Tower", RecipeId = id, Scenario = scenario, Seed = 1337, Budget = 200,
                DamageApplicable = true, LeaksApplicable = true, Damage = damage, Cost = 100, Copies = 2,
                CleaningApplicable = scenario == "Blots", BlotsCleared = cleaned, UnresolvedBlots = 10-cleaned };
            var rows = new[] { Row("attack","Fast",20,0),Row("mixed","Fast",10,0),Row("attack","Blots",0,0),Row("mixed","Blots",0,10) };
            Assert.That(AssemblyBalanceScenario.FindUniversalDominance(rows), Is.Empty,
                "Greater damage alone cannot dominate a mixed tower that cleans blots.");
        }
    }
}
