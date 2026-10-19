using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Compact player HUD, backed by the same commands as the development HUD.</summary>
    public sealed partial class PlayerBattleHud
    {
        readonly PlayerGameRoot game;
        readonly PlayerUi ui;
        readonly RectTransform renderRoot;
        readonly Text ink, health, wave, phase, message, selection, speedValue, forecast, fieldStats, towerInfo;
        readonly Button launch;
        readonly Button[] tools = new Button[12], materials = new Button[3], colors = new Button[4];
        readonly Text[] prices = new Text[12], colorAmounts = new Text[4];
        readonly BlueprintSketch[] sketches = new BlueprintSketch[4];
        readonly Button[] wells = new Button[3];
        readonly Text[] wellAmounts = new Text[3];
        readonly PlayerIconGraphic[] wellSymbols = new PlayerIconGraphic[3];
        static readonly StructureKind[] Construction = { StructureKind.Wall, StructureKind.Spikes, StructureKind.TarPit, StructureKind.RepairPost };
        static readonly string[] ToolIds = { "wall", "spikes", "tar", "repair", "erase", "upgrade", "blotter", "paint" };

        readonly RectTransform context;
        bool showSummary;
        string toolHint = "";
        static readonly string[] Captions = { "Башня 1", "Башня 2", "Башня 3", "Башня 4", "Стена", "Шипы", "Смола", "Ремонт", "Ластик", "Улучшить", "Промокашка", "Окраска" };
        static readonly string[] Keys = { "1", "2", "3", "4", "5", "6", "7", "8", "E", "U", "B", "C" };
        readonly Text[] names = new Text[4];
        readonly string[] displayedNames = new string[4];

        public PlayerBattleHud(PlayerGameRoot owner, PlayerUi style, RectTransform root, PlayerLayout layout)
        {
            game = owner; ui = style; renderRoot = root;
            if (game.Run.UsesAlbumSlots) { BuildAlbumHud(root, layout); return; }
            float w = layout.Header.width;
            bool narrow = layout.Width < 1050;
            var top = ui.Panel(root, "Battle header", layout.Header, PlayerUi.Card, true);
            Icon(top, PlayerIcon.Pencil, new Rect(12, 12, 26, 26), PlayerUi.Ink);
            ink = ui.Label(top, "", new Rect(44, 7, 126, 38), 19);
            health = ui.Label(top, "", new Rect(178, 7, 180, 38), 17);
            wave = ui.Label(top, "", new Rect(370, 7, 130, 38), 18);
            float controlY = narrow ? 48 : 5;
            float controlX = w - 304;
            phase = ui.Label(top, "", new Rect(narrow ? 16 : 510, narrow ? 50 : 3, narrow ? controlX - 24 : Mathf.Max(100, controlX - 520), 24), 15, PlayerUi.Muted);
            forecast = ui.Label(top, "", new Rect(narrow ? 16 : 510, narrow ? 72 : 27, narrow ? controlX - 24 : Mathf.Max(100, controlX - 520), 21), 14, PlayerUi.Muted);
            launch = ui.Button(top, "Launch wave", new Rect(controlX, controlY, 112, 40), PlayerIcon.Flag, "Волна", () => game.Run.NextWave(), true);
            launch.GetComponent<PlayerTooltip>().Message = "Следующая волна · N";
            ui.IconButton(top, "Battle speed", new Rect(controlX + 120, controlY, 44, 40), PlayerIcon.Fast, ui.T("speed"), () => game.Run.ToggleSpeed());
            speedValue = ui.Label(top, "", new Rect(controlX + 166, controlY, 34, 40), 17);
            ui.IconButton(top, "Battle ranges", new Rect(controlX + 204, controlY, 40, 40), PlayerIcon.Target, ui.T("ranges"), () => game.World.ShowRanges = !game.World.ShowRanges);
            ui.IconButton(top, "Battle pause", new Rect(controlX + 252, controlY, 40, 40), PlayerIcon.Pause, ui.T("pause"), game.Pause);

            var dock = ui.Panel(root, "Construction dock", layout.Dock, PlayerUi.Card, true);
            float contentW = Mathf.Max(w - 24, 1040);
            Transform toolParent = dock;
            if (narrow)
            {
                var row = ui.Scroll(dock, "Tools horizontal scroll", new Rect(12, 6, w - 24, 106), 106);
                var scroll = row.parent.GetComponent<ScrollRect>();
                scroll.verticalScrollbar.gameObject.SetActive(false); scroll.verticalScrollbar = null;
                scroll.vertical = false; scroll.horizontal = true; row.sizeDelta = new Vector2(contentW, 106);
                PlayerUi.HorizontalScrollbar(scroll); toolParent = row;
            }
            float startX = narrow ? 0 : 12, startY = narrow ? 0 : 8;
            float blueprintsW = contentW * .48f, groupW = (contentW - blueprintsW - 24) / 2;
            var blueprints = ui.Panel(toolParent, "Blueprint tools", new Rect(startX, startY, blueprintsW, 96), Color.clear);
            var construction = ui.Panel(toolParent, "Construction tools", new Rect(startX + blueprintsW + 12, startY, groupW, 96), Color.clear);
            var editing = ui.Panel(toolParent, "Editing tools", new Rect(startX + blueprintsW + groupW + 24, startY, groupW, 96), Color.clear);
            for (int i = 0; i < tools.Length; i++)
            {
                int index = i; bool blueprint = i < 4;
                float tw = blueprint ? blueprintsW / 4 - 6 : (groupW - 8) / 2;
                var parent = blueprint ? blueprints : i < 8 ? construction : editing;
                int local = blueprint ? i : (i - 4) % 4;
                var bounds = blueprint ? new Rect(local * (tw + 6), 0, tw, 96)
                    : new Rect(local % 2 * (tw + 8), local / 2 * 50, tw, 46);
                tools[i] = ui.IconButton(parent, "Build tool " + i, bounds, PlayerIcon.Book,
                    Keys[i] + " · " + ui.T("tool_" + i), () => Select(index), art: blueprint ? null : "ui.tool." + ToolIds[i - 4]);
                if (blueprint)
                {
                    Object.Destroy(tools[i].GetComponentInChildren<PlayerIconGraphic>().gameObject);
                    sketches[i] = ui.Blueprint(tools[i].transform, null, new Rect(22, 4, tw - 44, 62));
                    ui.Label(tools[i].transform, Keys[i], new Rect(8, 5, 20, 22), 15, PlayerUi.Muted);
                    names[i] = ui.Label(tools[i].transform, Captions[i], new Rect(6, 66, tw - 12, 24), 14, alignment: TextAnchor.MiddleCenter);
                    names[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                    prices[i] = ui.Label(tools[i].transform, "", new Rect(tw - 43, 5, 36, 22), 15, alignment: TextAnchor.MiddleRight);
                }
                else
                {
                    PlayerUi.Place(tools[i].GetComponentInChildren<NotebookArtGraphic>().rectTransform, new Rect(6, 8, 29, 29));
                    var caption = ui.Label(tools[i].transform, Keys[i] + "  " + Captions[i], new Rect(40, 3, tw - 44, 23), 14);
                    FitCaption(caption, Keys[i] + "  " + Captions[i]);
                    prices[i] = ui.Label(tools[i].transform, "", new Rect(40, 24, tw - 44, 19), 14, PlayerUi.Muted);
                }
            }
            float stripY = narrow ? 116 : 108;
            string[] ids = { "pencil", "pen", "marker" };
            string[] materialNames = { "Карандаш", "Ручка", "Маркер" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                materials[i] = ui.IconButton(dock, "Material " + i, new Rect(12 + i * 108, stripY, 102, 30), PlayerIcon.Drop,
                    ui.T("material_" + i) + " · Q", () => game.Drawing.SelectTier((MaterialTier)index), art: "ui.material." + ids[i]);
                PlayerUi.Place(materials[i].GetComponentInChildren<NotebookArtGraphic>().rectTransform, new Rect(5, 4, 22, 22));
                ui.Label(materials[i].transform, materialNames[i], new Rect(28, 2, 72, 26), 14);
            }
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                colors[i] = ui.IconButton(dock, "Ink " + i, new Rect(350 + i * 62, stripY, 58, 30), PlayerIcon.Drop, ui.T("color_" + i), () => game.Drawing.SelectColor((InkColor)index));
                var symbol = colors[i].GetComponentInChildren<PlayerIconGraphic>();
                PlayerUi.Place(symbol.rectTransform, new Rect(5, 6, 18, 18));
                symbol.color = i == 1 ? NotebookPalette.Red : i == 2 ? NotebookPalette.Blue : i == 3 ? NotebookPalette.Green : PlayerUi.Ink;
                colorAmounts[i] = ui.Label(colors[i].transform, "", new Rect(25, 2, 31, 26), 14);
            }
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                wells[i] = ui.IconButton(dock, "Ink well " + i, new Rect(narrow ? 12 + i * 78 : w - 310 + i * 78, narrow ? 150 : stripY, 72, 30), PlayerIcon.Drop, ui.T("well"), () => ActivateWell(index));
                wellAmounts[i] = ui.Label(wells[i].transform, "", new Rect(28, 2, 40, 26), 14);
                wellSymbols[i] = wells[i].GetComponentInChildren<PlayerIconGraphic>();
                PlayerUi.Place(wellSymbols[i].rectTransform, new Rect(5, 5, 22, 22));
            }
            ui.IconButton(dock, "Battle context toggle", new Rect(w - 64, narrow ? 150 : stripY, 48, 30), PlayerIcon.Help, "Сводка поля", () => { showSummary = !showSummary; Refresh(); });
            selection = ui.Label(root, "", new Rect(24, layout.Dock.y - 28, layout.Width - 48, 24), 15, PlayerUi.Ink, alignment: TextAnchor.MiddleCenter);
            selection.name = "Battle tool hint";
            message = ui.Label(root, "", new Rect(40, layout.Field.y + 4, layout.Width - 80, 26), 16, PlayerUi.Ink, alignment: TextAnchor.MiddleCenter);
            float contextH = Mathf.Min(252, layout.Field.height - 16);
            context = ui.Panel(root, "Battle context", new Rect(layout.Field.xMax - 288, layout.Field.y + 8, 280, contextH), PlayerUi.Card, true);
            ui.IconButton(context, "Close battle context", new Rect(238, 8, 32, 32), PlayerIcon.Back, "Закрыть сведения", () => { showSummary = false; game.Drawing.Inspect(null); Refresh(); });
            var contextBody = ui.Scroll(context, "Selected structure details", new Rect(12, 44, 256, contextH - 52), 200);
            towerInfo = ui.Label(contextBody, "", new Rect(4, 0, 238, 124), 17);
            fieldStats = ui.Label(contextBody, "", new Rect(4, 132, 238, 68), 15, PlayerUi.Muted);
            context.gameObject.SetActive(false);
        }

        void Icon(Transform parent, PlayerIcon icon, Rect rect, Color color)
        { var graphic = PlayerUi.Graphic<PlayerIconGraphic>(parent, "Indicator", rect); graphic.Icon = icon; graphic.Sketch = ui.NotebookStyle; graphic.color = color; }

        static void FitCaption(Text label, string value)
        {
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = value;
            int length = value.Length;
            while (length > 1 && label.preferredWidth > label.rectTransform.rect.width)
                label.text = value.Substring(0, --length).TrimEnd() + "…";
        }

        public void ShowHint(string value) { toolHint = value ?? ""; Refresh(); }

        void Select(int index)
        {
            if (index < 4) game.Drawing.SelectSlot(index);
            else if (index < 8) game.Drawing.SelectTool(Construction[index - 4]);
            else game.Drawing.SelectMode(index == 8 ? DrawingMode.Erase : index == 9 ? DrawingMode.Upgrade : index == 10 ? DrawingMode.Blotter : DrawingMode.Paint);
            Refresh();
        }

        void ActivateWell(int index)
        {
            if (index >= game.Run.Session.Wells.Count) return;
            var well = game.Run.Session.Wells[index];
            if (well.Active) game.Drawing.Focus(well.Cell);
            else game.Drawing.SetMessage(game.Run.ActivateWell(well.Id, out var reason) ? ui.T("well_active") : reason);
        }

        public void Refresh()
        {
            // Tooltip retirement clears the cached hint through ShowHint, including during
            // parent deactivation. Rebuilding that hierarchy would reactivate child controls
            // from inside OnDisable (and is invalid during scene/fixture destruction).
            if (game == null || renderRoot == null || !renderRoot.gameObject.activeInHierarchy) return;
            if (albumHud) { RefreshAlbumHud(); return; }
            var s = game.Run.Session; var input = game.Drawing;
            ink.text = s.Ink + " штрихов"; health.text = "Замок  " + Mathf.CeilToInt(s.BaseHealth) + " / " + Mathf.CeilToInt(s.Balance.BaseHealth);
            wave.text = ui.T("wave", s.WaveIndex, s.Balance.Waves.Count);
            phase.text = game.Finished ? "Миссия завершена" : s.PendingUpgrade ? "Выбери усиление" :
                s.Phase == RunPhase.Assault ? ui.T("enemies", s.Enemies.Count + s.RemainingToSpawn) : ui.T("planning", Mathf.CeilToInt(s.Countdown));
            int nextWave = s.WaveIndex;
            if (nextWave < s.Balance.Waves.Count)
            {
                var next = s.Balance.Waves[nextWave];
                forecast.text = "Далее: " + next.Count + " · " + GameText.Resolve(next.Title);
            }
            else forecast.text = ui.T("final_wave");
            fieldStats.text = ui.T("field_stats", s.Enemies.Count, s.Kills, s.Structures.Count);
            var inspected = input.InspectedCell.HasValue ? s.StructureAt(input.InspectedCell.Value) : null;
            context.gameObject.SetActive(showSummary || inspected.HasValue);
            fieldStats.gameObject.SetActive(showSummary);
            towerInfo.text = inspected.HasValue
                ? (inspected.Value.Blueprint == null ? GameText.Resolve(WorkshopHud.WeaponName(inspected.Value.Kind)) : GameText.Resolve(inspected.Value.Blueprint.Name)) +
                  "\nПрочность " + Mathf.CeilToInt(inspected.Value.Health) + " / " + Mathf.CeilToInt(inspected.Value.MaxHealth) +
                  "\n" + (inspected.Value.IsComplete ? "Готова к обороне" : "Строится") + "\nP над башней — приоритет"
                : "Сводка обороны\nНажми на постройку,\nчтобы увидеть её состояние";
            float towerHeight = Mathf.Ceil(towerInfo.preferredHeight);
            PlayerUi.Place(towerInfo.rectTransform, new Rect(4, 0, 238, towerHeight));
            float statsHeight = showSummary ? Mathf.Ceil(fieldStats.preferredHeight) : 0;
            PlayerUi.Place(fieldStats.rectTransform, new Rect(4, towerHeight + 12, 238, statsHeight));
            var contextContent = (RectTransform)towerInfo.transform.parent;
            float contextHeight = contextContent.parent.GetComponent<ScrollRect>().viewport.rect.height;
            contextContent.sizeDelta = new Vector2(contextContent.sizeDelta.x, Mathf.Max(contextHeight, towerHeight + (showSummary ? statsHeight + 12 : 0)));
            speedValue.text = "×" + game.Run.Speed;
            bool active = game.Page == PlayerPage.Battle;
            launch.interactable = active && !s.PendingUpgrade && (s.Phase == RunPhase.Planning || s.Phase == RunPhase.Intermission);
            message.text = game.Run.Workshop.HasUnsavedChanges ? GameText.Resolve(game.Run.Workshop.SaveMessage) : input.Message;
            int selected = input.Mode == DrawingMode.Erase ? 8 : input.Mode == DrawingMode.Upgrade ? 9 : input.Mode == DrawingMode.Blotter ? 10 : input.Mode == DrawingMode.Paint ? 11 : input.BlueprintSlot >= 0 ? input.BlueprintSlot : -1;
            if (selected < 0) for (int i = 0; i < Construction.Length; i++) if (input.Tool == Construction[i]) selected = i + 4;
            string selectedName = selected >= 0 && selected < 4 && selected < game.Run.BuildLoadout.Count
                ? GameText.Resolve(game.Run.BuildLoadout[selected].Name) : ui.T("tool_" + Mathf.Max(0, selected));
            selection.text = input.PreviewReason.Length > 0 ? input.PreviewReason : ui.T("selection", selectedName, ui.T("material_" + (int)input.Tier));
            for (int i = 0; i < tools.Length; i++)
            {
                bool available = true; int cost = 0;
                if (i < 4)
                {
                    var bp = i < game.Run.BuildLoadout.Count ? game.Run.BuildLoadout[i] : null;
                    sketches[i].SetBlueprint(bp); available = bp != null;
                    cost = bp == null ? 0 : game.Run.PreviewBlueprint(bp, input.Tier).Cost;
                    prices[i].text = bp == null ? "—" : cost.ToString();
                    string name = bp == null ? ui.T("empty") : GameText.Resolve(bp.Name);
                    if (displayedNames[i] != name) { displayedNames[i] = name; FitCaption(names[i], name); }
                }
                else if (i < 8)
                {
                    available = s.Balance.HasStructure(Construction[i - 4]);
                    cost = available ? s.Balance.ToolCost(Construction[i - 4], input.Tier) : 0;
                    prices[i].text = available ? cost.ToString() + " штр." : "—";
                }
                else { cost = i == 10 ? s.Balance.BlotterCost : 0; prices[i].text = cost > 0 ? cost + " штр." : ""; }
                if (i == 10) available = s.Balance.Mechanics.CorpseBlots;
                if (i == 11) available = s.Balance.Mechanics.ColorCartridges;
                string reason = !available ? "Недоступно в этой миссии" : cost > s.Ink ? "Не хватает " + (cost - s.Ink) + " штрихов" : "";
                tools[i].GetComponent<PlayerTooltip>().Message = (i < 4 ? Keys[i] + " · " + displayedNames[i] : ui.T("tool_" + i)) + (reason.Length > 0 ? " · " + reason : "");
                prices[i].color = cost > s.Ink ? PlayerUi.Muted : PlayerUi.Ink;
                if (selected == i && cost > s.Ink && input.PreviewReason.Length == 0) selection.text = reason;
                tools[i].interactable = active && available; ui.MarkSelected(tools[i], selected == i);
            }
            FitCaption(selection, toolHint.Length > 0 ? toolHint : selection.text);
            for (int i = 0; i < materials.Length; i++) { ui.MarkSelected(materials[i], (int)input.Tier == i); materials[i].interactable = active; }
            for (int i = 0; i < colors.Length; i++)
            {
                ui.MarkSelected(colors[i], (int)input.Color == i); colors[i].interactable = active && (i == 0 || s.Balance.Mechanics.ColorCartridges);
                colorAmounts[i].text = i == 0 ? "—" : s.ColoredInk((InkColor)i).ToString();
            }
            for (int i = 0; i < wells.Length; i++)
            {
                bool exists = s.Balance.Mechanics.InkWells && i < s.Wells.Count;
                wells[i].gameObject.SetActive(exists); if (!exists) continue;
                wells[i].interactable = active; ui.MarkSelected(wells[i], s.Wells[i].Active);
                wellSymbols[i].color = s.Wells[i].Color == InkColor.Red ? NotebookPalette.Red : s.Wells[i].Color == InkColor.Blue ? PlayerUi.Blue : NotebookPalette.Green;
                wellAmounts[i].color = s.Wells[i].Threatened ? NotebookPalette.Red : PlayerUi.Blue;
                wellAmounts[i].text = s.Wells[i].Active ? Mathf.CeilToInt(s.Wells[i].Health).ToString() : s.Balance.WellActivationCost.ToString();
            }
        }
    }
}
