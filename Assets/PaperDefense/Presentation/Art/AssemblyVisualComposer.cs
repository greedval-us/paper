using System;
using System.Collections.Generic;
using System.Linq;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Observed mechanical work only; it never changes recipe identity or fit anchors.</summary>
    public readonly struct AssemblyMechanicsPose
    {
        public readonly bool Reloading;
        public readonly float Reload, Fire, Activity, Hit;
        public AssemblyMechanicsPose(bool reloading = false, float reload = 0, float fire = 0, float activity = 0, float hit = 0)
        { Reloading = reloading; Reload = Unit(reload); Fire = Unit(fire); Activity = Unit(activity); Hit = Unit(hit); }
        static float Unit(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp01(value);
        internal AssemblyMechanicsPose Overlay(AssemblyMechanicsPose value)
            => new AssemblyMechanicsPose(Reloading, Reload, Fire, Mathf.Max(Activity, value.Activity), Mathf.Max(Hit, value.Hit));
    }

    public readonly struct AssemblyMountPose
    {
        public readonly int SlotIndex;
        public readonly float Radians, Recoil;
        public readonly AssemblyMechanicsPose Mechanics;
        public AssemblyMountPose(int slotIndex, float radians, float recoil = 0, AssemblyMechanicsPose mechanics = default)
        { SlotIndex = slotIndex; Radians = radians; Recoil = recoil; Mechanics = mechanics; }
    }
    /// <summary>Completed cosmetic equipment only; never participates in recipe resolution or mechanical fits.</summary>
    public readonly struct AssemblyDevelopmentPose
    {
        public readonly int Power, Mechanics, Range, Resilience, ColorRank;
        public readonly InkColor Color;
        public readonly Color WeaponInk;
        public AssemblyDevelopmentPose(int power = 0, int mechanics = 0, int range = 0, int resilience = 0,
            InkColor color = InkColor.None, int colorRank = 0, Color? weaponInk = null)
        {
            Power = Mathf.Clamp(power, 0, 3); Mechanics = Mathf.Clamp(mechanics, 0, 3);
            Range = Mathf.Clamp(range, 0, 3); Resilience = Mathf.Clamp(resilience, 0, 3);
            ColorRank = color == InkColor.None ? 0 : Mathf.Clamp(colorRank, 0, 3);
            Color = ColorRank > 0 ? color : InkColor.None;
            WeaponInk = weaponInk ?? NightNotebookEffectPalette.ColorInk(Color);
        }
        internal AssemblyDevelopmentPose ForWeapon(AssemblyWeaponFamily family)
            => family == AssemblyWeaponFamily.None || family == AssemblyWeaponFamily.Blotter
                ? new AssemblyDevelopmentPose(mechanics: Mechanics, range: Range, resilience: Resilience)
                : this;
    }
    public sealed class AssemblyVisualPose
    {
        public string VisualRevision { get; }
        public IReadOnlyList<AssemblyMountPose> Mounts { get; }
        public AssemblyDevelopmentPose Development { get; }
        public AssemblyVisualPose(IEnumerable<AssemblyMountPose> mounts = null, string visualRevision = null,
            AssemblyDevelopmentPose development = default)
        {
            VisualRevision = visualRevision;
            Development = development;
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
        public readonly AssemblyMechanicsPose Mechanics;
        public readonly AssemblyWeaponFamily WeaponFamily;
        internal AssemblyVisualFragment(string part, string sprite, int slot, int layer, Rect trim, Rect region, Vector2 origin, float scale, float radians,
            AssemblyMechanicsPose mechanics = default, AssemblyWeaponFamily weaponFamily = AssemblyWeaponFamily.None)
        { PartId = part; SpriteId = sprite; SlotIndex = slot; Layer = layer; Trim = trim; Region = region;
            Origin = origin; Scale = scale; Radians = radians; Mechanics = mechanics; WeaponFamily = weaponFamily; }
        public Vector2 Transform(Vector2 pixel) => Origin + NotebookMountLayout.Rotate(pixel * Scale, Radians);
        internal void Draw(MeshPen pen, Vector2 at, float opacity, Color tint, float uniformScale = 1,
            AssemblyMechanicsPose? overlay = null, AssemblyDevelopmentPose development = default)
        {
            NightNotebookArt.DrawPart(this, pen, at, opacity, tint, uniformScale, overlay.HasValue ? Mechanics.Overlay(overlay.Value) : Mechanics);
            NightNotebookArt.DrawDevelopment(this, development, pen, at, opacity, tint, uniformScale);
        }
    }
    public sealed class AssemblyVisualComposition
    {
        public string Revision { get; }
        public IReadOnlyList<AssemblyVisualFragment> Fragments { get; }
        public IReadOnlyList<AssemblyVisualAnchor> Anchors { get; }
        public Rect Bounds { get; }
        public AssemblyDevelopmentPose Development { get; }
        internal AssemblyVisualComposition(string revision, List<AssemblyVisualFragment> fragments, List<AssemblyVisualAnchor> anchors,
            Rect bounds, AssemblyDevelopmentPose development = default)
        { Revision = revision; Fragments = fragments.AsReadOnly(); Anchors = anchors.AsReadOnly(); Bounds = bounds; Development = development; }
        public void Draw(MeshPen pen, Vector2 at, float opacity = 1, Color? tint = null, float uniformScale = 1,
            AssemblyMechanicsPose? mechanics = null, float construction = 1)
        {
            if (float.IsNaN(uniformScale) || float.IsInfinity(uniformScale) || uniformScale <= 0)
                throw new ArgumentOutOfRangeException(nameof(uniformScale));
            foreach (var f in Fragments)
            {
                float reveal = ConstructionVisibility(f, construction);
                if (reveal > 0) f.Draw(pen, at, opacity * reveal, tint ?? Color.white, uniformScale, mechanics, Development);
            }
        }
        static float ConstructionVisibility(AssemblyVisualFragment fragment, float progress)
        {
            if (progress >= 1) return 1;
            int category = fragment.SlotIndex < 0 ? fragment.Layer : fragment.Layer == 5 ? (int)ComponentCategory.Turn : fragment.Layer - 6;
            float start = category == (int)ComponentCategory.Frame || category == (int)ComponentCategory.Site ? 0 :
                category == (int)ComponentCategory.Hull || category == (int)ComponentCategory.Shell ? .12f :
                category == (int)ComponentCategory.Armor || category == (int)ComponentCategory.Feed ? .28f :
                category == (int)ComponentCategory.Turn || category == (int)ComponentCategory.Mount ? .40f :
                category == (int)ComponentCategory.Mechanism || category == (int)ComponentCategory.Function ? .52f :
                category == (int)ComponentCategory.Head ? .68f : .80f;
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, Mathf.Min(1, start + .20f), progress));
        }
    }

    /// <summary>Shared notebook drawings. Mechanical fits remain the authority for
    /// socket positions, admissible recipes and motion limits; no art manifest is loaded.</summary>
    public static partial class AssemblyVisualComposer
    {
        static AssemblyFitCatalog fits;
        public static AssemblyFitCatalog Fits => fits ?? (fits = PaperDefense.Application.AssemblyFitResources.Load());
        public static string Revision => NightNotebookArt.Revision;

        public static AssemblyVisualComposition Compose(RecipeSnapshot snapshot, AssemblyVisualPose pose)
            => ComposePlaced(snapshot, pose, 0);

        internal static AssemblyVisualComposition ComposePlaced(RecipeSnapshot snapshot, AssemblyVisualPose pose, float bodyRadians)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            pose = pose ?? new AssemblyVisualPose();
            if (pose.VisualRevision != null && pose.VisualRevision != Revision) throw new FormatException("Unknown assembly visual revision: " + pose.VisualRevision);
            var issues = AssemblyFitRules.Validate(snapshot.Recipe, Fits);
            if (issues.Count != 0) throw new AssemblyCatalogException(issues);
            var fragments = new List<AssemblyVisualFragment>(); var anchors = new List<AssemblyVisualAnchor>();
            if (snapshot.Recipe.Structure != null)
            {
                if (pose.Mounts.Count > 0) throw new ArgumentException("A support structure has no weapon sockets.", nameof(pose));
                AppendStructure(snapshot.Recipe.Structure, fragments);
            }
            else
            {
                var tower = snapshot.Recipe.Tower;
                foreach (var motion in pose.Mounts)
                    if (!tower.Mounts.Any(m => m.SlotIndex == motion.SlotIndex)) throw new ArgumentException("Pose references an unoccupied socket.");
                AppendFoundation(tower.Foundation, fragments, false);
                Fits.TryGetFrame(tower.Foundation.Frame.Id, tower.Foundation.Frame.Revision, out var frame);
                foreach (var selected in tower.Mounts)
                {
                    var socket = frame.Mounts.Single(m => m.SlotIndex == selected.SlotIndex);
                    Fits.TryGetWeapon(selected.Weapon.Mechanism.Id, selected.Weapon.Mechanism.Revision, out var weapon);
                    var root = new Vector2(socket.Position.X + weapon.Pivot.X, socket.Position.Y + weapon.Pivot.Y);
                    var mechanism = snapshot.Definitions.Single(d => d.Id == selected.Weapon.Mechanism.Id && d.Revision == selected.Weapon.Mechanism.Revision);
                    AppendWeapon(selected.Weapon, selected.SlotIndex, root, pose.For(selected.SlotIndex), fragments, anchors,
                        tower.Foundation.Turn, FoundationSupportY(fragments, root.x), mechanism.WeaponFamilies);
                }
                // Weapon poses are relative to the chassis. Turn the complete drawing around
                // its cell, including its off-centre sockets and the projectile launch anchors.
                if (bodyRadians != 0)
                {
                    for (int i = 0; i < fragments.Count; i++)
                    {
                        var f = fragments[i];
                        fragments[i] = new AssemblyVisualFragment(f.PartId, f.SpriteId, f.SlotIndex, f.Layer, f.Trim,
                            f.Region, NotebookMountLayout.Rotate(f.Origin, bodyRadians), f.Scale, f.Radians + bodyRadians,
                            f.Mechanics, f.WeaponFamily);
                    }
                    for (int i = 0; i < anchors.Count; i++)
                    {
                        var a = anchors[i];
                        anchors[i] = new AssemblyVisualAnchor(a.SlotIndex, NotebookMountLayout.Rotate(a.Attach, bodyRadians),
                            NotebookMountLayout.Rotate(a.RotationCenter, bodyRadians), NotebookMountLayout.Rotate(a.Muzzle, bodyRadians));
                    }
                }
            }
            return Finish(fragments, anchors, pose.Development);
        }

        public static bool HasVisualMapping(PartRef reference) => Fits.TryGetPart(reference.Id, reference.Revision, out _);
        public static AssemblyVisualComposition ComposePart(PartRef reference) => ComposePart(reference, AssemblyStructureFamily.None);
        public static AssemblyVisualComposition ComposePart(PartRef reference, AssemblyStructureFamily family)
        {
            var part = RequiredPart(reference);
            var fragments = new List<AssemblyVisualFragment>();
            if (!IsEmpty(reference)) Add(fragments, reference, -1, (int)part.Category,
                PartRect(part.Category), Vector2.zero, 0);
            return Finish(fragments, new List<AssemblyVisualAnchor>());
        }
        public static AssemblyVisualComposition ComposeFoundation(FoundationRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            var fragments = new List<AssemblyVisualFragment>(); AppendFoundation(recipe, fragments, true);
            return Finish(fragments, new List<AssemblyVisualAnchor>());
        }
        public static AssemblyVisualComposition ComposeWeapon(WeaponRecipe recipe) => ComposeWeapon(recipe, new AssemblyVisualPose());
        public static AssemblyVisualComposition ComposeWeapon(WeaponRecipe recipe, AssemblyVisualPose pose, ComponentDefinition mechanism = null)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (mechanism != null && (mechanism.Id != recipe.Mechanism.Id || mechanism.Revision != recipe.Mechanism.Revision
                || mechanism.Category != ComponentCategory.Mechanism)) throw new ArgumentException("The mechanism definition must belong to this weapon.", nameof(mechanism));
            pose = pose ?? new AssemblyVisualPose();
            if (pose.Mounts.Any(m => m.SlotIndex != 0)) throw new ArgumentException("A standalone weapon has only socket zero.", nameof(pose));
            var fragments = new List<AssemblyVisualFragment>(); var anchors = new List<AssemblyVisualAnchor>();
            AppendWeapon(recipe, 0, Vector2.zero, pose.For(0), fragments, anchors, P("turn.standard"),
                weaponFamily: mechanism?.WeaponFamilies ?? AssemblyWeaponFamily.None);
            return Finish(fragments, anchors, pose.Development);
        }

        static void AppendFoundation(FoundationRecipe recipe, List<AssemblyVisualFragment> fragments, bool vacantMounts)
        {
            if (!Fits.TryGetFrame(recipe.Frame.Id, recipe.Frame.Revision, out var frame)) throw new ArgumentException("Unknown foundation frame.");
            int category = 0;
            foreach (var reference in recipe.Parts)
            {
                var part = RequiredPart(reference);
                if (part.Category != (ComponentCategory)category++) throw new ArgumentException("A foundation part uses the wrong socket category.");
                if (part.Category == ComponentCategory.Turn || IsEmpty(reference)) continue;
                bool any = false;
                foreach (var region in frame.Keepouts)
                {
                    if (region.PartId != reference.Id) continue;
                    Add(fragments, reference, -1, (int)part.Category,
                        new Rect(region.X, region.Y, region.Width, region.Height), Vector2.zero, 0);
                    any = true;
                }
                if (!any) Add(fragments, reference, -1, (int)part.Category, PartRect(part.Category), Vector2.zero, 0);
            }
            if (vacantMounts)
            {
                foreach (var socket in frame.Mounts)
                    AddTurn(fragments, recipe.Turn, socket.SlotIndex,
                        new Vector2(socket.Position.X, socket.Position.Y + .158f), FoundationSupportY(fragments, socket.Position.X));
            }
        }

        static void AppendWeapon(WeaponRecipe recipe, int slot, Vector2 root, AssemblyMountPose motion,
            List<AssemblyVisualFragment> fragments, List<AssemblyVisualAnchor> anchors, PartRef turn, float? supportY = null,
            AssemblyWeaponFamily weaponFamily = AssemblyWeaponFamily.None)
        {
            if (!Fits.TryGetWeapon(recipe.Mechanism.Id, recipe.Mechanism.Revision, out var weapon)) throw new ArgumentException("Unknown weapon mechanism.");
            if (motion.Radians < weapon.MinRadians - .00001f || motion.Radians > weapon.MaxRadians + .00001f
                || motion.Recoil > 0 || motion.Recoil < -weapon.RecoilLimit - .00001f)
                throw new ArgumentOutOfRangeException(nameof(motion), "Pose exceeds mechanical angle or recoil range.");
            AddTurn(fragments, turn, slot, root, supportY, weaponFamily);
            foreach (var reference in recipe.Parts)
            {
                var part = RequiredPart(reference);
                if (IsEmpty(reference)) continue;
                var at = root + NotebookMountLayout.Rotate(Vector2.up * (part.Category == ComponentCategory.Head ? motion.Recoil : 0), motion.Radians);
                Add(fragments, reference, slot, 6 + (int)part.Category, PartRect(part.Category), at, motion.Radians, motion.Mechanics, weaponFamily);
            }
            Vector2 attach = root - new Vector2(weapon.Pivot.X, weapon.Pivot.Y);
            anchors.Add(new AssemblyVisualAnchor(slot, attach, root,
                root + NotebookMountLayout.Rotate(new Vector2(0, .30f + motion.Recoil), motion.Radians)));
        }

        static void AppendStructure(StructureRecipe recipe, List<AssemblyVisualFragment> fragments)
        {
            foreach (var reference in recipe.Parts)
            {
                var part = RequiredPart(reference);
                if (IsEmpty(reference)) continue;
                // Optional equipment bolts to the right foot, leaving the working head visible.
                var mount = part.Category == ComponentCategory.Utility ? new Vector2(.23f, -.09f) : Vector2.zero;
                Add(fragments, reference, -1, (int)part.Category, PartRect(part.Category), mount, 0);
            }
        }
        static AssemblyFitPart RequiredPart(PartRef reference)
        {
            if (!Fits.TryGetPart(reference.Id, reference.Revision, out var part)) throw new ArgumentException("Unknown component: " + reference.Id);
            return part;
        }
        static bool IsEmpty(PartRef reference) => reference.Id.EndsWith(".none", StringComparison.Ordinal);
        static Rect PartRect(ComponentCategory category)
        {
            switch (category)
            {
                case ComponentCategory.Frame: return new Rect(-.45f, -.3f, .9f, .12f);
                case ComponentCategory.Hull: return new Rect(-.38f, -.22f, .76f, .2f);
                case ComponentCategory.Armor: return new Rect(-.2f, -.22f, .4f, .12f);
                case ComponentCategory.Feed: return new Rect(-.05f, -.22f, .1f, .13f);
                case ComponentCategory.Turn: case ComponentCategory.Mount: return new Rect(-.065f, -.065f, .13f, .13f);
                case ComponentCategory.Mechanism: return new Rect(-.09f, -.07f, .18f, .16f);
                case ComponentCategory.Head: return new Rect(-.035f, .04f, .07f, .26f);
                case ComponentCategory.Sight: return new Rect(-.13f, .03f, .055f, .055f);
                case ComponentCategory.Magazine: return new Rect(-.12f, -.09f, .09f, .08f);
                case ComponentCategory.Trigger: return new Rect(.07f, -.04f, .04f, .07f);
                case ComponentCategory.Site: return new Rect(-.4f, -.35f, .8f, .13f);
                case ComponentCategory.Function: return new Rect(-.25f, -.2f, .5f, .45f);
                case ComponentCategory.Shell: return new Rect(-.3f, -.25f, .6f, .11f);
                default: return new Rect(-.12f, -.12f, .24f, .24f);
            }
        }
        static void Add(List<AssemblyVisualFragment> fragments, PartRef part, int slot, int layer, Rect area, Vector2 root, float radians,
            AssemblyMechanicsPose mechanics = default, AssemblyWeaponFamily weaponFamily = AssemblyWeaponFamily.None)
            => fragments.Add(new AssemblyVisualFragment(part.Id, "notebook." + part.Id, slot, layer, area, area, root, 1, radians, mechanics, weaponFamily));
        static float? FoundationSupportY(List<AssemblyVisualFragment> fragments, float socketX)
        {
            float? top = null;
            // Hull regions are in chassis space here. A raised central housing
            // does not support an outboard socket beside it.
            foreach (var fragment in fragments)
            {
                var region = fragment.Region;
                if (fragment.SlotIndex >= 0 || fragment.Layer != (int)ComponentCategory.Hull ||
                    socketX < region.xMin || socketX > region.xMax) continue;
                float y = fragment.PartId == "hull.ribbed" ? region.yMin + region.height * NightNotebookArt.RibbedDeckTop : region.yMax;
                top = top.HasValue ? Mathf.Max(top.Value, y) : y;
            }
            return top;
        }
        static void AddTurn(List<AssemblyVisualFragment> fragments, PartRef part, int slot, Vector2 root, float? supportY,
            AssemblyWeaponFamily weaponFamily = AssemblyWeaponFamily.None)
        {
            var hub = new Rect(-.065f, -.065f, .13f, .13f);
            float bottom = supportY.HasValue ? Mathf.Min(hub.yMin, supportY.Value - root.y) : hub.yMin;
            var region = Rect.MinMaxRect(hub.xMin, bottom, hub.xMax, hub.yMax);
            // The stem shares its hub's visual fragment; no socket, layer, trim or mechanical anchor changes.
            fragments.Add(new AssemblyVisualFragment(part.Id, "notebook." + part.Id, slot, 5, hub, region, root, 1, 0,
                weaponFamily: weaponFamily));
        }
        static AssemblyVisualComposition Finish(List<AssemblyVisualFragment> fragments, List<AssemblyVisualAnchor> anchors,
            AssemblyDevelopmentPose development = default)
        {
            fragments = fragments.OrderBy(f => f.Layer).ThenBy(f => f.SlotIndex).ToList();
            bool any = false; Vector2 low = Vector2.zero, high = Vector2.zero;
            foreach (var fragment in fragments)
                foreach (var corner in Corners(NightNotebookArt.DevelopmentBounds(fragment, development)))
                {
                    Vector2 point = fragment.Transform(corner);
                    if (!any) { low = high = point; any = true; }
                    else { low = Vector2.Min(low, point); high = Vector2.Max(high, point); }
                }
            return new AssemblyVisualComposition(Revision, fragments, anchors,
                any ? Rect.MinMaxRect(low.x, low.y, high.x, high.y) : default, development);
        }
        static IEnumerable<Vector2> Corners(Rect r)
        { yield return r.min; yield return new Vector2(r.xMax, r.yMin); yield return r.max; yield return new Vector2(r.xMin, r.yMax); }
        static PartRef P(string id) => new PartRef(id, 1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetCache() { fits = null; }
    }
}
