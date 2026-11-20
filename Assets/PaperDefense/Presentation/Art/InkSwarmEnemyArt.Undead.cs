using UnityEngine;
using Ink = PaperDefense.Presentation.NotebookInkChannel;

namespace PaperDefense.Presentation
{
    internal static partial class InkSwarmEnemyArt
    {
        // The grave rubbings share exposed ivory bones, punched-out eye sockets and a violet shroud.
        // Their identifying silhouettes and animated joints are also drawn in the crowd LOD.
        static void DrawUndead(BattleInkDrawing d, int role, Pose pose, bool compact)
        {
            switch (role)
            {
                case 0: DrawSkeleton(d, pose, compact); break;
                case 1: DrawBoneHound(d, pose, compact); break;
                case 2: DrawJawBreaker(d, pose, compact); break;
                case 3: DrawBellBearer(d, pose, compact); break;
                case 4: DrawTombPorter(d, pose, compact); break;
                case 5: DrawCoffinGuard(d, pose, compact); break;
            }
        }

        static void FuneralSkull(BattleInkDrawing d, float x, float y, float size, bool compact)
        {
            // A forward cheek, short jaw and a dark eye cut right through the light cranium.
            d.Fill(Ink.Ivory, 1, x-size,y+.30f*size, x-.65f*size,y+.87f*size,
                x+.36f*size,y+size, x+.88f*size,y+.48f*size, x+size,y-.22f*size,
                x+.17f*size,y-.52f*size, x-.65f*size,y-.42f*size);
            d.Fill(Ink.Sketch, 1, x-.24f*size,y-.31f*size, x+.83f*size,y-.24f*size,
                x+.58f*size,y-.91f*size, x-.10f*size,y-.72f*size);
            d.Fill(Ink.Surface, 1, x+.03f*size,y+.39f*size, x+.68f*size,y+.54f*size,
                x+.64f*size,y+.01f*size, x+.23f*size,y-.10f*size);
            if (compact) return;
            d.Fill(Ink.Violet, 1, x+.37f*size,y+.26f*size, x+.54f*size,y+.30f*size, x+.49f*size,y+.12f*size);
            d.Line(Ink.Surface, x+.28f*size,y-.77f*size, x+.30f*size,y-.47f*size, size*.13f);
            d.Line(Ink.Surface, x+.78f*size,y-.22f*size, x+.83f*size,y-.07f*size, size*.16f);
        }

        static void BurialMarks(BattleInkDrawing d, Pose p, float x, float y, float span)
        {
            if (p.Tier == 0) return;
            d.Line(Ink.Ivory, x,y, x+span,y+span*.67f, .024f);
            if (p.Tier == 1) return;
            d.Line(Ink.Ivory, x+span*.48f,y, x+span*1.48f,y+span*.67f, .018f);
            d.Line(Ink.Ivory, x+span*.22f,y+span*.70f, x+span*1.15f,y-.007f, .016f);
        }

        static void DrawSkeleton(BattleInkDrawing d, Pose p, bool compact)
        {
            float stride = p.Stride;
            float head = .09f + .12f*p.Reach - .10f*p.Flinch;
            float sag = .10f*p.Fold;
            d.Path(Ink.Body,.047f,1, -.04f,-.13f, -.14f-stride,-.23f, -.20f-stride,-.38f+sag);
            d.Path(Ink.Ivory,.057f,1, .035f,-.12f, .14f+stride,-.23f, .05f+stride,-.37f+sag, .15f+stride,-.37f+sag);
            d.Path(Ink.Sketch,.038f,1, -.09f,.12f, -.20f,.01f, -.22f-p.Reach*.05f,-.10f);
            d.Path(Ink.Ivory,.044f,1, .075f,.12f, .15f+p.Reach*.04f,-.015f,
                .24f+p.Reach*.10f,.045f+p.Reach*.12f);
            d.Fill(Ink.Surface,1, -.105f,.14f,.09f,.14f,.115f,.04f,.055f,-.10f,-.055f,-.09f,-.12f,.055f);
            d.Path(Ink.Ivory,.039f,1, -.11f,.13f,-.095f,.035f,.015f,-.035f,.10f,.046f);
            d.Path(Ink.Sketch,.035f,1, -.09f,.00f,-.04f,-.08f,.03f,-.10f,.085f,-.045f);
            d.Fill(Ink.Body,1,-.075f,-.11f,.069f,-.10f,.035f,-.17f,-.036f,-.19f);
            d.Fill(Ink.Violet,1,head-.085f,.196f,head+.03f,.152f,-.23f+.4f*stride,-.035f,-.17f,.113f);
            FuneralSkull(d,head,.26f-.06f*p.Fold,.145f,compact);
            BurialMarks(d,p,-.09f,.10f,.065f);
            if (!compact)
            {
                d.Line(Ink.Sketch,-.09f,.135f,.065f,.128f,.025f);
                d.Line(Ink.Ivory,-.13f-stride,-.23f,-.17f-stride,-.21f,.027f);
                d.Line(Ink.Ivory,-.168f,.092f,-.206f,.004f,.013f);
            }
        }

