using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Page composition; navigation and simulation remain owned by their controllers.</summary>
    public sealed class PlayerShell : MonoBehaviour
    {
        PlayerGameRoot game;
        PlayerUi ui;
        RectTransform root, page, battleRoot;
        PlayerBattleHud battle;
        CanvasGroup battleInteraction;
        Text status, tooltip, gold;
        Texture2D landscape;
        float refresh;

        public void Initialize(PlayerGameRoot owner)
        {
            game = owner; ui = new PlayerUi(owner.NotebookStyle);
            landscape = Resources.Load<Texture2D>("Art/Player/NotebookLandscape");
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            gameObject.AddComponent<GraphicRaycaster>();
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1000); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var go = new GameObject("Player pages", typeof(RectTransform)); go.transform.SetParent(transform, false);
            root = go.GetComponent<RectTransform>(); root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f); root.sizeDelta = new Vector2(1600, 1000);
            battleRoot = ui.Panel(root, "Battle controls", new Rect(0, 0, 1600, 1000), Color.clear);
            battleInteraction = battleRoot.gameObject.AddComponent<CanvasGroup>();
            battle = new PlayerBattleHud(game, ui, battleRoot); battleRoot.gameObject.SetActive(false);
            ui.Tooltip = value => { if (tooltip != null) tooltip.text = value; };
            PlayerWorkshopActions.Apply(game.GetComponentInChildren<WorkshopHud>(true), ui);
        }

        public void Show(PlayerPage current)
        {
            if (page != null) { page.gameObject.SetActive(false); Destroy(page.gameObject); }
            bool battlefield = current == PlayerPage.Battle || current == PlayerPage.Pause || current == PlayerPage.Upgrades || current == PlayerPage.Result ||
                ((current == PlayerPage.Settings || current == PlayerPage.Help) && !game.Run.InWorkshop);
            battleRoot.gameObject.SetActive(battlefield);
            battleInteraction.interactable = current == PlayerPage.Battle;
            battleInteraction.blocksRaycasts = current == PlayerPage.Battle;
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            page = ui.Panel(root, "Page " + current, new Rect(0, 0, 1600, 1000), Color.clear);
            status = tooltip = gold = null;
            if (!battlefield && current != PlayerPage.Workshop) Background(current == PlayerPage.Home);
            switch (current)
            {
                case PlayerPage.Home: Home(); break;
                case PlayerPage.Map: Map(); break;
                case PlayerPage.Preparation: Preparation(); break;
                case PlayerPage.Workshop: Workshop(); break;
                case PlayerPage.Pause: Pause(); break;
                case PlayerPage.Upgrades: Upgrades(); break;
                case PlayerPage.Result: Result(); break;
                case PlayerPage.Settings: Settings(battlefield); break;
                case PlayerPage.Help: Help(battlefield); break;
            }
            tooltip = ui.Label(page, "", new Rect(160, 963, 1280, 30), 17, PlayerUi.Ink, alignment: TextAnchor.MiddleCenter);
            SetStatus(game.Status);
            if (battlefield) battle.Refresh();
        }

        void Background(bool illustrated = false)
        {
            ui.Panel(page, "Paper", new Rect(0, 0, 1600, 1000), PlayerUi.Paper, true);
            if (ui.NotebookStyle)
            {
                var paper = PlayerUi.Graphic<RawImage>(page, "Notebook paper texture", new Rect(0, 0, 1600, 1000));
                paper.texture = Resources.Load<Texture2D>("Art/Player/NotebookPaper");
            }
            if (landscape != null)
            {
                var art = PlayerUi.Graphic<RawImage>(page, "Notebook landscape", new Rect(0, 0, 1600, 1000));
                art.texture = landscape; art.color = new Color(1, 1, 1, illustrated ? 1 : .17f);
            }
            else PlayerUi.Graphic<PlayerPaperGraphic>(page, "Notebook ruling", new Rect(0, 0, 1600, 1000));
            ui.Panel(page, "Footer paper", new Rect(115, 912, 1380, 45), new Color(.98f, .966f, .92f, .93f));
            ui.Label(page, ui.T("imprint"), new Rect(130, 922, 1000, 26), 15, PlayerUi.Muted);
            ui.Label(page, "01 / 01", new Rect(1380, 920, 100, 30), 16, PlayerUi.Muted, alignment: TextAnchor.MiddleRight);
        }

        void Header(string title, System.Action back)
        {
            ui.IconButton(page, "Back", new Rect(48, 30, 58, 58), PlayerIcon.Back, ui.T("back"), back);
            ui.Label(page, title, new Rect(132, 32, 1050, 55), 38, title: true);
            gold = ui.Label(page, ui.T("gold", game.Run.Workshop.Gold), new Rect(1240, 40, 270, 40), 22, PlayerUi.Blue, alignment: TextAnchor.MiddleRight);
            gold.name = "Player gold";
        }

        void Home()
        {
            ui.Label(page, ui.T("eyebrow"), new Rect(142, 100, 650, 36), 17, PlayerUi.Blue);
            ui.Label(page, ui.T("title_first"), new Rect(132, 150, 700, 130), 88, title: true);
            ui.Label(page, ui.T("title_second"), new Rect(132, 272, 700, 130), 88, title: true);
            ui.Label(page, ui.T("tagline"), new Rect(142, 420, 590, 70), 27, PlayerUi.Muted);
            if (landscape == null) ui.Art(page, "object.castle", new Rect(845, 205, 600, 520));
            ui.Panel(page, "Menu paper", new Rect(115, 680, 640, 160), new Color(.98f, .966f, .92f, .94f));
            ui.IconButton(page, "Open campaign", new Rect(142, 538, 100, 100), PlayerIcon.Play, ui.T("open_map"), () => game.Navigate(PlayerPage.Map), true);
            ui.Label(page, ui.T("open_map"), new Rect(270, 542, 400, 45), 32, title: true);
            ui.Label(page, ui.T("chapter"), new Rect(272, 588, 440, 36), 19, PlayerUi.Muted);
            HomeShortcut(142, PlayerIcon.Book, "workshop", () => game.Navigate(PlayerPage.Workshop));
            HomeShortcut(302, PlayerIcon.Settings, "settings", () => game.OpenSettings());
            HomeShortcut(462, PlayerIcon.Help, "help", () => game.OpenSettings(true));
            HomeShortcut(622, PlayerIcon.Exit, "exit", () => UnityEngine.Application.Quit());
        }

        void HomeShortcut(float x, PlayerIcon icon, string key, System.Action action)
        {
            ui.IconButton(page, key, new Rect(x, 707, 70, 70), icon, ui.T(key), action);
            ui.Label(page, ui.T(key), new Rect(x - 28, 788, 130, 38), 17, PlayerUi.Muted, alignment: TextAnchor.MiddleCenter);
        }

        void Map()
        {
            Header(ui.T("campaign"), () => game.Navigate(PlayerPage.Home));
            ui.Label(page, ui.T("chapter"), new Rect(132, 125, 1050, 48), 28, title: true);
            var mapCard = ui.Panel(page, "Mission map", new Rect(130, 205, 875, 642), PlayerUi.Card);
            var map = PlayerUi.Graphic<WorkshopMapSketch>(mapCard, "Actual battlefield", new Rect(26, 40, 822, 540)); map.SetSession(game.Run.Session);
            ui.Label(mapCard, ui.T("map_caption"), new Rect(30, 580, 810, 40), 17, PlayerUi.Muted, alignment: TextAnchor.MiddleCenter);
            var info = ui.Panel(page, "Mission information", new Rect(1040, 205, 430, 642), PlayerUi.Card);
            ui.Label(info, ui.T("mission_number"), new Rect(32, 30, 360, 30), 16, PlayerUi.Blue);
            ui.Art(info, "object.castle", new Rect(120, 75, 190, 190));
            ui.Label(info, ui.T("mission"), new Rect(32, 280, 370, 55), 36, title: true);
            ui.Label(info, ui.T("mission_brief", game.Run.Session.Balance.Waves.Count), new Rect(32, 348, 360, 142), 21, PlayerUi.Muted);
            ui.IconButton(info, "Prepare mission", new Rect(290, 520, 90, 80), PlayerIcon.Play, ui.T("prepare"), () => game.Navigate(PlayerPage.Preparation), true);
            ui.Label(info, ui.T("prepare"), new Rect(32, 530, 240, 65), 23, title: true);
        }

        void Preparation()
        {
            Header(ui.T("prepare"), () => game.Navigate(PlayerPage.Map));
            ui.Label(page, ui.T("loadout"), new Rect(132, 132, 1200, 50), 32, title: true);
            ui.Label(page, ui.T("loadout_hint"), new Rect(132, 190, 1200, 38), 19, PlayerUi.Muted);
            for (int i = 0; i < 4; i++)
            {
                int slot = i; var bp = game.Run.Workshop.Loadout[i];
                var card = ui.Panel(page, "Loadout " + i, new Rect(130 + i * 338, 258, 310, 330), PlayerUi.Card);
                ui.Label(card, "0" + (i + 1), new Rect(22, 16, 100, 36), 22, PlayerUi.Muted);
                ui.Blueprint(card, bp, new Rect(65, 52, 180, 160));
                ui.Label(card, bp == null ? ui.T("empty") : GameText.Resolve(bp.Name), new Rect(22, 219, 266, 48), 22, title: true, alignment: TextAnchor.MiddleCenter);
                if (bp != null) ui.Label(card, ui.T("cost", game.Run.PreviewBlueprint(bp, game.Drawing.Tier).Cost), new Rect(22, 280, 190, 28), 18, PlayerUi.Blue);
                ui.IconButton(card, "Cycle blueprint " + i, new Rect(236, 269, 52, 44), PlayerIcon.Restart, ui.T("cycle_blueprint"), () => game.CycleBlueprint(slot));
            }
            var materials = ui.Panel(page, "Preparation material", new Rect(130, 632, 590, 135), PlayerUi.Card);
            ui.Label(materials, ui.T("material"), new Rect(24, 15, 540, 35), 23, title: true);
            string[] materialIds = { "pencil", "pen", "marker" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var b = ui.IconButton(materials, "Starting material " + i, new Rect(24 + i * 180, 62, 150, 54), PlayerIcon.Drop, ui.T("material_" + i), () => { game.Drawing.SelectTier((MaterialTier)index); Show(PlayerPage.Preparation); }, art: "ui.material." + materialIds[i]);
                ui.MarkSelected(b, (int)game.Drawing.Tier == i);
            }
            ui.Label(page, ui.T("preparation_note"), new Rect(770, 642, 680, 110), 21, PlayerUi.Muted);
            ui.IconButton(page, "Edit album", new Rect(130, 814, 78, 78), PlayerIcon.Book, ui.T("workshop"), () => game.Navigate(PlayerPage.Workshop));
            ui.Label(page, ui.T("workshop"), new Rect(228, 825, 470, 55), 25, title: true);
            ui.Label(page, ui.T("start_battle"), new Rect(1010, 825, 320, 55), 29, title: true, alignment: TextAnchor.MiddleRight);
            ui.IconButton(page, "Start battle", new Rect(1360, 808, 100, 84), PlayerIcon.Play, ui.T("start_battle"), () => game.StartBattle(), true);
            status = ui.Label(page, "", new Rect(350, 892, 1000, 38), 18, NotebookPalette.Red, alignment: TextAnchor.MiddleCenter);
        }

        void Workshop()
        {
            ui.Panel(page, "Workshop navigation", new Rect(0, 0, 1600, 90), PlayerUi.Paper, true);
            Header(ui.T("workshop"), () => game.Navigate(PlayerPage.Preparation));
        }

        RectTransform Modal(string title)
        {
            ui.Panel(page, "Modal input shield", new Rect(0, 0, 1600, 1000), new Color(.08f, .16f, .27f, .42f), true);
            var panel = ui.Panel(page, "Modal", new Rect(430, 225, 740, 525), PlayerUi.Card, true);
            ui.Label(panel, title, new Rect(40, 30, 660, 70), 48, title: true, alignment: TextAnchor.MiddleCenter);
            return panel;
        }

        void Pause()
        {
            var panel = Modal(ui.T("paused"));
            ui.Label(panel, ui.T("pause_hint"), new Rect(70, 116, 600, 70), 22, PlayerUi.Muted, alignment: TextAnchor.MiddleCenter);
            ui.IconButton(panel, "Resume", new Rect(310, 220, 120, 96), PlayerIcon.Play, ui.T("resume"), game.Resume, true);
            ui.IconButton(panel, "Pause settings", new Rect(110, 355, 70, 66), PlayerIcon.Settings, ui.T("settings"), () => game.OpenSettings());
            ui.IconButton(panel, "Restart battle", new Rect(335, 355, 70, 66), PlayerIcon.Restart, ui.T("restart"), game.Retry);
            ui.IconButton(panel, "Leave battle", new Rect(560, 355, 70, 66), PlayerIcon.Map, ui.T("leave"), game.ReturnToMap);
            ui.Label(panel, ui.T("leave_note"), new Rect(50, 452, 640, 50), 17, PlayerUi.Muted, alignment: TextAnchor.MiddleCenter);
        }

        void Upgrades()
        {
            var panel = Modal(ui.T("upgrade_title"));
            ui.Label(panel, ui.T("upgrade_hint"), new Rect(40, 105, 660, 45), 20, PlayerUi.Muted, alignment: TextAnchor.MiddleCenter);
            PlayerIcon[] icons = { PlayerIcon.Fast, PlayerIcon.Drop, PlayerIcon.Restart };
            var rules = game.Run.Session.Balance.Progression;
            string[] details = {
                ui.T("upgrade_stats_0", Mathf.RoundToInt((rules.RapidFireMultiplier - 1) * 100)),
                ui.T("upgrade_stats_1", Mathf.RoundToInt((rules.DeepInkDamageMultiplier - 1) * 100), Mathf.RoundToInt((rules.DeepInkHealthMultiplier - 1) * 100)),
                ui.T("upgrade_stats_2", rules.SalvageImmediateInk, Mathf.RoundToInt((rules.SalvageWaveRewardMultiplier - 1) * 100), Mathf.RoundToInt(rules.SalvageRefundBonus * 100))
            };
            for (int i = 0; i < 3; i++)
            {
                int selected = i;
                ui.IconButton(panel, "Choose upgrade " + i, new Rect(65 + 225 * i, 195, 160, 120), icons[i], ui.T("upgrade_" + i).Replace('\n', ' '), () => game.ChooseUpgrade((RunUpgrade)selected), true);
                ui.Label(panel, ui.T("upgrade_" + i), new Rect(45 + 225 * i, 330, 200, 64), 23, title: true, alignment: TextAnchor.UpperCenter);
                ui.Label(panel, details[i], new Rect(45 + 225 * i, 400, 200, 100), 17, PlayerUi.Muted, alignment: TextAnchor.UpperCenter);
            }
        }

        void Result()
        {
            bool win = game.Run.Session.Phase == RunPhase.Victory;
            ui.Panel(page, "Result input shield", new Rect(0, 0, 1600, 1000), new Color(.08f, .16f, .27f, .45f), true);
            var panel = ui.Panel(page, "Battle report", new Rect(235, 130, 1130, 730), PlayerUi.Card, true);
            ui.Label(panel, ui.T(win ? "victory" : "defeat"), new Rect(40, 25, 1050, 78), 48, title: true, alignment: TextAnchor.MiddleCenter);
            ui.Art(panel, win ? "object.castle" : "object.castle.damaged", new Rect(55, 115, 220, 230));
            var s = game.Run.Session;
            ui.Label(panel, ui.T("result", s.WaveIndex, s.Balance.Waves.Count, s.Kills, s.GoldEarned), new Rect(325, 140, 720, 180), 27);
            var stats = s.Statistics;
            ui.Label(panel, ui.T("result_stats_left", stats.BuildInvestment + stats.UpgradeInvestment, stats.RefundedInk, stats.LostInvestment, stats.PeakAliveEnemies), new Rect(55, 365, 490, 165), 22);
            ui.Label(panel, ui.T("result_stats_right", Mathf.RoundToInt(stats.TotalDamage), stats.TotalStructuresBuilt, stats.DestroyedStructures, stats.Upgrades.Count), new Rect(590, 365, 490, 165), 22);
            ui.IconButton(panel, "Result map", new Rect(360, 580, 100, 78), PlayerIcon.Map, ui.T("open_map"), game.ReturnToMap);
            ui.IconButton(panel, "Result retry", new Rect(515, 580, 100, 78), PlayerIcon.Restart, ui.T("restart"), game.Retry, true);
            ui.IconButton(panel, "Result workshop", new Rect(670, 580, 100, 78), PlayerIcon.Book, ui.T("workshop"), () => { game.Run.OpenWorkshop(); game.Navigate(PlayerPage.Workshop); });
            ui.Label(panel, GameText.Resolve(game.Run.Workshop.SaveMessage), new Rect(35, 684, 1060, 30), 16, PlayerUi.Muted, alignment: TextAnchor.MiddleCenter);
        }

        void Settings(bool modal)
        {
            var panel = modal ? Modal(ui.T("settings")) : ui.Panel(page, "Settings", new Rect(300, 175, 1000, 620), PlayerUi.Card);
            if (!modal) ui.Label(panel, ui.T("settings"), new Rect(50, 35, 850, 65), 44, title: true);
            ui.IconButton(panel, "Toggle fullscreen", new Rect(65, 140, 75, 70), PlayerIcon.Target, ui.T("fullscreen"), () => { Screen.fullScreen = !Screen.fullScreen; });
            ui.Label(panel, ui.T("fullscreen"), new Rect(170, 150, 480, 50), 25);
            var ranges = ui.IconButton(panel, "Toggle ranges", new Rect(65, 240, 75, 70), PlayerIcon.Target, ui.T("ranges"), () => { game.World.ShowRanges = !game.World.ShowRanges; Show(PlayerPage.Settings); });
            ui.MarkSelected(ranges, game.World.ShowRanges);
            ui.Label(panel, ui.T("ranges"), new Rect(170, 250, 480, 50), 25);
            ui.IconButton(panel, "Close settings", new Rect(65, 375, 75, 70), PlayerIcon.Back, ui.T("back"), game.CloseSettings);
            ui.IconButton(panel, "Open help", new Rect(170, 375, 75, 70), PlayerIcon.Help, ui.T("help"), () => game.Navigate(PlayerPage.Help));
        }

        void Help(bool modal)
        {
            var panel = modal ? Modal(ui.T("help")) : ui.Panel(page, "Help", new Rect(265, 160, 1070, 680), PlayerUi.Card);
            if (!modal) ui.Label(panel, ui.T("help"), new Rect(50, 30, 960, 65), 44, title: true);
            ui.Label(panel, ui.T("help_body"), new Rect(50, 120, modal ? 640 : 960, modal ? 270 : 400), modal ? 19 : 24);
            ui.IconButton(panel, "Close help", new Rect(55, modal ? 418 : 565, 75, 65), PlayerIcon.Back, ui.T("back"), game.CloseSettings);
        }

        public void SetStatus(string message) { if (status != null) status.text = message; }
        void Update()
        {
            refresh -= Time.unscaledDeltaTime;
            if (refresh > 0) return;
            refresh = .12f;
            if (gold != null) gold.text = ui.T("gold", game.Run.Workshop.Gold);
            if (battleRoot.gameObject.activeSelf) battle.Refresh();
        }
    }
}
