using System;
using System.Collections.Generic;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Display choices are staged until Apply; changing pages never changes the monitor.</summary>
    public sealed class PlayerDisplaySettings
    {
        static readonly Vector2Int[] fallback = { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) };
        readonly List<Vector2Int> sizes = new List<Vector2Int>();
        readonly HashSet<Vector2Int> nativeSizes = new HashSet<Vector2Int>();
        readonly Action<int, int, FullScreenMode> apply;
        readonly bool editorPreview;
        public IReadOnlyList<Vector2Int> Resolutions { get; }
        public Vector2Int CurrentResolution { get; private set; }
        public int SelectedIndex { get; private set; }
        public Vector2Int SelectedResolution => sizes[SelectedIndex];
        public bool HasPendingChange => SelectedResolution != CurrentResolution;

        public PlayerDisplaySettings(IEnumerable<Vector2Int> available, Vector2Int current, Action<int, int, FullScreenMode> apply)
            : this(available, current, apply, false) { }

        PlayerDisplaySettings(IEnumerable<Vector2Int> available, Vector2Int current, Action<int, int, FullScreenMode> apply, bool editorPreview)
        {
            this.apply = apply ?? throw new ArgumentNullException(nameof(apply));
            this.editorPreview = editorPreview;
            if (available != null)
                foreach (var size in available)
                    if (size.x > 0 && size.y > 0) { nativeSizes.Add(size); Add(size); }
            if (sizes.Count == 0) foreach (var size in fallback) Add(size);
            Resolutions = sizes.AsReadOnly();
            RefreshCurrentSize(current.x, current.y);
        }

        public static PlayerDisplaySettings FromScreen()
        {
            var available = new List<Vector2Int>();
            foreach (var mode in Screen.resolutions) available.Add(new Vector2Int(mode.width, mode.height));
            // The Editor is an embedded view, so common window sizes remain useful even if the
            // monitor advertises only its native fullscreen mode.
            bool preview = UnityEngine.Application.isEditor;
            var display = new PlayerDisplaySettings(available, new Vector2Int(Screen.width, Screen.height),
                (width, height, mode) => Screen.SetResolution(width, height, mode), preview);
            if (preview)
            {
                foreach (var size in fallback) display.Add(size);
                display.RefreshCurrentSize(Screen.width, Screen.height);
            }
            return display;
        }

        void Add(Vector2Int size)
        { if (size.x > 0 && size.y > 0 && !sizes.Contains(size)) sizes.Add(size); }

        public void RefreshCurrentSize(int width, int height)
        {
            bool keepPending = CurrentResolution.x > 0 && CurrentResolution.y > 0 && HasPendingChange;
            Vector2Int pending = keepPending ? SelectedResolution : default;
            CurrentResolution = width > 0 && height > 0 ? new Vector2Int(width, height) : fallback[0];
            Add(CurrentResolution);
            sizes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            SelectedIndex = sizes.IndexOf(keepPending ? pending : CurrentResolution);
        }

        public void SelectRelative(int direction)
        {
            int next = (SelectedIndex + direction) % sizes.Count;
            SelectedIndex = next < 0 ? next + sizes.Count : next;
        }

        public void CancelSelection() => SelectedIndex = sizes.IndexOf(CurrentResolution);

        public bool CanApply(FullScreenMode mode) => HasPendingChange &&
            (editorPreview || mode != FullScreenMode.ExclusiveFullScreen || nativeSizes.Contains(SelectedResolution));

        public bool Apply(FullScreenMode mode)
        {
            if (!CanApply(mode)) return false;
            var selected = SelectedResolution;
            apply(selected.x, selected.y, mode);
            CurrentResolution = selected;
            return true;
        }
    }
}