        static void DrawBoneHound(BattleInkDrawing d, Pose p, bool compact)
        {
            float step = p.Stride * 1.45f;
            float lift = Mathf.Max(0, -step) * .56f;
            float arch = -.31f*step;
            float head = .25f + .25f*step + .068f*p.Reach - .07f*p.Flinch;
            float neck = .12f - .055f*p.Reach - .08f*p.Fold;
            d.Path(Ink.Body,.032f,1, -.14f,.014f,-.16f+step,-.095f,-.24f+step,-.22f+lift);
            d.Path(Ink.Body,.035f,1, .105f,.018f,.11f-step,-.11f,.16f-step,-.23f+lift);
            d.Path(Ink.Ivory,.052f,1, -.20f,.025f,-.10f-step,-.09f,-.25f-step,-.252f+lift);
            d.Path(Ink.Ivory,.046f,1, .14f,.035f,.07f+step,-.13f,.20f+step,-.245f+lift);
            d.Fill(Ink.Violet,1, -.085f,.145f,-.20f,.13f,-.45f,.19f+step*.35f,-.32f,.235f+step*.35f);
            d.Fill(Ink.Surface,1, -.23f,.12f,-.13f,.185f+arch,.09f,.155f+arch,.15f,.055f,.01f,-.042f+arch,-.18f,-.03f);
            d.Path(Ink.Ivory,.039f,1, -.3f,.04f,-.23f,.14f,-.13f,.178f+arch,.095f,.14f+arch,head-.035f,neck);
            for (int i = 0; i < 3; i++)
            {
                float rib = -.15f + .09f*i;
                d.Path(Ink.Ivory,.032f,1, rib,.145f+arch, rib-.033f,.037f+arch, rib+.012f,-.025f+arch);
            }
            d.Fill(Ink.Ivory,1, head-.09f,neck+.025f, head-.04f,neck+.09f, head+.027f,neck+.078f,
                head+.17f,neck-.024f, head+.164f,neck-.063f, head-.034f,neck-.044f);
            d.Fill(Ink.Surface,1, head-.016f,neck+.035f,head+.045f,neck+.027f,head+.057f,neck-.007f,head-.014f,neck-.014f);
            d.Fill(Ink.Ivory,1, head+.09f,neck-.037f,head+.125f,neck-.047f,head+.113f,neck-.091f);
            BurialMarks(d,p,-.18f,.15f,.046f);
            if (!compact)
            {
                d.Path(Ink.Sketch,.017f,1, head+.014f,neck-.05f,head+.071f,neck-.083f-.02f*p.Reach,
                    head+.143f,neck-.073f-.02f*p.Reach);
                d.Line(Ink.Ivory,-.31f,.22f+step*.35f,-.4f,.21f+step*.35f,.012f);
                d.Fill(Ink.Violet,1,head+.01f,neck+.018f,head+.034f,neck+.013f,head+.02f,neck-.005f);
            }
        }

