using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Screen-owned paper artwork behind live controls, with procedural fallbacks at each call site.</summary>
    public static class NotebookPaperArt
    {
        static Sprite paper;
        static Texture2D paperTexture;

        public static bool Apply(Transform parent, string name)
        {
            if (parent == null) return false;
            var sprite = PaperSprite();
            if (sprite == null) return false;
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
            image.sprite = sprite; image.type = Image.Type.Sliced; image.fillCenter = true;
            image.pixelsPerUnitMultiplier = 4;
            image.color = Color.white; image.raycastTarget = false;
            rect.SetAsFirstSibling();
            return true;
        }

        static Sprite PaperSprite()
        {
            var source = Resources.Load<Sprite>("Art/Player/NotebookPaperCardV1");
            var texture = source != null ? source.texture : null;
            // This region belongs to the inspected native 1086 x 1448 card. An unexpected
            // replacement keeps the local procedural paper instead of silently cropping it.
            if (texture == null || texture.width != 1086 || texture.height != 1448) return null;
            if (paper != null && paperTexture == texture) return paper;
            ResetCache();
            paperTexture = texture;
            paper = Sprite.Create(texture, new Rect(24, 48, 1038, 1328), new Vector2(.5f, .5f),
                100, 0, SpriteMeshType.FullRect, new Vector4(96, 96, 96, 96));
            paper.name = "Notebook paper card display region";
            paper.hideFlags = HideFlags.DontSave;
            return paper;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlaySession() => ResetCache();

        public static void ResetCache()
        {
            if (paper != null)
            {
                if (UnityEngine.Application.isPlaying) Object.Destroy(paper);
                else Object.DestroyImmediate(paper);
            }
            paper = null; paperTexture = null;
        }
    }
}
