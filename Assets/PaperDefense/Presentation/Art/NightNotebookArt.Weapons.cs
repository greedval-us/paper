using UnityEngine;

namespace PaperDefense.Presentation
{
    public static partial class NightNotebookArt
    {
        // Convex material faces stay inside the certified +Y-forward regions. Open jaws,
        // rails and belt routes use separate pieces, retaining their actual negative space.
        static readonly float[] WeaponReceiver = { .06f,.24f,.28f,.07f,.79f,.07f,.94f,.24f,.94f,.79f,.76f,.93f,.16f,.93f,.06f,.78f };
        static readonly float[] WeaponPiercer = { .13f,.18f,.29f,.06f,.72f,.06f,.87f,.19f,.78f,.93f,.22f,.93f };
        static readonly float[] WeaponMortarLeft = { .07f,.21f,.24f,.13f,.32f,.27f,.29f,.78f,.17f,.88f,.06f,.73f };
        static readonly float[] WeaponMortarRight = { .76f,.13f,.93f,.21f,.94f,.73f,.83f,.88f,.71f,.78f,.68f,.27f };
        static readonly float[] WeaponMortarFoot = { .17f,.07f,.82f,.07f,.89f,.26f,.77f,.36f,.23f,.36f,.11f,.26f };
        static readonly float[] WeaponFan = { .37f,.06f,.63f,.06f,.97f,.78f,.97f,.94f,.03f,.94f,.03f,.78f };
        static readonly float[] WeaponDeflector = { .17f,.70f,.29f,.84f,.80f,.39f,.67f,.25f };
        static readonly float[] WeaponStaplerBase = { .10f,.11f,.83f,.07f,.94f,.20f,.79f,.36f,.11f,.38f };
        static readonly float[] WeaponStaplerHandle = { .12f,.34f,.28f,.23f,.85f,.66f,.90f,.84f,.77f,.94f,.16f,.51f };
        static readonly float[] WeaponStaplerHinge = { .08f,.24f,.25f,.24f,.26f,.70f,.09f,.70f };
        static readonly float[] WeaponBlotter = { .05f,.28f,.19f,.12f,.81f,.12f,.95f,.28f,.95f,.72f,.77f,.84f,.23f,.84f,.05f,.72f };
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
        static readonly float[] WeaponHeadFan = { .30f,.05f,.70f,.05f,.95f,.69f,.92f,.95f,.08f,.95f,.05f,.69f };
        static readonly float[] WeaponHeadWide = { .35f,.05f,.65f,.05f,.92f,.73f,.92f,.91f,.78f,.97f,.22f,.97f,.08f,.91f,.08f,.73f };
        static readonly float[] WeaponHeadPiercerLeft = { .24f,.05f,.43f,.05f,.42f,.93f,.29f,.98f,.20f,.91f };
        static readonly float[] WeaponHeadPiercerRight = { .57f,.05f,.76f,.05f,.80f,.91f,.71f,.98f,.58f,.93f };
        static readonly float[] WeaponHeadPiercer = { .24f,.05f,.76f,.05f,.80f,.91f,.71f,.98f,.58f,.93f,.57f,.22f,.43f,.22f,.42f,.93f,.29f,.98f,.20f,.91f };
        static readonly float[] WeaponHeadRicochetA = { .25f,.05f,.65f,.05f,.80f,.31f,.21f,.34f };
        static readonly float[] WeaponHeadRicochetB = { .21f,.34f,.80f,.31f,.59f,.53f,.36f,.53f };
        static readonly float[] WeaponHeadRicochetC = { .36f,.53f,.59f,.53f,.80f,.72f,.20f,.73f };
        static readonly float[] WeaponHeadRicochetD = { .20f,.73f,.80f,.72f,.65f,.96f,.31f,.96f };
        static readonly float[] WeaponHeadRicochet = { .25f,.05f,.65f,.05f,.80f,.31f,.59f,.53f,.80f,.72f,.65f,.96f,.31f,.96f,.20f,.73f,.36f,.53f,.21f,.34f };
        static readonly float[] WeaponHeadStaplerRoot = { .14f,.05f,.86f,.05f,.87f,.31f,.13f,.31f };
        static readonly float[] WeaponHeadStaplerLeft = { .13f,.31f,.36f,.31f,.34f,.96f,.10f,.96f };
        static readonly float[] WeaponHeadStaplerRight = { .64f,.31f,.87f,.31f,.90f,.96f,.66f,.96f };
        static readonly float[] WeaponHeadStapler = { .14f,.05f,.86f,.05f,.90f,.96f,.66f,.96f,.64f,.31f,.36f,.31f,.34f,.96f,.10f,.96f };
        static readonly float[] WeaponHeadBlotterStem = { .34f,.05f,.66f,.05f,.71f,.71f,.29f,.71f };
        static readonly float[] WeaponHeadBlotterPad = { .09f,.65f,.91f,.65f,.94f,.87f,.82f,.96f,.18f,.96f,.06f,.87f };
        static readonly float[] WeaponHeadBlotter = { .34f,.05f,.66f,.05f,.70f,.65f,.91f,.65f,.94f,.87f,.82f,.96f,.18f,.96f,.06f,.87f,.09f,.65f,.30f,.65f };
        static readonly float[] WeaponScopeTube = { .35f,.13f,.65f,.13f,.70f,.27f,.70f,.83f,.63f,.95f,.37f,.95f,.30f,.83f,.30f,.27f };
        static readonly float[] WeaponTrackingDish = { .14f,.73f,.29f,.93f,.68f,.91f,.84f,.73f,.65f,.48f,.44f,.41f,.27f,.53f };
        static readonly float[] WeaponIronFork = { .14f,.10f,.86f,.10f,.86f,.94f,.64f,.94f,.64f,.42f,.36f,.42f,.36f,.94f,.14f,.94f };
        static readonly float[] WeaponIronLeft = { .14f,.10f,.36f,.10f,.36f,.94f,.14f,.94f };
        static readonly float[] WeaponIronRight = { .64f,.10f,.86f,.10f,.86f,.94f,.64f,.94f };
        static readonly float[] WeaponIronBridge = { .36f,.10f,.64f,.10f,.64f,.42f,.36f,.42f };
        static readonly float[] WeaponMagazineBox = { .10f,.10f,.49f,.10f,.57f,.18f,.57f,.90f,.10f,.90f };
        static readonly float[] WeaponMagazineQuickBox = { .12f,.13f,.44f,.13f,.53f,.23f,.53f,.62f,.12f,.62f };
        static readonly float[] WeaponMagazineNeck = { .51f,.43f,.97f,.43f,.97f,.65f,.51f,.65f };
        static readonly float[] WeaponMagazineStandard = { .10f,.10f,.49f,.10f,.57f,.18f,.57f,.43f,.97f,.43f,.97f,.65f,.57f,.65f,.57f,.90f,.10f,.90f };
        static readonly float[] WeaponDrumNeck = { .69f,.43f,.97f,.43f,.97f,.65f,.69f,.65f };
        static readonly float[] WeaponBeltPack = { .07f,.17f,.17f,.08f,.54f,.08f,.63f,.18f,.60f,.56f,.07f,.56f };
        static readonly float[] WeaponBeltLeft = { .25f,.51f,.43f,.51f,.43f,.89f,.25f,.89f };
        static readonly float[] WeaponBeltTop = { .25f,.70f,.95f,.70f,.95f,.89f,.25f,.89f };
        static readonly float[] WeaponBeltNeck = { .76f,.44f,.95f,.44f,.95f,.89f,.76f,.89f };
        static readonly float[] WeaponMagazineBelt = { .07f,.17f,.17f,.08f,.54f,.08f,.63f,.18f,.60f,.56f,.43f,.56f,.43f,.70f,.76f,.70f,.76f,.44f,.95f,.44f,.95f,.89f,.25f,.89f,.25f,.56f,.07f,.56f };
        static readonly float[] WeaponTrigger = { .18f,.10f,.77f,.10f,.89f,.26f,.81f,.79f,.56f,.93f,.26f,.83f,.12f,.61f };
        static readonly float[] WeaponMount = { .12f,.04f,.88f,.04f,.88f,.31f,.75f,.35f,.75f,.15f,.25f,.15f,.25f,.35f,.12f,.31f };
        static readonly float[] WeaponMountBraced = { .08f,.04f,.92f,.04f,.92f,.15f,.81f,.35f,.70f,.35f,.70f,.15f,.30f,.15f,.30f,.35f,.19f,.35f,.08f,.15f };

