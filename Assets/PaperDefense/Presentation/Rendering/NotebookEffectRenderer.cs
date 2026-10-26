using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Shared event visuals for the live battlefield and recorded trials.</summary>
    internal static class NotebookEffectRenderer
    {
        internal static string EffectId(SimulationEvent item, NotebookVisualThemeAsset theme = null)
        {
            switch (item.Kind)
            {
                case SimulationEventKind.WeaponActionStarted: return theme?.FindRecoil(item.WeaponId)?.EffectId ?? "fx.muzzle";
                case SimulationEventKind.ProjectileImpact: return "fx.mortar.explosion";
                case SimulationEventKind.StructureDestroyed: return "fx.structure.destroy";
                case SimulationEventKind.BlotSpawned: return "fx.blot.birth";
                case SimulationEventKind.BlotExpired: return "fx.blot.expire";
                case SimulationEventKind.BlotCleared: return "fx.blot.cleanse";
                case SimulationEventKind.BlotAbsorbed: return "fx.blot.absorb";
                case SimulationEventKind.EnemyGlobImpact: return "fx.spitter.impact";
                case SimulationEventKind.BaseHit: return "fx.castle.hit";
                case SimulationEventKind.InkWellHit: return "fx.well.hit";
                case SimulationEventKind.InkWellActivated: return "fx.well.activate";
                case SimulationEventKind.StructureErased: return "fx.cleanup";
                case SimulationEventKind.StructureCompleted: return "fx.well.activate";
                case SimulationEventKind.Shot:
                    return item.Attack == AttackKind.Line ? "fx.piercer" : item.Attack == AttackKind.Cone ? "fx.fan"
                        : item.Attack == AttackKind.Ricochet ? "fx.ricochet" : item.Attack == AttackKind.Radial ? "fx.stapler"
                        : item.Attack == AttackKind.Cleanup ? "fx.cleanup" : "fx.tracer";
                default: return null;
            }
        }

        internal static float Duration(SimulationEvent item, NotebookVisualThemeAsset theme)
            => theme?.FindClip(EffectId(item, theme))?.Duration ??
                (item.Kind == SimulationEventKind.ProjectileImpact || item.Kind == SimulationEventKind.StructureDestroyed ? .46f :
                 item.Kind == SimulationEventKind.Shot || item.Kind == SimulationEventKind.WeaponActionStarted ? .17f : .5f);

        internal static void Draw(MeshPen pen, SimulationEvent item, Vector2 muzzle, float age, NotebookVisualThemeAsset theme)
        {
            theme = theme ?? NotebookVisualThemeAsset.Default;
            float duration = Duration(item, theme);
            if (age < 0 || age >= duration) return;
            float t = Mathf.Clamp01(age / duration), fade = 1 - t;
            var at = new Vector2(item.To.X, item.To.Y);
            string id = EffectId(item, theme);
            int seed = (int)item.Sequence;
            if (item.Kind == SimulationEventKind.StructureErased)
            {
                DrawResource(pen, theme, id, at, age, fade);
                return;
            }
            if (item.Kind == SimulationEventKind.StructureCompleted)
            {
                DrawResource(pen, theme, id, at, age, fade, .7f + t * .2f);
                return;
            }
            if (item.Kind == SimulationEventKind.WeaponActionStarted)
            {
                DrawResource(pen, theme, id, muzzle, age, fade);
                return;
            }
            if (item.Kind == SimulationEventKind.ProjectileImpact)
            {
                float radius = item.ImpactFootprint?.ForwardRadius ?? Mathf.Max(0, item.Radius);
                if (item.ImpactFootprint.HasValue && !item.ImpactFootprint.Value.IsContour)
                {
                    var footprint = item.ImpactFootprint.Value;
                    float angle = Mathf.Atan2(footprint.Forward.Y, footprint.Forward.X) - Mathf.PI * .5f;
                    DrawResource(pen, theme, id, at, age, fade, 1,
                        new Vector2(footprint.SideRadius, footprint.ForwardRadius), angle);
                }
                else if (!item.ImpactFootprint.HasValue) DrawResource(pen, theme, id, at, age, fade, radius);
                Color ink = MeshPen.Alpha(NotebookPalette.Blue, fade * .75f);
                // The perimeter stays at the actual affected area while the inner rings expand.
                DrawFootprint(pen, at, item.ImpactFootprint, item.Radius, 1, .025f, ink, 32, seed);
                DrawFootprint(pen, at, item.ImpactFootprint, item.Radius, .6f + t * .4f, .065f * fade + .025f, ink, 32, seed * 13);
                DrawFootprint(pen, at, item.ImpactFootprint, item.Radius, .45f + t * .4f, .035f,
                    MeshPen.Alpha(ink, fade * .35f), 32, seed * 19);
                return;
            }
            if (item.Kind == SimulationEventKind.StructureDestroyed)
            {
                DrawResource(pen, theme, id, at, age, fade, 1 + t * .5f);
                return;
            }
            if (item.Kind != SimulationEventKind.Shot)
            {
                DrawResource(pen, theme, id, at, age, fade, .8f + t * .5f);
                return;
            }
            if (item.Attack == AttackKind.Cleanup)
            {
                Color clean = MeshPen.Alpha(NotebookPalette.Green, fade * .75f);
                pen.Line(muzzle, at, .025f, clean);
                pen.Ring(at, Mathf.Max(0, item.Radius) * (.65f + t * .35f), .035f, clean, 24);
            }
            else
            {
                Color color = MeshPen.Alpha(item.Attack == AttackKind.Line ? NotebookPalette.Marker : NotebookPalette.Blue, fade * .85f);
                float width = item.Attack == AttackKind.Line ? .075f : item.Attack == AttackKind.Cone ? .025f : .04f;
                if (item.Attack == AttackKind.Single)
                {
                    // Ordinary bullets read as moving ink marks rather than long beams.
                    // The captured hit and its impact artwork keep their actual target.
                    Vector2 path = at - muzzle;
                    float distance = path.magnitude;
                    if (distance > .0001f)
                    {
                        float length = Mathf.Min(.65f, distance);
                        float tip = Mathf.Lerp(length, distance, t);
                        Vector2 direction = path / distance;
                        pen.Line(muzzle + direction * (tip - length), muzzle + direction * tip, width, color);
                    }
                }
                else
                {
                    Vector2 from = item.Attack == AttackKind.Line || item.Attack == AttackKind.Ricochet ? muzzle
                        : Vector2.Lerp(muzzle, at, Mathf.Clamp01(t * .75f));
                    pen.Line(from, at, width, color);
                }
            }
            DrawResource(pen, theme, id, at, age, fade);
        }

        static void DrawResource(MeshPen pen, NotebookVisualThemeAsset theme, string id, Vector2 at,
            float age, float fade, float scale = 1, Vector2? localScale = null, float radians = 0)
        {
            var frame = theme?.FindClip(id)?.Sample(age);
            DoodleGlyphs.DrawResource(pen, frame?.ShapeId ?? id, at + (frame?.Offset ?? Vector2.zero),
                scale * (frame?.Scale ?? 1), radians + (frame?.Rotation ?? 0), opacity: fade * (frame?.Alpha ?? 1),
                localScale: localScale, theme: theme);
        }

        internal static void DrawFootprint(MeshPen pen, Vector2 center, FootprintGeometry? footprint,
            float fallbackRadius, float scale, float width, Color color, int segments, int seed, bool dashed = false)
        {
            if (footprint.HasValue && footprint.Value.IsContour)
            {
                var shape = footprint.Value;
                for (int i = 0; i < shape.OutlineCount; i++)
                {
                    var a = shape.OutlineOffset(i); var b = shape.OutlineOffset(i + 1);
                    var start = center + new Vector2(a.X, a.Y) * scale;
                    var end = center + new Vector2(b.X, b.Y) * scale;
                    if (!dashed) { pen.Line(start, end, width, color); continue; }
                    // Dashes belong to the perimeter, not to authored vertex indices.
                    float length = Vector2.Distance(start, end);
                    int pieces = Mathf.Clamp(Mathf.CeilToInt(length / .2f), 1, 512);
                    for (int piece = 0; piece < pieces; piece++)
                        pen.Line(Vector2.Lerp(start, end, (float)piece / pieces),
                            Vector2.Lerp(start, end, (piece + .65f) / pieces), width, color);
                }
                return;
            }
            float along = (footprint?.ForwardRadius ?? Mathf.Max(0, fallbackRadius)) * scale;
            float across = (footprint?.SideRadius ?? Mathf.Max(0, fallbackRadius)) * scale;
            if (along <= 0 || across <= 0) return;
            var direction = footprint?.Forward ?? new Point2(1, 0);
            var forward = new Vector2(direction.X, direction.Y);
            var side = new Vector2(-forward.y, forward.x);
            Vector2 previous = center + forward * along;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vector2 next = center + forward * (Mathf.Cos(angle) * along) + side * (Mathf.Sin(angle) * across);
                if (!dashed || i % 3 != 0) pen.Line(previous, next, width, color);
                previous = next;
            }
        }
    }
}
