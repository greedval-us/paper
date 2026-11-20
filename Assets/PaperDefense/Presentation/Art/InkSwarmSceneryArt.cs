using System;

namespace PaperDefense.Presentation
{
    /// <summary>Quiet ink scenery, authored once through the semantic shape cache.</summary>
    internal static class InkSwarmSceneryArt
    {
        const NotebookInkChannel Ivory = NotebookInkChannel.Ivory;
        const NotebookInkChannel Sketch = NotebookInkChannel.Sketch;
        const NotebookInkChannel Surface = NotebookInkChannel.Surface;
        const NotebookInkChannel Amber = NotebookInkChannel.Amber;
        const NotebookInkChannel Body = NotebookInkChannel.Body;

        internal static NotebookArtShape Create(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var p = new BattleInkDrawing(id);
            switch (id)
            {
                case "terrain.pine": Pine(p); break;
                case "terrain.oak": Oak(p); break;
                case "terrain.mountain": Mountain(p); break;
                case "terrain.ridge": Ridge(p); break;
                case "terrain.rock": Rocks(p); break;
                case "terrain.shrub": Shrub(p); break;
                case "object.castle": Castle(p, 0); break;
                case "object.castle.damaged": Castle(p, 1); break;
                case "object.castle.ruined": Castle(p, 2); break;
                case "object.entrance": Entrance(p); break;
                case "object.barricade-site": BarricadeSite(p); break;
                case "object.well": Well(p, Ivory, false); break;
                default:
                    if (!id.StartsWith("object.well.", StringComparison.Ordinal)) return null;
                    var ink = id.IndexOf(".red", StringComparison.Ordinal) >= 0 ? NotebookInkChannel.Red
                        : id.IndexOf(".blue", StringComparison.Ordinal) >= 0 ? NotebookInkChannel.Blue
                        : id.IndexOf(".green", StringComparison.Ordinal) >= 0 ? NotebookInkChannel.Green : Ivory;
                    Well(p, ink, id.EndsWith(".active", StringComparison.Ordinal));
                    break;
            }
            // Terrain is context; retain the same silhouette below active ivory machinery.
            // Landmarks, resources and legal-placement markers keep their existing emphasis.
            if (id.StartsWith("terrain.", StringComparison.Ordinal))
                foreach (var mark in p.Shape.Marks)
                    if (mark.Channel == Ivory || mark.Channel == Sketch) mark.Alpha *= .82f;
            return p.Shape;
        }

        static void Pine(BattleInkDrawing p)
        {
            // Each opaque needle tier is convex. The irregular combined edge is an unfilled path.
            p.Fill(Surface, .95f, -.08f,-.39f, -.04f,.22f, .035f,.28f, .075f,-.39f);
            p.Fill(Surface, .95f, -.34f,-.28f, .005f,.19f, .36f,-.29f);
            p.Fill(Surface, .95f, -.27f,-.05f, -.005f,.37f, .27f,-.07f);
            p.Fill(Surface, .95f, -.19f,.15f, -.008f,.54f, .18f,.14f);
            p.Path(Ivory, .020f, .74f,
                -.37f,-.30f, -.23f,-.14f, -.29f,-.16f, -.16f,.02f, -.24f,-.02f,
                -.11f,.19f, -.17f,.17f, -.04f,.40f, -.008f,.54f, .055f,.36f,
                .16f,.18f, .10f,.20f, .24f,-.015f, .16f,.015f, .30f,-.17f,
                .24f,-.14f, .37f,-.30f, .20f,-.26f, .10f,-.30f);
            p.Path(Ivory, .018f, .65f, -.11f,-.30f, -.19f,-.27f, -.37f,-.30f);
            p.Path(Sketch, .015f, .59f, -.19f,-.19f, -.055f,-.15f, .025f,-.19f, .21f,-.21f);
            p.Path(Sketch, .014f, .55f, -.15f,.04f, -.035f,.09f, .11f,.03f);
            p.Line(Sketch, -.055f,.25f, .05f,.20f, .014f, .52f);
            p.Path(Ivory, .017f, .62f, -.045f,-.27f, -.055f,-.39f, -.13f,-.43f);
            p.Path(Ivory, .017f, .62f, .045f,-.28f, .06f,-.39f, .14f,-.42f);
            p.Path(Sketch, .014f, .48f, -.21f,-.43f, -.02f,-.42f, .24f,-.43f);
            p.Line(Sketch, -.24f,-.42f, -.27f,-.35f, .012f, .42f);
            p.Line(Sketch, .18f,-.42f, .22f,-.36f, .012f, .42f);
        }

