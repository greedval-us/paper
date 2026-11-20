using System;
using PaperDefense.Core;
using UnityEngine;
using Ink = PaperDefense.Presentation.NotebookInkChannel;

namespace PaperDefense.Presentation
{
    internal static partial class InkSwarmEnemyArt
    {
        static class BossCatalog
        {
            internal static readonly string[] Names = { "nibstag", "inkmanta", "boneram", "cryptking", "presswalker", "archivist" };
            internal static readonly string[,] Ids = BuildIds();

            static string[,] BuildIds()
            {
                var ids = new string[Names.Length, Motions.Length];
                for (int shape = 0; shape < Names.Length; shape++)
                for (int motion = 0; motion < Motions.Length; motion++)
                    ids[shape, motion] = "enemy.boss." + Names[shape] + "." + Motions[motion];
                return ids;
            }
        }

        internal static string BossId(EnemyFamily family, BossRank rank, NotebookEnemyMotion motion)
        {
            if (rank != BossRank.Midpoint && rank != BossRank.Finale) return null;
            return BossCatalog.Ids[(int)family * 2 + (rank == BossRank.Finale ? 1 : 0), (int)motion];
        }

        internal static void PopulateBossClips(NotebookArtSource data)
        {
            // Each column follows the working part: mandibles, wings, horn, censer, piston, drawer.
            float[] strideTimes = { .56f, .78f, .48f, .88f, .66f, .75f };
            float[] strikeTimes = { .44f, .57f, .46f, .62f, .54f, .48f };
            for (int shape = 0; shape < BossCatalog.Names.Length; shape++)
            for (int motion = 0; motion < Motions.Length; motion++)
            {
                var action = (NotebookEnemyMotion)motion;
                string id = BossCatalog.Ids[shape, motion];
                var clip = new NotebookArtClip { Id = id };
                switch (action)
                {
                    case NotebookEnemyMotion.Idle:
                    case NotebookEnemyMotion.Walk:
                        clip.Loop = true;
                        clip.Duration = action == NotebookEnemyMotion.Walk ? strideTimes[shape] : 1.2f;
                        Frame(clip, id, 0); Frame(clip, id + ".a", .25f);
                        Frame(clip, id, .5f); Frame(clip, id + ".b", .75f);
                        break;
                    case NotebookEnemyMotion.Attack:
                        clip.Duration = strikeTimes[shape];
                        Frame(clip, id, 0); Frame(clip, id + ".a", .32f);
                        Frame(clip, id + ".b", .70f); Frame(clip, id + ".a", .93f);
                        break;
                    case NotebookEnemyMotion.Hit:
                        clip.Duration = .30f;
                        Frame(clip, id, 0, rotation: -.075f); Frame(clip, id + ".a", .35f, rotation: -.03f);
                        Frame(clip, id + ".b", .75f);
                        break;
                    case NotebookEnemyMotion.Death:
                        clip.Duration = .92f;
                        Frame(clip, id, 0, rotation: -.045f); Frame(clip, id + ".a", .32f, rotation: -.12f);
                        Frame(clip, id + ".b", .65f, .65f, -.19f); Frame(clip, id + ".b", .97f, 0, -.19f);
                        break;
                }
                data.Clips.Add(clip);
            }
        }

        static NotebookArtShape CreateBossPose(string id)
        {
            if (Shapes.TryGetValue(id, out var cached)) return cached;
            string[] parts = id.Split('.');
            if (parts.Length != 4 && parts.Length != 5) return null;
            int shape = Array.IndexOf(BossCatalog.Names, parts[2]);
            int motion = Array.IndexOf(Motions, parts[3]);
            int phase = parts.Length == 4 ? 0 : parts[4] == "a" ? 1 : parts[4] == "b" ? 2 : -1;
            if (shape < 0 || motion < 0 || phase < 0) return null;
            var drawing = new BattleInkDrawing(id);
            var pose = new Pose((NotebookEnemyMotion)motion, phase, 0);
            switch (shape)
            {
                case 0: NibStag(drawing, pose); break;
                case 1: InkManta(drawing, pose); break;
                case 2: BoneRam(drawing, pose); break;
                case 3: CryptKing(drawing, pose); break;
                case 4: PressWalker(drawing, pose); break;
                case 5: Archivist(drawing, pose); break;
            }
            FinishPose(drawing.Shape, pose);
            Shapes.Add(id, drawing.Shape);
            return drawing.Shape;
        }

