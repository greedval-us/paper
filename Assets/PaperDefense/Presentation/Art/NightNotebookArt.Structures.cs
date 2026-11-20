using UnityEngine;

namespace PaperDefense.Presentation
{
    public static partial class NightNotebookArt
    {
        // The large working faces survive at map scale. Split paper edges, stamps and
        // fasteners only appear when there is room for them in the workshop or on zoom.
        static readonly float[] InkBottle =
        {
            .16f,.29f, .20f,.16f, .43f,.12f, .70f,.33f, .80f,.58f,
            .68f,.80f, .49f,.84f, .28f,.68f, .17f,.48f
        };
        static readonly float[] SignalLens =
        {
            .24f,.59f, .32f,.48f, .62f,.48f, .79f,.61f, .77f,.79f,
            .64f,.91f, .40f,.91f, .25f,.79f
        };

        static void DrawSite(Drawing p, string id)
        {
            bool compact = Has(id, "compact");
            float left = compact ? .20f : .07f;
            float right = 1 - left;
            float bottom = compact ? .34f : .09f;
            if (Has(id, "prism"))
            {
                p.PaperFace(left,.34f, .48f,bottom, right,.35f, .52f,.77f);
                p.PaperFace(left,.34f, .48f,bottom, .49f,bottom+.15f, left,.49f);
                p.Disc(.50f,.49f,compact ? .21f : .29f,.18f);
                p.InlayDisc(.50f,.49f,.16f,.09f,Copper);
            }
            else if (Has(id, "slow"))
            {
                // A pressed, round drip pan with a narrow exposed pool under the nozzle.
                float half = (right-left)*.5f;
                p.Disc(.5f,.55f,half,.36f);
                p.InlayDisc(.5f,.62f,half*.82f,.20f,Paper);
                p.InlayDisc(.5f,.67f,half*.68f,.12f,GlassInk);
            }
            else if (Has(id, "beacon") || Has(id, "repair"))
            {
                // Individual shoes under the two legs leave a visible gap below the frame.
                p.PaperFace(left,bottom, left+.21f,bottom, left+.29f,.95f, left+.10f,.95f);
                p.PaperFace(right-.21f,bottom, right,bottom, right-.10f,.95f, right-.29f,.95f);
                p.Seam(left+.04f,bottom+.18f,left+.20f,bottom+.18f);
                p.Seam(right-.20f,bottom+.18f,right-.04f,bottom+.18f);
                if (Has(id, "beacon")) p.PaperFace(.43f,.30f, .58f,.30f, .55f,.98f, .46f,.98f);
            }
            else if (Has(id, "generator"))
            {
                p.PaperFace(left+.03f,bottom, right-.19f,bottom, right-.10f,.51f, left,.51f);
                p.PaperFace(left+.08f,.60f, right-.04f,.60f, right,.89f, left+.07f,.89f);
                p.BandBox(left+.15f,bottom+.13f,.09f,.76f-bottom);
                p.BandBox(right-.23f,bottom+.13f,.09f,.76f-bottom);
                p.AccentBox(right-.23f,.21f,.09f,.13f);
            }
            else
            {
                p.PaperFace(left,bottom, right-.06f,bottom, right,.69f, left+.07f,.73f);
                p.PaperFacet(left,bottom, right-.06f,bottom, right,.69f);
                if (Has(id, "spikes"))
                {
                    for (int guide = 0; guide < 3; guide++)
                        p.BandBox(left+(right-left)*(.15f+guide*.27f),.51f,.09f,.31f,true);
                }
                else
                {
                    p.ColorBox(left+.09f,bottom+.13f,.12f,.23f,Copper);
                    p.ColorBox(right-.24f,bottom+.13f,.12f,.23f,Copper);
                }
            }
        }

