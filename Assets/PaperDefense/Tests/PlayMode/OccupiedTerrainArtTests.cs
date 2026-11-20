using System.Linq;
using NUnit.Framework;
using PaperDefense.Core;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class OccupiedTerrainArtTests
    {
        static BattlefieldView Map(int width, int height)
        {
            var source = new BattlefieldDefinition { Enabled = true, Mode = BattlefieldMode.OpenTerrain };
            source.Entrances.Add(new BattlefieldCell(0, 5));
            source.Props.Add(new TerrainPropDefinition { Kind = TerrainPropKind.Mountain, X = 4, Y = 4, Width = width, Height = height });
            return source.Resolve(36, 36, new Cell(32, 32));
        }

        static UIVertex[] Draw(TerrainPropView prop, bool world)
        {
            var pen = new MeshPen(null);
            if (world) pen.WorldUnitsPerPixel = .15f;
            else pen.MinimumLineWidth = .75f / 4;
            int budget = 512;
            NotebookBattlefieldRenderer.DrawOccupiedTerrain(pen, prop, NotebookVisualThemeAsset.Default, true, ref budget);
            using (var target = new VertexHelper())
            {
                pen.CopyTo(target, Vector2.zero, 1);
                var vertices = new UIVertex[target.currentVertCount];
                for (int i = 0; i < vertices.Length; i++) target.PopulateUIVertex(ref vertices[i], i);
                return vertices;
            }
        }

        [TestCase(24, 2)] [TestCase(2, 24)] [TestCase(8, 8)]
        public void TheFullBlockedRectangleHasMaterialInBothWorldAndMapWithoutChangingReservations(int width, int height)
        {
            var map = Map(width, height); var prop = map.Props.Single();
            var world = Draw(prop, true); var preview = Draw(prop, false);
            var expected = new[] { new Vector3(4,4), new Vector3(4+width,4), new Vector3(4+width,4+height), new Vector3(4,4+height) };
            Assert.That(world.Take(4).Select(vertex => vertex.position), Is.EqualTo(expected));
            Assert.That(preview.Take(4).Select(vertex => vertex.position), Is.EqualTo(expected));
            Assert.That(world.Take(4).All(vertex => vertex.color.a > 0), Is.True,
                "A long blocked ridge cannot be represented only by a small central symbol.");
            Assert.That(world.Length, Is.LessThan(2200), "Hatching remains bounded static geometry.");
            for (int y = 4; y < 4 + height; y++)
            for (int x = 4; x < 4 + width; x++) Assert.That(map.IsReservedTerrain(new Cell(x,y)), Is.True);
            Assert.That(map.IsReservedTerrain(new Cell(3,4)), Is.False);
            Assert.That(map.IsReservedTerrain(new Cell(4+width,4)), Is.False);
        }

        [Test] public void EntranceArrowUsesTheActualUiPixelHintAndRetainsABoundedWorldSize()
        {
            var pen = new MeshPen(null);
            Assert.That(NotebookBattlefieldRenderer.EntranceScale(pen), Is.EqualTo(2));
            using (pen.WithUiDetail(4)) Assert.That(NotebookBattlefieldRenderer.EntranceScale(pen), Is.EqualTo(6));
            using (pen.WithUiDetail(2))
                Assert.That(NotebookBattlefieldRenderer.EntranceScale(pen) * .44f * 2, Is.GreaterThanOrEqualTo(10),
                    "A compact mission preview must keep an actual ten-pixel entrance arrow.");
            pen.WorldUnitsPerPixel = .1f;
            Assert.That(NotebookBattlefieldRenderer.EntranceScale(pen), Is.GreaterThan(2));
            pen.WorldUnitsPerPixel = 10;
            Assert.That(NotebookBattlefieldRenderer.EntranceScale(pen), Is.EqualTo(12));
        }

        [Test] public void EachCampaignChartFitsOneUiMeshAtOverviewAndPreparationSizes()
        {
            var baseline = NotebookTowerDefensePreset.Create();
            var theme = NotebookVisualThemeAsset.Default;
            var pen = new MeshPen(null);
            foreach (var mission in CampaignCatalog.Default.Missions.Skip(1))
            {
                var map = MissionBalanceFactory.Create(baseline, mission).ResolveBattlefield();
                foreach (float pixelsPerCell in new[] { 2f, 6f })
                {
                    pen.Clear();
                    pen.MinimumLineWidth = .75f / pixelsPerCell;
                    using (pen.WithUiDetail(pixelsPerCell))
                        NotebookBattlefieldRenderer.Draw(pen, map, theme, true, 15);
                    Assert.That(pen.VertexCount, Is.InRange(80, 55000), mission.Id + " must leave room for the chart and castle.");
                }
            }
        }

        [Test] public void RiverDecksSpanJustTheTwoWalkableGapsBetweenTheWaterStrips()
        {
            var mission = CampaignCatalog.Default.Missions[3];
            var map = MissionBalanceFactory.Create(NotebookTowerDefensePreset.Create(), mission).ResolveBattlefield();
            var pen = new MeshPen(null);
            CampaignTerrainDrawing.DrawBridges(pen, map, NotebookVisualThemeAsset.Default, true);
            var water = map.Props.Where(p => p.Kind == TerrainPropKind.Water).OrderBy(p => p.Cell.Y).ToArray();
            Assert.That(water.Length, Is.EqualTo(3));
            bool lowerDeck = false, upperDeck = false;
            using (var vertices = new VertexHelper())
            {
                pen.CopyTo(vertices, Vector2.zero, 1);
                for (int i = 0; i < vertices.currentVertCount; i++)
                {
                    var point = new UIVertex();
                    vertices.PopulateUIVertex(ref point, i);
                    float x = point.position.x, y = point.position.y;
                    Assert.That(x, Is.InRange((float)water[0].Cell.X, (float)(water[0].Cell.X + water[0].Width)));
                    bool lower = y >= water[0].Cell.Y + water[0].Height && y <= water[1].Cell.Y;
                    bool upper = y >= water[1].Cell.Y + water[1].Height && y <= water[2].Cell.Y;
                    Assert.That(lower || upper, Is.True, "The drawn crossing may not extend into impassable water.");
                    lowerDeck |= lower; upperDeck |= upper;
                }
            }
            Assert.That(lowerDeck && upperDeck, Is.True);
        }
    }
}
