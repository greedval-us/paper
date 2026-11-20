using UnityEngine;

namespace PaperDefense.Presentation
{
    public static partial class NightNotebookArt
    {
        // Convex material faces stay inside the certified +Y-forward regions. Open jaws,
        // rails and belt routes use separate pieces, retaining their actual negative space.
        static readonly float[] WeaponReceiver = { .05f,.22f, .26f,.06f, .68f,.06f, .84f,.23f, .82f,.73f, .64f,.89f, .13f,.82f, .05f,.65f };
        static readonly float[] WeaponRailFoot = { .11f,.11f, .25f,.05f, .75f,.05f, .89f,.11f, .79f,.34f, .21f,.34f };
        static readonly float[] WeaponRailLeft = { .08f,.27f, .34f,.22f, .37f,.79f, .23f,.97f, .09f,.82f };
        static readonly float[] WeaponRailRight = { .66f,.22f, .92f,.27f, .91f,.82f, .77f,.97f, .63f,.79f };
        static readonly float[] WeaponMortarBowl = { .08f,.68f, .92f,.68f, .75f,.25f, .60f,.15f, .40f,.15f, .25f,.25f };
        static readonly float[] WeaponFanHeel = { .26f,.27f, .38f,.06f, .62f,.06f, .74f,.27f, .63f,.40f, .37f,.40f };
        static readonly float[] WeaponReflector = { .05f,.51f, .41f,.09f, .60f,.06f, .95f,.47f, .95f,.59f, .59f,.94f, .41f,.94f, .05f,.60f };
        static readonly float[] WeaponDeflector = { .16f,.59f, .33f,.77f, .79f,.42f, .66f,.26f };
        static readonly float[] WeaponStaplerBase = { .08f,.09f, .86f,.09f, .96f,.16f, .91f,.31f, .70f,.34f, .08f,.28f };
        static readonly float[] WeaponStaplerHandle = { .08f,.38f, .23f,.29f, .88f,.67f, .94f,.86f, .85f,.93f, .61f,.87f, .12f,.57f };
        static readonly float[] WeaponStaplerHinge = { .08f,.24f, .28f,.24f, .28f,.55f, .18f,.64f, .08f,.55f };
        static readonly float[] WeaponRoller = { .06f,.64f, .17f,.52f, .81f,.52f, .94f,.64f, .91f,.82f, .78f,.92f, .18f,.92f, .06f,.81f };
        static readonly float[] WeaponHeadGunStem = { .34f,.05f,.66f,.05f,.68f,.85f,.32f,.85f };
        static readonly float[] WeaponHeadGunCap = { .20f,.81f,.80f,.81f,.84f,.90f,.72f,.97f,.28f,.97f,.16f,.90f };
        static readonly float[] WeaponHeadGun = { .34f,.05f,.66f,.05f,.67f,.81f,.80f,.81f,.84f,.90f,.72f,.97f,.28f,.97f,.16f,.90f,.20f,.81f,.33f,.81f };
        static readonly float[] WeaponHeadShortStem = { .34f,.05f,.66f,.05f,.69f,.39f,.31f,.39f };
        static readonly float[] WeaponHeadShortSleeve = { .13f,.30f,.87f,.30f,.90f,.82f,.76f,.96f,.24f,.96f,.10f,.82f };
        static readonly float[] WeaponHeadShort = { .34f,.05f,.66f,.05f,.68f,.30f,.87f,.30f,.90f,.82f,.76f,.96f,.24f,.96f,.10f,.82f,.13f,.30f,.32f,.30f };
        static readonly float[] WeaponHeadLongRoot = { .23f,.05f,.77f,.05f,.77f,.18f,.60f,.27f,.40f,.27f,.23f,.18f };
        static readonly float[] WeaponHeadLongTube = { .39f,.21f,.61f,.21f,.61f,.94f,.39f,.94f };
        static readonly float[] WeaponHeadLongCap = { .29f,.89f,.71f,.89f,.71f,.95f,.61f,.98f,.39f,.98f,.29f,.95f };
        static readonly float[] WeaponHeadLong = { .23f,.05f,.77f,.05f,.77f,.18f,.61f,.27f,.61f,.89f,.71f,.89f,.71f,.95f,.61f,.98f,.39f,.98f,.29f,.95f,.29f,.89f,.39f,.89f,.39f,.27f,.23f,.18f };
        static readonly float[] WeaponHeadMortarNeck = { .33f,.05f,.67f,.05f,.72f,.62f,.28f,.62f };
        static readonly float[] WeaponHeadMortarCup = { .28f,.47f,.72f,.47f,.92f,.76f,.88f,.92f,.75f,.97f,.25f,.97f,.12f,.92f,.08f,.76f };
        static readonly float[] WeaponHeadMortar = { .33f,.05f,.67f,.05f,.71f,.47f,.72f,.47f,.92f,.76f,.88f,.92f,.75f,.97f,.25f,.97f,.12f,.92f,.08f,.76f,.28f,.47f,.29f,.47f };
        static readonly float[] WeaponHeadWide = { .35f,.05f,.65f,.05f,.92f,.73f,.92f,.91f,.78f,.97f,.22f,.97f,.08f,.91f,.08f,.73f };
        static readonly float[] WeaponHeadPiercerLeft = { .24f,.05f,.43f,.05f,.42f,.93f,.29f,.98f,.20f,.91f };
        static readonly float[] WeaponHeadPiercerRight = { .57f,.05f,.76f,.05f,.80f,.91f,.71f,.98f,.58f,.93f };
        static readonly float[] WeaponHeadPiercer = { .24f,.05f,.76f,.05f,.80f,.91f,.71f,.98f,.58f,.93f,.57f,.22f,.43f,.22f,.42f,.93f,.29f,.98f,.20f,.91f };
        static readonly float[] WeaponHeadRicochetA = { .28f,.05f, .63f,.05f, .94f,.32f, .59f,.32f };
        static readonly float[] WeaponHeadRicochetB = { .59f,.32f, .94f,.32f, .41f,.64f, .06f,.64f };
        static readonly float[] WeaponHeadRicochetC = { .06f,.64f, .41f,.64f, .69f,.96f, .34f,.96f };
        static readonly float[] WeaponHeadRicochet = { .28f,.05f, .63f,.05f, .94f,.32f, .41f,.64f, .69f,.96f, .34f,.96f, .06f,.64f, .59f,.32f };
        static readonly float[] WeaponHeadStaplerRoot = { .14f,.05f,.86f,.05f,.87f,.31f,.13f,.31f };
        static readonly float[] WeaponHeadStaplerLeft = { .13f,.31f,.36f,.31f,.34f,.96f,.10f,.96f };
        static readonly float[] WeaponHeadStaplerRight = { .64f,.31f,.87f,.31f,.90f,.96f,.66f,.96f };
        static readonly float[] WeaponHeadStapler = { .14f,.05f,.86f,.05f,.90f,.96f,.66f,.96f,.64f,.31f,.36f,.31f,.34f,.96f,.10f,.96f };
        static readonly float[] WeaponHeadBlotterStem = { .34f,.05f,.66f,.05f,.71f,.71f,.29f,.71f };
        static readonly float[] WeaponHeadBlotterPad = { .09f,.65f,.91f,.65f,.94f,.87f,.82f,.96f,.18f,.96f,.06f,.87f };
        static readonly float[] WeaponHeadBlotter = { .34f,.05f,.66f,.05f,.70f,.65f,.91f,.65f,.94f,.87f,.82f,.96f,.18f,.96f,.06f,.87f,.09f,.65f,.30f,.65f };
        static void WeaponBody(Drawing p, float[] convexContour) => p.Outline(convexContour);

