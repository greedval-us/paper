using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>A pooled continuation of one trial lane's triangle stream, in sibling draw order.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class TrialLaneBatchGraphic : MaskableGraphic
    {
        public override Texture mainTexture => NotebookSpriteCatalog.Texture;
        TrialLaneSketch owner;
        int batchIndex;

        internal void Initialize(TrialLaneSketch lane, int index)
        {
            owner = lane; batchIndex = index;
            raycastTarget = false;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear();
            if (owner != null) owner.PopulateBatch(target, rectTransform.rect, batchIndex);
        }
    }
}
