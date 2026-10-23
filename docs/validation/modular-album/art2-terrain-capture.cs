if (!UnityEngine.Application.isPlaying) throw new System.Exception("Play required.");
var fixtureType=System.AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("PaperDefense.Tests.AssemblyCommandFixture")).First(t=>t!=null);
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
var paths=new System.Collections.Generic.List<string>();
foreach(var size in new[]{new UnityEngine.Vector2Int(1280,720),new UnityEngine.Vector2Int(1024,768)}) {
 var fixture=(System.IDisposable)System.Activator.CreateInstance(fixtureType,flags,null,new object[]{94,390,null},null);
 var owner=new UnityEngine.GameObject("Refinement terrain capture"); owner.SetActive(false);
 var target=new UnityEngine.RenderTexture(size.x,size.y,24);
 try {
  var balance=(PaperDefense.Application.GameBalanceAsset)fixtureType.GetField("BalanceAsset",flags).GetValue(fixture);
  var store=(PaperDefense.Application.IWorkshopProfileStore)fixtureType.GetField("Store",flags).GetValue(fixture);
  var game=owner.AddComponent<PaperDefense.Presentation.PlayerGameRoot>(); game.Configure(balance,true);
  owner.SetActive(true); game.Run.AutomaticSimulation=false; game.Run.InitializeWorkshop(store); game.enabled=false;
  var shell=owner.GetComponentInChildren<PaperDefense.Presentation.PlayerShell>(); shell.enabled=false;
  var bootstrap=owner.GetComponentInChildren<PaperDefense.Presentation.PaperDefenseBootstrap>(); bootstrap.enabled=false;
  foreach(var responsive in owner.GetComponentsInChildren<PaperDefense.Presentation.WorkshopResponsiveLayout>(true))responsive.enabled=false;
  var overlayObject=new UnityEngine.GameObject("Refinement canvas viewport",typeof(UnityEngine.Camera));overlayObject.transform.SetParent(owner.transform);
  var overlay=overlayObject.GetComponent<UnityEngine.Camera>();overlay.enabled=false;overlay.targetTexture=target;
  var layout=new PaperDefense.Presentation.PlayerLayout(size.x,size.y);
  foreach(var canvas in owner.GetComponentsInChildren<UnityEngine.Canvas>(true)){
   var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();if(scaler!=null)scaler.enabled=false;
   canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceCamera;canvas.worldCamera=overlay;canvas.planeDistance=10;canvas.scaleFactor=layout.Scale;
  }
  shell.ApplyLayout(size.x,size.y);if(!game.StartBattle())throw new System.Exception(game.Status);shell.ApplyLayout(size.x,size.y);
  game.Drawing.enabled=false;var world=bootstrap.worldCamera;world.targetTexture=target;world.rect=shell.Layout.NormalizedField;
  world.aspect=size.x*world.rect.width/(size.y*world.rect.height);
  var session=game.Run.Session;var home=new UnityEngine.Vector2(session.BaseCell.X+.5f,session.BaseCell.Y+.5f);
  float full=UnityEngine.Mathf.Max(session.Balance.Height*.5f+1,session.Balance.Width*.5f/world.aspect+1);
  foreach(var zoom in new[]{1,3,6,7}){
   world.orthographicSize=full/(zoom==7?6:zoom);var center=zoom==1?new UnityEngine.Vector2(session.Balance.Width*.5f,session.Balance.Height*.5f):home;
   if(zoom==7)center+=new UnityEngine.Vector2(6,3);
   world.transform.position=new UnityEngine.Vector3(center.x,center.y,-10);
   typeof(PaperDefense.Presentation.NotebookWorldView).GetMethod("LateUpdate",flags).Invoke(game.World,null);
   UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);UnityEngine.Canvas.ForceUpdateCanvases();
   paths.Add(PaperDefense.Editor.PlayerVisualCapture.Capture("art2-terrain-"+(zoom==7?"pan6":"zoom"+zoom)+"-"+size.x+"x"+size.y,size.x,size.y,"docs/validation/modular-album/art2-screenshots"));
  }
 }finally{UnityEngine.Object.DestroyImmediate(owner);target.Release();UnityEngine.Object.DestroyImmediate(target);fixture.Dispose();}
}
return string.Join("\n",paths);