        static float BossSway(Pose p)
        {
            if (p.Motion == NotebookEnemyMotion.Walk) return p.Stride;
            if (p.Motion != NotebookEnemyMotion.Idle) return 0;
            return p.Phase == 1 ? .030f : p.Phase == 2 ? -.030f : 0;
        }

        // Low plated ink beetle. Its two split pen nibs close around the target on the strike key.
        static void NibStag(BattleInkDrawing d, Pose p)
        {
            float stride = p.Stride, sway = BossSway(p);
            float reach = .11f*p.Reach - .05f*p.Flinch, gape = .10f - .09f*p.Reach + sway*.25f;
            for (int leg = 0; leg < 3; leg++)
            {
                float hip = -.27f + .19f*leg, step = leg == 1 ? -stride : stride;
                d.Path(Ink.Ivory,.035f,1, hip,-.05f, hip-.095f+step,-.15f, hip-.16f+step,-.28f+p.Fold*.12f);
                d.Line(Ink.Sketch,hip+.05f,-.035f,hip+.13f-step,-.20f,.027f);
            }
            d.Fill(Ink.Surface,1, -.51f,.015f,-.42f,.20f,-.18f,.29f,.045f,.24f,.18f,.11f,.14f,-.095f,-.15f,-.13f,-.39f,-.065f);
            d.Path(Ink.Ivory,.029f,1, -.50f,.015f,-.42f,.20f,-.18f,.29f,.045f,.24f,.18f,.11f);
            d.Fill(Ink.Body,1, -.46f,.037f,-.39f,.175f,-.20f,.244f,-.07f,.21f,-.25f,.033f);
            d.Path(Ink.Sketch,.019f,1, -.42f,.015f,-.23f,.02f,-.06f,.21f,.01f,.23f);
            d.Fill(Ink.Danger,1, -.26f,.22f,-.24f,.345f,-.17f,.275f);
            d.Fill(Ink.Danger,1, -.10f,.26f,-.06f,.395f,.005f,.25f);
            d.Fill(Ink.Surface,1, .10f+reach,.12f,.18f+reach,.19f,.31f+reach,.12f,.32f+reach,-.02f,.17f+reach,-.07f);
            d.Path(Ink.Ivory,.032f,1, .095f+reach,.10f,.18f+reach,.19f,.30f+reach,.13f,.32f+reach,.07f);
            d.Fill(Ink.Amber,1, .195f+reach,.12f,.275f+reach,.105f,.225f+reach,.056f);
            d.Path(Ink.Ivory,.048f,1, .25f+reach,.11f,.38f+reach,.18f+gape,.46f+reach,.21f+gape);
            d.Fill(Ink.Ivory,1, .34f+reach,.16f+gape,.49f+reach,.25f+gape,.42f+reach,.11f+gape);
            d.Path(Ink.Sketch,.022f,1, .39f+reach,.19f+gape,.455f+reach,.214f+gape);
            d.Path(Ink.Ivory,.043f,1, .25f+reach,.005f,.39f+reach,-.065f-gape,.49f+reach,-.012f-gape);
            d.Fill(Ink.Ivory,1, .38f+reach,-.08f-gape,.525f+reach,.028f-gape,.45f+reach,-.105f-gape);
        }

