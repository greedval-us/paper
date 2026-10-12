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
        bool showRanges;
        public bool ShowRanges
        {
            get => showRanges;
            set { if (showRanges == value) return; showRanges = value; staticDirty = true; }
        }
        public int StaticGeometryRefreshCount { get; private set; }
        public int PreviewGeometryRefreshCount { get; private set; }
        public int ActiveVisualEffects => firingMarks.Count;
        public double LastMeshBuildMilliseconds { get; private set; }
        public double TotalMeshBuildMilliseconds { get; private set; }
        public int MeshBuildCount { get; private set; }
        public int StaticStructureBuildCount => StaticGeometryRefreshCount;

        IRunReadModel session;
        Material inkMaterial;
        MeshPen paperPen, structurePen, worldPen, effectsPen, previewPen;
        readonly List<Mesh> meshes = new List<Mesh>(5);
        readonly List<Cell> previewCells = new List<Cell>(128);
        readonly List<FiringMark> firingMarks = new List<FiringMark>(256);
        StructureKind previewKind;
        MaterialTier previewTier;
        bool previewValid;
        float previewRange = -1;
        int paperWidth, paperHeight;
        int visualVersion = -1, topologyVersion = -1, castleHealthBand = -1;
        RunPhase renderedPhase;
        bool staticDirty = true;

        struct FiringMark
        {
            public Vector2 From, To;
            public SimulationEventKind EventKind;
            public StructureKind Weapon;
            public AttackKind Attack;
            public float Radius, Born;
            public FootprintGeometry? ImpactFootprint;
        }

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
            previewCells.Clear();
            staticDirty = true;
            visualVersion = topologyVersion = castleHealthBand = -1;
            if (session != null)
            {
                paperWidth = session.Balance.Width;
                paperHeight = session.Balance.Height;
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

        public void SetPreview(IReadOnlyList<Cell> cells, StructureKind kind, MaterialTier tier, bool valid, float range = -1)
        {
            int count = cells == null ? 0 : cells.Count;
            bool changed = previewKind != kind || previewTier != tier || previewValid != valid || previewCells.Count != count || previewRange != range;
            for (int i = 0; !changed && i < count; i++) changed = previewCells[i] != cells[i];
            if (!changed) return;
            previewCells.Clear();
            if (cells != null)
                for (int i = 0; i < cells.Count; i++) previewCells.Add(cells[i]);
            previewKind = kind;
            previewTier = tier;
            previewValid = valid;
            previewRange = range;
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
            if (item.Kind == SimulationEventKind.ProjectileLaunched) return;
            if (firingMarks.Count >= 1024) firingMarks.RemoveAt(0);
            firingMarks.Add(new FiringMark
            {
                From = new Vector2(item.From.X, item.From.Y),
                To = new Vector2(item.To.X, item.To.Y),
                EventKind = item.Kind, Weapon = item.Weapon,
                Attack = session != null && session.Balance.Content.TryGetWeapon(item.WeaponId, out var weapon) ? weapon.Attack : LegacyContentCatalog.Attack(item.Weapon),
                Radius = item.Radius, Born = Time.unscaledTime,
                ImpactFootprint = item.ImpactFootprint
            });
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
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            int band = Mathf.CeilToInt(4 * session.BaseHealth / Mathf.Max(1, session.Balance.BaseHealth));
            if (staticDirty || visualVersion != session.StructureVisualVersion || topologyVersion != session.TopologyVersion
                || castleHealthBand != band || renderedPhase != session.Phase) DrawStructures();
            DrawWorld();
            DrawEffects();
            LastMeshBuildMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency;
            TotalMeshBuildMilliseconds += LastMeshBuildMilliseconds;
            MeshBuildCount++;
        }

        void DrawPaper()
        {
            MeshPen p = paperPen;
            p.Clear();
            Color navy = NotebookPalette.Navy;
            // The subtle page shadow and warm edge make the world feel like an actual sheet.
            p.Rect(-2.5f, -2.5f, paperWidth + 5, paperHeight + 5, new Color(.89f, .88f, .82f));
            p.Rect(-1.75f, -1.75f, paperWidth + 3.5f, paperHeight + 3.5f, NotebookPalette.Paper);
            p.Rect(-1.7f, -1.7f, .22f, paperHeight + 3.4f, new Color(.95f, .92f, .84f));
            Color minor = new Color(.35f, .54f, .68f, .25f);
            Color major = new Color(.30f, .48f, .65f, .32f);
            for (int x = -1; x <= paperWidth + 1; x++)
            {
                // The pale under-stroke prevents the fine core disappearing between pixels
                // at 720p; it reads as blue paper ink instead of a broken subpixel line.
                p.Line(new Vector2(x, -1.5f), new Vector2(x, paperHeight + 1.5f),
                       x % 5 == 0 ? .13f : .11f, MeshPen.Alpha(x % 5 == 0 ? major : minor, x % 5 == 0 ? .12f : .08f));
                p.Stroke(new Vector2(x, -1.5f), new Vector2(x, paperHeight + 1.5f),
                         x % 5 == 0 ? .06f : .045f, x % 5 == 0 ? major : minor, x * 71, .006f);
            }
            for (int y = -1; y <= paperHeight + 1; y++)
            {
                p.Line(new Vector2(-1.5f, y), new Vector2(paperWidth + 1.5f, y),
                       y % 5 == 0 ? .13f : .11f, MeshPen.Alpha(y % 5 == 0 ? major : minor, y % 5 == 0 ? .12f : .08f));
                p.Stroke(new Vector2(-1.5f, y), new Vector2(paperWidth + 1.5f, y),
                         y % 5 == 0 ? .06f : .045f, y % 5 == 0 ? major : minor, y * 83, .006f);
            }
            // Faint paper fibres stay static and are cheap to keep in the background batch.
            for (int i = 0; i < 700; i++)
            {
                float x = MeshPen.Hash(i * 79 + 13) * (paperWidth + 2) - 1;
                float y = MeshPen.Hash(i * 57 + 991) * (paperHeight + 2) - 1;
                p.Line(new Vector2(x, y), new Vector2(x + .045f + MeshPen.Hash(i) * .08f, y + .018f),
                       .015f, new Color(.55f, .47f, .32f, .045f));
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
            for (int i = 0; i < 46; i++)
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
            p.Ring(home, 6.5f, .04f, new Color(.24f, .42f, .67f, .16f), 120, 1, 7, true);
            Vector2 compass = new Vector2(paperWidth - 4, paperHeight - 4.5f);
            p.Ring(compass, .8f, .038f, sketch, 24, 1, 22);
            p.Line(compass + Vector2.down * 1.05f, compass + Vector2.up * 1.05f, .04f, sketch);
            p.Line(compass + Vector2.left * 1.05f, compass + Vector2.right * 1.05f, .04f, sketch);
            p.Triangle(compass + Vector2.up * .85f, compass + new Vector2(-.16f, -.14f), compass + new Vector2(.16f, -.14f), sketch);
            p.Upload();
        }

        void DrawStructures()
        {
            MeshPen p = structurePen;
            p.Clear();
            if (ShowRanges)
                for (int i = 0; i < session.Structures.Count; i++)
                {
                    StructureView structure = session.Structures[i];
                    float range = structure.Blueprint == null ? session.Balance.WeaponRange(null, structure.Kind) :
                        session.Balance.WeaponRange(structure.Blueprint, structure.Blueprint.FirstWeaponId);
                    if (structure.Blueprint != null && structure.Blueprint.SecondWeaponId != null)
                        range = Mathf.Max(range, session.Balance.WeaponRange(structure.Blueprint, structure.Blueprint.SecondWeaponId, 1));
                    if (range <= 1) continue;
                    p.Ring(new Vector2(structure.Cell.X + .5f, structure.Cell.Y + .5f), range, .035f,
                           MeshPen.Alpha(DoodleGlyphs.TierColor(structure.Tier), .16f), 96, 1, structure.Id, true);
                }
            for (int i = 0; i < session.Structures.Count; i++)
            {
                DoodleGlyphs.StructureBase(p, session.Structures[i]);
            }
            Vector2 home = new Vector2(session.BaseCell.X + .5f, session.BaseCell.Y + .5f);
            DoodleGlyphs.Castle(p, home, session.BaseHealth / session.Balance.BaseHealth);
            if (session.Phase == RunPhase.Defeat)
            {
                p.Line(home + new Vector2(-1.9f, -1.7f), home + new Vector2(1.9f, 1.7f), .13f, MeshPen.Alpha(NotebookPalette.Red, .7f));
                p.Line(home + new Vector2(-1.9f, 1.7f), home + new Vector2(1.9f, -1.7f), .13f, MeshPen.Alpha(NotebookPalette.Red, .7f));
            }
            p.Upload();
            StaticGeometryRefreshCount++;
            visualVersion = session.StructureVisualVersion;
            topologyVersion = session.TopologyVersion;
            castleHealthBand = Mathf.CeilToInt(4 * session.BaseHealth / Mathf.Max(1, session.Balance.BaseHealth));
            renderedPhase = session.Phase;
            staticDirty = false;
        }

        void DrawWorld()
        {
            MeshPen p = worldPen;
            p.Clear();
            for (int i = 0; i < session.Wells.Count; i++) DrawWell(p, session.Wells[i]);
            for (int i = 0; i < session.Structures.Count; i++)
            {
                var structure = session.Structures[i];
                float duration = session.Balance.Structure(structure.Kind).BuildSeconds
                               * session.Balance.Material(structure.Tier).BuildTimeMultiplier;
                DoodleGlyphs.StructureDynamic(p, structure, duration);
            }
            float time = Time.unscaledTime;
            bool compactSwarm = session.Enemies.Count > 1000;
            bool cull = WorldCamera != null && WorldCamera.orthographic;
            Vector3 cameraPosition = cull ? WorldCamera.transform.position : Vector3.zero;
            float halfHeight = cull ? WorldCamera.orthographicSize + 1 : 0;
            float halfWidth = cull ? WorldCamera.orthographicSize * WorldCamera.aspect + 1 : 0;
            for (int i = 0; i < session.Enemies.Count; i++)
            {
                EnemyView enemy = session.Enemies[i];
                if (cull && (Mathf.Abs(enemy.Position.X - cameraPosition.x) > halfWidth
                          || Mathf.Abs(enemy.Position.Y - cameraPosition.y) > halfHeight)) continue;
                DoodleGlyphs.Enemy(p, enemy, time, compactSwarm);
            }
            p.Upload();
        }
        static void DrawWell(MeshPen p, InkWellView well)
        {
            Vector2 at = new Vector2(well.Cell.X + .5f, well.Cell.Y + .5f);
            Color color = DoodleGlyphs.InkTone(well.Color);
            Color ink = well.Active ? color : MeshPen.Alpha(color, .42f);
            p.Disc(at, 1.1f, MeshPen.Alpha(color, well.Active ? .13f : .06f), 24, .85f);
            p.Box(at, new Vector2(.95f, .85f), NotebookPalette.Paper, NotebookPalette.Navy, .075f, well.Id);
            p.Disc(at, .36f, ink, 16, .73f);
            p.Ring(at + new Vector2(0, .33f), .35f, .07f, color, 16, .5f, well.Id);
            p.Line(at + new Vector2(-.55f, -.64f), at + new Vector2(.55f, -.64f), .07f, MeshPen.Alpha(NotebookPalette.Navy, .2f));
            p.Line(at + new Vector2(-.55f, -.64f), at + new Vector2(-.55f + 1.1f * Mathf.Clamp01(well.Health / Mathf.Max(1, well.MaxHealth)), -.64f), .07f, color);
            if (well.Threatened)
            {
                float pulse = .55f + .45f * Mathf.Sin(Time.unscaledTime * 8);
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
                p.Disc(center, .42f, color, 9, .65f);
                p.Disc(center + new Vector2(.21f, .08f), .2f, color, 7, .7f);
                p.Disc(center + new Vector2(-.23f, -.12f), .14f, color, 7);
            }
            for (int i = 0; i < session.EnemyProjectiles.Count; i++)
            {
                var glob = session.EnemyProjectiles[i];
                Vector2 from = new Vector2(glob.From.X, glob.From.Y), to = new Vector2(glob.To.X, glob.To.Y);
                float progress = Mathf.Clamp01(1 - glob.Remaining / Mathf.Max(.001f, glob.Duration));
                Vector2 point = Vector2.Lerp(from, to, progress);
                p.Line(Vector2.Lerp(from, to, Mathf.Max(0, progress - .12f)), point, .09f, MeshPen.Alpha(NotebookPalette.Marker, .55f));
                p.Disc(point, .14f, NotebookPalette.Navy, 8); p.Disc(point, .09f, NotebookPalette.Marker, 7);
                p.Ring(to, .36f, .035f, MeshPen.Alpha(NotebookPalette.Red, .5f), 12, 1, glob.Id, true);
            }
            for (int i = 0; i < session.Projectiles.Count; i++)
            {
                var projectile = session.Projectiles[i];
                Vector2 from = new Vector2(projectile.From.X, projectile.From.Y);
                Vector2 to = new Vector2(projectile.To.X, projectile.To.Y);
                float flight = Mathf.Clamp01(1 - projectile.Remaining / Mathf.Max(.001f, projectile.Duration));
                float height = Mathf.Min(3, Vector2.Distance(from, to) * .22f);
                Vector2 point = FlightPoint(from, to, flight, height);
                Vector2 trail = FlightPoint(from, to, Mathf.Max(0, flight - .1f), height);
                p.Line(trail, point, .045f, MeshPen.Alpha(NotebookPalette.Marker, .6f));
                p.Disc(point, .1f, NotebookPalette.Navy, 10);
                p.Disc(point + new Vector2(-.025f, .025f), .035f, NotebookPalette.Paper, 6);
                DrawFootprint(p, to, projectile.ImpactFootprint, Mathf.Max(.4f, projectile.Radius), 1, .025f,
                    MeshPen.Alpha(NotebookPalette.Marker, .2f), 24, projectile.Id, true);
            }
            float time = Time.unscaledTime;
            for (int i = firingMarks.Count - 1; i >= 0; i--)
            {
                FiringMark mark = firingMarks[i];
                float age = time - mark.Born;
                bool impact = mark.EventKind == SimulationEventKind.ProjectileImpact;
                bool destroyed = mark.EventKind == SimulationEventKind.StructureDestroyed;
                float duration = impact || destroyed ? .46f : .17f;
                if (age >= duration) { firingMarks.RemoveAt(i); continue; }
                float t = Mathf.Clamp01(age / duration);
                float fade = 1 - t;
                Color color = mark.Attack == AttackKind.Line ? NotebookPalette.Marker : NotebookPalette.Blue;
                color.a = fade * .85f;
                if (destroyed)
                {
                    p.Star(mark.To, .4f + t * .45f, MeshPen.Alpha(NotebookPalette.Red, fade * .7f), .045f, 7, i);
                }
                else if (impact)
                {
                    float radius = Mathf.Max(.65f, mark.Radius);
                    Color orange = new Color(.85f, .46f, .2f, fade * .75f);
                    if (mark.ImpactFootprint.HasValue && mark.ImpactFootprint.Value.ForwardRadius != mark.ImpactFootprint.Value.SideRadius)
                        DrawFootprint(p, mark.To, mark.ImpactFootprint, radius, 1, .025f, orange, 32, i * 23);
                    DrawFootprint(p, mark.To, mark.ImpactFootprint, radius, .6f + t * .4f, .065f * fade + .025f, orange, 32, i * 13);
                    DrawFootprint(p, mark.To, mark.ImpactFootprint, radius, .45f + t * .4f, .035f, MeshPen.Alpha(orange, fade * .35f), 32, i * 19);
                    if (!mark.ImpactFootprint.HasValue || mark.ImpactFootprint.Value.ForwardRadius == mark.ImpactFootprint.Value.SideRadius)
                        p.Star(mark.To, radius * (.4f + t * .6f), orange, .045f, 9, i * .83f);
                }
                else if (mark.Attack == AttackKind.Cleanup)
                {
                    Color clean = MeshPen.Alpha(NotebookPalette.Green, fade * .75f);
                    p.Line(mark.From, mark.To, .025f, clean);
                    p.Ring(mark.To, Mathf.Max(.2f, mark.Radius) * (.65f + t * .35f), .035f, clean, 24);
                }
                else
                {
                    float width = mark.Attack == AttackKind.Line ? .075f : .044f;
                    p.Line(mark.From, mark.To, width, color);
                    p.Star(mark.To, .22f + t * .2f, color, .035f, 5, i * 1.3f);
                    if (t < .55f) p.Star(mark.From, .17f, MeshPen.Alpha(color, fade), .035f, 4, .4f);
                }
            }
            p.Upload();
        }

        // Build each perimeter point in world space; axis direction comes from the shell snapshot.
        static void DrawFootprint(MeshPen pen, Vector2 center, FootprintGeometry? footprint,
            float fallbackRadius, float scale, float width, Color color, int segments, int seed, bool dashed = false)
        {
            if (!footprint.HasValue || footprint.Value.ForwardRadius == footprint.Value.SideRadius)
            {
                pen.Ring(center, fallbackRadius * scale, width, color, segments, 1, seed, dashed);
                return;
            }
            FootprintGeometry geometry = footprint.Value;
            Vector2 forward = new Vector2(geometry.Forward.X, geometry.Forward.Y);
            Vector2 side = new Vector2(-forward.y, forward.x);
            Vector2 previous = center + forward * (geometry.ForwardRadius * scale);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vector2 next = center + forward * (Mathf.Cos(angle) * geometry.ForwardRadius * scale)
                    + side * (Mathf.Sin(angle) * geometry.SideRadius * scale);
                if (!dashed || i % 3 != 0) pen.Line(previous, next, width, color);
                previous = next;
            }
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
                    if (previewKind == StructureKind.Wall)
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
                float range = previewRange >= 0 ? previewRange : session.Balance.Structure(previewKind).Range;
                p.Ring(new Vector2(cell.X + .5f, cell.Y + .5f), range, .045f, MeshPen.Alpha(color, .38f), 120, 1, 117, true);
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
