namespace PaperDefense.Presentation
{
    public static partial class NightNotebookArt
    {
        // Rails, feet and braces are separate convex material faces inside certified bands.
        static readonly float[] ReinforcedHullContour =
        {
            .04f,.18f, .17f,.04f, .84f,.04f, .96f,.18f,
            .96f,.82f, .84f,.96f, .16f,.96f, .04f,.82f
        };
        static readonly float[] HullLeftGusset =
        {
            .06f,.20f, .22f,.20f, .35f,.76f, .18f,.91f, .06f,.77f
        };
        static readonly float[] HullRightGusset =
        {
            .78f,.20f, .94f,.20f, .94f,.77f, .82f,.91f, .65f,.76f
        };
        static readonly float[] FrontArmorContour =
        {
            .18f,.10f, .82f,.10f, .91f,.29f, .78f,.75f,
            .50f,.95f, .22f,.75f, .09f,.29f
        };
        static readonly float[] FullArmorContour =
        {
            .06f,.24f, .22f,.06f, .78f,.06f, .94f,.24f,
            .94f,.76f, .78f,.94f, .22f,.94f, .06f,.76f
        };
        static readonly float[] StandardFeedContour =
        {
            .24f,.08f, .76f,.08f, .86f,.22f, .82f,.80f,
            .70f,.92f, .29f,.92f, .18f,.79f, .14f,.22f
        };
        static readonly float[] BoostFeedContour =
        {
            .16f,.05f, .84f,.05f, .95f,.24f, .84f,.82f,
            .70f,.95f, .30f,.95f, .16f,.82f, .05f,.24f
        };

        static void FoundationFace(Drawing p, float[] contour)
        {
            p.Mask(contour);
            p.Outline(contour, false);
        }

        static void DrawFrame(Drawing p, string id)
        {
            bool outpost=Has(id,"outpost"),narrow=Has(id,"precision")||outpost;
            bool siege=Has(id,"siege"),heavy=Has(id,"heavy"),battery=Has(id,"battery");
            float left=outpost?.24f:narrow?.20f:siege?.14f:heavy||battery?.02f:.03f;
            float right=1-left;
            // Frame and hull bands touch in the certified contract. Material reaches
            // that shared edge; two separately inset ivory rims made railway tracks.
            p.BandBox(left,.22f,right-left,.78f);
            if(battery)
            {
                p.BandBox(.02f,0,.23f,.30f);p.BandBox(.385f,0,.23f,.30f);p.BandBox(.75f,0,.23f,.30f);
                p.BandBox(.29f,.22f,.065f,.30f,true);p.BandBox(.645f,.22f,.065f,.30f,true);
                p.Line(.02f,.12f,.25f,.12f);p.Line(.385f,.12f,.615f,.12f);p.Line(.75f,.12f,.98f,.12f);
            }
            else
            {
                float foot=outpost?.15f:narrow?.18f:heavy?.31f:siege?.26f:.25f;
                p.BandBox(left,0,foot,.30f);p.BandBox(right-foot,0,foot,.30f);
                p.Line(left,.12f,left+foot,.12f);p.Line(right-foot,.12f,right,.12f);
            }
            p.Line(left,.30f,left,.84f);p.Line(right,.30f,right,.84f);
            if(!p.Tiny)
            {
                if(outpost)p.BandBox(.44f,.35f,.12f,.48f,true);
                else if(narrow)p.BandBox(.42f,.40f,.16f,.34f,true);
                else if(siege||heavy)p.BandBox(.36f,.27f,.28f,.31f,true);
                else if(!battery)
                { p.BandBox(.25f,.44f,.17f,.23f,true);p.BandBox(.58f,.44f,.17f,.23f,true); }
                p.AccentBox(narrow?.46f:.43f,.66f,narrow?.08f:.14f,.10f);
            }
        }

        static void DrawHull(Drawing p, string id)
        {
            if (Has(id, "light"))
            {
                // Material is confined to the truss, leaving its openings clear.
                p.Beam(.14f,.18f,.14f,.82f,.13f);
                p.Beam(.86f,.18f,.86f,.82f,.13f);
                p.Beam(.18f,.14f,.82f,.14f,.13f);
                p.Beam(.18f,.86f,.82f,.86f,.13f);
                p.Beam(.22f,.24f,.78f,.76f,.085f);
                p.Beam(.22f,.76f,.78f,.24f,.085f);
                p.Disc(.15f,.18f,.035f,.045f,true);
                p.Disc(.85f,.82f,.035f,.045f,true);
            }
            else if (Has(id, "reinforced") || Has(id, "ribbed"))
            {
                FoundationFace(p, ReinforcedHullContour);
                p.SolidBox(.30f,.23f,.40f,.53f,true);
                FoundationFace(p, HullLeftGusset);
                FoundationFace(p, HullRightGusset);
                p.Fold(.71f,.71f,.90f,.24f,.83f,.76f);
                p.SolidBox(.13f,.13f,.10f,.74f);
                p.SolidBox(.77f,.13f,.10f,.74f);
                p.Seam(.24f,.15f,.76f,.15f);
                p.Seam(.24f,.85f,.76f,.85f);
                p.Detail(.40f,.38f,.60f,.38f);
                p.Detail(.40f,.60f,.60f,.60f);
                p.Disc(.18f,.26f,.025f,.035f,true);
                p.Disc(.82f,.74f,.025f,.035f,true);
                if(Has(id,"ribbed"))
                    for(int rib=0;rib<3;rib++)p.SolidBox(.32f+rib*.15f,.21f,.07f,.58f);
            }
            else
            {
                p.BandBox(.08f,0,.84f,1);
                if(!p.Tiny)p.BandBox(.33f,.16f,.34f,.59f,true);
                p.Line(.13f,.94f,.87f,.94f);
                p.Disc(.14f,.39f,.027f,.04f,true);
                p.Disc(.86f,.63f,.027f,.04f,true);
            }
            p.Connect(.45f,.86f,.45f,.98f);
            p.Connect(.55f,.86f,.55f,.98f);
        }

