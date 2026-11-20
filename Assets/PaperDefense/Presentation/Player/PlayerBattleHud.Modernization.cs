using System;
using System.Linq;
using System.Text;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerBattleHud
    {
        bool modernizationHud, modernShowSummary, modernShowDetails;
        readonly Button[] modernCards = new Button[ProfileLimits.LoadoutSlotCount];
        readonly Text[] modernNames = new Text[ProfileLimits.LoadoutSlotCount], modernPrices = new Text[ProfileLimits.LoadoutSlotCount];
        readonly Button[] modernPriorities = new Button[3];
        readonly ModernizationRow[] directionRows = new ModernizationRow[4], colorRows = new ModernizationRow[3];
        Text modernTitle, modernSummary, modernWork, modernDetails, modernTacticalCaption;
        RectTransform modernContext, modernContent, modernWorkRow;
        Image modernProgress;
        Button modernFortify, modernErase, modernBlotter, modernUpgrade;
        Text modernFortifyCaption;
        PlayerIconGraphic modernTacticalGlyph, modernDetailsGlyph;
        PlayerTooltip modernSummaryHint, modernWorkHint;
        Button modernTactical, modernDetailsToggle;
        int modernInspectedId = -1;
        float modernWidth;
        Rect modernHintArea;
        TowerDevelopmentOverlay developmentOverlay;

        sealed class ModernizationRow
        {
            internal RectTransform Root;
            internal Text Title, Stats, Reason, BuyCaption;
            internal Button Buy, Info;
            internal PlayerIconGraphic Glyph;
            internal ModernizationQuote Quote;
        }

        void BuildModernizationHud(RectTransform parent, PlayerLayout layout)
        {
            albumHud = modernizationHud = true;
            float w = layout.Header.width;
            var header = ui.Panel(parent, "Battle header", layout.Header, PlayerUi.Card, true);
            MinimalGlyph(header, "Ink glyph", PlayerIcon.Pencil, new Rect(10, 15, 22, 22));
            albumInk = ui.Label(header, "", new Rect(38, 8, 54, 36), 17); albumInk.name = "Battle live ink chip";
            MinimalGlyph(header, "Health glyph", PlayerIcon.Shield, new Rect(96, 15, 22, 22));
            albumHealth = ui.Label(header, "", new Rect(123, 8, 82, 36), 16); albumHealth.name = "Battle live castle chip";
            var ribbon = ui.Panel(header, "Battle wave ribbon", new Rect(212, 8, 80, 36), FieldMenuSkin.Panel);
            MinimalGlyph(ribbon, "Wave glyph", PlayerIcon.Flag, new Rect(6, 8, 20, 20));
            albumWave = ui.Label(ribbon, "", new Rect(30, 0, 46, 36), 16);
            float controlX = w - 280;
            albumPhase = ui.Label(header, "", new Rect(298, 2, Mathf.Max(1, controlX - 302), 48), 14, PlayerUi.Muted);
            // The forecast remains available from the wave action's keyboard/pointer hint.
            albumForecast = ui.Label(header, "", new Rect(0, 0, 1, 1), 14); albumForecast.gameObject.SetActive(false);
            albumLaunch = ui.IconButton(header, "Launch wave", new Rect(controlX, 8, 36, 36), PlayerIcon.Flag, "Следующая волна · N", () => game.Run.NextWave(), true);
            var speed = ui.IconButton(header, "Battle speed", new Rect(controlX + 42, 8, 64, 36), PlayerIcon.Fast, ui.T("speed"), () => game.Run.ToggleSpeed());
            PlayerUi.Place(speed.GetComponentInChildren<PlayerIconGraphic>().rectTransform, new Rect(7, 8, 20, 20));
            albumSpeed = ui.Label(speed.transform, "", new Rect(31, 0, 29, 36), 14);
            ui.IconButton(header, "Battle ranges", new Rect(controlX + 112, 8, 36, 36), PlayerIcon.Target, ui.T("ranges"), () => game.World.ShowRanges = !game.World.ShowRanges);
            albumOverview = ui.IconButton(header, "Battle overview", new Rect(controlX + 154, 8, 36, 36), PlayerIcon.Map, BattleCopy("overview_hint"),
                () => { if (game.CanBuild) { game.Drawing.SetBattleView(game.Drawing.Zoom > 1.01f); Refresh(); } });
            albumOverviewCaption = ui.Label(albumOverview.transform, "", new Rect(0, 0, 1, 1), 14); albumOverviewCaption.gameObject.SetActive(false);
            modernTactical = ui.IconButton(header, "Tactical pause", new Rect(controlX + 196, 8, 36, 36), PlayerIcon.Pause, "Тактическая пауза · Space", () => {
                if (game.CanBuild) { game.Run.TogglePause(); Refresh(); }
            });
            modernTacticalGlyph = modernTactical.GetComponentInChildren<PlayerIconGraphic>();
            modernTacticalCaption = ui.Label(modernTactical.transform, "", new Rect(0, 0, 1, 1), 14); modernTacticalCaption.gameObject.SetActive(false);
            ui.IconButton(header, "Battle pause", new Rect(controlX + 238, 8, 36, 36), PlayerIcon.Settings, "Меню паузы · Esc", game.Pause);

            var dock = ui.Panel(parent, "Construction dock", layout.Dock, PlayerUi.Card, true);
            const float gap = 4, cardHeight = 64;
            float cardsWidth = w - 152, cardWidth = (cardsWidth - gap * 7) / 8;
            for (int i = 0; i < modernCards.Length; i++)
            {
                int slot = i;
                var card = modernCards[i] = ui.IconButton(dock, "Build tool " + i,
                    new Rect(8 + i * (cardWidth + gap), 8, cardWidth, cardHeight), PlayerIcon.Book, "",
                    () => { if (game.CanBuild) { game.Drawing.SelectSlot(slot); Refresh(); } });
                WorkshopUi.RemoveView(card.GetComponentInChildren<PlayerIconGraphic>().gameObject);
                var art = ui.Panel(card.transform, "Assembly thumbnail " + i, new Rect(8, 3, cardWidth - 16, 37), Color.clear);
                var entry = game.Run.RunLoadout.Entries[i];
                if (entry != null)
                {
                    AlbumIllustration(art, entry, new Rect(0, 0, cardWidth - 16, 37), "Battle slot " + i);
                    // A small family fallback is still a visual slot; its full name is in the hint.
                    foreach (var caption in art.GetComponentsInChildren<Text>()) caption.gameObject.SetActive(false);
                }
                ui.Label(card.transform, (i + 1).ToString(), new Rect(4, 1, 12, 17), 12, PlayerUi.Muted);
                modernNames[i] = ui.Label(card.transform, "", new Rect(0, 0, 1, 1), 14);
                modernNames[i].name = "Album battle name " + i; modernNames[i].gameObject.SetActive(false);
                modernPrices[i] = ui.Label(card.transform, "", new Rect(4, 40, cardWidth - 8, 21), 14, alignment: TextAnchor.MiddleCenter);
                modernPrices[i].name = "Album battle price " + i;
            }
            float actionX = w - 132;
            modernErase = ui.IconButton(dock, "Album battle erase", new Rect(actionX, 22, 36, 36), PlayerIcon.Eraser, "Ластик · E", () => { if (game.CanBuild) game.Drawing.SelectMode(DrawingMode.Erase); });
            modernBlotter = ui.IconButton(dock, "Album battle blotter", new Rect(actionX + 42, 22, 36, 36), PlayerIcon.Blotter, "Промокашка · B", () => { if (game.CanBuild) game.Drawing.SelectMode(DrawingMode.Blotter); });
            ui.IconButton(dock, "Battle context toggle", new Rect(actionX + 84, 22, 36, 36), PlayerIcon.Info, "Сводка поля", () => { modernShowSummary = !modernShowSummary; Refresh(); });
            albumSelection = CreateToolHint(parent, layout);
            modernHintArea = new Rect(layout.Field.x + 6, layout.Field.yMax,
                Mathf.Min(720, layout.Field.width - Mathf.Clamp(layout.Field.width * .3f, 320, 360) - 24), layout.Field.height);
            PlayerUi.Place((RectTransform)albumSelection.transform.parent, new Rect(modernHintArea.x, modernHintArea.y - 28, modernHintArea.width, 28));
            PlayerUi.Place(albumSelection.rectTransform, new Rect(10, 4, modernHintArea.width - 20, 20));
            albumSelection.fontSize = 14; albumSelection.transform.parent.gameObject.SetActive(false);
            albumMessageBadge = ui.Panel(parent, "Battle message paper", new Rect(layout.Field.x + 8, layout.Field.y + 42, Mathf.Min(380, layout.Field.width - 366), 48), PlayerUi.Card);
            albumMessage = ui.Label(albumMessageBadge, "", new Rect(8, 3, albumMessageBadge.rect.width - 16, 42), 14);
            BuildApproachIndicators(parent, layout.Field);
            if (game.Run.Session.Balance.Mechanics.WaveInkFlasks) BuildInkFlasks(parent, layout);
            else for (int i = 0; i < albumWells.Length; i++)
            {
                int index = i;
                albumWells[i] = ui.IconButton(parent, "Ink well " + i, new Rect(layout.Field.x + 6 + i * 68, layout.Field.y + 4, 62, 32), PlayerIcon.Drop, ui.T("well"), () => { if (game.CanBuild) ActivateWell(index); });
                albumWellSymbols[i] = albumWells[i].GetComponentInChildren<PlayerIconGraphic>();
                PlayerUi.Place(albumWellSymbols[i].rectTransform, new Rect(5, 6, 18, 20));
                albumWellAmounts[i] = ui.Label(albumWells[i].transform, "", new Rect(27, 2, 32, 28), 14);
            }
            modernUpgrade = AlbumTextButton(parent, "Battle upgrades",
                new Rect(layout.Field.x + 214, layout.Field.y + 4, 150, 32), "Улучшить · U", () => {
                    if (!game.CanBuild) return;
                    game.Drawing.SelectMode(DrawingMode.Upgrade); Refresh();
                });
            modernUpgrade.GetComponent<PlayerTooltip>().Message = "Улучшить башню · U\nЛКМ по башне или её отметке, затем кнопка с ценой.\n+ над башней — можно усилить сейчас; 0/6 — вложенные улучшения.\nКапля и римская цифра — закреплённый цвет и его ранг.\nSpace — тактическая пауза.";
            BuildModernizationInspector(parent, layout);
            var badges = ui.Panel(parent, "Tower development overlay", layout.Field, Color.clear);
            badges.SetAsFirstSibling();
            badges.gameObject.AddComponent<RectMask2D>();
            developmentOverlay = badges.gameObject.AddComponent<TowerDevelopmentOverlay>();
            developmentOverlay.Initialize(game, ui, layout, modernContext, albumMessageBadge, albumApproaches, Refresh, flaskReward);
        }

        PlayerIconGraphic MinimalGlyph(Transform parent, string name, PlayerIcon icon, Rect bounds)
        {
            var glyph = PlayerUi.Graphic<PlayerIconGraphic>(parent, name, bounds);
            glyph.Icon = icon; glyph.Sketch = true; glyph.color = PlayerUi.Ink; return glyph;
        }

        void RefreshMinimalHint(string value)
        {
            var plate = (RectTransform)albumSelection.transform.parent;
            value = value ?? ""; plate.gameObject.SetActive(value.Length > 0);
            albumSelection.text = value;
            if (value.Length == 0) return;
            float height = Mathf.Min(modernHintArea.height - 48, Mathf.Max(28, Mathf.Ceil(albumSelection.preferredHeight) + 8));
            PlayerUi.Place(plate, new Rect(modernHintArea.x, modernHintArea.y - height, modernHintArea.width, height));
            PlayerUi.Place(albumSelection.rectTransform, new Rect(10, 4, modernHintArea.width - 20, height - 8));
        }

        void BuildModernizationInspector(RectTransform parent, PlayerLayout layout)
        {
            modernWidth = Mathf.Clamp(layout.Field.width * .3f, 320, 360);
            float height = Mathf.Min(650, layout.Field.height - 12);
            modernContext = ui.Panel(parent, "Battle context", new Rect(layout.Field.xMax - modernWidth - 6, layout.Field.y + 6, modernWidth, height), PlayerUi.Card, true);
            modernTitle = ui.Label(modernContext, "", new Rect(10, 3, modernWidth - 56, 34), 18, title: true);
            modernTitle.name = "Album battle structure name";
            modernTitle.resizeTextForBestFit = true; modernTitle.resizeTextMinSize = 16; modernTitle.resizeTextMaxSize = 18;
            ui.IconButton(modernContext, "Close battle context", new Rect(modernWidth - 40, 5, 32, 32), PlayerIcon.Close, "Закрыть сведения", () => {
                modernShowSummary = modernShowDetails = false; game.Drawing.Inspect(null); Refresh();
            });
            modernContent = ui.Scroll(modernContext, "Selected structure details", new Rect(8, 42, modernWidth - 16, height - 88), height - 88);
            modernContent.offsetMax = new Vector2(modernContent.offsetMax.x - 12, modernContent.offsetMax.y);
            var layoutGroup = modernContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 4; layoutGroup.padding = new RectOffset(1, 1, 1, 4);
            layoutGroup.childControlWidth = true; layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true; layoutGroup.childForceExpandHeight = false;
            modernContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            modernSummary = FlowLabel("Modernization limits", 40, 14);
            modernSummary.raycastTarget = true;
            modernSummaryHint = modernSummary.gameObject.AddComponent<PlayerTooltip>(); modernSummaryHint.Display = text => ui.Tooltip?.Invoke(text);
            modernWorkRow = FlowPanel("Modernization work", 34);
            modernWork = ui.Label(modernWorkRow, "", new Rect(5, 0, modernWidth - 54, 24), 14);
            modernWorkRow.GetComponent<Graphic>().raycastTarget = true;
            modernWorkHint = modernWorkRow.gameObject.AddComponent<PlayerTooltip>(); modernWorkHint.Display = text => ui.Tooltip?.Invoke(text);
            var track = ui.Panel(modernWorkRow, "Modernization progress track", new Rect(5, 27, modernWidth - 60, 5), PlayerUi.Selected);
            modernProgress = PlayerUi.Graphic<Image>(track, "Modernization progress", new Rect(0, 0, 0, 5)); modernProgress.color = PlayerUi.Blue;
            for (int i = 0; i < directionRows.Length; i++)
            {
                int row = i; directionRows[i] = MakeModernizationRow("Modernize " + (ModernizationDirection)i, () => BuyModernization(directionRows[row], false));
            }
            for (int i = 0; i < colorRows.Length; i++)
            {
                int row = i; colorRows[i] = MakeModernizationRow("Modernize color " + (InkColor)(i + 1), () => BuyModernization(colorRows[row], true));
            }
            var support = FlowPanel("Support fortification", 64);
            modernFortify = AlbumTextButton(support, "Fortify support", new Rect(4, 4, modernWidth - 52, 54), "Укрепить материалом", () => {
                if (!game.CanBuild || !game.Drawing.InspectedCell.HasValue) return;
                game.Drawing.SetMessage(game.Run.Upgrade(game.Drawing.InspectedCell.Value, out var reason) ? "Начато укрепление." : reason); Refresh();
            });
            modernFortifyCaption = modernFortify.GetComponentInChildren<Text>();
            modernDetails = FlowLabel("Album battle structure details", 160, 14);
            modernDetails.gameObject.SetActive(false);
            PlayerIcon[] priorities = { PlayerIcon.Target, PlayerIcon.Shield, PlayerIcon.Home };
            for (int i = 0; i < modernPriorities.Length; i++)
            {
                int priority = i;
                modernPriorities[i] = ui.IconButton(modernContext, "Album battle priority " + i,
                    new Rect(8 + i * 42, height - 40, 36, 36), priorities[i], BattleCopy("priority_" + i), () => {
                        if (game.CanBuild && game.Drawing.InspectedCell.HasValue) game.Run.SetPriority(game.Drawing.InspectedCell.Value, (TargetPriority)priority);
                        Refresh();
                    });
            }
            modernDetailsToggle = ui.IconButton(modernContext, "Battle details toggle", new Rect(modernWidth - 46, height - 40, 36, 36), PlayerIcon.ChevronDown,
                "Характеристики и правила", ToggleModernDetails);
            modernDetailsGlyph = modernDetailsToggle.GetComponentInChildren<PlayerIconGraphic>();
            modernContext.gameObject.SetActive(false);
        }

        RectTransform FlowPanel(string name, float height)
        {
            var panel = ui.Panel(modernContent, name, new Rect(0, 0, modernWidth - 44, height), PlayerUi.Selected);
            panel.gameObject.AddComponent<LayoutElement>().preferredHeight = height; return panel;
        }
        Text FlowLabel(string name, float height, int size)
        {
            var label = ui.Label(modernContent, "", new Rect(0, 0, modernWidth - 44, height), size);
            label.name = name; label.gameObject.AddComponent<LayoutElement>().preferredHeight = height; return label;
        }
        ModernizationRow MakeModernizationRow(string name, Action buy)
        {
            var row = new ModernizationRow { Root = FlowPanel(name + " row", 56) };
            row.Root.GetComponent<Graphic>().raycastTarget = true;
            row.Info = row.Root.gameObject.AddComponent<PlayerNotebookButton>(); row.Info.targetGraphic = row.Root.GetComponent<Graphic>();
            var hint = row.Root.gameObject.AddComponent<PlayerTooltip>(); hint.Display = text => ui.Tooltip?.Invoke(text);
            row.Info.onClick.AddListener(() => ui.Tooltip?.Invoke(hint.Message));
            float inner = modernWidth - 54;
            row.Glyph = MinimalGlyph(row.Root, name + " glyph", PlayerIcon.Plus, new Rect(5, 8, 20, 20));
            row.Title = ui.Label(row.Root, "", new Rect(31, 1, inner - 140, 34), 14);
            row.Title.fontStyle = FontStyle.Bold;
            row.Buy = ui.IconButton(row.Root, name, new Rect(inner - 102, 1, 106, 34), PlayerIcon.Plus, "", buy);
            PlayerUi.Place(row.Buy.GetComponentInChildren<PlayerIconGraphic>().rectTransform, new Rect(5, 8, 16, 16));
            row.BuyCaption = ui.Label(row.Buy.transform, "", new Rect(26, 0, 76, 34), 14, alignment: TextAnchor.MiddleCenter);
            row.Stats = ui.Label(row.Root, "", new Rect(5, 35, inner, 20), 14);
            row.Reason = ui.Label(row.Root, "", new Rect(5, 56, inner, 18), 14, PlayerUi.Muted);
            return row;
        }
        void BuyModernization(ModernizationRow row, bool color)
        {
            if (!game.CanBuild || row.Quote == null || !row.Quote.Valid) return;
            var quote = row.Quote;
            if (color && quote.CurrentRank == 0) game.ConfirmModernizationColor(quote);
            else game.Drawing.SetMessage(game.Run.Modernize(quote, out var reason) ? "Начаты работы." : reason);
            Refresh();
        }

        void RefreshModernizationHud()
        {
            var s = game.Run.Session; var input = game.Drawing; bool active = game.CanBuild;
            modernUpgrade.interactable = active;
            RefreshApproachIndicators(s);
            albumInk.text = s.Ink.ToString(); albumHealth.text = Mathf.CeilToInt(s.BaseHealth) + "/" + Mathf.CeilToInt(s.Balance.BaseHealth);
            albumWave.text = s.WaveIndex + "/" + s.Balance.Waves.Count;
            albumPhase.text = game.Finished ? "Завершено" : s.Phase == RunPhase.Assault ?
                AssaultEnemyCount(s, compact: true) :
                (game.Run.TacticalPaused ? "Пауза" : Mathf.CeilToInt(s.Countdown) + " с");
            albumForecast.text = NextWaveForecast(s, detailed: true);
            if (!game.Finished && s.Phase != RunPhase.Assault && s.WaveIndex < s.Balance.Waves.Count)
            {
                var next = s.Balance.Waves[s.WaveIndex];
                albumPhase.text += next.IsHorde ? " · " + next.Count + " " + forecastDirection + "\n" + forecastHeadline : " · далее " + next.Count;
            }
            RefreshCastleReadability(s);
            albumSpeed.text = "×" + game.Run.Speed;
            modernTacticalCaption.text = game.Run.TacticalPaused ? "Продолжить · Space" : "Пауза · Space";
            modernTacticalGlyph.Icon = game.Run.TacticalPaused ? PlayerIcon.Play : PlayerIcon.Pause;
            modernTacticalGlyph.SetVerticesDirty();
            modernTactical.GetComponent<PlayerTooltip>().Message = modernTacticalCaption.text +
                (game.Run.TacticalPaused ? " · приказы доступны, время остановлено" : " · остановить время, сохранив приказы");
            ui.MarkSelected(modernTactical, game.Run.TacticalPaused);
            albumLaunch.interactable = active && (s.Phase == RunPhase.Planning || s.Phase == RunPhase.Intermission);
            albumLaunch.GetComponent<PlayerTooltip>().Message = "Следующая волна · N\n" + albumForecast.text;
            albumMessage.text = !input.IsPlacingTower && game.Run.Workshop.HasUnsavedChanges ? GameText.Resolve(game.Run.Workshop.SaveMessage)
                : input.HasInitialPlanningInstruction ? "" : input.Message;
            albumMessageBadge.gameObject.SetActive(!string.IsNullOrEmpty(albumMessage.text));
            RefreshMinimalHint(toolHint.Length > 0 ? toolHint : input.PreviewReason);
            for (int i = 0; i < modernCards.Length; i++)
            {
                var entry = game.Run.RunLoadout.Entries[i];
                var resolved = game.Run.RunLoadout.ResolveSlot(i, MaterialTier.Pencil);
                int cost = resolved?.Cost ?? 0;
                string name = entry == null ? ui.T("empty") : GameText.Resolve(entry.Name);
                modernNames[i].text = name;
                modernPrices[i].text = entry == null ? "—" : cost.ToString();
                modernPrices[i].color = cost > s.Ink ? NotebookPalette.Red : PlayerUi.Ink;
                modernCards[i].interactable = active && entry != null;
                modernCards[i].GetComponent<PlayerTooltip>().Message = (i + 1) + " · " + name + (entry == null ? "" : " · " + cost + " штр.") +
                    (cost > s.Ink ? " · не хватает штрихов" : resolved != null &&
                        resolved.Weapons.Any(w => w.Attack != AttackKind.Radial) ? "\nЛКМ — выбрать клетку, ещё раз ЛКМ — закрепить сектор." : "");
                ui.MarkSelected(modernCards[i], input.Mode == DrawingMode.Build && input.BlueprintSlot == i);
            }
            modernErase.interactable = active; modernBlotter.interactable = active && s.Balance.Mechanics.CorpseBlots;
            ui.MarkSelected(modernErase, input.Mode == DrawingMode.Erase); ui.MarkSelected(modernBlotter, input.Mode == DrawingMode.Blotter);
            if (flaskReward != null) RefreshInkFlasks(s);
            else for (int i = 0; i < albumWells.Length; i++)
            {
                bool exists = s.Balance.Mechanics.InkWells && i < s.Wells.Count; albumWells[i].gameObject.SetActive(exists); if (!exists) continue;
                var well = s.Wells[i]; albumWells[i].interactable = active;
                albumWellSymbols[i].color = well.Color == InkColor.Red ? NotebookPalette.Red : well.Color == InkColor.Blue ? PlayerUi.Blue : NotebookPalette.Green;
                albumWellAmounts[i].text = well.Active ? Mathf.CeilToInt(well.Health).ToString() : s.Balance.WellActivationCost.ToString();
                ui.MarkSelected(albumWells[i], well.Active);
                albumWells[i].GetComponent<PlayerTooltip>().Message = (well.Active ? "Защищай чернильницу" : "Активировать чернильницу: " + s.Balance.WellActivationCost + " штр.") +
                    " · запас " + s.ColoredInk(well.Color) + " · +" + s.Balance.WellInkPerWave + " за волну";
            }
            RefreshModernizationInspector(s);
            developmentOverlay?.RefreshNow();
        }

        void RefreshModernizationInspector(IRunReadModel s)
        {
            var inspected = game.Drawing.InspectedCell.HasValue ? s.StructureAt(game.Drawing.InspectedCell.Value) : null;
            modernContext.gameObject.SetActive(!game.Drawing.IsPlacingTower && (modernShowSummary || inspected.HasValue));
            if (!modernContext.gameObject.activeSelf) return;
            var development = inspected?.ModernizationEntry;
            var support = inspected?.ResolvedEntry?.Support;
            bool tower = development != null;
            bool attack = ModernizationRules.HasAttack(development);
            foreach (var row in directionRows) row.Root.gameObject.SetActive(tower);
            directionRows[(int)ModernizationDirection.Power].Root.gameObject.SetActive(attack);
            foreach (var row in colorRows) row.Root.gameObject.SetActive(attack);
            modernFortify.transform.parent.gameObject.SetActive(inspected.HasValue && !tower &&
                ConstructionToolRules.IsTool(inspected.Value.Kind) && inspected.Value.Kind != StructureKind.Generator);
            for (int i = 0; i < modernPriorities.Length; i++) modernPriorities[i].gameObject.SetActive(attack);
            modernWorkRow.gameObject.SetActive(tower && inspected.Value.Modernization.IsWorking);
            if (!inspected.HasValue)
            {
                modernTitle.text = "Сводка обороны"; modernSummary.text = ui.T("field_stats", s.Enemies.Count, s.Kills, s.Structures.Count);
                modernSummary.GetComponent<LayoutElement>().preferredHeight = Mathf.Ceil(modernSummary.preferredHeight) + 4;
                modernDetails.text = "Выбери установленную башню, чтобы развивать её направления и цвет.";
                modernSummaryHint.Message = modernDetails.text;
                RefreshModernDetails();
                return;
            }
            var structure = inspected.Value; var state = structure.Modernization;
            if (modernInspectedId != structure.Id)
            {
                modernInspectedId = structure.Id;
                modernShowDetails = false;
                modernContent.parent.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
            }
            var frozen = game.Run.RunLoadout.Entries.FirstOrDefault(page => page != null &&
                (page.Id == structure.SourceAlbumEntryId || structure.Blueprint != null && page.LegacyBlueprint?.Id == structure.Blueprint.Id));
            modernTitle.text = frozen == null ? GameText.Resolve(structure.Blueprint?.Name ?? WorkshopHud.WeaponName(structure.Kind)) : GameText.Resolve(frozen.Name);
            modernSummary.text = "HP " + Mathf.CeilToInt(structure.Health) + "/" + Mathf.CeilToInt(structure.MaxHealth) +
                (tower ? " · " + state.StepsUsed + "/6 · Тех. " + RankText(s.Balance.Mechanics.MaximumModernizationRank) + "\n" +
                    (!structure.IsComplete ? "Строится · " : "") +
                    (structure.Color == InkColor.None ? "Без цвета · одно III" : ColorName(structure.Color) + " " + RankText(state.ColorRank) + " · закреплён")
                    : "\n" + (structure.IsComplete ? "Построено · " : "Строится · ") + BattleCopy("material_" + (int)structure.Tier));
            if (structure.HasDirectionalWeapon && tower)
            {
                var coverage = development.Weapons.Where(w => w.Attack != AttackKind.Radial).Select(w =>
                    Number(w.FiringArcDegrees) + "° · " + (w.MinimumRange > 0 ? Number(w.MinimumRange) + "–" : "") + Number(w.Range) + " кл.").Distinct();
                modernSummary.text += "\nСектор " + string.Join("   /   ", coverage);
            }
            if (support?.Kind == AssemblyStructureFamily.Generator)
                modernSummary.text += "\n" + BattleCopy("generator_income", support.IncomePerWave);
            else if (GameText.TrapOrBeaconEffect(support) is string effect)
                modernSummary.text += "\n" + GameText.Resolve(effect);
            modernSummaryHint.Message = "HP " + Mathf.CeilToInt(structure.Health) + "/" + Mathf.CeilToInt(structure.MaxHealth) +
                (tower ? " · улучшения " + state.StepsUsed + "/6 · технология " + RankText(s.Balance.Mechanics.MaximumModernizationRank) +
                    "\nТолько одно обычное направление может достичь III. Цвет выбирается один раз; развитие действует только на эту копию." : "Сооружение сохраняет свою роль.");
            modernSummary.GetComponent<LayoutElement>().preferredHeight = Mathf.Ceil(modernSummary.preferredHeight) + 4;
            if (state.IsWorking)
            {
                modernWork.text = (state.PendingDirection.HasValue ? DirectionName(state.PendingDirection.Value) : "Цвет") + " · " + Number(state.WorkRemaining) + " с";
                modernWorkHint.Message = "Работы: " + modernWork.text + "\nСтрельба, очистка и прицеливание приостановлены. Ремонт доступен.";
                modernProgress.rectTransform.sizeDelta = new Vector2((modernWidth - 60) * (1 - Mathf.Clamp01(state.WorkRemaining / Mathf.Max(.001f, state.WorkDuration))), 5);
            }
            if (tower)
            {
                for (int i = 0; i < directionRows.Length; i++)
                {
                    if (!directionRows[i].Root.gameObject.activeSelf) continue;
                    var direction = (ModernizationDirection)i; var quote = game.Run.QuoteModernization(structure.Id, direction);
                    SetModernizationRow(directionRows[i], quote, DirectionName(direction) + " " + RankText(state.Rank(direction)) + (state.Rank(direction) < 3 ? " → " + RankText(state.Rank(direction) + 1) : ""),
                        ModernizationStats(quote, direction), false);
                }
                for (int i = 0; i < colorRows.Length; i++)
                {
                    var color = (InkColor)(i + 1); bool offered = attack && (structure.Color == InkColor.None || structure.Color == color);
                    colorRows[i].Root.gameObject.SetActive(offered); if (!offered) continue;
                    var quote = game.Run.QuoteColorModernization(structure.Id, color);
                    int rank = structure.Color == color ? state.ColorRank : 0;
                    SetModernizationRow(colorRows[i], quote, ColorName(color) + " " + RankText(rank) + (rank < 3 ? " → " + RankText(rank + 1) : ""),
                        ColorStats(quote) + "\nЗапас " + s.ColoredInk(color) + " · целей/с " +
                        (rank == 0 ? 0 : rank + 2) + (rank < 3 ? " → " + (Mathf.Max(1, rank + 1) + 2) : ""), true);
                }
            }
            var details = new StringBuilder();
            if (tower)
            {
                foreach (var weapon in development.Weapons)
                {
                    var mount = structure.GetMountState(weapon.SlotIndex); var cycle = mount.Cycle;
                    int rounds = cycle != null && cycle.Initialized ? cycle.RoundsRemaining : weapon.MagazineSize;
                    details.AppendLine("Орудие " + (weapon.SlotIndex + 1) + ": " + rounds + "/" + weapon.MagazineSize + " · захват " + Number(weapon.AcquisitionSeconds) + " с");
                    details.AppendLine(BattleCopy(cycle != null && cycle.IsReloading ? "reload" : "interval", Number(mount.Cooldown)));
                    details.AppendLine("Урон " + Number(weapon.Damage) + " · дальность " + Number(weapon.Range) + " · сектор " + Number(weapon.FiringArcDegrees) + "°");
                    details.AppendLine("Интервал " + Number(weapon.IntervalAtFeed(development.FeedMultiplier)) + " с · перезарядка " + Number(weapon.ReloadAtFeed(development.FeedMultiplier)) + " с");
                }
                details.Append("Чертёж и детали неизменны. Развитие действует только на эту копию.");
            }
            else if (support != null)
            {
                if (support.Kind == AssemblyStructureFamily.Generator) details.AppendLine(BattleCopy("generator_income", support.IncomePerWave)).AppendLine(BattleCopy("generator_paid", structure.GeneratorPaidWaves))
                    .AppendLine(BattleCopy("generator_rule")).AppendLine(BattleCopy("pencil_only")).AppendLine(BattleCopy("refund", 0));
                else if (support.Kind == AssemblyStructureFamily.Repair) details.AppendLine(BattleCopy("repair", Number(support.RepairPerSecond))).AppendLine(BattleCopy("radius", Number(support.Radius)));
                else if (support.Kind == AssemblyStructureFamily.Slow) details.AppendLine(BattleCopy("slow", Number(support.SlowSpeedMultiplier), Number(support.SlowSeconds))).AppendLine(BattleCopy("radius", Number(support.Radius)));
                details.Append("Сооружение сохраняет свою роль. Боевые направления доступны башням.");
            }
            modernDetails.text = details.ToString();
            modernDetails.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(52, Mathf.Ceil(modernDetails.preferredHeight) + 8);
            RefreshModernDetails();
            for (int i = 0; i < modernPriorities.Length; i++)
            { modernPriorities[i].interactable = attack && game.CanBuild; ui.MarkSelected(modernPriorities[i], (int)structure.Priority == i); }
            if (modernFortify.transform.parent.gameObject.activeSelf) RefreshSupportFortification(structure);
        }

        void RefreshModernDetails()
        {
            modernDetails.gameObject.SetActive(modernShowDetails);
            modernDetailsGlyph.Icon = modernShowDetails ? PlayerIcon.ChevronUp : PlayerIcon.ChevronDown;
            modernDetailsGlyph.SetVerticesDirty();
            modernDetailsToggle.GetComponent<PlayerTooltip>().Message = modernShowDetails ? "Скрыть характеристики" : "Характеристики, цикл и правила";
            ui.MarkSelected(modernDetailsToggle, modernShowDetails);
        }

        void ToggleModernDetails()
        {
            modernShowDetails = !modernShowDetails;
            Refresh();
            Canvas.ForceUpdateCanvases();
            var scroll = modernContent.parent.GetComponent<ScrollRect>();
            scroll.StopMovement();
            float overflow = modernContent.rect.height - scroll.viewport.rect.height;
            // Align the detail text's beginning, unless the remaining content is
            // shorter than the viewport. Closing returns to the direction rows.
            scroll.verticalNormalizedPosition = !modernShowDetails || overflow <= 0 ? 1 :
                Mathf.Clamp01(1 + modernDetails.rectTransform.anchoredPosition.y / overflow);
        }

        void SetModernizationRow(ModernizationRow row, ModernizationQuote quote, string title, string stats, bool color)
        {
            row.Quote = quote;
            row.Title.text = quote == null ? "—" : (color ? ColorName(quote.Color) : DirectionName(quote.Direction)) +
                "\n" + RankText(quote.CurrentRank) + (quote.CurrentRank < 3 ? " → " + RankText(quote.NextRank) : "");
            row.Stats.text = stats;
            row.Glyph.Icon = color ? PlayerIcon.Drop : quote?.Direction == ModernizationDirection.Power ? PlayerIcon.Bolt :
                quote?.Direction == ModernizationDirection.Mechanics ? PlayerIcon.Fast : quote?.Direction == ModernizationDirection.Range ? PlayerIcon.Target : PlayerIcon.Shield;
            row.Glyph.color = !color ? PlayerUi.Ink : quote?.Color == InkColor.Red ? NotebookPalette.Red : quote?.Color == InkColor.Blue ? PlayerUi.Blue : NotebookPalette.Green;
            row.Glyph.SetVerticesDirty();
            row.BuyCaption.text = quote != null && quote.Cost > 0 ? quote.Cost + (color ? " черн." : " штр.") + "\n" + Number(quote.WorkSeconds) + " с" : "—";
            string fullReason = quote == null ? "Недоступно" : !quote.Valid ? GameText.Resolve(quote.Reason) : "";
            row.Reason.text = CompactModernReason(quote, fullReason);
            row.Reason.gameObject.SetActive(row.Reason.text.Length > 0);
            // Every mount contributes real values. Let this row grow within the single
            // inspector scroll owner instead of truncating multi-mount previews.
            float statsHeight = Mathf.Ceil(row.Stats.preferredHeight) + 2;
            float reasonHeight = row.Reason.text.Length == 0 ? 0 : Mathf.Ceil(row.Reason.preferredHeight) + 2;
            PlayerUi.Place(row.Stats.rectTransform, new Rect(5, 35, modernWidth - 54, statsHeight));
            PlayerUi.Place(row.Reason.rectTransform, new Rect(5, 35 + statsHeight, modernWidth - 54, reasonHeight));
            row.Root.GetComponent<LayoutElement>().preferredHeight = 38 + statsHeight + reasonHeight;
            row.Buy.interactable = game.CanBuild && quote != null && quote.Valid;
            var buyGlyph = row.Buy.GetComponentInChildren<PlayerIconGraphic>();
            buyGlyph.Icon = quote != null && quote.Valid ? PlayerIcon.Plus : PlayerIcon.Lock; buyGlyph.SetVerticesDirty();
            row.Buy.GetComponent<PlayerTooltip>().Message = title + "\n" + row.Stats.text +
                (quote != null && quote.Cost > 0 ? "\n" + quote.Cost + (color ? " цветных чернил" : " штрихов") + " · " + Number(quote.WorkSeconds) + " с работ" : "") +
                (fullReason.Length > 0 ? "\n" + fullReason : color && quote?.CurrentRank == 0 ? "\nПервый цвет закрепляется навсегда" : "") +
                (!color && quote?.Direction == ModernizationDirection.Resilience ? "\nДоля текущего HP сохраняется." : "");
            // The row itself is an informational keyboard target even when purchase is locked.
            row.Info.GetComponent<PlayerTooltip>().Message = row.Buy.GetComponent<PlayerTooltip>().Message;
        }

        static string CompactModernReason(ModernizationQuote quote, string reason)
        {
            if (reason.Length == 0) return "";
            if (reason.Contains("технология")) return "Технология " + RankText(quote?.NextRank ?? 1);
            // Work/construction is already visible once in the inspector header.
            if (reason.Contains("работы") || reason.Contains("строится")) return "";
            if (reason.Contains("штрихов")) return "Нужны штрихи";
            if (reason.Contains("чернил")) return "Нужны чернила";
            if (reason.Contains("шесть")) return "Лимит 6/6";
            if (reason.Contains("одно направление")) return "Одно направление III";
            if (reason.Contains("максимальный")) return "Максимум";
            if (reason.Contains("отключены") || reason.Contains("отключено")) return "Отключено";
            return reason.Length <= 30 ? reason : "Недоступно";
        }
        static string RankText(int rank) => rank == 0 ? "0" : rank == 1 ? "I" : rank == 2 ? "II" : "III";
        static string DirectionName(ModernizationDirection direction) => direction == ModernizationDirection.Power ? "Мощность" : direction == ModernizationDirection.Mechanics ? "Механика" : direction == ModernizationDirection.Range ? "Дальность" : "Стойкость";
        static string ColorName(InkColor color) => color == InkColor.Red ? "Красный" : color == InkColor.Blue ? "Синий" : "Зелёный";
        static string ModernizationStats(ModernizationQuote quote, ModernizationDirection direction)
        {
            if (quote?.Before == null || quote.After == null) return "Нет совместимых параметров";
            if (direction == ModernizationDirection.Resilience) return "HP " + Number(quote.Before.Health) + " → " + Number(quote.After.Health);
            var copy = new StringBuilder();
            for (int i = 0; i < quote.Before.Weapons.Count; i++)
            {
                var before = quote.Before.Weapons[i]; var after = quote.After.Weapons[i];
                if (direction == ModernizationDirection.Power && before.Attack == AttackKind.Cleanup) continue;
                if (copy.Length > 0) copy.Append(" · ");
                if (direction == ModernizationDirection.Power) copy.Append("Урон ").Append(Number(before.Damage)).Append(" → ").Append(Number(after.Damage));
                else if (direction == ModernizationDirection.Range) copy.Append("Радиус ").Append(Number(before.Range)).Append(" → ").Append(Number(after.Range));
                else copy.Append("Инт. ").Append(Number(before.IntervalAtFeed(quote.Before.FeedMultiplier))).Append(" → ").Append(Number(after.IntervalAtFeed(quote.After.FeedMultiplier)))
                    .Append(" с; перез. ").Append(Number(before.ReloadAtFeed(quote.Before.FeedMultiplier))).Append(" → ").Append(Number(after.ReloadAtFeed(quote.After.FeedMultiplier))).Append(" с");
            }
            return copy.ToString();
        }
        static string ColorStats(ModernizationQuote quote)
        {
            if (quote == null) return "";
            var before = quote.BeforeColor; var after = quote.AfterColor;
            string effect = quote.Color == InkColor.Red ? "Урон/тик " : quote.Color == InkColor.Blue ? "Скорость ×" : "Снижение защиты ";
            float beforeStrength = before.Color == InkColor.None && quote.Color == InkColor.Blue ? 1 : before.Strength;
            return effect + Number(beforeStrength) + " → " + Number(after.Strength) + " · " + Number(after.Duration) + " с";
        }
        void RefreshSupportFortification(StructureView structure)
        {
            bool eligible = structure.IsComplete && structure.Tier < MaterialTier.Marker;
            int cost = 0;
            if (eligible)
            {
                var next = (MaterialTier)((int)structure.Tier + 1);
                var balance = game.Run.RunLoadout.Balance;
                if (structure.ResolvedEntry != null)
                {
                    var resolution = AssemblyResolver.Resolve(structure.ResolvedEntry.Snapshot, balance, next);
                    eligible = resolution.Valid;
                    if (eligible) cost = Math.Max(0, resolution.Value.Cost - structure.ResolvedEntry.Cost);
                }
                else
                    cost = Math.Max(0, structure.Blueprint == null
                        ? TowerCatalog.ToolCost(balance, structure.Kind, next) - TowerCatalog.ToolCost(balance, structure.Kind, structure.Tier)
                        : TowerCatalog.Cost(structure.Blueprint, balance, next) - TowerCatalog.Cost(structure.Blueprint, balance, structure.Tier));
            }
            modernFortifyCaption.text = !eligible ? !structure.IsComplete ? "Строится" : structure.Tier == MaterialTier.Marker ? "Максимальный материал" : "Материал недоступен" : "Укрепить: " + BattleCopy("material_" + ((int)structure.Tier + 1)) + "\n" + cost + " штр.";
            modernFortify.interactable = game.CanBuild && eligible && cost <= game.Run.Session.Ink;
        }
    }
}
