using UnityEngine;

namespace PaperDefense.Presentation
{
    public static partial class NightNotebookArt
    {
        // Coordinates are local to the certified attachment sockets. The right-hand
        // edges on sights and magazines meet the receiver in every assembled pose.
        static readonly float[] IronFork = { .10f,.09f, .88f,.09f, .88f,.91f, .68f,.91f, .68f,.39f, .30f,.39f, .30f,.91f, .10f,.91f };
        static readonly float[] ScopeTube = { .38f,.07f, .62f,.07f, .67f,.24f, .66f,.72f, .34f,.72f, .33f,.24f };
        static readonly float[] DishBowl = { .06f,.82f, .85f,.63f, .74f,.44f, .50f,.36f, .28f,.44f, .13f,.59f };
        static readonly float[] SnapFin = { .12f,.44f, .78f,.95f, .65f,.47f, .34f,.25f };
        static readonly float[] BoxMagazine = { .11f,.09f, .51f,.09f, .58f,.18f, .58f,.88f, .11f,.88f };
        static readonly float[] BoxWithNeck = { .11f,.09f, .51f,.09f, .58f,.18f, .58f,.43f, .96f,.43f, .96f,.65f, .58f,.65f, .58f,.88f, .11f,.88f };
        static readonly float[] BeltChest = { .06f,.17f, .16f,.07f, .55f,.07f, .64f,.17f, .61f,.57f, .06f,.57f };
        static readonly float[] BeltLoop = { .06f,.17f, .16f,.07f, .55f,.07f, .64f,.17f, .61f,.57f, .42f,.57f, .42f,.70f, .75f,.70f, .75f,.43f, .96f,.43f, .96f,.91f, .23f,.91f, .23f,.57f, .06f,.57f };
        static readonly float[] QuickWedge = { .09f,.20f, .24f,.12f, .70f,.46f, .60f,.67f, .18f,.57f };
        static readonly float[] TriggerPaddle = { .13f,.12f, .48f,.09f, .71f,.25f, .69f,.47f, .48f,.55f, .20f,.46f };
        static readonly float[] BurstComb = { .11f,.12f, .49f,.12f, .56f,.23f, .55f,.86f, .18f,.92f, .10f,.79f };

        static void DrawSight(Drawing p, string id)
        {
            switch (id)
            {
                case "sight.scope":
                    // A telescope with a flared objective pointing along +Y.
                    p.Outline(ScopeTube);
                    p.PaperFace(.34f,.62f, .66f,.62f, .75f,.94f, .25f,.94f);
                    p.ColorBox(.32f,.82f,.36f,.08f,GlassInk);
                    p.Seam(.38f,.37f,.62f,.37f);
                    break;
                case "sight.survey":
                    // Two widely spaced glass windows form a transverse rangefinder.
                    p.SolidBox(.40f,.10f,.20f,.44f);
                    p.PaperFace(.08f,.43f, .92f,.43f, .86f,.72f, .14f,.72f);
                    p.SolidBox(.06f,.39f,.22f,.44f);
                    p.SolidBox(.72f,.39f,.22f,.44f);
                    p.ColorBox(.10f,.62f,.14f,.13f,GlassInk);
                    p.ColorBox(.76f,.62f,.14f,.13f,GlassInk);
                    break;
                case "sight.tracking":
                    p.Beam(.41f,.12f,.52f,.45f,.20f);
                    p.Outline(DishBowl);
                    p.Beam(.48f,.70f,.64f,.94f,.08f);
                    p.ColorBox(.57f,.85f,.15f,.09f,GlassInk);
                    p.Seam(.23f,.69f,.70f,.57f);
                    break;
                case "sight.snap":
                    // A pointed indexing vane reads differently from a dish in profile.
                    p.SolidBox(.15f,.11f,.45f,.18f);
                    p.Outline(SnapFin);
                    p.Inlay(.26f,.46f, .63f,.76f, .54f,.55f, .34f,.38f,RedEnamel);
                    break;
                default:
                    p.Mask(IronForkFoot); p.Mask(IronForkLeft); p.Mask(IronForkRight);
                    p.Outline(IronFork, false);
                    p.AccentBox(.15f,.75f,.11f,.11f);
                    p.AccentBox(.72f,.75f,.11f,.11f);
                    break;
            }
            p.Connect(.50f,.23f,.97f,.23f);
            if (!p.Tiny) p.Connect(.50f,.31f,.97f,.31f);
        }

        static readonly float[] IronForkFoot = { .10f,.09f, .88f,.09f, .88f,.39f, .10f,.39f };
        static readonly float[] IronForkLeft = { .10f,.39f, .30f,.39f, .30f,.91f, .10f,.91f };
        static readonly float[] IronForkRight = { .68f,.39f, .88f,.39f, .88f,.91f, .68f,.91f };

