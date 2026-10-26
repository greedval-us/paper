using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PaperDefense.Application;
using PaperDefense.Core;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PaperDefense.Presentation
{
    /// <summary>
    /// Explicit built-player acceptance instrumentation. Ordinary launches create no probe.
    /// UI actions submit the production Button through EventSystem; no balance, rewards,
    /// profile data or simulation internals are patched by this probe.
    /// </summary>
    public sealed class AlbumReleaseProbe : MonoBehaviour
    {
        const string Flag = "-paper-release-probe";
        const int WarmupFrames = 60, SampleFrames = 300, SettleFrames = 30;
        string[] arguments;
        string output;
        PlayerGameRoot game;
        Report report;
        readonly List<string> actions = new List<string>();

        [Serializable] public sealed class Metric
        {
            public string source, unit;
            public bool available;
            public int samples;
            public double mean, p50, p95, maximum;
        }
        [Serializable] public sealed class Measurement
        {
            public string scenario = "production first wave; seed1337; no placed defenses; normal automatic simulation";
            public int warmupFrames = WarmupFrames, requestedFrames = SampleFrames, firstEnemyCount, lastEnemyCount;
            public long firstTick, lastTick;
            public int gen0Collections, gen1Collections, gen2Collections;
            public string collectionCountSource = "System.GC.CollectionCount(n) cumulative deltas; generation counts can describe the same collection on Mono and must not be summed";
            public bool frameTimingEnabled, allocationCounterSanityVerified;
            public int allocationCounterThreadId;
            public Metric cpuFrame, cpuMainThread, cpuMainThreadWork, gpu, gcAllocated, mainThreadManagedAllocated;
        }
        [Serializable] public sealed class Lifecycle
        {
            public int cycle, sceneGameObjects, sceneComponents, workshopChanged, sessionChanged, simulationEvents, selectionChanged;
            public long managedUsedBytes, totalAllocatedBytes;
            public bool eventObservationAvailable;
        }
        [Serializable] public sealed class ProfileSnapshot
        {
            public int gold, totalXp;
            public string saveState;
            public string[] ownedParts, slots, entries, library;
        }
        [Serializable] public sealed class Report
        {
            public string format = "paper-album-release-probe-v1", utc, unity, version, operatingSystem, cpu, gpu, graphicsApi;
            public string mode, error = "", observationScope = "EventSystem submit actions; scene objects/components; four named controller/input C# events. Does not count every possible subscription.";
            public bool passed, journeyComplete, lifecycleStable, performanceComplete, isEditor, developmentBuild, focused;
            public int width, height, quality, vSyncCount, targetFrameRate;
            public string[] actions;
            public Measurement measurement;
            public Lifecycle[] lifecycle;
            public ProfileSnapshot profile;
        }

        static bool Has(string[] args, string token) => args != null && Array.IndexOf(args, token) >= 0;
        static string Value(string[] args, string token)
        {
            if (args == null) return null;
            int index = Array.IndexOf(args, token);
            return index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("-", StringComparison.Ordinal) ? args[index + 1] : null;
        }

        public static string ValidateArguments(string[] args)
        {
            if (!Has(args, Flag)) return "The explicit -paper-release-probe flag is required.";
            foreach (string token in new[] { Flag, "-paper-profile-path", "-paper-release-verify-restart" })
            {
                if (args.Count(item => item == token) > 1) return "Duplicate argument: " + token;
                if (Has(args, token) && string.IsNullOrWhiteSpace(Value(args, token))) return "Missing value: " + token;
            }
            if (!Has(args, "-paper-memory-only") && !Has(args, "-paper-profile-path")) return "The probe requires an explicitly isolated profile.";
            if (Has(args, "-paper-profile-path"))
            {
                try
                {
                    string candidate = Path.GetFullPath(Value(args, "-paper-profile-path"));
                    string temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (!Path.IsPathRooted(Value(args, "-paper-profile-path")) || !candidate.StartsWith(temporary, StringComparison.OrdinalIgnoreCase))
                        return "The profile path must be an absolute descendant of the operating-system temporary directory.";
                }
                catch (Exception error) when (error is ArgumentException || error is NotSupportedException || error is PathTooLongException)
                { return "Invalid temporary profile path: " + error.Message; }
            }
            if (Has(args, "-paper-release-verify-restart") && Has(args, "-paper-memory-only")) return "Restart verification requires the same explicit temporary profile store.";
            if (Has(args, "-paper-release-verify-restart") && Has(args, "-paper-release-measure-only")) return "Choose restart verification or measurement mode.";
            return null;
        }

        /// <summary>
        /// Some Mono versions expose this API but return zero. Only a counter that observes
        /// a real allocation is usable. This calibration runs before the measurement warmup.
        /// </summary>
        public static Func<long> CreateVerifiedAllocationReader()
        {
            try
            {
                Func<long> reader = GC.GetAllocatedBytesForCurrentThread;
                long before = reader();
                var allocation = new byte[4096];
                long after = reader();
                GC.KeepAlive(allocation);
                return before >= 0 && after - before >= 4096 ? reader : null;
            }
            catch (Exception error) when (error is NotSupportedException || error is NotImplementedException)
            { return null; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (UnityEngine.Application.isEditor) return;
            string[] args = Environment.GetCommandLineArgs();
            if (!Has(args, Flag)) return;
            string invalid = ValidateArguments(args);
            if (invalid != null) { Debug.LogError("Release probe refused: " + invalid); UnityEngine.Application.Quit(2); return; }
            var host = new GameObject("Explicit album release probe");
            DontDestroyOnLoad(host);
            var probe = host.AddComponent<AlbumReleaseProbe>();
            probe.arguments = args; probe.output = Path.GetFullPath(Value(args, Flag));
        }

        IEnumerator Start()
        {
            report = new Report {
                utc = DateTime.UtcNow.ToString("O"), unity = UnityEngine.Application.unityVersion, version = UnityEngine.Application.version,
                operatingSystem = SystemInfo.operatingSystem, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), isEditor = UnityEngine.Application.isEditor,
                developmentBuild = Debug.isDebugBuild, mode = Has(arguments, "-paper-release-verify-restart") ? "restart" : Has(arguments, "-paper-release-measure-only") ? "measurement" : "journey"
            };
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            float deadline = Time.realtimeSinceStartup + 240;
            var routine = Execute();
            while (true)
            {
                object current;
                try
                {
                    if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Release probe exceeded240 real seconds.");
                    if (!routine.MoveNext()) break;
                    current = routine.Current;
                }
                catch (Exception error) { report.error = error.ToString(); break; }
                yield return current;
            }
            report.actions = actions.ToArray();
            report.passed = report.error.Length == 0;
            try { File.WriteAllText(output, JsonUtility.ToJson(report, true)); }
            catch (Exception error) { Debug.LogError("Cannot write release probe report: " + error); UnityEngine.Application.Quit(2); yield break; }
            Debug.Log("Release probe " + (report.passed ? "passed" : "FAILED") + ": " + output + (report.error.Length == 0 ? "" : "\n" + report.error));
            yield return null;
            UnityEngine.Application.Quit(report.passed ? 0 : 1);
        }

        IEnumerator Execute()
        {
            for (int i = 0; i < 120 && game == null; i++) { game = Object.FindFirstObjectByType<PlayerGameRoot>(); yield return null; }
            Require(game != null && game.Run != null, "Built scene did not create PlayerGameRoot.");
            Require(game.Run.Workshop.SaveState == ProfileSaveState.MemoryOnly || Has(arguments, "-paper-profile-path"), "Profile isolation was not applied before scene startup.");
            Require(game.Run.AssemblyCatalog != null, "Production assembly catalog is missing.");
            report.width = Screen.width; report.height = Screen.height; report.quality = QualitySettings.GetQualityLevel();
            report.vSyncCount = QualitySettings.vSyncCount; report.targetFrameRate = UnityEngine.Application.targetFrameRate; report.focused = UnityEngine.Application.isFocused;
            foreach (var frame in Settle()) yield return frame;
            if (report.mode == "restart")
            {
                var previous = JsonUtility.FromJson<Report>(File.ReadAllText(Path.GetFullPath(Value(arguments, "-paper-release-verify-restart"))));
                Require(previous != null && previous.passed && previous.journeyComplete && previous.profile != null, "Prior successful full journey report is required.");
                report.profile = SnapshotProfile();
                Require(JsonUtility.ToJson(report.profile) == JsonUtility.ToJson(previous.profile), "Restarted store differs in Gold/XP, owned catalog parts, entries, library or eight slots.");
                actions.Add("Separate process restart restored the complete prior profile snapshot.");
                Click("Open album"); Screenshot("restart-album"); foreach (var frame in Settle()) yield return frame;
                report.journeyComplete = true; yield break;
            }
            if (report.mode == "journey") foreach (var frame in InitialJourney()) yield return frame;
            foreach (var frame in MeasureBattle()) yield return frame;
            if (report.mode == "journey") foreach (var frame in RepeatedLifecycle()) yield return frame;
            report.profile = SnapshotProfile();
        }

        IEnumerable InitialJourney()
        {
            Click("Open album");
            string starter = game.Run.AssemblyCatalog.Starters.First(entry => entry.Kind == AlbumEntryKind.Tower).Id;
            Click("Album choose " + starter); Click("Album edit");
            // Let the newly enabled workshop finish its initial responsive layout before
            // inspecting a part; the layout rebuilds the catalog on its first frame.
            foreach (var frame in Settle()) yield return frame;
            Click("Composite step 1"); Click("Composite weapon category Head");
            Click("Component show unavailable");
            var panel = game.GetComponentInChildren<WorkshopHud>(true).Composite;
            var part = panel.DraftSession.CompatibleParts.Where(item => item.Category == ComponentCategory.Head && !game.Run.Workshop.IsUnlocked(item.Id))
                .OrderBy(item => item.RequiredLevel).ThenBy(item => item.UnlockGold).ThenBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault();
            Require(part != null, "Acceptance fixture must include a locked barrel part to inspect.");
            Click("Component inspect " + part.Id); Screenshot("catalog"); foreach (var frame in Settle()) yield return frame;
            string installed = panel.DraftSession.PreviewRecipe.Tower.Mounts.First().Weapon.Head.Id;
            if (Has(arguments, "-paper-memory-only"))
            {
                if (TryFindButton("Component purchase part",out var purchase))
                    Require(!purchase.IsInteractable(), "Fresh memory profile unexpectedly affords the selected locked part.");
                else
                    Require(game.Run.Workshop.Level < part.RequiredLevel || game.Run.Workshop.Gold < part.UnlockGold,
                        "An eligible purchase must remain available in the production catalog.");
                actions.Add("Fresh memory profile: locked purchase is disabled; durable purchase is covered by the separately seeded temporary-store journey.");
            }
            else
            {
                int gold = game.Run.Workshop.Gold, xp = game.Run.Workshop.TotalXp;
                Require(game.Run.Workshop.Level >= part.RequiredLevel && gold >= part.UnlockGold, "Temporary acceptance fixture must legitimately contain enough Gold/XP for the selected part.");
                Click("Component purchase part"); Screenshot("purchase"); foreach (var frame in Settle()) yield return frame;
                Click("Component purchase confirm");
                Require(game.Run.Workshop.IsUnlocked(part.Id) && game.Run.Workshop.Gold == gold - part.UnlockGold && game.Run.Workshop.TotalXp == xp, "Purchase must debit once and preserve XP.");
                Require(panel.DraftSession.PreviewRecipe.Tower.Mounts.First().Weapon.Head.Id == installed, "Purchase must not implicitly install the part.");
                Click("Component select part"); Require(panel.DraftSession.PreviewRecipe.Tower.Mounts.First().Weapon.Head.Id == part.Id, "Explicit part installation did not change the draft.");
            }
            int libraries = game.Run.Workshop.Library.Count;
            Click("Composite save component"); Require(game.Run.Workshop.Library.Count == libraries + 1, "Saving the component did not create one library entry.");
            string library = game.Run.Workshop.Library.Last().Id;
            Click("Composite library"); Screenshot("library"); foreach (var frame in Settle()) yield return frame;
            Click("Composite apply library " + library); Click("Composite step 2"); Click("Composite save");
            Require(panel.DraftSession.LastResult?.Success == true, "The edited album entry was not committed.");
            foreach (var frame in Trial()) yield return frame;
            Click("Composite return"); Require(game.Page == PlayerPage.Album, "Editor did not return to its album entry point.");
            Click("Album filter All");
            var entries = game.Run.AssemblyCatalog.Starters.Take(8).Select(entry => entry.Id).ToArray();
            Require(entries.Length == 8, "Production acceptance requires eight standard pages.");
            for (int slot = 0; slot < 8; slot++)
            {
                Click("Album choose " + entries[slot]); Click("Album assign " + slot);
                if (TryFindButton("Album confirm move", out _)) Click("Album confirm move");
                Require(game.Run.Workshop.LoadoutEntries[slot]?.Id == entries[slot], "Album assignment failed for slot " + slot);
            }
            Require(game.Run.Workshop.LoadoutEntries.All(entry => entry != null), "All eight slots must be populated.");
            Screenshot("eight-slots"); foreach (var frame in Settle()) yield return frame;
            Click("Album close"); Require(game.Page == PlayerPage.Home, "Album did not return home.");
            report.journeyComplete = true;
        }

        IEnumerable Trial()
        {
            var panel = game.GetComponentInChildren<WorkshopHud>(true).Composite;
            int gold = game.Run.Workshop.Gold, xp = game.Run.Workshop.TotalXp;
            Click("Composite trial"); Click("Composite trial run");
            Require(panel.LastAssemblyTrial != null && panel.LastAssemblyTrial.Seed == 1337 && panel.LastAssemblyTrial.Budget == 400, "The production trial did not record its fixed seed/budget.");
            Require(panel.LastAssemblyTrial.Frames.Count > 0, "Trial recording has no rendered frames.");
            Screenshot("trial");
            foreach (var frame in Settle()) yield return frame;
            Click("Composite close trial");
            Require(game.Run.Workshop.Gold == gold && game.Run.Workshop.TotalXp == xp, "The trial must not grant progression rewards.");
        }

        IEnumerable MeasureBattle()
        {
            Click("Open campaign"); Click("Prepare mission"); Click("Start battle");
            Require(game.Page == PlayerPage.Battle && game.Run.UsesAlbumSlots, "Production battle did not use album slots.");
            Click("Launch wave");
            using (var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1))
            using (var cpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Main Thread Frame Time", 1))
            {
                var timing = new FrameTiming[1];
                var cpuFrame = new List<double>(SampleFrames); var cpuMain = new List<double>(SampleFrames);
                var cpuWork = new List<double>(SampleFrames); var gpu = new List<double>(SampleFrames); var allocations = new List<double>(SampleFrames);
                var mainThreadAllocations = new List<double>(SampleFrames);
                var allocationReader = CreateVerifiedAllocationReader();
                int allocationThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                for (int i = 0; i < WarmupFrames; i++) { FrameTimingManager.CaptureFrameTimings(); yield return null; }
                var m = report.measurement = new Measurement { frameTimingEnabled = FrameTimingManager.IsFeatureEnabled(), firstTick = game.Run.Session.TickIndex, firstEnemyCount = game.Run.Session.Enemies.Count,
                    allocationCounterSanityVerified = allocationReader != null, allocationCounterThreadId = allocationThreadId };
                int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
                long previousAllocated = allocationReader?.Invoke() ?? 0;
                ulong timestamp = 0;
                for (int i = 0; i < SampleFrames; i++)
                {
                    FrameTimingManager.CaptureFrameTimings(); yield return null;
                    if (allocationReader != null)
                    {
                        try
                        {
                            long currentAllocated = allocationReader();
                            if (currentAllocated < previousAllocated || System.Threading.Thread.CurrentThread.ManagedThreadId != allocationThreadId)
                            { allocationReader = null; mainThreadAllocations.Clear(); }
                            else { mainThreadAllocations.Add(currentAllocated - previousAllocated); previousAllocated = currentAllocated; }
                        }
                        catch (Exception error) when (error is NotSupportedException || error is NotImplementedException)
                        { allocationReader = null; mainThreadAllocations.Clear(); }
                    }
                    if (cpu.Valid && cpu.Count > 0) cpuMain.Add(cpu.GetSample(cpu.Count - 1).Value / 1000000.0);
                    if (gc.Valid && gc.Count > 0) allocations.Add(gc.GetSample(gc.Count - 1).Value);
                    if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].frameStartTimestamp != timestamp)
                    {
                        var sample = timing[0]; timestamp = sample.frameStartTimestamp;
                        if (sample.cpuFrameTime > 0) cpuFrame.Add(sample.cpuFrameTime);
                        if (sample.cpuMainThreadFrameTime > 0) cpuWork.Add(Math.Max(0, sample.cpuMainThreadFrameTime - sample.cpuMainThreadPresentWaitTime));
                        if (sample.gpuFrameTime > 0) gpu.Add(sample.gpuFrameTime);
                    }
                }
                m.lastTick = game.Run.Session.TickIndex; m.lastEnemyCount = game.Run.Session.Enemies.Count;
                m.gen0Collections = GC.CollectionCount(0) - gen0; m.gen1Collections = GC.CollectionCount(1) - gen1; m.gen2Collections = GC.CollectionCount(2) - gen2;
                m.cpuFrame = Summarize(cpuFrame, "FrameTimingManager.cpuFrameTime", "ms");
                m.cpuMainThread = Summarize(cpuMain, "ProfilerRecorder CPU Main Thread Frame Time", "ms");
                m.cpuMainThreadWork = Summarize(cpuWork, "FrameTimingManager cpuMainThreadFrameTime minus cpuMainThreadPresentWaitTime", "ms");
                m.gpu = Summarize(gpu, "FrameTimingManager.gpuFrameTime (unique timestamps; positive samples only)", "ms");
                m.gcAllocated = Summarize(allocations, "ProfilerRecorder GC Allocated In Frame", "bytes/frame");
                m.mainThreadManagedAllocated = Summarize(mainThreadAllocations,
                    "System.GC.GetAllocatedBytesForCurrentThread cumulative deltas between coroutine frame boundaries; verified4096-byte allocation; main thread only; includes probe overhead; distinct from ProfilerRecorder GC Allocated In Frame", "bytes/frame");
                report.performanceComplete = m.cpuFrame.available && m.gpu.available && (m.gcAllocated.available || m.mainThreadManagedAllocated.available);
                Require(m.lastTick > m.firstTick && game.Page == PlayerPage.Battle, "The measured battle must remain active and advance normally.");
            }
            Screenshot("battle"); foreach (var frame in Settle()) yield return frame;
            foreach (var frame in LeaveBattle()) yield return frame;
        }

        IEnumerable LeaveBattle()
        {
            Click("Battle pause"); Click("Leave battle"); Click("Confirm action");
            Require(game.Page == PlayerPage.Map && game.Run.InWorkshop, "Confirmed exit did not return to the map.");
            Click("Back"); Require(game.Page == PlayerPage.Home, "Map did not return home.");
            foreach (var frame in Settle()) yield return frame;
        }

        IEnumerable RepeatedLifecycle()
        {
            var snapshots = new List<Lifecycle>();
            string starter = game.Run.AssemblyCatalog.Starters.First(entry => entry.Kind == AlbumEntryKind.Tower).Id;
            for (int cycle = 0; cycle < 7; cycle++)
            {
                Click("Open album"); Click("Album filter All"); Click("Album choose " + starter); Click("Album edit");
                Click("Composite library"); Click("Composite library back"); Click("Composite step 2");
                foreach (var frame in Trial()) yield return frame;
                Click("Composite return"); Click("Album close");
                Click("Open campaign"); Click("Prepare mission"); Click("Start battle"); Click("Launch wave");
                foreach (var frame in Settle()) yield return frame;
                foreach (var frame in LeaveBattle()) yield return frame;
                if (cycle >= 2) snapshots.Add(SnapshotLifecycle(cycle - 2));
            }
            report.lifecycle = snapshots.ToArray();
            var first = snapshots[0];
            report.lifecycleStable = snapshots.All(item => item.eventObservationAvailable && item.sceneGameObjects <= first.sceneGameObjects && item.sceneComponents <= first.sceneComponents &&
                item.workshopChanged == first.workshopChanged && item.sessionChanged == first.sessionChanged && item.simulationEvents == first.simulationEvents && item.selectionChanged == first.selectionChanged);
            Require(report.lifecycleStable, "Settled objects/components or observed C# subscriptions grew across five cycles after two warmups.");
        }

        ProfileSnapshot SnapshotProfile() => new ProfileSnapshot {
            gold = game.Run.Workshop.Gold, totalXp = game.Run.Workshop.TotalXp, saveState = game.Run.Workshop.SaveState.ToString(),
            ownedParts = game.Run.AssemblyCatalog.Definitions.Select(part => part.Id).Distinct().Where(game.Run.Workshop.IsUnlocked).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            slots = game.Run.Workshop.LoadoutEntries.Select(entry => entry?.Id ?? "").ToArray(),
            entries = game.Run.Workshop.Entries.OrderBy(entry => entry.Id, StringComparer.Ordinal).Select(entry => JsonUtility.ToJson(new AlbumEntryData(entry))).ToArray(),
            library = game.Run.Workshop.Library.OrderBy(entry => entry.Id, StringComparer.Ordinal).Select(entry => JsonUtility.ToJson(new ComponentLibraryEntryData(entry))).ToArray()
        };

        Lifecycle SnapshotLifecycle(int cycle)
        {
            var components = Resources.FindObjectsOfTypeAll<Component>().Where(component => component != null && component.gameObject.scene.IsValid()).ToArray();
            int workshop = Subscribers(game.Run, "WorkshopChanged"), session = Subscribers(game.Run, "SessionChanged"), events = Subscribers(game.Run, "SimulationEventReceived"), selection = Subscribers(game.Drawing, "SelectionChanged");
            return new Lifecycle { cycle = cycle, sceneGameObjects = components.Count(component => component is Transform), sceneComponents = components.Length,
                workshopChanged = workshop, sessionChanged = session, simulationEvents = events, selectionChanged = selection,
                eventObservationAvailable = workshop >= 0 && session >= 0 && events >= 0 && selection >= 0,
                managedUsedBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(), totalAllocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() };
        }
        static int Subscribers(object owner, string name)
        {
            var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? -1 : (field.GetValue(owner) as Delegate)?.GetInvocationList().Length ?? 0;
        }
        static Metric Summarize(List<double> values, string source, string unit)
        {
            values.Sort(); int count = values.Count;
            return new Metric { source = source, unit = unit, available = count > 0, samples = count,
                mean = count == 0 ? 0 : values.Average(), p50 = count == 0 ? 0 : values[(count - 1) / 2],
                p95 = count == 0 ? 0 : values[(int)Math.Ceiling(count * .95) - 1], maximum = count == 0 ? 0 : values[count - 1] };
        }
        IEnumerable Settle() { for (int i = 0; i < SettleFrames; i++) yield return null; }
        void Screenshot(string stage)
        {
            ReleaseVisualCapture.Capture(Path.GetFileName(output)+"."+stage,Screen.width,Screen.height,Path.GetDirectoryName(output));
        }
        static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
        bool TryFindButton(string name, out Button button)
        {
            button = game.GetComponentsInChildren<Button>(false).FirstOrDefault(item => item.name == name && item.isActiveAndEnabled);
            return button != null;
        }
        Button FindButton(string name)
        {
            Require(TryFindButton(name, out var button), "Missing active production button: " + name + " (page " + game.Page + ")"); return button;
        }
        void Click(string name)
        {
            var button = FindButton(name);
            Require(button.IsInteractable(), "Production button is disabled: " + name);
            Require(EventSystem.current != null, "Production EventSystem is missing.");
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            actions.Add(name);
        }
    }
}
