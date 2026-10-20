if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Capture requires the isolated empty PlayMode scene.");
var fixtureType = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PaperDefense.Tests.AssemblyCommandFixture")).First(t => t != null);
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var originalRoots = new System.Collections.Generic.HashSet<UnityEngine.GameObject>(scene.GetRootGameObjects());
var paths = new System.Collections.Generic.List<string>();
var proofs = new System.Collections.Generic.List<string>();
string onlySize = UnityEditor.SessionState.GetString("PaperDefense.Task13.CaptureSize", "");
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
        var doubleGun = catalog.Starters.First(entry => entry.Kind == PaperDefense.Core.AlbumEntryKind.Tower && entry.Snapshot.Recipe.Tower.Mounts.Count == 2);
        capture("04-foundation"); click("Composite next"); capture("05-weapons");
        click("Composite edit weapon"); click("Component show unavailable"); capture("06-drawing");
        click("Component category Head"); click("Component inspect head.long"); capture("11-stencils");
        click("Component purchase part"); capture("25-purchase"); click("Component purchase confirm");
        if (run.Workshop.Gold != 14 || !run.Workshop.IsUnlocked("head.long")) throw new System.InvalidOperationException("Purchase capture did not preserve 94 - 80 = 14.");
        capture("07-details");
        click("Component category Magazine");
        foreach (string magazine in new[] { "magazine.drum", "magazine.belt" })
        {
            click("Component inspect " + magazine);
            var cycle = panel.Root.GetComponentsInChildren<UnityEngine.UI.Text>().Single(text => text.name == "Component magazine cycle");
            if (!cycle.text.Contains("→") || cycle.rectTransform.rect.height < 60) throw new System.InvalidOperationException("Magazine inspection lost its visible current-to-candidate firing cycle.");
            capture("07-" + magazine.Replace(".", "-"));
        }
        click("Composite cancel component"); panel.OpenEntry(doubleGun); capture("08-review");
        string trialBefore = (string)fixtureType.GetMethod("State", flags).Invoke(fixture, null);
        int trialWrites = (int)store.GetType().GetField("SaveCalls", flags).GetValue(store);
        click("Composite trial"); click("Composite trial run");
        if (panel.LastAssemblyTrial == null || panel.LastAssemblyTrialComparison == null) throw new System.InvalidOperationException("The actual two-page comparison was rejected.");
        capture("10-trial"); click("Composite trial overview"); capture("10-trial-overview"); click("Composite close trial");
        if (trialBefore != (string)fixtureType.GetMethod("State", flags).Invoke(fixture, null) || trialWrites != (int)store.GetType().GetField("SaveCalls", flags).GetValue(store)) throw new System.InvalidOperationException("The visual trial wrote a profile.");
        var mortar = catalog.Starters.First(entry => entry.Kind == PaperDefense.Core.AlbumEntryKind.Tower && entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.mortar");
        panel.OpenEntry(mortar); panel.DraftSession.OpenWeapon(0); panel.DraftSession.SelectCategory(PaperDefense.Core.ComponentCategory.Head); panel.Refresh(); click("Component show unavailable"); capture("12-mortar");
        var old = gun.Snapshot.Recipe.Tower.Mounts[0].Weapon;
        var fanWeapon = new PaperDefense.Core.WeaponRecipe(new PaperDefense.Core.PartRef("core.fan", 1), new PaperDefense.Core.PartRef("head.fan", 1), old.Sight, old.Magazine, old.Trigger);
        var fan = new PaperDefense.Core.AlbumEntry("task13.fan", "Готовый веер", 1, new PaperDefense.Core.RecipeSnapshot(new PaperDefense.Core.AssemblyRecipe(new PaperDefense.Core.TowerRecipe(gun.Snapshot.Recipe.Tower.Foundation, new[] { new PaperDefense.Core.WeaponMountRecipe(0, fanWeapon) })), catalog.BalanceRevision, catalog.Definitions));
        panel.OpenEntry(fan); panel.DraftSession.OpenWeapon(0); panel.Refresh(); capture("13-fan");
        panel.OpenEntry(gun); panel.DraftSession.OpenFoundation(); panel.DraftSession.SelectCategory(PaperDefense.Core.ComponentCategory.Armor); panel.Refresh(); click("Component show unavailable"); click("Component inspect armor.plate"); capture("14-armor");
        panel.OpenEntry(gun); panel.DraftSession.OpenFoundation(); panel.DraftSession.SelectCategory(PaperDefense.Core.ComponentCategory.Hull); panel.Refresh(); click("Component show unavailable"); capture("30-foundation-parts");
        foreach (var entry in catalog.Starters.Where(page => page.Kind == PaperDefense.Core.AlbumEntryKind.Structure))
        {
            panel.OpenEntry(entry); panel.DraftSession.OpenStructure(); panel.DraftSession.SelectCategory(PaperDefense.Core.ComponentCategory.Utility); panel.Refresh(); click("Component show unavailable");
            capture(entry.Snapshot.Recipe.Structure.Family == PaperDefense.Core.AssemblyStructureFamily.Generator ? "29-structure" : "29-" + entry.Snapshot.Recipe.Structure.Family.ToString().ToLowerInvariant());
        }
        panel.OpenEntry(new PaperDefense.Core.AlbumEntry("task13.io", "Сохранить двойной дозор", 1, doubleGun.Snapshot));
        store.GetType().GetField("RefuseWrites", flags).SetValue(store, true); click("Composite save");
        if (!panel.Status.Contains("Запись")) throw new System.InvalidOperationException("IO error did not retain its actionable diagnostic.");
        capture("26-save-error");
        store.GetType().GetField("RefuseWrites", flags).SetValue(store, false); click("Composite save");
        if (run.Workshop.Entries.Count(entry => entry.Id == "task13.io") != 1) throw new System.InvalidOperationException("Save retry did not produce exactly one page.");
        capture("26-save-retry");
        panel.OpenEntry(gun); panel.DraftSession.OpenWeapon(0); panel.DraftSession.SelectCategory(PaperDefense.Core.ComponentCategory.Magazine);
        if (!panel.DraftSession.SelectPart("magazine.drum") || !panel.DraftSession.SelectPart("trigger.burst")) throw new System.InvalidOperationException("Invalid draft fixture could not select both owned parts.");
        panel.Refresh();
        if (panel.DraftSession.CanSaveComponent || !panel.DraftSession.Diagnostics.Any(issue => issue.Code == "assembly.part.required")) throw new System.InvalidOperationException("Queue/drum conflict was not rejected before save.");
        capture("27-invalid");
        panel.OpenEntry(gun); panel.DraftSession.OpenFoundation(); panel.DraftSession.SaveComponent(System.Guid.NewGuid()); panel.Refresh(); click("Composite library"); capture("30-library");
        proofs.Add(size.x + "x" + size.y + ": all workshop states captured; purchase 94-80=14; independent comparison has no IO; failed save retains draft; retry has one page; invalid queue/drum blocked.");
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(owner); target.Release(); UnityEngine.Object.DestroyImmediate(target); fixture.Dispose();
        foreach (var root in scene.GetRootGameObjects()) if (!originalRoots.Contains(root)) UnityEngine.Object.DestroyImmediate(root);
    }
}
string proofPath = "D:/Projekt Unity/paper/docs/validation/modular-album/scene-fidelity-workshop-capture-proof.txt";
if (onlySize.Length > 0 && System.IO.File.Exists(proofPath)) proofs.InsertRange(0, System.IO.File.ReadAllLines(proofPath).Where(line => !line.StartsWith(onlySize + ":")));
System.IO.File.WriteAllLines(proofPath, proofs);
return string.Join("\n", paths);
