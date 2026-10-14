using System;
using System.Collections.Generic;
using UnityEngine;

namespace PaperDefense.Presentation
{
    public enum NotebookArtPrimitive { Polyline, Polygon, Circle, Ring }
    public enum NotebookInkChannel { Navy, Pencil, Paper, Red, Blue, Green, Wash, White }

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
        public float MaximumTerminalTailSeconds = 1.2f;
        public int MaximumEffects = 256, MaximumDeaths = 128;
        public Color[] Palette = Array.Empty<Color>();
        public List<NotebookArtShape> Shapes = new List<NotebookArtShape>();
        public List<NotebookArtClip> Clips = new List<NotebookArtClip>();
        public List<NotebookWeaponRecoil> Recoils = new List<NotebookWeaponRecoil>();
    }

    /// <summary>Shared, immutable-at-runtime visual data. Geometry and timing belong to the authored source.</summary>
    [CreateAssetMenu(menuName = "Paper Defense/Notebook Visual Theme")]
    public sealed class NotebookVisualThemeAsset : ScriptableObject
    {
        public NotebookArtSource Data = new NotebookArtSource();
        Dictionary<string, NotebookArtShape> shapes;
        Dictionary<string, NotebookArtClip> clips;
        Dictionary<string, NotebookWeaponRecoil> recoils;
        static NotebookVisualThemeAsset defaultTheme;
        public static NotebookVisualThemeAsset Default => defaultTheme != null ? defaultTheme : defaultTheme = Resources.Load<NotebookVisualThemeAsset>("Art/NotebookVisualTheme");
        public static void ResetDefaultCache() { defaultTheme = null; }
        public float MaximumTerminalTailSeconds => Data.MaximumTerminalTailSeconds;
        public int MaximumEffects => Data.MaximumEffects;
        public int MaximumDeaths => Data.MaximumDeaths;
        public NotebookArtShape FindShape(string id) { Index(); return id != null && shapes.TryGetValue(id, out var value) ? value : null; }
        public NotebookArtClip FindClip(string id) { Index(); return id != null && clips.TryGetValue(id, out var value) ? value : null; }
        public NotebookWeaponRecoil FindRecoil(string weaponId) { Index(); return weaponId != null && recoils.TryGetValue(weaponId, out var value) ? value : null; }
        public Color Color(NotebookInkChannel channel)
            => Data.Palette != null && (int)channel < Data.Palette.Length ? Data.Palette[(int)channel] : UnityEngine.Color.white;
        public void ReplaceData(NotebookArtSource value) { Data = value ?? throw new ArgumentNullException(nameof(value)); shapes = null; clips = null; recoils = null; }
        void OnEnable() { shapes = null; clips = null; recoils = null; }
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