        // Floating ink ray. The fin tips rise on alternate beats and its pen-shaped prow thrusts forward to fire.
        static void InkManta(BattleInkDrawing d, Pose p)
        {
            float wing = BossSway(p)*1.75f - .05f*p.Reach - .14f*p.Fold;
            float nib = .105f*p.Reach - .065f*p.Flinch;
            d.Path(Ink.Sketch,.048f,1, -.13f,-.07f,-.31f,-.17f,-.45f,-.23f+wing,-.56f,-.20f+wing);
            d.Fill(Ink.Danger,1, -.44f,-.215f+wing,-.57f,-.15f+wing,-.51f,-.247f+wing);
            d.Fill(Ink.Body,1, -.16f,.02f,-.43f,.38f+wing,-.12f,.315f+wing,.19f,.095f);
            d.Path(Ink.Ivory,.025f,1, -.25f,.115f,-.43f,.38f+wing,-.135f,.32f+wing,.12f,.14f);
            d.Fill(Ink.Surface,1, -.27f,-.005f,-.08f,.17f,.16f,.16f,.32f,.05f,.18f,-.095f,-.08f,-.13f);
            d.Path(Ink.Ivory,.033f,1, -.24f,.01f,-.065f,.17f,.16f,.16f,.32f,.05f);
            d.Fill(Ink.Surface,1, -.085f,.055f,.19f,.065f,.055f,-.17f,-.29f,-.42f-wing,-.23f,-.19f);
            d.Path(Ink.Ivory,.029f,1, .17f,.03f,.055f,-.17f,-.29f,-.42f-wing,-.22f,-.18f);
            d.Path(Ink.Danger,.036f,1, -.035f,-.015f,-.081f,-.14f,-.215f,-.30f-wing);
            d.Fill(Ink.Body,1, -.19f,.22f,-.32f,.313f+wing,-.15f,.27f+wing);
            d.Fill(Ink.Ivory,1, .155f+nib,.11f,.30f+nib,.14f,.49f+nib,.047f,.30f+nib,-.028f,.155f+nib,.005f);
            d.Line(Ink.Surface,.25f+nib,.055f,.485f+nib,.047f,.018f);
            d.Ellipse(Ink.Surface,.255f+nib,.065f,.026f,.88f,0,1,9);
            d.Fill(Ink.Amber,1, .10f,.127f,.181f,.127f,.152f,.081f);
        }

        // A bone ram leans onto its front hooves before driving its horn into the barricade.
        static void BoneRam(BattleInkDrawing d, Pose p)
        {
            float run = p.Stride, head = .09f*p.Reach - .055f*p.Flinch;
            float dip = -.105f*p.Reach + .34f*BossSway(p) - .06f*p.Fold;
            for (int j = 0; j < 4; j++)
            {
                float hip = j < 2 ? -.225f+j*.072f : .055f+(j-2)*.09f;
                float step = j == 0 || j == 3 ? run : -run;
                float knee = hip + (j < 2 ? -.065f : .085f) + step;
                d.Path(j%2 == 0 ? Ink.Sketch : Ink.Ivory,.041f,1,
                    hip,-.035f,knee,-.155f,knee-.065f+step,-.295f+p.Fold*.10f);
                d.Line(Ink.Body,knee-.10f+step,-.305f+p.Fold*.10f,knee-.016f+step,-.305f+p.Fold*.10f,.038f);
            }
            d.Fill(Ink.Violet,1, -.21f,.16f,-.33f,.18f,-.50f,.04f+run*.5f,-.40f,.025f+run*.5f);
            d.Fill(Ink.Surface,1, -.29f,.08f,-.19f,.205f,.13f,.20f,.21f,.085f,.10f,-.07f,-.20f,-.06f);
            d.Path(Ink.Ivory,.047f,1, -.31f,.055f,-.23f,.18f,-.11f,.205f,.10f,.19f,.19f,.235f+dip);
            for (int i = 0; i < 4; i++)
            {
                float rib = -.205f + i*.085f;
                d.Path(Ink.Ivory,.026f,1, rib,.148f,rib-.024f,.063f,rib+.015f,-.035f);
            }
            d.Fill(Ink.Ivory,1, .11f+head,.235f+dip,.235f+head,.27f+dip,.355f+head,.19f+dip,
                .455f+head,.044f+dip,.393f+head,-.012f+dip,.18f+head,.05f+dip);
            d.Fill(Ink.Surface,1, .24f+head,.175f+dip,.329f+head,.149f+dip,.28f+head,.088f+dip);
            d.Line(Ink.Sketch,.34f+head,.048f+dip,.42f+head,.031f+dip,.022f);
            d.Path(Ink.Sketch,.065f,1, .25f+head,.24f+dip,.25f+head,.37f+dip,.12f+head,.409f+dip);
            d.Path(Ink.Ivory,.079f,1, .24f+head,.218f+dip,.15f+head,.33f+dip,.019f+head,.31f+dip,
                -.026f+head,.209f+dip,.027f+head,.147f+dip,.108f+head,.16f+dip);
            d.Line(Ink.Violet,.048f+head,.155f+dip,.105f+head,.173f+dip,.025f);
        }

