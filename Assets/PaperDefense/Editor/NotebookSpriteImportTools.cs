using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public const string AtlasPath = "Assets/PaperDefense/Resources/Art/NotebookAtlas.png";
        public const string CatalogPath = "Assets/PaperDefense/Resources/Art/NotebookSprites.json";
        public const string ThemePath = "Assets/PaperDefense/Resources/Art/NotebookVisualTheme.asset";

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
            var theme = AssetDatabase.LoadAssetAtPath<NotebookVisualThemeAsset>(ThemePath);
            if (theme == null) throw Error(ThemePath, "theme", "the existing visual asset is required to preserve its GUID");

            var images = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            var crops = new List<Texture2D>();
            Texture2D packed = null, atlas = null;
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
                foreach (var sprite in source.sprites)
                {
                    var crop = new Texture2D(sprite.width, sprite.height, TextureFormat.RGBA32, false);
                    crops.Add(crop);
                    crop.SetPixels(images[ResolveSourcePath(sprite.source)].GetPixels(sprite.x, sprite.y, sprite.width, sprite.height));
                    crop.Apply(false, false);
                }
                packed = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Rect[] rectangles = packed.PackTextures(crops.ToArray(), 4, 4096, false);
                if (rectangles.Length != source.sprites.Length) throw Error(SourcePath, "atlas", "packing lost source sprites");
                atlas = new Texture2D(packed.width + 32, packed.height + 32, TextureFormat.RGBA32, false);
                atlas.SetPixels32(new Color32[atlas.width * atlas.height]);
                atlas.SetPixels32(16, 16, packed.width, packed.height, packed.GetPixels32());
                var white = new Color32[81];
                for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
                atlas.SetPixels32(0, 0, 9, 9, white);
                atlas.Apply(false, false);

                var entries = new List<PackedSprite>();
                for (int i = 0; i < rectangles.Length; i++)
                {
                    Rect rect = rectangles[i]; var sprite = source.sprites[i];
                    entries.Add(new PackedSprite {
                        id = sprite.id, x = 16 + Mathf.RoundToInt(rect.x * packed.width), y = 16 + Mathf.RoundToInt(rect.y * packed.height),
                        width = Mathf.RoundToInt(rect.width * packed.width), height = Mathf.RoundToInt(rect.height * packed.height), bounds = sprite.bounds
                    });
                }
                foreach (var alias in source.aliases) entries.Add(new PackedSprite { id = alias.id, alias = alias.alias });
                string json = JsonUtility.ToJson(new RuntimeManifest {
                    revision = source.revision, whitePixel = new Pixel { x = 4, y = 4 }, sprites = entries.ToArray()
                }, true);
                NotebookSpriteCatalog.Validate(json, atlas.width, atlas.height);
                byte[] png = atlas.EncodeToPNG();
                if (png == null || png.Length == 0) throw Error(SourcePath, "atlas", "PNG encoding failed");

                // No project asset is touched until the detached packed atlas and every binding pass.
                Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
                File.WriteAllBytes(AtlasPath, png);
                File.WriteAllText(CatalogPath, json);
                AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureAtlasImporter();
                AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceSynchronousImport);
                theme.ReplaceData(animation);
                EditorUtility.SetDirty(theme);
                // Save only this visual asset; unrelated dirty presets must remain untouched.
                AssetDatabase.SaveAssetIfDirty(theme);
                NotebookVisualThemeAsset.ResetDefaultCache();
                NotebookSpriteCatalog.ResetCache();
                string summary = $"Blue pen atlas rebuilt: {source.sprites.Length} sprites, {source.aliases.Length} aliases, " +
                    $"{animation.Shapes.Count} shape bindings, {animation.Clips.Count} clips, {animation.Recoils.Count} recoils; {atlas.width}x{atlas.height}.";
                Debug.Log(summary);
                return summary;
            }
            finally
            {
                foreach (var image in images.Values) UnityEngine.Object.DestroyImmediate(image);
                foreach (var crop in crops) UnityEngine.Object.DestroyImmediate(crop);
                if (packed != null) UnityEngine.Object.DestroyImmediate(packed);
                if (atlas != null) UnityEngine.Object.DestroyImmediate(atlas);
            }
        }

        static void ConfigureAtlasImporter()
        {
            var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            if (importer == null) throw Error(AtlasPath, "importer", "missing texture importer");
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 8192;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
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
