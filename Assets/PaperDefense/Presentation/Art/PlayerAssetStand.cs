using System;
using System.Collections.Generic;
using PaperDefense.Application;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Complete player-art inventory and an opt-in, memory-only game preview.</summary>
    public sealed class PlayerAssetStand : MonoBehaviour
    {
        public BalancePresetAsset preset;
        public TextResourcesAsset textResources;
        PlayerUi ui;
        GameObject canvasObject, previewBar;
        RectTransform root, content, inspector;
        Text hint, counter;
        Texture2D[] textures;
        Camera galleryCamera;
        GameBalanceAsset previewBalance;
        PlayerGameRoot preview;
        readonly List<string> ids = new List<string>();
        readonly Button[] tabButtons = new Button[4];
        int tab, page;
        const int PageSize = 24;
        public int ItemCount { get; private set; }
        public int VisibleItemCount { get; private set; }
        public PlayerGameRoot Preview => preview;
        public int TextureCount => textures.Length;
        public int Tab => tab;

        void Awake()
        {
            ui = new PlayerUi(true);
            textures = Resources.LoadAll<Texture2D>("Art/Player");
            Array.Sort(textures, (a, b) => string.CompareOrdinal(a.name, b.name));
            canvasObject = CreateCanvas("Player asset album", 100, out root);
            var paper = PlayerUi.Graphic<RawImage>(root, "Paper texture", new Rect(0, 0, 1600, 1000));
            paper.texture = Resources.Load<Texture2D>("Art/Player/NotebookPaper");
            ui.Label(root, ui.T("stand.title"), new Rect(42, 20, 1190, 65), 40, title: true);
            ui.Label(root, ui.T("stand.subtitle"), new Rect(46, 83, 1120, 30), 18, PlayerUi.Ink);
            var navigator = MechanicsSceneNavigator.Attach(gameObject);
            ui.IconButton(root, "Return to mechanics hub", new Rect(1468, 25, 80, 65), PlayerIcon.Home, ui.T("stand.hub"), navigator.ReturnToHub);
            PlayerIcon[] tabs = { PlayerIcon.Book, PlayerIcon.Target, PlayerIcon.Pencil, PlayerIcon.Flag };
            for (int i = 0; i < tabs.Length; i++)
            {
                int selected = i;
                tabButtons[i] = ui.IconButton(root, "Asset tab " + i, new Rect(42 + i * 282, 126, 65, 52), tabs[i], ui.T("stand.tab_" + i), () => ShowTab(selected));
                ui.Label(root, ui.T("stand.tab_" + i), new Rect(118 + i * 282, 130, 200, 45), 21, title: true);
            }
            ui.IconButton(root, "Open player preview", new Rect(1420, 120, 125, 60), PlayerIcon.Play, ui.T("stand.preview"), OpenPreview, true);
            ui.Label(root, ui.T("stand.preview"), new Rect(1160, 130, 250, 42), 21, title: true);
            hint = ui.Label(root, ui.T("stand.inspect"), new Rect(45, 947, 1495, 38), 19, alignment: TextAnchor.MiddleCenter);
            ui.Tooltip = value => hint.text = string.IsNullOrEmpty(value) ? ui.T("stand.inspect") : value;
            if (EventSystem.current == null)
            {
                var events = new GameObject("Asset album events", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            ShowTab(0);
        }

        GameObject CreateCanvas(string name, int order, out RectTransform pageRoot)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1000); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var page = new GameObject("Album page", typeof(RectTransform)); page.transform.SetParent(go.transform, false);
            pageRoot = page.GetComponent<RectTransform>(); pageRoot.anchorMin = pageRoot.anchorMax = pageRoot.pivot = new Vector2(.5f, .5f);
            pageRoot.sizeDelta = new Vector2(1600, 1000);
            return go;
        }

        public void ShowTab(int value)
        {
            tab = Mathf.Clamp(value, 0, 3); page = 0;
            for (int i = 0; i < tabButtons.Length; i++) ui.MarkSelected(tabButtons[i], i == tab);
            Rebuild();
        }

        void Rebuild()
        {
            if (content != null) { content.gameObject.SetActive(false); Destroy(content.gameObject); }
            content = ui.Panel(root, "Asset inventory", new Rect(40, 205, 1520, 725), Color.clear);
            inspector = ui.Panel(content, "Asset inspector", new Rect(1080, 0, 440, 725), PlayerUi.Card);
            if (tab == 0) { BuildPaper(); return; }
            ids.Clear();
            if (tab == 1) foreach (PlayerIcon icon in Enum.GetValues(typeof(PlayerIcon))) ids.Add(icon.ToString());
            else foreach (var shape in NotebookVisualThemeAsset.Default.Data.Shapes)
            {
                bool included = tab == 2 ? shape.Id.StartsWith("ui.", StringComparison.Ordinal) :
                    shape.Id.StartsWith("terrain.", StringComparison.Ordinal) || shape.Id.StartsWith("object.", StringComparison.Ordinal);
                if (included) ids.Add(shape.Id);
            }
            if (tab != 1) ids.Sort(StringComparer.Ordinal);
            ItemCount = ids.Count;
            int start = page * PageSize, end = Mathf.Min(start + PageSize, ids.Count); VisibleItemCount = end - start;
            for (int i = start; i < end; i++)
            {
                string id = ids[i]; int cell = i - start;
                var card = ui.Panel(content, "Asset " + id, new Rect(cell % 6 * 177, cell / 6 * 156, 167, 145), PlayerUi.Card, true);
                var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = card.GetComponent<PlayerCardGraphic>();
                button.onClick.AddListener(() => Inspect(id));
                if (tab == 1) Icon(card, id, new Rect(55, 17, 58, 58));
                else ui.Art(card, id, new Rect(32, 7, 104, 91));
                ui.Label(card, Name(id), new Rect(9, 101, 149, 36), 15, alignment: TextAnchor.MiddleCenter);
                var tooltip = card.gameObject.AddComponent<PlayerTooltip>(); tooltip.Message = Name(id); tooltip.Display = ui.Tooltip;
            }
            ui.IconButton(content, "Previous asset page", new Rect(10, 650, 70, 55), PlayerIcon.Back, ui.T("stand.previous"), () => { page--; Rebuild(); }).interactable = page > 0;
            ui.IconButton(content, "Next asset page", new Rect(972, 650, 70, 55), PlayerIcon.Next, ui.T("stand.next"), () => { page++; Rebuild(); }).interactable = end < ids.Count;
            counter = ui.Label(content, ui.T("stand.count", start + 1, end, ids.Count), new Rect(110, 656, 830, 40), 22, alignment: TextAnchor.MiddleCenter);
            if (ids.Count > 0) Inspect(ids[start]);
        }

        string Name(string id) => ui.T("asset." + id);

        void Icon(Transform parent, string id, Rect bounds, bool sketch = true)
        {
            var icon = PlayerUi.Graphic<PlayerIconGraphic>(parent, "Icon " + id, bounds);
            icon.Icon = (PlayerIcon)Enum.Parse(typeof(PlayerIcon), id); icon.Sketch = sketch; icon.color = PlayerUi.Ink;
        }

        void ClearInspector()
        {
            for (int i = inspector.childCount - 1; i >= 0; i--)
            { var child = inspector.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
        }

        void Inspect(string id)
        {
            ClearInspector();
            ui.Label(inspector, Name(id), new Rect(28, 26, 384, 70), 30, title: true);
            if (tab == 1) Icon(inspector, id, new Rect(116, 118, 210, 210));
            else ui.Art(inspector, id, new Rect(64, 104, 312, 280));
            ui.Label(inspector, ui.T("stand.scale"), new Rect(28, 406, 384, 42), 23, title: true);
            for (int i = 0; i < 3; i++)
            {
                int size = 24 << i; float x = 28 + i * 132;
                ui.Panel(inspector, "Scale card " + size, new Rect(x, 463, 119, 124), PlayerUi.Paper);
                if (tab == 1) Icon(inspector, id, new Rect(x + (119 - size) * .5f, 475 + (96 - size) * .5f, size, size));
                else ui.Art(inspector, id, new Rect(x + (119 - size) * .5f, 475 + (96 - size) * .5f, size, size));
                ui.Label(inspector, size + " px", new Rect(x, 602, 119, 30), 17, alignment: TextAnchor.MiddleCenter);
            }
            ui.Label(inspector, id, new Rect(28, 662, 384, 34), 15, PlayerUi.Muted, alignment: TextAnchor.MiddleCenter);
        }

        void BuildPaper()
        {
            ItemCount = textures.Length; VisibleItemCount = textures.Length;
            var main = ui.Panel(content, "Illustration frame", new Rect(0, 0, 1060, 590), PlayerUi.Card);
            var image = PlayerUi.Graphic<RawImage>(main, "Selected texture", new Rect(10, 10, 1040, 570));
            if (textures.Length > 0) FitTexture(image, textures[0], new Rect(10, 10, 1040, 570));
            for (int i = 0; i < textures.Length; i++)
            {
                var texture = textures[i];
                var card = ui.Panel(content, "Texture " + texture.name, new Rect(i * 270, 610, 255, 108), PlayerUi.Card, true);
                var thumbnail = PlayerUi.Graphic<RawImage>(card, "Thumbnail", new Rect(6, 6, 243, 96)); FitTexture(thumbnail, texture, new Rect(6, 6, 243, 96));
                var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = card.GetComponent<PlayerCardGraphic>();
                button.onClick.AddListener(() => FitTexture(image, texture, new Rect(10, 10, 1040, 570)));
                var tip = card.gameObject.AddComponent<PlayerTooltip>(); tip.Message = ui.T("asset." + texture.name); tip.Display = ui.Tooltip;
            }
            ui.Label(inspector, ui.T("stand.materials"), new Rect(28, 26, 384, 60), 30, title: true);
            ui.Label(inspector, ui.T("stand.materials_body"), new Rect(28, 105, 384, 150), 21);
            var plate = ui.Panel(inspector, "Paper panel sample", new Rect(28, 280, 384, 120), PlayerUi.Paper);
            ui.Label(plate, ui.T("stand.panel_sample"), new Rect(24, 22, 335, 72), 26, title: true);
            ui.IconButton(inspector, "Primary sample", new Rect(30, 452, 105, 90), PlayerIcon.Play, ui.T("stand.primary"), () => {}, true);
            var selected = ui.IconButton(inspector, "Selected sample", new Rect(166, 452, 105, 90), PlayerIcon.Pencil, ui.T("stand.selected"), () => {}); ui.MarkSelected(selected, true);
            ui.IconButton(inspector, "Disabled sample", new Rect(303, 452, 105, 90), PlayerIcon.Trash, ui.T("stand.disabled"), () => {}).interactable = false;
            ui.Label(inspector, ui.T("stand.states"), new Rect(28, 566, 384, 88), 20);
        }

        static void FitTexture(RawImage image, Texture2D texture, Rect box)
        {
            image.texture = texture;
            float scale = Mathf.Min(box.width / texture.width, box.height / texture.height);
            var size = new Vector2(texture.width * scale, texture.height * scale);
            PlayerUi.Place(image.rectTransform, new Rect(box.position + (box.size - size) * .5f, size));
        }

        public void OpenPreview()
        {
            if (preview != null) return;
            canvasObject.SetActive(false);
            galleryCamera = Camera.main; if (galleryCamera != null) galleryCamera.enabled = false;
            previewBalance = ScriptableObject.CreateInstance<GameBalanceAsset>(); previewBalance.dataPreset = preset;
            var go = new GameObject("Isolated player preview"); go.SetActive(false); go.transform.SetParent(transform, false);
            preview = go.AddComponent<PlayerGameRoot>(); preview.Configure(previewBalance, true, true); go.SetActive(true);
            previewBar = CreateCanvas("Return to asset album", 500, out var barRoot);
            var plate = ui.Panel(barRoot, "Preview return plate", new Rect(1155, 944, 405, 50), PlayerUi.Card, true);
            ui.Label(plate, ui.T("stand.close_preview"), new Rect(15, 8, 316, 33), 18);
            ui.IconButton(plate, "Close player preview", new Rect(337, 3, 62, 44), PlayerIcon.Back, ui.T("stand.close_preview"), ClosePreview);
        }

        public void ClosePreview()
        {
            if (preview != null) { preview.gameObject.SetActive(false); Destroy(preview.gameObject); preview = null; }
            if (previewBar != null) { previewBar.SetActive(false); Destroy(previewBar); }
            if (previewBalance != null) Destroy(previewBalance);
            if (galleryCamera != null) galleryCamera.enabled = true;
            canvasObject.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(null);
        }

        void OnDestroy() { if (previewBalance != null) Destroy(previewBalance); }
    }
}
