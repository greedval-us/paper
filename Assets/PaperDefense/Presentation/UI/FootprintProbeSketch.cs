using System;
using PaperDefense.Core;
using UnityEngine;
namespace PaperDefense.Presentation
{
    /// <summary>Two views share their owner's clock; world X and Y use one letterboxed scale.</summary>
    public sealed class FootprintProbeSketch : UnityEngine.UI.Graphic
    {
        FootprintProbeReport report;
        Func<float> clock;
        public float WorldScale => Mathf.Min((rectTransform.rect.width - 20) / 16, (rectTransform.rect.height - 20) / 12);
        public float PlaybackSeconds => clock?.Invoke() ?? 0;
        public void SetRecording(FootprintProbeReport value, Func<float> sharedClock)
        {
            report = value;
            clock = sharedClock;
            SetVerticesDirty();
        }
        void Update()
        {
            if (report != null)
            {
                SetVerticesDirty();
            }
        }
        Vector2 Point(float x, float y) => rectTransform.rect.center + new Vector2(x - 16, y - 10.5f) * WorldScale;
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            if (report == null || report.Frames.Count == 0)
            {
                return;
            }
            int index = 0;
            float seconds = PlaybackSeconds;
            for (int i = 1; i<report.Frames.Count && report.Frames[i].Seconds <= seconds; i++)
            {
                index = i;
            }
            for (int x = 8; x <= 24; x+=2)
            {
                BlueprintSketch.Line(vh, Point(x, 4.5f), Point(x, 16.5f), 1, MeshPen.Alpha(NotebookPalette.Blue, .14f));
            }
            for (float y = 4.5f; y <= 16.5f; y+=2)
            {
                BlueprintSketch.Line(vh, Point(8, y), Point(24, y), 1, MeshPen.Alpha(NotebookPalette.Blue, .14f));
            }
            var frame = report.Frames[index];
            foreach (var tower in frame.Towers)
            {
                BlueprintSketch.Ring(vh, Point(tower.Cell.X + .5f, tower.Cell.Y + .5f), 5, 3, NotebookPalette.Blue);
            }
            foreach (var enemy in frame.Enemies)
            {
                BlueprintSketch.Ring(vh, Point(enemy.Position.X, enemy.Position.Y), 4, 3, enemy.Health<enemy.MaxHealth?NotebookPalette.Red:NotebookPalette.Navy);
            }
            foreach (var projectile in frame.Projectiles)
            {
                float t = Mathf.Clamp01(1 - projectile.Remaining / Mathf.Max(.001f, projectile.Duration));
                var from = Point(projectile.From.X, projectile.From.Y);
                var to = Point(projectile.To.X, projectile.To.Y);
                BlueprintSketch.Line(vh, from, to, 1, MeshPen.Alpha(NotebookPalette.Blue, .3f));
                BlueprintSketch.Ring(vh, Vector2.Lerp(from, to, t) + Vector2.up * Mathf.Sin(t * Mathf.PI) * 14, 3, 3, NotebookPalette.Marker);
            }
            foreach (var impact in frame.Impacts)
            {
                if (!impact.ImpactFootprint.HasValue)
                {
                    continue;
                }
                var shape = impact.ImpactFootprint.Value;
                var forward = new Vector2(shape.Forward.X, shape.Forward.Y);
                var side = new Vector2(-forward.y, forward.x);
                var center = Point(impact.To.X, impact.To.Y);
                for (int i = 0; i<64; i++)
                {
                    float a = i * Mathf.PI / 32, b = (i + 1) * Mathf.PI / 32;
                    var p = center + (forward * Mathf.Cos(a) * shape.ForwardRadius + side * Mathf.Sin(a) * shape.SideRadius) * WorldScale;
                    var q = center + (forward * Mathf.Cos(b) * shape.ForwardRadius + side * Mathf.Sin(b) * shape.SideRadius) * WorldScale;
                    BlueprintSketch.Line(vh, p, q, 2, NotebookPalette.Red);
                }
            }
        }
    }
}
