using System;
using System.Collections.Generic;
using UnityEngine;

namespace PaperDefense.Presentation
{
    public enum NotebookArtPrimitive { Polyline, Polygon, Circle, Ring }
    public enum NotebookInkChannel { Navy, Pencil, Paper, Red, Blue, Green, Wash, White, Ivory, Sketch, Surface, Amber, Danger, Clean, NotebookPaper, Body, GlassInk, Copper, BladeSteel, Violet }

    [Serializable] public sealed class NotebookArtMark
    {
        public string Name;
        public NotebookArtPrimitive Primitive;
        public NotebookInkChannel Channel;
        public float Width = .025f, Alpha = 1;
        public bool Reference;
        public Vector2[] Points = Array.Empty<Vector2>();
        public Vector2 Center;
        public float Radius, Aspect = 1;
        public int Segments = 24;
    }
    [Serializable] public sealed class NotebookArtShape
    {
        public string Id;
        public string TemplateId;
        public int ReferenceRevision = 3;
        public List<NotebookArtMark> Marks = new List<NotebookArtMark>();
    }
    [Serializable] public sealed class NotebookArtFrame
    {
        public float Time;
        public string ShapeId;
        public Vector2 Offset;
        public float Rotation, Scale = 1, Alpha = 1;
    }
    [Serializable] public sealed class NotebookArtClip
    {
        public string Id;
        public float Duration;
        public bool Loop;
        public List<NotebookArtFrame> Frames = new List<NotebookArtFrame>();
        public NotebookArtFrame Sample(float elapsed)
        {
            if (Frames == null || Frames.Count == 0) return null;
            float time = Loop && Duration > 0 ? Mathf.Repeat(Mathf.Max(0, elapsed), Duration) : Mathf.Clamp(elapsed, 0, Duration);
            for (int i = Frames.Count - 1; i >= 0; i--) if (time >= Frames[i].Time) return Frames[i];
            return Frames[0];
        }
        internal void HoldFirstPose()
        {
            if (Frames.Count == 0) return;
            string still = Frames[0].ShapeId;
            foreach (var frame in Frames)
            {
                frame.ShapeId = still;
                frame.Offset = Vector2.zero;
                frame.Rotation = 0;
                frame.Scale = 1;
            }
        }
    }
    [Serializable] public sealed class NotebookRecoilKey
    {
        public float Time, Value;
    }
    [Serializable] public sealed class NotebookWeaponRecoil
    {
        public string WeaponId, EffectId;
        public float Duration, Distance;
        public Vector2 Muzzle;
        public List<NotebookRecoilKey> Keys = new List<NotebookRecoilKey>();
        public float Sample(float elapsed)
        {
            if (elapsed < 0 || elapsed >= Duration || Keys == null || Keys.Count == 0) return 0;
            for (int i = 1; i < Keys.Count; i++)
                if (elapsed <= Keys[i].Time)
                    return -Distance * Mathf.Lerp(Keys[i - 1].Value, Keys[i].Value,
                        Mathf.InverseLerp(Keys[i - 1].Time, Keys[i].Time, elapsed));
            return -Distance * Keys[Keys.Count - 1].Value;
        }
    }
    [Serializable] public sealed class NotebookArtSource
    {
        public int SchemaVersion = 1;
        public string Revision;
        public bool ReducedMotion;
        // An explicit semantic policy survives runtime accessibility copies.
        // Caller-authored themes retain their own colors unless they opt in.
        public bool NightInkOnDarkSurfaces;
        public float MaximumTerminalTailSeconds = 1.2f;
        public int MaximumEffects = 256, MaximumDeaths = 128;
        public Color[] Palette = Array.Empty<Color>();
        public List<NotebookArtShape> Shapes = new List<NotebookArtShape>();
        public List<NotebookArtClip> Clips = new List<NotebookArtClip>();
        public List<NotebookWeaponRecoil> Recoils = new List<NotebookWeaponRecoil>();
    }

