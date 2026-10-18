using System;
using System.Collections.Generic;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Application
{
    [CreateAssetMenu(menuName = "Paper Defense/Content Database", fileName = "ContentDatabase")]
    public sealed class ContentDatabaseAsset : ScriptableObject
    {
        public ContentCatalogInput catalog = new ContentCatalogInput();
        public BalancePresetAsset[] presets = Array.Empty<BalancePresetAsset>();
        // Authoring reference only: the player route switches after assembly dependencies are ready.
        public AssemblyCatalogAsset assemblyCatalog;
        public ContentCatalogSnapshot CreateSnapshot(ContentOverrides overrides = null)
        {
            if (catalog == null) throw new ArgumentException(UserText.Message("copy.bef2598cc49b8725", name));
            var candidate = catalog.CreateCopy();
            if (overrides != null) overrides.Apply(candidate);
            return candidate.CreateSnapshot();
        }
    }

    /// <summary>Each entry is an explicit complete replacement. Missing entry means inherit; zero/false remain values.</summary>
    [Serializable]
    public sealed class ContentOverrides
    {
        public List<ChassisDefinitionInput> Chassis = new List<ChassisDefinitionInput>();
        public List<WeaponDefinitionInput> Weapons = new List<WeaponDefinitionInput>();
        public List<ModuleDefinitionInput> Modules = new List<ModuleDefinitionInput>();
        public List<EffectDefinitionInput> Effects = new List<EffectDefinitionInput>();
        public List<DrawingTemplateDefinitionInput> Templates = new List<DrawingTemplateDefinitionInput>();
        internal void Apply(ContentCatalogInput candidate)
        {
            var owned = new ContentCatalogInput { Chassis=Chassis, Weapons=Weapons, Modules=Modules, Effects=Effects, Templates=Templates }.CreateCopy();
            Replace(candidate.Chassis, owned.Chassis, item => item.Id, "chassis overrides");
            Replace(candidate.Weapons, owned.Weapons, item => item.Id, "weapon overrides");
            Replace(candidate.Modules, owned.Modules, item => item.Id, "module overrides");
            Replace(candidate.Effects, owned.Effects, item => item.Id, "effect overrides");
            Replace(candidate.Templates, owned.Templates, item => item.Id, "template overrides");
        }
        static void Replace<T>(List<T> target, List<T> replacements, Func<T, string> id, string field) where T : class
        {
            if (target == null || replacements == null) throw new ArgumentException(UserText.Message("copy.fd573191ffd72212", field));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in replacements)
            {
                if (item == null) throw new ArgumentException(UserText.Message("copy.91407d8768e3608b", field));
                string key = id(item);
                if (!seen.Add(key ?? "")) throw new ArgumentException(UserText.Message("copy.76cf7c205a3b6947", field, key));
                int index = target.FindIndex(existing => existing != null && id(existing) == key);
                if (index < 0) throw new ArgumentException(UserText.Message("copy.b77d635e2c99fd64", field, key));
                target[index] = item;
            }
        }
    }
}
