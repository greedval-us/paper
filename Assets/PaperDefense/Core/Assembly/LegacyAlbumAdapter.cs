using System;

namespace PaperDefense.Core
{
    /// <summary>Captures the old sequential modifier path. Never converts legacy geometry to new parts.</summary>
    public static class LegacyAlbumAdapter
    {
        public static AssemblyResolution Resolve(AlbumEntry entry, GameBalance balance, MaterialTier material = MaterialTier.Pencil)
        {
            if (entry == null || entry.Kind != AlbumEntryKind.Legacy || entry.LegacyBlueprint == null)
                return AssemblyResolution.Failure(new[] { new AssemblyDiagnostic("assembly.legacy.required", "entry") });
            try
            {
                AssemblyValidation.ValidateBalance(balance, material);
                // The legacy validator examines every material, not only the selected one.
                if (balance.MaterialDefinitions.Count != Enum.GetValues(typeof(MaterialTier)).Length)
                    throw AssemblyCatalogException.At("assembly.material.unknown", "materials");
                for (int i = 0; i < balance.MaterialDefinitions.Count; i++)
                    AssemblyValidation.ValidateBalance(balance, (MaterialTier)i);
                if (balance.Effects == null) throw AssemblyCatalogException.At("assembly.balance.missing", "balance.effects");
                AssemblyValidation.Fraction(balance.Effects.MaximumDefense, "balance.maximumDefense");
                if (balance.Effects.MaximumDefense >= 1) throw AssemblyCatalogException.At("assembly.number.range", "balance.maximumDefense");
                if (!TowerCatalog.Validate(entry.LegacyBlueprint, balance, out var reason))
                    return AssemblyResolution.Failure(new[] { new AssemblyDiagnostic("assembly.legacy.invalid", "entry", reason) });
                var result = TowerCatalog.Resolve(entry.LegacyBlueprint, balance).CreateLegacySnapshot(balance, material);
                _ = result.EffectiveHealth; _ = result.Dps;
                return AssemblyResolution.Success(result);
            }
            catch (AssemblyCatalogException ex) { return AssemblyResolution.Failure(ex.Diagnostics); }
            catch (ArgumentException) { return AssemblyResolution.Failure(new[] { new AssemblyDiagnostic("assembly.legacy.invalid", "entry") }); }
        }
    }
}