        static void DrawJawBreaker(BattleInkDrawing d, Pose p, bool compact)
        {
            float jaw = .07f*p.Reach - .08f*p.Flinch;
            float gape = p.Motion == NotebookEnemyMotion.Attack ? .045f-.08f*p.Reach : 0;
            d.Path(Ink.Sketch,.057f,1, -.15f,-.09f,-.24f-p.Stride,-.20f,-.22f-p.Stride,-.30f);
            d.Path(Ink.Ivory,.067f,1, -.19f,-.02f,-.32f+p.Stride,-.17f,-.35f+p.Stride,-.29f);
            d.Path(Ink.Ivory,.073f,1, .02f,-.045f,.11f-p.Stride,-.21f,.22f-p.Stride,-.24f);
            d.Fill(Ink.Surface,1, -.30f,-.02f,-.24f,.165f,-.10f,.235f,.07f,.15f,.10f,-.06f,-.14f,-.145f);
            d.Path(Ink.Ivory,.041f,1, -.30f,-.02f,-.24f,.165f,-.10f,.235f,.07f,.15f);
            d.Fill(Ink.Violet,1, -.24f,.15f,-.15f,.214f,-.04f,.15f,-.17f,.112f);
            // The jaws have separate roots and tips so the space between the chisels stays clear.
            d.Fill(Ink.Ivory,1, jaw-.07f,.083f,jaw+.015f,.24f+gape,jaw+.17f,.26f+gape,
                jaw+.36f,.18f+gape,jaw+.36f,.085f+gape,jaw+.115f,.11f+gape);
            d.Fill(Ink.Surface,1, jaw+.03f,.19f+gape,jaw+.115f,.20f+gape,jaw+.095f,.138f+gape,jaw+.044f,.13f+gape);
            d.Fill(Ink.Ivory,1, jaw+.275f,.125f+gape,jaw+.35f,.10f+gape,jaw+.325f,.018f+gape);
            d.Path(Ink.Sketch,.059f,1, jaw-.046f,.063f,jaw+.035f,-.13f-gape,jaw+.245f,-.17f-gape,jaw+.36f,-.06f-gape);
            d.Fill(Ink.Ivory,1, jaw+.23f,-.17f-gape,jaw+.365f,-.04f-gape,jaw+.335f,.016f-gape);
            BurialMarks(d,p,-.23f,.037f,.09f);
            if (!compact)
            {
                d.Path(Ink.Sketch,.028f,1,-.20f,.103f,-.20f,-.01f,-.15f,-.066f);
                d.Path(Ink.Sketch,.028f,1,-.12f,.123f,-.11f,.012f,-.06f,-.027f);
                d.Line(Ink.Surface,jaw+.19f,.20f+gape,jaw+.23f,.13f+gape,.013f);
            }
        }

        static void DrawBellBearer(BattleInkDrawing d, Pose p, bool compact)
        {
            float swing = .68f*p.Stride + .13f*p.Reach - .073f*p.Flinch;
            float clapper = -.85f*p.Stride + .19f*p.Reach;
            d.Path(Ink.Body,.049f,1,-.09f,-.07f,-.14f-p.Stride,-.22f,-.20f-p.Stride,-.36f);
            d.Path(Ink.Ivory,.054f,1,.04f,-.07f,.10f+p.Stride,-.23f,.025f+p.Stride,-.36f,.12f+p.Stride,-.36f);
            d.Path(Ink.Sketch,.048f,1,-.24f,-.26f,-.27f,.26f,-.05f,.415f,.047f,.415f,.055f,.33f);
            d.Fill(Ink.Violet,1,-.25f,.265f,-.18f,.31f,-.19f,.19f,-.302f,.085f);
            FuneralSkull(d,.035f+swing*.32f,-.087f,.081f,compact);
            d.Line(Ink.Ivory,.118f+swing,.044f,.16f+clapper,-.165f,.031f);
            d.Fill(Ink.Sketch,1,.10f+clapper,-.17f,.16f+clapper,-.135f,.215f+clapper,-.173f,
                .204f+clapper,-.235f,.138f+clapper,-.253f,.099f+clapper,-.219f);
            d.Fill(Ink.Body,1, -.215f+swing,-.004f, -.145f+swing*.5f,.115f, -.10f,.266f,
                .01f,.336f, .14f,.274f, .20f+swing*.5f,.105f, .29f+swing,-.002f);
            d.Path(Ink.Ivory,p.Edge,1, -.215f+swing,-.004f, -.145f+swing*.5f,.115f, -.10f,.266f,
                .01f,.336f, .14f,.274f, .20f+swing*.5f,.105f, .29f+swing,-.002f);
            d.Fill(Ink.Surface,1,-.215f+swing,-.004f,.29f+swing,-.002f,.22f+swing,-.038f,-.125f+swing,-.044f);
            d.Fill(Ink.Violet,1,-.15f+swing*.5f,.112f,.20f+swing*.5f,.105f,.218f+swing*.65f,.066f,-.17f+swing*.65f,.074f);
            BurialMarks(d,p,-.082f,.16f,.065f);
            if (!compact)
            {
                d.Path(Ink.Surface,.017f,.9f,.107f,.245f,.068f,.196f,.093f,.157f,.045f,.088f);
                d.Line(Ink.Ivory,-.16f+swing*.75f,.04f,.235f+swing*.75f,.035f,.017f);
            }
        }

