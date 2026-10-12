using System;
using System.Collections.Generic;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;

namespace PaperDefense.Validation
{
    /// <summary>One scene-owned driver, with all isolation selected before the child bootstrap awakens.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class MechanicsTestScene : MonoBehaviour
    {
        public MechanicsTestScenarioAsset scenario;
        public int variant;
        public PaperDefenseBootstrap Bootstrap { get; private set; }
        public RunController Controller => Bootstrap != null ? Bootstrap.Controller : null;
        public int ResetGeneration { get; private set; }
        public bool Prepared { get; private set; }
        public string PreparationError { get; private set; }
        public string FixtureDirectory => fixture?.DirectoryPath;
        public int ProfileLoadCalls => fixture?.LoadCalls ?? 0;
        public int ProfileSaveCalls => fixture?.SaveCalls ?? 0;
        public TrialComparison QualityComparison { get; private set; }
        public bool Paused { get; private set; }
        public int SimulationDriverCount => Prepared && !Controller.AutomaticSimulation ? 1 : 0;
        UserTextResolver texts;
        GameObject runtimeRoot;
        GameBalanceAsset runtimeAdapter;
        BalancePresetAsset runtimePreset;
        MechanicsProfileFixtureStore fixture;
        PressureScenario pressure;
        readonly FixedStepClock clock = new FixedStepClock();
        string status;

        void Awake() => ResetScenario();

        public void ResetScenario() => Prepare(false);
        public void ReloadFixture() { if (scenario.focus == MechanicsTestFocus.Profile) Prepare(true); }
        public void NewFixture() { fixture?.Dispose(); fixture = null; Prepare(false); }

        void Prepare(bool preserveFiles)
        {
            Prepared = false; PreparationError = null; pressure = null; QualityComparison = null; status = null;
            clock.Reset(); DestroyRuntime();
            try
            {
                if (scenario == null) throw new ArgumentException("mechanics.scene: assign scenario before Play.");
                texts = scenario.textResources.CreateResolver();
                texts.RequireKeys(new[] { scenario.titleKey, scenario.instructionsKey, "test.status", "test.reset", "test.pause", "test.step",
                    "test.blueprint_name", "test.tower_counters", "test.mount_counters", "test.target_counters", "test.color_counters",
                    "test.well_counters", "test.economy_counters", "test.profile_counters", "test.pressure_counters", "test.active", "test.inactive" });
                texts.RequireKeys(scenario.observationKeys);
                var balance = scenario.CreateBalance();
                if (scenario.focus == MechanicsTestFocus.Profile)
                {
                    if (!preserveFiles || fixture == null) { fixture?.Dispose(); fixture = new MechanicsProfileFixtureStore(); }
                    fixture.Reopen();
                }
                runtimePreset = Instantiate(scenario.preset);
                runtimePreset.rules = GlobalBalanceSettings.From(balance);
                runtimeAdapter = ScriptableObject.CreateInstance<GameBalanceAsset>();
                runtimeAdapter.legacyAuthoringAdapter = false; runtimeAdapter.dataPreset = runtimePreset;
                runtimeRoot = new GameObject("Mechanics Runtime"); runtimeRoot.SetActive(false);
                runtimeRoot.transform.SetParent(transform, false);
                var cameraObject = new GameObject("Mechanics World Camera", typeof(Camera));
                cameraObject.transform.SetParent(runtimeRoot.transform, false); cameraObject.tag = "MainCamera";
                Bootstrap = runtimeRoot.AddComponent<PaperDefenseBootstrap>();
                Bootstrap.balance = runtimeAdapter; Bootstrap.worldCamera = cameraObject.GetComponent<Camera>();
                Bootstrap.textResources = scenario.textResources; Bootstrap.memoryOnlyProfile = scenario.focus != MechanicsTestFocus.Profile;
                Bootstrap.ProfileStore = scenario.focus == MechanicsTestFocus.Profile ? fixture : null;
                Bootstrap.standMode = true; Bootstrap.showBattleHud = scenario.focus == MechanicsTestFocus.Integration;
                Bootstrap.showWorkshopHud = scenario.workshopMode;
                Bootstrap.PrepareController = PrepareController;
                runtimeRoot.SetActive(true);
                if (Bootstrap.Controller == null || Bootstrap.View == null || Bootstrap.Drawing == null)
                    throw new InvalidOperationException("mechanics.scene: bootstrap preparation did not complete.");
                Paused = scenario.startPaused; clock.Reset(); ResetGeneration++; Prepared = true;
            }
            catch (Exception error)
            {
                PreparationError = error.GetType().Name + ": " + error.Message;
                Debug.LogException(error, this); DestroyRuntime();
            }
        }

        void PrepareController(RunController controller)
        {
            controller.AutomaticSimulation = false;
            if (scenario.workshopMode)
            {
                if (scenario.focus == MechanicsTestFocus.Drawing) PrepareWorkshopPages(controller);
                return;
            }
            if (scenario.focus == MechanicsTestFocus.Pressure)
            {
                // Match PressurePlayerProbe's established fixture and finite default combat rules.
                // The scene still owns the deterministic seed and elevated starting budget.
                pressure = new PressureScenario(new PressureScenarioConfig
                { Seed = scenario.seed, StartingInk = scenario.startingInk });
                controller.Initialize(pressure.Session);
                return;
            }
            PrepareWorkshopPages(controller);
            if (!controller.StartRun(out string reason)) throw new InvalidOperationException(reason);
            GameSession session;
            if (scenario.focus == MechanicsTestFocus.Construction || scenario.focus == MechanicsTestFocus.Economy ||
                scenario.focus == MechanicsTestFocus.Wells)
                session = new GameSession(scenario.CreateBalance(), scenario.seed, false);
            else session = scenario.CreatePreparedSession(variant);
            controller.Initialize(session);
            for (int i = 0; i < scenario.placements.Count; i++)
            {
                var placement = scenario.placements[i];
                InkColor color = scenario.focus == MechanicsTestFocus.Color ? (InkColor)(1 + PositiveModulo(variant, 3)) : placement.color;
                if (!controller.BuildBlueprint(new[] { new Cell(placement.cell.x, placement.cell.y) }, i,
                    placement.material, color, out reason)) throw new InvalidOperationException(reason);
            }
            if (scenario.focus == MechanicsTestFocus.Construction)
            {
                if (!controller.Build(WallCells(), StructureKind.Wall, MaterialTier.Pencil, out reason))
                    throw new InvalidOperationException(reason);
                if (!controller.Build(new[] { new Cell(21, 14), new Cell(21, 15), new Cell(21, 16) },
                    StructureKind.Spikes, MaterialTier.Ink, out reason)) throw new InvalidOperationException(reason);
            }
        }

        void PrepareWorkshopPages(RunController controller)
        {
            for (int i = 0; i < scenario.placements.Count; i++)
            {
                var placement = scenario.placements[i];
                var weapons = new List<WeaponMountSelection>();
                int mountCount = placement.weaponIds.Length;
                if (scenario.focus == MechanicsTestFocus.Feed) mountCount = variant < 3 ? 1 + PositiveModulo(variant, 3) : 3;
                for (int mount = 0; mount < mountCount; mount++)
                    weapons.Add(new WeaponMountSelection(mount, scenario.focus == MechanicsTestFocus.Feed && variant >= 3 && mount == 2
                        ? "weapon.mortar" : placement.weaponIds[Math.Min(mount, placement.weaponIds.Length - 1)]));
                var modules = new List<ModuleSelection>();
                for (int module = 0; module < placement.moduleIds.Length; module++)
                    modules.Add(new ModuleSelection("fixture.module." + module, placement.moduleIds[module]));
                var page = new TowerBlueprint("fixture." + scenario.scenarioId + "." + i,
                    Text("test.blueprint_name", i + 1), placement.chassisId, weapons, modules, content: controller.Content);
                if (!controller.SaveBlueprint(page, out string reason) || !controller.AssignBlueprint(i, page.Id, out reason))
                    throw new InvalidOperationException(reason);
            }
        }

        void Update() => AdvanceFrame(Time.unscaledDeltaTime);
        public int AdvanceFrame(float realDelta)
        {
            if (!Prepared || Controller.InWorkshop) return 0;
            bool paused = Paused || Controller.Paused;
            // Ordinary fixtures use the controller's clock so the full HUD pause/speed controls
            // have exactly the same behavior as production. Pressure retains its own driver.
            if (pressure == null) return Controller.AdvanceFrame(realDelta, paused, Controller.Speed);
            int steps = clock.AdvanceFrame(realDelta, paused, Controller.Speed);
            for (int i = 0; i < steps; i++) AdvanceOneTick();
            return steps;
        }
        public void TogglePause()
        {
            Paused = !Paused; clock.ClearDebt();
            if (Prepared && pressure == null) Controller.AdvanceFrame(0, true, Controller.Speed);
        }
        public void Step() { if (Prepared && !Controller.InWorkshop) { clock.ClearDebt(); AdvanceOneTick(); } }
        void AdvanceOneTick()
        {
            if (pressure != null) { pressure.Step(); Controller.SynchronizeExternalStep(); }
            else Controller.StepSimulation();
        }
        public void ChangeVariant()
        {
            variant = PositiveModulo(variant + 1, scenario.focus == MechanicsTestFocus.Feed ? 4 : 3);
            ResetScenario();
        }
        public void CompareDrawing()
        {
            if (Prepared && Controller.Workshop.Album.Count > 0)
                QualityComparison = Controller.RunQualityTrial(Controller.Workshop.Loadout[0], TrialScenario.Crowd);
        }
        public bool SaveProfileVariant()
        {
            if (!Prepared || scenario.focus != MechanicsTestFocus.Profile) return false;
            var page = Controller.Workshop.Album[0].AsVariant("fixture.copy." + Controller.Workshop.Album.Count,
                Text("test.blueprint_name", Controller.Workshop.Album.Count + 1));
            bool result = Controller.SaveBlueprint(page, out string reason); status = reason; return result;
        }
        public void SetProfileWriteFailure(bool fail) { if (fixture != null) fixture.FailWrites = fail; }
        public void RestoreProfileBackup() { fixture?.CorruptPrimary(); ReloadFixture(); }
        public void PrepareLegacyProfile() { fixture?.WriteVersionFive(Controller.Content, scenario.CreateBalance()); ReloadFixture(); }

        static Cell[] WallCells() => new[] { new Cell(21, 12), new Cell(21, 13), new Cell(21, 17), new Cell(21, 18) };
        static int PositiveModulo(int value, int divisor) => (value % divisor + divisor) % divisor;
        string Text(string key, params object[] arguments) => texts.Resolve(UserText.Message(key, arguments));
        void OnGUI()
        {
            if (texts == null) return;
            bool compact = !scenario.workshopMode;
            // WorkshopHud reserves 60 screen pixels at each edge for these stand controls.
            float scale = compact ? Mathf.Max(.65f, Mathf.Min(Screen.width / 1280f, Screen.height / 800f)) : 1f;
            var previous = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            float panelWidth = compact ? width * .20f : width - 24;
            float panelHeight = compact ? Screen.height / scale - 80 : 52;
            var wrapped = new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.BeginArea(new Rect(12, compact ? 8 : 4, panelWidth, panelHeight), GUI.skin.box);
            GUILayout.Label(Text(scenario.titleKey), wrapped); GUILayout.Label(Text(scenario.instructionsKey), wrapped);
            if (compact && !Prepared) GUILayout.Label(Text("test.preparation_failed"));
            else if (compact)
            {
                var session = Controller.Session;
                GUILayout.Label(Text("test.status", session.TickIndex, session.Ink, session.Enemies.Count, session.Kills,
                    session.Statistics.TotalDamage, Launches(session), session.Blots.Count, session.BaseHealth), wrapped);
                DrawObservations(session, wrapped);
                if (!string.IsNullOrEmpty(status)) GUILayout.Label(texts.Resolve(status));
            }
            GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(12, Screen.height / scale - 60, width - 24, 52), GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("test.reset"))) ResetScenario();
            if (GUILayout.Button(Text("test.pause"))) TogglePause();
            if (GUILayout.Button(Text("test.step"))) Step();
            if (Prepared)
            {
                if (scenario.focus == MechanicsTestFocus.Feed || scenario.focus == MechanicsTestFocus.Color)
                    if (GUILayout.Button(Text("test.variant", variant + 1))) ChangeVariant();
                if (scenario.focus == MechanicsTestFocus.Drawing)
                    if (GUILayout.Button(Text("test.run_trial"))) CompareDrawing();
                if (scenario.focus == MechanicsTestFocus.Wells)
                    for (int i = 0; i < Controller.Session.Wells.Count; i++)
                    { int index = i; if (GUILayout.Button(Text("test.activate_well", i + 1))) Controller.ActivateWell(index, out status); }
                if (scenario.focus == MechanicsTestFocus.Blots)
                    if (GUILayout.Button(Text("test.clear_blots"))) Controller.Blotter(new Cell(24, 16), out status);
                if (scenario.focus == MechanicsTestFocus.Economy || scenario.focus == MechanicsTestFocus.Wells ||
                    scenario.focus == MechanicsTestFocus.Construction)
                {
                    if (GUILayout.Button(Text("test.next_wave"))) Controller.NextWave();
                    if (Controller.Session.PendingUpgrade && GUILayout.Button(Text("test.choose_upgrade"))) Controller.ChooseUpgrade(RunUpgrade.RapidFire);
                }
                if (scenario.focus == MechanicsTestFocus.Construction)
                {
                    if (GUILayout.Button(Text("test.upgrade"))) Controller.Upgrade(new Cell(21, 12), out status);
                    if (GUILayout.Button(Text("test.erase"))) Controller.Erase(new Cell(21, 13), out _);
                }
                if (scenario.focus == MechanicsTestFocus.Profile)
                {
                    if (GUILayout.Button(Text("test.save"))) SaveProfileVariant();
                    if (GUILayout.Button(Text("test.profile_error"))) SetProfileWriteFailure(!fixture.FailWrites);
                    if (GUILayout.Button(Text("test.reload"))) ReloadFixture();
                    if (GUILayout.Button(Text("test.backup"))) RestoreProfileBackup();
                    if (GUILayout.Button(Text("test.migration"))) PrepareLegacyProfile();
                    if (GUILayout.Button(Text("test.new_fixture"))) NewFixture();
                }
            }
            GUILayout.EndHorizontal();
            if (!compact)
            {
                GUILayout.BeginHorizontal();
                if (!Prepared) GUILayout.Label(Text("test.preparation_failed"));
                else if (scenario.focus == MechanicsTestFocus.Profile)
                    GUILayout.Label(Text("test.profile_counters", ProfileLoadCalls, ProfileSaveCalls, Controller.Workshop.Album.Count));
                if (!string.IsNullOrEmpty(status)) GUILayout.Label(texts.Resolve(status));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea(); GUI.matrix = previous;
        }
        void DrawObservations(IRunReadModel session, GUIStyle style)
        {
            foreach (string key in scenario.observationKeys) GUILayout.Label(Text(key), style);
            if (scenario.focus == MechanicsTestFocus.Pressure)
            {
                GUILayout.Label(Text("test.pressure_counters", session.Statistics.PeakAliveEnemies,
                    session.Statistics.DestroyedStructures, session.Statistics.NavigationRebuilds,
                    session.Statistics.NavigationMaxMilliseconds), style);
                return;
            }
            foreach (var structure in session.Structures)
            {
                if (structure.Blueprint == null) continue;
                GUILayout.Label(Text("test.tower_counters", structure.Id, structure.Health, structure.MaxHealth,
                    structure.BuildRemaining), style);
                foreach (var mount in structure.Blueprint.WeaponMounts)
                {
                    var state = structure.GetMountState(mount.SlotIndex);
                    GUILayout.Label(Text("test.mount_counters", mount.SlotIndex + 1, state.TargetId,
                        state.AcquisitionRemaining, state.ActionRemaining), style);
                }
            }
            for (int i = 0; i < Math.Min(5, session.Enemies.Count); i++)
            {
                var target = session.Enemies[i];
                GUILayout.Label(Text("test.target_counters", target.Id, target.Health, target.MaxHealth, target.WindupRemaining,
                    target.RedRemaining, target.BlueRemaining, target.GreenRemaining, target.BlotRemaining), style);
            }
            if (scenario.mechanics.ColorCartridges)
                GUILayout.Label(Text("test.color_counters", session.ColoredInk(InkColor.Red), session.ColoredInk(InkColor.Blue),
                    session.ColoredInk(InkColor.Green)), style);
            if (scenario.mechanics.InkWells)
                foreach (var well in session.Wells)
                    GUILayout.Label(Text("test.well_counters", well.Id + 1, well.Health, well.MaxHealth,
                        Text(well.Active ? "test.active" : "test.inactive")), style);
            if (scenario.focus == MechanicsTestFocus.Construction || scenario.focus == MechanicsTestFocus.Economy)
                GUILayout.Label(Text("test.economy_counters", session.Statistics.BuildInvestment,
                    session.Statistics.UpgradeInvestment, session.Statistics.RefundedInk, session.GoldEarned), style);
        }
        static int Launches(IRunReadModel session)
        { int count = 0; foreach (var weapon in session.Balance.Content.Weapons) count += session.Statistics.Launches(weapon.Id); return count; }
        void DestroyRuntime()
        {
            if (runtimeRoot != null) { runtimeRoot.SetActive(false); Destroy(runtimeRoot); }
            if (runtimeAdapter != null) Destroy(runtimeAdapter);
            if (runtimePreset != null) Destroy(runtimePreset);
            Bootstrap = null; runtimeRoot = null; runtimeAdapter = null; runtimePreset = null;
        }
        void OnDestroy() { Prepared = false; DestroyRuntime(); fixture?.Dispose(); fixture = null; }
    }
}