        static void DrawArmor(Drawing p, string id)
        {
            if (Has(id, "front"))
            {
                // The shield points +Y with the fixed foundation, not with each gun.
                p.SolidBox(.13f,.03f,.22f,.17f);
                p.SolidBox(.65f,.03f,.22f,.17f);
                FoundationFace(p, FrontArmorContour);
                p.Fold(.50f,.16f,.83f,.30f,.50f,.86f);
                p.Seam(.50f,.18f,.50f,.86f);
                p.Seam(.22f,.28f,.43f,.75f);
                p.Seam(.78f,.28f,.57f,.75f);
                p.Disc(.25f,.27f,.035f,.04f,true);
                p.Disc(.75f,.27f,.035f,.04f,true);
                p.Connect(.50f,.74f,.50f,.95f);
            }
            else
            {
                // Four separate plates wrap the recessed body.
                p.SolidBox(.06f,.22f,.22f,.56f);
                p.SolidBox(.72f,.22f,.22f,.56f);
                p.SolidBox(.22f,.06f,.56f,.22f);
                p.SolidBox(.22f,.72f,.56f,.22f);
                p.Outline(FullArmorContour, false);
                p.Seam(.50f,.08f,.50f,.26f);
                p.Seam(.50f,.74f,.50f,.92f);
                p.Disc(.16f,.49f,.035f,.045f,true);
                p.Disc(.84f,.49f,.035f,.045f,true);
                p.Disc(.35f,.16f,.025f,.035f,true);
                p.Disc(.65f,.84f,.025f,.035f,true);
            }
            p.Connect(.08f,.47f,.20f,.47f);
        }

        static void DrawFeed(Drawing p, string id)
        {
            bool boost = Has(id, "boost");
            FoundationFace(p, boost ? BoostFeedContour : StandardFeedContour);
            // The base-mounted conveyor is distinct from the weapon's ammunition box.
            p.SolidBox(.32f,.17f,.40f,.65f,true);
            p.Disc(.52f,.26f,.17f,.105f);
            p.Disc(.52f,.74f,.17f,.105f);
            p.Disc(.52f,.26f,.055f,.037f,true);
            p.Disc(.52f,.74f,.055f,.037f,true);
            for (int step = 0; step < 3; step++)
                p.SolidBox(.32f,.39f+step*.12f,.40f,.055f);
            if (boost)
            {
                p.Disc(.24f,.51f,.19f,.22f);
                p.Disc(.24f,.51f,.085f,.10f,true);
                p.Seam(.10f,.37f,.18f,.44f);
                p.Seam(.10f,.65f,.18f,.58f);
                p.Seam(.24f,.30f,.24f,.39f);
                p.Seam(.24f,.63f,.24f,.72f);
                p.Connect(.38f,.50f,.50f,.50f);
                p.AccentBox(.74f,.35f,.07f,.30f);
            }
            else p.Seam(.24f,.29f,.24f,.70f);
            p.Connect(.50f,.02f,.50f,.17f);
            p.Connect(.50f,.83f,.50f,.98f);
        }

        static void DrawTurn(Drawing p, string id)
        {
            if (p.HasSupport)
            {
                // Native coordinates bridge the real gap without stretching the bearing.
                p.LocalSolidBox(-.050f,p.SupportBottom,.100f,-.022f-p.SupportBottom);
                p.LocalSolidBox(-.055f,p.SupportBottom,.110f,.017f);
                p.LocalSeam(-.015f,p.SupportBottom+.025f,-.015f,-.040f);
                p.LocalSeam(.015f,p.SupportBottom+.025f,.015f,-.040f);
            }
            p.LocalDisc(0,0,.055f,.047f);
            p.LocalDisc(0,0,.034f,.028f,true);
            p.LocalSolidBox(-.018f,-.012f,.036f,.024f);
            p.LocalConnect(-.046f,0,-.018f,0);
            p.LocalConnect(.018f,0,.046f,0);
            p.LocalConnect(0,-.039f,0,-.014f);
            p.LocalConnect(0,.014f,0,.039f);
            if (Has(id,"deliberate"))
            {
                p.LocalSolidBox(-.059f,-.040f,.022f,.080f);p.LocalSolidBox(.037f,-.040f,.022f,.080f);
                p.LocalEllipse(0,0,.058f,.050f);
            }
            if (Has(id, "quick"))
            {
                p.LocalSolidBox(.031f,-.059f,.030f,.050f);
                p.LocalDisc(.046f,-.019f,.016f,.022f);
                p.LocalSeam(.041f,-.052f,.041f,-.029f);
                p.LocalSeam(.051f,-.052f,.051f,-.029f);
                p.LocalConnect(.029f,-.014f,.045f,-.014f);
            }
        }
    }
}