        static void WeaponBody(Drawing p, float[] convexContour) => p.Outline(convexContour);

        static void DrawTinyMechanism(Drawing p,string id)
        {
            // At 3–8 actual pixels, several outlined subassemblies become a white
            // blob. Keep one large mass and the family's principal negative space.
            if(Has(id,"piercer"))
            {
                p.SolidBox(.08f,.10f,.28f,.80f);p.SolidBox(.64f,.10f,.28f,.80f);
                p.SolidBox(.14f,.08f,.72f,.16f);p.AccentBox(.40f,.20f+p.Fire*.40f,.20f,.12f);
            }
            else if(Has(id,"mortar"))
            {
                p.Disc(.50f,.56f-p.Fire*.05f,.44f,.37f);p.Disc(.50f,.66f-p.Fire*.05f,.26f,.22f,true);
                p.SolidBox(.24f,.08f,.52f,.18f);
            }
            else if(Has(id,"fan"))
            {
                WeaponBody(p,WeaponFan);p.SolidBox(.39f,.40f-p.Fire*.09f,.22f,.43f,true);
            }
            else if(Has(id,"ricochet"))
            {
                p.Disc(.50f,.50f,.44f,.43f);p.Disc(.50f,.50f,.27f,.26f,true);
                p.Hinged(WeaponDeflector,.5f,.5f,p.Fire*.28f);
            }
            else if(Has(id,"stapler"))
            {
                p.SolidBox(.10f,.08f,.81f,.18f);p.SolidBox(.10f,.24f,.20f,.44f);
                p.Hinged(WeaponStaplerHandle,.19f,.39f,-p.Fire*.22f);
            }
            else if(Has(id,"blotter"))
            {
                p.SolidBox(.05f,.24f+p.Fire*.07f,.90f,.48f-p.Fire*.07f);
                p.SolidBox(.20f,.41f,.60f,.13f,true);
            }
            else
            {
                WeaponBody(p,WeaponReceiver);p.SolidBox(.29f,.34f-p.Fire*.12f,.43f,.27f,true);
                p.AccentBox(.33f,.69f-p.Fire*.09f,.35f,.10f);
            }
        }

