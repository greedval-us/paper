using PaperDefense.Application;
using PaperDefense.Core;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperDefense.Presentation
{
    public enum PlayerPage { Home, Map, Preparation, Workshop, Battle, Pause, Upgrades, Result, Settings, Help, Album, Campaign }

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
        public PlayerAudioSettings AudioSettings { get; private set; }
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
                data.ReducedMotion = true;
                foreach (var clip in data.Clips) clip.HoldFirstPose();
                foreach (var recoil in data.Recoils) recoil.Distance = 0;
                reducedTheme.ReplaceData(data);
            }
            ReducedMotion = value; World.VisualTheme = value ? reducedTheme : originalTheme;
            if (value) UiMenuMotion.FinishIn(transform);
            foreach (var scroll in GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true)) scroll.inertia = !value;
        }
        void OnDestroy() { if (reducedTheme != null) Destroy(reducedTheme); }
        public PlayerPage Page { get; private set; }
        public RunController Run => bootstrap.Controller;
        public DrawingInput Drawing => bootstrap.Drawing;
        public NotebookWorldView World => bootstrap.View;
        public string Status { get; private set; } = "";
        public string MissionContinuationStatus { get; private set; } = "";
        public bool CanBuild => Page == PlayerPage.Battle && !Run.CommandsSuspended && !Run.Session.PendingUpgrade && !Finished &&
            !(shell?.HasConfirmation ?? false) && !Drawing.IsGameplayInputBlocked;
        public bool Finished => Run.Session.Phase == RunPhase.Victory || Run.Session.Phase == RunPhase.Defeat;

        public void Configure(GameBalanceAsset configuration, bool memoryOnly, bool useNotebookStyle = true)
        { balance = configuration; memoryOnlyProfile = memoryOnly; notebookStyle = useNotebookStyle; }

        void Awake()
        {
            DisplaySettings = PlayerDisplaySettings.FromScreen();
            AudioSettings = PlayerAudioSettings.Load(PaperDefenseBootstrap.ShouldUseMemoryOnlyProfile(
                memoryOnlyProfile, System.Environment.GetCommandLineArgs()));
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
            bootstrap.PlayerPaperColor = NightNotebookArt.Paper;
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
            var audioObject = new GameObject("Battle audio");
            audioObject.transform.SetParent(transform, false);
            audioObject.AddComponent<PlayerBattleAudio>().Initialize(this);
        }

        void Update()
        {
            if (shell == null) return;
            // React to existing controller transitions, including workshop commands.
            bool inBattle = Page == PlayerPage.Battle || Page == PlayerPage.Pause || Page == PlayerPage.Upgrades;
            if (inBattle && Run.InWorkshop) Navigate(PlayerPage.Preparation);
            else if (inBattle && Finished) Navigate(PlayerPage.Result);
            else if (Page == PlayerPage.Battle && Run.Session.PendingUpgrade) Navigate(PlayerPage.Upgrades);

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (AlbumSearchInput.ConsumeEscape(transform)) return;
                    Back();
                    return;
                }
                if (!shell.HasConfirmation && !Drawing.IsGameplayInputBlocked && keyboard.spaceKey.wasPressedThisFrame && Page == PlayerPage.Battle)
                    Run.TogglePause();
                if (Page == PlayerPage.Battle && !shell.HasConfirmation && !Drawing.IsGameplayInputBlocked)
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
            if (Page == PlayerPage.Battle) Drawing.UpdateHordeFraming(shell.Layout.Field);
        }

        public void Navigate(PlayerPage page)
        {
            if (page != Page) MissionContinuationStatus = "";
            if (page == PlayerPage.Campaign && (Page == PlayerPage.Home || Page == PlayerPage.Map || Page == PlayerPage.Result))
                shell.ResetCampaignSelection();
            if (page == PlayerPage.Workshop && Page != PlayerPage.Workshop) workshopReturn = Page;
            Page = page;
            Run.SetMenuSuspended(!Run.InWorkshop && page != PlayerPage.Battle);
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
        public void Back()
        {
            if (shell.HasConfirmation) { shell.CancelConfirmation(); return; }
            if (WorkshopModal.TryCloseTop(transform)) return;
            switch (Page)
            {
                case PlayerPage.Battle: if (!Drawing.IsGameplayInputBlocked && !Drawing.CancelPlacement()) Pause(); break;
                case PlayerPage.Pause: Resume(); break;
                case PlayerPage.Settings: CloseSettings(); break;
                case PlayerPage.Help: CloseHelp(); break;
                case PlayerPage.Album: CloseAlbum(); break;
                case PlayerPage.Workshop: ReturnFromWorkshop(); break;
                case PlayerPage.Preparation: Navigate(PlayerPage.Map); break;
                case PlayerPage.Map: Navigate(Run.CampaignEnabled ? PlayerPage.Campaign : PlayerPage.Home); break;
                case PlayerPage.Campaign: Navigate(PlayerPage.Home); break;
                case PlayerPage.Result: OpenCampaign(); break;
            }
        }
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
            if (Run.HasMissionCheckpoint)
            {
                if (!shell.HasConfirmation)
                    shell.Confirm("Начать новую попытку?", "Сохранённая миссия будет заменена новой попыткой. Продолжить прежнюю после этого не получится.",
                        () => StartBattleReplacingCheckpoint());
                return false;
            }
            if (!Run.StartRun(out var reason)) { Status = GameText.Resolve(reason); shell.SetStatus(Status); return false; }
            EnterBattlePresentation();
            return true;
        }

        void StartBattleReplacingCheckpoint()
        {
            if (!Run.InWorkshop) return;
            if (!Run.StartRunReplacingCheckpoint(out var reason))
            { ShowMissionContinuationFailure(reason, "Start battle"); return; }
            EnterBattlePresentation();
        }

        void EnterBattlePresentation()
        {
            MissionContinuationStatus = "";
            Status = ""; Drawing.SelectSlot(0);
            World.CastleVisualScale = Run.UsesAlbumSlots ? .55f : 1;
            World.ModernEnemyPresentation = Run.UsesAlbumSlots;
            Navigate(PlayerPage.Battle);
            if (Run.UsesAlbumSlots) Drawing.FrameBattleOpening(shell.Layout.Field);
        }

        public void SaveAndExitMission()
        {
            if (Page != PlayerPage.Pause) return;
            if (!Run.TrySuspendMission(out var reason))
            { ShowMissionContinuationFailure(reason, "Pause save and exit"); return; }
            // Suspension has already committed the attempt and published the workshop preview.
            // OpenWorkshop here would finish the attempt as an ordinary abandoned battle.
            Status = MissionContinuationStatus = "";
            Navigate(PlayerPage.Home);
        }

        public void ResumeMissionCheckpoint()
        {
            if (Page != PlayerPage.Home || !Run.InWorkshop) return;
            if (!Run.TryResumeMission(out var reason))
            { ShowMissionContinuationFailure(reason, "Home continue mission"); return; }
            EnterBattlePresentation();
        }

        public void DiscardMissionCheckpoint()
        {
            if (Page != PlayerPage.Home || !Run.HasMissionCheckpoint || shell.HasConfirmation) return;
            shell.Confirm("Удалить сохранённую миссию?", "Расстановка и ход этой попытки будут потеряны. Чертежи и уже полученные награды останутся.", () =>
            {
                if (!Run.TryDiscardMissionCheckpoint(out var reason))
                { ShowMissionContinuationFailure(reason, "Home discard mission"); return; }
                Status = MissionContinuationStatus = "";
                Navigate(PlayerPage.Home);
            });
        }

        void ShowMissionContinuationFailure(string reason, string focus)
        {
            Status = MissionContinuationStatus = GameText.Resolve(reason);
            shell.RefreshMissionContinuation(focus);
        }

        public void Pause()
        {
            if (Page != PlayerPage.Battle || Finished) return;
            Navigate(PlayerPage.Pause);
        }

        public void Resume()
        {
            if (Page != PlayerPage.Pause && Page != PlayerPage.Upgrades) return;
            Navigate(Run.Session.PendingUpgrade ? PlayerPage.Upgrades : PlayerPage.Battle);
        }

        public void ReturnToMap()
        {
            Run.OpenWorkshop(); Status = ""; Navigate(PlayerPage.Map);
        }

        public void ReturnToPreparation()
        {
            Run.OpenWorkshop(); Status = ""; Navigate(PlayerPage.Preparation);
        }

        public void OpenCampaign()
        {
            if (!Run.InWorkshop) Run.OpenWorkshop();
            Status = "";
            Navigate(Run.CampaignEnabled ? PlayerPage.Campaign : PlayerPage.Map);
        }

        public bool OpenMission(string id)
        {
            string reason = "";
            if (!Run.InWorkshop || !Run.SelectMission(id, out reason))
            {
                Status = GameText.Resolve(Run.InWorkshop ? reason : "Сначала заверши текущий бой.");
                if (Page == PlayerPage.Campaign) shell.ShowCampaignMissionFailure(Status);
                else shell.SetStatus(Status);
                return false;
            }
            Status = ""; Navigate(PlayerPage.Map); return true;
        }

        public void NextCampaignMission()
        {
            string next = Run.MissionResult?.NextMissionId;
            Run.OpenWorkshop();
            if (!string.IsNullOrEmpty(next) && Run.IsMissionUnlocked(next)) OpenMission(next);
            else OpenCampaign();
        }

        public void ReturnToHome()
        { Run.OpenWorkshop(); Status = ""; Navigate(PlayerPage.Home); }

        public void ReturnToAlbumFromResult()
        { Run.OpenWorkshop(); Status = ""; albumReturn = Run.CampaignEnabled ? PlayerPage.Campaign : PlayerPage.Map; Navigate(PlayerPage.Album); }

        internal void InspectResultModule(AlbumEntry entry, ComponentDefinition part, int slot)
        {
            if (Page != PlayerPage.Result || !Run.IsCurrentMissionRewardSaved) return;
            Run.OpenWorkshop();
            Status = "";
            Navigate(PlayerPage.Workshop);
            workshopReturn = PlayerPage.Campaign;
            workshop.Composite.OpenEntry(entry, part, slot);
        }

        internal bool CanStartFirstRefitTutorial => Run.CanOfferFirstRefitTutorial && FirstRefitSource() != null;

        AlbumEntry FirstRefitSource()
        {
            bool Suitable(AlbumEntry entry) => entry?.Kind == AlbumEntryKind.Tower &&
                entry.Snapshot.Recipe.Tower.Mounts.Count == 1 &&
                entry.Snapshot.Recipe.Tower.Mounts[0].SlotIndex == 0 &&
                entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id == "core.gun" &&
                entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Head.Id != "head.long";
            return Run.Workshop.LoadoutEntries.FirstOrDefault(Suitable) ??
                Run.Workshop.Entries.FirstOrDefault(Suitable) ?? Run.AssemblyCatalog?.Starters.FirstOrDefault(Suitable);
        }

        public void StartFirstRefitTutorial()
        {
            if (!CanStartFirstRefitTutorial || (Page != PlayerPage.Result && !Run.InWorkshop)) return;
            var source = FirstRefitSource();
            var entries = Run.Workshop.LoadoutEntries;
            int slot = -1;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i]?.Id == source.Id) { slot = i; break; }
            if (slot < 0)
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i] == null) { slot = i; break; }
            // A full custom loadout is confirmed by name before the guide replaces its target slot.
            if (slot < 0) slot = 0;
            Run.OpenWorkshop(); Status = "";
            Navigate(PlayerPage.Workshop);
            workshopReturn = PlayerPage.Campaign;
            if (!workshop.Composite.FirstRefitActive) workshop.Composite.BeginFirstRefit(source, slot);
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

        public void ConfirmModernizationColor(ModernizationQuote quote)
        {
            if (!CanBuild || quote == null || !quote.Valid || !quote.IsColor) return;
            // Capture the immutable quote, not a later selected cell or freshly resolved target.
            shell.Confirm("Закрепить " + (quote.Color == InkColor.Red ? "красный" : quote.Color == InkColor.Blue ? "синий" : "зелёный") + " цвет?",
                "Цвет останется у этой башни до её уничтожения или продажи. Заменить или снять его нельзя.\n" +
                quote.Cost + " цветных чернил · " + quote.WorkSeconds.ToString("0.#") + " с работ",
                () => Drawing.SetMessage(Run.Modernize(quote, out var reason) ? "Начата установка цвета." : reason));
        }

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
