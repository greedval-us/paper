using System;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    internal static partial class InkSwarmEnemyArt
    {
        static readonly Dictionary<string, NotebookArtShape> CompactShapes = new Dictionary<string, NotebookArtShape>(StringComparer.Ordinal);

        // At a few pixels tall the hood, light face and two marching feet carry the
        // imp silhouette. Solid facets avoid repeatedly tracing subpixel outlines.
        internal static void DrawDistantDoodle(MeshPen pen, Vector2 at, float size,
            NotebookEnemyMotion motion, float phase, NotebookVisualThemeAsset theme, EnemyFamily family = EnemyFamily.Ink)
        {
            float step = motion == NotebookEnemyMotion.Walk && !theme.Data.ReducedMotion
                ? Mathf.Sin(phase * (2 * Mathf.PI / .52f)) : 0;
            float lean = motion == NotebookEnemyMotion.Attack ? .12f : 0;
            float head = .025f + lean;
            float rise = Mathf.Abs(step) * .028f;
            var edge = theme.Color(NotebookInkChannel.Ivory);
            var body = theme.Color(NotebookInkChannel.Sketch);
            var shade = theme.Color(NotebookInkChannel.Surface);
            var crest = motion == NotebookEnemyMotion.Hit ? theme.Color(NotebookInkChannel.Danger) : edge;
            Vector2 Point(float x, float y) => at + new Vector2(x * 1.30f, (y + rise) * 1.10f) * size;

            if (family == EnemyFamily.Undead)
            {
                pen.Triangle(Point(-.10f,-.09f), Point(-.035f,-.10f), Point(-.14f-.10f*step,-.39f), edge);
                pen.Triangle(Point(.025f,-.10f), Point(.10f,-.09f), Point(.16f+.10f*step,-.36f), edge);
                pen.Quad(Point(-.14f,.105f), Point(.12f,.12f), Point(.06f,-.13f), Point(-.055f,-.14f), body);
                pen.Triangle(Point(-.14f,.08f), Point(.11f,.09f), Point(-.01f,.02f), edge);
                pen.Triangle(Point(-.11f,-.012f), Point(.08f,-.005f), Point(-.015f,-.07f), edge);
                pen.Quad(Point(head-.16f,.36f), Point(head+.14f,.36f), Point(head+.11f,.16f), Point(head-.11f,.14f), crest);
                pen.Triangle(Point(head-.025f,.28f), Point(head+.12f,.28f), Point(head+.045f,.215f), shade);
                pen.Triangle(Point(-.12f,.12f), Point(-.08f,.03f), Point(-.25f,-.07f), theme.Color(NotebookInkChannel.Violet));
                return;
            }
            if (family == EnemyFamily.Clockwork)
            {
                float stride = .09f * step;
                pen.Quad(Point(-.14f,-.12f), Point(-.055f,-.11f), Point(-.09f-stride,-.33f), Point(-.22f-stride,-.33f), body);
                pen.Quad(Point(.05f,-.12f), Point(.14f,-.12f), Point(.22f+stride,-.30f), Point(.09f+stride,-.30f), edge);
                // The press descends onto the front pad at the strike key, while its feet stay planted.
                if (motion == NotebookEnemyMotion.Attack && !theme.Data.ReducedMotion)
                    rise += phase < .24f * .7f ? .035f : -.10f;
                pen.Quad(Point(-.16f,.13f), Point(.17f,.13f), Point(.21f,-.14f), Point(-.19f,-.14f), theme.Color(NotebookInkChannel.Copper));
                pen.Quad(Point(-.22f,-.11f), Point(.24f,-.11f), Point(.24f,-.175f), Point(-.22f,-.175f), crest);
                pen.Quad(Point(-.045f,.14f), Point(.02f,.14f), Point(.02f,.31f), Point(-.045f,.31f), edge);
                pen.Quad(Point(-.16f,.35f), Point(.14f,.35f), Point(.105f,.26f), Point(-.13f,.26f), theme.Color(NotebookInkChannel.Amber));
                pen.Triangle(Point(.055f,.06f), Point(.17f,.06f), Point(.105f,-.01f), shade);
                return;
            }
            pen.Triangle(Point(-.13f,-.10f), Point(.00f,-.11f), Point(-.17f - .075f * step,-.38f), body);
            pen.Triangle(Point(-.01f,-.13f), Point(.13f,-.09f), Point(.16f + .075f * step,-.35f), edge);
            pen.Triangle(Point(-.09f,.10f), Point(-.04f,.01f), Point(-.27f,-.12f), body);
            pen.Quad(Point(-.15f,.12f), Point(.14f,.11f), Point(.12f,-.12f), Point(-.085f,-.21f), body);
            pen.Quad(Point(head-.18f,.29f), Point(head-.085f,.42f), Point(head+.18f,.29f), Point(head+.04f,.14f), crest);
            pen.Quad(Point(head-.18f,.29f), Point(head+.18f,.29f), Point(head+.11f,.09f), Point(head-.07f,.08f), body);
            pen.Triangle(Point(head-.065f,.25f), Point(head+.16f,.25f), Point(head+.045f,.13f), shade);
        }

        /// <summary>Bounded crowd LOD. Shares authored pose phases, not the full-detail tessellation.</summary>
        internal static NotebookArtShape CreateCompact(string id)
        {
            if (id == null || id.EndsWith(".compact", StringComparison.Ordinal)) return null;
            return CreatePose(id, true);
        }

        static void DrawCompactInk(BattleInkDrawing d, int role, Pose p)
        {
            switch (role)
            {
                case 0: CompactDoodle(d, p); break;
                case 1: CompactRunner(d, p); break;
                case 2: CompactGnawer(d, p); break;
                case 3: CompactSpitter(d, p); break;
                case 4: CompactBrute(d, p); break;
                default: CompactBulwark(d, p); break;
            }
        }

        static void CompactDoodle(BattleInkDrawing d, Pose p)
        {
            float x = .025f+p.Reach*.06f-p.Flinch*.055f;
            float y = .19f-Mathf.Abs(p.Reach)*.025f;
            CompactLimb(d,p,-.085f,-.13f,-.13f-p.Stride,-.24f+p.Fold*.08f,-.18f-p.Stride,-.35f+p.Fold*.16f,.064f);
            CompactLimb(d,p,.07f,-.13f,.1f+p.Stride,-.25f+p.Fold*.09f,.16f+p.Stride,-.35f+p.Fold*.15f,.072f);
            CompactLimb(d,p,-.09f,.04f,-.21f-p.Reach*.025f,-.055f,-.24f+p.Fold*.13f,-.18f+p.Flinch*.08f,.057f);
            CompactLimb(d,p,.12f,.035f,.21f+p.Reach*.055f,-.03f+p.Reach*.07f,.25f+p.Reach*.075f-p.Fold*.15f,-.14f+p.Reach*.16f,.067f);
            d.Fill(NotebookInkChannel.Surface,1,-.13f,.06f,.10f,.06f,.10f,-.14f,-.03f,-.20f);
            DoodleChest(d);
            d.Path(NotebookInkChannel.Ivory,p.Edge,1,-.13f,.035f,-.105f,-.15f,-.03f,-.20f,.09f,-.15f);
            d.Fill(NotebookInkChannel.Surface,1,x-.145f,y+.08f,x-.02f,y+.16f,x+.14f,y+.10f,x+.165f,y-.01f,x+.10f,y-.13f,x-.10f,y-.11f);
            DoodleCrest(d,x,y);
            // One sharp ear/horn break keeps the small imp separate from round heavy bodies.
            d.Path(NotebookInkChannel.Ivory,p.Edge,1,x-.145f,y-.03f,x-.17f,y+.09f,x-.075f,y+.11f,x-.09f,y+.22f,
                x+.14f,y+.10f,x+.165f,y-.01f,x+.10f,y-.13f,x-.10f,y-.11f);
            DoodleEyes(d,x,y,p);
        }

        static void CompactRunner(BattleInkDrawing d, Pose p)
        {
            float x = p.Reach*.045f-p.Flinch*.04f;
            float y = .215f-p.Reach*.055f+p.Flinch*.035f;
            CompactLimb(d,p,-.21f,.09f,-.27f+p.Stride,-.105f+p.Fold*.10f,-.36f+p.Stride,-.37f+p.Fold*.20f,.036f);
            CompactLimb(d,p,-.11f,.065f,-.13f-p.Stride,-.15f+p.Fold*.11f,-.035f-p.Stride,-.345f+p.Fold*.16f,.04f);
            CompactLimb(d,p,.145f,.1f,.23f-p.Stride+p.Reach*.04f,-.1f+p.Fold*.08f,.15f-p.Stride+p.Reach*.085f,-.34f+p.Fold*.18f,.036f);
            CompactLimb(d,p,.09f,.07f,.055f+p.Stride+p.Reach*.05f,-.11f+p.Fold*.08f,.265f+p.Stride+p.Reach*.06f,-.36f+p.Fold*.20f,.043f);
            d.Path(NotebookInkChannel.Surface,.055f,1,-.22f,.16f,-.34f,.22f,-.42f,.32f);
            d.Path(NotebookInkChannel.Ivory,.017f,.9f,-.22f,.185f,-.34f,.245f,-.42f,.32f);
            d.Fill(NotebookInkChannel.Surface,1,-.28f,.08f,-.24f,.20f,-.10f,.22f,.12f,.20f,.16f,.10f,-.10f,.02f);
            RunnerFlank(d);
            d.Path(NotebookInkChannel.Ivory,p.Edge,1,-.28f,.08f,-.24f,.20f,-.10f,.22f,.12f,.20f,.155f+x,.28f);
            d.Fill(NotebookInkChannel.Surface,1,.17f+x,y+.04f,.25f+x,y+.085f,.40f+x,y-.01f,.28f+x,y-.09f);
            d.Path(NotebookInkChannel.Ivory,p.Edge,1,.17f+x,y+.04f,.18f+x,y+.155f,.25f+x,y+.085f,
                .40f+x,y-.01f,.28f+x,y-.09f,.18f+x,y-.045f);
            Eye(d,.29f+x,y+.018f,.021f,p.Fold);
        }

        static void CompactGnawer(BattleInkDrawing d, Pose p)
        {
            float x = p.Reach*.06f-p.Flinch*.045f;
            float gape = p.Motion == NotebookEnemyMotion.Attack ? p.Phase == 2 ? -.025f : .065f : .025f-p.Fold*.04f;
            CompactLimb(d,p,-.22f,-.015f,-.31f+p.Stride,-.12f,-.37f+p.Stride,-.29f+p.Fold*.11f,.055f);
            CompactLimb(d,p,-.10f,-.08f,-.09f-p.Stride,-.19f,-p.Stride,-.29f+p.Fold*.11f,.058f);
            CompactLimb(d,p,.09f,-.04f,.17f+p.Stride,-.17f,.25f+p.Stride+p.Reach*.04f,-.30f+p.Fold*.13f,.047f);
            d.Fill(NotebookInkChannel.Surface,1,-.345f,.005f,-.26f,.175f,-.025f,.175f,.065f,-.12f);
            GnawerHide(d);
            d.Path(NotebookInkChannel.Ivory,p.Edge,1,-.345f,.005f,-.26f,.175f,-.11f,.21f,.035f+x,.21f);
            d.Fill(NotebookInkChannel.Surface,1,-.01f+x,.12f,.10f+x,.24f+gape,.26f+x,.22f+gape,.37f+x,.09f+gape);
            d.Fill(NotebookInkChannel.Surface,1,-.015f+x,-.075f,.15f+x,-.20f-gape,.31f+x,-.17f-gape,.36f+x,-.075f-gape);
            d.Path(NotebookInkChannel.Ivory,p.Edge+.003f,1,-.02f+x,.12f,.09f+x,.25f+gape,.24f+x,.245f+gape,
                .385f+x,.075f+gape,.20f+x,.135f+gape,.06f+x,.095f);
            d.Path(NotebookInkChannel.Ivory,p.Edge+.003f,1,-.025f+x,-.04f,.025f+x,-.19f-gape,.15f+x,-.245f-gape,
                .365f+x,-.07f-gape,.26f+x,-.13f-gape,.09f+x,-.12f-gape);
            // Two widely spaced teeth on each jaw survive the smallest crowd view.
            d.Path(NotebookInkChannel.Ivory,.014f,1,.09f+x,.105f,.13f+x,.035f,.19f+x,.13f+gape,.24f+x,.05f+gape);
            d.Path(NotebookInkChannel.Ivory,.014f,1,.09f+x,-.12f-gape,.14f+x,-.045f-gape,.20f+x,-.14f-gape,.27f+x,-.065f-gape);
            Eye(d,.055f+x,.165f+gape,.018f,p.Fold);
        }

        static void CompactSpitter(BattleInkDrawing d, Pose p)
        {
            float sac = p.Motion == NotebookEnemyMotion.Attack && p.Phase < 2 ? .025f : 0;
            float x = p.Reach*.045f-p.Flinch*.06f;
            float y = .12f-p.Reach*.065f+p.Flinch*.045f;
            CompactLimb(d,p,-.17f,-.13f,-.25f+p.Stride,-.23f,-.30f+p.Stride,-.34f+p.Fold*.13f,.057f);
            CompactLimb(d,p,.015f,-.15f,.07f-p.Stride,-.25f,.15f-p.Stride,-.34f+p.Fold*.15f,.06f);
            d.Fill(NotebookInkChannel.Surface,1,-.345f-sac,.205f,-.21f,.395f+sac,-.10f+sac,.365f+sac,-.035f+sac,.15f,-.22f,.09f);
            // Keep the swollen gland and its two-tone silhouette under the crowd budget,
            // including the status ink and attack warning that accompany this pose.
            d.Fill(NotebookInkChannel.Danger,.59f,-.31f-sac,.23f,-.215f,.368f+sac,
                -.096f+sac,.245f,-.195f,.153f);
            d.Path(NotebookInkChannel.Ivory,p.Edge,1,-.28f-sac,.08f,-.345f-sac,.205f,-.21f,.395f+sac,
                -.10f+sac,.365f+sac,-.035f+sac,.15f);
            d.Fill(NotebookInkChannel.Surface,1,-.23f,.105f,-.29f,-.035f,-.10f,-.295f,.095f,-.075f,.045f,.095f);
            d.Fill(NotebookInkChannel.Body,.52f,-.23f,.08f,-.105f,.015f,-.13f,-.22f);
            d.Path(NotebookInkChannel.Ivory,p.Edge,1,-.23f,.105f,-.29f,-.035f,-.10f,-.295f,.095f,-.075f,.045f,.095f);
            d.Fill(NotebookInkChannel.Surface,1,.015f+x,.20f,.395f+x,y-.025f,.405f+x,y-.08f,.075f+x,.045f);
            d.Path(NotebookInkChannel.Ivory,p.Edge,1,.015f+x,.20f,.20f+x,y+.045f,.395f+x,y-.025f,
                .405f+x,y-.08f,.14f+x,y-.035f);
            Eye(d,.075f+x,.158f,.018f,p.Fold);
            d.Line(NotebookInkChannel.Danger,-.225f,.285f,-.195f,.303f,.027f,.7f);
        }

        static void CompactBrute(BattleInkDrawing d, Pose p)
        {
            float punch = p.Reach;
            CompactLimb(d,p,-.12f,-.17f,-.16f+p.Stride,-.28f+p.Fold*.07f,-.21f+p.Stride,-.405f+p.Fold*.16f,.09f);
            CompactLimb(d,p,.12f,-.17f,.16f-p.Stride,-.28f+p.Fold*.07f,.22f-p.Stride,-.405f+p.Fold*.16f,.095f);
            CompactLimb(d,p,-.23f,.11f,-.315f,-.035f+p.Flinch*.06f,-.31f+p.Fold*.11f,-.19f+p.Fold*.09f,.105f);
            CompactLimb(d,p,.22f,.10f,.30f+punch*.025f,-.035f+punch*.09f,.31f+punch*.035f-p.Fold*.10f,-.19f+punch*.23f,.11f);
            d.Fill(NotebookInkChannel.Surface,1,-.32f,.07f,-.26f,.285f,0,.36f,.29f,.275f,.32f,.065f,.13f,-.245f,-.17f,-.245f);
            d.Fill(NotebookInkChannel.Body,.76f,-.315f,.145f,-.24f,.265f,-.014f,.16f);
            d.Fill(NotebookInkChannel.Body,.56f,.065f,.145f,.185f,.275f,.308f,.135f);
            d.Path(NotebookInkChannel.Ivory,p.Edge+.002f,1,-.32f,.07f,-.26f,.285f,.29f,.275f,.32f,.065f);
            float x = .025f+punch*.025f-p.Flinch*.045f;
            d.Fill(NotebookInkChannel.Surface,1,x-.14f,.29f,x-.03f,.435f,x+.15f,.32f,x+.04f,.18f);
            d.Path(NotebookInkChannel.Ivory,p.Edge,1,x-.14f,.29f,x-.03f,.435f,x+.15f,.32f,x+.04f,.18f,x-.07f,.205f);
            CompactFist(d,p,-.31f+p.Fold*.11f,-.20f+p.Fold*.09f,.105f);
            CompactFist(d,p,.31f+punch*.035f-p.Fold*.10f,-.20f+punch*.23f,.115f);
            Eye(d,x+.065f,.30f,.023f,p.Fold);
        }

        static void CompactBulwark(BattleInkDrawing d, Pose p)
        {
            float x = p.Reach*.045f-p.Flinch*.035f;
            CompactLimb(d,p,-.22f,-.04f,-.31f+p.Stride,-.13f,-.36f+p.Stride,-.28f+p.Fold*.10f,.043f);
            CompactLimb(d,p,-.04f,-.08f,-.12f-p.Stride,-.18f,-.10f-p.Stride,-.30f+p.Fold*.12f,.048f);
            CompactLimb(d,p,.145f,-.065f,.23f+p.Stride,-.16f,.32f+p.Stride,-.27f+p.Fold*.10f,.048f);
            d.Fill(NotebookInkChannel.Surface,1,-.345f,-.075f,-.23f,.23f,.025f,.295f,.235f,.15f,.23f,-.145f,-.10f,-.19f);
            BulwarkShell(d);
            d.Path(NotebookInkChannel.Ivory,p.Edge+.002f,1,-.345f,-.075f,-.30f,.055f,-.33f,.12f,-.205f,.16f,
                -.23f,.23f,.025f,.295f,.235f,.15f,.285f,.025f);
            // Two plate breaks remain visible even after the small decorative seams disappear.
            d.Path(NotebookInkChannel.Ivory,.019f,1,-.07f,.24f,-.075f,.035f,-.21f,-.105f);
            d.Path(NotebookInkChannel.Ivory,.019f,1,.07f,.265f,.10f,.045f,-.02f,-.14f);
            d.Fill(NotebookInkChannel.Surface,1,.175f+x,-.055f,.26f+x,-.025f,.335f+x,-.09f,.26f+x,-.22f);
            d.Path(NotebookInkChannel.Ivory,p.Edge,1,.175f+x,-.055f,.26f+x,-.025f,.335f+x,-.09f,.26f+x,-.22f);
            Eye(d,.285f+x,-.105f,.017f,p.Fold);
            d.Path(NotebookInkChannel.Ivory,.016f,1,.31f+x,-.17f,.365f+x,-.205f,.34f+x,-.235f);
        }

        static void CompactLimb(BattleInkDrawing d, Pose p, float ax, float ay, float bx, float by, float cx, float cy, float width)
        {
            d.Path(NotebookInkChannel.Surface,width,1,ax,ay,bx,by,cx,cy);
            // A single lit edge preserves the bend without drawing both sides and tiny claws.
            d.Path(NotebookInkChannel.Ivory,p.Edge*.85f,.95f,ax-width*.25f,ay,bx-width*.25f,by,cx-width*.25f,cy);
        }

        static void CompactFist(BattleInkDrawing d, Pose p, float x, float y, float radius)
        {
            d.Fill(NotebookInkChannel.Body,.62f,x-radius,y+radius*.45f,x+radius*.35f,y+radius,
                x+radius,y-radius*.25f,x-radius*.35f,y-radius);
            d.Path(NotebookInkChannel.Ivory,p.Edge,1,x-radius,y+radius*.45f,x+radius*.35f,y+radius,
                x+radius,y-radius*.25f,x-radius*.35f,y-radius);
        }
    }
}
