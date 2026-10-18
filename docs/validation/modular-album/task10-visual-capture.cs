if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Capture only in the isolated empty PlayMode scene.");
var fixtureType = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PaperDefense.Tests.AssemblyCommandFixture")).First(t => t != null);
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
var paths = new System.Collections.Generic.List<string>();
foreach (var size in new[] { new UnityEngine.Vector2Int(1024,768), new UnityEngine.Vector2Int(1280,720), new UnityEngine.Vector2Int(1920,1080) })
{
    var fixture = (System.IDisposable)System.Activator.CreateInstance(fixtureType,flags,null,new object[] {94,390,null},null);
    var root = new UnityEngine.GameObject("Task10 isolated visual fixture"); root.SetActive(false);
    var target = new UnityEngine.RenderTexture(size.x,size.y,24);
    try
    {
        var balance = (PaperDefense.Application.GameBalanceAsset)fixtureType.GetField("BalanceAsset",flags).GetValue(fixture);
        var store = (PaperDefense.Application.IWorkshopProfileStore)fixtureType.GetField("Store",flags).GetValue(fixture);
        var game = root.AddComponent<PaperDefense.Presentation.PlayerGameRoot>(); game.Configure(balance,true);
        root.SetActive(true); game.Run.AutomaticSimulation = false; game.Run.InitializeWorkshop(store); game.enabled = false;
        var shell = root.GetComponentInChildren<PaperDefense.Presentation.PlayerShell>(); shell.enabled = false;
        foreach (var responsive in root.GetComponentsInChildren<PaperDefense.Presentation.WorkshopResponsiveLayout>(true)) responsive.enabled = false;
        var cameraObject = new UnityEngine.GameObject("Task10 canvas viewport",typeof(UnityEngine.Camera)); cameraObject.transform.SetParent(root.transform);
        var camera = cameraObject.GetComponent<UnityEngine.Camera>(); camera.enabled = false; camera.targetTexture = target;
        var layout = new PaperDefense.Presentation.PlayerLayout(size.x,size.y);
        foreach (var canvas in root.GetComponentsInChildren<UnityEngine.Canvas>(true))
        {
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); if (scaler != null) scaler.enabled = false;
            canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            canvas.scaleFactor = layout.Scale;
        }
        UnityEngine.Canvas.ForceUpdateCanvases(); shell.ApplyLayout(size.x,size.y);
        System.Action<string> capture = name => {
            UnityEngine.Canvas.ForceUpdateCanvases();
            paths.Add(PaperDefense.Editor.PlayerVisualCapture.Capture("task10-"+name+"-"+size.x+"x"+size.y,size.x,size.y,"docs/validation/modular-album/screenshots"));
        };
        game.Navigate(PaperDefense.Presentation.PlayerPage.Home); capture("home");
        game.Navigate(PaperDefense.Presentation.PlayerPage.Map); capture("map");
        game.Navigate(PaperDefense.Presentation.PlayerPage.Preparation); capture("preparation");
        game.OpenAlbum(); capture("album"); shell.Album.SetFilter(PaperDefense.Presentation.AlbumFilter.Mine); capture("empty-mine");
        shell.Album.SetFilter(PaperDefense.Presentation.AlbumFilter.Towers); shell.Album.SelectEntry("album.guard"); capture("album-selected");
    }
    finally { UnityEngine.Object.DestroyImmediate(root); target.Release(); UnityEngine.Object.DestroyImmediate(target); fixture.Dispose(); }
}
return string.Join("\n",paths);