        static void CryptKing(BattleInkDrawing d, Pose p)
        {
            float sway = BossSway(p), shoulder = -.046f*p.Flinch, fall = .12f*p.Fold;
            float reach = .105f*p.Reach - .04f*p.Flinch;
            d.Fill(Ink.Surface,1,-.13f,.17f,.095f,.15f,.15f,-.21f,.12f,-.37f,-.16f,-.35f,-.24f,-.29f);
            d.Fill(Ink.Violet,1,-.13f,.171f,-.038f,.14f,-.085f,-.19f,-.24f-sway,-.34f,-.19f,-.105f);
            d.Path(Ink.Ivory,.025f,1, -.13f,.17f,-.17f,-.07f,-.25f-sway,-.34f);
            d.Path(Ink.Ivory,.028f,1, .095f,.145f,.142f,-.14f,.18f+sway,-.32f);
            d.Path(Ink.Sketch,.023f,1, -.065f,.076f,-.015f,-.02f,.075f,.073f);
            d.Path(Ink.Ivory,.025f,1, -.052f,.005f,.0f,-.073f,.068f,.008f);
            FuneralSkull(d, shoulder,.237f-fall,.115f,false);
            d.Fill(Ink.Amber,1, shoulder-.105f,.31f-fall,shoulder+.102f,.31f-fall,shoulder+.10f,.346f-fall,shoulder-.108f,.346f-fall);
            for (int i = 0; i < 3; i++)
            {
                float x = shoulder-.108f+i*.07f;
                d.Fill(Ink.Ivory,1,x,.34f-fall,x+i*.018f,.418f+(i==1 ? .038f : 0)-fall,x+.066f,.34f-fall);
            }
            d.Path(Ink.Ivory,.043f,1, .092f,.126f,.16f,.004f,.275f+reach,.084f);
            d.Line(Ink.Sketch,.275f+reach,-.32f,.314f+reach,.346f,.035f);
            d.Path(Ink.Ivory,.028f,1, .314f+reach,.33f,.357f+reach,.395f,.43f+reach,.365f);
            float bell = .399f+reach + sway*.47f+.047f*p.Reach;
            d.Line(Ink.Violet,.412f+reach,.367f,bell,.293f,.021f);
            d.Fill(Ink.Sketch,1, bell-.043f,.288f,bell+.037f,.295f,bell+.065f,.195f,bell-.075f,.188f);
            d.Line(Ink.Ivory,bell-.08f,.185f,bell+.073f,.195f,.027f);
            d.Ellipse(Ink.Amber,bell+.017f+sway*.2f,.164f,.022f,1,0,1,8);
        }