        static void Oak(BattleInkDrawing p)
        {
            p.Fill(Surface, .96f, -.07f,-.38f, -.065f,.12f, .055f,.17f, .09f,-.38f);
            p.Ellipse(Surface, -.23f,.15f,.21f,.94f,0,.96f,12);
            p.Ellipse(Surface, -.055f,.30f,.23f,.92f,0,.96f,12);
            p.Ellipse(Surface, .20f,.24f,.22f,.99f,0,.96f,12);
            p.Ellipse(Surface, .29f,.08f,.18f,.85f,0,.96f,12);
            p.Ellipse(Surface, -.045f,.06f,.28f,.65f,0,.96f,12);
            p.Path(Ivory, .019f, .72f,
                -.27f,-.015f, -.36f,.015f, -.35f,.075f, -.43f,.11f, -.41f,.20f,
                -.34f,.24f, -.35f,.31f, -.27f,.34f, -.23f,.42f, -.14f,.42f,
                -.09f,.50f, .005f,.48f, .06f,.53f, .12f,.46f, .20f,.47f,
                .23f,.40f, .32f,.40f, .34f,.31f, .41f,.27f, .41f,.18f,
                .47f,.11f, .43f,.04f, .45f,-.015f, .35f,-.065f, .26f,-.04f,
                .17f,-.08f, .09f,-.035f);
            p.Path(Ivory, .018f, .68f, -.12f,-.035f, -.20f,-.065f, -.27f,-.015f);
            p.Path(Ivory, .020f, .72f, -.15f,-.41f, -.065f,-.34f, -.05f,-.12f, -.16f,.06f, -.24f,.13f);
            p.Path(Ivory, .020f, .72f, .16f,-.41f, .065f,-.32f, .045f,-.12f, .17f,.065f, .22f,.17f);
            p.Path(Sketch, .014f, .64f, -.015f,-.35f, .005f,-.15f, -.005f,.10f, -.055f,.24f);
            p.Path(Sketch, .013f, .51f, -.02f,-.07f, -.12f,.16f, -.16f,.26f);
            p.Path(Sketch, .013f, .51f, .025f,-.02f, .11f,.16f, .11f,.31f);
            p.Path(Sketch, .014f, .47f, -.30f,.19f, -.23f,.23f, -.18f,.18f);
            p.Path(Sketch, .014f, .47f, .17f,.33f, .24f,.30f, .28f,.24f);
            p.Path(Sketch, .013f, .43f, .28f,.04f, .34f,.08f, .38f,.06f);
            p.Path(Sketch, .013f, .43f, -.10f,.37f, -.04f,.40f, .02f,.36f);
            p.Path(Sketch, .014f, .50f, -.23f,-.43f, -.08f,-.42f, .04f,-.44f, .24f,-.42f);
        }

        static void Mountain(BattleInkDrawing p)
        {
            p.Fill(Surface, .92f, -.46f,-.38f, -.065f,.52f, .32f,-.38f);
            p.Fill(Surface, .92f, -.10f,-.38f, .20f,.075f, .48f,-.38f);
            p.Path(Ivory, .020f, .65f, -.48f,-.40f, -.31f,-.11f, -.22f,.05f, -.13f,.27f,
                -.065f,.52f, .015f,.38f, .12f,.15f, .20f,.075f, .30f,-.12f, .49f,-.40f);
            p.Path(Sketch, .016f, .57f, -.065f,.49f, -.04f,.24f, -.12f,.07f, -.08f,-.13f, -.21f,-.38f);
            p.Path(Ivory, .016f, .46f, -.16f,.20f, -.10f,.14f, -.045f,.23f, .025f,.16f, .065f,.21f);
            p.Path(Sketch, .014f, .49f, .19f,.055f, .12f,-.14f, .17f,-.35f);
            p.Path(Sketch, .014f, .45f, -.29f,-.17f, -.23f,-.27f, -.26f,-.36f);
            p.Path(Sketch, .013f, .42f, .30f,-.23f, .33f,-.34f, .39f,-.39f);
            p.Path(Sketch, .014f, .45f, -.46f,-.42f, -.28f,-.41f, -.10f,-.43f, .14f,-.41f, .46f,-.43f);
        }

