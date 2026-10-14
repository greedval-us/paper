using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PaperDefense.Application;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Editor
{
    /// <summary>Validate the complete candidate before replacing the last good visual asset or current templates.</summary>
    public static class NotebookArtImportTools
    {
        public const string SourcePath = "ArtSource/Notebook/notebook-theme.json";
        public const string ThemePath = "Assets/PaperDefense/Resources/Art/NotebookVisualTheme.asset";
        public const string TargetPath = "Assets/PaperDefense/Configuration/Content/Target.asset";
        static readonly string[] parts = {
            "chassis.standard", "chassis.heavy", "chassis.precision", "chassis.battery",
            "weapon.gun", "weapon.piercer", "weapon.mortar", "weapon.fan", "weapon.ricochet", "weapon.stapler", "weapon.blotter",
            "module.armor", "module.feed", "module.tracking", "module.rail"
        };

        [MenuItem("Paper Defense/Art/Import notebook art and current templates")]
        public static void ImportAll()
        {
            var source = ParseAndValidate(File.ReadAllText(SourcePath), SourcePath);
            var target = AssetDatabase.LoadAssetAtPath<BalancePresetAsset>(TargetPath);
            if (target == null || target.database == null) throw Error(TargetPath, "target", "missing active preset");
            var overrides = CreateTargetOverrides(source, target);

            var catalogs = new List<KeyValuePair<ContentDatabaseAsset, ContentCatalogInput>>();
            foreach (var guid in AssetDatabase.FindAssets("t:ContentDatabaseAsset", new[] { "Assets/PaperDefense/Configuration/Tests" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(path);
                var candidate = asset.catalog.CreateCopy();
                PublishTemplates(source, candidate.Templates, candidate.Templates);
                foreach (var part in candidate.Chassis) part.VisualStyle = DrawingVisualStyle.Template;
                foreach (var part in candidate.Weapons) part.VisualStyle = DrawingVisualStyle.Template;
                foreach (var part in candidate.Modules) part.VisualStyle = DrawingVisualStyle.Template;
                candidate.CreateSnapshot();
                catalogs.Add(new KeyValuePair<ContentDatabaseAsset, ContentCatalogInput>(asset, candidate));
            }
            // Everything above is a detached candidate. Historical, source catalog and player profiles are untouched.
            var theme = AssetDatabase.LoadAssetAtPath<NotebookVisualThemeAsset>(ThemePath);
            if (theme == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
                theme = ScriptableObject.CreateInstance<NotebookVisualThemeAsset>();
                theme.ReplaceData(source);
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            else { theme.ReplaceData(source); EditorUtility.SetDirty(theme); }
            target.overrides = overrides; EditorUtility.SetDirty(target);
            foreach (var pair in catalogs) { pair.Key.catalog = pair.Value; EditorUtility.SetDirty(pair.Key); }
            AssetDatabase.SaveAssets(); NotebookVisualThemeAsset.ResetDefaultCache();
            ExportSvgContactSheets(source);
            AssetDatabase.Refresh();
            Debug.Log($"Notebook art imported: {source.Shapes.Count} shapes, {source.Clips.Count} clips, {source.Recoils.Count} recoil curves; {catalogs.Count + 1} current catalogs updated.");
        }

        public static NotebookArtSource ParseAndValidate(string json, string sourcePath = SourcePath)
        {
            NotebookArtSource data;
            try { data = JsonUtility.FromJson<NotebookArtSource>(json); }
            catch (Exception exception) { throw Error(sourcePath, "json", exception.Message); }
            Validate(data, sourcePath); return data;
        }
        /// <summary>Build the active-preset candidate without mutating either its database or existing overrides.</summary>
        public static ContentOverrides CreateTargetOverrides(NotebookArtSource source, BalancePresetAsset target)
        {
            Validate(source);
            if (target == null || target.database == null) throw Error(TargetPath, "target", "missing active preset");
            var overrides = Clone(target.overrides);
            var originals = target.database.catalog;
            PublishTemplates(source, originals.Templates, overrides.Templates);
            OptIntoArt(originals.Chassis, overrides.Chassis);
            OptIntoArt(originals.Weapons, overrides.Weapons);
            OptIntoArt(originals.Modules, overrides.Modules);
            target.database.CreateSnapshot(overrides);
            return overrides;
        }
        public static void Validate(NotebookArtSource data, string sourcePath = SourcePath)
        {
            if (data == null || data.SchemaVersion != 1) throw Error(sourcePath, "schema", "expected schema version 1");
            if (string.IsNullOrWhiteSpace(data.Revision)) throw Error(sourcePath, "revision", "missing revision");
            Finite(data.MaximumTerminalTailSeconds, sourcePath, "terminal-tail");
            if (data.MaximumTerminalTailSeconds <= 0 || data.MaximumTerminalTailSeconds > 5 || data.MaximumEffects < 1 || data.MaximumEffects > 4096 || data.MaximumDeaths < 1 || data.MaximumDeaths > 4096)
                throw Error(sourcePath, "visual-budgets", "invalid bounded effect or terminal tail budget");
            if (data.Palette == null || data.Palette.Length != Enum.GetValues(typeof(NotebookInkChannel)).Length)
                throw Error(sourcePath, "palette", "all ink channels are required");
            foreach (var color in data.Palette) { Finite(color.r, sourcePath, "palette"); Finite(color.g, sourcePath, "palette"); Finite(color.b, sourcePath, "palette"); Finite(color.a, sourcePath, "palette"); }
            if (data.Shapes == null || data.Clips == null || data.Recoils == null) throw Error(sourcePath, "catalog", "missing lists");
            var shapes = new Dictionary<string, NotebookArtShape>(StringComparer.Ordinal);
            var partIds = new HashSet<string>(parts, StringComparer.Ordinal);
            var templateIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var shape in data.Shapes)
            {
                if (shape == null || string.IsNullOrWhiteSpace(shape.Id) || shapes.ContainsKey(shape.Id)) throw Error(sourcePath, shape?.Id, "missing or duplicate shape ID");
                shapes.Add(shape.Id, shape);
                if (partIds.Contains(shape.Id) && !string.Equals(shape.TemplateId, "template." + shape.Id, StringComparison.Ordinal))
                    throw Error(sourcePath, shape.Id, "drawing template binding must be template." + shape.Id);
                if (!string.IsNullOrEmpty(shape.TemplateId))
                {
                    if (!templateIds.Add(shape.TemplateId)) throw Error(sourcePath, shape.Id, "duplicate drawing template binding " + shape.TemplateId);
                    if (!partIds.Contains(shape.Id)) throw Error(sourcePath, shape.Id, "only modular part shapes may publish drawing templates");
                }
                if (shape.Marks == null || shape.Marks.Count == 0 || shape.Marks.Count > 512) throw Error(sourcePath, shape.Id, "invalid mark count");
                int references = 0, points = 0;
                foreach (var mark in shape.Marks)
                {
                    if (mark == null || !Enum.IsDefined(typeof(NotebookArtPrimitive), mark.Primitive) || !Enum.IsDefined(typeof(NotebookInkChannel), mark.Channel)) throw Error(sourcePath, shape.Id, "unsupported primitive or color channel");
                    Finite(mark.Width, sourcePath, shape.Id); Finite(mark.Alpha, sourcePath, shape.Id);
                    if (mark.Width <= 0 || mark.Width > .2f || mark.Alpha < 0 || mark.Alpha > 1) throw Error(sourcePath, shape.Id, "invalid stroke width or alpha");
                    if (mark.Points == null) throw Error(sourcePath, shape.Id, "null points");
                    foreach (var point in mark.Points) CheckPoint(point, sourcePath, shape.Id);
                    if (mark.Primitive == NotebookArtPrimitive.Polyline && mark.Points.Length < 2) throw Error(sourcePath, shape.Id, "polyline requires at least two points");
                    if (mark.Primitive == NotebookArtPrimitive.Polygon && (mark.Points.Length < 3 || !Convex(mark.Points))) throw Error(sourcePath, shape.Id, "fill must be a simple convex polygon with distinct vertices");
                    if (mark.Primitive == NotebookArtPrimitive.Circle || mark.Primitive == NotebookArtPrimitive.Ring)
                    {
                        CheckPoint(mark.Center, sourcePath, shape.Id); Finite(mark.Radius, sourcePath, shape.Id); Finite(mark.Aspect, sourcePath, shape.Id);
                        if (mark.Radius <= 0 || mark.Radius > 1 || mark.Aspect <= 0 || mark.Aspect > 3 || mark.Segments < 8 || mark.Segments > 64) throw Error(sourcePath, shape.Id, "invalid circle or ring");
                    }
                    if (!mark.Reference) continue;
                    if (mark.Primitive != NotebookArtPrimitive.Polyline || string.IsNullOrWhiteSpace(shape.TemplateId)) throw Error(sourcePath, shape.Id, "reference marks must be template polylines");
                    references++; points += mark.Points.Length;
                }
                if (!string.IsNullOrEmpty(shape.TemplateId) && (shape.ReferenceRevision < 1 || references < 1 || references > DrawingSnapshot.MaxStrokes || points > DrawingSnapshot.MaxPoints)) throw Error(sourcePath, shape.Id, "invalid drawing reference or drawing budget");
            }
            foreach (var id in parts) if (!shapes.ContainsKey(id)) throw Error(sourcePath, id, "required modular part missing");
            foreach (var id in parts) if (!shapes.ContainsKey("ui." + id)) throw Error(sourcePath, "ui." + id, "required workshop icon missing");
            foreach (var id in new[] { "object.castle", "object.castle.damaged", "object.spikes", "object.blot", "object.well.red", "object.well.blue", "object.well.green",
                "fx.muzzle", "fx.tracer", "fx.piercer", "fx.mortar.projectile", "fx.mortar.explosion", "fx.fan", "fx.ricochet", "fx.stapler", "fx.cleanup",
                "fx.status.red", "fx.status.blue", "fx.status.green", "fx.blot.birth", "fx.blot.absorb", "fx.blot.cleanse", "fx.blot.expire", "fx.spitter.projectile", "fx.spitter.impact", "fx.well.activate", "fx.well.hit", "fx.castle.hit", "fx.structure.destroy" })
                if (!shapes.ContainsKey(id)) throw Error(sourcePath, id, "required object or semantic effect missing");
            for (int mask = 0; mask < 16; mask++) if (!shapes.ContainsKey("object.wall." + mask)) throw Error(sourcePath, "object.wall." + mask, "required wall connection missing");
            var clips = new HashSet<string>(StringComparer.Ordinal);
            foreach (var clip in data.Clips)
            {
                if (clip == null || string.IsNullOrWhiteSpace(clip.Id) || !clips.Add(clip.Id)) throw Error(sourcePath, clip?.Id, "missing or duplicate clip ID");
                Finite(clip.Duration, sourcePath, clip.Id);
                if (clip.Duration <= 0 || clip.Duration > data.MaximumTerminalTailSeconds || clip.Frames == null || clip.Frames.Count == 0) throw Error(sourcePath, clip.Id, "invalid clip duration or frames");
                float previous = -1;
                foreach (var frame in clip.Frames)
                {
                    if (frame == null || !shapes.ContainsKey(frame.ShapeId ?? "")) throw Error(sourcePath, clip.Id, "frame shape does not exist");
                    Finite(frame.Time, sourcePath, clip.Id); Finite(frame.Scale, sourcePath, clip.Id); Finite(frame.Rotation, sourcePath, clip.Id); Finite(frame.Alpha, sourcePath, clip.Id); CheckPoint(frame.Offset, sourcePath, clip.Id);
                    if (frame.Time <= previous || frame.Time < 0 || frame.Time > clip.Duration || frame.Scale < 0 || frame.Scale > 4 || frame.Alpha < 0 || frame.Alpha > 1) throw Error(sourcePath, clip.Id, "invalid frame time or transform");
                    previous = frame.Time;
                }
                if (clip.Frames[0].Time != 0) throw Error(sourcePath, clip.Id, "first frame must start at zero");
            }
            foreach (string family in new[] { "doodle", "runner", "gnawer", "spitter", "brute", "bulwark" })
                foreach (string tier in new[] { "sketch", "outlined", "hatched" })
                    foreach (string state in new[] { "idle", "walk", "attack", "hit", "death" })
                        if (!clips.Contains($"enemy.{family}.{tier}.{state}")) throw Error(sourcePath, $"enemy.{family}.{tier}.{state}", "required enemy clip missing");
            var recoilIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var recoil in data.Recoils)
            {
                if (recoil == null || !recoilIds.Add(recoil.WeaponId ?? "") || !shapes.ContainsKey(recoil.WeaponId ?? "") || !shapes.ContainsKey(recoil.EffectId ?? "")) throw Error(sourcePath, recoil?.WeaponId, "invalid recoil ID or effect");
                Finite(recoil.Duration, sourcePath, recoil.WeaponId); Finite(recoil.Distance, sourcePath, recoil.WeaponId); CheckPoint(recoil.Muzzle, sourcePath, recoil.WeaponId);
                if (recoil.Duration <= 0 || recoil.Distance < 0 || recoil.Distance > .3f || recoil.Keys == null || recoil.Keys.Count < 2) throw Error(sourcePath, recoil.WeaponId, "invalid recoil curve");
                float previous = -1;
                foreach (var key in recoil.Keys)
                {
                    if (key == null) throw Error(sourcePath, recoil.WeaponId, "null recoil key");
                    Finite(key.Time, sourcePath, recoil.WeaponId); Finite(key.Value, sourcePath, recoil.WeaponId);
                    if (key.Time <= previous || key.Time > recoil.Duration || key.Value < 0 || key.Value > 1) throw Error(sourcePath, recoil.WeaponId, "invalid recoil key"); previous = key.Time;
                }
                if (recoil.Keys[0].Time != 0 || recoil.Keys[0].Value != 0 || recoil.Keys[recoil.Keys.Count - 1].Time != recoil.Duration || recoil.Keys[recoil.Keys.Count - 1].Value != 0) throw Error(sourcePath, recoil.WeaponId, "recoil must return exactly to rest");
            }
            foreach (var id in parts.Where(id => id.StartsWith("weapon.", StringComparison.Ordinal))) if (!recoilIds.Contains(id)) throw Error(sourcePath, id, "required recoil missing");
        }

        public static DrawingTemplateDefinitionInput CreateReference(NotebookArtShape shape, DrawingTemplateDefinitionInput previous)
        {
            if (shape == null || previous == null) throw new ArgumentNullException(shape == null ? nameof(shape) : nameof(previous));
            var result = Clone(previous); result.Revision = shape.ReferenceRevision; result.Contours.Clear();
            foreach (var mark in shape.Marks)
                if (mark.Reference)
                {
                    var contour = new DrawingContourInput();
                    foreach (var point in mark.Points) contour.Points.Add(new DrawingCoordinateInput(point.x, point.y));
                    result.Contours.Add(contour);
                }
            result.CreateSnapshot(); return result;
        }
        static void PublishTemplates(NotebookArtSource source, List<DrawingTemplateDefinitionInput> originals, List<DrawingTemplateDefinitionInput> destination)
        {
            foreach (var shape in source.Shapes)
            {
                if (string.IsNullOrEmpty(shape.TemplateId)) continue;
                // The effective current override owns metadata such as anchors and assessment weights.
                // Artwork changes only its reference contour and authored revision on reimport.
                var previous = destination.FirstOrDefault(item => item.Id == shape.TemplateId)
                    ?? originals.FirstOrDefault(item => item.Id == shape.TemplateId);
                if (previous == null) throw Error(SourcePath, shape.Id, "current catalog template missing");
                Replace(destination, CreateReference(shape, previous), item => item.Id);
            }
        }
        static void OptIntoArt<T>(List<T> originals, List<T> destination) where T : PartDefinitionInput
        {
            foreach (var original in originals)
            {
                var candidate = Clone(destination.FirstOrDefault(item => item.Id == original.Id) ?? original);
                candidate.VisualStyle = DrawingVisualStyle.Template; Replace(destination, candidate, item => item.Id);
            }
        }
        static void Replace<T>(List<T> list, T value, Func<T, string> id)
        { int index = list.FindIndex(item => id(item) == id(value)); if (index < 0) list.Add(value); else list[index] = value; }
        static T Clone<T>(T source) => JsonUtility.FromJson<T>(JsonUtility.ToJson(source));
        static bool Convex(Vector2[] points)
        {
            const float epsilon = .0000001f;
            for (int i = 0; i < points.Length; i++)
                for (int j = i + 1; j < points.Length; j++)
                    if ((points[i] - points[j]).sqrMagnitude <= epsilon * epsilon) return false;
            // Consistent local turns alone also accept a pentagram. A polygon must first be simple.
            for (int i = 0; i < points.Length; i++)
                for (int j = i + 1; j < points.Length; j++)
                {
                    int nextI = (i + 1) % points.Length, nextJ = (j + 1) % points.Length;
                    if (nextI == j || nextJ == i) continue;
                    if (SegmentsIntersect(points[i], points[nextI], points[j], points[nextJ], epsilon)) return false;
                }
            float sign = 0;
            for (int i = 0; i < points.Length; i++)
            {
                var a = points[(i + 1) % points.Length] - points[i]; var b = points[(i + 2) % points.Length] - points[(i + 1) % points.Length];
                float cross = a.x * b.y - a.y * b.x;
                if (Mathf.Abs(cross) <= epsilon) continue;
                if (sign != 0 && sign * cross < 0) return false; sign = cross;
            }
            return sign != 0;
        }
        static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float epsilon)
        {
            float abC = Cross(b - a, c - a), abD = Cross(b - a, d - a);
            float cdA = Cross(d - c, a - c), cdB = Cross(d - c, b - c);
            if (((abC > epsilon && abD < -epsilon) || (abC < -epsilon && abD > epsilon))
                && ((cdA > epsilon && cdB < -epsilon) || (cdA < -epsilon && cdB > epsilon))) return true;
            return Mathf.Abs(abC) <= epsilon && WithinSegment(c, a, b, epsilon)
                || Mathf.Abs(abD) <= epsilon && WithinSegment(d, a, b, epsilon)
                || Mathf.Abs(cdA) <= epsilon && WithinSegment(a, c, d, epsilon)
                || Mathf.Abs(cdB) <= epsilon && WithinSegment(b, c, d, epsilon);
        }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static bool WithinSegment(Vector2 point, Vector2 a, Vector2 b, float epsilon)
            => point.x >= Mathf.Min(a.x, b.x) - epsilon && point.x <= Mathf.Max(a.x, b.x) + epsilon
                && point.y >= Mathf.Min(a.y, b.y) - epsilon && point.y <= Mathf.Max(a.y, b.y) + epsilon;
        static void CheckPoint(Vector2 point, string path, string id)
        { Finite(point.x, path, id); Finite(point.y, path, id); if (Mathf.Abs(point.x) > 1 || Mathf.Abs(point.y) > 1) throw Error(path, id, "point outside normalized [-1,1] bounds"); }
        static void Finite(float value, string path, string id) { if (float.IsNaN(value) || float.IsInfinity(value)) throw Error(path, id, "non-finite value"); }
        static ArgumentException Error(string path, string id, string reason) => new ArgumentException(path + " → " + (id ?? "<null>") + ": " + reason);

        [MenuItem("Paper Defense/Art/Export notebook SVG contact sheets")]
        public static void ExportSvgContactSheets() => ExportSvgContactSheets(ParseAndValidate(File.ReadAllText(SourcePath)));
        static void ExportSvgContactSheets(NotebookArtSource source)
        {
            ExportSheet(source, "Imported-modular-parts", source.Shapes.Where(shape => !string.IsNullOrEmpty(shape.TemplateId)).ToList(), 5);
            ExportSheet(source, "Imported-enemies", source.Shapes.Where(shape => shape.Id.StartsWith("enemy.", StringComparison.Ordinal) && shape.Id.EndsWith(".idle", StringComparison.Ordinal)).ToList(), 3);
            ExportSheet(source, "Imported-objects", source.Shapes.Where(shape => shape.Id.StartsWith("object.", StringComparison.Ordinal)).ToList(), 5);
            ExportSheet(source, "Imported-terrain", source.Shapes.Where(shape => shape.Id.StartsWith("terrain.", StringComparison.Ordinal)).ToList(), 3);
            ExportSheet(source, "Imported-effects", source.Shapes.Where(shape => shape.Id.StartsWith("fx.", StringComparison.Ordinal)).ToList(), 5);
        }
        static void ExportSheet(NotebookArtSource source, string name, List<NotebookArtShape> shapes, int columns)
        {
            int width = columns * 230, height = (shapes.Count + columns - 1) / columns * 210 + 60;
            var svg = new StringBuilder();
            svg.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\"><rect width=\"100%\" height=\"100%\" fill=\"#f9f4e6\"/>");
            for (int i = 0; i < shapes.Count; i++)
            {
                int x = i % columns * 230 + 115, y = i / columns * 210 + 120;
                svg.Append($"<g transform=\"translate({x},{y}) scale(130)\">");
                foreach (var mark in shapes[i].Marks)
                {
                    var color = source.Palette[(int)mark.Channel]; string hex = "#" + ColorUtility.ToHtmlStringRGB(color);
                    string opacity = F(mark.Alpha * color.a);
                    if (mark.Primitive == NotebookArtPrimitive.Polyline || mark.Primitive == NotebookArtPrimitive.Polygon)
                    {
                        string points = string.Join(" ", mark.Points.Select(point => F(point.x) + "," + F(-point.y)));
                        if (mark.Primitive == NotebookArtPrimitive.Polygon) svg.Append($"<polygon points=\"{points}\" fill=\"{hex}\" opacity=\"{opacity}\"/>");
                        else svg.Append($"<polyline points=\"{points}\" fill=\"none\" stroke=\"{hex}\" stroke-width=\"{F(mark.Width)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" opacity=\"{opacity}\"/>");
                    }
                    else
                    {
                        string style = mark.Primitive == NotebookArtPrimitive.Circle ? $"fill=\"{hex}\"" : $"fill=\"none\" stroke=\"{hex}\" stroke-width=\"{F(mark.Width)}\"";
                        svg.Append($"<ellipse cx=\"{F(mark.Center.x)}\" cy=\"{F(-mark.Center.y)}\" rx=\"{F(mark.Radius)}\" ry=\"{F(mark.Radius * mark.Aspect)}\" {style} opacity=\"{opacity}\"/>");
                    }
                }
                svg.Append($"</g><text x=\"{x}\" y=\"{y + 82}\" text-anchor=\"middle\" font-family=\"monospace\" font-size=\"11\" fill=\"#1f3047\">{System.Security.SecurityElement.Escape(shapes[i].Id)}</text>");
            }
            svg.Append("</svg>"); Directory.CreateDirectory("Assets/PaperDefense/Art");
            File.WriteAllText("Assets/PaperDefense/Art/" + name + ".svg", svg.ToString());
        }
        static string F(float value) => value.ToString("0.#####", CultureInfo.InvariantCulture);
    }
}
