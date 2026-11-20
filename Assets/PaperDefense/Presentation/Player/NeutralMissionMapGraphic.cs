using PaperDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Presentation
{
    /// <summary>Read-only map preview drawn from the current battlefield, including protected wells.</summary>
    public sealed class NeutralMissionMapGraphic : NotebookAtlasGraphic
    {
        IRunReadModel session;
        DefeatReport defeatReport;
        public void Bind(IRunReadModel value) => Bind(value, null);
        public void Bind(IRunReadModel value, DefeatReport report)
        {
            session = value;
            defeatReport = value != null && report != null && report.RunId == value.RunId ? report : null;
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear();
            if (session == null) return;
            Rect rect = rectTransform.rect;
            float scale = .9f * Mathf.Min(rect.width / session.Balance.Width, rect.height / session.Balance.Height);
            if (scale <= 0) return;
            // This is a UI overview: keep thin terrain strokes and important landmarks
            // readable without changing a cell, footprint or battlefield camera.
            var pen = new MeshPen(null, 4096) { MinimumLineWidth = .75f / scale };
            var size = new Vector2(session.Balance.Width, session.Balance.Height);
            var origin = rect.center - size * (.5f * scale);
            DrawChart(pen, size, scale, rect, origin);
            var theme = NotebookVisualThemeAsset.Default;
            using (pen.WithUiDetail(scale))
                NotebookBattlefieldRenderer.Draw(pen, session.Battlefield, theme, darkSurface: true,
                    hordeSides: HordeWaveForecast.DisplayedSides(session), hordeBands: HordeWaveForecast.DisplayedBands(session));
            var home = session.BaseCell;
            var castle = new Vector2(home.X + .5f, home.Y + .5f);
            float castleSize = Mathf.Max(2, 22 / scale);
            pen.Disc(castle, castleSize * .59f, MeshPen.Alpha(FieldMenuSkin.Background, .87f), 24);
            pen.Ring(castle, castleSize * .59f, .85f / scale, MeshPen.Alpha(FieldMenuSkin.Accent, .8f), 24);
            float health = session.BaseHealth / Mathf.Max(1, session.Balance.BaseHealth);
            DoodleGlyphs.Castle(pen, castle, health, theme: theme, uniformScale: castleSize / 3);
            foreach (var well in session.Wells)
                DoodleGlyphs.DrawResource(pen, "object.well." + well.Color.ToString().ToLowerInvariant(),
                    new Vector2(well.Cell.X + .5f, well.Cell.Y + .5f), Mathf.Max(1.4f, 15 / scale), theme: theme);
            if (defeatReport != null) DrawDefeatReview(pen, scale);
            pen.CopyTo(target, origin, scale);
        }

        void DrawDefeatReview(MeshPen pen, float scale)
        {
            bool OnMap(Cell cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < session.Balance.Width && cell.Y < session.Balance.Height;
            Vector2 Center(Cell cell) => new Vector2(cell.X + .5f, cell.Y + .5f);
            Color loss = new Color32(231, 119, 101, 255);
            Color idle = new Color32(238, 196, 93, 255);
            float pixel = 1 / scale;
            // These are recorded destruction cells, never an inferred route or missing defence.
            foreach (var breach in session.Statistics.Breaches)
            {
                if (!OnMap(breach.Cell)) continue;
                var center = Center(breach.Cell);
                var diagonal = new Vector2(3.5f, 3.5f) * pixel;
                pen.Line(center - diagonal, center + diagonal, 1.6f * pixel, loss);
                diagonal.x = -diagonal.x;
                pen.Line(center - diagonal, center + diagonal, 1.6f * pixel, loss);
            }
            foreach (var position in defeatReport.IdlePositions)
            {
                if (!OnMap(position.Cell)) continue;
                pen.Ring(Center(position.Cell), 6 * pixel, 1.8f * pixel, idle, 24);
            }
            if (!defeatReport.DangerousApproach.HasValue) return;
            var source = defeatReport.DangerousApproach.Value.Cell;
            if (!OnMap(source)) return;
            // Cell records the actual attack/launch position. It does not identify a spawn or path.
            var point = Center(source);
            pen.Disc(point, 8 * pixel, MeshPen.Alpha(FieldMenuSkin.Background, .85f), 24);
            pen.Ring(point, 7 * pixel, 2 * pixel, loss, 24);
            pen.Disc(point, 2.5f * pixel, loss, 16);
        }

        static void DrawChart(MeshPen pen, Vector2 size, float scale, Rect rect, Vector2 origin)
        {
            // The map grid is registered to cells. Only the surrounding notebook uses the paper's grid.
            float pixel = 1 / scale;
            pen.Rect(0, 0, size.x, size.y, new Color32(31, 52, 44, 255));
            Color rule = MeshPen.Alpha(FieldMenuSkin.Muted, .17f);
            int cells = Mathf.Max(4, Mathf.CeilToInt(21 * pixel / 4) * 4);
            for (int x = cells; x < size.x; x += cells)
                pen.Line(new Vector2(x, 0), new Vector2(x, size.y), .7f * pixel, rule);
            for (int y = cells; y < size.y; y += cells)
                pen.Line(new Vector2(0, y), new Vector2(size.x, y), .7f * pixel, rule);

            Color border = MeshPen.Alpha(FieldMenuSkin.Border, .68f);
            pen.Line(Vector2.zero, new Vector2(size.x, 0), pixel, border);
            pen.Line(new Vector2(size.x, 0), size, pixel, border);
            pen.Line(size, new Vector2(0, size.y), pixel, border);
            pen.Line(new Vector2(0, size.y), Vector2.zero, pixel, border);
            float cornerLength = Mathf.Min(9 * pixel, size.x * .08f, size.y * .08f);
            for (int i = 0; i < 4; i++)
            {
                bool left = i < 2, bottom = i % 2 == 0;
                var corner = new Vector2(left ? 0 : size.x, bottom ? 0 : size.y);
                pen.Line(corner, corner + Vector2.right * (left ? cornerLength : -cornerLength),
                    1.3f * pixel, FieldMenuSkin.Muted);
                pen.Line(corner, corner + Vector2.up * (bottom ? cornerLength : -cornerLength),
                    1.3f * pixel, FieldMenuSkin.Muted);
            }

            // Keep the compass in the empty margin, where it cannot be mistaken for a unit.
            float sideMargin = origin.x - rect.xMin;
            if (sideMargin < 48 || rect.height < 96) return;
            Vector2 compass = new Vector2(rect.xMax - sideMargin * .5f, rect.center.y);
            compass = (compass - origin) * pixel;
            float radius = Mathf.Min(22, sideMargin * .24f) * pixel;
            pen.Ring(compass, radius * .7f, pixel, border, 28);
            for (int i = 0; i < 4; i++)
            {
                Vector2 direction = i == 0 ? Vector2.up : i == 1 ? Vector2.right : i == 2 ? Vector2.down : Vector2.left;
                Vector2 across = new Vector2(direction.y, -direction.x);
                Vector2 tip = compass + direction * radius * (i == 0 ? 1.18f : .83f);
                pen.Triangle(compass, compass + across * radius * .16f, tip, i == 0 ? FieldMenuSkin.Accent : border);
                pen.Triangle(compass, tip, compass - across * radius * .16f, rule);
            }
        }
    }
}