        static void DrawTinyHead(Drawing p,string id)
        {
            if(Has(id,"mortar"))
            { p.SolidBox(.34f,.07f,.32f,.57f);p.SolidBox(.08f,.59f,.84f,.34f);p.SolidBox(.28f,.77f,.44f,.12f,true); }
            else if(Has(id,"fan")||Has(id,"wide"))
            { WeaponBody(p,WeaponHeadFan);p.SolidBox(.27f,.76f,.46f,.12f,true); }
            else if(Has(id,"stapler"))
            { p.SolidBox(.10f,.08f,.28f,.86f);p.SolidBox(.62f,.08f,.28f,.86f);p.SolidBox(.28f,.08f,.44f,.17f); }
            else if(Has(id,"blotter")||Has(id,"absorb"))
            { p.SolidBox(.34f,.07f,.32f,.66f);p.SolidBox(.06f,.70f,.88f,.24f); }
            else if(Has(id,"ricochet"))
            { p.Mask(WeaponHeadRicochetA);p.Mask(WeaponHeadRicochetB);p.Mask(WeaponHeadRicochetC);p.Mask(WeaponHeadRicochetD);p.Outline(WeaponHeadRicochet,false); }
            else if(Has(id,"piercer"))
            { p.SolidBox(.10f,.08f,.30f,.87f);p.SolidBox(.60f,.08f,.30f,.87f); }
            else if(Has(id,"short"))
            { p.SolidBox(.34f,.07f,.32f,.36f);p.SolidBox(.10f,.38f,.80f,.57f); }
            else if(id=="head.long")p.SolidBox(.34f,.06f,.32f,.90f);
            else p.SolidBox(.19f,.06f,.62f,.90f);
        }

