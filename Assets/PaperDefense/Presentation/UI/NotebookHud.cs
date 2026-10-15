using System;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed class NotebookHud : MonoBehaviour
    {
        public UserTextResolver TextResolver { get; set; }
        public bool ReserveStandControls { get; set; }
        string Resolve(string value) => GameText.Resolve(TextResolver, value);
        const int WallToolIndex = ProfileLimits.LoadoutSlotCount, SpikesToolIndex = WallToolIndex + 1;
        const int EraseToolIndex = SpikesToolIndex + 1, UpgradeToolIndex = EraseToolIndex + 1;
        const int BlotterToolIndex = UpgradeToolIndex + 1, PaintToolIndex = BlotterToolIndex + 1;
        const int TarToolIndex = PaintToolIndex + 1, RepairToolIndex = TarToolIndex + 1;
        static readonly Color Navy = new Color(.09f, .16f, .30f);
        static readonly Color Paper = new Color(.98f, .96f, .87f);
        static readonly Color Red = new Color(.78f, .15f, .17f);
        static readonly Color Blue = new Color(.13f, .36f, .69f);
        static readonly Color Purple = new Color(.53f, .19f, .66f);
        RunController run;
        DrawingInput input;
        Font font;
        GameObject canvasObject;
        CanvasScaler canvasScaler;
        RectTransform root;
        Text hp, ink, wave, threats, forecast, message, toolHint, launchText, pauseText, speedText, progress, inspect, blotterPrice, wellTitle, navigationHint;
        Image hpFill;
        Button launch;
        readonly Button[] tools = new Button[RepairToolIndex + 1];
        readonly Text[] toolPrices = new Text[RepairToolIndex + 1], toolNames = new Text[RepairToolIndex + 1], wellLabels = new Text[3], colorLabels = new Text[4];
        readonly Button[] wellActions = new Button[3], wellFocus = new Button[3], colors = new Button[4];
        readonly BlueprintSketch[] toolSketches = new BlueprintSketch[ProfileLimits.LoadoutSlotCount];
        readonly Button[] tiers = new Button[3];
        GameObject upgradePanel, outcomePanel, pauseBadge;
        Text outcomeTitle, outcomeBody;
        CatalogScrollList outcomeScroll;
        float refresh;

        public void Initialize(RunController controller, DrawingInput drawing)
        {
            run = controller; input = drawing;
            font = Resources.Load<Font>("NotebookHand") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("Notebook HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvasScaler = canvasObject.GetComponent<CanvasScaler>(); canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            FitCanvas();
            var board = new GameObject("Notebook layout 1600x1000",typeof(RectTransform)); board.transform.SetParent(canvasObject.transform,false);
            root=board.GetComponent<RectTransform>();root.anchorMin=root.anchorMax=new Vector2(.5f,.5f);root.pivot=new Vector2(.5f,.5f);
            root.sizeDelta=new Vector2(1600,1000);
            BuildHeader(); BuildNotes(); BuildTools(); BuildDialogs();
            input.SelectionChanged += RefreshSelection;
            RefreshSelection(); Refresh();
        }

        void OnDestroy() { if (input != null) input.SelectionChanged -= RefreshSelection; }
        void FitCanvas()
        {
            float height = ReserveStandControls ? 1000f * Screen.height / Mathf.Max(1, Screen.height - 120) : 1000;
            canvasScaler.referenceResolution = new Vector2(1600, height);
        }
        void Update()
        {
            if (run == null) return;
            FitCanvas();
            canvasObject.SetActive(!run.InWorkshop);
            if (run.InWorkshop) return;
            refresh -= Time.unscaledDeltaTime; if (refresh <= 0) { refresh = .08f; Refresh(); }
        }

        void BuildHeader()
        {
            var header = Panel(root, "Header", new Rect(24, 18, 1552, 122), Paper);
            Label(header, UserText.Message("copy.757d91d8dbacda0d"), new Rect(22, 9, 380, 53), 38, Navy, FontStyle.Bold);
            Label(header, UserText.Message("copy.3120ea83be1240ef"), new Rect(26, 68, 385, 28), 15, Navy);
            hp = Label(header, UserText.Message("copy.71b19cdbe3e7f263"), new Rect(432, 13, 205, 40), 23, Navy, FontStyle.Bold);
            var bar = Panel(header, "Health", new Rect(437, 66, 180, 16), new Color(.86f,.85f,.75f), false);
            hpFill = Fill(bar, "Health ink", new Color(.30f,.57f,.34f));
            ink = Label(header, "420", new Rect(665, 8, 160, 49), 36, Blue, FontStyle.Bold);
            Label(header, UserText.Message("copy.acf98a506884cf6a"), new Rect(667, 65, 183, 27), 17, Navy);
            wave = Label(header, UserText.Message("copy.0baf9e04fa92ed14"), new Rect(867, 11, 242, 43), 25, Navy, FontStyle.Bold);
            progress = Label(header, UserText.Message("copy.d31403f3ccb466bd"), new Rect(869, 63, 228, 29), 16, Navy);
            launch = MakeButton(header, "Launch wave", new Rect(1122, 15, 255, 75), UserText.Message("copy.acfeb09be9aaa8b1"), 21, Blue,
                () => run.NextWave(), out launchText);
            MakeButton(header, "Pause", new Rect(1392, 15, 136, 33), UserText.Message("copy.213309ff484c29b6"), 13, Navy,
                () => run.TogglePause(), out pauseText);
            MakeButton(header, "Speed", new Rect(1392, 58, 136, 33), UserText.Message("copy.4033d9b367714c42"), 13, Navy,
                () => run.ToggleSpeed(), out speedText);
        }

        void BuildNotes()
        {
            var note = Panel(root, "Field notes", new Rect(35, 166, 232, 390), new Color(1,.98f,.86f));
            note.localEulerAngles = new Vector3(0,0,2);
            wellTitle = Label(note, UserText.Message("copy.5b88c2e8e78d81dc"), new Rect(14,12,210,35), 23, Navy, FontStyle.Bold);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                wellLabels[i] = Label(note, "", new Rect(14, 56 + i * 98, 203, 44), 16, Navy);
                wellActions[i] = MakeButton(note, "Activate well " + i, new Rect(14, 103 + i * 98, 137, 31), UserText.Message("copy.a975ed8c71a70c75"), 13, Blue,
                    () => { if (index < run.Session.Wells.Count) input.SetMessage(run.ActivateWell(run.Session.Wells[index].Id, out var reason) ? UserText.Message("copy.c35a47cb54590976") : reason); }, out _);
                wellFocus[i] = MakeButton(note, "Focus well " + i, new Rect(157, 103 + i * 98, 60, 31), UserText.Message("common.focus_symbol"), 18, Navy,
                    () => { if (index < run.Session.Wells.Count) input.Focus(run.Session.Wells[index].Cell); }, out _);
            }
            MakeButton(note, "Focus base", new Rect(14, 351, 203, 28), UserText.Message("copy.202eda5b5d6069bc"), 15, Navy, () => input.Focus(run.Session.BaseCell), out _);
            navigationHint = Label(root, "", new Rect(43,574,225,140), 17, Blue);
            var danger = Panel(root, "Forecast", new Rect(1335,170,231,222), new Color(1,.95f,.74f));
            danger.localEulerAngles = new Vector3(0,0,-2);
            Label(danger, UserText.Message("copy.aae58a43f70e3181"), new Rect(13,8,215,35), 22, Red, FontStyle.Bold);
            forecast = Label(danger, "", new Rect(14,53,203,150), 18, Navy);
            threats = Label(root, UserText.Message("copy.a5e9d1eda4169ee0"), new Rect(1355,430,210,104), 29, Red, FontStyle.Bold);
            inspect = Label(root, UserText.Message("copy.8bf6205579fddebb"), new Rect(1355,571,213,116), 17, Navy);
            pauseBadge = Panel(root, "Paused badge", new Rect(580,158,440,54), new Color(1,.96f,.72f)).gameObject;
            Label(pauseBadge.transform, UserText.Message("copy.1c177288ff8d4ff2"), new Rect(15,5,410,42), 24, Navy, FontStyle.Bold, TextAnchor.MiddleCenter);
            pauseBadge.SetActive(false);
        }

        void BuildTools()
        {
            var footer = Panel(root, "Drawing desk", new Rect(24,794,1552,188), Paper);
            string[] materialNames = { UserText.Message("copy.37cc365a81a9e1da"), UserText.Message("copy.280f0b15f654144d"), UserText.Message("copy.8bb1c1810f1988a2") };
            Color[] materialColors = { new Color(.61f,.39f,.12f), Blue, Purple };
            for (int i = 0; i < 3; i++)
            { int index = i; tiers[i] = MakeButton(footer, "Material " + i, new Rect(18+i*158,8,148,34), materialNames[i], 15,
                materialColors[i], () => input.SelectTier((MaterialTier)index), out _); }
            string[] colorNames = { UserText.Message("copy.5b3a35476fadf70d"), UserText.Message("copy.0d3f8bcf19bb7c5a"), UserText.Message("copy.5f49eedf22605e85"), UserText.Message("copy.fe07436768c0ef5d") };
            Color[] colorInks = { Navy, Red, Blue, NotebookPalette.Green };
            for (int i = 0; i < 4; i++)
            { int index = i; colors[i] = MakeButton(footer, "Ink color " + i, new Rect(500+i*158,8,148,34), colorNames[i], 14,
                colorInks[i], () => input.SelectColor((InkColor)index), out colorLabels[i]); }
            MakeButton(footer, "Restart", new Rect(1152,8,179,34), UserText.Message("copy.d5335a42ec267095"), 15, Navy, () => run.Restart(), out _);
            MakeButton(footer, "Open workshop", new Rect(1342,8,188,34), UserText.Message("copy.b49ada27eb109bc1"), 15, Navy, () => run.OpenWorkshop(), out _);
            string[] names = { UserText.Message("hud.tool.blueprint", 1), UserText.Message("hud.tool.blueprint", 2), UserText.Message("hud.tool.blueprint", 3), UserText.Message("hud.tool.blueprint", 4),
                UserText.Message("copy.95a4ba0731bceb67"), UserText.Message("copy.d688530d0db50106"), UserText.Message("copy.e2469d7e62002b1f"),
                UserText.Message("copy.6e7d348fa6aaa5f3"), UserText.Message("hud.tool.blotter"), UserText.Message("copy.28c686dce839c9a7"),
                UserText.Message("hud.tool.tar"), UserText.Message("hud.tool.repair") };
            for (int i = 0; i < tools.Length; i++)
            {
                int index = i;
                // Keep stable object identities for old hotkeys and scene automation, group construction cards visually.
                int column = i < 6 ? i : i >= TarToolIndex ? i - 4 : i + 2;
                tools[i] = MakeButton(footer, "Tool " + i, new Rect(18 + column * 127, 52, 119, 94), "", 17, Navy, () => {
                    if (index < ProfileLimits.LoadoutSlotCount) input.SelectSlot(index);
                    else if (TryConstructionTool(index, out var kind)) input.SelectTool(kind);
                    else input.SelectMode(index == EraseToolIndex ? DrawingMode.Erase : index == UpgradeToolIndex ? DrawingMode.Upgrade : index == BlotterToolIndex ? DrawingMode.Blotter : DrawingMode.Paint);
                }, out _);
                if (i < ProfileLimits.LoadoutSlotCount)
                {
                    var go = new GameObject("Blueprint illustration", typeof(RectTransform), typeof(CanvasRenderer), typeof(BlueprintSketch));
                    go.transform.SetParent(tools[i].transform, false);
                    Place(go.GetComponent<RectTransform>(), new Rect(34, 4, 51, 45));
                    toolSketches[i] = go.GetComponent<BlueprintSketch>(); toolSketches[i].raycastTarget = false;
                }
                else
                {
                    var go = new GameObject("Tool illustration", typeof(RectTransform), typeof(CanvasRenderer), typeof(NotebookArtGraphic));
                    go.transform.SetParent(tools[i].transform, false);
                    Place(go.GetComponent<RectTransform>(), new Rect(37, 4, 45, 45));
                    var art = go.GetComponent<NotebookArtGraphic>(); art.raycastTarget = false;
                    string icon = i == WallToolIndex ? "wall" : i == SpikesToolIndex ? "spikes" : i == TarToolIndex ? "tar" :
                        i == RepairToolIndex ? "repair" : i == EraseToolIndex ? "erase" : i == UpgradeToolIndex ? "upgrade" : i == BlotterToolIndex ? "blotter" : "paint";
                    art.Bind("ui.tool." + icon);
                }
                toolNames[i] = Label(tools[i].transform, names[i], new Rect(3, 49, 113, 20), 14, Navy, FontStyle.Bold, TextAnchor.MiddleCenter);
                toolNames[i].verticalOverflow = VerticalWrapMode.Truncate;
                toolPrices[i] = Label(tools[i].transform, "", new Rect(3, 71, 113, 20), 13, Blue, FontStyle.Normal, TextAnchor.MiddleCenter);
                if (i == BlotterToolIndex) blotterPrice = toolPrices[i];
                else if (i >= EraseToolIndex && i <= PaintToolIndex) toolPrices[i].text = Resolve(i == EraseToolIndex ? UserText.Message("copy.3337c8fa1eed04a4") : i == UpgradeToolIndex ? UserText.Message("copy.d7ba3d4cc2223633") : UserText.Message("copy.7e642f606c53837a"));
            }
            toolHint = Label(footer, "", new Rect(20, 151, 1512, 31), 16, Navy);
            message = Label(root, "", new Rect(270,748,1055,34), 19, Blue, FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        void BuildDialogs()
        {
            upgradePanel = Panel(root, "Upgrade choices", new Rect(300,280,1000,340), Paper).gameObject;
            upgradePanel.GetComponent<Image>().raycastTarget=true;
            Label(upgradePanel.transform, UserText.Message("copy.ecc7f0cf4c0fd95f"), new Rect(30,18,940,49), 34, Navy, FontStyle.Bold, TextAnchor.MiddleCenter);
            Label(upgradePanel.transform, UserText.Message("copy.6c51f44f2b982a45"), new Rect(30,75,940,36), 22, Navy, FontStyle.Normal, TextAnchor.MiddleCenter);
            string[] titles = { UserText.Message("copy.43834ce548d0746e"), UserText.Message("copy.be2cd454027d0498"), UserText.Message("copy.3e687796ac2bbc51") };
            string[] bodies = { UserText.Message("copy.86e0b83f54747d59"), UserText.Message("copy.a800350567ca0a56"), UserText.Message("copy.780939a301e3fb0b") };
            for (int i=0; i<3; i++)
            {
                int choice = i;
                var b = MakeButton(upgradePanel.transform, "Upgrade " + i, new Rect(32+i*322,137,292,164), "", 20, Blue,
                    () => {run.ChooseUpgrade((RunUpgrade)choice); input.SetMessage(UserText.Message("copy.a2c452514b532e1d"));}, out _);
                Label(b.transform, titles[i], new Rect(12,14,268,43), 21, Navy, FontStyle.Bold, TextAnchor.MiddleCenter);
                Label(b.transform, bodies[i], new Rect(12,64,268,83), 21, Navy, FontStyle.Normal, TextAnchor.MiddleCenter);
            }
            upgradePanel.SetActive(false);
            outcomePanel = Panel(root, "Run result", new Rect(420,260,760,420), Paper).gameObject;
            outcomePanel.GetComponent<Image>().raycastTarget=true;
            outcomeTitle = Label(outcomePanel.transform, "", new Rect(25,25,710,65), 42, Navy, FontStyle.Bold, TextAnchor.MiddleCenter);
            outcomeScroll = new CatalogScrollList(outcomePanel.transform, "Run statistics", new Rect(25, 100, 710, 222), 0, 1, 25, 0);
            outcomeBody = Label(outcomeScroll.Content, "", new Rect(0, 0, 704, 222), 19, Navy, FontStyle.Normal, TextAnchor.UpperCenter);
            MakeButton(outcomePanel.transform, "Try another defense", new Rect(140,340,480,61), UserText.Message("copy.7b0050e591870b58"), 22, Blue, () => run.OpenWorkshop(), out _);
            outcomePanel.SetActive(false);
        }

        static bool TryConstructionTool(int index, out StructureKind kind)
        {
            kind = index == WallToolIndex ? StructureKind.Wall : index == SpikesToolIndex ? StructureKind.Spikes :
                index == TarToolIndex ? StructureKind.TarPit : StructureKind.RepairPost;
            return index == WallToolIndex || index == SpikesToolIndex || index == TarToolIndex || index == RepairToolIndex;
        }

        string BuildDescription()
        {
            var balance = run.Session.Balance;
            if (input.BlueprintSlot >= 0 && input.BlueprintSlot < run.BuildLoadout.Count)
            {
                var bp = run.BuildLoadout[input.BlueprintSlot];
                if (bp == null) return "";
                var preview = run.PreviewBlueprint(bp, input.Tier);
                return UserText.Message("hud.detail.blueprint", bp.Name, preview.Cost, preview.Range,
                    balance.Battlefield != null && !balance.Battlefield.IsOpenTerrain ? UserText.Message("hud.ground.tower") : UserText.Message("hud.ground.free"));
            }
            if (!balance.HasStructure(input.Tool)) return "";
            var spec = balance.Structure(input.Tool);
            string placement = balance.Battlefield == null || balance.Battlefield.IsOpenTerrain ? UserText.Message("hud.ground.free") :
                input.Tool == StructureKind.Wall ? UserText.Message("hud.ground.wall") : ConstructionToolRules.IsTrap(input.Tool) ?
                    UserText.Message("hud.ground.road") : UserText.Message("hud.ground.tower");
            string purpose = input.Tool == StructureKind.TarPit ? UserText.Message("hud.detail.tar", (1 - spec.SlowSpeedMultiplier) * 100) :
                input.Tool == StructureKind.RepairPost ? UserText.Message("hud.detail.repair", spec.RepairPerSecond, spec.Range) :
                input.Tool == StructureKind.Wall ? UserText.Message("hud.detail.wall", spec.MaxHealth * balance.Material(input.Tier).HealthMultiplier) :
                    UserText.Message("hud.detail.spikes", spec.Damage);
            return UserText.Message("hud.detail.tool", purpose, balance.ToolCost(input.Tool, input.Tier), placement);
        }

        void RefreshSelection()
        {
            if (run == null || run.Session == null) return;
            var mechanics = run.Session.Balance.Mechanics;
            var loadout = run.BuildLoadout;
            tools[BlotterToolIndex].gameObject.SetActive(mechanics.CorpseBlots);
            tools[PaintToolIndex].gameObject.SetActive(mechanics.ColorCartridges);
            blotterPrice.text = Resolve(UserText.Message("hud.tool.price", run.Session.Balance.BlotterCost));
            for (int i=0;i<tools.Length;i++)
            {
                bool selected = i < ProfileLimits.LoadoutSlotCount ? input.Mode == DrawingMode.Build && input.BlueprintSlot == i :
                    i == WallToolIndex ? input.Mode == DrawingMode.Build && input.BlueprintSlot < 0 && input.Tool == StructureKind.Wall :
                    i == SpikesToolIndex ? input.Mode == DrawingMode.Build && input.BlueprintSlot < 0 && input.Tool == StructureKind.Spikes :
                    i == TarToolIndex ? input.Mode == DrawingMode.Build && input.BlueprintSlot < 0 && input.Tool == StructureKind.TarPit :
                    i == RepairToolIndex ? input.Mode == DrawingMode.Build && input.BlueprintSlot < 0 && input.Tool == StructureKind.RepairPost :
                    i == EraseToolIndex ? input.Mode == DrawingMode.Erase : i == UpgradeToolIndex ? input.Mode == DrawingMode.Upgrade : i == BlotterToolIndex ? input.Mode == DrawingMode.Blotter : input.Mode == DrawingMode.Paint;
                if (TryConstructionTool(i, out var kind)) tools[i].gameObject.SetActive(run.Session.Balance.HasStructure(kind));
                tools[i].GetComponent<Image>().color = selected ? new Color(.78f,.87f,.95f) : Paper;
            }
            for (int i=0;i<3;i++) tiers[i].GetComponent<Image>().color = (int)input.Tier == i ? new Color(.89f,.84f,.98f) : Paper;
            string[] colorNames = { UserText.Message("copy.5b3a35476fadf70d"), UserText.Message("copy.0d3f8bcf19bb7c5a"), UserText.Message("copy.5f49eedf22605e85"), UserText.Message("copy.fe07436768c0ef5d") };
            for (int i = 0; i < colors.Length; i++)
            { colors[i].gameObject.SetActive(mechanics.ColorCartridges); colors[i].GetComponent<Image>().color = (int)input.Color == i ? new Color(.82f,.9f,.84f) : Paper; colorLabels[i].text = Resolve(colorNames[i] + (i == 0 ? "" : " " + run.Session.ColoredInk((InkColor)i))); }
            for (int i=0;i<toolPrices.Length;i++)
            {
                if (i < ProfileLimits.LoadoutSlotCount)
                {
                    bool assigned = i < loadout.Count && loadout[i] != null;
                    tools[i].interactable = assigned;
                    if (!assigned)
                    {
                        toolSketches[i].SetBlueprint(null);
                        toolNames[i].text = Resolve(UserText.Message("hud.tool.blueprint", i + 1));
                        toolPrices[i].text = Resolve(UserText.Message("copy.dbf292d621b20c3c"));
                        continue;
                    }
                    var bp = loadout[i]; var preview = run.PreviewBlueprint(bp, input.Tier);
                    toolSketches[i].SetBlueprint(bp);
                    toolNames[i].text = Resolve(UserText.Message("hud.tool.blueprint", i + 1));
                    toolPrices[i].text = Resolve(UserText.Message("copy.75ee45ea828ded7b", preview.Cost, (input.Color == InkColor.None ? "" : UserText.Message("copy.9a6907b592de7b89", preview.ColorCost))));
                }
                else if (TryConstructionTool(i, out var kind) && run.Session.Balance.HasStructure(kind))
                {
                    int cost = run.Session.Balance.ToolCost(kind, input.Tier);
                    toolPrices[i].text = Resolve(UserText.Message("hud.tool.price", cost));
                }
            }
        }

        void Refresh()
        {
            if (run == null || run.Session == null || hp == null) return;
            var s = run.Session;
            var mechanics = s.Balance.Mechanics;
            wellTitle.text = Resolve(mechanics.InkWells ? UserText.Message("copy.5b88c2e8e78d81dc") : UserText.Message("copy.6c0a36f0a82af27b"));
            bool hasTar = s.Balance.HasStructure(StructureKind.TarPit), hasRepair = s.Balance.HasStructure(StructureKind.RepairPost);
            string supportHelp = hasTar && hasRepair ? UserText.Message("hud.navigation.support") : hasTar ?
                UserText.Message("hud.navigation.tar") : hasRepair ? UserText.Message("hud.navigation.repair") : "";
            navigationHint.text = Resolve(UserText.Message("hud.navigation", (mechanics.ColorCartridges ? UserText.Message("hud.color_navigation") : "") + supportHelp));
            for (int i = 0; i < wellLabels.Length; i++)
            {
                bool exists = mechanics.InkWells && i < s.Wells.Count;
                wellLabels[i].gameObject.SetActive(exists); wellActions[i].gameObject.SetActive(exists); wellFocus[i].gameObject.SetActive(exists);
                if (!exists) continue;
                var well = s.Wells[i];
                wellLabels[i].text = Resolve(UserText.Message("hud.well_status", Resolve(WorkshopHud.ColorName(well.Color)).ToUpperInvariant(), Mathf.CeilToInt(well.Health), Mathf.CeilToInt(well.MaxHealth),
                    well.Threatened ? UserText.Message("copy.cb3fe9fc2c1d293f") : well.Active ? UserText.Message("copy.7683aad600e31720", s.Balance.WellInkPerWave) : UserText.Message("copy.1b2a00ae9bb11df8", s.Balance.WellActivationCost)));
                wellLabels[i].color = well.Threatened ? Red : Navy;
                wellActions[i].interactable = !well.Active;
            }
            hp.text = Resolve(UserText.Message("copy.27c2a6466d066b29", Mathf.CeilToInt(s.BaseHealth), Mathf.CeilToInt(s.Balance.BaseHealth)));
            hpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(s.BaseHealth / s.Balance.BaseHealth),1);
            ink.text = Resolve(UserText.Message("hud.ink_value", s.Ink));
            wave.text = Resolve(UserText.Message("copy.8b8e56d82fca6365", s.WaveIndex, s.Balance.Waves.Count));
            progress.text = Resolve(s.Phase == RunPhase.Assault ? UserText.Message("copy.ccb8a5e0870a188a", s.RemainingToSpawn) :
                s.Phase == RunPhase.Victory ? UserText.Message("copy.8a0f60996b5a1207") : s.Phase == RunPhase.Defeat ? UserText.Message("copy.5c3601622bd34904") :
                UserText.Message("copy.4da6286e40e65070", Mathf.CeilToInt(s.Countdown)));
            threats.text = Resolve(UserText.Message("copy.6c146e1186473ee4", s.Enemies.Count));
            int next = s.Phase == RunPhase.Assault ? s.WaveIndex-1 : s.WaveIndex;
            if (next >= 0 && next < s.Balance.Waves.Count)
            {
                var w = s.Balance.Waves[next];
                string directions = ((w.Sides&1)!=0 ? UserText.Message("copy.5df048b6711a3c36") : "") + ((w.Sides&2)!=0 ? UserText.Message("copy.1560b116b94f7915") : "") +
                    ((w.Sides&4)!=0 ? UserText.Message("copy.6b3d6f077552e970") : "") + ((w.Sides&8)!=0 ? UserText.Message("copy.fe10822f38def82b") : "");
                forecast.text = Resolve(UserText.Message("copy.53c30c8509f69f84", w.Title, directions, w.Count, (w.BruteFraction > 0 ? UserText.Message("hud.threat.brute") : w.BulwarkFraction > 0 ? UserText.Message("hud.threat.bulwark") : mechanics.RangedEnemies && w.SpitterFraction > 0 ? UserText.Message("copy.0dc97464c8838d7d") : w.GnawerFraction>0 ? UserText.Message("copy.5fc321a6caf6a1ae") : w.RunnerFraction>0 ? UserText.Message("copy.807e868e6de58bea") : UserText.Message("copy.e5540bf1c3261b6e")), (mechanics.EnemyDrawingTiers ? (w.HatchedFraction > 0 ? UserText.Message("copy.861f99b195ee6f6f") : w.OutlinedFraction > 0 ? UserText.Message("copy.146f1f40157f1f06") : "") : "")));
            }
            else forecast.text = Resolve(UserText.Message("copy.01dd83e7e64f7ca1"));
            launch.interactable = (s.Phase == RunPhase.Planning || s.Phase == RunPhase.Intermission) && !s.PendingUpgrade;
            launchText.text = Resolve(s.Phase == RunPhase.Assault ? UserText.Message("copy.757d91d8dbacda0d") : UserText.Message("copy.981cdaa4f99125ec"));
            pauseText.text = Resolve(run.Paused ? UserText.Message("copy.6fd1f50c445008ce") : UserText.Message("copy.213309ff484c29b6"));
            speedText.text = Resolve(UserText.Message("copy.999ba601b0319a15", run.Speed));
            pauseBadge.SetActive(run.Paused && s.Phase != RunPhase.Defeat && s.Phase != RunPhase.Victory);
            message.text = Resolve(input.Message);
            toolHint.text = Resolve(input.Mode == DrawingMode.Erase ? UserText.Message("copy.c611ebfb5f1ad4ef") :
                input.Mode == DrawingMode.Upgrade ? UserText.Message("copy.e269b19b30016bc5") :
                input.Mode == DrawingMode.Blotter ? UserText.Message("copy.710ac373fff75b43", s.Balance.BlotterRadius, (s.BlotterCooldown > 0 ? UserText.Message("copy.4e3722729b4f917d", s.BlotterCooldown) : UserText.Message("copy.1abdee4640babd7e"))) :
                input.Mode == DrawingMode.Paint ? UserText.Message("copy.49ccf98f60e87674") :
                BuildDescription());
            if (input.Mode == DrawingMode.Build && input.PreviewReason.Length > 0)
                toolHint.text = Resolve(UserText.Message("copy.2c037da520ce29a5", input.PreviewCost, input.PreviewReason));
            toolHint.color = input.PreviewReason.Length > 0 ? Red : Navy;
            if (input.HasHover)
            {
                var st = s.StructureAt(input.HoverCell);
                if (st.HasValue && st.Value.WaitingForClearance)
                    inspect.text = Resolve(UserText.Message("copy.5f0e5acdafad2e95"));
                else if (st.HasValue && !st.Value.IsComplete)
                    inspect.text = Resolve(UserText.Message("copy.19ea6da1c8952906", st.Value.BuildRemaining));
                else if (st.HasValue && !ConstructionToolRules.IsTool(st.Value.Kind))
                    inspect.text = Resolve(UserText.Message("hud.inspect_tower", st.Value.Blueprint != null ? st.Value.Blueprint.Name : UserText.Message("copy.6a001d6a4f6a3ef2"), Mathf.CeilToInt(st.Value.Health), mechanics.ColorCartridges ? UserText.Message("copy.5539bfabca1b7c39", WorkshopHud.ColorName(st.Value.Color)) : "", st.Value.Priority==TargetPriority.Nearest ? UserText.Message("copy.e8587d93da4cf0eb") : st.Value.Priority==TargetPriority.Strongest ? UserText.Message("copy.38d5a59f100989bb") : UserText.Message("copy.ae092c38e45a8570")));
                else inspect.text = Resolve(UserText.Message("copy.8bf6205579fddebb"));
            }
            else inspect.text = Resolve(UserText.Message("copy.8bf6205579fddebb"));
            upgradePanel.SetActive(mechanics.RunUpgrades && s.PendingUpgrade);
            bool finished = s.Phase == RunPhase.Victory || s.Phase == RunPhase.Defeat;
            outcomePanel.SetActive(finished);
            if (finished)
            {
                outcomeTitle.text = Resolve(s.Phase == RunPhase.Victory ? UserText.Message("copy.573be472ca1a8a11") : UserText.Message("copy.a58d79aad3ecdd0e"));
                outcomeTitle.color = s.Phase == RunPhase.Victory ? Blue : Red;
                var stats = s.Statistics;
                double idle = stats.TotalIdleSeconds;
                string damage = "";
                foreach (string id in stats.WeaponIds)
                {
                    string name = s.Balance.Content.TryGetWeapon(id, out var weapon) ? weapon.Name : id;
                    damage += (damage.Length == 0 ? "" : "\n") + UserText.Message("hud.weapon_damage", name, stats.Damage(id));
                }
                string breach = UserText.Message("copy.ee9a42d8494c55dc");
                if (stats.Breaches.Count > 0)
                {
                    var last = stats.Breaches[stats.Breaches.Count - 1];
                    breach = UserText.Message("copy.8cade4b86f612664", last.Cell, last.Elapsed);
                }
                outcomeBody.text = Resolve(UserText.Message("copy.119b2a3170e4560c", s.WaveIndex, s.Kills, stats.PeakAliveEnemies, stats.TotalDamage, (damage.Length > 0 ? "\n" + damage : ""), idle, (stats.BuildInvestment + stats.UpgradeInvestment), stats.RefundedInk, stats.LostInvestment, breach, stats.Upgrades.Count, s.GoldEarned, (s.Phase == RunPhase.Victory ? UserText.Message("copy.3b8b29f972fe2eca") : UserText.Message("copy.4bee799da45bfde8"))));
                float height = Mathf.Max(222, outcomeBody.preferredHeight);
                outcomeBody.rectTransform.sizeDelta = new Vector2(704, height);
                outcomeScroll.Content.sizeDelta = new Vector2(0, height);
            }
            RefreshSelection();
        }

        RectTransform Panel(Transform parent, string name, Rect box, Color color, bool outline = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent,false);
            var r = go.GetComponent<RectTransform>(); Place(r,box);
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            if (outline) {var o=go.AddComponent<Outline>(); o.effectColor=Navy; o.effectDistance=new Vector2(1.4f,-1.4f); o.useGraphicAlpha=false;}
            return r;
        }
        Image Fill(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var r=go.GetComponent<RectTransform>(); r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        Text Label(Transform parent, string content, Rect box, int size, Color color, FontStyle style=FontStyle.Normal, TextAnchor align=TextAnchor.UpperLeft)
        {
            var go=new GameObject("Text " + content.Replace('\n',' '),typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false);
            Place(go.GetComponent<RectTransform>(),box);var text=go.GetComponent<Text>();text.font=font;text.text=Resolve(content);text.fontSize=size;
            text.color=color;text.fontStyle=style;text.alignment=align;text.raycastTarget=false;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;return text;
        }
        Button MakeButton(Transform parent,string name,Rect box,string title,int size,Color color,Action action,out Text text)
        {
            var r=Panel(parent,name,box,Paper);var image=r.GetComponent<Image>();image.raycastTarget=true;
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;
            var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.88f,.93f,1);colors.pressedColor=new Color(.68f,.80f,.94f);
            colors.disabledColor=new Color(.8f,.8f,.8f,.6f);b.colors=colors;b.onClick.AddListener(()=>action());
            var nav=b.navigation;nav.mode=Navigation.Mode.None;b.navigation=nav;
            text=Label(r,title,new Rect(6,3,box.width-12,box.height-6),size,color,FontStyle.Bold,TextAnchor.MiddleCenter);return b;
        }
        static void Place(RectTransform r,Rect box)
        {r.anchorMin=new Vector2(0,1);r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(box.x,-box.y);r.sizeDelta=new Vector2(box.width,box.height);}
    }
}
