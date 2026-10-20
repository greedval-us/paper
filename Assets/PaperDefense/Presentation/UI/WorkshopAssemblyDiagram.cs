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
        bool foundationDiagram;
        public IReadOnlyList<PartRef> Parts => parts;

        public void Configure(AssemblyRecipe recipe, WorkshopComponentKind component, int mount)
        {
            foundationDiagram = component == WorkshopComponentKind.Foundation;
            if (component == WorkshopComponentKind.Structure)
            {
                parts = recipe.Structure.Parts.ToArray();
                slots = new[] { new Rect(.15f,.02f,.72f,.25f), new Rect(.09f,.37f,.47f,.42f), new Rect(.53f,.67f,.43f,.30f), new Rect(.72f,.30f,.26f,.29f) };
            }
            else if (component == WorkshopComponentKind.Foundation)
            {
                parts = recipe.Tower.Foundation.Parts.ToArray();
                slots = new[] { new Rect(.18f,.03f,.72f,.23f), new Rect(.10f,.38f,.64f,.29f), new Rect(.78f,.36f,.21f,.30f), new Rect(.02f,.69f,.26f,.22f), new Rect(.43f,.70f,.37f,.24f) };
            }
            else
            {
                parts = recipe.Tower.Mounts.Single(value => value.SlotIndex == mount).Weapon.Parts.ToArray();
                slots = new[] { new Rect(.29f,.25f,.43f,.42f), new Rect(.35f,.66f,.38f,.33f), new Rect(.76f,.76f,.22f,.20f), new Rect(.01f,.34f,.25f,.34f), new Rect(.76f,.15f,.22f,.35f) };
            }
            drawings.Clear();
            foreach (var reference in parts) drawings.Add(AssemblyVisualComposer.ComposePart(reference));
            raycastTarget = false; SetVerticesDirty(); SetMaterialDirty();
        }

        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear(); arrows.Clear();
            var area = rectTransform.rect;
            if (area.width <= 0 || area.height <= 0 || drawings.Count == 0) return;
            Vector2 Point(float x, float y) => area.min + new Vector2(x * area.width, y * area.height);
            var hub = Point(.52f, .31f);
            for (int i = 0; i < drawings.Count; i++)
            {
                if (drawings[i].Fragments.Count == 0) continue;
                var slot = slots[i]; var at = Point(slot.center.x, slot.center.y);
                if (i != 0)
                {
                    var destination = foundationDiagram
                        ? Point(slots[i == 1 ? 0 : 1].center.x, slots[i == 1 ? 0 : 1].center.y) : hub;
                    Vector2 end = destination + (at - destination).normalized * 16;
                    Vector2 start = Vector2.Lerp(at, end, .40f);
                    int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(start, end) / 8));
                    for (int dash = 0; dash < steps; dash += 2)
                        arrows.Line(Vector2.Lerp(start,end,(float)dash/steps), Vector2.Lerp(start,end,Mathf.Min(1,(dash+1f)/steps)), 1.6f, WorkshopUi.Blue);
                    var along = (end-start).normalized; var side = new Vector2(-along.y, along.x);
                    arrows.Line(end, end-along*8+side*4, 1.7f, WorkshopUi.Blue); arrows.Line(end, end-along*8-side*4, 1.7f, WorkshopUi.Blue);
                }
            }
            if (!foundationDiagram) arrows.CopyTo(target, Vector2.zero, 1);
            for (int i = 0; i < drawings.Count; i++)
            {
                var drawing = drawings[i]; if (drawing.Fragments.Count == 0) continue;
                var slot = slots[i]; var at = Point(slot.center.x, slot.center.y);
                float scale = .94f * Mathf.Min(area.width * slot.width / Mathf.Max(.001f,drawing.Bounds.width), area.height * slot.height / Mathf.Max(.001f,drawing.Bounds.height));
                part.Clear(); drawing.Draw(part, Vector2.zero); part.CopyTo(target, at-drawing.Bounds.center*scale, scale);
            }
            if (foundationDiagram) arrows.CopyTo(target, Vector2.zero, 1);
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
