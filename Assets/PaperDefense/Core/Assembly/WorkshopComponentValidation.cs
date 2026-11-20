using System;
using System.Collections.Generic;
using System.Linq;

namespace PaperDefense.Core
{
    /// <summary>Validates a reusable component without choosing the other half of a tower for it.</summary>
    public static class WorkshopComponentValidation
    {
        public static IReadOnlyList<AssemblyDiagnostic> Validate(ComponentRecipeSnapshot component, ComponentCatalog catalog,
            GameBalance balance, AssemblyFitCatalog fits)
        {
            var issues = new List<AssemblyDiagnostic>();
            if (component == null) issues.Add(new AssemblyDiagnostic("profile.library.missing", "component"));
            if (catalog == null) issues.Add(new AssemblyDiagnostic("assembly.catalog.missing", "catalog"));
            if (component == null || catalog == null) return issues.AsReadOnly();
            var references = (component.Foundation?.Parts ?? component.Weapon.Parts).ToArray();
            var parts = new List<ComponentDefinition>();
            foreach (var reference in references)
            {
                if (!catalog.TryGet(reference.Id, reference.Revision, out var definition))
                    issues.Add(new AssemblyDiagnostic("assembly.part.missing", reference.Id, reference.Revision.ToString()));
                else parts.Add(definition);
            }
            if (issues.Count != 0) return issues.AsReadOnly();
            try
            {
                // Construction verifies categories, family and required parts using exact catalog revisions.
                if (component.Foundation != null) _ = new ComponentRecipeSnapshot(component.Foundation, catalog.BalanceRevision, parts);
                else _ = new ComponentRecipeSnapshot(component.Weapon, catalog.BalanceRevision, parts);
                foreach (var definition in parts)
                foreach (var modifier in definition.Modifiers)
                {
                    if (modifier.Operation == ModifierOperation.Multiply && modifier.Value <= 0)
                        issues.Add(new AssemblyDiagnostic("assembly.number.range", definition.Id + "." + modifier.Stat));
                    if (modifier.Stat == AssemblyStat.Slow && modifier.Operation != ModifierOperation.Set)
                        issues.Add(new AssemblyDiagnostic("assembly.modifier.operation", definition.Id + ".Slow"));
                }
                foreach (MaterialTier material in Enum.GetValues(typeof(MaterialTier)))
                {
                    AssemblyValidation.ValidateBalance(balance, material);
                    if (component.Foundation != null) ValidateFoundation(parts.ToArray(), balance.Material(material));
                    else
                    {
                        AssemblyValidation.Integer(AssemblyResolver.Fold(null, parts, AssemblyStat.Load), "weapon.load");
                        AssemblyValidation.NonNegative(AssemblyResolver.Fold(parts[0], parts, AssemblyStat.BuildSeconds), "weapon.build");
                        _ = AssemblyResolver.Cost(parts, balance.Material(material).CostMultiplier);
                        var weapon = AssemblyResolver.ResolveWeapon(0, parts.ToArray(), Array.Empty<ComponentDefinition>(), balance, balance.Material(material));
                        _ = weapon.DpsAtFeed(1);
                    }
                }
            }
            catch (AssemblyCatalogException error) { issues.AddRange(error.Diagnostics); }
            catch (OverflowException) { issues.Add(new AssemblyDiagnostic("assembly.number.nonfinite", "component")); }
            catch (ArgumentException error) { issues.Add(new AssemblyDiagnostic("assembly.invalid", "component", error.Message)); }
            ValidateFit(component, references, parts, fits, issues);
            return issues.AsReadOnly();
        }