        static void DrawMechanism(Drawing p, string id)
        {
            if (Has(id,"mortar"))
            {
                WeaponBody(p,WeaponMortarFoot);
                WeaponBody(p,WeaponMortarLeft); WeaponBody(p,WeaponMortarRight);
                p.Fold(.74f,.32f,.89f,.24f,.86f,.73f);
                p.Disc(.50f,.59f-p.Fire*.07f,.37f,.33f); p.Disc(.50f,.68f-p.Fire*.07f,.24f,.19f,true);
                p.SolidBox(.36f,.17f,.28f,.14f); p.AccentBox(.39f,.20f,.22f,.07f);
            }
            else if (Has(id,"fan"))
            {
                WeaponBody(p,WeaponFan); p.Fold(.57f,.13f,.92f,.79f,.91f,.88f);
                p.SolidBox(.31f,.17f,.38f,.20f,true);
                p.Beam(.40f,.39f,.18f+p.Fire*.08f,.83f-p.Fire*.09f,.12f);
                p.Beam(.50f,.39f,.50f,.88f-p.Fire*.12f,.12f);
                p.Beam(.60f,.39f,.82f-p.Fire*.08f,.83f-p.Fire*.09f,.12f);
                p.AccentBox(.40f,.19f,.20f,.07f); p.Seam(.13f,.88f,.87f,.88f);
            }
            else if (Has(id,"ricochet"))
            {
                p.Disc(.50f,.50f,.44f,.43f); p.Disc(.50f,.50f,.32f,.31f,true);
                p.Disc(.35f,.40f,.15f,.18f); p.Disc(.66f,.58f,.15f,.18f);
                p.Disc(.35f,.40f,.055f,.065f,true); p.Disc(.66f,.58f,.055f,.065f,true);
                p.Hinged(WeaponDeflector,.50f,.50f,p.Fire*.28f); p.Seam(.24f,.73f,.71f,.33f);
                p.AccentBox(.43f,.12f,.14f,.07f);
            }
            else if (Has(id,"stapler"))
            {
                WeaponBody(p,WeaponStaplerBase); WeaponBody(p,WeaponStaplerHinge);
                p.Fold(.17f,.16f,.82f,.12f,.76f,.29f);
                p.Hinged(WeaponStaplerHandle,.19f,.39f,-p.Fire*.22f); p.Disc(.19f,.39f,.09f,.105f,true);
                p.SolidBox(.32f,.17f,.39f,.12f,true); p.AccentBox(.38f,.20f,.29f,.05f);
                p.Seam(.29f,.37f,.75f,.73f); p.Detail(.45f,.48f,.64f,.62f);
            }
            else if (Has(id,"blotter"))
            {
                WeaponBody(p,WeaponBlotter); p.Fold(.22f,.17f,.79f,.17f,.90f,.29f);
                p.SolidBox(.17f,.25f,.66f,.49f,true);
                p.SolidBox(.25f,.34f+p.Fire*.10f,.50f,.33f-p.Fire*.10f);
                p.Disc(.25f,.505f,.10f,.165f); p.Disc(.75f,.505f,.10f,.165f);
                p.Seam(.31f,.39f,.40f,.61f); p.Seam(.48f,.39f,.57f,.61f); p.Seam(.65f,.39f,.72f,.57f);
                p.AccentBox(.38f,.15f,.24f,.07f);
            }
            else if (Has(id,"piercer"))
            {
                WeaponBody(p,WeaponPiercer); p.Fold(.71f,.16f,.82f,.21f,.75f,.85f);
                p.SolidBox(.37f,.21f,.26f,.61f,true);
                p.SolidBox(.22f,.24f,.15f,.62f); p.SolidBox(.63f,.24f,.15f,.62f);
                p.SolidBox(.32f,.13f,.36f,.17f); p.AccentBox(.40f,.17f+p.Fire*.42f,.20f,.07f);
                p.Seam(.28f,.41f,.32f,.73f); p.Seam(.72f,.41f,.68f,.73f);
            }
            else
            {
                WeaponBody(p,WeaponReceiver); p.Fold(.77f,.13f,.89f,.27f,.84f,.80f);
                p.SolidBox(.23f,.34f,.51f,.32f,true);
                p.SolidBox(.29f,.39f-p.Fire*.14f,.32f,.20f); p.Beam(.22f,.75f-p.Fire*.09f,.76f,.75f-p.Fire*.09f,.12f);
                p.AccentBox(.28f,.22f,.23f,.07f); p.Seam(.35f,.44f,.56f,.44f);
                p.Detail(.80f,.34f,.80f,.65f);
            }
            p.Connect(.43f,.88f,.43f,.98f); p.Connect(.57f,.88f,.57f,.98f);
            p.Connect(.02f,.71f,.25f,.71f); p.Connect(.02f,.75f,.25f,.75f);
        }

