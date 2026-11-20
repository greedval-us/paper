using System;
using System.Collections.Generic;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Authored side profiles for the six ink creatures. Geometry is cached, never redrawn per frame.</summary>
    internal static partial class InkSwarmEnemyArt
    {
        static readonly string[] Families = { "doodle", "runner", "gnawer", "spitter", "brute", "bulwark" };
        static readonly string[] Tiers = { "sketch", "outlined", "hatched" };
        static readonly string[] Motions = { "idle", "walk", "attack", "hit", "death" };
        static readonly Dictionary<string, NotebookArtShape> Shapes = new Dictionary<string, NotebookArtShape>(StringComparer.Ordinal);

        internal static NotebookArtShape Create(string id)
        {
            if (id == null || !id.StartsWith("enemy.", StringComparison.Ordinal)) return null;
            if (id.EndsWith(".compact", StringComparison.Ordinal))
                return CreateCompact(id.Substring(0, id.Length - ".compact".Length));
            if (Shapes.TryGetValue(id, out var cached)) return cached;
            string[] parts = id.Split('.');
            if (parts.Length < 3 || parts.Length > 5) return null;
            int family = Array.IndexOf(Families, parts[1]);
            if (family < 0) return null;
            // Catalogue previews also use enemy.<family>.idle.
            bool shorthand = parts.Length == 3;
            int tier = shorthand ? 0 : Array.IndexOf(Tiers, parts[2]);
            int motion = Array.IndexOf(Motions, parts[shorthand ? 2 : 3]);
            int phase = parts.Length == 5 ? parts[4] == "a" ? 1 : parts[4] == "b" ? 2 : -1 : 0;
            if (tier < 0 || motion < 0 || phase < 0) return null;
            var drawing = new BattleInkDrawing(id);
            var pose = new Pose((NotebookEnemyMotion)motion, phase, tier);
            switch (family)
            {
                case 0: Doodle(drawing, pose); break;
                case 1: Runner(drawing, pose); break;
                case 2: Gnawer(drawing, pose); break;
                case 3: Spitter(drawing, pose); break;
                case 4: Brute(drawing, pose); break;
                default: Bulwark(drawing, pose); break;
            }
            FinishPose(drawing.Shape, pose);
            Shapes.Add(id, drawing.Shape);
            return drawing.Shape;
        }

        internal static void PopulateClips(NotebookArtSource data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            foreach (string family in Families)
            foreach (string tier in Tiers)
            for (int motion = 0; motion < Motions.Length; motion++)
            {
                string id = "enemy." + family + "." + tier + "." + Motions[motion];
                var clip = new NotebookArtClip { Id = id,
                    Duration = motion == 0 ? 1.2f : motion == 1 ? family == "runner" ? .36f : .52f : motion == 4 ? .6f : .24f,
                    Loop = motion == 0 || motion == 1 };
                if (motion == 0)
                    clip.Frames.Add(new NotebookArtFrame { ShapeId = id });
                else if (motion == 1)
                {
                    Frame(clip, id + ".a", 0);
                    Frame(clip, id, .25f);
                    Frame(clip, id + ".b", .5f);
                    Frame(clip, id, .75f);
                }
                else if (motion == 2)
                {
                    Frame(clip, id, 0);
                    Frame(clip, id + ".a", .28f);
                    Frame(clip, id + ".b", .7f);
                    Frame(clip, id + ".a", .9f);
                }
                else if (motion == 3)
                {
                    Frame(clip, id, 0, rotation: -.08f);
                    Frame(clip, id + ".a", .32f, rotation: -.035f);
                    Frame(clip, id + ".b", .74f);
                }
                else
                {
                    Frame(clip, id, 0, rotation: -.05f);
                    Frame(clip, id + ".a", .3f, alpha: .88f, rotation: -.13f);
                    Frame(clip, id + ".b", .68f, alpha: .46f, rotation: -.17f);
                    Frame(clip, id + ".b", .96f, alpha: 0, rotation: -.17f);
                }
                data.Clips.Add(clip);
            }
        }

        static void Frame(NotebookArtClip clip, string shapeId, float fraction, float alpha = 1, float rotation = 0)
            => clip.Frames.Add(new NotebookArtFrame { Time = clip.Duration * fraction, ShapeId = shapeId, Alpha = alpha, Rotation = rotation });

        readonly struct Pose
        {
            public readonly NotebookEnemyMotion Motion;
            public readonly int Phase, Tier;
            public readonly float Stride, Reach, Flinch, Fold, Bob, Edge;
            public Pose(NotebookEnemyMotion motion, int phase, int tier)
            {
                Motion = motion; Phase = phase; Tier = tier;
                Stride = motion == NotebookEnemyMotion.Walk ? phase == 1 ? .075f : phase == 2 ? -.075f : 0 : 0;
                Reach = motion == NotebookEnemyMotion.Attack ? phase == 2 ? 1 : phase == 1 ? -.15f : -.45f : 0;
                Flinch = motion == NotebookEnemyMotion.Hit ? phase == 0 ? 1 : phase == 1 ? .6f : .15f : 0;
                Fold = motion == NotebookEnemyMotion.Death ? .5f + phase * .25f : 0;
                Bob = motion == NotebookEnemyMotion.Walk ? phase == 1 ? .012f : phase == 2 ? -.012f : 0 : 0;
                Edge = tier == 0 ? .019f : .024f;
            }
        }

        static void Doodle(BattleInkDrawing d, Pose p)
        {
            float headX = .025f + p.Reach * .06f - p.Flinch * .055f;
            float headY = .19f - Mathf.Abs(p.Reach) * .025f;
            Limb(d, p, -.085f, -.13f, -.13f - p.Stride, -.24f + p.Fold * .08f,
                -.18f - p.Stride, -.35f + p.Fold * .16f, .064f);
            Limb(d, p, .07f, -.13f, .1f + p.Stride, -.25f + p.Fold * .09f,
                .16f + p.Stride, -.35f + p.Fold * .15f, .072f);
            Limb(d, p, -.09f, .04f, -.21f - p.Reach * .025f, -.055f,
                -.24f + p.Fold * .13f, -.18f + p.Flinch * .08f, .057f);
            Limb(d, p, .12f, .035f, .21f + p.Reach * .055f, -.03f + p.Reach * .07f,
                .25f + p.Reach * .075f - p.Fold * .15f, -.14f + p.Reach * .16f, .067f);
            Mass(d, -.005f, -.05f, .14f, 1.06f);
            DoodleChest(d);
            d.Path(NotebookInkChannel.Ivory, p.Edge, 1,
                -.13f,.035f, -.105f,-.15f, -.03f,-.20f, .09f,-.15f, .135f,.035f);
            Mass(d, headX, headY, .16f, 1.02f);
            DoodleCrest(d, headX, headY);
            d.Path(NotebookInkChannel.Ivory, p.Edge, 1,
                headX-.145f,headY-.03f, headX-.17f,headY+.09f, headX-.075f,headY+.11f,
                headX-.09f,headY+.22f, headX-.015f,headY+.155f, headX+.045f,headY+.18f,
                headX+.14f,headY+.10f, headX+.165f,headY-.01f, headX+.125f,headY-.11f,
                headX+.02f,headY-.16f, headX-.10f,headY-.11f, headX-.145f,headY-.03f);
            DoodleEyes(d, headX, headY, p);
            d.Fill(NotebookInkChannel.Ivory, .9f, headX+.055f,headY-.072f, headX+.103f,headY-.065f, headX+.077f,headY-.107f);
            Hatch(d, p, -.07f, -.035f, .028f, 3);
        }

        static void Runner(BattleInkDrawing d, Pose p)
        {
            float neckX = p.Reach * .045f - p.Flinch * .04f;
            float headY = .215f - p.Reach * .055f + p.Flinch * .035f;
            Limb(d, p, -.21f,.09f, -.27f+p.Stride,-.105f+p.Fold*.10f,
                -.36f+p.Stride,-.37f+p.Fold*.20f, .036f);
            Limb(d, p, -.11f,.065f, -.13f-p.Stride,-.15f+p.Fold*.11f,
                -.035f-p.Stride,-.345f+p.Fold*.16f, .04f);
            Limb(d, p, .145f,.1f, .23f-p.Stride+p.Reach*.04f,-.1f+p.Fold*.08f,
                .15f-p.Stride+p.Reach*.085f,-.34f+p.Fold*.18f, .036f);
            Limb(d, p, .09f,.07f, .055f+p.Stride+p.Reach*.05f,-.11f+p.Fold*.08f,
                .265f+p.Stride+p.Reach*.06f,-.36f+p.Fold*.20f, .043f);
            d.Path(NotebookInkChannel.Surface, .055f, 1, -.22f,.16f, -.34f,.22f, -.42f,.32f);
            d.Path(NotebookInkChannel.Ivory, .017f, .9f, -.22f,.185f, -.34f,.245f, -.42f,.32f);
            Mass(d, -.055f, .115f, .25f, .47f);
            Mass(d, .13f + neckX, .17f, .105f, 1.03f);
            RunnerFlank(d);
            d.Path(NotebookInkChannel.Ivory, p.Edge, 1,
                -.29f,.10f, -.26f,.195f, -.16f,.24f, -.05f,.215f, .05f,.245f,
                .095f+neckX,.30f, .155f+neckX,.25f);
            d.Path(NotebookInkChannel.Ivory, p.Edge, 1,
                -.24f,.035f, -.13f,-.005f, .045f,.025f, .14f+neckX,.09f);
            Mass(d, .27f + neckX, headY, .115f, .80f);
            d.Fill(NotebookInkChannel.Surface, 1,
                .29f+neckX,headY+.04f, .40f+neckX,headY-.01f,
                .37f+neckX,headY-.07f, .27f+neckX,headY-.075f);
            d.Path(NotebookInkChannel.Ivory, p.Edge, 1,
                .17f+neckX,headY+.04f, .18f+neckX,headY+.155f, .25f+neckX,headY+.085f,
                .32f+neckX,headY+.075f, .40f+neckX,headY-.01f, .37f+neckX,headY-.07f,
                .28f+neckX,headY-.09f, .18f+neckX,headY-.045f);
            Eye(d, .29f + neckX, headY + .018f, .021f, p.Fold);
            d.Line(NotebookInkChannel.Ivory, .34f+neckX,headY-.035f,
                .385f+neckX,headY-.03f-p.Reach*.024f, .014f);
            d.Path(NotebookInkChannel.Sketch, .013f, .75f, .05f,.15f, .10f,.19f, .145f,.155f, .115f,.09f);
            Hatch(d, p, -.18f, .14f, .03f, 3);
        }

        static void Gnawer(BattleInkDrawing d, Pose p)
        {
            float jawX = p.Reach * .06f - p.Flinch * .045f;
            float gape = p.Motion == NotebookEnemyMotion.Attack ? p.Phase == 2 ? -.025f : .065f : .025f - p.Fold * .04f;
            Limb(d, p, -.22f,-.015f, -.31f+p.Stride,-.12f,
                -.37f+p.Stride,-.29f+p.Fold*.11f, .055f);
            Limb(d, p, -.10f,-.08f, -.09f-p.Stride,-.19f,
                .0f-p.Stride,-.29f+p.Fold*.11f, .058f);
            Limb(d, p, .09f,-.04f, .17f+p.Stride,-.17f,
                .25f+p.Stride+p.Reach*.04f,-.30f+p.Fold*.13f, .047f);
            Mass(d, -.16f, .02f, .21f, .74f);
            Mass(d, .065f+jawX, .035f, .20f, .92f);
            GnawerHide(d);
            d.Path(NotebookInkChannel.Ivory, p.Edge, 1,
                -.345f,.005f, -.32f,.13f, -.23f,.195f, -.17f,.15f,
                -.11f,.21f, -.025f,.175f, .035f+jawX,.21f);
            // The hollow, oversized gape is two open jaw contours, never a filled concave fan.
            d.Fill(NotebookInkChannel.Surface, 1,
                -.01f+jawX,.12f, .10f+jawX,.24f+gape, .26f+jawX,.22f+gape,
                .37f+jawX,.09f+gape, .16f+jawX,.075f);
            d.Path(NotebookInkChannel.Ivory, p.Edge+.003f, 1,
                -.02f+jawX,.12f, .09f+jawX,.25f+gape, .24f+jawX,.245f+gape,
                .34f+jawX,.17f+gape, .385f+jawX,.075f+gape,
                .30f+jawX,.11f+gape, .20f+jawX,.135f+gape, .06f+jawX,.095f);
            d.Fill(NotebookInkChannel.Surface, 1,
                -.015f+jawX,-.075f, .15f+jawX,-.20f-gape,
                .31f+jawX,-.17f-gape, .36f+jawX,-.075f-gape);
            d.Path(NotebookInkChannel.Ivory, p.Edge+.003f, 1,
                -.025f+jawX,-.04f, .025f+jawX,-.19f-gape, .15f+jawX,-.245f-gape,
                .29f+jawX,-.20f-gape, .365f+jawX,-.07f-gape,
                .26f+jawX,-.13f-gape, .09f+jawX,-.12f-gape, .02f+jawX,-.045f);
            d.Path(NotebookInkChannel.Ivory, .014f, 1,
                .07f+jawX,.105f, .09f+jawX,.045f, .12f+jawX,.115f,
                .16f+jawX,.045f, .19f+jawX,.13f+gape, .23f+jawX,.065f,
                .27f+jawX,.12f+gape, .30f+jawX,.06f+gape);
            d.Path(NotebookInkChannel.Ivory, .014f, 1,
                .075f+jawX,-.12f-gape, .105f+jawX,-.055f-gape, .13f+jawX,-.14f-gape,
                .18f+jawX,-.06f-gape, .21f+jawX,-.145f-gape, .26f+jawX,-.07f-gape, .285f+jawX,-.13f-gape);
            Eye(d, .055f+jawX, .165f+gape, .018f, p.Fold);
            Hatch(d, p, -.26f, .075f, .03f, 3);
        }

        static void Spitter(BattleInkDrawing d, Pose p)
        {
            float sac = p.Motion == NotebookEnemyMotion.Attack && p.Phase < 2 ? .025f : 0;
            float neck = p.Reach * .045f - p.Flinch * .06f;
            float noseY = .12f - p.Reach * .065f + p.Flinch * .045f;
            Limb(d, p, -.17f,-.13f, -.25f+p.Stride,-.23f,
                -.30f+p.Stride,-.34f+p.Fold*.13f, .057f);
            Limb(d, p, .015f,-.15f, .07f-p.Stride,-.25f,
                .15f-p.Stride,-.34f+p.Fold*.15f, .06f);
            Mass(d, -.185f, .22f, .155f+sac, 1.02f);
            SpitterGland(d, sac);
            d.Path(NotebookInkChannel.Ivory, p.Edge, 1,
                -.28f-sac,.08f, -.345f-sac,.205f, -.31f-sac,.32f,
                -.21f,.395f+sac, -.10f+sac,.365f+sac, -.035f+sac,.26f,
                -.055f+sac,.13f);
            Mass(d, -.09f, -.085f, .19f, 1.14f);
            d.Fill(NotebookInkChannel.Body, .52f, -.23f,.08f, -.105f,.015f, -.075f,-.12f, -.13f,-.22f, -.225f,-.13f);
            d.Path(NotebookInkChannel.Ivory, p.Edge, 1,
                -.23f,.105f, -.29f,-.035f, -.23f,-.22f, -.10f,-.295f,
                .035f,-.225f, .095f,-.075f, .045f,.095f);
            Mass(d, .055f+neck, .115f, .105f, 1.08f);
            d.Fill(NotebookInkChannel.Surface, 1,
                .055f+neck,.19f, .20f+neck,noseY+.045f,
                .26f+neck,noseY-.055f, .075f+neck,.045f);
            d.Fill(NotebookInkChannel.Surface, 1,
                .20f+neck,noseY+.045f, .395f+neck,noseY-.025f,
                .405f+neck,noseY-.08f, .26f+neck,noseY-.055f);
            d.Path(NotebookInkChannel.Ivory, p.Edge, 1,
                -.015f+neck,.17f, .035f+neck,.235f, .115f+neck,.205f,
                .22f+neck,noseY+.045f, .395f+neck,noseY-.025f,
                .405f+neck,noseY-.08f, .31f+neck,noseY-.055f,
                .14f+neck,noseY-.035f, .075f+neck,.045f);
            d.Line(NotebookInkChannel.Ivory, .377f+neck,noseY-.028f, .385f+neck,noseY-.07f, .014f);
            Eye(d, .075f+neck, .158f, .018f, p.Fold);
            d.Ellipse(NotebookInkChannel.Sketch, -.19f,.24f,.10f,1.03f,.013f,.82f,12);
            d.Path(NotebookInkChannel.Sketch, .012f, .75f, -.17f,.17f, -.12f,.22f, -.135f,.29f);
            d.Line(NotebookInkChannel.Danger, -.225f,.285f, -.195f,.303f, .027f, .7f);
            d.Line(NotebookInkChannel.Sketch, -.16f,-.08f, -.1f,-.13f, .014f, .75f);
            Hatch(d, p, -.18f, -.12f, .027f, 3);
        }

        static void Brute(BattleInkDrawing d, Pose p)
        {
            float punch = p.Reach;
            Limb(d, p, -.12f,-.17f, -.16f+p.Stride,-.28f+p.Fold*.07f,
                -.21f+p.Stride,-.405f+p.Fold*.16f, .09f);
            Limb(d, p, .12f,-.17f, .16f-p.Stride,-.28f+p.Fold*.07f,
                .22f-p.Stride,-.405f+p.Fold*.16f, .095f);
            Limb(d, p, -.23f,.11f, -.315f,-.035f+p.Flinch*.06f,
                -.31f+p.Fold*.11f,-.19f+p.Fold*.09f, .105f, false);
            Limb(d, p, .22f,.10f, .30f+punch*.025f,-.035f+punch*.09f,
                .31f+punch*.035f-p.Fold*.10f,-.19f+punch*.23f, .11f, false);
            Mass(d, 0,.015f,.285f,1.03f);
            Mass(d, -.215f,.13f,.145f,1.0f);
            Mass(d, .215f,.13f,.145f,1.0f);
            BruteShoulders(d);
            d.Path(NotebookInkChannel.Ivory, p.Edge+.002f, 1,
                -.32f,.07f, -.35f,.18f, -.29f,.285f, -.17f,.305f,
                -.10f,.34f, .105f,.34f, .19f,.30f, .29f,.275f, .355f,.16f, .32f,.065f);
            d.Path(NotebookInkChannel.Ivory, p.Edge, 1,
                -.23f,-.115f, -.17f,-.245f, -.035f,-.28f, .13f,-.245f, .235f,-.115f);
            float headX = .025f + punch * .025f - p.Flinch * .045f;
            Mass(d, headX,.285f,.145f,.94f);
            d.Path(NotebookInkChannel.Ivory, p.Edge, 1,
                headX-.14f,.29f, headX-.115f,.395f, headX-.03f,.435f,
                headX+.085f,.41f, headX+.15f,.32f, headX+.12f,.22f,
                headX+.04f,.18f, headX-.07f,.205f);
            Eye(d, headX+.025f,.30f,.02f,p.Fold);
            Eye(d, headX+.10f,.30f,.015f,p.Fold);
            d.Path(NotebookInkChannel.Ivory, .016f, .9f, headX+.005f,.245f, headX+.09f,.23f, headX+.115f,.25f);
            Fist(d, p, -.31f+p.Fold*.11f,-.20f+p.Fold*.09f,.105f);
            Fist(d, p, .31f+punch*.035f-p.Fold*.10f,-.20f+punch*.23f,.115f);
            d.Path(NotebookInkChannel.Sketch, .014f, .8f, -.17f,.1f, -.06f,.035f, .065f,.035f, .16f,.09f);
            Hatch(d, p, -.1f,-.09f,.033f,3);
        }

        static void Bulwark(BattleInkDrawing d, Pose p)
        {
            float headX = p.Reach * .045f - p.Flinch * .035f;
            Limb(d,p,-.22f,-.04f,-.31f+p.Stride,-.13f,
                -.36f+p.Stride,-.28f+p.Fold*.10f,.043f);
            Limb(d,p,-.04f,-.08f,-.12f-p.Stride,-.18f,
                -.10f-p.Stride,-.30f+p.Fold*.12f,.048f);
            Limb(d,p,.145f,-.065f,.23f+p.Stride,-.16f,
                .32f+p.Stride,-.27f+p.Fold*.10f,.048f);
            Mass(d,-.045f,.015f,.285f,.74f);
            BulwarkShell(d);
            d.Path(NotebookInkChannel.Ivory,p.Edge+.002f,1,
                -.345f,-.075f, -.30f,.055f, -.33f,.12f, -.205f,.16f,
                -.23f,.23f, -.07f,.24f, .025f,.295f, .125f,.25f,
                .235f,.15f, .285f,.025f, .23f,-.145f,
                .09f,-.205f, -.10f,-.19f, -.265f,-.145f, -.345f,-.075f);
            // Scalloped, overlapping carapace plates are body seams, with no aura or shield ring.
            d.Path(NotebookInkChannel.Ivory,.018f,1,
                -.205f,.16f, -.17f,.10f, -.19f,.025f, -.27f,-.035f, -.31f,-.07f);
            d.Path(NotebookInkChannel.Ivory,.019f,1,
                -.07f,.24f, -.035f,.12f, -.075f,.035f, -.15f,-.025f, -.21f,-.105f);
            d.Path(NotebookInkChannel.Ivory,.019f,1,
                .07f,.265f, .13f,.135f, .10f,.045f, .02f,-.015f, -.02f,-.14f);
            Mass(d,.24f+headX,-.115f,.105f,1.0f);
            d.Path(NotebookInkChannel.Ivory,p.Edge,1,
                .175f+headX,-.055f, .26f+headX,-.025f, .335f+headX,-.09f,
                .335f+headX,-.175f, .26f+headX,-.22f, .18f+headX,-.175f);
            Eye(d,.285f+headX,-.105f,.017f,p.Fold);
            d.Path(NotebookInkChannel.Ivory,.016f,1,
                .31f+headX,-.17f, .365f+headX,-.205f, .34f+headX,-.235f);
            d.Path(NotebookInkChannel.Ivory,.016f,1,
                .26f+headX,-.205f, .28f+headX,-.26f, .31f+headX,-.245f);
            Hatch(d,p,-.15f,.09f,.027f,3);
        }

        static void Mass(BattleInkDrawing d, float x, float y, float radius, float aspect)
            => d.Ellipse(NotebookInkChannel.Surface,x,y,radius,aspect,0,1,14);

        // Broad, directional ink washes sit over the opaque body. These same facets survive
        // in the crowd silhouettes while the thin hatching is reserved for the close view.
        static void DoodleChest(BattleInkDrawing d)
            => d.Fill(NotebookInkChannel.Body, .68f, -.13f,.065f, .085f,.036f, .045f,-.04f, -.065f,-.16f, -.105f,-.12f);

        static void DoodleCrest(BattleInkDrawing d, float x, float y)
        {
            d.Fill(NotebookInkChannel.Body,.82f, x-.085f,y+.205f, x+.105f,y+.102f,
                x+.044f,y+.038f, x-.08f,y+.057f);
            d.Fill(NotebookInkChannel.Body,.53f, x-.155f,y+.09f, x-.071f,y+.079f, x-.073f,y-.082f, x-.116f,y-.095f);
        }

        static void DoodleEyes(BattleInkDrawing d, float x, float y, Pose p)
        {
            if (p.Fold > .6f)
            {
                Eye(d, x+.026f, y+.012f, .032f, p.Fold);
                Eye(d, x+.11f, y+.019f, .022f, p.Fold);
                return;
            }
            d.Fill(NotebookInkChannel.Ivory, 1, x-.017f,y+.036f, x+.060f,y+.017f, x+.034f,y-.023f);
            d.Fill(NotebookInkChannel.Ivory, 1, x+.086f,y+.018f, x+.138f,y+.044f, x+.109f,y-.009f);
        }

        static void RunnerFlank(BattleInkDrawing d)
            => d.Fill(NotebookInkChannel.Body, .72f, -.265f,.167f, -.167f,.215f, .022f,.195f,
                .10f,.17f, -.046f,.104f, -.212f,.076f);

        static void GnawerHide(BattleInkDrawing d)
            => d.Fill(NotebookInkChannel.Body, .69f, -.333f,.04f, -.259f,.148f, -.175f,.154f,
                -.107f,.095f, -.166f,.022f, -.29f,-.03f);

        static void SpitterGland(BattleInkDrawing d, float swell)
        {
            d.Fill(NotebookInkChannel.Danger,.59f, -.31f-swell,.23f, -.215f,.368f+swell,
                -.128f+swell,.335f+swell, -.09f+swell,.235f, -.164f,.157f, -.255f,.158f);
        }

        static void BruteShoulders(BattleInkDrawing d)
        {
            d.Fill(NotebookInkChannel.Body,.76f, -.33f,.145f, -.26f,.265f, -.115f,.285f, -.014f,.16f, -.09f,.065f);
            d.Fill(NotebookInkChannel.Body,.56f, .065f,.145f, .185f,.275f, .29f,.245f, .318f,.135f, .17f,.073f);
        }

        static void BulwarkShell(BattleInkDrawing d)
        {
            d.Fill(NotebookInkChannel.Body,.77f, -.197f,.148f, -.082f,.221f, -.049f,.124f,
                -.088f,.047f, -.192f,-.035f, -.161f,.068f);
            d.Fill(NotebookInkChannel.Body,.55f, -.031f,.218f, .066f,.249f, .107f,.139f,
                .084f,.061f, -.012f,-.034f, .021f,.088f);
        }

        static void Eye(BattleInkDrawing d, float x, float y, float radius, float fold)
        {
            if (fold > .6f)
                d.Line(NotebookInkChannel.Sketch,x-radius,y,x+radius,y-.008f,.014f,.85f);
            else
                d.Line(NotebookInkChannel.Ivory,x-radius*.28f,y-radius*.69f,x+radius*.35f,y+radius*.69f,radius*1.48f);
        }

        static void Fist(BattleInkDrawing d, Pose p, float x, float y, float radius)
        {
            Mass(d,x,y,radius,.98f);
            d.Fill(NotebookInkChannel.Body,.63f, x-radius*.85f,y+radius*.45f, x-radius*.14f,y+radius*.85f,
                x+radius*.59f,y+radius*.57f, x+radius*.26f,y+.04f*radius, x-radius*.56f,y-.36f*radius);
            d.Path(NotebookInkChannel.Ivory,p.Edge,1,
                x-radius*.8f,y+radius*.55f, x-radius*.2f,y+radius,
                x+radius*.55f,y+radius*.8f, x+radius,y+radius*.1f,
                x+radius*.7f,y-radius*.8f, x-radius*.2f,y-radius,
                x-radius*.9f,y-radius*.35f, x-radius*.8f,y+radius*.55f);
            d.Path(NotebookInkChannel.Sketch,.014f,.85f,
                x-radius*.45f,y+radius*.23f, x-radius*.10f,y-radius*.2f,
                x+radius*.2f,y+radius*.16f, x+radius*.5f,y-radius*.2f);
        }

        static void Limb(BattleInkDrawing d, Pose p, float ax, float ay, float bx, float by,
            float cx, float cy, float width, bool claws = true)
        {
            var a = new Vector2(ax,ay); var b = new Vector2(bx,by); var c = new Vector2(cx,cy);
            Vector2 ab = (b-a).normalized, bc = (c-b).normalized;
            var n1 = new Vector2(-ab.y,ab.x)*width*.5f;
            var n2 = new Vector2(-bc.y,bc.x)*width*.43f;
            Vector2 joint = (n1+n2)*.5f;
            // Each fill is a convex limb segment; the bent outer contour is stroke-only.
            d.Fill(NotebookInkChannel.Surface,1,
                a.x+n1.x,a.y+n1.y, b.x+joint.x,b.y+joint.y,
                b.x-joint.x,b.y-joint.y, a.x-n1.x,a.y-n1.y);
            d.Fill(NotebookInkChannel.Surface,1,
                b.x+joint.x,b.y+joint.y, c.x+n2.x,c.y+n2.y,
                c.x-n2.x,c.y-n2.y, b.x-joint.x,b.y-joint.y);
            d.Path(NotebookInkChannel.Ivory,p.Edge*.85f,.95f,
                a.x+n1.x,a.y+n1.y, b.x+joint.x,b.y+joint.y, c.x+n2.x,c.y+n2.y,
                c.x-n2.x,c.y-n2.y, b.x-joint.x,b.y-joint.y, a.x-n1.x,a.y-n1.y);
            if (claws)
                d.Path(NotebookInkChannel.Ivory,.014f,.94f,
                    cx-.025f,cy+.005f, cx-.015f,cy-.015f, cx+.025f,cy-.018f,
                    cx+.017f,cy+.002f, cx+.055f,cy-.01f);
        }

        static void Hatch(BattleInkDrawing d, Pose p, float x, float y, float length, int count)
        {
            int marks = p.Tier == 0 ? 1 : p.Tier == 1 ? 2 : count;
            for (int i=0;i<marks;i++)
                d.Line(NotebookInkChannel.Sketch,x+i*.026f,y,x+i*.026f+length,y+length*.8f,.012f,.76f);
            if (p.Tier == 2)
                d.Line(NotebookInkChannel.Sketch,x+.006f,y+length*.8f,x+length+.04f,y-.012f,.010f,.58f);
        }

        static void FinishPose(NotebookArtShape shape, Pose pose)
        {
            float flatten = pose.Motion == NotebookEnemyMotion.Death ? 1-pose.Fold*.62f : 1;
            float sink = pose.Motion == NotebookEnemyMotion.Death ? -.08f-pose.Phase*.065f : pose.Bob;
            float widen = pose.Motion == NotebookEnemyMotion.Death ? 1+pose.Phase*.035f : 1;
            foreach (var mark in shape.Marks)
            {
                for (int i=0;i<mark.Points.Length;i++)
                {
                    Vector2 point = mark.Points[i];
                    mark.Points[i] = new Vector2(point.x*widen,point.y*flatten+sink);
                }
                mark.Center = new Vector2(mark.Center.x*widen,mark.Center.y*flatten+sink);
                mark.Radius *= widen;
                mark.Aspect *= flatten/widen;
            }
        }
    }
}
