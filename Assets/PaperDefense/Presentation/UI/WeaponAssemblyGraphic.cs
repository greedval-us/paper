using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Fits the same assembly used by album, trial and battle into the weapon screen.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WeaponAssemblyGraphic : MaskableGraphic
    {
        public override Texture mainTexture => NotebookSpriteCatalog.Texture;
        public TowerBlueprint Blueprint { get; private set; }
        Rect bounds;
        bool hasBounds;

        public void SetBlueprint(TowerBlueprint value)
        {
            Blueprint = value;
            hasBounds = NotebookTowerComposition.TryGetBounds(value, out bounds);
            SetVerticesDirty(); SetMaterialDirty();
        }

        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear();
            var available = rectTransform.rect;
            if (Blueprint == null || !hasBounds || available.width <= 0 || available.height <= 0) return;
            float scale = .9f * Mathf.Min(available.width / Mathf.Max(.001f, bounds.width),
                available.height / Mathf.Max(.001f, bounds.height));
            NotebookTowerComposition.DrawUI(target, Blueprint, available.center - bounds.center * scale, scale);
        }
    }
}
