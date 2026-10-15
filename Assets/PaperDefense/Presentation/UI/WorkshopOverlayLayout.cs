using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Readable album and trial sheets with a fixed close control and a scrollable body.</summary>
    public sealed class WorkshopOverlayLayout : MonoBehaviour
    {
        RectTransform sheet, content, viewport;
        readonly Dictionary<string, RectTransform> items = new Dictionary<string, RectTransform>();
        float lastWidth, lastHeight;
        bool album;
        RectTransform Find(string key) => items.TryGetValue(key, out var value) ? value : null;
        void Place(string key, Rect bounds) { var item = Find(key); if (item != null) WorkshopUi.Place(item, bounds); }
        void Initialize()
        {
            sheet = (RectTransform)transform; album = name == "Wizard album sheet";
            var children = sheet.Cast<Transform>().OfType<RectTransform>().ToArray();
            content = new PlayerUi().Scroll(sheet, "Overlay page scroll", new Rect(20, 92, 1000, 600), 1000);
            viewport = (RectTransform)content.parent;
            foreach (var child in children)
            {
                items[child.name] = child;
                if (child.name == "Album title" || child.name == "Wizard trial heading" || child.name == "Wizard close album" || child.name == "Wizard close trial") continue;
                child.SetParent(content, false);
            }
        }
        public void Reflow(float width, float height)
        {
            if (sheet == null) Initialize();
            if (lastWidth == width && lastHeight == height) return;
            lastWidth = width; lastHeight = height;
            float w = Mathf.Min(album ? 1360 : 1440, width - 32), h = height - 32;
            WorkshopUi.Place(sheet, new Rect((width - w) / 2, 16, w, h)); sheet.localScale = Vector3.one;
            Place(album ? "Album title" : "Wizard trial heading", new Rect(24, 16, w - 124, 56));
            Place(album ? "Wizard close album" : "Wizard close trial", new Rect(w - 80, 20, 56, 48));
            WorkshopUi.Place(viewport, new Rect(20, 92, w - 40, h - 116));
            float bodyW = w - 56;
            float extent = album ? Album(bodyW) : Trial(bodyW);
            content.sizeDelta = new Vector2(w - 40, Mathf.Max(viewport.rect.height, extent));
        }
        float Album(float w)
        {
            float newW = Mathf.Min(300, w * .43f);
            Place("Album search", new Rect(0, 0, w - newW - 16, 52));
            var search = Find("Album search");
            foreach (var text in search.GetComponentsInChildren<Text>()) WorkshopUi.Place(text.rectTransform, new Rect(12, 4, search.rect.width - 24, 44));
            Place("Wizard new blueprint", new Rect(w - newW, 0, newW, 52));
            int columns = w >= 1100 ? 2 : 1;
            float cardW = (w - (columns - 1) * 20) / columns;
            int index = 0;
            foreach (var card in items.Values.Where(x => x.name.StartsWith("Wizard album card ")))
            {
                WorkshopUi.Place(card, new Rect(index % columns * (cardW + 20), 80 + index / columns * 214, cardW, 194)); index++;
                var sketch = card.GetComponentInChildren<BlueprintSketch>();
                WorkshopUi.Place(sketch.rectTransform, new Rect(12, 12, 132, 160));
                foreach (Transform child in card)
                {
                    var r = (RectTransform)child;
                    if (r.name == "Album page name") WorkshopUi.Place(r, new Rect(160, 12, cardW - 176, 54));
                    else if (r.name == "Album page price") WorkshopUi.Place(r, new Rect(160, 70, cardW - 176, 34));
                    else if (r.name.StartsWith("Wizard album edit ")) WorkshopUi.Place(r, new Rect(160, 124, (cardW - 248) / 2, 48));
                    else if (r.name.StartsWith("Wizard album select ")) WorkshopUi.Place(r, new Rect(172 + (cardW - 248) / 2, 124, (cardW - 248) / 2, 48));
                    else if (r.name.StartsWith("Wizard album delete ")) WorkshopUi.Place(r, new Rect(cardW - 64, 124, 48, 48));
                }
            }
            float y = 80 + Mathf.Max(1, Mathf.Ceil(index / (float)columns)) * 214;
            Place("Album empty", new Rect(20, 100, w - 40, 140));
            Place("Wizard album previous", new Rect(0, y, 170, 48));
            Place("Wizard album page", new Rect(184, y, w - 390, 48));
            Place("Wizard album next", new Rect(w - 190, y, 190, 48));
            Place("Album slots heading", new Rect(0, y + 80, w, 40));
            int slots = w < 1000 ? 2 : 4;
            float slotW = (w - (slots - 1) * 16) / slots;
            for (int i = 0; i < 4; i++) Place("Wizard assign " + i, new Rect(i % slots * (slotW + 16), y + 136 + i / slots * 84, slotW, 72));
            y += 136 + 4 / slots * 84;
            Place("Wizard album status", new Rect(0, y + 12, w, 100));
            Place("Wizard retry profile", new Rect(0, y + 124, w, 48));
            return y + (Find("Wizard retry profile") != null ? 190 : 120);
        }
        float Trial(float w)
        {
            Place("Wizard trial hint", new Rect(0, 0, w, 70));
            bool compact = w < 1000;
            string[] options = { "Wizard trial scenario", "Wizard trial budget", "Wizard trial counterfire" };
            float optionW = compact ? w : (w - 32) / 3;
            for (int i = 0; i < 3; i++) Place(options[i], new Rect(compact ? 0 : i * (optionW + 16), 86 + (compact ? i * 64 : 0), optionW, 50));
            float y = compact ? 288 : 160;
            Place("Wizard trial conditions", new Rect(0, y, w, 64)); y += 80;
            bool stacked = w < 1200;
            float laneW = stacked ? w : (w - 24) / 2;
            string[] names = { "first", "second" };
            for (int i = 0; i < 2; i++)
            {
                float x = stacked ? 0 : i * (laneW + 24), top = y + (stacked ? i * 430 : 0);
                Place("Trial " + names[i] + " heading", new Rect(x, top, laneW, 44));
                Place("Wizard trial " + names[i] + " viewport", new Rect(x, top + 52, laneW, 260));
                var lane = Find("Wizard trial " + names[i] + " viewport").GetComponentInChildren<TrialLaneSketch>();
                WorkshopUi.Place(lane.rectTransform, new Rect(0, 0, laneW, 260));
                Place("Wizard trial " + names[i] + " result", new Rect(x, top + 324, laneW, 90));
            }
            y += stacked ? 860 : 430;
            Place("Wizard trial run", new Rect(w - Mathf.Min(360, w), y, Mathf.Min(360, w), 52));
            Place("Wizard trial details", new Rect(0, y + 72, w, 48));
            Place("Wizard trial expanded", new Rect(0, y + 136, w, 180));
            var detail = Find("Wizard trial expanded").GetComponentInChildren<Text>();
            WorkshopUi.Place(detail.rectTransform, new Rect(16, 12, w - 32, 156));
            return y + 336;
        }
    }
}
