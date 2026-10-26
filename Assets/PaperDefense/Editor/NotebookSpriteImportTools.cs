using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PaperDefense.Application;
using PaperDefense.Presentation;
using UnityEditor;
using UnityEngine;

namespace PaperDefense.Editor
{
    /// <summary>Rebuilds visual resources without publishing or changing gameplay drawing templates.</summary>
    public static class NotebookSpriteImportTools
    {
        public const string SourcePath = "ArtSource/Notebook/NewStyle/sprites.source.json";
        public const string AnimationPath = "ArtSource/Notebook/NewStyle/animation.json";
        public const string AssemblyPath = "ArtSource/Notebook/NewStyle/assembly-parts.source.json";
        public const string AtlasPath = "Assets/PaperDefense/Resources/Art/NotebookAtlas.png";
        public const string CatalogPath = "Assets/PaperDefense/Resources/Art/NotebookSprites.json";
        public const string MountsPath = "Assets/PaperDefense/Resources/Art/NotebookMounts.json";
        public const string ThemePath = "Assets/PaperDefense/Resources/Art/NotebookVisualTheme.asset";
        public const string AssemblyCatalogPath = "Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset";
        public const string PackingPath = "Tools/ArtAssembly13/packing-4096.json";

        [Serializable] public sealed class SpriteBounds { public float x, y, width, height; }
        [Serializable] public sealed class SpriteSource
        {
            public string id, source;
            public int x, y, width, height;
            public SpriteBounds bounds;
        }
        [Serializable] public sealed class SpriteAlias { public string id, alias; }
        [Serializable] public sealed class SourceManifest
        {
            public string revision;
            public SpriteSource[] sprites = Array.Empty<SpriteSource>();
            public SpriteAlias[] aliases = Array.Empty<SpriteAlias>();
        }
        [Serializable] sealed class Pixel { public int x, y; }
        [Serializable] sealed class NativePacking
        {
            public int atlas, height, gutter;
            public int Height => height > 0 ? height : atlas;
            public NativeRectangle[] rectangles;
        }
        [Serializable] sealed class NativeRectangle
        {
            public string id;
            public int index, x, y, width, height;
        }
        [Serializable] sealed class PackedSprite
        {
            public string id, alias;
            public int x, y, width, height;
            public SpriteBounds bounds;
        }
        [Serializable] sealed class RuntimeManifest
        {
            public string revision;
            public Pixel whitePixel;
            public PackedSprite[] sprites;
        }

        [MenuItem("Paper Defense/Art/Rebuild blue pen atlas")]
        static void RebuildFromMenu() => Rebuild();

