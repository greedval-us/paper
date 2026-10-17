using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Screen 05 header and footer styling, restored before another wizard page is laid out.</summary>
    internal sealed class WizardWeaponHeader
    {
        readonly RectTransform board;
        readonly Func<string, RectTransform> find;
        readonly Dictionary<Graphic, GraphicState> originals = new Dictionary<Graphic, GraphicState>();
        readonly List<GameObject> decorations = new List<GameObject>();
        RectTransform layer;
        bool applied, startWasActive;

        internal WizardWeaponHeader(RectTransform board, Func<string, RectTransform> find)
        { this.board = board; this.find = find; }

        internal void Apply(float width, float height, float offset, bool compact)
        {
            if (!applied) Remember();
            if (layer == null)
            {
                layer = new GameObject("Weapons header decoration", typeof(RectTransform)).GetComponent<RectTransform>();
                layer.SetParent(board, false);
            }
            layer.gameObject.SetActive(true); layer.SetSiblingIndex(2);
            WorkshopUi.Place(layer, new Rect(0, 0, board.sizeDelta.x, height));
            find("Wizard title")?.gameObject.SetActive(false);
            find("Start run")?.gameObject.SetActive(false);
            var hand = Resources.Load<Font>("NotebookHand");

            var header = Detail(layer, "Weapons top frame", FoundationDetailGraphic.Kind.Frame, new Rect(offset, 1, width, 58));
            header.Fill = WorkshopUi.Paper; header.Radius = 8; header.color = WorkshopUi.Ink;
            Icon(layer, "Weapons brand castle", WorkshopIcon.Tower, new Rect(offset + 18, 7, 48, 46));
            Label(layer, "Weapons brand lettering", "ДЕРЖИ ЛИНИЮ", new Rect(offset + 77, 5, compact ? 212 : 255, 49), hand, compact ? 29 : 32);
            float titleWidth = compact ? 234 : 290;
            float titleX = offset + (width - titleWidth) * .5f - (compact ? 30 : 0);
            Illustration(layer, "Weapons workshop tools", "icon.workshop", new Rect(titleX, 6, 44, 46));
            Label(layer, "Weapons workshop lettering", "Мастерская", new Rect(titleX + 54, 4, titleWidth - 54, 51), hand, compact ? 32 : 37);

            float albumWidth = compact ? 164 : 192, goldWidth = compact ? 174 : 205;
            float albumX = offset + width - albumWidth - 8, goldX = albumX - goldWidth - 18;
            Place("Wizard album", new Rect(albumX, 3, albumWidth, 52));
            StyleButton("Wizard album", hand, compact ? 26 : 29, false);
            Detail(layer, "Weapons gold coin", FoundationDetailGraphic.Kind.GoldCoin, new Rect(goldX, 10, 38, 38));
            var gold = find("Wizard gold")?.GetComponent<Text>();
            if (gold != null)
            {
                WorkshopUi.Place(gold.rectTransform, new Rect(goldX + 43, 8, goldWidth - 43, 44));
                Lettering(gold, hand, compact ? 25 : 28, TextAnchor.MiddleLeft);
            }

            float stepRowWidth = Mathf.Min(compact ? width - 108 : 940, width - 48);
            float stepGap = compact ? 27 : 48, stepWidth = (stepRowWidth - stepGap * 3) / 4;
            float stepX = offset + (width - stepRowWidth) * .5f, stepY = compact ? 74 : 72;
            float stepHeight = compact ? 47 : 50, diameter = compact ? 34 : 38;
            for (int i = 0; i < 4; i++)
            {
                var bounds = new Rect(stepX + i * (stepWidth + stepGap), stepY, stepWidth, stepHeight);
                Place("Wizard step " + i, bounds);
                var button = find("Wizard step " + i);
                if (button == null) continue;
                HideSurface(button);
                var symbol = button.Find("Icon")?.GetComponent<WorkshopIconGraphic>();
                if (symbol != null) symbol.enabled = false;
                var caption = button.Find("Caption")?.GetComponent<Text>();
                if (caption != null) caption.enabled = false;
                var tab = Detail(layer, "Weapons step tab " + i, FoundationDetailGraphic.Kind.StepTab, bounds);
                tab.Count = i; tab.Total = 4; tab.Selected = i == 1; tab.color = WorkshopUi.Ink;
                button.GetComponent<WorkshopButton>()?.SetDecoration(tab);
                Detail(layer, "Weapons step circle " + i, FoundationDetailGraphic.Kind.ProgressCircle,
                    new Rect(bounds.x + 14, bounds.y + (stepHeight - diameter) * .5f, diameter, diameter)).color = WorkshopUi.Ink;
                var number = find("Wizard progress " + i)?.GetComponent<Text>();
                if (number != null)
                {
                    number.gameObject.SetActive(true); number.enabled = true;
                    WorkshopUi.Place(number.rectTransform, new Rect(bounds.x + 14, bounds.y + (stepHeight - diameter) * .5f, diameter, diameter));
                    Lettering(number, hand, compact ? 27 : 30, TextAnchor.MiddleCenter);
                }
                var check = find("Wizard completed " + i)?.GetComponent<WorkshopIconGraphic>();
                if (check != null) check.enabled = false;
                Label(layer, "Weapons step lettering " + i, caption == null ? "" : caption.text,
                    new Rect(bounds.x + 54, bounds.y + 3, stepWidth - (i == 3 ? 64 : 74), stepHeight - 6),
                    hand, compact ? 24 : 27, i == 1 ? WorkshopUi.Paper : WorkshopUi.Ink, TextAnchor.MiddleCenter);
                if (i < 3)
                {
                    var arrow = Icon(layer, "Weapons step arrow " + i, WorkshopIcon.Chevron,
                        new Rect(bounds.xMax + (stepGap - 22) * .5f, stepY + (stepHeight - 27) * .5f, 22, 27));
                    arrow.rectTransform.pivot = new Vector2(.5f, .5f);
                    arrow.rectTransform.anchoredPosition += new Vector2(11, -13.5f);
                    arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
                }
            }

            Place("Wizard back", new Rect(offset + 8, height - 74, 260, 64));
            StyleButton("Wizard back", hand, compact ? 30 : 34, false);
            Place("Wizard next", new Rect(offset + width - 308, height - 74, 300, 64));
            StyleButton("Wizard next", hand, compact ? 29 : 32, true);
            var next = find("Wizard next");
            if (next != null)
            {
                var nextIcon = next.Find("Icon") as RectTransform;
                if (nextIcon != null) WorkshopUi.Place(nextIcon, new Rect(248, 14, 36, 36));
                var nextCaption = next.Find("Caption") as RectTransform;
                if (nextCaption != null) WorkshopUi.Place(nextCaption, new Rect(61, 4, 178, 56));
                Icon(next, "Weapons next castle", WorkshopIcon.Tower, new Rect(13, 10, 44, 44), WorkshopUi.Paper);
            }
            float noticeX = offset + 286, noticeWidth = width - 612;
            var notice = find("Wizard notice")?.GetComponent<Text>();
            bool hasNotice = notice != null && !string.IsNullOrWhiteSpace(notice.text);
            var backing = Detail(notice == null ? layer : notice.transform.parent, "Weapons notice backing", FoundationDetailGraphic.Kind.Frame,
                new Rect(noticeX, height - 70, noticeWidth, 56));
            backing.Fill = WorkshopUi.Paper; backing.color = WorkshopUi.Ink; backing.gameObject.SetActive(hasNotice);
            if (notice != null)
            {
                backing.transform.SetSiblingIndex(Mathf.Max(0, notice.transform.GetSiblingIndex() - 1));
                WorkshopUi.Place(notice.rectTransform, new Rect(noticeX + 10, height - 68, noticeWidth - 20, 52));
                Lettering(notice, hand, 22, TextAnchor.MiddleCenter, WorkshopUi.Warning);
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
            applied = true;
        }

        internal void Restore(int step)
        {
            if (!applied) return;
            foreach (var item in originals) if (item.Key != null) item.Value.Restore(item.Key);
            if (layer != null) layer.gameObject.SetActive(false);
            foreach (var item in decorations) if (item != null) item.SetActive(false);
            find("Wizard title")?.gameObject.SetActive(true);
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

        void StyleButton(string name, Font hand, int fontSize, bool primary)
        {
            var button = find(name); if (button == null) return;
            HideSurface(button);
            var frame = Detail(button, "Weapons button frame", FoundationDetailGraphic.Kind.Frame, new Rect(0, 0, button.sizeDelta.x, button.sizeDelta.y));
            frame.Fill = primary ? WorkshopUi.Blue : WorkshopUi.Paper; frame.color = primary ? WorkshopUi.Blue : WorkshopUi.Ink;
            button.GetComponent<WorkshopButton>()?.SetDecoration(frame);
            var icon = button.Find("Icon")?.GetComponent<WorkshopIconGraphic>();
            if (icon != null)
            {
                icon.enabled = true; icon.color = primary ? WorkshopUi.Paper : WorkshopUi.Ink;
                WorkshopUi.Place(icon.rectTransform, new Rect(18, (button.sizeDelta.y - 36) * .5f, 36, 36));
            }
            var caption = button.Find("Caption")?.GetComponent<Text>();
            if (caption != null)
            {
                Lettering(caption, hand, fontSize, TextAnchor.MiddleCenter, primary ? WorkshopUi.Paper : WorkshopUi.Ink);
                WorkshopUi.Place(caption.rectTransform, new Rect(60, 3, button.sizeDelta.x - 72, button.sizeDelta.y - 6));
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

        FoundationDetailGraphic Detail(Transform parent, string name, FoundationDetailGraphic.Kind kind, Rect bounds)
        {
            var graphic = Own<FoundationDetailGraphic>(parent, name, bounds); graphic.Shape = kind;
            if (parent != layer) graphic.transform.SetAsFirstSibling();
            return graphic;
        }

        WorkshopIconGraphic Icon(Transform parent, string name, WorkshopIcon icon, Rect bounds, Color? tint = null)
        { var graphic = Own<WorkshopIconGraphic>(parent, name, bounds); graphic.Icon = icon; graphic.color = tint ?? WorkshopUi.Ink; return graphic; }

        void Illustration(Transform parent, string name, string id, Rect bounds)
        {
            var image = Own<Image>(parent, name, bounds);
            if (NotebookSpriteCatalog.TryGet(id, out var sprite)) image.sprite = sprite;
            image.preserveAspect = true;
            image.rectTransform.pivot = new Vector2(.5f, .5f);
            image.rectTransform.anchoredPosition += new Vector2(bounds.width * .5f, -bounds.height * .5f);
        }

        void Label(Transform parent, string name, string value, Rect bounds, Font hand, int size,
            Color? tint = null, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = Own<Text>(parent, name, bounds); label.text = value;
            Lettering(label, hand, size, alignment, tint);
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