        static void DrawTombPorter(BattleInkDrawing d, Pose p, bool compact)
        {
            float tilt = -.48f*p.Stride - .10f*p.Reach + .065f*p.Flinch;
            float shoulder = .09f*p.Reach - .065f*p.Flinch;
            d.Path(Ink.Body,.086f,1,-.155f,-.12f,-.24f-p.Stride,-.21f,-.26f-p.Stride,-.32f);
            d.Path(Ink.Ivory,.083f,1,-.08f,-.12f,-.14f+p.Stride,-.24f,-.21f+p.Stride,-.335f,-.095f+p.Stride,-.335f);
            d.Path(Ink.Ivory,.072f,1,.125f+shoulder,-.072f,.245f+shoulder,-.18f,.285f+shoulder,-.30f);
            d.Fill(Ink.Surface,1,-.27f,-.11f,-.205f,.108f,.11f,.155f,.258f+shoulder,.023f,.12f+shoulder,-.17f,-.085f,-.21f);
            FuneralSkull(d,.259f+shoulder,-.029f,.105f,compact);
            // Rounded headstone, leaned back over the shoulder. Its top lags behind each ram.
            d.Fill(Ink.Body,1,-.29f,-.13f,-.28f+tilt,.285f,-.21f+tilt,.389f,-.095f+tilt,.438f,
                .035f+tilt,.379f,.094f+tilt,.29f,.15f,-.073f);
            d.Path(Ink.Ivory,p.Edge+.006f,1,-.29f,-.13f,-.28f+tilt,.285f,-.21f+tilt,.389f,
                -.095f+tilt,.438f,.035f+tilt,.379f,.094f+tilt,.29f,.15f,-.073f);
            d.Line(Ink.Surface, -.10f+tilt,.346f,-.085f+tilt,.18f,.031f);
            d.Line(Ink.Surface, -.16f+tilt,.275f,-.04f+tilt,.288f,.032f);
            d.Fill(Ink.Violet,1,-.29f,.031f,.123f,.120f,.136f,.052f,-.29f,-.042f);
            BurialMarks(d,p,-.24f+.6f*tilt,.13f,.063f);
            if (!compact)
            {
                d.Path(Ink.Sketch,.017f,1,-.246f+.8f*tilt,.22f,-.215f+tilt,.317f,-.10f+tilt,.395f);
                d.Path(Ink.Surface,.015f,1,.033f+tilt,.342f,-.004f+tilt,.278f,.018f+tilt,.244f);
                d.Fill(Ink.Ivory,.82f,-.132f,.103f,-.017f,.13f,-.013f,.179f,-.142f,.153f);
            }
        }

        static void DrawCoffinGuard(BattleInkDrawing d, Pose p, bool compact)
        {
            float shield = .07f*p.Reach - .08f*p.Flinch;
            d.Path(Ink.Body,.057f,1,-.07f,-.10f,.01f-p.Stride,-.23f,.09f-p.Stride,-.345f);
            d.Path(Ink.Ivory,.062f,1,-.125f,-.08f,-.22f+p.Stride,-.215f,-.23f+p.Stride,-.35f,-.14f+p.Stride,-.35f);
            d.Fill(Ink.Violet,1,-.22f,-.16f,-.275f,.057f,-.17f,.18f,-.035f,.12f,-.072f,-.095f);
            FuneralSkull(d,-.107f,.205f-.055f*p.Flinch,.111f,compact);
            d.Fill(Ink.Body,1, shield-.072f,.21f, shield+.005f,.307f, shield+.189f,.32f,
                shield+.30f,.17f, shield+.24f,-.162f, shield+.115f,-.30f, shield+.006f,-.235f);
            d.Path(Ink.Ivory,p.Edge+.006f,1, shield-.072f,.21f, shield+.005f,.307f, shield+.189f,.32f,
                shield+.30f,.17f, shield+.24f,-.162f, shield+.115f,-.30f, shield+.006f,-.235f,shield-.072f,.21f);
            d.Fill(Ink.Surface,1,shield+.01f,.172f,shield+.11f,.226f,shield+.16f,.135f,
                shield+.12f,-.142f,shield+.075f,-.184f,shield+.035f,-.14f);
            d.Fill(Ink.Violet,1,shield-.05f,.135f,shield+.243f,-.097f,shield+.236f,-.148f,shield-.04f,.071f);
            BurialMarks(d,p,shield+.14f,.19f,.055f);
            if (!compact)
            {
                d.Path(Ink.Sketch,.017f,1,shield+.046f,.258f,shield+.163f,.27f,shield+.245f,.157f,shield+.208f,-.057f);
                d.Line(Ink.Ivory,-.23f,.065f,-.12f,-.039f,.021f);
            }
        }
    }
}
