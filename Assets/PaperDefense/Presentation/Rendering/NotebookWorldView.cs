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
        public double VisualSeconds => visualClock.Seconds;
        public NotebookAnimationState AnimationState => animation;
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
        NotebookViewSettingsAsset Settings => ViewSettings != null ? ViewSettings : NotebookViewSettingsAsset.Default;
        float minorGridOpacity = 1, terrainStrokePixels = 1, towerMarkerPixels;
        StructureKind previewKind;
        MaterialTier previewTier;
        bool previewValid;
        float previewRange = -1;
        TowerBlueprint previewBlueprint;
        int paperWidth, paperHeight;
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
            firingMarks.Clear();
            animation.Clear();
            visualClock.Reset(session?.Elapsed ?? 0);
            DroppedVisualEffects = 0;
            previewCells.Clear();
            previewBlueprint = null;
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
            TowerBlueprint blueprint = null)
        {
            int count = cells == null ? 0 : cells.Count;
            bool changed = previewKind != kind || previewTier != tier || previewValid != valid || previewCells.Count != count || previewRange != range
                || !ReferenceEquals(previewBlueprint, blueprint);
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
            if (previewPen != null && session != null) DrawPreview();
        }

        public void ClearPreview()
        {
            if (previewCells.Count == 0) return;
            previewCells.Clear();
            if (previewPen != null) { previewPen.Clear(); previewPen.Upload(); PreviewGeometryRefreshCount++; }
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
            if (UpdateViewScale())
            {
                DrawPaper();
                if (previewCells.Count > 0) DrawPreview();
                staticDirty = true;
            }
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
            Color navy = NotebookPalette.Navy;
            // The subtle page shadow and warm edge make the world feel like an actual sheet.
            p.Rect(-2.5f, -2.5f, paperWidth + 5, paperHeight + 5, PaperColor.HasValue ? new Color32(220, 229, 242, 255) : new Color(.89f, .88f, .82f));
            p.Rect(-1.75f, -1.75f, paperWidth + 3.5f, paperHeight + 3.5f, PaperColor ?? NotebookPalette.Paper);
            p.Rect(-1.7f, -1.7f, .22f, paperHeight + 3.4f, PaperColor.HasValue ? Color.white : new Color(.95f, .92f, .84f));
            // A single pale stroke stays quiet at the full-map overview. Overlapping
            // subpixel cores make alternating dark bands as the grid crosses pixels.
            Color minor = new Color(.35f, .54f, .68f, .07f * minorGridOpacity);
            Color major = new Color(.30f, .48f, .65f, .18f);
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
            // Microtexture disappears before it becomes subpixel sparkle on the overview.
            for (int i = 0; minorGridOpacity > 0 && i < 700; i++)
            {
                float x = MeshPen.Hash(i * 79 + 13) * (paperWidth + 2) - 1;
                float y = MeshPen.Hash(i * 57 + 991) * (paperHeight + 2) - 1;
                p.Line(new Vector2(x, y), new Vector2(x + .045f + MeshPen.Hash(i) * .08f, y + .018f),
                       .015f, new Color(.55f, .47f, .32f, .045f * minorGridOpacity));
            }
            Color margin = new Color(.77f, .32f, .35f, .38f);
            p.Stroke(new Vector2(4, -1.4f), new Vector2(4, paperHeight + 1.4f), .045f, margin, 71, .025f);
            p.Stroke(new Vector2(4.18f, -1.4f), new Vector2(4.18f, paperHeight + 1.4f), .02f, MeshPen.Alpha(margin, .22f), 72);
            // Double-line map boundary reads as an ink frame drawn on graph paper.
            p.Box(new Vector2(paperWidth * .5f, paperHeight * .5f), new Vector2(paperWidth, paperHeight),
                  MeshPen.Alpha(NotebookPalette.Paper, 0), MeshPen.Alpha(navy, .32f), .047f, 123);
            p.Box(new Vector2(paperWidth * .5f + .08f, paperHeight * .5f - .08f), new Vector2(paperWidth - .26f, paperHeight - .26f),
                  MeshPen.Alpha(NotebookPalette.Paper, 0), MeshPen.Alpha(navy, .14f), .025f, 988);

            Color sketch = new Color(.35f, .45f, .48f, .43f);
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
            NotebookBattlefieldRenderer.Draw(p, session.Battlefield, Theme);
            p.Upload();
        }

        void DrawStructureRanges(MeshPen p)
        {
            for (int i = 0; i < session.Structures.Count; i++)
            {
                StructureView structure = session.Structures[i];
                var origin = new Vector2(structure.Cell.X + .5f, structure.Cell.Y + .5f);
                if (structure.Blueprint == null)
                {
                    float range = session.Balance.WeaponRange(null, structure.Kind);
                    if (range > 1) p.Ring(origin, range, .035f,
                        MeshPen.Alpha(DoodleGlyphs.TierColor(structure.Tier), .16f), 96, 1, structure.Id, true);
                    continue;
                }
                foreach (var mount in structure.Blueprint.WeaponMounts)
                {
                    float range = session.Balance.WeaponRange(structure.Blueprint, mount.WeaponId, mount.SlotIndex);
                    float minimum = session.Balance.WeaponMinimumRange(structure.Blueprint, mount.WeaponId, mount.SlotIndex);
                    if (range > 1) p.Ring(origin, range, .035f,
                        MeshPen.Alpha(DoodleGlyphs.TierColor(structure.Tier), .16f), 96, 1, structure.Id + mount.SlotIndex, true);
                    if (minimum > 0) p.Ring(origin, minimum, .055f,
                        MeshPen.Alpha(NotebookPalette.Red, .35f), 72, 1, structure.Id + mount.SlotIndex, true);
                }
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
            pen.Disc(at, diameter * .5f, MeshPen.Alpha(NotebookPalette.Paper, opacity * .92f), 20);
            pen.Ring(at, diameter * .5f, pen.WorldUnitsPerPixel, MeshPen.Alpha(NotebookPalette.Blue, opacity * .75f), 20);
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
                if (session.Structures[i].Kind == StructureKind.Wall) wallCells.Add(session.Structures[i].Cell);
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
            else DoodleGlyphs.Castle(p, home, session.BaseHealth / session.Balance.BaseHealth, (float)visualClock.Seconds, Theme);
            if (session.Phase == RunPhase.Defeat)
            {
                p.Line(home + new Vector2(-1.9f, -1.7f), home + new Vector2(1.9f, 1.7f), .13f, MeshPen.Alpha(NotebookPalette.Red, .7f));
                p.Line(home + new Vector2(-1.9f, 1.7f), home + new Vector2(1.9f, -1.7f), .13f, MeshPen.Alpha(NotebookPalette.Red, .7f));
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
                float duration = structure.Blueprint != null ? session.Balance.BuildSeconds(structure.Blueprint, structure.Tier)
                    : session.Balance.Structure(structure.Kind).BuildSeconds * session.Balance.Material(structure.Tier).BuildTimeMultiplier;
                DoodleGlyphs.StructureDynamic(p, structure, duration, visualClock.Seconds, animation, Theme);
            }
            float time = (float)visualClock.Seconds;
            bool compactSwarm = session.Enemies.Count > 1000;
            bool cull = WorldCamera != null && WorldCamera.orthographic;
            Vector3 cameraPosition = cull ? WorldCamera.transform.position : Vector3.zero;
            float halfHeight = cull ? WorldCamera.orthographicSize : 0;
            float halfWidth = cull ? WorldCamera.orthographicSize * WorldCamera.aspect : 0;
            for (int i = 0; i < session.Enemies.Count; i++)
            {
                EnemyView enemy = session.Enemies[i];
                // Include scaled art/status marks and the HP bar above the silhouette.
                float margin = DoodleGlyphs.VisualScale(enemy.VisualScale) + .5f;
                if (cull && (Mathf.Abs(enemy.Position.X - cameraPosition.x) > halfWidth + margin
                          || Mathf.Abs(enemy.Position.Y - cameraPosition.y) > halfHeight + margin)) continue;
                DoodleGlyphs.Enemy(p, enemy, time, compactSwarm, animation, Theme, windupSeconds: session.Balance.Enemy(enemy.Kind).WindupSeconds,
                    showHealth: ShowEnemyHealthBars);
            }
            foreach (var death in animation.Deaths)
            {
                float margin = DoodleGlyphs.VisualScale(death.VisualScale) + .5f;
                if (cull && (Mathf.Abs(death.Position.x - cameraPosition.x) > halfWidth + margin
                    || Mathf.Abs(death.Position.y - cameraPosition.y) > halfHeight + margin)) continue;
                DoodleGlyphs.DrawResourceEnemy(p, death.Kind, death.Tier, death.Position, NotebookEnemyMotion.Death,
                    (float)(visualClock.Seconds - death.Born), Theme, death.VisualScale);
            }
            p.Upload();
        }
        void DrawWell(MeshPen p, InkWellView well)
        {
            Vector2 at = new Vector2(well.Cell.X + .5f, well.Cell.Y + .5f);
            Color color = DoodleGlyphs.InkTone(well.Color);
            Color ink = well.Active ? color : MeshPen.Alpha(color, .42f);
            p.Disc(at, 1.1f, MeshPen.Alpha(color, well.Active ? .13f : .06f), 24, .85f);
            string id = well.Color == InkColor.Red ? "object.well.red" : well.Color == InkColor.Blue ? "object.well.blue" : "object.well.green";
            // Resource art uses normalized icon coordinates; match the fallback bottle size.
            if (!NotebookArtRenderer.Draw(p, Theme?.FindShape(well.Active ? id + ".active" : id), at, scale: 1.4f, opacity: well.Health > 0 ? 1 : .35f, theme: Theme))
            {
                p.Box(at, new Vector2(.95f, .85f), NotebookPalette.Paper, NotebookPalette.Navy, .075f, well.Id);
                p.Disc(at, .36f, ink, 16, .73f);
                p.Ring(at + new Vector2(0, .33f), .35f, .07f, color, 16, .5f, well.Id);
            }
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
            for (int i = 0; i < session.Blots.Count; i++)
            {
                var blot = session.Blots[i]; Vector2 center = new Vector2(blot.Cell.X + .5f, blot.Cell.Y + .5f);
                float fade = Mathf.Clamp01(blot.Remaining / Mathf.Max(.01f, session.Balance.BlotLifetime));
                Color color = MeshPen.Alpha(NotebookPalette.Marker, .12f + fade * .38f);
                if (!NotebookArtRenderer.Draw(p, Theme?.FindShape("object.blot"), center, opacity: .3f + fade * .7f, theme: Theme))
                {
                    p.Disc(center, .42f, color, 9, .65f);
                    p.Disc(center + new Vector2(.21f, .08f), .2f, color, 7, .7f);
                    p.Disc(center + new Vector2(-.23f, -.12f), .14f, color, 7);
                }
            }
            for (int i = 0; i < session.EnemyProjectiles.Count; i++)
            {
                var glob = session.EnemyProjectiles[i];
                Vector2 from = new Vector2(glob.From.X, glob.From.Y), to = new Vector2(glob.To.X, glob.To.Y);
                float progress = Mathf.Clamp01(1 - glob.Remaining / Mathf.Max(.001f, glob.Duration));
                Vector2 point = Vector2.Lerp(from, to, progress);
                p.Line(Vector2.Lerp(from, to, Mathf.Max(0, progress - .12f)), point, .09f, MeshPen.Alpha(NotebookPalette.Marker, .55f));
                if (!NotebookArtRenderer.Draw(p, Theme?.FindShape("fx.spitter.projectile"), point, theme: Theme))
                { p.Disc(point, .14f, NotebookPalette.Navy, 8); p.Disc(point, .09f, NotebookPalette.Marker, 7); }
                p.Ring(to, .36f, .035f, MeshPen.Alpha(NotebookPalette.Red, .5f), 12, 1, glob.Id, true);
            }
            for (int i = 0; i < session.Projectiles.Count; i++)
            {
                var projectile = session.Projectiles[i];
                Vector2 from = new Vector2(projectile.From.X, projectile.From.Y);
                Vector2 to = new Vector2(projectile.To.X, projectile.To.Y);
                from = VisualMuzzle(projectile.SourceStructureId, projectile.MountSlotIndex, projectile.To, from);
                float flight = Mathf.Clamp01(1 - projectile.Remaining / Mathf.Max(.001f, projectile.Duration));
                float height = Mathf.Min(3, Vector2.Distance(from, to) * .22f);
                Vector2 point = FlightPoint(from, to, flight, height);
                Vector2 trail = FlightPoint(from, to, Mathf.Max(0, flight - .1f), height);
                p.Line(trail, point, .045f, MeshPen.Alpha(NotebookPalette.Marker, .6f));
                if (!NotebookArtRenderer.Draw(p, Theme?.FindShape("fx.mortar.projectile"), point, theme: Theme))
                {
                    p.Disc(point, .1f, NotebookPalette.Navy, 10);
                    p.Disc(point + new Vector2(-.025f, .025f), .035f, NotebookPalette.Paper, 6);
                }
                NotebookEffectRenderer.DrawFootprint(p, to, projectile.ImpactFootprint, projectile.Radius, 1, .025f,
                    MeshPen.Alpha(NotebookPalette.Marker, .2f), 24, projectile.Id, true);
            }
            double time = visualClock.Seconds;
            for (int i = firingMarks.Count - 1; i >= 0; i--)
            {
                var mark = firingMarks[i];
                float age = (float)(time - mark.SimulationSeconds);
                if (age >= NotebookEffectRenderer.Duration(mark, Theme)) { firingMarks.RemoveAt(i); continue; }
                Vector2 muzzle = VisualMuzzle(mark.SourceStructureId, mark.MountSlotIndex, mark.To, new Vector2(mark.From.X, mark.From.Y));
                NotebookEffectRenderer.Draw(p, mark, muzzle, age, Theme);
            }
            p.Upload();
        }

        Vector2 VisualMuzzle(int structureId, int slot, Point2 aim, Vector2 fallback)
        {
            if (slot < 0) return fallback;
            for (int i = 0; i < session.Structures.Count; i++)
                if (session.Structures[i].Id == structureId)
                    return NotebookTowerComposition.SegmentOrigin(session.Structures[i], slot, aim, fallback, visualClock.Seconds, animation, Theme);
            return fallback;
        }
        static Vector2 FlightPoint(Vector2 from, Vector2 to, float t, float height)
            => Vector2.Lerp(from, to, t) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * height);

        void DrawPreview()
        {
            MeshPen p = previewPen;
            p.Clear();
            Color color = previewValid ? DoodleGlyphs.TierColor(previewTier) : NotebookPalette.Red;
            for (int i = 0; i < previewCells.Count; i++)
            {
                Cell cell = previewCells[i];
                Vector2 center = new Vector2(cell.X + .5f, cell.Y + .5f);
                p.Box(center, new Vector2(.91f, .91f), MeshPen.Alpha(color, .15f), MeshPen.Alpha(color, .8f), .056f, cell.GetHashCode());
                if (previewValid)
                {
                    if (NotebookTowerComposition.UsesComposition(previewBlueprint))
                    {
                        NotebookTowerComposition.DrawStatic(p, previewBlueprint, center, previewTier, InkColor.None, .65f, Theme);
                        NotebookTowerComposition.DrawMounts(p, previewBlueprint, center, previewTier, InkColor.None,
                            default, false, null, 0, 0, .65f, Theme);
                    }
                    else if (previewBlueprint == null && ConstructionToolRules.IsTool(previewKind) && NotebookArtRenderer.Draw(p,
                        (Theme ?? NotebookVisualThemeAsset.Default)?.FindShape(previewKind == StructureKind.TarPit ? "object.tar" :
                            previewKind == StructureKind.RepairPost ? "object.repair" : previewKind == StructureKind.Spikes ? "object.spikes" : "object.wall.0"),
                        center, strokeTint: color, opacity: .65f, theme: Theme)) { }
                    else if (previewKind == StructureKind.Wall)
                        p.Line(center + new Vector2(-.31f, -.28f), center + new Vector2(.3f, .28f), .032f, MeshPen.Alpha(color, .5f));
                    else
                    {
                        p.Ring(center, .23f, .043f, MeshPen.Alpha(color, .7f), 14, 1, cell.GetHashCode());
                        p.Line(center, center + Vector2.up * .36f, .06f, MeshPen.Alpha(color, .7f));
                    }
                }
                else
                {
                    p.Line(center + new Vector2(-.18f, -.18f), center + new Vector2(.18f, .18f), .05f, color);
                    p.Line(center + new Vector2(-.18f, .18f), center + new Vector2(.18f, -.18f), .05f, color);
                }
            }
            if (previewCells.Count == 1 && previewKind != StructureKind.Wall && previewKind != StructureKind.Spikes)
            {
                Cell cell = previewCells[0];
                Vector2 center = new Vector2(cell.X + .5f, cell.Y + .5f);
                if (previewBlueprint != null)
                {
                    foreach (var mount in previewBlueprint.WeaponMounts)
                    {
                        float range = session.Balance.WeaponRange(previewBlueprint, mount.WeaponId, mount.SlotIndex);
                        float minimum = session.Balance.WeaponMinimumRange(previewBlueprint, mount.WeaponId, mount.SlotIndex);
                        p.Ring(center, range, .045f, MeshPen.Alpha(color, .38f), 120, 1, 117 + mount.SlotIndex, true);
                        if (minimum > 0)
                            p.Ring(center, minimum, .045f, MeshPen.Alpha(NotebookPalette.Red, .45f), 80, 1, 217 + mount.SlotIndex, true);
                    }
                }
                else
                {
                    var definition = session.Balance.Structure(previewKind);
                    float range = previewRange >= 0 ? previewRange : previewKind == StructureKind.TarPit ? Mathf.Max(.5f, definition.Radius) : definition.Range;
                    p.Ring(center, range, .045f, MeshPen.Alpha(color, .38f), 120, 1, 117, true);
                }
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
