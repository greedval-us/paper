using System;
using System.Collections.Generic;
using System.Linq;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    public static partial class AssemblyVisualComposer
    {
        [Serializable] public sealed class VisualPart
        {
            public string partId, assetRevision, neutral;
            public int revision = 1;
            public ComponentCategory category;
            public AssemblyWeaponFamily weaponFamilies;
            public AssemblyStructureFamily structureFamilies;
            public float hullBaseFraction, hullFrontFraction;
            public VisualMount[] mounts;
            public VisualPartFragment[] fragments = Array.Empty<VisualPartFragment>();
        }
        [Serializable] public sealed class VisualMount { public int slot; public Vector2 position; }
        [Serializable] public sealed class VisualPartFragment
        {
            public string spriteId, socketId, motion = "fixed", collisionMode = "mount-contact";
            public int layer;
            public AssemblyStructureFamily family;
            public Vector2 canvas, attach, position, muzzle;
            public NotebookMountLayout.PixelRect trim, region;
            public float scale = 1, radians;
            public bool hasMuzzle;
        }

        public static bool HasVisualMapping(PartRef reference)
        {
            EnsureLoaded(); return FindPart(manifest, reference) != null;
        }

        /// <summary>Part inspection uses exactly the authored canvases, pivots and ordered layers
        /// used by the completed recipe. Neutral options deliberately have no raster fragments.</summary>
        public static AssemblyVisualComposition ComposePart(PartRef reference)
        {
            EnsureLoaded(); var part = FindPart(manifest, reference);
            if (part == null) throw new ArgumentException("No visual mapping for " + reference.Id + "@" + reference.Revision);
            var fragments = new List<AssemblyVisualFragment>();
            var family = part.structureFamilies;
            if (family == AssemblyStructureFamily.All || !SingleFamily(family)) family = AssemblyStructureFamily.Wall;
            Append(part, -1, Vector2.zero, 0, 0, fragments, family);
            return Finish(fragments, new List<AssemblyVisualAnchor>());
        }

        public static AssemblyVisualComposition ComposeWeapon(WeaponRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            EnsureLoaded();
            var foundation = new FoundationRecipe(P("frame.standard"), P("hull.standard"), P("armor.none"), P("feed.standard"), P("turn.standard"));
            var assembly = new AssemblyRecipe(new TowerRecipe(foundation, new[] { new WeaponMountRecipe(0, recipe) }));
            var diagnostics = AssemblyFitRules.Validate(assembly, fits);
            if (diagnostics.Count != 0) throw new AssemblyCatalogException(diagnostics);
            var full = ComposeCatalog(assembly, new AssemblyVisualPose()); var anchor = full.Anchors.Single();
            var fragments = full.Fragments.Where(f => f.SlotIndex == 0).Select(f => new AssemblyVisualFragment(
                f.PartId, f.SpriteId, f.SlotIndex, f.Layer, f.Trim, f.Region, f.Origin - anchor.RotationCenter, f.Scale, f.Radians)).ToList();
            return Finish(fragments, new List<AssemblyVisualAnchor> { new AssemblyVisualAnchor(0,
                anchor.Attach - anchor.RotationCenter, Vector2.zero, anchor.Muzzle - anchor.RotationCenter) });
        }

        public static AssemblyVisualComposition ComposeFoundation(FoundationRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            EnsureLoaded(); int category = 0;
            foreach (var reference in recipe.Parts)
            {
                var part = RequiredPart(reference);
                if (part.category != (ComponentCategory)category++) throw new ArgumentException("A foundation part uses the wrong socket category.");
            }
            if (!fits.TryGetFrame(recipe.Frame.Id, recipe.Frame.Revision, out var frame)) throw new ArgumentException("Unknown foundation frame.");
            var fragments = new List<AssemblyVisualFragment>();
            foreach (var reference in recipe.Parts)
            {
                if (reference.Id == recipe.Turn.Id) continue;
                if (reference.Id == recipe.Hull.Id)
                {
                    AppendFrameHull(RequiredPart(recipe.Frame), fragments, reference.Id);
                    if (reference.Id != "hull.standard") Append(RequiredPart(reference), -1, Vector2.zero, 0, 0, fragments);
                }
                else Append(RequiredPart(reference), -1, Vector2.zero, 0, 0, fragments);
            }
            fits.TryGetWeapon("core.gun", 1, out var mounting);
            foreach (var socket in frame.Mounts)
                Append(RequiredPart(recipe.Turn), socket.SlotIndex,
                    new Vector2(socket.Position.X + mounting.Pivot.X, socket.Position.Y + mounting.Pivot.Y), 0, 0, fragments);
            return Finish(fragments, new List<AssemblyVisualAnchor>());
        }

        static bool IsPilotRecipe(AssemblyRecipe recipe)
        {
            if (recipe.Tower == null) return false;
            var foundation = recipe.Tower.Foundation;
            return foundation.Frame.Id == "frame.standard" && foundation.Hull.Id == "hull.standard"
                && (foundation.Armor.Id == "armor.none" || foundation.Armor.Id == "armor.plate")
                && foundation.Feed.Id == "feed.standard" && foundation.Turn.Id == "turn.standard"
                && recipe.Tower.Mounts.All(m => m.Weapon.Mechanism.Id == "core.gun" && m.Weapon.Head.Id == "head.gun"
                    && m.Weapon.Sight.Id == "sight.iron" && m.Weapon.Magazine.Id == "magazine.standard" && m.Weapon.Trigger.Id == "trigger.standard");
        }

        static AssemblyVisualComposition ComposeCatalog(AssemblyRecipe recipe, AssemblyVisualPose pose)
        {
            if (manifest.visualParts == null) throw new FormatException("The full assembly visual catalogue is missing.");
            var fragments = new List<AssemblyVisualFragment>(); var anchors = new List<AssemblyVisualAnchor>();
            if (recipe.Structure != null)
            {
                foreach (var reference in recipe.Structure.Parts)
                    Append(RequiredPart(reference), -1, Vector2.zero, 0, 0, fragments, recipe.Structure.Family);
                return Finish(fragments, anchors);
            }
            var tower = recipe.Tower;
            foreach (var reference in tower.Foundation.Parts)
            {
                if (reference.Id == tower.Foundation.Turn.Id) continue;
                if (reference.Id == tower.Foundation.Hull.Id)
                {
                    AppendFrameHull(RequiredPart(tower.Foundation.Frame), fragments, reference.Id);
                    if (reference.Id != "hull.standard") Append(RequiredPart(reference), -1, Vector2.zero, 0, 0, fragments);
                }
                else Append(RequiredPart(reference), -1, Vector2.zero, 0, 0, fragments);
            }
            fits.TryGetFrame(tower.Foundation.Frame.Id, tower.Foundation.Frame.Revision, out var frame);
            foreach (var selected in tower.Mounts)
            {
                var socket = frame.Mounts.Single(m => m.SlotIndex == selected.SlotIndex);
                var root = new Vector2(socket.Position.X, socket.Position.Y);
                fits.TryGetWeapon(selected.Weapon.Mechanism.Id, selected.Weapon.Mechanism.Revision, out var weapon);
                var motion = pose.For(selected.SlotIndex);
                if (motion.Radians < weapon.MinRadians - .00001f || motion.Radians > weapon.MaxRadians + .00001f
                    || motion.Recoil > 0 || motion.Recoil < -weapon.RecoilLimit - .00001f)
                    throw new ArgumentOutOfRangeException(nameof(pose), "Pose exceeds certified angle or recoil range.");
                root += new Vector2(weapon.Pivot.X, weapon.Pivot.Y);
                Append(RequiredPart(tower.Foundation.Turn), selected.SlotIndex, root, motion.Radians, motion.Recoil, fragments);
                foreach (var reference in selected.Weapon.Parts)
                    Append(RequiredPart(reference), selected.SlotIndex, root, motion.Radians, motion.Recoil, fragments);
                var head = RequiredPart(selected.Weapon.Head);
                var muzzle = head.fragments.Single(f => f.hasMuzzle);
                float angle = muzzle.motion == "fixed" ? 0 : motion.Radians;
                var localMuzzle = muzzle.position + NotebookMountLayout.Rotate((muzzle.muzzle - muzzle.attach) * muzzle.scale, muzzle.radians);
                var muzzlePoint = root + NotebookMountLayout.Rotate(localMuzzle, angle);
                if (muzzle.motion == "recoiling") muzzlePoint += NotebookMountLayout.Rotate(Vector2.up * motion.Recoil, angle);
                anchors.Add(new AssemblyVisualAnchor(selected.SlotIndex,
                    new Vector2(socket.Position.X, socket.Position.Y), root, muzzlePoint));
            }
            var composition = Finish(fragments, anchors); var bounds = composition.Bounds;
            foreach (var anchor in anchors)
            {
                var selected = tower.Mounts.Single(m => m.SlotIndex == anchor.SlotIndex);
                fits.TryGetWeapon(selected.Weapon.Mechanism.Id, selected.Weapon.Mechanism.Revision, out var weapon);
                float radius = AssemblyFitRules.SweptRadius(weapon);
                bounds = Union(bounds, new Rect(anchor.RotationCenter - Vector2.one * radius, Vector2.one * radius * 2));
            }
            return new AssemblyVisualComposition(Revision, fragments.OrderBy(f => f.Layer).ThenBy(f => f.SlotIndex)
                .ThenBy(f => f.PartId, StringComparer.Ordinal).ThenBy(f => f.SpriteId, StringComparer.Ordinal).ToList(), anchors, bounds);
        }

        static void AppendFrameHull(VisualPart frame, List<AssemblyVisualFragment> output, string hullId)
        {
            var f = frame.fragments[0]; var trim = f.trim.Rect;
            // The frame art is split into base, rear roof and foreground wall. This is an
            // authored occlusion split, not a whole concept illustration used as a part.
            float bottom = frame.hullBaseFraction > 0 ? frame.hullBaseFraction : .18f;
            float top = frame.hullFrontFraction > 0 ? frame.hullFrontFraction : .32f;
            foreach (var split in new[] { (bottom: top, height: 1 - top, layer: 0), (bottom, height: top - bottom, layer: 14) })
            {
                var region = new Rect(trim.x, trim.y + trim.height * split.bottom, trim.width, trim.height * split.height);
                output.Add(new AssemblyVisualFragment(hullId, f.spriteId, -1, split.layer, trim, region,
                    f.position - NotebookMountLayout.Rotate(f.attach * f.scale, f.radians), f.scale, f.radians));
            }
        }
        static void Append(VisualPart part, int slot, Vector2 root, float radians, float recoil,
            List<AssemblyVisualFragment> output, AssemblyStructureFamily family = AssemblyStructureFamily.None)
        {
            foreach (var f in part.fragments)
            {
                if (f.family != AssemblyStructureFamily.None && f.family != family) continue;
                float angle = f.motion == "fixed" ? 0 : radians;
                var local = f.position - NotebookMountLayout.Rotate(f.attach * f.scale, f.radians);
                var origin = root + NotebookMountLayout.Rotate(local, angle);
                if (f.motion == "recoiling") origin += NotebookMountLayout.Rotate(Vector2.up * recoil, angle);
                output.Add(new AssemblyVisualFragment(part.partId, f.spriteId, slot, f.layer, f.trim.Rect,
                    f.region?.Rect ?? f.trim.Rect, origin, f.scale, angle + f.radians));
            }
        }
        static AssemblyVisualComposition Finish(List<AssemblyVisualFragment> fragments, List<AssemblyVisualAnchor> anchors)
        {
            fragments = fragments.OrderBy(f => f.Layer).ThenBy(f => f.SlotIndex).ThenBy(f => f.PartId, StringComparer.Ordinal)
                .ThenBy(f => f.SpriteId, StringComparer.Ordinal).ToList();
            bool first = true; Rect bounds = default;
            foreach (var f in fragments) foreach (var p in Corners(f.Region))
            { var b = new Rect(f.Transform(p), Vector2.zero); bounds = first ? b : Union(bounds, b); first = false; }
            return new AssemblyVisualComposition(Revision, fragments, anchors, bounds);
        }
        static VisualPart FindPart(Manifest data, PartRef reference)
            => data.visualParts?.SingleOrDefault(p => p.partId == reference.Id && p.revision == reference.Revision);
        static VisualPart RequiredPart(PartRef reference)
            => FindPart(manifest, reference) ?? throw new FormatException("Missing visual part: " + reference.Id);
        static bool SingleFamily(AssemblyStructureFamily value) => value != 0 && ((int)value & ((int)value - 1)) == 0;

        static void PrepareFullCatalog(Manifest data, ISet<string> spriteIds, IReadOnlyList<ComponentDefinition> authoritativeDefinitions)
        {
            if (data.visualParts == null) return; // Historical pilot fixtures remain a valid input format.
            if (!Finite(data.modernPivot.x) || !Finite(data.modernPivot.y) || data.modernPivot.y < 0)
                throw new FormatException("Invalid authored modern pivot.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var part in data.visualParts)
            {
                if (part == null || string.IsNullOrWhiteSpace(part.partId) || !keys.Add(part.partId + "@" + part.revision) || part.revision < 1
                    || part.assetRevision != data.revision || part.fragments == null
                    || !Enum.IsDefined(typeof(ComponentCategory), part.category)) throw new FormatException("Invalid visual part revision or identity.");
                if (!string.IsNullOrEmpty(part.neutral))
                {
                    if ((part.partId != "armor.none" && part.partId != "utility.none") || part.fragments.Length != 0)
                        throw new FormatException("Only explicitly neutral armor/equipment may omit a sprite.");
                    continue;
                }
                if (part.fragments.Length == 0) throw new FormatException("Visible visual part has no fragments: " + part.partId);
                var sockets = new HashSet<string>(StringComparer.Ordinal);
                foreach (var f in part.fragments)
                {
                    if (f == null || string.IsNullOrWhiteSpace(f.socketId) || !sockets.Add(f.socketId)
                        || string.IsNullOrWhiteSpace(f.spriteId) || spriteIds != null && !spriteIds.Contains(f.spriteId)
                        || f.trim == null || f.canvas.x <= 0 || f.canvas.y <= 0 || f.scale <= 0
                        || !Finite(f.canvas.x) || !Finite(f.canvas.y) || !Finite(f.trim.x) || !Finite(f.trim.y)
                        || !Finite(f.trim.width) || !Finite(f.trim.height) || f.layer < 0 || f.layer > 32
                        || !Finite(f.scale) || !Finite(f.radians) || !Finite(f.position.x) || !Finite(f.position.y)
                        || !Finite(f.attach.x) || !Finite(f.attach.y) || f.attach.x < 0 || f.attach.y < 0
                        || f.attach.x > f.canvas.x || f.attach.y > f.canvas.y
                        || f.trim.x < 0 || f.trim.y < 0 || f.trim.width <= 0 || f.trim.height <= 0
                        || f.trim.x + f.trim.width > f.canvas.x + .001f || f.trim.y + f.trim.height > f.canvas.y + .001f
                        || f.motion != "fixed" && f.motion != "rotating" && f.motion != "recoiling"
                        || f.collisionMode != "solid" && f.collisionMode != "mount-contact" && f.collisionMode != "armor")
                        throw new FormatException("Invalid visual fragment: " + part.partId);
                    var region = f.region?.Rect ?? f.trim.Rect;
                    if (!Finite(region.x) || !Finite(region.y) || !Finite(region.width) || !Finite(region.height)
                        || region.width <= 0 || region.height <= 0 || region.xMin < f.trim.x || region.yMin < f.trim.y
                        || region.xMax > f.trim.x + f.trim.width + .001f || region.yMax > f.trim.y + f.trim.height + .001f)
                        throw new FormatException("Occupied region lies outside the source trim: " + part.partId);
                    if (f.hasMuzzle && (!Finite(f.muzzle.x) || !Finite(f.muzzle.y) || f.muzzle.x < f.trim.x || f.muzzle.y < f.trim.y
                        || f.muzzle.x > f.trim.x + f.trim.width || f.muzzle.y > f.trim.y + f.trim.height))
                        throw new FormatException("Muzzle lies outside the actual head canvas.");
                    if (f.hasMuzzle)
                    {
                        var axis = f.position + NotebookMountLayout.Rotate((f.muzzle - f.attach) * f.scale, f.radians);
                        if (Mathf.Abs(axis.x) > .00001f || axis.y <= .01f) throw new FormatException("Rest muzzle must define the authored +Y exit axis.");
                    }
                }
                if (part.category == ComponentCategory.Head && part.fragments.Count(f => f.hasMuzzle) != 1)
                    throw new FormatException("Every head requires one authored muzzle: " + part.partId);
            }
            if (data.visualParts.Length != 54) throw new FormatException("The production visual catalog requires all 54 definitions.");
            if (data.fits.Parts == null || data.fits.Parts.Length != data.visualParts.Length)
                throw new FormatException("Visual and physical part coverage disagree.");
            var supplied = new HashSet<string>(StringComparer.Ordinal);
            foreach (var part in data.fits.Parts)
            {
                if (part == null || !supplied.Add(part.Id + "@" + part.Revision)) throw new FormatException("Duplicate physical part identity.");
                var visual = FindPart(data, new PartRef(part.Id, part.Revision));
                string socket = part.Category == ComponentCategory.Mechanism ? "gun.standard" : "assembly." + part.Category;
                if (visual == null || part.Category != visual.category || part.AssetRevision != visual.assetRevision
                    || part.SocketSchemaVersion != 1 || part.AttachType != socket || part.ExpectedSocketType != socket)
                    throw new FormatException("Supplied physical socket or revision disagrees with the visual part: " + part.Id);
            }
            if (authoritativeDefinitions != null)
            {
                if (authoritativeDefinitions.Count != data.visualParts.Length) throw new FormatException("Production definition coverage differs from the visual catalog.");
                foreach (var definition in authoritativeDefinitions)
                {
                    var visual = FindPart(data, new PartRef(definition.Id, definition.Revision));
                    if (visual == null || visual.category != definition.Category || visual.weaponFamilies != definition.WeaponFamilies
                        || visual.structureFamilies != definition.StructureFamilies)
                        throw new FormatException("Unknown production visual identity, revision or category: " + definition.Id + "@" + definition.Revision);
                }
            }
            foreach (var frame in data.fits.Frames)
            {
                // Non-standard frames render their own authored wall/roof split; a copy
                // of the standard frame's front wall would certify different artwork.
                // Derive their base and foreground from the exact drawing fragments.
                var visualFrame = FindPart(data, new PartRef(frame.FrameId, frame.Revision))
                    ?? throw new FormatException("Unknown visual frame revision: " + frame.FrameId);
                bool authored = visualFrame.mounts != null;
                if (authored)
                {
                    if (visualFrame.mounts.Length < 1 || visualFrame.mounts.Length > 3 || visualFrame.mounts.Any(m => m == null)
                        || visualFrame.mounts.Select(m => m.slot).Distinct().Count() != visualFrame.mounts.Length
                        || frame.Mounts.Any(m => m.SocketType != "gun.standard" || m.SocketId != "mount." + m.SlotIndex))
                        throw new FormatException("Invalid authored frame sockets: " + frame.FrameId);
                    var transform = visualFrame.fragments[0];
                    frame.Mounts = visualFrame.mounts.Select(m => {
                        if (m.slot < 0 || m.slot > 2 || !Finite(m.position.x) || !Finite(m.position.y)
                            || m.position.x < 0 || m.position.x > transform.canvas.x || m.position.y < 0 || m.position.y > transform.canvas.y)
                            throw new FormatException("Frame socket lies outside the authored canvas: " + frame.FrameId);
                        var p = transform.position + NotebookMountLayout.Rotate((m.position - transform.attach) * transform.scale, transform.radians);
                        return new AssemblyFitMountInput { SlotIndex = m.slot, SocketId = "mount." + m.slot, SocketType = "gun.standard", X = p.x, Y = p.y };
                    }).ToArray();
                }
                var keepouts = frame.FrameId == "frame.standard" && !authored
                    ? (frame.Keepouts ?? Array.Empty<AssemblyFitKeepoutInput>()).Where(k => string.IsNullOrEmpty(k.PartId)).ToList()
                    : new List<AssemblyFitKeepoutInput>();
                if (frame.FrameId != "frame.standard" || authored)
                {
                    foreach (var fragment in visualFrame.fragments.Where(f => f.collisionMode == "solid"))
                        keepouts.Add(Keepout(Region(fragment).Points.Select(p => new Vector2(p.X, p.Y)), frame.FrameId));
                    // Every selected hull retains this frame's real roof/front.
                    // Give its foreground the selected owner in both drawing and
                    // physical data; an unselected standard hull is never implied.
                    foreach (var selectedHull in data.visualParts.Where(p => p.category == ComponentCategory.Hull))
                    {
                        var hull = new List<AssemblyVisualFragment>(); AppendFrameHull(visualFrame, hull, selectedHull.partId);
                        foreach (var fragment in hull.Where(f => f.Layer == 14))
                            keepouts.Add(Keepout(Corners(fragment.Region).Select(fragment.Transform), selectedHull.partId));
                    }
                }
                foreach (var part in data.visualParts.Where(p => p.category >= ComponentCategory.Hull && p.category <= ComponentCategory.Feed))
                {
                    if (part.partId == "hull.standard" && (frame.FrameId != "frame.standard" || authored)) continue;
                    foreach (var fragment in part.fragments.Where(f => f.collisionMode == "solid" || f.collisionMode == "armor"))
                        keepouts.Add(Keepout(Region(fragment).Points.Select(p => new Vector2(p.X, p.Y)), part.partId));
                }
                frame.Keepouts = keepouts.ToArray();
            }
            var weapons = new List<AssemblyFitWeaponInput>();
            var mountingHeight = data.fits.Weapons.Single(w => w.MechanismId == "core.gun");
            foreach (var core in data.visualParts.Where(p => p.category == ComponentCategory.Mechanism))
            {
                var existing = data.fits.Weapons.SingleOrDefault(w => w.MechanismId == core.partId);
                var compatible = data.visualParts.Where(p => p.category >= ComponentCategory.Mechanism && p.category <= ComponentCategory.Trigger
                    && (p.weaponFamilies & core.weaponFamilies) != 0).ToArray();
                string family = core.partId.Substring("core.".Length);
                var standards = new[] { core.partId, "head." + family, "sight.iron", "magazine.standard", "trigger.standard" };
                var regions = new List<AssemblyFitRegionInput>();
                // Include every compatible variant. This conservative envelope certifies independent
                // rotations without accepting a collision merely because the standard barrel fits.
                foreach (var p in compatible)
                foreach (var f in p.fragments)
                    regions.Add(Region(f));
                foreach (var f in data.visualParts.Where(p => p.category == ComponentCategory.Turn).SelectMany(p => p.fragments)) regions.Add(Region(f));
                weapons.Add(new AssemblyFitWeaponInput { MechanismId = core.partId, Revision = core.revision,
                    MountType = "gun.standard", RequiredParts = standards.Select(id => new PartRefInput { Id = id, Revision = 1 }).ToArray(),
                    CompatibleParts = compatible.Select(p => new PartRefInput { Id = p.partId, Revision = p.revision }).ToArray(),
                    PivotX = data.modernPivot.y > 0 ? data.modernPivot.x : existing?.PivotX ?? mountingHeight.PivotX,
                    PivotY = data.modernPivot.y > 0 ? data.modernPivot.y : existing?.PivotY ?? mountingHeight.PivotY,
                    MinRadians = existing?.MinRadians ?? -Mathf.PI, MaxRadians = existing?.MaxRadians ?? Mathf.PI,
                    RecoilLimit = existing?.RecoilLimit ?? .085f, Clearance = .01f, Regions = regions.ToArray() });
            }
            data.fits.Weapons = weapons.ToArray();
            var structures = new List<AssemblyFitStructureInput>();
            foreach (var family in new[] { AssemblyStructureFamily.Wall, AssemblyStructureFamily.Slow, AssemblyStructureFamily.Repair, AssemblyStructureFamily.Generator })
            {
                string suffix = family.ToString().ToLowerInvariant();
                var eligible = data.visualParts.Where(p => (p.structureFamilies & family) != 0).ToArray();
                structures.Add(new AssemblyFitStructureInput { Family = family,
                    RequiredParts = new[] { "site." + suffix, "function." + suffix, "shell.standard", "utility.none" }
                        .Select(id => new PartRefInput { Id = id, Revision = 1 }).ToArray(),
                    CompatibleParts = eligible.Select(p => new PartRefInput { Id = p.partId, Revision = p.revision }).ToArray(),
                    Regions = eligible.SelectMany(p => p.fragments).Where(f => f.family == 0 || f.family == family).Select(Region).ToArray() });
            }
            data.fits.Structures = structures.ToArray();
        }
        static AssemblyFitKeepoutInput Keepout(IEnumerable<Vector2> points, string partId)
        {
            var corners = points.ToArray(); float x = corners.Min(p => p.x), y = corners.Min(p => p.y);
            return new AssemblyFitKeepoutInput { PartId = partId, X = x, Y = y,
                Width = corners.Max(p => p.x) - x, Height = corners.Max(p => p.y) - y };
        }
        static AssemblyFitRegionInput Region(VisualPartFragment fragment)
        {
            var r = fragment.region?.Rect ?? fragment.trim.Rect;
            return new AssemblyFitRegionInput { Motion = fragment.motion,
                Points = Corners(r).Select(p => {
                    var v = fragment.position + NotebookMountLayout.Rotate((p - fragment.attach) * fragment.scale, fragment.radians);
                    return new AssemblyFitPointInput { X = v.x, Y = v.y };
                }).ToArray() };
        }
    }
}