        static void Ridge(BattleInkDrawing p)
        {
            p.Fill(Surface, .92f, -.51f,-.23f, -.31f,.17f, -.05f,-.23f);
            p.Fill(Surface, .92f, -.29f,-.23f, -.025f,.32f, .27f,-.23f);
            p.Fill(Surface, .92f, .05f,-.23f, .29f,.19f, .52f,-.23f);
            p.Path(Ivory, .019f, .62f, -.53f,-.25f, -.43f,-.07f, -.31f,.17f, -.20f,.04f,
                -.12f,.13f, -.025f,.32f, .095f,.11f, .18f,-.01f, .29f,.19f, .38f,.045f, .53f,-.25f);
            p.Path(Sketch, .015f, .50f, -.31f,.15f, -.27f,-.02f, -.34f,-.20f);
            p.Path(Sketch, .015f, .53f, -.025f,.29f, -.04f,.065f, .04f,-.06f, -.015f,-.23f);
            p.Path(Sketch, .014f, .48f, .29f,.17f, .255f,-.035f, .32f,-.22f);
            p.Path(Ivory, .013f, .41f, -.12f,.12f, -.06f,.07f, -.025f,.135f, .035f,.075f);
            p.Path(Sketch, .014f, .43f, -.50f,-.27f, -.22f,-.25f, .03f,-.28f, .30f,-.25f, .51f,-.27f);
        }

        static void Rocks(BattleInkDrawing p)
        {
            Stone(p, .68f, -.17f,-.25f, -.20f,.04f, -.08f,.30f, .14f,.35f, .29f,.11f, .21f,-.18f);
            Stone(p, .66f, -.43f,-.27f, -.45f,-.08f, -.34f,.055f, -.18f,.015f, -.10f,-.18f, -.18f,-.29f);
            Stone(p, .72f, .03f,-.30f, .055f,-.13f, .24f,.035f, .41f,-.025f, .48f,-.24f, .31f,-.33f);
            p.Path(Sketch, .015f, .56f, -.075f,.28f, -.015f,.08f, -.15f,-.18f);
            p.Path(Sketch, .015f, .55f, -.015f,.08f, .14f,.13f, .265f,.105f);
            p.Path(Sketch, .014f, .50f, -.34f,.035f, -.30f,-.10f, -.40f,-.23f);
            p.Path(Sketch, .015f, .57f, .24f,.015f, .21f,-.13f, .32f,-.29f);
            p.Line(Sketch, .21f,-.13f, .43f,-.22f, .013f, .45f);
            p.Path(Sketch, .013f, .44f, -.46f,-.34f, -.28f,-.32f, -.03f,-.35f, .23f,-.35f, .47f,-.34f);
        }

        static void Shrub(BattleInkDrawing p)
        {
            p.Ellipse(Surface, -.22f,-.08f,.17f,.88f,0,.92f,10);
            p.Ellipse(Surface, -.03f,.005f,.19f,.89f,0,.92f,10);
            p.Ellipse(Surface, .20f,-.06f,.18f,.83f,0,.92f,10);
            p.Path(Ivory, .018f, .65f, -.39f,-.20f, -.42f,-.12f, -.35f,-.075f, -.38f,-.005f,
                -.29f,.015f, -.285f,.09f, -.21f,.08f, -.18f,.16f, -.09f,.12f,
                -.025f,.20f, .04f,.13f, .12f,.16f, .15f,.08f, .25f,.09f,
                .27f,.025f, .35f,.035f, .35f,-.035f, .415f,-.08f, .385f,-.17f, .33f,-.20f);
            p.Path(Sketch, .015f, .58f, -.25f,-.26f, -.17f,-.17f, -.20f,-.06f);
            p.Path(Sketch, .016f, .63f, -.10f,-.27f, -.025f,-.12f, -.055f,.065f);
            p.Path(Sketch, .015f, .55f, .04f,-.25f, .14f,-.11f, .23f,-.045f);
            p.Path(Sketch, .013f, .44f, -.30f,-.04f, -.25f,.005f, -.20f,-.025f);
            p.Path(Sketch, .013f, .44f, .08f,.045f, .13f,.005f, .20f,.02f);
            p.Path(Sketch, .014f, .44f, -.40f,-.28f, -.19f,-.29f, .025f,-.27f, .37f,-.29f);
        }

