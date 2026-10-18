using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    public static class AssemblyFitRules
    {
        // Regions are authored relative to the weapon rotation pivot, in U, with the muzzle axis +Y.
        // A full circle is deliberately conservative for narrower angle limits: it may reject a safe
        // restricted assembly, but cannot certify one whose independent rotations can intersect.
        public static float SweptRadius(AssemblyFitWeapon weapon)
        {
            if (weapon == null) throw new ArgumentNullException(nameof(weapon));
            double radiusSquared = 0;
            foreach (var region in weapon.Regions)
            {
                if (region.Motion == "fixed") continue;
                foreach (var point in region.Points)
                {
                    double x = point.X, y = point.Y;
                    radiusSquared = Math.Max(radiusSquared, x * x + y * y);
                    if (region.Motion == "recoiling")
                    { y -= weapon.RecoilLimit; radiusSquared = Math.Max(radiusSquared, x * x + y * y); }
                }
            }
            double radius = Math.Sqrt(radiusSquared);
            if (radius > float.MaxValue) throw AssemblyCatalogException.At("assembly.fit.number", weapon.MechanismId);
            return (float)radius;
        }

        public static IReadOnlyList<AssemblyDiagnostic> Validate(AssemblyRecipe recipe, AssemblyFitCatalog fits)
        {
            var issues = new List<AssemblyDiagnostic>();
            if (recipe == null) issues.Add(new AssemblyDiagnostic("assembly.fit.recipe", "recipe"));
            if (fits == null) issues.Add(new AssemblyDiagnostic("assembly.fit.catalog", "fits"));
            if (recipe == null || fits == null) return issues.AsReadOnly();
            foreach (var reference in recipe.Parts)
            {
                if (!fits.TryGetPart(reference.Id, reference.Revision, out var part))
                    issues.Add(new AssemblyDiagnostic(fits.HasPartId(reference.Id) ? "assembly.fit.revision" : "assembly.fit.part",
                        reference.Id ?? "part", reference.Revision.ToString()));
                else if (part.AttachType != part.ExpectedSocketType)
                    issues.Add(new AssemblyDiagnostic("assembly.fit.socket_type", part.Id, part.AttachType, part.ExpectedSocketType));
            }
            if (issues.Count > 0) return issues.AsReadOnly();
            if (recipe.Tower == null)
            { issues.Add(new AssemblyDiagnostic("assembly.fit.coverage", "structure")); return issues.AsReadOnly(); }
            var tower = recipe.Tower;
            if (!fits.TryGetFrame(tower.Foundation.Frame.Id, tower.Foundation.Frame.Revision, out var frame))
            { issues.Add(new AssemblyDiagnostic("assembly.fit.coverage", "foundation.frame")); return issues.AsReadOnly(); }
            var placements = new List<Placement>();
            foreach (var selected in tower.Mounts)
            {
                string path = "mounts[" + selected.SlotIndex + "]";
                AssemblyFitMount socket = null;
                foreach (var mount in frame.Mounts) if (mount.SlotIndex == selected.SlotIndex) { socket = mount; break; }
                if (socket == null)
                { issues.Add(new AssemblyDiagnostic("assembly.fit.socket", path, "mount." + selected.SlotIndex)); continue; }
                if (!fits.TryGetWeapon(selected.Weapon.Mechanism.Id, selected.Weapon.Mechanism.Revision, out var weapon)
                    || !Covers(selected.Weapon, weapon))
                { issues.Add(new AssemblyDiagnostic("assembly.fit.coverage", path)); continue; }
                if (socket.SocketType != weapon.MountType)
                { issues.Add(new AssemblyDiagnostic("assembly.fit.socket_type", path, socket.SocketType, weapon.MountType)); continue; }
                float pivotX = socket.Position.X + weapon.Pivot.X, pivotY = socket.Position.Y + weapon.Pivot.Y;
                if (float.IsInfinity(pivotX) || float.IsInfinity(pivotY))
                { issues.Add(new AssemblyDiagnostic("assembly.fit.number", path)); continue; }
                var placement = new Placement(socket, weapon, path); placements.Add(placement);
                foreach (var keepout in frame.Keepouts)
                {
                    if (keepout.ArmorOnly && tower.Foundation.Armor.Id != "armor.plate") continue;
                    var bounds = new Bounds(keepout);
                    bool collision = CircleNear(placement.Pivot, (double)placement.Radius + weapon.Clearance, bounds);
                    foreach (var support in placement.Fixed) collision |= RectsNear(support, bounds, weapon.Clearance);
                    if (collision) issues.Add(new AssemblyDiagnostic("assembly.clearance", path,
                        keepout.ArmorOnly ? "armor.plate" : "foundation"));
                }
            }
            for (int i = 0; i < placements.Count; i++)
            for (int j = i + 1; j < placements.Count; j++)
            {
                var a = placements[i]; var b = placements[j]; double clearance = Math.Max(a.Weapon.Clearance, b.Weapon.Clearance);
                double required = (double)a.Radius + b.Radius + clearance;
                bool collision = SquaredDistance(a.Pivot, b.Pivot) <= required * required;
                foreach (var support in a.Fixed) collision |= CircleNear(b.Pivot, b.Radius + clearance, support);
                foreach (var support in b.Fixed) collision |= CircleNear(a.Pivot, a.Radius + clearance, support);
                foreach (var first in a.Fixed)
                foreach (var second in b.Fixed) collision |= RectsNear(first, second, clearance);
                if (collision) issues.Add(new AssemblyDiagnostic("assembly.clearance", a.Path, b.Path));
            }
            return issues.AsReadOnly();
        }

        static bool Covers(WeaponRecipe recipe, AssemblyFitWeapon fit)
        {
            int index = 0;
            foreach (var reference in recipe.Parts)
            {
                var required = fit.RequiredParts[index++];
                if (reference.Id != required.Id || reference.Revision != required.Revision) return false;
            }
            return index == fit.RequiredParts.Count;
        }
        sealed class Placement
        {
            public readonly AssemblyFitWeapon Weapon;
            public readonly Point2 Pivot; public readonly float Radius; public readonly string Path;
            public readonly List<Bounds> Fixed = new List<Bounds>();
            public Placement(AssemblyFitMount socket, AssemblyFitWeapon weapon, string path)
            {
                Weapon = weapon; Path = path; Pivot = new Point2(socket.Position.X + weapon.Pivot.X, socket.Position.Y + weapon.Pivot.Y);
                Radius = SweptRadius(weapon);
                // Fixed supports may contact their own rotating assembly. Other mounts and body/armor
                // keepouts are never exempt; contact is scoped to this single weapon placement.
                foreach (var region in weapon.Regions) if (region.Motion == "fixed") Fixed.Add(new Bounds(region, Pivot));
            }
        }
        readonly struct Bounds
        {
            public readonly double MinX, MinY, MaxX, MaxY;
            public Bounds(AssemblyFitKeepout k) { MinX = k.X; MinY = k.Y; MaxX = (double)k.X + k.Width; MaxY = (double)k.Y + k.Height; }
            public Bounds(AssemblyFitRegion region, Point2 pivot)
            {
                double minX = double.PositiveInfinity, minY = double.PositiveInfinity, maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
                foreach (var p in region.Points)
                { minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); }
                MinX = minX + pivot.X; MinY = minY + pivot.Y; MaxX = maxX + pivot.X; MaxY = maxY + pivot.Y;
            }
        }
        static double SquaredDistance(Point2 a, Point2 b)
        { double x = (double)a.X - b.X, y = (double)a.Y - b.Y; return x * x + y * y; }
        static bool CircleNear(Point2 center, double radius, Bounds bounds)
        {
            double x = Math.Max(0, Math.Max(bounds.MinX - center.X, center.X - bounds.MaxX));
            double y = Math.Max(0, Math.Max(bounds.MinY - center.Y, center.Y - bounds.MaxY));
            return x * x + y * y <= radius * radius;
        }
        static bool RectsNear(Bounds a, Bounds b, double clearance)
        {
            double x = Math.Max(0, Math.Max(a.MinX - b.MaxX, b.MinX - a.MaxX));
            double y = Math.Max(0, Math.Max(a.MinY - b.MaxY, b.MinY - a.MaxY));
            return x * x + y * y <= clearance * clearance;
        }
    }
}
