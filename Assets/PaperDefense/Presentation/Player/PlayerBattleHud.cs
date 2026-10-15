using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Compact player HUD, backed by the same commands as the development HUD.</summary>
    public sealed class PlayerBattleHud
    {
        readonly PlayerGameRoot game;
        readonly PlayerUi ui;
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

        public PlayerBattleHud(PlayerGameRoot owner, PlayerUi style, RectTransform root, PlayerLayout layout)
        {
            game = owner; ui = style;
            float w = layout.Width, h = layout.Height;
            bool compact = w < 1100;
            bool narrow = w < 900;
            var top = ui.Panel(root, "Battle header", new Rect(16, 16, w - 32, narrow ? 136 : 88), PlayerUi.Card, true);
            float labelW = compact ? 130 : 210;
            if (!narrow) {
                ui.Label(top, compact ? "Рубеж 01" : ui.T("mission"), new Rect(16, 12, labelW, 40), compact ? 23 : 28, title: true);
                ui.Label(top, compact ? "ОБОРОНА" : "ОБОРОНА СТРАНИЦЫ", new Rect(16, 52, labelW, 22), 12, PlayerUi.Muted);
            }
            float resourceX = narrow ? 16 : compact ? 158 : 250;
            ink = ui.Label(top, "", new Rect(resourceX, 12, 112, 38), 27, PlayerUi.Blue);
            ui.Label(top, "штрихов", new Rect(resourceX, 52, 110, 22), 14, PlayerUi.Muted);
            health = ui.Label(top, "", new Rect(resourceX + 125, 12, 140, 38), 22);
            ui.Label(top, "прочность замка", new Rect(resourceX + 125, 52, 160, 22), 14, PlayerUi.Muted);
            wave = ui.Label(top, "", new Rect(resourceX + 285, 12, 180, 32), 21);
            phase = ui.Label(top, "", new Rect(resourceX + 285, 48, 190, 28), 15, PlayerUi.Muted);
            float controlsX = w - 370;
            launch = ui.IconButton(top, "Launch wave", new Rect(controlsX, 16, 62, 54), PlayerIcon.Flag, ui.T("next_wave"), () => game.Run.NextWave(), true);
            ui.IconButton(top, "Battle speed", new Rect(controlsX + 72, 16, 58, 54), PlayerIcon.Fast, ui.T("speed"), () => game.Run.ToggleSpeed());
            speedValue = ui.Label(top, "", new Rect(controlsX + 132, 26, 46, 30), 18, PlayerUi.Blue);
            ui.IconButton(top, "Battle ranges", new Rect(controlsX + 184, 16, 54, 54), PlayerIcon.Target, ui.T("ranges"), () => game.World.ShowRanges = !game.World.ShowRanges);
            ui.IconButton(top, "Battle pause", new Rect(controlsX + 250, 16, 70, 54), PlayerIcon.Pause, ui.T("pause"), game.Pause);
            if (narrow)
            {
                foreach (var button in top.GetComponentsInChildren<Button>())
                { var rect = (RectTransform)button.transform; rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -76); }
                speedValue.rectTransform.anchoredPosition = new Vector2(speedValue.rectTransform.anchoredPosition.x, -86);
                PlayerUi.Place(phase.rectTransform, new Rect(16, 84, 210, 32));
            }

            int columns = compact ? 6 : 12;
            float dockH = narrow ? 136 : compact ? 214 : 158, dockY = h - dockH - 16;
            var dock = ui.Panel(root, "Construction dock", new Rect(16, dockY, w - 32, dockH), PlayerUi.Card, true);
            float toolW = (w - 64) / columns;
            Transform toolParent = dock;
            if (narrow)
            {
                var row = ui.Scroll(dock, "Tools horizontal scroll", new Rect(16, 4, w - 64, 88), 88);
                var scroll = row.parent.GetComponent<ScrollRect>();
                scroll.verticalScrollbar.gameObject.SetActive(false); scroll.verticalScrollbar = null;
                scroll.vertical = false; scroll.horizontal = true; row.sizeDelta = new Vector2(12 * 104, 88);
                PlayerUi.HorizontalScrollbar(scroll);
                toolParent = row; toolW = 104;
            }
            string[] captions = { "Башня 1", "Башня 2", "Башня 3", "Башня 4", "Стена", "Шипы", "Смола", "Ремонт", "Ластик", "Улучшить", "Промокашка", "Окраска" };
            for (int i = 0; i < tools.Length; i++)
            {
                int index = i;
                float cellH = narrow ? 84 : compact ? 74 : 100, x = narrow ? i * toolW : 16 + i % columns * toolW, y = narrow ? 0 : 10 + i / columns * 78;
                tools[i] = ui.IconButton(toolParent, "Build tool " + i, new Rect(x, y, toolW - 8, cellH), PlayerIcon.Book,
                    ui.T("tool_" + i), () => Select(index), art: i < 4 ? null : "ui.tool." + ToolIds[i - 4]);
                float iconH = compact ? 35 : 48;
                if (i < 4)
                {
                    var icon = tools[i].GetComponentInChildren<PlayerIconGraphic>(); Object.Destroy(icon.gameObject);
                    sketches[i] = ui.Blueprint(tools[i].transform, null, new Rect((toolW - 58) / 2, 3, 50, iconH));
                }
                else PlayerUi.Place(tools[i].GetComponentInChildren<NotebookArtGraphic>().rectTransform, new Rect((toolW - 58) / 2, 3, 50, iconH));
                ui.Label(tools[i].transform, captions[i], new Rect(2, iconH + 2, toolW - 12, 22), compact ? 13 : 14, alignment: TextAnchor.MiddleCenter);
                prices[i] = ui.Label(tools[i].transform, "", new Rect(2, cellH - 23, toolW - 12, 21), 14, PlayerUi.Blue, alignment: TextAnchor.MiddleCenter);
            }
            float stripY = dockH - 42;
            var materialPanel = ui.Panel(dock, "Battle materials", new Rect(16, stripY, 290, 36), PlayerUi.Wash, true);
            string[] ids = { "pencil", "pen", "marker" };
            string[] materialNames = { "Карандаш", "Ручка", "Маркер" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                materials[i] = ui.IconButton(materialPanel, "Material " + i, new Rect(i * 94 + 4, 0, 90, 36), PlayerIcon.Drop,
                    ui.T("material_" + i), () => game.Drawing.SelectTier((MaterialTier)index), art: "ui.material." + ids[i]);
                PlayerUi.Place(materials[i].GetComponentInChildren<NotebookArtGraphic>().rectTransform, new Rect(8, 5, 24, 26));
                ui.Label(materials[i].transform, materialNames[i], new Rect(27, 3, 60, 30), 12);
            }
            var colorPanel = ui.Panel(dock, "Battle inks", new Rect(w - 392, stripY, 340, 36), PlayerUi.Wash, true);
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                colors[i] = ui.IconButton(colorPanel, "Ink " + i, new Rect(i * 84, 0, 80, 36), PlayerIcon.Drop, ui.T("color_" + i), () => game.Drawing.SelectColor((InkColor)index));
                var symbol = colors[i].GetComponentInChildren<PlayerIconGraphic>();
                PlayerUi.Place(symbol.rectTransform, new Rect(8, 8, 20, 20));
                symbol.color = i == 1 ? NotebookPalette.Red : i == 2 ? PlayerUi.Blue : i == 3 ? NotebookPalette.Green : PlayerUi.Ink;
                colorAmounts[i] = ui.Label(colors[i].transform, "", new Rect(36, 4, 42, 28), 15);
            }
            selection = ui.Label(root, "", new Rect(100, dockY - 42, w - 140, 32), 17, PlayerUi.Ink, alignment: TextAnchor.MiddleCenter);
            message = ui.Label(root, "", new Rect(100, narrow ? 152 : 104, w - 200, 28), 16, PlayerUi.Blue, alignment: TextAnchor.MiddleCenter);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                wells[i] = ui.IconButton(root, "Ink well " + i, new Rect(16, narrow ? 180 + i * 48 : 144 + i * 84, 70, narrow ? 44 : 72), PlayerIcon.Drop, ui.T("well"), () => ActivateWell(index));
                wellAmounts[i] = ui.Label(wells[i].transform, "", new Rect(3, 48, 64, 20), 14, PlayerUi.Blue, alignment: TextAnchor.MiddleCenter);
                wellSymbols[i] = wells[i].GetComponentInChildren<PlayerIconGraphic>();
                PlayerUi.Place(wellSymbols[i].rectTransform, new Rect(20, 8, 30, 32));
                if (narrow) {
                    PlayerUi.Place(wellSymbols[i].rectTransform, new Rect(5, 8, 22, 26));
                    PlayerUi.Place(wellAmounts[i].rectTransform, new Rect(30, 8, 36, 26));
                }
            }
            var context = ui.Panel(root, "Battle context", new Rect(w - 250, layout.Field.y + 12, 230, Mathf.Max(100, layout.Field.height - 12)), PlayerUi.Card, true);
            var body = ui.Scroll(context, "Battle context scroll", new Rect(12, 12, 206, Mathf.Max(76, layout.Field.height - 36)), 570);
            ui.Label(body, ui.T("forecast_title"), new Rect(4, 0, 198, 42), 25, title: true);
            forecast = ui.Label(body, "", new Rect(4, 54, 198, 130), 18, PlayerUi.Muted);
            ui.Label(body, ui.T("field_title"), new Rect(4, 204, 198, 40), 25, title: true);
            fieldStats = ui.Label(body, "", new Rect(4, 254, 198, 106), 18, PlayerUi.Muted);
            ui.Label(body, "Под курсором", new Rect(4, 382, 198, 42), 23, title: true);
            towerInfo = ui.Label(body, "Наведи курсор на постройку", new Rect(4, 434, 198, 130), 17, PlayerUi.Muted);
            if (compact)
            {
                context.gameObject.SetActive(false);
                ui.IconButton(root, "Battle context toggle", new Rect(narrow ? w - 88 : 16, narrow ? 178 : 410, 70, 48), PlayerIcon.Help, "Сводка и башня", () => context.gameObject.SetActive(!context.gameObject.activeSelf));
            }
        }

        void Icon(Transform parent, PlayerIcon icon, Rect rect, Color color)
        { var graphic = PlayerUi.Graphic<PlayerIconGraphic>(parent, "Indicator", rect); graphic.Icon = icon; graphic.Sketch = ui.NotebookStyle; graphic.color = color; }

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
            var s = game.Run.Session; var input = game.Drawing;
            ink.text = s.Ink.ToString(); health.text = Mathf.CeilToInt(s.BaseHealth) + " / " + Mathf.CeilToInt(s.Balance.BaseHealth);
            wave.text = ui.T("wave", s.WaveIndex, s.Balance.Waves.Count);
            phase.text = game.Finished ? "Миссия завершена" : s.PendingUpgrade ? "Выбери усиление" :
                s.Phase == RunPhase.Assault ? ui.T("enemies", s.Enemies.Count + s.RemainingToSpawn) : ui.T("planning", Mathf.CeilToInt(s.Countdown));
            int nextWave = s.WaveIndex;
            if (nextWave < s.Balance.Waves.Count)
            {
                var next = s.Balance.Waves[nextWave];
                forecast.text = ui.T("forecast", nextWave + 1, next.Count, GameText.Resolve(next.Title));
            }
            else forecast.text = ui.T("final_wave");
            fieldStats.text = ui.T("field_stats", s.Enemies.Count, s.Kills, s.Structures.Count);
            if (input.HasHover)
            {
                var hovered = s.StructureAt(input.HoverCell);
                towerInfo.text = hovered.HasValue
                    ? (hovered.Value.Blueprint == null ? GameText.Resolve(WorkshopHud.WeaponName(hovered.Value.Kind)) : GameText.Resolve(hovered.Value.Blueprint.Name)) +
                      "\nHP " + Mathf.CeilToInt(hovered.Value.Health) + " / " + Mathf.CeilToInt(hovered.Value.MaxHealth) +
                      "\n" + (hovered.Value.IsComplete ? "Готова к обороне" : "Строится") + "\nP — приоритет цели"
                    : "Наведи курсор на постройку";
            }
            else towerInfo.text = "Наведи курсор на постройку";
            speedValue.text = "×" + game.Run.Speed;
            bool active = game.Page == PlayerPage.Battle;
            launch.interactable = active && !s.PendingUpgrade && (s.Phase == RunPhase.Planning || s.Phase == RunPhase.Intermission);
            message.text = game.Run.Workshop.HasUnsavedChanges ? GameText.Resolve(game.Run.Workshop.SaveMessage) : input.Message;
            int selected = input.Mode == DrawingMode.Erase ? 8 : input.Mode == DrawingMode.Upgrade ? 9 : input.Mode == DrawingMode.Blotter ? 10 : input.Mode == DrawingMode.Paint ? 11 : input.BlueprintSlot >= 0 ? input.BlueprintSlot : -1;
            if (selected < 0) for (int i = 0; i < Construction.Length; i++) if (input.Tool == Construction[i]) selected = i + 4;
            selection.text = input.PreviewReason.Length > 0 ? input.PreviewReason : ui.T("selection", ui.T("tool_" + Mathf.Max(0, selected)), ui.T("material_" + (int)input.Tier));
            for (int i = 0; i < tools.Length; i++)
            {
                bool available = true;
                if (i < 4)
                {
                    var bp = i < game.Run.BuildLoadout.Count ? game.Run.BuildLoadout[i] : null;
                    sketches[i].SetBlueprint(bp); available = bp != null;
                    prices[i].text = bp == null ? "—" : game.Run.PreviewBlueprint(bp, input.Tier).Cost.ToString();
                }
                else if (i < 8)
                {
                    available = s.Balance.HasStructure(Construction[i - 4]);
                    prices[i].text = available ? s.Balance.ToolCost(Construction[i - 4], input.Tier).ToString() : "—";
                }
                else prices[i].text = i == 10 ? s.Balance.BlotterCost.ToString() : "";
                if (i == 10) available = s.Balance.Mechanics.CorpseBlots;
                if (i == 11) available = s.Balance.Mechanics.ColorCartridges;
                tools[i].interactable = active && available; ui.MarkSelected(tools[i], selected == i);
            }
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
