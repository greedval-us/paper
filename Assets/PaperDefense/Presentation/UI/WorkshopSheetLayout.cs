using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Responsive specialist sheets. Geometry and pointer coordinates remain local to each canvas.</summary>
    public sealed class WorkshopSheetLayout : MonoBehaviour
    {
        readonly Dictionary<RectTransform, Rect> originals = new Dictionary<RectTransform, Rect>();
        RectTransform sheet, title, close, viewport, content;
        bool study;
        float split, rightOrigin, lastWidth, lastHeight;
        public void Initialize()
        {
            sheet = (RectTransform)transform; study = name == "Footprint study sheet";
            split = name == "Reinforcement sheet" ? 780 : name == "Fan volley sheet" ? 920 : 850;
            rightOrigin = name == "Reinforcement sheet" ? 810 : name == "Fan volley sheet" ? 940 : 880;
            var children = sheet.Cast<Transform>().OfType<RectTransform>().ToArray();
            title = children.FirstOrDefault(x => x.GetComponent<Text>() != null);
            close = children.FirstOrDefault(x => x.name.StartsWith("Close "));
            var ui = new PlayerUi(); content = ui.Scroll(sheet, "Specialist page scroll", new Rect(16, 84, 1528, 780), 850);
            viewport = (RectTransform)content.parent;
            PlayerUi.HorizontalScrollbar(viewport.GetComponent<ScrollRect>());
            foreach (var child in children)
            {
                if (child == title || child == close) continue;
                originals[child] = new Rect(child.anchoredPosition.x, -child.anchoredPosition.y, child.sizeDelta.x, child.sizeDelta.y);
                child.SetParent(content, false);
            }
        }
        public void Reflow(float width, float height)
        {
            if (sheet == null) Initialize();
            if (Mathf.Approximately(lastWidth, width) && Mathf.Approximately(lastHeight, height)) return;
            lastWidth = width; lastHeight = height;
            float w = Mathf.Min(1560, width - 32), h = height - 32;
            WorkshopUi.Place(sheet, new Rect((width - w) / 2, 16, w, h)); sheet.localScale = Vector3.one;
            WorkshopUi.Place(title, new Rect(24, 14, w - 48, 54));
            WorkshopUi.Place(close, new Rect(w - 284, h - 66, 260, 48));
            WorkshopUi.Place(viewport, new Rect(16, 84, w - 32, h - 164));
            // Detailed geometry editors retain a readable minimum working width.
            // Narrow windows expose the remaining width through the visible horizontal scrollbar.
            float bodyW = Mathf.Max(900, w - 32), extent = 0;
            bool stacked = bodyW < 1440;
            foreach (var pair in originals)
            {
                if (pair.Key == null) continue;
                Rect b = pair.Value;
                if (study) b = StudyRect(b, bodyW);
                else if (!stacked)
                {
                    float scale = bodyW / 1560;
                    b = new Rect(b.x * scale, b.y - 70, b.width * scale, b.height);
                }
                else if (b.width > 1100) b = new Rect(16, b.y - 70, bodyW - 32, Mathf.Max(b.height, 80));
                else if (b.x >= split) b = new Rect(16 + b.x - rightOrigin, b.y - 70 + 840, b.width, b.height);
                else b = new Rect(16 + b.x - 30, b.y - 70, b.width, b.height);
                // The study link lives in the fixed header in the original sheet.
                if (!study && pair.Key.name == "Footprint study") continue;
                if (b.xMax > bodyW - 16) b.width = Mathf.Max(80, bodyW - 16 - b.x);
                WorkshopUi.Place(pair.Key, b); extent = Mathf.Max(extent, b.yMax + 20);
            }
            var studyLink = originals.Keys.FirstOrDefault(x => x != null && x.name == "Footprint study");
            if (studyLink != null) { WorkshopUi.Place(studyLink, new Rect(16, extent + 12, 310, 48)); extent += 80; }
            content.sizeDelta = new Vector2(bodyW, Mathf.Max(viewport.rect.height, extent));
        }
        static Rect StudyRect(Rect b, float w)
        {
            int columns = w >= 1440 ? 3 : w >= 1000 ? 2 : 1;
            float cell = (w - 32 - 20 * (columns - 1)) / columns;
            if (b.y >= 250 && b.y <= 735)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt((b.x - 30) / 510), 0, 2);
                return new Rect(16 + i % columns * (cell + 20), 190 + i / columns * 510 + b.y - 250, cell, b.height);
            }
            if (b.y == 145)
            { int i = Mathf.RoundToInt((b.x - 30) / 510); return new Rect(16 + i * (w - 32) / 3, 86, (w - 56) / 3, 48); }
            if (b.y == 205) return new Rect(16, 142, w - 32, 44);
            float rowsEnd = 190 + Mathf.Ceil(3f / columns) * 510;
            if (b.y >= 748)
            {
                if (b.width > 1000) return new Rect(16, rowsEnd, w - 32, 130);
                if (b.x >= 850) return new Rect(16, rowsEnd + 210, w - 32, 70);
                return new Rect(16 + b.x - 30, rowsEnd + 150, b.width, b.height);
            }
            return new Rect(16, b.y - 70, w - 32, Mathf.Max(65, b.height));
        }
    }
}
