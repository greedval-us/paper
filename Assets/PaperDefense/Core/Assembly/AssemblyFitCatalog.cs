using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    [Serializable] public sealed class AssemblyFitCatalogInput
    {
        public int SchemaVersion = 1;
        public string Revision;
        public AssemblyFitPartInput[] Parts = Array.Empty<AssemblyFitPartInput>();
        public AssemblyFitFrameInput[] Frames = Array.Empty<AssemblyFitFrameInput>();
        public AssemblyFitWeaponInput[] Weapons = Array.Empty<AssemblyFitWeaponInput>();
    }
    [Serializable] public sealed class AssemblyFitPartInput
    {
        public string Id, AssetRevision, AttachType, ExpectedSocketType;
        public int Revision = 1, SocketSchemaVersion = 1;
        public ComponentCategory Category;
    }
    [Serializable] public sealed class AssemblyFitFrameInput
    {
        public string FrameId;
        public int Revision = 1;
        public AssemblyFitMountInput[] Mounts = Array.Empty<AssemblyFitMountInput>();
        public AssemblyFitKeepoutInput[] Keepouts = Array.Empty<AssemblyFitKeepoutInput>();
    }
    [Serializable] public sealed class AssemblyFitMountInput
    {
        public int SlotIndex;
        public string SocketId, SocketType;
        public float X, Y;
    }
    [Serializable] public sealed class AssemblyFitKeepoutInput
    {
        public float X, Y, Width, Height;
        public bool ArmorOnly;
    }
    [Serializable] public sealed class AssemblyFitWeaponInput
    {
        public string MechanismId, MountType = "gun.standard";
        public int Revision = 1;
        public PartRefInput[] RequiredParts = Array.Empty<PartRefInput>();
        public float PivotX, PivotY, MinRadians = -(float)Math.PI, MaxRadians = (float)Math.PI;
        public float RecoilLimit = .085f, Clearance = .01f;
        public AssemblyFitRegionInput[] Regions = Array.Empty<AssemblyFitRegionInput>();
    }
    [Serializable] public sealed class AssemblyFitRegionInput
    {
        public string Motion;
        public AssemblyFitPointInput[] Points = Array.Empty<AssemblyFitPointInput>();
    }
    [Serializable] public sealed class AssemblyFitPointInput { public float X, Y; }

    public sealed class AssemblyFitPart
    {
        public string Id { get; } public int Revision { get; } public string AssetRevision { get; }
        public int SocketSchemaVersion { get; } public ComponentCategory Category { get; }
        public string AttachType { get; } public string ExpectedSocketType { get; }
        internal AssemblyFitPart(AssemblyFitPartInput p)
        { Id = p.Id; Revision = p.Revision; AssetRevision = p.AssetRevision; SocketSchemaVersion = p.SocketSchemaVersion;
            Category = p.Category; AttachType = p.AttachType; ExpectedSocketType = p.ExpectedSocketType; }
    }
    public sealed class AssemblyFitMount
    {
        public int SlotIndex { get; } public string SocketId { get; } public string SocketType { get; }
        public Point2 Position { get; }
        internal AssemblyFitMount(AssemblyFitMountInput p)
        { SlotIndex = p.SlotIndex; SocketId = p.SocketId; SocketType = p.SocketType; Position = new Point2(p.X, p.Y); }
    }
    public sealed class AssemblyFitKeepout
    {
        public float X { get; } public float Y { get; } public float Width { get; } public float Height { get; }
        public bool ArmorOnly { get; }
        internal AssemblyFitKeepout(AssemblyFitKeepoutInput p)
        { X = p.X; Y = p.Y; Width = p.Width; Height = p.Height; ArmorOnly = p.ArmorOnly; }
    }
    public sealed class AssemblyFitFrame
    {
        public string FrameId { get; } public int Revision { get; }
        public IReadOnlyList<AssemblyFitMount> Mounts { get; } public IReadOnlyList<AssemblyFitKeepout> Keepouts { get; }
        internal AssemblyFitFrame(string id, int revision, List<AssemblyFitMount> mounts, List<AssemblyFitKeepout> keepouts)
        { FrameId = id; Revision = revision; Mounts = mounts.AsReadOnly(); Keepouts = keepouts.AsReadOnly(); }
    }
    public sealed class AssemblyFitRegion
    {
        public string Motion { get; } public IReadOnlyList<Point2> Points { get; }
        internal AssemblyFitRegion(string motion, List<Point2> points) { Motion = motion; Points = points.AsReadOnly(); }
    }
    public sealed class AssemblyFitWeapon
    {
        public string MechanismId { get; } public int Revision { get; } public string MountType { get; }
        public Point2 Pivot { get; } public float MinRadians { get; } public float MaxRadians { get; }
        public float RecoilLimit { get; } public float Clearance { get; }
        public IReadOnlyList<PartRef> RequiredParts { get; } public IReadOnlyList<AssemblyFitRegion> Regions { get; }
        internal AssemblyFitWeapon(AssemblyFitWeaponInput p, List<PartRef> required, List<AssemblyFitRegion> regions)
        { MechanismId = p.MechanismId; Revision = p.Revision; MountType = p.MountType; Pivot = new Point2(p.PivotX, p.PivotY);
            MinRadians = p.MinRadians; MaxRadians = p.MaxRadians; RecoilLimit = p.RecoilLimit; Clearance = p.Clearance;
            RequiredParts = required.AsReadOnly(); Regions = regions.AsReadOnly(); }
    }

    /// <summary>Owned pilot fit metadata in U. Unsupported recipes need explicit coverage, never inferred bounds.</summary>
    public sealed class AssemblyFitCatalog
    {
        readonly Dictionary<(string, int), AssemblyFitPart> parts = new Dictionary<(string, int), AssemblyFitPart>();
        readonly Dictionary<(string, int), AssemblyFitFrame> frames = new Dictionary<(string, int), AssemblyFitFrame>();
        readonly Dictionary<(string, int), AssemblyFitWeapon> weapons = new Dictionary<(string, int), AssemblyFitWeapon>();
        readonly HashSet<string> partIds = new HashSet<string>(StringComparer.Ordinal);
        public string Revision { get; }
        public IReadOnlyList<AssemblyFitPart> Parts { get; }
        public IReadOnlyList<AssemblyFitFrame> Frames { get; }
        public IReadOnlyList<AssemblyFitWeapon> Weapons { get; }
        public AssemblyFitCatalog(AssemblyFitCatalogInput input)
        {
            Require(input != null && input.SchemaVersion == 1, "schema", "catalog");
            Require(!string.IsNullOrWhiteSpace(input.Revision) && input.Parts != null && input.Parts.Length > 0
                && input.Frames != null && input.Weapons != null, "catalog", "catalog");
            Revision = input.Revision;
            var ownedParts = new List<AssemblyFitPart>(); var ownedFrames = new List<AssemblyFitFrame>();
            var ownedWeapons = new List<AssemblyFitWeapon>();
            foreach (var p in input.Parts)
            {
                Require(p != null && !string.IsNullOrWhiteSpace(p.Id) && p.Revision > 0
                    && !string.IsNullOrWhiteSpace(p.AssetRevision) && Enum.IsDefined(typeof(ComponentCategory), p.Category)
                    && !string.IsNullOrWhiteSpace(p.AttachType) && !string.IsNullOrWhiteSpace(p.ExpectedSocketType), "part", "parts");
                Require(p.SocketSchemaVersion == 1, "schema", p.Id);
                Require(!parts.ContainsKey((p.Id, p.Revision)), "duplicate", p.Id);
                var part = new AssemblyFitPart(p); parts.Add((p.Id, p.Revision), part); partIds.Add(p.Id); ownedParts.Add(part);
            }
            foreach (var f in input.Frames)
            {
                Require(f != null && f.FrameId != null && TryGetPart(f.FrameId, f.Revision, out var _)
                    && parts[(f.FrameId, f.Revision)].Category == ComponentCategory.Frame
                    && f.Mounts != null && f.Mounts.Length > 0 && f.Keepouts != null, "coverage", "frames");
                Require(!frames.ContainsKey((f.FrameId, f.Revision)), "duplicate", f.FrameId);
                var mounts = new List<AssemblyFitMount>(); var slots = new HashSet<int>();
                foreach (var m in f.Mounts)
                {
                    Require(m != null && m.SlotIndex >= 0 && m.SlotIndex <= 2 && slots.Add(m.SlotIndex)
                        && m.SocketId == "mount." + m.SlotIndex && !string.IsNullOrWhiteSpace(m.SocketType), "socket", f.FrameId);
                    Number(m.X, m.Y); mounts.Add(new AssemblyFitMount(m));
                }
                var keepouts = new List<AssemblyFitKeepout>();
                foreach (var k in f.Keepouts)
                {
                    Require(k != null, "number", f.FrameId); Number(k.X, k.Y, k.Width, k.Height, k.X + k.Width, k.Y + k.Height);
                    Require(k.Width > 0 && k.Height > 0, "number", f.FrameId); keepouts.Add(new AssemblyFitKeepout(k));
                }
                var frame = new AssemblyFitFrame(f.FrameId, f.Revision, mounts, keepouts);
                frames.Add((f.FrameId, f.Revision), frame); ownedFrames.Add(frame);
            }
            foreach (var w in input.Weapons)
            {
                Require(w != null && w.MechanismId != null && TryGetPart(w.MechanismId, w.Revision, out var _)
                    && parts[(w.MechanismId, w.Revision)].Category == ComponentCategory.Mechanism
                    && !string.IsNullOrWhiteSpace(w.MountType) && w.RequiredParts != null && w.RequiredParts.Length == 5
                    && w.Regions != null && w.Regions.Length > 0, "coverage", "weapons");
                Require(!weapons.ContainsKey((w.MechanismId, w.Revision)), "duplicate", w.MechanismId);
                Number(w.PivotX, w.PivotY, w.MinRadians, w.MaxRadians, w.RecoilLimit, w.Clearance);
                Require(w.MaxRadians >= w.MinRadians && w.MaxRadians - w.MinRadians <= Math.PI * 2 + .00001
                    && w.RecoilLimit >= 0 && w.Clearance >= 0, "number", w.MechanismId);
                var required = new List<PartRef>(); var categories = new HashSet<ComponentCategory>();
                foreach (var p in w.RequiredParts)
                {
                    Require(p != null && p.Id != null && TryGetPart(p.Id, p.Revision, out var _), "coverage", w.MechanismId);
                    var category = parts[(p.Id, p.Revision)].Category;
                    Require(category >= ComponentCategory.Mechanism && category <= ComponentCategory.Trigger
                        && categories.Add(category), "coverage", w.MechanismId);
                    required.Add(new PartRef(p.Id, p.Revision));
                }
                required.Sort((a, b) => parts[(a.Id, a.Revision)].Category.CompareTo(parts[(b.Id, b.Revision)].Category));
                Require(required[0].Id == w.MechanismId && required[0].Revision == w.Revision, "coverage", w.MechanismId);
                var regions = new List<AssemblyFitRegion>(); bool moving = false;
                foreach (var r in w.Regions)
                {
                    Require(r != null && (r.Motion == "fixed" || r.Motion == "rotating" || r.Motion == "recoiling"), "motion", w.MechanismId);
                    Require(r.Points != null && r.Points.Length > 0, "number", w.MechanismId);
                    var points = new List<Point2>();
                    foreach (var p in r.Points)
                    { Require(p != null, "number", w.MechanismId); Number(p.X, p.Y); points.Add(new Point2(p.X, p.Y)); }
                    regions.Add(new AssemblyFitRegion(r.Motion, points)); moving |= r.Motion != "fixed";
                }
                Require(moving, "coverage", w.MechanismId);
                var weapon = new AssemblyFitWeapon(w, required, regions);
                AssemblyFitRules.SweptRadius(weapon); // Reject finite author coordinates whose derived radius overflows U.
                weapons.Add((w.MechanismId, w.Revision), weapon); ownedWeapons.Add(weapon);
            }
            Parts = ownedParts.AsReadOnly(); Frames = ownedFrames.AsReadOnly(); Weapons = ownedWeapons.AsReadOnly();
        }
        public bool TryGetPart(string id, int revision, out AssemblyFitPart part)
        { part = null; return id != null && parts.TryGetValue((id, revision), out part); }
        public bool TryGetFrame(string id, int revision, out AssemblyFitFrame frame)
        { frame = null; return id != null && frames.TryGetValue((id, revision), out frame); }
        public bool TryGetWeapon(string id, int revision, out AssemblyFitWeapon weapon)
        { weapon = null; return id != null && weapons.TryGetValue((id, revision), out weapon); }
        internal bool HasPartId(string id) => id != null && partIds.Contains(id);
        static void Require(bool valid, string code, string path)
        { if (!valid) throw AssemblyCatalogException.At("assembly.fit." + code, path); }
        static void Number(params float[] values)
        { foreach (float value in values) Require(!float.IsNaN(value) && !float.IsInfinity(value), "number", "geometry"); }
    }
}
