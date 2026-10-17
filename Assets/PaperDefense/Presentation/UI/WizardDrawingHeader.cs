using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Screen 06 framing over the existing wizard commands and live profile values.</summary>
    internal sealed class WizardDrawingHeader
    {
        readonly RectTransform board;
        readonly Func<string, RectTransform> find;
        readonly PlayerGameRoot player;
        readonly WorkshopUi ui = new WorkshopUi();
        readonly Dictionary<Graphic, GraphicState> originals = new Dictionary<Graphic, GraphicState>();
        readonly List<GameObject> decorations = new List<GameObject>();
        RectTransform layer;
        Button settings, help;
        bool applied, startWasActive, titleWasActive;

        internal WizardDrawingHeader(RectTransform board, Func<string, RectTransform> find, PlayerGameRoot player)
        { this.board = board; this.find = find; this.player = player; }

        internal void Apply(float width, float height, float offset, bool compact)
        {
            if (!applied) Remember();
            if (layer == null)
            {
                layer = new GameObject("Drawing header decoration", typeof(RectTransform)).GetComponent<RectTransform>();
                layer.SetParent(board, false);
            }
            layer.gameObject.SetActive(true); layer.SetSiblingIndex(2);
            WorkshopUi.Place(layer, new Rect(0, 0, board.sizeDelta.x, height));
            find("Wizard title")?.gameObject.SetActive(false);
            find("Start run")?.gameObject.SetActive(false);
            var hand = Resources.Load<Font>("NotebookHand");

            var header = Detail(layer, "Drawing top frame", new Rect(offset, 1, width, 63));
            header.Fill = WorkshopUi.Paper; header.Radius = 8;
            Illustration(layer, "Drawing workshop illustration", "icon.workshop", new Rect(offset + 16, 2, compact ? 60 : 80, 60));
            Label(layer, "Drawing workshop lettering", "Мастерская", new Rect(offset + (compact ? 84 : 112), 3, compact ? 238 : 250, 57), hand, compact ? 36 : 42);

            float goldX = offset + (compact ? 334 : 386), goldWidth = compact ? 178 : 194;
            var goldIcon = Own<FoundationDetailGraphic>(layer, "Drawing gold coin", new Rect(goldX, 12, 38, 38));
            goldIcon.Shape = FoundationDetailGraphic.Kind.GoldCoin; goldIcon.color = WorkshopUi.Ink;
            var gold = find("Wizard gold")?.GetComponent<Text>();
            if (gold != null)
            {
                WorkshopUi.Place(gold.rectTransform, new Rect(goldX + 47, 8, goldWidth - 47, 48));
                Lettering(gold, hand, compact ? 26 : 29, TextAnchor.MiddleLeft);
            }
            float albumX = goldX + goldWidth + 20, albumWidth = compact ? 168 : 186;
            Place("Wizard album", new Rect(albumX, 6, albumWidth, 52));
            StyleButton("Wizard album", hand, compact ? 27 : 30, false, false);
            var albumFrame = find("Wizard album")?.Find("Drawing button frame")?.GetComponent<FoundationDetailGraphic>();
            if (albumFrame != null) albumFrame.Fill = Color.clear;

            float sceneryX = albumX + albumWidth + 18;
            float sceneryWidth = Mathf.Max(80, offset + width - 148 - sceneryX);
            Landscape(sceneryX, sceneryWidth);
            NavigationButtons(offset + width - 130, hand);

            float rowWidth = Mathf.Min(width - 110, compact ? 1000 : 1060);
            float gap = compact ? 44 : 68, tabWidth = (rowWidth - 3 * gap) / 4;
            float rowX = offset + (width - rowWidth) * .5f;
            const float stepY = 76, stepHeight = 49, diameter = 40;
            for (int i = 0; i < 4; i++)
            {
                var bounds = new Rect(rowX + i * (tabWidth + gap), stepY, tabWidth, stepHeight);
                Place("Wizard step " + i, bounds);
                var button = find("Wizard step " + i);
                if (button == null) continue;
                HideSurface(button);
                var symbol = button.Find("Icon")?.GetComponent<WorkshopIconGraphic>();
                if (symbol != null) symbol.enabled = false;
                var caption = button.Find("Caption")?.GetComponent<Text>();
                if (caption != null) caption.enabled = false;
                var tab = Detail(layer, "Drawing step tab " + i, bounds);
                tab.Selected = i == 2; tab.Fill = i == 2 ? WorkshopUi.Selected : WorkshopUi.Paper; tab.Radius = 11;
                button.GetComponent<WorkshopButton>()?.SetDecoration(tab);
                var circle = Own<FoundationDetailGraphic>(layer, "Drawing step circle " + i,
                    new Rect(bounds.x + 9, bounds.y + 4.5f, diameter, diameter));
                circle.Shape = FoundationDetailGraphic.Kind.ProgressCircle; circle.Selected = i == 2; circle.color = WorkshopUi.Ink;
                var number = find("Wizard progress " + i)?.GetComponent<Text>();
                if (number != null)
                {
                    number.gameObject.SetActive(true); number.enabled = true;
                    WorkshopUi.Place(number.rectTransform, new Rect(bounds.x + 9, bounds.y + 4.5f, diameter, diameter));
                    Lettering(number, hand, 30, TextAnchor.MiddleCenter, i == 2 ? WorkshopUi.Paper : WorkshopUi.Ink);
                }
                var check = find("Wizard completed " + i)?.GetComponent<WorkshopIconGraphic>();
                if (check != null) check.enabled = false;
                Label(layer, "Drawing step lettering " + i, caption == null ? "" : caption.text,
                    new Rect(bounds.x + 57, bounds.y + 1, tabWidth - 67, stepHeight - 2), hand, compact ? 26 : 29, TextAnchor.MiddleCenter);
                if (i < 3)
                    Icon(layer, "Drawing step arrow " + i, WorkshopIcon.Next,
                        new Rect(bounds.xMax + (gap - 32) * .5f, bounds.y + 12, 32, 26));
            }

            Place("Wizard back", new Rect(offset + 12, height - 74, 232, 64));
            StyleButton("Wizard back", hand, compact ? 31 : 34, false, false);
            Place("Wizard next", new Rect(offset + width - 308, height - 74, 296, 64));
            StyleButton("Wizard next", hand, compact ? 30 : 33, true, true);
            var notice = find("Wizard notice")?.GetComponent<Text>();
            float noticeX = offset + 264, noticeWidth = width - 594;
            var backing = Detail(notice == null ? layer : notice.transform.parent, "Drawing notice backing",
                new Rect(noticeX, height - 69, noticeWidth, 54));
            backing.Fill = WorkshopUi.Paper;
            backing.gameObject.SetActive(notice != null && !string.IsNullOrWhiteSpace(notice.text));
            if (notice != null)
            {
                backing.transform.SetSiblingIndex(Mathf.Max(0, notice.transform.GetSiblingIndex() - 1));
                WorkshopUi.Place(notice.rectTransform, new Rect(noticeX + 12, height - 68, noticeWidth - 24, 52));
                Lettering(notice, hand, 22, TextAnchor.MiddleCenter, WorkshopUi.Warning);
            }
        }

        void Landscape(float x, float width)
        {
            Illustration(layer, "Drawing header mountain", "terrain.mountain", new Rect(x + width * .27f, 8, width * .27f, 50));
            Illustration(layer, "Drawing header castle", "object.castle", new Rect(x + width * .62f, 8, width * .20f, 50));
            float[] positions = { .02f, .14f, .52f, .84f, .94f };
            for (int i = 0; i < positions.Length; i++)
            {
                float size = i == 1 || i == 3 ? 46 : 30;
                Illustration(layer, "Drawing header pine " + i, "terrain.pine", new Rect(x + width * positions[i] - size * .5f, 59 - size, size, size));
            }
            var ground = Own<FoundationDetailGraphic>(layer, "Drawing header landscape ground", new Rect(x - 8, 58, width + 16, 2));
            ground.Shape = FoundationDetailGraphic.Kind.Divider; ground.color = WorkshopUi.Ink;
        }

        void NavigationButtons(float x, Font hand)
        {
            if (player == null) return;
            if (settings == null)
            {
                settings = ui.IconButton(layer, "Guided drawing settings", new Rect(x, 8, 52, 48), WorkshopIcon.Settings, "Настройки", () => player.OpenSettings());
                help = ui.IconButton(layer, "Guided drawing help", new Rect(x + 64, 8, 52, 48), WorkshopIcon.Help, "Помощь", () => player.OpenSettings(true));
                decorations.Add(settings.gameObject); decorations.Add(help.gameObject);
            }
            foreach (var button in new[] { settings, help })
            {
                button.gameObject.SetActive(true);
                WorkshopUi.Place((RectTransform)button.transform, new Rect(button == settings ? x : x + 64, 8, 52, 48));
                HideSurface((RectTransform)button.transform);
                var frame = Detail(button.transform, "Drawing header icon frame", new Rect(0, 0, 52, 48));
                frame.Fill = WorkshopUi.Paper; frame.Radius = 10;
                button.GetComponent<WorkshopButton>()?.SetDecoration(frame);
                var symbol = button.transform.Find("Icon")?.GetComponent<WorkshopIconGraphic>();
                if (symbol != null) WorkshopUi.Place(symbol.rectTransform, new Rect(9, 7, 34, 34));
            }
        }

        void Remember()
        {
            string[] roots = { "Wizard title", "Wizard gold", "Wizard album", "Wizard back", "Wizard next", "Wizard notice",
                "Wizard step 0", "Wizard step 1", "Wizard step 2", "Wizard step 3", "Wizard progress 0", "Wizard progress 1",
                "Wizard progress 2", "Wizard progress 3", "Wizard completed 0", "Wizard completed 1", "Wizard completed 2", "Wizard completed 3" };
            foreach (string name in roots)
            {
                var item = find(name); if (item == null) continue;
                foreach (var graphic in item.GetComponentsInChildren<Graphic>(true))
                    if (!(graphic is FoundationDetailGraphic) && !decorations.Contains(graphic.gameObject))
                        originals[graphic] = new GraphicState(graphic);
            }
            var start = find("Start run"); startWasActive = start != null && start.gameObject.activeSelf;
            var title = find("Wizard title"); titleWasActive = title != null && title.gameObject.activeSelf;
            applied = true;
        }

        internal void Restore(int step)
        {
            if (!applied) return;
            foreach (var item in originals) if (item.Key != null) item.Value.Restore(item.Key);
            if (layer != null) layer.gameObject.SetActive(false);
            foreach (var item in decorations) if (item != null) item.SetActive(false);
            find("Wizard title")?.gameObject.SetActive(titleWasActive);
            find("Start run")?.gameObject.SetActive(startWasActive);
            foreach (string name in new[] { "Wizard back", "Wizard album", "Wizard next", "Wizard step 0", "Wizard step 1", "Wizard step 2", "Wizard step 3" })
                find(name)?.GetComponent<WorkshopButton>()?.SetDecoration(null);
            for (int i = 0; i < 4; i++)
            {
                find("Wizard progress " + i)?.gameObject.SetActive(i >= step);
                find("Wizard completed " + i)?.gameObject.SetActive(i < step);
                find("Wizard step " + i)?.GetComponent<WorkshopButton>()?.SetChosen(i == step);
            }
            originals.Clear(); applied = false;
        }

        void StyleButton(string name, Font hand, int size, bool primary, bool iconOnRight)
        {
            var button = find(name); if (button == null) return;
            HideSurface(button);
            var frame = Detail(button, "Drawing button frame", new Rect(0, 0, button.sizeDelta.x, button.sizeDelta.y));
            frame.Fill = primary ? WorkshopUi.Blue : WorkshopUi.Paper; frame.color = primary ? WorkshopUi.Blue : WorkshopUi.Ink;
            button.GetComponent<WorkshopButton>()?.SetDecoration(frame);
            var symbol = button.Find("Icon")?.GetComponent<WorkshopIconGraphic>();
            float iconSize = name == "Wizard album" ? 35 : 38;
            if (symbol != null)
            {
                symbol.enabled = true; symbol.color = primary ? WorkshopUi.Paper : WorkshopUi.Ink;
                WorkshopUi.Place(symbol.rectTransform, new Rect(iconOnRight ? button.sizeDelta.x - 55 : 16, (button.sizeDelta.y - iconSize) * .5f, iconSize, iconSize));
            }
            var caption = button.Find("Caption")?.GetComponent<Text>();
            if (caption != null)
            {
                caption.enabled = true;
                Lettering(caption, hand, size, TextAnchor.MiddleCenter, primary ? WorkshopUi.Paper : WorkshopUi.Ink);
                WorkshopUi.Place(caption.rectTransform, new Rect(iconOnRight ? 10 : 60, 3, button.sizeDelta.x - 74, button.sizeDelta.y - 6));
            }
        }

        static void HideSurface(RectTransform button)
        {
            var surface = button.GetComponent<WorkshopSurfaceGraphic>();
            if (surface == null) return;
            surface.canvasRenderer.cullTransparentMesh = false; surface.canvasRenderer.SetAlpha(0);
        }

        void Place(string name, Rect bounds)
        { var item = find(name); if (item != null) WorkshopUi.Place(item, bounds); }

        T Own<T>(Transform parent, string name, Rect bounds) where T : Graphic
        {
            var child = parent.Find(name);
            var graphic = child == null ? WorkshopUi.Graphic<T>(parent, name, bounds) : child.GetComponent<T>();
            WorkshopUi.Place(graphic.rectTransform, bounds); graphic.gameObject.SetActive(true); graphic.raycastTarget = false;
            if (!decorations.Contains(graphic.gameObject)) decorations.Add(graphic.gameObject);
            return graphic;
        }

        FoundationDetailGraphic Detail(Transform parent, string name, Rect bounds)
        {
            var graphic = Own<FoundationDetailGraphic>(parent, name, bounds);
            graphic.Shape = FoundationDetailGraphic.Kind.Frame; graphic.color = WorkshopUi.Ink;
            if (parent != layer) graphic.transform.SetAsFirstSibling();
            return graphic;
        }

        void Icon(Transform parent, string name, WorkshopIcon icon, Rect bounds)
        { var graphic = Own<WorkshopIconGraphic>(parent, name, bounds); graphic.Icon = icon; graphic.color = WorkshopUi.Ink; }

        void Illustration(Transform parent, string name, string id, Rect bounds)
        {
            var image = Own<Image>(parent, name, bounds);
            var house=id=="icon.workshop"?Resources.Load<Sprite>("Art/Player/DrawingWorkshopHouse"):null;
            if(house!=null)image.sprite=house;
            else if (NotebookSpriteCatalog.TryGet(id, out var sprite)) image.sprite = sprite;
            image.preserveAspect = true;
            image.rectTransform.pivot = new Vector2(.5f, .5f);
            image.rectTransform.anchoredPosition += new Vector2(bounds.width * .5f, -bounds.height * .5f);
        }

        void Label(Transform parent, string name, string value, Rect bounds, Font hand, int size, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = Own<Text>(parent, name, bounds); label.text = value;
            Lettering(label, hand, size, alignment);
        }

        static void Lettering(Text label, Font hand, int size, TextAnchor alignment, Color? tint = null)
        {
            label.font = hand != null ? hand : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size; label.resizeTextForBestFit = true; label.resizeTextMinSize = 17; label.resizeTextMaxSize = size;
            label.alignment = alignment; label.color = tint ?? WorkshopUi.Ink;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            label.supportRichText = false;
        }

        sealed class GraphicState
        {
            readonly Vector2 anchorMin, anchorMax, pivot, position, size;
            readonly Color tint;
            readonly bool enabled, cullTransparent, bestFit, richText;
            readonly float alpha;
            readonly Font font;
            readonly int fontSize, minSize, maxSize;
            readonly TextAnchor alignment;
            readonly HorizontalWrapMode horizontalOverflow;
            readonly VerticalWrapMode verticalOverflow;
            internal GraphicState(Graphic graphic)
            {
                var rect = graphic.rectTransform;
                anchorMin = rect.anchorMin; anchorMax = rect.anchorMax; pivot = rect.pivot; position = rect.anchoredPosition; size = rect.sizeDelta;
                tint = graphic.color; enabled = graphic.enabled; alpha = graphic.canvasRenderer.GetAlpha(); cullTransparent = graphic.canvasRenderer.cullTransparentMesh;
                if (graphic is Text label)
                {
                    font = label.font; fontSize = label.fontSize; minSize = label.resizeTextMinSize; maxSize = label.resizeTextMaxSize;
                    bestFit = label.resizeTextForBestFit; alignment = label.alignment; horizontalOverflow = label.horizontalOverflow;
                    verticalOverflow = label.verticalOverflow; richText = label.supportRichText;
                }
            }
            internal void Restore(Graphic graphic)
            {
                var rect = graphic.rectTransform;
                rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
                graphic.color = tint; graphic.enabled = enabled; graphic.canvasRenderer.cullTransparentMesh = cullTransparent; graphic.canvasRenderer.SetAlpha(alpha);
                if (graphic is Text label)
                {
                    label.font = font; label.fontSize = fontSize; label.resizeTextMinSize = minSize; label.resizeTextMaxSize = maxSize;
                    label.resizeTextForBestFit = bestFit; label.alignment = alignment; label.horizontalOverflow = horizontalOverflow;
                    label.verticalOverflow = verticalOverflow; label.supportRichText = richText;
                }
            }
        }
    }
}
