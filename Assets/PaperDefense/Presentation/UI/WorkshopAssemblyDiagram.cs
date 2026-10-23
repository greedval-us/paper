using System;
using System.Collections.Generic;
using System.Linq;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>A read-only exploded illustration of the selected recipe; it never changes physical assembly geometry.</summary>
    public sealed class WorkshopAssemblyDiagram : NotebookAtlasGraphic
    {
        readonly MeshPen arrows = new MeshPen(null, 1024), part = new MeshPen(null, 2048);
        readonly List<AssemblyVisualComposition> drawings = new List<AssemblyVisualComposition>();
        PartRef[] parts = Array.Empty<PartRef>();
        Rect[] slots = Array.Empty<Rect>();
        Rect[] fitted = Array.Empty<Rect>();
        float[] scales = Array.Empty<float>();
        Text[] captions = Array.Empty<Text>();
        bool foundationDiagram;
        WorkshopComponentKind componentKind;
        public IReadOnlyList<PartRef> Parts => parts;
        public float Opacity { get; set; } = 1;

        public void Configure(AssemblyRecipe recipe, WorkshopComponentKind component, int mount)
        {
            componentKind = component;
            foundationDiagram = component == WorkshopComponentKind.Foundation;
            if (component == WorkshopComponentKind.Structure)
            {
                parts = recipe.Structure.Parts.ToArray();
                slots = new[] { new Rect(.20f,.04f,.64f,.21f), new Rect(.12f,.32f,.57f,.38f), new Rect(.43f,.78f,.32f,.20f), new Rect(.77f,.40f,.22f,.26f) };
            }
            else if (component == WorkshopComponentKind.Foundation)
            {
                parts = recipe.Tower.Foundation.Parts.ToArray();
                // Authored frame and hull bands stay intact. A thin base band is an actual
                // part, so pack the stack rather than expanding its source Region or scale.
                slots = new[] { new Rect(.13f,.02f,.72f,.18f), new Rect(.14f,.27f,.72f,.27f), new Rect(.79f,.30f,.20f,.28f), new Rect(.02f,.60f,.27f,.25f), new Rect(.39f,.62f,.33f,.29f) };
            }
            else
            {
                parts = recipe.Tower.Mounts.Single(value => value.SlotIndex == mount).Weapon.Parts.ToArray();
                slots = new[] { new Rect(.30f,.24f,.40f,.35f), new Rect(.35f,.70f,.31f,.25f), new Rect(.77f,.72f,.21f,.20f), new Rect(.01f,.34f,.24f,.29f), new Rect(.77f,.15f,.21f,.29f) };
            }
            drawings.Clear();
            foreach (var reference in parts) drawings.Add(component == WorkshopComponentKind.Structure
                ? AssemblyVisualComposer.ComposePart(reference, recipe.Structure.Family) : AssemblyVisualComposer.ComposePart(reference));
            fitted = new Rect[drawings.Count]; scales = new float[drawings.Count];
            FitParts(); LayoutCaptions();
            raycastTarget = false; SetVerticesDirty(); SetMaterialDirty();
        }

        /// <summary>The large workshop owns these captions; book and help miniatures remain unlabelled.</summary>
        public void ShowCategoryCaptions(WorkshopUi ui)
        {
            foreach (var caption in captions) if (caption != null) WorkshopUi.RemoveView(caption.gameObject);
            var categories = componentKind == WorkshopComponentKind.Structure
                ? new[] { "Site", "Function", "Shell", "Utility" }
                : foundationDiagram ? new[] { "Frame", "Hull", "Armor", "Feed", "Turn" }
                : new[] { "Mechanism", "Head", "Sight", "Magazine", "Trigger" };
            captions = categories.Select((category, index) => {
                var text = ui.Label(transform, "Assembly diagram category " + index,
                    ui.Resolve(UserText.Message("assembly.component.category." + category)), new Rect(0, 0, 140, 24), 18, color: WorkshopUi.Blue);
                text.alignment = TextAnchor.MiddleCenter; text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 15; text.resizeTextMaxSize = 18;
                return text;
            }).ToArray();
            FitParts(); LayoutCaptions();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange(); FitParts(); LayoutCaptions();
        }

        void FitParts()
        {
            var area = rectTransform.rect;
            for (int i = 0; i < drawings.Count; i++)
            {
                if (drawings[i].Fragments.Count == 0) { fitted[i] = Rect.zero; scales[i] = 0; continue; }
                var visible = AssemblyPreviewPose.VisibleBounds(drawings[i]); var slot = slots[i];
                scales[i] = .94f * Mathf.Min(area.width * slot.width / Mathf.Max(.001f, visible.width), area.height * slot.height / Mathf.Max(.001f, visible.height));
                var center = area.min + Vector2.Scale(slot.center, area.size);
                fitted[i] = new Rect(center - visible.size * scales[i] * .5f, visible.size * scales[i]);
            }
        }

        void LayoutCaptions()
        {
            var area = rectTransform.rect;
            bool large = area.width >= 520 && area.height >= 340;
            for (int i = 0; i < captions.Length; i++)
            {
                var text = captions[i]; if (text == null) continue;
                bool visible = large && i < fitted.Length && fitted[i].width > 0;
                text.gameObject.SetActive(visible); if (!visible) continue;
                var art = fitted[i]; float width = Mathf.Clamp(art.width, 110, 160);
                bool above = componentKind == WorkshopComponentKind.Weapon && (i == 1 || i == 2)
                    || foundationDiagram && i >= 2 || componentKind == WorkshopComponentKind.Structure && i >= 2;
                float x = art.center.x - width * .5f;
                // Keep labels beside the central stack's vertical connector corridor.
                if (i == 1 && componentKind == WorkshopComponentKind.Structure) x = art.xMin;
                if (i == 1 && foundationDiagram) x = art.xMax - width;
                float bottom = above ? art.yMax + 5 : art.yMin - 29;
                x = Mathf.Clamp(x, area.xMin, area.xMax - width);
                bottom = Mathf.Clamp(bottom, area.yMin, area.yMax - 24);
                WorkshopUi.Place(text.rectTransform, new Rect(x - area.xMin, area.yMax - bottom - 24, width, 24));
            }
        }

        static Vector2 EdgeToward(Rect bounds, Vector2 direction)
        {
            float alongX = Mathf.Abs(direction.x) < .001f ? float.PositiveInfinity : bounds.width * .5f / Mathf.Abs(direction.x);
            float alongY = Mathf.Abs(direction.y) < .001f ? float.PositiveInfinity : bounds.height * .5f / Mathf.Abs(direction.y);
            return bounds.center + direction * Mathf.Min(alongX, alongY);
        }

        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear(); arrows.Clear();
            var area = rectTransform.rect;
            if (area.width <= 0 || area.height <= 0 || drawings.Count == 0) return;
            FitParts();
            for (int i = 0; i < drawings.Count; i++)
            {
                if (drawings[i].Fragments.Count == 0) continue;
                if (i != 0)
                {
                    int receiver = foundationDiagram || componentKind == WorkshopComponentKind.Structure ? i == 1 ? 0 : 1 : 0;
                    if (fitted[receiver].width <= 0) continue;
                    var along = (fitted[receiver].center - fitted[i].center).normalized;
                    // End outside the fitted source artwork. The arrows are an assembly guide,
                    // not physical sockets, and must remain visible between opaque part drawings.
                    Vector2 start = EdgeToward(fitted[i], along) + along * 4;
                    Vector2 end = EdgeToward(fitted[receiver], -along) - along * 5;
                    float length = Vector2.Dot(end - start, along);
                    if (length < 4) continue;
                    int steps = Mathf.Max(2, Mathf.CeilToInt(length / 8));
                    for (int dash = 0; dash < steps; dash += 2)
                        arrows.Line(Vector2.Lerp(start,end,(float)dash/steps), Vector2.Lerp(start,end,Mathf.Min(1,(dash+1f)/steps)), 1.6f, MeshPen.Alpha(WorkshopUi.Blue, Opacity));
                    var side = new Vector2(-along.y, along.x); float tip = Mathf.Min(8, length * .6f);
                    arrows.Line(end, end-along*tip+side*tip*.5f, 1.7f, MeshPen.Alpha(WorkshopUi.Blue, Opacity)); arrows.Line(end, end-along*tip-side*tip*.5f, 1.7f, MeshPen.Alpha(WorkshopUi.Blue, Opacity));
                }
            }
            for (int i = 0; i < drawings.Count; i++)
            {
                var drawing = drawings[i]; if (drawing.Fragments.Count == 0) continue;
                var visible = AssemblyPreviewPose.VisibleBounds(drawing);
                part.Clear(); drawing.Draw(part, Vector2.zero, Opacity); part.CopyTo(target, fitted[i].center-visible.center*scales[i], scales[i]);
            }
            arrows.CopyTo(target, Vector2.zero, 1);
        }
    }

    /// <summary>Directions around the real foundation preview; this is a rule illustration, not a simulated hit.</summary>
    public sealed class WorkshopProtectionDirections : NotebookAtlasGraphic
    {
        readonly MeshPen pen = new MeshPen(null,512);
        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear(); pen.Clear(); var r=rectTransform.rect;
            foreach (var direction in new[] {Vector2.up,Vector2.down,Vector2.left,Vector2.right})
            {
                var side=new Vector2(-direction.y,direction.x);
                var end=r.center+Vector2.Scale(direction,r.size)*.46f;
                var start=end-direction*19;
                pen.Line(start,end,2.4f,WorkshopUi.Blue);
                pen.Line(end,end-direction*9+side*6,2.4f,WorkshopUi.Blue);
                pen.Line(end,end-direction*9-side*6,2.4f,WorkshopUi.Blue);
            }
            pen.CopyTo(target,Vector2.zero,1);
        }
    }
}
