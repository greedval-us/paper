using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("PaperDefense.Editor.Tests")]

namespace PaperDefense.Presentation
{
    /// <summary>One blue-pen atlas shared by the world batches and notebook illustrations.</summary>
    public static class NotebookSpriteCatalog
    {
        [Serializable] sealed class Pixel { public int x, y; }
        [Serializable] sealed class Bounds
        {
            public float x, y, width, height;
            public Rect Rect => new Rect(x, y, width, height);
        }
        [Serializable] sealed class Entry
        {
            public string id, alias;
            public int x, y, width, height;
            public Bounds bounds;
        }
        [Serializable] sealed class Source
        {
            public string revision;
            public Pixel whitePixel;
            public Entry[] sprites;
        }

        static Texture2D texture;
        static Vector2 whiteUV;
        static bool loaded;
        static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        public static Texture2D Texture { get { EnsureLoaded(); return texture != null ? texture : Texture2D.whiteTexture; } }
        public static Vector2 WhiteUV { get { EnsureLoaded(); return texture != null ? whiteUV : new Vector2(.5f, .5f); } }
        public static bool Available { get { EnsureLoaded(); return texture != null && entries.Count > 0; } }
        public static int Count { get { EnsureLoaded(); return entries.Count; } }

        public static bool TryGet(string id, out Sprite sprite)
        {
            EnsureLoaded();
            sprite = null;
            if (id == null || texture == null || !entries.TryGetValue(id, out var entry)) return false;
            if (sprites.TryGetValue(entry.id, out sprite) && sprite != null) return true;
            Rect bounds = entry.bounds.Rect;
            sprite = Sprite.Create(texture, new Rect(entry.x, entry.y, entry.width, entry.height),
                new Vector2(-bounds.x / bounds.width, -bounds.y / bounds.height),
                entry.width / bounds.width, 0, SpriteMeshType.FullRect);
            sprite.name = entry.id;
            sprite.hideFlags = HideFlags.DontSave;
            sprites[entry.id] = sprite;
            return true;
        }

        public static bool TryGetBounds(string id, out Rect bounds)
        {
            EnsureLoaded();
            bounds = default;
            if (id == null || !entries.TryGetValue(id, out var entry)) return false;
            bounds = entry.bounds.Rect;
            return true;
        }

        internal static bool TryDraw(MeshPen pen, string id, Vector2 pivot, float scale = 1,
            float radians = 0, float opacity = 1, Vector2? localScale = null, Color? tint = null)
        {
            EnsureLoaded();
            if (pen == null || texture == null || id == null || !entries.TryGetValue(id, out var entry)) return false;
            Rect uv = UV(entry);
            Rect bounds = entry.bounds.Rect;
            Vector2 stretch = localScale ?? Vector2.one;
            float cosine = Mathf.Cos(radians), sine = Mathf.Sin(radians);
            var color = tint ?? Color.white;
            color.a *= opacity;
            pen.TexturedQuad(
                Transform(new Vector2(bounds.xMin, bounds.yMin), pivot, scale, stretch, cosine, sine),
                Transform(new Vector2(bounds.xMax, bounds.yMin), pivot, scale, stretch, cosine, sine),
                Transform(new Vector2(bounds.xMax, bounds.yMax), pivot, scale, stretch, cosine, sine),
                Transform(new Vector2(bounds.xMin, bounds.yMax), pivot, scale, stretch, cosine, sine), uv, color);
            return true;
        }

        public static bool TryDrawUI(VertexHelper target, string id, Vector2 pivot, float scale = 1,
            float radians = 0, float opacity = 1, Vector2? localScale = null, Color? tint = null)
        {
            EnsureLoaded();
            if (target == null || texture == null || id == null || !entries.TryGetValue(id, out var entry)) return false;
            Rect bounds = entry.bounds.Rect, uv = UV(entry);
            Vector2 stretch = localScale ?? Vector2.one;
            float cosine = Mathf.Cos(radians), sine = Mathf.Sin(radians);
            var color = tint ?? Color.white;
            color.a *= opacity;
            int first = target.currentVertCount;
            target.AddVert(Transform(new Vector2(bounds.xMin, bounds.yMin), pivot, scale, stretch, cosine, sine), color, new Vector2(uv.xMin, uv.yMin));
            target.AddVert(Transform(new Vector2(bounds.xMax, bounds.yMin), pivot, scale, stretch, cosine, sine), color, new Vector2(uv.xMax, uv.yMin));
            target.AddVert(Transform(new Vector2(bounds.xMax, bounds.yMax), pivot, scale, stretch, cosine, sine), color, new Vector2(uv.xMax, uv.yMax));
            target.AddVert(Transform(new Vector2(bounds.xMin, bounds.yMax), pivot, scale, stretch, cosine, sine), color, new Vector2(uv.xMin, uv.yMax));
            target.AddTriangle(first, first + 1, first + 2);
            target.AddTriangle(first, first + 2, first + 3);
            return true;
        }

