using System;
using System.Collections.Generic;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace PaperDefense.Presentation
{
    public sealed class PaperDefenseBootstrap : MonoBehaviour
    {
        public GameBalanceAsset balance;
        public Camera worldCamera;
        public bool memoryOnlyProfile;
        public bool standMode;
        public bool showBattleHud = true;
        public bool showWorkshopHud = true;
        public TextResourcesAsset textResources;
        [NonSerialized] public IWorkshopProfileStore ProfileStore;
        [NonSerialized] public Action<RunController> PrepareController;
        public UserTextResolver TextResolver { get; private set; }
        public RunController Controller { get; private set; }
        public DrawingInput Drawing { get; private set; }
        public NotebookWorldView View { get; private set; }
        internal Func<string[]> ArgumentProvider=Environment.GetCommandLineArgs;
        internal IWorkshopProfileStore ProfileStoreOverride;

        internal static bool ShouldUseMemoryOnlyProfile(bool configured,IReadOnlyList<string> arguments)
        {
            if(configured) return true;
            if(arguments!=null)
                for(int i=0;i<arguments.Count;i++)
                    if(arguments[i]=="-paper-memory-only" || arguments[i]=="-paper-pressure") return true;
            return false;
        }

        void Awake()
        {
            // Validate text before opening any profile, including an isolated fixture store.
            TextResolver = textResources != null ? textResources.CreateResolver() : TextResourcesAsset.Default;
            UserTextContracts.Validate(TextResolver);
            TextResolver.RequireCompatible(TextResourcesAsset.Default);
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null)
            { var go = new GameObject("Notebook Camera"); worldCamera = go.AddComponent<Camera>(); go.tag = "MainCamera"; }
            Controller = Child("Application — Run").AddComponent<RunController>(); Controller.balanceAsset = balance;
            bool isolateProfile=ShouldUseMemoryOnlyProfile(memoryOnlyProfile,ArgumentProvider());
            var selectedStore = ProfileStore ?? ProfileStoreOverride;
            if(!isolateProfile && selectedStore!=null) Controller.InitializeWorkshop(selectedStore);
            else Controller.InitializeWorkshop(!isolateProfile);
            PrepareController?.Invoke(Controller);
            var background=Child("Paper background camera").AddComponent<Camera>();background.depth=-10;background.cullingMask=0;
            background.clearFlags=CameraClearFlags.SolidColor;background.backgroundColor=new Color(.975f,.961f,.9f);
            ConfigureCamera();
            View = Child("Presentation — Notebook world").AddComponent<NotebookWorldView>();
            View.WorldCamera = worldCamera; View.Initialize(Controller.Session);
            Drawing = Child("Presentation — Drawing input").AddComponent<DrawingInput>();
            Drawing.TextResolver = TextResolver;
            Drawing.Initialize(Controller,View,worldCamera);
            if (showBattleHud)
            {
                var hud = Child("Presentation — Interface").AddComponent<NotebookHud>();
                hud.TextResolver = TextResolver; hud.Initialize(Controller,Drawing);
            }
            if (showWorkshopHud)
            {
                var workshop = Child("Presentation — Workshop").AddComponent<WorkshopHud>();
                workshop.TextResolver = TextResolver; workshop.ReserveStandControls = standMode; workshop.Initialize(Controller);
            }
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
        void OnSessionChanged(IRunReadModel session) {View.Bind(session); ConfigureCamera();}
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
            worldCamera.rect = standMode && !showBattleHud && !showWorkshopHud
                ? new Rect(.22f,.04f,.76f,.92f)
                : new Rect((x+32*scale)/Screen.width,(y+205*scale)/Screen.height,1536*scale/Screen.width,640*scale/Screen.height);
            var b=Controller.Session.Balance;worldCamera.orthographicSize=Mathf.Max(b.Height*.5f+1,b.Width*.5f/worldCamera.aspect+1)/(Drawing!=null ? Drawing.Zoom : 1);
            if(Drawing!=null) worldCamera.transform.position=new Vector3(Drawing.CameraCenter.x,Drawing.CameraCenter.y,-10);
        }
        GameObject Child(string name) {var go=new GameObject(name);go.transform.SetParent(transform,false);return go;}
    }
}