        static void DrawMagazine(Drawing p, string id)
        {
            switch (id)
            {
                case "magazine.drum":
                    p.SolidBox(.67f,.43f,.29f,.22f);
                    p.Disc(.43f,.50f,.39f,.43f);
                    p.InlayDisc(.43f,.50f,.19f,.21f,BladeSteel);
                    p.Rotor(.43f,.50f,.17f,p.Reloading ? p.Reload : 0);
                    p.Seam(.16f,.50f,.22f,.50f);
                    p.Seam(.43f,.20f,.43f,.27f);
                    break;
                case "magazine.reserve":
                    // Offset bobbins sitting against one another, carried on one bridge.
                    p.SolidBox(.29f,.40f,.67f,.23f);
                    p.Disc(.31f,.64f,.26f,.27f);
                    p.Disc(.61f,.34f,.26f,.27f);
                    p.InlayDisc(.31f,.64f,.10f,.11f,Copper);
                    p.InlayDisc(.61f,.34f,.10f,.11f,Copper);
                    break;
                case "magazine.belt":
                    p.Mask(BeltChest);
                    p.ColorBox(.23f,.55f,.19f,.36f,Body);
                    p.ColorBox(.23f,.70f,.73f,.21f,Body);
                    p.ColorBox(.75f,.43f,.21f,.27f,Body);
                    p.Outline(BeltLoop, false);
                    p.AccentBox(.13f,.36f,.41f,.10f);
                    if (!p.Tiny)
                    {
                        float travel = p.Reloading ? Mathf.Repeat(p.Reload * 2, 1) * .08f : 0;
                        for (int i = 0; i < 4; i++)
                            p.ColorBox(.28f + i * .15f + travel,.73f,.065f,.15f,Copper);
                    }
                    break;
                case "magazine.quick":
                    p.SolidBox(.49f,.43f,.47f,.20f);
                    p.Outline(QuickWedge);
                    p.PaperFace(.11f,.54f, .29f,.58f, .32f,.84f, .12f,.84f);
                    p.ColorBox(.15f,.68f,.12f,.11f,RedEnamel);
                    p.Seam(.27f,.28f,.54f,.45f);
                    break;
                case "magazine.mortar.pair":
                    p.SolidBox(.23f,.31f,.73f,.22f);
                    for (int tube = 0; tube < 2; tube++)
                    {
                        float x = .08f + tube * .43f;
                        p.SolidBox(x,.21f,.33f,.57f);
                        p.Disc(x+.165f,.77f,.165f,.13f);
                        p.InlayDisc(x+.165f,.77f,.09f,.066f,Copper);
                        p.AccentBox(x+.03f,.27f,.27f,.07f);
                    }
                    break;
                default:
                    p.Mask(BoxMagazine);
                    p.ColorBox(.57f,.43f,.39f,.22f,Body);
                    p.Outline(BoxWithNeck, false);
                    p.ColorBox(.21f,.27f,.24f,.42f,BladeSteel);
                    if (!p.Tiny)
                        for (int i = 0; i < 3; i++) p.AccentBox(.25f,.32f+i*.12f,.16f,.055f);
                    p.Seam(.16f,.78f,.50f,.78f);
                    break;
            }
            p.Connect(.83f,.52f,.97f,.52f);
        }

        static void DrawTrigger(Drawing p, string id)
        {
            switch (id)
            {
                case "trigger.burst":
                    p.Outline(BurstComb);
                    for (int tooth = 0; tooth < 3; tooth++)
                    {
                        float y = .19f + tooth * .26f;
                        p.PaperFace(.46f,y, .87f,y+.07f, .79f,y+.18f, .46f,y+.15f);
                    }
                    p.ColorBox(.20f,.24f,.12f,.52f,Copper);
                    break;
                case "trigger.economy":
                    p.SolidBox(.11f,.11f,.51f,.18f);
                    p.Beam(.29f,.23f,.72f,.78f,.18f);
                    p.SolidBox(.52f,.74f,.36f,.15f);
                    p.InlayDisc(.28f,.22f,.10f,.09f,Copper);
                    break;
                default:
                    p.Beam(.40f,.35f,.67f,.83f,.20f);
                    p.PaperFace(.57f,.73f, .86f,.76f, .79f,.93f, .52f,.89f);
                    p.Outline(TriggerPaddle);
                    p.InlayDisc(.37f,.31f,.13f,.10f,Copper);
                    break;
            }
            p.Connect(.05f,.18f,.26f,.18f);
        }

        static void DrawMount(Drawing p, string id)
        {
            // This last layer only occupies the rear edge of the receiver. The
            // empty center identifies the support even with the turret mounted.
            switch (id)
            {
                case "mount.light":
                    p.SolidBox(.10f,.05f,.19f,.10f);
                    p.SolidBox(.71f,.05f,.19f,.10f);
                    p.Beam(.21f,.13f,.48f,.30f,.08f);
                    p.Beam(.52f,.30f,.79f,.13f,.08f);
                    p.AccentBox(.43f,.26f,.14f,.08f);
                    break;
                case "mount.stabilized":
                    p.Beam(.10f,.13f,.30f,.31f,.14f);
                    p.Beam(.70f,.31f,.90f,.13f,.14f);
                    p.SolidBox(.07f,.05f,.86f,.11f);
                    p.SolidBox(.17f,.24f,.18f,.10f);
                    p.SolidBox(.65f,.24f,.18f,.10f);
                    p.ColorBox(.15f,.08f,.70f,.05f,Copper);
                    break;
                default:
                    p.SolidBox(.13f,.05f,.74f,.10f);
                    p.SolidBox(.13f,.14f,.13f,.18f);
                    p.SolidBox(.74f,.14f,.13f,.18f);
                    p.AccentBox(.42f,.065f,.16f,.06f);
                    break;
            }
        }
    }
}
