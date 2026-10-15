using System;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Page composition only; controllers remain the sources of game data.</summary>
    public sealed partial class PlayerShell : MonoBehaviour
    {
        PlayerGameRoot game;
        PlayerUi ui;
        CanvasScaler scaler;
        RectTransform root, page, battleRoot;
        PlayerBattleHud battle;
        CanvasGroup battleInteraction;
        Text status, tooltip, gold;
        float refresh, appliedScale;
        int screenWidth, screenHeight;
        bool statistics;
        Action confirmAction;
        string confirmTitle, confirmMessage;
        GameObject confirmation;
        public bool HasConfirmation => confirmAction != null;
        public PlayerLayout Layout { get; private set; }
        float W => Layout.Width;
        float H => Layout.Height;
        float M => Layout.Margin;

        public void Initialize(PlayerGameRoot owner)
        {
            game = owner; ui = new PlayerUi(owner.NotebookStyle);
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            gameObject.AddComponent<GraphicRaycaster>();
            scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root = new GameObject("Player pages", typeof(RectTransform)).GetComponent<RectTransform>(); root.SetParent(transform, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
            ApplyLayout(Screen.width, Screen.height);
            ui.Tooltip = value => { if (tooltip != null) tooltip.text = value; };
        }

        public void ApplyLayout(int width, int height)
        {
            screenWidth = width; screenHeight = height; appliedScale = game.InterfaceScale;
            Layout = new PlayerLayout(width, height, appliedScale);
            scaler.referenceResolution = new Vector2(W, H); root.sizeDelta = new Vector2(W, H);
            if (battleRoot != null) { battleRoot.gameObject.SetActive(false); Destroy(battleRoot.gameObject); }
            battleRoot = ui.Panel(root, "Battle controls", new Rect(0, 0, W, H), Color.clear);
            battleInteraction = battleRoot.gameObject.AddComponent<CanvasGroup>();
            battle = new PlayerBattleHud(game, ui, battleRoot, Layout);
            Show(game.Page);
        }

        public void Show(PlayerPage current)
        {
            if (page != null) { page.gameObject.SetActive(false); Destroy(page.gameObject); }
            bool battlefield = !game.Run.InWorkshop && current != PlayerPage.Workshop;
            battleRoot.gameObject.SetActive(battlefield);
            battleInteraction.interactable = current == PlayerPage.Battle && !HasConfirmation;
            battleInteraction.blocksRaycasts = current == PlayerPage.Battle && !HasConfirmation;
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            page = ui.Panel(root, "Page " + current, new Rect(0, 0, W, H), Color.clear);
            status = tooltip = gold = null;
            if (!battlefield && current != PlayerPage.Workshop) ui.Panel(page, "Paper", new Rect(0, 0, W, H), PlayerUi.Paper, true);
            switch (current)
            {
                case PlayerPage.Home: Home(); break;
                case PlayerPage.Map: Map(); break;
                case PlayerPage.Preparation: Preparation(); break;
                case PlayerPage.Pause: Pause(); break;
                case PlayerPage.Upgrades: Upgrades(); break;
                case PlayerPage.Result: Result(); break;
                case PlayerPage.Settings: Settings(); break;
                case PlayerPage.Help: Help(); break;
            }
            tooltip = ui.Label(page, "", new Rect(M, H - 27, W - M * 2, 24), 15, PlayerUi.Muted, alignment: TextAnchor.MiddleCenter);
            SetStatus(game.Status);
            if (battlefield) battle.Refresh();
            if (HasConfirmation) BuildConfirmation();
        }

        void Header(string title, Action back)
        {
            ui.IconButton(page, "Back", new Rect(M, 24, 48, 48), PlayerIcon.Back, ui.T("back"), back);
            bool narrow = W < 900;
            ui.Label(page, title, new Rect(M + 64, 20, W - M * 2 - (narrow ? 210 : 310), 56), narrow ? 27 : 34, title: true);
            gold = ui.Label(page, ui.T("gold", game.Run.Workshop.Gold), new Rect(W - M - (narrow ? 140 : 240), 28, narrow ? 140 : 240, 40), narrow ? 18 : 20, PlayerUi.Muted, alignment: TextAnchor.MiddleRight);
            gold.name = "Player gold";
        }

        void Home()
        {
            ui.Label(page, "Держи линию", new Rect(M, 20, 430, 56), 38, title: true);
            gold = ui.Label(page, ui.T("gold", game.Run.Workshop.Gold), new Rect(W - M - 280, 30, 220, 40), 18, PlayerUi.Muted, alignment: TextAnchor.MiddleRight);
            ui.IconButton(page, "Home settings", new Rect(W - M - 48, 24, 48, 48), PlayerIcon.Settings, ui.T("settings"), () => game.OpenSettings());
            bool narrow = W < 1000;
            float width = W - M * 2, height = H - 196;
            var sheet = ui.Panel(page, "Your notebook", new Rect(M, 92, width, height), PlayerUi.Card);
            float artW = narrow ? width : width - 310;
            float artH = narrow ? height - 70 : height;
            PlayerUi.Graphic<PlayerPaperGraphic>(sheet, "Notebook grid", new Rect(12, 12, artW - 24, artH - 24));
            ui.Art(sheet, "object.castle", new Rect(artW * .50f, artH * .16f, artW * .34f, artH * .50f));
            ui.Blueprint(sheet, game.Run.Workshop.Loadout[0], new Rect(artW * .14f, artH * .26f, artW * .34f, artH * .59f));
            if (!narrow)
            {
                ui.Label(sheet, "Твоя оборона", new Rect(artW + 12, height * .25f, 270, 56), 34, title: true);
                ui.Label(sheet, "Собери. Нарисуй.\nЗащити страницу.", new Rect(artW + 12, height * .25f + 68, 270, 64), 20, PlayerUi.Muted);
                ui.Label(sheet, ui.T("mission") + " · " + game.Run.Session.Balance.Waves.Count + " волн", new Rect(artW + 12, height * .25f + 152, 270, 56), 16, PlayerUi.Muted);
            }
            ui.Button(sheet, "Open campaign", new Rect(narrow ? (width - 250) / 2 : artW + 12, narrow ? height - 62 : height * .25f + 222, 250, 50), PlayerIcon.Play, "Начать игру", () => game.Navigate(PlayerPage.Map), true);
            float footer = H - 82, gap = 12, buttonWidth = (width - gap * 3) / 4;
            ui.Button(page, "workshop", new Rect(M, footer, buttonWidth, 48), PlayerIcon.Pencil, ui.T("workshop"), () => game.Navigate(PlayerPage.Workshop));
            ui.Button(page, "settings", new Rect(M + buttonWidth + gap, footer, buttonWidth, 48), PlayerIcon.Settings, ui.T("settings"), () => game.OpenSettings());
            ui.Button(page, "help", new Rect(M + 2 * (buttonWidth + gap), footer, buttonWidth, 48), PlayerIcon.Help, "Как играть", () => game.OpenSettings(true));
            ui.Button(page, "exit", new Rect(M + 3 * (buttonWidth + gap), footer, buttonWidth, 48), PlayerIcon.Exit, "Выйти", () => Confirm("Выйти из игры?", "Сохранённые чертежи и золото останутся в профиле.", () => UnityEngine.Application.Quit()));
        }

        void Map()
        {
            Header("Разведка · первая страница", () => game.Navigate(PlayerPage.Home));
            if (W < 900) { CompactMap(); return; }
            float width = W - M * 2, side = Mathf.Min(370, width * .34f), height = H - 165;
            var map = ui.Panel(page, "Mission map", new Rect(M, 112, width - side - 24, height), PlayerUi.Card);
            var sketch = PlayerUi.Graphic<WorkshopMapSketch>(map, "Actual battlefield", new Rect(24, 70, width - side - 72, height - 150)); sketch.SetSession(game.Run.Session);
            ui.Label(map, "Первая страница", new Rect(24, 18, width - side - 72, 44), 29, title: true);
            ui.Label(map, "Стрелки — входы · замок — цель защиты · капли — источники", new Rect(24, height - 60, width - side - 72, 40), 17, PlayerUi.Muted);
            var info = ui.Panel(page, "Mission information", new Rect(W - M - side, 112, side, height), PlayerUi.Card);
            ui.Label(info, "МИССИЯ 01", new Rect(24, 20, side - 48, 30), 16, PlayerUi.Blue);
            ui.Art(info, "object.castle", new Rect(side * .28f, 66, side * .44f, Mathf.Min(150, height * .25f)));
            float y = Mathf.Min(230, height * .4f);
            ui.Label(info, ui.T("mission"), new Rect(24, y, side - 48, 50), 32, title: true);
            ui.Label(info, ui.T("mission_brief", game.Run.Session.Balance.Waves.Count), new Rect(24, y + 62, side - 48, 140), 19, PlayerUi.Muted);
            ui.Button(info, "Prepare mission", new Rect(24, height - 78, side - 48, 54), PlayerIcon.Play, "Подготовка", () => game.Navigate(PlayerPage.Preparation), true);
        }

        void CompactMap()
        {
            float width = W - 2 * M;
            var body = ui.Scroll(page, "Mission scroll", new Rect(M, 100, width, H - 204), 610);
            var map = ui.Panel(body, "Mission map", new Rect(0, 0, width, 290), PlayerUi.Card);
            ui.Label(map, "Первая страница", new Rect(20, 12, width - 40, 44), 28, title: true);
            var sketch = PlayerUi.Graphic<WorkshopMapSketch>(map, "Actual battlefield", new Rect(20, 64, width - 40, 210)); sketch.SetSession(game.Run.Session);
            ui.Label(body, ui.T("mission"), new Rect(20, 310, width - 40, 52), 30, title: true);
            ui.Label(body, ui.T("mission_brief", game.Run.Session.Balance.Waves.Count), new Rect(20, 374, width - 40, 200), 20, PlayerUi.Muted);
            ui.Button(page, "Prepare mission", new Rect(W - M - 250, H - 84, 250, 52), PlayerIcon.Play, "Подготовка", () => game.Navigate(PlayerPage.Preparation), true);
        }

        void Preparation()
        {
            Header("Подготовка к миссии", () => game.Navigate(PlayerPage.Map));
            int columns = W < 1200 ? 2 : 4;
            float width = W - M * 2, cardW = (width - 18 * (columns - 1)) / columns;
            float cardH = columns == 2 ? 220 : 310, materialY = 110 + (cardH + 18) * (4 / columns);
            var body = ui.Scroll(page, "Preparation scroll", new Rect(M, 100, width, H - 204), materialY + 154);
            ui.Label(body, "Твой набор обороны", new Rect(0, 0, width, 44), 30, title: true);
            ui.Label(body, "Четыре чертежа. Материал выбирается отдельно — стоимость меняется вместе с ним.", new Rect(0, 48, width, 46), 18, PlayerUi.Muted);
            for (int i = 0; i < 4; i++)
            {
                int slot = i; var bp = game.Run.Workshop.Loadout[i];
                var card = ui.Panel(body, "Loadout " + i, new Rect(i % columns * (cardW + 18), 110 + i / columns * (cardH + 18), cardW, cardH), PlayerUi.Card);
                ui.Label(card, "0" + (i + 1), new Rect(18, 12, 40, 28), 17, PlayerUi.Muted);
                ui.Blueprint(card, bp, new Rect(28, 16, cardW - 56, cardH - 140));
                ui.Label(card, bp == null ? ui.T("empty") : GameText.Resolve(bp.Name), new Rect(18, cardH - 126, cardW - 36, 36), 23, title: true);
                if (bp != null) ui.Label(card, ui.T("cost", game.Run.PreviewBlueprint(bp, game.Drawing.Tier).Cost), new Rect(18, cardH - 90, cardW - 36, 28), 17, PlayerUi.Blue);
                ui.Button(card, "Replace blueprint " + i, new Rect(14, cardH - 54, cardW - 28, 42), PlayerIcon.Book, "Заменить чертёж", () => OpenBlueprintPicker(slot));
            }
            var materials = ui.Panel(body, "Preparation material", new Rect(0, materialY, width, 132), PlayerUi.Card);
            ui.Label(materials, "Материал строительства", new Rect(20, 12, width - 40, 32), 24, title: true);
            float materialW = Mathf.Min(210, (width - 64) / 3);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var button = ui.Button(materials, "Starting material " + i, new Rect(20 + i * (materialW + 12), 62, materialW, 48), PlayerIcon.Pencil, new[] { "Карандаш", "Ручка", "Маркер" }[i], () => { game.Drawing.SelectTier((MaterialTier)index); Show(PlayerPage.Preparation); });
                button.GetComponent<PlayerTooltip>().Message = ui.T("material_" + i);
                ui.MarkSelected(button, (int)game.Drawing.Tier == i);
            }
            ui.Button(page, "Edit album", new Rect(M, H - 84, 240, 52), PlayerIcon.Book, "В мастерскую", () => game.Navigate(PlayerPage.Workshop));
            ui.Button(page, "Start battle", new Rect(W - M - 250, H - 84, 250, 52), PlayerIcon.Play, "Начать оборону", () => game.StartBattle(), true);
            status = ui.Label(page, "", new Rect(M + 260, H - 84, Mathf.Max(80, width - 540), 52), 17, NotebookPalette.Red, alignment: TextAnchor.MiddleCenter);
        }

        void OpenBlueprintPicker(int slot)
        {
            var shield = ui.Panel(page, "Blueprint picker shield", new Rect(0, 0, W, H), new Color(.10f, .16f, .22f, .4f), true);
            float width = Mathf.Min(1040, W - 48), height = Mathf.Min(720, H - 48);
            var sheet = ui.Panel(shield, "Blueprint album picker", new Rect((W - width) / 2, (H - height) / 2, width, height), PlayerUi.Card, true);
            Action close = () => { shield.gameObject.SetActive(false); Destroy(shield.gameObject); };
            ui.Label(sheet, "Альбом · чертёж " + (slot + 1), new Rect(24, 12, width - 100, 52), 30, title: true);
            ui.IconButton(sheet, "Close blueprint picker", new Rect(width - 64, 16, 44, 40), PlayerIcon.Back, "Отменить выбор", close);
            int columns = width < 760 ? 2 : 3;
            float cardW = (width - 60 - 12 * (columns - 1)) / columns;
            var album = game.Run.Workshop.Album;
            var list = ui.Scroll(sheet, "Blueprint album choices", new Rect(24, 80, width - 48, height - 104), Mathf.Ceil(album.Count / (float)columns) * 218);
            for (int i = 0; i < album.Count; i++)
            {
                var bp = album[i]; string id = bp.Id;
                int occupiedSlot = -1;
                for (int assigned = 0; assigned < 4; assigned++)
                    if (game.Run.Workshop.Loadout[assigned]?.Id == id) occupiedSlot = assigned;
                var button = ui.IconButton(list, "Pick blueprint " + id, new Rect(i % columns * (cardW + 12), i / columns * 218, cardW, 206), PlayerIcon.Book, GameText.Resolve(bp.Name), () => game.AssignBlueprint(slot, id));
                Destroy(button.GetComponentInChildren<PlayerIconGraphic>().gameObject);
                ui.Blueprint(button.transform, bp, new Rect(20, 12, cardW - 40, 120));
                ui.Label(button.transform, GameText.Resolve(bp.Name), new Rect(12, 132, cardW - 24, 40), 19, title: true);
                bool unavailable = occupiedSlot >= 0 && occupiedSlot != slot;
                button.interactable = !unavailable;
                ui.Label(button.transform, unavailable ? "Уже в наборе · " + (occupiedSlot + 1) : ui.T("cost", game.Run.PreviewBlueprint(bp, game.Drawing.Tier).Cost), new Rect(12, 174, cardW - 24, 26), 16, PlayerUi.Muted);
                ui.MarkSelected(button, game.Run.Workshop.Loadout[slot]?.Id == id);
            }
            if (album.Count == 0) ui.Label(list, "Альбом пуст. Сохрани чертёж в мастерской.", new Rect(12, 12, width - 84, 80), 20);
            WorkshopModal.Open(shield.gameObject, close);
        }

        public void SetStatus(string message) { if (status != null) status.text = message; }
        void Update()
        {
            if (screenWidth != Screen.width || screenHeight != Screen.height || !Mathf.Approximately(appliedScale, game.InterfaceScale)) ApplyLayout(Screen.width, Screen.height);
            refresh -= Time.unscaledDeltaTime; if (refresh > 0) return; refresh = .12f;
            if (gold != null) gold.text = ui.T("gold", game.Run.Workshop.Gold);
            if (battleRoot.gameObject.activeSelf) battle.Refresh();
        }
    }
}