        static Rect UV(Entry entry) => new Rect((float)entry.x / texture.width, (float)entry.y / texture.height,
            (float)entry.width / texture.width, (float)entry.height / texture.height);

        static Vector2 Transform(Vector2 point, Vector2 pivot, float scale, Vector2 stretch, float cosine, float sine)
        {
            point = Vector2.Scale(point, stretch) * scale;
            return pivot + new Vector2(cosine * point.x - sine * point.y, sine * point.x + cosine * point.y);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlaySession() => ResetCache();

        public static void ResetCache()
        {
            foreach (var sprite in sprites.Values)
                if (sprite != null)
                {
                    if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(sprite);
                    else UnityEngine.Object.DestroyImmediate(sprite);
                }
            sprites.Clear(); entries.Clear(); texture = null; loaded = false;
        }

        /// <summary>Reject broken aliases, clipped atlas rectangles and invalid local transforms before import.</summary>
        public static void Validate(string json, int atlasWidth, int atlasHeight)
            => Read(json, atlasWidth, atlasHeight, out _);

        static void EnsureLoaded()
        {
            if (loaded)
            {
                if (texture != null || entries.Count == 0) return;
                // Fast Enter Play and resource unloading can destroy Unity's native object while
                // static managed dictionaries survive. Reload every binding against a live atlas.
                ResetCache();
            }
            loaded = true;
            texture = Resources.Load<Texture2D>("Art/NotebookAtlas");
            var source = Resources.Load<TextAsset>("Art/NotebookSprites");
            if (texture == null || source == null) { texture = null; return; }
            var resolved = Read(source.text, texture.width, texture.height, out var white);
            whiteUV = new Vector2((white.x + .5f) / texture.width, (white.y + .5f) / texture.height);
            foreach (var entry in resolved) entries.Add(entry.Key, entry.Value);
        }

        static Dictionary<string, Entry> Read(string json, int atlasWidth, int atlasHeight, out Pixel white)
        {
            Source source;
            try { source = JsonUtility.FromJson<Source>(json); }
            catch (Exception exception) { throw new FormatException("Notebook sprites: invalid JSON.", exception); }
            if (source == null || string.IsNullOrWhiteSpace(source.revision) || source.sprites == null || source.sprites.Length == 0)
                throw new FormatException("Notebook sprites: revision and semantic sprite entries are required.");
            white = source.whitePixel;
            if (atlasWidth < 1 || atlasHeight < 1 || white == null || white.x < 0 || white.y < 0 || white.x >= atlasWidth || white.y >= atlasHeight)
                throw new FormatException("Notebook sprites: whitePixel must be inside the atlas.");
            var definitions = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in source.sprites)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || definitions.ContainsKey(entry.id))
                    throw new FormatException("Notebook sprites: missing or duplicate ID " + entry?.id + ".");
                definitions.Add(entry.id, entry);
                if (!string.IsNullOrEmpty(entry.alias)) continue;
                if (entry.width <= 0 || entry.height <= 0 || entry.x < 0 || entry.y < 0 || (long)entry.x + entry.width > atlasWidth || (long)entry.y + entry.height > atlasHeight)
                    throw new FormatException("Notebook sprites: atlas rectangle is clipped for " + entry.id + ".");
                var bounds = entry.bounds;
                if (bounds == null || !Finite(bounds.x) || !Finite(bounds.y) || !Finite(bounds.width) || !Finite(bounds.height) || bounds.width <= 0 || bounds.height <= 0)
                    throw new FormatException("Notebook sprites: finite positive local bounds are required for " + entry.id + ".");
            }
            var resolved = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var id in definitions.Keys) Resolve(id, definitions, resolved, new HashSet<string>(StringComparer.Ordinal));
            return resolved;
        }

        static Entry Resolve(string id, Dictionary<string, Entry> definitions, Dictionary<string, Entry> resolved, HashSet<string> visiting)
        {
            if (resolved.TryGetValue(id, out var known)) return known;
            if (!definitions.TryGetValue(id, out var entry)) throw new FormatException("Notebook sprites: alias target " + id + " is missing.");
            if (!visiting.Add(id)) throw new FormatException("Notebook sprites: alias cycle at " + id + ".");
            var target = string.IsNullOrEmpty(entry.alias) ? entry : Resolve(entry.alias, definitions, resolved, visiting);
            visiting.Remove(id); resolved.Add(id, target);
            return target;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
