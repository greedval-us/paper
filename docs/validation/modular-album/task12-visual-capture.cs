if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Capture requires the isolated empty PlayMode scene.");
var fixtureType = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PaperDefense.Tests.AssemblyCommandFixture")).First(t => t != null);
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
var rootScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var existingRoots = new System.Collections.Generic.HashSet<UnityEngine.GameObject>(rootScene.GetRootGameObjects());
var paths = new System.Collections.Generic.List<string>();
var measurements = new System.Collections.Generic.List<string> { "EntryId,Scenario,Budget,Seed,UnitCost,Copies,Cost,Damage,Leaks,UnresolvedEnemies,DurationSeconds" };
var geometry = new System.Collections.Generic.List<string>();
foreach (var size in new[] { new UnityEngine.Vector2Int(1024, 768), new UnityEngine.Vector2Int(1280, 720), new UnityEngine.Vector2Int(1920, 1080) })
{
    var fixture = (System.IDisposable)System.Activator.CreateInstance(fixtureType, flags, null, new object[] { 0, 0, null }, null);
    var owner = new UnityEngine.GameObject("Task12 isolated capture " + size.x + "x" + size.y);
    var target = new UnityEngine.RenderTexture(size.x, size.y, 24);
    try
    {
        target.Create();
        var run = (PaperDefense.Application.RunController)fixtureType.GetField("Run", flags).GetValue(fixture);
        run.AutomaticSimulation = false;
        var catalog = (PaperDefense.Core.ComponentCatalog)fixtureType.GetField("Catalog", flags).GetValue(fixture);
        var store = fixtureType.GetField("Store", flags).GetValue(fixture);
        int writes = (int)store.GetType().GetField("SaveCalls", flags).GetValue(store);
        string before = (string)fixtureType.GetMethod("State", flags).Invoke(fixture, null);
        byte[] savedBytes = (byte[])fixtureType.GetProperty("SavedBytes", flags).GetValue(fixture);
        var cameraObject = new UnityEngine.GameObject("Task12 canvas viewport", typeof(UnityEngine.Camera));
        cameraObject.transform.SetParent(owner.transform, false);
        var camera = cameraObject.GetComponent<UnityEngine.Camera>();
        camera.enabled = false; camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
        camera.targetTexture = target;
        var canvasObject = new UnityEngine.GameObject("Task12 actual workshop canvas", typeof(UnityEngine.RectTransform), typeof(UnityEngine.Canvas));
        canvasObject.transform.SetParent(owner.transform, false);
        var canvas = canvasObject.GetComponent<UnityEngine.Canvas>();
        canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
        canvas.planeDistance = 10; canvas.scaleFactor = 1;
        var eventObject = new UnityEngine.GameObject("Task12 isolated events", typeof(UnityEngine.EventSystems.EventSystem));
        eventObject.transform.SetParent(owner.transform, false);
        var panel = new PaperDefense.Presentation.CompositeWorkshopPanel(canvas.transform, run, null, null);
        panel.Reflow(size.x, size.y);
        System.Action<string> click = name =>
        {
            var button = panel.Root.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == name);
            if (!button.IsInteractable()) throw new System.InvalidOperationException("Unreachable capture control: " + name);
            button.onClick.Invoke();
        };
        System.Func<PaperDefense.Presentation.TrialLaneSketch> lane = () => panel.Root.GetComponentInChildren<PaperDefense.Presentation.TrialLaneSketch>();
        System.Action<string> capture = state =>
        {
            var field = lane();
            if (panel.LastAssemblyTrial != null)
            {
                var report = panel.LastTrial;
                float at = .75f;
                if (state == "generator") at = report.DurationSeconds;
                else if (state == "repair") at = report.DurationSeconds * .5f;
                else
                {
                    var action = report.Frames.SelectMany(f => f.Events).Where(e => e.Kind == PaperDefense.Core.SimulationEventKind.WeaponActionStarted).ToArray();
                    if (action.Length > 0) at = (float)action[0].SimulationSeconds + .01f;
                }
                at = UnityEngine.Mathf.Min(at, report.DurationSeconds);
                field.SetResult(report, UnityEngine.Time.unscaledTime - at / field.PlaybackSpeed, report.DurationSeconds);
                field.PlaybackPaused = true;
                typeof(PaperDefense.Presentation.TrialLaneSketch).GetMethod("Update", flags).Invoke(field, null);
            }
            UnityEngine.Canvas.ForceUpdateCanvases();
            if (panel.LastAssemblyTrial != null)
            {
                int vertices = 0;
                foreach (var graphic in field.GetComponentsInChildren<UnityEngine.UI.Graphic>())
                {
                    if (!graphic.isActiveAndEnabled) continue;
                    using (var helper = new UnityEngine.UI.VertexHelper())
                    {
                        graphic.GetType().GetMethod("OnPopulateMesh", flags, null, new[] { typeof(UnityEngine.UI.VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                        vertices += helper.currentVertCount;
                    }
                }
                if (vertices <= 80) throw new System.InvalidOperationException("Recorded field contains only background grid: " + state);
                if (panel.LastAssemblyTrial.Resolved.Support != null && !field.Playback.Report.Frames[field.Playback.FrameIndex].Towers.Any(t => t.ResolvedEntry?.Support?.Kind == panel.LastAssemblyTrial.Resolved.Support.Kind))
                    throw new System.InvalidOperationException("Selected support is absent from the actual shared recording: " + state);
                geometry.Add(size.x + "x" + size.y + " " + state + ": " + vertices + " vertices");
            }
            paths.Add(PaperDefense.Editor.PlayerVisualCapture.Capture("task12-" + state + "-" + size.x + "x" + size.y, size.x, size.y, "docs/validation/modular-album/screenshots"));
        };
        var gun = catalog.Starters.First(entry => entry.Kind == PaperDefense.Core.AlbumEntryKind.Tower && entry.Snapshot.Recipe.Tower.Mounts.Count == 1 && entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.gun");
        panel.OpenEntry(gun); click("Composite trial");
        if (panel.LastTrial != null || lane().HasRecording) throw new System.InvalidOperationException("Unstarted field contains fabricated results.");
        capture("unstarted");
        foreach (var scenario in new[] { PaperDefense.Core.AssemblyTrialScenario.Single, PaperDefense.Core.AssemblyTrialScenario.Line, PaperDefense.Core.AssemblyTrialScenario.Crowd, PaperDefense.Core.AssemblyTrialScenario.Armored, PaperDefense.Core.AssemblyTrialScenario.Blots })
        {
            click("Composite trial scenario " + scenario); click("Composite trial run");
            if (panel.LastAssemblyTrial == null) throw new System.InvalidOperationException("Capture gun trial was rejected: " + scenario);
            capture(scenario.ToString().ToLowerInvariant());
            if (size.x == 1024 && scenario != PaperDefense.Core.AssemblyTrialScenario.Blots)
            {
                var r = panel.LastAssemblyTrial;
                measurements.Add(string.Join(",", new[] { r.EntryId, r.Scenario.ToString(), r.Budget.ToString(), r.Seed.ToString(), r.UnitCost.ToString(), r.Copies.ToString(), r.Cost.ToString(),
                    r.Damage?.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture) ?? "", r.Leaks?.ToString() ?? "", r.UnresolvedEnemies?.ToString() ?? "", r.DurationSeconds.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture) }));
            }
        }
        click("Composite close trial");
        foreach (var family in new[] { PaperDefense.Core.AssemblyStructureFamily.Repair, PaperDefense.Core.AssemblyStructureFamily.Generator, PaperDefense.Core.AssemblyStructureFamily.Wall, PaperDefense.Core.AssemblyStructureFamily.Slow })
        {
            var entry = catalog.Starters.Single(page => page.Kind == PaperDefense.Core.AlbumEntryKind.Structure && page.Snapshot.Recipe.Structure.Family == family);
            panel.OpenEntry(entry); click("Composite trial"); click("Composite trial run");
            if (panel.LastAssemblyTrial == null) throw new System.InvalidOperationException("Capture support trial was rejected: " + family);
            capture(family.ToString().ToLowerInvariant()); click("Composite close trial");
        }
        if (run.Workshop.Gold != 0 || run.Workshop.TotalXp != 0 || (int)store.GetType().GetField("SaveCalls", flags).GetValue(store) != writes ||
            (string)fixtureType.GetMethod("State", flags).Invoke(fixture, null) != before ||
            !savedBytes.SequenceEqual((byte[])fixtureType.GetProperty("SavedBytes", flags).GetValue(fixture)))
            throw new System.InvalidOperationException("A visual trial changed the isolated profile.");
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(owner);
        target.Release(); UnityEngine.Object.DestroyImmediate(target); fixture.Dispose();
        // WorkshopUi safely retires submitted controls as detached inactive roots until end of frame.
        // The synchronous capture owns every root introduced in this explicitly isolated scene.
        foreach (var retired in rootScene.GetRootGameObjects())
            if (!existingRoots.Contains(retired)) UnityEngine.Object.DestroyImmediate(retired);
    }
}
if (paths.Count != 30) throw new System.InvalidOperationException("Expected the complete 10 × 3 capture matrix.");
string reportPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../docs/validation/modular-album/task12-measurements.csv"));
System.IO.File.WriteAllLines(reportPath, measurements);
return string.Join("\n", paths) + "\nMEASUREMENTS: " + reportPath + "\n" + string.Join("\n", geometry);
