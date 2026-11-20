using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    public sealed partial class PlayerBattleHud
    {
        string fittedToolHint;
        Vector2 fittedToolHintSize;

        Text CreateToolHint(RectTransform parent, PlayerLayout layout)
        {
            // Keep the overview button's side clear and protect the caption from any map detail.
            float width = Mathf.Min(720, Mathf.Max(160, layout.Width - 360));
            var plate = ui.Panel(parent, "Battle tool hint plate",
                new Rect((layout.Width - width) * .5f, layout.Dock.y - 30, width, 28), FieldMenuSkin.Panel);
            plate.GetComponent<Graphic>().raycastTarget = false;
            var text = ui.Label(plate, "", new Rect(10, 4, width - 20, 20), 15,
                FieldMenuSkin.Ink, alignment: TextAnchor.MiddleCenter);
            text.name = "Battle tool hint";
            text.raycastTarget = false;
            return text;
        }

        void WriteToolHint(Text label, string full)
        {
            full = full ?? "";
            var size = label.rectTransform.rect.size;
            if (fittedToolHint == full && fittedToolHintSize == size) return;
            fittedToolHint = full; fittedToolHintSize = size;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.text = full;
            int length = full.Length;
            while (length > 1 && label.preferredHeight > size.y + .1f)
                label.text = full.Substring(0, --length).TrimEnd() + "…";
        }
    }
}
