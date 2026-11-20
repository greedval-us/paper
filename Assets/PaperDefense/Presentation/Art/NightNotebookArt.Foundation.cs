using PaperDefense.Core;

namespace PaperDefense.Presentation
{
    public static partial class NightNotebookArt
    {
        internal const float RibbedDeckTop = .68f;
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
        static readonly float[] CrawlerTread =
        {
            .02f,.50f, .12f,.08f, .88f,.08f, .98f,.50f,
            .88f,.90f, .12f,.90f
        };
        static readonly float[] SiegeShoe =
        {
            .01f,.10f, .28f,.07f, .44f,.47f, .41f,.86f, .22f,.73f
        };
        static readonly float[] SiegeHeel =
        {
            .59f,.86f, .56f,.47f, .72f,.07f, .99f,.10f, .78f,.73f
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
        static void FoundationFace(Drawing p, float[] contour)
        {
            p.Mask(contour);
            p.Outline(contour, false);
        }

        static void DrawFrame(Drawing p, string id)
        {
            // The top lip reaches the real hull band. The negative space below it
            // belongs to the frame and stays visible even under a standard hull.
            if (Has(id, "heavy"))
            {
                p.BandBox(.20f,.77f,.60f,.23f);
                FoundationFace(p, CrawlerTread);
                p.ColorBox(.18f,.26f,.64f,.47f,Paper);
                for (int wheel = 0; wheel < 3; wheel++)
                {
                    float x = .26f + wheel*.24f;
                    p.InlayDisc(x,.50f,.058f,.26f,BladeSteel);
                    if (!p.Tiny) p.InlayDisc(x,.50f,.018f,.085f,Accent);
                }
                return;
            }
            if (Has(id, "battery"))
            {
                p.BandBox(.02f,.71f,.96f,.29f);
                for (int bay = 0; bay < 3; bay++)
                {
                    float x = .03f + bay*.33f;
                    p.PaperFace(x,.06f, x+.21f,.06f, x+.27f,.72f, x+.04f,.72f);
                    p.Inlay(x+.025f,.14f, x+.20f,.14f, x+.22f,.33f, x+.04f,.33f,BladeSteel);
                }
                return;
            }
            if (Has(id, "siege"))
            {
                p.BandBox(.29f,.65f,.42f,.35f);
                FoundationFace(p, SiegeShoe);
                FoundationFace(p, SiegeHeel);
                p.Inlay(.01f,.10f,.28f,.07f,.33f,.21f,.06f,.26f,BladeSteel);
                p.Inlay(.67f,.21f,.72f,.07f,.99f,.10f,.94f,.26f,BladeSteel);
                p.PaperFacet(.23f,.68f,.37f,.70f,.28f,.25f,true);
                p.PaperFacet(.77f,.68f,.63f,.70f,.72f,.25f,true);
                return;
            }
            if (Has(id, "outpost"))
            {
                // A single neck on a spreading plinth, like a folded map pin.
                p.BandBox(.42f,.54f,.16f,.46f);
                p.PaperFace(.17f,.08f,.83f,.08f,.58f,.61f,.42f,.61f);
                p.Inlay(.18f,.10f,.82f,.10f,.75f,.26f,.25f,.26f,Copper);
                p.Seam(.50f,.29f,.50f,.58f);
                return;
            }
            if (Has(id, "precision"))
            {
                p.BandBox(.35f,.81f,.30f,.19f);
                p.PaperFace(.17f,.07f,.28f,.07f,.48f,.84f,.37f,.84f);
                p.PaperFace(.72f,.07f,.83f,.07f,.63f,.84f,.52f,.84f);
                p.BandBox(.31f,.42f,.38f,.12f);
                p.AccentBox(.43f,.73f,.14f,.10f);
                return;
            }
            // Folded workbench with two wide feet and an open arch between them.
            p.BandBox(.10f,.61f,.80f,.39f);
            p.PaperFace(.04f,.08f,.26f,.08f,.35f,.67f,.14f,.67f);
            p.PaperFace(.74f,.08f,.96f,.08f,.86f,.67f,.65f,.67f);
            p.Inlay(.05f,.09f,.25f,.09f,.28f,.29f,.08f,.29f,BladeSteel);
            p.Inlay(.75f,.09f,.95f,.09f,.92f,.29f,.72f,.29f,BladeSteel);
            p.Seam(.31f,.74f,.69f,.74f);
        }

        static void DrawHull(Drawing p, string id)
        {
            if (Has(id, "light"))
            {
                p.BandBox(.08f,.04f,.84f,.17f);
                p.BandBox(.19f,.78f,.62f,.22f);
                p.PaperFace(.08f,.18f,.19f,.18f,.29f,.81f,.19f,.81f);
                p.PaperFace(.81f,.18f,.92f,.18f,.81f,.81f,.71f,.81f);
                p.Beam(.25f,.79f,.50f,.15f,.09f);
                p.Beam(.50f,.15f,.75f,.79f,.09f);
                if (!p.Tiny) p.InlayDisc(.50f,.18f,.042f,.08f,Copper);
            }
            else if (Has(id, "ribbed"))
            {
                // Three raised folds break the top and bottom silhouette. The middle
                // rib sticks out from the body even when the side ribs run under other parts.
                p.BandBox(.06f,.32f,.88f,RibbedDeckTop-.32f);
                for (int rib = 0; rib < 3; rib++)
                {
                    float x = .07f + rib*.30f;
                    float tip = rib == 1 ? .97f : .83f;
                    p.PaperFace(x,.30f, x+.12f,.04f, x+.24f,tip-.17f, x+.12f,tip);
                    p.PaperFacet(x+.12f,.11f, x+.22f,tip-.19f, x+.12f,tip-.04f);
                    p.Seam(x+.12f,.17f,x+.12f,tip-.12f);
                }
            }
            else if (Has(id, "reinforced"))
            {
                FoundationFace(p, ReinforcedHullContour);
                p.Inlay(.29f,.29f,.71f,.29f,.65f,.68f,.35f,.68f,BladeSteel);
                FoundationFace(p, HullLeftGusset);
                FoundationFace(p, HullRightGusset);
                p.Fold(.71f,.71f,.90f,.24f,.83f,.76f);
                if (!p.Tiny)
                {
                    p.Seam(.24f,.15f,.76f,.15f);
                    p.Seam(.24f,.85f,.76f,.85f);
                    p.InlayDisc(.16f,.31f,.035f,.055f,Copper);
                    p.InlayDisc(.84f,.31f,.035f,.055f,Copper);
                }
            }
            else
            {
                // One folded carton and its closure strip make the stock body.
                p.BandBox(.11f,0,.78f,1);
                p.Line(.11f,.10f,.11f,.90f);
                p.Line(.89f,.10f,.89f,.90f);
                p.PaperFacet(.11f,.08f,.28f,.08f,.11f,.84f);
                p.PaperFacet(.89f,.08f,.72f,.08f,.89f,.84f);
                p.ColorBox(.46f,.09f,.08f,.82f,Copper);
                p.Seam(.16f,.16f,.84f,.16f);
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
                p.PaperFacet(.50f,.16f,.83f,.30f,.50f,.86f);
                p.Seam(.50f,.18f,.50f,.86f);
                p.Inlay(.27f,.22f,.50f,.36f,.50f,.46f,.25f,.31f,BladeSteel);
                p.Inlay(.50f,.36f,.73f,.22f,.75f,.31f,.50f,.46f,BladeSteel);
                p.Connect(.50f,.78f,.50f,.95f);
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
                p.ColorBox(.09f,.34f,.10f,.32f,BladeSteel);
                p.ColorBox(.81f,.34f,.10f,.32f,BladeSteel);
                if (!p.Tiny)
                {
                    p.InlayDisc(.36f,.16f,.035f,.045f,Copper);
                    p.InlayDisc(.64f,.84f,.035f,.045f,Copper);
                }
            }
            p.Connect(.08f,.47f,.20f,.47f);
        }

        static void DrawFeed(Drawing p, string id)
        {
            if (Has(id, "boost"))
            {
                // A stepped housing and exposed drive wheel identify the fast feeder.
                p.PaperFace(.41f,.13f,.92f,.13f,.86f,.82f,.49f,.92f);
                p.ColorBox(.57f,.22f,.22f,.54f,BladeSteel);
                p.Disc(.28f,.54f,.26f,.29f);
                p.InlayDisc(.28f,.54f,.125f,.14f,Copper);
                if (!p.Tiny)
                {
                    p.InlayDisc(.28f,.54f,.055f,.063f,Paper);
                    p.AccentBox(.58f,.68f,.19f,.10f);
                }
            }
            else
            {
                FoundationFace(p, StandardFeedContour);
                p.ColorBox(.36f,.19f,.28f,.62f,Paper);
                p.InlayDisc(.50f,.24f,.15f,.09f,Copper);
                p.InlayDisc(.50f,.76f,.15f,.09f,Copper);
                if (!p.Tiny)
                    for (int step = 0; step < 3; step++)
                        p.ColorBox(.34f,.36f+step*.11f,.32f,.055f,Body);
            }
            p.Connect(.50f,.02f,.50f,.17f);
            p.Connect(.50f,.83f,.50f,.98f);
        }

        static void DrawWeaponSupport(Drawing p)
        {
            const float top = -.022f;
            float foot = p.SupportBottom;
            float height = top - foot;
            // These pieces bridge the chassis and its occupied socket. The hub above
            // them still depicts the turn part selected by the player.
            switch (p.WeaponFamily)
            {
                case AssemblyWeaponFamily.Piercer:
                    p.TaperedSupport(foot, -.056f, -.030f, top, -.042f, -.018f);
                    p.TaperedSupport(foot, .030f, .056f, top, .018f, .042f);
                    p.LocalSolidBox(-.041f, foot + height * .43f, .082f, height * .10f);
                    break;
                case AssemblyWeaponFamily.Mortar:
                    p.TaperedSupport(foot, -.054f, .054f, foot + height * .35f, -.031f, .031f);
                    p.TaperedSupport(foot + height * .34f, -.031f, .031f, top, -.056f, .056f);
                    p.LocalSolidBox(-.049f, foot + height * .54f, .098f, height * .13f);
                    break;
                case AssemblyWeaponFamily.Fan:
                    p.TaperedSupport(foot, -.023f, .001f, top, -.057f, -.031f);
                    p.TaperedSupport(foot, -.001f, .023f, top, .031f, .057f);
                    break;
                case AssemblyWeaponFamily.Ricochet:
                    float elbow = foot + height * .48f;
                    p.TaperedSupport(foot, -.046f, -.010f, elbow, .016f, .052f);
                    p.TaperedSupport(elbow, .016f, .052f, top, -.030f, .008f);
                    break;
                case AssemblyWeaponFamily.Stapler:
                    p.TaperedSupport(foot, -.054f, -.016f, top, -.046f, -.008f);
                    p.TaperedSupport(foot + height * .14f, -.022f, .016f, top, .024f, .055f);
                    break;
                case AssemblyWeaponFamily.Blotter:
                    p.LocalSolidBox(-.024f, foot, .048f, height);
                    p.LocalDisc(0, foot + height * .54f, .052f, height * .28f);
                    p.LocalDisc(0, foot + height * .54f, .022f, height * .12f, true);
                    break;
                default:
                    p.LocalSolidBox(-.050f, foot, .100f, height);
                    p.LocalSeam(-.015f, foot + .025f, -.015f, -.040f);
                    p.LocalSeam(.015f, foot + .025f, .015f, -.040f);
                    break;
            }
            p.LocalSolidBox(-.055f, foot, .110f, .017f);
        }

        static void DrawTurn(Drawing p, string id)
        {
            if (p.HasSupport) DrawWeaponSupport(p);
            if (Has(id, "deliberate"))
            {
                // Squared brake shoes flank the exposed round flywheel.
                p.LocalSolidBox(-.059f,-.035f,.118f,.070f);
                p.LocalDisc(0,0,.039f,.052f);
                p.LocalSolidBox(-.059f,-.045f,.020f,.090f);
                p.LocalSolidBox(.039f,-.045f,.020f,.090f);
                p.LocalSolidBox(-.013f,-.016f,.026f,.032f,true);
                p.LocalConnect(-.052f,-.018f,-.052f,.018f);
                p.LocalConnect(.052f,-.018f,.052f,.018f);
            }
            else if (Has(id, "quick"))
            {
                // Off-axis pinion and lightweight annular bearing.
                p.LocalDisc(-.006f,.005f,.045f,.047f);
                p.LocalDisc(-.006f,.005f,.023f,.024f,true);
                p.LocalDisc(.039f,-.031f,.021f,.022f);
                p.LocalConnect(.026f,-.043f,.050f,-.021f);
                p.LocalConnect(-.041f,.006f,-.025f,.006f);
            }
            else
            {
                p.LocalDisc(0,0,.055f,.047f);
                p.LocalDisc(0,0,.034f,.028f,true);
                p.LocalSolidBox(-.018f,-.012f,.036f,.024f);
                p.LocalConnect(-.046f,0,-.018f,0);
                p.LocalConnect(.018f,0,.046f,0);
                p.LocalConnect(0,-.039f,0,-.014f);
                p.LocalConnect(0,.014f,0,.039f);
            }
        }
    }
}
