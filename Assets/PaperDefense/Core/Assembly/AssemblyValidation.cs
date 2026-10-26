using System;
using System.Collections.Generic;

namespace PaperDefense.Core
{
    /// <summary>Numerical/semantic validation only. Physical fit is the separate fit-catalog stage.</summary>
    public static class AssemblyValidation
    {
        public static IReadOnlyList<AssemblyDiagnostic> Validate(AssemblyRecipe recipe, ComponentCatalog catalog, GameBalance balance, MaterialTier material)
        {
            var issues = new List<AssemblyDiagnostic>();
            if (recipe == null) issues.Add(new AssemblyDiagnostic("assembly.recipe.missing", "recipe"));
            if (catalog == null) issues.Add(new AssemblyDiagnostic("assembly.catalog.missing", "catalog"));
            try { ValidateBalance(balance, material); }
            catch (AssemblyCatalogException ex) { issues.AddRange(ex.Diagnostics); }
            if (recipe == null || catalog == null) return issues.AsReadOnly();
            issues.AddRange(catalog.ValidateRecipe(recipe));
            if (issues.Count != 0) return issues.AsReadOnly();
            foreach (var part in recipe.Parts)
            {
                catalog.TryGet(part.Id, part.Revision, out var d);
                if (d.Category == ComponentCategory.Mechanism && !SingleFamily(d.WeaponFamilies))
                    issues.Add(new AssemblyDiagnostic("assembly.weapon.family", part.Id));
                foreach (var m in d.Modifiers)
                {
                    if (m.Operation == ModifierOperation.Multiply && m.Value <= 0)
                        issues.Add(new AssemblyDiagnostic("assembly.number.range", d.Id + "." + m.Stat));
                    if (m.Stat == AssemblyStat.Slow && m.Operation != ModifierOperation.Set)
                        issues.Add(new AssemblyDiagnostic("assembly.modifier.operation", d.Id + ".Slow"));
                }
            }
            if (recipe.Structure?.Family == AssemblyStructureFamily.Generator && material != MaterialTier.Pencil)
                issues.Add(new AssemblyDiagnostic("assembly.material.unsupported", "structure.material", material.ToString()));
            return issues.AsReadOnly();
        }
        static bool SingleFamily(AssemblyWeaponFamily family)
        { int value = (int)family; return value > 0 && (value & (value - 1)) == 0; }
        internal static void ValidateBalance(GameBalance balance, MaterialTier material)
        {
            if (balance == null) throw AssemblyCatalogException.At("assembly.balance.missing", "balance");
            if (!Enum.IsDefined(typeof(MaterialTier), material) || balance.MaterialDefinitions == null || (int)material >= balance.MaterialDefinitions.Count || balance.MaterialDefinitions[(int)material] == null)
                throw AssemblyCatalogException.At("assembly.material.unknown", "material");
            var m = balance.Material(material);
            NonNegative(m.CostMultiplier, "material.cost"); Positive(m.HealthMultiplier, "material.health");
            NonNegative(m.DamageMultiplier, "material.damage"); NonNegative(m.BuildTimeMultiplier, "material.build");
            Fraction(m.RefundFraction, "material.refund");
            if (balance.Construction == null || balance.Combat == null) throw AssemblyCatalogException.At("assembly.balance.missing", "balance.tuning");
            NonNegative(balance.Construction.AcquisitionSeconds, "balance.acquisition");
            NonNegative(balance.Construction.SecondaryBuildSeconds, "balance.secondaryBuild");
            Positive(balance.Combat.MinimumActionInterval, "balance.minimumInterval");
        }
        internal static float Finite(double value, string path)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value > float.MaxValue || value < -float.MaxValue)
                throw AssemblyCatalogException.At("assembly.number.nonfinite", path);
            return (float)value;
        }
        internal static float NonNegative(double value, string path)
        {
            float result = Finite(value, path);
            if (value < 0) throw AssemblyCatalogException.At("assembly.number.range", path);
            return result;
        }
        internal static float Positive(double value, string path)
        {
            float result = NonNegative(value, path);
            if (result <= 0) throw AssemblyCatalogException.At("assembly.number.range", path);
            return result;
        }
        internal static float Fraction(double value, string path)
        {
            float result = NonNegative(value, path);
            if (value > 1) throw AssemblyCatalogException.At("assembly.number.range", path);
            return result;
        }
        internal static int Integer(double value, string path, bool positive = false)
        {
            NonNegative(value, path);
            if (value != Math.Truncate(value) || value > int.MaxValue) throw AssemblyCatalogException.At("assembly.number.integer", path);
            if (positive && value == 0) throw AssemblyCatalogException.At("assembly.number.range", path);
            return (int)value;
        }
    }
}
