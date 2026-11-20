using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Non-interactive surface inside themed pause and leave-dialog controls.</summary>
    public static class NotebookPaperArt
    {
        public static bool Apply(Transform parent, string name)
        {
            if (parent == null) return false;
            var child = parent.Find(name);
            var image = child != null ? child.GetComponent<Image>() : null;
            if (image == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);
                image = go.GetComponent<Image>();
            }
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.sprite = null; image.color = NotebookThemeDefaults.Surface; image.raycastTarget = false;
            rect.SetAsFirstSibling();
            return true;
        }
    }
}