        static void DrawHead(Drawing p, string id)
        {
            if (Has(id,"short"))
            {
                p.Mask(WeaponHeadShortStem); p.Mask(WeaponHeadShortSleeve); p.Outline(WeaponHeadShort,false);
                p.SolidBox(.21f,.43f,.58f,.14f,true); p.SolidBox(.24f,.82f,.52f,.07f,true);
                p.AccentBox(.34f,.64f,.32f,.05f);
            }
            else if (id=="head.long")
            {
                p.Mask(WeaponHeadLongRoot); p.Mask(WeaponHeadLongTube); p.Mask(WeaponHeadLongCap);
                p.Outline(WeaponHeadLong,false); p.Seam(.49f,.30f,.49f,.81f);
                p.Seam(.31f,.16f,.69f,.16f); p.AccentBox(.34f,.09f,.32f,.055f);
            }
            else if (Has(id,"wide"))
            {
                WeaponBody(p,WeaponHeadWide); p.SolidBox(.20f,.79f,.60f,.10f,true);
                p.Seam(.32f,.42f,.21f,.69f); p.Seam(.68f,.42f,.79f,.69f);
                p.AccentBox(.36f,.26f,.28f,.055f);
            }
            else if (Has(id,"stapler"))
            {
                p.Mask(WeaponHeadStaplerRoot); p.Mask(WeaponHeadStaplerLeft); p.Mask(WeaponHeadStaplerRight);
                p.Outline(WeaponHeadStapler,false);
                p.SolidBox(.20f,.73f,.10f,.16f,true); p.SolidBox(.70f,.73f,.10f,.16f,true);
                p.AccentBox(.28f,.15f,.44f,.07f); p.Seam(.20f,.40f,.24f,.63f); p.Seam(.80f,.40f,.76f,.63f);
            }
            else if (Has(id,"mortar"))
            {
                p.Mask(WeaponHeadMortarNeck); p.Mask(WeaponHeadMortarCup); p.Outline(WeaponHeadMortar,false);
                p.Disc(.50f,.82f,.40f,.12f); p.Disc(.50f,.82f,.27f,.060f,true);
                p.Seam(.28f,.62f,.72f,.62f); p.AccentBox(.35f,.27f,.30f,.06f);
            }
            else if (Has(id,"fan"))
            {
                bool focused=Has(id,"focus");
                if(focused)p.SolidBox(.22f,.20f,.56f,.68f);else WeaponBody(p,WeaponHeadFan);
                for(int i=0;i<3;i++)
                {
                    float x=(focused?.24f:.13f)+i*(focused?.19f:.28f);
                    p.SolidBox(x,.66f,focused?.14f:.18f,.28f); p.SolidBox(x+.025f,.83f,focused?.085f:.11f,.055f,true);
                }
                p.Seam(.32f,.32f,.20f,.58f); p.Seam(.68f,.32f,.80f,.58f);
                p.AccentBox(.37f,.22f,.26f,.06f);
            }
            else if (Has(id,"blotter") || Has(id,"absorb"))
            {
                p.Mask(WeaponHeadBlotterStem); p.Mask(WeaponHeadBlotterPad); p.Outline(WeaponHeadBlotter,false);
                p.Seam(.17f,.73f,.24f,.88f); p.Seam(.34f,.73f,.41f,.89f);
                p.Seam(.51f,.73f,.58f,.89f); p.Seam(.68f,.73f,.75f,.89f);
                p.Seam(.43f,.20f,.43f,.56f); p.AccentBox(.35f,.54f,.30f,.06f);
                if(Has(id,"absorb"))
                {
                    p.Disc(.50f,.43f,.31f,.17f);p.Disc(.50f,.43f,.17f,.08f,true);
                    p.SolidBox(.14f,.78f,.72f,.11f,true);p.AccentBox(.22f,.82f,.56f,.035f);
                }
            }
            else if (Has(id,"ricochet"))
            {
                p.Mask(WeaponHeadRicochetA); p.Mask(WeaponHeadRicochetB);
                p.Mask(WeaponHeadRicochetC); p.Mask(WeaponHeadRicochetD); p.Outline(WeaponHeadRicochet,false);
                p.Seam(.33f,.22f,.61f,.36f); p.Seam(.61f,.36f,.39f,.54f);
                p.Seam(.39f,.54f,.63f,.73f); p.Seam(.63f,.73f,.44f,.87f);
                p.AccentBox(.35f,.10f,.27f,.06f);
                if(Has(id,"long"))
                {
                    p.SolidBox(.27f,.19f,.13f,.15f,true);p.SolidBox(.60f,.56f,.13f,.15f,true);
                    p.AccentBox(.23f,.40f,.11f,.07f);p.AccentBox(.66f,.77f,.11f,.07f);
                }
            }
            else if (Has(id,"piercer"))
            {
                p.Mask(WeaponHeadPiercerLeft); p.Mask(WeaponHeadPiercerRight);
                p.SolidBox(.38f,.06f,.24f,.16f); p.Outline(WeaponHeadPiercer,false);
                p.Seam(.31f,.28f,.31f,.84f); p.Seam(.69f,.28f,.69f,.84f);
                p.AccentBox(.37f,.10f,.26f,.055f);
            }
            else
            {
                p.Mask(WeaponHeadGunStem); p.Mask(WeaponHeadGunCap); p.Outline(WeaponHeadGun,false);
                p.SolidBox(.31f,.87f,.38f,.045f,true); p.Seam(.48f,.23f,.48f,.72f);
                p.AccentBox(.35f,.17f,.30f,.05f);
            }
            p.Connect(.36f,.06f,.64f,.06f);
        }