        public static string Rebuild()
        {
            var source = ParseSource(File.ReadAllText(SourcePath));
            var semanticIds = SemanticIds(source);
            var animation = ParseAnimation(File.ReadAllText(AnimationPath), semanticIds);
            var catalog = AssetDatabase.LoadAssetAtPath<AssemblyCatalogAsset>(AssemblyCatalogPath);
            if (catalog == null) throw new FormatException("The production component catalog is required before art import.");
            string mountsJson = AssemblyVisualComposer.BuildRuntimeJson(File.ReadAllText(AssemblyPath), semanticIds,
                source.sprites.ToDictionary(s => s.id, s => new Rect(s.bounds.x, s.bounds.y, s.bounds.width, s.bounds.height)), catalog.CreateSnapshot().Definitions);
            NotebookMountLayout.Validate(mountsJson);
            var theme = AssetDatabase.LoadAssetAtPath<NotebookVisualThemeAsset>(ThemePath);
            if (theme == null) throw Error(ThemePath, "theme", "the existing visual asset is required to preserve its GUID");
            var packing = ReadNativePacking(source);

            var images = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            Texture2D atlas = null;
            try
            {
                foreach (var sprite in source.sprites)
                {
                    string path = ResolveSourcePath(sprite.source);
                    if (images.ContainsKey(path)) continue;
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    images.Add(path, texture);
                    if (!texture.LoadImage(File.ReadAllBytes(path), false))
                        throw Error(SourcePath, sprite.id, "cannot decode source PNG " + sprite.source);
                }
                ValidateSource(source, path => {
                    var image = images[ResolveSourcePath(path)];
                    return new Vector2Int(image.width, image.height);
                });
                var imagePixels = images.ToDictionary(pair => pair.Key, pair => pair.Value.GetPixels32(), StringComparer.OrdinalIgnoreCase);
                var atlasPixels = new Color32[packing.atlas * packing.Height];
                for (int i = 0; i < source.sprites.Length; i++)
                {
                    var sprite = source.sprites[i]; var rect = packing.rectangles[i]; string path = ResolveSourcePath(sprite.source);
                    // Integer row copies preserve every original RGBA byte, including
                    // transparent pixels. The cleared array supplies all four-pixel gutters.
                    var pixels = imagePixels[path]; int imageWidth = images[path].width;
                    for (int y = 0; y < sprite.height; y++)
                        Array.Copy(pixels, (sprite.y + y) * imageWidth + sprite.x,
                            atlasPixels, (rect.y + y) * packing.atlas + rect.x, sprite.width);
                }
                var pen = packing.rectangles[packing.rectangles.Length - 1];
                for (int y = 0; y < 9; y++) for (int x = 0; x < 9; x++)
                    atlasPixels[(pen.y + y) * packing.atlas + pen.x + x] = new Color32(255, 255, 255, 255);
                atlas = new Texture2D(packing.atlas, packing.Height, TextureFormat.RGBA32, false);
                atlas.SetPixels32(atlasPixels);
                atlas.Apply(false, false);

                var entries = new List<PackedSprite>();
                for (int i = 0; i < source.sprites.Length; i++)
                {
                    var rect = packing.rectangles[i]; var sprite = source.sprites[i];
                    entries.Add(new PackedSprite {
                        id = sprite.id, x = rect.x, y = rect.y,
                        width = rect.width, height = rect.height, bounds = sprite.bounds
                    });
                }
                foreach (var alias in source.aliases) entries.Add(new PackedSprite { id = alias.id, alias = alias.alias });
                string json = JsonUtility.ToJson(new RuntimeManifest {
                    revision = source.revision, whitePixel = new Pixel {
                        x = pen.x + 4, y = pen.y + 4 }, sprites = entries.ToArray()
                }, true);
                NotebookSpriteCatalog.Validate(json, atlas.width, atlas.height);
                byte[] png = atlas.EncodeToPNG();
                if (png == null || png.Length == 0) throw Error(SourcePath, "atlas", "PNG encoding failed");

                // No project asset is touched until the detached packed atlas and every binding pass.
                Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
                PublishResources(new[] { AtlasPath, CatalogPath, MountsPath }, new[] {
                    png, new UTF8Encoding(false).GetBytes(json), new UTF8Encoding(false).GetBytes(mountsJson) });
                AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureAtlasImporter();
                AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(MountsPath, ImportAssetOptions.ForceSynchronousImport);
                theme.ReplaceData(animation);
                EditorUtility.SetDirty(theme);
                // Save only this visual asset; unrelated dirty presets must remain untouched.
                AssetDatabase.SaveAssetIfDirty(theme);
                NotebookVisualThemeAsset.ResetDefaultCache();
                NotebookSpriteCatalog.ResetCache();
                NotebookMountLayout.ResetCache();
                AssemblyVisualComposer.ResetCache();
                string summary = $"Blue pen atlas rebuilt: {source.sprites.Length} sprites, {source.aliases.Length} aliases, " +
                    $"{animation.Shapes.Count} shape bindings, {animation.Clips.Count} clips, {animation.Recoils.Count} recoils; {atlas.width}x{atlas.height}.";
                Debug.Log(summary);
                return summary;
            }
            finally
            {
                foreach (var image in images.Values) UnityEngine.Object.DestroyImmediate(image);
                if (atlas != null) UnityEngine.Object.DestroyImmediate(atlas);
            }
        }

