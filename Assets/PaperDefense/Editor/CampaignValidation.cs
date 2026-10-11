using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using PaperDefense.Core;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Editor
{
    /// <summary>Replays a complete campaign using only public commands and earned resources.</summary>
    public static class CampaignValidation
    {
        private const int Seed = 1337;
        private const float DeltaSeconds = 1f / 30f;
        private const int MaxTicksPerWave = 18000;
        private static readonly int[] Radii = { 3, 4, 6, 7, 8, 9 };
        private static readonly Cell[] Directions =
        {
            new Cell(-1, 0), new Cell(1, 0), new Cell(0, -1), new Cell(0, 1),
            new Cell(-1, -1), new Cell(1, -1), new Cell(-1, 1), new Cell(1, 1)
        };

        [MenuItem("Paper Defense/Проверить все 8 волн")]
        public static void Run()
        {
            var balance = GameBalance.CreateDefault();
            var game = new GameSession(balance, Seed, true);
            var candidates = CandidateCells(game.BaseCell);
            var report = new CampaignReport
            {
                runAtUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = UnityEngine.Application.unityVersion,
                seed = Seed,
                fixedDeltaSeconds = DeltaSeconds,
                maxTicksPerWave = MaxTicksPerWave,
                startingInk = game.Ink,
                startingBaseHealth = game.BaseHealth,
                starterStructures = game.Structures.Count,
                radii = Radii,
                strategy = "Before each wave, choose DeepInk if offered. Visit radii 3,4,6,7,8,9 around the base in W,E,S,N,SW,SE,NW,NE order. Skip occupied cells. Buy three pencil mortars then one pencil piercer, repeating by cumulative successful purchase count. Stop purchasing when ink is below 90, including before a piercer. Launch immediately. Spend only the default starting ink, passive supply, kills, wave grants, and bounded early-launch grants."
            };
            var timer = Stopwatch.StartNew();
            int purchasedWeapons = 0;
            for (int waveNumber = 1; waveNumber <= balance.Waves.Count; waveNumber++)
            {
                if (game.PendingUpgrade)
                {
                    report.upgradeAfterWaves.Add(game.WaveIndex);
                    game.ChooseUpgrade(RunUpgrade.DeepInk);
                }
                foreach (var cell in candidates)
                {
                    if (game.Ink < 90) break;
                    if (game.StructureAt(cell) != null) continue;
                    var kind = purchasedWeapons % 4 == 3 ? StructureKind.Piercer : StructureKind.Mortar;
                    int beforeInk = game.Ink;
                    if (!game.TryBuild(new[] { cell }, kind, MaterialTier.Pencil, out _)) continue;
                    purchasedWeapons++;
                    report.purchases.Add(new PurchaseRecord
                    {
                        beforeWave = waveNumber, x = cell.X, y = cell.Y, kind = kind.ToString(),
                        tier = MaterialTier.Pencil.ToString(), cost = beforeInk - game.Ink, inkAfter = game.Ink
                    });
                }
                game.StartNextWave();
                int ticks = 0;
                while (ticks < MaxTicksPerWave && game.Phase == RunPhase.Assault)
                { game.Tick(DeltaSeconds); ticks++; }
                report.waves.Add(new WaveRecord
                {
                    wave = waveNumber, title = balance.Waves[waveNumber - 1].Title,
                    phase = game.Phase.ToString(), baseHealth = game.BaseHealth, kills = game.Kills,
                    purchasedWeapons = purchasedWeapons, survivingStructures = game.Structures.Count,
                    ink = game.Ink, elapsedSeconds = game.Elapsed, ticks = ticks,
                    timedOut = game.Phase == RunPhase.Assault
                });
                if (game.Phase != RunPhase.Intermission) break;
            }
            timer.Stop();
            int expectedKills = 0;
            foreach (var wave in balance.Waves) expectedKills += wave.Count;
            report.phase = game.Phase.ToString();
            report.kills = game.Kills;
            report.expectedKills = expectedKills;
            report.baseHealth = game.BaseHealth;
            report.elapsedSeconds = game.Elapsed;
            report.purchasedWeapons = purchasedWeapons;
            report.survivingStructures = game.Structures.Count;
            report.ink = game.Ink;
            report.wallTimeMilliseconds = timer.Elapsed.TotalMilliseconds;
            report.passed = game.Phase == RunPhase.Victory && game.BaseHealth > 0 &&
                game.Kills == expectedKills && report.waves.Count == balance.Waves.Count;

            string path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "docs", "validation", "campaign-smoke.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            string message = $"Paper Defense campaign validation: passed={report.passed}, phase={report.phase}, " +
                $"kills={report.kills}/{report.expectedKills}, base={report.baseHealth}, seconds={report.elapsedSeconds:F1}. Report: {path}";
            if (!report.passed)
            {
                UnityEngine.Debug.LogError(message);
                throw new InvalidOperationException(message);
            }
            UnityEngine.Debug.Log(message);
        }

        private static List<Cell> CandidateCells(Cell center)
        {
            var cells = new List<Cell>();
            foreach (int radius in Radii)
                foreach (var direction in Directions)
                    cells.Add(new Cell(center.X + direction.X * radius, center.Y + direction.Y * radius));
            return cells;
        }

        [Serializable]
        private sealed class CampaignReport
        {
            public string runAtUtc, unityVersion, strategy, phase;
            public int seed, maxTicksPerWave, startingInk, starterStructures, expectedKills;
            public int kills, purchasedWeapons, survivingStructures, ink;
            public float fixedDeltaSeconds, startingBaseHealth, baseHealth, elapsedSeconds;
            public double wallTimeMilliseconds;
            public bool passed;
            public int[] radii;
            public List<int> upgradeAfterWaves = new List<int>();
            public List<WaveRecord> waves = new List<WaveRecord>();
            public List<PurchaseRecord> purchases = new List<PurchaseRecord>();
        }

        [Serializable]
        private sealed class WaveRecord
        {
            public int wave, kills, purchasedWeapons, survivingStructures, ink, ticks;
            public string title, phase;
            public float baseHealth, elapsedSeconds;
            public bool timedOut;
        }

        [Serializable]
        private sealed class PurchaseRecord
        {
            public int beforeWave, x, y, cost, inkAfter;
            public string kind, tier;
        }
    }
}
