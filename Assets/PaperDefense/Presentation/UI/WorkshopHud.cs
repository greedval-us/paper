using System;
using System.Collections.Generic;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public enum WorkshopHudMode { Full = 0, Assembly = 1, Drawing = 2, Profile = 3 }

    /// <summary>The album edits immutable drawings; a run receives four frozen choices.</summary>
    public sealed class WorkshopHud : MonoBehaviour
    {
        public UserTextResolver TextResolver { get; set; }
        public WorkshopHudMode Mode { get; set; }
        public Action StartRequested { get; set; }
        public bool UseGuidedFlow { get; set; }
        public WorkshopWizard Guided { get; private set; }
        bool presentationVisible = true;
        public bool PresentationVisible
        {
            get => presentationVisible;
            set { bool changed=presentationVisible!=value;presentationVisible = value; if (canvasObject != null) canvasObject.SetActive(value && run.InWorkshop);if(changed&&Guided!=null){if(value)Guided.Resume();else Guided.Suspend();} }
        }
        public void SetPlayerLayout()
        {
            if(Guided!=null)
            {
                var responsive = canvasObject.GetComponent<WorkshopResponsiveLayout>() ?? canvasObject.AddComponent<WorkshopResponsiveLayout>();
                responsive.Initialize(root, GetComponentInParent<PlayerGameRoot>());
                return;
            }
            root.localScale = Vector3.one * .9f;
            root.anchoredPosition = new Vector2(0, -35);
        }
        string Resolve(string value) => GameText.Resolve(TextResolver, value);
        const int AlbumRowsPerPage = 8;
        RunController run;
        Font font;
        GameObject canvasObject, trialPanel, trialShade;
        RectTransform root;
        Text gold, stats, status, briefing, trialResults, trialSettings, trialFirst, trialSecond, trialPlayback, trialConditions;
        Text profileSaveStatus;
        Button retryProfileSave;
        Text trialFirstHeader, trialSecondHeader, armorSlotsLabel, upgradeSlotsLabel;
        InputField title;
        BlueprintSketch sketch;
        readonly BlueprintSketch[] albumSketches = new BlueprintSketch[AlbumRowsPerPage];
        public DrawingEditorPanel DrawingEditor { get; private set; }
        public MortarFootprintPanel MortarFootprintEditor { get; private set; }
        public FanVolleyPanel FanVolleyEditor { get; private set; }
        public ArmorReinforcementPanel ReinforcementEditor { get; private set; }
        Button reinforcementButton;
        WorkshopMapSketch map;
        readonly Button[] footprintButtons = new Button[3];
        readonly Button[] trialScenarioButtons = new Button[4];
        TrialLaneSketch trialDiagramFirst, trialDiagramSecond;
        readonly Button[] albumButtons = new Button[AlbumRowsPerPage];
        readonly Text[] albumLabels = new Text[AlbumRowsPerPage], slotLabels = new Text[ProfileLimits.LoadoutSlotCount];
        Button[] partButtons, firstWeapons, secondWeapons, thirdWeapons, moduleButtons;
        Button[] chassisButtons;
        Text[] partLabels;
        string[] partIds;
        string[] weaponIds, moduleIds, chassisIds;
        int albumPage, trialBudget = 400;
        float refresh;
        bool counterfire;
        TrialScenario trialScenario;
        public TowerBlueprint Draft { get; private set; }
        TowerBlueprint initialDraft;
        string statusMessage = UserText.Message("copy.6ac1fcec14161a7a");
        public string Status { get => Resolve(statusMessage); private set => statusMessage = value; }

        public void Initialize(RunController controller)
        {
            run = controller;
            trialBudget = run.Session.Balance.Trials.DefaultBudget;
            var mounts = new List<string>();
            var modules = new List<string>();
            var chassis = new List<string>();
            var unlocks = new List<string>();
            foreach (var part in run.Content.AllParts)
            {
                if (!part.Enabled) continue;
                if (part.Category == PartCategory.Weapon && part.Id != LegacyContentIds.Weapon(StructureKind.Wall) && part.Id != LegacyContentIds.Weapon(StructureKind.Spikes)) mounts.Add(part.Id);
                if (part.Category == PartCategory.Module) modules.Add(part.Id);
                if (part.Category == PartCategory.Chassis) chassis.Add(part.Id);
                if (!part.InitiallyUnlocked) unlocks.Add(part.Id);
            }
            weaponIds = mounts.ToArray(); firstWeapons = new Button[weaponIds.Length]; secondWeapons = new Button[weaponIds.Length + 1]; thirdWeapons = new Button[weaponIds.Length + 1];
            moduleIds = modules.ToArray(); moduleButtons = new Button[moduleIds.Length];
            chassisIds = chassis.ToArray(); chassisButtons = new Button[chassisIds.Length];
            partIds = unlocks.ToArray(); partButtons = new Button[partIds.Length]; partLabels = new Text[partIds.Length];
            font = Resources.Load<Font>("NotebookHand") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("Workshop HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1000); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var board = new GameObject("Workshop paper", typeof(RectTransform), typeof(Image));
            board.transform.SetParent(canvasObject.transform, false); root = board.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f); root.sizeDelta = new Vector2(1600, 1000);
            board.GetComponent<Image>().color = NotebookPalette.Paper; board.GetComponent<Image>().raycastTarget = true;
            Draft = run.Workshop.Album.Count > 0 ? run.Workshop.Album[0] : NewDrawing();
            initialDraft = Draft;
            if(UseGuidedFlow&&Mode==WorkshopHudMode.Full)
            {
                Guided=new WorkshopWizard(root,run,TextResolver,value=>Draft=value,()=>StartRequested?.Invoke());
                MortarFootprintEditor=Guided.Mortar;FanVolleyEditor=Guided.Volley;ReinforcementEditor=Guided.Armor;
                run.WorkshopChanged+=Refresh;Refresh();return;
            }
            switch (Mode)
            {
                case WorkshopHudMode.Assembly: BuildAssembly(); break;
                case WorkshopHudMode.Drawing: BuildDrawing(); break;
                case WorkshopHudMode.Profile: BuildProfile(); break;
                default: BuildComposer(); BuildAlbum(); BuildLoadout(); BuildTrial(); break;
            }
            if (Mode == WorkshopHudMode.Full || Mode == WorkshopHudMode.Drawing)
            {
                MortarFootprintEditor = new MortarFootprintPanel(root, font, value => { Draft = value; Refresh(); }, run.CompareMortarFootprints, value => run.PreviewBlueprint(value).Reason, TextResolver, run.CompareMortarForms);
                FanVolleyEditor = new FanVolleyPanel(root,font,value=>{Draft=value;Refresh();},run.ProbeFanVolley,TextResolver);
                ReinforcementEditor=new ArmorReinforcementPanel(root,font,value=>{Draft=value;Refresh();},run.ProbeArmor,run.ArmorDefense,run.MaximumStructureDefense,TextResolver);
                DrawingEditor = new DrawingEditorPanel(root, font, drawing => { CloseGeometryEditors(); Draft = Draft.WithFreeformDrawing(drawing); SetStatus(UserText.Message(Mode == WorkshopHudMode.Drawing ? "workshop.drawing_hint" : "copy.65c92ef5c0dfe24a")); }, TextResolver,
                    context: new DrawingEditorContext { Preview = candidate => run.PreviewBlueprint(candidate), Workshop = () => run.Workshop, Purchase = run.UnlockDrawingAid, Probe=run.ProbeDrawing });
            }
            if (title != null) title.SetTextWithoutNotify(Resolve(Draft.Name));
            run.WorkshopChanged += Refresh;
            Refresh();
        }

        void OnDestroy() { if (run != null) run.WorkshopChanged -= Refresh; }
        void Update()
        {
            if (run == null) return;
            canvasObject.SetActive(run.InWorkshop && PresentationVisible);
            if (!run.InWorkshop) { if(Guided!=null)Guided.Suspend();else CloseGeometryEditors(); return; }
            refresh -= Time.unscaledDeltaTime;
            if (refresh <= 0) { refresh = .15f; Refresh(); }
        }

        TowerBlueprint NewDrawing() => new TowerBlueprint("drawing-" + Guid.NewGuid().ToString("N"), Resolve(UserText.Message("copy.74a4cbe8134d0a38")), chassisIds[0], weaponIds[0],
            origin: DrawingOrigin.Freeform, content: run.Content);
        void NewDraft() { CloseGeometryEditors(); Draft = NewDrawing(); if (title != null) title.SetTextWithoutNotify(Resolve(Draft.Name)); SetStatus(UserText.Message("copy.a2f3120b30cce36d")); }
        void ResetDraft() { CloseGeometryEditors(); Draft = initialDraft; if (title != null) title.SetTextWithoutNotify(Resolve(Draft.Name)); SetStatus(UserText.Message("workshop.reset_done")); }
        void SetDraft(string chassis, string first, string second, IEnumerable<ModuleSelection> modules, string selectedPart = null)
        {
            CloseGeometryEditors();
            Draft = Draft.WithContent(run.Content).WithName(title != null ? title.text : Draft.Name).WithAssembly(chassis, first, second, modules);
            if (selectedPart != null) foreach (var part in run.Workshop.Parts) if (part.Id == selectedPart) { Status = part.Description; break; }
            Refresh();
        }
        void SetWeaponMount(int mount, string weaponId)
        {
            CloseGeometryEditors();
            Draft = Draft.WithContent(run.Content).WithName(title != null ? title.text : Draft.Name).WithWeaponMount(mount, weaponId);
            if (weaponId != null) Status = run.Content.GetWeapon(weaponId).Description;
            Refresh();
        }
        void ToggleModule(string definitionId)
        {
            var selected = new List<ModuleSelection>(Draft.ModuleSelections);
            int existing = selected.FindIndex(value => value.DefinitionId == definitionId);
            bool repeatable = run.Content.GetModule(definitionId).Repeatable;
            if (existing >= 0 && !repeatable) selected.RemoveAt(existing);
            else
            {
                var legacy = LegacyContentIds.Module(definitionId);
                string instance = legacy == TowerModules.None || repeatable && existing >= 0
                    ? Guid.NewGuid().ToString("N") : definitionId.Substring("module.".Length);
                selected.Add(new ModuleSelection(instance, definitionId));
            }
            SetDraft(Draft.ChassisId, Draft.FirstWeaponId, Draft.SecondWeaponId, selected, definitionId);
        }
        void RemoveModule(string id)
        {
            var selected = new List<ModuleSelection>(Draft.ModuleSelections);
            int existing = selected.FindLastIndex(value => value.DefinitionId == id);
            if (existing >= 0) selected.RemoveAt(existing);
            SetDraft(Draft.ChassisId, Draft.FirstWeaponId, Draft.SecondWeaponId, selected, id);
        }
        bool SelectedModule(string id) { foreach (var selected in Draft.ModuleSelections) if (selected.DefinitionId == id) return true; return false; }
        string CatalogButtonName(WorkshopPartDefinition part)
            => part.MountKind.HasValue && LegacyContentIds.Weapon(part.MountKind.Value) == part.Id ? part.MountKind.Value.ToString() :
                part.Chassis.HasValue && LegacyContentIds.Chassis(part.Chassis.Value) == part.Id ? part.Chassis.Value.ToString() :
                part.Module != TowerModules.None && LegacyContentIds.Module(part.Module) == part.Id ? part.Module.ToString() : part.Id;
        void SetStatus(string value) { Status = value; Refresh(); }

        void FocusedHeader(string heading, string hint, string initialStatus)
        {
            Status = UserText.Message(initialStatus);
            Label(root, UserText.Message(heading), new Rect(35, 17, 1525, 57), 38, NotebookPalette.Navy, true);
            Label(root, UserText.Message(hint), new Rect(37, 78, 1525, 31), 18, NotebookPalette.Navy);
            status = Label(root, "", new Rect(35, 910, 1525, 66), 19, NotebookPalette.Blue);
        }
        void BuildAssembly()
        {
            FocusedHeader("workshop.assembly_title", "workshop.assembly_hint", "workshop.assembly_status");
            var composer = Panel(root, "Composer", new Rect(28, 126, 1000, 740), new Color(1, .985f, .91f));
            title = Input(composer, "Blueprint name", new Rect(20, 18, 600, 43));
            title.onEndEdit.AddListener(value => { Draft = Draft.WithName(value); Refresh(); });
            Button(composer, "New blueprint", new Rect(638, 18, 150, 43), UserText.Message("copy.c1e5de3d705e623d"), NewDraft);
            Button(composer, "Reset blueprint", new Rect(802, 18, 178, 43), UserText.Message("workshop.reset_draft"), ResetDraft);
            Label(composer, UserText.Message("workshop.chassis_hint"), new Rect(20, 78, 960, 30), 20, NotebookPalette.Navy, true);
            var chassis = Panel(composer, "Chassis catalogue", new Rect(20, 118, 960, 112), Color.clear);
            for (int i = 0; i < chassisIds.Length; i++)
            {
                string id = chassisIds[i]; var definition = run.Content.GetChassis(id);
                chassisButtons[i] = Button(chassis, "Chassis " + CatalogButtonName(definition), new Rect(i % 2 * 485, i / 2 * 56, 475, 49),
                    UserText.Message("workshop.chassis_button", definition.ButtonLabel, definition.Capacity), () => SetDraft(id, Draft.FirstWeaponId, Draft.SecondWeaponId, Draft.ModuleSelections, id));
                NotebookArtGraphic.Attach(chassisButtons[i], id);
            }
            string[] catalogueNames = { "First weapon catalogue", "Second weapon catalogue", "Third weapon catalogue" };
            string[] buttonPrefixes = { "First ", "Second ", "Third " };
            string[] mountLabels = { "copy.2557f70bf5dad935", "copy.8fbc6e7635c25190", "workshop.third_mount" };
            for (int mount = 0; mount < 3; mount++)
            {
                int selectedMount = mount;
                Label(composer, UserText.Message(mountLabels[mount]), new Rect(20 + mount * 323, 247, 310, 30), 21, NotebookPalette.Blue, true);
                var catalogue = Panel(composer, catalogueNames[mount], new Rect(20 + mount * 323, 285, 310, 200), Color.clear);
                var buttons = mount == 0 ? firstWeapons : mount == 1 ? secondWeapons : thirdWeapons;
                for (int i = 0; i < weaponIds.Length; i++)
                {
                    string id = weaponIds[i]; var definition = run.Content.GetWeapon(id);
                    buttons[i] = Button(catalogue, buttonPrefixes[mount] + CatalogButtonName(definition), new Rect(i % 2 * 157, i / 2 * 49, 149, 43), definition.ButtonLabel, () => SetWeaponMount(selectedMount, id));
                    buttons[i].GetComponentInChildren<Text>().fontSize = 16;
                    NotebookArtGraphic.Attach(buttons[i], id);
                }
                if (mount > 0)
                {
                    int empty = weaponIds.Length;
                    buttons[empty] = Button(catalogue, buttonPrefixes[mount] + "None", new Rect(empty % 2 * 157, empty / 2 * 49, 149, 43),
                        UserText.Message("copy.e12d011cadf1b6f4"), () => SetWeaponMount(selectedMount, null));
                }
            }
            armorSlotsLabel = Label(composer, "", new Rect(20, 510, 220, 30), 19, NotebookPalette.Navy, true);
            upgradeSlotsLabel = Label(composer, "", new Rect(260, 510, 720, 30), 19, NotebookPalette.Navy, true);
            var armor = Panel(composer, "Armor catalogue", new Rect(20, 555, 220, 125), Color.clear);
            var upgrades = Panel(composer, "Module catalogue", new Rect(260, 555, 720, 125), Color.clear);
            int armorIndex = 0, upgradeIndex = 0;
            for (int i = 0; i < moduleIds.Length; i++)
            {
                string id = moduleIds[i]; var part = run.Content.GetModule(id);
                bool isArmor = part.SlotKind == ModuleSlotKind.Armor;
                int index = isArmor ? armorIndex++ : upgradeIndex++;
                int columns = isArmor ? 1 : 3;
                var parent = isArmor ? armor : upgrades;
                float x = index % columns * 240, y = index / columns * 55;
                moduleButtons[i] = Button(parent, "Module " + CatalogButtonName(part), new Rect(x, y, part.Repeatable ? 180 : 220, 47), part.ButtonLabel, () => ToggleModule(id));
                moduleButtons[i].GetComponentInChildren<Text>().fontSize = 15;
                NotebookArtGraphic.Attach(moduleButtons[i], id);
                if (part.Repeatable) Button(parent, "Remove module " + id, new Rect(x + 187, y, 33, 47), UserText.Message("common.remove_symbol"), () => RemoveModule(id));
            }
            BuildFocusedPreview(new Rect(1046, 126, 525, 740), true);
        }
        void BuildFocusedPreview(Rect bounds, bool showReference = false)
        {
            var panel = Panel(root, "Live drawing", bounds, new Color(.97f, .97f, .91f));
            string heading = Mode == WorkshopHudMode.Assembly ? "workshop.assembly_preview" : Mode == WorkshopHudMode.Drawing ? "workshop.drawing_preview" : "workshop.profile_preview";
            Label(panel, UserText.Message(heading), new Rect(22, 16, bounds.width - 44, 40), 26, NotebookPalette.Blue, true);
            var illustration = new GameObject("Live blueprint sketch", typeof(RectTransform), typeof(CanvasRenderer), typeof(BlueprintSketch));
            illustration.transform.SetParent(panel, false); Place(illustration.GetComponent<RectTransform>(), new Rect(30, 70, bounds.width - 60, 325));
            sketch = illustration.GetComponent<BlueprintSketch>(); sketch.raycastTarget = false; sketch.ShowReference = showReference;
            stats = Label(panel, "", new Rect(24, 420, bounds.width - 48, bounds.height - 440), 19, NotebookPalette.Navy);
        }
        void BuildDrawing()
        {
            FocusedHeader("workshop.drawing_title", "workshop.drawing_hint", "workshop.drawing_status");
            BuildFocusedPreview(new Rect(28, 126, 745, 740));
            var controls = Panel(root, "Drawing controls", new Rect(800, 126, 771, 740), new Color(1, .985f, .91f));
            Label(controls, UserText.Message("workshop.drawing_name"), new Rect(30, 22, 711, 36), 24, NotebookPalette.Blue, true);
            title = Input(controls, "Blueprint name", new Rect(30, 70, 711, 47));
            title.onEndEdit.AddListener(value => { Draft = Draft.WithName(value); Refresh(); });
            Button(controls, "Open drawing editor", new Rect(30, 143, 711, 67), UserText.Message("copy.d7e0dfdd628b75da"), () =>
            {
                CloseGeometryEditors();
                if (DrawingTemplateCatalog.Create(Draft).Count == 0) { SetStatus(UserText.Message("copy.b86b24244e915380")); return; }
                DrawingEditor.Open(Draft);
            });
            for (int mount = 0; mount < 3; mount++)
            {
                int selected = mount;
                footprintButtons[mount] = Button(controls, "Mortar footprint " + mount, new Rect(30 + mount * 240, 239, 231, 67),
                    UserText.Message("copy.b6caeb9a0b784643"), () => OpenMountGeometry(selected));
            }
            reinforcementButton=Button(controls,"Open reinforcement",new Rect(30,334,711,60),UserText.Message("reinforce.open"),OpenReinforcement);
            Button(controls, "Open trial", new Rect(30, 428, 711, 67), UserText.Message("copy.65e901944fe5438f"), () => RunDrawingTrial());
            Label(controls, UserText.Message("workshop.comparison_hint"), new Rect(30, 529, 711, 160), 23, NotebookPalette.Navy);
            BuildDrawingComparison();
        }
        void BuildDrawingComparison()
        {
            trialShade = Panel(root, "Trial background", new Rect(0, 0, 1600, 1000), new Color(.17f, .22f, .29f, .18f)).gameObject;
            trialShade.GetComponent<Image>().raycastTarget = true;
            trialPanel = Panel(root, "Test sheet", new Rect(180, 100, 1240, 800), NotebookPalette.Paper).gameObject;
            trialPanel.GetComponent<Image>().raycastTarget = true;
            Label(trialPanel.transform, UserText.Message("freeform.trial_title"), new Rect(28, 20, 1184, 46), 30, NotebookPalette.Blue, true);
            trialConditions = Label(trialPanel.transform, UserText.Message("workshop.comparison_hint"), new Rect(30, 81, 1180, 70), 21, NotebookPalette.Navy);
            trialSettings = Label(trialPanel.transform, "", new Rect(30, 156, 1180, 45), 20, NotebookPalette.Navy);
            trialPlayback = Label(trialPanel.transform, "", new Rect(30, 205, 1180, 30), 18, NotebookPalette.Blue);
            trialDiagramFirst = LaneDiagram(trialPanel.transform, "Blueprint test lane", new Rect(30, 250, 570, 230));
            trialDiagramSecond = LaneDiagram(trialPanel.transform, "Budget test lane", new Rect(640, 250, 570, 230));
            trialFirstHeader = Label(trialPanel.transform, "", new Rect(403, 497, 350, 35), 19, NotebookPalette.Blue, true, TextAnchor.MiddleCenter);
            trialSecondHeader = Label(trialPanel.transform, "", new Rect(790, 497, 420, 35), 19, NotebookPalette.Blue, true, TextAnchor.MiddleCenter);
            trialResults = Label(trialPanel.transform, UserText.Message("copy.8f02821e075dc0d1"), new Rect(30, 541, 354, 166), 15, NotebookPalette.Navy);
            trialFirst = Label(trialPanel.transform, "", new Rect(403, 541, 350, 166), 16, NotebookPalette.Navy, false, TextAnchor.UpperCenter);
            trialSecond = Label(trialPanel.transform, "", new Rect(790, 541, 420, 166), 16, NotebookPalette.Navy, false, TextAnchor.UpperCenter);
            Button(trialPanel.transform, "Run trial", new Rect(30, 724, 570, 55), UserText.Message("copy.65e901944fe5438f"), () => RunDrawingTrial());
            Button(trialPanel.transform, "Close trial", new Rect(640, 724, 570, 55), UserText.Message("copy.071e234de8bba5a1"), () => SetTrial(false));
            SetTrial(false);
        }
        void BuildProfile()
        {
            FocusedHeader("workshop.profile_title", "workshop.profile_hint", "workshop.profile_status");
            var controls = Panel(root, "Profile controls", new Rect(28, 126, 745, 125), new Color(1, .985f, .91f));
            title = Input(controls, "Blueprint name", new Rect(20, 14, 705, 43));
            title.onEndEdit.AddListener(value => { Draft = Draft.WithName(value); Refresh(); });
            Button(controls, "Save blueprint", new Rect(20, 70, 210, 43), UserText.Message("copy.28760c1a5ec2d81f"), SaveDraft);
            Button(controls, "Delete blueprint", new Rect(247, 70, 190, 43), UserText.Message("copy.1669c94226115554"), () => SetStatus(run.DeleteBlueprint(Draft.Id, out var reason) ? UserText.Message("copy.5212a5c55afa3a20") : reason));
            Button(controls, "Save blueprint as new", new Rect(454, 70, 271, 43), UserText.Message("copy.ec1a51e19086049c"), SaveAsNew);
            var album = Panel(root, "Blueprint album", new Rect(28, 270, 745, 560), new Color(1, .98f, .86f));
            Label(album, UserText.Message("copy.c2e65d663e93fb11"), new Rect(20, 15, 705, 40), 26, NotebookPalette.Blue, true);
            for (int i = 0; i < albumButtons.Length; i++)
            {
                int index = i;
                albumButtons[i] = Button(album, "Album drawing " + i, new Rect(20, 70 + i * 50, 705, 45), "", () => SelectAlbum(albumPage * AlbumRowsPerPage + index), out albumLabels[i]);
                var thumb = new GameObject("Album thumbnail " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(BlueprintSketch));
                thumb.transform.SetParent(albumButtons[i].transform, false); Place(thumb.GetComponent<RectTransform>(), new Rect(4, 2, 48, 39));
                albumSketches[i] = thumb.GetComponent<BlueprintSketch>(); albumSketches[i].raycastTarget = false;
                Place(albumLabels[i].rectTransform, new Rect(62, 1, 635, 43)); albumLabels[i].fontSize = 15;
            }
            Button(album, "Album previous", new Rect(20, 492, 343, 47), UserText.Message("copy.ec67fe18caaef874"), () => { albumPage = Mathf.Max(0, albumPage - 1); Refresh(); });
            Button(album, "Album next", new Rect(382, 492, 343, 47), UserText.Message("copy.689b295f49461225"), () => { albumPage = Mathf.Min(Mathf.Max(0, (run.Workshop.Album.Count - 1) / AlbumRowsPerPage), albumPage + 1); Refresh(); });
            BuildFocusedPreview(new Rect(800, 126, 771, 704));
            profileSaveStatus = Label(root, "", new Rect(35, 850, 1205, 50), 18, NotebookPalette.Navy);
            profileSaveStatus.name = "Profile save status";
            retryProfileSave = Button(root, "Retry profile save", new Rect(1262, 850, 307, 43), UserText.Message("copy.936aa189718b6d93"), () =>
                SetStatus(run.RetrySave(out var reason) ? reason : UserText.Message("copy.0525c85d076db630", reason)));
        }

        void BuildComposer()
        {
            Label(root, UserText.Message("copy.159b7c81e8a7413c"), new Rect(35, 17, 1050, 57), 38, NotebookPalette.Navy, true);
            gold = Label(root, "", new Rect(1170, 23, 395, 46), 27, NotebookPalette.Pencil, true, TextAnchor.MiddleRight);
            Label(root, UserText.Message("copy.6dc47581f3429f71"), new Rect(37, 78, 1525, 31), 18, NotebookPalette.Navy);
            var left = Panel(root, "Composer", new Rect(28, 126, 532, 570), new Color(1, .985f, .91f));
            Label(left, UserText.Message("copy.453386ab17042692"), new Rect(18, 10, 490, 35), 25, NotebookPalette.Blue, true);
            title = Input(left, "Blueprint name", new Rect(18, 54, 490, 43));
            title.onEndEdit.AddListener(value => { if (Draft != null) SetDraft(Draft.ChassisId, Draft.FirstWeaponId, Draft.SecondWeaponId, Draft.ModuleSelections); });
            Label(left, UserText.Message("workshop.chassis_hint"), new Rect(18, 107, 490, 29), 18, NotebookPalette.Navy);
            var chassisList = new CatalogScrollList(left, "Chassis catalogue", new Rect(18, 141, 490, 49), chassisIds.Length, 2, 44, 5);
            for (int i = 0; i < chassisIds.Length; i++)
            {
                string id = chassisIds[i]; var definition = run.Content.GetChassis(id);
                chassisButtons[i] = Button(chassisList.Content, "Chassis " + CatalogButtonName(definition), new Rect(i % 2 * 250, i / 2 * 49, 235, 44),
                    UserText.Message("workshop.chassis_button", definition.ButtonLabel, definition.Capacity), () => SetDraft(id, Draft.FirstWeaponId, Draft.SecondWeaponId, Draft.ModuleSelections, id));
                chassisButtons[i].GetComponentInChildren<Text>().fontSize = 14;
                NotebookArtGraphic.Attach(chassisButtons[i], id);
            }
            Label(left, UserText.Message("copy.2557f70bf5dad935"), new Rect(18, 196, 490, 24), 18, NotebookPalette.Navy, true);
            var firstList = new CatalogScrollList(left, "First weapon catalogue", new Rect(18, 221, 490, 45), weaponIds.Length, 4, 32, 6);
            var secondList = new CatalogScrollList(left, "Second weapon catalogue", new Rect(18, 292, 490, 45), weaponIds.Length + 1, 4, 32, 6);
            var thirdList = new CatalogScrollList(left, "Third weapon catalogue", new Rect(18, 363, 490, 45), weaponIds.Length + 1, 4, 32, 6);
            for (int i = 0; i < weaponIds.Length; i++)
            {
                string id = weaponIds[i]; var definition = run.Content.GetWeapon(id);
                firstWeapons[i] = Button(firstList.Content, "First " + CatalogButtonName(definition), new Rect(i % 4 * 123, i / 4 * 38, 115, 32), definition.ButtonLabel, () => SetDraft(Draft.ChassisId, id, Draft.SecondWeaponId, Draft.ModuleSelections, id));
                secondWeapons[i] = Button(secondList.Content, "Second " + CatalogButtonName(definition), new Rect(i % 4 * 123, i / 4 * 38, 115, 32), definition.ButtonLabel, () => SetDraft(Draft.ChassisId, Draft.FirstWeaponId, id, Draft.ModuleSelections, id));
                thirdWeapons[i] = Button(thirdList.Content, "Third " + CatalogButtonName(definition), new Rect(i % 4 * 123, i / 4 * 38, 115, 32), definition.ButtonLabel, () => SetWeaponMount(2, id));
                firstWeapons[i].GetComponentInChildren<Text>().fontSize = secondWeapons[i].GetComponentInChildren<Text>().fontSize = thirdWeapons[i].GetComponentInChildren<Text>().fontSize = 13;
                NotebookArtGraphic.Attach(firstWeapons[i], id); NotebookArtGraphic.Attach(secondWeapons[i], id); NotebookArtGraphic.Attach(thirdWeapons[i], id);
            }
            Label(left, UserText.Message("copy.8fbc6e7635c25190"), new Rect(18, 267, 490, 24), 18, NotebookPalette.Navy, true);
            Label(left, UserText.Message("workshop.third_mount"), new Rect(18, 338, 490, 24), 18, NotebookPalette.Navy, true);
            int emptyIndex = weaponIds.Length;
            secondWeapons[emptyIndex] = Button(secondList.Content, "Second None", new Rect(emptyIndex % 4 * 123, emptyIndex / 4 * 38, 115, 32), UserText.Message("copy.e12d011cadf1b6f4"), () => SetDraft(Draft.ChassisId, Draft.FirstWeaponId, null, Draft.ModuleSelections));
            secondWeapons[emptyIndex].GetComponentInChildren<Text>().fontSize = 13;
            thirdWeapons[emptyIndex] = Button(thirdList.Content, "Third None", new Rect(emptyIndex % 4 * 123, emptyIndex / 4 * 38, 115, 32), UserText.Message("copy.e12d011cadf1b6f4"), () => SetWeaponMount(2, null));
            thirdWeapons[emptyIndex].GetComponentInChildren<Text>().fontSize = 13;
            armorSlotsLabel = Label(left, "", new Rect(18, 412, 116, 25), 14, NotebookPalette.Navy, true);
            upgradeSlotsLabel = Label(left, "", new Rect(147, 412, 360, 25), 14, NotebookPalette.Navy, true);
            int armorCount = 0, upgradeCount = 0;
            foreach (string id in moduleIds) if (run.Content.GetModule(id).SlotKind == ModuleSlotKind.Armor) armorCount++; else upgradeCount++;
            var armorList = new CatalogScrollList(left, "Armor catalogue", new Rect(18, 445, 116, 36), armorCount, 1, 33, 6);
            var modulesList = new CatalogScrollList(left, "Module catalogue", new Rect(147, 445, 360, 36), upgradeCount, 3, 33, 6);
            int armorIndex = 0, upgradeIndex = 0;
            for (int i = 0; i < moduleIds.Length; i++)
            {
                string id = moduleIds[i]; var part = run.Content.GetModule(id);
                float width = part.Repeatable ? 85 : 115;
                bool armor = part.SlotKind == ModuleSlotKind.Armor;
                int index = armor ? armorIndex++ : upgradeIndex++;
                var moduleParent = armor ? armorList.Content : modulesList.Content;
                int columns = armor ? 1 : 3;
                moduleButtons[i] = Button(moduleParent, "Module " + CatalogButtonName(part), new Rect(index % columns * 123, index / columns * 39, width, 33), part.ButtonLabel, () => ToggleModule(id));
                moduleButtons[i].GetComponentInChildren<Text>().fontSize = 12;
                NotebookArtGraphic.Attach(moduleButtons[i], id);
                if (part.Repeatable) Button(moduleParent, "Remove module " + id, new Rect(index % columns * 123 + 88, index / columns * 39, 27, 33), UserText.Message("common.remove_symbol"), () => RemoveModule(id));
            }
            Button(left, "New blueprint", new Rect(18, 487, 140, 43), UserText.Message("copy.c1e5de3d705e623d"), NewDraft);
            Button(left, "Save blueprint", new Rect(170, 487, 200, 43), UserText.Message("copy.28760c1a5ec2d81f"), SaveDraft);
            Button(left, "Delete blueprint", new Rect(382, 487, 126, 43), UserText.Message("copy.1669c94226115554"), () => { CloseGeometryEditors(); SetStatus(run.DeleteBlueprint(Draft.Id, out var reason) ? UserText.Message("copy.5212a5c55afa3a20") : reason); });
            Button(left, "Save blueprint as new", new Rect(18, 537, 490, 27), UserText.Message("copy.ec1a51e19086049c"), SaveAsNew);

            var center = Panel(root, "Live drawing", new Rect(578, 126, 475, 570), new Color(.97f, .97f, .91f));
            Label(center, UserText.Message("copy.e67c0e47d45131bf"), new Rect(18, 10, 440, 35), 24, NotebookPalette.Blue, true);
            var illustration = new GameObject("Live blueprint sketch", typeof(RectTransform), typeof(CanvasRenderer), typeof(BlueprintSketch));
            illustration.transform.SetParent(center, false); Place(illustration.GetComponent<RectTransform>(), new Rect(30, 58, 415, 191));
            sketch = illustration.GetComponent<BlueprintSketch>(); sketch.raycastTarget = false;
            Button(center, "Open drawing editor", new Rect(22, 251, 432, 42), UserText.Message("copy.d7e0dfdd628b75da"), () =>
            {
                CloseGeometryEditors();
                if (DrawingTemplateCatalog.Create(Draft).Count == 0) { SetStatus(UserText.Message("copy.b86b24244e915380")); return; }
                DrawingEditor.Open(Draft);
            });
            for (int mount = 0; mount < 3; mount++)
            { int selected = mount; footprintButtons[mount] = Button(center, "Mortar footprint " + mount, new Rect(22 + mount * 146, 301, 140, 53), UserText.Message("copy.b6caeb9a0b784643"), () => OpenMountGeometry(selected)); }
            stats = Label(center, "", new Rect(22, 362, 432, 116), 17, NotebookPalette.Navy);
            reinforcementButton=Button(center,"Open reinforcement",new Rect(22,483,432,34),UserText.Message("reinforce.open"),OpenReinforcement);
            Button(center, "Open trial", new Rect(22, 523, 432, 39), UserText.Message("copy.65e901944fe5438f"), () => { SetTrial(true); RefreshTrialSettings(); });
        }

        void BuildAlbum()
        {
            var album = Panel(root, "Blueprint album", new Rect(1071, 126, 500, 570), new Color(1, .98f, .86f));
            Label(album, UserText.Message("copy.c2e65d663e93fb11"), new Rect(16, 10, 465, 35), 24, NotebookPalette.Blue, true);
            Label(album, UserText.Message("copy.3c36b01d56a8257a"), new Rect(18, 47, 460, 27), 17, NotebookPalette.Navy);
            for (int i = 0; i < albumButtons.Length; i++)
            {
                int index = i;
                albumButtons[i] = Button(album, "Album drawing " + i, new Rect(18, 82 + i * 43, 460, 41), "", () => SelectAlbum(albumPage * AlbumRowsPerPage + index), out albumLabels[i]);
                var thumb = new GameObject("Album thumbnail " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(BlueprintSketch));
                thumb.transform.SetParent(albumButtons[i].transform, false); Place(thumb.GetComponent<RectTransform>(), new Rect(4, 1, 48, 35));
                albumSketches[i] = thumb.GetComponent<BlueprintSketch>(); albumSketches[i].raycastTarget = false;
                Place(albumLabels[i].rectTransform, new Rect(58, 1, 394, 39)); albumLabels[i].fontSize = 13;
            }
            Button(album, "Album previous", new Rect(18, 435, 224, 33), UserText.Message("copy.ec67fe18caaef874"), () => { CloseGeometryEditors(); albumPage = Mathf.Max(0, albumPage - 1); Refresh(); });
            Button(album, "Album next", new Rect(254, 435, 224, 33), UserText.Message("copy.689b295f49461225"), () => { CloseGeometryEditors(); albumPage = Mathf.Min(Mathf.Max(0, (run.Workshop.Album.Count - 1) / AlbumRowsPerPage), albumPage + 1); Refresh(); });
            var unlockList = new CatalogScrollList(album, "Unlock catalogue", new Rect(18, 483, 460, 78), partIds.Length, 4, 35, 4);
            for (int i = 0; i < partIds.Length; i++)
            {
                int index = i;
                partButtons[i] = Button(unlockList.Content, "Unlock " + partIds[i], new Rect(i % 4 * 117, i / 4 * 39, 109, 35), "", () => SetStatus(run.UnlockPart(partIds[index], out var reason) ? UserText.Message("copy.fca0caaef1b0ba96") : reason), out partLabels[i]);
                Place(partLabels[i].rectTransform, new Rect(1, 1, 107, 33));
                partLabels[i].fontSize = 11;
            }
        }

        void BuildLoadout()
        {
            Label(root, UserText.Message("copy.488d9b0c997f1609"), new Rect(33, 708, 1528, 33), 23, NotebookPalette.Blue, true);
            for (int i = 0; i < slotLabels.Length; i++)
            {
                int slot = i;
                Button(root, "Assign slot " + i, new Rect(32 + i * 306, 748, 294, 74), "", () => SetStatus(run.AssignBlueprint(slot, Draft.Id, out var reason) ? UserText.Message("copy.70dd786b46cf9291", (slot + 1)) : reason), out slotLabels[i]);
            }
            Button(root, "Start run", new Rect(1262, 748, 307, 74), UserText.Message("copy.bf1131aabeef6a41"), () => {
                if (StartRequested != null) StartRequested();
                else SetStatus(run.StartRun(out var reason) ? UserText.Message("copy.3eade8b0825d164d") : reason);
            });
            var diagram = new GameObject("Pre-run map", typeof(RectTransform), typeof(CanvasRenderer), typeof(WorkshopMapSketch));
            diagram.transform.SetParent(root, false); Place(diagram.GetComponent<RectTransform>(), new Rect(35, 839, 295, 80));
            map = diagram.GetComponent<WorkshopMapSketch>(); map.raycastTarget = false;
            briefing = Label(root, "", new Rect(352, 839, 1208, 68), 17, NotebookPalette.Navy);
            profileSaveStatus = Label(root, "", new Rect(35, 909, 1205, 42), 15, NotebookPalette.Navy);
            profileSaveStatus.name = "Profile save status";
            status = Label(root, "", new Rect(35, 953, 1525, 35), 17, NotebookPalette.Blue, false, TextAnchor.MiddleLeft);
            retryProfileSave = Button(root, "Retry profile save", new Rect(1262, 909, 307, 42), UserText.Message("copy.936aa189718b6d93"), () =>
                SetStatus(run.RetrySave(out var reason) ? reason : UserText.Message("copy.0525c85d076db630", reason)));
        }

        void BuildTrial()
        {
            trialShade = Panel(root, "Trial background", new Rect(0, 0, 1600, 1000), new Color(.17f, .22f, .29f, .18f)).gameObject;
            trialShade.GetComponent<Image>().raycastTarget = true;
            trialPanel = Panel(root, "Test sheet", new Rect(240, 100, 1120, 800), NotebookPalette.Paper).gameObject;
            trialPanel.GetComponent<Image>().raycastTarget = true;
            Label(trialPanel.transform, UserText.Message("copy.70ddf1bc82ac3b25"), new Rect(28, 20, 1060, 46), 30, NotebookPalette.Blue, true);
            trialConditions = Label(trialPanel.transform, "", new Rect(30, 81, 1060, 54), 17, NotebookPalette.Navy);
            foreach (var scenario in run.Session.Balance.Trials.Scenarios)
            { var selected = scenario.Scenario; int i = (int)selected; trialScenarioButtons[i] = Button(trialPanel.transform, "Trial " + i, new Rect(30 + i * 204, 145, 191, 48), Resolve(scenario.Name).ToUpperInvariant(), () => { trialScenario = selected; RefreshTrialSettings(); }); }
            Button(trialPanel.transform, "Counterfire", new Rect(858, 145, 230, 48), UserText.Message("copy.9d66625ddee13b15"), () => { counterfire = !counterfire; RefreshTrialSettings(); });
            var budgets = run.Session.Balance.Trials.BudgetChoices;
            var budgetList = new CatalogScrollList(trialPanel.transform, "Trial budgets", new Rect(30, 209, 560, 42), budgets.Count, 3, 42, 5);
            for (int i = 0; i < budgets.Count; i++)
            { int budget = budgets[i]; Button(budgetList.Content, "Trial budget " + budget, new Rect(i % 3 * 188, i / 3 * 47, 175, 42), UserText.Message("copy.386e5ceae5aa3494", budget), () => { trialBudget = budget; RefreshTrialSettings(); }); }
            trialSettings = Label(trialPanel.transform, "", new Rect(30, 270, 1058, 50), 21, NotebookPalette.Navy, true);
            trialPlayback = Label(trialPanel.transform, UserText.Message("copy.3f04ed6c12dba0c3"), new Rect(30, 314, 1058, 27), 17, NotebookPalette.Blue);
            trialDiagramFirst = LaneDiagram(trialPanel.transform, "Blueprint test lane", new Rect(30, 347, 505, 153));
            trialDiagramSecond = LaneDiagram(trialPanel.transform, "Budget test lane", new Rect(582, 347, 505, 153));
            trialFirstHeader = Label(trialPanel.transform, UserText.Message("copy.d5f1a2a73fcf51d5"), new Rect(403, 517, 310, 29), 18, NotebookPalette.Blue, true, TextAnchor.MiddleCenter);
            trialSecondHeader = Label(trialPanel.transform, UserText.Message("copy.0d0e3e111b1f0f24"), new Rect(740, 517, 348, 29), 18, NotebookPalette.Blue, true, TextAnchor.MiddleCenter);
            trialResults = Label(trialPanel.transform, UserText.Message("copy.8f02821e075dc0d1"), new Rect(30, 552, 354, 166), 15, NotebookPalette.Navy);
            trialFirst = Label(trialPanel.transform, "", new Rect(403, 552, 310, 166), 15, NotebookPalette.Navy, false, TextAnchor.UpperCenter);
            trialSecond = Label(trialPanel.transform, "", new Rect(740, 552, 348, 166), 15, NotebookPalette.Navy, false, TextAnchor.UpperCenter);
            Button(trialPanel.transform, "Run trial", new Rect(30, 724, 503, 55), UserText.Message("copy.5321cfaaab8a3477"), () => RunTest());
            Button(trialPanel.transform, "Close trial", new Rect(557, 724, 531, 55), UserText.Message("copy.071e234de8bba5a1"), () => SetTrial(false));
            SetTrial(false);
        }
        void SaveDraft()
        {
            CloseGeometryEditors();
            Draft = Draft.WithName(title.text);
            SetStatus(run.SaveBlueprint(Draft, out var reason) ? UserText.Message("copy.e048f17356f3c1ea", Draft.Name) : reason);
        }
        void SaveAsNew()
        {
            CloseGeometryEditors();
            var candidate = Draft.AsVariant("drawing-" + Guid.NewGuid().ToString("N"), title.text);
            if (!run.SaveBlueprint(candidate, out var reason)) { SetStatus(reason); return; }
            Draft = candidate; SetStatus(UserText.Message("copy.4c7b8b8a912d4e2b"));
        }
        void SelectAlbum(int index)
        {
            if (index < 0 || index >= run.Workshop.Album.Count) return;
            CloseGeometryEditors();
            Draft = run.Workshop.Album[index]; title.SetTextWithoutNotify(Resolve(Draft.Name));
            SetStatus(UserText.Message("copy.fd2246577ecd4462", Draft.Name));
        }
        void Refresh()
        {
            if(Guided!=null){Guided.Refresh();return;}
            if (Mode == WorkshopHudMode.Full) { RefreshFull(); return; }
            if (run == null || Draft == null) return;
            if (Mode == WorkshopHudMode.Assembly) RefreshAssemblySelections();
            if (Mode == WorkshopHudMode.Drawing) RefreshFootprints();
            if (Mode == WorkshopHudMode.Profile) { RefreshAlbumRows(); RefreshSaveStatus(); }
            RefreshFocusedPreview();
            if (status != null) status.text = Resolve(Status);
            if (trialPanel != null && trialPanel.activeSelf && trialDiagramFirst != null && trialDiagramFirst.HasRecording)
                trialPlayback.text = Resolve(UserText.Message("copy.5b5aabf711c8afb0", trialDiagramFirst.PlaybackSeconds));
        }
        void RefreshAssemblySelections()
        {
            for (int i = 0; i < chassisButtons.Length; i++)
            {
                var definition = run.Content.GetChassis(chassisIds[i]);
                chassisButtons[i].GetComponentInChildren<Text>().text = Resolve(UserText.Message("workshop.chassis_button", definition.ButtonLabel, definition.Capacity));
                Choice(chassisButtons[i], Draft.ChassisId == chassisIds[i]);
            }
            for (int i = 0; i < weaponIds.Length; i++)
            {
                Choice(firstWeapons[i], Draft.GetWeaponId(0) == weaponIds[i]);
                Choice(secondWeapons[i], Draft.GetWeaponId(1) == weaponIds[i]);
                Choice(thirdWeapons[i], Draft.GetWeaponId(2) == weaponIds[i]);
            }
            int armorUsed = 0, upgradesUsed = 0;
            foreach (var module in Draft.ModuleSelections)
                if (run.Content.TryGetModule(module.DefinitionId, out var part)) { if (part.SlotKind == ModuleSlotKind.Armor) armorUsed++; else upgradesUsed++; }
            if (run.Content.TryGetChassis(Draft.ChassisId, out var selectedChassis))
            {
                armorSlotsLabel.text = Resolve(UserText.Message("workshop.armor_slots", armorUsed, selectedChassis.ArmorSlotCount));
                upgradeSlotsLabel.text = Resolve(UserText.Message("workshop.upgrade_slots", upgradesUsed, selectedChassis.UpgradeSlotCount));
                for (int i = 0; i < weaponIds.Length; i++) { secondWeapons[i].interactable = selectedChassis.MountCount >= 2; thirdWeapons[i].interactable = selectedChassis.MountCount >= 3; }
            }
            for (int i = 0; i < moduleIds.Length; i++)
            {
                var definition = run.Content.GetModule(moduleIds[i]);
                moduleButtons[i].GetComponentInChildren<Text>().text = Resolve(UserText.Message("workshop.module_button", definition.ButtonLabel, definition.Load));
                Choice(moduleButtons[i], SelectedModule(moduleIds[i]));
            }
            Choice(secondWeapons[weaponIds.Length], Draft.GetWeaponId(1) == null);
            Choice(thirdWeapons[weaponIds.Length], Draft.GetWeaponId(2) == null);
        }
        void CloseGeometryEditors(){MortarFootprintEditor?.Close();FanVolleyEditor?.Close();ReinforcementEditor?.Close();}
        void OpenReinforcement()
        {
            CloseGeometryEditors();
            if(!Draft.HasArmor){SetStatus(UserText.Message("reinforce.need_armor"));return;}
            ReinforcementEditor.Open(Draft);
        }
        void OpenMountGeometry(int mount)
        {
            CloseGeometryEditors();
            if(Draft.SupportsVolley(mount))FanVolleyEditor.Open(Draft,mount);else MortarFootprintEditor.Open(Draft,mount);
        }
        void RefreshFootprints()
        {
            for (int mount = 0; mount < footprintButtons.Length; mount++)
            {
                if (footprintButtons[mount] == null) continue;
                string id = Draft.GetWeaponId(mount);
                bool mortar = id != null && run.Content.TryGetWeapon(id, out var weapon) && (weapon.SupportsFootprint||weapon.SupportsVolley);
                footprintButtons[mount].gameObject.SetActive(mortar);
                if (mortar) footprintButtons[mount].GetComponentInChildren<Text>().text = Resolve(Draft.SupportsVolley(mount)?UserText.Message("volley.mount",mount+1):Draft.GetMortarFootprint(mount).IsContour ? UserText.Message("imprint.mount_label", mount + 1) : UserText.Message("copy.65784a4e3ffc4d05", mount + 1, Draft.GetMortarFootprint(mount).AspectRatio));
            }
        }
        void RefreshFocusedPreview()
        {
            var balance = run.Session.Balance;
            var preview = run.PreviewBlueprint(Draft, MaterialTier.Pencil);
            int deficit = Mathf.Max(0, preview.Load - preview.Capacity);
            stats.text = Resolve(UserText.Message("copy.154e011c15594a63", preview.Load, preview.Capacity, deficit > 0 ? UserText.Message("copy.b60d6857f04f0a4f", deficit) : "",
                preview.Cost, preview.Health, preview.Defense * 100, preview.BuildSeconds, preview.Range, preview.FireRate, preview.FeedMultiplier * 100,
                balance.Mechanics.ColorCartridges ? UserText.Message("copy.8e013f7d4704e587", preview.ColorCost, balance.LimitTowerColor ? UserText.Message("copy.79f1620b4fb8d375", balance.ColorTargetsPerWindow, balance.ColorWindowSeconds) : "") : "",
                preview.Valid ? "" : "\n" + preview.Reason));
            stats.color = preview.Valid ? NotebookPalette.Navy : NotebookPalette.Red;
            sketch.SetBlueprint(Draft);
        }
        void RefreshAlbumRows()
        {
            albumPage = Mathf.Clamp(albumPage, 0, Mathf.Max(0, (run.Workshop.Album.Count - 1) / AlbumRowsPerPage));
            for (int i = 0; i < albumButtons.Length; i++)
            {
                int index = albumPage * AlbumRowsPerPage + i;
                albumButtons[i].gameObject.SetActive(index < run.Workshop.Album.Count);
                if (index >= run.Workshop.Album.Count) continue;
                var bp = run.Workshop.Album[index];
                var preview = run.PreviewBlueprint(bp);
                albumLabels[i].text = Resolve(UserText.Message("copy.2de8fa26b46862ac", bp.Name, bp.Origin == DrawingOrigin.LegacyStandard ? UserText.Message("copy.2123de7cfc728c2c") : UserText.Message("freeform.authored"), preview.Cost, WeaponSummary(bp), "", preview.Health, preview.FireRate));
                albumSketches[i].SetBlueprint(bp);
                Choice(albumButtons[i], bp.Id == Draft.Id);
            }
        }
        void RefreshFull()
        {
            if (run == null || Draft == null || gold == null) return;
            RefreshFootprints();
            gold.text = Resolve(UserText.Message("copy.dfbc594044dd606d", run.Workshop.Gold));
            albumPage = Mathf.Clamp(albumPage, 0, Mathf.Max(0, (run.Workshop.Album.Count - 1) / AlbumRowsPerPage));
            var balance = run.Session.Balance;
            var mechanics = balance.Mechanics;
            trialScenarioButtons[(int)TrialScenario.Blots].gameObject.SetActive(mechanics.CorpseBlots);
            trialScenarioButtons[(int)TrialScenario.Armored].GetComponentInChildren<Text>().text = Resolve(mechanics.EnemyDrawingTiers ? Resolve(balance.Trials.For(TrialScenario.Armored).Name).ToUpperInvariant() : UserText.Message("copy.c8839f3bfabe7fc2"));
            if (!mechanics.CorpseBlots && trialScenario == TrialScenario.Blots)
                RefreshTrialSettings();
            for (int i = 0; i < chassisButtons.Length; i++)
            {
                var definition = run.Content.GetChassis(chassisIds[i]);
                chassisButtons[i].GetComponentInChildren<Text>().text = Resolve(UserText.Message("workshop.chassis_button", definition.ButtonLabel, definition.Capacity));
                Choice(chassisButtons[i], Draft.ChassisId == chassisIds[i]);
            }
            for (int i = 0; i < weaponIds.Length; i++)
            {
                Choice(firstWeapons[i], Draft.FirstWeaponId == weaponIds[i]);
                Choice(secondWeapons[i], Draft.SecondWeaponId == weaponIds[i]);
                Choice(thirdWeapons[i], Draft.GetWeaponId(2) == weaponIds[i]);
            }
            int armorUsed = 0, upgradesUsed = 0;
            foreach (var module in Draft.ModuleSelections)
                if (run.Content.TryGetModule(module.DefinitionId, out var part)) { if (part.SlotKind == ModuleSlotKind.Armor) armorUsed++; else upgradesUsed++; }
            if (run.Content.TryGetChassis(Draft.ChassisId, out var selectedChassis))
            {
                armorSlotsLabel.text = Resolve(UserText.Message("workshop.armor_slots", armorUsed, selectedChassis.ArmorSlotCount));
                upgradeSlotsLabel.text = Resolve(UserText.Message("workshop.upgrade_slots", upgradesUsed, selectedChassis.UpgradeSlotCount));
                for (int i = 0; i < weaponIds.Length; i++) { secondWeapons[i].interactable = selectedChassis.MountCount >= 2; thirdWeapons[i].interactable = selectedChassis.MountCount >= 3; }
            }
            for (int i = 0; i < moduleIds.Length; i++)
            {
                var definition = run.Content.GetModule(moduleIds[i]);
                moduleButtons[i].GetComponentInChildren<Text>().text = Resolve(UserText.Message("workshop.module_button", definition.ButtonLabel, definition.Load));
                Choice(moduleButtons[i], SelectedModule(moduleIds[i]));
            }
            Choice(secondWeapons[weaponIds.Length], Draft.SecondWeaponId == null);
            Choice(thirdWeapons[weaponIds.Length], Draft.GetWeaponId(2) == null);
            var preview = run.PreviewBlueprint(Draft, MaterialTier.Pencil);
            int deficit = Mathf.Max(0, preview.Load - preview.Capacity);
            stats.text = Resolve(UserText.Message("copy.154e011c15594a63", preview.Load, preview.Capacity, (deficit > 0 ? UserText.Message("copy.b60d6857f04f0a4f", deficit) : ""), preview.Cost, preview.Health, (preview.Defense * 100), preview.BuildSeconds, preview.Range, preview.FireRate, (preview.FeedMultiplier * 100), (mechanics.ColorCartridges ? UserText.Message("copy.8e013f7d4704e587", preview.ColorCost, (balance.LimitTowerColor ?
                    UserText.Message("copy.79f1620b4fb8d375", balance.ColorTargetsPerWindow, balance.ColorWindowSeconds) : "")) : ""), (preview.Valid ? "" : "\n" + preview.Reason)));
            stats.color = preview.Valid ? NotebookPalette.Navy : NotebookPalette.Red;
            sketch.SetBlueprint(Draft);
            status.text = Resolve(Status);
            RefreshSaveStatus();
            if (trialPanel.activeSelf && trialDiagramFirst.HasRecording)
                trialPlayback.text = Resolve(UserText.Message("copy.5b5aabf711c8afb0", trialDiagramFirst.PlaybackSeconds));
            for (int i = 0; i < albumButtons.Length; i++)
            {
                int index = albumPage * AlbumRowsPerPage + i;
                albumButtons[i].gameObject.SetActive(index < run.Workshop.Album.Count);
                if (index >= run.Workshop.Album.Count) continue;
                var bp = run.Workshop.Album[index];
                var albumPreview = run.PreviewBlueprint(bp);
                albumLabels[i].text = Resolve(UserText.Message("copy.2de8fa26b46862ac", bp.Name, (bp.Origin == DrawingOrigin.LegacyStandard ? UserText.Message("copy.2123de7cfc728c2c") : UserText.Message("freeform.authored")), albumPreview.Cost, WeaponSummary(bp), "", albumPreview.Health, albumPreview.FireRate));
                albumLabels[i].fontSize = 13;
                albumSketches[i].SetBlueprint(bp);
                albumButtons[i].GetComponent<Image>().color = bp.Id == Draft.Id ? new Color(.8f, .87f, .94f) : Color.white;
            }
            var loadout = run.Workshop.Loadout;
            for (int i = 0; i < slotLabels.Length; i++)
            {
                slotLabels[i].text = Resolve(UserText.Message("copy.62773216378bce38", (i + 1), (i < loadout.Count && loadout[i] != null ? loadout[i].Name : UserText.Message("copy.67dc5403f9c82c31"))));
            }
            for (int i = 0; i < partIds.Length; i++)
            {
                bool unlocked = run.Workshop.IsUnlocked(partIds[i]);
                run.Content.TryGet(partIds[i], out var definition);
                string name = definition.UnlockLabel;
                int price = 0;
                foreach (var part in run.Workshop.Parts) if (part.Id == partIds[i]) price = part.GoldCost;
                partLabels[i].text = Resolve(UserText.Message("workshop.unlock_button", name, unlocked ? UserText.Message("copy.86417e741b0e18e4") : UserText.Message("copy.964f2bea7f75b5b6", price)));
                partButtons[i].interactable = !unlocked;
            }
            string wells = "";
            if (run.Session != null)
                for (int i = 0; i < run.Session.Wells.Count; i++)
                { var well = run.Session.Wells[i]; wells += (i > 0 ? "   ·   " : "") + ColorName(well.Color) + " " + well.Cell; }
            string threat = run.Session != null && run.Session.Balance.Waves.Count > 0 ? run.Session.Balance.Waves[0].Title : UserText.Message("copy.b5974db5e807bfce");
            map.SetSession(run.Session);
            bool runners=false,gnawers=false,spitters=false,brutes=false,bulwarks=false;
            foreach(var wave in run.Session.Balance.Waves)
            {runners|=wave.RunnerFraction>0;gnawers|=wave.GnawerFraction>0;spitters|=mechanics.RangedEnemies&&wave.SpitterFraction>0;brutes|=wave.BruteFraction>0;bulwarks|=wave.BulwarkFraction>0;}
            string enemies=UserText.Message("copy.8834fdb6964a6838", (runners?UserText.Message("copy.84d971fcad33df9e"):""), (gnawers?UserText.Message("copy.ec0efac73a0bc3ab"):""), (spitters?UserText.Message("copy.9aa4314a8e7e2cf4"):""));
            if (brutes || bulwarks) enemies = UserText.Message("hud.enemy_roster", enemies,
                brutes ? UserText.Message("art.enemy.brute") : "", bulwarks ? UserText.Message("art.enemy.bulwark") : "");
            briefing.text = Resolve(UserText.Message("copy.851980894ad17adb", balance.Width, balance.Height, (mechanics.InkWells ? UserText.Message("copy.bd969a1552a7f8b0", wells) : ""), enemies, (spitters ? UserText.Message("copy.9f16585ff7173d3c") : ""), (run.Session.Balance.Preset == GameBalancePreset.Target ?
                    UserText.Message("copy.9d7e64acad5dda4e", balance.StartingInk, (mechanics.InkWells ? UserText.Message("copy.01bea534f0c9cb02") : "")) :
                    UserText.Message("copy.4f11dbcf3d576c90", threat, (mechanics.InkWells ? UserText.Message("copy.012fc5ef84805c4b") : "")))));
        }
        void RefreshSaveStatus()
        {
            if (profileSaveStatus == null || retryProfileSave == null) return;
            var profile = run.Workshop;
            bool failed = profile.SaveState == ProfileSaveState.WriteFailed;
            bool protectedSource = profile.SaveState == ProfileSaveState.RecoveryRequired;
            string scope = profile.SaveState == ProfileSaveState.MemoryOnly ? UserText.Message("copy.53121a816b260323") :
                protectedSource ? UserText.Message("copy.91cbbe4d8a4fb25d") : failed ? UserText.Message("copy.268d7bcfe051e702") :
                profile.HasUnsavedChanges ? UserText.Message("copy.d1770b2fff113f11") : UserText.Message("copy.72734ce7eccce5f3");
            string detail = profile.SaveMessage;
            if (profile.HasUnsavedChanges) detail += UserText.Message("copy.48e94b4f89c58cc7");
            profileSaveStatus.text = Resolve(UserText.Message("workshop.save_status", scope, detail));
            profileSaveStatus.color = failed || protectedSource ? NotebookPalette.Red : NotebookPalette.Navy;
            // Failed manual commands remain on their original buttons; retry never replays a purchase.
            retryProfileSave.gameObject.SetActive(profile.HasUnsavedChanges && !protectedSource);
        }
        void SetTrial(bool visible) { if (trialShade != null) trialShade.SetActive(visible); if (trialPanel != null) trialPanel.SetActive(visible); }
        void RefreshTrialSettings()
        {
            if (trialPanel == null) return;
            if (Mode == WorkshopHudMode.Drawing)
            {
                trialScenario = TrialScenario.Crowd; counterfire = false;
                trialSettings.text = Resolve(UserText.Message("copy.4c14dc9bbbade9ed", Resolve(run.Session.Balance.Trials.For(trialScenario).Name).ToLowerInvariant(), trialBudget, UserText.Message("copy.73766e6851e92703")));
                trialFirstHeader.text = Resolve(UserText.Message("copy.d5f1a2a73fcf51d5"));
                trialSecondHeader.text = Resolve(UserText.Message("copy.0d0e3e111b1f0f24"));
                ClearTrial();
                return;
            }
            var mechanics = run.Session.Balance.Mechanics;
            if (!mechanics.CorpseBlots && trialScenario == TrialScenario.Blots) trialScenario = TrialScenario.Crowd;
            bool blotTraining = trialScenario == TrialScenario.Blots;
            var rules = run.Session.Balance.Trials;
            string scenarioName = trialScenario == TrialScenario.Armored && !mechanics.EnemyDrawingTiers ? UserText.Message("copy.ae092c38e45a8570") : Resolve(rules.For(trialScenario).Name).ToLowerInvariant();
            trialSettings.text = Resolve(UserText.Message("copy.4c14dc9bbbade9ed", scenarioName, trialBudget, (blotTraining ? UserText.Message("copy.5c541247ef6b3bec") : counterfire ? UserText.Message("copy.6e782106d7934d03") : UserText.Message("copy.73766e6851e92703"))));
            trialConditions.text = Resolve(UserText.Message("copy.3f138ba9b66893d3", (blotTraining ? UserText.Message("copy.ac6f23c50da2aca7", (rules.StepSeconds * rules.MaximumTicks)) :
                    UserText.Message("copy.6c080edef345b261", rules.CounterfireDamage, rules.CounterfireInterval, rules.CounterfireRange))));
            trialFirstHeader.text = Resolve(UserText.Message("copy.d5f1a2a73fcf51d5"));
            trialSecondHeader.text = Resolve(UserText.Message("copy.0d0e3e111b1f0f24"));
            ClearTrial();
        }
        void ClearTrial()
        {
            if (trialDiagramFirst == null || trialDiagramSecond == null) return;
            trialDiagramFirst.SetResult(null, 0, 1); trialDiagramSecond.SetResult(null, 0, 1);
            trialFirst.text = Resolve(trialSecond.text = Resolve("")); trialResults.text = Resolve(UserText.Message("copy.8f02821e075dc0d1"));
            trialPlayback.text = Resolve(UserText.Message("copy.cfad8860edae4ba5"));
        }
        public TrialComparison RunDrawingTrial()
        {
            if(Guided!=null)return Guided.RunTrial();
            if (trialPanel == null || Draft == null) return null;
            CloseGeometryEditors();
            if (title != null) Draft = Draft.WithName(title.text);
            SetTrial(true); RefreshTrialSettings();
            return RunTest();
        }
        TrialComparison RunTest()
        {
            var preview = run.PreviewBlueprint(Draft, MaterialTier.Pencil);
            if (!preview.Valid) { ClearTrial(); trialResults.text = Resolve(preview.Reason); return null; }
            var comparison = run.RunTrial(Draft, trialScenario, counterfire, trialBudget);
            if (comparison == null)
            {
                SetStatus(UserText.Message("copy.b8c85b921b3e435e"));
                ClearTrial(); trialResults.text = Resolve(Status); return null;
            }
            var a = comparison.First; var b = comparison.Second;
            trialResults.text = Resolve(UserText.Message("copy.20aa3fc90aec2cbe"));
            trialFirst.text = Resolve(TrialValues(a)); trialSecond.text = Resolve(TrialValues(b));
            float started = Time.unscaledTime, duration = Mathf.Max(a.DurationSeconds, b.DurationSeconds);
            trialDiagramFirst.SetResult(a, started, duration); trialDiagramSecond.SetResult(b, started, duration);
            return comparison;
        }
        static string TrialValues(TrialReport report) => UserText.Message("workshop.trial_values", report.Copies, report.Cost, report.Damage, report.Leaks,
            report.SurvivingTowers, report.IdleSeconds, report.DurationSeconds, report.UnresolvedEnemies, report.BlotsCleared, report.UnresolvedBlots);
        static void Choice(Button button, bool chosen) => button.GetComponent<Image>().color = chosen ? new Color(.8f, .87f, .94f) : Color.white;
        TrialLaneSketch LaneDiagram(Transform parent, string name, Rect box)
        {
            var diagram = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TrialLaneSketch)); diagram.transform.SetParent(parent, false);
            Place(diagram.GetComponent<RectTransform>(), box); var graphic = diagram.GetComponent<TrialLaneSketch>(); graphic.raycastTarget = false; return graphic;
        }
        internal static string WeaponName(StructureKind kind)
            => PartCatalog.TryGetMount(kind, out var part) ? part.ButtonLabel : kind.ToString().ToUpperInvariant();
        string WeaponName(string id) => run.Content.TryGetWeapon(id, out var part) ? part.ButtonLabel : UserText.Message("copy.28de2c7a9d82e464", id);
        string WeaponSummary(TowerBlueprint blueprint)
        {
            string result = "";
            foreach (var mount in blueprint.WeaponMounts) result = result.Length == 0 ? WeaponName(mount.WeaponId) : UserText.Message("workshop.weapon_pair", result, WeaponName(mount.WeaponId));
            return result;
        }
        internal static string ColorName(InkColor color) => color == InkColor.Red ? UserText.Message("copy.bd395e180a485a0a") : color == InkColor.Blue ? UserText.Message("copy.3b6332e897ab71c4") : color == InkColor.Green ? UserText.Message("copy.05142bc07be813fc") : UserText.Message("copy.715c066d1a65e690");

        RectTransform Panel(Transform parent, string name, Rect box, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); Place(rect, box); var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            var outline = go.AddComponent<Outline>(); outline.effectColor = new Color(.15f, .24f, .35f, .5f); outline.effectDistance = new Vector2(1, -1);
            return rect;
        }
        Text Label(Transform parent, string text, Rect box, int size, Color color, bool bold = false, TextAnchor align = TextAnchor.UpperLeft)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false); Place(go.GetComponent<RectTransform>(), box);
            var label = go.GetComponent<Text>(); label.font = font; label.text = Resolve(text); label.fontSize = size; label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.color = color; label.alignment = align; label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        Button Button(Transform parent, string name, Rect box, string text, Action action) => Button(parent, name, box, text, action, out _);
        Button Button(Transform parent, string name, Rect box, string text, Action action, out Text label)
        {
            var rect = Panel(parent, name, box, Color.white); rect.GetComponent<Image>().raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            button.onClick.AddListener(() => action());
            // NotebookHand's line height exceeds its point size; compact controls need the full vertical space.
            float verticalInset = box.height <= 35 ? 1 : 4;
            label = Label(rect, text, new Rect(6, verticalInset, box.width - 12, box.height - verticalInset * 2),
                box.height <= 30 || box.height > 60 ? 16 : 17, NotebookPalette.Navy, true, TextAnchor.MiddleCenter);
            return button;
        }
        InputField Input(Transform parent, string name, Rect box)
        {
            var rect = Panel(parent, name, box, Color.white); rect.GetComponent<Image>().raycastTarget = true;
            var input = rect.gameObject.AddComponent<InputField>(); input.targetGraphic = rect.GetComponent<Image>(); input.characterLimit = 32;
            input.textComponent = Label(rect, "", new Rect(10, 5, box.width - 20, box.height - 10), 22, NotebookPalette.Navy);
            return input;
        }
        static void Place(RectTransform rect, Rect box)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(box.x, -box.y); rect.sizeDelta = box.size; }
    }

    /// <summary>A live hand-drawn diagram, including both weapon mounts and armor plates.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BlueprintSketch : MaskableGraphic
    {
        TowerBlueprint blueprint;
        NotebookAnimationState previewAnimation;
        Func<int, Point2> previewAim;
        double previewSeconds;
        public void SetPreviewPose(NotebookAnimationState animation, double seconds, Func<int, Point2> aim)
        { previewAnimation = animation; previewSeconds = seconds; previewAim = aim; SetVerticesDirty(); }
        public bool ShowReference { get; set; }
        public bool ShowReinforcement { get; set; } = true;
        public void SetBlueprint(TowerBlueprint value) { if (blueprint == value) return; blueprint = value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (blueprint == null || DrawingTemplateCatalog.Create(blueprint).Count == 0) return;
            Vector2 center = rectTransform.rect.center;
            if(ShowReinforcement&&blueprint.Reinforcement!=null)ArmorReinforcementGraphic.DrawSectors(vh,center,Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.42f,blueprint.Reinforcement);
            if (!ShowReference && (blueprint.HasPlayerDrawing || NotebookTowerComposition.UsesResourceArt(blueprint)))
            {
                float size = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .68f;
                if (previewAnimation != null)
                    NotebookTowerComposition.DrawPreviewUI(vh, blueprint, center, size, previewAnimation, 1, previewSeconds, previewAim);
                else NotebookTowerComposition.DrawUI(vh, blueprint, center, size);
                return;
            }
            if (ShowReference || blueprint.HasPlayerDrawing || ReferenceDrawingCache.UsesTemplateVisual(blueprint))
            {
                float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .68f;
                var drawing = ShowReference ? ReferenceDrawingCache.For(blueprint) : ReferenceDrawingCache.VisibleDrawing(blueprint);
                foreach (var template in DrawingTemplateCatalog.Create(blueprint))
                {
                    var part = drawing.GetPart(template.BindingId);
                    Point2 pivot = template.Pivot;
                    foreach (var stroke in part.Strokes)
                        DrawingCanvasGraphic.Draw(vh, stroke.Points, center + new Vector2(pivot.X, pivot.Y) * scale, scale, NotebookPalette.Blue, .018f);
                }
                return;
            }
            float legacyScale = Mathf.Min(1, Mathf.Min(rectTransform.rect.width / 270, rectTransform.rect.height / 250));
            // Standard pages retain their historical diagram, fitted to both the preview and thumbnail.
            int before = vh.currentVertCount;
            float radius = blueprint.Chassis == ChassisKind.Heavy ? 76 : 65;
            Ring(vh, center, radius, 4, NotebookPalette.Blue);
            Ring(vh, center, radius - 13, 2, NotebookPalette.Blue);
            for (int side = -1; side <= 1; side += 2)
            {
                Line(vh, center + new Vector2(side * radius * .7f, -42), center + new Vector2(side * (radius + 20), -72), 5, NotebookPalette.Navy);
                if ((blueprint.Modules & TowerModules.Armor) != 0)
                { Line(vh, center + new Vector2(side * (radius + 8), -39), center + new Vector2(side * (radius + 8), 40), 13, NotebookPalette.Navy); }
            }
            foreach (var mount in blueprint.WeaponMounts)
            {
                Point2 pivot = blueprint.Content != null && blueprint.Content.TryGetChassis(blueprint.ChassisId, out var chassis)
                    ? chassis.GetMountPivot(mount.SlotIndex, blueprint.WeaponMounts.Count) : new Point2((mount.SlotIndex - (blueprint.WeaponMounts.Count - 1) * .5f) * .28f, 0);
                Weapon(vh, center + new Vector2(pivot.X, pivot.Y) * 190, LegacyContentIds.Weapon(mount.WeaponId));
            }
            if ((blueprint.Modules & TowerModules.Feed) != 0) Ring(vh, center - Vector2.up * 22, 15, 5, NotebookPalette.Green);
            if ((blueprint.Modules & TowerModules.Tracking) != 0)
            { Ring(vh, center + new Vector2(radius + 24, 57), 15, 3, NotebookPalette.Red); Line(vh, center + new Vector2(radius, 26), center + new Vector2(radius + 24, 57), 3, NotebookPalette.Navy); }
            if ((blueprint.Modules & TowerModules.Rail) != 0)
            {
                Line(vh, center + new Vector2(-34, -52), center + new Vector2(34, -52), 4, NotebookPalette.Navy);
                Line(vh, center + new Vector2(-34, -63), center + new Vector2(34, -63), 4, NotebookPalette.Navy);
                for (int i = -2; i <= 2; i++) Line(vh, center + new Vector2(i * 14, -49), center + new Vector2(i * 14, -66), 2, NotebookPalette.Blue);
            }
            UIVertex vertex = new UIVertex();
            for (int i = before; i < vh.currentVertCount; i++)
            { vh.PopulateUIVertex(ref vertex, i); vertex.position = (Vector3)center + (vertex.position - (Vector3)center) * legacyScale; vh.SetUIVertex(vertex, i); }
        }
        static void Weapon(VertexHelper vh, Vector2 origin, StructureKind kind)
        {
            if (DoodleGlyphs.UsesTemplateMount(kind))
            {
                var geometry = DoodleGlyphs.StandardMount(kind);
                int start = vh.currentVertCount;
                foreach (Vector2 vertex in geometry.Vertices) vh.AddVert(origin + vertex * 190, NotebookPalette.Blue, Vector2.zero);
                for (int i = 0; i < geometry.Indices.Count; i += 3)
                    vh.AddTriangle(start + geometry.Indices[i], start + geometry.Indices[i + 1], start + geometry.Indices[i + 2]);
                return;
            }
            float length = kind == StructureKind.Piercer ? 106 : kind == StructureKind.Mortar ? 49 : 83;
            float width = kind == StructureKind.Mortar ? 24 : kind == StructureKind.Piercer ? 12 : 17;
            Vector2 end = origin + Vector2.up * length;
            Line(vh, origin, end, width + 7, NotebookPalette.Navy); Line(vh, origin, end, width, NotebookPalette.Blue);
            Line(vh, end + Vector2.left * (width + 4), end + Vector2.right * (width + 4), 6, NotebookPalette.Navy);
        }
        internal static void Ring(VertexHelper vh, Vector2 center, float radius, float width, Color color)
        { for (int i = 0; i < 32; i++) { float a = i * Mathf.PI / 16, b = (i + 1) * Mathf.PI / 16; Line(vh, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, color); } }
        internal static void Line(VertexHelper vh, Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 direction = to - from; if (direction.sqrMagnitude < .001f) return;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * width * .5f;
            int start = vh.currentVertCount;
            vh.AddVert(from - normal, color, Vector2.zero); vh.AddVert(to - normal, color, Vector2.zero);
            vh.AddVert(to + normal, color, Vector2.zero); vh.AddVert(from + normal, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkshopMapSketch : MaskableGraphic
    {
        IRunReadModel session;
        public void SetSession(IRunReadModel value) { if (session == value) return; session = value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (session == null) return;
            Rect rect = rectTransform.rect; Vector2 low = rect.min + Vector2.one * 8, high = rect.max - Vector2.one * 8;
            BlueprintSketch.Line(vh, low, new Vector2(high.x, low.y), 2, NotebookPalette.Navy);
            BlueprintSketch.Line(vh, low, new Vector2(low.x, high.y), 2, NotebookPalette.Navy);
            BlueprintSketch.Line(vh, high, new Vector2(high.x, low.y), 2, NotebookPalette.Navy);
            BlueprintSketch.Line(vh, high, new Vector2(low.x, high.y), 2, NotebookPalette.Navy);
            var battlefield = session.Battlefield;
            if (battlefield != null)
            {
                Vector2 cellSize = new Vector2((high.x - low.x) / session.Balance.Width, (high.y - low.y) / session.Balance.Height);
                foreach (var prop in battlefield.Props)
                {
                    Vector2 at = Point(prop.Cell, low, high) - cellSize * .5f;
                    Vector2 size = Vector2.Scale(cellSize, new Vector2(prop.Width, prop.Height));
                    int start = vh.currentVertCount;
                    Color tint = MeshPen.Alpha(prop.Kind == TerrainPropKind.Mountain || prop.Kind == TerrainPropKind.Rock ? NotebookPalette.Pencil : NotebookPalette.Green, .22f);
                    vh.AddVert(at, tint, Vector2.zero); vh.AddVert(at + Vector2.right * size.x, tint, Vector2.zero);
                    vh.AddVert(at + size, tint, Vector2.zero); vh.AddVert(at + Vector2.up * size.y, tint, Vector2.zero);
                    vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
                }
                foreach (var cell in battlefield.Roads)
                {
                    Vector2 at = Point(cell, low, high);
                    var east = new Cell(cell.X + 1, cell.Y); var north = new Cell(cell.X, cell.Y + 1);
                    if (battlefield.IsRoad(east)) BlueprintSketch.Line(vh, at, Point(east, low, high), 4, NotebookPalette.Pencil);
                    if (battlefield.IsRoad(north)) BlueprintSketch.Line(vh, at, Point(north, low, high), 4, NotebookPalette.Pencil);
                }
                foreach (var cell in battlefield.BarricadeSites)
                    BlueprintSketch.Ring(vh, Point(cell, low, high), 3, 1.5f, NotebookPalette.Blue);
                foreach (var cell in battlefield.Entrances)
                    Arrow(vh, Point(cell, low, high), cell.X == 0 ? Vector2.right : cell.X == session.Balance.Width - 1 ? Vector2.left : cell.Y == 0 ? Vector2.up : Vector2.down);
            }
            Vector2 home = Point(session.BaseCell, low, high);
            BlueprintSketch.Ring(vh, home, 7, 4, NotebookPalette.Navy);
            for (int i = 0; i < session.Wells.Count; i++)
            { var well = session.Wells[i]; BlueprintSketch.Ring(vh, Point(well.Cell, low, high), 5, 4, DoodleGlyphs.InkTone(well.Color)); }
            int sides = battlefield != null ? 0 : session.Balance.Waves.Count > 0 ? session.Balance.Waves[0].Sides : 1;
            if ((sides & 1) != 0) Arrow(vh, new Vector2(low.x + 2, home.y), Vector2.right);
            if ((sides & 2) != 0) Arrow(vh, new Vector2(high.x - 2, home.y), Vector2.left);
            if ((sides & 4) != 0) Arrow(vh, new Vector2(home.x, high.y - 2), Vector2.down);
            if ((sides & 8) != 0) Arrow(vh, new Vector2(home.x, low.y + 2), Vector2.up);
        }
        Vector2 Point(Cell cell, Vector2 low, Vector2 high) => new Vector2(Mathf.Lerp(low.x, high.x, (cell.X + .5f) / session.Balance.Width), Mathf.Lerp(low.y, high.y, (cell.Y + .5f) / session.Balance.Height));
        static void Arrow(VertexHelper vh, Vector2 at, Vector2 direction)
        {
            Vector2 tip = at + direction * 20, side = new Vector2(-direction.y, direction.x) * 6;
            BlueprintSketch.Line(vh, at, tip, 3, NotebookPalette.Red);
            BlueprintSketch.Line(vh, tip, tip - direction * 8 + side, 3, NotebookPalette.Red);
            BlueprintSketch.Line(vh, tip, tip - direction * 8 - side, 3, NotebookPalette.Red);
        }
    }

}
