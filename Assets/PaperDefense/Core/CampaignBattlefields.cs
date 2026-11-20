namespace PaperDefense.Core
{
    /// <summary>Authored 96 × 64 campaign pages. Every terrain patch occupies its full rectangle in the navigation grid.</summary>
    internal static class CampaignBattlefields
    {
        internal static BattlefieldDefinition Create(GameBalance rules, int page)
        {
            var map = new BattlefieldDefinition { Enabled = true, Mode = BattlefieldMode.OpenTerrain };
            void Castle(CampaignTerrainStyle style, int x, int y)
            {
                map.Style = style; map.OverrideCastle = true; map.CastleX = x; map.CastleY = y;
            }
            void Gate(int x, int y) => map.Entrances.Add(new BattlefieldCell(x, y));
            void Patch(TerrainPropKind kind, int x, int y, int width, int height)
                => map.Props.Add(new TerrainPropDefinition { Kind = kind, X = x, Y = y, Width = width, Height = height });
            void Wells(Cell red, Cell blue, Cell green)
            {
                foreach (var well in rules.WellPlacements)
                {
                    var at = well.Color == InkColor.Red ? red : well.Color == InkColor.Blue ? blue : green;
                    well.XNumerator = at.X; well.XDenominator = rules.Width;
                    well.YNumerator = at.Y; well.YDenominator = rules.Height;
                }
            }
            const TerrainPropKind mountain = TerrainPropKind.Mountain, pine = TerrainPropKind.Pine,
                grove = TerrainPropKind.Broadleaf, rock = TerrainPropKind.Rock, hedge = TerrainPropKind.Shrub,
                water = TerrainPropKind.Water, wall = TerrainPropKind.Rampart;
            switch (page)
            {
                case 0:
                    // Keep the original opening's terrain and seeded gate order.
                    for (int lane = 0; lane < 2; lane++)
                    {
                        int y = 20 + 24 * lane, x = 32 + 32 * lane;
                        Gate(95, y); Gate(x, 63); Gate(x, 0); Gate(0, y);
                    }
                    Patch(mountain, 26, 10, 7, 10); Patch(mountain, 26, 44, 7, 10);
                    for (int i = 0; i < 6; i++)
                    {
                        int x = 8 + i * 13, y = 7 + i * 17 % 48;
                        // The original scatter excludes the castle apron and the three resource deposits.
                        if (x <= 52 && x + 1 >= 44 && y <= 36 && y + 2 >= 28) continue;
                        bool reserved = false;
                        foreach (var well in rules.WellPlacements)
                        {
                            var at = well.Resolve(96, 64);
                            int radius = well.ReservedRadius + 1;
                            if (x <= at.X + radius && x + 1 >= at.X - radius &&
                                y <= at.Y + radius && y + 2 >= at.Y - radius) reserved = true;
                        }
                        if (!reserved) Patch(pine, x, y, 2, 3);
                    }
                    break;
                case 1:
                    Castle(CampaignTerrainStyle.NorthPass, 48, 13);
                    Gate(28, 63); Gate(68, 63);
                    Patch(mountain, 0, 39, 20, 11); Patch(mountain, 76, 39, 20, 11);
                    Patch(mountain, 40, 39, 16, 19); Patch(rock, 44, 33, 8, 6);
                    Patch(pine, 7, 21, 12, 6); Patch(pine, 76, 21, 12, 6);
                    Patch(grove, 22, 7, 9, 5); Patch(grove, 65, 7, 9, 5);
                    Wells(new Cell(28, 32), new Cell(68, 32), new Cell(48, 23));
                    break;
                case 2:
                    Castle(CampaignTerrainStyle.Crossroads, 48, 32);
                    Gate(0, 32); Gate(95, 32); Gate(48, 63); Gate(48, 0);
                    // Four impassable quarters extend to the map boundary. Only the arms meet the castle.
                    Patch(mountain, 0, 0, 39, 23); Patch(mountain, 58, 0, 38, 23);
                    Patch(mountain, 0, 42, 39, 22); Patch(mountain, 58, 42, 38, 22);
                    Wells(new Cell(20, 32), new Cell(75, 32), new Cell(48, 53));
                    break;
                case 3:
                    Castle(CampaignTerrainStyle.RiverBridges, 76, 32);
                    Gate(0, 17); Gate(20, 0); Gate(0, 46); Gate(35, 0);
                    // Shore-to-shore openings at y=12..22 and y=41..51 carry the two bridges.
                    Patch(water, 45, 0, 8, 12); Patch(water, 45, 23, 8, 18); Patch(water, 45, 52, 8, 12);
                    Patch(grove, 22, 29, 11, 7); Patch(hedge, 58, 31, 7, 4);
                    Patch(pine, 75, 53, 10, 6); Patch(pine, 79, 7, 8, 5);
                    Wells(new Cell(27, 19), new Cell(69, 15), new Cell(64, 48));
                    break;
                case 4:
                    Castle(CampaignTerrainStyle.Redoubt, 58, 33);
                    Gate(95, 24); Gate(45, 0); Gate(95, 48); Gate(76, 0);
                    Patch(wall, 40, 16, 9, 4); Patch(wall, 61, 16, 15, 4);
                    Patch(wall, 72, 20, 4, 9); Patch(wall, 72, 39, 4, 11);
                    Patch(wall, 40, 46, 32, 4); Patch(wall, 40, 26, 4, 20);
                    Patch(rock, 20, 22, 7, 5); Patch(pine, 84, 7, 6, 8);
                    Wells(new Cell(53, 23), new Cell(65, 35), new Cell(32, 43));
                    break;
                case 5:
                    Castle(CampaignTerrainStyle.FourFields, 48, 31);
                    Gate(0, 18); Gate(95, 45); Gate(27, 63); Gate(68, 0);
                    Gate(0, 46); Gate(95, 17); Gate(68, 63); Gate(27, 0);
                    Patch(grove, 14, 40, 18, 5); Patch(hedge, 29, 45, 3, 15);
                    Patch(grove, 57, 47, 5, 13); Patch(hedge, 62, 47, 22, 3);
                    Patch(grove, 64, 17, 18, 5); Patch(hedge, 64, 4, 3, 13);
                    Patch(grove, 34, 5, 5, 12); Patch(hedge, 12, 14, 22, 3);
                    Wells(new Cell(22, 26), new Cell(75, 32), new Cell(48, 53));
                    break;
                case 6:
                    Castle(CampaignTerrainStyle.Fold, 69, 16);
                    Gate(0, 26); Gate(26, 63); Gate(0, 49); Gate(61, 63);
                    // A continuous stepped fold separates the northwest from the castle except at two saddle gaps.
                    Patch(mountain, 16, 0, 9, 8); Patch(mountain, 24, 8, 8, 7);
                    Patch(mountain, 38, 21, 8, 7); Patch(mountain, 45, 28, 8, 7);
                    Patch(mountain, 52, 35, 8, 8);
                    Patch(mountain, 67, 48, 8, 7); Patch(mountain, 74, 55, 9, 9);
                    Patch(rock, 11, 42, 5, 5); Patch(pine, 78, 26, 7, 4);
                    Wells(new Cell(33, 20), new Cell(64, 41), new Cell(55, 14));
                    break;
                case 7:
                    Castle(CampaignTerrainStyle.StoneGate, 22, 33);
                    Gate(95, 17); Gate(95, 47);
                    Patch(mountain, 55, 25, 41, 13); Patch(rock, 45, 28, 10, 8);
                    Patch(mountain, 39, 0, 9, 17); Patch(mountain, 40, 47, 9, 17);
                    Patch(rock, 65, 7, 13, 4); Patch(rock, 67, 54, 14, 4);
                    Patch(pine, 6, 16, 5, 6); Patch(pine, 10, 48, 5, 6);
                    Wells(new Cell(35, 33), new Cell(68, 17), new Cell(68, 47));
                    break;
                case 8:
                    Castle(CampaignTerrainStyle.Citadel, 50, 29);
                    Gate(0, 19); Gate(95, 44); Gate(28, 63); Gate(69, 0);
                    Gate(0, 46); Gate(95, 16); Gate(70, 63); Gate(29, 0);
                    Patch(wall, 17, 10, 19, 3); Patch(wall, 50, 10, 30, 3);
                    Patch(wall, 17, 50, 34, 3); Patch(wall, 65, 50, 15, 3);
                    Patch(wall, 17, 13, 3, 12); Patch(wall, 17, 38, 3, 12);
                    Patch(wall, 77, 13, 3, 17); Patch(wall, 77, 42, 3, 8);
                    Patch(wall, 35, 20, 19, 2); Patch(wall, 64, 20, 2, 11);
                    Patch(wall, 49, 39, 17, 2); Patch(wall, 35, 31, 2, 10);
                    Wells(new Cell(28, 27), new Cell(70, 35), new Cell(48, 45));
                    break;
                default: throw new System.ArgumentOutOfRangeException(nameof(page));
            }
            return map;
        }
    }
}
