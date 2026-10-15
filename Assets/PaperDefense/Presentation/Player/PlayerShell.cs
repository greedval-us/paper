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
            ui.Label(page, "Держи линию", new Rect(M, 28, 430, 48), 30, title: true);
            gold = ui.Label(page, ui.T("gold", game.Run.Workshop.Gold), new Rect(W - M - 280, 30, 220, 40), 18, PlayerUi.Muted, alignment: TextAnchor.MiddleRight);
            ui.IconButton(page, "Home settings", new Rect(W - M - 48, 24, 48, 48), PlayerIcon.Settings, ui.T("settings"), () => game.OpenSettings());
            bool narrow = W < 900;
            float mid = narrow ? 0 : Mathf.Max(0, (H - 670) / 2), left = narrow ? W - 2 * M : Mathf.Min(650, W * .47f - M);
            var hero = ui.Scroll(page, "Home story", new Rect(0, 100, W, Mathf.Max(120, H - (narrow ? 330 : 210))), mid + 444);
            ui.Label(hero, "Оборона, которую ты нарисовал", new Rect(M, mid, left, 32), 17, PlayerUi.Blue);
            int heroSize = Mathf.Clamp(Mathf.RoundToInt(left / 9.2f), 40, 70);
            ui.Label(hero, "Твоя линия.\nТвои правила.", new Rect(M, mid + 42, left, 196), heroSize, title: true);
            ui.Label(hero, "Собери башню, нарисуй её характер и защити страницу от каракулей.", new Rect(M, mid + 238, left, 72), 21, PlayerUi.Muted);
            ui.Button(narrow ? page : hero, "Open campaign", new Rect(M, narrow ? H - 212 : mid + 330, 250, 54), PlayerIcon.Play, "Начать игру", () => game.Navigate(PlayerPage.Map), true);
            ui.Label(hero, ui.T("mission") + " · " + game.Run.Session.Balance.Waves.Count + " волн", new Rect(M, mid + 398, left, 30), 16, PlayerUi.Muted);
            float artX = W * .52f, artW = W - artX - M, artH = Mathf.Min(610, H - 220);
            var art = ui.Panel(hero, "Your notebook", new Rect(artX, (H - artH) / 2 - 115, artW, artH), PlayerUi.Card);
            art.gameObject.SetActive(!narrow);
            PlayerUi.Graphic<PlayerPaperGraphic>(art, "Notebook grid", new Rect(0, 0, artW, artH));
            ui.Art(art, "object.castle", new Rect(artW * .50f, artH * .22f, artW * .30f, artH * .32f));
            ui.Blueprint(art, game.Run.Workshop.Loadout[0], new Rect(artW * .12f, artH * .42f, artW * .36f, artH * .32f));
            ui.Art(art, "ui.material.pen", new Rect(artW * .78f, artH * .44f, artW * .15f, artH * .34f));
            ui.Label(art, "Всё начинается с одного штриха", new Rect(20, artH - 58, artW - 40, 44), 25, PlayerUi.Muted, true, TextAnchor.MiddleCenter);
            float footer = narrow ? H - 142 : H - 86, buttonWidth = narrow ? (W - 2 * M - 12) / 2 : 205;
            ui.Panel(page, "Menu divider", new Rect(M, footer - 19, W - 2 * M, 1), new Color32(219, 227, 239, 255));
            ui.Button(page, "workshop", new Rect(M, footer, buttonWidth, 48), PlayerIcon.Pencil, ui.T("workshop"), () => game.Navigate(PlayerPage.Workshop));
            ui.Button(page, "settings", new Rect(M + buttonWidth + 12, footer, narrow ? buttonWidth : 190, 48), PlayerIcon.Settings, ui.T("settings"), () => game.OpenSettings());
            ui.Button(page, "help", new Rect(narrow ? M : M + 425, narrow ? footer + 60 : footer, buttonWidth, 48), PlayerIcon.Help, "Как играть", () => game.OpenSettings(true));
            ui.Button(page, "exit", new Rect(narrow ? M + buttonWidth + 12 : W - M - 140, narrow ? footer + 60 : footer, narrow ? buttonWidth : 140, 48), PlayerIcon.Exit, "Выйти", () => Confirm("Выйти из игры?", "Сохранённые чертежи и золото останутся в профиле.", () => UnityEngine.Application.Quit()));
        }

        void Map()
        {
            Header("Карта кампании", () => game.Navigate(PlayerPage.Home));
            if (W < 900) { CompactMap(); return; }
            float width = W - M * 2, side = Mathf.Min(370, width * .34f), height = H - 165;
            var map = ui.Panel(page, "Mission map", new Rect(M, 112, width - side - 24, height), PlayerUi.Card);
            var sketch = PlayerUi.Graphic<WorkshopMapSketch>(map, "Actual battlefield", new Rect(24, 70, width - side - 72, height - 150)); sketch.SetSession(game.Run.Session);
            ui.Label(map, "Первая страница", new Rect(24, 18, width - side - 72, 44), 29, title: true);
            ui.Label(map, "Дороги и источники текущей миссии", new Rect(24, height - 60, width - side - 72, 40), 17, PlayerUi.Muted);
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
                ui.Blueprint(card, bp, new Rect(cardW * .25f, 26, cardW * .5f, cardH - 130));
                ui.Label(card, bp == null ? ui.T("empty") : GameText.Resolve(bp.Name), new Rect(18, cardH - 96, cardW - 36, 40), 23, title: true);
                if (bp != null) ui.Label(card, ui.T("cost", game.Run.PreviewBlueprint(bp, game.Drawing.Tier).Cost), new Rect(18, cardH - 48, cardW - 90, 30), 17, PlayerUi.Blue);
                ui.IconButton(card, "Cycle blueprint " + i, new Rect(cardW - 62, cardH - 55, 44, 40), PlayerIcon.Restart, "Следующий чертёж в альбоме", () => game.CycleBlueprint(slot));
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