        // The stamp platform hangs in a fixed outer frame. Only the platen descends at impact.
        static void PressWalker(BattleInkDrawing d, Pose p)
        {
            float sway = BossSway(p), press = -.15f*p.Reach - .08f*p.Fold + sway*.24f;
            for (int leg = 0; leg < 4; leg++)
            {
                bool left = leg < 2;
                float foot = left ? -.43f+.12f*leg : .30f+.12f*(leg-2);
                float step = leg%2 == 0 ? p.Stride : -p.Stride;
                d.Path(leg%2 == 0 ? Ink.Copper : Ink.Ivory,.039f,1, left ? -.17f : .18f,-.09f,
                    foot+step*.35f,-.035f,foot+step,-.24f+p.Fold*.11f);
                d.Line(Ink.Sketch,foot+step-.052f,-.253f+p.Fold*.11f,foot+step+.052f,-.253f+p.Fold*.11f,.039f);
            }
            ClockPanel(d,p,-.245f,-.135f,.25f,-.135f,.205f,-.072f,-.199f,-.072f);
            d.Path(Ink.Copper,.055f,1,-.175f,-.07f,-.175f,.22f,-.12f,.30f,.135f,.30f,.195f,.23f,.195f,-.07f);
            d.Fill(Ink.Surface,1,-.163f,.235f,-.117f,.313f,.125f,.313f,.175f,.235f);
            d.Line(Ink.Ivory,-.15f,.242f,.166f,.242f,.029f);
            d.Line(Ink.Ivory,.008f,.23f,.008f,.14f+press,.068f);
            d.Fill(Ink.Copper,1,-.105f,.16f+press,.115f,.16f+press,.14f,.088f+press,-.13f,.088f+press);
            d.Line(Ink.Ivory,-.144f,.085f+press,.153f,.085f+press,.039f);
            d.Ellipse(Ink.Surface,-.191f,.268f,.093f,1,0,1,12);
            d.Ellipse(Ink.Ivory,-.191f,.268f,.084f,1,.023f,1,12);
            d.Line(Ink.Danger,-.19f,.268f,-.17f+.42f*sway+.03f*p.Reach,.317f,.021f);
            d.Line(Ink.Sketch,.07f,-.095f,.13f,-.13f,.020f);
        }

        // A mobile filing cabinet feeds a loose card from its upper drawer; the two wheels counter the recoil.
        static void Archivist(BattleInkDrawing d, Pose p)
        {
            float recoil = -.07f*p.Reach - .055f*p.Flinch, fall = .10f*p.Fold;
            float drawer = .11f*p.Reach, sway = BossSway(p);
            for (int wheel = 0; wheel < 2; wheel++)
            {
                float hub = -.19f + wheel*.379f;
                d.Ellipse(Ink.Surface,hub,-.241f,.108f,1,0,1,14);
                d.Ellipse(Ink.Copper,hub,-.241f,.097f,1,.029f,1,14);
                for (int i = 0; i < 3; i++)
                {
                    float angle = i * Mathf.PI*2/3 + sway*7;
                    d.Line(Ink.Sketch,hub,-.241f,hub+.08f*Mathf.Cos(angle),-.241f+.08f*Mathf.Sin(angle),.017f);
                }
            }
            ClockPanel(d,p,recoil-.27f,-.17f,recoil+.213f,-.17f,recoil+.21f,.225f-fall,
                recoil+.14f,.292f-fall,recoil-.21f,.292f-fall,recoil-.27f,.219f-fall);
            d.Fill(Ink.Body,1,recoil-.25f,.198f-fall,recoil-.19f,.25f-fall,recoil-.17f,-.137f,recoil-.25f,-.143f);
            for (int row = 0; row < 2; row++)
            {
                float y = -.124f+row*.107f;
                d.Box(Ink.Copper,recoil-.135f,y,.27f,.088f,.016f);
                d.Line(Ink.Ivory,recoil-.046f,y+.056f,recoil+.046f,y+.056f,.018f);
            }
            ClockPanel(d,p,recoil-.14f,.126f-fall,recoil+.19f,.126f-fall,recoil+.28f+drawer,.176f-fall,recoil-.065f,.187f-fall);
            d.Fill(Ink.Ivory,1,recoil+.168f+drawer,.185f-fall,recoil+.309f+drawer,.277f-fall,recoil+.383f+drawer,.227f-fall,recoil+.246f+drawer,.151f-fall);
            d.Line(Ink.Surface,recoil+.244f+drawer,.208f-fall,recoil+.30f+drawer,.235f-fall,.016f);
            d.Fill(Ink.Copper,1,recoil-.146f,.286f-fall,recoil-.135f,.396f-fall,recoil-.032f,.404f-fall,recoil+.006f,.287f-fall);
            d.Fill(Ink.Ivory,1,recoil-.032f,.287f-fall,recoil+.012f,.427f+sway*.30f-fall,recoil+.117f,.387f+sway*.30f-fall,recoil+.091f,.282f-fall);
            d.Line(Ink.Sketch,recoil+.009f,.363f-fall,recoil+.068f,.342f-fall,.016f);
            d.Ellipse(Ink.Amber,recoil+.172f,.218f-fall,.024f,1,0,1,8);
        }
    }
}
