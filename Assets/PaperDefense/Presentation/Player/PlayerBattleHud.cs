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
        readonly Text ink, health, wave, phase, message, selection, speedValue, forecast, fieldStats;
        readonly Button launch;
        readonly Button[] tools = new Button[12], materials = new Button[3], colors = new Button[4];
        readonly Text[] prices = new Text[12], colorAmounts = new Text[4];
        readonly BlueprintSketch[] sketches = new BlueprintSketch[4];
        readonly Button[] wells = new Button[3];
        readonly Text[] wellAmounts = new Text[3];
        readonly PlayerIconGraphic[] wellSymbols = new PlayerIconGraphic[3];
        static readonly StructureKind[] Construction = { StructureKind.Wall, StructureKind.Spikes, StructureKind.TarPit, StructureKind.RepairPost };
        static readonly string[] ToolIds = { "wall", "spikes", "tar", "repair", "erase", "upgrade", "blotter", "paint" };

        public PlayerBattleHud(PlayerGameRoot owner, PlayerUi style, RectTransform root)
        {
            game = owner; ui = style;
            var top = ui.Panel(root, "Battle header", new Rect(30, 24, 1540, 82), PlayerUi.Card, true);
            ui.Label(top, ui.T("mission"), new Rect(25, 12, 255, 56), 28, title: true);
            Icon(top, PlayerIcon.Drop, new Rect(310, 24, 30, 34), PlayerUi.Blue);
            ink = ui.Label(top, "", new Rect(355, 16, 150, 46), 30, PlayerUi.Blue);
            Icon(top, PlayerIcon.Heart, new Rect(535, 25, 29, 30), NotebookPalette.Red);
            health = ui.Label(top, "", new Rect(580, 16, 150, 46), 25);
            wave = ui.Label(top, "", new Rect(760, 10, 250, 36), 23);
            phase = ui.Label(top, "", new Rect(760, 46, 280, 26), 16, PlayerUi.Muted);
            launch = ui.IconButton(top, "Launch wave", new Rect(1080, 12, 74, 58), PlayerIcon.Flag, ui.T("next_wave"), () => game.Run.NextWave(), true);
            ui.IconButton(top, "Battle speed", new Rect(1170, 12, 74, 58), PlayerIcon.Fast, ui.T("speed"), () => game.Run.ToggleSpeed());
            speedValue = ui.Label(top, "", new Rect(1246, 25, 58, 30), 18, PlayerUi.Blue);
            ui.IconButton(top, "Battle ranges", new Rect(1312, 12, 74, 58), PlayerIcon.Target, ui.T("ranges"), () => game.World.ShowRanges = !game.World.ShowRanges);
            ui.IconButton(top, "Battle pause", new Rect(1402, 12, 112, 58), PlayerIcon.Pause, ui.T("pause"), game.Pause);

            var dock = ui.Panel(root, "Construction dock", new Rect(150, 814, 1300, 138), PlayerUi.Paper, true);
            for (int i = 0; i < tools.Length; i++)
            {
                int index = i;
                tools[i] = ui.IconButton(dock, "Build tool " + i, new Rect(18 + i * 106, 15, 98, 100), PlayerIcon.Book,
                    ui.T("tool_" + i), () => Select(index), art: i < 4 ? null : "ui.tool." + ToolIds[i - 4]);
                if (i < 4)
                {
                    var icon = tools[i].GetComponentInChildren<PlayerIconGraphic>(); Object.Destroy(icon.gameObject);
                    sketches[i] = ui.Blueprint(tools[i].transform, null, new Rect(18, 8, 62, 60));
                    ui.Label(tools[i].transform, (i + 1).ToString(), new Rect(7, 3, 20, 25), 13, PlayerUi.Muted);
                }
                else
                {
                    var graphic = tools[i].GetComponentInChildren<NotebookArtGraphic>();
                    PlayerUi.Place(graphic.rectTransform, new Rect(22, 8, 54, 60));
                }
                prices[i] = ui.Label(tools[i].transform, "", new Rect(5, 73, 88, 23), 17, PlayerUi.Blue, alignment: TextAnchor.MiddleCenter);
            }
            selection = ui.Label(root, "", new Rect(165, 765, 1250, 36), 19, PlayerUi.Ink, alignment: TextAnchor.MiddleCenter);
            message = ui.Label(root, "", new Rect(300, 115, 1000, 32), 18, PlayerUi.Blue, alignment: TextAnchor.MiddleCenter);

            var materialPanel = ui.Panel(root, "Battle materials", new Rect(30, 814, 100, 138), PlayerUi.Card, true);
            string[] ids = { "pencil", "pen", "marker" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                materials[i] = ui.IconButton(materialPanel, "Material " + i, new Rect(7, 5 + i * 43, 86, 40), PlayerIcon.Drop, ui.T("material_" + i), () => game.Drawing.SelectTier((MaterialTier)index), art: "ui.material." + ids[i]);
            }
            var colorPanel = ui.Panel(root, "Battle inks", new Rect(1470, 814, 100, 138), PlayerUi.Card, true);
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                colors[i] = ui.IconButton(colorPanel, "Ink " + i, new Rect(5, 3 + i * 33, 90, 31), PlayerIcon.Drop, ui.T("color_" + i), () => game.Drawing.SelectColor((InkColor)index));
                var symbol = colors[i].GetComponentInChildren<PlayerIconGraphic>();
                PlayerUi.Place(symbol.rectTransform, new Rect(9, 5, 20, 20));
                symbol.color = i == 1 ? NotebookPalette.Red : i == 2 ? PlayerUi.Blue : i == 3 ? NotebookPalette.Green : PlayerUi.Ink;
                colorAmounts[i] = ui.Label(colors[i].transform, "", new Rect(35, 3, 48, 25), 15);
            }
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                wells[i] = ui.IconButton(root, "Ink well " + i, new Rect(36, 160 + i * 74, 78, 62), PlayerIcon.Drop, ui.T("well"), () => ActivateWell(index));
                wellAmounts[i] = ui.Label(wells[i].transform, "", new Rect(3, 43, 72, 17), 12, PlayerUi.Blue, alignment: TextAnchor.MiddleCenter);
                wellSymbols[i] = wells[i].GetComponentInChildren<PlayerIconGraphic>();
            }
            var next = ui.Panel(root, "Wave forecast", new Rect(1300, 158, 270, 260), PlayerUi.Card, true);
            ui.Label(next, ui.T("forecast_title"), new Rect(20, 16, 230, 40), 26, title: true);
            forecast = ui.Label(next, "", new Rect(20, 70, 230, 165), 19, PlayerUi.Muted);
            var notes = ui.Panel(root, "Field statistics", new Rect(1300, 446, 270, 220), PlayerUi.Card, true);
            ui.Label(notes, ui.T("field_title"), new Rect(20, 16, 230, 40), 26, title: true);
            fieldStats = ui.Label(notes, "", new Rect(20, 70, 230, 125), 19, PlayerUi.Muted);
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
            phase.text = s.Phase == RunPhase.Assault ? ui.T("enemies", s.Enemies.Count + s.RemainingToSpawn) : ui.T("planning", Mathf.CeilToInt(s.Countdown));
            int nextWave = s.WaveIndex;
            if (nextWave < s.Balance.Waves.Count)
            {
                var next = s.Balance.Waves[nextWave];
                forecast.text = ui.T("forecast", nextWave + 1, next.Count, GameText.Resolve(next.Title));
            }
            else forecast.text = ui.T("final_wave");
            fieldStats.text = ui.T("field_stats", s.Enemies.Count, s.Kills, s.Structures.Count);
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
