using System;
using System.Collections.Generic;
using System.Linq;

namespace PaperDefense.Core
{
    public static class AssemblyResolver
    {
        /// <summary>Publication gate: both numerical rules and the supplied physical-fit coverage must pass.</summary>
        public static AssemblyResolution Resolve(AssemblyRecipe recipe, ComponentCatalog catalog, GameBalance balance,
            MaterialTier material, AssemblyFitCatalog fits)
        {
            var numerical = Resolve(recipe, catalog, balance, material);
            var issues = new List<AssemblyDiagnostic>(numerical.Diagnostics);
            issues.AddRange(AssemblyFitRules.Validate(recipe, fits));
            return issues.Count == 0 ? numerical : AssemblyResolution.Failure(issues);
        }

        /// <summary>Numerical resolution only; callers publishing a new assembly supply a fit catalog via the overload.</summary>
        public static AssemblyResolution Resolve(AssemblyRecipe recipe, ComponentCatalog catalog, GameBalance balance, MaterialTier material)
        {
            var issues = AssemblyValidation.Validate(recipe, catalog, balance, material);
            if (issues.Count != 0) return AssemblyResolution.Failure(issues);
            try
            {
                var result = recipe.Tower != null ? ResolveTower(recipe, catalog, balance, material) : ResolveStructure(recipe, catalog, balance, material);
                // Exercise derived values before publishing; a finite component may still overflow a product.
                _ = result.EffectiveHealth; _ = result.Dps;
                return AssemblyResolution.Success(result);
            }
            catch (AssemblyCatalogException ex) { return AssemblyResolution.Failure(ex.Diagnostics); }
            catch (OverflowException) { return AssemblyResolution.Failure(new[] { new AssemblyDiagnostic("assembly.number.nonfinite", "entry.cost") }); }
        }
        public static AssemblyResolution Resolve(RecipeSnapshot snapshot, GameBalance balance, MaterialTier material)
        {
            if (snapshot == null) return AssemblyResolution.Failure(new[] { new AssemblyDiagnostic("assembly.recipe.missing", "snapshot") });
            return Resolve(snapshot.Recipe, new ComponentCatalog(snapshot.BalanceRevision, snapshot.Definitions), balance, material);
        }
        public static AssemblyResolution ResolveEntry(AlbumEntry entry, GameBalance balance, MaterialTier material)
        {
            if (entry == null) return AssemblyResolution.Failure(new[] { new AssemblyDiagnostic("assembly.entry.missing", "entry") });
            return entry.Kind == AlbumEntryKind.Legacy ? LegacyAlbumAdapter.Resolve(entry, balance, material) : Resolve(entry.Snapshot, balance, material);
        }
        static ResolvedEntry ResolveTower(AssemblyRecipe recipe, ComponentCatalog catalog, GameBalance balance, MaterialTier material)
        {
            var tower = recipe.Tower; var foundation = Parts(tower.Foundation.Parts, catalog); var frame = foundation[0];
            var all = Parts(recipe.Parts, catalog); var m = balance.Material(material);
            int capacity = AssemblyValidation.Integer(Fold(frame, foundation, AssemblyStat.Capacity), "foundation.capacity");
            int load = AssemblyValidation.Integer(Fold(null, all, AssemblyStat.Load), "entry.load");
            int mounts = AssemblyValidation.Integer(frame.Get(AssemblyStat.MountCount, ModifierOperation.Set), "foundation.mounts", true);
            foreach (var mount in tower.Mounts) if (mount.SlotIndex >= mounts) throw AssemblyCatalogException.At("assembly.mount.unavailable", "mounts[" + mount.SlotIndex + "]");
            if (load > capacity) throw AssemblyCatalogException.At("assembly.load.exceeded", "entry.load", load.ToString(), capacity.ToString());
            var weapons = new List<ResolvedWeapon>(); double weaponBuild = 0;
            foreach (var mount in tower.Mounts)
            {
                var parts = Parts(mount.Weapon.Parts, catalog);
                weapons.Add(ResolveWeapon(mount.SlotIndex, parts, foundation, balance, m));
                weaponBuild = Math.Max(weaponBuild, AssemblyValidation.NonNegative(Fold(parts[0], parts, AssemblyStat.BuildSeconds), "mounts[" + mount.SlotIndex + "].build"));
            }
            double seconds = Fold(frame, foundation, AssemblyStat.BuildSeconds) + weaponBuild + (tower.Mounts.Count - 1) * (double)balance.Construction.SecondaryBuildSeconds;
            var health = AssemblyValidation.Positive(Fold(frame, foundation, AssemblyStat.Health), "foundation.health");
            var defense = Math.Min(.6f, AssemblyValidation.NonNegative(Fold(null, foundation, AssemblyStat.Defense), "foundation.defense"));
            var front = Math.Min(.6f, AssemblyValidation.NonNegative(Fold(null, foundation, AssemblyStat.FrontDefense), "foundation.frontDefense"));
            double feedCapacity = frame.Get(AssemblyStat.Feed, ModifierOperation.Set) + Add(foundation, AssemblyStat.Feed);
            return new ResolvedEntry(AlbumEntryKind.Tower, material, Snapshot(recipe, catalog, all), Cost(all, m.CostMultiplier),
                AssemblyValidation.Positive(health * (double)m.HealthMultiplier, "entry.health"), load, capacity,
                AssemblyValidation.NonNegative(AssemblyValidation.NonNegative(seconds, "entry.build") * (double)m.BuildTimeMultiplier, "entry.build"), defense, front,
                AssemblyValidation.Positive(feedCapacity, "foundation.feed"), AssemblyValidation.Positive(Product(foundation, AssemblyStat.Feed), "foundation.feedModifier"),
                m.RefundFraction, weapons);
        }
        static ResolvedWeapon ResolveWeapon(int slot, ComponentDefinition[] parts, ComponentDefinition[] foundation, GameBalance balance, MaterialDefinition material)
        {
            var core = parts[0]; string path = "mounts[" + slot + "]"; var family = core.WeaponFamilies;
            double size = Fold(core, parts, AssemblyStat.MagazineSize);
            var overrides = parts.Select(p => p.Get(AssemblyStat.MagazineOverride, ModifierOperation.Set)).Where(v => v != 0).ToArray();
            if (overrides.Length > 1) throw AssemblyCatalogException.At("assembly.magazine.override", path + ".magazine");
            if (overrides.Length == 1) size = overrides[0];
            int magazine = AssemblyValidation.Integer(size, path + ".magazine", true);
            float floor = Math.Max(.05f, balance.Combat.MinimumActionInterval);
            float interval = AssemblyValidation.Positive(Fold(core, parts, AssemblyStat.Interval), path + ".interval");
            float reload = AssemblyValidation.Positive(Fold(core, parts, AssemblyStat.ReloadSeconds), path + ".reload");
            float damage = AssemblyValidation.NonNegative(Fold(core, parts, AssemblyStat.Damage), path + ".damage");
            float range = AssemblyValidation.NonNegative(Fold(core, parts, AssemblyStat.Range), path + ".range");
            float minimum = AssemblyValidation.NonNegative(Fold(core, parts, AssemblyStat.MinimumRange), path + ".minimumRange");
            if (family == AssemblyWeaponFamily.Mortar) minimum = Math.Max(6, minimum);
            if (minimum > 0 && range <= minimum) throw AssemblyCatalogException.At("assembly.range.minimum", path + ".range");
            int targetLimit = AssemblyValidation.Integer(Fold(core, parts, AssemblyStat.TargetLimit), path + ".targets");
            int cap = family == AssemblyWeaponFamily.Piercer ? 7 : family == AssemblyWeaponFamily.Fan ? 8 : family == AssemblyWeaponFamily.Ricochet ? 4 : 1;
            targetLimit = family == AssemblyWeaponFamily.Mortar || family == AssemblyWeaponFamily.Blotter ? 0 : Math.Min(cap, targetLimit);
            float acquisition = AssemblyValidation.NonNegative(Fold(null, parts.Concat(foundation), AssemblyStat.AcquisitionSeconds, balance.Construction.AcquisitionSeconds), path + ".acquisition");
            var w = new ResolvedWeapon(slot, core.Id, family, Attack(family),
                family == AssemblyWeaponFamily.Blotter ? 0 : AssemblyValidation.NonNegative(damage * (double)material.DamageMultiplier, path + ".damage"),
                range, minimum, interval, magazine, reload, acquisition, targetLimit,
                AssemblyValidation.NonNegative(Fold(core, parts, AssemblyStat.Radius), path + ".radius"),
                AssemblyValidation.NonNegative(Fold(core, parts, AssemblyStat.FlightSeconds), path + ".flight"),
                Math.Min(family == AssemblyWeaponFamily.Ricochet ? 3 : float.MaxValue, AssemblyValidation.NonNegative(Fold(core, parts, AssemblyStat.JumpRange), path + ".jump")),
                AssemblyValidation.NonNegative(Fold(core, parts, AssemblyStat.ConeDegrees), path + ".cone"), Slow(parts),
                AssemblyValidation.NonNegative(Fold(core, parts, AssemblyStat.SlowSeconds), path + ".slowSeconds"), floor);
            _ = w.CycleSeconds; _ = w.NominalDps;
            return w;
        }
        static ResolvedEntry ResolveStructure(AssemblyRecipe recipe, ComponentCatalog catalog, GameBalance balance, MaterialTier material)
        {
            var s = recipe.Structure; var parts = Parts(s.Parts, catalog); var site = parts[0]; var function = parts[1]; var m = balance.Material(material);
            int capacity = AssemblyValidation.Integer(Fold(site, parts, AssemblyStat.Capacity), "structure.capacity");
            int load = AssemblyValidation.Integer(Fold(null, parts, AssemblyStat.Load), "structure.load");
            if (load > capacity) throw AssemblyCatalogException.At("assembly.load.exceeded", "structure.load");
            float rate = AssemblyValidation.NonNegative(Fold(function, parts, AssemblyStat.SupportRate), "structure.rate");
            float radius = AssemblyValidation.NonNegative(Fold(function, parts, AssemblyStat.Range), "structure.radius");
            var support = new ResolvedSupport(s.Family, radius, s.Family == AssemblyStructureFamily.Repair ? rate : 0, Slow(parts),
                s.Family == AssemblyStructureFamily.Slow ? AssemblyValidation.NonNegative(Fold(function, parts, AssemblyStat.SlowSeconds, .6f), "structure.slowSeconds") : 0,
                s.Family == AssemblyStructureFamily.Generator ? AssemblyValidation.Integer(rate, "structure.income", true) : 0, m.RefundFraction);
            return new ResolvedEntry(AlbumEntryKind.Structure, material, Snapshot(recipe, catalog, parts), Cost(parts, m.CostMultiplier),
                AssemblyValidation.Positive(Fold(site, parts, AssemblyStat.Health) * m.HealthMultiplier, "structure.health"), load, capacity,
                AssemblyValidation.NonNegative(Fold(null, parts, AssemblyStat.BuildSeconds) * m.BuildTimeMultiplier, "structure.build"),
                Math.Min(.6f, AssemblyValidation.NonNegative(Fold(null, parts, AssemblyStat.Defense), "structure.defense")), 0, 0, 1,
                support.RefundFraction, Array.Empty<ResolvedWeapon>(), support);
        }
        static ComponentDefinition[] Parts(IEnumerable<PartRef> parts, ComponentCatalog catalog)
            => parts.Select(p => { catalog.TryGet(p.Id, p.Revision, out var d); return d; }).ToArray();
        static RecipeSnapshot Snapshot(AssemblyRecipe recipe, ComponentCatalog catalog, IEnumerable<ComponentDefinition> parts)
            => new RecipeSnapshot(recipe, catalog.BalanceRevision, parts.GroupBy(d => (d.Id, d.Revision)).Select(g => g.First()));
        static double Fold(ComponentDefinition primary, IEnumerable<ComponentDefinition> parts, AssemblyStat stat, double fallback = 0)
        {
            var owned = parts.OrderBy(d => d.Id, StringComparer.Ordinal).ThenBy(d => d.Revision).ToArray();
            double initial = primary?.Get(stat, ModifierOperation.Set, (float)fallback) ?? fallback;
            return (initial + Add(owned, stat)) * Product(owned, stat);
        }
        static double Add(IEnumerable<ComponentDefinition> parts, AssemblyStat stat)
        { double sum = 0; foreach (var p in parts) sum += p.Get(stat, ModifierOperation.Add); return sum; }
        static double Product(IEnumerable<ComponentDefinition> parts, AssemblyStat stat)
        { double product = 1; foreach (var p in parts) product *= p.Get(stat, ModifierOperation.Multiply, 1); return product; }
        static int Cost(IEnumerable<ComponentDefinition> parts, float material)
        {
            decimal sum = 0, product = 1;
            foreach (var p in parts.OrderBy(d => d.Id, StringComparer.Ordinal).ThenBy(d => d.Revision))
            { sum += (decimal)p.Get(AssemblyStat.Cost, ModifierOperation.Add); product *= (decimal)p.Get(AssemblyStat.Cost, ModifierOperation.Multiply, 1); }
            decimal cost = decimal.Ceiling(sum * product * (decimal)material);
            if (cost < 0 || cost > int.MaxValue) throw AssemblyCatalogException.At("assembly.number.range", "entry.cost");
            return (int)cost;
        }
        static float Slow(IEnumerable<ComponentDefinition> parts)
        {
            float slow = 1;
            foreach (var p in parts) slow = Math.Min(slow, AssemblyValidation.Fraction(p.Get(AssemblyStat.Slow, ModifierOperation.Set, 1), p.Id + ".slow"));
            return Math.Max(.4f, slow);
        }
        static AttackKind Attack(AssemblyWeaponFamily family)
        {
            switch (family)
            {
                case AssemblyWeaponFamily.Gun: case AssemblyWeaponFamily.Stapler: return AttackKind.Single;
                case AssemblyWeaponFamily.Piercer: return AttackKind.Line;
                case AssemblyWeaponFamily.Mortar: return AttackKind.AreaProjectile;
                case AssemblyWeaponFamily.Fan: return AttackKind.Cone;
                case AssemblyWeaponFamily.Ricochet: return AttackKind.Ricochet;
                case AssemblyWeaponFamily.Blotter: return AttackKind.Cleanup;
                default: throw AssemblyCatalogException.At("assembly.weapon.family", "weapon.family");
            }
        }
    }
}
