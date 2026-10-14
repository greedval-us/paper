using System;
using System.Collections.Generic;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>A single launch page for the isolated saved mechanics and art scenes.</summary>
    public sealed class MechanicsSceneHub : MonoBehaviour
    {
        public MechanicsSceneCatalog catalog;
        public TextResourcesAsset textResources;
        public int VisibleSceneCount { get; private set; }
        public string SelectedGroupKey { get; private set; }
        readonly List<KeyValuePair<MechanicsSceneEntry, Button>> cards = new List<KeyValuePair<MechanicsSceneEntry, Button>>();
        readonly Dictionary<string, Button> filters = new Dictionary<string, Button>();
        MechanicsSceneNavigator navigator;
        UserTextResolver text;
        Font font;
        Text status;
        string T(string key, params object[] arguments) => text.Resolve(UserText.Message(key, arguments));

        void Awake()
        {
            catalog = catalog != null ? catalog : MechanicsSceneCatalog.Default;
            text = textResources != null ? textResources.CreateResolver() : TextResourcesAsset.Default;
            font = Resources.Load<Font>("NotebookHand") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            navigator = MechanicsSceneNavigator.Attach(gameObject);
            var root = new GameObject("Mechanics hub canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1000); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var page = Panel(root.transform, "Mechanics hub page", new Rect(0, 0, 1600, 1000), new Color(.975f, .961f, .9f));
            page.anchorMin = page.anchorMax = new Vector2(.5f, 1); page.anchoredPosition = new Vector2(-800, 0);
            var paper = new GameObject("Notebook grid", typeof(RectTransform), typeof(CanvasRenderer), typeof(NotebookGalleryPaper));
            paper.transform.SetParent(page, false); Place(paper.GetComponent<RectTransform>(), new Rect(0, 0, 1600, 1000));
            paper.GetComponent<NotebookGalleryPaper>().raycastTarget = false;
            Label(page, "Hub title", T("hub.title"), new Rect(40, 24, 1520, 60), 38, true);
            Label(page, "Hub hint", T("hub.hint"), new Rect(42, 86, 1510, 45), 20);
            if (EventSystem.current == null)
            {
                var events = new GameObject("Hub UI events", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            status = Label(page, "Hub status", T("hub.loading"), new Rect(42, 944, 1510, 40), 20);
            if (catalog == null) { status.text = T("hub.unavailable", MechanicsSceneCatalog.ResourcePath); return; }
            var groups = new List<string>();
            foreach (var entry in catalog.Scenes) if (!groups.Contains(entry.GroupKey)) groups.Add(entry.GroupKey);
            float tabWidth = 1520f / (groups.Count + 1);
            AddFilter(page, null, 0, tabWidth);
            for (int i = 0; i < groups.Count; i++) AddFilter(page, groups[i], i + 1, tabWidth);
            foreach (var entry in catalog.Scenes)
            {
                var route = entry;
                var button = MakeButton(page, "Open test " + route.Id, new Rect(0, 0, 368, 96), () => navigator.OpenScene(route.ScenePath));
                Label(button.transform, "Scene title", T(route.TitleKey), new Rect(14, 3, 340, 48), 18, true);
                Label(button.transform, "Scene description", T(route.DescriptionKey), new Rect(14, 53, 340, 40), 15);
                cards.Add(new KeyValuePair<MechanicsSceneEntry, Button>(route, button));
            }
            navigator.StatusChanged += UpdateStatus;
            ShowGroup();
        }
        void OnDestroy() { if (navigator != null) navigator.StatusChanged -= UpdateStatus; }
        void AddFilter(Transform parent, string group, int index, float width)
        {
            string key = group ?? "";
            var button = MakeButton(parent, "Hub group " + (group ?? "all"), new Rect(40 + index * width, 143, width - 10, 48), () => ShowGroup(group));
            Label(button.transform, "Group title", T(group ?? "hub.all"), new Rect(6, 1, width - 22, 46), 19, true, TextAnchor.MiddleCenter);
            filters.Add(key, button);
        }
        public void ShowGroup(string key = null)
        {
            if (key != null && !filters.ContainsKey(key)) key = null;
            SelectedGroupKey = key; VisibleSceneCount = 0;
            foreach (var item in cards)
            {
                bool visible = key == null || item.Key.GroupKey == key; item.Value.gameObject.SetActive(visible);
                if (!visible) continue;
                int index = VisibleSceneCount++;
                Place(item.Value.GetComponent<RectTransform>(), new Rect(40 + index % 4 * 384, 220 + index / 4 * 100, 368, 96));
            }
            foreach (var item in filters)
                item.Value.GetComponent<Image>().color = item.Key == (key ?? "") ? new Color(.78f, .87f, .96f) : Color.white;
            UpdateStatus();
        }
        void UpdateStatus()
        {
            if (status == null) return;
            status.text = navigator.IsLoading ? T("hub.loading") : !string.IsNullOrEmpty(navigator.LastError) ? navigator.LastError
                : T("hub.count", VisibleSceneCount, catalog != null ? catalog.Scenes.Count : 0);
            foreach (var item in cards) item.Value.interactable = !navigator.IsLoading;
            foreach (var item in filters) item.Value.interactable = !navigator.IsLoading;
        }
        RectTransform Panel(Transform parent, string name, Rect box, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); Place(rect, box);
            go.GetComponent<Image>().color = color; go.GetComponent<Image>().raycastTarget = false; return rect;
        }
        Button MakeButton(Transform parent, string name, Rect box, Action action)
        {
            var panel = Panel(parent, name, box, Color.white); var image = panel.GetComponent<Image>(); image.raycastTarget = true;
            var outline = panel.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.07f, .12f, .33f, .35f); outline.effectDistance = new Vector2(1, -1);
            var button = panel.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => action()); return button;
        }
        Text Label(Transform parent, string name, string value, Rect box, int size, bool bold = false, TextAnchor align = TextAnchor.UpperLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false); Place(go.GetComponent<RectTransform>(), box);
            var label = go.GetComponent<Text>(); label.font = font; label.text = value; label.fontSize = size;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; label.color = NotebookPalette.Navy;
            label.alignment = align; label.raycastTarget = false; label.verticalOverflow = VerticalWrapMode.Truncate; return label;
        }
        static void Place(RectTransform rect, Rect box)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(box.x, -box.y); rect.sizeDelta = box.size; }
    }
}
