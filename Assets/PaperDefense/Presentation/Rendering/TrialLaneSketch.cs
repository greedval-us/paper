using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Trial playback uses simulation timestamps, the shared composition and recorded interval events.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TrialLaneSketch : NotebookAtlasGraphic
    {
        readonly NotebookTrialPlayback playback = new NotebookTrialPlayback();
        readonly MeshPen pen = new MeshPen(null, 8192);
        readonly List<TrialLaneBatchGraphic> batches = new List<TrialLaneBatchGraphic>();
        float started, duration, offset, lastSeconds = -1;
        bool playbackPaused, geometryDirty = true;
        Rect recordingBounds = new Rect(8, 8, 42, 28);
        Rect combatBounds = new Rect(8, 8, 42, 28);
        bool combatFocus;
        bool quietGrid;
        public bool QuietGrid
        {
            get => quietGrid;
            set { if (quietGrid == value) return; quietGrid = value; geometryDirty = true; RefreshGeometry(); }
        }
        /// <summary>The modern shared trial frames the recorded activity rather than unused fixture cells.</summary>
        public bool FrameRecording { get; set; }
        public Rect RecordingBounds => recordingBounds;
        public bool CombatFocus
        {
            get => combatFocus;
            set { if (combatFocus == value) return; combatFocus = value; geometryDirty = true; RefreshGeometry(); }
        }
        /// <summary>One uniform world viewport; only the camera changes between combat and the full recording.</summary>
        public Rect ViewBounds => !FrameRecording ? new Rect(8, 8, 42, 28) : combatFocus ? combatBounds : recordingBounds;
        public bool HasRecording => playback.Report != null && playback.Report.Frames.Count > 0;
        public NotebookTrialPlayback Playback => playback;
        public float PlaybackSpeed { get; set; } = 4;
        public bool PlaybackPaused
        {
            get => playbackPaused;
            set { if (playbackPaused == value) return; offset = PlaybackSeconds; started = Time.unscaledTime; playbackPaused = value; }
        }
        public float PlaybackSeconds => Mathf.Repeat(offset + (playbackPaused ? 0 : (Time.unscaledTime - started) * PlaybackSpeed), Mathf.Max(.1f, duration + .5f));
        public void SetResult(TrialReport value, float startTime, float totalDuration)
        {
            playback.Bind(value); started = startTime; duration = totalDuration; offset = 0;
            recordingBounds = NotebookTrialRenderer.RecordingBounds(value);
            combatBounds = NotebookTrialRenderer.CombatBounds(value, recordingBounds);
            lastSeconds = -1; playbackPaused = false; geometryDirty = true;
            RefreshGeometry();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            geometryDirty = true;
            RefreshGeometry();
        }

        protected override void OnDisable()
        {
            // Disabling just this component does not disable child GameObjects.
            foreach (var batch in batches) if (batch != null) batch.enabled = false;
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            foreach (var batch in batches)
            {
                if (batch == null) continue;
                if (UnityEngine.Application.isPlaying) Destroy(batch.gameObject);
                else DestroyImmediate(batch.gameObject);
            }
            batches.Clear();
            base.OnDestroy();
        }

        void Update() => RefreshGeometry();

        void RefreshGeometry()
        {
            // Pool mutations and dirty registration belong outside the Canvas rebuild. Every Graphic
            // reads the same already-prepared pose; none rebuilds another Graphic in OnPopulateMesh.
            if (!IsActive() || CanvasUpdateRegistry.IsRebuildingGraphics() || CanvasUpdateRegistry.IsRebuildingLayout())
                return;
            float seconds = PlaybackSeconds;
            float minimumWidth = 1f / (DrawingScale(rectTransform.rect) * Mathf.Max(.01f, canvas == null ? 1 : canvas.scaleFactor));
            if (!Mathf.Approximately(pen.MinimumLineWidth, minimumWidth))
            { pen.MinimumLineWidth = minimumWidth; geometryDirty = true; }
            if (!geometryDirty && (!HasRecording || seconds == lastSeconds))
            {
                SyncBatchAppearance();
                return;
            }
            if (HasRecording)
            {
                playback.Seek(seconds);
                NotebookTrialRenderer.Draw(pen, playback, FrameRecording ? ViewBounds : (Rect?)null, quietGrid ? .035f : .12f);
            }
            else pen.Clear();
            lastSeconds = seconds; geometryDirty = false;

            int overflowCount = Mathf.Max(0, pen.UiBatchCount - 1);
            while (batches.Count < overflowCount)
            {
                var child = new GameObject("Trial geometry batch " + (batches.Count + 1), typeof(RectTransform));
                child.layer = gameObject.layer;
                child.SetActive(false);
                child.transform.SetParent(transform, false);
                var batch = child.AddComponent<TrialLaneBatchGraphic>();
                batch.Initialize(this, batches.Count + 1);
                batches.Add(batch);
                child.SetActive(true);
            }
            for (int i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];
                batch.enabled = i < overflowCount;
                if (!batch.enabled) continue;
                batch.SetVerticesDirty();
            }
            SyncBatchAppearance();
            SetVerticesDirty();
        }

        void SyncBatchAppearance()
        {
            foreach (var batch in batches)
            {
                batch.material = material;
                batch.maskable = maskable;
            }
        }

        protected override void OnPopulateMesh(VertexHelper target)
            => PopulateBatch(target, rectTransform.rect, 0);

        internal void PopulateBatch(VertexHelper target, Rect rect, int batchIndex)
        {
            target.Clear();
            if (!isActiveAndEnabled || !HasRecording) return;
            float scale = DrawingScale(rect);
            var center = ViewBounds.center;
            // Source 10 places towers on the left. Reflect the complete recorded
            // triangle stream, so bodies, muzzle positions, impacts and health bars
            // retain exactly the same spatial relationships and timestamps.
            if (FrameRecording) center.x = -center.x;
            pen.CopyUiBatchTo(target, rect.center - center * scale, scale, batchIndex, FrameRecording);
        }
        float DrawingScale(Rect rect)
        {
            var size = ViewBounds.size;
            return Mathf.Max(.1f, Mathf.Min((rect.width - (FrameRecording ? 24 : 16)) / size.x,
                (rect.height - (FrameRecording ? 24 : 14)) / size.y));
        }
    }
}