        static void Castle(BattleInkDrawing p, int damage)
        {
            if (damage == 2) { RuinedCastle(p); return; }
            // Broad folded stone faces carry the shape on the overview; seams are for close inspection.
            p.Fill(Body, .92f, -.33f,-.32f, -.33f,.10f, .33f,.10f, .33f,-.32f);
            p.Fill(Body, 1, -.14f,.08f, -.14f,.46f, .14f,.46f, .14f,.08f);
            p.Fill(Surface, .42f, .055f,.09f, .055f,.46f, .14f,.46f, .14f,.09f);
            p.Fill(Ivory, .90f, -.14f,.41f, -.14f,.47f, .14f,.47f, .14f,.41f);
            p.Fill(Ivory, .93f, -.145f,.46f, -.145f,.52f, -.09f,.52f, -.09f,.46f);
            if (damage == 0)
                p.Fill(Ivory, .93f, .04f,.46f, .04f,.52f, .095f,.52f, .095f,.46f);
            Tower(p, -.43f, -.24f, .31f, false);
            Tower(p, .24f, .43f, damage == 0 ? .31f : .19f, damage != 0);
            p.Path(Ivory, .021f, .85f, -.24f,-.31f, -.24f,.10f, -.20f,.10f, -.20f,.16f,
                -.14f,.16f, -.14f,.10f, -.09f,.10f);
            if (damage == 0)
            {
                p.Path(Ivory, .021f, .85f, .09f,.10f, .14f,.10f, .14f,.16f, .20f,.16f,
                    .20f,.10f, .24f,.10f, .24f,-.31f);
                p.Path(Ivory, .021f, .85f, -.145f,.18f, -.145f,.47f, -.10f,.47f, -.10f,.52f,
                    -.045f,.52f, -.045f,.47f, .015f,.47f, .015f,.52f, .075f,.52f,
                    .075f,.47f, .145f,.47f, .145f,.18f);
                p.Path(Sketch, .014f, .62f, -.125f,.39f, .12f,.39f);
                Flag(p, .015f,.50f, .66f, false);
            }
            else
            {
                p.Path(Ivory, .022f, .82f, -.145f,.18f, -.145f,.47f, -.10f,.47f, -.10f,.52f,
                    -.045f,.52f, -.035f,.43f, .015f,.46f, .06f,.40f, .145f,.44f, .145f,.18f);
                p.Path(Ivory, .021f, .83f, .09f,.10f, .14f,.07f, .155f,.015f, .20f,.045f,
                    .23f,-.015f, .26f,.015f);
                p.Path(Surface, .042f, .94f, .105f,.38f, .035f,.29f, .075f,.22f, .015f,.12f);
                p.Path(Sketch, .016f, .66f, -.255f,.20f, -.30f,.125f, -.275f,.045f, -.32f,-.025f);
                Flag(p, -.015f,.48f, .62f, true);
            }
            Gate(p, damage == 0 ? .87f : .76f);
            p.Path(Ivory, .018f, .72f, -.43f,-.32f, -.20f,-.33f, -.16f,-.31f);
            p.Path(Ivory, .018f, .72f, .16f,-.31f, .27f,-.33f, .43f,-.32f);
            Masonry(p);
            if (damage != 0)
            {
                Stone(p, .70f, .25f,-.32f, .28f,-.25f, .36f,-.24f, .39f,-.31f, .34f,-.36f);
                Stone(p, .66f, .39f,-.35f, .42f,-.29f, .49f,-.30f, .50f,-.37f);
                Stone(p, .63f, -.47f,-.35f, -.44f,-.28f, -.37f,-.30f, -.35f,-.36f);
            }
            Ground(p, -.49f, .50f, -.38f);
        }

