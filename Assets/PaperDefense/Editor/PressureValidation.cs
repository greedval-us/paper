using System;
using System.Diagnostics;
using System.IO;
using PaperDefense.Validation;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Editor
{
    public static class PressureValidation
    {
        [MenuItem("Paper Defense/Проверить естественный пролом · 3000 врагов")]
        public static void Run()
        {
            var timer = Stopwatch.StartNew();
            PressureScenario normal = Replay(1);
            PressureScenario accelerated = Replay(2);
            timer.Stop();
            var report = new PressureReplayReport
            {
                runAtUtc = DateTime.UtcNow.ToString("O"), unityVersion = UnityEngine.Application.unityVersion,
                scope = "Headless core replay in Editor. The second run groups two unchanged fixed steps per simulated rendering frame; it does not measure Player performance.",
                configuration = normal.Config, commands = normal.Commands.ToArray(),
                normal = normal.Report, accelerated = accelerated.Report,
                identicalState = normal.Report.Tick == accelerated.Report.Tick && normal.Report.Fingerprint == accelerated.Report.Fingerprint,
                wallTimeMilliseconds = timer.Elapsed.TotalMilliseconds
            };
            report.passed = normal.Report.Passed && accelerated.Report.Passed && report.identicalState;
            string path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "docs", "validation", "pressure-replay.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            string message = "Pressure replay: passed=" + report.passed + ", phase=" + normal.Report.Phase +
                ", peak=" + normal.Report.PeakAliveEnemies + ", kills=" + normal.Report.Kills +
                ", breachTick=" + normal.Report.FirstBreachTick + ", fingerprint=" + normal.Report.Fingerprint + ". Report: " + path;
            if (!report.passed) throw new InvalidOperationException(message);
            UnityEngine.Debug.Log(message);
        }

        private static PressureScenario Replay(int stepsPerFrame)
        {
            var scenario = new PressureScenario();
            while (!scenario.Finished)
                for (int i = 0; i < stepsPerFrame && !scenario.Finished; i++) scenario.Step();
            return scenario;
        }

        [Serializable] private sealed class PressureReplayReport
        {
            public string runAtUtc, unityVersion, scope;
            public bool passed, identicalState;
            public double wallTimeMilliseconds;
            public PressureScenarioConfig configuration;
            public PressureScenarioCommand[] commands;
            public PressureScenarioReport normal, accelerated;
        }
    }
}
