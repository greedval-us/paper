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
        public RecipeSnapshot Recipe { get; private set; }
        public AssemblyVisualPose Pose { get; private set; }
        Rect bounds;
        bool hasBounds;

        public void SetBlueprint(TowerBlueprint value)
        {
            Blueprint = value;
            Recipe = null;
            Pose = null;
            hasBounds = NotebookTowerComposition.TryGetBounds(value, out bounds);
            SetVerticesDirty(); SetMaterialDirty();
        }

        public void SetRecipe(RecipeSnapshot value, AssemblyVisualPose pose = null)
        {
            var nextPose = pose ?? new AssemblyVisualPose();
            Rect nextBounds = value == null ? default : AssemblyVisualComposer.Compose(value, nextPose).Bounds;
            Blueprint = null;
            Recipe = value;
            Pose = nextPose;
            bounds = nextBounds;
            hasBounds = value != null;
            SetVerticesDirty(); SetMaterialDirty();
        }

        public void SetPose(AssemblyVisualPose value)
        {
            var nextPose = value ?? new AssemblyVisualPose();
            if (Recipe != null) bounds = AssemblyVisualComposer.Compose(Recipe, nextPose).Bounds;
            Pose = nextPose;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear();
            var available = rectTransform.rect;
            if ((Blueprint == null && Recipe == null) || !hasBounds || available.width <= 0 || available.height <= 0) return;
            float scale = .9f * Mathf.Min(available.width / Mathf.Max(.001f, bounds.width),
                available.height / Mathf.Max(.001f, bounds.height));
            Vector2 center = available.center - bounds.center * scale;
            if (Recipe != null) NotebookTowerComposition.DrawRecipeUI(target, Recipe, Pose, center, scale);
            else NotebookTowerComposition.DrawUI(target, Blueprint, center, scale);
        }
    }
}