        static void Tower(BattleInkDrawing p, float left, float right, float top, bool broken)
        {
            float span = right-left;
            p.Fill(Body, .98f, left,-.31f, left,top, right,top, right,-.31f);
            p.Fill(Surface, .50f, right-span*.33f,-.31f, right-span*.33f,top, right,top, right,-.31f);
            p.Fill(Ivory, .94f, left-.015f,top-.045f, left-.015f,top+.025f,
                right+.015f,top+.025f, right+.015f,top-.045f);
            p.Path(Ivory, .021f, .84f, left,-.30f, left,top-.04f);
            p.Path(Ivory, .021f, .84f, right,top-.04f, right,-.30f);
            if (broken)
                p.Path(Ivory, .021f, .82f, left-.015f,top-.035f, left-.015f,top+.04f,
                    left+.04f,top+.07f, left+.065f,top-.01f, left+.11f,top+.025f,
                    right-.025f,top-.045f, right+.015f,top-.02f, right+.015f,top-.065f);
            else
                p.Path(Ivory, .021f, .85f, left-.015f,top-.045f, left-.015f,top+.065f,
                    left+.025f,top+.065f, left+.025f,top+.02f, left+.07f,top+.02f,
                    left+.07f,top+.065f, right-.07f,top+.065f, right-.07f,top+.02f,
                    right-.025f,top+.02f, right-.025f,top+.065f, right+.015f,top+.065f,
                    right+.015f,top-.045f, left-.015f,top-.045f);
            p.Line(Sketch, left,top-.08f, right,top-.08f, .014f, .57f);
            float middle = (left+right)*.5f;
            p.Fill(Surface, 1, middle-.028f,-.035f, middle-.028f,.065f,
                middle,.105f, middle+.028f,.065f, middle+.028f,-.035f);
            p.Fill(Amber, .98f, middle-.048f,-.085f, middle+.048f,-.085f,
                middle+.048f,-.225f, middle,-.265f, middle-.048f,-.225f);
            p.Path(Ivory, .016f, .67f, middle-.018f,-.035f, middle-.018f,.065f,
                middle,.095f, middle+.018f,.065f, middle+.018f,-.035f);
            p.Line(Sketch, left,-.17f, right,-.17f, .013f, .47f);
            p.Line(Sketch, left+span*.27f,-.17f, left+span*.27f,-.25f, .012f, .48f);
            p.Line(Sketch, left,-.07f, left+span*.30f,-.07f, .012f, .43f);
            p.Path(Amber, .013f, .53f, middle-.035f,-.08f, middle-.035f,-.24f,
                middle,-.21f, middle+.035f,-.24f, middle+.035f,-.08f);
        }

        static void Gate(BattleInkDrawing p, float alpha)
        {
            p.Fill(Surface, 1, -.125f,-.32f, -.125f,-.11f, -.09f,-.04f,
                -.035f,.015f, .025f,.02f, .085f,-.035f, .125f,-.11f, .125f,-.32f);
            p.Path(Ivory, .022f, alpha, -.16f,-.31f, -.16f,-.105f, -.14f,-.025f,
                -.09f,.035f, -.025f,.065f, .04f,.06f, .11f,.02f, .15f,-.05f, .16f,-.12f, .16f,-.31f);
            p.Path(Ivory, .018f, alpha*.90f, -.115f,-.31f, -.115f,-.11f, -.09f,-.04f,
                -.035f,.005f, .025f,.015f, .085f,-.035f, .115f,-.11f, .115f,-.31f);
            p.Line(Sketch, 0,-.005f, 0,-.31f, .014f, .62f);
            p.Line(Sketch, -.066f,-.065f, -.066f,-.30f, .012f, .53f);
            p.Line(Sketch, .066f,-.065f, .066f,-.30f, .012f, .53f);
            p.Line(Sketch, -.11f,-.20f, .11f,-.20f, .014f, .53f);
            p.Line(Sketch, -.14f,-.04f, -.105f,-.07f, .012f, .51f);
            p.Line(Sketch, -.075f,.04f, -.055f,.005f, .012f, .51f);
            p.Line(Sketch, .075f,.045f, .055f,.005f, .012f, .51f);
            p.Line(Sketch, .145f,-.035f, .11f,-.065f, .012f, .51f);
        }