        static void DrawShell(Drawing p, string id)
        {
            bool light = Has(id, "light");
            float left = light ? .20f : .07f, right = 1 - left;
            // Low sill and side saddles overlap the site and working assembly. The
            // support stays legible without a second full plate obscuring their feet.
            p.BandBox(left,.17f,right-left,.24f);
            p.PaperFace(left,.07f, left+.21f,.07f, left+.26f,.78f, left+.10f,.78f);
            p.PaperFace(right-.21f,.07f, right,.07f, right-.10f,.78f, right-.26f,.78f);
            if (Has(id, "reinforced"))
            {
                p.PaperFace(.09f,.38f, .29f,.38f, .24f,.94f, .07f,.94f);
                p.PaperFace(.70f,.38f, .89f,.38f, .94f,.94f, .77f,.94f);
            }
            else if (!p.Tiny)
            {
                p.AccentBox(left+.10f,.34f,.055f,.14f);
                p.AccentBox(right-.16f,.34f,.055f,.14f);
            }
        }

        static void DrawFunction(Drawing p, string id)
        {
            if (Has(id,"wall")) Palisade(p, Has(id,"braced"));
            else if (Has(id,"slow")) InkDispenser(p, Has(id,"diluted"));
            else if (Has(id,"repair")) RepairCrane(p, Has(id,"focused"));
            else if (Has(id,"generator")) FlywheelPump(p, Has(id,"quick"));
            else if (Has(id,"spikes")) SpikeBed(p, Has(id,"barbed"));
            else if (Has(id,"beacon")) SignalBeacon(p, Has(id,"focused"));
            else if (Has(id,"prism")) PrismLens(p, Has(id,"focused"));
            else { p.Outline(Plate); p.Disc(.5f,.51f,.26f,.26f); }
        }

        static void PrismLens(Drawing p, bool focused)
        {
            float left = focused ? .20f : .08f, right = 1f - left;
            float tip = .91f + p.Activity * .06f;
            p.BandBox(.43f,.02f,.14f,.28f);
            p.PaperFace(left,.55f, .48f,.16f, right,.56f, .54f,tip);
            p.Inlay(left+.035f,.55f, .48f,.21f, .46f,.57f, .53f,tip-.06f,RedEnamel);
            p.Inlay(.48f,.21f, right-.035f,.56f, .61f,.59f, .46f,.57f,GlassInk);
            p.Inlay(.47f,.59f, .61f,.60f, right-.04f,.57f, .54f,tip-.05f,Pencil);
            p.Seam(.54f,tip-.04f,.46f,.57f);
            p.Seam(.46f,.57f,.48f,.19f);
            p.Beam(.15f,.11f,.31f,.31f,.075f);
            p.Beam(.83f,.11f,.68f,.31f,.075f);
            if (focused) p.Beam(.24f,.52f,.74f,.52f,.055f);
        }

        static void Picket(Drawing p, float x, float width, float tip)
        {
            float ridge = x + width*.60f;
            p.PaperFace(x,.10f, x+width,.13f, x+width,tip-.15f, x,tip-.22f);
            p.PaperFace(x,tip-.22f, x+width,tip-.15f, ridge,tip, x+.05f,tip-.12f);
            p.PaperFacet(ridge,.12f, x+width,.13f, ridge,tip);
            p.Seam(ridge,.19f,ridge,tip-.06f);
        }

        static void Palisade(Drawing p, bool braced)
        {
            p.BandBox(.11f,0,.12f,.24f);
            p.BandBox(.75f,0,.12f,.24f);
            Picket(p,.06f,.24f,.78f);
            Picket(p,.36f,.27f,.97f);
            Picket(p,.70f,.24f,.85f);
            p.PaperFace(.04f,.24f, .95f,.42f, .96f,.56f, .05f,.39f);
            p.Inlay(.05f,.27f, .93f,.44f, .94f,.53f, .06f,.37f,Copper);
            p.InlayDisc(.18f,.35f,.036f,.038f,Ink);
            p.InlayDisc(.81f,.47f,.036f,.038f,Ink);
            if (braced) p.Beam(.19f,.70f,.83f,.18f,.12f);
        }

