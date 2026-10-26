using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public bool showBattleHud = true;
        public bool showWorkshopHud = true;
        public WorkshopHudMode workshopHudMode;
        public bool enableDrawingInput = true;
        [NonSerialized] public Color? PlayerPaperColor;
        public TextResourcesAsset textResources;
        public NotebookViewSettingsAsset viewSettings;
        [NonSerialized] public IWorkshopProfileStore ProfileStore;
        public UserTextResolver TextResolver { get; private set; }
        public RunController Controller { get; private set; }
        public DrawingInput Drawing { get; private set; }
        public NotebookWorldView View { get; private set; }
        public Rect? ViewportOverride { get; set; }
        internal Func<string[]> ArgumentProvider=Environment.GetCommandLineArgs;
        internal IWorkshopProfileStore ProfileStoreOverride;

        internal static bool ShouldUseMemoryOnlyProfile(bool configured,IReadOnlyList<string> arguments)
        {
            if(configured) return true;
            if(arguments!=null)
                for(int i=0;i<arguments.Count;i++)
                    if(arguments[i]=="-paper-memory-only") return true;
            return false;
        }

        internal static string ResolveTemporaryProfilePath(bool configuredMemory, IReadOnlyList<string> arguments, string temporaryRoot)
        {
            var args = arguments == null ? Array.Empty<string>() : arguments.ToArray();
            if (args.Contains("-paper-release-probe"))
            {
                string error = AlbumReleaseProbe.ValidateArguments(args);
                if (error != null) throw new ArgumentException(error);
            }
            if (ShouldUseMemoryOnlyProfile(configuredMemory,args)) return null;
            var indices = Enumerable.Range(0,args.Length).Where(i => args[i] == "-paper-profile-path").ToArray();
            if (indices.Length == 0) return null;
            if (indices.Length != 1 || indices[0]+1 >= args.Length || args[indices[0]+1].StartsWith("-",StringComparison.Ordinal))
                throw new ArgumentException("An explicit temporary profile needs one unambiguous path.");
            string candidate = args[indices[0]+1];
            if (!Path.IsPathRooted(candidate)) throw new ArgumentException("The temporary profile path must be absolute.");
            string path = Path.GetFullPath(candidate);
            string prefix = Path.GetFullPath(temporaryRoot).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("An explicit acceptance profile must be inside the temporary directory.");
            return path;
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
            var arguments = ArgumentProvider();
            string temporaryProfilePath = ResolveTemporaryProfilePath(memoryOnlyProfile,arguments,Path.GetTempPath());
            Controller = Child("Application — Run").AddComponent<RunController>(); Controller.balanceAsset = balance;
            bool isolateProfile=ShouldUseMemoryOnlyProfile(memoryOnlyProfile,arguments);
            var selectedStore = ProfileStore ?? ProfileStoreOverride;
            if (temporaryProfilePath != null) selectedStore = new WorkshopProfileStore(temporaryProfilePath,
                catalog: Controller.AssemblyCatalog, legacyBalance: balance.CreateWorkshopBalance());
            if(!isolateProfile && selectedStore!=null) Controller.InitializeWorkshop(selectedStore);
            else Controller.InitializeWorkshop(!isolateProfile);
            var background=Child("Paper background camera").AddComponent<Camera>();background.depth=-10;background.cullingMask=0;
            background.clearFlags=CameraClearFlags.SolidColor;background.backgroundColor=PlayerPaperColor ?? new Color(.975f,.961f,.9f);
            ConfigureCamera();
            View = Child("Presentation — Notebook world").AddComponent<NotebookWorldView>();
            View.WorldCamera = worldCamera; View.ViewSettings = viewSettings; View.PaperColor = PlayerPaperColor; View.Initialize(Controller.Session);
            Drawing = Child("Presentation — Drawing input").AddComponent<DrawingInput>();
            Drawing.TextResolver = TextResolver;
            Drawing.ViewSettings = viewSettings;
            Drawing.Initialize(Controller,View,worldCamera);
            Drawing.enabled = enableDrawingInput;
            if (showBattleHud)
            {
                var hud = Child("Presentation — Interface").AddComponent<NotebookHud>();
                hud.TextResolver = TextResolver; hud.Initialize(Controller,Drawing);
            }
            if (showWorkshopHud)
            {
                var workshop = Child("Presentation — Workshop").AddComponent<WorkshopHud>();
                workshop.TextResolver = TextResolver;
                workshop.Mode = workshopHudMode; workshop.Initialize(Controller);
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
        void Update() {if(Controller!=null && View!=null) View.Paused=Controller.Paused;}
        void LateUpdate() {if(Controller!=null) FitCamera();}
        void ConfigureCamera()
        {
            worldCamera.transform.SetPositionAndRotation(new Vector3(Controller.Session.Balance.Width*.5f,Controller.Session.Balance.Height*.5f,-10),Quaternion.identity);
            worldCamera.orthographic=true;worldCamera.clearFlags=CameraClearFlags.SolidColor;
            worldCamera.backgroundColor=PlayerPaperColor ?? new Color(.975f,.961f,.9f);worldCamera.nearClipPlane=.1f;worldCamera.farClipPlane=100;
            worldCamera.depth=0;FitCamera();
        }
        void FitCamera()
        {
            float scale=Mathf.Min(Screen.width/1600f,Screen.height/1000f);
            float x=(Screen.width-1600*scale)*.5f,y=(Screen.height-1000*scale)*.5f;
            if (ViewportOverride.HasValue) worldCamera.rect = ViewportOverride.Value;
            else worldCamera.rect = new Rect((x+32*scale)/Screen.width,(y+205*scale)/Screen.height,1536*scale/Screen.width,640*scale/Screen.height);
            var b=Controller.Session.Balance;worldCamera.orthographicSize=Mathf.Max(b.Height*.5f+1,b.Width*.5f/worldCamera.aspect+1)/(Drawing!=null ? Drawing.Zoom : 1);
            if(Drawing!=null) worldCamera.transform.position=new Vector3(Drawing.CameraCenter.x,Drawing.CameraCenter.y,-10);
        }
        GameObject Child(string name) {var go=new GameObject(name);go.transform.SetParent(transform,false);return go;}
    }
}
