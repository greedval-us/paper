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
        public PartRef? Part { get; private set; }
        readonly MeshPen partPen = new MeshPen(null, 4096);
        AssemblyVisualComposition standalone;
        Rect bounds;
        bool hasBounds;

        public void SetBlueprint(TowerBlueprint value)
        {
            Blueprint = value;
            Recipe = null;
            Part = null;
            standalone = null;
            Pose = null;
            hasBounds = NotebookTowerComposition.TryGetBounds(value, out bounds);
            SetVerticesDirty(); SetMaterialDirty();
        }

        public void SetRecipe(RecipeSnapshot value, AssemblyVisualPose pose = null)
        {
            var nextPose = pose ?? new AssemblyVisualPose();
            Rect nextBounds = value == null ? default : AssemblyVisualComposer.Compose(value, nextPose).Bounds;
            Blueprint = null;
            Part = null;
            standalone = null;
            Recipe = value;
            Pose = nextPose;
            bounds = nextBounds;
            hasBounds = value != null;
            SetVerticesDirty(); SetMaterialDirty();
        }

        /// <summary>Uses the authored fragments and scale of an individual catalogue part.</summary>
        public void SetPart(PartRef value)
        {
            var composition = AssemblyVisualComposer.ComposePart(value);
            Blueprint = null; Recipe = null; Pose = null; Part = value;
            standalone = null;
            bounds = composition.Bounds; hasBounds = composition.Fragments.Count > 0;
            SetVerticesDirty(); SetMaterialDirty();
        }

        public void SetComposition(AssemblyVisualComposition value)
        {
            Blueprint = null; Recipe = null; Pose = null; Part = null; standalone = value;
            bounds = value?.Bounds ?? default; hasBounds = value != null && value.Fragments.Count > 0;
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
            if ((Blueprint == null && Recipe == null && !Part.HasValue && standalone == null) || !hasBounds || available.width <= 0 || available.height <= 0) return;
            float scale = .9f * Mathf.Min(available.width / Mathf.Max(.001f, bounds.width),
                available.height / Mathf.Max(.001f, bounds.height));
            Vector2 center = available.center - bounds.center * scale;
            if (Part.HasValue || standalone != null)
            {
                partPen.Clear();
                (standalone ?? AssemblyVisualComposer.ComposePart(Part.Value)).Draw(partPen, Vector2.zero);
                partPen.CopyTo(target, center, scale);
            }
            else if (Recipe != null) NotebookTowerComposition.DrawRecipeUI(target, Recipe, Pose, center, scale);
            else NotebookTowerComposition.DrawUI(target, Blueprint, center, scale);
        }
    }
}
