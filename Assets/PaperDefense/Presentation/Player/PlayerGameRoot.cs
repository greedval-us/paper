using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperDefense.Presentation
{
    public enum PlayerPage { Home, Map, Preparation, Workshop, Battle, Pause, Upgrades, Result, Settings, Help, Album }

    /// <summary>Player navigation owns presentation only. All gameplay and profile commands use RunController.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerGameRoot : MonoBehaviour
    {
        [SerializeField] GameBalanceAsset balance;
        [SerializeField] TextResourcesAsset textResources;
        [SerializeField] NotebookViewSettingsAsset viewSettings;
        [SerializeField] bool memoryOnlyProfile;
        [SerializeField] bool notebookStyle = true;
        public bool NotebookStyle => notebookStyle;
        PaperDefenseBootstrap bootstrap;
        WorkshopHud workshop;
        PlayerShell shell;
        PlayerPage settingsReturn = PlayerPage.Home;
        PlayerPage helpReturn = PlayerPage.Home;
        PlayerPage albumReturn = PlayerPage.Home, workshopReturn = PlayerPage.Preparation;
        public PlayerDisplaySettings DisplaySettings { get; private set; }
        NotebookVisualThemeAsset reducedTheme, originalTheme;
        public bool ReducedMotion { get; private set; }
        public void SetReducedMotion(bool value)
        {
            if (value == ReducedMotion) return;
            if (value && reducedTheme == null)
            {
                originalTheme = World.VisualTheme;
                reducedTheme = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
                reducedTheme.name = "Player reduced motion (runtime)";
                var source = originalTheme != null ? originalTheme : NotebookVisualThemeAsset.Default;
                var data = JsonUtility.FromJson<NotebookArtSource>(JsonUtility.ToJson(source.Data));
                foreach (var clip in data.Clips)
                    foreach (var frame in clip.Frames) { frame.Offset = Vector2.zero; frame.Rotation = 0; frame.Scale = 1; }
                foreach (var recoil in data.Recoils) recoil.Distance = 0;
                reducedTheme.ReplaceData(data);
            }
            ReducedMotion = value; World.VisualTheme = value ? reducedTheme : originalTheme;
            foreach (var scroll in GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true)) scroll.inertia = !value;
        }
        void OnDestroy() { if (reducedTheme != null) Destroy(reducedTheme); }
        public PlayerPage Page { get; private set; }
        public RunController Run => bootstrap.Controller;
        public DrawingInput Drawing => bootstrap.Drawing;
        public NotebookWorldView World => bootstrap.View;
        public string Status { get; private set; } = "";
        public bool CanBuild => Page == PlayerPage.Battle && !Run.Paused && !Run.Session.PendingUpgrade && !Finished;
        public bool Finished => Run.Session.Phase == RunPhase.Victory || Run.Session.Phase == RunPhase.Defeat;

        public void Configure(GameBalanceAsset configuration, bool memoryOnly, bool useNotebookStyle = true)
        { balance = configuration; memoryOnlyProfile = memoryOnly; notebookStyle = useNotebookStyle; }

        void Awake()
        {
            DisplaySettings = PlayerDisplaySettings.FromScreen();
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
            bootstrap.PlayerPaperColor = PlayerUi.Paper;
            bootstrap.showBattleHud = false; bootstrap.showWorkshopHud = false;
            bootstrap.enableDrawingInput = false;
            runtime.SetActive(true);
            Drawing.HandleSessionHotkeys = false;
            workshop = new GameObject("Player workshop").AddComponent<WorkshopHud>();
            workshop.transform.SetParent(transform, false); workshop.TextResolver = bootstrap.TextResolver;
            workshop.PresentationVisible = false; workshop.StartRequested = () => Navigate(PlayerPage.Preparation);
            workshop.UseGuidedFlow=true;
            workshop.Initialize(Run);
            if (workshop.Composite != null) workshop.Composite.BackRequested = ReturnFromWorkshop;
            workshop.SetPlayerLayout(); workshop.PresentationVisible = false;
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
                    if (shell.HasConfirmation) { shell.CancelConfirmation(); return; }
                    if (Page == PlayerPage.Battle) Pause();
                    else if (Page == PlayerPage.Pause) Resume();
                    else if (Page == PlayerPage.Settings) CloseSettings();
                    else if (Page == PlayerPage.Help) CloseHelp();
                }
                if (!shell.HasConfirmation && keyboard.spaceKey.wasPressedThisFrame)
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
            bootstrap.ViewportOverride = shell.Layout.NormalizedField;
        }

        public void Navigate(PlayerPage page)
        {
            if (page == PlayerPage.Workshop && Page != PlayerPage.Workshop) workshopReturn = Page;
            Page = page;
            workshop.PresentationVisible = page == PlayerPage.Workshop;
            Drawing.enabled = CanBuild;
            Drawing.Cancel();
            shell.Show(page);
        }

        public void OpenAlbum()
        {
            if (!Run.InWorkshop) return;
            if (Page != PlayerPage.Album) albumReturn = Page == PlayerPage.Workshop ? workshopReturn : Page;
            Navigate(PlayerPage.Album);
        }
        public void ShowAlbum() => OpenAlbum();
        public void CloseAlbum() => Navigate(albumReturn);
        public void EditAlbumEntry(AlbumEntry entry)
        {
            if (!Run.InWorkshop) return;
            Navigate(PlayerPage.Workshop);
            if (entry == null) workshop.Composite?.BeginNewEntry();
            else workshop.Composite?.OpenEntry(entry);
        }
        public void ReturnFromWorkshop()
        {
            if (workshopReturn == PlayerPage.Album)
                shell.SelectAlbumOnReturn(workshop.Composite?.DraftSession.Draft?.Id);
            Navigate(workshopReturn);
        }
        public void AssignAlbumEntry(int slot, string id)
        {
            if (Page != PlayerPage.Preparation || !Run.InWorkshop) return;
            var result = Run.AssemblyCommands.AssignSlot(slot, id, System.Guid.NewGuid());
            Status = result.Success ? "" : GameText.Resolve(UserText.Message(result.Code));
            shell.Show(PlayerPage.Preparation);
            var replacement = shell.GetComponentsInChildren<UnityEngine.UI.Button>();
            foreach (var button in replacement)
                if (button.name == "Replace album entry " + slot)
                { UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(button.gameObject); break; }
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
        {
            if (Page == PlayerPage.Battle) Pause();
            if (help) { helpReturn = Page; Navigate(PlayerPage.Help); }
            else { settingsReturn = Page; DisplaySettings.RefreshCurrentSize(Screen.width, Screen.height); DisplaySettings.CancelSelection(); Navigate(PlayerPage.Settings); }
        }
        public void CloseSettings() { DisplaySettings.CancelSelection(); Navigate(settingsReturn); }
        public void CloseHelp() => Navigate(helpReturn);

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

        public void AssignBlueprint(int slot, string id)
        {
            if (Page != PlayerPage.Preparation || !Run.InWorkshop) return;
            Status = Run.AssignBlueprint(slot, id, out var reason) ? "" : GameText.Resolve(reason);
            shell.Show(PlayerPage.Preparation);
        }
    }
}