        static void DrawSight(Drawing p, string id)
        {
            if(Has(id,"scope") || Has(id,"survey"))
            {
                WeaponBody(p,WeaponScopeTube);
                p.SolidBox(.27f,.23f,.46f,.13f); p.SolidBox(.27f,.72f,.46f,.13f);
                p.Disc(.50f,.88f,.23f,.07f,true); p.Seam(.48f,.42f,.48f,.65f);
                p.AccentBox(.40f,.17f,.20f,.06f);
                if(Has(id,"survey")) { p.SolidBox(.19f,.33f,.62f,.10f);p.SolidBox(.19f,.63f,.62f,.10f); }
            }
            else if(Has(id,"tracking") || Has(id,"snap"))
            {
                p.SolidBox(.38f,.11f,.20f,.43f); WeaponBody(p,WeaponTrackingDish);
                p.Disc(.48f,.70f,.20f,.12f,true); p.Beam(.49f,.69f,.88f,.94f,.07f);
                p.AccentBox(.40f,.18f,.16f,.06f);
                if(Has(id,"snap")) { p.SolidBox(.14f,.59f,.21f,.30f,true);p.AccentBox(.63f,.70f,.13f,.08f); }
            }
            else
            {
                p.Mask(WeaponIronLeft); p.Mask(WeaponIronRight); p.Mask(WeaponIronBridge);
                p.Outline(WeaponIronFork,false); p.Seam(.23f,.22f,.77f,.22f);
                p.AccentBox(.43f,.17f,.14f,.08f);
            }
            p.Connect(.52f,.20f,.98f,.20f); p.Connect(.52f,.32f,.98f,.32f);
        }

