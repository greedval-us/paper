using System;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>Visual attachment metadata for the existing standard atlas. Never changes gameplay or saved drawings.</summary>
    public static class NotebookMountLayout
    {
        [Serializable] sealed class Socket
        {
            public int slot, order;
            public Vector2 position;
            public float diameter;
        }
        [Serializable] sealed class Foundation
        {
            public string id;
            public Socket[] sockets;
        }
        [Serializable] sealed class Weapon
        {
            public string id;
            public Vector2 canvas, attach, muzzle;
            public PixelRect trim;
            public float diameter;
        }
        [Serializable] sealed class PixelRect
        {
            public float x, y, width, height;
            public Rect Rect => new Rect(x, y, width, height);
        }
        [Serializable] sealed class Source
        {
            public int schemaVersion;
            public string revision;
            public Foundation[] foundations;
            public Weapon[] weapons;
            public Pilot[] pilots;
        }
        [Serializable] sealed class FragmentSource
        {
            public string id, spriteId;
            public Vector2 canvas, attach, offset;
            public PixelRect trim;
            public float scale, radians;
            public int group, layer;
        }
        [Serializable] sealed class Pilot
        {
            public string chassisId, weaponId, revision, muzzleFragment;
            public Vector2 muzzlePoint, pivotOffset;
            public FragmentSource[] fragments;
            internal IReadOnlyList<Fragment> resolved;
        }
        public enum FragmentMotion { Fixed, Rotating, Recoiling }
        public readonly struct Fragment
        {
            public readonly string Id, SpriteId;
            public readonly Vector2 Canvas, Attach, Offset;
            public readonly Rect Trim;
            public readonly float Scale, Radians;
            public readonly FragmentMotion Motion;
            public readonly int Layer;
            internal Fragment(string id, string sprite, Vector2 canvas, Vector2 attach, Vector2 offset, Rect trim,
                float scale, float radians, FragmentMotion motion, int layer)
            { Id = id; SpriteId = sprite; Canvas = canvas; Attach = attach; Offset = offset; Trim = trim;
                Scale = scale; Radians = radians; Motion = motion; Layer = layer; }
            public Vector2 ToMount(Vector2 point, Vector2 rootAttach)
                => rootAttach + Offset + Rotate((point - Attach) * Scale, Radians);
        }

        /// <summary>All source points use the untrimmed author canvas; Scale is uniform world units per pixel.</summary>
        public readonly struct Mount
        {
            public readonly string WeaponId;
            public readonly int SlotIndex, DrawOrder;
            public readonly Vector2 Socket, Attach, Muzzle, Canvas, PivotOffset;
            public readonly Rect Trim;
            public readonly float Scale;
            public readonly IReadOnlyList<Fragment> Fragments;
            public readonly string VisualRevision;
            public bool IsSplit => Fragments != null && Fragments.Count > 0;
            public Vector2 RotationCenter => Socket + PivotOffset * Scale;
            internal Mount(string id, int slot, int order, Vector2 socket, Vector2 attach, Vector2 muzzle, Vector2 canvas, Rect trim,
                float scale, IReadOnlyList<Fragment> fragments, string visualRevision, Vector2 pivotOffset)
            { WeaponId = id; SlotIndex = slot; DrawOrder = order; Socket = socket; Attach = attach; Muzzle = muzzle;
                Canvas = canvas; Trim = trim; Scale = scale; Fragments = fragments; VisualRevision = visualRevision; PivotOffset = pivotOffset; }
            public Vector2 Transform(Vector2 sourcePoint, float radians = 0, float recoil = 0)
            {
                Vector2 forward = Rotate((Muzzle - Attach - PivotOffset).normalized, radians);
                return RotationCenter + Rotate((sourcePoint - Attach - PivotOffset) * Scale, radians) + forward * recoil;
            }
            public float AimAngle(Point2 aim, Vector2 origin)
            {
                if (aim.X == 0 && aim.Y == 0) return 0;
                Vector2 direction = new Vector2(aim.X, aim.Y) - (origin + RotationCenter);
                if (direction.sqrMagnitude < .00001f) return 0;
                Vector2 local = Muzzle - Attach - PivotOffset;
                return Mathf.Atan2(direction.y, direction.x) - Mathf.Atan2(local.y, local.x);
            }
            public Vector2 TransformFragment(Fragment fragment, Vector2 pixel, float radians = 0, float recoil = 0)
                => Transform(fragment.ToMount(pixel, Attach), fragment.Motion == FragmentMotion.Fixed ? 0 : radians,
                    fragment.Motion == FragmentMotion.Recoiling ? recoil : 0);
            internal void Draw(MeshPen pen, Vector2 origin, float radians, float recoil, float opacity, Color tint, int layer)
            {
                if (IsSplit)
                {
                    foreach (var fragment in Fragments)
                    {
                        if (fragment.Layer != layer) continue;
                        float rotation = fragment.Motion == FragmentMotion.Fixed ? 0 : radians;
                        NotebookSpriteCatalog.TryDrawFrame(pen, fragment.SpriteId, fragment.Trim,
                            new Rect(Vector2.zero, fragment.Canvas), origin + TransformFragment(fragment, Vector2.zero, radians, recoil),
                            Scale * fragment.Scale, rotation + fragment.Radians, opacity, tint);
                    }
                    return;
                }
                if (layer != 2) return;
                Vector2 offset = origin + Transform(Vector2.zero, radians, recoil);
                NotebookSpriteCatalog.TryDrawFrame(pen, WeaponId, Trim,
                    new Rect(Vector2.zero, Canvas), offset, Scale, radians, opacity, tint);
            }
            internal float MotionRadius(float recoil)
            {
                if (!IsSplit)
                    return new Vector2(Mathf.Max(Attach.x, Canvas.x - Attach.x),
                        Mathf.Max(Attach.y, Canvas.y - Attach.y)).magnitude * Scale + Mathf.Abs(recoil);
                float radius = 0;
                foreach (var fragment in Fragments)
                {
                    if (fragment.Motion == FragmentMotion.Fixed) continue;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        var point = new Vector2(corner % 2 == 0 ? fragment.Trim.xMin : fragment.Trim.xMax,
                            corner < 2 ? fragment.Trim.yMin : fragment.Trim.yMax);
                        radius = Mathf.Max(radius, (fragment.ToMount(point, Attach) - Attach - PivotOffset).magnitude * Scale
                            + (fragment.Motion == FragmentMotion.Recoiling ? Mathf.Abs(recoil) : 0));
                    }
                }
                return radius;
            }
        }

        static readonly Dictionary<string, Foundation> foundations = new Dictionary<string, Foundation>(StringComparer.Ordinal);
        static readonly Dictionary<string, Weapon> weapons = new Dictionary<string, Weapon>(StringComparer.Ordinal);
        static readonly Dictionary<string, Pilot> pilots = new Dictionary<string, Pilot>(StringComparer.Ordinal);
        static bool loaded;
        public static string Revision { get { EnsureLoaded(); return revision; } }
        static string revision;

        public static bool Supports(TowerBlueprint blueprint)
        {
            if (blueprint == null || blueprint.HasPlayerDrawing) return false;
            EnsureLoaded();
            if (!foundations.TryGetValue(blueprint.ChassisId, out var foundation)) return false;
            foreach (var mount in blueprint.WeaponMounts)
                if (!weapons.ContainsKey(mount.WeaponId) || Find(foundation, mount.SlotIndex) == null) return false;
            return true;
        }
        public static Mount Resolve(TowerBlueprint blueprint, int slot)
        {
            if (!Supports(blueprint)) throw new ArgumentException("No standard visual attachment layout for this blueprint.", nameof(blueprint));
            var foundation = foundations[blueprint.ChassisId];
            var socket = Find(foundation, slot);
            string id = blueprint.GetWeaponId(slot);
            if (socket == null || id == null) throw new ArgumentException("Missing occupied mount." , nameof(slot));
            var weapon = weapons[id];
            if (!NotebookSpriteCatalog.TryGetBounds(blueprint.ChassisId, out var bounds))
                throw new InvalidOperationException("Missing foundation atlas binding: " + blueprint.ChassisId);
            pilots.TryGetValue(blueprint.ChassisId + "|" + id, out var pilot);
            Vector2 muzzle = weapon.muzzle;
            if (pilot != null)
                foreach (var fragment in pilot.resolved)
                    if (fragment.Id == pilot.muzzleFragment) muzzle = fragment.ToMount(pilot.muzzlePoint, weapon.attach);
            return new Mount(id, slot, socket.order, bounds.min + Vector2.Scale(bounds.size, socket.position),
                weapon.attach, muzzle, weapon.canvas, weapon.trim.Rect, bounds.width * socket.diameter / weapon.diameter,
                pilot?.resolved, pilot?.revision ?? revision, pilot?.pivotOffset ?? Vector2.zero);
        }
        internal static Vector2 Rotate(Vector2 point, float radians)
        { float c = Mathf.Cos(radians), s = Mathf.Sin(radians); return new Vector2(c * point.x - s * point.y, s * point.x + c * point.y); }
        static Socket Find(Foundation foundation, int slot)
        { foreach (var socket in foundation.sockets) if (socket.slot == slot) return socket; return null; }

        public static void Validate(string json) => Parse(json);
        static Source Parse(string json)
        {
            Source source;
            try { source = JsonUtility.FromJson<Source>(json); }
            catch (Exception error) { throw new FormatException("Notebook mounts: invalid JSON.", error); }
            if (source == null || (source.schemaVersion != 1 && source.schemaVersion != 2) || string.IsNullOrWhiteSpace(source.revision)
                || source.foundations == null || source.foundations.Length == 0 || source.weapons == null || source.weapons.Length == 0)
                throw new FormatException("Notebook mounts: unsupported schema or empty catalogue.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var foundation in source.foundations)
            {
                if (foundation == null || string.IsNullOrWhiteSpace(foundation.id) || !ids.Add(foundation.id)
                    || foundation.sockets == null || foundation.sockets.Length == 0)
                    throw new FormatException("Notebook mounts: missing or duplicate foundation.");
                var slots = new HashSet<int>(); var orders = new HashSet<int>();
                foreach (var socket in foundation.sockets)
                    if (socket == null || socket.slot < 0 || socket.slot > 2 || socket.order < 0 || socket.order > 2 || !slots.Add(socket.slot) || !orders.Add(socket.order)
                        || !Unit(socket.position.x) || !Unit(socket.position.y) || !Finite(socket.diameter) || socket.diameter <= 0 || socket.diameter > 1)
                        throw new FormatException("Notebook mounts: invalid socket in " + foundation.id);
            }
            var foundationIds = new HashSet<string>(ids, StringComparer.Ordinal);
            ids.Clear();
            foreach (var weapon in source.weapons)
                if (weapon == null || string.IsNullOrWhiteSpace(weapon.id) || !ids.Add(weapon.id)
                    || !Positive(weapon.canvas.x) || !Positive(weapon.canvas.y) || !Positive(weapon.diameter)
                    || weapon.diameter > weapon.canvas.x || !Inside(weapon.attach, weapon.canvas) || !Inside(weapon.muzzle, weapon.canvas)
                    || weapon.trim == null || !Inside(new Vector2(weapon.trim.x, weapon.trim.y), weapon.canvas)
                    || !Positive(weapon.trim.width) || !Positive(weapon.trim.height)
                    || !Inside(new Vector2(weapon.trim.x + weapon.trim.width, weapon.trim.y + weapon.trim.height), weapon.canvas)
                    || (weapon.muzzle - weapon.attach).sqrMagnitude < 1)
                    throw new FormatException("Notebook mounts: invalid weapon canvas, attach or muzzle.");
            var pairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pilot in source.pilots ?? Array.Empty<Pilot>())
            {
                if (source.schemaVersion < 2 || pilot == null || !foundationIds.Contains(pilot.chassisId ?? "")
                    || !ids.Contains(pilot.weaponId ?? "") || !pairs.Add(pilot.chassisId + "|" + pilot.weaponId)
                    || string.IsNullOrWhiteSpace(pilot.revision) || pilot.fragments == null || pilot.fragments.Length == 0
                    || !Finite(pilot.pivotOffset.x) || !Finite(pilot.pivotOffset.y))
                    throw new FormatException("Notebook mounts: invalid split pilot definition.");
                var names = new HashSet<string>(StringComparer.Ordinal); var layers = new HashSet<int>();
                var resolved = new List<Fragment>(); FragmentSource muzzle = null; int groups = 0;
                foreach (var part in pilot.fragments)
                {
                    if (part == null || string.IsNullOrWhiteSpace(part.id) || !names.Add(part.id) || string.IsNullOrWhiteSpace(part.spriteId)
                        || !Positive(part.canvas.x) || !Positive(part.canvas.y) || !Inside(part.attach, part.canvas)
                        || part.trim == null || !Inside(new Vector2(part.trim.x, part.trim.y), part.canvas)
                        || !Positive(part.trim.width) || !Positive(part.trim.height)
                        || !Inside(new Vector2(part.trim.x + part.trim.width, part.trim.y + part.trim.height), part.canvas)
                        || !Finite(part.offset.x) || !Finite(part.offset.y) || !Positive(part.scale) || !Finite(part.radians)
                        || part.group < 0 || part.group > 2 || part.layer < 0 || part.layer > 3 || !layers.Add(part.layer))
                        throw new FormatException("Notebook mounts: invalid split fragment.");
                    groups |= 1 << part.group;
                    if (part.id == pilot.muzzleFragment) muzzle = part;
                    resolved.Add(new Fragment(part.id, part.spriteId, part.canvas, part.attach, part.offset, part.trim.Rect,
                        part.scale, part.radians, (FragmentMotion)part.group, part.layer));
                }
                if (groups != 7 || muzzle == null || muzzle.group != 2 || !Inside(pilot.muzzlePoint, muzzle.canvas)
                    || !muzzle.trim.Rect.Contains(pilot.muzzlePoint))
                    throw new FormatException("Notebook mounts: muzzle must belong to a recoiling fragment.");
                Vector2 axis = muzzle.offset + Rotate((pilot.muzzlePoint - muzzle.attach) * muzzle.scale, muzzle.radians)
                    - pilot.pivotOffset;
                if (!Finite(axis.x) || !Finite(axis.y) || axis.sqrMagnitude < 1)
                    throw new FormatException("Notebook mounts: split muzzle must define a non-zero aiming axis.");
                pilot.resolved = resolved.AsReadOnly();
            }
            return source;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Positive(float value) => Finite(value) && value > 0;
        static bool Unit(float value) => Finite(value) && value >= 0 && value <= 1;
        static bool Inside(Vector2 point, Vector2 canvas) => Finite(point.x) && Finite(point.y)
            && point.x >= 0 && point.y >= 0 && point.x <= canvas.x && point.y <= canvas.y;
        static void EnsureLoaded()
        {
            if (loaded) return;
            var asset = Resources.Load<TextAsset>("Art/NotebookMounts");
            if (asset == null) throw new InvalidOperationException("Notebook mounts catalogue is missing.");
            var source = Parse(asset.text);
            foreach (var pilot in source.pilots ?? Array.Empty<Pilot>())
                foreach (var part in pilot.resolved)
                    if (!NotebookSpriteCatalog.TryGetBounds(part.SpriteId, out _))
                        throw new InvalidOperationException("Notebook mounts: missing split sprite " + part.SpriteId);
            foundations.Clear(); weapons.Clear(); pilots.Clear();
            foreach (var foundation in source.foundations) foundations.Add(foundation.id, foundation);
            foreach (var weapon in source.weapons) weapons.Add(weapon.id, weapon);
            foreach (var pilot in source.pilots ?? Array.Empty<Pilot>()) pilots.Add(pilot.chassisId + "|" + pilot.weaponId, pilot);
            revision = source.revision; loaded = true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { loaded = false; revision = null; foundations.Clear(); weapons.Clear(); pilots.Clear(); }
    }
}
