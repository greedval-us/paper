using System;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;
namespace PaperDefense.Presentation
{
    /// <summary>Two views share their owner's clock; world X and Y use one letterboxed scale.</summary>
    public sealed class FootprintProbeSketch : UnityEngine.UI.MaskableGraphic
    {
        IReadOnlyList<TrialFrame> frames;
        Func<float> clock;
        Rect world = new Rect(8,4.5f,16,12);
        readonly MeshPen towerPen = new MeshPen(null, 4096);
        public float WorldScale => Mathf.Min((rectTransform.rect.width - 20) / world.width, (rectTransform.rect.height - 20) / world.height);
        public float PlaybackSeconds => clock?.Invoke() ?? 0;
        public void SetRecording(FootprintProbeReport value, Func<float> sharedClock)
        {
            SetFrames(value?.Frames,sharedClock,new Rect(8,4.5f,16,12));
        }
        public void SetFrames(IReadOnlyList<TrialFrame> value,Func<float> sharedClock,Rect bounds)
        {
            frames = value;
            clock = sharedClock;
            world = bounds;
            SetVerticesDirty();
        }
        void Update()
        {
            if (frames != null)
            {
                SetVerticesDirty();
            }
        }
        Vector2 Point(float x, float y) => rectTransform.rect.center + new Vector2(x-world.center.x,y-world.center.y) * WorldScale;
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            if (frames == null || frames.Count == 0)
            {
                return;
            }
            int index = 0;
            float seconds = PlaybackSeconds;
            for (int i = 1; i<frames.Count && frames[i].Seconds <= seconds; i++)
            {
                index = i;
            }
            for (int x = 0; x <= 8; x++)
            {
                float at=world.xMin+x*world.width/8;
                BlueprintSketch.Line(vh, Point(at,world.yMin),Point(at,world.yMax),1,MeshPen.Alpha(NotebookPalette.Blue,.14f));
            }
            for (int y = 0; y <= 6; y++)
            {
                float at=world.yMin+y*world.height/6;
                BlueprintSketch.Line(vh, Point(world.xMin,at),Point(world.xMax,at),1,MeshPen.Alpha(NotebookPalette.Blue,.14f));
            }
            var frame = frames[index];
            foreach(var shot in frame.Shots)
                BlueprintSketch.Line(vh,Point(shot.From.X,shot.From.Y),Point(shot.To.X,shot.To.Y),2,MeshPen.Alpha(NotebookPalette.Blue,.55f));
            foreach (var tower in frame.Towers)
            {
                towerPen.Clear(); towerPen.MinimumLineWidth = 1.2f / Mathf.Max(.1f, WorldScale);
                DoodleGlyphs.StructureBase(towerPen, tower);
                DoodleGlyphs.StructureDynamic(towerPen, tower, 1, seconds);
                towerPen.CopyTo(vh, rectTransform.rect.center - world.center * WorldScale, WorldScale);
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
                var center = Point(impact.To.X, impact.To.Y);
                var from = Point(impact.From.X, impact.From.Y);
                var direction = (center - from).normalized;
                var normal = new Vector2(-direction.y, direction.x);
                var arrow = center - direction * 12;
                var ink = MeshPen.Alpha(NotebookPalette.Blue, .4f);
                BlueprintSketch.Line(vh, from, arrow, 1, ink);
                BlueprintSketch.Line(vh, arrow, arrow - direction * 7 + normal * 4, 1, ink);
                BlueprintSketch.Line(vh, arrow, arrow - direction * 7 - normal * 4, 1, ink);
                for (int i = 0; i < shape.OutlineCount; i++)
                {
                    var a = shape.OutlineOffset(i); var b = shape.OutlineOffset(i + 1);
                    var p = center + new Vector2(a.X, a.Y) * WorldScale;
                    var q = center + new Vector2(b.X, b.Y) * WorldScale;
                    BlueprintSketch.Line(vh, p, q, 2, NotebookPalette.Red);
                }
            }
        }
    }
}
