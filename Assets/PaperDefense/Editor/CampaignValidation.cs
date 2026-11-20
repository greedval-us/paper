using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PaperDefense.Core;
using PaperDefense.Validation;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Editor
{
    /// <summary>Exercises all nine authored missions with paid starter assemblies and paired undefended controls.</summary>
    public static class CampaignValidation
    {
        public const string ReportPath = "docs/validation/campaign-playability.json";

        [Serializable]
        public sealed class Report
        {
            public int SchemaVersion = 5;
            public string RunAtUtc, UnityVersion, Source, Strategy;
            public float StepSeconds;
            public int MaximumTicksPerWave;
            public double MaximumWallSecondsPerMission;
            public bool Passed, RoleComparisonsPassed;
            public List<CampaignPlayabilityResult> Missions = new List<CampaignPlayabilityResult>();
            public List<CampaignPlayabilityResult> StrategyComparisons = new List<CampaignPlayabilityResult>();
        }

        [MenuItem("Paper Defense/Проверить кампанию · 9 страниц")]
        public static void Run()
        {
            AssemblyBalanceScenario.LoadRuntime(out var baseline, out var catalog);
            var report = new Report {
                RunAtUtc = DateTime.UtcNow.ToString("O"), UnityVersion = UnityEngine.Application.unityVersion,
                Source = "PlayerBalance -> PlayerRules -> PlayerContent -> PlayerAssemblyCatalog -> MissionBalanceFactory",
                StepSeconds = CampaignPlayabilityScenario.StepSeconds,
                MaximumTicksPerWave = CampaignPlayabilityScenario.MaximumTicksPerWave,
                MaximumWallSecondsPerMission = CampaignPlayabilityScenario.MaximumWallSeconds,
                Strategy = "Each defense uses the eight starter album pages, the mission seed and its first-traversal technology rank. Every structure and rank costs the ordinary player price. After each cleared wave the player claims an earned flask, cycling green, red and blue. The mixed strategy uses its ink for tower color upgrades, builds new positions and buys ordinary ranks. Expansion buys basic copies. Concentration develops its initial positions. Each policy launches after ten seconds of planning. The report records purchases, work completions and flask payouts."
            };
            report.Passed = true;
            report.RoleComparisonsPassed = true;
            foreach (var mission in CampaignCatalog.Default.Missions)
            {
                foreach (bool defend in new[] { true, false })
                {
                    var result = CampaignPlayabilityScenario.Run(baseline, catalog, mission, defend);
                    report.Missions.Add(result);
                    report.Passed &= result.Passed;
                    Debug.Log($"Campaign {mission.Id}/{result.Policy}: {result.Outcome}, waves={result.CompletedWaves}/{result.PlannedWaves}, HP={result.BaseHealth:F1}, ink={result.Ink}, paid={result.PaidInk}, builds={result.Builds.Count}");
                }
                foreach (var policy in new[] { CampaignDefensePolicy.Expansion, CampaignDefensePolicy.Concentrated })
                    report.StrategyComparisons.Add(CampaignPlayabilityScenario.Run(baseline, catalog, mission, policy: policy));
            }
            var firstExpansion = report.StrategyComparisons.First(r => r.MissionId == "notebook-1-page-2" && r.Policy == "field-expansion-v2");
            var firstConcentration = report.StrategyComparisons.First(r => r.MissionId == "notebook-1-page-2" && r.Policy == "field-concentrated-v2");
            var coverageExpansion = report.StrategyComparisons.First(r => r.MissionId == "notebook-1-page-3" && r.Policy == "field-expansion-v2");
            var coverageConcentration = report.StrategyComparisons.First(r => r.MissionId == "notebook-1-page-3" && r.Policy == "field-concentrated-v2");
            report.RoleComparisonsPassed = firstConcentration.Passed && firstExpansion.Passed &&
                firstConcentration.PaidInk < firstExpansion.PaidInk && firstConcentration.Builds.Count < firstExpansion.Builds.Count &&
                coverageExpansion.CompletedWaves > coverageConcentration.CompletedWaves &&
                report.Missions.Where(r => r.Outcome == "Victory").All(r => r.Waves.All(w => w.PeakAlive >= 200) &&
                    r.Flasks.Count == r.CompletedWaves - 1 &&
                    r.Flasks.All(f => f.Amount == 12) && r.Flasks.Select(f => f.Color).Distinct().Count() == 3) &&
                report.Missions.Where(r => r.Outcome == "Victory").SelectMany(r => r.Modernizations)
                    .Where(u => u.IsColor && u.CompletedAt >= 0).Select(u => u.Color).Distinct().Count() == 3 &&
                report.Missions.Concat(report.StrategyComparisons).All(r => r.GlobalUpgradeChoices == 0 && r.FieldModernization);

            report.Passed &= report.RoleComparisonsPassed;
            string path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", ReportPath));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            if (!report.Passed) throw new InvalidOperationException("Campaign playability failed. Report: " + path);
            Debug.Log("Nine paid-assembly victories and nine undefended defeats verified. Report: " + path);
        }
    }
}
