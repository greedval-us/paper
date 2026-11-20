using UnityEngine;
using Ink = PaperDefense.Presentation.NotebookInkChannel;

namespace PaperDefense.Presentation
{
    internal static partial class InkSwarmEnemyArt
    {
        static void DrawClockwork(BattleInkDrawing d, int role, Pose pose, bool compact)
        {
            switch (role)
            {
                case 0: WalkingStamp(d, pose, compact); break;
                case 1: SpringHopper(d, pose, compact); break;
                case 2: HolePunch(d, pose, compact); break;
                case 3: PaperBlower(d, pose, compact); break;
                case 4: Flywheel(d, pose, compact); break;
                case 5: BinderClip(d, pose, compact); break;
            }
        }

        // Convex sheet metal panels have a dark face and a continuous folded edge.
        static void ClockPanel(BattleInkDrawing d, Pose p, params float[] points)
        {
            d.Fill(Ink.Surface, 1, points);
            d.Path(Ink.Ivory, p.Edge + .004f, 1, points);
            d.Line(Ink.Ivory, points[points.Length - 2], points[points.Length - 1],
                points[0], points[1], p.Edge + .004f);
        }

        static void ClockBoot(BattleInkDrawing d, float x, float mountY, float step, float fold)
        {
            float ankle = x + step;
            float sole = -.32f + Mathf.Max(0, step) * .65f + fold * .12f;
            d.Line(Ink.Copper, x, mountY, ankle, sole + .039f, .053f);
            d.Fill(Ink.Ivory, 1, ankle - .066f, sole, ankle - .045f, sole + .065f,
                ankle + .032f, sole + .066f, ankle + .088f, sole);
        }

        // Two open lobes keep the key recognizable in the crowd silhouette.
        static void ClockKey(BattleInkDrawing d, float x, float y, float turn, bool compact)
        {
            float arm = .08f - Mathf.Abs(turn) * .32f;
            d.Line(Ink.Copper, x, y - .13f, x, y, .037f);
            d.Line(Ink.Amber, x - arm * .62f, y - .016f, x + arm * .62f, y + .016f, .031f);
            int segments = compact ? 5 : 14;
            d.Ellipse(Ink.Copper, x - arm, y - .025f, .044f, 1.05f, .022f, 1, segments);
            d.Ellipse(Ink.Amber, x + arm, y + .025f, .044f, 1.05f, .022f, 1, segments);
        }

        // Armor tiers are physical stripes on the casing, visible in both levels of detail.
        static void ClockArmor(BattleInkDrawing d, Pose p, float x, float y)
        {
            for (int i = 0; i < p.Tier; i++)
                d.Line(Ink.Ivory, x + i * .065f, y, x + .041f + i * .065f, y + .066f, .021f);
        }

        static void WalkingStamp(BattleInkDrawing d, Pose p, bool compact)
        {
            float x = p.Reach * .075f - p.Flinch * .07f;
            float drop = .105f * p.Reach + .04f * p.Fold;
            ClockBoot(d, -.105f, -.13f, -p.Stride, p.Fold);
            ClockBoot(d, .103f, -.13f, p.Stride, p.Fold);
            ClockKey(d, x - .035f, .335f - drop, p.Stride + p.Flinch * .10f, compact);
            ClockPanel(d, p, x - .20f,.15f - drop, x - .13f,.245f - drop,
                x + .09f,.245f - drop, x + .192f,.16f - drop,
                x + .18f,-.033f - drop, x - .177f,-.033f - drop);
            d.Fill(Ink.Body, 1, x - .185f,.146f - drop, x - .123f,.227f - drop,
                x + .063f,.226f - drop, x + .028f,.153f - drop);
            d.Line(Ink.Danger, x + .082f,.126f - drop, x + .146f,.126f - drop, .035f);
            d.Line(Ink.Copper, x, .005f - drop, x, -.141f - drop, .093f);
            ClockPanel(d, p, x - .226f,-.108f - drop, x + .194f,-.108f - drop,
                x + .239f,-.181f - drop, x - .245f,-.181f - drop);
            ClockArmor(d, p, x - .14f, .023f - drop);
            if (!compact)
            {
                d.Box(Ink.Sketch, x - .132f,.034f - drop,.145f,.097f,.012f);
                d.Line(Ink.Amber, x - .20f,-.166f - drop, x + .192f,-.166f - drop, .014f);
            }
        }