        static void InkDispenser(Drawing p, bool diluted)
        {
            // An angled glass carboy feeds the lip of a low spill trough.
            p.BandBox(.17f,0,.14f,.30f);
            p.BandBox(.68f,0,.10f,.20f);
            p.Beam(.59f,.71f,.79f,.87f,.22f);
            p.Beam(.70f,.75f,.85f,.89f,.28f);
            p.Outline(InkBottle);
            p.PaperFacet(.20f,.16f, .43f,.12f, .70f,.33f);
            p.Disc(.46f,.49f,.228f,.255f,true);
            float level = (diluted ? .38f : .44f) + .07f*p.Activity;
            p.Inlay(.29f,.39f, .62f,.39f, .67f,level+.065f, .25f,level+.09f,GlassInk);
            p.Inlay(.38f,.27f, .53f,.26f, .62f,.39f, .29f,.39f,GlassInk);
            p.Inlay(.25f,level+.06f, .67f,level+.04f, .66f,level+.07f, .26f,level+.10f,Pencil);
            p.Inlay(.30f,.57f, .33f,.55f, .43f,.67f, .40f,.69f,Ink);
            p.Inlay(.69f,.78f, .74f,.74f, .86f,.86f, .81f,.91f,Copper);
            float right = diluted ? .96f : .83f;
            p.PaperFace(.04f,.10f, right-.05f,.06f, right,.19f, .08f,.23f);
            if (!p.Tiny) p.Connect(.13f,.17f,.52f,.15f);
        }

        static void RepairCrane(Drawing p, bool focused)
        {
            float reach = focused ? .65f : .78f;
            float wrist = (focused ? .75f : .65f) - p.Activity*.09f;
            p.BandBox(.14f,0,.16f,.24f);
            p.BandBox(.48f,0,.20f,.20f);
            p.PaperFace(.09f,.12f, .35f,.12f, .32f,.75f, .17f,.81f);
            p.PaperFacet(.09f,.17f, .17f,.81f, .23f,.20f);
            p.PaperFace(.16f,.76f, reach-.04f,wrist-.09f, reach+.08f,wrist+.05f, .26f,.93f);
            p.Disc(.23f,.81f,.095f,.10f);
            p.AccentBox(.18f,.78f,.10f,.07f);
            // Open pincer jaws and the clear space above the anvil give the arm a silhouette.
            p.Beam(reach,wrist,reach-.06f,wrist-.15f,.105f);
            p.Beam(reach-.06f,wrist-.15f,reach+.025f,wrist-.22f,.085f);
            p.Beam(reach+.09f,wrist,reach+.17f,wrist-.13f,.085f);
            p.Inlay(reach+.055f,wrist-.01f, reach+.138f,wrist-.14f,
                reach+.202f,wrist-.12f, reach+.12f,wrist+.02f,RedEnamel);
            p.Inlay(.136f,.28f, .316f,.28f, .306f,.55f, .164f,.56f,RedEnamel);
            p.ColorBox(.205f,.32f,.043f,.19f,Ink);
            p.ColorBox(.164f,.39f,.13f,.05f,Ink);
            p.PaperFace(.37f,.18f, .80f,.18f, .95f,.31f, .30f,.34f);
            p.SolidBox(.45f,.06f,.27f,.12f);
        }

        static void FlywheelPump(Drawing p, bool quick)
        {
            float press = p.Activity*.12f;
            p.BandBox(.19f,0,.20f,.20f);
            p.BandBox(.65f,0,.15f,.28f);
            p.PaperFace(.49f,.15f, .86f,.13f, .87f,.57f, .52f,.62f);
            p.Inlay(.53f,.18f, .85f,.15f, .84f,.39f, .52f,.44f,Copper);
            p.PaperFacet(.74f,.15f, .86f,.13f, .87f,.57f);
            p.SolidBox(.61f,.57f,.12f,.28f-press);
            p.PaperFace(.44f,.79f-press, .89f,.79f-press, .92f,.92f-press, .46f,.94f-press);
            p.Inlay(.46f,.805f-press, .88f,.805f-press, .90f,.88f-press, .47f,.90f-press,Copper);
            p.Disc(.32f,.37f,.28f,.30f);
            p.InlayDisc(.32f,.37f,.246f,.265f,Copper);
            p.InlayDisc(.32f,.37f,.193f,.210f,Paper);
            int spokes = p.Tiny ? 3 : 5;
            for (int i = 0; i < spokes; i++)
            {
                float angle = (i/(float)spokes + p.Activity*.24f + .09f)*2*Mathf.PI;
                Vector2 along = new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                Vector2 side = new Vector2(-along.y,along.x);
                Vector2 center = new Vector2(.32f,.37f);
                var a = center+along*.04f-side*.025f;
                var b = center+along*.20f-side*.035f;
                var c = center+along*.20f+side*.025f;
                var d = center+along*.04f+side*.045f;
                p.Inlay(a.x,a.y,b.x,b.y,c.x,c.y,d.x,d.y,Copper);
            }
            p.InlayDisc(.32f,.37f,.061f,.065f,Accent);
            p.AccentBox(.55f,.29f,quick ? .16f : .10f,.17f);
            if (quick) p.Beam(.84f,.42f,.95f,.63f,.085f);
        }

