using System;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Small, bounded battle marks; the renderer still owns travel, footprints and event age.</summary>
    internal static class InkSwarmEffectArt
    {
        static readonly string[] EventIds = {
            "fx.muzzle", "fx.tracer", "fx.piercer", "fx.fan", "fx.ricochet", "fx.stapler",
            "fx.mortar.explosion", "fx.structure.destroy", "fx.blot.birth", "fx.blot.expire",
            "fx.blot.cleanse", "fx.blot.absorb", "fx.spitter.impact", "fx.castle.hit",
            "fx.well.hit", "fx.well.activate", "fx.structure.hit", "fx.support.repair", "fx.support.slow", "fx.generator.paid",
            "fx.support.spikes", "fx.support.beacon", "fx.support.prism"
        };

        internal static NotebookArtShape Create(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            string semantic = id;
            int phase = 0;
            if (id.EndsWith(".settle", StringComparison.Ordinal))
            { semantic = id.Substring(0, id.Length - ".settle".Length); phase = 1; }
            else if (id.EndsWith(".residue", StringComparison.Ordinal))
            { semantic = id.Substring(0, id.Length - ".residue".Length); phase = 2; }

            if (phase > 0)
            {
                bool animated = false;
                foreach (string eventId in EventIds)
                    if (eventId == semantic) { animated = true; break; }
                if (!animated) return null;
            }

            var p = new BattleInkDrawing(id);
            switch (semantic)
            {
                case "fx.muzzle": Muzzle(p, phase); break;
                case "fx.tracer": Impact(p, phase); break;
                case "fx.piercer": Piercer(p, phase); break;
                case "fx.fan": Fan(p, phase); break;
                case "fx.ricochet": Ricochet(p, phase); break;
                case "fx.stapler": Stapler(p, phase); break;
                case "fx.cleanup": Cleanup(p); break;
                case "fx.mortar.projectile": MortarProjectile(p); break;
                case "fx.mortar.explosion": Explosion(p, phase); break;
                case "fx.spitter.projectile": SpitterProjectile(p); break;
                case "fx.spitter.impact": SpitterImpact(p, phase); break;
                case "fx.structure.destroy": Destroy(p, phase); break;
                case "fx.blot.birth": BlotBirth(p, phase); break;
                case "fx.blot.expire": BlotExpire(p, phase); break;
                case "fx.blot.cleanse": BlotCleanse(p, phase); break;
                case "fx.blot.absorb": BlotAbsorb(p, phase); break;
                case "fx.castle.hit": CastleHit(p, phase); break;
                case "fx.well.hit": WellHit(p, phase); break;
                case "fx.well.activate": Activate(p, phase); break;
                case "fx.structure.hit": StructureHit(p, phase); break;
                case "fx.support.repair": Repair(p, phase); break;
                case "fx.support.slow": SlowWork(p, phase); break;
                case "fx.support.spikes":
                    p.Path(NotebookInkChannel.Amber,.037f,.92f,-.13f,-.09f, .01f,.14f, .12f,-.06f);
                    if (phase == 0) p.Line(NotebookInkChannel.Ivory,0,.18f,.03f,.28f,.025f,.82f);
                    break;
                case "fx.support.beacon":
                    float radius = phase == 0 ? .50f : .40f;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        float x = corner < 2 ? -radius : radius;
                        float y = corner % 2 == 0 ? -radius : radius;
                        p.Path(NotebookInkChannel.Green,.037f,1, x*.56f,y, x,y, x,y*.56f);
                    }
                    break;
                case "fx.support.prism":
                    p.Path(NotebookInkChannel.Ivory,.038f,1,-.29f,-.15f,0,.32f,.29f,-.15f,-.29f,-.15f);
                    break;
                case "fx.generator.paid": Paid(p, phase); break;
                case "fx.status.red": RedStatus(p); break;
                case "fx.status.blue": BlueStatus(p); break;
                case "fx.status.green": GreenStatus(p); break;
                case "object.blot": Blot(p); break;
                default: return null;
            }
            return p.Shape;
        }

        internal static void PopulateClips(NotebookArtSource data)
        {
            if (data == null) return;
            foreach (string id in EventIds)
            {
                bool exists = false;
                foreach (var existing in data.Clips)
                    if (existing.Id == id) { exists = true; break; }
                if (exists) continue;
                float duration = id == "fx.structure.hit" ? .18f : id == "fx.support.repair" || id == "fx.support.slow" ? .24f :
                    id == "fx.support.spikes" ? .26f : id == "fx.support.beacon" ? .38f :
                    id == "fx.generator.paid" ? .45f : id == "fx.mortar.explosion" || id == "fx.structure.destroy" ? .46f
                    : id == "fx.muzzle" || id == "fx.tracer" || id == "fx.piercer" || id == "fx.fan"
                        || id == "fx.ricochet" || id == "fx.stapler" ? .17f : .5f;
                var clip = new NotebookArtClip { Id = id, Duration = duration };
                clip.Frames.Add(new NotebookArtFrame { ShapeId = id });
                clip.Frames.Add(new NotebookArtFrame { Time = duration * .38f, ShapeId = id + ".settle" });
                clip.Frames.Add(new NotebookArtFrame { Time = duration * .72f, ShapeId = id + ".residue" });
                data.Clips.Add(clip);
            }
            // Cleanup is both a .17s shot and a .5s erasure. Its event-derived fallback must remain intact.
            // Projectiles and status marks are driven directly by the existing simulation, with no clip clock.
        }

        static void StructureHit(BattleInkDrawing p, int phase)
        {
            float spread=phase==0?.10f:phase==1?.17f:.23f;
            p.Path(NotebookInkChannel.Danger,.032f,.92f,-spread,.02f,-spread-.09f,.13f,-spread-.05f,.22f);
            p.Path(NotebookInkChannel.Ivory,.026f,.88f,0,.04f,.015f,.20f,.09f,.27f);
            p.Path(NotebookInkChannel.Danger,.025f,.80f,spread,.01f,spread+.10f,.12f,spread+.08f,.19f);
        }
        static void Repair(BattleInkDrawing p, int phase)
        {
            float size=phase==0?.13f:phase==1?.20f:.25f;
            p.Path(NotebookInkChannel.Clean,.028f,.90f,-size,-.07f,-size,.07f);
            p.Path(NotebookInkChannel.Clean,.028f,.90f,size,-.07f,size,.07f);
            p.Path(NotebookInkChannel.Ivory,.022f,.75f,-.07f,-size,.07f,-size);
            p.Path(NotebookInkChannel.Ivory,.022f,.75f,-.07f,size,.07f,size);
        }
        static void SlowWork(BattleInkDrawing p, int phase)
        {
            float spread=phase==0?.13f:phase==1?.20f:.26f;
            p.Path(NotebookInkChannel.Amber,.025f,.82f,-spread,-.12f,-spread-.04f,0,-spread,.12f);
            p.Path(NotebookInkChannel.Amber,.025f,.82f,spread,-.12f,spread+.04f,0,spread,.12f);
            p.Ellipse(NotebookInkChannel.Sketch,0,-.17f,.12f,.38f,.018f,.6f,12);
        }
        static void Paid(BattleInkDrawing p, int phase)
        {
            p.Fill(NotebookInkChannel.Surface,.96f,-.12f,-.17f,.12f,-.17f,.12f,.18f,-.12f,.18f);
            p.Path(NotebookInkChannel.Amber,.028f,.95f,-.12f,-.17f,-.12f,.18f,.12f,.18f,.12f,-.17f,-.12f,-.17f);
            p.Line(NotebookInkChannel.Ivory,-.065f,.08f,.065f,.08f,.022f,.85f);
            p.Line(NotebookInkChannel.Ivory,-.065f,-.02f,.065f,-.02f,.022f,.85f);
            if(phase<2)p.Line(NotebookInkChannel.Amber,.19f,.08f,.25f,.13f,.022f,.65f);
        }

        static void Muzzle(BattleInkDrawing p, int phase)
        {
            float s = phase == 0 ? 1 : phase == 1 ? .8f : .55f;
            p.Fill(NotebookInkChannel.Amber, .7f, -.045f * s, -.025f * s, .025f * s, -.045f * s,
                .075f * s, .015f * s, -.02f * s, .055f * s);
            p.Path(NotebookInkChannel.Amber, .021f, 1, -.25f * s, -.035f * s, -.07f * s, .005f * s,
                -.18f * s, .15f * s);
            p.Path(NotebookInkChannel.Ivory, .018f, .95f, -.075f * s, -.15f * s, -.015f * s, -.055f * s,
                .025f * s, -.24f * s);
            p.Path(NotebookInkChannel.Amber, .022f, 1, .06f * s, .035f * s, .26f * s, .10f * s,
                .12f * s, .025f * s, .30f * s, -.025f * s);
            p.Line(NotebookInkChannel.Ivory, .015f * s, .075f * s, .075f * s, .22f * s, .018f);
            if (phase == 0)
            {
                p.Line(NotebookInkChannel.Amber, -.32f, .10f, -.28f, .08f, .014f, .65f);
                p.Line(NotebookInkChannel.Amber, .18f, -.12f, .23f, -.17f, .014f, .75f);
            }
        }

        static void Impact(BattleInkDrawing p, int phase)
        {
            float s = phase == 2 ? .7f : 1;
            p.Path(NotebookInkChannel.Ivory, .023f, .95f, -.20f * s, -.10f * s, -.055f, -.015f,
                -.10f * s, .17f * s);
            p.Path(NotebookInkChannel.Ivory, .021f, 1, -.025f, .045f, .02f * s, .24f * s,
                .055f, .065f, .18f * s, .11f * s);
            p.Path(NotebookInkChannel.Ivory, .021f, .9f, .20f * s, -.095f * s, .05f, -.04f,
                .10f * s, -.20f * s);
            p.Line(NotebookInkChannel.Sketch, -.15f, .045f, -.25f * s, .10f * s, .014f, .7f);
            if (phase < 2) p.Line(NotebookInkChannel.Amber, -.035f, -.055f, .04f, .035f, .019f, .9f);
        }

        static void Piercer(BattleInkDrawing p, int phase)
        {
            float s = phase == 2 ? .7f : 1;
            p.Path(NotebookInkChannel.Ivory, .023f, 1, -.07f, -.30f * s, -.015f, -.08f, .035f, .04f,
                .10f, .30f * s);
            p.Path(NotebookInkChannel.Amber, .018f, .9f, .06f, -.22f * s, .03f, -.045f, .075f, .13f * s);
            p.Line(NotebookInkChannel.Ivory, -.06f, .015f, -.22f * s, .13f * s, .018f, .9f);
            p.Line(NotebookInkChannel.Ivory, .075f, -.025f, .23f * s, -.12f * s, .018f, .9f);
            if (phase == 0) p.Line(NotebookInkChannel.Amber, -.14f, -.14f, -.22f, -.22f, .013f, .75f);
        }

        static void Fan(BattleInkDrawing p, int phase)
        {
            Impact(p, phase);
            if (phase == 2) return;
            p.Line(NotebookInkChannel.Sketch, -.26f, -.015f, -.32f, -.045f, .014f, .8f);
            p.Line(NotebookInkChannel.Ivory, -.20f, .19f, -.25f, .25f, .016f, .85f);
            p.Line(NotebookInkChannel.Ivory, .22f, .17f, .28f, .21f, .016f, .85f);
        }

        static void Ricochet(BattleInkDrawing p, int phase)
        {
            p.Path(NotebookInkChannel.Ivory, .024f, 1, -.24f, -.12f, -.055f, .025f, .10f, -.045f, .23f, .12f);
            p.Path(NotebookInkChannel.Amber, .017f, .9f, -.055f, .025f, -.08f, .21f, -.035f, .13f);
            if (phase < 2)
            {
                p.Line(NotebookInkChannel.Ivory, .10f, -.045f, .16f, -.20f, .017f, .85f);
                p.Line(NotebookInkChannel.Sketch, -.28f, .03f, -.20f, .065f, .014f, .65f);
            }
        }

        static void Stapler(BattleInkDrawing p, int phase)
        {
            p.Path(NotebookInkChannel.Ivory, .023f, 1, -.19f, -.15f, -.17f, .12f, .14f, .13f, .18f, -.13f);
            p.Line(NotebookInkChannel.Amber, -.12f, .055f, .10f, .065f, .018f, .85f);
            if (phase < 2)
            {
                p.Line(NotebookInkChannel.Ivory, -.25f, -.07f, -.31f, -.12f, .016f, .9f);
                p.Line(NotebookInkChannel.Ivory, .24f, -.045f, .30f, -.08f, .016f, .9f);
                p.Line(NotebookInkChannel.Sketch, -.035f, .20f, -.02f, .29f, .014f, .7f);
            }
        }

        static void MortarProjectile(BattleInkDrawing p)
        {
            p.Ellipse(NotebookInkChannel.Surface, 0, 0, .115f, .88f, 0, .95f, 12);
            p.Ellipse(NotebookInkChannel.Ivory, 0, 0, .115f, .88f, .020f, .95f, 12);
            p.Path(NotebookInkChannel.Amber, .018f, 1, -.065f, .075f, -.035f, .145f, -.065f, .20f);
            p.Line(NotebookInkChannel.Ivory, -.055f, -.055f, .075f, .015f, .015f, .75f);
            p.Line(NotebookInkChannel.Amber, -.09f, .17f, -.13f, .20f, .014f, .7f);
        }

        static void Explosion(BattleInkDrawing p, int phase)
        {
            // All fragments stay inside the unit footprint. The renderer draws its exact perimeter.
            float s = phase == 0 ? .70f : phase == 1 ? .86f : .92f;
            p.Path(NotebookInkChannel.Ivory, .024f, .95f, -.62f * s, -.15f * s, -.18f, -.02f,
                -.30f * s, .25f * s, -.085f, .08f, -.15f * s, .63f * s);
            p.Path(NotebookInkChannel.Ivory, .025f, 1, .02f, .15f, .23f * s, .55f * s,
                .17f, .07f, .57f * s, .27f * s);
            p.Path(NotebookInkChannel.Amber, .021f, .9f, .56f * s, -.22f * s, .13f, -.075f,
                .28f * s, -.43f * s);
            p.Path(NotebookInkChannel.Sketch, .018f, .85f, -.47f * s, -.39f * s, -.14f, -.10f,
                -.10f * s, -.52f * s);
            p.Line(NotebookInkChannel.Ivory, -.39f * s, .39f * s, -.48f * s, .47f * s, .019f, .85f);
            p.Line(NotebookInkChannel.Ivory, .43f * s, .44f * s, .50f * s, .52f * s, .018f, .9f);
            if (phase < 2)
            {
                p.Line(NotebookInkChannel.Amber, -.025f, -.11f, .07f, .07f, .027f, .85f);
                p.Ellipse(NotebookInkChannel.Amber, .02f, -.015f, .04f, .75f, 0, .6f, 8);
            }
        }

        static void SpitterProjectile(BattleInkDrawing p)
        {
            p.Fill(NotebookInkChannel.Danger, .95f, -.14f, -.025f, -.09f, -.10f, .035f, -.11f,
                .15f, -.025f, .075f, .09f, -.035f, .11f);
            p.Path(NotebookInkChannel.Ivory, .015f, .85f, -.08f, .055f, -.025f, .08f, .055f, .055f);
            p.Path(NotebookInkChannel.Surface, .019f, .85f, -.035f, -.04f, .015f, -.07f, .065f, -.045f);
            p.Ellipse(NotebookInkChannel.Danger, -.205f, .085f, .029f, .75f, 0, .8f, 8);
        }

        static void SpitterImpact(BattleInkDrawing p, int phase)
        {
            float s = phase == 0 ? .85f : 1;
            p.Ellipse(NotebookInkChannel.Danger, 0, -.07f, .18f * s, .28f, 0, .38f, 12);
            p.Path(NotebookInkChannel.Danger, .024f, 1, -.33f * s, -.085f, -.10f, -.03f,
                -.23f * s, .17f * s, -.035f, .055f, -.045f, .29f * s);
            p.Path(NotebookInkChannel.Danger, .025f, 1, .045f, .055f, .22f * s, .23f * s,
                .13f, .015f, .34f * s, .035f);
            p.Path(NotebookInkChannel.Ivory, .016f, .75f, -.16f, -.10f, -.035f, -.12f, .16f, -.09f);
            if (phase < 2)
            {
                p.Ellipse(NotebookInkChannel.Danger, -.27f, .24f, .030f, 1.35f, 0, .9f, 8);
                p.Ellipse(NotebookInkChannel.Danger, .29f, .17f, .026f, 1.1f, 0, .9f, 8);
            }
        }

        static void Destroy(BattleInkDrawing p, int phase)
        {
            float s = phase == 0 ? .8f : phase == 1 ? .95f : 1;
            p.Ellipse(NotebookInkChannel.Surface, 0, -.12f, .29f, .30f, 0, .85f, 12);
            p.Path(NotebookInkChannel.Danger, .020f, .75f, -.36f * s, -.13f, -.12f, -.17f,
                -.03f, -.14f, .13f, -.18f, .34f * s, -.12f);
            Debris(p, -.21f * s, .14f * s, .075f, -.3f);
            Debris(p, .14f * s, .23f * s, .08f, .65f);
            Debris(p, .31f * s, -.005f, .055f, .2f);
            p.Path(NotebookInkChannel.Sketch, .016f, .8f, -.19f, -.02f, -.32f * s, .055f * s,
                -.37f * s, .12f * s);
            if (phase < 2)
                p.Line(NotebookInkChannel.Ivory, .02f, .09f, .045f, .37f * s, .019f, .9f);
        }

        static void Debris(BattleInkDrawing p, float x, float y, float size, float slant)
        {
            p.Fill(NotebookInkChannel.Surface, .95f, x - size, y - size * .5f,
                x + size * .5f, y - size * .7f, x + size * .75f, y + size * .6f, x - size * .4f, y + size);
            p.Path(NotebookInkChannel.Ivory, .018f, .9f, x - size, y - size * .5f,
                x - size * .4f, y + size, x + size * .75f, y + size * .6f);
            p.Line(NotebookInkChannel.Sketch, x - size * .25f, y - size * .3f,
                x + size * slant, y + size * .45f, .013f, .75f);
        }

        static void Blot(BattleInkDrawing p)
        {
            // Overlapping convex patches give an organic pool without a concave triangle fan.
            p.Ellipse(NotebookInkChannel.Surface, -.03f, -.025f, .25f, .43f, 0, .85f, 14);
            p.Ellipse(NotebookInkChannel.Danger, .08f, -.03f, .18f, .31f, 0, .22f, 12);
            p.Path(NotebookInkChannel.Sketch, .017f, .75f, -.28f, -.035f, -.23f, -.09f,
                -.13f, -.095f, -.11f, -.13f, .015f, -.11f, .13f, -.12f, .24f, -.075f, .29f, -.055f);
            p.Path(NotebookInkChannel.Ivory, .015f, .55f, -.23f, .045f, -.14f, .07f, -.055f, .055f);
            p.Ellipse(NotebookInkChannel.Danger, -.33f, -.025f, .032f, .52f, 0, .5f, 8);
            p.Ellipse(NotebookInkChannel.Sketch, .32f, -.08f, .024f, .5f, .013f, .5f, 8);
        }

        static void BlotBirth(BattleInkDrawing p, int phase)
        {
            Blot(p);
            if (phase == 2) return;
            p.Path(NotebookInkChannel.Danger, .020f, .8f, -.12f, -.025f, -.17f, .10f, -.24f, .17f);
            p.Path(NotebookInkChannel.Ivory, .016f, .75f, .06f, .015f, .14f, .17f, .18f, .20f);
            p.Ellipse(NotebookInkChannel.Danger, -.24f, .22f, .025f, 1.1f, 0, .75f, 8);
        }

        static void BlotExpire(BattleInkDrawing p, int phase)
        {
            p.Path(NotebookInkChannel.Sketch, .017f, .75f, -.27f, -.055f, -.15f, -.09f, -.08f, -.07f);
            p.Path(NotebookInkChannel.Sketch, .017f, .65f, .02f, -.09f, .15f, -.095f, .26f, -.045f);
            p.Line(NotebookInkChannel.Ivory, -.18f, .025f, -.12f, .075f, .015f, .7f);
            p.Line(NotebookInkChannel.Sketch, .11f, .015f, .16f, .07f, .014f, .6f);
            if (phase == 0)
                p.Ellipse(NotebookInkChannel.Surface, -.01f, -.04f, .14f, .25f, 0, .45f, 10);
        }

        static void BlotCleanse(BattleInkDrawing p, int phase)
        {
            float x = phase == 0 ? -.07f : phase == 1 ? .015f : .09f;
            p.Path(NotebookInkChannel.Clean, .024f, .95f, -.28f, -.075f, -.17f, -.10f, .02f, -.065f, .25f, -.075f);
            p.Line(NotebookInkChannel.Ivory, -.22f, -.01f, .23f, .025f, .016f, .85f);
            Spark(p, NotebookInkChannel.Clean, x, .15f, .10f);
            if (phase < 2) Spark(p, NotebookInkChannel.Ivory, -.24f, .13f, .045f);
        }

        static void BlotAbsorb(BattleInkDrawing p, int phase)
        {
            float s = phase == 0 ? 1 : phase == 1 ? .75f : .45f;
            p.Path(NotebookInkChannel.Amber, .018f, .9f, -.28f * s, -.05f, -.12f * s, .015f,
                -.05f * s, .11f, -.04f * s, .21f);
            p.Path(NotebookInkChannel.Clean, .018f, .85f, .28f * s, -.055f, .12f * s, .02f,
                .04f * s, .12f, .025f * s, .23f);
            p.Ellipse(NotebookInkChannel.Ivory, 0, .09f, .035f, 1.5f, 0, .75f, 8);
            p.Line(NotebookInkChannel.Sketch, -.15f * s, -.095f, .15f * s, -.095f, .015f, .65f);
        }

        static void CastleHit(BattleInkDrawing p, int phase)
        {
            p.Path(NotebookInkChannel.Danger, .026f, .95f, -.13f, .31f, -.06f, .13f, -.10f, .035f,
                .025f, -.055f, -.025f, -.17f, .06f, -.29f);
            p.Path(NotebookInkChannel.Ivory, .019f, .9f, -.06f, .13f, .07f, .17f, .20f, .27f);
            p.Path(NotebookInkChannel.Ivory, .019f, .9f, .025f, -.055f, .16f, -.08f, .27f, -.14f);
            if (phase < 2)
            {
                Debris(p, -.25f, -.08f, .04f, .2f);
                p.Line(NotebookInkChannel.Sketch, -.26f, .16f, -.32f, .21f, .015f, .75f);
            }
        }

        static void WellHit(BattleInkDrawing p, int phase)
        {
            p.Path(NotebookInkChannel.Danger, .023f, .95f, -.27f, -.08f, -.11f, -.015f,
                -.16f, .13f, -.035f, .045f, -.02f, .27f);
            p.Path(NotebookInkChannel.Ivory, .019f, .9f, .025f, .045f, .16f, .19f, .115f, -.025f, .27f, -.065f);
            p.Path(NotebookInkChannel.Sketch, .016f, .8f, -.20f, -.12f, -.035f, -.15f, .18f, -.125f);
            if (phase < 2)
                p.Ellipse(NotebookInkChannel.Danger, .22f, .18f, .028f, 1.5f, 0, .85f, 8);
        }

        static void Activate(BattleInkDrawing p, int phase)
        {
            // Repair/activation reads as three fine amber glints, never as a protective shell.
            Spark(p, NotebookInkChannel.Amber, -.03f, .10f, phase == 2 ? .12f : .19f);
            Spark(p, NotebookInkChannel.Ivory, -.24f, -.01f, .075f);
            if (phase < 2) Spark(p, NotebookInkChannel.Amber, .24f, .18f, .085f);
            p.Path(NotebookInkChannel.Clean, .016f, .7f, -.18f, -.15f, -.04f, -.18f, .15f, -.15f);
        }

        static void Cleanup(BattleInkDrawing p)
        {
            p.Path(NotebookInkChannel.Clean, .022f, .95f, -.26f, -.08f, -.11f, -.11f, .26f, -.04f);
            p.Path(NotebookInkChannel.Ivory, .018f, .9f, -.24f, .025f, -.04f, .005f, .18f, .075f);
            p.Line(NotebookInkChannel.Sketch, -.16f, -.18f, .10f, -.15f, .015f, .65f);
            Spark(p, NotebookInkChannel.Clean, .22f, .17f, .075f);
        }

        static void RedStatus(BattleInkDrawing p)
        {
            p.Path(NotebookInkChannel.Danger, .019f, .9f, -.24f, -.18f, -.29f, -.09f, -.25f, -.025f);
            p.Path(NotebookInkChannel.Danger, .019f, .9f, .24f, -.14f, .28f, -.04f, .25f, .045f);
            p.Line(NotebookInkChannel.Ivory, -.26f, -.10f, -.29f, -.045f, .013f, .65f);
        }

        static void BlueStatus(BattleInkDrawing p)
        {
            p.Path(NotebookInkChannel.Blue, .019f, .95f, -.29f, -.17f, -.25f, -.23f, -.17f, -.225f);
            p.Path(NotebookInkChannel.Blue, .019f, .95f, .17f, -.23f, .26f, -.23f, .30f, -.16f);
            p.Line(NotebookInkChannel.Ivory, -.075f, -.26f, .07f, -.26f, .013f, .75f);
        }

        static void GreenStatus(BattleInkDrawing p)
        {
            p.Path(NotebookInkChannel.Clean, .019f, .9f, -.28f, -.07f, -.24f, -.12f, -.20f, -.07f,
                -.24f, -.015f, -.28f, -.07f);
            p.Path(NotebookInkChannel.Clean, .019f, .9f, .20f, -.13f, .24f, -.18f, .28f, -.13f,
                .24f, -.075f, .20f, -.13f);
        }

        static void Spark(BattleInkDrawing p, NotebookInkChannel c, float x, float y, float size)
        {
            p.Line(c, x, y - size, x, y + size, .017f, .95f);
            p.Line(c, x - size * .48f, y, x + size * .48f, y, .016f, .85f);
        }
    }
}
