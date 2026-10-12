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
        public ContentCatalogSnapshot CreateSnapshot(ContentOverrides overrides = null)
        {
            if (catalog == null) throw new ArgumentException(name + ".catalog: отсутствует база.");
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
            if (target == null || replacements == null) throw new ArgumentException(field + ": отсутствует список.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in replacements)
            {
                if (item == null) throw new ArgumentException(field + ": пустая запись.");
                string key = id(item);
                if (!seen.Add(key ?? "")) throw new ArgumentException(field + "." + key + ": повторный ID.");
                int index = target.FindIndex(existing => existing != null && id(existing) == key);
                if (index < 0) throw new ArgumentException(field + "." + key + ": переопределение неизвестной записи.");
                target[index] = item;
            }
        }
    }
}
