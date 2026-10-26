// Execute only after Art3 native art + runtime import has completed. Never open the user's Game scene here.
if (!UnityEngine.Application.isPlaying || !string.IsNullOrEmpty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path))
    throw new System.InvalidOperationException("Art3 capture requires the isolated, unsaved empty PlayMode scene.");
if (UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>().Length != 0 || UnityEngine.Object.FindAnyObjectByType<PaperDefense.Presentation.PlayerGameRoot>() != null)
    throw new System.InvalidOperationException("Art3 capture refuses an existing UI/game. Start from an empty temporary scene.");
if (PaperDefense.Presentation.AssemblyVisualComposer.Revision != "modular-full-catalog-2026-10-24-v8")
    throw new System.InvalidOperationException("Art3 requires imported catalog revision modular-full-catalog-2026-10-24-v8.");
var fixtureType=System.AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("PaperDefense.Tests.AssemblyCommandFixture")).First(t=>t!=null);
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
var paths=new System.Collections.Generic.List<string>();
foreach(var size in new[]{new UnityEngine.Vector2Int(1024,768),new UnityEngine.Vector2Int(1280,720),new UnityEngine.Vector2Int(1920,1080),new UnityEngine.Vector2Int(1280,960),new UnityEngine.Vector2Int(1600,900)}.Where(size => string.IsNullOrEmpty(UnityEditor.SessionState.GetString("PaperDefense.Art3.CaptureSize", "")) || UnityEditor.SessionState.GetString("PaperDefense.Art3.CaptureSize", "") == size.x + "x" + size.y)) {
 var fixture=(System.IDisposable)System.Activator.CreateInstance(fixtureType,flags,null,new object[]{94,390,null},null);
 // The fixture uses its own GUID directory under OS temp; bootstrap starts in memory-only mode.
 string isolatedProfile = System.IO.Path.GetFullPath((string)fixtureType.GetField("ProfilePath", flags).GetValue(fixture));
 string temporaryRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
 if (!isolatedProfile.StartsWith(temporaryRoot, System.StringComparison.OrdinalIgnoreCase)) { fixture.Dispose(); throw new System.InvalidOperationException("Art3 fixture must use an isolated temporary profile."); }
 ((UnityEngine.GameObject)fixtureType.GetField("Root", flags).GetValue(fixture)).name = "Art3 isolated profile fixture";
 var owner=new UnityEngine.GameObject("Art3 album pages");owner.SetActive(false);var target=new UnityEngine.RenderTexture(size.x,size.y,24);
 try{
  var balance=(PaperDefense.Application.GameBalanceAsset)fixtureType.GetField("BalanceAsset",flags).GetValue(fixture);
  var store=(PaperDefense.Application.IWorkshopProfileStore)fixtureType.GetField("Store",flags).GetValue(fixture);
  var game=owner.AddComponent<PaperDefense.Presentation.PlayerGameRoot>();game.Configure(balance,true);
  owner.SetActive(true);game.Run.AutomaticSimulation=false;game.Run.InitializeWorkshop(store);game.enabled=false;
  var shell=owner.GetComponentInChildren<PaperDefense.Presentation.PlayerShell>();shell.enabled=false;
  owner.GetComponentInChildren<PaperDefense.Presentation.PaperDefenseBootstrap>().enabled=false;
  foreach(var responsive in owner.GetComponentsInChildren<PaperDefense.Presentation.WorkshopResponsiveLayout>(true))responsive.enabled=false;
  var cameraObject=new UnityEngine.GameObject("Art3 page viewport",typeof(UnityEngine.Camera));cameraObject.transform.SetParent(owner.transform);
  var camera=cameraObject.GetComponent<UnityEngine.Camera>();camera.enabled=false;camera.targetTexture=target;
  var layout=new PaperDefense.Presentation.PlayerLayout(size.x,size.y);
  foreach(var canvas in owner.GetComponentsInChildren<UnityEngine.Canvas>(true)){
   var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();if(scaler!=null)scaler.enabled=false;
   canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;canvas.scaleFactor=layout.Scale;
  }
  game.Navigate(PaperDefense.Presentation.PlayerPage.Preparation);game.OpenAlbum();
  owner.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name.StartsWith("Album choose ")).onClick.Invoke();
  string before=(string)fixtureType.GetMethod("State",flags).Invoke(fixture,null);
  int writes=(int)store.GetType().GetField("SaveCalls",flags).GetValue(store);
  foreach(string name in new[]{"09-album","24-empty"}){
   if(name=="24-empty")shell.Album.SetFilter(PaperDefense.Presentation.AlbumFilter.Mine);
   shell.ApplyLayout(size.x,size.y);
   UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
   foreach(var hint in owner.GetComponentsInChildren<PaperDefense.Presentation.PlayerTooltip>(true))hint.OnPointerExit(null);
   UnityEngine.Canvas.ForceUpdateCanvases();
   paths.Add(PaperDefense.Editor.PlayerVisualCapture.Capture("art3-"+name+"-"+size.x+"x"+size.y,size.x,size.y,"docs/validation/modular-album/art3-screenshots"));
  }
  if(before!=(string)fixtureType.GetMethod("State",flags).Invoke(fixture,null)||writes!=(int)store.GetType().GetField("SaveCalls",flags).GetValue(store))throw new System.Exception("Browsing album wrote a profile");
  game.CloseAlbum(); if(game.Page!=PaperDefense.Presentation.PlayerPage.Preparation)throw new System.Exception("Album return lost preparation");
 }finally{UnityEngine.Object.DestroyImmediate(owner);target.Release();UnityEngine.Object.DestroyImmediate(target);fixture.Dispose();}
}
return string.Join("\n",paths);