        static void Masonry(BattleInkDrawing p)
        {
            p.Line(Sketch, -.22f,-.10f, -.16f,-.10f, .013f, .52f);
            p.Line(Sketch, .16f,-.10f, .23f,-.10f, .013f, .52f);
            p.Line(Sketch, -.23f,-.23f, -.17f,-.23f, .013f, .46f);
            p.Line(Sketch, .18f,-.23f, .23f,-.23f, .013f, .46f);
            p.Line(Sketch, -.12f,.19f, .12f,.19f, .013f, .51f);
            p.Line(Sketch, -.12f,.32f, .12f,.32f, .013f, .51f);
            p.Line(Sketch, -.055f,.19f, -.055f,.26f, .012f, .47f);
            p.Line(Sketch, .07f,.32f, .07f,.39f, .012f, .47f);
            p.Path(Ivory, .015f, .68f, -.022f,.225f, -.022f,.285f, 0,.31f, .022f,.285f, .022f,.225f);
        }

        static void RuinedCastle(BattleInkDrawing p)
        {
            p.Fill(Body, .84f, -.43f,-.31f, -.43f,.09f, -.34f,.20f, -.26f,-.31f);
            p.Fill(Body, .74f, -.12f,-.25f, -.12f,.27f, .06f,.27f, .10f,-.25f);
            p.Fill(Body, .80f, .28f,-.31f, .31f,.19f, .41f,.19f, .43f,-.31f);
            p.Path(Ivory, .022f, .77f, -.43f,-.30f, -.44f,.13f, -.405f,.14f,
                -.405f,.23f, -.36f,.24f, -.36f,.16f, -.32f,.19f, -.335f,.04f,
                -.28f,.005f, -.26f,-.12f);
            p.Path(Ivory, .022f, .80f, -.12f,-.04f, -.12f,.27f, -.085f,.28f,
                -.085f,.36f, -.035f,.34f, -.025f,.26f, .015f,.30f, .035f,.24f,
                .075f,.25f, .07f,.13f, .10f,.09f);
            p.Path(Ivory, .022f, .77f, .27f,-.14f, .30f,.07f, .30f,.23f,
                .34f,.23f, .34f,.28f, .39f,.27f, .39f,.19f, .43f,.21f, .43f,-.31f);
            // The surviving arch and three separated tower stumps retain the castle identity.
            p.Path(Ivory, .021f, .80f, -.16f,-.29f, -.16f,-.115f, -.13f,-.035f,
                -.075f,.015f, -.005f,.035f, .06f,.015f, .10f,-.025f);
            p.Path(Ivory, .017f, .68f, -.115f,-.29f, -.115f,-.12f, -.075f,-.055f,
                -.015f,-.025f, .045f,-.035f, .07f,-.065f);
            p.Path(Sketch, .015f, .63f, -.065f,.25f, -.025f,.17f, -.06f,.09f);
            p.Line(Sketch, -.43f,-.13f, -.31f,-.13f, .013f, .50f);
            p.Line(Sketch, .32f,-.06f, .42f,-.06f, .013f, .50f);
            p.Line(Sketch, .34f,-.19f, .42f,-.19f, .013f, .50f);
            p.Path(Amber, .013f, .48f, -.385f,.035f, -.385f,-.135f, -.36f,-.10f);
            Flag(p, -.005f,.28f, .46f, true);
            Stone(p, .71f, -.50f,-.34f, -.46f,-.25f, -.36f,-.23f, -.32f,-.31f, -.38f,-.38f);
            Stone(p, .76f, -.34f,-.35f, -.29f,-.20f, -.19f,-.17f, -.105f,-.29f, -.18f,-.38f);
            Stone(p, .67f, -.12f,-.34f, -.09f,-.25f, -.015f,-.23f, .035f,-.32f, -.035f,-.38f);
            Stone(p, .78f, .06f,-.33f, .11f,-.15f, .22f,-.11f, .31f,-.28f, .24f,-.38f);
            Stone(p, .70f, .26f,-.36f, .34f,-.25f, .43f,-.27f, .50f,-.36f, .40f,-.40f);
            p.Line(Sketch, -.25f,-.20f, -.22f,-.33f, .013f, .51f);
            p.Line(Sketch, .20f,-.135f, .17f,-.30f, .013f, .51f);
            Ground(p, -.51f, .51f, -.415f);
        }