        static void DrawFanLeaves(Drawing p, bool focused, float flex)
        {
            float spread = focused ? .20f : .36f;
            float outerY = focused ? .88f : .75f;
            WeaponBody(p, WeaponFanHeel);
            p.Beam(.38f, .29f, .50f - spread + flex, outerY - flex, .20f);
            p.Beam(.62f, .29f, .50f + spread - flex, outerY - flex, .20f);
            p.Beam(.50f, .31f, .50f, .96f - flex, .22f);
            p.ColorBox(.44f, .63f - flex, .12f, .21f, Copper);
            p.AccentBox(.41f, .14f, .18f, .08f);
        }

        static void DrawMechanism(Drawing p, string id)
        {
            if (Has(id, "piercer"))
            {
                WeaponBody(p, WeaponRailFoot);
                WeaponBody(p, WeaponRailLeft);
                WeaponBody(p, WeaponRailRight);
                float shuttle = .23f + p.Fire * .44f;
                p.ColorBox(.29f, shuttle, .42f, .14f, BladeSteel);
                p.AccentBox(.42f, shuttle, .16f, .14f);
                p.Seam(.19f, .38f, .24f, .79f);
                p.Seam(.81f, .38f, .76f, .79f);
            }
            else if (Has(id, "mortar"))
            {
                WeaponBody(p, WeaponMortarBowl);
                p.SolidBox(.14f, .08f, .72f, .12f);
                float rim = .71f - p.Fire * .10f;
                p.Disc(.50f, rim, .44f, .24f);
                p.InlayDisc(.50f, rim, .30f, .13f, Paper);
                p.Inlay(.28f, .39f, .34f, .29f, .65f, .29f, .72f, .39f, Copper);
                p.Fold(.78f,.54f,.86f,.56f,.65f,.23f);
            }
            else if (Has(id, "fan"))
            {
                DrawFanLeaves(p, false, p.Fire * .09f);
            }
            else if (Has(id, "ricochet"))
            {
                WeaponBody(p, WeaponReflector);
                p.Inlay(.13f,.55f, .47f,.18f, .86f,.50f, .52f,.85f, BladeSteel);
                p.Hinged(WeaponDeflector, .50f, .50f, p.Fire * .35f);
                p.InlayDisc(.50f, .50f, .075f, .075f, Copper);
                p.Seam(.38f,.76f,.49f,.85f);
            }
            else if (Has(id, "stapler"))
            {
                WeaponBody(p, WeaponStaplerBase);
                WeaponBody(p, WeaponStaplerHinge);
                p.Inlay(.29f,.12f, .83f,.12f, .89f,.20f, .31f,.23f, RedEnamel);
                p.Hinged(WeaponStaplerHandle, .18f, .43f, -p.Fire * .30f);
                p.InlayDisc(.18f, .43f, .083f, .09f, Copper);
                p.Seam(.23f,.17f,.64f,.17f);
            }
            else if (Has(id, "blotter"))
            {
                // An offset wire yoke leaves a window beneath the absorbent roller.
                p.Beam(.44f, .12f, .18f, .31f, .15f);
                p.Beam(.18f, .28f, .18f, .65f, .15f);
                p.SolidBox(.35f, .07f, .34f, .14f);
                WeaponBody(p, WeaponRoller);
                float compression = p.Fire * .08f;
                if (p.Tiny) p.ColorBox(.36f, .66f + compression, .32f, .10f, GlassInk);
                else p.Inlay(.14f,.63f + compression, .84f,.63f + compression, .78f,.80f, .20f,.80f, GlassInk);
                p.Seam(.32f,.58f,.40f,.84f);
                p.Seam(.61f,.58f,.69f,.84f);
            }
            else
            {
                WeaponBody(p, WeaponReceiver);
                // A side crank and a cut-away breech distinguish the starter on the first wave.
                p.SolidBox(.76f, .43f, .16f, .21f);
                p.ColorBox(.28f, .34f, .39f, .31f, Paper);
                float bolt = .39f - p.Fire * .13f;
                p.ColorBox(.29f, bolt, .24f, .12f, Copper);
                p.Seam(.22f,.69f,.61f,.74f);
                p.Fold(.08f,.24f,.25f,.12f,.15f,.57f);
                p.AccentBox(.24f, .19f, .29f, .07f);
            }
            if (p.Tiny) return;
            p.Connect(.43f,.88f,.43f,.98f); p.Connect(.57f,.88f,.57f,.98f);
            p.Connect(.02f,.71f,.25f,.71f); p.Connect(.02f,.75f,.25f,.75f);
        }

