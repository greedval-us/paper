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
        float refresh;
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
            ui.Tooltip = value =>
            {
                if (game.Page == PlayerPage.Home || game.Page == PlayerPage.Map || game.Page == PlayerPage.Preparation) return;
                if (game.Page == PlayerPage.Battle) battle.ShowHint(value);
                else if (tooltip != null) tooltip.text = value;
            };
        }

        public void ApplyLayout(int width, int height)
        {
            screenWidth = width; screenHeight = height;
            Layout = new PlayerLayout(width, height);
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
            if (!battlefield && current != PlayerPage.Workshop && current != PlayerPage.Home)
                ui.Panel(page, "Paper", new Rect(0, 0, W, H), PlayerUi.Paper, true);
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
            BuildNotebookHome();
        }

        void OpenBlueprintPicker(int slot)
        {
            var shield = ui.Panel(page, "Blueprint picker shield", new Rect(0, 0, W, H), new Color(.10f, .16f, .22f, .4f), true);
            float width = Mathf.Min(1040, W - 48), height = Mathf.Min(720, H - 48);
            var sheet = ui.Panel(shield, "Blueprint album picker", new Rect((W - width) / 2, (H - height) / 2, width, height), PlayerUi.Card, true);
            Action close = () => { shield.gameObject.SetActive(false); Destroy(shield.gameObject); };
            NotebookLabel(sheet, "Альбом · чертёж " + (slot + 1), new Rect(24, 12, width - 100, 52), 30);
            ui.IconButton(sheet, "Close blueprint picker", new Rect(width - 64, 16, 44, 40), PlayerIcon.Back, "Отменить выбор", close);
            float tabWidth = (width - 84) / 4;
            for (int i = 0; i < 4; i++)
            {
                int destination = i;
                var tab = NotebookAction(sheet, "Album slot " + i, new Rect(24 + i * (tabWidth + 12), 72, tabWidth, 42),
                    PlayerIcon.Book, "Чертёж " + (i + 1), () => { close(); OpenBlueprintPicker(destination); });
                ui.MarkSelected(tab, slot == i);
            }
            int columns = width < 760 ? 2 : 3;
            float cardW = (width - 60 - 12 * (columns - 1)) / columns;
            var album = game.Run.Workshop.Album;
            var list = ui.Scroll(sheet, "Blueprint album choices", new Rect(24, 130, width - 48, height - 154), Mathf.Ceil(album.Count / (float)columns) * 218);
            for (int i = 0; i < album.Count; i++)
            {
                var bp = album[i]; string id = bp.Id;
                int occupiedSlot = -1;
                for (int assigned = 0; assigned < 4; assigned++)
                    if (game.Run.Workshop.Loadout[assigned]?.Id == id) occupiedSlot = assigned;
                var button = ui.IconButton(list, "Pick blueprint " + id, new Rect(i % columns * (cardW + 12), i / columns * 218, cardW, 206), PlayerIcon.Book, GameText.Resolve(bp.Name), () => game.AssignBlueprint(slot, id));
                Destroy(button.GetComponentInChildren<PlayerIconGraphic>().gameObject);
                NotebookBlueprintIllustration(button.transform, bp, new Rect(20, 12, cardW - 40, 120));
                NotebookLabel(button.transform, GameText.Resolve(bp.Name), new Rect(12, 132, cardW - 24, 40), 22);
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
            if (screenWidth != Screen.width || screenHeight != Screen.height)
            {
                game.DisplaySettings.RefreshCurrentSize(Screen.width, Screen.height);
                ApplyLayout(Screen.width, Screen.height);
            }
            if (game.Page == PlayerPage.Settings && settingsFullscreen != Screen.fullScreen) Show(PlayerPage.Settings);
            refresh -= Time.unscaledDeltaTime; if (refresh > 0) return; refresh = .12f;
            if (gold != null) gold.text = ui.T("gold", game.Run.Workshop.Gold);
            if (battleRoot.gameObject.activeSelf) battle.Refresh();
        }
    }
}