        static void SpikeBed(Drawing p, bool barbed)
        {
            float rise = p.Activity*.16f;
            p.BandBox(.16f,0,.12f,.25f);
            p.BandBox(.72f,0,.12f,.25f);
            p.PaperFace(.05f,.22f, .87f,.18f, .97f,.33f, .15f,.38f);
            p.InlayDisc(.21f,.29f,.14f,.054f,Paper);
            p.InlayDisc(.50f,.28f,.15f,.054f,Paper);
            p.InlayDisc(.78f,.27f,.14f,.054f,Paper);
            SpikeBlade(p,.09f,.31f,.28f,.04f,.56f+rise,barbed);
            SpikeBlade(p,.37f,.63f,.30f,.54f,.82f+rise,barbed);
            SpikeBlade(p,.68f,.89f,.28f,.95f,.63f+rise,barbed);
            p.PaperFace(.05f,.10f, .87f,.07f, .90f,.20f, .05f,.24f);
            p.PaperFacet(.05f,.10f, .87f,.07f, .90f,.20f);
            p.AccentBox(.40f,.14f,.20f,.045f);
        }

        static void SpikeBlade(Drawing p, float left, float right, float y, float tipX, float tipY, bool barbed)
        {
            float mid = (left+right)*.5f;
            p.PaperFace(left,y, mid,y-.08f, right,y, tipX,tipY);
            p.Inlay(mid,y-.06f, right-.012f,y, tipX,tipY-.03f, mid,y-.005f,BladeSteel);
            if (barbed) p.PaperFace(mid,y+.05f, right+.055f,y+.24f, right-.02f,y+.19f, mid,y+.16f);
        }

        static void SignalBeacon(Drawing p, bool focused)
        {
            // A glass reading lens stands high on an open tripod. The iris closes on a mark.
            p.BandBox(.12f,0,.13f,.18f);
            p.BandBox(.75f,0,.13f,.18f);
            p.PaperFace(.08f,.07f, .27f,.09f, .50f,.53f, .42f,.59f);
            p.PaperFace(.72f,.09f, .92f,.07f, .59f,.59f, .51f,.53f);
            p.SolidBox(.43f,.20f,.14f,.32f);
            p.Outline(SignalLens);
            p.PaperFacet(.25f,.79f, .40f,.91f, .63f,.90f);
            p.Disc(.51f,.70f,.228f,.178f,true);
            float aperture = (focused ? .194f : .163f) + p.Activity*.018f;
            p.Inlay(.51f-aperture,.69f, .51f,.84f, .51f+aperture,.71f, .51f,.56f,NightNotebookEffectPalette.Clean);
            p.Inlay(.51f-aperture,.69f, .51f,.84f, .556f,.77f, .48f,.685f,Pencil);
            p.PaperFacet(.51f,.56f, .51f+aperture,.71f, .48f,.685f);
            p.InlayDisc(.47f,.76f,.030f,.024f,Ink);
            if (focused) p.Beam(.70f,.83f,.86f,.94f,.095f);
            p.Detail(.31f,.59f,.40f,.54f);
        }
    }
}
