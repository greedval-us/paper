using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Application
{
    [CreateAssetMenu(menuName = "Paper Defense/Assembly Catalog", fileName = "AssemblyCatalog")]
    public sealed class AssemblyCatalogAsset : ScriptableObject
    {
        public AssemblyCatalogInput catalog = new AssemblyCatalogInput();
        public ComponentCatalog CreateSnapshot() => catalog.CreateSnapshot();
    }
}
