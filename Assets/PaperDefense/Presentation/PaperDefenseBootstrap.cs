using PaperDefense.Application;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace PaperDefense.Presentation
{
    public sealed class PaperDefenseBootstrap : MonoBehaviour
    {
        public GameBalanceAsset balance;
        public Camera worldCamera;
        public RunController Controller { get; private set; }
        public DrawingInput Drawing { get; private set; }
        public NotebookWorldView View { get; private set; }

        void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null)
            { var go = new GameObject("Notebook Camera"); worldCamera = go.AddComponent<Camera>(); go.tag = "MainCamera"; }
            Controller = Child("Application — Run").AddComponent<RunController>(); Controller.balanceAsset = balance;
            Controller.Initialize();
            var background=Child("Paper background camera").AddComponent<Camera>();background.depth=-10;background.cullingMask=0;
            background.clearFlags=CameraClearFlags.SolidColor;background.backgroundColor=new Color(.975f,.961f,.9f);
            ConfigureCamera();
            View = Child("Presentation — Notebook world").AddComponent<NotebookWorldView>();
            View.WorldCamera = worldCamera; View.Initialize(Controller.Session);
            Drawing = Child("Presentation — Drawing input").AddComponent<DrawingInput>(); Drawing.Initialize(Controller,View,worldCamera);
            Child("Presentation — Interface").AddComponent<NotebookHud>().Initialize(Controller,Drawing);
            if (EventSystem.current == null)
            { var go=Child("UI Event System");go.AddComponent<EventSystem>();go.AddComponent<InputSystemUIInputModule>(); }
            Controller.SessionChanged += OnSessionChanged;
            Controller.SimulationEventReceived += View.AddEvent;
            UnityEngine.Application.targetFrameRate = 60;
        }
        void OnDestroy()
        {
            if (Controller != null) {Controller.SessionChanged -= OnSessionChanged; if(View != null) Controller.SimulationEventReceived -= View.AddEvent;}
        }
        void OnSessionChanged(PaperDefense.Core.IRunReadModel session) {View.Bind(session); ConfigureCamera();}
        void LateUpdate() {if(Controller!=null) FitCamera();}
        void ConfigureCamera()
        {
            worldCamera.transform.SetPositionAndRotation(new Vector3(Controller.Session.Balance.Width*.5f,Controller.Session.Balance.Height*.5f,-10),Quaternion.identity);
            worldCamera.orthographic=true;worldCamera.clearFlags=CameraClearFlags.SolidColor;
            worldCamera.backgroundColor=new Color(.975f,.961f,.9f);worldCamera.nearClipPlane=.1f;worldCamera.farClipPlane=100;
            worldCamera.depth=0;FitCamera();
        }
        void FitCamera()
        {
            float scale=Mathf.Min(Screen.width/1600f,Screen.height/1000f);
            float x=(Screen.width-1600*scale)*.5f,y=(Screen.height-1000*scale)*.5f;
            worldCamera.rect=new Rect((x+32*scale)/Screen.width,(y+205*scale)/Screen.height,1536*scale/Screen.width,640*scale/Screen.height);
            var b=Controller.Session.Balance;worldCamera.orthographicSize=Mathf.Max(b.Height*.5f+1,b.Width*.5f/worldCamera.aspect+1)/(Drawing!=null ? Drawing.Zoom : 1);
            if(Drawing!=null) worldCamera.transform.position=new Vector3(Drawing.CameraCenter.x,Drawing.CameraCenter.y,-10);
        }
        GameObject Child(string name) {var go=new GameObject(name);go.transform.SetParent(transform,false);return go;}
    }
}
