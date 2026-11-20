using System;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Stock semantic aliases reuse the same approved parts as exact modern recipes.</summary>
    internal static class StandardNotebookArt
    {
        static readonly Dictionary<string, NotebookArtShape> stock = new Dictionary<string, NotebookArtShape>();

        internal static NotebookArtShape Shape(string id)
        {
            string canonical = Canonical(id);
            if (canonical == null) return null;
            if (stock.TryGetValue(canonical, out var found)) return found;
            var pen = new MeshPen(null, 2048);
            if (canonical.StartsWith("weapon.", StringComparison.Ordinal))
            {
                string family = canonical.Substring(7);
                var weapon = new WeaponRecipe(P("core." + family), P("head." + family), P("sight.iron"),
                    P("magazine.standard"), P("trigger.standard"));
                var composition = AssemblyVisualComposer.ComposeWeapon(weapon);
                composition.Draw(pen, Vector2.zero);
            }
            else if (canonical.StartsWith("chassis.", StringComparison.Ordinal))
            {
                bool heavy = canonical == "chassis.heavy";
                AssemblyVisualComposer.ComposeFoundation(new FoundationRecipe(P(heavy ? "frame.heavy" : "frame.standard"),
                    P(heavy ? "hull.reinforced" : "hull.standard"), P("armor.none"), P("feed.standard"), P("turn.standard")))
                    .Draw(pen, Vector2.zero);
            }
            else if (canonical.StartsWith("module.", StringComparison.Ordinal))
            {
                string part = canonical == "module.armor" ? "armor.plate" : canonical == "module.feed" ? "feed.boost"
                    : canonical == "module.tracking" ? "turn.quick" : canonical == "module.rail" ? "head.piercer" : "utility.range";
                Part(pen, part);
            }
            else
            {
                string family = canonical.Substring("support.".Length);
                Part(pen, "site." + family);
                Part(pen, "function." + family);
                Part(pen, "shell.standard");
            }
            var shape = Capture(pen, canonical);
            stock.Add(canonical, shape);
            return shape;
        }

        static void Part(MeshPen pen, string id) => AssemblyVisualComposer.ComposePart(P(id)).Draw(pen, Vector2.zero);
        static PartRef P(string id) => new PartRef(id, 1);

        static string Canonical(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (id.StartsWith("ui.", StringComparison.Ordinal)) id = id.Substring(3);
            if (id.StartsWith("weapon.", StringComparison.Ordinal))
            {
                switch (id.Substring(7))
                {
                    case "gun": case "piercer": case "mortar": case "fan": case "ricochet": case "stapler": case "blotter": return id;
                    // A retired visual-only name uses the approved long barrel. Gameplay IDs are untouched.
                    case "rail": return "weapon.piercer";
                    default: return null;
                }
            }
            switch (id)
            {
                case "chassis.standard": case "chassis.heavy":
                case "module.armor": case "module.feed": case "module.tracking": case "module.rail": case "module.range": return id;
                case "object.wall": case "tool.wall": case "structure.wall": return "support.wall";
                case "object.spikes": case "tool.spikes": case "structure.spikes": return "support.spikes";
                case "object.tar": case "tool.tar": case "structure.slow": return "support.slow";
                case "object.repair": case "tool.repair": case "structure.repair": return "support.repair";
                case "object.generator": case "structure.generator": return "support.generator";
                case "object.beacon": case "structure.beacon": return "support.beacon";
                case "object.prism": case "structure.prism": return "support.prism";
            }
            if (id.StartsWith("object.wall.", StringComparison.Ordinal) &&
                int.TryParse(id.Substring("object.wall.".Length), out int mask) && mask >= 0 && mask <= 15)
                return "support.wall";
            return null;
        }

        static NotebookArtShape Capture(MeshPen pen, string id)
        {
            var shape = new NotebookArtShape { Id = id, TemplateId = NightNotebookArt.Revision + "/" + id };
            using (var mesh = new VertexHelper())
            {
                pen.CopyTo(mesh, Vector2.zero, 1);
                var triangles = new List<UIVertex>(); mesh.GetUIVertexStream(triangles);
                for (int index = 0; index < triangles.Count;)
                {
                    var a = triangles[index]; var b = triangles[index + 1]; var c = triangles[index + 2];
                    var mark = new NotebookArtMark { Channel = Channel(a.color), Alpha = a.color.a / 255f };
                    if (index + 5 < triangles.Count && a.position == triangles[index + 3].position &&
                        c.position == triangles[index + 4].position && a.color.Equals(triangles[index + 5].color) &&
                        ((a.position + c.position) - (b.position + triangles[index + 5].position)).sqrMagnitude < .0000000001f &&
                        Perpendicular(b.position - a.position, triangles[index + 5].position - a.position))
                    {
                        // MeshPen.Line emits one quad. Retain its centerline and width, so
                        // the existing screen-space floor and antialiasing still apply in battle.
                        var d = triangles[index + 5];
                        mark.Primitive = NotebookArtPrimitive.Polyline;
                        mark.Points = new[] { (Vector2)((a.position + d.position) * .5f), (Vector2)((b.position + c.position) * .5f) };
                        mark.Width = Vector2.Distance(a.position, d.position);
                        index += 6;
                    }
                    else
                    {
                        mark.Primitive = NotebookArtPrimitive.Polygon;
                        mark.Points = new[] { (Vector2)a.position, (Vector2)b.position, (Vector2)c.position };
                        index += 3;
                    }
                    shape.Marks.Add(mark);
                }
                if (id.StartsWith("weapon.", StringComparison.Ordinal))
                {
                    // The visible head deliberately leaves a margin inside the certified
                    // modern envelope. Align its actual front edge, not that envelope, with
                    // the existing legacy (0,.35) muzzle. This rigid shift changes no strokes.
                    Vector2 offset = new Vector2(0, .35f) - FrontTip(triangles);
                    foreach (var mark in shape.Marks)
                        for (int index = 0; index < mark.Points.Length; index++) mark.Points[index] += offset;
                }
            }
            return shape;
        }

        static bool Perpendicular(Vector3 a, Vector3 b)
            => Mathf.Abs(Vector3.Dot(a, b)) <= a.magnitude * b.magnitude * .0001f;

        static Vector2 FrontTip(List<UIVertex> triangles)
        {
            Color32 ink = NightNotebookArt.Ink;
            float front = float.NegativeInfinity, low = float.PositiveInfinity, high = float.NegativeInfinity;
            foreach (var vertex in triangles)
                if (vertex.color.Equals(ink)) front = Mathf.Max(front, vertex.position.y);
            foreach (var vertex in triangles)
                if (vertex.color.Equals(ink) && Mathf.Abs(vertex.position.y - front) < .000001f)
                { low = Mathf.Min(low, vertex.position.x); high = Mathf.Max(high, vertex.position.x); }
            return new Vector2((low + high) * .5f, front);
        }

        static NotebookInkChannel Channel(Color32 color)
        {
            if (Same(color, NightNotebookArt.Paper)) return NotebookInkChannel.NotebookPaper;
            if (Same(color, NightNotebookArt.Pencil)) return NotebookInkChannel.Sketch;
            if (Same(color, NightNotebookArt.Body)) return NotebookInkChannel.Body;
            if (Same(color, NightNotebookArt.Accent)) return NotebookInkChannel.Amber;
            if (Same(color, NightNotebookEffectPalette.Clean)) return NotebookInkChannel.Clean;
            if (Same(color, NightNotebookArt.GlassInk)) return NotebookInkChannel.GlassInk;
            if (Same(color, NightNotebookArt.Copper)) return NotebookInkChannel.Copper;
            if (Same(color, NightNotebookArt.RedEnamel)) return NotebookInkChannel.Danger;
            if (Same(color, NightNotebookArt.BladeSteel)) return NotebookInkChannel.BladeSteel;
            return NotebookInkChannel.Ivory;
        }
        static bool Same(Color32 a, Color b)
        { var color = (Color32)b; return a.r == color.r && a.g == color.g && a.b == color.b; }
    }
}