        static void SpringHopper(BattleInkDrawing d, Pose p, bool compact)
        {
            float lift = p.Stride * .70f - p.Reach * .045f - p.Fold * .055f;
            float nose = p.Reach * .07f - p.Flinch * .06f;
            // Opposite coils extend and compress as the flea changes its lead foot.
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -.157f : .145f;
                float stride = i == 0 ? -p.Stride : p.Stride;
                float footY = -.317f + Mathf.Max(0, stride) * 1.5f;
                float top = .002f + lift;
                float span = top - footY;
                d.Path(Ink.Copper, .034f, 1, x,top, x - .052f,top - span * .26f,
                    x + .054f,top - span * .46f, x - .035f,top - span * .68f, x + stride,footY);
                d.Fill(Ink.Ivory, 1, x + stride - .042f,footY, x + stride,footY + .05f, x + stride + .112f,footY);
            }
            ClockKey(d, -.075f + nose, .312f + lift, p.Stride, compact);
            ClockPanel(d, p, -.277f + nose,.014f + lift, -.24f + nose,.123f + lift,
                .125f + nose,.237f + lift, .242f + nose,.184f + lift, .301f + nose,.046f + lift,
                -.174f + nose,-.024f + lift);
            d.Path(Ink.Body, .035f, 1, -.199f + nose,.04f + lift, -.177f + nose,.09f + lift,
                .072f + nose,.16f + lift, .03f + nose,.085f + lift, -.126f + nose,.049f + lift);
            d.Line(Ink.Danger, .192f + nose,.14f + lift, .207f + nose,.116f + lift, .037f);
            ClockArmor(d, p, -.054f + nose, .036f + lift);
            if (!compact) d.Line(Ink.Ivory, -.229f + nose,.092f + lift, .111f + nose,.202f + lift, .012f);
        }

        static void HolePunch(BattleInkDrawing d, Pose p, bool compact)
        {
            ClockBoot(d, -.194f, -.10f, -p.Stride, p.Fold);
            ClockBoot(d, .191f, -.10f, p.Stride, p.Fold);
            ClockPanel(d, p, -.385f,-.145f, -.349f,-.056f, .292f,-.056f, .349f,-.145f);
            d.Fill(Ink.Body, 1, -.31f,-.058f, -.271f,.153f, -.189f,.161f, -.132f,-.058f);

            // All moving parts turn about the same rear hinge. The *tips* of the two pins
            // reach the base on the impact frame; the mouth between lever and shoe stays open on windup.
            float angle = (21f - 28f * p.Reach + 9f * p.Flinch - 21f * p.Fold) * Mathf.Deg2Rad;
            var axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var normal = new Vector2(-axis.y, axis.x);
            var hinge = new Vector2(-.239f, .095f);
            Vector2 Lever(float along, float across) => hinge + axis * along + normal * across;
            var a = Lever(-.061f,-.036f);
            var b = Lever(-.048f,.053f);
            var c = Lever(.106f,.116f);
            var e = Lever(.528f,.112f);
            var f = Lever(.609f,.069f);
            var g = Lever(.591f,.015f);
            for (int i = 0; i < 2; i++)
            {
                var pin = Lever(.34f + .14f * i, .015f);
                d.Line(Ink.Copper, pin.x,pin.y, pin.x,pin.y - .104f, .053f);
            }
            ClockPanel(d, p, a.x,a.y, b.x,b.y, c.x,c.y, e.x,e.y, f.x,f.y, g.x,g.y);
            var glint = Lever(.15f,.074f);
            d.Line(Ink.Body, glint.x,glint.y, e.x,e.y, .042f);
            d.Ellipse(Ink.Amber, hinge.x,hinge.y, .046f, 1, 0, 1, compact ? 5 : 14);
            ClockArmor(d, p, -.075f, -.132f);
            if (!compact)
            {
                d.Ellipse(Ink.Ivory, hinge.x,hinge.y, .067f, 1, .016f);
                d.Ellipse(Ink.Danger, .277f,-.102f, .022f, 1, 0);
                if (p.Motion == NotebookEnemyMotion.Attack && p.Phase == 2)
                    d.Ellipse(Ink.Ivory, .402f,-.201f, .032f, .40f, 0);
            }
        }

        static void PaperBlower(BattleInkDrawing d, Pose p, bool compact)
        {
            float recoil = -.086f * p.Reach - .055f * p.Flinch;
            float bellY = .189f + .059f * p.Reach - .1f * p.Fold;
            ClockBoot(d, -.13f, -.115f, -p.Stride, p.Fold);
            ClockBoot(d, .09f, -.115f, p.Stride, p.Fold);
            ClockPanel(d, p, -.275f,-.028f, -.195f,.098f, .122f,.08f,
                .198f,-.048f, .126f,-.172f, -.205f,-.172f);
            // Rolled receipt at the back, flared paper chute at the front.
            d.Fill(Ink.Ivory, 1, -.35f + recoil,.295f, -.144f + recoil,.355f,
                -.084f + recoil,.281f, -.16f + recoil,.083f, -.285f + recoil,.117f);
            d.Path(Ink.Sketch, .025f, 1, -.322f + recoil,.258f, -.178f + recoil,.299f, -.159f + recoil,.254f);
            ClockPanel(d, p, -.049f + recoil,.03f, -.085f + recoil,.127f,
                .28f + recoil,bellY + .125f, .397f + recoil,bellY - .012f, .297f + recoil,bellY - .091f);
            d.Line(Ink.Copper, .28f + recoil,bellY + .117f, .385f + recoil,bellY - .013f, .038f);
            ClockArmor(d, p, -.109f,-.134f);
            if (!compact)
            {
                d.Ellipse(Ink.Body, -.235f + recoil,.25f, .049f, .76f, .015f);
                d.Line(Ink.Copper, -.238f,-.041f, .092f,-.085f, .023f);
                d.Line(Ink.Danger, .135f,-.008f, .159f,-.013f, .029f);
            }
        }

