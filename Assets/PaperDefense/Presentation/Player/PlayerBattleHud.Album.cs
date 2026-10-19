using System;
using System.Globalization;
using System.Text;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerBattleHud
    {
        bool albumHud, albumShowSummary, albumTypographyReady;
        readonly Button[] albumCards = new Button[ProfileLimits.LoadoutSlotCount];
        readonly Text[] albumNames = new Text[ProfileLimits.LoadoutSlotCount], albumPrices = new Text[ProfileLimits.LoadoutSlotCount];
        readonly string[] albumDisplayedNames = new string[ProfileLimits.LoadoutSlotCount];
        readonly Vector3[] albumNameGeometry = new Vector3[ProfileLimits.LoadoutSlotCount];
        readonly Button[] albumMaterials = new Button[3], albumColors = new Button[4], albumWells = new Button[3], albumPriorities = new Button[3];
        readonly Text[] albumColorAmounts = new Text[4], albumWellAmounts = new Text[3];
        readonly PlayerIconGraphic[] albumWellSymbols = new PlayerIconGraphic[3];
        readonly Cell[] albumQuoteCell = new Cell[1];
        readonly RectTransform[] albumApproaches = new RectTransform[4];
        readonly Text[] albumApproachCounts = new Text[4];
        readonly int[] albumOffscreenCounts = new int[4];
        readonly bool[] albumApproachSides = new bool[4];
        Text albumInk, albumHealth, albumWave, albumPhase, albumForecast, albumSpeed, albumMessage, albumSelection;
        Text albumStructureName, albumDetails, albumFieldSummary;
        Button albumLaunch, albumErase, albumUpgrade, albumPaint, albumBlotter;
        Button albumOverview;
        Text albumOverviewCaption;
        RectTransform albumContext, albumContextContent, albumPreview, albumMessageBadge;
        int albumPreviewId = -1;
        float albumContextWidth;

        static string BattleCopy(string key, params object[] args)
            => GameText.Resolve(UserText.Message("assembly.battle." + key, args));
        static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        static void FitAlbumName(Text label, string fullName)
        {
            label.text = fullName;
            int length = fullName.Length;
            while (length > 1 && label.preferredHeight > label.rectTransform.rect.height + .5f)
                label.text = fullName.Substring(0, --length).TrimEnd() + "…";
        }

        void BuildAlbumHud(RectTransform parent, PlayerLayout layout)
        {
            albumHud = true;
            float w = layout.Header.width; bool narrow = layout.Width < 1050;
            var header = ui.Panel(parent, "Battle header", layout.Header, PlayerUi.Card, true);
            Icon(header, PlayerIcon.Pencil, new Rect(10, 10, 26, 26), PlayerUi.Ink);
            albumInk = ui.Label(header, "", new Rect(42, 4, 150, 40), 19);
            albumHealth = ui.Label(header, "", new Rect(194, 4, 195, 40), 18);
            albumWave = ui.Label(header, "", new Rect(400, 2, 152, 25), 18, alignment: TextAnchor.MiddleCenter);
            float controlX = w - 310, controlY = narrow ? 48 : 6;
            albumPhase = ui.Label(header, "", narrow ? new Rect(14, 48, controlX - 24, 23) : new Rect(400, 26, 152, 22), 14, PlayerUi.Muted, alignment: narrow ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            albumForecast = ui.Label(header, "", narrow ? new Rect(14, 71, controlX - 24, 21) : new Rect(562, 4, Mathf.Max(10, controlX - 574), 42), 15, PlayerUi.Muted);
            albumLaunch = AlbumTextButton(header, "Launch wave", new Rect(controlX, controlY, 118, 40), BattleCopy("launch"), () => game.Run.NextWave(), true);
            albumLaunch.GetComponent<PlayerTooltip>().Message = BattleCopy("launch_hint");
            ui.IconButton(header, "Battle speed", new Rect(controlX + 124, controlY, 42, 40), PlayerIcon.Fast, ui.T("speed"), () => game.Run.ToggleSpeed());
            albumSpeed = ui.Label(header, "", new Rect(controlX + 170, controlY, 34, 40), 16);
            ui.IconButton(header, "Battle ranges", new Rect(controlX + 208, controlY, 40, 40), PlayerIcon.Target, ui.T("ranges"), () => game.World.ShowRanges = !game.World.ShowRanges);
            ui.IconButton(header, "Battle pause", new Rect(controlX + 258, controlY, 40, 40), PlayerIcon.Pause, ui.T("pause"), game.Pause);

            var dock = ui.Panel(parent, "Construction dock", layout.Dock, PlayerUi.Card, true);
            const float inset = 8, gap = 6, cardHeight = 128;
            float cardWidth = (w - inset * 2 - gap * 7) / ProfileLimits.LoadoutSlotCount;
            for (int slot = 0; slot < ProfileLimits.LoadoutSlotCount; slot++)
            {
                int selected = slot; var entry = game.Run.RunLoadout.Entries[slot];
                albumCards[slot] = ui.IconButton(dock, "Build tool " + slot, new Rect(inset + slot * (cardWidth + gap), inset, cardWidth, cardHeight), PlayerIcon.Book, "", () => {
                    if (!game.CanBuild) return; game.Drawing.SelectSlot(selected); Refresh();
                });
                WorkshopUi.RemoveView(albumCards[slot].GetComponentInChildren<PlayerIconGraphic>().gameObject);
                AlbumIllustration(albumCards[slot].transform, entry, new Rect(6, 2, cardWidth - 12, 51), "Battle card " + slot);
                ui.Label(albumCards[slot].transform, (slot + 1).ToString(), new Rect(5, 3, 22, 22), 16, PlayerUi.Ink);
                albumNames[slot] = ui.Label(albumCards[slot].transform, "", new Rect(5, 54, cardWidth - 10, 35), 15, alignment: TextAnchor.MiddleCenter);
                albumNames[slot].name = "Album battle name " + slot;
                albumPrices[slot] = ui.Label(albumCards[slot].transform, "", new Rect(4, 90, cardWidth - 8, 36), 14, alignment: TextAnchor.MiddleCenter);
                albumPrices[slot].name = "Album battle price " + slot;
            }
            float stripY = 144, availableWidth = w - inset * 2;
            float materialWidth = availableWidth * .32f, colorsWidth = availableWidth * .28f;
            string[] materialIds = { "pencil", "pen", "marker" };
            for (int i = 0; i < albumMaterials.Length; i++)
            {
                int index = i; float width = (materialWidth - gap * 2) / 3;
                var button = ui.IconButton(dock, "Material " + i, new Rect(inset + i * (width + gap), stripY, width, 44), PlayerIcon.Pencil, ui.T("material_" + i), () => {
                    if (!game.CanBuild) return; game.Drawing.SelectTier((MaterialTier)index); Refresh();
                }, art: "ui.material." + materialIds[i]);
                albumMaterials[i] = button;
                PlayerUi.Place(button.GetComponentInChildren<NotebookArtGraphic>().rectTransform, new Rect(5, 11, 22, 22));
                var materialLabel = ui.Label(button.transform, BattleCopy("material_" + i), new Rect(27, 2, width - 28, 40), 14, alignment: TextAnchor.MiddleCenter);
                materialLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            float colorX = inset + materialWidth + gap;
            for (int i = 0; i < albumColors.Length; i++)
            {
                int index = i; float width = (colorsWidth - gap * 3) / 4;
                albumColors[i] = ui.IconButton(dock, "Ink " + i, new Rect(colorX + i * (width + gap), stripY, width, 44), PlayerIcon.Drop, BattleCopy("color_" + i), () => {
                    if (!game.CanBuild) return; game.Drawing.SelectColor((InkColor)index); Refresh();
                });
                var icon = albumColors[i].GetComponentInChildren<PlayerIconGraphic>();
                PlayerUi.Place(icon.rectTransform, new Rect(4, 12, 19, 19));
                icon.color = i == 1 ? NotebookPalette.Red : i == 2 ? NotebookPalette.Blue : i == 3 ? NotebookPalette.Green : PlayerUi.Ink;
                albumColorAmounts[i] = ui.Label(albumColors[i].transform, "", new Rect(24, 2, width - 27, 40), 15, alignment: TextAnchor.MiddleCenter);
            }
            float actionX = colorX + colorsWidth + gap;
            float actionWidth = (w - inset - actionX - gap * 4) / 5;
            albumErase = AlbumAction(dock, "Album battle erase", new Rect(actionX, stripY, actionWidth, 44), PlayerIcon.Trash, "erase", DrawingMode.Erase, "ui.tool.erase");
            albumUpgrade = AlbumAction(dock, "Album battle upgrade", new Rect(actionX + (actionWidth + gap), stripY, actionWidth, 44), PlayerIcon.Plus, "upgrade", DrawingMode.Upgrade, "ui.tool.upgrade");
            albumBlotter = AlbumAction(dock, "Album battle blotter", new Rect(actionX + (actionWidth + gap) * 2, stripY, actionWidth, 44), PlayerIcon.Drop, "blotter", DrawingMode.Blotter, "ui.tool.blotter");
            albumPaint = AlbumAction(dock, "Album battle paint", new Rect(actionX + (actionWidth + gap) * 3, stripY, actionWidth, 44), PlayerIcon.Pencil, "paint", DrawingMode.Paint, "ui.tool.paint");
            ui.IconButton(dock, "Battle context toggle", new Rect(actionX + (actionWidth + gap) * 4, stripY, actionWidth, 44), PlayerIcon.Help, BattleCopy("summary"), () => { albumShowSummary = !albumShowSummary; Refresh(); });
            albumSelection = ui.Label(parent, "", new Rect(180, layout.Dock.y - 20, layout.Width - 360, 20), 15, PlayerUi.Ink, alignment: TextAnchor.MiddleCenter);
            albumSelection.name = "Battle tool hint";
            float messageWidth = Mathf.Min(440, layout.Field.width * .42f);
            albumMessageBadge = ui.Panel(parent, "Battle message paper", new Rect(layout.Field.x + 8, layout.Field.yMax - 94, messageWidth, 42), PlayerUi.Card);
            albumMessage = ui.Label(albumMessageBadge, "", new Rect(10, 3, messageWidth - 20, 36), 16, PlayerUi.Ink, alignment: TextAnchor.MiddleLeft);
            albumOverview = AlbumTextButton(parent, "Battle overview", new Rect(layout.Field.x + 8, layout.Field.yMax - 42, 118, 34),
                BattleCopy("overview"), () => { if (!game.CanBuild) return; game.Drawing.SetBattleView(game.Drawing.Zoom > 1.01f); Refresh(); });
            albumOverview.GetComponent<PlayerTooltip>().Message = BattleCopy("overview_hint");
            albumOverviewCaption = albumOverview.GetComponentInChildren<Text>();
            BuildApproachIndicators(parent, layout.Field);
            for (int i = 0; i < albumWells.Length; i++)
            {
                int index = i;
                albumWells[i] = ui.IconButton(parent, "Ink well " + i, new Rect(layout.Field.x + 6 + i * 78, layout.Field.y + 4, 72, 32), PlayerIcon.Drop, ui.T("well"), () => ActivateWell(index));
                albumWellSymbols[i] = albumWells[i].GetComponentInChildren<PlayerIconGraphic>();
                PlayerUi.Place(albumWellSymbols[i].rectTransform, new Rect(5, 5, 22, 22));
                albumWellAmounts[i] = ui.Label(albumWells[i].transform, "", new Rect(29, 2, 39, 28), 15);
            }
            albumContextWidth = Mathf.Min(340, layout.Field.width * .34f);
            float contextHeight = Mathf.Min(540, layout.Field.height - 16);
            albumContext = ui.Panel(parent, "Battle context", new Rect(layout.Field.xMax - albumContextWidth - 8, layout.Field.y + 8, albumContextWidth, contextHeight), PlayerUi.Card, true);
            albumStructureName = ui.Label(albumContext, "", new Rect(12, 4, albumContextWidth - 64, 44), 22, title: true);
            albumStructureName.name = "Album battle structure name";
            albumStructureName.resizeTextForBestFit = true; albumStructureName.resizeTextMinSize = 16; albumStructureName.resizeTextMaxSize = 22;
            ui.IconButton(albumContext, "Close battle context", new Rect(albumContextWidth - 44, 8, 32, 32), PlayerIcon.Back, BattleCopy("close_details"), () => {
                albumShowSummary = false; game.Drawing.Inspect(null); Refresh();
            });
            albumContextContent = ui.Scroll(albumContext, "Selected structure details", new Rect(10, 52, albumContextWidth - 20, Mathf.Max(1, contextHeight - 112)), 360);
            albumPreview = ui.Panel(albumContextContent, "Album battle inspected preview", new Rect(4, 0, albumContextWidth - 44, 104), Color.clear);
            albumDetails = ui.Label(albumContextContent, "", new Rect(4, 110, albumContextWidth - 48, 200), 17);
            albumDetails.name = "Album battle structure details";
            albumFieldSummary = ui.Label(albumContextContent, "", new Rect(4, 320, albumContextWidth - 48, 100), 16, PlayerUi.Muted);
            float priorityWidth = (albumContextWidth - 32) / 3;
            for (int i = 0; i < albumPriorities.Length; i++)
            {
                int index = i;
                albumPriorities[i] = AlbumTextButton(albumContext, "Album battle priority " + i,
                    new Rect(10 + i * (priorityWidth + 6), contextHeight - 52, priorityWidth, 42), BattleCopy("priority_" + i), () => {
                        if (!game.CanBuild || !game.Drawing.InspectedCell.HasValue) return;
                        game.Run.SetPriority(game.Drawing.InspectedCell.Value, (TargetPriority)index); Refresh();
                    });
            }
            albumContext.gameObject.SetActive(false);
        }

        void BuildApproachIndicators(Transform parent, Rect field)
        {
            if (game.Run.Session.Battlefield != null)
                foreach (var cell in game.Run.Session.Battlefield.Entrances)
                {
                    if (cell.X == 0) albumApproachSides[0] = true;
                    if (cell.X == game.Run.Session.Balance.Width - 1) albumApproachSides[1] = true;
                    if (cell.Y == game.Run.Session.Balance.Height - 1) albumApproachSides[2] = true;
                    if (cell.Y == 0) albumApproachSides[3] = true;
                }
            var positions = new[] { new Vector2(field.x + 8, field.center.y - 15), new Vector2(field.xMax - 72, field.center.y - 15),
                new Vector2(field.center.x - 32, field.y + 8), new Vector2(field.center.x - 32, field.yMax - 34) };
            for (int side = 0; side < 4; side++)
            {
                var panel = ui.Panel(parent, "Battle approach " + side, new Rect(positions[side], new Vector2(64, 30)), PlayerUi.Card);
                albumApproaches[side] = panel;
                var arrow = PlayerUi.Graphic<PlayerIconGraphic>(panel, "Enemy approach arrow", new Rect(3, 1, 30, 28));
                arrow.Icon = PlayerIcon.Next; arrow.Sketch = true; arrow.color = NotebookPalette.Red;
                arrow.rectTransform.pivot = new Vector2(.5f, .5f); arrow.rectTransform.anchoredPosition = new Vector2(18, -15);
                arrow.rectTransform.localEulerAngles = new Vector3(0, 0, side == 0 ? 0 : side == 1 ? 180 : side == 2 ? -90 : 90);
                albumApproachCounts[side] = ui.Label(panel, "", new Rect(34, 0, 28, 30), 16, NotebookPalette.Red, alignment: TextAnchor.MiddleCenter);
            }
        }

        void RefreshApproachIndicators(IRunReadModel session)
        {
            for (int side = 0; side < 4; side++) albumOffscreenCounts[side] = 0;
            var camera = game.World.WorldCamera;
            if (camera != null)
                foreach (var enemy in session.Enemies)
                {
                    var point = camera.WorldToViewportPoint(new Vector3(enemy.Position.X, enemy.Position.Y, 0));
                    int side = point.x < 0 ? 0 : point.x > 1 ? 1 : point.y > 1 ? 2 : point.y < 0 ? 3 : -1;
                    if (side >= 0) albumOffscreenCounts[side]++;
                }
            bool focused = game.Drawing.Zoom > 1.01f;
            for (int side = 0; side < 4; side++)
            {
                albumApproaches[side].gameObject.SetActive(focused && (albumApproachSides[side] || albumOffscreenCounts[side] > 0));
                albumApproachCounts[side].text = albumOffscreenCounts[side] > 0 ? albumOffscreenCounts[side].ToString() : "";
            }
            albumOverviewCaption.text = BattleCopy(focused ? "overview" : "castle_focus");
            albumOverview.interactable = game.CanBuild;
        }

        Button AlbumAction(Transform parent, string name, Rect bounds, PlayerIcon icon, string copy, DrawingMode mode, string art)
            => ui.IconButton(parent, name, bounds, icon, BattleCopy(copy), () => {
                if (!game.CanBuild) return; game.Drawing.SelectMode(mode); Refresh();
            }, art: art);

        Button AlbumTextButton(Transform parent, string name, Rect bounds, string copy, Action action, bool primary = false)
        {
            var button = ui.IconButton(parent, name, bounds, PlayerIcon.Target, copy, action, primary);
            WorkshopUi.RemoveView(button.GetComponentInChildren<PlayerIconGraphic>().gameObject);
            var text = ui.Label(button.transform, copy, new Rect(3, 2, bounds.width - 6, bounds.height - 4), 15, primary ? PlayerUi.Card : PlayerUi.Ink, alignment: TextAnchor.MiddleCenter);
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 14; text.resizeTextMaxSize = 16;
            return button;
        }

        void AlbumIllustration(Transform parent, AlbumEntry entry, Rect bounds, string identity)
        {
            if (entry == null) return;
            if (entry.Kind == AlbumEntryKind.Tower || entry.Kind == AlbumEntryKind.Structure)
            {
                var exact = PlayerUi.Graphic<WeaponAssemblyGraphic>(parent, identity + " certified assembly", bounds);
                try { exact.SetRecipe(entry.Snapshot); return; }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
                catch (NotSupportedException) { }
                WorkshopUi.RemoveView(exact.gameObject);
            }
            var artBounds = new Rect(bounds.x, bounds.y, bounds.width, Mathf.Max(1, bounds.height - 16));
            if (entry.Kind == AlbumEntryKind.Legacy) ui.Blueprint(parent, entry.LegacyBlueprint, artBounds);
            else if (entry.Kind == AlbumEntryKind.Tower)
            {
                string mechanism = entry.Snapshot.Recipe.Tower.Mounts[0].Weapon.Mechanism.Id;
                ui.Art(parent, "ui.weapon." + (mechanism.StartsWith("core.", StringComparison.Ordinal) ? mechanism.Substring(5) : mechanism), artBounds);
            }
            else if (entry.Snapshot.Recipe.Structure.Family == AssemblyStructureFamily.Generator)
            {
                var symbol = PlayerUi.Graphic<WorkshopIconGraphic>(parent, identity + " generator schematic", artBounds);
                symbol.Icon = WorkshopIcon.Ink; symbol.color = PlayerUi.Blue;
            }
            else
            {
                var family = entry.Snapshot.Recipe.Structure.Family;
                ui.Art(parent, family == AssemblyStructureFamily.Wall ? "object.barricade-site" : family == AssemblyStructureFamily.Repair ? "object.repair" : "object.tar", artBounds);
            }
            var caption = ui.Label(parent, BattleCopy("schematic"), new Rect(bounds.x, bounds.yMax - 16, bounds.width, 16), 12, PlayerUi.Muted, alignment: TextAnchor.MiddleCenter);
            caption.name = identity + " schematic caption";
        }

        void RefreshAlbumHud()
        {
            if (!albumTypographyReady)
            {
                albumTypographyReady = true;
                // ApplyLayout publishes the reference resolution before building this HUD.
                // Resolve the resulting canvas geometry before caching any text measurements.
                Canvas.ForceUpdateCanvases();
            }
            var s = game.Run.Session; var input = game.Drawing; bool active = game.CanBuild;
            RefreshApproachIndicators(s);
            albumInk.text = BattleCopy("ink", s.Ink); albumHealth.text = BattleCopy("castle", Mathf.CeilToInt(s.BaseHealth), Mathf.CeilToInt(s.Balance.BaseHealth));
            albumWave.text = ui.T("wave", s.WaveIndex, s.Balance.Waves.Count);
            albumPhase.text = game.Finished ? BattleCopy("completed") : s.PendingUpgrade ? BattleCopy("upgrade_choice") : s.Phase == RunPhase.Assault ? ui.T("enemies", s.Enemies.Count + s.RemainingToSpawn) : ui.T("planning", Mathf.CeilToInt(s.Countdown));
            albumForecast.text = s.WaveIndex < s.Balance.Waves.Count ? BattleCopy("next_wave", s.Balance.Waves[s.WaveIndex].Count, GameText.Resolve(s.Balance.Waves[s.WaveIndex].Title)) : ui.T("final_wave");
            albumSpeed.text = "×" + game.Run.Speed;
            albumLaunch.interactable = active && !s.PendingUpgrade && (s.Phase == RunPhase.Planning || s.Phase == RunPhase.Intermission);
            albumMessage.text = game.Run.Workshop.HasUnsavedChanges ? GameText.Resolve(game.Run.Workshop.SaveMessage) : input.Message;
            albumMessageBadge.gameObject.SetActive(!string.IsNullOrEmpty(albumMessage.text));
            string selectedName = input.BlueprintSlot >= 0 && input.BlueprintSlot < game.Run.RunLoadout.Entries.Count && game.Run.RunLoadout.Entries[input.BlueprintSlot] != null
                ? GameText.Resolve(game.Run.RunLoadout.Entries[input.BlueprintSlot].Name) : ui.T("empty");
            if (input.Mode != DrawingMode.Build) selectedName = BattleCopy(input.Mode == DrawingMode.Erase ? "erase" : input.Mode == DrawingMode.Upgrade ? "upgrade" : input.Mode == DrawingMode.Paint ? "paint" : "blotter");
            albumSelection.text = toolHint.Length > 0 ? toolHint : input.PreviewReason.Length > 0 ? input.PreviewReason : ui.T("selection", selectedName, BattleCopy("material_" + (int)input.Tier));
            for (int i = 0; i < albumCards.Length; i++)
            {
                var entry = game.Run.RunLoadout.Entries[i]; var resolved = game.Run.RunLoadout.Resolved[i];
                bool generator = resolved?.Support?.Kind == AssemblyStructureFamily.Generator;
                MaterialTier tier = generator ? MaterialTier.Pencil : input.Tier;
                int cost = entry == null ? 0 : game.Run.RunLoadout.ResolveSlot(i, tier).Cost;
                if (entry != null && input.HasHover)
                {
                    albumQuoteCell[0] = input.HoverCell;
                    var quote = game.Run.QuoteEntry(albumQuoteCell, i, tier, entry.Kind == AlbumEntryKind.Structure ? InkColor.None : input.Color);
                    if (quote.Cost > 0) cost = quote.Cost;
                }
                string name = entry == null ? ui.T("empty") : GameText.Resolve(entry.Name);
                var bounds = albumNames[i].rectTransform.rect;
                var geometry = new Vector3(bounds.width, bounds.height, albumNames[i].pixelsPerUnit);
                if (albumDisplayedNames[i] != name || albumNameGeometry[i] != geometry ||
                    albumNames[i].preferredHeight > bounds.height + .5f)
                {
                    albumDisplayedNames[i] = name;
                    albumNameGeometry[i] = geometry;
                    FitAlbumName(albumNames[i], name);
                }
                albumPrices[i].text = entry == null ? "—" : BattleCopy("cost", cost) + (generator ? "\n" + BattleCopy("pencil_card") : "");
                albumPrices[i].color = cost > s.Ink ? NotebookPalette.Red : PlayerUi.Ink;
                string reason = entry == null ? BattleCopy("unavailable") : cost > s.Ink ? BattleCopy("shortage", cost - s.Ink) : "";
                albumCards[i].GetComponent<PlayerTooltip>().Message = (i + 1) + " · " + name + (generator ? " · " + BattleCopy("pencil_only") : "") + (reason.Length > 0 ? " · " + reason : "");
                albumCards[i].interactable = active && entry != null;
                ui.MarkSelected(albumCards[i], input.Mode == DrawingMode.Build && input.BlueprintSlot == i);
            }
            for (int i = 0; i < albumMaterials.Length; i++) { albumMaterials[i].interactable = active; ui.MarkSelected(albumMaterials[i], (int)input.Tier == i); }
            for (int i = 0; i < albumColors.Length; i++)
            {
                albumColors[i].interactable = active && (i == 0 || s.Balance.Mechanics.ColorCartridges);
                ui.MarkSelected(albumColors[i], (int)input.Color == i); albumColorAmounts[i].text = i == 0 ? "—" : s.ColoredInk((InkColor)i).ToString();
            }
            albumErase.interactable = albumUpgrade.interactable = active;
            albumPaint.interactable = active && s.Balance.Mechanics.ColorCartridges; albumBlotter.interactable = active && s.Balance.Mechanics.CorpseBlots;
            ui.MarkSelected(albumErase, input.Mode == DrawingMode.Erase); ui.MarkSelected(albumUpgrade, input.Mode == DrawingMode.Upgrade);
            ui.MarkSelected(albumPaint, input.Mode == DrawingMode.Paint); ui.MarkSelected(albumBlotter, input.Mode == DrawingMode.Blotter);
            for (int i = 0; i < albumWells.Length; i++)
            {
                bool exists = s.Balance.Mechanics.InkWells && i < s.Wells.Count; albumWells[i].gameObject.SetActive(exists); if (!exists) continue;
                var well = s.Wells[i]; albumWells[i].interactable = active; ui.MarkSelected(albumWells[i], well.Active);
                albumWellSymbols[i].color = well.Color == InkColor.Red ? NotebookPalette.Red : well.Color == InkColor.Blue ? PlayerUi.Blue : NotebookPalette.Green;
                albumWellAmounts[i].text = well.Active ? Mathf.CeilToInt(well.Health).ToString() : s.Balance.WellActivationCost.ToString();
                albumWellAmounts[i].color = well.Threatened ? NotebookPalette.Red : PlayerUi.Ink;
            }
            RefreshAlbumInspector(s);
        }

        void RefreshAlbumInspector(IRunReadModel s)
        {
            var inspected = game.Drawing.InspectedCell.HasValue ? s.StructureAt(game.Drawing.InspectedCell.Value) : null;
            albumContext.gameObject.SetActive(albumShowSummary || inspected.HasValue);
            if (!albumContext.gameObject.activeSelf) return;
            albumStructureName.text = BattleCopy("summary");
            string text = BattleCopy("inspect_hint"); bool tower = false;
            if (inspected.HasValue)
            {
                var structure = inspected.Value; var entry = structure.ResolvedEntry; AlbumEntry frozen = null;
                foreach (var page in game.Run.RunLoadout.Entries)
                    if (page != null && (page.Id == structure.SourceAlbumEntryId ||
                        page.Kind == AlbumEntryKind.Legacy && page.LegacyBlueprint?.Id == structure.Blueprint?.Id)) { frozen = page; break; }
                albumStructureName.text = frozen == null ? GameText.Resolve(structure.Blueprint?.Name ?? WorkshopHud.WeaponName(structure.Kind)) : GameText.Resolve(frozen.Name);
                if (albumPreviewId != structure.Id)
                {
                    for (int child = albumPreview.childCount - 1; child >= 0; child--) WorkshopUi.RemoveView(albumPreview.GetChild(child).gameObject);
                    AlbumIllustration(albumPreview, frozen, new Rect(0, 0, albumContextWidth - 44, 104), "Battle inspected");
                    albumPreviewId = structure.Id;
                    albumContextContent.parent.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
                }
                var copy = new StringBuilder();
                copy.AppendLine(BattleCopy("health", Mathf.CeilToInt(structure.Health), Mathf.CeilToInt(structure.MaxHealth)));
                copy.AppendLine(BattleCopy(structure.IsComplete ? "ready" : "building"));
                copy.AppendLine(BattleCopy("material", BattleCopy("material_" + (int)structure.Tier)));
                if (entry != null)
                {
                    copy.AppendLine(BattleCopy("price", entry.Cost));
                    if (entry.Weapons.Count > 0)
                    {
                        tower = true; copy.AppendLine(BattleCopy("load", entry.Load, entry.Capacity));
                        foreach (var weapon in entry.Weapons)
                        {
                            var mount = structure.GetMountState(weapon.SlotIndex); var cycle = mount.Cycle;
                            int rounds = cycle != null && cycle.Initialized ? cycle.RoundsRemaining : weapon.MagazineSize;
                            copy.AppendLine(BattleCopy("weapon", weapon.SlotIndex + 1, rounds, weapon.MagazineSize));
                            copy.AppendLine(BattleCopy(cycle != null && cycle.IsReloading ? "reload" : "interval", Number(mount.Cooldown)));
                            copy.AppendLine(BattleCopy("range", Number(weapon.Range)));
                        }
                    }
                    else if (entry.Support != null)
                    {
                        var support = entry.Support;
                        if (support.Kind == AssemblyStructureFamily.Generator)
                        {
                            copy.AppendLine(BattleCopy("generator_income", support.IncomePerWave));
                            copy.AppendLine(BattleCopy("generator_paid", structure.GeneratorPaidWaves));
                            copy.AppendLine(BattleCopy("generator_rule"));
                            copy.AppendLine(BattleCopy("pencil_only")); copy.AppendLine(BattleCopy("refund", 0));
                        }
                        else if (support.Kind == AssemblyStructureFamily.Repair)
                        { copy.AppendLine(BattleCopy("repair", Number(support.RepairPerSecond))); copy.AppendLine(BattleCopy("radius", Number(support.Radius))); }
                        else if (support.Kind == AssemblyStructureFamily.Slow)
                        { copy.AppendLine(BattleCopy("slow", Number(support.SlowSpeedMultiplier), Number(support.SlowSeconds))); copy.AppendLine(BattleCopy("radius", Number(support.Radius))); }
                    }
                }
                text = copy.ToString().TrimEnd();
                for (int i = 0; i < albumPriorities.Length; i++) { albumPriorities[i].interactable = tower && game.CanBuild; ui.MarkSelected(albumPriorities[i], (int)structure.Priority == i); }
            }
            albumPreview.gameObject.SetActive(inspected.HasValue);
            albumDetails.text = text;
            float y = inspected.HasValue ? 110 : 0, height = Mathf.Ceil(albumDetails.preferredHeight);
            PlayerUi.Place(albumDetails.rectTransform, new Rect(4, y, albumContextWidth - 48, height));
            albumFieldSummary.gameObject.SetActive(albumShowSummary);
            albumFieldSummary.text = ui.T("field_stats", s.Enemies.Count, s.Kills, s.Structures.Count);
            float summaryHeight = albumShowSummary ? Mathf.Ceil(albumFieldSummary.preferredHeight) : 0;
            PlayerUi.Place(albumFieldSummary.rectTransform, new Rect(4, y + height + 12, albumContextWidth - 48, summaryHeight));
            float viewportHeight = albumContextContent.parent.GetComponent<ScrollRect>().viewport.rect.height;
            albumContextContent.sizeDelta = new Vector2(albumContextContent.sizeDelta.x, Mathf.Max(viewportHeight, y + height + (albumShowSummary ? summaryHeight + 12 : 0)));
            for (int i = 0; i < albumPriorities.Length; i++) albumPriorities[i].gameObject.SetActive(tower);
        }
    }
}
