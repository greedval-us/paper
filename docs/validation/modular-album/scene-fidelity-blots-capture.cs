if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Capture requires the isolated empty PlayMode scene.");
var fixtureType = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PaperDefense.Tests.AssemblyCommandFixture")).First(t => t != null);
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var originalRoots = new System.Collections.Generic.HashSet<UnityEngine.GameObject>(scene.GetRootGameObjects());
var paths = new System.Collections.Generic.List<string>();
var proofs = new System.Collections.Generic.List<string>();
string onlySize = "1280x720";
foreach (var size in new[] { new UnityEngine.Vector2Int(1024, 768), new UnityEngine.Vector2Int(1280, 720), new UnityEngine.Vector2Int(1920, 1080), new UnityEngine.Vector2Int(1280, 960), new UnityEngine.Vector2Int(1600, 900) })
{
    if (onlySize.Length > 0 && onlySize != size.x + "x" + size.y) continue;
    // These are previously owned rights in a temporary profile. The long barrel remains locked,
    // so its actual confirmation still demonstrates exactly 94 - 80 = 14.
    System.Action<PaperDefense.Core.WorkshopProfileData> configure = data => data.Unlocked = data.Unlocked.Concat(new[] { "core.fan", "head.fan", "armor.plate", "magazine.drum", "trigger.burst" }).Distinct().ToArray();
    var fixture = (System.IDisposable)System.Activator.CreateInstance(fixtureType, flags, null, new object[] { 94, 390, configure }, null);
    var owner = new UnityEngine.GameObject("Task13 isolated workshop " + size.x + "x" + size.y);
    var target = new UnityEngine.RenderTexture(size.x, size.y, 24);
    try
    {
        target.Create();
        var run = (PaperDefense.Application.RunController)fixtureType.GetField("Run", flags).GetValue(fixture);
        run.AutomaticSimulation = false;
        var catalog = (PaperDefense.Core.ComponentCatalog)fixtureType.GetField("Catalog", flags).GetValue(fixture);
        var store = fixtureType.GetField("Store", flags).GetValue(fixture);
        var cameraObject = new UnityEngine.GameObject("Task13 workshop viewport", typeof(UnityEngine.Camera));
        cameraObject.transform.SetParent(owner.transform, false);
        var camera = cameraObject.GetComponent<UnityEngine.Camera>(); camera.enabled = false; camera.targetTexture = target;
        var canvasObject = new UnityEngine.GameObject("Task13 actual workshop canvas", typeof(UnityEngine.RectTransform), typeof(UnityEngine.Canvas));
        canvasObject.transform.SetParent(owner.transform, false);
        var canvas = canvasObject.GetComponent<UnityEngine.Canvas>();
        var layout = new PaperDefense.Presentation.PlayerLayout(size.x, size.y);
        canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; canvas.scaleFactor = layout.Scale;
        var eventObject = new UnityEngine.GameObject("Task13 workshop events", typeof(UnityEngine.EventSystems.EventSystem)); eventObject.transform.SetParent(owner.transform, false);
        var panel = new PaperDefense.Presentation.CompositeWorkshopPanel(canvas.transform, run, PaperDefense.Application.TextResourcesAsset.Default, null);
        panel.Reflow(layout.Width, layout.Height);
        System.Action<string> click = name => {
            var button = canvasObject.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == name);
            if (!button.IsInteractable()) throw new System.InvalidOperationException("Unreachable workshop capture control: " + name);
            button.onClick.Invoke();
        };
        System.Action<string> capture = name => {
            // A screenshot documents the requested screen state rather than incidental keyboard
            // selection/hover help left behind by the preceding automated click.
            foreach (var tooltip in canvasObject.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true).Where(component => component.GetType().Name == "WorkshopTooltipOverlay"))
                tooltip.GetType().GetMethod("Hide", flags).Invoke(tooltip, null);
            var trialFields = panel.Root.GetComponentsInChildren<PaperDefense.Presentation.TrialLaneSketch>().Where(field => field.Playback.Report != null).ToArray();
            float commonAt = 0;
            foreach (var field in trialFields)
            {
                var report = field.Playback.Report;
                if (report == null) continue;
                var actions = report.Frames.SelectMany(frame => frame.Events).Where(item => item.Kind == PaperDefense.Core.SimulationEventKind.Shot || item.Kind == PaperDefense.Core.SimulationEventKind.ProjectileLaunched)
                    .OrderBy(item => item.SimulationSeconds).ThenBy(item => item.Sequence).ToArray();
                if (actions.Length == 0) actions = report.Frames.SelectMany(frame => frame.Events).Where(item => item.Kind == PaperDefense.Core.SimulationEventKind.WeaponActionStarted)
                    .OrderBy(item => item.SimulationSeconds).ThenBy(item => item.Sequence).ToArray();
                float at = actions.Length == 0 ? report.DurationSeconds * .5f : (float)actions[0].SimulationSeconds + .01f;
                commonAt = UnityEngine.Mathf.Max(commonAt, at);
            }
            foreach (var field in trialFields)
            {
                var report = field.Playback.Report;
                field.SetResult(report, UnityEngine.Time.unscaledTime - commonAt / field.PlaybackSpeed, report.DurationSeconds); field.PlaybackPaused = true;
                typeof(PaperDefense.Presentation.TrialLaneSketch).GetMethod("Update", flags).Invoke(field, null);
            }
            UnityEngine.Canvas.ForceUpdateCanvases();
            paths.Add(PaperDefense.Editor.PlayerVisualCapture.Capture("fidelity-" + name + "-" + size.x + "x" + size.y, size.x, size.y, "docs/validation/modular-album/fidelity-screenshots"));
        };
        var gun = catalog.Starters.First(entry => entry.Kind == PaperDefense.Core.AlbumEntryKind.Tower && entry.Snapshot.Recipe.Tower.Mounts.Count == 1 && entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.gun");
        panel.OpenEntry(gun); click("Composite trial"); click("Composite trial scenario Blots"); click("Composite trial run"); capture("10-trial-blots-comparison");
        for(int i=0; i<20 && panel.Root.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.name=="Composite trial comparison measured result"); i++) click("Composite trial compare");
        click("Composite trial run"); capture("10-trial-blots-single");
        var lineProofs=new System.Collections.Generic.List<string>();
        foreach(var text in panel.Root.GetComponentsInChildren<UnityEngine.UI.Text>().Where(t=>t.name=="Composite trial measured result")) {
         lineProofs.Add(text.name+" length="+text.text.Length+" visible="+text.cachedTextGenerator.characterCountVisible+" rows="+text.text.Split('\n').Length+" renderedLines="+text.cachedTextGenerator.lineCount+" height="+text.rectTransform.rect.height);
        }
        System.IO.File.WriteAllLines("D:/Projekt Unity/paper/docs/validation/modular-album/fidelity-trial-blots-proof.txt",lineProofs);
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(owner); target.Release(); UnityEngine.Object.DestroyImmediate(target); fixture.Dispose();
        foreach (var root in scene.GetRootGameObjects()) if (!originalRoots.Contains(root)) UnityEngine.Object.DestroyImmediate(root);
    }
}
return string.Join("\n", paths);