        static void DrawHead(Drawing p, string id)
        {
            if (Has(id, "short"))
            {
                p.Mask(WeaponHeadShortStem); p.Mask(WeaponHeadShortSleeve);
                p.Outline(WeaponHeadShort,false);
                p.ColorBox(.21f,.70f,.58f,.13f,Paper);
                p.ColorBox(.19f,.41f,.62f,.10f,Copper);
            }
            else if (id == "head.long")
            {
                p.Mask(WeaponHeadLongRoot); p.Mask(WeaponHeadLongTube); p.Mask(WeaponHeadLongCap);
                p.Outline(WeaponHeadLong,false);
                p.ColorBox(.40f,.69f,.20f,.13f,BladeSteel);
                p.Seam(.49f,.30f,.49f,.66f);
                p.AccentBox(.34f,.09f,.32f,.055f);
            }
            else if (Has(id, "wide"))
            {
                WeaponBody(p,WeaponHeadWide);
                p.ColorBox(.18f,.80f,.64f,.09f,Paper);
                p.Inlay(.23f,.66f, .32f,.49f, .68f,.49f, .77f,.66f,Copper);
                p.Seam(.35f,.38f,.28f,.51f);
            }
            else if (Has(id, "stapler"))
            {
                p.Mask(WeaponHeadStaplerRoot); p.Mask(WeaponHeadStaplerLeft); p.Mask(WeaponHeadStaplerRight);
                p.Outline(WeaponHeadStapler,false);
                p.ColorBox(.19f,.60f,.13f,.28f,RedEnamel);
                p.ColorBox(.68f,.60f,.13f,.28f,RedEnamel);
                p.AccentBox(.28f,.15f,.44f,.07f);
            }
            else if (Has(id, "mortar"))
            {
                p.Mask(WeaponHeadMortarNeck); p.Mask(WeaponHeadMortarCup);
                p.Outline(WeaponHeadMortar,false);
                p.Disc(.50f,.82f,.40f,.12f);
                p.InlayDisc(.50f,.82f,.30f,.07f,Paper);
                p.Inlay(.29f,.52f, .71f,.52f, .78f,.63f, .22f,.63f,Copper);
                p.AccentBox(.35f,.27f,.30f,.06f);
            }
            else if (Has(id, "fan"))
            {
                DrawFanLeaves(p, Has(id, "focus"), 0);
            }
            else if (Has(id, "absorb"))
            {
                // A refill sponge has two broad folded pads on a narrow common spine.
                p.SolidBox(.37f,.06f,.26f,.83f);
                p.SolidBox(.14f,.37f,.72f,.18f);
                p.SolidBox(.06f,.70f,.88f,.25f);
                p.ColorBox(.13f,.78f,.74f,.10f,GlassInk);
                p.ColorBox(.20f,.43f,.60f,.065f,GlassInk);
            }
            else if (Has(id, "blotter"))
            {
                p.Mask(WeaponHeadBlotterStem); p.Mask(WeaponHeadBlotterPad);
                p.Outline(WeaponHeadBlotter,false);
                p.ColorBox(.18f,.74f,.64f,.13f,GlassInk);
                p.Seam(.26f,.71f,.33f,.91f); p.Seam(.60f,.71f,.67f,.91f);
                p.AccentBox(.35f,.54f,.30f,.06f);
            }
            else if (Has(id, "ricochet"))
            {
                p.Mask(WeaponHeadRicochetA); p.Mask(WeaponHeadRicochetB); p.Mask(WeaponHeadRicochetC);
                p.Outline(WeaponHeadRicochet,false);
                p.Inlay(.55f,.37f, .80f,.37f, .41f,.61f, .17f,.61f,BladeSteel);
                if (Has(id, "long"))
                {
                    // Exposed turning vanes widen the two opposed elbows.
                    p.SolidBox(.72f,.25f,.23f,.17f);
                    p.SolidBox(.05f,.56f,.23f,.17f);
                    p.ColorBox(.75f,.29f,.15f,.09f,GlassInk);
                    p.ColorBox(.10f,.60f,.15f,.09f,GlassInk);
                }
                p.AccentBox(.36f,.11f,.22f,.06f);
            }
            else if (Has(id, "piercer"))
            {
                p.Mask(WeaponHeadPiercerLeft); p.Mask(WeaponHeadPiercerRight);
                p.SolidBox(.38f,.06f,.24f,.16f); p.Outline(WeaponHeadPiercer,false);
                p.Inlay(.22f,.66f, .40f,.66f, .39f,.87f, .21f,.87f,BladeSteel);
                p.Inlay(.60f,.66f, .77f,.66f, .79f,.87f, .61f,.87f,BladeSteel);
                p.AccentBox(.37f,.10f,.26f,.055f);
            }
            else
            {
                p.Mask(WeaponHeadGunStem); p.Mask(WeaponHeadGunCap);
                p.Outline(WeaponHeadGun,false);
                p.ColorBox(.33f,.51f,.34f,.11f,Copper);
                p.ColorBox(.30f,.86f,.40f,.055f,Paper);
                p.Seam(.49f,.22f,.49f,.46f);
            }
            if (!p.Tiny) p.Connect(.36f,.06f,.64f,.06f);
        }
    }
}