        static NativePacking ReadNativePacking(SourceManifest source)
        {
            FormatException Stale(string reason) => new FormatException(PackingPath + ": " + reason
                + ". Regenerate the native layout with: node Tools/ArtAssembly13/pack-catalog.mjs 0");
            if (!File.Exists(PackingPath)) throw Stale("missing committed packing layout");
            NativePacking layout;
            try { layout = JsonUtility.FromJson<NativePacking>(File.ReadAllText(PackingPath)); }
            catch (Exception error) { throw Stale("invalid layout JSON: " + error.Message); }
            if (layout == null || layout.atlas != 4096 || (layout.Height != 4096 && layout.Height != 4352)
                || layout.gutter != 4 || layout.rectangles == null
                || layout.rectangles.Length != source.sprites.Length + 1) throw Stale("stale atlas size, gutter or native crop count");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < layout.rectangles.Length; i++)
            {
                var rect = layout.rectangles[i]; bool pen = i == source.sprites.Length;
                string id = pen ? "$pen-white" : source.sprites[i].id;
                int width = pen ? 9 : source.sprites[i].width, height = pen ? 9 : source.sprites[i].height;
                if (rect == null || rect.index != i || rect.id != id || !ids.Add(rect.id)
                    || rect.width != width || rect.height != height) throw Stale("native crop ID, order or dimensions changed at " + id);
                if (rect.x < 0 || rect.y < 0 || rect.width <= 0 || rect.height <= 0
                    || (long)rect.x + rect.width > layout.atlas || (long)rect.y + rect.height > layout.Height)
                    throw Stale("native rectangle extends outside the declared atlas at " + id);
                for (int j = 0; j < i; j++)
                {
                    var other = layout.rectangles[j];
                    int dx = Math.Max(rect.x - other.x - other.width, other.x - rect.x - rect.width);
                    int dy = Math.Max(rect.y - other.y - other.height, other.y - rect.y - rect.height);
                    if (Math.Max(dx, dy) < layout.gutter) throw Stale("overlap or missing four-pixel gutter at " + id + " / " + other.id);
                }
            }
            return layout;
        }

        // Unity may memory-map loaded TextAssets on Windows. Truncating those paths
        // fails with Win32 1224; replacing a complete staged file preserves the old
        // mapping until Unity imports the new resource. Staging also keeps a failed
        // publication from leaving an atlas paired with another catalogue revision.
        static void PublishResources(string[] targets, byte[][] bytes)
        {
            string staging = Path.GetFullPath(Path.Combine("Temp", "PaperDefenseArtImport", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(staging);
            var staged = new string[targets.Length]; var backups = new string[targets.Length];
            var existed = new bool[targets.Length]; bool preserveRecoveryFiles = false;
            try
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    staged[i] = Path.Combine(staging, i + ".new"); backups[i] = Path.Combine(staging, i + ".previous");
                    File.WriteAllBytes(staged[i], bytes[i]); existed[i] = File.Exists(targets[i]);
                    if (existed[i]) File.Copy(targets[i], backups[i]);
                }
                try
                {
                    for (int i = 0; i < targets.Length; i++)
                    {
                        if (existed[i]) FileUtil.ReplaceFile(staged[i], Path.GetFullPath(targets[i]));
                        else File.Move(staged[i], targets[i]);
                    }
                }
                catch (Exception publicationError)
                {
                    var errors = new List<Exception> { publicationError };
                    for (int i = 0; i < targets.Length; i++)
                    {
                        try
                        {
                            if (existed[i]) FileUtil.ReplaceFile(backups[i], Path.GetFullPath(targets[i]));
                            else if (File.Exists(targets[i])) File.Delete(targets[i]);
                        }
                        catch (Exception rollbackError) { errors.Add(rollbackError); }
                    }
                    preserveRecoveryFiles = errors.Count > 1;
                    if (preserveRecoveryFiles) throw new AggregateException("Art publication failed; recovery files retained at " + staging, errors);
                    throw;
                }
            }
            finally
            {
                if (!preserveRecoveryFiles)
                {
                    foreach (string file in staged.Concat(backups)) if (file != null && File.Exists(file)) File.Delete(file);
                    if (Directory.Exists(staging)) Directory.Delete(staging, false);
                }
            }
        }

