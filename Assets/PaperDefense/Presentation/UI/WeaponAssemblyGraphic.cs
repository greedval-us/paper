using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Fits the current resource or player-drawn assembly without clipping any selected part.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WeaponAssemblyGraphic : MaskableGraphic
    {
        public override Texture mainTexture => NotebookSpriteCatalog.Texture;
        public TowerBlueprint Blueprint { get; private set; }

        TowerBlueprint rendered;
        Rect bounds;
        bool hasBounds;
        bool resourceAssembly;
        readonly List<SpritePlacement> sprites = new List<SpritePlacement>();

        readonly struct SpritePlacement
        {
            public readonly string Id;
            public readonly Vector2 Offset;
            public readonly float Scale;
            public SpritePlacement(string id, Vector2 offset, float scale)
            { Id = id; Offset = offset; Scale = scale; }
        }

        public void SetBlueprint(TowerBlueprint value)
        {
            Blueprint = value;
            rendered = value;
            resourceAssembly = value != null && (!value.HasPlayerDrawing || value.Drawing.IsEmpty);
            Measure();
            SetVerticesDirty();
            SetMaterialDirty();
        }

        void Measure()
        {
            hasBounds = false;
            sprites.Clear();
            if (rendered == null) return;
            Vector2 low = Vector2.zero, high = Vector2.zero;
            if (resourceAssembly)
            {
                BuildResourceAssembly(ref low, ref high);
                if (hasBounds) bounds = Rect.MinMaxRect(low.x, low.y, high.x, high.y);
                return;
            }
            var authored = AuthoredGeometryCache.For(rendered.Drawing);
            foreach (var part in DrawingTemplateCatalog.Create(rendered))
            {
                var pivot = new Vector2(part.Pivot.X, part.Pivot.Y);
                var geometry = authored.GetPart(part.BindingId);
                if (geometry != null)
                    foreach (var vertex in geometry.Vertices) Include(vertex + pivot, ref low, ref high);
            }
            if (hasBounds) bounds = Rect.MinMaxRect(low.x, low.y, high.x, high.y);
        }

        void BuildResourceAssembly(ref Vector2 low, ref Vector2 high)
        {
            if (!NotebookSpriteCatalog.TryGetBounds(rendered.ChassisId, out var chassis)) return;
            AddSprite(rendered.ChassisId, Vector2.zero, 1, ref low, ref high);
            foreach (var part in DrawingTemplateCatalog.Create(rendered))
                if (part.Role == DrawingPartRole.Module)
                    foreach (var module in rendered.ModuleSelections)
                        if (DrawingBindings.Module(module.InstanceId) == part.BindingId)
                        {
                            AddSprite(module.DefinitionId, new Vector2(part.Pivot.X, part.Pivot.Y), 1, ref low, ref high);
                            break;
                        }

            // Coordinates refer to the tightly cropped ModularSprites.png source rectangles, with Y up.
            // Castle roofs are near 78% of the crop height; the precision tripod's top ring is higher.
            Vector2 roof = rendered.ChassisId == "chassis.precision" ? new Vector2(.50f, .85f)
                : rendered.ChassisId == "chassis.heavy" ? new Vector2(.50f, .79f) : new Vector2(.50f, .78f);
            int count = rendered.WeaponMounts.Count;
            float footprint = chassis.width * (count == 1 ? .44f : count == 2 ? .29f : .23f);
            // The middle weapon sits at the back of a three-mount roof, then the two nearer weapons cover it.
            if (count == 3) AddWeapon(1, new Vector2(roof.x, roof.y + .055f), chassis, footprint, ref low, ref high);
            for (int i = 0; i < count; i++)
            {
                if (count == 3 && i == 1) continue;
                float x = count == 1 ? roof.x : count == 2 ? (i == 0 ? .34f : .66f) : (i == 0 ? .30f : .70f);
                AddWeapon(i, new Vector2(x, roof.y - (count == 3 ? .025f : 0)), chassis, footprint, ref low, ref high);
            }
        }

        void AddWeapon(int index, Vector2 socket, Rect chassis, float footprint, ref Vector2 low, ref Vector2 high)
        {
            string id = rendered.WeaponMounts[index].WeaponId;
            if (!NotebookSpriteCatalog.TryGetBounds(id, out var source)) return;
            Pedestal(id, out Vector2 anchor, out float diameter);
            // Normalize the physical pedestal, not the old gameplay bounds or the barrel's full width.
            float scale = footprint / (source.width * diameter);
            Vector2 at = chassis.min + Vector2.Scale(chassis.size, socket);
            Vector2 localAnchor = source.min + Vector2.Scale(source.size, anchor);
            AddSprite(id, at - localAnchor * scale, scale, ref low, ref high);
        }

        static void Pedestal(string id, out Vector2 anchor, out float diameter)
        {
            // (X,Y) is the base-disc centre inside each source crop; diameter is its fraction of crop width.
            // Sideways barrels leave transparent space to the right, so their support lies left of centre.
            switch (id)
            {
                case "weapon.gun": anchor = new Vector2(.53f, .08f); diameter = .78f; break;
                case "weapon.piercer": anchor = new Vector2(.39f, .08f); diameter = .74f; break;
                case "weapon.mortar": anchor = new Vector2(.44f, .08f); diameter = .84f; break;
                case "weapon.fan": anchor = new Vector2(.36f, .08f); diameter = .68f; break;
                case "weapon.ricochet": anchor = new Vector2(.31f, .08f); diameter = .59f; break;
                case "weapon.stapler": anchor = new Vector2(.41f, .08f); diameter = .67f; break;
                case "weapon.blotter": anchor = new Vector2(.36f, .08f); diameter = .69f; break;
                default: anchor = new Vector2(.50f, .08f); diameter = .75f; break;
            }
        }

        void AddSprite(string id, Vector2 offset, float scale, ref Vector2 low, ref Vector2 high)
        {
            if (!NotebookSpriteCatalog.TryGetBounds(id, out var source)) return;
            sprites.Add(new SpritePlacement(id, offset, scale));
            Include(source.min * scale + offset, ref low, ref high);
            Include(source.max * scale + offset, ref low, ref high);
        }

        void Include(Vector2 point, ref Vector2 low, ref Vector2 high)
        {
            if (!hasBounds) { low = high = point; hasBounds = true; }
            else { low = Vector2.Min(low, point); high = Vector2.Max(high, point); }
        }

        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear();
            if (rendered == null || !hasBounds) return;
            var available = rectTransform.rect;
            if (available.width <= 0 || available.height <= 0) return;
            // Uniform fitting preserves proportions; five percent on each side keeps the ink off the frame.
            float scale = .9f * Mathf.Min(available.width / Mathf.Max(.001f, bounds.width),
                available.height / Mathf.Max(.001f, bounds.height));
            Vector2 center = available.center - bounds.center * scale;
            if (resourceAssembly)
                foreach (var sprite in sprites)
                    NotebookSpriteCatalog.TryDrawUI(target, sprite.Id, center + sprite.Offset * scale, sprite.Scale * scale);
            else NotebookTowerComposition.DrawUI(target, rendered, center, scale);
        }
    }
}