        static void DrawMagazine(Drawing p, string id)
        {
            if(id == "magazine.mortar.pair")
            {
                p.BandBox(.36f,.12f,.28f,.72f);
                for (int tube = 0; tube < 2; tube++)
                {
                    float x = .12f + tube * .42f;
                    p.PaperFace(x,.25f, x+.28f,.25f, x+.28f,.79f, x,.79f);
                    p.Disc(x+.14f,.79f,.14f,.13f);
                    p.InlayDisc(x+.14f,.79f,.075f,.067f,Copper);
                    p.AccentBox(x,.29f,.28f,.08f);
                }
            }
            else if(Has(id,"quick"))
            {
                WeaponBody(p,WeaponMagazineQuickBox);p.SolidBox(.48f,.43f,.49f,.20f);
                p.SolidBox(.22f,.25f,.20f,.24f,true);
                p.AccentBox(.25f,.29f,.13f,.06f);p.AccentBox(.25f,.40f,.13f,.06f);
            }
            else if(Has(id,"drum") || Has(id,"reserve"))
            {
                bool reserve=Has(id,"reserve");
                WeaponBody(p,WeaponDrumNeck);
                p.Disc(.42f,.49f,reserve?.40f:.36f,reserve?.46f:.41f); p.Disc(.42f,.49f,.23f,.27f,true);
                p.Disc(.42f,.49f,.11f,.13f);
                p.Rotor(.42f,.49f,.18f,p.Reloading?p.Reload:0);
                p.Seam(.42f,.17f,.42f,.28f); p.Seam(.17f,.49f,.26f,.49f); p.Seam(.42f,.70f,.42f,.81f);
                p.AccentBox(.37f,.45f,.10f,.08f);
            }
            else if(Has(id,"belt"))
            {
                p.Mask(WeaponBeltPack); p.Mask(WeaponBeltLeft); p.Mask(WeaponBeltTop); p.Mask(WeaponBeltNeck);
                p.Outline(WeaponMagazineBelt,false); p.SolidBox(.17f,.21f,.33f,.22f,true);
                for(int i=0;i<4;i++)
                    p.AccentBox(.28f+i*.15f+(p.Reloading?Mathf.Repeat(p.Reload*2,1)*.08f:0),.73f,.075f,.12f);
                p.Seam(.32f,.58f,.41f,.58f); p.Seam(.80f,.51f,.91f,.51f);
            }
            else
            {
                p.Mask(WeaponMagazineBox); p.Mask(WeaponMagazineNeck); p.Outline(WeaponMagazineStandard,false);
                p.SolidBox(.23f,.28f,.28f,.43f,true);
                for(int i=0;i<3;i++) p.AccentBox(.28f,.34f+i*.13f,.18f,.065f);
                p.Seam(.18f,.79f,.50f,.79f);
            }
            p.Connect(.84f,.46f,.98f,.46f); p.Connect(.84f,.62f,.98f,.62f);
        }

        static void DrawTrigger(Drawing p, string id)
        {
            WeaponBody(p,WeaponTrigger);
            if(Has(id,"burst"))
            {
                for(int i=0;i<3;i++)
                {
                    float y=.29f+i*.21f;
                    p.Disc(.50f,y,.22f,.115f); p.Disc(.50f,y,.065f,.035f,true);
                }
                p.AccentBox(.20f,.23f,.09f,.46f);
            }
            else if(Has(id,"economy"))
            {
                p.Beam(.28f,.24f,.67f,.73f,.16f);p.Disc(.28f,.24f,.13f,.14f);
                p.SolidBox(.56f,.65f,.24f,.11f);p.AccentBox(.23f,.20f,.10f,.07f);
            }
            else
            {
                p.Disc(.50f,.50f,.25f,.29f); p.Disc(.50f,.50f,.105f,.125f,true);
                p.Seam(.50f,.50f,.67f,.68f); p.AccentBox(.24f,.18f,.20f,.07f);
            }
            p.Connect(.12f,.18f,.30f,.18f);
        }

        static void DrawMount(Drawing p, string id)
        {
            // This layer paints last. Its filled brackets stay below the receiver face.
            if(Has(id,"stabilized"))
            {
                p.SolidBox(.08f,.04f,.84f,.11f);
                p.Beam(.19f,.14f,.25f,.32f,.14f); p.Beam(.81f,.14f,.75f,.32f,.14f);
                p.Outline(WeaponMountBraced,false); p.Seam(.18f,.095f,.82f,.095f);
                p.AccentBox(.41f,.065f,.18f,.045f);
            }
            else if(Has(id,"light"))
            {
                // Separate feet and diagonal arms leave a large open underside. There is
                // no full-width base bar for the auto-fit to turn into another standard U.
                p.SolidBox(.14f,.05f,.21f,.08f); p.SolidBox(.65f,.05f,.21f,.08f);
                p.Beam(.24f,.12f,.46f,.30f,.07f); p.Beam(.76f,.12f,.54f,.30f,.07f);
                p.SolidBox(.42f,.28f,.16f,.07f);
                p.AccentBox(.18f,.07f,.10f,.035f); p.AccentBox(.72f,.07f,.10f,.035f);
                p.AccentBox(.45f,.295f,.10f,.035f);
                return;
            }
            else
            {
                p.SolidBox(.12f,.04f,.76f,.11f);
                p.SolidBox(.12f,.15f,.13f,.16f); p.SolidBox(.75f,.15f,.13f,.16f);
                p.Outline(WeaponMount,false); p.Seam(.25f,.095f,.75f,.095f);
                p.AccentBox(.42f,.065f,.16f,.045f);
            }
            p.Connect(.44f,.04f,.44f,.12f); p.Connect(.59f,.04f,.59f,.12f);
        }
    }
}
