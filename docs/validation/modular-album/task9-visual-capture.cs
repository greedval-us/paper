var fixtureType = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PaperDefense.Tests.AssemblyCommandFixture")).First(t => t != null);
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
var originals = UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>();
var enabled = originals.Select(c => c.enabled).ToArray();
var paths = new System.Collections.Generic.List<string>();
try
{
    foreach (var c in originals) c.enabled = false;
    foreach (var size in new[] { new UnityEngine.Vector2Int(1024,768), new UnityEngine.Vector2Int(1920,1080) })
    {
        var fixture = (System.IDisposable)System.Activator.CreateInstance(fixtureType, flags, null, new object[] { 94,390,null }, null);
        var canvasObject = new UnityEngine.GameObject("Task 9 isolated visual evidence", typeof(UnityEngine.RectTransform), typeof(UnityEngine.Canvas));
        var cameraObject = new UnityEngine.GameObject("Task 9 evidence viewport", typeof(UnityEngine.Camera));
        var target = new UnityEngine.RenderTexture(size.x,size.y,24);
        try
        {
            var run = (PaperDefense.Application.RunController)fixtureType.GetField("Run", flags).GetValue(fixture);
            var canvas = canvasObject.GetComponent<UnityEngine.Canvas>();
            var camera = cameraObject.GetComponent<UnityEngine.Camera>(); camera.enabled = false; camera.targetTexture = target;
            canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            var layout = new PaperDefense.Presentation.PlayerLayout(size.x,size.y);
            canvas.scaleFactor = layout.Scale;
            UnityEngine.Canvas.ForceUpdateCanvases();
            var panel = new PaperDefense.Presentation.CompositeWorkshopPanel(canvasObject.transform, run, PaperDefense.Application.TextResourcesAsset.Default, () => {});
            panel.Reflow(layout.Width,layout.Height);
            System.Action<string> click = name => canvasObject.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == name).onClick.Invoke();
            System.Action<string> capture = name => {
                UnityEngine.Canvas.ForceUpdateCanvases();
                paths.Add(PaperDefense.Editor.PlayerVisualCapture.Capture("task9-"+name+"-"+size.x+"x"+size.y,size.x,size.y,"docs/validation/modular-album/screenshots"));
            };
            capture("foundation");
            click("Composite next"); capture("weapons");
            click("Composite next"); capture("review");
            panel.DraftSession.OpenFoundation(); panel.DraftSession.SaveComponent(System.Guid.NewGuid()); panel.Refresh();
            click("Composite library"); capture("library"); click("Composite library back");
            panel.DraftSession.OpenWeapon(0); panel.DraftSession.SelectCategory(PaperDefense.Core.ComponentCategory.Head); panel.Refresh();
            click("Component show unavailable"); click("Component inspect head.long"); capture("component");
            click("Component purchase part"); capture("purchase"); click("Component purchase cancel");
            click("Component category Magazine"); capture("magazine");
            click("Composite cancel component"); click("Composite route structure");
            click("Composite choose family Generator"); click("Composite next"); click("Composite next"); capture("generator");
            click("Composite back"); click("Composite edit structure"); capture("structure-parts");
        }
        finally { UnityEngine.Object.DestroyImmediate(canvasObject); UnityEngine.Object.DestroyImmediate(cameraObject); target.Release(); UnityEngine.Object.DestroyImmediate(target); fixture.Dispose(); }
    }
}
finally { for (int i = 0; i < originals.Length; i++) if (originals[i] != null) originals[i].enabled = enabled[i]; }
return string.Join("\n", paths);
