using System;
using System.Collections.Generic;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>The album edits immutable drawings; a run receives four frozen choices.</summary>
    public sealed class WorkshopHud : MonoBehaviour
    {
        public UserTextResolver TextResolver { get; set; }
        public bool ReserveStandControls { get; set; }
        string Resolve(string value) => GameText.Resolve(TextResolver, value);
        const int AlbumRowsPerPage = 8;
        RunController run;
        Font font;
        GameObject canvasObject, trialPanel, trialShade, qualityPanel, qualityShade;
        RectTransform root;
        Text gold, stats, status, briefing, trialResults, trialSettings, trialFirst, trialSecond, trialPlayback, trialConditions;
        Text profileSaveStatus;
        Button retryProfileSave;
        Text qualityExplanation, trialFirstHeader, trialSecondHeader, qualityModeLabel, armorSlotsLabel, upgradeSlotsLabel;
        InputField title;
        BlueprintSketch sketch;
        readonly BlueprintSketch[] albumSketches = new BlueprintSketch[AlbumRowsPerPage];
        public DrawingEditorPanel DrawingEditor { get; private set; }
        public MortarFootprintPanel MortarFootprintEditor { get; private set; }
        WorkshopMapSketch map;
        readonly Button[] footprintButtons = new Button[3];
        readonly Button[] trialScenarioButtons = new Button[4];
        Button qualityModeButton;
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
        bool counterfire, qualityTrial;
        TrialScenario trialScenario;
        public TowerBlueprint Draft { get; private set; }
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
            scaler.referenceResolution = new Vector2(1600, ReserveStandControls ? 1200 : 1000); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var board = new GameObject("Workshop paper", typeof(RectTransform), typeof(Image));
            board.transform.SetParent(canvasObject.transform, false); root = board.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f); root.sizeDelta = new Vector2(1600, 1000);
            board.GetComponent<Image>().color = NotebookPalette.Paper; board.GetComponent<Image>().raycastTarget = true;
            BuildComposer(); BuildAlbum(); BuildLoadout(); BuildTrial(); BuildQualityDetails();
            MortarFootprintEditor = new MortarFootprintPanel(root, font, value => { Draft = value; Refresh(); }, run.CompareMortarFootprints, value => run.PreviewBlueprint(value).Reason, TextResolver);
            DrawingEditor = new DrawingEditorPanel(root, font, drawing => { MortarFootprintEditor.Close(); Draft = Draft.WithDrawing(drawing); SetStatus(UserText.Message("copy.65c92ef5c0dfe24a")); }, TextResolver);
            Draft = ReserveStandControls && run.Workshop.Loadout[0] != null
                ? run.Workshop.Loadout[0]
                : run.Workshop.Album.Count > 0 ? run.Workshop.Album[0] : NewDrawing();
            title.SetTextWithoutNotify(Resolve(Draft.Name));
            run.WorkshopChanged += Refresh;
            Refresh();
        }

        void OnDestroy() { if (run != null) run.WorkshopChanged -= Refresh; }
        void Update()
        {
            if (run == null) return;
            canvasObject.SetActive(run.InWorkshop);
            if (!run.InWorkshop) { MortarFootprintEditor.Close(); return; }
            refresh -= Time.unscaledDeltaTime;
            if (refresh <= 0) { refresh = .15f; Refresh(); }
        }

        TowerBlueprint NewDrawing() => new TowerBlueprint("drawing-" + Guid.NewGuid().ToString("N"), Resolve(UserText.Message("copy.74a4cbe8134d0a38")), chassisIds[0], weaponIds[0],
            origin: DrawingOrigin.PlayerDrawing, content: run.Content);
        void NewDraft() { MortarFootprintEditor.Close(); Draft = NewDrawing(); title.SetTextWithoutNotify(Resolve(Draft.Name)); SetStatus(UserText.Message("copy.a2f3120b30cce36d")); }
        void SetDraft(string chassis, string first, string second, IEnumerable<ModuleSelection> modules, string selectedPart = null)
        {
            MortarFootprintEditor.Close();
            Draft = Draft.WithContent(run.Content).WithName(title.text).WithAssembly(chassis, first, second, modules);
            if (selectedPart != null) foreach (var part in run.Workshop.Parts) if (part.Id == selectedPart) { Status = part.Description; break; }
            Refresh();
        }
        void SetWeaponMount(int mount, string weaponId)
        {
            MortarFootprintEditor.Close();
            Draft = Draft.WithContent(run.Content).WithName(title.text).WithWeaponMount(mount, weaponId);
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
                if (part.Repeatable) Button(moduleParent, "Remove module " + id, new Rect(index % columns * 123 + 88, index / columns * 39, 27, 33), UserText.Message("common.remove_symbol"), () => RemoveModule(id));
            }
            Button(left, "New blueprint", new Rect(18, 487, 140, 43), UserText.Message("copy.c1e5de3d705e623d"), NewDraft);
            Button(left, "Save blueprint", new Rect(170, 487, 200, 43), UserText.Message("copy.28760c1a5ec2d81f"), SaveDraft);
            Button(left, "Delete blueprint", new Rect(382, 487, 126, 43), UserText.Message("copy.1669c94226115554"), () => { MortarFootprintEditor.Close(); SetStatus(run.DeleteBlueprint(Draft.Id, out var reason) ? UserText.Message("copy.5212a5c55afa3a20") : reason); });
            Button(left, "Save blueprint as new", new Rect(18, 537, 490, 27), UserText.Message("copy.ec1a51e19086049c"), SaveAsNew);

            var center = Panel(root, "Live drawing", new Rect(578, 126, 475, 570), new Color(.97f, .97f, .91f));
            Label(center, UserText.Message("copy.e67c0e47d45131bf"), new Rect(18, 10, 440, 35), 24, NotebookPalette.Blue, true);
            var illustration = new GameObject("Live blueprint sketch", typeof(RectTransform), typeof(CanvasRenderer), typeof(BlueprintSketch));
            illustration.transform.SetParent(center, false); Place(illustration.GetComponent<RectTransform>(), new Rect(30, 58, 415, 191));
            sketch = illustration.GetComponent<BlueprintSketch>(); sketch.raycastTarget = false;
            Button(center, "Open drawing editor", new Rect(22, 251, 432, 42), UserText.Message("copy.d7e0dfdd628b75da"), () =>
            {
                MortarFootprintEditor.Close();
                if (DrawingTemplateCatalog.Create(Draft).Count == 0) { SetStatus(UserText.Message("copy.b86b24244e915380")); return; }
                DrawingEditor.Open(Draft);
            });
            for (int mount = 0; mount < 3; mount++)
            { int selected = mount; footprintButtons[mount] = Button(center, "Mortar footprint " + mount, new Rect(22 + mount * 146, 301, 140, 53), UserText.Message("copy.b6caeb9a0b784643"), () => MortarFootprintEditor.Open(Draft, selected)); }
            stats = Label(center, "", new Rect(22, 365, 432, 121), 17, NotebookPalette.Navy);
            Button(center, "Drawing quality details", new Rect(22, 487, 432, 30), UserText.Message("copy.43f9d3e836476314"), ShowQualityDetails);
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
            Button(album, "Album previous", new Rect(18, 435, 224, 33), UserText.Message("copy.ec67fe18caaef874"), () => { MortarFootprintEditor.Close(); albumPage = Mathf.Max(0, albumPage - 1); Refresh(); });
            Button(album, "Album next", new Rect(254, 435, 224, 33), UserText.Message("copy.689b295f49461225"), () => { MortarFootprintEditor.Close(); albumPage = Mathf.Min(Mathf.Max(0, (run.Workshop.Album.Count - 1) / AlbumRowsPerPage), albumPage + 1); Refresh(); });
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
            Button(root, "Start run", new Rect(1262, 748, 307, 74), UserText.Message("copy.bf1131aabeef6a41"), () => SetStatus(run.StartRun(out var reason) ? UserText.Message("copy.3eade8b0825d164d") : reason));
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
            qualityModeButton = Button(trialPanel.transform, "Trial quality mode", new Rect(602, 209, 486, 42), "", () => { qualityTrial = !qualityTrial; RefreshTrialSettings(); }, out qualityModeLabel);
            trialSettings = Label(trialPanel.transform, "", new Rect(30, 270, 1058, 50), 21, NotebookPalette.Navy, true);
            trialPlayback = Label(trialPanel.transform, UserText.Message("copy.3f04ed6c12dba0c3"), new Rect(30, 314, 1058, 27), 17, NotebookPalette.Blue);
            trialDiagramFirst = LaneDiagram(trialPanel.transform, "Blueprint test lane", new Rect(30, 347, 505, 153));
            trialDiagramSecond = LaneDiagram(trialPanel.transform, "Reference test lane", new Rect(582, 347, 505, 153));
            trialFirstHeader = Label(trialPanel.transform, UserText.Message("copy.d5f1a2a73fcf51d5"), new Rect(403, 517, 310, 29), 18, NotebookPalette.Blue, true, TextAnchor.MiddleCenter);
            trialSecondHeader = Label(trialPanel.transform, UserText.Message("copy.0d0e3e111b1f0f24"), new Rect(740, 517, 348, 29), 18, NotebookPalette.Blue, true, TextAnchor.MiddleCenter);
            trialResults = Label(trialPanel.transform, UserText.Message("copy.8f02821e075dc0d1"), new Rect(30, 552, 354, 166), 15, NotebookPalette.Navy);
            trialFirst = Label(trialPanel.transform, "", new Rect(403, 552, 310, 166), 15, NotebookPalette.Navy, false, TextAnchor.UpperCenter);
            trialSecond = Label(trialPanel.transform, "", new Rect(740, 552, 348, 166), 15, NotebookPalette.Navy, false, TextAnchor.UpperCenter);
            Button(trialPanel.transform, "Run trial", new Rect(30, 724, 503, 55), UserText.Message("copy.5321cfaaab8a3477"), RunTest);
            Button(trialPanel.transform, "Close trial", new Rect(557, 724, 531, 55), UserText.Message("copy.071e234de8bba5a1"), () => SetTrial(false));
            SetTrial(false);
        }
        void BuildQualityDetails()
        {
            qualityShade = Panel(root, "Drawing quality shade", new Rect(0, 0, 1600, 1000), new Color(.12f, .18f, .28f, .4f)).gameObject;
            qualityShade.GetComponent<Image>().raycastTarget = true;
            qualityPanel = Panel(root, "Drawing quality details panel", new Rect(330, 140, 940, 720), NotebookPalette.Paper).gameObject;
            qualityPanel.GetComponent<Image>().raycastTarget = true;
            Label(qualityPanel.transform, UserText.Message("copy.9981e518516b62e5"), new Rect(28, 20, 884, 48), 30, NotebookPalette.Blue, true);
            qualityExplanation = Label(qualityPanel.transform, "", new Rect(28, 86, 884, 535), 21, NotebookPalette.Navy);
            qualityExplanation.name = "Drawing quality explanation";
            Button(qualityPanel.transform, "Close quality details", new Rect(28, 644, 884, 48), UserText.Message("copy.071e234de8bba5a1"), () => { qualityPanel.SetActive(false); qualityShade.SetActive(false); });
            qualityPanel.SetActive(false); qualityShade.SetActive(false);
        }
        void ShowQualityDetails()
        {
            var assessment = Draft.Assessment;
            var preview = run.PreviewBlueprint(Draft);
            string text = UserText.Message("copy.c524c57d052a0a88", assessment.Quality, (preview.QualityMultiplier * 100));
            text += UserText.Message("copy.ba96f2ca52eddb03");
            if (Draft.Origin == DrawingOrigin.LegacyStandard) text += UserText.Message("copy.d1ae9c8debd10e79");
            foreach (var part in assessment.PartFeedback)
                text += UserText.Message("workshop.part_quality", DrawingEditorPanel.Name(Draft, part.BindingId, TextResolver), part.Quality, part.Message);
            if (assessment.IsArchived) text += UserText.Message("copy.5e68da129f7caadb");
            text += run.Session.Balance.Mechanics.DrawingQuality
                ? UserText.Message("copy.da823b72ab4cef52")
                : UserText.Message("copy.daa49761a0e89302");
            qualityExplanation.text = Resolve(text); qualityShade.SetActive(true); qualityPanel.SetActive(true);
        }

        void SaveDraft()
        {
            MortarFootprintEditor.Close();
            Draft = Draft.WithName(title.text);
            SetStatus(run.SaveBlueprint(Draft, out var reason) ? UserText.Message("copy.e048f17356f3c1ea", Draft.Name) : reason);
        }
        void SaveAsNew()
        {
            MortarFootprintEditor.Close();
            var candidate = Draft.AsVariant("drawing-" + Guid.NewGuid().ToString("N"), title.text);
            if (!run.SaveBlueprint(candidate, out var reason)) { SetStatus(reason); return; }
            Draft = candidate; SetStatus(UserText.Message("copy.4c7b8b8a912d4e2b"));
        }
        void SelectAlbum(int index)
        {
            if (index < 0 || index >= run.Workshop.Album.Count) return;
            MortarFootprintEditor.Close();
            Draft = run.Workshop.Album[index]; title.SetTextWithoutNotify(Resolve(Draft.Name));
            SetStatus(UserText.Message("copy.fd2246577ecd4462", Draft.Name));
        }
        void Refresh()
        {
            if (run == null || Draft == null || gold == null) return;
            for (int mount = 0; mount < 3; mount++)
            { string id = Draft.GetWeaponId(mount); bool mortar = id != null && run.Content.TryGetWeapon(id, out var weapon) && weapon.SupportsFootprint; footprintButtons[mount].gameObject.SetActive(mortar); if (mortar) footprintButtons[mount].GetComponentInChildren<Text>().text = Resolve(UserText.Message("copy.65784a4e3ffc4d05", (mount + 1), Draft.GetMortarFootprint(mount).AspectRatio)); }
            gold.text = Resolve(UserText.Message("copy.dfbc594044dd606d", run.Workshop.Gold));
            albumPage = Mathf.Clamp(albumPage, 0, Mathf.Max(0, (run.Workshop.Album.Count - 1) / AlbumRowsPerPage));
            var balance = run.Session.Balance;
            var mechanics = balance.Mechanics;
            trialScenarioButtons[(int)TrialScenario.Blots].gameObject.SetActive(mechanics.CorpseBlots);
            trialScenarioButtons[(int)TrialScenario.Armored].GetComponentInChildren<Text>().text = Resolve(mechanics.EnemyDrawingTiers ? Resolve(balance.Trials.For(TrialScenario.Armored).Name).ToUpperInvariant() : UserText.Message("copy.c8839f3bfabe7fc2"));
            qualityModeButton.gameObject.SetActive(mechanics.DrawingQuality);
            if ((!mechanics.CorpseBlots && trialScenario == TrialScenario.Blots) || (!mechanics.DrawingQuality && qualityTrial))
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
            stats.text = Resolve(UserText.Message("copy.154e011c15594a63", preview.Load, preview.Capacity, (deficit > 0 ? UserText.Message("copy.b60d6857f04f0a4f", deficit) : ""), preview.Cost, preview.Health, (preview.Defense * 100), preview.BuildSeconds, preview.Range, preview.FireRate, (preview.FeedMultiplier * 100), preview.DrawingQuality, (preview.QualityMultiplier * 100), (mechanics.ColorCartridges ? UserText.Message("copy.8e013f7d4704e587", preview.ColorCost, (balance.LimitTowerColor ?
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
                albumLabels[i].text = Resolve(UserText.Message("copy.2de8fa26b46862ac", bp.Name, (bp.Origin == DrawingOrigin.LegacyStandard ? UserText.Message("copy.2123de7cfc728c2c") : UserText.Message("workshop.quality_score", bp.Assessment.Quality)), albumPreview.Cost, WeaponSummary(bp), "", albumPreview.Health, albumPreview.FireRate));
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
            bool runners=false,gnawers=false,spitters=false;
            foreach(var wave in run.Session.Balance.Waves)
            {runners|=wave.RunnerFraction>0;gnawers|=wave.GnawerFraction>0;spitters|=mechanics.RangedEnemies&&wave.SpitterFraction>0;}
            string enemies=UserText.Message("copy.8834fdb6964a6838", (runners?UserText.Message("copy.84d971fcad33df9e"):""), (gnawers?UserText.Message("copy.ec0efac73a0bc3ab"):""), (spitters?UserText.Message("copy.9aa4314a8e7e2cf4"):""));
            briefing.text = Resolve(UserText.Message("copy.851980894ad17adb", balance.Width, balance.Height, (mechanics.InkWells ? UserText.Message("copy.bd969a1552a7f8b0", wells) : ""), enemies, (spitters ? UserText.Message("copy.9f16585ff7173d3c") : ""), (run.Session.Balance.Preset == GameBalancePreset.Target ?
                    UserText.Message("copy.9d7e64acad5dda4e", balance.StartingInk, (mechanics.InkWells ? UserText.Message("copy.01bea534f0c9cb02") : "")) :
                    UserText.Message("copy.4f11dbcf3d576c90", threat, (mechanics.InkWells ? UserText.Message("copy.012fc5ef84805c4b") : "")))));
        }
        void RefreshSaveStatus()
        {
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
        void SetTrial(bool visible) { trialShade.SetActive(visible); trialPanel.SetActive(visible); }
        void RefreshTrialSettings()
        {
            var mechanics = run.Session.Balance.Mechanics;
            if (!mechanics.CorpseBlots && trialScenario == TrialScenario.Blots) trialScenario = TrialScenario.Crowd;
            if (!mechanics.DrawingQuality) qualityTrial = false;
            bool blotTraining = trialScenario == TrialScenario.Blots;
            var rules = run.Session.Balance.Trials;
            string scenarioName = trialScenario == TrialScenario.Armored && !mechanics.EnemyDrawingTiers ? UserText.Message("copy.ae092c38e45a8570") : Resolve(rules.For(trialScenario).Name).ToLowerInvariant();
            trialSettings.text = Resolve(UserText.Message("copy.4c14dc9bbbade9ed", scenarioName, trialBudget, (blotTraining ? UserText.Message("copy.5c541247ef6b3bec") : counterfire ? UserText.Message("copy.6e782106d7934d03") : UserText.Message("copy.73766e6851e92703"))));
            trialConditions.text = Resolve(UserText.Message("copy.3f138ba9b66893d3", (blotTraining ? UserText.Message("copy.ac6f23c50da2aca7", (rules.StepSeconds * rules.MaximumTicks)) :
                    UserText.Message("copy.6c080edef345b261", rules.CounterfireDamage, rules.CounterfireInterval, rules.CounterfireRange))));
            qualityModeLabel.text = Resolve(qualityTrial ? UserText.Message("copy.8cf1dfea8e5d3b5c") : UserText.Message("copy.10b122043549cab7"));
            trialFirstHeader.text = Resolve(qualityTrial ? UserText.Message("copy.034c4ac46d600a47", Draft.Assessment.Quality) : UserText.Message("copy.d5f1a2a73fcf51d5"));
            trialSecondHeader.text = Resolve(qualityTrial ? UserText.Message("copy.195f11eb3d3b0a6f") : UserText.Message("copy.0d0e3e111b1f0f24"));
            ClearTrial();
        }
        void ClearTrial()
        {
            trialDiagramFirst.SetResult(null, 0, 1); trialDiagramSecond.SetResult(null, 0, 1);
            trialFirst.text = Resolve(trialSecond.text = Resolve("")); trialResults.text = Resolve(UserText.Message("copy.8f02821e075dc0d1"));
            trialPlayback.text = Resolve(UserText.Message("copy.cfad8860edae4ba5"));
        }
        void RunTest()
        {
            var preview = run.PreviewBlueprint(Draft, MaterialTier.Pencil);
            if (!preview.Valid) { ClearTrial(); trialResults.text = Resolve(preview.Reason); return; }
            var comparison = qualityTrial ? run.RunQualityTrial(Draft, trialScenario, counterfire, trialBudget) : run.RunTrial(Draft, trialScenario, counterfire, trialBudget);
            if (comparison == null)
            {
                SetStatus(UserText.Message("copy.b8c85b921b3e435e"));
                ClearTrial(); trialResults.text = Resolve(Status); return;
            }
            var a = comparison.First; var b = comparison.Second;
            trialResults.text = Resolve(UserText.Message("copy.20aa3fc90aec2cbe"));
            trialFirst.text = Resolve(TrialValues(a)); trialSecond.text = Resolve(TrialValues(b));
            float started = Time.unscaledTime, duration = Mathf.Max(a.DurationSeconds, b.DurationSeconds);
            trialDiagramFirst.SetResult(a, started, duration); trialDiagramSecond.SetResult(b, started, duration);
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
    public sealed class BlueprintSketch : Graphic
    {
        TowerBlueprint blueprint;
        public void SetBlueprint(TowerBlueprint value) { if (blueprint == value) return; blueprint = value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (blueprint == null || DrawingTemplateCatalog.Create(blueprint).Count == 0) return;
            Vector2 center = rectTransform.rect.center;
            if (blueprint.Origin == DrawingOrigin.PlayerDrawing || ReferenceDrawingCache.UsesTemplateVisual(blueprint))
            {
                float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .68f;
                var drawing = ReferenceDrawingCache.VisibleDrawing(blueprint);
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
    public sealed class WorkshopMapSketch : Graphic
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
            Vector2 home = Point(session.BaseCell, low, high);
            BlueprintSketch.Ring(vh, home, 7, 4, NotebookPalette.Navy);
            for (int i = 0; i < session.Wells.Count; i++)
            { var well = session.Wells[i]; BlueprintSketch.Ring(vh, Point(well.Cell, low, high), 5, 4, DoodleGlyphs.InkTone(well.Color)); }
            int sides = session.Balance.Waves.Count > 0 ? session.Balance.Waves[0].Sides : 1;
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

    /// <summary>Playback of value snapshots captured from the actual trial simulation.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TrialLaneSketch : Graphic
    {
        TrialReport report;
        int frameIndex;
        float started, duration;
        public bool HasRecording => report != null && report.Frames.Count > 0;
        public float PlaybackSeconds => Mathf.Repeat((Time.unscaledTime - started) * 4, Mathf.Max(.1f, duration + .5f));
        public void SetResult(TrialReport value, float startTime, float totalDuration)
        { report = value; started = startTime; duration = totalDuration; frameIndex = 0; SetVerticesDirty(); }
        void Update()
        {
            if (!HasRecording) return;
            float seconds = PlaybackSeconds;
            int next = 0;
            for (int i = 1; i < report.Frames.Count && report.Frames[i].Seconds <= seconds; i++) next = i;
            if (next == frameIndex) return;
            frameIndex = next; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (!HasRecording) return;
            Rect rect = rectTransform.rect;
            Vector2 low = rect.min + new Vector2(8, 7), high = rect.max - new Vector2(8, 7);
            for (int y = 8; y <= 36; y += 4)
                BlueprintSketch.Line(vh, Point(8, y, low, high), Point(50, y, low, high), 1, MeshPen.Alpha(NotebookPalette.Blue, .12f));
            for (int x = 8; x <= 50; x += 4)
                BlueprintSketch.Line(vh, Point(x, 8, low, high), Point(x, 36, low, high), 1, MeshPen.Alpha(NotebookPalette.Blue, .12f));
            BlueprintSketch.Line(vh, Point(48, 8, low, high), Point(48, 36, low, high), 2, MeshPen.Alpha(NotebookPalette.Red, .65f));
            var frame = report.Frames[frameIndex];
            foreach (var blot in frame.Blots)
            {
                Vector2 at = Point(blot.Cell.X + .5f, blot.Cell.Y + .5f, low, high);
                BlueprintSketch.Ring(vh, at, 2.7f, 2, MeshPen.Alpha(NotebookPalette.Marker, .65f));
            }
            foreach (var tower in frame.Towers)
            {
                Vector2 at = Point(tower.Cell.X + .5f, tower.Cell.Y + .5f, low, high);
                if (tower.Health <= 0)
                { BlueprintSketch.Line(vh, at - Vector2.one * 3, at + Vector2.one * 3, 2, NotebookPalette.Red); continue; }
                if (tower.Blueprint != null && (tower.Blueprint.Origin == DrawingOrigin.PlayerDrawing || ReferenceDrawingCache.UsesTemplateVisual(tower.Blueprint)))
                { DrawAuthorTower(vh, tower, low, high); continue; }
                BlueprintSketch.Ring(vh, at, 4, 2, NotebookPalette.Blue);
                Vector2 aim = Point(tower.Aim.X, tower.Aim.Y, low, high) - at;
                if (aim.sqrMagnitude < 1) aim = Vector2.left;
                Vector2 end = at + aim.normalized * 9;
                BlueprintSketch.Line(vh, at, end, 2, NotebookPalette.Blue);
                if (tower.Blueprint != null)
                    for (int mount = 1; mount < tower.Blueprint.WeaponMounts.Count; mount++)
                        BlueprintSketch.Line(vh, at + Vector2.up * (mount * 3), end + Vector2.up * (mount * 3), 2, NotebookPalette.Blue);
                if (tower.Blueprint != null && (tower.Blueprint.Modules & TowerModules.Armor) != 0)
                    BlueprintSketch.Line(vh, at + new Vector2(4, -4), at + new Vector2(4, 4), 3, NotebookPalette.Navy);
            }
            foreach (var enemy in frame.Enemies)
            {
                if (enemy.Health <= 0) continue;
                Vector2 at = Point(enemy.Position.X, enemy.Position.Y, low, high);
                BlueprintSketch.Ring(vh, at, enemy.Kind == EnemyKind.Gnawer ? 3 : 2, 1.5f, NotebookPalette.Navy);
                if (enemy.DrawingTier == DrawingTier.Hatched) BlueprintSketch.Line(vh, at - Vector2.one * 2, at + Vector2.one * 2, 1, NotebookPalette.Navy);
            }
            foreach (var shot in frame.Shots)
            {
                Vector2 from = Point(shot.From.X, shot.From.Y, low, high), to = Point(shot.To.X, shot.To.Y, low, high);
                if (shot.Kind == StructureKind.Blotter)
                { BlueprintSketch.Line(vh, from, to, 1, NotebookPalette.Green); BlueprintSketch.Ring(vh, to, Mathf.Max(3, shot.Radius * (high.y - low.y) / 28), 1, NotebookPalette.Green); }
                else BlueprintSketch.Line(vh, from, to, 1, MeshPen.Alpha(NotebookPalette.Blue, .65f));
            }
            foreach (var projectile in frame.Projectiles)
            {
                float progress = Mathf.Clamp01(1 - projectile.Remaining / Mathf.Max(.001f, projectile.Duration));
                Vector2 from = Point(projectile.From.X, projectile.From.Y, low, high), to = Point(projectile.To.X, projectile.To.Y, low, high);
                Vector2 at = Vector2.Lerp(from, to, progress) + Vector2.up * Mathf.Sin(progress * Mathf.PI) * 15;
                BlueprintSketch.Ring(vh, at, 2, 2, NotebookPalette.Marker);
            }
        }
        static void DrawAuthorTower(VertexHelper vh, StructureView tower, Vector2 low, Vector2 high)
        {
            Point2 origin = new Point2(tower.Cell.X + .5f, tower.Cell.Y + .5f);
            var drawing = ReferenceDrawingCache.VisibleDrawing(tower.Blueprint);
            foreach (var template in DrawingTemplateCatalog.Create(tower.Blueprint))
            {
                var part = drawing.GetPart(template.BindingId); Point2 pivot = template.Pivot;
                bool moving = template.Role == DrawingPartRole.Mount;
                int mountIndex = template.BindingId == DrawingBindings.Mount1 ? 1 : template.BindingId == DrawingBindings.Mount(2) ? 2 : 0;
                Point2 aim = tower.GetMountAim(mountIndex);
                float angle = moving && (aim.X != 0 || aim.Y != 0)
                    ? Mathf.Atan2(aim.Y - origin.Y - pivot.Y, aim.X - origin.X - pivot.X) - Mathf.PI * .5f : 0;
                float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                foreach (var stroke in part.Strokes)
                    for (int i = 1; i < stroke.Points.Count; i++)
                    {
                        var a = stroke.Points[i - 1]; var b = stroke.Points[i];
                        Vector2 from = Point(origin.X + pivot.X + a.X * c - a.Y * s, origin.Y + pivot.Y + a.X * s + a.Y * c, low, high);
                        Vector2 to = Point(origin.X + pivot.X + b.X * c - b.Y * s, origin.Y + pivot.Y + b.X * s + b.Y * c, low, high);
                        BlueprintSketch.Line(vh, from, to, 1, NotebookPalette.Blue);
                    }
            }
        }
        static Vector2 Point(float x, float y, Vector2 low, Vector2 high) => new Vector2(Mathf.Lerp(low.x, high.x, (x - 8) / 42), Mathf.Lerp(low.y, high.y, (y - 8) / 28));
    }
}