        static void Flag(BattleInkDrawing p, float x, float bottom, float top, bool torn)
        {
            p.Line(Ivory, x,bottom, x,top, .023f, .90f);
            if (torn)
            {
                p.Fill(Amber, .91f, x,top, x+.13f,top-.035f, x+.058f,top-.067f, x,top-.08f);
                p.Fill(Amber, .91f, x,top-.05f, x+.058f,top-.064f, x+.07f,top-.13f, x,top-.089f);
            }
            else
            {
                p.Fill(Amber, 1, x,top, x+.13f,top-.018f, x+.12f,top-.12f, x,top-.096f);
                p.Fill(Amber, .83f, x+.13f,top-.018f, x+.235f,top-.033f, x+.14f,top-.076f, x+.12f,top-.074f);
                p.Fill(Amber, .83f, x+.12f,top-.074f, x+.14f,top-.076f, x+.205f,top-.122f, x+.115f,top-.122f);
            }
        }

        static void Well(BattleInkDrawing p, NotebookInkChannel ink, bool active)
        {
            // Wide glass reservoirs keep their ink colour in a few pixels. The stopper, shoulder
            // and bottle silhouette also distinguish the three resources without colour.
            p.Ellipse(Surface, .005f,-.24f,.41f,.13f,0,.87f,16);
            float pigment = active ? 1 : .88f;
            if (ink == NotebookInkChannel.Red)
            {
                // Tapered flask with a wide foot and the red drop on its face.
                p.Fill(Ivory, .94f, -.36f,-.22f, -.105f,.12f, .105f,.12f, .36f,-.22f, .28f,-.26f, -.28f,-.26f);
                p.Fill(ink, pigment, -.30f,-.215f, -.15f,-.01f, .15f,-.01f, .30f,-.215f);
                p.Fill(Surface, .30f, .065f,.067f, .106f,.10f, .34f,-.218f, .22f,-.218f);
                p.Line(Ivory,-.24f,-.152f,-.18f,-.064f,.019f,.68f);
                p.Fill(Ivory, .90f, -.047f,-.14f, 0,-.045f, .049f,-.14f, 0,-.195f);
            }
            else if (ink == NotebookInkChannel.Blue)
            {
                // Round, squat bottle with a pale meniscus and two short ripple marks.
                p.Ellipse(Ivory, 0,-.057f,.35f,.62f,0,.93f,20);
                p.Ellipse(ink, 0,-.075f,.305f,.51f,0,pigment,20);
                p.Path(Surface,.035f,.33f,.27f,-.04f,.23f,-.14f,.13f,-.196f,.025f,-.208f);
                p.Path(Ivory,.023f,.91f,-.22f,-.04f,-.13f,-.015f,-.045f,-.043f,.043f,-.018f);
                p.Line(Ivory,-.11f,-.105f,.025f,-.105f,.022f,.77f);
            }
            else
            {
                // Square apothecary bottle, bevelled corners and a folded leaf on the label.
                p.Fill(Ivory, .93f, -.32f,-.22f, -.32f,.028f, -.18f,.12f, .18f,.12f,
                    .32f,.028f, .32f,-.22f, .26f,-.26f, -.26f,-.26f);
                p.Fill(ink, pigment, -.27f,-.21f, -.27f,.017f, .27f,.017f, .27f,-.21f);
                p.Fill(Surface, .31f, .19f,.097f, .30f,.026f, .30f,-.21f, .19f,-.21f);
                p.Line(Ivory,-.22f,-.14f,-.22f,-.036f,.020f,.73f);
                p.Fill(Ivory, .88f, -.08f,-.17f, -.08f,-.093f, .11f,-.034f, .061f,-.141f);
                p.Line(ink,-.065f,-.16f,.055f,-.081f,.015f,.95f);
            }

            // Clear collar and dark rim read as glass. An active well has an open, ink-filled mouth.
            p.Fill(Body, 1, -.116f,.076f, -.116f,.18f, .116f,.18f, .116f,.076f);
            p.Fill(Ivory, 1, -.162f,.14f, -.162f,.186f, .162f,.186f, .162f,.14f);
            if (active)
            {
                p.Ellipse(ink, 0,.184f,.127f,.25f,0,1,12);
                p.Line(Ivory,-.07f,.19f,.015f,.192f,.015f,.9f);
                p.Path(ink,.027f,.9f,-.32f,-.275f,-.12f,-.29f,.10f,-.29f,.32f,-.275f);
            }
            else
            {
                p.Fill(Amber, .96f, -.083f,.18f, -.103f,.243f, .107f,.243f, .087f,.18f);
                p.Line(Surface,-.09f,.195f,.095f,.195f,.014f,.68f);
            }
        }

