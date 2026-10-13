using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Trial playback uses simulation timestamps, the shared composition and recorded interval events.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TrialLaneSketch : Graphic
    {
        readonly NotebookTrialPlayback playback = new NotebookTrialPlayback();
        readonly MeshPen pen = new MeshPen(null, 8192);
        float started, duration, offset, lastSeconds = -1;
        bool playbackPaused;
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
            lastSeconds = -1; playbackPaused = false; SetVerticesDirty();
        }
        void Update()
        {
            if (!HasRecording) return;
            float seconds = PlaybackSeconds;
            if (seconds == lastSeconds) return;
            playback.Seek(seconds); lastSeconds = seconds; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear(); if (!HasRecording) return;
            playback.Seek(PlaybackSeconds);
            NotebookTrialRenderer.Draw(pen, playback);
            Rect rect = rectTransform.rect;
            float scale = Mathf.Max(.1f, Mathf.Min((rect.width - 16) / 42, (rect.height - 14) / 28));
            pen.CopyTo(target, rect.center - new Vector2(29, 22) * scale, scale);
        }
    }
}