    /// <summary>Code-only visual contract shared by standard drawings, animation and caller-owned themes.</summary>
    [CreateAssetMenu(menuName = "Paper Defense/Notebook Visual Theme")]
    public sealed class NotebookVisualThemeAsset : ScriptableObject
    {
        public NotebookArtSource Data = new NotebookArtSource();
        Dictionary<string, NotebookArtShape> shapes;
        Dictionary<string, NotebookArtShape> compactOverrides;
        Dictionary<string, NotebookArtClip> clips;
        Dictionary<string, NotebookWeaponRecoil> recoils;
        static NotebookVisualThemeAsset defaultTheme;
        public static NotebookVisualThemeAsset Default => defaultTheme != null ? defaultTheme : defaultTheme = InkSwarmArt.CreateTheme();
        static NotebookVisualThemeAsset()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseBeforeAssemblyReload;
#endif
        }
        public static void ResetDefaultCache()
        {
            var owned = defaultTheme;
            defaultTheme = null;
            NotebookSpriteCatalog.ResetCache();
            if (owned == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(owned); else DestroyImmediate(owned);
        }
#if UNITY_EDITOR
        static void ReleaseBeforeAssemblyReload()
        {
            // Reload discards managed statics before a deferred Destroy can run.
            var owned = defaultTheme;
            defaultTheme = null;
            NotebookSpriteCatalog.ResetCache();
            if (owned != null) DestroyImmediate(owned);
        }
#endif
        public float MaximumTerminalTailSeconds => Data.MaximumTerminalTailSeconds;
        public int MaximumEffects => Data.MaximumEffects;
        public int MaximumDeaths => Data.MaximumDeaths;
        internal bool UsesNativeCrowdGeometry => Data.Revision == InkSwarmArt.Revision &&
            Data.Palette?.Length > (int)NotebookInkChannel.Violet && Data.Shapes.Count == 0;

        internal NotebookArtShape FindCompactEnemy(string id)
        {
            if (Data.Revision != InkSwarmArt.Revision || id == null) return null;
            if (UsesNativeCrowdGeometry) return InkSwarmEnemyArt.CreateCompact(id);
            Index();
            if (compactOverrides == null) compactOverrides = new Dictionary<string, NotebookArtShape>(StringComparer.Ordinal);
            if (compactOverrides.TryGetValue(id, out var found)) return found;
            string compactId = id + ".compact";
            foreach (var authored in Data.Shapes)
            {
                if (authored.Id == compactId) { found = authored; break; }
                if (authored.Id == id) found = authored;
            }
            found = found ?? FindShape(compactId);
            compactOverrides.Add(id, found);
            return found;
        }
        public NotebookArtShape FindShape(string id)
        {
            Index();
            if (id == null) return null;
            if (shapes.TryGetValue(id, out var value)) return value;
            value = StandardNotebookArt.Shape(id);
            int paletteSize = Data.Palette?.Length ?? 0;
            if (value != null) value = NotebookThemeDefaults.ForPalette(value, paletteSize);
            else
            {
                value = InkSwarmArt.Shape(id);
                // The active battle cache owns its compact poses. Preserve that identity;
                // only caller-owned/short palettes need an isolated remapped copy.
                if (Data.Revision != InkSwarmArt.Revision || paletteSize <= (int)NotebookInkChannel.Violet)
                    value = NotebookThemeDefaults.ForPalette(value, paletteSize);
            }
            if (value != null) { value.Id = id; shapes.Add(id, value); }
            return value;
        }
        public NotebookArtClip FindClip(string id)
        {
            Index();
            if (id == null) return null;
            if (clips.TryGetValue(id, out var clip)) return clip;
            if (!(id.StartsWith("enemy.undead.", StringComparison.Ordinal) || id.StartsWith("enemy.clockwork.", StringComparison.Ordinal) ||
                id.StartsWith("enemy.boss.", StringComparison.Ordinal)) ||
                this == Default) return null;
            clip = Default.FindClip(id);
            if (clip == null) return null;
            if (Data.ReducedMotion)
            {
                // Theme-owned copy retains event duration and death fades for older saved palettes.
                clip = JsonUtility.FromJson<NotebookArtClip>(JsonUtility.ToJson(clip));
                clip.HoldFirstPose();
            }
            clips.Add(id, clip);
            return clip;
        }
        public NotebookWeaponRecoil FindRecoil(string weaponId) { Index(); return weaponId != null && recoils.TryGetValue(weaponId, out var value) ? value : null; }
        public Color Color(NotebookInkChannel channel)
            => Data.Palette != null && (int)channel < Data.Palette.Length ? Data.Palette[(int)channel] : UnityEngine.Color.white;
        public void ReplaceData(NotebookArtSource value)
        {
            Data = value ?? throw new ArgumentNullException(nameof(value));
            shapes = null; clips = null; recoils = null; compactOverrides = null;
            if (ReferenceEquals(this, defaultTheme)) NotebookSpriteCatalog.ResetCache();
        }
        void OnEnable() { shapes = null; clips = null; recoils = null; compactOverrides = null; }
        void Index()
        {
            if (shapes != null) return;
            shapes = new Dictionary<string, NotebookArtShape>(StringComparer.Ordinal);
            clips = new Dictionary<string, NotebookArtClip>(StringComparer.Ordinal);
            recoils = new Dictionary<string, NotebookWeaponRecoil>(StringComparer.Ordinal);
            foreach (var shape in Data.Shapes) shapes[shape.Id] = shape;
            foreach (var clip in Data.Clips) clips[clip.Id] = clip;
            foreach (var recoil in Data.Recoils) recoils[recoil.WeaponId] = recoil;
        }
    }
}
