using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    // One cartographic layer for both the battle and the page preview. Wash stays on
    // reserved cells. Only exposed edges get ink, so adjoining pieces form one bank.
    internal static class CampaignTerrainDrawing
    {
        internal static void Draw(MeshPen pen, BattlefieldView map, TerrainPropView prop, NotebookVisualThemeAsset theme, bool dark)
        {
            var rect = new Rect(prop.Cell.X, prop.Cell.Y, prop.Width, prop.Height);
            bool water = prop.Kind == TerrainPropKind.Water;
            bool wall = prop.Kind == TerrainPropKind.Rampart;
            bool grove = prop.Kind == TerrainPropKind.Pine || prop.Kind == TerrainPropKind.Broadleaf || prop.Kind == TerrainPropKind.Shrub;
            bool citadel = map.Style == CampaignTerrainStyle.Citadel;
            var wash = water ? new Color32(36, 96, 104, 255) : wall ? (citadel ? new Color32(86, 95, 96, 255) : new Color32(92, 78, 58, 255))
                : grove ? new Color32(52, 86, 59, 255) : new Color32(79, 88, 79, 255);
            Color outline = NightNotebookEffectPalette.Tone(NotebookPalette.Navy,
                water ? NightNotebookArt.GlassInk : wall ? (citadel ? NightNotebookArt.Ink : NightNotebookArt.Accent) : NightNotebookArt.Pencil, theme, dark);
            Color body = NightNotebookEffectPalette.Tone(Color.Lerp(wash, Color.white, .48f), wash, theme, dark);
            Color stroke = MeshPen.Alpha(outline, wall ? .56f : water ? .47f : .33f);
            Color fine = MeshPen.Alpha(outline, .31f);
            pen.Rect(rect.x, rect.y, rect.width, rect.height, MeshPen.Alpha(body, water ? .63f : wall ? .53f : grove ? .33f : .31f));

            bool smallPrint = Mathf.Max(pen.WorldUnitsPerPixel, pen.ArtUnitsPerPixel) > .34f;
            int seed = prop.Cell.X * 97 + prop.Cell.Y * 223;
            if (water) DrawCurrent(pen, rect, fine, seed, smallPrint);
            else if (wall) DrawFortification(pen, map, rect, fine, citadel, seed);
            else if (grove) DrawGrove(pen, rect, prop.Kind, map.Style, theme, dark, seed, smallPrint);
            else DrawRelief(pen, rect, prop.Kind, map.Style, theme, dark, fine, seed, smallPrint);

            DrawCoast(pen, map, prop, stroke, fine, wall);
        }

        static void DrawCoast(MeshPen pen, BattlefieldView map, TerrainPropView prop, Color edge, Color fine, bool wall)
        {
            int x = prop.Cell.X, y = prop.Cell.Y, w = prop.Width, h = prop.Height;
            Side(x, y, w, 1, 0, 0, -1);
            Side(x, y + h - 1, w, 1, 0, 0, 1);
            Side(x, y, h, 0, 1, -1, 0);
            Side(x + w - 1, y, h, 0, 1, 1, 0);

            void Side(int cx, int cy, int length, int dx, int dy, int nx, int ny)
            {
                var tangent = new Vector2(dx, dy);
                var normal = new Vector2(nx, ny);
                var origin = new Vector2(cx + .5f, cy + .5f) + normal * .39f - tangent * .5f;
                int start = 0;
                for (int i = 0; i <= length; i++)
                {
                    if (i < length && !map.IsReservedTerrain(new Cell(cx + i * dx + nx, cy + i * dy + ny))) continue;
                    if (i > start)
                    {
                        Vector2 a = origin + tangent * start, b = origin + tangent * i;
                        pen.Line(a, b, wall ? .19f : .11f, edge);
                        if (wall && i - start >= 3)
                        {
                            pen.Line(a - normal * .35f, b - normal * .35f, .075f, fine);
                            for (float s = start + 1; s < i - .6f; s += 2.4f)
                            {
                                var merlon = origin + tangent * s;
                                pen.Line(merlon, merlon - normal * .59f, .18f, edge);
                            }
                        }
                        else if (prop.Kind == TerrainPropKind.Mountain && i - start >= 6)
                        {
                            for (float s = start + 1.3f; s < i - .8f; s += 2.6f)
                            {
                                float weight = MeshPen.Hash((cx + (int)s) * 67 + cy);
                                var foot = origin + tangent * s - normal * .25f;
                                pen.Line(foot, foot + tangent * .37f - normal * (.43f + weight * .65f), .055f, fine);
                            }
                        }
                    }
                    start = i + 1;
                }
            }
        }

        static void DrawCurrent(MeshPen pen, Rect r, Color ink, int seed, bool smallPrint)
        {
            // Draw the length of the current and a few broken surface ripples.
            for (int stream = 0; stream < 3; stream++)
            {
                Vector2 previous = default;
                int segments = Mathf.Max(3, Mathf.CeilToInt(r.height / (smallPrint ? 3 : 1.65f)));
                for (int i = 0; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    float drift = .075f * Mathf.Sin((r.y + t * r.height) * .37f + stream * .58f);
                    var here = new Vector2(r.x + r.width * (.26f + .19f * stream + drift), r.y + .42f + (r.height - .84f) * t);
                    if (i > 0 && (i + stream * 2) % 7 != 0) pen.Line(previous, here, .06f, ink);
                    previous = here;
                }
            }
            for (float y = r.yMin + 1.4f; y < r.yMax - 1; y += smallPrint ? 4.1f : 2.7f)
            {
                float side = MeshPen.Hash(seed++) < .5f ? 1 : -1;
                var bank = new Vector2(side > 0 ? r.xMin + .38f : r.xMax - .38f, y);
                pen.Line(bank, bank + new Vector2(side * .32f, .70f), .08f, ink);
                pen.Line(bank + new Vector2(side * .24f, .1f), bank + new Vector2(side * .72f, .49f), .07f, ink);
                var ripple = bank + new Vector2(side * .94f, -.2f);
                pen.Line(ripple, ripple + new Vector2(side * .65f, -.08f), .05f, ink);
            }
        }

        static void DrawFortification(MeshPen pen, BattlefieldView map, Rect r, Color ink, bool citadel, int seed)
        {
            bool horizontal = r.width >= r.height;
            float run = horizontal ? r.width : r.height, depth = horizontal ? r.height : r.width;
            Vector2 along = horizontal ? Vector2.right : Vector2.up, across = horizontal ? Vector2.up : Vector2.right;
            Vector2 At(float s, float t) => r.min + along * s + across * t;
            int rows = citadel ? 2 : 1;
            float course = (depth - .58f) / rows;
            for (int row = 0; row < rows; row++)
            {
                float t = .29f + row * course, end = run - .28f, s = .28f;
                for (int i = 0; s < end - .15f; i++)
                {
                    float block = Mathf.Min(end - s, (citadel ? 2.5f : 4.2f) + MeshPen.Hash(seed + row * 71 + i) * 1.7f);
                    float bevel = Mathf.Min(.20f, block * .18f);
                    if (citadel)
                    {
                        if (((i + row) & 1) == 0)
                            pen.Quad(At(s,t), At(s+block,t), At(s+block,t+course), At(s,t+course),
                                MeshPen.Alpha(ink, .095f + .045f * MeshPen.Hash(seed + i * 13)));
                        pen.Line(At(s + bevel,t + course), At(s + block - bevel,t + course), .07f, ink);
                        pen.Line(At(s,t + bevel), At(s + bevel,t + course), .075f, ink);
                        if ((i + row) % 4 == 1)
                            pen.Line(At(s + block * .55f,t + course), At(s + block * .42f,t + course * .58f), .055f, ink);
                    }
                    else
                    {
                        // Timber tie and diagonal bracing on the earthen redoubt.
                        pen.Line(At(s,t),At(s + block - bevel,t + course),.10f,ink);
                        pen.Line(At(s,t),At(s,t + course),.13f,ink);
                        pen.Disc(At(s + bevel,t + bevel),.105f,ink,6);
                    }
                    s += block;
                }
            }
            for (int end = 0; end <= 1; end++)
            {
                float direction = end == 0 ? 1 : -1;
                var outside = At(end * run - direction * .5f, depth * .5f);
                if (map.IsReservedTerrain(new Cell(Mathf.FloorToInt(outside.x), Mathf.FloorToInt(outside.y)))) continue;
                float s = end * run + direction * .16f, back = s + direction * Mathf.Min(depth * .55f, run * .25f);
                pen.Quad(At(s,.18f), At(back,.18f), At(back,depth-.18f), At(s,depth-.18f), MeshPen.Alpha(ink, .33f));
                pen.Line(At(back,.18f),At(back,depth-.18f),.13f, MeshPen.Alpha(ink,.73f));
                if (citadel)
                {
                    var fissure = At(s + (back - s) * .43f, depth * .6f);
                    pen.Line(At(s,depth * .78f), fissure, .065f, ink);
                    pen.Line(fissure, At(s + (back - s) * .26f, depth * .40f), .065f, ink);
                }
            }
        }

        static void DrawGrove(MeshPen pen, Rect r, TerrainPropKind kind, CampaignTerrainStyle style,
            NotebookVisualThemeAsset theme, bool dark, int seed, bool smallPrint)
        {
            // Repeated low canopies read as field boundaries; the woods have taller,
            // staggered groups. No decorative tree can spill into an adjacent lane.
            bool field = style == CampaignTerrainStyle.FourFields;
            int columns = Mathf.Clamp(Mathf.RoundToInt(r.width / (field ? 4.2f : 4.7f)), 1, smallPrint ? 6 : 9);
            int rows = Mathf.Clamp(Mathf.RoundToInt(r.height / (field ? 4.2f : 6.5f)), 1, smallPrint ? 4 : 6);
            string id = kind == TerrainPropKind.Pine ? "terrain.pine" : kind == TerrainPropKind.Broadleaf ? "terrain.oak" : "terrain.shrub";
            float w = r.width / columns, h = r.height / rows;
            if (!field && columns > 1 && r.height >= 4)
                for (int x = 1; x < columns; x++)
                {
                    var at = new Vector2(r.x + x * w, r.y + r.height * (.60f + .15f * MeshPen.Hash(seed + x)));
                    DrawStamp(pen, id, at, new Vector2(w * .77f, Mathf.Min(h * .47f, r.height * .40f)), theme, dark, .51f);
                }
            for (int y = rows - 1; y >= 0; y--)
            for (int x = 0; x < columns; x++)
            {
                Rect cell = new Rect(r.x + x * w, r.y + y * h, w, h);
                float offset = MeshPen.Hash(seed + 17 * x + 71 * y) - .5f;
                var center = cell.center + new Vector2(offset * .16f * w, field ? 0 : (-.08f + offset * .16f) * h);
                DrawStamp(pen, id, center, cell.size * (field ? .73f : .71f + offset * .25f), theme, dark, .78f);
            }
        }

        static void DrawRelief(MeshPen pen, Rect r, TerrainPropKind material, CampaignTerrainStyle style, NotebookVisualThemeAsset theme,
            bool dark, Color fine, int seed, bool smallPrint)
        {
            if (style == CampaignTerrainStyle.Crossroads)
            {
                DrawPlateau(pen, r, theme, dark, fine, seed, smallPrint);
                return;
            }
            bool canyon = style == CampaignTerrainStyle.StoneGate;
            bool strata = canyon || style == CampaignTerrainStyle.Fold;
            if (canyon && material == TerrainPropKind.Mountain)
                DrawStrata(pen, r, fine, seed, smallPrint);
            else if (r.width >= 4 && r.height >= 4)
            {
                int rings = Mathf.Min(r.width, r.height) >= 12 && !smallPrint ? 2 : 1;
                for (int i = 0; i < rings; i++)
                    Contour(pen, r, .46f - i * .073f, fine, seed, strata);
            }
            bool horizontal = r.width >= r.height;
            float longSide = horizontal ? r.width : r.height;
            int count = Mathf.Clamp(Mathf.CeilToInt(longSide / (canyon ? 13f : 8f)), 1, smallPrint ? 3 : 5);
            Vector2 size = new Vector2(r.width / (horizontal ? count : 1), r.height / (horizontal ? 1 : count));
            for (int i = 0; i < count; i++)
            {
                float noise = MeshPen.Hash(seed + i * 7);
                float drift = (noise - .5f) * .35f;
                float x = horizontal ? (i + .5f) / count : .5f + drift;
                float y = horizontal ? .5f + drift : (i + .5f) / count;
                var center = new Vector2(r.x + x * r.width, r.y + y * r.height);
                string id = material == TerrainPropKind.Rock || (canyon && i == 0) ? "terrain.rock" :
                    strata || noise > .47f ? "terrain.ridge" : "terrain.mountain";
                DrawStamp(pen, id, center, size * (.65f + .22f * noise), theme, dark, .82f);
            }
        }

        static void DrawStrata(MeshPen pen, Rect r, Color ink, int seed, bool smallPrint)
        {
            bool horizontal = r.width >= r.height;
            var along = horizontal ? Vector2.right : Vector2.up;
            var across = horizontal ? Vector2.up : Vector2.right;
            float length = horizontal ? r.width : r.height, width = horizontal ? r.height : r.width;
            int steps = Mathf.Max(4,Mathf.CeilToInt(length / 3.5f));
            int beds = smallPrint ? 3 : 5;
            for (int layer=1;layer<=beds;layer++)
            {
                Vector2 previous = default;
                for (int i=0;i<=steps;i++)
                {
                    float u = (float)i / steps;
                    float lift = Mathf.Sin(u * 6.4f + seed % 17) * .045f + .09f * (MeshPen.Hash(seed + i * 43) - .5f);
                    var p = r.min + along * (length * (.025f + .95f * u)) + across * (width * ((float)layer / (beds + 1) + lift));
                    if (i > 0 && (i + layer) % 8 != 0) pen.Line(previous, p, .085f, ink);
                    previous = p;
                }
            }
        }

        // Survey landmarks for the four quadrants. X/Y locate a group on its plateau;
        // the third value scales the group relative to its neighbours.
        static readonly Vector3[][] PlateauPeaks = {
            new[] { new Vector3(.18f,.29f,.54f), new Vector3(.37f,.46f,.98f), new Vector3(.49f,.62f,.66f), new Vector3(.77f,.65f,.47f) },
            new[] { new Vector3(.23f,.68f,.52f), new Vector3(.50f,.51f,.66f), new Vector3(.67f,.39f,1.02f), new Vector3(.78f,.24f,.43f) },
            new[] { new Vector3(.19f,.66f,.62f), new Vector3(.34f,.54f,.74f), new Vector3(.62f,.37f,1.04f), new Vector3(.79f,.41f,.46f) },
            new[] { new Vector3(.26f,.28f,.72f), new Vector3(.38f,.43f,.57f), new Vector3(.64f,.62f,.98f), new Vector3(.77f,.72f,.51f) }
        };

        static void DrawPlateau(MeshPen pen, Rect r, NotebookVisualThemeAsset theme, bool dark, Color ink, int seed, bool smallPrint)
        {
            int quadrant = (r.x > 0 ? 1 : 0) + (r.y > 0 ? 2 : 0);
            var peaks = PlateauPeaks[quadrant];
            Vector2 FromMap(Vector2 v) => r.min + Vector2.Scale(v, r.size);
            bool rising = quadrant == 0 || quadrant == 3;
            float tilt = rising ? .58f : -.65f;

            // The contour centres migrate towards the dominant summit with altitude.
            // Their long axis follows the chain rather than the edge of the collision patch.
            Vector2 summit = peaks[quadrant == 0 ? 1 : 2];
            var baseCentre = Vector2.Lerp(new Vector2(.50f,.50f), summit, .34f);
            for (int level = 0; level < (smallPrint ? 2 : 3); level++)
            {
                var center = Vector2.Lerp(baseCentre, summit, level * .26f);
                Vector2 Vertex(int i)
                {
                    Vector2 v = ReliefRing[i];
                    float variation = .75f + .22f * MeshPen.Hash(seed + i * 23 + level * 5);
                    float x = v.x * (.44f - level * .075f);
                    float y = v.y * (.30f - level * .055f) + tilt * x;
                    return FromMap(center + new Vector2(x, y) * variation);
                }
                var previous = Vertex(ReliefRing.Length - 1);
                for (int i = 0; i < ReliefRing.Length; i++)
                {
                    var next = Vertex(i);
                    if ((i + quadrant * 3 + level) % 11 != 1) pen.Line(previous, next, .075f, ink);
                    previous = next;
                }
            }

            // Little pointed scree marks and fallen rocks break the empty low ground.
            for (int i = 0; i < (smallPrint ? 5 : 12); i++)
            {
                float x = .08f + .84f * MeshPen.Hash(seed + i * 79 + 10);
                bool upper = (i & 1) == 0;
                float y = (upper ? .75f : .10f) + .13f * MeshPen.Hash(seed + i * 47 + 11);
                var at = FromMap(new Vector2(x,y));
                pen.Line(at, at + new Vector2(.42f,.29f), .06f, ink);
                pen.Line(at + new Vector2(.42f,.29f), at + new Vector2(.70f,.06f), .055f, ink);
            }
            foreach (var peak in peaks)
            {
                string id = peak.z > .8f ? "terrain.mountain" : peak.z > .51f ? "terrain.ridge" : "terrain.rock";
                var size = new Vector2(r.width * .31f, r.height * .57f) * peak.z;
                DrawStamp(pen, id, FromMap(peak), size, theme, dark, .69f + peak.z * .18f);
            }
        }

        static readonly Vector2[] ReliefRing = {
            new Vector2(-1,-.25f), new Vector2(-.80f,-.79f), new Vector2(-.22f,-1), new Vector2(.4f,-.81f),
            new Vector2(.92f,-.61f), new Vector2(1,.12f), new Vector2(.67f,.82f), new Vector2(.24f,.89f),
            new Vector2(-.16f,1), new Vector2(-.72f,.69f), new Vector2(-.89f,.18f)
        };

        static void Contour(MeshPen pen, Rect r, float radius, Color ink, int seed, bool slant)
        {
            Vector2 Point(int i)
            {
                var v = ReliefRing[i] * (1 - .14f * MeshPen.Hash(seed + i * 53));
                if (slant) v.y = .68f * v.y + .28f * v.x;
                return r.center + Vector2.Scale(r.size, v) * radius;
            }
            Vector2 previous = Point(ReliefRing.Length - 1);
            for (int i = 0; i < ReliefRing.Length; i++)
            {
                var next = Point(i);
                pen.Line(previous, next, .075f, ink); previous = next;
            }
        }

        static void DrawStamp(MeshPen pen, string id, Vector2 center, Vector2 size, NotebookVisualThemeAsset theme,
            bool dark, float opacity)
        {
            if (!NotebookSpriteCatalog.TryGetBounds(id, out var bounds) || bounds.width <= 0 || bounds.height <= 0) return;
            float scale = Mathf.Min(size.x / bounds.width, size.y / bounds.height);
            NightNotebookEffectPalette.DrawResource(pen, id, center - bounds.center * scale, theme, dark, scale, opacity: opacity);
        }

        internal static void DrawTilledFields(MeshPen pen, BattlefieldView map, NotebookVisualThemeAsset theme, bool dark)
        {
            // Where two perpendicular hedgerows meet, the open corner is a cultivated field.
            // Broken furrows leave the underlying build grid visible right up to the hedge.
            Color ink = MeshPen.Alpha(NightNotebookEffectPalette.Tone(NotebookPalette.Pencil, NightNotebookArt.Pencil, theme, dark), .23f);
            foreach (var a in map.Props)
            foreach (var b in map.Props)
            {
                if (a.Width <= a.Height || b.Width >= b.Height) continue;
                var h = new Rect(a.Cell.X, a.Cell.Y, a.Width, a.Height);
                var v = new Rect(b.Cell.X, b.Cell.Y, b.Width, b.Height);
                bool adjacentX = (h.xMax == v.xMin || v.xMax == h.xMin) && v.yMin < h.yMax && h.yMin < v.yMax;
                bool adjacentY = (h.yMax == v.yMin || v.yMax == h.yMin) && v.xMin < h.xMax && h.xMin < v.xMax;
                if (!adjacentX && !adjacentY) continue;
                float x0 = Mathf.Max(h.xMin, v.center.x < h.center.x ? v.xMax : h.xMin) + .7f;
                float x1 = Mathf.Min(h.xMax, v.center.x > h.center.x ? v.xMin : h.xMax) - .7f;
                float y0 = Mathf.Max(v.yMin, h.center.y < v.center.y ? h.yMax : v.yMin) + .7f;
                float y1 = Mathf.Min(v.yMax, h.center.y > v.center.y ? h.yMin : v.yMax) - .7f;
                bool upright = a.Kind == TerrainPropKind.Shrub;
                float length = upright ? y1 - y0 : x1 - x0, rows = upright ? x1 - x0 : y1 - y0;
                var along = upright ? Vector2.up : Vector2.right;
                var across = upright ? Vector2.right : Vector2.up;
                for (float row = .8f; row < rows; row += 2.1f)
                for (float s = .4f; s < length - .5f; s += 3.4f)
                {
                    var from = new Vector2(x0,y0) + along * s + across * row;
                    pen.Line(from, from + along * Mathf.Min(2.6f, length - s) + across * .13f, .07f, ink);
                }
            }
        }

        internal static void DrawBridges(MeshPen pen, BattlefieldView map, NotebookVisualThemeAsset theme, bool dark)
        {
            // Two strips on the same river channel leave an actual walkable gap.
            // A timber deck follows its exact width; gates and navigation remain map-owned.
            Color ink = NightNotebookEffectPalette.Tone(NotebookPalette.Pencil, NightNotebookArt.Accent, theme, dark);
            foreach (var lower in map.Props)
            {
                if (lower.Kind != TerrainPropKind.Water) continue;
                int y = lower.Cell.Y + lower.Height, top = int.MaxValue;
                foreach (var upper in map.Props)
                    if (upper.Kind == TerrainPropKind.Water && upper.Cell.X == lower.Cell.X && upper.Width == lower.Width && upper.Cell.Y >= y)
                        top = Mathf.Min(top, upper.Cell.Y);
                if (top == int.MaxValue || top <= y) continue;
                int x = lower.Cell.X, end = x + lower.Width;
                bool clear = true;
                for (int row = y; row < top && clear; row++)
                for (int col = x; col < end; col++)
                    if (!map.IsWalkable(new Cell(col, row))) { clear = false; break; }
                if (!clear) continue;
                pen.Rect(x, y, lower.Width, top - y, MeshPen.Alpha(ink, .10f));
                for (float plank = x + .45f; plank < end; plank += 1.1f)
                    pen.Line(new Vector2(plank, y + .2f), new Vector2(plank, top - .2f), .085f, MeshPen.Alpha(ink, .40f));
                Color rail = MeshPen.Alpha(ink, .75f);
                pen.Line(new Vector2(x, y + .16f), new Vector2(end, y + .16f), .15f, rail);
                pen.Line(new Vector2(x, top - .16f), new Vector2(end, top - .16f), .15f, rail);
                for (float post = x + .38f; post < end - .1f; post += (lower.Width - .76f) / 2)
                {
                    pen.Disc(new Vector2(post, y + .27f), .19f, rail, 6);
                    pen.Disc(new Vector2(post, top - .27f), .19f, rail, 6);
                    pen.Line(new Vector2(post + .1f, y + .47f), new Vector2(post + .1f, y + .9f), .08f, rail);
                }
            }
        }
    }
}
