using System;
using System.Collections.Generic;
using System.Linq;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    public readonly struct AssemblyMountPose
    {
        public readonly int SlotIndex;
        public readonly float Radians, Recoil;
        public AssemblyMountPose(int slotIndex, float radians, float recoil = 0)
        { SlotIndex = slotIndex; Radians = radians; Recoil = recoil; }
    }
    public sealed class AssemblyVisualPose
    {
        public string VisualRevision { get; }
        public IReadOnlyList<AssemblyMountPose> Mounts { get; }
        public AssemblyVisualPose(IEnumerable<AssemblyMountPose> mounts = null, string visualRevision = null)
        {
            VisualRevision = visualRevision;
            var copy = new List<AssemblyMountPose>(); var slots = new HashSet<int>();
            foreach (var m in mounts ?? Array.Empty<AssemblyMountPose>())
            {
                if (m.SlotIndex < 0 || m.SlotIndex > 2 || !slots.Add(m.SlotIndex) || !Finite(m.Radians) || !Finite(m.Recoil))
                    throw new ArgumentException("Invalid or duplicate mount pose.");
                copy.Add(m);
            }
            Mounts = copy.AsReadOnly();
        }
        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        internal AssemblyMountPose For(int slot)
        { foreach (var m in Mounts) if (m.SlotIndex == slot) return m; return new AssemblyMountPose(slot, 0); }
    }
    public readonly struct AssemblyVisualAnchor
    {
        public readonly int SlotIndex;
        public readonly Vector2 Attach, RotationCenter, Muzzle;
        internal AssemblyVisualAnchor(int slot, Vector2 attach, Vector2 pivot, Vector2 muzzle)
        { SlotIndex = slot; Attach = attach; RotationCenter = pivot; Muzzle = muzzle; }
    }
    public readonly struct AssemblyVisualFragment
    {
        public readonly string PartId, SpriteId;
        public readonly int SlotIndex, Layer;
        public readonly Rect Trim, Region;
        public readonly Vector2 Origin;
        public readonly float Scale, Radians;
        internal AssemblyVisualFragment(string part, string sprite, int slot, int layer, Rect trim, Rect region, Vector2 origin, float scale, float radians)
        { PartId = part; SpriteId = sprite; SlotIndex = slot; Layer = layer; Trim = trim; Region = region;
            Origin = origin; Scale = scale; Radians = radians; }
        public Vector2 Transform(Vector2 pixel) => Origin + NotebookMountLayout.Rotate(pixel * Scale, Radians);
        internal void Draw(MeshPen pen, Vector2 at, float opacity, Color tint, float uniformScale = 1)
        {
            if (!NotebookSpriteCatalog.TryDrawFrame(pen, SpriteId, Trim, Region, at + Origin * uniformScale, Scale * uniformScale, Radians, opacity, tint))
                throw new InvalidOperationException("Missing assembly sprite: " + SpriteId);
        }
    }
    public sealed class AssemblyVisualComposition
    {
        public string Revision { get; }
        public IReadOnlyList<AssemblyVisualFragment> Fragments { get; }
        public IReadOnlyList<AssemblyVisualAnchor> Anchors { get; }
        public Rect Bounds { get; }
        internal AssemblyVisualComposition(string revision, List<AssemblyVisualFragment> fragments, List<AssemblyVisualAnchor> anchors, Rect bounds)
        { Revision = revision; Fragments = fragments.AsReadOnly(); Anchors = anchors.AsReadOnly(); Bounds = bounds; }
        public void Draw(MeshPen pen, Vector2 at, float opacity = 1, Color? tint = null, float uniformScale = 1)
        {
            if (float.IsNaN(uniformScale) || float.IsInfinity(uniformScale) || uniformScale <= 0)
                throw new ArgumentOutOfRangeException(nameof(uniformScale));
            foreach (var f in Fragments) f.Draw(pen, at, opacity, tint ?? Color.white, uniformScale);
        }
    }

    /// <summary>One ordered, immutable visual result for recipe previews and legacy pilot adapters.
    /// Author pixels, trim and packing affect UVs only; Core receives the same transformed occupied corners.</summary>
    public static partial class AssemblyVisualComposer
    {
        [Serializable] public sealed class FoundationPart
        {
            public string partId, spriteId, collisionMode;
            public int layer;
            public Vector2 canvas, position;
            public NotebookMountLayout.PixelRect trim, region;
            public float scale;
        }
        [Serializable] public sealed class Manifest : NotebookMountLayout.Source
        {
            public int visualSchemaVersion;
            public NotebookMountLayout.PixelRect frameBounds;
            public AssemblyFitCatalogInput fits;
            public FoundationPart[] foundationParts;
            public VisualPart[] visualParts;
            public Vector2 modernPivot;
            public AssemblyFitCatalogInput legacyFits;
        }
        static Manifest manifest;
        static AssemblyFitCatalog fits;
        static AssemblyFitCatalog historicalFits;
        static NotebookMountLayout.Source mounts;
        public static AssemblyFitCatalog Fits { get { EnsureLoaded(); return fits; } }
        public static string Revision { get { EnsureLoaded(); return manifest.revision; } }

        public static AssemblyVisualComposition Compose(RecipeSnapshot snapshot, AssemblyVisualPose pose)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            pose = pose ?? new AssemblyVisualPose(); EnsureLoaded();
            if (pose.VisualRevision != null && pose.VisualRevision != Revision) throw new FormatException("Unknown assembly visual revision: " + pose.VisualRevision);
            var diagnostics = AssemblyFitRules.Validate(snapshot.Recipe, fits);
            if (diagnostics.Count != 0) throw new AssemblyCatalogException(diagnostics);
            foreach (var p in pose.Mounts)
                if (snapshot.Recipe.Tower == null || !snapshot.Recipe.Tower.Mounts.Any(m => m.SlotIndex == p.SlotIndex)) throw new ArgumentException("Pose references an unoccupied socket.");
            return ComposeCatalog(snapshot.Recipe, pose);
        }

        static AssemblyVisualComposition ComposePilot(AssemblyRecipe recipe, TowerBlueprint blueprint, AssemblyVisualPose pose)
        {
            var fragments = new List<AssemblyVisualFragment>(); var anchors = new List<AssemblyVisualAnchor>();
            bool armor = recipe.Tower.Foundation.Armor.Id == "armor.plate";
            foreach (var p in manifest.foundationParts)
            {
                if (p.partId == "armor.plate" && !armor) continue;
                fragments.Add(new AssemblyVisualFragment(p.partId, p.spriteId, -1, p.layer, p.trim.Rect, p.region.Rect, p.position, p.scale, 0));
            }
            foreach (var selected in recipe.Tower.Mounts)
            {
                var mount = NotebookMountLayout.Resolve(blueprint, selected.SlotIndex);
                var motion = pose.For(selected.SlotIndex);
                historicalFits.TryGetWeapon(selected.Weapon.Mechanism.Id, selected.Weapon.Mechanism.Revision, out var fit);
                if (motion.Radians < fit.MinRadians - .00001f || motion.Radians > fit.MaxRadians + .00001f || motion.Recoil > 0 || motion.Recoil < -fit.RecoilLimit - .00001f)
                    throw new ArgumentOutOfRangeException(nameof(pose), "Pose exceeds certified angle or recoil range.");
                var source = mounts.pilots.Single(p => p.chassisId == blueprint.ChassisId && p.weaponId == mount.WeaponId);
                foreach (var f in mount.Fragments)
                {
                    string part = source.fragments.Single(s => s.id == f.Id).componentId;
                    float angle = f.Motion == NotebookMountLayout.FragmentMotion.Fixed ? 0 : motion.Radians;
                    fragments.Add(new AssemblyVisualFragment(part, f.SpriteId, mount.SlotIndex, 3 + f.Layer, f.Trim,
                        new Rect(Vector2.zero, f.Canvas), mount.TransformFragment(f, Vector2.zero, motion.Radians, motion.Recoil),
                        mount.Scale * f.Scale, angle + f.Radians));
                }
                anchors.Add(new AssemblyVisualAnchor(mount.SlotIndex, mount.Socket, mount.RotationCenter,
                    mount.Transform(mount.Muzzle, motion.Radians, motion.Recoil)));
            }
            fragments = fragments.OrderBy(f => f.Layer).ThenBy(f => f.SlotIndex).ToList();
            bool first = true; Rect bounds = default;
            foreach (var f in fragments)
                foreach (var p in Corners(f.Region))
                { Vector2 point = f.Transform(p); var b = new Rect(point, Vector2.zero); bounds = first ? b : Union(bounds, b); first = false; }
            // Reserve the entire certified motion envelope: UI scale stays constant across poses.
            foreach (var a in anchors)
            {
                historicalFits.TryGetWeapon("core.gun", 1, out var fit); float r = AssemblyFitRules.SweptRadius(fit);
                bounds = Union(bounds, new Rect(a.RotationCenter - Vector2.one * r, Vector2.one * 2 * r));
            }
            return new AssemblyVisualComposition(Revision, fragments, anchors, bounds);
        }

        internal static bool Supports(TowerBlueprint blueprint) => blueprint != null && !blueprint.HasPlayerDrawing
            && blueprint.ChassisId == "chassis.standard" && blueprint.WeaponMounts.Count > 0 && blueprint.WeaponMounts.Count <= 2
            && blueprint.WeaponMounts.All(m => m.SlotIndex < 2 && m.WeaponId == "weapon.gun")
            && blueprint.ModuleSelections.All(m => m.DefinitionId == "module.armor");

        // Compatibility bridge until profile recipe migration (task 4). No saved blueprint is mutated.
        internal static AssemblyVisualComposition ComposeLegacy(TowerBlueprint blueprint, Func<int, Point2> aim,
            Vector2 origin, Func<int, float> recoil)
        {
            EnsureLoaded();
            var foundation = new FoundationRecipe(P("frame.standard"), P("hull.standard"), P(blueprint.ModuleSelections.Any(m => m.DefinitionId == "module.armor") ? "armor.plate" : "armor.none"), P("feed.standard"), P("turn.standard"));
            var weapon = new WeaponRecipe(P("core.gun"), P("head.gun"), P("sight.iron"), P("magazine.standard"), P("trigger.standard"));
            var recipe = new AssemblyRecipe(new TowerRecipe(foundation, blueprint.WeaponMounts.Select(m => new WeaponMountRecipe(m.SlotIndex, weapon))));
            var issues = AssemblyFitRules.Validate(recipe, historicalFits); if (issues.Count != 0) throw new AssemblyCatalogException(issues);
            var poses = blueprint.WeaponMounts.Select(m => {
                var mount = NotebookMountLayout.Resolve(blueprint, m.SlotIndex);
                float angle = mount.AimAngle(aim?.Invoke(m.SlotIndex) ?? default, origin);
                angle = Mathf.Atan2(Mathf.Sin(angle), Mathf.Cos(angle));
                return new AssemblyMountPose(m.SlotIndex, angle, recoil?.Invoke(m.SlotIndex) ?? 0);
            });
            return ComposePilot(recipe, blueprint, new AssemblyVisualPose(poses));
        }
        static PartRef P(string id) => new PartRef(id, 1);

        /// <summary>Import transaction preparation. Derive fit positions and swept regions from the visual source.</summary>
        public static string BuildRuntimeJson(string json, ISet<string> spriteIds, IReadOnlyDictionary<string, Rect> spriteBounds = null,
            IReadOnlyList<ComponentDefinition> authoritativeDefinitions = null)
        {
            var data = JsonUtility.FromJson<Manifest>(json); var layout = NotebookMountLayout.Parse(json);
            if (data == null || data.visualSchemaVersion != 1 || data.fits == null || data.fits.SchemaVersion != 1
                || data.foundationParts == null || data.foundationParts.Length == 0 || data.revision != data.fits.Revision)
                throw new FormatException("Invalid assembly visual schema or revision.");
            Rect frameBounds = data.frameBounds?.Rect ?? default;
            if (spriteBounds != null && (!spriteBounds.TryGetValue("chassis.standard", out var actualBounds)
                || Vector2.Distance(actualBounds.min, frameBounds.min) > .000001f || Vector2.Distance(actualBounds.size, frameBounds.size) > .000001f))
                throw new FormatException("Pilot frame bounds disagree with sprite source bounds.");
            var pilot = layout.pilots.Single(p => p.chassisId == "chassis.standard" && p.weaponId == "weapon.gun");
            var foundation = layout.foundations.Single(f => f.id == "chassis.standard");
            var weapon = layout.weapons.Single(w => w.id == "weapon.gun");
            var frame = data.fits.Frames.Single(f => f.FrameId == "frame.standard");
            if (frame.Mounts.Length != foundation.sockets.Length || frameBounds.width <= 0 || frameBounds.height <= 0) throw new FormatException("Invalid pilot foundation sockets.");
            foreach (var socket in foundation.sockets)
            {
                var fitSocket = frame.Mounts.Single(m => m.SlotIndex == socket.slot);
                if (fitSocket.SocketType != "gun.standard" || fitSocket.SocketId != "mount." + socket.slot) throw new FormatException("Unknown pilot socket.");
                fitSocket.X = frameBounds.xMin + frameBounds.width * socket.position.x;
                fitSocket.Y = frameBounds.yMin + frameBounds.height * socket.position.y;
            }
            float units = frameBounds.width * foundation.sockets[0].diameter / weapon.diameter;
            var gunFit = data.fits.Weapons.Single(w => w.MechanismId == "core.gun");
            if (gunFit.MountType != "gun.standard" || foundation.sockets.Any(s => Mathf.Abs(frameBounds.width * s.diameter / weapon.diameter - units) > .000001f))
                throw new FormatException("Pilot sockets must share the certified diameter.");
            gunFit.PivotX = pilot.pivotOffset.x * units; gunFit.PivotY = pilot.pivotOffset.y * units;
            var regions = new List<AssemblyFitRegionInput>();
            foreach (var f in pilot.fragments)
            {
                if (!data.fits.Parts.Any(p => p.Id == f.componentId) || (spriteIds != null && !spriteIds.Contains(f.spriteId))) throw new FormatException("Unknown pilot fragment binding.");
                regions.Add(new AssemblyFitRegionInput {
                    Motion = f.group == 0 ? "fixed" : f.group == 1 ? "rotating" : "recoiling",
                    Points = Corners(f.trim.Rect).Select(p => {
                        Vector2 v = (f.offset - pilot.pivotOffset + NotebookMountLayout.Rotate((p - f.attach) * f.scale, f.radians)) * units;
                        return new AssemblyFitPointInput { X = v.x, Y = v.y };
                    }).ToArray()
                });
            }
            gunFit.Regions = regions.ToArray();
            var muzzle = pilot.fragments.Single(f => f.id == pilot.muzzleFragment);
            Vector2 axis = muzzle.offset - pilot.pivotOffset + NotebookMountLayout.Rotate((pilot.muzzlePoint - muzzle.attach) * muzzle.scale, muzzle.radians);
            if (Mathf.Abs(axis.normalized.x) > .00001f || axis.y <= 0) throw new FormatException("Pilot rest muzzle must point +Y for Core recoil certification.");
            var keepouts = new List<AssemblyFitKeepoutInput>();
            foreach (var p in data.foundationParts)
            {
                if (p == null || !data.fits.Parts.Any(part => part.Id == p.partId) || p.scale <= 0 || !Finite(p.scale) || p.trim == null || p.region == null
                    || !Finite(p.position.x) || !Finite(p.position.y) || !Finite(p.canvas.x) || !Finite(p.canvas.y)
                    || !Finite(p.trim.x) || !Finite(p.trim.y) || !Finite(p.trim.width) || !Finite(p.trim.height)
                    || !Finite(p.region.x) || !Finite(p.region.y) || !Finite(p.region.width) || !Finite(p.region.height)
                    || (p.collisionMode != "solid" && p.collisionMode != "mount-contact" && p.collisionMode != "armor")
                    || (p.collisionMode == "armor") != (p.partId == "armor.plate")
                    || p.trim.width <= 0 || p.trim.height <= 0 || p.region.width <= 0 || p.region.height <= 0
                    || p.region.x < p.trim.x || p.region.y < p.trim.y || p.region.x + p.region.width > p.trim.x + p.trim.width + .001f
                    || p.region.y + p.region.height > p.trim.y + p.trim.height + .001f || (spriteIds != null && !spriteIds.Contains(p.spriteId)))
                    throw new FormatException("Invalid foundation fragment binding.");
                if (p.spriteId == "chassis.standard" && (Vector2.Distance(p.position, frameBounds.min) > .000001f
                    || Vector2.Distance(p.canvas * p.scale, frameBounds.size) > .000001f))
                    throw new FormatException("Foundation fragment disagrees with certified frame bounds.");
                // The roof is the authored seating/occlusion band. Every solid body/feed/armor
                // region is exported from its actual visual transform, never a separately sized box.
                if (p.collisionMode != "mount-contact")
                    keepouts.Add(new AssemblyFitKeepoutInput { X = p.position.x + p.region.x * p.scale,
                        Y = p.position.y + p.region.y * p.scale, Width = p.region.width * p.scale,
                        Height = p.region.height * p.scale, ArmorOnly = p.collisionMode == "armor" });
            }
            frame.Keepouts = keepouts.ToArray();
            foreach (var p in data.fits.Parts)
                if (p.AssetRevision != data.revision) throw new FormatException("Unknown part asset revision.");
            data.legacyFits = JsonUtility.FromJson<AssemblyFitCatalogInput>(JsonUtility.ToJson(data.fits));
            var legacy = new AssemblyFitCatalog(data.legacyFits);
            PrepareFullCatalog(data, spriteIds, authoritativeDefinitions);
            var certified = new AssemblyFitCatalog(data.fits);
            foreach (bool armor in new[] { false, true })
            {
                var f = new FoundationRecipe(P("frame.standard"), P("hull.standard"), P(armor ? "armor.plate" : "armor.none"), P("feed.standard"), P("turn.standard"));
                var w = new WeaponRecipe(P("core.gun"), P("head.gun"), P("sight.iron"), P("magazine.standard"), P("trigger.standard"));
                var recipe = new AssemblyRecipe(new TowerRecipe(f, new[] { new WeaponMountRecipe(0, w), new WeaponMountRecipe(1, w) }));
                var issues = AssemblyFitRules.Validate(recipe, certified);
                if (issues.Count != 0) throw new AssemblyCatalogException(issues);
                var historicalIssues = AssemblyFitRules.Validate(recipe, legacy);
                if (historicalIssues.Count != 0) throw new AssemblyCatalogException(historicalIssues);
            }
            return JsonUtility.ToJson(data, true);
        }

        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static IEnumerable<Vector2> Corners(Rect r)
        { yield return r.min; yield return new Vector2(r.xMax, r.yMin); yield return r.max; yield return new Vector2(r.xMin, r.yMax); }
        static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin,b.xMin),Mathf.Min(a.yMin,b.yMin),Mathf.Max(a.xMax,b.xMax),Mathf.Max(a.yMax,b.yMax));
        static void EnsureLoaded()
        {
            if (manifest != null) return;
            var asset = Resources.Load<TextAsset>("Art/NotebookMounts");
            if (asset == null) throw new InvalidOperationException("Missing assembly visual catalogue.");
            if (!NotebookSpriteCatalog.TryGetBounds("chassis.standard", out var bounds)) throw new FormatException("Missing pilot frame sprite.");
            var parsed = JsonUtility.FromJson<Manifest>(BuildRuntimeJson(asset.text, null,
                new Dictionary<string, Rect> { { "chassis.standard", bounds } }));
            if (parsed == null || parsed.visualSchemaVersion != 1) throw new FormatException("Unsupported assembly visual version.");
            fits = new AssemblyFitCatalog(parsed.fits); historicalFits = new AssemblyFitCatalog(parsed.legacyFits ?? parsed.fits);
            mounts = NotebookMountLayout.Parse(asset.text); manifest = parsed;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetCache() { manifest = null; mounts = null; fits = null; historicalFits = null; }
    }
}