        static void Entrance(BattleInkDrawing p)
        {
            p.Ellipse(Surface, 0,-.15f,.33f,.24f,0,.98f,16);
            p.Path(Sketch, .018f, .54f, -.38f,-.17f, -.27f,-.10f, -.22f,-.13f,
                -.145f,-.08f, -.08f,-.11f);
            p.Path(Sketch, .018f, .54f, .08f,-.11f, .16f,-.08f, .23f,-.13f, .29f,-.10f, .38f,-.17f);
            p.Path(Ivory, .015f, .48f, -.33f,-.21f, -.22f,-.24f, -.10f,-.215f);
            p.Path(Ivory, .015f, .48f, .10f,-.215f, .22f,-.24f, .33f,-.21f);
            p.Path(NotebookInkChannel.Danger, .023f, .72f, -.12f,.085f, 0,.28f, .12f,.085f);
            p.Line(NotebookInkChannel.Danger, 0,-.16f, 0,.255f, .023f, .68f);
            p.Line(Sketch, -.28f,-.025f, -.31f,.07f, .013f, .42f);
            p.Line(Sketch, .28f,-.025f, .31f,.07f, .013f, .42f);
        }

        static void BarricadeSite(BattleInkDrawing p)
        {
            p.Path(Sketch, .016f, .53f, -.34f,-.15f, -.34f,-.28f, -.22f,-.28f);
            p.Path(Sketch, .016f, .53f, .22f,-.28f, .34f,-.28f, .34f,-.15f);
            p.Path(Sketch, .016f, .53f, -.34f,.15f, -.34f,.28f, -.22f,.28f);
            p.Path(Sketch, .016f, .53f, .22f,.28f, .34f,.28f, .34f,.15f);
            p.Path(Ivory, .024f, .58f, -.24f,-.18f, -.05f,-.025f, .245f,.20f);
            p.Path(Ivory, .024f, .58f, -.24f,.20f, .055f,-.025f, .245f,-.18f);
            p.Ellipse(Amber, 0,.01f,.026f,1,0,.61f,8);
        }

        static void Ground(BattleInkDrawing p, float left, float right, float y)
            => p.Path(Sketch, .013f, .44f, left,y, left*.45f,y+.012f, 0,y-.008f, right*.55f,y+.006f, right,y);

        static void Stone(BattleInkDrawing p, float alpha, params float[] xy)
        {
            // All callers provide a convex stone face; fan triangulation never crosses an outline notch.
            p.Fill(Surface, .97f, xy);
            var closed = new float[xy.Length + 2];
            Array.Copy(xy, closed, xy.Length);
            closed[xy.Length] = xy[0];
            closed[xy.Length+1] = xy[1];
            p.Path(Ivory, .018f, alpha, closed);
        }
    }
}
