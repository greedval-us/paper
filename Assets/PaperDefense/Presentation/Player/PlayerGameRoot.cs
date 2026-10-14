using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperDefense.Presentation
{
    public enum PlayerPage { Home, Map, Preparation, Workshop, Battle, Pause, Upgrades, Result, Settings, Help }

    /// <summary>Player navigation owns presentation only. All gameplay and profile commands use RunController.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerGameRoot : MonoBehaviour
    {
        [SerializeField] GameBalanceAsset balance;
        [SerializeField] TextResourcesAsset textResources;
        [SerializeField] NotebookViewSettingsAsset viewSettings;
        [SerializeField] bool memoryOnlyProfile;
        [SerializeField] bool notebookStyle;
        public bool NotebookStyle => notebookStyle;
        PaperDefenseBootstrap bootstrap;
        WorkshopHud workshop;
        PlayerShell shell;
        PlayerPage settingsReturn = PlayerPage.Home;
        public PlayerPage Page { get; private set; }
        public RunController Run => bootstrap.Controller;
        public DrawingInput Drawing => bootstrap.Drawing;
        public NotebookWorldView World => bootstrap.View;
        public string Status { get; private set; } = "";
        public bool CanBuild => Page == PlayerPage.Battle && !Run.Paused && !Run.Session.PendingUpgrade && !Finished;
        public bool Finished => Run.Session.Phase == RunPhase.Victory || Run.Session.Phase == RunPhase.Defeat;

        public void Configure(GameBalanceAsset configuration, bool memoryOnly, bool useNotebookStyle = false)
        { balance = configuration; memoryOnlyProfile = memoryOnly; notebookStyle = useNotebookStyle; }

        void Awake()
        {
            var runtime = new GameObject("Shared game mechanics");
            runtime.SetActive(false); runtime.transform.SetParent(transform, false);
            bootstrap = runtime.AddComponent<PaperDefenseBootstrap>();
            bootstrap.balance = balance; bootstrap.textResources = textResources; bootstrap.viewSettings = viewSettings;
            bootstrap.worldCamera = GetComponentInChildren<Camera>(true);
            if (bootstrap.worldCamera == null)
            {
                var cameraObject = new GameObject("Player camera", typeof(Camera));
                cameraObject.transform.SetParent(runtime.transform, false); cameraObject.tag = "MainCamera";
                bootstrap.worldCamera = cameraObject.GetComponent<Camera>();
            }
            bootstrap.memoryOnlyProfile = memoryOnlyProfile;
            bootstrap.showBattleHud = false; bootstrap.showWorkshopHud = false;
            bootstrap.enableDrawingInput = false;
            runtime.SetActive(true);
            Drawing.HandleSessionHotkeys = false;
            workshop = new GameObject("Player workshop").AddComponent<WorkshopHud>();
            workshop.transform.SetParent(transform, false); workshop.TextResolver = bootstrap.TextResolver;
            workshop.PresentationVisible = false; workshop.StartRequested = () => Navigate(PlayerPage.Preparation);
            workshop.Initialize(Run); workshop.SetPlayerLayout(); workshop.PresentationVisible = false;
            shell = new GameObject("Player interface").AddComponent<PlayerShell>();
            shell.transform.SetParent(transform, false); shell.Initialize(this);
            Navigate(PlayerPage.Home);
        }

        void Update()
        {
            if (shell == null) return;
            // React to existing controller transitions, including workshop commands.
            bool inBattle = Page == PlayerPage.Battle || Page == PlayerPage.Pause || Page == PlayerPage.Upgrades;
            if (inBattle && Run.InWorkshop) Navigate(PlayerPage.Preparation);
            else if (inBattle && Finished) Navigate(PlayerPage.Result);
            else if (Page == PlayerPage.Battle && Run.Session.PendingUpgrade) Navigate(PlayerPage.Upgrades);
            else if (Page == PlayerPage.Battle && Run.Paused) Navigate(PlayerPage.Pause);

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (Page == PlayerPage.Battle) Pause();
                    else if (Page == PlayerPage.Pause) Resume();
                    else if (Page == PlayerPage.Settings || Page == PlayerPage.Help) Navigate(settingsReturn);
                }
                if (keyboard.spaceKey.wasPressedThisFrame)
                { if (Page == PlayerPage.Battle) Pause(); else if (Page == PlayerPage.Pause) Resume(); }
                if (Page == PlayerPage.Battle)
                {
                    if (keyboard.nKey.wasPressedThisFrame) Run.NextWave();
                    if (keyboard.fKey.wasPressedThisFrame) Run.ToggleSpeed();
                }
            }
            Drawing.enabled = CanBuild;
            FitViewport();
        }

        void FitViewport()
        {
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 1000f);
            float x = (Screen.width - 1600 * scale) * .5f, y = (Screen.height - 1000 * scale) * .5f;
            bootstrap.ViewportOverride = new Rect((x + 125 * scale) / Screen.width, (y + 205 * scale) / Screen.height,
                1150 * scale / Screen.width, 675 * scale / Screen.height);
        }

        public void Navigate(PlayerPage page)
        {
            Page = page;
            workshop.PresentationVisible = page == PlayerPage.Workshop;
            Drawing.enabled = CanBuild;
            Drawing.Cancel();
            shell.Show(page);
        }

        public bool StartBattle()
        {
            if (!Run.InWorkshop) return false;
            if (!Run.StartRun(out var reason)) { Status = GameText.Resolve(reason); shell.SetStatus(Status); return false; }
            Status = ""; Drawing.SelectSlot(0); Navigate(PlayerPage.Battle); return true;
        }

        public void Pause()
        {
            if (Page != PlayerPage.Battle || Finished) return;
            if (!Run.Paused) Run.TogglePause();
            Navigate(PlayerPage.Pause);
        }

        public void Resume()
        {
            if (Page != PlayerPage.Pause && Page != PlayerPage.Upgrades) return;
            if (Run.Paused) Run.TogglePause();
            Navigate(Run.Session.PendingUpgrade ? PlayerPage.Upgrades : PlayerPage.Battle);
        }

        public void ReturnToMap()
        {
            Run.OpenWorkshop(); Status = ""; Navigate(PlayerPage.Map);
        }

        public void Retry()
        { Run.OpenWorkshop(); StartBattle(); }

        public void OpenSettings(bool help = false)
        { settingsReturn = Page; Navigate(help ? PlayerPage.Help : PlayerPage.Settings); }
        public void CloseSettings() => Navigate(settingsReturn);

        public void ChooseUpgrade(RunUpgrade upgrade)
        { Run.ChooseUpgrade(upgrade); Navigate(PlayerPage.Battle); }

        public void CycleBlueprint(int slot)
        {
            var album = Run.Workshop.Album;
            if (album.Count == 0) return;
            string current = Run.Workshop.Loadout[slot]?.Id;
            int next = 0;
            for (int i = 0; i < album.Count; i++) if (album[i].Id == current) { next = (i + 1) % album.Count; break; }
            if (!Run.AssignBlueprint(slot, album[next].Id, out var reason)) Status = GameText.Resolve(reason);
            else Status = "";
            shell.Show(PlayerPage.Preparation);
        }
    }
}
