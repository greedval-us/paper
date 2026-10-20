using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Illustrated margins on the sheet, below every simulation actor and placement mark.</summary>
    internal static class NotebookSceneryComposition
    {
        internal static void Draw(MeshPen pen, Rect page, NotebookVisualThemeAsset theme)
        {
            // Source 15/16 uses four rocky corners and separate forest islands. The
            // open centre remains legible; scenery introduces no terrain/collision cells.
            RockMass(.045f, .84f, .16f, .28f, "terrain.mountain");
            RockMass(.95f, .86f, .19f, .29f, "terrain.mountain");
            RockMass(.055f, .045f, .22f, .32f, "terrain.ridge");
            RockMass(.95f, .025f, .24f, .31f, "terrain.mountain");

            Forest(.105f, .87f, .17f, 3, 17);
            Forest(.275f, .87f, .19f, 4, 31);
            Forest(.705f, .87f, .18f, 5, 71);
            Forest(.875f, .70f, .16f, 4, 113);
            Forest(.235f, .56f, .16f, 4, 137);
            Forest(.265f, .20f, .19f, 5, 173);
            Forest(.73f, .20f, .20f, 5, 211);
            Forest(.09f, .25f, .16f, 3, 251);
            Forest(.94f, .40f, .14f, 3, 283);

            RockMass(.165f, .67f, .07f, .10f, "terrain.rock");
            RockMass(.32f, .34f, .08f, .11f, "terrain.rock");
            RockMass(.81f, .50f, .06f, .085f, "terrain.rock");
            RockMass(.64f, .87f, .06f, .09f, "terrain.rock");
            RockMass(.19f, .40f, .055f, .065f, "terrain.shrub");
            RockMass(.82f, .27f, .055f, .06f, "terrain.shrub");
            // Small tufts occupy the negative space, rather than another wall of trees.
            for (int index = 0; index < 135; index++)
            {
                float x = .018f + MeshPen.Hash(index * 47 + 91) * .964f;
                float y = .018f + MeshPen.Hash(index * 73 + 119) * .964f;
                if (x > .34f && x < .66f && y > .22f && y < .79f) continue;
                var root = Point(x, y);
                float size = page.height * (.009f + MeshPen.Hash(index + 801) * .008f);
                Color ink = MeshPen.Alpha(NotebookPalette.Blue, .40f + MeshPen.Hash(index) * .17f);
                float width = page.height * .0009f;
                pen.Line(root, root + new Vector2(-size * .28f, size * .65f), width, ink);
                pen.Line(root + Vector2.right * size * .13f, root + new Vector2(.06f, 1) * size, width, ink);
                pen.Line(root + Vector2.right * size * .24f, root + new Vector2(.55f, .46f) * size, width, ink);
                for (int dot = 0; dot < 3; dot++)
                    pen.Disc(root + new Vector2((dot - 1) * size * .48f, -size * .16f), width * .8f, ink, 4);
            }

            Vector2 Point(float x, float y) => page.min + Vector2.Scale(page.size, new Vector2(x, y));
            void RockMass(float x, float y, float width, float height, string id)
                => Fit(pen, id, new Rect(Point(x, y) - new Vector2(page.width * width, page.height * height) * .5f,
                    new Vector2(page.width * width, page.height * height)), .89f, theme);
            void Forest(float x, float y, float height, int count, int seed)
            {
                for (int index = 0; index < count; index++)
                {
                    float treeHeight = page.height * height * (.67f + MeshPen.Hash(seed + index * 11) * .40f);
                    float offsetX = (index - (count - 1) * .5f) * treeHeight * .29f;
                    float offsetY = (index % 2 == 0 ? .10f : -.16f) * treeHeight;
                    var root = Point(x, y) + new Vector2(offsetX, offsetY);
                    Fit(pen, "terrain.pine", new Rect(root.x - treeHeight * .33f, root.y - treeHeight * .5f,
                        treeHeight * .66f, treeHeight), .91f + (index % 2) * .05f, theme);
                }
            }
        }

        static void Fit(MeshPen pen, string id, Rect available, float opacity, NotebookVisualThemeAsset theme)
        {
            if (!NotebookSpriteCatalog.TryGetBounds(id, out var bounds) || bounds.width <= 0 || bounds.height <= 0) return;
            float scale = Mathf.Min(available.width / bounds.width, available.height / bounds.height);
            DoodleGlyphs.DrawResource(pen, id, available.center - bounds.center * scale, scale, opacity: opacity, theme: theme);
        }
    }
}
