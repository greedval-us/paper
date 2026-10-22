if (!UnityEngine.Application.isPlaying) throw new System.Exception("Isolated Play scene required.");
var fixtureType=System.AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("PaperDefense.Tests.AssemblyCommandFixture")).First(t=>t!=null);
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
var paths=new System.Collections.Generic.List<string>();
foreach(var size in new[]{new UnityEngine.Vector2Int(1024,768),new UnityEngine.Vector2Int(1280,720),new UnityEngine.Vector2Int(1920,1080),new UnityEngine.Vector2Int(1280,960),new UnityEngine.Vector2Int(1600,900)}){
 var fixture=(System.IDisposable)System.Activator.CreateInstance(fixtureType,flags,null,new object[]{94,390,null},null);
 var owner=new UnityEngine.GameObject("Refinement first pages");owner.SetActive(false);var target=new UnityEngine.RenderTexture(size.x,size.y,24);
 try{
  var balance=(PaperDefense.Application.GameBalanceAsset)fixtureType.GetField("BalanceAsset",flags).GetValue(fixture);
  var store=(PaperDefense.Application.IWorkshopProfileStore)fixtureType.GetField("Store",flags).GetValue(fixture);
  var game=owner.AddComponent<PaperDefense.Presentation.PlayerGameRoot>();game.Configure(balance,true);
  owner.SetActive(true);game.Run.AutomaticSimulation=false;game.Run.InitializeWorkshop(store);game.enabled=false;
  var shell=owner.GetComponentInChildren<PaperDefense.Presentation.PlayerShell>();shell.enabled=false;
  owner.GetComponentInChildren<PaperDefense.Presentation.PaperDefenseBootstrap>().enabled=false;
  foreach(var responsive in owner.GetComponentsInChildren<PaperDefense.Presentation.WorkshopResponsiveLayout>(true))responsive.enabled=false;
  var cameraObject=new UnityEngine.GameObject("Refinement page viewport",typeof(UnityEngine.Camera));cameraObject.transform.SetParent(owner.transform);
  var camera=cameraObject.GetComponent<UnityEngine.Camera>();camera.enabled=false;camera.targetTexture=target;
  var layout=new PaperDefense.Presentation.PlayerLayout(size.x,size.y);
  foreach(var canvas in owner.GetComponentsInChildren<UnityEngine.Canvas>(true)){
   var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();if(scaler!=null)scaler.enabled=false;
   canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;canvas.scaleFactor=layout.Scale;
  }
  foreach(var destination in new[]{PaperDefense.Presentation.PlayerPage.Home,PaperDefense.Presentation.PlayerPage.Map,PaperDefense.Presentation.PlayerPage.Preparation}){
   game.Navigate(destination);shell.ApplyLayout(size.x,size.y);
   UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
   foreach(var hint in owner.GetComponentsInChildren<PaperDefense.Presentation.PlayerTooltip>(true))hint.OnPointerExit(null);
   UnityEngine.Canvas.ForceUpdateCanvases();
   string name=destination==PaperDefense.Presentation.PlayerPage.Home?"01-home":destination==PaperDefense.Presentation.PlayerPage.Map?"02-map":"03-preparation";
   paths.Add(PaperDefense.Editor.PlayerVisualCapture.Capture("refinement-"+name+"-"+size.x+"x"+size.y,size.x,size.y,"docs/validation/modular-album/refinement-screenshots"));
  }
 }finally{UnityEngine.Object.DestroyImmediate(owner);target.Release();UnityEngine.Object.DestroyImmediate(target);fixture.Dispose();}
}
return string.Join("\n",paths);