        static void Flywheel(BattleInkDrawing d, Pose p, bool compact)
        {
            float x = .077f + p.Reach * .065f - .062f * p.Flinch;
            float y = .012f - .045f * p.Fold;
            d.Fill(Ink.Surface, 1, -.347f,-.271f, -.26f,-.104f, .176f,-.08f, .326f,-.23f, .283f,-.292f);
            d.Path(Ink.Ivory, p.Edge, 1, -.33f,-.24f, -.253f,-.114f, -.10f,-.105f);
            d.Line(Ink.Copper, -.27f,-.16f, -.327f,.098f - p.Fold * .07f, .051f);
            d.Fill(Ink.Body, 1, -.403f,.052f - p.Fold * .07f, -.372f,.171f - p.Fold * .07f,
                -.28f,.142f - p.Fold * .07f, -.303f,.024f - p.Fold * .07f);
            d.Ellipse(Ink.Copper, -.282f,-.245f, .076f, 1, 0, 1, compact ? 6 : 14);
            d.Ellipse(Ink.Surface, x,y, .298f, 1, 0, 1, compact ? 10 : 24);
            d.Ellipse(Ink.Ivory, x,y, .303f, 1, .038f, 1, compact ? 10 : 24);
            float rotation = (90f + p.Stride * (40f / .075f) - p.Reach * 34f + p.Flinch * 22f + p.Fold * 28f) * Mathf.Deg2Rad;
            for (int i = 0; i < 3; i++)
            {
                float angle = rotation + i * Mathf.PI * 2 / 3;
                d.Line(Ink.Amber, x,y, x + .281f * Mathf.Cos(angle), y + .281f * Mathf.Sin(angle), .046f);
            }
            d.Ellipse(Ink.Copper, x,y, .053f, 1, 0, 1, compact ? 5 : 14);
            ClockArmor(d, p, -.157f,-.264f);
            if (!compact)
            {
                d.Ellipse(Ink.Copper, x,y, .275f, 1, .022f, 1, 24);
                d.Ellipse(Ink.Ivory, -.282f,-.245f, .055f, 1, .013f);
                d.Line(Ink.Ivory, x-.026f,y, x+.026f,y, .016f);
            }
        }

        static void BinderClip(BattleInkDrawing d, Pose p, bool compact)
        {
            float jaw = p.Reach * .075f - p.Flinch * .055f;
            float lift = p.Reach * .055f - p.Fold * .13f;
            ClockBoot(d, -.17f, -.143f, -p.Stride, p.Fold);
            ClockBoot(d, .19f, -.143f, p.Stride, p.Fold);
            // Tall doubled wire handle and a broad folded wedge distinguish the shield bearer.
            d.Path(Ink.Ivory, .032f, 1, -.242f,0f, -.322f,.247f + lift, -.296f,.361f + lift,
                -.197f,.381f + lift, -.14f,.307f + lift, -.15f,.103f);
            ClockPanel(d, p, -.367f,-.145f, -.174f,.15f, .027f,.205f, .255f + jaw,.085f,
                .365f + jaw,-.146f, .302f + jaw,-.216f, -.287f,-.214f);
            d.Fill(Ink.Body, .92f, -.346f,-.13f, -.158f,.133f, .015f,.174f, -.095f,-.122f);
            d.Path(Ink.Copper, .041f, 1, .052f,.166f, .241f + jaw,.062f, .334f + jaw,-.139f);
            d.Line(Ink.Ivory, -.281f,-.16f, .33f + jaw,-.16f, .025f);
            d.Line(Ink.Danger, .197f + jaw,-.005f, .244f + jaw,-.025f, .028f);
            ClockArmor(d, p, -.109f,-.099f);
            if (!compact)
            {
                d.Path(Ink.Body, .018f, 1, -.218f,.106f, -.251f,.251f + lift, -.223f,.327f + lift);
                d.Ellipse(Ink.Copper, -.23f,-.084f, .031f, 1, 0);
                d.Ellipse(Ink.Copper, .028f,-.086f, .031f, 1, 0);
            }
        }
    }
}
