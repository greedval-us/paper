using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>Barrel traverse around the tower's fixed world heading. A default value leaves radial effects unrestricted.</summary>
    internal readonly struct FiringSector
    {
        readonly Point2 forward;
        readonly double minimumCosine;
        readonly bool directional;

        internal FiringSector(StructureState tower, float width)
        {
            forward = tower.Forward;
            minimumCosine = Math.Cos(width * Math.PI / 360);
            directional = true;
        }

        internal bool Contains(double dx, double dy)
            => !directional || dx * forward.X + dy * forward.Y >= (minimumCosine - 1e-6) * Math.Sqrt(dx * dx + dy * dy);

        internal static float Normalize(float angle)
        {
            float wrapped = angle % 360f;
            if (wrapped < 0) wrapped += 360f;
            return wrapped >= 360f ? 0 : wrapped;
        }

        internal static float Heading(double dx, double dy)
            => Normalize((float)(Math.Atan2(dy, dx) * 180 / Math.PI));
    }

    public sealed partial class GameSession
    {
        BattlefieldApproaches approaches;
        readonly Dictionary<Cell, float> placementHeadings = new Dictionary<Cell, float>();
        int cachedApproachSides;
        StructureKind cachedHeadingKind;
        ResolvedEntry cachedHeadingEntry;
        TowerBlueprint cachedHeadingBlueprint;

        static bool ValidPlacementFacing(float? facingDegrees, ref string reason)
        {
            if (!facingDegrees.HasValue || !(float.IsNaN(facingDegrees.Value) || float.IsInfinity(facingDegrees.Value))) return true;
            reason = "Укажите направление установки башни.";
            return false;
        }

        public float DefaultFacingDegrees(Cell cell)
        {
            var tower = StructureAt(cell);
            return DefaultFacingDegrees(cell, tower?.Kind ?? StructureKind.Gun, tower?.ResolvedEntry, tower?.Blueprint);
        }

        public float DefaultFacingDegrees(Cell cell, StructureKind kind, ResolvedEntry entry = null, TowerBlueprint blueprint = null)
        {
            int wave = Phase == RunPhase.Assault ? WaveIndex - 1 : WaveIndex;
            int sides = rules.Waves.Count == 0 ? 1 : rules.Waves[Math.Max(0, Math.Min(wave, rules.Waves.Count - 1))].Sides;
            if ((sides & 15) == 0) sides = 1;
            if (Battlefield == null || !Inside(cell)) return ApproachHeading(cell, sides);
            // An album slot can supply both its legacy summary and the authored blueprint used in combat.
            if (entry?.Kind == AlbumEntryKind.Legacy && blueprint != null) entry = null;
            if (sides != cachedApproachSides || kind != cachedHeadingKind || entry != cachedHeadingEntry || blueprint != cachedHeadingBlueprint)
            {
                placementHeadings.Clear();
                cachedApproachSides = sides; cachedHeadingKind = kind; cachedHeadingEntry = entry; cachedHeadingBlueprint = blueprint;
            }
            if (placementHeadings.TryGetValue(cell, out float heading)) return heading;
            heading = ApproachHeading(cell, sides);
            if (entry?.Weapons.Count > 0 || blueprint != null || !ConstructionToolRules.IsTool(kind))
            {
                if (approaches == null) approaches = new BattlefieldApproaches(Battlefield, BaseCell, rules.Width, rules.Height);
                approaches.Begin(cell, sides, heading);
                if (entry != null)
                {
                    foreach (var weapon in entry.Weapons)
                        approaches.AddWeapon(weapon.Attack, weapon.MinimumRange, weapon.Range, weapon.FiringArcDegrees);
                }
                else if (blueprint != null)
                {
                    var assembly = TowerCatalog.Resolve(blueprint, rules.Content);
                    for (int slot = 0; slot < assembly.Weapons.Length; slot++)
                    {
                        var weapon = assembly.Weapons[slot];
                        if (weapon != null) approaches.AddWeapon(weapon.Attack, weapon.MinimumRange,
                            TowerCatalog.WeaponRange(assembly, weapon, slot), weapon.FiringArcDegrees);
                    }
                }
                else if (weapons.TryGetDirectWeapon(kind, out var direct))
                    approaches.AddWeapon(direct.Attack, direct.MinimumRange, TowerCatalog.WeaponRange(null, direct, 0), direct.FiringArcDegrees);
                heading = approaches.BestHeading();
            }
            placementHeadings[cell] = heading;
            return heading;
        }

        float ApproachHeading(Cell cell, int sides)
        {
            Cell nearest = default;
            double bestDistance = double.PositiveInfinity;
            if (Battlefield != null)
                foreach (var entry in Battlefield.Entrances)
                    if ((sides & Battlefield.Side(entry)) != 0) Consider(entry);
            // Classic fields admit enemies along the whole side, without authored entrances.
            if (double.IsPositiveInfinity(bestDistance))
            {
                if ((sides & 1) != 0) Consider(new Cell(0, cell.Y));
                if ((sides & 2) != 0) Consider(new Cell(rules.Width - 1, cell.Y));
                if ((sides & 4) != 0) Consider(new Cell(cell.X, rules.Height - 1));
                if ((sides & 8) != 0) Consider(new Cell(cell.X, 0));
            }
            return FiringSector.Heading(nearest.X - cell.X, nearest.Y - cell.Y);

            void Consider(Cell candidate)
            {
                double dx = (double)candidate.X - cell.X, dy = (double)candidate.Y - cell.Y;
                double distance = dx * dx + dy * dy;
                if (distance < bestDistance) { bestDistance = distance; nearest = candidate; }
            }
        }
    }
}
