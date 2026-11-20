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
                case SimulationEventKind.ModernizationCompleted: return "fx.modernization.complete";
                case SimulationEventKind.StructureHit: return "fx.structure.hit";
                case SimulationEventKind.RepairApplied: return "fx.support.repair";
                case SimulationEventKind.GeneratorPaid: return "fx.generator.paid";
                case SimulationEventKind.SlowApplied: return "fx.support.slow";
                case SimulationEventKind.SpikesTriggered: return "fx.support.spikes";
                case SimulationEventKind.BeaconMarked: return "fx.support.beacon";
                case SimulationEventKind.PrismPulsed: return "fx.support.prism";
                case SimulationEventKind.Shot:
                    return item.Attack == AttackKind.Line ? "fx.piercer" : item.Attack == AttackKind.Cone ? "fx.fan"
                        : item.Attack == AttackKind.Ricochet ? "fx.ricochet" : item.Attack == AttackKind.Radial ? "fx.stapler"
                        : item.Attack == AttackKind.Cleanup ? "fx.cleanup" : "fx.tracer";
                default: return null;
            }
        }

        internal static float Duration(SimulationEvent item, NotebookVisualThemeAsset theme)
        {
            float? authored = theme?.FindClip(EffectId(item, theme))?.Duration;
            // Live effects and recorded playback share this lifetime. Even a short
            // theme clip must leave enough time to recognise the completion stamp.
            if (item.Kind == SimulationEventKind.ModernizationCompleted) return Mathf.Max(.9f, authored ?? .9f);
            return authored ?? (item.Kind == SimulationEventKind.ProjectileImpact || item.Kind == SimulationEventKind.StructureDestroyed ? .46f :
                item.Kind == SimulationEventKind.Shot || item.Kind == SimulationEventKind.WeaponActionStarted ? .17f : .5f);
        }

        internal static void Draw(MeshPen pen, SimulationEvent item, Vector2 muzzle, float age,
            NotebookVisualThemeAsset theme, bool darkSurface = false)
        {
            theme = theme ?? NotebookVisualThemeAsset.Default;
            float duration = Duration(item, theme);
            if (age < 0 || age >= duration) return;
            float t = Mathf.Clamp01(age / duration), fade = 1 - t;
            var at = new Vector2(item.To.X, item.To.Y);
            string id = EffectId(item, theme);
            int seed = (int)item.Sequence;
            if (item.Kind == SimulationEventKind.PrismPulsed)
            {
                var color = item.ColorEffect.Color;
                var ink = MeshPen.Alpha(NightNotebookEffectPalette.ColorInk(color, theme, darkSurface), fade);
                float reach = item.Radius * (theme.Data.ReducedMotion ? .92f : Mathf.Lerp(.2f, 1f, t));
                // Three different outlines make the cycle readable alongside the colour cue.
                int sides = color == InkColor.Red ? 3 : color == InkColor.Blue ? 6 : 4;
                var previous = Vector2.up;
                for (int edge = 1; edge <= sides; edge++)
                {
                    float angle = edge * 2f * Mathf.PI / sides;
                    var next = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                    pen.Line(at + previous * reach, at + Vector2.Lerp(previous, next, .65f) * reach, .045f, ink);
                    previous = next;
                }
                return;
            }
            if (item.Kind == SimulationEventKind.SpikesTriggered)
            {
                if (item.Radius <= 0) return;
                // Four short cuts on the actual contact perimeter leave the lane visible.
                float radius = item.Radius * (theme.Data.ReducedMotion ? 1 : Mathf.Lerp(.78f, 1, t));
                for (int edge = 0; edge < 4; edge++)
                {
                    float angle = (.25f + edge*.5f)*Mathf.PI;
                    Vector2 outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    DrawResource(pen, theme, id, at+outward*radius, theme.Data.ReducedMotion ? 0 : age,
                        fade, .85f, radians: angle-Mathf.PI*.5f, darkSurface: darkSurface);
                }
                return;
            }
            if (item.Kind == SimulationEventKind.BeaconMarked)
            {
                if (item.Magnitude <= 0) return;
                var from = new Vector2(item.From.X, item.From.Y);
                Color beam = NightNotebookEffectPalette.ColorInk(item.ColorEffect.Color, theme, darkSurface);
                pen.Line(from, at, .02f, MeshPen.Alpha(beam, fade*.42f));
                DrawResource(pen, theme, id, at, theme.Data.ReducedMotion ? 0 : age, fade,
                    darkSurface: darkSurface, inkColor: item.ColorEffect.Color);
                return;
            }
            if (item.Kind == SimulationEventKind.ModernizationCompleted)
            {
                // A single approval stamp above the source tower, never an expanding
                // blast or a repeating pulse. Reduced motion removes the initial settle.
                float settle = theme.Data.ReducedMotion ? 1 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .14f));
                // Overview cells can be only six pixels wide. Keep the stamp body at
                // least fourteen pixels there, with a bounded 2.4-cell maximum.
                float readableScale = Mathf.Clamp(14 * pen.WorldUnitsPerPixel / .78f, 1, 2.4f / .78f);
                float scale = Mathf.Min(Mathf.Lerp(1.12f, 1, settle) * readableScale, 2.4f / .78f);
                float opacity = 1 - Mathf.InverseLerp(.55f, 1, t);
                var center = at + Vector2.up * (.39f + .25f * scale + .06f * (1 - settle));
                var ink = MeshPen.Alpha(NightNotebookEffectPalette.Tone(new Color32(127, 88, 26, 255),
                    NightNotebookArt.Accent, theme, darkSurface), opacity);
                var paper = MeshPen.Alpha(NightNotebookEffectPalette.Tone(NotebookPalette.Paper,
                    NightNotebookArt.Paper, theme, darkSurface), opacity * .94f);
                var half = new Vector2(.39f, .25f) * scale;
                pen.Rect(center.x - half.x, center.y - half.y, half.x * 2, half.y * 2, paper);
                pen.Line(center + new Vector2(-half.x, -half.y), center + new Vector2(half.x, -half.y), .035f, ink);
                pen.Line(center + new Vector2(half.x, -half.y), center + new Vector2(half.x, half.y), .035f, ink);
                pen.Line(center + new Vector2(half.x, half.y), center + new Vector2(-half.x, half.y), .035f, ink);
                pen.Line(center + new Vector2(-half.x, half.y), center + new Vector2(-half.x, -half.y), .035f, ink);
                pen.Line(center + new Vector2(-.20f, .02f) * scale, center + new Vector2(-.055f, -.10f) * scale, .065f, ink);
                pen.Line(center + new Vector2(-.055f, -.10f) * scale, center + new Vector2(.23f, .15f) * scale, .065f, ink);
                return;
            }
            if (item.Kind == SimulationEventKind.StructureHit)
            {
                if (item.Magnitude <= 0) return;
                Vector2 impact = at - new Vector2(item.From.X,item.From.Y);
                DrawResource(pen,theme,id,at,age,fade,.7f, radians: Mathf.Atan2(impact.y,impact.x)-Mathf.PI*.5f,darkSurface:darkSurface);
                return;
            }
            if (item.Kind == SimulationEventKind.RepairApplied || item.Kind == SimulationEventKind.SlowApplied)
            {
                if (item.Magnitude <= 0) return;
                var from=new Vector2(item.From.X,item.From.Y);
                bool repair=item.Kind==SimulationEventKind.RepairApplied;
                float phase = theme.Data.ReducedMotion ? .5f : t;
                var direction=at-from;var side=direction.sqrMagnitude>.00001f?new Vector2(-direction.y,direction.x).normalized:Vector2.right;
                Color work=MeshPen.Alpha(NightNotebookEffectPalette.Tone(repair?NotebookPalette.Green:NotebookPalette.Blue,
                    repair?NightNotebookEffectPalette.Clean:NightNotebookArt.Accent,theme,darkSurface),fade*.72f);
                // This connector identifies the actual source and recipient. Short moving
                // stitches carry work, without painting a permanent beam across the map.
                pen.Line(from,at,.016f,MeshPen.Alpha(work,fade*.20f));
                for(int stitch=0;stitch<2;stitch++)
                {
                    var point=Vector2.Lerp(from,at,Mathf.Clamp01(.18f+phase*.58f+stitch*.12f));
                    pen.Line(point-side*.07f,point+side*.07f,.025f,work);
                }
                DrawResource(pen,theme,id,at,age,fade,.65f,darkSurface:darkSurface);
                return;
            }
            if (item.Kind == SimulationEventKind.GeneratorPaid)
            {
                if (item.Magnitude <= 0) return;
                float rise=theme.Data.ReducedMotion?0:t*.4f;
                DrawResource(pen,theme,id,at+Vector2.up*(.25f+rise),age,fade,.8f,darkSurface:darkSurface);
                return;
            }
            if (item.Kind == SimulationEventKind.StructureErased)
            {
                DrawResource(pen, theme, id, at, age, fade, darkSurface: darkSurface);
                return;
            }
            if (item.Kind == SimulationEventKind.StructureCompleted)
            {
                DrawResource(pen, theme, id, at, age, fade, .7f + t * .2f, darkSurface: darkSurface);
                return;
            }
            if (item.Kind == SimulationEventKind.WeaponActionStarted)
            {
                DrawResource(pen, theme, id, muzzle, age, fade, darkSurface: darkSurface, inkColor: item.ColorEffect.Color);
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
                        new Vector2(footprint.SideRadius, footprint.ForwardRadius), angle, darkSurface, item.ColorEffect.Color);
                }
                else if (!item.ImpactFootprint.HasValue) DrawResource(pen, theme, id, at, age, fade, radius, darkSurface: darkSurface, inkColor: item.ColorEffect.Color);
                Color ink = MeshPen.Alpha(item.ColorEffect.Color == InkColor.None ? NightNotebookEffectPalette.Tone(NotebookPalette.Blue,
                    NightNotebookArt.Accent, theme, darkSurface) : NightNotebookEffectPalette.ColorInk(item.ColorEffect.Color, theme, darkSurface), fade * .75f);
                // The perimeter stays at the actual affected area while the inner rings expand.
                DrawFootprint(pen, at, item.ImpactFootprint, item.Radius, 1, .025f, ink, 32, seed);
                DrawFootprint(pen, at, item.ImpactFootprint, item.Radius, .6f + t * .4f, .065f * fade + .025f, ink, 32, seed * 13);
                DrawFootprint(pen, at, item.ImpactFootprint, item.Radius, .45f + t * .4f, .035f,
                    MeshPen.Alpha(ink, fade * .35f), 32, seed * 19);
                return;
            }
            if (item.Kind == SimulationEventKind.StructureDestroyed)
            {
                DrawResource(pen, theme, id, at, age, fade, 1 + t * .5f, darkSurface: darkSurface);
                return;
            }
            if (item.Kind != SimulationEventKind.Shot)
            {
                DrawResource(pen, theme, id, at, age, fade, .8f + t * .5f, darkSurface: darkSurface);
                return;
            }
            if (item.Attack == AttackKind.Cleanup)
            {
                Color clean = MeshPen.Alpha(item.ColorEffect.Color == InkColor.None ? NightNotebookEffectPalette.Tone(NotebookPalette.Green,
                    NightNotebookEffectPalette.Clean, theme, darkSurface) : NightNotebookEffectPalette.ColorInk(item.ColorEffect.Color, theme, darkSurface), fade * .75f);
                pen.Line(muzzle, at, .025f, clean);
                pen.Ring(at, Mathf.Max(0, item.Radius) * (.65f + t * .35f), .035f, clean, 24);
            }
            else
            {
                Color color = MeshPen.Alpha(item.ColorEffect.Color == InkColor.None ? NightNotebookEffectPalette.Tone(
                    item.Attack == AttackKind.Line ? NotebookPalette.Marker : NotebookPalette.Blue,
                    item.Attack == AttackKind.Line ? NightNotebookArt.Accent : NightNotebookArt.Ink,
                    theme, darkSurface) : NightNotebookEffectPalette.ColorInk(item.ColorEffect.Color, theme, darkSurface), fade * .85f);
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
            DrawResource(pen, theme, id, at, age, fade, darkSurface: darkSurface, inkColor: item.ColorEffect.Color);
        }

        static void DrawResource(MeshPen pen, NotebookVisualThemeAsset theme, string id, Vector2 at,
            float age, float fade, float scale = 1, Vector2? localScale = null, float radians = 0, bool darkSurface = false,
            InkColor inkColor = InkColor.None)
        {
            var frame = theme?.FindClip(id)?.Sample(age);
            NightNotebookEffectPalette.DrawResource(pen, frame?.ShapeId ?? id, at + (frame?.Offset ?? Vector2.zero),
                theme, darkSurface, scale * (frame?.Scale ?? 1), radians + (frame?.Rotation ?? 0),
                opacity: fade * (frame?.Alpha ?? 1), localScale: localScale, inkColor: inkColor);
        }

        // Both live combat and trial playback draw the same launch-owned color and
        // trajectory. Presentation never looks up the tower's current ink here.
        internal static void DrawProjectile(MeshPen pen, Vector2 from, Vector2 to, float progress,
            FootprintGeometry? footprint, float radius, int id, InkColor inkColor,
            NotebookVisualThemeAsset theme, bool darkSurface)
        {
            progress = Mathf.Clamp01(progress);
            float height = Mathf.Min(3, Vector2.Distance(from, to) * .22f);
            Vector2 point = FlightPoint(from, to, progress, height);
            Vector2 trail = FlightPoint(from, to, Mathf.Max(0, progress - .1f), height);
            Color ink = NightNotebookEffectPalette.ColorInk(inkColor, theme, darkSurface);
            pen.Line(trail, point, .045f, MeshPen.Alpha(ink, .6f));
            NightNotebookEffectPalette.DrawResource(pen, "fx.mortar.projectile", point, theme, darkSurface, inkColor: inkColor);
            DrawFootprint(pen, to, footprint, radius, 1, .025f, MeshPen.Alpha(ink, .2f), 24, id, true);
        }

        static Vector2 FlightPoint(Vector2 from, Vector2 to, float t, float height)
            => Vector2.Lerp(from, to, t) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * height);

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
