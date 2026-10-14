using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PaperDefense.Validation
{
    /// <summary>Opt-in rendered Player measurement; never active in an ordinary game.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class PressurePlayerProbe : MonoBehaviour
    {
        const int MaxFrames = 36000;
        readonly List<FrameSample> frames = new List<FrameSample>(MaxFrames);
        readonly FixedStepClock clock = new FixedStepClock();
        readonly Stopwatch elapsed = new Stopwatch();
        readonly WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
        PressureScenario scenario;
        NotebookWorldView view;
        ProfilerRecorder gc, mainThread;
        string outputPath;
        int speed, stepsThisFrame;
        long eventCursor;
        double tickMilliseconds, tickTotalMilliseconds, maxTickMilliseconds;
        bool complete, finishing, hasPendingSample, measuring;
        FrameSample pendingSample;
        RunController profileController;
        ProfileSaveState profileStateAtStart;

        public static bool IsWorkshopProfileIsolated(ProfileSaveState atStart,ProfileSaveState atEnd)
            =>atStart==ProfileSaveState.MemoryOnly && atEnd==ProfileSaveState.MemoryOnly;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-paper-pressure") < 0) return;
            new GameObject("Validation — rendered pressure scenario").AddComponent<PressurePlayerProbe>();
        }

        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            speed = Argument(args, "-pressure-speed", "1") == "2" ? 2 : 1;
            outputPath = Path.GetFullPath(Argument(args, "-pressure-report", "pressure-player.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            var bootstrap = FindAnyObjectByType<PaperDefenseBootstrap>();
            if (bootstrap == null) throw new InvalidOperationException("Pressure probe requires the notebook scene.");
            profileController=bootstrap.Controller;
            profileStateAtStart=profileController.Workshop.SaveState;
            bootstrap.Controller.enabled = false;
            scenario = new PressureScenario();
            bootstrap.Controller.Initialize(scenario.Session);
            if (speed == 2) bootstrap.Controller.ToggleSpeed();
            view = bootstrap.View;
            QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = 60;
            UnityEngine.Application.runInBackground = true;
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            // Do not retain the initialization frame, including its later delta/GC sample.
            yield return endOfFrame;
            elapsed.Start();
            measuring = true;
            while (!complete)
            {
                yield return endOfFrame;
                if (scenario == null) continue;
                // Frame delta and profiler recorders describe the preceding completed frame.
                // Match them with that frame's work/state, rather than this frame's tick.
                if (hasPendingSample && frames.Count < MaxFrames)
                {
                    pendingSample.frameMilliseconds = Time.unscaledDeltaTime * 1000d;
                    pendingSample.mainThreadMilliseconds = mainThread.Valid ? mainThread.LastValue / 1000000d : -1;
                    pendingSample.allocatedBytes = gc.Valid ? gc.LastValue : -1;
                    frames.Add(pendingSample);
                }
                if (finishing)
                {
                    complete = true;
                    WriteReport();
                    if (!UnityEngine.Application.isEditor) UnityEngine.Application.Quit(scenario.Report.Passed ? 0 : 2);
                    continue;
                }
                var state = scenario.ReadModel;
                pendingSample = new FrameSample
                    {
                        frame = frames.Count, tick = state.TickIndex, alive = state.Enemies.Count,
                        structures = state.Structures.Count, projectiles = state.Projectiles.Count,
                        simulationSteps = stepsThisFrame,
                        tickMilliseconds = tickMilliseconds, meshMilliseconds = view.LastMeshBuildMilliseconds,
                        navigationRebuilds = state.Statistics.NavigationRebuilds
                    };
                hasPendingSample = true;
                if (scenario.Finished || elapsed.Elapsed.TotalSeconds > 600 || frames.Count >= MaxFrames)
                    finishing = true;
            }
        }

        void Update()
        {
            if (scenario == null || !measuring || complete || finishing) return;
            int requestedSteps = clock.AdvanceFrame(Time.unscaledDeltaTime, false, speed);
            stepsThisFrame = 0;
            tickMilliseconds = 0;
            for (int i = 0; i < requestedSteps && !scenario.Finished; i++)
            {
                long started = Stopwatch.GetTimestamp();
                scenario.Step();
                stepsThisFrame++;
                double milliseconds = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
                tickMilliseconds += milliseconds;
                tickTotalMilliseconds += milliseconds;
                maxTickMilliseconds = Math.Max(maxTickMilliseconds, milliseconds);
                SimulationEventBuffer events = scenario.Session.EventBuffer;
                for (int j = 0; j < events.Count; j++)
                    if (events[j].Sequence > eventCursor) view.AddEvent(events[j]);
                eventCursor = events.LatestSequence;
            }
        }

        void WriteReport()
        {
            FrameSample[] ordered = frames.ToArray();
            Array.Sort(ordered, (a, b) => b.frameMilliseconds.CompareTo(a.frameMilliseconds));
            ProfileSaveState profileStateAtEnd=profileController.Workshop.SaveState;
            bool profileIsolated=IsWorkshopProfileIsolated(profileStateAtStart,profileStateAtEnd);
            var report = new PlayerPressureReport
            {
                profileSaveStateAtStart=profileStateAtStart.ToString(),
                profileSaveStateAtEnd=profileStateAtEnd.ToString(),workshopProfileMemoryOnly=profileIsolated,
                unityVersion = UnityEngine.Application.unityVersion,
                processor = SystemInfo.processorType, processorCount = SystemInfo.processorCount,
                graphicsDevice = SystemInfo.graphicsDeviceName, operatingSystem = SystemInfo.operatingSystem,
                memoryMegabytes = SystemInfo.systemMemorySize, width = Screen.width, height = Screen.height,
                developmentBuild = Debug.isDebugBuild, speed = speed, rendered = true,
                scope = "Rendered Windows Player, full notebook scene with ordinary HUD and input enabled, automatic replay commands. VSync off, target60. Frame delta includes pacing and is aligned to prior frame work. Profiler recorders sampled for preceding completed frame. Startup/scenario construction and final report serialization excluded. No screenshots during measurement. Do not send manual commands during replay; compare state fingerprint with headless replay."+
                    (profileIsolated?" Workshop profile was MemoryOnly before the scenario rebind and at measurement end; workshop profile load/save is isolated.":" Workshop profile I/O was not isolated; see the recorded start/end states."),
                realSeconds = elapsed.Elapsed.TotalSeconds, discardedSimulationSeconds = clock.DiscardedSeconds,
                completed = scenario.Finished, configuration = scenario.Config, commands = scenario.Commands.ToArray(),
                simulation = scenario.Report, allFrames = Summarize(frames, 0),
                swarm3000Frames = Summarize(frames, 3000),
                gcRecorderAvailable = gc.Valid, mainThreadRecorderAvailable = mainThread.Valid,
                tickTotalMilliseconds = tickTotalMilliseconds, maxTickMilliseconds = maxTickMilliseconds,
                navigationRebuilds = scenario.ReadModel.Statistics.NavigationRebuilds,
                navigationTotalMilliseconds = scenario.ReadModel.Statistics.NavigationTotalMilliseconds,
                navigationMaxMilliseconds = scenario.ReadModel.Statistics.NavigationMaxMilliseconds,
                meshBuilds = view.MeshBuildCount, staticStructureBuilds = view.StaticStructureBuildCount,
                meshTotalMilliseconds = view.TotalMeshBuildMilliseconds,
                heaviestFrames = new FrameSample[Math.Min(20, ordered.Length)], samples = frames.ToArray()
            };
            Array.Copy(ordered, report.heaviestFrames, report.heaviestFrames.Length);
            File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
            Debug.Log("Pressure Player report: " + outputPath + "; phase=" + scenario.ReadModel.Phase + "; peak=" + scenario.ReadModel.Statistics.PeakAliveEnemies);
        }

        static FrameSummary Summarize(List<FrameSample> source, int minimumAlive)
        {
            var times = new List<double>();
            double total = 0, allocated = 0, mesh = 0, ticks = 0;
            long maxAllocated = 0;
            foreach (var frame in source)
            {
                if (frame.alive < minimumAlive) continue;
                times.Add(frame.frameMilliseconds); total += frame.frameMilliseconds;
                allocated += Math.Max(0, frame.allocatedBytes); maxAllocated = Math.Max(maxAllocated, frame.allocatedBytes);
                mesh += frame.meshMilliseconds; ticks += frame.tickMilliseconds;
            }
            times.Sort();
            if (times.Count == 0) return new FrameSummary();
            return new FrameSummary { frames = times.Count, averageMilliseconds = total / times.Count,
                p95Milliseconds = times[(int)((times.Count - 1) * .95)], p99Milliseconds = times[(int)((times.Count - 1) * .99)],
                maxMilliseconds = times[times.Count - 1], averageAllocatedBytes = allocated / times.Count,
                maxAllocatedBytes = maxAllocated, averageMeshMilliseconds = mesh / times.Count,
                averageTickMillisecondsPerFrame = ticks / times.Count };
        }

        static string Argument(string[] args, string key, string fallback)
        { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }

        void OnGUI()
        {
            if (scenario == null) return;
            GUI.Label(new Rect(12, 12, 800, 28), "PRESSURE VALIDATION  ×" + speed + "    alive " + scenario.ReadModel.Enemies.Count
                + "    tick " + scenario.ReadModel.TickIndex + "    " + scenario.ReadModel.Phase);
        }

        void OnDestroy() { gc.Dispose(); mainThread.Dispose(); }

        [Serializable] public struct FrameSample
        {
            public int frame, alive, structures, projectiles, simulationSteps, navigationRebuilds;
            public long tick, allocatedBytes;
            public double frameMilliseconds, mainThreadMilliseconds, tickMilliseconds, meshMilliseconds;
        }
        [Serializable] public sealed class FrameSummary
        {
            public int frames;
            public double averageMilliseconds, p95Milliseconds, p99Milliseconds, maxMilliseconds,
                averageAllocatedBytes, averageMeshMilliseconds, averageTickMillisecondsPerFrame;
            public long maxAllocatedBytes;
        }
        [Serializable] public sealed class PlayerPressureReport
        {
            public string profileSaveStateAtStart,profileSaveStateAtEnd;
            public bool workshopProfileMemoryOnly;
            public string unityVersion, processor, graphicsDevice, operatingSystem, scope;
            public int processorCount, memoryMegabytes, width, height, speed, navigationRebuilds, meshBuilds, staticStructureBuilds;
            public bool developmentBuild, rendered, completed, gcRecorderAvailable, mainThreadRecorderAvailable;
            public double realSeconds, discardedSimulationSeconds, tickTotalMilliseconds, maxTickMilliseconds,
                navigationTotalMilliseconds, navigationMaxMilliseconds, meshTotalMilliseconds;
            public PressureScenarioConfig configuration;
            public PressureScenarioCommand[] commands;
            public PressureScenarioReport simulation;
            public FrameSummary allFrames, swarm3000Frames;
            public FrameSample[] heaviestFrames, samples;
        }
    }
}