        static void ValidateFoundation(ComponentDefinition[] parts, MaterialDefinition material)
        {
            var frame = parts[0];
            int capacity = AssemblyValidation.Integer(AssemblyResolver.Fold(frame, parts, AssemblyStat.Capacity), "foundation.capacity");
            int load = AssemblyValidation.Integer(AssemblyResolver.Fold(null, parts, AssemblyStat.Load), "foundation.load");
            if (load > capacity) throw AssemblyCatalogException.At("assembly.load.exceeded", "foundation.load", load.ToString(), capacity.ToString());
            int mounts = AssemblyValidation.Integer(frame.Get(AssemblyStat.MountCount, ModifierOperation.Set), "foundation.mounts", true);
            if (mounts > TowerBlueprint.MaximumWeaponMounts) throw AssemblyCatalogException.At("assembly.recipe.mount_count", "foundation.mounts");
            double health = AssemblyValidation.Positive(AssemblyResolver.Fold(frame, parts, AssemblyStat.Health), "foundation.health");
            health = AssemblyValidation.Positive(health * material.HealthMultiplier, "foundation.health");
            float defense = Math.Min(AssemblyBalanceLimits.MaximumDefense,
                AssemblyValidation.NonNegative(AssemblyResolver.Fold(null, parts, AssemblyStat.Defense), "foundation.defense"));
            _ = AssemblyValidation.Positive(health / (1 - defense), "foundation.effectiveHealth");
            _ = AssemblyValidation.NonNegative(AssemblyResolver.Fold(null, parts, AssemblyStat.FrontDefense), "foundation.frontDefense");
            double feed = frame.Get(AssemblyStat.Feed, ModifierOperation.Set) + parts.Sum(p => (double)p.Get(AssemblyStat.Feed, ModifierOperation.Add));
            _ = AssemblyValidation.Positive(feed, "foundation.feed");
            double feedModifier = 1;
            foreach (var part in parts) feedModifier *= part.Get(AssemblyStat.Feed, ModifierOperation.Multiply, 1);
            _ = AssemblyValidation.Positive(feedModifier, "foundation.feedModifier");
            _ = AssemblyValidation.NonNegative(AssemblyResolver.Fold(null, parts, AssemblyStat.AcquisitionSeconds), "foundation.acquisition");
            // A light hull may reduce the final build duration. It is a valid adjustment on its own.
            _ = AssemblyValidation.Finite(AssemblyResolver.Fold(frame, parts, AssemblyStat.BuildSeconds) * material.BuildTimeMultiplier, "foundation.build");
            _ = AssemblyResolver.Cost(parts, material.CostMultiplier);
        }

        static void ValidateFit(ComponentRecipeSnapshot component, PartRef[] references, List<ComponentDefinition> definitions,
            AssemblyFitCatalog fits, List<AssemblyDiagnostic> issues)
        {
            if (fits == null) { issues.Add(new AssemblyDiagnostic("assembly.fit.catalog", "fits")); return; }
            bool covered = true;
            for (int index = 0; index < references.Length; index++)
            {
                var reference = references[index];
                if (!fits.TryGetPart(reference.Id, reference.Revision, out var part))
                {
                    issues.Add(new AssemblyDiagnostic(fits.HasPartId(reference.Id) ? "assembly.fit.revision" : "assembly.fit.part", reference.Id, reference.Revision.ToString()));
                    covered = false;
                }
                else if (part.Category != definitions[index].Category)
                { issues.Add(new AssemblyDiagnostic("assembly.fit.coverage", reference.Id)); covered = false; }
                else if (part.AttachType != part.ExpectedSocketType)
                { issues.Add(new AssemblyDiagnostic("assembly.fit.socket_type", part.Id, part.AttachType, part.ExpectedSocketType)); covered = false; }
            }
            if (!covered) return;
            if (component.Foundation != null)
            {
                var selected = component.Foundation.Frame;
                if (!fits.TryGetFrame(selected.Id, selected.Revision, out var frame))
                    issues.Add(new AssemblyDiagnostic("assembly.fit.coverage", "foundation.frame"));
                else
                {
                    int count = (int)Math.Max(0, Math.Min(TowerBlueprint.MaximumWeaponMounts,
                        definitions[0].Get(AssemblyStat.MountCount, ModifierOperation.Set)));
                    for (int slot = 0; slot < count; slot++)
                        if (!frame.Mounts.Any(m => m.SlotIndex == slot)) issues.Add(new AssemblyDiagnostic("assembly.fit.socket", "foundation.frame", slot.ToString()));
                }
            }
            else
            {
                var selected = component.Weapon.Mechanism;
                if (!fits.TryGetWeapon(selected.Id, selected.Revision, out var weapon) ||
                    !AssemblyFitRules.Covers(references, weapon.RequiredParts, weapon.CompatibleParts, fits))
                    issues.Add(new AssemblyDiagnostic("assembly.fit.coverage", "weapon"));
                else if (component.Weapon.Mount.HasValue)
                {
                    var mount = component.Weapon.Mount.Value;
                    fits.TryGetPart(mount.Id, mount.Revision, out var part);
                    if (part.AttachType != weapon.MountType)
                        issues.Add(new AssemblyDiagnostic("assembly.fit.socket_type", mount.Id, part.AttachType, weapon.MountType));
                }
            }
        }
    }
}