        static void ConfigureAtlasImporter()
        {
            var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            if (importer == null) throw Error(AtlasPath, "importer", "missing texture importer");
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 8192;
            // Packed native crops have only four pixels of gutter. Full-atlas mipmaps
            // would mix neighbouring illustrations at small sizes; the notebook shaders
            // instead average four samples from mip zero inside a bounded footprint.
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipMapBias = 0;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        public static SourceManifest ParseSource(string json, string path = SourcePath)
        {
            SourceManifest source;
            try { source = JsonUtility.FromJson<SourceManifest>(json); }
            catch (Exception error) { throw Error(path, "json", error.Message); }
            ValidateSource(source, sourcePath: path);
            return source;
        }

        public static void ValidateSource(SourceManifest source, Func<string, Vector2Int> imageSize = null, string sourcePath = SourcePath)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.revision) || source.sprites == null || source.sprites.Length == 0 || source.aliases == null)
                throw Error(sourcePath, "manifest", "revision, sprites and aliases are required");
            var definitions = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var sprite in source.sprites)
            {
                if (sprite == null || string.IsNullOrWhiteSpace(sprite.id) || definitions.ContainsKey(sprite.id))
                    throw Error(sourcePath, sprite?.id, "missing or duplicate sprite ID");
                definitions.Add(sprite.id, null);
                ResolveSourcePath(sprite.source);
                if (sprite.x < 0 || sprite.y < 0 || sprite.width <= 0 || sprite.height <= 0)
                    throw Error(sourcePath, sprite.id, "crop must have a nonnegative origin and positive size");
                var bounds = sprite.bounds;
                if (bounds == null || !Finite(bounds.x) || !Finite(bounds.y) || !Finite(bounds.width) || !Finite(bounds.height) || bounds.width <= 0 || bounds.height <= 0)
                    throw Error(sourcePath, sprite.id, "finite positive local bounds are required");
                if (imageSize == null) continue;
                var size = imageSize(sprite.source);
                if (size.x <= 0 || size.y <= 0 || (long)sprite.x + sprite.width > size.x || (long)sprite.y + sprite.height > size.y)
                    throw Error(sourcePath, sprite.id, "crop extends outside its source PNG");
            }
            foreach (var alias in source.aliases)
            {
                if (alias == null || string.IsNullOrWhiteSpace(alias.id) || string.IsNullOrWhiteSpace(alias.alias) || definitions.ContainsKey(alias.id))
                    throw Error(sourcePath, alias?.id, "missing or duplicate alias ID or target");
                definitions.Add(alias.id, alias.alias);
            }
            foreach (var id in definitions.Keys)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal);
                string current = id;
                while (current != null)
                {
                    if (!visited.Add(current)) throw Error(sourcePath, id, "alias cycle");
                    if (!definitions.TryGetValue(current, out var target)) throw Error(sourcePath, id, "unknown alias target " + current);
                    current = target;
                }
            }
        }

        public static NotebookArtSource ParseAnimation(string json, ISet<string> spriteIds, string path = AnimationPath)
        {
            NotebookArtSource animation;
            try { animation = JsonUtility.FromJson<NotebookArtSource>(json); }
            catch (Exception error) { throw Error(path, "json", error.Message); }
            ValidateAnimation(animation, spriteIds, path);
            return animation;
        }

        public static void ValidateAnimation(NotebookArtSource animation, ISet<string> spriteIds, string path = AnimationPath)
        {
            if (animation == null || animation.SchemaVersion != 1 || string.IsNullOrWhiteSpace(animation.Revision) || spriteIds == null)
                throw Error(path, "animation", "schema 1, revision and sprite bindings are required");
            if (!Finite(animation.MaximumTerminalTailSeconds) || animation.MaximumTerminalTailSeconds <= 0 || animation.MaximumTerminalTailSeconds > 5 ||
                animation.MaximumEffects < 1 || animation.MaximumEffects > 4096 || animation.MaximumDeaths < 1 || animation.MaximumDeaths > 4096)
                throw Error(path, "budgets", "invalid bounded animation budgets");
            if (animation.Palette == null || animation.Palette.Length != Enum.GetValues(typeof(NotebookInkChannel)).Length ||
                animation.Palette.Any(c => !Finite(c.r) || !Finite(c.g) || !Finite(c.b) || !Finite(c.a)))
                throw Error(path, "palette", "all finite ink channels are required");
            if (animation.Shapes == null || animation.Shapes.Count == 0 || animation.Clips == null || animation.Recoils == null)
                throw Error(path, "catalog", "shape, clip and recoil collections are required");
            var shapes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var shape in animation.Shapes)
            {
                if (shape == null || string.IsNullOrWhiteSpace(shape.Id) || !shapes.Add(shape.Id) || !spriteIds.Contains(shape.Id))
                    throw Error(path, shape?.Id, "missing, duplicate or unbound animation shape");
                if (shape.Marks != null && shape.Marks.Count != 0)
                    throw Error(path, shape.Id, "legacy decorative marks are forbidden in the sprite animation source");
            }
            var clips = new HashSet<string>(StringComparer.Ordinal);
            foreach (var clip in animation.Clips)
            {
                if (clip == null || string.IsNullOrWhiteSpace(clip.Id) || !clips.Add(clip.Id) || !Finite(clip.Duration) || clip.Duration <= 0 ||
                    clip.Duration > animation.MaximumTerminalTailSeconds || clip.Frames == null || clip.Frames.Count == 0)
                    throw Error(path, clip?.Id, "invalid or duplicate animation clip");
                float previous = -1;
                foreach (var frame in clip.Frames)
                {
                    if (frame == null || !shapes.Contains(frame.ShapeId ?? "") || !Finite(frame.Time) || frame.Time < 0 || frame.Time > clip.Duration || frame.Time <= previous ||
                        !Finite(frame.Scale) || frame.Scale < 0 || frame.Scale > 4 || !Finite(frame.Rotation) || !Finite(frame.Alpha) || frame.Alpha < 0 || frame.Alpha > 1 || !Finite(frame.Offset))
                        throw Error(path, clip.Id, "invalid frame shape, time or transform");
                    previous = frame.Time;
                }
                if (clip.Frames[0].Time != 0) throw Error(path, clip.Id, "first animation frame must start at zero");
            }
            var recoils = new HashSet<string>(StringComparer.Ordinal);
            foreach (var recoil in animation.Recoils)
            {
                if (recoil == null || !recoils.Add(recoil.WeaponId ?? "") || !shapes.Contains(recoil.WeaponId ?? "") || !shapes.Contains(recoil.EffectId ?? "") ||
                    !Finite(recoil.Duration) || recoil.Duration <= 0 || !Finite(recoil.Distance) || recoil.Distance < 0 || recoil.Distance > .3f || !Finite(recoil.Muzzle) || recoil.Keys == null || recoil.Keys.Count < 2)
                    throw Error(path, recoil?.WeaponId, "invalid recoil binding or curve");
                float previous = -1;
                foreach (var key in recoil.Keys)
                {
                    if (key == null || !Finite(key.Time) || key.Time < 0 || key.Time <= previous || key.Time > recoil.Duration || !Finite(key.Value) || key.Value < 0 || key.Value > 1)
                        throw Error(path, recoil.WeaponId, "invalid recoil key");
                    previous = key.Time;
                }
                var first = recoil.Keys[0]; var last = recoil.Keys[recoil.Keys.Count - 1];
                if (first.Time != 0 || first.Value != 0 || last.Time != recoil.Duration || last.Value != 0)
                    throw Error(path, recoil.WeaponId, "recoil curve must start and finish at rest");
            }
        }

        static HashSet<string> SemanticIds(SourceManifest source)
            => new HashSet<string>(source.sprites.Select(sprite => sprite.id).Concat(source.aliases.Select(alias => alias.id)), StringComparer.Ordinal);

        static string ResolveSourcePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
                throw Error(SourcePath, path, "source must name a PNG inside the project");
            string project = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            string absolute = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(project, path));
            if (!absolute.StartsWith(project.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw Error(SourcePath, path, "source PNG must remain inside the project");
            return absolute;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        static ArgumentException Error(string path, string id, string message) => new ArgumentException(path + " [" + (id ?? "?") + "]: " + message);
    }
}
