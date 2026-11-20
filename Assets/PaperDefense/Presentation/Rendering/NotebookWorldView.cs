using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>
    /// Draws five independently refreshed batches. Simulation enemies remain plain data;
    /// even the largest swarm does not create a GameObject for each unit.
    /// </summary>
    public sealed class NotebookWorldView : MonoBehaviour
    {
        public Camera WorldCamera { get; set; }
        public NotebookVisualThemeAsset VisualTheme;
        public Color? PaperColor { get; set; }
        public NotebookViewSettingsAsset ViewSettings;
        public bool Paused { get; set; }
        public bool ShowEnemyHealthBars { get; set; } = true;
        public bool ModernEnemyPresentation { get; set; }
        float castleVisualScale = 1;
        public float CastleVisualScale
        {
            get => castleVisualScale;
            set
            {
                float next = Mathf.Max(.1f, value);
                if (castleVisualScale == next) return;
                castleVisualScale = next; staticDirty = true;
            }
        }
        public double VisualSeconds => visualClock.Seconds;
        public NotebookAnimationState AnimationState => animation;
        public BattleReadability Readability { get; } = new BattleReadability();
        bool showRanges;
        bool compactBaseMarker;
        public bool CompactBaseMarker
        {
            get => compactBaseMarker;
            set
            {
                if (compactBaseMarker == value) return;
                compactBaseMarker = value; staticDirty = true;
                if (session != null && paperPen != null) DrawPaper();
            }
        }
        public bool ShowRanges
        {
            get => showRanges;
            set { if (showRanges == value) return; showRanges = value; staticDirty = true; }
        }
        public int StaticGeometryRefreshCount { get; private set; }
        public int PreviewGeometryRefreshCount { get; private set; }
        public int ActiveVisualEffects => firingMarks.Count + animation.Deaths.Count;
        public long DroppedVisualEffects { get; private set; }
        public double LastMeshBuildMilliseconds { get; private set; }
        public double TotalMeshBuildMilliseconds { get; private set; }
        public int MeshBuildCount { get; private set; }
        public int StaticStructureBuildCount => StaticGeometryRefreshCount;

        IRunReadModel session;
        Material inkMaterial;
        MeshPen paperPen, structurePen, worldPen, effectsPen, previewPen;
        readonly List<Mesh> meshes = new List<Mesh>(5);
        readonly List<Cell> previewCells = new List<Cell>(128);
        readonly List<SimulationEvent> firingMarks = new List<SimulationEvent>(256);
        readonly NotebookVisualClock visualClock = new NotebookVisualClock();
        readonly NotebookAnimationState animation = new NotebookAnimationState();
        readonly HashSet<Cell> wallCells = new HashSet<Cell>();
        NotebookVisualThemeAsset Theme => VisualTheme != null ? VisualTheme : NotebookVisualThemeAsset.Default;
        bool DarkSurface => PaperColor.HasValue && PaperColor.Value.grayscale < .3f;
        NotebookViewSettingsAsset Settings => ViewSettings != null ? ViewSettings : NotebookViewSettingsAsset.Default;
        float minorGridOpacity = 1, terrainStrokePixels = 1, towerMarkerPixels;
        StructureKind previewKind;
        MaterialTier previewTier;
        bool previewValid;
        float previewRange = -1;
        TowerBlueprint previewBlueprint;
        RecipeSnapshot previewRecipe;
        ResolvedEntry previewEntry;
        Cell? inspectedSector;
        float? previewFacing;
        int paperWidth, paperHeight, paperHordeSides, paperHordeBands;
        int visualVersion = -1, topologyVersion = -1, castleHealthBand = -1;
        RunPhase renderedPhase;
        bool staticDirty = true;

        public void Initialize(IRunReadModel newSession)
        {
            EnsureMeshes();
            Bind(newSession);
        }

        public void Bind(IRunReadModel newSession)
        {
            EnsureMeshes();
            if (worldPen == null) return;
            session = newSession;
            RefreshReadability();
            firingMarks.Clear();
            animation.Clear();
            visualClock.Reset(session?.Elapsed ?? 0);
            DroppedVisualEffects = 0;
            previewCells.Clear();
            previewBlueprint = null;
            previewRecipe = null;
            previewEntry = null;
            inspectedSector = null;
            previewFacing = null;
            staticDirty = true;
            visualVersion = topologyVersion = castleHealthBand = -1;
            if (session != null)
            {
                paperWidth = session.Balance.Width;
                paperHeight = session.Balance.Height;
                UpdateViewScale();
                DrawPaper();
                DrawStructures();
                DrawWorld();
            }
            else
            {
                paperPen.Clear(); paperPen.Upload();
                structurePen.Clear(); structurePen.Upload();
                worldPen.Clear(); worldPen.Upload();
            }
            effectsPen.Clear(); effectsPen.Upload();
            previewPen.Clear(); previewPen.Upload();
        }

        public void SetPreview(IReadOnlyList<Cell> cells, StructureKind kind, MaterialTier tier, bool valid, float range = -1,
            TowerBlueprint blueprint = null, RecipeSnapshot recipe = null, ResolvedEntry entry = null, float? facingDegrees = null)
        {
            int count = cells == null ? 0 : cells.Count;
            bool changed = previewKind != kind || previewTier != tier || previewValid != valid || previewCells.Count != count || previewRange != range
                || !ReferenceEquals(previewBlueprint, blueprint) || !ReferenceEquals(previewRecipe, recipe) || !ReferenceEquals(previewEntry, entry)
                || previewFacing != facingDegrees;
            for (int i = 0; !changed && i < count; i++) changed = previewCells[i] != cells[i];
            if (!changed) return;
            previewCells.Clear();
            if (cells != null)
                for (int i = 0; i < cells.Count; i++) previewCells.Add(cells[i]);
            previewKind = kind;
            previewTier = tier;
            previewValid = valid;
            previewRange = range;
            previewBlueprint = blueprint;
            previewRecipe = recipe;
            previewEntry = entry;
            previewFacing = facingDegrees;
            if (previewPen != null && session != null) DrawPreview();
        }

        public void ClearPreview()
        {
            if (previewCells.Count == 0) return;
            previewCells.Clear();
            if (previewPen != null && session != null) DrawPreview();
        }

        public void SetInspectedSector(Cell? cell)
        {
            if (inspectedSector == cell) return;
            if (showRanges) staticDirty = true;
            inspectedSector = cell;
            if (session != null && previewPen != null) DrawPreview();
        }

        /// <summary>Launches are represented by live projectiles; explosions only by impact events.</summary>
        public void AddEvent(SimulationEvent item)
        {
            if (visualClock.Terminal || !animation.AddEvent(item, Theme?.MaximumDeaths ?? 128)) return;
            if (item.Kind == SimulationEventKind.ProjectileLaunched || item.Kind == SimulationEventKind.EnemyGlobLaunched
                || item.Kind == SimulationEventKind.EnemyAttack || item.Kind == SimulationEventKind.EnemyHit
                || item.Kind == SimulationEventKind.EnemyDied) return;
            int maximum = Mathf.Max(1, Theme?.MaximumEffects ?? 256);
            if (firingMarks.Count >= maximum) { firingMarks.RemoveAt(0); DroppedVisualEffects++; }
            firingMarks.Add(item);
        }

        void EnsureMeshes()
        {
            if (worldPen != null) return;
            Shader shader = Resources.Load<Shader>("NotebookInk");
            if (shader == null) shader = Shader.Find("PaperDefense/NotebookInk");
            if (shader == null)
            {
                Debug.LogError("NotebookInk shader is missing. Reimport PaperDefense/Presentation/Rendering/Resources.");
                return;
            }
            inkMaterial = new Material(shader) { name = "Notebook ink (runtime)" };
            paperPen = CreatePen("Graph paper", 0, 12000);
            structurePen = CreatePen("Castle, walls and weapon bases", 5, 30000);
            worldPen = CreatePen("Swarm, aiming weapons and construction", 10, 180000);
            effectsPen = CreatePen("Ink firing and impact marks", 20, 16000);
            previewPen = CreatePen("Pencil building preview", 30, 16000);
        }

        MeshPen CreatePen(string layerName, int order, int capacity)
        {
            GameObject layer = new GameObject(layerName);
            layer.transform.SetParent(transform, false);
            Mesh mesh = new Mesh { name = layerName };
            meshes.Add(mesh);
            MeshFilter filter = layer.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = layer.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = inkMaterial;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return new MeshPen(mesh, capacity);
        }

        void LateUpdate()
        {
            LastMeshBuildMilliseconds = 0;
            if (session == null || worldPen == null) return;
            visualClock.Advance(session.Elapsed, session.Phase, Paused, Time.unscaledDeltaTime, Theme?.MaximumTerminalTailSeconds ?? 1.2f);
            animation.Prune(visualClock.Seconds, Theme);
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            bool scaleChanged = UpdateViewScale();
            if (scaleChanged || paperHordeSides != HordeWaveForecast.DisplayedSides(session)
                || paperHordeBands != HordeWaveForecast.DisplayedBands(session)) DrawPaper();
            if (scaleChanged) staticDirty = true;
            if ((previewCells.Count > 0 && (scaleChanged || renderedPhase != session.Phase)) ||
                (scaleChanged && inspectedSector.HasValue) ||
                (inspectedSector.HasValue && visualVersion != session.StructureVisualVersion)) DrawPreview();
            int band = CastleHealthBand();
            if (staticDirty || visualVersion != session.StructureVisualVersion || topologyVersion != session.TopologyVersion
                || castleHealthBand != band || renderedPhase != session.Phase) DrawStructures();
            DrawWorld();
            DrawEffects();
            LastMeshBuildMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency;
            TotalMeshBuildMilliseconds += LastMeshBuildMilliseconds;
            MeshBuildCount++;
        }

        bool UpdateViewScale()
        {
            var settings = Settings;
            float units = 0;
            if (WorldCamera != null && WorldCamera.orthographic && WorldCamera.pixelHeight > 0)
            {
                float pixels = WorldCamera.pixelHeight / Mathf.Max(.001f, 2 * WorldCamera.orthographicSize);
                // Cache static geometry per eighth of a screen pixel/cell, independent of camera position.
                units = 1 / Mathf.Max(.125f, Mathf.Round(pixels * 8) / 8);
            }
            float grid = units > 0 ? settings.MinorGridOpacity(1 / units) : 1;
            float minimum = Mathf.Clamp(settings.MinimumStrokePixels, .5f, 3);
            float terrain = Mathf.Clamp(settings.TerrainStrokePixels, .5f, 2);
            if (worldPen.WorldUnitsPerPixel == units && worldPen.MinimumStrokePixels == minimum
                && minorGridOpacity == grid && terrainStrokePixels == terrain && towerMarkerPixels == settings.TowerMarkerPixels) return false;
            paperPen.WorldUnitsPerPixel = structurePen.WorldUnitsPerPixel = worldPen.WorldUnitsPerPixel =
                effectsPen.WorldUnitsPerPixel = previewPen.WorldUnitsPerPixel = units;
            structurePen.MinimumStrokePixels = worldPen.MinimumStrokePixels = effectsPen.MinimumStrokePixels = previewPen.MinimumStrokePixels = minimum;
            minorGridOpacity = grid; terrainStrokePixels = terrain;
            towerMarkerPixels = settings.TowerMarkerPixels;
            return true;
        }

        void DrawPaper()
        {
            MeshPen p = paperPen;
            p.Clear();
            p.MinimumStrokePixels = 0;
            bool night = DarkSurface;
            Color navy = night ? NightNotebookArt.Ink : NotebookPalette.Navy;
            const float surround = 96;
            // Wide windows reveal the scenery outside the playable sheet. Keep that space
            // illustrated without adding blocked cells or changing the simulation map.
            p.Rect(-surround, -surround, paperWidth + surround * 2, paperHeight + surround * 2,
                PaperColor ?? NotebookPalette.Paper);
            Color extensionGrid = night ? MeshPen.Alpha(NightNotebookArt.Grid, .075f) : new Color(.35f, .54f, .68f, .10f);
            float extensionStroke = p.WorldUnitsPerPixel > 0 ? .7f * p.WorldUnitsPerPixel : .1f;
            for (int x = -90; x <= paperWidth + 90; x += 5)
                p.Line(new Vector2(x, -surround), new Vector2(x, paperHeight + surround), extensionStroke, extensionGrid);
            for (int y = -90; y <= paperHeight + 90; y += 5)
                p.Line(new Vector2(-surround, y), new Vector2(paperWidth + surround, y), extensionStroke, extensionGrid);
            DrawSurroundingScenery(p);
            // The subtle page shadow and warm edge make the world feel like an actual sheet.
            p.Rect(-2.5f, -2.5f, paperWidth + 5, paperHeight + 5, night ? new Color32(12, 27, 23, 255) : PaperColor.HasValue ? new Color32(220, 229, 242, 255) : new Color(.89f, .88f, .82f));
            p.Rect(-1.75f, -1.75f, paperWidth + 3.5f, paperHeight + 3.5f, PaperColor ?? NotebookPalette.Paper);
            p.Rect(-1.7f, -1.7f, .22f, paperHeight + 3.4f, night ? MeshPen.Alpha(NightNotebookArt.Grid, .18f) : PaperColor.HasValue ? Color.white : new Color(.95f, .92f, .84f));
            // A single pale stroke stays quiet at the full-map overview. Overlapping
            // subpixel cores make alternating dark bands as the grid crosses pixels.
            Color minor = night ? MeshPen.Alpha(NightNotebookArt.Grid, .075f * minorGridOpacity) : new Color(.35f, .54f, .68f, .07f * minorGridOpacity);
            Color major = night ? MeshPen.Alpha(NightNotebookArt.Grid, .17f) : new Color(.30f, .48f, .65f, .18f);
            float majorWidth = p.WorldUnitsPerPixel > 0 ? .9f * p.WorldUnitsPerPixel : .14f;
            float minorWidth = p.WorldUnitsPerPixel > 0 ? .65f * p.WorldUnitsPerPixel : .12f;
            for (int x = -1; x <= paperWidth + 1; x++)
            {
                if (x % 5 != 0 && minorGridOpacity <= 0) continue;
                p.Line(new Vector2(x, -1.5f), new Vector2(x, paperHeight + 1.5f),
                       x % 5 == 0 ? majorWidth : minorWidth, x % 5 == 0 ? major : minor);
            }
            for (int y = -1; y <= paperHeight + 1; y++)
            {
                if (y % 5 != 0 && minorGridOpacity <= 0) continue;
                p.Line(new Vector2(-1.5f, y), new Vector2(paperWidth + 1.5f, y),
                       y % 5 == 0 ? majorWidth : minorWidth, y % 5 == 0 ? major : minor);
            }
            if (ModernEnemyPresentation && minorGridOpacity > 0)
            {
                // Fine notebook ruling is decorative paper texture. The full-unit
                // placement grid and every simulation coordinate retain their scale.
                var fine = night ? MeshPen.Alpha(NightNotebookArt.Grid, .05f * minorGridOpacity) : new Color(.35f, .54f, .68f, .105f * minorGridOpacity);
                for (int column = -3; column <= (paperWidth + 1) * 3; column++)
                    if (column % 3 != 0)
                        p.Line(new Vector2(column / 3f, -1.5f), new Vector2(column / 3f, paperHeight + 1.5f), minorWidth, fine);
                for (int row = -3; row <= (paperHeight + 1) * 3; row++)
                    if (row % 3 != 0)
                        p.Line(new Vector2(-1.5f, row / 3f), new Vector2(paperWidth + 1.5f, row / 3f), minorWidth, fine);
            }
            // Microtexture disappears before it becomes subpixel sparkle on the overview.
            for (int i = 0; minorGridOpacity > 0 && i < 700; i++)
            {
                float x = MeshPen.Hash(i * 79 + 13) * (paperWidth + 2) - 1;
                float y = MeshPen.Hash(i * 57 + 991) * (paperHeight + 2) - 1;
                p.Line(new Vector2(x, y), new Vector2(x + .045f + MeshPen.Hash(i) * .08f, y + .018f),
                       .015f, new Color(.55f, .47f, .32f, .045f * minorGridOpacity));
            }
            Color margin = night ? MeshPen.Alpha(NightNotebookArt.Accent, .42f) : new Color(.77f, .32f, .35f, .38f);
            p.Stroke(new Vector2(4, -1.4f), new Vector2(4, paperHeight + 1.4f), .045f, margin, 71, .025f);
            p.Stroke(new Vector2(4.18f, -1.4f), new Vector2(4.18f, paperHeight + 1.4f), .02f, MeshPen.Alpha(margin, .22f), 72);
            // Double-line map boundary reads as an ink frame drawn on graph paper.
            p.Box(new Vector2(paperWidth * .5f, paperHeight * .5f), new Vector2(paperWidth, paperHeight),
                  MeshPen.Alpha(NotebookPalette.Paper, 0), MeshPen.Alpha(navy, .32f), .047f, 123);
            p.Box(new Vector2(paperWidth * .5f + .08f, paperHeight * .5f - .08f), new Vector2(paperWidth - .26f, paperHeight - .26f),
                  MeshPen.Alpha(NotebookPalette.Paper, 0), MeshPen.Alpha(navy, .14f), .025f, 988);

            Color sketch = night ? MeshPen.Alpha(NightNotebookArt.Pencil, .30f) : new Color(.35f, .45f, .48f, .43f);
            for (int i = 0; session.Battlefield == null && i < 46; i++)
            {
                float x, y;
                if (i < 24)
                {
                    x = 6 + MeshPen.Hash(i * 13 + 77) * (paperWidth - 8);
                    y = i % 2 == 0 ? .8f + MeshPen.Hash(i + 5) * 2.1f : paperHeight - .8f - MeshPen.Hash(i + 5) * 2.1f;
                }
                else
                {
                    x = i % 2 == 0 ? 5.2f + MeshPen.Hash(i) * 1.8f : paperWidth - .8f - MeshPen.Hash(i) * 1.8f;
                    y = 4 + MeshPen.Hash(i * 11) * (paperHeight - 8);
                }
                float size = .55f + MeshPen.Hash(i * 19) * .7f;
                if (i % 4 != 0) DoodleGlyphs.Tree(p, new Vector2(x, y), size, sketch, i * 101);
                else DoodleGlyphs.Rock(p, new Vector2(x, y), size, sketch, i * 101);
            }
            // Notebook punched holes, beyond the red margin, have their own soft ink shadow.
            for (int i = 0; i < 5; i++)
            {
                Vector2 hole = new Vector2(1.65f, 4 + i * (paperHeight - 8) / 4f);
                p.Disc(hole + new Vector2(.055f, -.07f), .31f, MeshPen.Alpha(navy, .1f), 16);
                p.Disc(hole, .27f, new Color(.89f, .88f, .83f), 16);
                p.Ring(hole, .28f, .028f, MeshPen.Alpha(navy, .2f), 16, 1, i * 71);
            }
            // A ghost planning circle and compass doodle ground the empty starting page.
            Vector2 home = new Vector2(session.BaseCell.X + .5f, session.BaseCell.Y + .5f);
            if (!CompactBaseMarker) p.Ring(home, 6.5f, .04f, new Color(.24f, .42f, .67f, .16f), 120, 1, 7, true);
            Vector2 compass = new Vector2(paperWidth - 4, paperHeight - 4.5f);
            p.Ring(compass, .8f, .038f, sketch, 24, 1, 22);
            p.Line(compass + Vector2.down * 1.05f, compass + Vector2.up * 1.05f, .04f, sketch);
            p.Line(compass + Vector2.left * 1.05f, compass + Vector2.right * 1.05f, .04f, sketch);
            p.Triangle(compass + Vector2.up * .85f, compass + new Vector2(-.16f, -.14f), compass + new Vector2(.16f, -.14f), sketch);
            p.MinimumStrokePixels = terrainStrokePixels;
            // The immutable battlefield owns every playable terrain prop. Camera-relative
            // decoration would slide over buildable cells when panning or zooming.
            paperHordeSides = HordeWaveForecast.DisplayedSides(session);
            paperHordeBands = HordeWaveForecast.DisplayedBands(session);
            NotebookBattlefieldRenderer.Draw(p, session.Battlefield, Theme, night, paperHordeSides, paperHordeBands);
            p.Upload();
        }

        void DrawSurroundingScenery(MeshPen pen)
        {
            if (session.Battlefield == null) return;
            var style = session.Battlefield.Style;
            bool cliffs = style == CampaignTerrainStyle.Crossroads || style == CampaignTerrainStyle.StoneGate ||
                style == CampaignTerrainStyle.Fold || style == CampaignTerrainStyle.NorthPass;
            bool orchard = style == CampaignTerrainStyle.FourFields || style == CampaignTerrainStyle.Redoubt;
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1 : 1;
                float edge = side == 0 ? -8 : paperWidth + 8;
                // Scenery frames the playable sheet; it must not compete with enemies and towers.
                for (int band = 0; band < 2; band++)
                for (int row = 0; row < 5; row++)
                {
                    int seed = side * 313 + band * 47 + row + (int)style * 143;
                    var at = new Vector2(edge + sign * (band * 14 + MeshPen.Hash(seed + 17) * 5),
                        -3 + row * 16 + MeshPen.Hash(seed + 99) * 6);
                    bool mountain = band > 0 && row % 3 == 0;
                    string id = mountain ? (cliffs ? "terrain.ridge" : "terrain.mountain") :
                        cliffs && row % 2 == 0 ? "terrain.rock" : orchard ? "terrain.oak" : "terrain.pine";
                    DoodleGlyphs.DrawResource(pen, id, at,
                        mountain ? 7 : 3.5f + MeshPen.Hash(seed + 11) * 2.5f, opacity: .20f, theme: Theme);
                    if (!mountain && row % 2 == 0)
                        DoodleGlyphs.DrawResource(pen, "terrain.rock", at + new Vector2(sign * 4, -3), 5,
                            opacity: .16f, theme: Theme);
                }
            }
        }

        void DrawStructureRanges(MeshPen p)
        {
            for (int i = 0; i < session.Structures.Count; i++)
            {
                var structure = session.Structures[i];
                if (structure.Cell == inspectedSector) continue;
                DrawCoverage(p, structure, RangeInk(structure.Tier), false);
            }
        }

        Color RangeInk(MaterialTier tier) => DarkSurface ? NightNotebookArt.Pencil : DoodleGlyphs.TierColor(tier);

        void DrawCoverage(MeshPen p, StructureView tower, Color ink, bool highlighted)
        {
            DrawCoverage(p, tower.Cell, tower.Kind, tower.ModernizationEntry ?? tower.ResolvedEntry, tower.Blueprint,
                tower.FacingDegrees, ink, highlighted, tower.ContentId);
        }

        void DrawCoverage(MeshPen p, Cell cell, StructureKind kind, ResolvedEntry entry, TowerBlueprint blueprint,
            float facing, Color ink, bool highlighted, string directId = null, float fallbackRange = -1)
        {
            Vector2 origin = new Vector2(cell.X + .5f, cell.Y + .5f);
            var balance = session.Balance;
            if (entry != null)
            {
                foreach (var weapon in entry.Weapons)
                {
                    if (weapon.Attack == AttackKind.Radial) DrawSupportRadius(p, origin, weapon.Range, ink, highlighted);
                    else DrawWeaponSector(p, origin, facing, weapon.FiringArcDegrees, weapon.MinimumRange, weapon.Range, ink, highlighted);
                }
                DrawSupportRadius(p, origin, entry.Support?.Radius ?? 0, ink, highlighted);
                return;
            }
            if (blueprint != null && blueprint.WeaponMounts.Count > 0)
            {
                foreach (var mount in blueprint.WeaponMounts)
                {
                    var definition = balance.Content.GetWeapon(mount.WeaponId);
                    float range = balance.WeaponRange(blueprint, mount.WeaponId, mount.SlotIndex);
                    if (definition.Attack == AttackKind.Radial)
                        DrawSupportRadius(p, origin, range, ink, highlighted);
                    else
                        DrawWeaponSector(p, origin, facing, definition.FiringArcDegrees, definition.MinimumRange, range, ink, highlighted);
                }
                return;
            }
            if (!balance.HasStructure(kind)) return;
            if (ConstructionToolRules.IsTool(kind))
            {
                var spec = balance.Structure(kind);
                float radius = kind == StructureKind.RepairPost ? spec.Range : kind == StructureKind.TarPit ? Mathf.Max(.5f, spec.Radius) : 0;
                if (kind == StructureKind.Spikes && balance.Content.TryGetWeapon(LegacyContentIds.Weapon(kind), out var trap))
                    radius = Mathf.Max(trap.MinimumRadialRange, trap.Stats.Radius);
                DrawSupportRadius(p, origin, radius, ink, highlighted);
                return;
            }
            string weaponId = directId ?? LegacyContentIds.Weapon(kind);
            if (balance.Content.TryGetWeapon(weaponId, out var direct))
                DrawWeaponSector(p, origin, facing, direct.FiringArcDegrees, direct.MinimumRange,
                    fallbackRange >= 0 ? fallbackRange : balance.WeaponRange(null, weaponId), ink, highlighted);
            else
                DrawWeaponSector(p, origin, facing, WeaponDefinition.DefaultFiringArc(LegacyContentCatalog.Attack(kind), kind == StructureKind.Stapler),
                    0, fallbackRange >= 0 ? fallbackRange : balance.Structure(kind).Range, ink, highlighted);
        }

        static void DrawSupportRadius(MeshPen p, Vector2 origin, float radius, Color ink, bool highlighted)
        {
            if (radius > 0) p.Ring(origin, radius, .04f, MeshPen.Alpha(ink, highlighted ? .72f : .27f), 96, dashed: true);
        }

        // One annular sector per mount. The hollow band follows the real minimum range of a mortar.
        internal static void DrawWeaponSector(MeshPen p, Vector2 origin, float facing, float arc, float minimum, float outer,
            Color ink, bool highlighted)
        {
            if (outer <= 0 || minimum >= outer) return;
            int segments = Mathf.CeilToInt(arc / 4);
            float start = (facing - arc * .5f) * Mathf.Deg2Rad, step = arc * Mathf.Deg2Rad / segments;
            var direction = new Vector2(Mathf.Cos(start), Mathf.Sin(start));
            var firstInner = origin + direction * minimum;
            var firstOuter = origin + direction * outer;
            var previousInner = firstInner; var previousOuter = firstOuter;
            var edge = MeshPen.Alpha(ink, highlighted ? .86f : .38f);
            var deadZone = MeshPen.Alpha(NotebookPalette.Red, highlighted ? .70f : .32f);
            var fill = MeshPen.Alpha(ink, .075f);
            float width = highlighted ? .055f : .035f;
            p.Line(firstInner, firstOuter, width, edge);
            for (int i = 1; i <= segments; i++)
            {
                float radians = start + step * i;
                direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                Vector2 inner = origin + direction * minimum, end = origin + direction * outer;
                if (highlighted)
                {
                    if (minimum > 0) p.Quad(previousInner, previousOuter, end, inner, fill);
                    else p.Triangle(origin, previousOuter, end, fill);
                }
                if (highlighted || i % 3 != 0) p.Line(previousOuter, end, width, edge);
                if (minimum > 0 && i % 3 != 0) p.Line(previousInner, inner, width, deadZone);
                previousInner = inner; previousOuter = end;
            }
            p.Line(previousInner, previousOuter, width, edge);
            if (highlighted)
            {
                float bearing = facing * Mathf.Deg2Rad;
                var forward = new Vector2(Mathf.Cos(bearing), Mathf.Sin(bearing));
                var tangent = new Vector2(-forward.y, forward.x);
                float arm = Mathf.Min(.65f, outer * .18f);
                var tip = origin + forward * (outer - .1f);
                p.Line(tip - (forward + tangent * .59f) * arm, tip, width, edge);
                p.Line(tip - (forward - tangent * .59f) * arm, tip, width, edge);
            }
        }

        void DrawOverviewMarker(MeshPen pen, StructureView structure)
        {
            if (structure.Blueprint == null && structure.Kind != StructureKind.Gun
                && structure.Kind != StructureKind.Piercer && structure.Kind != StructureKind.Mortar) return;
            float diameter = Mathf.Max(0, towerMarkerPixels) * pen.WorldUnitsPerPixel;
            float opacity = Mathf.Clamp01((diameter - 1) / .7f) * (structure.IsComplete ? 1 : .45f);
            if (opacity <= 0) return;
            var at = new Vector2(structure.Cell.X + .5f, structure.Cell.Y + .5f);
            // The old opaque disc covered the actual body on a full-page view.
            // Quiet open corner marks locate it without replacing its silhouette.
            float half=diameter*.5f, arm=diameter*.18f;
            Color ink=MeshPen.Alpha(NightNotebookArt.Grid,opacity*.28f);
            for(int corner=0;corner<4;corner++)
            {
                float x=corner<2?-half:half,y=corner%2==0?-half:half;
                var point=at+new Vector2(x,y);
                pen.Line(point,point+new Vector2(x<0?arm:-arm,0),pen.WorldUnitsPerPixel*.7f,ink);
                pen.Line(point,point+new Vector2(0,y<0?arm:-arm),pen.WorldUnitsPerPixel*.7f,ink);
            }
        }

        void DrawStructures()
        {
            MeshPen p = structurePen;
            p.Clear();
            if (ShowRanges) DrawStructureRanges(p);
            wallCells.Clear();
            for (int i = 0; i < session.Structures.Count; i++)
            {
                DrawOverviewMarker(p, session.Structures[i]);
                if (session.Structures[i].Kind == StructureKind.Wall && session.Structures[i].IsComplete)
                    wallCells.Add(session.Structures[i].Cell);
            }
            for (int i = 0; i < session.Structures.Count; i++)
            {
                var structure = session.Structures[i]; Cell cell = structure.Cell;
                int mask = (wallCells.Contains(new Cell(cell.X, cell.Y + 1)) ? 1 : 0)
                    | (wallCells.Contains(new Cell(cell.X + 1, cell.Y)) ? 2 : 0)
                    | (wallCells.Contains(new Cell(cell.X, cell.Y - 1)) ? 4 : 0)
                    | (wallCells.Contains(new Cell(cell.X - 1, cell.Y)) ? 8 : 0);
                DoodleGlyphs.StructureBase(p, structure, Theme, mask);
            }
            Vector2 home = new Vector2(session.BaseCell.X + .5f, session.BaseCell.Y + .5f);
            if (CompactBaseMarker)
            {
                // Prepared targets may share the base's neighbourhood. Keep the base cell visible
                // without covering those actors with the three-cell decorative castle.
                p.Ring(home, .38f, .025f, MeshPen.Alpha(NotebookPalette.Blue, .35f), 20);
            }
            else DoodleGlyphs.Castle(p, home, session.BaseHealth / session.Balance.BaseHealth, (float)visualClock.Seconds, Theme, castleVisualScale);
            if (session.Phase == RunPhase.Defeat)
            {
                p.Line(home + new Vector2(-1.9f, -1.7f) * castleVisualScale, home + new Vector2(1.9f, 1.7f) * castleVisualScale, .13f, MeshPen.Alpha(NotebookPalette.Red, .7f));
                p.Line(home + new Vector2(-1.9f, 1.7f) * castleVisualScale, home + new Vector2(1.9f, -1.7f) * castleVisualScale, .13f, MeshPen.Alpha(NotebookPalette.Red, .7f));
            }
            p.Upload();
            StaticGeometryRefreshCount++;
            visualVersion = session.StructureVisualVersion;
            topologyVersion = session.TopologyVersion;
            castleHealthBand = CastleHealthBand();
            renderedPhase = session.Phase;
            staticDirty = false;
        }

        int CastleHealthBand()
        {
            float health = session.BaseHealth / Mathf.Max(1, session.Balance.BaseHealth);
            return Mathf.CeilToInt(4 * health) + (health < .6f ? 10 : 0);
        }

        void DrawWorld()
        {
            MeshPen p = worldPen;
            p.Clear();
            for (int i = 0; i < session.Wells.Count; i++) DrawWell(p, session.Wells[i]);
            for (int i = 0; i < session.Structures.Count; i++)
            {
                var structure = session.Structures[i];
                float duration = structure.ResolvedEntry?.BuildSeconds ?? (structure.Blueprint != null ? session.Balance.BuildSeconds(structure.Blueprint, structure.Tier)
                    : session.Balance.Structure(structure.Kind).BuildSeconds * session.Balance.Material(structure.Tier).BuildTimeMultiplier);
                DoodleGlyphs.StructureDynamic(p, structure, duration, visualClock.Seconds, animation, Theme);
            }
            float time = (float)visualClock.Seconds;
            float enemyScale = NotebookEnemyView.ScaleFor(ModernEnemyPresentation);
            bool cull = WorldCamera != null && WorldCamera.orthographic;
            Vector3 cameraPosition = cull ? WorldCamera.transform.position : Vector3.zero;
            float halfHeight = cull ? WorldCamera.orthographicSize : 0;
            float halfWidth = cull ? WorldCamera.orthographicSize * WorldCamera.aspect : 0;
            int wave = session.WaveIndex - 1;
            bool horde = ModernEnemyPresentation && wave >= 0 && wave < session.Balance.Waves.Count && session.Balance.Waves[wave].IsHorde;
            var hordeView = !horde ? HordeView.None : cull && halfHeight * 2 >= session.Balance.Height / 3f ? HordeView.Overview : HordeView.Close;
            bool compactSwarm = session.Enemies.Count > 1000 || (horde && session.Enemies.Count >= 160);
            var theme = Theme;
            for (int i = 0; i < session.Enemies.Count; i++)
            {
                EnemyView enemy = session.Enemies[i];
                // Include scaled art/status marks and the HP bar above the silhouette.
                float margin = DoodleGlyphs.VisualScale(enemy.VisualScale) * enemyScale + .5f;
                if (cull && (Mathf.Abs(enemy.Position.X - cameraPosition.x) > halfWidth + margin
                          || Mathf.Abs(enemy.Position.Y - cameraPosition.y) > halfHeight + margin)) continue;
                DoodleGlyphs.Enemy(p, enemy, time, compactSwarm, animation, theme,
                    windupSeconds: enemy.WindupRemaining > 0 ? session.Balance.Enemy(enemy.Kind).WindupSeconds : 0,
                    showHealth: ShowEnemyHealthBars, uniformScale: enemyScale, horde: hordeView);
            }
            foreach (var death in animation.Deaths)
            {
                float margin = DoodleGlyphs.VisualScale(death.VisualScale) * enemyScale + .5f;
                if (cull && (Mathf.Abs(death.Position.x - cameraPosition.x) > halfWidth + margin
                    || Mathf.Abs(death.Position.y - cameraPosition.y) > halfHeight + margin)) continue;
                DoodleGlyphs.DrawResourceEnemy(p, death.Kind, death.Tier, death.Position, NotebookEnemyMotion.Death,
                    (float)(visualClock.Seconds - death.Born), Theme, death.VisualScale, enemyScale);
            }
            p.Upload();
        }
        void DrawWell(MeshPen p, InkWellView well)
        {
            Vector2 at = new Vector2(well.Cell.X + .5f, well.Cell.Y + .5f);
            Color color = DoodleGlyphs.InkTone(well.Color);
            p.Disc(at, 1.1f, MeshPen.Alpha(color, well.Active ? .13f : .06f), 24, .85f);
            string id = well.Color == InkColor.Red ? "object.well.red" : well.Color == InkColor.Blue ? "object.well.blue" : "object.well.green";
            DoodleGlyphs.DrawResource(p, well.Active ? id + ".active" : id, at, scale: 1.4f,
                opacity: well.Health > 0 ? 1 : .35f, theme: Theme);
            p.Line(at + new Vector2(-.55f, -.64f), at + new Vector2(.55f, -.64f), .07f, MeshPen.Alpha(NotebookPalette.Navy, .2f));
            p.Line(at + new Vector2(-.55f, -.64f), at + new Vector2(-.55f + 1.1f * Mathf.Clamp01(well.Health / Mathf.Max(1, well.MaxHealth)), -.64f), .07f, color);
            if (well.Threatened)
            {
                float pulse = .55f + .45f * Mathf.Sin((float)visualClock.Seconds * 8);
                p.Ring(at, 1.35f, .08f, MeshPen.Alpha(NotebookPalette.Red, pulse), 24, 1, well.Id, true);
                p.Star(at + Vector2.up, .3f, NotebookPalette.Red, .06f, 5);
            }
        }

        void DrawEffects()
        {
            MeshPen p = effectsPen;
            p.Clear();
            bool darkSurface = DarkSurface;
            var theme = Theme;
            Color trailInk = NightNotebookEffectPalette.Tone(NotebookPalette.Marker, NightNotebookArt.Accent, theme, darkSurface);
            for (int i = 0; i < session.Blots.Count; i++)
            {
                var blot = session.Blots[i]; Vector2 center = new Vector2(blot.Cell.X + .5f, blot.Cell.Y + .5f);
                float fade = Mathf.Clamp01(blot.Remaining / Mathf.Max(.01f, session.Balance.BlotLifetime));
                NightNotebookEffectPalette.DrawResource(p, "object.blot", center, theme, darkSurface, opacity: .3f + fade * .7f);
            }
            for (int i = 0; i < session.EnemyProjectiles.Count; i++)
            {
                var glob = session.EnemyProjectiles[i];
                Vector2 from = new Vector2(glob.From.X, glob.From.Y), to = new Vector2(glob.To.X, glob.To.Y);
                float progress = Mathf.Clamp01(1 - glob.Remaining / Mathf.Max(.001f, glob.Duration));
                Vector2 point = Vector2.Lerp(from, to, progress);
                p.Line(Vector2.Lerp(from, to, Mathf.Max(0, progress - .12f)), point, .09f, MeshPen.Alpha(trailInk, .55f));
                NightNotebookEffectPalette.DrawResource(p, "fx.spitter.projectile", point, theme, darkSurface);
                p.Ring(to, .36f, .035f, MeshPen.Alpha(NotebookPalette.Red, .5f), 12, 1, glob.Id, true);
            }
            for (int i = 0; i < session.Projectiles.Count; i++)
            {
                var projectile = session.Projectiles[i];
                Vector2 from = new Vector2(projectile.From.X, projectile.From.Y);
                Vector2 to = new Vector2(projectile.To.X, projectile.To.Y);
                from = VisualMuzzle(projectile.SourceStructureId, projectile.MountSlotIndex, projectile.To, from);
                float flight = Mathf.Clamp01(1 - projectile.Remaining / Mathf.Max(.001f, projectile.Duration));
                NotebookEffectRenderer.DrawProjectile(p, from, to, flight, projectile.ImpactFootprint,
                    projectile.Radius, projectile.Id, projectile.ColorEffect.Color, theme, darkSurface);
            }
            double time = visualClock.Seconds;
            for (int i = firingMarks.Count - 1; i >= 0; i--)
            {
                var mark = firingMarks[i];
                float age = (float)(time - mark.SimulationSeconds);
                if (age >= NotebookEffectRenderer.Duration(mark, Theme)) { firingMarks.RemoveAt(i); continue; }
                Vector2 muzzle = VisualMuzzle(mark.SourceStructureId, mark.MountSlotIndex, mark.To, new Vector2(mark.From.X, mark.From.Y));
                NotebookEffectRenderer.Draw(p, mark, muzzle, age, theme, darkSurface);
            }
            RefreshReadability();
            if (ModernEnemyPresentation) Readability.Draw(p, theme);
            p.Upload();
        }

        void RefreshReadability()
        {
            if (WorldCamera == null || !WorldCamera.orthographic || WorldCamera.pixelHeight <= 0)
            { Readability.Refresh(session, default, 0); return; }
            float halfHeight = WorldCamera.orthographicSize;
            float halfWidth = halfHeight * WorldCamera.aspect;
            Vector3 center = WorldCamera.transform.position;
            Readability.Refresh(session, new Rect(center.x - halfWidth, center.y - halfHeight, halfWidth * 2, halfHeight * 2),
                halfHeight * 2 / WorldCamera.pixelHeight);
        }

        Vector2 VisualMuzzle(int structureId, int slot, Point2 aim, Vector2 fallback)
        {
            if (slot < 0) return fallback;
            for (int i = 0; i < session.Structures.Count; i++)
                if (session.Structures[i].Id == structureId)
                {
                    var structure = session.Structures[i];
                    return DoodleGlyphs.SegmentOrigin(structure, slot, aim, fallback, visualClock.Seconds, animation, Theme);
                }
            return fallback;
        }

        void DrawPreview()
        {
            MeshPen p = previewPen;
            p.Clear();
            Color color = previewValid ? DoodleGlyphs.TierColor(previewTier) : NotebookPalette.Red;
            float opacity = previewFacing.HasValue ? 1 : .65f;
            float heading = 0;
            for (int i = 0; i < previewCells.Count; i++)
            {
                Cell cell = previewCells[i];
                Vector2 center = new Vector2(cell.X + .5f, cell.Y + .5f);
                p.Box(center, new Vector2(.91f, .91f), MeshPen.Alpha(color, .15f), MeshPen.Alpha(color, .8f), .056f, cell.GetHashCode());
                if (previewValid)
                {
                    heading = NotebookMountLayout.HasDirectionalMount(previewKind, previewEntry, previewBlueprint, session.Balance.Content)
                        ? previewFacing ?? session.DefaultFacingDegrees(cell, previewKind, previewEntry, previewBlueprint) : 90;
                    float radians = NotebookMountLayout.BodyRadians(heading);
                    if (previewRecipe != null)
                        AssemblyVisualComposer.ComposePlaced(previewRecipe, null, radians).Draw(p, center, opacity,
                            uniformScale: NotebookAssemblyView.ScaleFor(previewRecipe));
                    else if (NotebookTowerComposition.UsesComposition(previewBlueprint))
                    {
                        NotebookTowerComposition.DrawStatic(p, previewBlueprint, center, previewTier, InkColor.None, opacity, Theme, radians);
                        NotebookTowerComposition.DrawMounts(p, previewBlueprint, center, previewTier, InkColor.None,
                            default, false, null, 0, 0, opacity, Theme, previewHeading: heading);
                    }
                    else if (ConstructionToolRules.IsTool(previewKind))
                        DoodleGlyphs.DrawResource(p,
                            previewKind == StructureKind.TarPit ? "object.tar" : previewKind == StructureKind.RepairPost ? "object.repair"
                            : previewKind == StructureKind.Spikes ? "object.spikes" : "object.wall.0", center, opacity: .65f, theme: Theme);
                    else
                    {
                        DoodleGlyphs.DrawResource(p, "chassis.standard", center, radians: radians, opacity: opacity, theme: Theme);
                        DoodleGlyphs.DrawResource(p, LegacyContentIds.Weapon(previewKind), center + NotebookMountLayout.Rotate(Vector2.up * .04f, radians),
                            radians: radians, opacity: opacity, theme: Theme);
                    }
                }
                else
                {
                    p.Line(center + new Vector2(-.18f, -.18f), center + new Vector2(.18f, .18f), .05f, color);
                    p.Line(center + new Vector2(-.18f, .18f), center + new Vector2(.18f, -.18f), .05f, color);
                }
            }
            var inspected = inspectedSector.HasValue ? session.StructureAt(inspectedSector.Value) : null;
            if (inspected.HasValue)
            {
                var structure = inspected.Value;
                DrawCoverage(p, structure, DarkSurface ? FieldMenuSkin.Accent : NotebookPalette.Blue, true);
            }
            else if (previewValid && previewCells.Count == 1)
            {
                var cell = previewCells[0];
                DrawCoverage(p, cell, previewKind, previewEntry, previewBlueprint, heading,
                    previewFacing.HasValue && DarkSurface ? FieldMenuSkin.Accent : RangeInk(previewTier), true, fallbackRange: previewRange);
            }
            p.Upload();
            PreviewGeometryRefreshCount++;
        }

        void OnDestroy()
        {
            foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
            if (inkMaterial != null) Destroy(inkMaterial);
        }
    }
}
