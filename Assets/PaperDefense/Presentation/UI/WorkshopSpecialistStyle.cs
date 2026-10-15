using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>The specialist editors keep their controls and simulations, using the workshop palette.</summary>
    internal static class WorkshopSpecialistStyle
    {
        public static void Apply(Transform sheet, WorkshopUi ui)
        {
            if (sheet == null) return;
            if (sheet.TryGetComponent<Image>(out var paper)) paper.color = WorkshopUi.Paper;
            var labels = sheet.GetComponentsInChildren<Text>(true);
            foreach (var label in labels) { label.font = ui.BodyFont; label.color = WorkshopUi.Ink; }
            if (labels.Length > 0) { labels[0].font = ui.HeadingFont; labels[0].fontSize = labels[0].rectTransform.rect.height < 44 ? 30 : 32; }
            foreach (var button in sheet.GetComponentsInChildren<Button>(true))
            {
                if (button.TryGetComponent<Image>(out var image)) image.color = WorkshopUi.Surface;
                var colors = button.colors;
                colors.highlightedColor = WorkshopUi.Selected;
                colors.selectedColor = WorkshopUi.Selected;
                colors.pressedColor = new Color(.72f, .81f, .98f);
                button.colors = colors;
                var caption = button.GetComponentInChildren<Text>();
                if (caption != null) { ui.Tip(button.gameObject, caption.text); button.GetComponent<WorkshopTooltip>().FollowCaption(caption); }
            }
        }
    }
}
