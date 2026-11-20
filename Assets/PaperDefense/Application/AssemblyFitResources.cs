using System;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Application
{
    /// <summary>Mechanical compatibility is configuration and does not depend on an art atlas.</summary>
    public static class AssemblyFitResources
    {
        public const string ResourcePath = "Configuration/AssemblyFits";
        [Serializable] sealed class Envelope
        {
            public AssemblyFitCatalogInput fits;
            public AssemblyFitCatalogInput legacyFits;
        }

        // Return a fresh input so editor validation cannot mutate another session's rules.
        public static AssemblyFitCatalogInput ReadInput(bool legacy = false)
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new InvalidOperationException("Mechanical assembly compatibility configuration is missing.");
            var envelope = JsonUtility.FromJson<Envelope>(asset.text);
            var input = legacy ? envelope?.legacyFits : envelope?.fits;
            if (input == null) throw new InvalidOperationException("Mechanical assembly compatibility configuration is invalid.");
            return input;
        }

        public static AssemblyFitCatalog Load(bool legacy = false) => new AssemblyFitCatalog(ReadInput(legacy));
    }
}
