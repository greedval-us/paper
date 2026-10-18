using System;
using PaperDefense.Application;
using PaperDefense.Core;
using UnityEngine;

namespace PaperDefense.Presentation
{
    /// <summary>The four structure sockets use the same compatible catalogue and purchasing boundary.</summary>
    public sealed class StructureAssemblyPanel
    {
        readonly ComponentAssemblyPanel panel;
        public RectTransform Root => panel.Root;
        public ComponentCatalogPanel Catalog => panel.Catalog;
        public StructureAssemblyPanel(Transform parent, RunController run, WorkshopDraftSession draft,
            UserTextResolver resolver, Action changed)
        { panel = new ComponentAssemblyPanel(parent, run, draft, resolver, changed); }
        public void Show() => panel.Show();
        public void Hide() => panel.Hide();
        public void Refresh() => panel.Refresh();
        public void Reflow(float width, float height) => panel.Reflow(width, height);
    }
}
